using System.Collections.ObjectModel;
using Microsoft.AspNetCore.JsonPatch.Operations;

namespace Autopatch.Core;

public abstract record OperationContainer<T>();
public record DefaultOperationContainer<T>(Operation Operation) : OperationContainer<T>();
public record FullDataOperationContainer<T>(Operation[] Operation, string ConnectionId) : OperationContainer<T>();
//TODO: full Data can exceed the max item count of a message, so we need to chunk it


