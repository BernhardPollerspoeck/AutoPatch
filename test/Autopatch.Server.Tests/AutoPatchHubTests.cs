using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Autopatch.Server.Extensions;
using Autopatch.Server.Models;
using Autopatch.Server.SignalR;
using Autopatch.Server.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using CollidingMonitor = Autopatch.Server.Tests.Infrastructure.Collision.Monitor;

namespace Autopatch.Server.Tests;

[TestClass]
public sealed class AutoPatchHubTests
{
    [TestMethod]
    [TestCategory("S5")]
    public async Task S5_SubscribeBeforeTheCollectionExists_ReceivesItemsAddedLater()
    {
        using var harness = new HubHarness(s => s.AddTrackedCollection<TestItem>());
        var client = harness.Client("early-client");

        (await harness.CreateHub("early-client").SubscribeToType(nameof(TestItem), "zone-1")).Should().BeTrue();
        harness.Manager.GetOrCreateCollection<TestItem>("zone-1").Add(TestItem.Create(1, "created later"));

        await Eventually.WaitUntilAsync(() => client.Items.Count == 1, TimeSpan.FromSeconds(1));
        client.IsInitialized.Should().BeTrue(
            "without a tracker SendFullData is a no-op, so the client never gets an initial set and ignores every later patch");
        client.Items.Should().HaveCount(1);
    }

    [TestMethod]
    [TestCategory("S7")]
    public async Task S7_UnknownTypeName_IsRejected()
    {
        using var harness = new HubHarness(s => s.AddTrackedCollection<TestItem>());

        var accepted = await harness.CreateHub("client").SubscribeToType("DoesNotExist");

        accepted.Should().BeFalse("a type name that cannot be resolved is treated as 'no validator' and allowed (fail-open)");
    }

    [TestMethod]
    [TestCategory("S7")]
    public async Task S7_TypeNameContainingTheKey_DoesNotBypassTheValidator()
    {
        using var harness = new HubHarness(s => s.AddTrackedCollection<SecureItem, RejectAllValidator<SecureItem>>());
        harness.Manager.GetOrCreateCollection<SecureItem>("tenant-a").Add(new SecureItem { Id = 1, Name = "confidential" });

        (await harness.CreateHub("attacker").SubscribeToType(nameof(SecureItem), "tenant-a"))
            .Should().BeFalse("control: the validator rejects the regular subscription");

        var bypassAccepted = await harness.CreateHub("attacker").SubscribeToType($"{nameof(SecureItem)}/tenant-a");
        await Task.Delay(300);

        bypassAccepted.Should().BeFalse(
            "'SecureItem/tenant-a' matches no type name, so no validator runs, yet it resolves to the same group and tracker");
        harness.Client("attacker").Items.Should().BeEmpty("the attacker receives the full data of the protected collection");
    }

    [TestMethod]
    [TestCategory("S7")]
    public async Task S7_TypeNameCollidingWithAFrameworkType_StillRunsTheValidator()
    {
        using var harness = new HubHarness(s => s.AddTrackedCollection<CollidingMonitor, RejectAllValidator<CollidingMonitor>>());
        harness.Manager.GetOrCreateCollection<CollidingMonitor>().Add(new CollidingMonitor { Id = 1, Name = "confidential" });

        var accepted = await harness.CreateHub("attacker").SubscribeToType(typeof(CollidingMonitor).Name);

        accepted.Should().BeFalse(
            "the lookup takes the first type named 'Monitor' in any loaded assembly (System.Threading.Monitor), finds no validator for it and allows the subscription");
    }

