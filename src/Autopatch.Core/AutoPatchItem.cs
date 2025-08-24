namespace Autopatch.Core;

public class AutoPatchItem<T>
{
    public required object ItemId { get; set; }
    public required string Action { get; set; }
}