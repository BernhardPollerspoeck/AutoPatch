namespace Autopatch.Core;

public class AutoPatchRemoveItem<T> where T : class
{
    public required T Item { get; set; }
    public int Index { get; set; }
}
