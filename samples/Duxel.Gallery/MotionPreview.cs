using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MotionPreview : UiWidget
{
    private readonly UiState<bool> _reducedMotion = new(false);
    private readonly UiState<bool> _overshoot = new(false);
    private readonly UiState<bool> _lowerFrameRate = new(false);
    private readonly UiState<float> _duration = new(.9f);
    private bool _atEnd;
    private int _reset;

    protected override IUiView Build(UiBuildContext context) => context.Child("motion", () =>
        Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
            Fluent.Subtitle2(GalleryText.Get("Timing curves")),
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8, CrossAlignment : UiWrapCrossAlignment.Center),
                Fluent.Button(GalleryText.Get("Move or reverse"), () => _atEnd = !_atEnd),
                Fluent.Button(GalleryText.Get("Reset animations"), () => { _atEnd = false; _reset++; }),
                Fluent.Switch(GalleryText.Get("Reduced motion"), _reducedMotion.Binding)),
            Fluent.Caption1(() => string.Format(GalleryText.Get("Duration: {0:0.00} s"), _duration.Value)),
            Fluent.Slider(GalleryText.Get("Animation duration (seconds)"), _duration.Binding, 0, 2, .05f),
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8),
                Fluent.Switch(GalleryText.Get("30 Hz frame requests"), _lowerFrameRate.Binding),
                Fluent.Switch(GalleryText.Get("Bezier overshoot"), _overshoot.Binding)),
            Fluent.Caption1(GalleryText.Get("Linear")),
            new Track(this, UiAnimationEasing.Linear),
            Fluent.Caption1(GalleryText.Get("Ease-in-out sine")),
            new Track(this, UiAnimationEasing.InOutSine),
            Fluent.Caption1(GalleryText.Get("Ease-out cubic")),
            new Track(this, UiAnimationEasing.OutCubic),
            Fluent.Caption1(GalleryText.Get("Custom cubic Bezier")),
            new Track(this, UiAnimationEasing.Linear, custom : true)));

    private sealed class Track(MotionPreview owner, UiAnimationEasing easing, bool custom = false) : UiControlWidget
    {
        private UiFloatTransition _transition = new(0);
        private int _reset;
        protected override UiVector2 MeasureContent(UiControlContext context) =>
            new(MathF.Min(560, context.AvailableWidth), 26);

        protected override void RenderControl(UiImmediateContext ui)
        {
            var width = MathF.Max(0, MathF.Min(560, ui.GetContentRegionAvail().X));
            var position = ui.GetCursorScreenPos();
            if (_reset != owner._reset)
            {
                _reset = owner._reset;
                _transition.Reset(0);
            }
            var curve = custom ? owner._overshoot.Value ? new UiCubicBezier(.3, -.3, .55, 1.5) : new(.25, .1, .25, 1) : (UiCubicBezier?)null;
            var value = ui.AnimateFloat(ref _transition, owner._atEnd ? 1 : 0,
                new(owner._duration.Value, easing, owner._reducedMotion.Value, 1d / (owner._lowerFrameRate.Value ? 30 : 60))
                { Curve = curve });
            var draw = ui.GetWindowDrawList();
            var theme = ui.GetStyle();
            var margin = custom ? width * .12f : 0;
            draw.AddRectFilled(new(position.X + margin, position.Y + 12, width - margin * 2, 2), theme.Text, ui.WhiteTextureId);
            var marker = MathF.Min(width, 18);
            draw.AddRectFilled(new(position.X + margin + value * MathF.Max(0, width - marker - margin * 2), position.Y + 4, marker, 18),
                new(66, 149, 237), ui.WhiteTextureId);
            ui.Dummy(new(width, 26));
        }
    }
}
