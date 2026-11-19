using Duxel.Core;

internal static class MarkdownStructuralParagraphTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string text = "> abc **אב גד** 123\n\n- [ ] pending\n- [x] complete\n\n---\n\nTail";
        var source = new UiMarkdownTextSource(new(text));
        var visual = new UiVisualTextLines();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        void Frame(int active = -1)
        {
            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => visual.Prepare(ui, text, 160, 300, 20, 0, styled: source, activeSourceIndex: active)));
            using var draw = context.GetDrawData();
        }
        Frame();
        check(visual.NativeParagraph is { Objects.Length: 2 },
            "Markdown quote, task and rule document uses one native paragraph snapshot with two task objects");
        check(source.Projection.Atoms.Length == 2,
            "Projected task syntax shares the same source-atom contract as images");
        var tasks = source.Runs.Where(run => run.Display == "\uFFFC" && (run.Style.Flags & UiStyledTextFlags.Task) != 0).ToArray();
        foreach (var task in tasks)
        {
            var bounds = visual.NativeObjectBounds(task.Source.Start);
            check(MathF.Abs(bounds.Width - 16) < .002f && MathF.Abs(bounds.Height - 16) < .002f,
                "Native task allocation reserves the actual Fluent checkbox size : " + task.Source.Start);
            var leading = visual.Hit(bounds.X + bounds.Width / 4, bounds.Y + bounds.Height / 2, 20);
            var trailing = visual.Hit(bounds.X + bounds.Width * .75f, bounds.Y + bounds.Height / 2, 20);
            check(leading == task.Source.Start && trailing == task.Source.End,
                "Native task pointer edges skip every hidden marker character : " + task.Source.Start);
            visual.Hit(bounds.X + bounds.Width / 4, bounds.Y + bounds.Height / 2, 20);
            check(visual.MoveHorizontal(task.Source.Start, right: true) == task.Source.End
                && visual.MoveHorizontal(task.Source.End, right: false) == task.Source.Start
                && visual.Next(task.Source.Start) == task.Source.End && visual.Previous(task.Source.End) == task.Source.Start,
                "Task physical and logical movement both preserve complete marker source atoms : " + task.Source.Start);
        }
        var snapshot = visual.NativeParagraph;
        Frame();
        check(ReferenceEquals(snapshot, visual.NativeParagraph),
            "Unchanged quote/task/rule layout retains the complete native snapshot");
        Frame(text.IndexOf("pending", StringComparison.Ordinal));
        check(visual.NativeParagraph is { Objects.Length: 0 } && source.Projection.Atoms.Length == 0
            && source.Projection.Text.Contains("- [ ] pending", StringComparison.Ordinal),
            "Revealing a task list paints literal source and removes its task objects and atomic boundaries");
        Frame();
        check(source.Document.ParseCount == 1 && visual.NativeParagraph is { Objects.Length: 2 },
            "Task source reveal and collapse restore native objects without reparsing Markdown");
        visual.Clear();
        source.Release();
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
