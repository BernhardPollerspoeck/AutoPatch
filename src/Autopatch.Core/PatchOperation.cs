using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autopatch.Core;

/// <summary>
/// A single JSON Patch (RFC 6902) operation as it is sent between the AutoPatch server and its clients.
/// </summary>
/// <remarks>
/// Paths address the tracked collection: <c>/3</c> is the item at index 3, <c>/3/Name</c> the property <c>Name</c> of that item
/// and <c>/-</c> the end of the collection. Values are pre-serialized JSON, so they reflect the state at the time of the change.
/// </remarks>
public sealed record PatchOperation
{
    /// <summary>JSON Patch operation type for adding items.</summary>
    public const string Add = "add";

    /// <summary>JSON Patch operation type for removing items.</summary>
    public const string Remove = "remove";

    /// <summary>JSON Patch operation type for replacing items or property values.</summary>
    public const string Replace = "replace";

    /// <summary>JSON Patch operation type for moving items.</summary>
    public const string Move = "move";

    /// <summary>
    /// Gets the operation type, one of <see cref="Add"/>, <see cref="Remove"/>, <see cref="Replace"/> or <see cref="Move"/>.
    /// </summary>
    [JsonPropertyName("op")]
    public string Op { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target path of the operation.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source path of a <see cref="Move"/> operation.
    /// </summary>
    [JsonPropertyName("from")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? From { get; init; }

    /// <summary>
    /// Gets the value of an <see cref="Add"/> or <see cref="Replace"/> operation. A JSON <c>null</c> value is represented by an
    /// element of kind <see cref="JsonValueKind.Null"/>; operations without a value leave it unset.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Value { get; init; }
}
