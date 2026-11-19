using Duxel.Core;

internal static class DialogMotionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var ime = new Ime();
        context.SetImeHandler(ime);
        var raster = new RasterProbe();
        var text = "";
        var clicks = 0;
        var childClicks = 0;
        var child = Fluent.Popover("Child portal", Fluent.Button("Child action", () => childClicks++), options: new(MotionEnabled: false));
        using var dialog = Fluent.Dialog("Moving dialog", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Input("Moving name", new UiBinding<string>(() => text, value => text = value), 100, 220), raster, child),
            [Fluent.Button("Motion action", () => clicks++)]);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        void Frame(float delta = 0, UiInputState? input = null)
        {
            context.NewFrame(new(delta, new(800, 600), new(1, 1)), input ?? idle, new(0, 0, 800, 600), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); dialog.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityNode Find(string name, UiAccessibilityRole role) =>
            tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == name && x.Node.Role == role).Node;
        void Click(UiRect bounds)
        {
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(input: idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(input: idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
            Frame();
        }
        Frame();
        dialog.Show();
        Frame();
        check(dialog.Options.MotionEnabled && MathF.Abs(Find("Moving dialog", UiAccessibilityRole.Dialog).Bounds.Width - 510) < .01f,
            "Default Dialog entry starts at85 percent scale with mapped accessible geometry");
        check(context.State.NextScheduledFrameTimeSeconds is not null && dialog.Input.SurfacePaint is not null,
            "Moving Dialog owns a finite paint group and next frame deadline");
        Frame();
        var nameBounds = Find("Moving name", UiAccessibilityRole.Edit).Bounds;
        check(MathF.Abs(ime.Bounds.X - nameBounds.X) < .01f && MathF.Abs(ime.Bounds.Width - nameBounds.Width) < .01f,
            "Dialog entry maps IME input geometry to the same displayed bounds as accessibility");
        var initialFontHeight = ime.FontHeight;
        Frame(.125f);
        check(raster.Resolution == 1 && ime.FontHeight > initialFontHeight,
            "Dialog entry keeps cached raster resolution fixed while scaling native IME font geometry");
        var middle = Find("Moving dialog", UiAccessibilityRole.Dialog).Bounds;
        check(middle.Width > 510 && middle.Width < 600 && MathF.Abs(middle.X * 2 + middle.Width - 800) < .01f,
            "Dialog entry scales around its center without changing the natural layout");
        Click(Find("Moving name", UiAccessibilityRole.Edit).Bounds);
        ime.Pending = "X";
        Frame();
        check(text == "X", "Displayed moving editor receives keyboard text at its mapped position");
        ime.Pending = "정";
        Frame();
        check(text == "X정", "Moving editor consumes Korean composition through its mapped IME owner");
        child.Show();
        Frame();
        Frame();
        Click(Find("Child action", UiAccessibilityRole.Button).Bounds);
        check(childClicks == 1 && child.IsOpen && dialog.IsOpen,
            "A child portal escapes Dialog entry coordinates and retains real screen-space input");
        child.Close();
        Frame();
        Click(Find("Motion action", UiAccessibilityRole.Button).Bounds);
        check(clicks == 1, "Moving Dialog action activates once at its displayed coordinates");
        Frame(.125f);
        check(Find("Moving dialog", UiAccessibilityRole.Dialog).Bounds.Width == 600
            && dialog.Input.SurfacePaint is not null && dialog.Input.MotionNextAt is null,
            "Dialog entry settles at natural size and retains only its reusable exit snapshot");
        var settled = requests;
        Frame();
        Frame();
        check(requests == settled && context.State.NextScheduledFrameTimeSeconds is null,
            "Settled Dialog entry leaves no animation deadline or periodic frame request");
        dialog.Close();
        Frame();
        dialog.Show();
        Frame();
        dialog.Options = dialog.Options with
        {
            MotionEnabled = false
        };
        Frame();
        check(Find("Moving dialog", UiAccessibilityRole.Dialog).Bounds.Width == 600 && dialog.Input.SurfacePaint is null,
            "Disabling Dialog motion mid-entry immediately restores natural geometry and releases motion resources");
    }

    private sealed class RasterProbe : UiControlWidget
    {
        internal float Resolution;
        protected override UiVector2 MeasureContent(UiControlContext context) => new(100, 16);
        protected override void RenderControl(UiImmediateContext ui)
        {
            Resolution = ui.WidgetRasterScale;
            ui.Text("Raster probe");
        }
    }

    private sealed class Ime : IUiImeHandler
    {
        internal UiRect Bounds;
        internal float FontHeight;
        internal string? Owner, Pending;
        public string? GetCompositionText() => null;
        public void SetCompositionOwner(string? inputId) => Owner = inputId;
        public string? ConsumeCommittedText(string inputId)
        {
            if (Owner != inputId)
                return null;
            var value = Pending;
            Pending = null;
            return value;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
            Bounds = inputRect;
            FontHeight = fontPixelHeight;
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
