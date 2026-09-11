using System.Text.Json;
using Autopatch.Client.Extensions;
using Autopatch.Client.Models;
using Autopatch.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Autopatch.Client.Tests;

/// <summary>
/// Shape of the public client API and its dependencies.
/// </summary>
[TestClass]
public sealed class ClientApiTests
{
    [TestMethod]
    [TestCategory("C8")]
    public void C8_AddAutoPatch_WorksWithoutHostedServices()
    {
        var services = new ServiceCollection();

        services.AddAutoPatch(config => config.Endpoint = "http://localhost");

        services.Where(d => d.ServiceType == typeof(IHostedService)).Select(d => d.ImplementationType?.Name)
            .Should().BeEmpty("Blazor WebAssembly does not run hosted services, so the connection would never be started");
    }

    [TestMethod]
    [TestCategory("C8")]
    public void C8_ClientAssembly_DoesNotUseNewtonsoftJson()
    {
        typeof(AutoPatchClient).Assembly.GetReferencedAssemblies().Select(a => a.Name)
            .Should().NotContain("Newtonsoft.Json",
                "Microsoft.AspNetCore.JsonPatch pulls in Newtonsoft.Json and a reflection-based ObjectAdapter, which increases the WebAssembly download and causes trimming/AOT warnings");
    }

    [TestMethod]
    [TestCategory("C8")]
    public void C8_ClientPackage_DoesNotDependOnGenericHost()
    {
        var depsFile = Path.Combine(AppContext.BaseDirectory, "Autopatch.Client.Tests.deps.json");
        using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));
        var clientDependencies = deps.RootElement.GetProperty("targets").EnumerateObject().First().Value
            .EnumerateObject().Single(library => library.Name.StartsWith("AutoPatch.Client/", StringComparison.OrdinalIgnoreCase))
            .Value.GetProperty("dependencies").EnumerateObject().Select(d => d.Name);

        clientDependencies.Should().NotContain("Microsoft.Extensions.Hosting",
            "the full generic host is only needed for the hosted service, which does not run in Blazor WebAssembly anyway");
    }

    [TestMethod]
    [TestCategory("C9")]
    public void C9_ChangeTrackingMode_MatchesTheSpecification()
    {
        Enum.GetNames<ChangeTrackingMode>().Should().Equal(["Disabled", "ManualCommit", "AutoCommit"],
            "spec.md documents Disabled | ManualCommit | AutoCommit with Disabled as default, the code has Auto instead and does not use the enum at all");
    }

    [TestMethod]
    [TestCategory("C9")]
    public void C9_Client_IsAsyncDisposable()
    {
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(IAutoPatchClient))
            .Should().BeTrue("the HubConnection is never disposed");
    }
}
