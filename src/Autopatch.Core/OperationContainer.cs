using System.Collections.Generic;
using System.Text.Json;

namespace Autopatch.Core;

/// <summary>
/// Base type for entries of the per-collection operation queue.
/// </summary>
/// <typeparam name="T">The item type of the tracked collection.</typeparam>
public abstract record OperationContainer<T>();

/// <summary>
/// A single patch operation that is sent to all subscribers of the collection with the next batch.
/// </summary>
/// <typeparam name="T">The item type of the tracked collection.</typeparam>
/// <param name="Operation">The patch operation.</param>
public record DefaultOperationContainer<T>(PatchOperation Operation) : OperationContainer<T>();

/// <summary>
/// Replaces the complete content of the collection for all subscribers, e.g. after <c>Clear()</c>.
/// </summary>
/// <typeparam name="T">The item type of the tracked collection.</typeparam>
/// <param name="Items">The serialized items of the collection after the reset.</param>
public record ResetOperationContainer<T>(IReadOnlyList<JsonElement> Items) : OperationContainer<T>();