    [TestMethod]
    [TestCategory("S7")]
    [DoNotParallelize]
    public async Task S7_AssemblyWithUnloadableTypes_DoesNotBreakSubscriptions()
    {
        var (accepted, failure, context) = await SubscribeWhileABrokenAssemblyIsLoadedAsync();
        for (var i = 0; i < 50 && context.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        failure.Should().BeNull(
            "AppDomain.GetAssemblies().SelectMany(a => a.GetTypes()) throws ReflectionTypeLoadException for any loaded assembly with an unresolvable dependency");
        accepted.Should().BeTrue();
    }

    /// <remarks>
    /// Only strings leave this method, so the broken assembly can be unloaded before other tests scan the AppDomain.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(bool? Accepted, string? Failure, WeakReference Context)> SubscribeWhileABrokenAssemblyIsLoadedAsync()
    {
        var context = new AssemblyLoadContext("autopatch-broken-types", isCollectible: true);
        var weakContext = new WeakReference(context);
        try
        {
            context.LoadFromStream(new MemoryStream(CreateAssemblyWithMissingDependency()));

            // The tracked type lives in an assembly that is loaded after the broken one, like a plugin or a lazily loaded
            // model assembly, so the type lookup has to scan past the broken assembly to find it.
            var lateItemType = EmitItemTypeInNewAssembly();
            using var harness = new HubHarness(s => RegisterWithValidator(s, lateItemType));
            try
            {
                return (await harness.CreateHub("client").SubscribeToType(lateItemType.Name), null, weakContext);
            }
            catch (Exception ex)
            {
                return (null, $"{ex.GetType().Name}: {ex.Message}", weakContext);
            }
        }
        finally
        {
            context.Unload();
        }
    }

    [TestMethod]
    [TestCategory("S7")]
    [DoNotParallelize]
    public async Task S7_ValidatorLookup_DoesNotScanAllAssembliesOnEverySubscribe()
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == BrokenAssemblyName))
        {
            Assert.Inconclusive("The broken assembly of another S7 test is still loaded.");
        }

        // Models usually live in their own assembly that is loaded after the framework assemblies.
        var modelType = EmitItemTypeInNewAssembly();
        using var harness = new HubHarness(s => RegisterWithValidator(s, modelType));
        (await harness.CreateHub("warmup").SubscribeToType(modelType.Name)).Should().BeTrue();

        const int subscriptions = 500;
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < subscriptions; i++)
        {
            await harness.CreateHub($"client-{i}").SubscribeToType(modelType.Name);
        }
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(250),
            "each subscribe enumerates every type of every loaded assembly to find the validator");
    }

    [TestMethod]
    [TestCategory("S14")]
    public void S14_ClientChangePolicy_IsEitherEffectiveOrMarkedObsolete()
    {
        var hasEntryPoint = typeof(AutoPatchHub).GetMethod("SubmitClientChange", BindingFlags.Instance | BindingFlags.Public) is not null;
        var isObsolete = typeof(ObjectTypeConfiguration<>).GetProperty("ClientChangePolicy")?.GetCustomAttribute<ObsoleteAttribute>() is not null;

        (hasEntryPoint || isObsolete).Should().BeTrue(
            "ClientChangePolicy can be configured, but the hub has no method for client changes, so the setting has no effect");
    }

    private const string BrokenAssemblyName = "AutoPatch.Tests.Broken";

    /// <summary>Calls <c>AddTrackedCollection&lt;itemType, AllowAllValidator&lt;itemType&gt;&gt;()</c> for a type only known at runtime.</summary>
    private static void RegisterWithValidator(IServiceCollection services, Type itemType)
        => typeof(IServiceCollectionExtensions).GetMethods()
            .Single(m => m.Name == nameof(IServiceCollectionExtensions.AddTrackedCollection) && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(itemType, typeof(AllowAllValidator<>).MakeGenericType(itemType))
            .Invoke(null, [services, null]);

    /// <summary>
    /// Emits an assembly whose only type derives from a type in an assembly that does not exist.
    /// </summary>
    private static byte[] CreateAssemblyWithMissingDependency()
    {
        var missing = new PersistedAssemblyBuilder(new AssemblyName("AutoPatch.Tests.Missing"), typeof(object).Assembly);
        var missingBase = missing.DefineDynamicModule("AutoPatch.Tests.Missing")
            .DefineType("MissingBase", TypeAttributes.Public | TypeAttributes.Class);
        missingBase.DefineDefaultConstructor(MethodAttributes.Public);
        missingBase.CreateType();

        var broken = new PersistedAssemblyBuilder(new AssemblyName(BrokenAssemblyName), typeof(object).Assembly);
        var derived = broken.DefineDynamicModule(BrokenAssemblyName)
            .DefineType("DerivedFromMissing", TypeAttributes.Public | TypeAttributes.Class, missingBase);
        derived.CreateType();

        using var stream = new MemoryStream();
        broken.Save(stream);
        return stream.ToArray();
    }

    private static Type EmitItemTypeInNewAssembly()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"AutoPatch.Tests.LateLoaded{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("AutoPatch.Tests.LateLoaded")
            .DefineType("LateLoadedItem", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, typeof(ObservableModel));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        return type.CreateType();
    }
}
