using System;
using System.Collections.Generic;

namespace Duxel.Core;

public sealed partial class UiState
{
    internal (string Owner, UiRect? Bounds)? PointerMoveCoalescing { get; set; }
    internal string? SliderPointerMoveCoalescing { get; set; }
    internal (float Wheel, double Remaining, bool Horizontal)? WheelCoalescing { get; set; }
    internal (string CodeId, UiRect Bounds)? DocumentWheelRegion { get; set; }
}

public sealed partial class UiContext
{
    private IReadOnlyList<UiInputState>? _inputBatch;
    private int _initialInputIndex;

    /// <summary>Begins one host frame with ordered, complete input states. Each state describes one
    /// logical input step. Borrowed input lists must remain unchanged until Render returns.
    /// The host submits only the final GetDrawData result. Host time advances once.</summary>
    public void NewInputBatch(UiFrameInfo frameInfo, IReadOnlyList<UiInputState> inputs, UiRect clipRect, UiTextSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
            throw new ArgumentException("An ordered input batch must contain at least one input state.", nameof(inputs));
        for (var i = 0; i < inputs.Count; i++)
            ValidateInputMetadata(inputs[i]);
        var initialIndex = 0;
        var absoluteDragOwner = _state.PreciseScrollbarDrag is { CoalesceMoves: true } drag
            ? drag.Id : _state.SliderPointerMoveCoalescing;
        if (inputs.Count > 1 && absoluteDragOwner is not null && _state.ActiveId == absoluteDragOwner
            && _state.PreviousPopupBlockingRects.Count == 0
            && _queuedMousePos is null && _queuedMouseDown is null && _queuedRightMouseDown is null
            && _queuedMiddleMouseDown is null && _queuedMouseWheel == 0 && _queuedMouseWheelHorizontal == 0
            && _queuedKeyEvents.Count == 0 && _queuedCharEvents.Count == 0
            && IsCapturedMove(inputs[0], _inputState, null))
        {
            // An already captured scrollbar or opted-in slider uses the latest absolute position.
            // Only skip a whole batch of motion; mixed batches retain their usual ordering.
            var next = 1;
            while (next < inputs.Count && IsCapturedMove(inputs[next], inputs[next - 1], null))
                next++;
            if (next == inputs.Count)
                initialIndex = next - 1;
        }
        NewFrame(frameInfo, inputs[initialIndex], clipRect, settings);
        _inputBatch = inputs;
        _initialInputIndex = initialIndex;
    }

    private static void ValidateInputMetadata(UiInputState input)
    {
        if (input.EventTimeSeconds is { } time && (!double.IsFinite(time) || time < 0))
            throw new ArgumentOutOfRangeException(nameof(input), "Input event time must be finite and nonnegative.");
        UiState.ValidateKeyRepeatSource(input.KeyRepeatSettings.Source);
    }

    private void RenderInputBatch(UiScreen screen, IReadOnlyList<UiInputState> inputs, ref UiInputProcessingMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(screen);
        // A failed callback is not replayed automatically, including input already consumed.
        _inputBatch = null;
        for (var i = _initialInputIndex; i < inputs.Count; i++)
        {
            if (i > _initialInputIndex)
            {
                // A completed traversal has confirmed that the control still holds capture.
                if (_state.PointerMoveCoalescing is { } capture && _state.ActiveId == capture.Owner
                    && (capture.Bounds is null || _state.CurrentPopupBlockingRects.Count == 0))
                    while (i + 1 < inputs.Count && IsCapturedMove(inputs[i], inputs[i - 1], capture.Bounds)
                        && IsCapturedMove(inputs[i + 1], inputs[i], capture.Bounds))
                        i++;
                var input = inputs[i];
                if (_state.WheelCoalescing is { } wheel && _state.CurrentPopupBlockingRects.Count == 0
                    && IsCoalescibleWheel(input, inputs[i - 1], wheel.Wheel, wheel.Horizontal))
                {
                    var sum = wheel.Horizontal ? input.MouseWheelHorizontal : input.MouseWheel;
                    while (i + 1 < inputs.Count && IsCoalescibleWheel(inputs[i + 1], inputs[i], wheel.Wheel, wheel.Horizontal))
                    {
                        var next = sum + (wheel.Horizontal ? inputs[i + 1].MouseWheelHorizontal : inputs[i + 1].MouseWheel);
                        if (!float.IsFinite(next) || Math.Abs((double)next) >= wheel.Remaining)
                            break;
                        sum = next;
                        i++;
                    }
                    input = wheel.Horizontal ? inputs[i] with { MouseWheelHorizontal = sum } : inputs[i] with { MouseWheel = sum };
                }
                SetInput(input);
                _state.BeginFrame();
            }
            RenderSingleInput(screen, ref measurement,
                i == inputs.Count - 1 ? UiRenderPhase.Presentation : UiRenderPhase.Input);
        }
    }

    private static bool IsCoalescibleWheel(in UiInputState input, in UiInputState previous, float direction, bool horizontal)
    {
        var wheel = horizontal ? input.MouseWheelHorizontal : input.MouseWheel;
        var otherWheel = horizontal ? input.MouseWheel : input.MouseWheelHorizontal;
        return float.IsFinite(wheel) && MathF.Abs(wheel) > .001f
        && (wheel > 0) == (direction > 0)
        && otherWheel == 0 && input.MousePosition == previous.MousePosition
        && !input.LeftMouseDown && !input.RightMouseDown && !input.MiddleMouseDown
        && !input.LeftMousePressed && !input.LeftMouseReleased && !input.RightMousePressed && !input.RightMouseReleased
        && !input.MiddleMousePressed && !input.MiddleMouseReleased && input.Dispatch is null
        && input.KeyEvents is { Count: 0 } && input.CharEvents is { Count: 0 }
        && input.Modifiers == previous.Modifiers && input.KeyRepeatSettings == previous.KeyRepeatSettings
        && input.CoordinateScale == previous.CoordinateScale;
    }

    private static bool IsCapturedMove(in UiInputState input, in UiInputState previous, UiRect? bounds) =>
        input.LeftMouseDown && previous.LeftMouseDown && !input.RightMouseDown && !previous.RightMouseDown
        && !input.MiddleMouseDown && !previous.MiddleMouseDown && input.MousePosition != previous.MousePosition
        && !input.LeftMousePressed && !input.LeftMouseReleased && !input.RightMousePressed && !input.RightMouseReleased
        && !input.MiddleMousePressed && !input.MiddleMouseReleased && input.Dispatch is null
        && input.MouseWheel == 0 && input.MouseWheelHorizontal == 0
        && input.KeyEvents is { Count: 0 } && input.CharEvents is { Count: 0 }
        && input.Modifiers == previous.Modifiers && input.KeyRepeatSettings == previous.KeyRepeatSettings
        && input.CoordinateScale == previous.CoordinateScale
        && (bounds is not { } region || UiWidgetClipGeometry.Contains(region, input.MousePosition)
            && UiWidgetClipGeometry.Contains(region, previous.MousePosition));
}
