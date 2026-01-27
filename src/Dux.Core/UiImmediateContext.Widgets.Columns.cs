namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public void Columns(int count, bool border = false)
    {
        if (count <= 1)
        {
            if (_columnsActive)
            {
                var current = _layouts.Pop();
                current = current with { Cursor = new UiVector2(_columnsStartX, _columnsMaxY), LineStartX = _columnsStartX };
                _layouts.Push(current);
            }

            _columnsActive = false;
            _columnsCount = 0;
            _columnsIndex = 0;
            _columnsWidth = 0f;
            _columnsStartX = 0f;
            _columnsStartY = 0f;
            _columnsMaxY = 0f;
            _columnYs = Array.Empty<float>();
            return;
        }

        _columnsActive = true;
        _columnsCount = count;
        _columnsIndex = 0;

        var currentState = _layouts.Pop();
        _columnsStartX = currentState.Cursor.X;
        _columnsStartY = currentState.Cursor.Y;

        var availableWidth = _hasWindowRect
            ? MathF.Max(0f, _windowRect.Width - (WindowPadding * 2f))
            : (InputWidth * count) + (ItemSpacingX * (count - 1));
        _columnsWidth = MathF.Max(1f, (availableWidth - (ItemSpacingX * (count - 1))) / count);
        _columnsMaxY = _columnsStartY;

        if (_columnYs.Length != count)
        {
            _columnYs = new float[count];
        }
        if (_columnWidths.Length != count)
        {
            _columnWidths = new float[count];
        }
        for (var i = 0; i < count; i++)
        {
            _columnYs[i] = _columnsStartY;
            _columnWidths[i] = _columnsWidth;
        }

        currentState = currentState with { Cursor = new UiVector2(_columnsStartX, _columnsStartY), LineStartX = _columnsStartX };
        _layouts.Push(currentState);
        _ = border;
    }

    public void NextColumn()
    {
        if (!_columnsActive || _columnsCount <= 1)
        {
            return;
        }

        _columnsIndex = (_columnsIndex + 1) % _columnsCount;
    }

    public int GetColumnIndex() => _columnsIndex;

    public float GetColumnWidth(int index)
    {
        if (!_columnsActive || _columnsCount <= 0)
        {
            return 0f;
        }

        return GetColumnsColumnWidth(index);
    }

    public void SetColumnWidth(int index, float width)
    {
        if (!_columnsActive || (uint)index >= (uint)_columnsCount)
        {
            return;
        }

        _columnWidths[index] = MathF.Max(1f, width);
    }

    public float GetColumnOffset(int index)
    {
        if (!_columnsActive || _columnsCount <= 0)
        {
            return 0f;
        }

        return GetColumnsColumnX(index) - _columnsStartX;
    }

    public void SetColumnOffset(int index, float offset)
    {
        if (!_columnsActive || (uint)index >= (uint)_columnsCount)
        {
            return;
        }

        var targetX = _columnsStartX + MathF.Max(0f, offset);
        if (index == 0)
        {
            _columnsStartX = targetX;
            return;
        }

        var prevX = GetColumnsColumnX(index - 1);
        var prevWidth = MathF.Max(1f, targetX - prevX - ItemSpacingX);
        _columnWidths[index - 1] = prevWidth;
    }

    public int GetColumnsCount() => _columnsCount;
}
