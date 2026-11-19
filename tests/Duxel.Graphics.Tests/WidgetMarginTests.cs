using Duxel.Core;

internal static class WidgetMarginTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var activations = 0;
        using var button = Fluent.Button("margin-button", () => activations++);
        button.Size = new(80, 30);
        button.Margin = new(7, 11, 13, 17);
        using var next = Fluent.Button("next-button");
        next.Size = new(40, 20);
        UiWidget current = button;
        UiVector2 measured = default, following = default, inline = default, itemSize = default;
        var hovered = false;
        var focused = false;
        var renderLeadingButton = false;
        static UiInputState Pointer(UiVector2 point, bool down = false, bool pressed = false, bool released = false) =>
            new(point, down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        var view = new View(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(20, 20));
            if (renderLeadingButton)
            {
                button.Render(ui);
                ui.SetCursorScreenPos(new(140, 20));
            }
            measured = current.MeasureConstrained(ui, new UiBoxConstraints());
            current.Render(ui);
            hovered = ui.IsItemHovered();
            focused = ui.IsItemFocused();
            itemSize = ui.GetItemRectSize();
            following = ui.GetCursorScreenPos();
            ui.SameLine();
            inline = ui.GetCursorScreenPos();
            next.Render(ui);
        });
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(500, 400), new(1, 1)), input ?? Pointer(new(-100, -100)), new(0, 0, 500, 400), UiTextSettings.Default);
            context.Render(view);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityNode Find(string name) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name).Node;
        void Click(UiVector2 point)
        {
            Frame(Pointer(point, down: true, pressed: true));
            Frame(Pointer(point, released: true));
        }
        button.Margin = default;
        Frame();
        var baselineFollowing = following;
        var baselineInline = inline;
        var baselineNextBounds = Find("next-button").Bounds;
        button.Margin = new(7, 11, 13, 17);
        Frame();
        var initial = Find("margin-button");
        check(measured == new UiVector2(100, 58) && itemSize == measured &&
            following.Y - baselineFollowing.Y == 28 && inline.X - baselineInline.X == 20 && inline.Y == baselineInline.Y,
            "common widget margin reserves four edges in pure measurement and vertical/horizontal flow");
        check(initial.Bounds == new UiRect(27, 31, 80, 30) && Find("next-button").Bounds.X - baselineNextBounds.X == 20,
            "button accessibility exposes its content rectangle while the next widget follows the outer margin extent");
        foreach (var point in new[] { new UiVector2(22, 40), new(114, 40), new(50, 24), new(50, 72) })
            Click(point);
        check(activations == 0 && !hovered, "button margin space is neither clickable nor hovered");
        Click(new(67, 46));
        check(activations == 1 && hovered && focused, "margin-wrapped button retains real pointer activation, hover and focus semantics");
        var id = Find("margin-button").Id;
        button.Margin = new(17, 21, 3, 7);
        Frame();
        check(Find("margin-button").Id == id && Find("margin-button").Bounds == new UiRect(37, 41, 80, 30) && focused,
            "changing a widget margin preserves its semantic identity and keyboard focus");
        Frame(new(new(-100, -100), false, false, false, false, false, false, 0, 0,
            [new(UiKey.Space, true, default)], [], default, default));
        Frame(new(new(-100, -100), false, false, false, false, false, false, 0, 0,
            [new(UiKey.Space, false, default)], [], default, default));
        check(activations == 2, "focused margin-wrapped button keeps its Space-key activation contract");
        button.Margin = default;
        Frame();
        check(Find("margin-button").Id == id && measured == new UiVector2(80, 30) && inline == baselineInline,
            "clearing common margins restores intrinsic flow without replacing the button identity");
        using var column = Dux.Column(new(Spacing: 0, MainAxisSize: UiMainAxisSize.Min), button);
        current = column;
        button.Margin = new(7, 11, 13, 17);
        Frame();
        var before = measured;
        button.Margin = new(17, 11, 13, 17);
        Frame();
        check(before == new UiVector2(100, 58) && measured == new UiVector2(110, 58),
            "child margin changes invalidate the actual retained parent measurement chain");
        using var constrained = Fluent.Button("constrained-button");
        constrained.Size = new(80, 30);
        constrained.Margin = new(7, 11, 13, 17);
        using var box = Dux.Container(constrained, new(Width: 70, Height: 40));
        current = box;
        Frame();
        check(measured == new UiVector2(70, 40) && Find("constrained-button").Bounds == new UiRect(27, 31, 50, 12),
            "explicit parent allocation deflates common margins before allocating button content");
        var rejected = 0;
        foreach (var margin in new[] { new UiEdgeInsets(-1, 0, 0, 0), new(float.NaN, 0, 0, 0), new(float.MaxValue, 0, float.MaxValue, 0) })
        {
            try
            {
                constrained.Margin = margin;
            }
            catch (ArgumentOutOfRangeException) { rejected++; }
        }
        check(rejected == 3, "common widget margin rejects negative, nonfinite and overflowing extents before layout");
        check(typeof(UiSvg).GetProperty(nameof(UiWidget.Margin))!.DeclaringType == typeof(UiWidget),
            "SVG consumes the same inherited Margin property as buttons rather than a duplicate implementation");
        bool? selected = false;
        using var checkbox = Fluent.Checkbox("margin-check", new UiBinding<bool?>(() => selected, value => selected = value));
        checkbox.Margin = new(13, 9, 7, 11);
        current = checkbox;
        Frame();
        var toggleBounds = Find("margin-check").Bounds;
        Click(new(toggleBounds.X + toggleBounds.Width / 2, toggleBounds.Y + toggleBounds.Height / 2));
        check(selected == true && Find("margin-check").ToggleState == UiAccessibilityToggleState.On,
            "common margin preserves checkbox pointer input and published toggle state");
        var text = "text";
        using var input = Fluent.Input("margin-input", new UiBinding<string>(() => text, value => text = value), 100, 180);
        input.Margin = new(13, 9, 7, 11);
        current = input;
        Frame();
        var inputBounds = Find("margin-input").Bounds;
        Click(new(inputBounds.X + 10, inputBounds.Y + inputBounds.Height / 2));
        Frame(Pointer(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.End, true, default)]
        });
        Frame(Pointer(new(-100, -100)) with
        {
            CharEvents = [new('!')]
        });
        check(text == "text!" && Find("margin-input").Value == text,
            "common margin preserves actual editor focus, caret movement and text binding");
        using var composition = new Composition { Margin = new(5, 7, 9, 11) };
        current = composition;
        Frame();
        check(measured == new UiVector2(94, 48) && Find("built-margin-button").Bounds == new UiRect(25, 27, 80, 30),
            "common margin naturally measures and renders a retained Build composition through its widget root");
        using var empty = new EmptyWidget { Margin = new(4) };
        current = empty;
        button.Margin = default;
        renderLeadingButton = true;
        Frame(Pointer(new(67, 40)));
        check(empty.Renders == 1 && measured == new UiVector2(8, 8) && !hovered,
            "zero-sized margin content still executes bindings without inheriting the previous button's hover identity");
    }

    private sealed class Composition : UiWidget
    {
        protected override IUiView Build(UiBuildContext context)
        {
            var button = Fluent.Button("built-margin-button");
            button.Size = new(80, 30);
            return button;
        }
    }

    private sealed class EmptyWidget : UiControlWidget
    {
        internal int Renders;
        protected override UiVector2 MeasureContent(UiControlContext context) => default;
        protected override void RenderControl(UiImmediateContext ui) => Renders++;
    }

    private sealed class View(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
