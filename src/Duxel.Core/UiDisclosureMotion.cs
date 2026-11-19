using System.Runtime.InteropServices;

namespace Duxel.Core;

internal static class UiDisclosureMotion
{
    internal const float DurationSeconds = .16f;
    internal static UiTransitionOptions Timing(double duration = DurationSeconds, bool reducedMotion = false)
        => new(duration, UiAnimationEasing.OutCubic, reducedMotion);

    internal static UiColor Blend(UiColor from, UiColor to, float progress)
    {
        if (progress == 0 || from == to) return from;
        if (progress == 1) return to;
        var fromAlpha = (byte)(from.Rgba >> 24);
        var toAlpha = (byte)(to.Rgba >> 24);
        var fromRgb = fromAlpha == 0 ? to.Rgba : from.Rgba;
        var toRgb = toAlpha == 0 ? from.Rgba : to.Rgba;
        byte Mix(byte a, byte b) => (byte)MathF.Round(a + (b - a) * progress);
        return new(Mix((byte)fromRgb, (byte)toRgb), Mix((byte)(fromRgb >> 8), (byte)(toRgb >> 8)),
            Mix((byte)(fromRgb >> 16), (byte)(toRgb >> 16)), Mix(fromAlpha, toAlpha));
    }

    internal static void Paint(UiControlContext context, UiVector2 center, UiColor color, float thickness, float degrees)
    {
        var (sin, cos) = MathF.SinCos(degrees * (MathF.PI / 180));
        UiVector2 Rotate(float x, float y) => new(center.X + x * cos - y * sin, center.Y + x * sin + y * cos);
        var tip = Rotate(0, 2);
        context.Line(Rotate(-4, -2), tip, color, thickness);
        context.Line(tip, Rotate(4, -2), color, thickness);
    }
}

public readonly partial struct UiControlContext
{
    internal float SampleStateMotion(bool active, bool secondary = false)
        => MotionKey is { } key ? _ui.SampleControlMotion(key, active, secondary) : active ? 1 : 0;

    internal void PaintDisclosure(UiVector2 center, UiColor color, bool expanded, float collapsedDegrees, float expandedDegrees = 0)
    {
        var angle = collapsedDegrees + (expandedDegrees - collapsedDegrees) * SampleStateMotion(expanded);
        UiDisclosureMotion.Paint(this, center, color, 1.5f, angle);
    }
}

public sealed partial class UiImmediateContext
{
    private struct ControlMotionTrack
    {
        internal UiFloatTransition Transition;
        internal UiFloatTransition Secondary;
        internal int LastFrame;
    }

    private Dictionary<string, ControlMotionTrack>? _controlMotion;
    private int _controlMotionSweepFrame;

    internal float SampleDisclosure(string id, bool expanded, float collapsedDegrees, float expandedDegrees)
        => collapsedDegrees + (expandedDegrees - collapsedDegrees) * SampleControlMotion(id, expanded);

    internal float SampleControlMotion(string id, bool active, bool secondary = false)
    {
        _controlMotion ??= new(StringComparer.Ordinal);
        var frame = GetFrameCount();
        if (unchecked((uint)(frame - _controlMotionSweepFrame)) >= 256)
        {
            foreach (var track in _controlMotion)
                if (unchecked((uint)(frame - track.Value.LastFrame)) >= 256)
                    _controlMotion.Remove(track.Key);
            _controlMotionSweepFrame = frame;
        }
        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_controlMotion, id, out _);
        entry.LastFrame = frame;
        ref var transition = ref (secondary ? ref entry.Secondary : ref entry.Transition);
        return AnimateFloat(ref transition, active ? 1 : 0, UiDisclosureMotion.Timing());
    }
}
