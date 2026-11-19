using Duxel.Core;

internal static class WidgetTransformPortalRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int width)
    {
        using var popup = new UiPopover<Surface>("Portal", new Content(), new(Width: 100, MotionEnabled: false,
            Positioning: new()
            {
                Position = UiPopupPosition.BelowStart
            }));
        popup.Transform = new(RotationDegrees: 90);
        popup.Show();
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(40, 40));
            popup.Render(ui);
        }
        var pixels = capture("screen-portal", Draw);
        var expected = UiPopupPlacement.Arrange(new(40, 40, 30, 80), new(100, 38), new(0, 0, width, 240), popup.Options.Positioning).Bounds;
        check(Surface.Bounds == expected, "GPU Popover arranges at the transformed screen trigger");
        bool Red(int x, int y) => pixels[(y * width + x) * 4] == 255 && pixels[(y * width + x) * 4 + 1] == 0;
        var x = (int)expected.X + 4;
        var y = (int)expected.Y + 4;
        check(Red(x + 2, y + 2) && Red(x + 77, y + 27) && !Red(x + 2, y + 40),
            "GPU popup content stays upright and escapes the transformed trigger allocation clip");
        check(pixels.AsSpan().SequenceEqual(capture("screen-portal-replay", Draw)),
            "GPU screen portal replays exact pixels with its transformed owner");
    }
    private sealed class Content : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => new(80, 30);
        protected override void RenderControl(UiImmediateContext ui)
        {
            var position = ui.GetCursorScreenPos();
            ui.Dummy(new(80, 30));
            new UiControlContext(ui).Frame(new(position.X, position.Y, 80, 30), new(255, 0, 0), default, 0, 0);
        }
    }
    private readonly struct Surface : IUiPopoverPresentation
    {
        internal static UiRect Bounds;
        public static UiControlTypographyScope BeginTriggerText(UiControlContext context) => default;
        public static UiControlTypographyScope BeginBodyText(UiControlContext context) => default;
        public static UiVector2 MeasureTrigger(UiControlContext context, string label) => new(80, 30);
        public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction) =>
            context.Frame(bounds, new(0, 255, 0), default, 0, 0);
        public static float Padding(UiPopoverOptions options) => 4;
        public static UiColor Foreground(UiControlContext context, UiPopoverOptions options) => new(255, 255, 255);
        public static void PaintSurface(UiControlContext context, UiPopupSurface surface, UiPopupLayout layout, UiPopoverOptions options)
        {
            Bounds = layout.Bounds;
            context.Frame(Bounds, new(0, 0, 255), default, 0, 0);
        }
    }
}
