using System.Buffers;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Autopatch.Client.Extensions;
using Autopatch.Client.Models;
using Autopatch.Client.Services;
using Autopatch.Core;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Autopatch.Client.Tests.Infrastructure;

/// <summary>
/// Feeds batches into <see cref="AutoPatchClient"/> exactly as SignalR would deliver them, without a server.
/// </summary>
/// <remarks>
/// The test builds <see cref="Operation"/> objects, SignalR serializes them with <see cref="JsonHubProtocol"/> and the client
/// deserializes them as <see cref="PatchOperation"/>, so values arrive as <see cref="System.Text.Json.JsonElement"/>. The subscription is registered and the
/// batch handed over via reflection because both are private.
/// </remarks>
public sealed class ClientPatchHarness<T> : IDisposable where T : class
{
    private static readonly JsonHubProtocol Protocol = new();

    private static readonly MethodInfo HandleBatch = typeof(AutoPatchClient)
        .GetMethod("HandleAutoPatchItem", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("AutoPatchClient.HandleAutoPatchItem not found - update the test harness.");

    private readonly ServiceProvider _services;
    private long _sequence;

    public ClientPatchHarness()
    {
        var services = new ServiceCollection();
        services.AddTrackedCollection<T>();
        _services = services.BuildServiceProvider();

        Client = new AutoPatchClient(Options.Create(new AutoPatchConfiguration { Endpoint = "http://localhost:1" }), _services);
        Collection = _services.GetRequiredService<ObservableCollection<T>>();

        var subscriptionType = typeof(AutoPatchClient).Assembly.GetType("Autopatch.Client.Models.Subscription", throwOnError: true)!;
        var subscriptions = (IDictionary)typeof(AutoPatchClient)
            .GetField("_subscriptions", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Client)!;
        subscriptions.Add(MethodName, Activator.CreateInstance(subscriptionType, Collection, typeof(T)));
    }

    public AutoPatchClient Client { get; }

    public ObservableCollection<T> Collection { get; }

    public string MethodName { get; } = $"AutoPatch/{typeof(T).Name}";

    /// <summary>Delivers full data with the sequence number of the last batch, like the server does.</summary>
    public void DeliverInitialSet(params object[] items)
        => Deliver(true, _sequence, [.. items.Select(i => new Operation { op = "add", path = "/-", value = i })]);

    /// <summary>Delivers the next batch.</summary>
    public void DeliverPatch(params Operation[] operations) => Deliver(false, ++_sequence, operations);

    public static Operation Replace(string path, object? value) => new() { op = "replace", path = path, value = value };

    /// <summary>Only the SignalR serialization round trip, as a baseline for measurements.</summary>
    public void RoundTripOnly(params Operation[] operations) => RoundTrip([MethodName, operations, false, 0L]);

    private void Deliver(bool isInitialSet, long sequence, Operation[] operations)
    {
        var arguments = RoundTrip([MethodName, operations, isInitialSet, sequence]);
        try
        {
            HandleBatch.Invoke(Client, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    private object?[] RoundTrip(object?[] arguments)
    {
        var writer = new ArrayBufferWriter<byte>();
        Protocol.WriteMessage(new InvocationMessage(MethodName, arguments), writer);
        var input = new ReadOnlySequence<byte>(writer.WrittenMemory);
        if (!Protocol.TryParseMessage(ref input, new Binder(), out var message) || message is not InvocationMessage invocation)
        {
            throw new InvalidOperationException("Could not parse the SignalR message.");
        }
        return invocation.Arguments;
    }

    public void Dispose() => _services.Dispose();

    /// <summary>The parameter types the client registers with <c>HubConnection.On&lt;string, PatchOperation[], bool, long&gt;</c>.</summary>
    private sealed class Binder : IInvocationBinder
    {
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => [typeof(string), typeof(PatchOperation[]), typeof(bool), typeof(long)];

        public Type GetReturnType(string invocationId) => throw new NotSupportedException();

        public Type GetStreamItemType(string streamId) => throw new NotSupportedException();
    }
}
