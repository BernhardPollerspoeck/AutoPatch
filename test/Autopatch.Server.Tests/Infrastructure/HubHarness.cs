using System.Security.Claims;
using Autopatch.Server.Extensions;
using Autopatch.Server.Services;
using Autopatch.Server.SignalR;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autopatch.Server.Tests.Infrastructure;

/// <summary>
/// The server side of AutoPatch (DI registrations, <see cref="ITrackedCollectionManager"/>, <see cref="AutoPatchHub"/>)
/// with a <see cref="RecordingHubContext"/> instead of a real SignalR transport.
/// </summary>
public sealed class HubHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public HubHarness(Action<IServiceCollection> registerCollections, TimeSpan? throttleInterval = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(Logs);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton<IHubContext<AutoPatchHub>>(Hub);
        services.AddAutoPatch(options => options.DefaultThrottleInterval = throttleInterval ?? TimeSpan.FromMilliseconds(20));
        registerCollections(services);
        _services = services.BuildServiceProvider();
    }

    public RecordingHubContext Hub { get; } = new();

    public CapturingLoggerFactory Logs { get; } = new();

    public IServiceProvider Services => _services;

    public ITrackedCollectionManager Manager => _services.GetRequiredService<ITrackedCollectionManager>();

    /// <summary>Creates the hub instance SignalR would create for one invocation of the given connection.</summary>
    public AutoPatchHub CreateHub(string connectionId, ClaimsPrincipal? user = null)
    {
        var scope = _services.CreateScope();
        return new AutoPatchHub(Manager, scope.ServiceProvider)
        {
            Context = new FakeHubCallerContext(connectionId, user),
            Groups = Hub,
        };
    }

    public ClientMirror Client(string connectionId) => new(Hub, connectionId, requireInitialSet: true);

    public void Dispose()
    {
        foreach (var tracker in Manager.GetAllTrackers().ToArray())
        {
            (tracker as IDisposable)?.Dispose();
        }
        _services.Dispose();
    }

    private sealed class FakeHubCallerContext(string connectionId, ClaimsPrincipal? user) : HubCallerContext
    {
        public override string ConnectionId => connectionId;

        public override string? UserIdentifier => user?.Identity?.Name;

        public override ClaimsPrincipal? User => user;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
        }
    }
}
