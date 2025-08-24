namespace Autopatch.Core;

public class AutoPatchItem
{
    public required object ItemId { get; set; }
    public required AutoPatchAction Action { get; set; }
    public required string? Data { get; set; }
}
public class AutoPatchAddDocument
{
    public required int Index { get; set; }
    public required string Json { get; set; }
}
