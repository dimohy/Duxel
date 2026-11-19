namespace Duxel.Core;

/// <summary>An owned keyed widget with an explicit slot allocation. Match the child's size to this allocation.</summary>
public readonly record struct UiInputSlot(string Key,UiWidget Content,UiVector2 Size)
{
    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrEmpty(Key);ArgumentNullException.ThrowIfNull(Content);
        if(!float.IsFinite(Size.X)||!float.IsFinite(Size.Y)||Size.X<=0||Size.Y<=0)throw new ArgumentOutOfRangeException(nameof(Size));
    }
}
internal abstract class UiInputChildren
{
    internal UiInputSlot? Before,After;
}
