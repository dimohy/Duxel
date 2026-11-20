using Duxel.Core;

internal static class TextDragScrollTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        int wakeups = 0;
        context.SetRequestFrameCallback(() => wakeups++);
        var source = string.Join('\n', Enumerable.Range(0, 80).Select(i => "var item" + i + " = " + new string('a', 100) + ";"));
        using var document = Dux.ScrollView(new UiCodeView(source, new(Wrap: false, MaximumVisibleLines: null, ShowToolbar: false)),
            new(Width: 310, Height: 144));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var draw = new Screen(document);
        void Frame(UiInputState input)
        {
            context.NewFrame(new(1f / 60, new(520, 350), new(1, 1)), input, new(0, 0, 520, 350), UiTextSettings.Default);
            context.Render(draw);
            using var data = context.GetDrawData();
        }
        UiAccessibilityNode Editor() => tree.CaptureSnapshot().Elements.Single(element => element.Node is
            { Role: UiAccessibilityRole.Edit, Name: "C# 예제 소스" }).Node;
        void Hold(UiVector2 pointer, int frames = 10)
        {
            for (var i = 0; i < frames; i++)
                Frame(idle with { MousePosition = pointer, LeftMouseDown = true });
        }
        Frame(idle);
        var editor = Editor();
        var clip = editor.TextLayout!.ClipBounds;
        var start = new UiVector2(clip.X + 45, clip.Y + 54);
        Frame(idle with { MousePosition = start, LeftMouseDown = true, LeftMousePressed = true });
        var selection = Editor().TextInfo!;
        var wakeBefore = wakeups;
        Hold(new(start.X, clip.Y + clip.Height + 60));
        var below = Editor().TextInfo!;
        var down = document.Offset.Y;
        check(down > 30 && below.AnchorUtf16 == selection.AnchorUtf16 && below.CaretUtf16 > selection.CaretUtf16
            && wakeups > wakeBefore, "CodeView selection continues downward while stationary and scrolls its enclosing viewport");
        Hold(new(start.X, clip.Y - 45), 6);
        check(document.Offset.Y < down && Editor().TextInfo!.CaretUtf16 < below.CaretUtf16,
            "CodeView selection reverses upward without replacing its anchor");
        var middleY = MathF.Max(clip.Y + 24, start.Y);
        Hold(new(clip.X + clip.Width + 78, middleY));
        var right = Editor().Scroll!.Value.HorizontalOffset;
        var rightCaret = Editor().TextInfo!.CaretUtf16;
        check(right > 30, "unwrapped CodeView selection scrolls right within the editor");
        Hold(new(clip.X - 54, middleY), 6);
        check(Editor().Scroll!.Value.HorizontalOffset < right && Editor().TextInfo!.CaretUtf16 < rightCaret,
            "unwrapped CodeView selection scrolls back left");
        Frame(idle with { MousePosition = new(start.X, clip.Y + clip.Height + 60), LeftMouseReleased = true });
        var stop = document.Offset;
        var caret = Editor().TextInfo!.CaretUtf16;
        Frame(idle with { MousePosition = new(start.X, clip.Y + clip.Height + 60) });
        check(document.Offset == stop && Editor().TextInfo!.CaretUtf16 == caret,
            "releasing CodeView selection stops automatic scrolling and keeps the selected endpoint");

        var value = new UiState<string>(new('w', 130));
        using var single = Fluent.Input("One line", value.Binding, 200, 180);
        draw.Widget = single;
        Frame(idle);
        var field = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "One line").Node;
        var inside = new UiVector2(field.Bounds.X + field.Bounds.Width * .45f, field.Bounds.Y + field.Bounds.Height / 2);
        Frame(idle with { MousePosition = inside, LeftMouseDown = true, LeftMousePressed = true });
        var initial = context.State.GetSelection(field.Id);
        Hold(new(field.Bounds.X + field.Bounds.Width + 74, inside.Y));
        var end = context.State.GetSelection(field.Id);
        check(end.Start == initial.Start && end.End > initial.End + 10 && value.Value.Length == 130,
            "single-line Fluent input shares the horizontal drag selection behavior without changing text");
        Hold(new(field.Bounds.X - 70, inside.Y), 10);
        check(context.State.GetSelection(field.Id).End < end.End,
            "single-line Fluent input reverses selection drag movement at the left edge");
        Frame(idle with { MousePosition = inside, LeftMouseReleased = true });

        var notes = new UiState<string>(source);
        using var multiline = Fluent.Textarea("Notes", notes.Binding, 16_384, 260, 122);
        draw.Widget = multiline;
        Frame(idle);
        UiAccessibilityNode Notes() => tree.CaptureSnapshot().Elements.Single(element =>
            element.Node is { Role: UiAccessibilityRole.Edit, Name: "Notes" }).Node;
        var note = Notes();
        inside = new(note.Bounds.X + 43, note.Bounds.Y + 30);
        Frame(idle with { MousePosition = inside, LeftMouseDown = true, LeftMousePressed = true });
        var origin = Notes().TextInfo!;
        Hold(new(inside.X, note.Bounds.Y + note.Bounds.Height + 70));
        var later = Notes().TextInfo!;
        check(later.AnchorUtf16 == origin.AnchorUtf16 && later.CaretUtf16 > origin.CaretUtf16
            && Notes().Scroll!.Value.VerticalOffset > 0 && notes.Value == source,
            "editable multiline text scrolls its own viewport and retains selection and text");
    }

    private sealed class Screen(UiWidget widget) : UiScreen
    {
        internal UiWidget Widget { get; set; } = widget;
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(35, 45));
            Widget.Render(ui);
        }
    }
}
