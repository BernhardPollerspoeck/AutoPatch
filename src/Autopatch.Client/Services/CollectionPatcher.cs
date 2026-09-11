using System.Collections;
using System.Text.Json;
using Autopatch.Client.Models;
using Autopatch.Core;

namespace Autopatch.Client.Services;

/// <summary>
/// Applies AutoPatch batches to a collection. A batch is validated and all values are converted before the first change,
/// so a batch is either applied completely or not at all.
/// </summary>
internal static class CollectionPatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Validates a batch against the current collection and prepares its steps.
    /// </summary>
    /// <exception cref="InvalidOperationException">The batch does not fit the collection.</exception>
    public static List<Action<Subscription>> Prepare(Subscription subscription, IReadOnlyList<PatchOperation> operations)
    {
        var count = subscription.Items.Count;
        var steps = new List<Action<Subscription>>(operations.Count);

        foreach (var operation in operations)
        {
            if (!AutoPatchProtocol.TryParsePath(operation.Path, out var index, out var property))
            {
                throw Invalid(operation, "unsupported path");
            }

            switch (operation.Op)
            {
                case PatchOperation.Add when property is null:
                    var insertAt = index < 0 ? count : index;
                    EnsureIndex(operation, insertAt, count + 1);
                    var added = Convert(operation, subscription.ItemType);
                    steps.Add(s => s.Items.Insert(insertAt, added));
                    count++;
                    break;

                case PatchOperation.Remove when property is null:
                    EnsureIndex(operation, index, count);
                    steps.Add(s => s.Items.RemoveAt(index));
                    count--;
                    break;

                case PatchOperation.Replace when property is null:
                    EnsureIndex(operation, index, count);
                    var replacement = Convert(operation, subscription.ItemType);
                    steps.Add(s => s.Items[index] = replacement);
                    break;

                case PatchOperation.Replace:
                    EnsureIndex(operation, index, count);
                    var propertyInfo = subscription.GetPropertyInfo(property);
                    if (propertyInfo is not { CanWrite: true })
                    {
                        throw Invalid(operation, $"'{subscription.ItemType.Name}' has no writable property '{property}'");
                    }
                    var value = Convert(operation, propertyInfo.PropertyType);
                    steps.Add(s => propertyInfo.SetValue(s.Items[index] ?? throw Invalid(operation, "the item is null"), value));
                    break;

                case PatchOperation.Move when property is null
                    && AutoPatchProtocol.TryParsePath(operation.From, out var from, out var fromProperty) && fromProperty is null:
                    EnsureIndex(operation, from, count);
                    EnsureIndex(operation, index, count);
                    steps.Add(s => s.Move(from, index));
                    break;

                default:
                    throw Invalid(operation, "unsupported operation");
            }
        }

        return steps;
    }

    /// <summary>
    /// Converts the items of an initial set.
    /// </summary>
    public static List<object?> PrepareInitialSet(Subscription subscription, IReadOnlyList<PatchOperation> operations)
        => [.. operations.Select(operation => operation.Op == PatchOperation.Add
            ? Convert(operation, subscription.ItemType)
            : throw Invalid(operation, "an initial set may only contain 'add' operations"))];

    private static object? Convert(PatchOperation operation, Type targetType)
    {
        if (operation.Value is not { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } value)
        {
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null
                ? null
                : throw Invalid(operation, $"null is not a valid {targetType.Name}");
        }

        try
        {
            return value.Deserialize(targetType, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Cannot apply '{operation.Op} {operation.Path}': the value is not a valid {targetType.Name}.", ex);
        }
    }

    private static void EnsureIndex(PatchOperation operation, int index, int exclusiveUpperBound)
    {
        if (index < 0 || index >= exclusiveUpperBound)
        {
            throw Invalid(operation, $"index {index} is out of range");
        }
    }

    private static InvalidOperationException Invalid(PatchOperation operation, string reason)
        => new($"Cannot apply '{operation.Op} {operation.Path}': {reason}.");
}
