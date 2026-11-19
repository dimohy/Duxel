using Duxel.Core;

internal static class MultilineDragTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var mode in new[] { "plain", "wrapped", "hard" })
        {
            using var editor = new Editor(mode);
            void Check(bool condition, string message) => check(condition, mode + " multiline drag : " + message);
            editor.State.ActiveId = editor.State.FocusedId = editor.Id;
            editor.Drag(250, 250);
            Check(!editor.Selection.HasSelection && editor.Caret == 1, "keyboard focus cannot begin selection without an owned press");
            editor.Reset();
            editor.Press(27, 25);
            var anchor = editor.Selection.Start;
            editor.Drag(500, 500);
            Check(editor.Selection.Start == anchor && editor.Selection.HasSelection && editor.ScrollY > 0
                && (mode == "wrapped" || editor.ScrollX > 0), "owned outside drag extends selection and pans its available axes");
            Check(editor.Requests > 0, "stationary outside dragging wakes the next frame");
            editor.Frame(editor.Idle with { LeftMouseReleased = true });
            var selection = editor.Selection;
            editor.Drag(50, 30);
            Check(editor.Selection == selection, "release prevents a later held frame from extending selection");
            editor.Reset();
            editor.Frame(editor.Idle with { MousePosition = new(27, 25), LeftMousePressed = true, LeftMouseReleased = true });
            selection = editor.Selection;
            editor.Drag(50, 30);
            Check(editor.Selection == selection, "coalesced press and release cannot leave capture");
            editor.Reset();
            editor.Press(27, 25);
            editor.Hidden = true;
            editor.Drag(250, 250);
            editor.Hidden = false;
            editor.Drag(50, 30);
            Check(!editor.Selection.HasSelection, "omitting the owner ends capture before it returns");
            editor.Reset();
            editor.Press(27, 25);
            editor.Clip = new(300, 180, 10, 10);
            editor.Drag(250, 250);
            editor.Clip = null;
            editor.Drag(50, 30);
            Check(!editor.Selection.HasSelection, "fully clipping the owner ends capture before it returns");
            editor.Reset();
            editor.Press(27, 25);
            editor.Disabled = true;
            editor.Drag(250, 250);
            editor.Disabled = false;
            editor.State.ActiveId = editor.Id;
            editor.Drag(50, 30);
            Check(!editor.Selection.HasSelection, "disable and subsequent focus do not restore capture");
            editor.Reset();
            editor.Press(27, 25);
            editor.State.AddPopupBlockingRect(new(0, 0, 320, 240), 1);
            editor.Drag(250, 250);
            editor.Drag(50, 30);
            Check(!editor.Selection.HasSelection, "a higher popup cancels background capture");
            editor.Reset();
            editor.Press(27, 25);
            editor.State.ActiveId = "another-control";
            editor.State.ActiveId = editor.Id;
            editor.Drag(50, 30);
            Check(!editor.Selection.HasSelection, "active-control transfer cancels capture before focus returns");
            editor.Reset();
            selection = editor.Selection;
            editor.Press(98, 25);
            editor.Drag(50, 30);
            Check(editor.Selection == selection, "scrollbar presses cannot become text drags");
            editor.Reset();
            editor.Clip = new(60, 20, 40, 60);
            editor.Press(27, 25);
            editor.Clip = null;
            editor.State.ActiveId = editor.Id;
            selection = editor.Selection;
            editor.Drag(50, 30);
            Check(editor.Selection == selection, "clipped-away presses cannot claim capture through later keyboard focus");
            editor.Reset();
            editor.ReadOnly = true;
            editor.Press(27, 25);
            for (var frame = 0; frame < 1200; frame++) editor.Drag(500, 500);
            Check(editor.Selection.HasSelection && editor.ScrollY > 0 && editor.Writes == 0,
                "readonly capture selects and pans without binding writes");
            var requests = editor.Requests;
            editor.Drag(500, 500);
            Check(editor.Requests == requests, "stationary capture at both scroll endpoints requests no extra frames");
            for (var frame = 0; frame < 1200; frame++) editor.Drag(-500, -500);
            Check(editor.Selection.End == 0 && editor.ScrollX == 0 && editor.ScrollY == 0,
                "reversed capture reaches the source beginning and both lower scroll endpoints");
            editor.Reset();
            editor.Press(27, 25);
            using var other = new Editor(mode);
            other.Frame(other.Idle with { LeftMouseDown = true }, editor.View);
            editor.Drag(50, 30);
            Check(!editor.Selection.HasSelection, "moving the widget through another context releases its previous capture");
            editor.WidgetDispose();
            Check(editor.State.TextInputStorage == (0, 0) && editor.State.ScrollStorage == (0, 0),
                "disposing a held editor releases editing,scrollbar and auxiliary state");
        }
    }
    private sealed class Editor : IDisposable
    {
        private readonly UiContext _context;
        private readonly UiWidget _widget;
        private readonly Action<bool> _setReadOnly;
        internal readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        internal string Text = string.Join('\n', Enumerable.Repeat("abcdefghijklmnopqrstuvwxyzabcd", 12));
        internal int Writes;
        internal int Requests;
        internal bool Hidden;
        internal bool Disabled;
        internal UiRect? Clip;
        internal IUiView View => _widget;
        internal UiState State => _context.State;
        internal string Id { get; }
        internal int Caret => State.GetCursor(Id, 0);
        internal UiTextSelection Selection => State.GetSelection(Id);
        internal float ScrollX => State.GetScrollX(Id);
        internal float ScrollY => State.GetScrollY(Id);
        internal bool ReadOnly { set => _setReadOnly(value); }
        internal Editor(string mode)
        {
            var glyphs = Enumerable.Range(32, 95).ToDictionary(code => code,
                code => new UiGlyphInfo(7, 0, -10, code == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
            _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255], glyphs,
                new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
            _context.SetRequestFrameCallback(() => Requests++);
            var binding = new UiBinding<string>(() => Text, next => { Text = next; Writes++; });
            if (mode == "plain")
            {
                var widget = new UiMultilineInput<UiStandardMultilineInputBehavior, Presentation>("Drag", binding, 4096, 80, 60);
                _widget = widget;
                _setReadOnly = value => widget.Options = widget.Options with { Flags = value ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None };
            }
            else
            {
                var widget = new UiMultilineInput<UiFluentMultilineInputBehavior, Presentation>("Drag", binding, 4096, 80, 60)
                    { Options = new() { Wrap = mode == "wrapped" } };
                _widget = widget;
                _setReadOnly = value => widget.Options = widget.Options with { Flags = value ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None };
            }
            Frame();
            Id = Presentation.Last.Id;
            Reset();
        }
        internal void Reset()
        {
            Frame();
            State.ActiveId = null;
            State.SetCursor(Id, 1);
            State.SetSelection(Id, new(1, 1));
            State.SetScrollX(Id, 0);
            State.SetScrollY(Id, 0);
            State.RegisterClick("reset-multi-drag-clicks", new(-100, -100));
            Requests = 0;
        }
        internal void Press(float x, float y) => Frame(Idle with { MousePosition = new(x, y), LeftMouseDown = true, LeftMousePressed = true });
        internal void Drag(float x, float y) => Frame(Idle with { MousePosition = new(x, y), LeftMouseDown = true });
        internal void Frame(UiInputState? input = null, IUiView? view = null)
        {
            _context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? Idle, new(0, 0, 320, 240), UiTextSettings.Default);
            _context.Render(new Screen(ui =>
            {
                if (Hidden) return;
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(20, 20));
                using var clip = new UiControlContext(ui).Clip(Clip ?? new(0, 0, 320, 240));
                ui.BeginDisabled(Disabled);
                try { (view ?? _widget).Render(ui); }
                finally { ui.EndDisabled(); }
            }));
            using var draw = _context.GetDrawData();
        }
        internal void WidgetDispose() => _widget.Dispose();
        public void Dispose() { _widget.Dispose(); _context.Dispose(); }
    }
    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            new(new(0, 0, width, height), 20) { Content = new(0, 0, width, height) };
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
            new(new(0, 0, size.X, size.Y), 20) { Content = new(0, 0, size.X, size.Y) };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit) => Last = edit;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
