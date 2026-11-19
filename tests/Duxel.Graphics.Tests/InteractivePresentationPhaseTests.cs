using Duxel.Core;

internal static class InteractivePresentationPhaseTests
{
    private static readonly UiRect Clip = new(0, 0, 500, 400);
    private static readonly UiFrameInfo Frame = new(.016f, new(500, 400), new(1, 1));
    private static readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

    internal static void Run(Action<bool, string> check)
    {
        foreach (var kind in new[] { "margin", "layout-rotation", "visual-rotation", "scale", "parent-rotation", "scroll" })
            foreach (var reverse in new[] { false, true })
                RunButton(check, kind, reverse);
        foreach (var editor in new[] { "Input", "Textarea", "Markdown" })
            foreach (var rotate in new[] { false, true })
                foreach (var reverse in new[] { false, true })
                    RunEditor(check, editor, rotate, reverse);
    }

    private static void RunButton(Action<bool, string> check, string kind, bool reverse)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var clicks = 0;
        var moves = 0;
        var prematureClicks = -1;
        using var target = Fluent.Button("Target", () => clicks++);
        target.Size = new(80, 30);
        using var parent = kind == "parent-rotation"
            ? Dux.Column(new(Spacing: 0, MainAxisSize: UiMainAxisSize.Min), target)
            : null;
        using var scroll = kind == "scroll"
            ? Dux.ScrollView(Dux.Column(new(Spacing: 0, MainAxisSize: UiMainAxisSize.Min),
                Dux.Container(Fluent.Body1("Before"), new(Width: 80, Height: 200)), target,
                Dux.Container(Fluent.Body1("After"), new(Width: 80, Height: 500))), new(Width: 120, Height: 100))
            : null;
        UiWidget root = (UiWidget?)parent ?? (UiWidget?)scroll ?? target;
        using var move = Fluent.Button("Move", () =>
        {
            moves++;
            switch (kind)
            {
                case "margin":
                    target.Margin = new(100, 40, 0, 0);
                    break;
                case "layout-rotation":
                    target.Transform = new(RotationDegrees: 90);
                    break;
                case "visual-rotation":
                    target.Transform = new(RotationDegrees: 90, Origin: new(0, 0), Mode: UiWidgetTransformMode.Visual);
                    break;
                case "scale":
                    target.Transform = new(Scale: 2);
                    break;
                case "parent-rotation":
                    parent!.Transform = new(RotationDegrees: 90);
                    break;
                case "scroll":
                    scroll!.Offset = new(0, 200);
                    break;
            }
        });
        move.Size = new(80, 30);
        var phases = new List<UiRenderPhase>();
        var screen = new Screen(ui =>
        {
            phases.Add(ui.RenderPhase);
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            void DrawMove()
            {
                ui.SetCursorScreenPos(new(10, 10));
                move.Render(ui);
            }
            void DrawTarget()
            {
                ui.SetCursorScreenPos(new(150, 80));
                root.Render(ui);
            }
            if (reverse)
            {
                DrawTarget();
                DrawMove();
            }
            else
            {
                DrawMove();
                DrawTarget();
            }
            if (phases.Count == 4)
                prematureClicks = clicks;
        });
        context.NewFrame(Frame, Idle, Clip, UiTextSettings.Default);
        context.Render(screen);
        using (context.GetDrawData())
        {
        }
        var originalId = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Target").Node.Id;
        phases.Clear();
        var (point, expected) = kind switch
        {
            "margin" => (new UiVector2(290, 135), new UiRect(250, 120, 80, 30)),
            "visual-rotation" => (new UiVector2(135, 120), new UiRect(120, 80, 30, 80)),
            "scale" => (new UiVector2(290, 110), new UiRect(150, 80, 160, 60)),
            "scroll" => (new UiVector2(190, 95), new UiRect(150, 80, 80, 30)),
            _ => (new UiVector2(165, 120), new UiRect(150, 80, 30, 80))
        };
        // Fixed coordinates are independent of the accessibility geometry under test.
        var rejectsOldPoint = kind is not ("scale" or "scroll");
        var oldPoint = new UiVector2(190, 95);
        UiInputState[] steps = [Pointer(new(20, 20), true), Pointer(new(20, 20), false),
            rejectsOldPoint ? Pointer(oldPoint, true) : Idle,
            rejectsOldPoint ? Pointer(oldPoint, false) : Idle,
            Pointer(point, true), Pointer(point, false), Idle];
        context.NewInputBatch(Frame, steps, Clip, UiTextSettings.Default);
        context.Render(screen);
        using var drawing = context.GetDrawData();
        var node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Target").Node;
        var prefix = $"Interactive phase : {kind}, reverse={reverse}";
        check(moves == 1 && prematureClicks == 0 && clicks == 1,
            prefix + $" / real callback moves once; stale point rejected and new point activates once / {moves},{prematureClicks},{clicks}");
        check(node.Bounds == expected && node.Id == originalId && context.State.FocusedId == node.Id,
            prefix + $" / transformed accessibility bounds, identity and pointer focus / actual={node.Bounds}, expected={expected}");
        check(phases.Count == 7 && phases.Take(6).All(p => p == UiRenderPhase.Input) && phases[6] == UiRenderPhase.Presentation
            && context.InputProcessingMetrics is { Completed: true, CompletedSteps: 7, BuiltDrawings: 1 },
            prefix + " / seven real event traversals produce one final presentation");
        check(drawing.DrawLists.Any(list => list.Commands.Count > 0) && (scroll is null || scroll.Offset == new UiVector2(0, 200)),
            prefix + " / final drawing remains nonempty and requested scroll offset is consumed");
    }

    private static void RunEditor(Action<bool, string> check, string kind, bool rotate, bool reverse)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var source = "a";
        var binding = new UiBinding<string>(() => source, value => source = value);
        using UiWidget editor = kind switch
        {
            "Input" => Fluent.Input(string.Empty, binding, 100, 80),
            "Textarea" => Fluent.Textarea(string.Empty, binding, 100, 80, 60),
            "Markdown" => new UiMarkdownEditor("Interactive Markdown", binding, new() { Width = 80, Height = 60 }),
            _ => throw new InvalidOperationException("Unknown interactive editor.")
        };
        var moves = 0;
        using var move = Fluent.Button("Move", () =>
        {
            moves++;
            if (rotate)
                editor.Transform = new(RotationDegrees: 90);
            else
                editor.Margin = new(100, 40, 0, 0);
        });
        move.Size = new(80, 30);
        var renders = 0;
        var sourceBeforeNewClick = string.Empty;
        string? focusBeforeNewClick = null;
        var screen = new Screen(ui =>
        {
            renders++;
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            void DrawMove()
            {
                ui.SetCursorScreenPos(new(10, 10));
                move.Render(ui);
            }
            void DrawEditor()
            {
                ui.SetCursorScreenPos(new(150, 80));
                editor.Render(ui);
            }
            if (reverse)
            {
                DrawEditor();
                DrawMove();
            }
            else
            {
                DrawMove();
                DrawEditor();
            }
            if (renders == 4)
            {
                sourceBeforeNewClick = source;
                focusBeforeNewClick = context.State.FocusedId;
            }
        });
        context.NewFrame(Frame, Idle, Clip, UiTextSettings.Default);
        context.Render(screen);
        using (context.GetDrawData())
        {
        }
        renders = 0;
        var point = !rotate ? new UiVector2(290, 135)
            : kind == "Input" ? new UiVector2(167, 120) : new UiVector2(195, 120);
        UiInputState[] steps = [Pointer(new(20, 20), true), Pointer(new(20, 20), false),
            Pointer(new(225, 90), true), Pointer(new(225, 90), false),
            Pointer(point, true), Pointer(point, false), Idle with { CharEvents = [new('X')] }, Idle];
        context.NewInputBatch(Frame, steps, Clip, UiTextSettings.Default);
        context.Render(screen);
        using var drawing = context.GetDrawData();
        var prefix = $"Interactive editor : {kind}, rotate={rotate}, reverse={reverse}";
        check(moves == 1 && sourceBeforeNewClick == "a" && source == "aX",
            prefix + $" / moved real editor consumes character at inverse-mapped caret / source={source}");
        check(!string.IsNullOrEmpty(context.State.FocusedId) && context.State.FocusedId != focusBeforeNewClick,
            prefix + " / stale point does not focus the moved editor; new point does");
        check(renders == 8 && context.InputProcessingMetrics is { Completed: true, CompletedSteps: 8, BuiltDrawings: 1 }
            && drawing.DrawLists.Any(list => list.Commands.Count > 0),
            prefix + " / eight editor traversals publish one nonempty drawing");
    }

    private static UiInputState Pointer(UiVector2 point, bool press) => Idle with
    {
        MousePosition = point,
        LeftMouseDown = press,
        LeftMousePressed = press,
        LeftMouseReleased = !press
    };

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
