using Autopatch.Client.Models;
using Autopatch.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.ServiceDiscovery;

namespace Autopatch.Client.Tests;

[TestClass]
public class AutoPatchClientAspireTests
{
    [TestMethod]
    public async Task ResolveEndpointAsync_WithDirectEndpoint_ReturnsEndpoint()
    {
        // Arrange
        const string expectedEndpoint = "http://localhost:5000";
        var config = new AutoPatchConfiguration { Endpoint = expectedEndpoint };
        var options = Options.Create(config);
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        
        var client = new AutoPatchClient(options, serviceProvider);
        
        // Use reflection to access the private method
        var method = typeof(AutoPatchClient).GetMethod("ResolveEndpointAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var result = await (Task<string>)method!.Invoke(client, [CancellationToken.None])!;
        
        // Assert
        result.Should().Be(expectedEndpoint);
    }

    [TestMethod]
    public async Task ResolveEndpointAsync_WithBothEndpointAndServiceName_ThrowsException()
    {
        // Arrange
        var config = new AutoPatchConfiguration 
        { 
            Endpoint = "http://localhost:5000",
            ServiceName = "test-service"
        };
        var options = Options.Create(config);
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        
        var client = new AutoPatchClient(options, serviceProvider);
        
        // Use reflection to access the private method
        var method = typeof(AutoPatchClient).GetMethod("ResolveEndpointAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
        {
            await (Task<string>)method!.Invoke(client, [CancellationToken.None])!;
        });
        
        exception.Message.Should().Contain("mutually exclusive");
    }

    [TestMethod]
    public async Task ResolveEndpointAsync_WithNeitherEndpointNorServiceName_ThrowsException()
    {
        // Arrange
        var config = new AutoPatchConfiguration(); // Both null
        var options = Options.Create(config);
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        
        var client = new AutoPatchClient(options, serviceProvider);
        
        // Use reflection to access the private method
        var method = typeof(AutoPatchClient).GetMethod("ResolveEndpointAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
        {
            await (Task<string>)method!.Invoke(client, [CancellationToken.None])!;
        });
        
        exception.Message.Should().Contain("Either Endpoint or ServiceName must be configured");
    }

    [TestMethod]
    public async Task ResolveEndpointAsync_WithServiceNameButNoServiceDiscovery_ThrowsException()
    {
        // Arrange
        var config = new AutoPatchConfiguration { ServiceName = "test-service" };
        var options = Options.Create(config);
        var serviceProvider = new ServiceCollection().BuildServiceProvider(); // No ServiceEndpointResolver
        
        var client = new AutoPatchClient(options, serviceProvider);
        
        // Use reflection to access the private method
        var method = typeof(AutoPatchClient).GetMethod("ResolveEndpointAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
        {
            await (Task<string>)method!.Invoke(client, [CancellationToken.None])!;
        });
        
        exception.Message.Should().Contain("Service discovery is not configured");
    }
}