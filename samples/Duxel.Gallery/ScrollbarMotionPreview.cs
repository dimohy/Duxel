using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ScrollbarMotionPreview : UiWidget
{
    private readonly UiState<bool> _reducedMotion = new(false);
    private int _scrollOffset;

    protected override IUiView Build(UiBuildContext context) => context.Child("scrollbar", () =>
        Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Subtitle2(GalleryText.Get("Scrollbar fade")),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button(GalleryText.Get("Reveal scrollbar"), () => _scrollOffset++),
                Fluent.Switch(GalleryText.Get("Reduced motion"), _reducedMotion.Binding)),
            new Track(this)));

    private sealed class Track(ScrollbarMotionPreview owner) : UiControlWidget
    {
        private UiScrollbarMotion _motion;
        protected override UiVector2 MeasureContent(UiControlContext context) => new(146, 30);

        protected override void RenderControl(UiImmediateContext ui)
        {
            var start = ui.GetCursorScreenPos();
            var hovering = ui.IsMouseHoveringRect(start, new(start.X + 146, start.Y + 30));
            var frame = _motion.Update(owner._scrollOffset, false, hovering, ui.GetTime(),
                new(.5, ReducedMotion : owner._reducedMotion.Value));
            if (frame.NextFrameTimeSeconds is { } deadline) ui.RequestFrameAt(deadline);
            ui.GetWindowDrawList().AddRectFilled(new(start.X, start.Y + 8, 146, 12),
                new(66, 149, 237, (byte)Math.Round(255 * frame.Opacity)), ui.WhiteTextureId);
            ui.Dummy(new(146, 30));
        }
    }
}
