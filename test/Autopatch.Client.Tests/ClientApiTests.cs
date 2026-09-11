using Autopatch.Client.Extensions;
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
    [TestCategory("C9")]
    public void C9_Client_IsAsyncDisposable()
    {
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(IAutoPatchClient))
            .Should().BeTrue("the HubConnection is never disposed");
    }
}
