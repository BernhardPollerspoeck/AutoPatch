using System.Diagnostics;
using System.Reflection;
using Autopatch.Client.Services;
using Autopatch.Client.Tests.Infrastructure;
using Microsoft.AspNetCore.JsonPatch.Adapters;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Newtonsoft.Json.Serialization;

namespace Autopatch.Client.Tests;

/// <summary>
/// How <see cref="AutoPatchClient"/> applies batches it receives from the server.
/// </summary>
[TestClass]
public sealed class PatchApplicationTests
{
    [TestMethod]
    [TestCategory("C2")]
    public void C2_SecondInitialSet_ReplacesTheCollectionInsteadOfAppending()
    {
        using var harness = new ClientPatchHarness<Order>();
        harness.DeliverInitialSet(Order.Create(1, "a"), Order.Create(2, "b"));

        harness.DeliverInitialSet(Order.Create(1, "a"), Order.Create(2, "b"));

        harness.Collection.Select(o => o.Name).Should().Equal(["a", "b"],
            "full data is applied as a series of 'add' operations on top of the existing items, so a resubscribe duplicates everything");
    }

    [TestMethod]
    [TestCategory("C5")]
    public void C5_ReplacingAComplexProperty_KeepsItsValues()
    {
        using var harness = new ClientPatchHarness<Order>();
        harness.DeliverInitialSet(Order.Create(1, "a"));

        harness.DeliverPatch(ClientPatchHarness<Order>.Replace("/0/Address", new Address { Street = "New Street 2" }));

        harness.Collection[0].Address!.Street.Should().Be("New Street 2",
            "the nested object arrives in camelCase and is deserialized without the case-insensitive options");
    }

    /// <remarks>
    /// Review finding C6 does not reproduce: System.Text.Json deserializes JSON null into the <c>object value</c> of an
    /// <see cref="Operation"/> as <see langword="null"/>, not as a <see cref="System.Text.Json.JsonElement"/>, so the
    /// GetInt32()/GetDouble()/... branch is never reached. Kept as a regression test.
    /// </remarks>
    [TestMethod]
    [TestCategory("C6")]
    [DataRow(nameof(Order.NullableInt))]
    [DataRow(nameof(Order.NullableDouble))]
    [DataRow(nameof(Order.NullableBool))]
    [DataRow(nameof(Order.NullableDateTime))]
    [DataRow(nameof(Order.NullableDecimal))]
    public void C6_SettingANullablePrimitiveToNull_Works(string propertyName)
    {
        using var harness = new ClientPatchHarness<Order>();
        harness.DeliverInitialSet(Order.Create(1, "a"));

        var act = () => harness.DeliverPatch(ClientPatchHarness<Order>.Replace($"/0/{propertyName}", null));

        act.Should().NotThrow("GetInt32() and friends throw for JSON null");
        typeof(Order).GetProperty(propertyName)!.GetValue(harness.Collection[0]).Should().BeNull();
    }

    [TestMethod]
    [TestCategory("C7")]
    public void C7_FailingOperation_DoesNotLeaveTheBatchHalfApplied()
    {
        using var harness = new ClientPatchHarness<Order>();
        harness.DeliverInitialSet(Order.Create(1, "a"));

        try
        {
            harness.DeliverPatch(
                ClientPatchHarness<Order>.Replace("/0/Name", "changed"),
                ClientPatchHarness<Order>.Replace("/-1/Name", "invalid path, e.g. produced by S2"));
        }
        catch (Exception)
        {
            // Whether the error surfaces is checked by C7_Client_ReportsPatchErrors.
        }

        harness.Collection[0].Name.Should().Be("a",
            "operations are applied one by one without a transaction, so the first half of the batch stays applied and the state silently drifts");
    }

    [TestMethod]
    [TestCategory("C7")]
    public void C7_Client_ReportsPatchErrors()
    {
        typeof(IAutoPatchClient).GetEvents().Select(e => e.Name)
            .Should().Contain(name => name.Contains("Error"),
                "a failed batch is neither caught nor reported, so the application cannot trigger a resync");
    }

    [TestMethod]
    [TestCategory("C9")]
    public void C9_ReplacingAWholeItem_Works()
    {
        using var harness = new ClientPatchHarness<Order>();
        harness.DeliverInitialSet(Order.Create(1, "a"));

        var act = () => harness.DeliverPatch(ClientPatchHarness<Order>.Replace("/0", Order.Create(1, "replaced")));

        act.Should().NotThrow("only paths of depth 3 (/i/Prop) are converted to the target type; '/i' keeps the raw JsonElement");
        harness.Collection[0].Name.Should().Be("replaced",
            "only paths of depth 3 (/i/Prop) are converted to the target type; for '/i' the raw JsonElement is handed to the adapter, which produces an empty item");
    }

    [TestMethod]
    [TestCategory("C9")]
    [DoNotParallelize]
    public void C9_ApplyingBatches_ReusesTheContractResolver()
    {
        const int batches = 3_000;
        using var harness = new ClientPatchHarness<Order>();
        harness.DeliverInitialSet(Order.Create(1, "a"));
        var cachedTarget = new List<Order> { Order.Create(1, "a") };
        var cachedAdapter = new ObjectAdapter(new DefaultContractResolver(), null, new AdapterFactory());

        for (var i = 0; i < 200; i++)
        {
            harness.DeliverPatch(ClientPatchHarness<Order>.Replace("/0/Name", $"warmup-{i}"));
            ClientPatchHarness<Order>.Replace("/0/Name", $"warmup-{i}").Apply(cachedTarget, cachedAdapter);
        }

        var client = Stopwatch.StartNew();
        for (var i = 0; i < batches; i++)
        {
            harness.DeliverPatch(ClientPatchHarness<Order>.Replace("/0/Name", $"value-{i}"));
        }
        client.Stop();

        var baseline = Stopwatch.StartNew();
        for (var i = 0; i < batches; i++)
        {
            // Same serialization round trip as the client path, then a cached adapter.
            var operation = ClientPatchHarness<Order>.Replace("/0/Name", $"value-{i}");
            harness.RoundTripOnly(operation);
            operation.Apply(cachedTarget, cachedAdapter);
        }
        baseline.Stop();

        (client.Elapsed.TotalMilliseconds / baseline.Elapsed.TotalMilliseconds).Should().BeLessThan(3,
            "a new DefaultContractResolver and ObjectAdapter per batch throws away Newtonsoft's contract cache (client {0:N0} ms, cached {1:N0} ms)",
            client.Elapsed.TotalMilliseconds, baseline.Elapsed.TotalMilliseconds);
    }

    [TestMethod]
    [TestCategory("C9")]
    public void C9_SubscriptionState_IsThreadSafe()
    {
        var field = typeof(AutoPatchClient).GetField("_subscriptions", BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull();
        field!.FieldType.GetGenericTypeDefinition().Should().NotBe(typeof(Dictionary<,>),
            "subscriptions are read on the SignalR receive thread and written on the caller thread");
    }
}
