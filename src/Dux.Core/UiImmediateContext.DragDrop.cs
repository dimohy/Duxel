using System;
using System.Buffers;

namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool IsDragDropActive() => _dragDropActive;

    public void ClearDragDrop()
    {
        if (_dragDropPayloadBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_dragDropPayloadBuffer, clearArray: false);
            _dragDropPayloadBuffer = null;
        }

        _dragDropActive = false;
        _dragDropWithinSource = false;
        _dragDropWithinTarget = false;
        _dragDropSourceFlags = UiDragDropFlags.None;
        _dragDropAcceptFlags = UiDragDropFlags.None;
        _dragDropSourceId = null;
        _dragDropTargetId = null;
        _dragDropMouseButton = (int)UiMouseButton.Left;
        _dragDropSourceFrame = -1;
        _dragDropAcceptFrame = -1;
        _dragDropTargetRect = default;
        _dragDropTargetClipRect = default;
        _dragDropPayloadSet = false;
        _dragDropPayload.DataType = string.Empty;
        _dragDropPayload.Data = ReadOnlyMemory<byte>.Empty;
        _dragDropPayload.Preview = false;
        _dragDropPayload.Delivery = false;
        _dragDropPayload.SourceId = null;
        _dragDropPayload.DataFrameCount = -1;
    }

    public bool BeginDragDropSource(UiDragDropFlags flags = UiDragDropFlags.None)
    {
        if (_dragDropWithinTarget)
        {
            return false;
        }

        if ((_currentItemFlags & UiItemFlags.Disabled) != 0)
        {
            return false;
        }

        string? sourceId = _lastItemId;
        if ((flags & UiDragDropFlags.SourceExtern) != 0)
        {
            if (!_leftMouseDown)
            {
                return false;
            }

            sourceId = "##DragDropExtern";
            _state.ActiveId = sourceId;
        }
        else
        {
            if (string.IsNullOrEmpty(sourceId))
            {
                if ((flags & UiDragDropFlags.SourceAllowNullID) == 0)
                {
                    return false;
                }

                var rect = new UiRect(_lastItemPos.X, _lastItemPos.Y, _lastItemSize.X, _lastItemSize.Y);
                sourceId = BuildDragDropNullId(rect);
            }

            if (!string.Equals(_state.ActiveId, sourceId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!_leftMouseDown || !IsMouseDragging(_dragDropMouseButton))
            {
                return false;
            }
        }

        if (!_dragDropActive || !string.Equals(_dragDropSourceId, sourceId, StringComparison.Ordinal))
        {
            ClearDragDrop();
            _dragDropActive = true;
            _dragDropSourceId = sourceId;
            _dragDropSourceFlags = flags;
            _dragDropMouseButton = (int)UiMouseButton.Left;
            _dragDropPayload.SourceId = sourceId;
        }

        _dragDropWithinSource = true;
        _dragDropSourceFrame = _state.FrameCount;

        if ((flags & UiDragDropFlags.SourceNoPreviewTooltip) == 0)
        {
            BeginTooltip();
        }

        return true;
    }

    public void EndDragDropSource()
    {
        if (!_dragDropActive || !_dragDropWithinSource)
        {
            return;
        }

        if ((_dragDropSourceFlags & UiDragDropFlags.SourceNoPreviewTooltip) == 0)
        {
            EndTooltip();
        }

        if (!_dragDropPayloadSet)
        {
            ClearDragDrop();
        }

        _dragDropWithinSource = false;
    }

    public bool SetDragDropPayload(string type, ReadOnlySpan<byte> data)
    {
        if (!_dragDropActive || !_dragDropWithinSource)
        {
            throw new InvalidOperationException("BeginDragDropSource must be called before setting the payload.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        ReadOnlyMemory<byte> buffer = ReadOnlyMemory<byte>.Empty;
        if (data.Length > 0)
        {
            if (_dragDropPayloadBuffer is null || _dragDropPayloadBuffer.Length < data.Length)
            {
                if (_dragDropPayloadBuffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(_dragDropPayloadBuffer, clearArray: false);
                }

                _dragDropPayloadBuffer = ArrayPool<byte>.Shared.Rent(data.Length);
            }

            data.CopyTo(_dragDropPayloadBuffer);
            buffer = new ReadOnlyMemory<byte>(_dragDropPayloadBuffer, 0, data.Length);
        }
        _dragDropPayload.DataType = type;
        _dragDropPayload.Data = buffer;
        _dragDropPayload.DataFrameCount = _state.FrameCount;
        _dragDropPayload.Preview = false;
        _dragDropPayload.Delivery = false;
        _dragDropPayload.SourceId = _dragDropSourceId;
        _dragDropPayloadSet = true;

        return _dragDropAcceptFrame == _state.FrameCount || _dragDropAcceptFrame == _state.FrameCount - 1;
    }

    public bool BeginDragDropTarget()
    {
        if (!_dragDropActive)
        {
            return false;
        }

        if (!IsItemHovered() || _lastItemId is null)
        {
            return false;
        }

        if (string.Equals(_lastItemId, _dragDropSourceId, StringComparison.Ordinal))
        {
            return false;
        }

        _dragDropTargetId = _lastItemId;
        _dragDropTargetRect = new UiRect(_lastItemPos.X, _lastItemPos.Y, _lastItemSize.X, _lastItemSize.Y);
        _dragDropTargetClipRect = CurrentClipRect;
        _dragDropWithinTarget = true;
        return true;
    }

    public UiDragDropPayload? AcceptDragDropPayload(string type, UiDragDropFlags flags = UiDragDropFlags.None)
    {
        if (!_dragDropActive || !_dragDropWithinTarget || !_dragDropPayloadSet)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(type) && !_dragDropPayload.IsDataType(type))
        {
            return null;
        }

        _dragDropAcceptFlags = flags;
        _dragDropAcceptFrame = _state.FrameCount;
        _dragDropPayload.Preview = true;
        _dragDropPayload.Delivery = !_leftMouseDown;

        if (!_dragDropPayload.Delivery && (flags & UiDragDropFlags.AcceptBeforeDelivery) == 0)
        {
            return null;
        }

        return _dragDropPayload;
    }

    public UiDragDropPayload? GetDragDropPayload()
    {
        return _dragDropActive && _dragDropPayloadSet ? _dragDropPayload : null;
    }

    public void EndDragDropTarget()
    {
        if (!_dragDropWithinTarget)
        {
            return;
        }

        _dragDropWithinTarget = false;
        if (_dragDropPayload.Delivery)
        {
            ClearDragDrop();
        }
    }

    private static string BuildDragDropNullId(UiRect rect)
    {
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"##DragDropNull:{rect.X:F1},{rect.Y:F1},{rect.Width:F1},{rect.Height:F1}");
    }
}
