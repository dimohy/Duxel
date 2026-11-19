using Duxel.Core;

internal static class MarkdownLifetimeTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var first = CreateContext();
        using var second = CreateContext();
        var ime = new Ime();
        first.SetImeHandler(ime);
        const string initial = "# 제목\n\n본문";
        var source = initial;
        var writes = 0;
        var changes = 0;
        var rejectWrite = false;
        var disabled = false;
        var shown = true;
        using var editor = Dux.MarkdownEditor("Markdown lifetime", new(() => source, value =>
        {
            writes++;
            if (!rejectWrite)
                source = value;
        }), new(Width: 300, Height: 180));
        editor.Changed = _ => changes++;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiContext context, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(400, 260), new(1, 1)), input ?? idle, new(0, 0, 400, 260), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 20));
                if (shown)
                {
                    ui.BeginDisabled(disabled);
                    try
                    {
                        editor.Render(ui);
                    }
                    finally { ui.EndDisabled(); }
                }
            }));
            using var data = context.GetDrawData();
        }
        void Focus()
        {
            first.State.AdvanceTime(.6f);
            Frame(first, idle with
            {
                MousePosition = new(44, 48),
                LeftMousePressed = true,
                LeftMouseDown = true
            });
            Frame(first, idle with
            {
                MousePosition = new(44, 48),
                LeftMouseReleased = true
            });
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(first, idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            Frame(first, idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
        }
        Frame(first);
        Focus();
        var id = first.State.ActiveId ?? throw new InvalidOperationException("Markdown pointer focus missing.");
        Key(UiKey.Home, KeyModifiers.Ctrl);
        Key(UiKey.End);
        var headingCaret = ime.Caret;
        var headingFont = ime.FontHeight;
        check(ime.Owner == id && headingCaret.Height == 36 && headingFont == 28,
            "Markdown heading IME uses the actual styled caret line and native font metrics");
        var parses = editor.Document.ParseCount;
        ime.Composition = "한";
        Frame(first);
        Frame(first);
        check(source == initial && writes == 0 && changes == 0 && editor.Document.ParseCount == parses,
            "Markdown preedit does not mutate the source, emit changes or reparse committed syntax");
        check(ime.Caret.X > headingCaret.X && ime.Caret.Y == headingCaret.Y && ime.Caret.Height == 36,
            "Markdown IME candidate caret follows the composed Korean text in its styled heading");
        ime.Composition = null;
        Frame(first);
        check(ime.Caret == headingCaret && source == initial,
            "Markdown cancelled preedit restores the same logical caret without an edit");
        ime.Queue(id, "한");
        Frame(first);
        Frame(first);
        check(source == "# 제목한\n\n본문" && writes == 1 && changes == 1 && editor.Document.ParseCount == parses + 1,
            "Markdown commits Korean once with one authoritative write, callback and syntax revision");
        Key(UiKey.Z, KeyModifiers.Ctrl);
        check(source == initial && writes == 2 && changes == 2,
            "Markdown committed composition is one undoable source edit");
        Key(UiKey.End, KeyModifiers.Ctrl);
        check(ime.Caret.Height == 20 && ime.FontHeight < headingFont && ime.Caret.Y > headingCaret.Y,
            $"Markdown body IME geometry switches to the actual body line and font size: caret={ime.Caret}, font={ime.FontHeight}, heading={headingCaret}, headingFont={headingFont}");
        var before = writes;
        ime.Queue(id, "금");
        disabled = true;
        Frame(first);
        disabled = false;
        Frame(first);
        check(source == initial && writes == before && ime.Pending is null && first.State.ActiveId is null && ime.Owner is null,
            "Disabled Markdown discards queued committed text and releases IME ownership without replay");
        Focus();
        editor.Options = editor.Options with
        {
            ReadOnly = true
        };
        ime.Queue(id, "금");
        Frame(first, idle with
        {
            CharEvents = [new('X')]
        });
        check(source == initial && writes == before && ime.Owner is null && ime.Pending is null,
            "Read-only Markdown blocks both committed IME input and ordinary character input");
        editor.Options = editor.Options with
        {
            ReadOnly = false
        };
        Focus();
        ime.Composition = "남";
        shown = false;
        Frame(first);
        check(first.State.ActiveId is null && ime.Owner is null && source == initial,
            "Omitting a Markdown editor from the frame releases active composition without changing its source");
        shown = true;
        Frame(first);
        Focus();
        Key(UiKey.End, KeyModifiers.Ctrl);
        rejectWrite = true;
        var callbacks = changes;
        Frame(first, idle with
        {
            CharEvents = [new('X')]
        });
        check(source == initial && editor.Document.Source == initial && changes == callbacks
            && first.State.GetEditBuffer(id, initial).Text == initial
            && first.State.GetCursor(id, 0) <= source.Length,
            "Markdown rejected writes reconcile source, edit buffer, styled document and caret in the same frame without a callback");
        rejectWrite = false;
        Frame(second);
        check(first.State.TextInputStorage == (0, 0) && first.State.ScrollStorage == (0, 0)
            && first.State.ActiveId is null && ime.Owner is null && editor.Document.Source == initial,
            "Markdown context transfer releases the old editor, scrolling and IME state while preserving its source");
        editor.Dispose();
        editor.Dispose();
        check(second.State.TextInputStorage == (0, 0) && second.State.ScrollStorage == (0, 0)
            && editor.Document.Source.Length == 0 && editor.Document.BlockCount == 0 && editor.Changed is null,
            "Markdown disposal idempotently releases both native editing state and retained source data");
        var disposedBinding = false;
        try
        {
            _ = editor.Value;
        }
        catch (ObjectDisposedException) { disposedBinding = true; }
        check(disposedBinding, "Disposed Markdown rejects source binding access");
        var disposedCallback = false;
        try
        {
            editor.Changed = _ => { };
        }
        catch (ObjectDisposedException) { disposedCallback = true; }
        check(disposedCallback, "Disposed Markdown rejects callback reattachment");
    }

    private static UiContext CreateContext()
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        return context;
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    internal sealed class Ime : IUiImeHandler
    {
        internal string? Composition;
        internal string? Owner;
        internal string? Pending;
        private string? _pendingOwner;
        internal UiRect Caret;
        internal float FontHeight;
        internal Action? AfterCaret;
        internal void Queue(string id, string text)
        {
            _pendingOwner = id;
            Pending = text;
            Composition = null;
        }
        public string? ConsumeCommittedText(string inputId)
        {
            if (inputId != _pendingOwner)
                return null;
            var value = Pending;
            Pending = null;
            _pendingOwner = null;
            return value;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public string? GetCompositionText() => Owner is null ? null : Composition;
        public void SetCompositionOwner(string? inputId)
        {
            Owner = inputId;
            if (inputId is null)
                Composition = null;
        }
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
            Caret = caretRect;
            FontHeight = fontPixelHeight;
            AfterCaret?.Invoke();
        }
    }
}
