using Duxel.Core;

internal static class DrawerTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        VerifyFractionalSurfaceHeight(context, check);
        var memo = new UiState<string>("Retained text");
        var saved = 0;
        var outside = 0;
        using var drawer = Fluent.Drawer("Details", Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            [Fluent.Input("Memo", memo, 4096, 220), .. Enumerable.Range(0, 60).Select(i => Fluent.Body1("Content " + i)).ToArray()]),
            [Fluent.Button("Save", () => saved++), Fluent.Button("Cancel")], new(MotionEnabled : false));
        using var trigger = Fluent.Button("Open drawer", drawer.Show);
        using var background = Fluent.Button("Outside", () => outside++);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var renderDrawer = true;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 640), new(1, 1)), input ?? idle, new(0, 0, 640, 640), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                trigger.Render(ui);
                ui.SetCursorScreenPos(new(400, 40));
                background.Render(ui);
                ui.SetCursorScreenPos(new(0, 100));
                if (renderDrawer) drawer.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        bool SurfaceExists() => tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "Details" && element.Node.Role is UiAccessibilityRole.Dialog or UiAccessibilityRole.Group);
        void Click(string name, UiAccessibilityRole role)
        {
            var rect = Find(name, role).Node.Bounds;
            var point = new UiVector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        }
        void Key(UiKey key) => Frame(idle with { KeyEvents = [new(key, true, default)] });
        Frame();
        check(!drawer.IsOpen && !SurfaceExists(), "Drawer starts closed without publishing an accessibility surface");
        Click("Open drawer", UiAccessibilityRole.Button);
        Frame();
        var surface = Find("Details", UiAccessibilityRole.Dialog).Node;
        check(drawer.IsOpen && surface.Bounds == new UiRect(0, 0, 320, 640), "Drawer small start overlay occupies the viewport edge at its standard extent");
        check(tree.CaptureSnapshot().Elements.Count(element => element.Node.Name == "Details" && element.Node.Role == UiAccessibilityRole.Label) == 1,
            "Shared surface header exposes its title exactly once");
        check(Find("Close", UiAccessibilityRole.Button).Node.Focused, "Drawer overlay focuses its visible close control on opening");
        Click("Outside", UiAccessibilityRole.Button);
        check(outside == 0 && !drawer.IsOpen, "Drawer modal light-dismiss blocks background activation and closes after a matching backdrop click");
        Click("Open drawer", UiAccessibilityRole.Button);
        Frame();
        var title = Find("Details", UiAccessibilityRole.Label).Node.Bounds;
        var save = Find("Save", UiAccessibilityRole.Button).Node.Bounds;
        var viewport = tree.CaptureSnapshot().Elements.First(element => element.Node.Scroll is not null && element.Node.Bounds.Y > title.Y).Node.Bounds;
        Frame(idle with { MousePosition = new(viewport.X + 10, viewport.Y + 10), MouseWheel = -8 });
        Frame();
        check(drawer.Body.Content.Offset.Y > 0 && drawer.Viewport.Offset.Y == 0
            && Find("Details", UiAccessibilityRole.Label).Node.Bounds == title && Find("Save", UiAccessibilityRole.Button).Node.Bounds == save,
            "Drawer scrolls its body while retaining fixed title and footer slots");
        Click("Save", UiAccessibilityRole.Button);
        check(saved == 1 && drawer.IsOpen, "Drawer footer action remains operable without implicit dismissal");
        Click("Close", UiAccessibilityRole.Button);
        Frame();
        check(!drawer.IsOpen && context.State.FocusedId == Find("Open drawer", UiAccessibilityRole.Button).Node.Id,
            "Drawer close restores its invoking control focus");
        check(memo.Value == "Retained text", "Drawer close preserves its owned input model");
        foreach (var position in Enum.GetValues<UiDrawerPosition>())
        {
            drawer.Options = drawer.Options with { Position = position };
            drawer.Show();
            Frame();
            Frame();
            var bounds = Find("Details", UiAccessibilityRole.Dialog).Node.Bounds;
            check(position switch
            {
                UiDrawerPosition.Start => bounds == new UiRect(0, 0, 320, 640),
                UiDrawerPosition.End => bounds == new UiRect(320, 0, 320, 640),
                _ => bounds == new UiRect(0, 320, 640, 320)
            }, "Drawer resolves actual start/end/bottom surface geometry : " + position);
            drawer.Close();
            Frame();
        }
        drawer.Options = drawer.Options with { Position = UiDrawerPosition.Start, RightToLeft = true };
        drawer.Show();
        Frame();
        check(Find("Details", UiAccessibilityRole.Dialog).Node.Bounds.X == 320, "Drawer RTL reverses logical start placement");
        drawer.Close();
        Frame();
        drawer.Options = drawer.Options with { Type = UiDrawerType.Inline, InlineCrossExtent = 400, RightToLeft = false, Separator = true };
        drawer.Show();
        Frame();
        check(Find("Details", UiAccessibilityRole.Group).Node.Bounds == new UiRect(0, 100, 320, 400),
            "Drawer inline surface consumes its assigned layout space without becoming a dialog");
        Click("Outside", UiAccessibilityRole.Button);
        check(outside == 1 && drawer.IsOpen, "Drawer inline surface permits independent background interaction");
        drawer.Close();
        Frame();
        drawer.Options = drawer.Options with { Type = UiDrawerType.Overlay, Modal = false };
        drawer.Show();
        Frame();
        Click("Outside", UiAccessibilityRole.Button);
        check(outside == 2 && !drawer.IsOpen && context.State.FocusedId == Find("Outside", UiAccessibilityRole.Button).Node.Id,
            "Drawer nonmodal light-dismiss preserves the background action and its new focus");
        drawer.Options = drawer.Options with { Modal = true, LightDismiss = false };
        drawer.Show();
        Frame();
        Click("Outside", UiAccessibilityRole.Button);
        check(drawer.IsOpen && outside == 2, "Drawer modal light-dismiss option preserves background isolation");
        Key(UiKey.Escape);
        check(!drawer.IsOpen, "Drawer focused Escape dismisses its overlay");
        drawer.Show();
        Frame();
        renderDrawer = false;
        Frame();
        check(!drawer.IsOpen && !SurfaceExists(), "Drawer owner removal retires its overlay and accessibility subtree");
        RunInlineLayout(check);
    }
    private static void RunInlineLayout(Action<bool, string> check)
    {
        foreach (var position in Enum.GetValues<UiDrawerPosition>())
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            ControlFontFixture.Configure(context);
            var open = new UiState<bool>(false);
            var inline = Fluent.Drawer("Inline motion", Fluent.Body1("Unscaled content"),
                options : new(Type : UiDrawerType.Inline, Position : position, Extent : 200, InlineCrossExtent : 200));
            inline.Open = open.Binding;
            var marker = Fluent.Button("After inline");
            using var root = position == UiDrawerPosition.Bottom
                ? (UiWidget)Dux.Column(new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min), inline, marker)
                : Dux.Row(new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min), inline, marker);
            UiImmediateContext? uiContext = null;
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Frame()
            {
                context.NewFrame(new(1f / 60, new(800, 600), new(1, 1)), idle, new(0, 0, 800, 600), UiTextSettings.Default);
                context.Render(new Screen(ui => { uiContext = ui; ui.EnableRootViewportContentLayout(contentPadding : 0); root.Render(ui); }));
                using var draw = context.GetDrawData();
            }
            float Marker()
            {
                var bounds = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "After inline" && element.Node.Role == UiAccessibilityRole.Button).Node.Bounds;
                return position == UiDrawerPosition.Bottom ? bounds.Y : bounds.X;
            }
            Frame();
            check(Marker() == 0, "inline closed allocation leaves no sibling gap : " + position);
            open.Value = true;
            Frame();
            context.State.AdvanceTime(.12f);
            Frame();
            check(Marker() is > 0 and < 200, "inline motion reflows the actual sibling through an intermediate extent : " + position);
            var before = inline.Input.InlineMotion.Sample(true, true, context.State.TimeSeconds);
            var deadline = context.State.NextScheduledFrameTimeSeconds;
            inline.Measure(uiContext!, new(800, 600));
            check(inline.IsOpen && open.Value && before == inline.Input.InlineMotion.Sample(true, true, context.State.TimeSeconds)
                && deadline == context.State.NextScheduledFrameTimeSeconds,
                "inline pure measurement does not retarget or schedule its timeline : " + position);
            context.State.AdvanceTime(.3f);
            Frame();
            check(Marker() == 200, "inline settled allocation reserves its exact declared extent : " + position);
            open.Value = false;
            Frame();
            context.State.AdvanceTime(.12f);
            Frame();
            check(!inline.IsOpen && Marker() is > 0 and < 200
                && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "Inline motion" && element.Node.Role == UiAccessibilityRole.Group),
                "inline close releases layout gradually without retaining interactive content : " + position);
            open.Value = true;
            Frame();
            check(inline.IsOpen && Marker() is > 0 and < 200, "inline reversal preserves its intermediate sibling placement : " + position);
            context.State.AdvanceTime(.3f);
            Frame();
            inline.Options = inline.Options with { MotionEnabled = false };
            open.Value = false;
            Frame();
            check(!inline.IsOpen && Marker() == 0, "inline motion-disabled close immediately releases its allocation : " + position);
        }
    }
    private static void VerifyFractionalSurfaceHeight(UiContext context, Action<bool, string> check)
    {
        using var body = new UiSurfaceBody<FractionalSurfacePresentation>("Fractional drawer",
            new ScrollViewTests.Probe(new(270, 2000)), [new ScrollViewTests.Probe(new(70, 26.415094f))], static () => { });
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.NewFrame(new(1f / 60, new(1024, 900), new(1, 1)), input, new(0, 0, 1024, 900), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            var constraints = new UiBoxConstraints(270, 270, 0, float.PositiveInfinity);
            body.Configure(false, false, false, "Close", 850, fillHeight : true);
            check(body.MeasureConstrained(ui, constraints).Y == 850,
                "a drawer that fills its viewport reports the exact height even when chrome sizes are fractional");
            body.Configure(false, false, false, "Close", 40, fillHeight : true);
            check(body.MeasureConstrained(ui, constraints).Y > 40,
                "a drawer still reports overflow when its header, footer and gaps exceed the height limit");
        }));
        using var draw = context.GetDrawData();
    }

    private readonly struct FractionalSurfacePresentation : IUiSurfacePresentation
    {
        public static UiDialogMetrics Metrics => UiFluentDrawerPresentation.Metrics;
        public static UiVector2 MeasureTitle(UiControlContext context, string title) => new(20, 24.619719f);
        public static UiControlTypographyScope BeginBodyText(UiControlContext context) => default;
        public static void PaintTitle(UiControlContext context, string title, UiColor? color) { }
        public static UiBoxShadowOptions Shadow(UiControlContext context) => default;
        public static UiColor Background(UiControlContext context) => default;
        public static UiColor Foreground(UiControlContext context) => default;
        public static UiColor Backdrop(UiControlContext context) => default;
        public static void PaintClose(UiControlContext context, UiRect bounds, UiButtonInteraction interaction) { }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
