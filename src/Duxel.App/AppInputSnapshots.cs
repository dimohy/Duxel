using Duxel.Core;

namespace Duxel.App;

internal static class AppInputSnapshots
{
    internal static InputSnapshot Copy(InputSnapshot input)
    {
        UiInputState[]? ordered = null;
        if (input.OrderedInputs is { } source)
        {
            if (source.Count == 0 && (input.KeyEvents.Count > 0 || input.CharEvents.Count > 0
                || input.LeftMousePressedEvent || input.LeftMouseReleasedEvent
                || input.RightMousePressedEvent || input.RightMouseReleasedEvent
                || input.MiddleMousePressedEvent || input.MiddleMouseReleasedEvent
                || input.MouseWheel != 0 || input.MouseWheelHorizontal != 0))
                throw new InvalidOperationException("Ordered capture published aggregate events without ordered input steps.");
            ordered = source.Count == 0 ? [] : new UiInputState[source.Count];
            for (var i = 0; i < source.Count; i++)
                ordered[i] = source[i] with
                {
                    KeyEvents = CopyEvents(source[i].KeyEvents),
                    CharEvents = CopyEvents(source[i].CharEvents)
                };
        }
        return input with
        {
            KeyEvents = CopyEvents(input.KeyEvents),
            CharEvents = CopyEvents(input.CharEvents),
            OrderedInputs = ordered
        };
    }

    private static T[] CopyEvents<T>(IReadOnlyList<T> source)
    {
        if (source.Count == 0)
            return [];
        var copy = new T[source.Count];
        for (var i = 0; i < source.Count; i++)
            copy[i] = source[i];
        return copy;
    }

    private static IReadOnlyList<T> AppendPending<T>(IReadOnlyList<T> first, IReadOnlyList<T> second)
    {
        if (first.Count == 0)
            return second;
        if (second.Count == 0)
            return first;
        // Only the pending slot owns this list. A drain replaces it before
        // the platform thread can append another packet.
        var pending = first as List<T> ?? new List<T>(first);
        pending.AddRange(second);
        return pending;
    }

    internal sealed class PendingInput
    {
        internal InputSnapshot Latest { get; private set; }
        internal bool HasSnapshot { get; private set; }

        internal void Add(InputSnapshot copied)
        {
            Latest = HasSnapshot ? MergePending(Latest, copied) : copied;
            HasSnapshot = true;
        }

        internal InputSnapshot Drain()
        {
            var input = Latest;
            Latest = Clear(input);
            return input;
        }
    }

    private static InputSnapshot MergePending(InputSnapshot first, InputSnapshot second)
    {
        if ((first.OrderedInputs is null) != (second.OrderedInputs is null))
            throw new InvalidOperationException("A platform cannot change input ordering mode during an application session.");
        return second with
        {
            LeftMousePressedEvent = first.LeftMousePressedEvent || second.LeftMousePressedEvent,
            LeftMouseReleasedEvent = first.LeftMouseReleasedEvent || second.LeftMouseReleasedEvent,
            RightMousePressedEvent = first.RightMousePressedEvent || second.RightMousePressedEvent,
            RightMouseReleasedEvent = first.RightMouseReleasedEvent || second.RightMouseReleasedEvent,
            MiddleMousePressedEvent = first.MiddleMousePressedEvent || second.MiddleMousePressedEvent,
            MiddleMouseReleasedEvent = first.MiddleMouseReleasedEvent || second.MiddleMouseReleasedEvent,
            MouseWheel = first.MouseWheel + second.MouseWheel,
            MouseWheelHorizontal = first.MouseWheelHorizontal + second.MouseWheelHorizontal,
            KeyEvents = AppendPending(first.KeyEvents, second.KeyEvents),
            CharEvents = AppendPending(first.CharEvents, second.CharEvents),
            OrderedInputs = first.OrderedInputs is { } ordered ? AppendPending(ordered, second.OrderedInputs!) : null
        };
    }

