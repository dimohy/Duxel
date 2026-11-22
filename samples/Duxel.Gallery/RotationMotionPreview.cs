using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class RotationMotionPreview : UiWidget
{
    private readonly UiState<bool> _reducedMotion = new(false);
    private bool _rotated;
    private const float Duration = .9f;

    protected override IUiView Build(UiBuildContext context) => context.Child("rotation", () =>
        Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Subtitle2(GalleryText.Get("Toggle rotation")),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button(GalleryText.Get("Rotate marker"), () => _rotated = !_rotated),
                Fluent.Switch(GalleryText.Get("Reduced motion"), _reducedMotion.Binding)),
            new Track(this)));

    private sealed class Track(RotationMotionPreview owner) : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => new(48, 44);

        protected override void RenderControl(UiImmediateContext ui)
        {
            var origin = ui.GetCursorScreenPos();
            var angle = ui.AnimateToggleRotationDegrees("marker", owner._rotated, expandedDegrees : 180,
                durationSeconds : owner._reducedMotion.Value ? 0 : Duration) * (MathF.PI / 180);
            var center = new UiVector2(origin.X + 22, origin.Y + 22);
            var direction = new UiVector2(MathF.Cos(angle) * 15, MathF.Sin(angle) * 15);
            ui.GetWindowDrawList().AddLine(center, new(center.X + direction.X, center.Y + direction.Y),
                new(66, 149, 237), 4, ui.WhiteTextureId);
            ui.Dummy(new(48, 44));
        }
    }
}
