using Duxel.Core;

internal static class WidgetTransformTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var ime = new Ime();
        context.SetImeHandler(ime);
        var activations = 0;
        using var button = Fluent.Button("rotated-button", () => activations++);
        button.Size = new(80, 30);
        UiWidget current = button;
        var origin = new UiVector2(20, 20);
        UiVector2 measured = default;
        var hovered = false;
        var sceneCount = 0;
        var constraints = new UiBoxConstraints();
        UiVector2 measuredAvailable = default;
        var view = new View(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(origin);
            measured = current.MeasureConstrained(ui, constraints);
            if (current is ConstraintProbe constraintProbe)
                measuredAvailable = constraintProbe.Available;
            current.Render(ui);
            hovered = ui.IsItemHovered();
        });
        static UiInputState Pointer(UiVector2 point, bool down = false, bool pressed = false, bool released = false) =>
            new(point, down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(500, 400), new(1, 1)), input ?? Pointer(new(-100, -100)), new(0, 0, 500, 400), UiTextSettings.Default);
            context.Render(view);
            using var draw = context.GetDrawData();
            sceneCount = draw.DrawLists.Count(list => list.Scene3D?.WidgetTransform is not null);
        }
        UiAccessibilityNode Find(string name) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name).Node;
        void Click(UiVector2 point)
        {
            Frame(Pointer(point, down: true, pressed: true));
            Frame(Pointer(point, released: true));
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(Pointer(new(-100, -100)) with
            {
                KeyEvents = [new(key, true, modifiers)]
            });
            Frame(Pointer(new(-100, -100)) with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
        }
        button.Transform = new(RotationDegrees: 90);
        Frame();
        check(measured == new UiVector2(30, 80) && Find("rotated-button").Bounds == new UiRect(20, 20, 30, 80),
            "common layout rotation swaps a real button's measured and accessible bounds");
        check(sceneCount > 0, "transformed button submits the common GPU scene and affine parent masks");
        Click(new(35, 60));
        check(activations == 1 && hovered && context.State.FocusedId == Find("rotated-button").Id,
            "real pointer input inverse-maps into a quarter-turned button and retains focus");
        var id = Find("rotated-button").Id;
        button.Transform = new(RotationDegrees: 45);
        Frame();
        var diagonal = Find("rotated-button").Bounds;
        var pointGeometry = Find("rotated-button").GetPointGeometry()!;
        check(!pointGeometry.ContainsPoint(22, 22) && pointGeometry.ContainsPoint(diagonal.X + diagonal.Width / 2, diagonal.Y + diagonal.Height / 2),
            "published accessibility point geometry rejects the rotated bounding corners used by native providers");
        Click(new(22, 22));
        check(activations == 1 && !hovered, "empty corners of a rotated button's bounding box do not activate or hover");
        Click(new(diagonal.X + diagonal.Width / 2, diagonal.Y + diagonal.Height / 2));
        check(activations == 2 && Find("rotated-button").Id == id,
            "arbitrary rotation keeps the same button identity and actual center activation");
        Key(UiKey.Space);
        check(activations == 3, "rotated pointer focus preserves Space-key activation");
        button.Margin = new(7, 11, 13, 17);
        button.Transform = new(RotationDegrees: 450);
        Frame();
        check(measured == new UiVector2(50, 108) && Find("rotated-button").Bounds == new UiRect(27, 31, 30, 80),
            "common margins remain outside the normalized transformed content and full turns are exact");
        button.Margin = default;
        button.Transform = new(Scale: 2, RotationDegrees: -90);
        Frame();
        check(measured == new UiVector2(60, 160) && Find("rotated-button").Bounds == new UiRect(20, 20, 60, 160),
            "common uniform scale and negative quarter rotation participate in layout");
        origin = new(200, 120);
        var visualBounds = new List<UiRect>();
        foreach (var pivot in new[] { new UiVector2(0, 0), new(.5f, .5f), new(1, 1) })
        {
            button.Transform = new(RotationDegrees: 90, Origin: pivot, Mode: UiWidgetTransformMode.Visual);
            Frame();
            visualBounds.Add(Find("rotated-button").Bounds);
        }
        check(measured == new UiVector2(80, 30) && visualBounds.SequenceEqual(new[]
            { new UiRect(170, 120, 30, 80), new(225, 95, 30, 80), new(280, 70, 30, 80) }),
            "visual rotation preserves flow while top-left, center and bottom-right origins cause distinct actual placement");
        origin = new(20, 20);
        button.Transform = new(RotationDegrees: 90);
        using var column = Dux.Column(new(Spacing: 0, MainAxisSize: UiMainAxisSize.Min), button);
        column.Transform = new(RotationDegrees: 90);
        current = column;
        Frame();
        check(measured == new UiVector2(80, 30) && Find("rotated-button").Bounds == new UiRect(20, 20, 80, 30),
            "nested parent and child transforms compose measurement and accessible screen coordinates");
        Click(new(60, 35));
        check(activations == 4, "nested inverse transforms activate the real child once");
        using var clippedButton = Fluent.Button("clipped-rotated-button");
        clippedButton.Size = new(120, 80);
        clippedButton.Transform = new(RotationDegrees: -45, Mode: UiWidgetTransformMode.Visual);
        using var clippedParent = Dux.Container(clippedButton, new(Width: 60, Height: 60));
        clippedParent.Transform = new(RotationDegrees: 45);
        current = clippedParent;
        Frame();
        var clippedNode = Find("clipped-rotated-button");
        var clippedGeometry = clippedNode.GetPointGeometry()!;
        check(!clippedGeometry.ContainsPoint(clippedNode.Bounds.X + 1, clippedNode.Bounds.Y + 1)
            && clippedNode.ClickablePoint is { } visiblePoint && clippedGeometry.ContainsPoint(visiblePoint.X, visiblePoint.Y),
            "nested transformed accessibility clips preserve a real visible clickable point and reject empty envelope corners");
        var text = "text";
        using var input = Fluent.Input("rotated-input", new UiBinding<string>(() => text, value => text = value), 100, 180);
        input.Transform = new(RotationDegrees: 90);
        current = input;
        Frame();
        var field = Find("rotated-input").Bounds;
        Click(new(field.X + field.Width / 2, field.Y + field.Height / 2));
        Key(UiKey.End);
        ime.Committed = "!";
        Frame();
        check(text == "text!", $"rotated editor preserves real pointer focus, caret movement and bound text input (value : {text}; active : {context.State.ActiveId}; focused : {context.State.FocusedId})");
        var layout = Find("rotated-input").TextLayout!;
        var character = layout.GetCharacterExtents(0);
        check(layout.GetOffsetAtPoint(character.X + character.Width / 2, character.Y + character.Height / 2) == 0,
            "rotated accessible character boxes and inverse point queries use the same screen coordinates");
        check(ime.Caret is { } caret && caret.X >= field.X && caret.X + caret.Width <= field.X + field.Width
            && caret.Y >= field.Y && caret.Y + caret.Height <= field.Y + field.Height && ime.Input == field,
            "rotated editor maps the real IME caret and input rectangle back to client coordinates");
        var originalImeHeight = ime.FontHeight;
        input.Transform = new(Scale: 2, RotationDegrees: 90);
        Frame();
        check(ime.FontHeight == originalImeHeight * 2,
            "scaled editor applies the geometry scale to IME font metrics exactly once");
        using var probe = new ScaleProbe { Transform = new(Scale: 4) };
        using var shrink = Dux.Column(new(Spacing: 0, MainAxisSize: UiMainAxisSize.Min), probe);
        shrink.Transform = new(Scale: .25f);
        current = shrink;
        Frame();
        check(probe.Scale == 1, "nested shrink and grow requests avoid unnecessary raster oversampling");
        context.SetContentScale(2);
        Frame();
        check(probe.Scale == 2, "nested widget raster quality preserves the host display scale");
        context.SetContentScale(1);
        using var constraintProbe = new ConstraintProbe { Transform = new(RotationDegrees: 90) };
        current = constraintProbe;
        constraints = new(0, 80, 0, float.PositiveInfinity);
        Frame();
        check(float.IsPositiveInfinity(measuredAvailable.X) && measuredAvailable.Y == 80 && measured == new UiVector2(20, 60),
            "quarter-turned widget measures content against swapped parent maxima without inventing a finite unbounded axis");
        constraints = new();
        var rejected = 0;
        foreach (var invalid in new[] { new UiWidgetTransform(Scale: 0), new(Scale: float.NaN), new(Origin: new(-1, 0)), new(Mode: (UiWidgetTransformMode)99) })
        {
            try
            {
                input.Transform = invalid;
            }
            catch (ArgumentOutOfRangeException) { rejected++; }
        }
        check(rejected == 4, "common transforms reject invalid scale, origin and mode before changing widget state");
    }

    private sealed class View(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }

    private sealed class ScaleProbe : UiControlWidget
    {
        internal float Scale;
        protected override UiVector2 MeasureContent(UiControlContext context) => new(10, 10);
        protected override void RenderControl(UiImmediateContext ui)
        {
            Scale = ui.ContentScale * ui.WidgetRasterScale;
            ui.Dummy(new(10, 10));
        }
    }

    private sealed class ConstraintProbe : UiControlWidget
    {
        internal UiVector2 Available;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Available = context.AvailableSize;
            return new(60, 20);
        }
        protected override void RenderControl(UiImmediateContext ui) => ui.Dummy(new(60, 20));
    }

    private sealed class Ime : IUiImeHandler
    {
        internal UiRect? Caret;
        internal UiRect Input;
        internal float FontHeight;
        internal string? Committed;
        private string? _owner;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth) => (Caret, Input, FontHeight) = (caretRect, inputRect, fontPixelHeight);
        public string? GetCompositionText() => null;
        public void SetCompositionOwner(string? inputId) => _owner = inputId;
        public string? ConsumeCommittedText(string inputId)
        {
            if (_owner != inputId)
                return null;
            var committed = Committed;
            Committed = null;
            return committed;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
    }
}
