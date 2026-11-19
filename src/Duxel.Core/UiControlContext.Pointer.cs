namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    /// <summary>Queries pointer overlap using the current clip without registering input or focus.</summary>
    public bool IsHovered(UiRect bounds) => _ui.IsControlHovering(bounds);
    public void SetMouseCursor(UiMouseCursor cursor) => _ui.SetMouseCursor(cursor);
}

public sealed partial class UiImmediateContext
{
    internal bool IsControlHovering(UiRect bounds) => !_layoutPointerBlocked && _popupTierDepth >= _blockingKeyboardTier
        && IsScrollHoverable(bounds, CurrentClipRect);
}
