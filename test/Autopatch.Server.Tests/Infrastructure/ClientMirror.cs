using System.Text.Json.Nodes;

namespace Autopatch.Server.Tests.Infrastructure;

/// <summary>
/// Replays the messages a single connection received and applies them like a well-behaved client:
/// an initial set replaces the local state, patches before the initial set are ignored (same as the .NET client),
/// and every operation follows JSON Patch (RFC 6902) semantics for arrays.
/// </summary>
public sealed class ClientMirror(RecordingHubContext hub, string connectionId, bool requireInitialSet)
{
    private readonly List<JsonObject> _items = [];
    private int _processed;

    public string ConnectionId => connectionId;

    public bool IsInitialized { get; private set; } = !requireInitialSet;

    /// <summary>Starts the mirror from a known state and skips everything sent so far.</summary>
    public void Seed(IEnumerable<object> items)
    {
        _items.Clear();
        _items.AddRange(items.Select(SignalRWire.SerializeItem));
        IsInitialized = true;
        _processed = hub.Messages.Count;
    }

    /// <summary>Applies all messages received since the last call.</summary>
    public ClientMirror Sync()
    {
        var messages = hub.Messages;
        for (; _processed < messages.Count; _processed++)
        {
            var message = messages[_processed];
            if (!message.Recipients.Contains(connectionId) || !message.Method.StartsWith("AutoPatch/", StringComparison.Ordinal))
            {
                continue;
            }
            Apply(message);
        }
        return this;
    }

    public IReadOnlyList<string> Snapshot() => [.. Sync()._items.Select(i => i.ToJsonString())];

    public IReadOnlyList<JsonObject> Items => Sync()._items;

    private void Apply(SentMessage message)
    {
        if (message.IsInitialSet)
        {
            _items.Clear();
            IsInitialized = true;
        }
        else if (!IsInitialized)
        {
            return;
        }

        foreach (var node in message.Operations)
        {
            ApplyOperation(node!.AsObject());
        }
    }

    private void ApplyOperation(JsonObject operation)
    {
        var op = operation["op"]!.GetValue<string>();
        var path = operation["path"]!.GetValue<string>();
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        switch (op)
        {
            case "add" when segments is ["-"]:
                _items.Add(Value(operation).AsObject());
                break;
            case "add" when segments.Length == 1:
                _items.Insert(Index(segments[0], path, allowEnd: true), Value(operation).AsObject());
                break;
            case "remove" when segments.Length == 1:
                _items.RemoveAt(Index(segments[0], path));
                break;
            case "replace" when segments.Length == 1:
                _items[Index(segments[0], path)] = Value(operation).AsObject();
                break;
            case "replace" when segments.Length == 2:
                _items[Index(segments[0], path)][CamelCase(segments[1])] = operation["value"]?.DeepClone();
                break;
            case "move" when segments.Length == 1:
                var from = operation["from"]!.GetValue<string>().Split('/', StringSplitOptions.RemoveEmptyEntries);
                var moved = _items[Index(from[0], path)];
                _items.Remove(moved);
                _items.Insert(Index(segments[0], path, allowEnd: true), moved);
                break;
            default:
                throw new InvalidOperationException($"Client cannot apply operation '{op}' on path '{path}'.");
        }
    }

    private int Index(string segment, string path, bool allowEnd = false)
    {
        var upperBound = allowEnd ? _items.Count : _items.Count - 1;
        if (!int.TryParse(segment, out var index) || index < 0 || index > upperBound)
        {
            throw new InvalidOperationException(
                $"Server sent path '{path}' but the client only has {_items.Count} items - the patch cannot be applied.");
        }
        return index;
    }

    private static JsonNode Value(JsonObject operation) => operation["value"]!.DeepClone();

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
