using System.Collections.ObjectModel;
using Microsoft.AspNetCore.JsonPatch.Operations;

namespace Autopatch.Core;

/// <summary>
/// Represents a container for an operation that encapsulates a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the value associated with the operation.</typeparam>
public abstract record OperationContainer<T>();

/// <summary>
/// Represents the default implementation of an operation container for a specific type.
/// </summary>
/// <remarks>This container encapsulates an <see cref="Operation"/> and provides a default mechanism for
/// associating it with a specific type.</remarks>
/// <typeparam name="T">The type of the value associated with the operation.</typeparam>
/// <param name="Operation"></param>
public record DefaultOperationContainer<T>(Operation Operation) : OperationContainer<T>();

/// <summary>
/// Represents a container for a full set of data operations, along with the associated connection identifier.
/// </summary>
/// <remarks>This record is used to encapsulate a collection of operations and their associated connection
/// context. It extends <see cref="OperationContainer{T}"/> to provide additional details specific to full data
/// operations.</remarks>
/// <typeparam name="T">The type of data associated with the operations.</typeparam>
/// <param name="Operation">An array of operations to be performed. Cannot be null.</param>
/// <param name="ConnectionId">The identifier for the connection associated with the operations. Cannot be null or empty.</param>
public record FullDataOperationContainer<T>(Operation[] Operation, string ConnectionId) : OperationContainer<T>();




