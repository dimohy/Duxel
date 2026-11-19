using Duxel.Core;

internal static class AccessibilityTextEditTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new UiFontAtlas(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var clipboard = new Clipboard();
        context.SetClipboard(clipboard);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = "start";
        var memo = "첫\n줄";
        var readOnly = false;
        var disabled = false;
        Action? afterRender = null;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.1f, new(420, 300), new(1, 1)), input ?? idle, new(0, 0, 420, 300), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.SetCursorScreenPos(new(5, 5));
                ui.BeginDisabled(disabled);
                ui.InputText("Edit", ref value, 16, readOnly ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None);
                ui.EndDisabled();
                ui.SetCursorScreenPos(new(5, 65));
                ui.InputTextMultiline<UiFluentMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>("Memo", ref memo, 16, 280, 100);
                afterRender?.Invoke();
            }));
            context.GetDrawData().ReleasePooled();
        }
        UiAccessibilityElement Target(string name = "Edit") => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name);
        UiAccessibilityTextEdit Command(UiAccessibilityTextEditKind kind, int start, int end, string? replacement = null, string name = "Edit")
            => new(Target(name).Node.TextInfo!.Content, kind, start, end, replacement);
        UiAccessibilityResult Apply(UiAccessibilityTextEdit command, string name = "Edit", Action? beforeFrame = null)
        {
            var target = Target(name);
            using var wake = new ManualResetEventSlim();
            tree.SetRequestFrameCallback(wake.Set);
            var operation = Task.Run(() => tree.RequestTextEdit(target.Node.Id, target.RuntimeId, command));
            var completed = operation.ContinueWith(_ => wake.Set(), TaskScheduler.Default);
            try
            {
                if (!wake.Wait(TimeSpan.FromSeconds(1)))
                    throw new TimeoutException("The range edit did not reach the render queue.");
                if (!operation.IsCompleted)
                {
                    beforeFrame?.Invoke();
                    Frame();
                }
                var result = operation.GetAwaiter().GetResult();
                completed.GetAwaiter().GetResult();
                return result;
            }
            finally { context.SetAccessibilityTree(tree); }
        }
        void Key(UiKey key)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, KeyModifiers.Ctrl)],
                Modifiers = KeyModifiers.Ctrl
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, KeyModifiers.Ctrl)],
                Modifiers = KeyModifiers.Ctrl
            });
            Frame();
        }
        Frame();
        check(Target().Node.TextInfo?.CanEditText == true, "Standard editors explicitly advertise guarded range editing");
        var original = Target().Node.TextInfo!.Content;
        check(Apply(Command(UiAccessibilityTextEditKind.Replace, 0, value.Length, "정😀z")) == UiAccessibilityResult.Success
            && value == "정😀z" && Target().Node.TextInfo is { CaretUtf16: 4, AnchorUtf16: 4 },
            "A guarded whole-text replacement updates the actual buffer and collapsed end caret");
        check(tree.RequestTextEdit(Target().Node.Id, Target().RuntimeId, new(original, UiAccessibilityTextEditKind.Copy, 0, 1), wait: false)
            == UiAccessibilityResult.InvalidArgument && clipboard.Writes == 0, "Stale content is rejected before clipboard access");
        check(Apply(Command(UiAccessibilityTextEditKind.Replace, 1, 1, "한😀")) == UiAccessibilityResult.Success
            && value == "정한😀😀z" && Target().Node.TextInfo is { CaretUtf16: 4, AnchorUtf16: 4 },
            "Insertion at a UTF16 source boundary preserves complete Korean and supplementary characters");
        check(tree.RequestAction(Target().Node.Id, Target().RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "The edited field accepts actual focus for history validation");
        Frame();
        var focus = context.State.FocusedId;
        check(Apply(Command(UiAccessibilityTextEditKind.Cut, 1, 4)) == UiAccessibilityResult.Success && value == "정😀z" && clipboard.Text == "한😀",
            "Cut copies the exact source range before removing it from the real editor");
        Key(UiKey.Z);
        check(value == "정한😀😀z", "One actual Ctrl+Z reverses exactly one native-style cut operation");
        Key(UiKey.Y);
        check(value == "정😀z" && context.State.FocusedId == focus, "Actual redo restores the range edit without changing keyboard focus");
        readOnly = true;
        Frame();
        var before = Target().Node.TextInfo!;
        check(Apply(Command(UiAccessibilityTextEditKind.Copy, 1, 3)) == UiAccessibilityResult.Success && clipboard.Text == "😀"
            && Target().Node.TextInfo!.CaretUtf16 == before.CaretUtf16 && Target().Node.TextInfo!.AnchorUtf16 == before.AnchorUtf16,
            "Readonly copying preserves text, selection and caret");
        var reads = clipboard.Reads;
        check(Apply(Command(UiAccessibilityTextEditKind.Paste, 0, 0)) == UiAccessibilityResult.ReadOnly && clipboard.Reads == reads,
            "Readonly mutation is rejected before reading clipboard data");
        readOnly = false;
        Frame();
        clipboard.Text = "한😀";
        check(Apply(Command(UiAccessibilityTextEditKind.Paste, 1, 1)) == UiAccessibilityResult.Success && value == "정한😀😀z",
            "Paste inserts at its explicit source position rather than replacing the current selection");
        clipboard.Text = "bad\nline";
        var unchanged = value;
        check(Apply(Command(UiAccessibilityTextEditKind.Paste, 0, 0)) == UiAccessibilityResult.InvalidArgument && value == unchanged,
            "Single-line native paste rejects forbidden controls instead of silently rewriting them");
        clipboard.Text = "\ud800";
        check(Apply(Command(UiAccessibilityTextEditKind.Paste, 0, 0)) == UiAccessibilityResult.InvalidArgument && value == unchanged,
            "Malformed clipboard UTF16 cannot enter the committed editor buffer");
        check(Apply(Command(UiAccessibilityTextEditKind.Replace, 0, value.Length, new string('x', 16))) == UiAccessibilityResult.Success,
            "A native replacement can exactly fill the declared UTF16 maximum");
        check(Apply(Command(UiAccessibilityTextEditKind.Replace, 0, 0, "z")) == UiAccessibilityResult.InvalidArgument && value == new string('x', 16),
            "Overlong range insertion fails atomically without truncating the payload");
        check(Apply(Command(UiAccessibilityTextEditKind.Replace, 0, value.Length, "e\u0301x")) == UiAccessibilityResult.Success,
            "A source edit can publish a combining grapheme");
        var writes = clipboard.Writes;
        check(Apply(Command(UiAccessibilityTextEditKind.Cut, 0, 1)) == UiAccessibilityResult.InvalidArgument && value == "e\u0301x" && clipboard.Writes == writes,
            "A range inside a grapheme is refused before either clipboard or text changes");
        check(Apply(Command(UiAccessibilityTextEditKind.Replace, 0, 0, "a"), beforeFrame: () => value = "external")
            == UiAccessibilityResult.InvalidArgument && value == "external", "An external model change after admission rejects obsolete source offsets");
        writes = clipboard.Writes;
        check(Apply(Command(UiAccessibilityTextEditKind.Copy, 0, 1), beforeFrame: () => disabled = true) == UiAccessibilityResult.Disabled
            && clipboard.Writes == writes, "Disabling a target after admission prevents clipboard access");
        disabled = false;
        Frame();
        clipboard.WriteFailure = new IOException("Fixture clipboard write failure.");
        check(Apply(Command(UiAccessibilityTextEditKind.Cut, 0, 1)) == UiAccessibilityResult.Failed && value == "external",
            "A clipboard write failure reports failure and leaves a cut's source text intact");
        clipboard.WriteFailure = null;
        clipboard.ReadFailure = new TimeoutException("Fixture clipboard read timeout.");
        check(Apply(Command(UiAccessibilityTextEditKind.Paste, 0, 0)) == UiAccessibilityResult.Timeout && value == "external",
            "A clipboard read timeout is explicit and cannot commit partial text");
        clipboard.ReadFailure = null;
        check(Apply(Command(UiAccessibilityTextEditKind.Replace, 0, memo.Length, "한\r\n글", "Memo"), "Memo") == UiAccessibilityResult.Success
            && memo == "한\n글" && Target("Memo").Node.TextInfo is { CaretUtf16: 3, AnchorUtf16: 3 },
            "Multiline native replacement uses the editor's canonical newline contract and updated end caret");
        afterRender = () => { value = "binding"; tree.SetControlValue(Target().Node.Id, value); };
        check(Apply(Command(UiAccessibilityTextEditKind.Replace, 0, value.Length, "candidate")) == UiAccessibilityResult.Failed,
            "A later authoritative binding rejection is not reported as successful range editing");
        afterRender = null;
        Frame();
        foreach (var make in new Action[]
        {
            () => new UiAccessibilityTextEdit(new("😀"), UiAccessibilityTextEditKind.Replace, 1, 1, "x"),
            () => new UiAccessibilityTextEdit(new("a"), UiAccessibilityTextEditKind.Replace, 0, 1, "\ud800"),
            () => new UiAccessibilityTextEdit(new("ab"), UiAccessibilityTextEditKind.Paste, 0, 1)
        })
        {
            var rejected = false;
            try
            {
                make();
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Invalid range-edit contracts fail before entering the frame queue");
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class Clipboard : IUiClipboard
    {
        internal string Text = "";
        internal int Reads, Writes;
        internal Exception? ReadFailure, WriteFailure;
        public string GetText()
        {
            Reads++;
            if (ReadFailure is not null)
                throw ReadFailure;
            return Text;
        }
        public void SetText(string text)
        {
            if (WriteFailure is not null)
                throw WriteFailure;
            Writes++;
            Text = text;
        }
    }
}
