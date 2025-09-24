using Autopatch.Client.Extensions;
using Autopatch.Client.Models;
using Autopatch.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ServiceDiscovery;

namespace Autopatch.Client.Tests;

[TestClass]
public class AspireIntegrationTests
{
    [TestMethod]
    public void AddAutoPatchWithServiceDiscovery_WithServiceName_ConfiguresCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        const string serviceName = "test-autopatch";

        // Act
        services.AddAutoPatchWithServiceDiscovery(serviceName);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        
        // Verify service discovery is registered
        serviceProvider.GetService<ServiceEndpointResolver>().Should().NotBeNull();
        
        // Verify AutoPatch client is registered
        serviceProvider.GetService<IAutoPatchClient>().Should().NotBeNull();
        
        // Verify configuration is set correctly
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AutoPatchConfiguration>>();
        options.Value.ServiceName.Should().Be(serviceName);
        options.Value.Endpoint.Should().BeNull();
    }

    [TestMethod]
    public void AddAutoPatchWithServiceDiscovery_WithDefaultServiceName_UsesDefaultName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAutoPatchWithServiceDiscovery();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AutoPatchConfiguration>>();
        options.Value.ServiceName.Should().Be("autopatch");
    }

    [TestMethod]
    public void AddAutoPatchWithServiceDiscovery_WithAdditionalConfiguration_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        Action<Action> testDispatcher = action => action();

        // Act
        services.AddAutoPatchWithServiceDiscovery("test-service", config =>
        {
            config.Dispatcher = testDispatcher;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AutoPatchConfiguration>>();
        options.Value.ServiceName.Should().Be("test-service");
        options.Value.Dispatcher.Should().Be(testDispatcher);
    }
}