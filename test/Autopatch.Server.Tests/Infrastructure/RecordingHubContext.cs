using System.Text.Json.Nodes;
using Autopatch.Server.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace Autopatch.Server.Tests.Infrastructure;

public enum MessageTargetKind
{
    All,
    Client,
    Group,
}

/// <summary>
/// A message as it left the server, serialized with the SignalR JSON protocol.
/// <see cref="Recipients"/> holds the connection ids that would have received it at send time.
/// </summary>
public sealed record SentMessage(
    MessageTargetKind Kind,
    string Target,
    string Method,
    JsonArray Arguments,
    IReadOnlyList<string> Recipients)
{
    public JsonArray Operations => Arguments.Count > 1 && Arguments[1] is JsonArray operations ? operations : [];

    public bool IsInitialSet => Arguments.Count > 2 && Arguments[2] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}

/// <summary>
/// In-memory replacement for <see cref="IHubContext{THub}"/> that records every message and group membership.
/// </summary>
public sealed class RecordingHubContext : IHubContext<AutoPatchHub>, IHubClients, IGroupManager
{
    private readonly Lock _gate = new();
    private readonly List<SentMessage> _messages = [];
    private readonly Dictionary<string, HashSet<string>> _groups = [];

    /// <summary>
    /// Invoked for every message before it is recorded. Throw or block here to simulate transport problems.
    /// </summary>
    public Func<SentMessage, Task>? BeforeSend { get; set; }

    public IReadOnlyList<SentMessage> Messages
    {
        get { lock (_gate) return [.. _messages]; }
    }

    public IReadOnlyList<SentMessage> MessagesTo(MessageTargetKind kind)
        => [.. Messages.Where(m => m.Kind == kind)];

    public void ClearMessages()
    {
        lock (_gate) _messages.Clear();
    }

    IHubClients IHubContext<AutoPatchHub>.Clients => this;

    IGroupManager IHubContext<AutoPatchHub>.Groups => this;

    public IClientProxy All => new Proxy(this, MessageTargetKind.All, "*");

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

    public IClientProxy Client(string connectionId) => new Proxy(this, MessageTargetKind.Client, connectionId);

    ISingleClientProxy IHubClients.Client(string connectionId) => new Proxy(this, MessageTargetKind.Client, connectionId);

    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

    public IClientProxy Group(string groupName) => new Proxy(this, MessageTargetKind.Group, groupName);

    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

    public IClientProxy Groups(IReadOnlyList<string> groupNames)
        => groupNames.Count == 1 ? Group(groupNames[0]) : throw new NotSupportedException();

    public IClientProxy User(string userId) => throw new NotSupportedException();

    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_groups.TryGetValue(groupName, out var members))
            {
                _groups[groupName] = members = [];
            }
            members.Add(connectionId);
        }
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_groups.TryGetValue(groupName, out var members))
            {
                members.Remove(connectionId);
            }
        }
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<string> GetGroupMembers(string groupName)
    {
        lock (_gate) return _groups.TryGetValue(groupName, out var members) ? [.. members] : [];
    }

    private async Task SendAsync(MessageTargetKind kind, string target, string method, object?[] args)
    {
        var arguments = SignalRWire.SerializeArguments(method, args);
        IReadOnlyList<string> recipients;
        lock (_gate)
        {
            recipients = kind switch
            {
                MessageTargetKind.Client => [target],
                MessageTargetKind.Group => _groups.TryGetValue(target, out var members) ? [.. members] : [],
                _ => [.. _groups.Values.SelectMany(m => m).Distinct()],
            };
        }

        var message = new SentMessage(kind, target, method, arguments, recipients);
        if (BeforeSend is { } hook)
        {
            await hook(message);
        }

        lock (_gate) _messages.Add(message);
    }

    private sealed class Proxy(RecordingHubContext owner, MessageTargetKind kind, string target) : ISingleClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            => owner.SendAsync(kind, target, method, args);

        public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
