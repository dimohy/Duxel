namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginChild(string id, UiVector2 size, bool border = false)
    {
        id ??= "Child";

        var width = size.X > 0f ? size.X : InputWidth;
        var height = size.Y > 0f ? size.Y : GetFrameHeight() * 6f;
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        if (border)
        {
            AddRectFilled(rect, _theme.FrameBg, _whiteTexture);
        }

        PushClipRect(rect, true);
        var start = new UiVector2(rect.X + 2f, rect.Y + 2f);
        _layouts.Push(new UiLayoutState(start, false, 0f, start.X));
        _childStack.Push(new UiChildState(rect, start));
        return true;
    }

    public void EndChild()
    {
        if (_childStack.Count == 0)
        {
            return;
        }

        _layouts.Pop();
        PopClipRect();
        _childStack.Pop();
    }
}
