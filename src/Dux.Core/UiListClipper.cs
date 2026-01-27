using System;

namespace Dux.Core;

public sealed class UiListClipper
{
    private UiImmediateContext? _ui;
    private bool _stepped;

    public int ItemsCount { get; private set; }
    public float ItemsHeight { get; private set; }
    public int DisplayStart { get; private set; }
    public int DisplayEnd { get; private set; }

    public void Begin(UiImmediateContext ui, int itemsCount, float itemsHeight = -1f)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        ItemsCount = Math.Max(0, itemsCount);
        ItemsHeight = itemsHeight > 0f ? itemsHeight : ui.GetTextLineHeightWithSpacing();
        DisplayStart = 0;
        DisplayEnd = 0;
        _stepped = false;
    }

    public bool Step()
    {
        if (_ui is null || ItemsCount == 0)
        {
            return false;
        }

        if (_stepped)
        {
            return false;
        }

        ApplyRequests();
        _stepped = true;
        return DisplayStart < DisplayEnd;
    }

    public void IncludeItemsByIndex(int itemStart, int itemEnd)
    {
        if (_ui is null)
        {
            return;
        }

        var start = Math.Clamp(itemStart, 0, ItemsCount);
        var end = Math.Clamp(itemEnd + 1, start, ItemsCount);
        DisplayStart = Math.Min(DisplayStart, start);
        DisplayEnd = Math.Max(DisplayEnd, end);
    }

    public void SeekCursorForItem(int itemIndex)
    {
        if (_ui is null)
        {
            return;
        }

        var cursor = _ui.GetCursorPos();
        var targetY = cursor.Y + (itemIndex * ItemsHeight);
        _ui.SetCursorPos(new UiVector2(cursor.X, targetY));
    }

    public void ApplyRequests()
    {
        if (_ui is null)
        {
            return;
        }

        _ui.CalcListClipping(ItemsCount, ItemsHeight, out var start, out var end);
        DisplayStart = start;
        DisplayEnd = end;
    }
}
