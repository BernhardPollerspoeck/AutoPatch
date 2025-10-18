using Autopatch.Client.Models;

namespace Autopatch.Client.Tests;

[TestClass]
public class AutoPatchConfigurationTests
{
    [TestMethod]
    public void AutoPatchConfiguration_CanSetEndpoint()
    {
        // Arrange
        var config = new AutoPatchConfiguration();
        const string endpoint = "http://localhost:5000";

        // Act
        config.Endpoint = endpoint;

        // Assert
        config.Endpoint.Should().Be(endpoint);
        config.ServiceName.Should().BeNull();
    }

    [TestMethod]
    public void AutoPatchConfiguration_CanSetServiceName()
    {
        // Arrange
        var config = new AutoPatchConfiguration();
        const string serviceName = "my-autopatch-service";

        // Act
        config.ServiceName = serviceName;

        // Assert
        config.ServiceName.Should().Be(serviceName);
        config.Endpoint.Should().BeNull();
    }

    [TestMethod]
    public void AutoPatchConfiguration_CanSetDispatcher()
    {
        // Arrange
        var config = new AutoPatchConfiguration();
        Action<Action> dispatcher = action => action();

        // Act
        config.Dispatcher = dispatcher;

        // Assert
        config.Dispatcher.Should().Be(dispatcher);
    }

    [TestMethod]
    public void AutoPatchConfiguration_AllowsBothEndpointAndServiceNameNull()
    {
        // Arrange & Act
        var config = new AutoPatchConfiguration();

        // Assert
        config.Endpoint.Should().BeNull();
        config.ServiceName.Should().BeNull();
        config.Dispatcher.Should().BeNull();
    }
}