    internal static InputSnapshot Clear(InputSnapshot input) => input with
    {
        LeftMousePressedEvent = false,
        LeftMouseReleasedEvent = false,
        RightMousePressedEvent = false,
        RightMouseReleasedEvent = false,
        MiddleMousePressedEvent = false,
        MiddleMouseReleasedEvent = false,
        KeyEvents = [],
        CharEvents = [],
        MouseWheel = 0,
        MouseWheelHorizontal = 0,
        OrderedInputs = input.OrderedInputs is null ? null : []
    };

    internal static IReadOnlyList<UiInputState>? ToLogical(InputSnapshot input, float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Input content scale must be finite and positive.");
        if (input.OrderedInputs is not { Count: > 0 } source)
            return input.OrderedInputs;
        var result = new UiInputState[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var capturedScale = source[i].CoordinateScale ?? scale;
            if (!float.IsFinite(capturedScale) || capturedScale <= 0)
                throw new InvalidOperationException("Native input capture scale must be finite and positive.");
            result[i] = source[i] with
            {
                MousePosition = new(source[i].MousePosition.X / capturedScale, source[i].MousePosition.Y / capturedScale),
                CoordinateScale = null
            };
        }
        return result;
    }

    internal static int CaptureGlyphs(string? text, HashSet<int> codepoints)
    {
        var added = 0;
        if (text is null)
            return added;
        // Preserve the existing BMP atlas-discovery policy without publishing UTF-16 halves.
        foreach (var unit in text)
            if (!char.IsSurrogate(unit) && !char.IsControl(unit) && codepoints.Add(unit))
                added++;
        return added;
    }

    internal static int CaptureGlyphs(InputSnapshot input, HashSet<int> codepoints)
    {
        if (input.OrderedInputs is not { } ordered)
            return CaptureGlyphs(input.CharEvents, codepoints);
        var added = 0;
        for (var i = 0; i < ordered.Count; i++)
            added += CaptureGlyphs(ordered[i].CharEvents, codepoints);
        return added;
    }

    private static int CaptureGlyphs(IReadOnlyList<UiCharEvent> characters, HashSet<int> codepoints)
    {
        var added = 0;
        for (var i = 0; i < characters.Count; i++)
            if (characters[i].CodePoint <= 0xFFFFu && codepoints.Add((int)characters[i].CodePoint))
                added++;
        return added;
    }
    internal static bool HasInputDrivenInvalidation(InputSnapshot snapshot, InputSnapshot previous)
    {
        if (snapshot.IsWindowInactive != previous.IsWindowInactive
            || snapshot.OrderedInputs is { Count: > 0 }
            || snapshot.KeyEvents.Count > 0 || snapshot.CharEvents.Count > 0)
        {
            return true;
        }

        if (MathF.Abs(snapshot.MouseWheel) > 0.0001f || MathF.Abs(snapshot.MouseWheelHorizontal) > 0.0001f)
        {
            return true;
        }

        if (snapshot.LeftMousePressedEvent
            || snapshot.LeftMouseReleasedEvent
            || snapshot.RightMousePressedEvent
            || snapshot.RightMouseReleasedEvent
            || snapshot.MiddleMousePressedEvent
            || snapshot.MiddleMouseReleasedEvent)
        {
            return true;
        }

        // Holding a stationary button is state, not new input. Controls
        // with repeats or drag autoscroll request their own future work.
        if (snapshot.LeftMouseDown != previous.LeftMouseDown
            || snapshot.RightMouseDown != previous.RightMouseDown
            || snapshot.MiddleMouseDown != previous.MiddleMouseDown)
        {
            return true;
        }

        return MathF.Abs(snapshot.MousePosition.X - previous.MousePosition.X) > 0.01f
            || MathF.Abs(snapshot.MousePosition.Y - previous.MousePosition.Y) > 0.01f;
    }
}
