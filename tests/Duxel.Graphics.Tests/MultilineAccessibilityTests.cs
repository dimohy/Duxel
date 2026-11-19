using Duxel.Core;

internal static class MultilineAccessibilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = "before";
        var writes = 0;
        var disabled = false;
        var clipped = false;
        using var editor = new UiMultilineInput<UiStandardMultilineInputBehavior, Presentation>("Notes##edit",
            new(() => value, next => { value = next; writes++; }), 64, 180, 90);
        using var field = Fluent.Field(editor);
        field.Width = 200;
        field.Options = new() { Label = "Notes", Hint = "Multiple lines", Required = true, ValidationMessage = "Review" };

        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle,
                new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(20, 20));
                using var clip = new UiControlContext(ui).Clip(clipped ? new(300, 200, 0, 0) : new(0, 0, 320, 240));
                ui.BeginDisabled(disabled);
                try
                {
                    field.Render(ui);
                }
                finally
                {
                    ui.EndDisabled();
                }
            }));
            using var draw = context.GetDrawData();
        }
        Frame();
        var id = tree.Root.First!;
        check(tree.TryGet(id, out var node) && node.Node.Multiline && node.Node.ValuePattern && !node.Node.Focusable,
            "compiled multiline advertises implemented Value actions while focus actions remain unavailable");
        check(node.Node.Name == "Notes" && node.Node.Hint == "Multiple lines" && node.Node.Required && node.Node.Invalid,
            "compiled multiline preserves Field name, hint, required and validation metadata");
        check(tree.RequestAction(id, node.RuntimeId, UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.InvalidArgument,
            "compiled standard multiline explicitly rejects unimplemented accessibility focus");
        var initialRevision = tree.Revision;
        Frame();
        check(tree.Revision == initialRevision, "unchanged multiline accessibility publication preserves its revision");

        UiAccessibilityResult Queue(string next) => tree.RequestAction(id, node.RuntimeId,
            UiAccessibilityAction.SetValue, next, wait : false);
        check(Queue("one\r\ntwo\rthree\nfour") == UiAccessibilityResult.Success && value == "before" && writes == 0,
            "multiline Value action queues CRLF, CR and LF without mutating the caller model on the provider thread");
        Frame();
        check(value == "one\ntwo\nthree\nfour" && writes == 1 && tree.TryGet(id, out var current) && current.Node.Value == value,
            "multiline Value action normalizes hard breaks and publishes the actual binding exactly once");
        Frame();
        check(writes == 1, "consumed multiline Value action is not replayed on the following frame");
        check(Queue(value) == UiAccessibilityResult.Success, "unchanged multiline Value action is accepted");
        Frame();
        check(writes == 1, "unchanged multiline Value action avoids a binding write");

        context.State.ActiveId = id;
        Frame(idle with { KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl), new(UiKey.Z, false, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        check(value == "before" && writes == 2, "multiline Value action enters the existing editor undo history");
        Frame(idle with { KeyEvents = [new(UiKey.Y, true, KeyModifiers.Ctrl), new(UiKey.Y, false, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        check(value == "one\ntwo\nthree\nfour" && writes == 3, "multiline Value action can be redone through the actual editor");
        context.State.ActiveId = null;

        foreach (var control in new[] { '\0', '\t', '\u001b', '\u007f', '\u0085' })
            check(Queue("bad" + control) == UiAccessibilityResult.InvalidArgument,
                "multiline Value action rejects control U+" + ((int)control).ToString("X4"));
        check(Queue(new('x', 65)) == UiAccessibilityResult.InvalidArgument,
            "multiline Value action enforces the published maximum before enqueueing");
        check(Queue("first\nvalue") == UiAccessibilityResult.Success && Queue("second\nvalue") == UiAccessibilityResult.Success,
            "multiline Value actions accept ordered replacement requests");
        Frame();
        check(value == "first\nvalue" && writes == 4, "first queued multiline replacement is applied once in its frame");
        Frame();
        check(value == "second\nvalue" && writes == 5, "second queued multiline replacement retains arrival order");

        Queue("disabled\nrequest");
        disabled = true;
        Frame();
        check(value == "second\nvalue" && writes == 5 && Queue("x") == UiAccessibilityResult.Disabled,
            "multiline Value action revalidates disabled state both before enqueueing and during consumption");
        disabled = false;
        Frame();
        Queue("clipped\nrequest");
        clipped = true;
        Frame();
        check(value == "second\nvalue" && writes == 5 && Queue("x") == UiAccessibilityResult.Unavailable,
            "multiline Value action rejects a queued replacement after the editor becomes fully clipped");
        clipped = false;
        Frame();
        Queue("length\nrequest");
        editor.MaxLength = 5;
        Frame();
        check(value == "second\nvalue" && writes == 5,
            "multiline Value action revalidates a reduced maximum without truncating or replacing the model");
        editor.MaxLength = 64;
        Frame();
        check(writes == 5, "rejected multiline Value actions are drained without delayed replay");

        using var singleLineTree = new UiAccessibilityTree();
        singleLineTree.BeginFrame();
        singleLineTree.Add(new("single", "", UiAccessibilityRole.Edit, "Single", new(0, 0, 100, 32),
            true, false, false, MaximumLength : 64));
        singleLineTree.EndFrame();
        singleLineTree.TryGet("single", out var single);
        check(!single.Node.Multiline && singleLineTree.RequestAction("single", single.RuntimeId,
            UiAccessibilityAction.SetValue, "one\ntwo", wait : false) == UiAccessibilityResult.InvalidArgument,
            "existing single-line Value action still rejects newlines by default");
        singleLineTree.BeginFrame();
        singleLineTree.Add(single.Node with { Multiline = true, ReadOnly = true });
        singleLineTree.EndFrame();
        check(singleLineTree.RequestAction("single", single.RuntimeId, UiAccessibilityAction.SetValue,
            "one\ntwo", wait : false) == UiAccessibilityResult.ReadOnly,
            "multiline metadata does not bypass the accessibility read-only guard");
        field.Dispose();
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(_ => { }));
        using var emptyDraw = context.GetDrawData();
        check(tree.Count == 0 && Queue("removed") == UiAccessibilityResult.Unavailable,
            "removed multiline editor rejects actions using its former runtime identity");
    }

    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            new(new(0, 0, width, height), 20) { Wrap = true };
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
            natural with { Field = new(0, 0, size.X, size.Y) };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit) =>
            context.PaintEditableMultiline(layout, edit, new(new(240, 240, 240), new(80, 120, 180), new(255, 255, 255)));
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
