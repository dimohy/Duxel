using System.Numerics;

namespace Duxel.Core;

internal readonly record struct UiOverlayMotionSample(float Scale, float Opacity, float BackdropOpacity, UiVector2 Translation = default);

/// <summary>Owns frozen GPU paint after interactive overlay ownership has ended.</summary>
internal sealed class UiOverlayExitPaint(UiOverlayInputState owner, UiPaintGroup paint, UiRect body,
    UiColor backdrop, int tier, UiOverlayMotionSample initial, double duration, float targetScale,
    UiCubicBezier surfaceCurve, UiCubicBezier backdropCurve, UiVector2 targetTranslation = default) : IDisposable
{
    internal UiOverlayInputState Owner => owner;
    internal UiPaintGroup Paint => paint;
    internal UiRect Body => body;
    internal UiColor Backdrop => backdrop;
    internal int Tier => tier;
    private double? _started;
    internal void Start(double now) => _started ??= now;
    internal bool Complete(double now) => _started is { } start && now >= start + duration;
    internal double Next(double now) => Math.Min(_started!.Value + duration, now + 1d / 60);
    internal UiOverlayMotionSample Sample(double now)
    {
        var progress = _started is { } start ? Math.Clamp((now - start) / duration, 0, 1) : 0;
        var surface = (float)surfaceCurve.Evaluate(progress);
        return new(initial.Scale + (targetScale - initial.Scale) * surface,
            initial.Opacity * (1 - surface), initial.BackdropOpacity * (1 - (float)backdropCurve.Evaluate(progress)),
            new(initial.Translation.X + (targetTranslation.X - initial.Translation.X) * surface,
                initial.Translation.Y + (targetTranslation.Y - initial.Translation.Y) * surface));
    }
    public void Dispose() => paint.Dispose();
}

public sealed partial class UiImmediateContext
{
    private List<UiOverlayExitPaint>? _overlayExits;

    internal void PrepareDialogOpening(UiDialogInputState input)
    {
        input.EntryFrom = ResolveOverlayEntry(input, new(.85f, 0, 0));
    }
    private UiOverlayMotionSample ResolveOverlayEntry(UiOverlayInputState input, UiOverlayMotionSample initial)
    {
        if (_overlayExits is null)
            return initial;
        foreach (var exit in _overlayExits)
            if (ReferenceEquals(exit.Owner, input))
                return exit.Sample(_state.TimeSeconds);
        return initial;
    }
    internal void PrepareDialogClosing(UiDialogInputState input)
    {
        if (!input.MotionEnabled || input.SurfacePaint is null)
            return;
        PrepareOverlaySurfaceExit(input, ref input.SurfacePaint, input.NaturalBounds, input.BackdropColor,
            input.LastMotion, UiDialogMotion.DurationSeconds, .85f, UiDialogMotion.Exit, UiDialogMotion.Backdrop);
    }
    private void PrepareOverlaySurfaceExit(UiOverlayInputState input, ref UiPaintGroup? surfacePaint,
        UiRect naturalBounds, UiColor backdrop, UiOverlayMotionSample initial, double duration, float targetScale,
        UiCubicBezier surfaceCurve, UiCubicBezier backdropCurve)
    {
        var paint = surfacePaint!;
        surfacePaint = null;
        if (initial.Opacity <= 0 || paint.For(this).Content is null)
        {
            paint.Dispose();
            return;
        }
        CancelOverlayExit(input);
        (_overlayExits ??= []).Add(new(input, paint, naturalBounds, backdrop, input.Tier,
            initial, duration, targetScale, surfaceCurve, backdropCurve));
    }
    private void CancelOverlayExit(UiOverlayInputState owner)
    {
        if (_overlayExits is null)
            return;
        for (var i = _overlayExits.Count - 1; i >= 0; i--)
            if (ReferenceEquals(_overlayExits[i].Owner, owner))
            {
                _overlayExits[i].Dispose();
                _overlayExits.RemoveAt(i);
            }
    }
    private void ReleaseOverlayExits()
    {
        if (_overlayExits is null)
            return;
        foreach (var exit in _overlayExits)
            exit.Dispose();
        _overlayExits.Clear();
    }
    private int ResolveOverlayTier(UiOverlayInputState input, int parentTier, bool opened)
    {
        var tier = Math.Max(parentTier + 1, (_overlays?.IndexOf(input) ?? 0) + 1);
        if (!opened)
            return Math.Max(tier, input.Tier);
        if (_overlayExits is not null)
            foreach (var exit in _overlayExits)
                tier = Math.Max(tier, checked(exit.Tier + 1));
        if (_overlays is not null)
            foreach (var owner in _overlays)
                if (!ReferenceEquals(owner, input) && owner.Open)
                    tier = Math.Max(tier, checked(owner.Tier + 1));
        return tier;
    }
    private void EndOverlayExitFrame()
    {
        if (_overlayExits is null)
            return;
        for (var i = _overlayExits.Count - 1; i >= 0; i--)
        {
            var exit = _overlayExits[i];
            var now = _state.TimeSeconds;
            if (exit.Owner.SeenFrame != GetFrameCount() || exit.Complete(now))
            {
                exit.Dispose();
                _overlayExits.RemoveAt(i);
                continue;
            }
            exit.Start(now);
            var motion = exit.Sample(now);
            using var screen = BeginWidgetScreenSpace();
            var tier = _popupTierDepth;
            _popupTierDepth = exit.Tier - 1;
            PushPopup();
            PushClipRect(new(0, 0, _displaySize.X, _displaySize.Y), false);
            try
            {
                var color = exit.Backdrop;
                color = new((color.Rgba & 0x00ffffff) | ((uint)MathF.Round((color.Rgba >> 24) * motion.BackdropOpacity) << 24));
                if (color.Rgba >> 24 != 0)
                    AddRectFilled(new(0, 0, _displaySize.X, _displaySize.Y), color, _whiteTexture);
                var binding = exit.Paint.For(this);
                var matrix = Matrix3x2.CreateScale(motion.Scale, new Vector2(exit.Body.X + exit.Body.Width / 2, exit.Body.Y + exit.Body.Height / 2))
                    * Matrix3x2.CreateTranslation(motion.Translation.X, motion.Translation.Y);
                CompositeInteractivePaintGroup(binding, UiWidgetAffineLayout.MapBounds(binding.Content!.Bounds, matrix), motion.Opacity);
                RequestFrameAt(exit.Next(now));
            }
            finally { PopClipRect(); PopPopup(); _popupTierDepth = tier; }
        }
    }
}
