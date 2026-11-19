using Duxel.Core;

internal static class TextEditIntegrityTests
{
    internal static void Run(Action<bool, string> check)
    {
        var dirty = new UiTextEditBuffer("old");
        dirty.Replace(0, 3, "pending");
        dirty.Sync("old");
        check(dirty.Text == "old", "buffer Sync replaces dirty builder data even when the requested value equals the previous cache");
        foreach (var multiline in new[] { false, true })
        {
            var kind = multiline ? "multiline" : "single-line";
            void Verify(bool condition, string name) => check(condition, kind + " : " + name);
            using (var input = new Editor(multiline, "base"))
            {
                input.Type('x');
                input.Text = "remote";
                input.Frame();
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                Verify(input.Text == "remote" && input.Writes == 1, "external model replacement resets undo without notifying the binding");
            }
            using (var input = new Editor(multiline, "base"))
            {
                input.Type('x');
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                input.Text = "remote";
                input.Frame();
                input.Key(UiKey.Y, KeyModifiers.Ctrl);
                Verify(input.Text == "remote" && input.Writes == 2, "external model replacement invalidates stale redo");
            }
            using (var input = new Editor(multiline, "base"))
            {
                input.Type('x');
                input.Text = "remote";
                input.Frame();
                input.Select(input.Text.Length, input.Text.Length);
                input.Type('h');
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                Verify(input.Text == "remote", "new editing after external replacement undoes to the new model baseline");
            }
            using (var input = new Editor(multiline, "base"))
            {
                input.Type('x');
                input.Text = new(input.Text.ToCharArray());
                input.Frame();
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                Verify(input.Text == "base", "equal caller values preserve undo even when string identity changes");
            }
            using (var input = new Editor(multiline, "base"))
            {
                input.Type('x');
                input.QueueValue("api");
                input.Frame();
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                Verify(input.Text == "basex", "accessibility Value replacement remains an undoable editor transaction");
                input.Key(UiKey.Y, KeyModifiers.Ctrl);
                Verify(input.Text == "api", "accessibility Value replacement retains redo");
            }
            using (var input = new Editor(multiline, "base"))
            {
                input.Type('x');
                input.Text = "remote";
                input.QueueValue("api");
                input.Frame();
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                Verify(input.Text == "remote", "queued Value action starts from a simultaneous external model replacement");
            }
            using (var input = new Editor(multiline, "base"))
            {
                input.Type('x');
                input.QueueValue("api");
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                Verify(input.Text == "basex", "keyboard undo in the Value-action frame observes the API transaction first");
            }
            var cases = new (string Source, int Maximum, string Expected)[]
            {
                ("😀", 1, ""),
                ("ab😀c", 3, "ab"),
                ("e\u0301x", 1, ""),
                ("e\u0301x", 2, "e\u0301"),
                ("👩\u200d💻x", 4, ""),
                ("🇰🇷x", 3, ""),
                ("1\ufe0f\u20e3x", 2, ""),
                ("a\nb", 2, multiline ? "a\n" : "ab")
            };
            foreach (var item in cases)
            {
                using var input = new Editor(multiline, "", item.Maximum);
                input.Paste(item.Source);
                Verify(input.Text == item.Expected && ValidUtf16(input.Text) && input.Writes == (item.Expected.Length == 0 ? 0 : 1),
                    "bounded paste keeps a complete text-element prefix : " + item.Source + " / " + item.Maximum);
            }
            using (var input = new Editor(multiline, "abcd", 4))
            {
                input.Select(1, 3);
                input.Type('Z');
                Verify(input.Text == "aZd" && input.Writes == 1, "character input can replace selection at the maximum length");
            }
            using (var input = new Editor(multiline, "abcd", 4))
            {
                input.Select(1, 2);
                input.Paste("😀");
                Verify(input.Text == "abcd" && input.Writes == 0 && input.State.GetSelection(input.Id) == new UiTextSelection(1, 2),
                    "an element that cannot fit leaves both the value and selected range intact");
            }
            using (var input = new Editor(multiline, "", 1))
            {
                input.Type(0x1f600);
                Verify(input.Text.Length == 0 && input.Writes == 0, "a supplementary character event is rejected atomically when one UTF16 unit remains");
            }
            using (var input = new Editor(multiline, "", 2, emoji: true))
            {
                input.Type(0x1f600);
                Verify(input.Text == "😀" && input.Writes == 1, "a supplementary character event fits an exact UTF16 budget");
            }
            using (var input = new Editor(multiline, "", 1))
            {
                input.Commit("😀");
                input.Frame();
                Verify(input.Text.Length == 0 && input.Writes == 0,
                    "bounded committed IME input is consumed atomically without delayed replay");
            }
            using (var input = new Editor(multiline, "", 2))
            {
                input.Commit("e\u0301x");
                Verify(input.Text == "e\u0301" && input.Writes == 1,
                    "committed IME input retains a complete fitting text-element prefix");
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                Verify(input.Text.Length == 0, "bounded IME insertion remains undoable");
            }
            using (var input = new Editor(multiline, "base"))
            {
                input.Type('x');
                input.Text = "remote";
                input.Commit("!");
                input.Key(UiKey.Z, KeyModifiers.Ctrl);
                Verify(input.Text == "remote", "IME insertion after external replacement undoes to the new model baseline");
            }
        }
    }
    private static bool ValidUtf16(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]))
            {
                if (++index >= text.Length || !char.IsLowSurrogate(text[index]))
                    return false;
            }
            else if (char.IsLowSurrogate(text[index]))
                return false;
        }
        return true;
    }
    private sealed class Editor : IDisposable
    {
        private readonly UiContext _context;
        private readonly UiAccessibilityTree _tree = new();
        private readonly Clipboard _clipboard = new();
        private readonly Ime _ime = new();
        private readonly UiWidget _widget;
        private readonly UiInputState _idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        internal string Text;
        internal int Writes;
        internal string Id
        {
            get;
        }
        internal UiState State => _context.State;
        internal Editor(bool multiline, string initial, int maximum = 256, bool emoji = false)
        {
            Text = initial;
            _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            if (emoji)
                ControlFontFixture.ConfigureEmoji(_context);
            else
                ControlFontFixture.Configure(_context);
            _context.SetAccessibilityTree(_tree);
            _context.SetClipboard(_clipboard);
            var binding = new UiBinding<string>(() => Text, next => { Text = next; Writes++; });
            _widget = multiline ? Fluent.Textarea("Editor", binding, maximum, 180, 80) : Fluent.Input("Editor", binding, maximum, 180);
            Frame();
            Id = _tree.Root.First!;
            State.ActiveId = State.FocusedId = Id;
            Select(Text.Length, Text.Length);
        }
        internal void Frame(UiInputState? input = null)
        {
            _context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? _idle, new(0, 0, 320, 240), UiTextSettings.Default);
            _context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 20));
                _widget.Render(ui);
            }));
            using var draw = _context.GetDrawData();
        }
        internal void Select(int start, int end)
        {
            State.SetSelection(Id, new(start, end));
            State.SetCursor(Id, end);
        }
        internal void Type(int scalar) => Frame(_idle with { CharEvents = [new((uint)scalar)] });
        internal void Key(UiKey key, KeyModifiers modifiers = default) => Frame(_idle with
        {
            KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)],
            Modifiers = modifiers
        });
        internal void Paste(string source)
        {
            _clipboard.Text = source;
            Key(UiKey.V, KeyModifiers.Ctrl);
        }
        internal void Commit(string source)
        {
            _ime.Target = Id;
            _ime.Pending = source;
            _context.SetImeHandler(_ime);
            Frame();
        }
        internal void QueueValue(string value)
        {
            _tree.TryGet(Id, out var entry);
            if (_tree.RequestAction(Id, entry.RuntimeId, UiAccessibilityAction.SetValue, value, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Value action was not queued.");
        }
        public void Dispose()
        {
            _widget.Dispose();
            _context.Dispose();
            _tree.Dispose();
        }
    }
    private sealed class Clipboard : IUiClipboard
    {
        internal string Text = "";
        public string GetText() => Text;
        public void SetText(string text) => Text = text;
    }
    private sealed class Ime : IUiImeHandler
    {
        internal string? Pending, Target;
        public string? ConsumeCommittedText(string inputId)
        {
            if (Target != inputId)
                return null;
            var result = Pending;
            Pending = null;
            return result;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public string? GetCompositionText() => null;
        public void SetCompositionOwner(string? inputId)
        {
        }
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
