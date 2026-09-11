using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Autopatch.Server.Tests.Infrastructure;

/// <summary>
/// Serializes hub invocations with the real SignalR <see cref="JsonHubProtocol"/>, so tests see exactly what a client would receive.
/// </summary>
public static class SignalRWire
{
    private static readonly JsonHubProtocol Protocol = new();

    public static JsonSerializerOptions PayloadOptions { get; } = new JsonHubProtocolOptions().PayloadSerializerOptions;

    public static JsonArray SerializeArguments(string method, object?[] arguments)
    {
        var writer = new ArrayBufferWriter<byte>();
        Protocol.WriteMessage(new InvocationMessage(method, arguments), writer);

        // Strip the trailing record separator (0x1e).
        var message = JsonNode.Parse(writer.WrittenSpan[..^1])!.AsObject();
        return (JsonArray)message["arguments"]!.DeepClone();
    }

    public static JsonObject SerializeItem(object item)
        => JsonSerializer.SerializeToNode(item, item.GetType(), PayloadOptions)!.AsObject();
}
