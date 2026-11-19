using Duxel.Core;

internal static class SingleLineDragTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var fluent in new[] { false, true })
        {
            using var editor = new Editor(fluent);
            void Check(bool condition, string message) => check(condition, (fluent ? "Fluent" : "standard") + " input drag : " + message);
            editor.Press(33);
            var anchor = editor.Selection.Start;
            editor.Drag(250, -50);
            Check(editor.Selection.Start == anchor && editor.Caret > anchor && editor.Scroll > 0,
                "capture extends beyond the root on both axes and begins horizontal scrolling");
            Check(editor.Requests > 0, "stationary overflow dragging requests another frame");
            for (var frame = 0; frame < 120; frame++) editor.Drag(250);
            Check(editor.Caret == editor.Text.Length && editor.Scroll > 0, "held overflow drag reaches the complete source endpoint");
            var requests = editor.Requests;
            editor.Drag(250);
            Check(editor.Requests == requests, "scrolling at its endpoint does not schedule redundant frames");
            for (var frame = 0; frame < 120; frame++) editor.Drag(-100);
            Check(editor.Caret == 0 && editor.Selection.Start == anchor && editor.Scroll == 0,
                "reversing overflow drag reaches the original source beginning without changing its anchor");
            editor.Frame(editor.Idle with { LeftMouseReleased = true });
            var released = editor.Selection;
            editor.Drag(80);
            Check(editor.Selection == released, "release ends capture even if a later frame reports only a held button");
            editor.Reset();
            editor.State.ActiveId = editor.Id;
            editor.Drag(80);
            Check(!editor.Selection.HasSelection && editor.Caret == 0, "keyboard focus alone cannot start pointer selection");
            editor.Reset();
            editor.Frame(editor.Idle with { MousePosition = new(33, 30), LeftMousePressed = true, LeftMouseReleased = true });
            editor.Drag(80);
            Check(!editor.Selection.HasSelection, "a press and release delivered together cannot leave a drag owner");
            editor.Reset();
            editor.Press(33);
            editor.State.ActiveId = "another-control";
            editor.State.ActiveId = editor.Id;
            editor.Drag(80);
            Check(!editor.Selection.HasSelection, "transferring active control cancels capture before focus returns");
            editor.Reset();
            editor.Press(33);
            editor.Hidden = true;
            editor.Drag(250);
            editor.Hidden = false;
            editor.Drag(80);
            Check(!editor.Selection.HasSelection, "omitting the owner for a frame ends its drag");
            editor.Reset();
            editor.Press(33);
            editor.Clip = new(300, 180, 10, 10);
            editor.Drag(250);
            editor.Clip = null;
            editor.Drag(80);
            Check(!editor.Selection.HasSelection, "a fully clipped owner releases capture before becoming visible again");
            editor.Reset();
            editor.Clip = new(60, 20, 40, 32);
            editor.Press(33);
            Check(editor.State.ActiveId != editor.Id, "a clipped-away press cannot claim drag capture");
            editor.Clip = null;
            editor.Reset();
            editor.Press(33);
            editor.Disabled = true;
            editor.Drag(250);
            editor.Disabled = false;
            editor.State.ActiveId = editor.Id;
            editor.Drag(80);
            Check(!editor.Selection.HasSelection, "disabling the owner ends capture without restoring it on focus");
            editor.Reset();
            editor.Press(33);
            editor.State.AddPopupBlockingRect(new(0, 0, 320, 240), 1);
            editor.Drag(250);
            editor.Drag(80);
            Check(!editor.Selection.HasSelection, "a higher popup cancels a background drag");
            editor.Reset();
            editor.ReadOnly = true;
            editor.Press(33);
            for (var frame = 0; frame < 120; frame++) editor.Drag(250);
            Check(editor.Caret == editor.Text.Length && editor.Selection.HasSelection && editor.Writes == 0,
                "readonly dragging selects and scrolls without writing the binding");
            editor.Text = "hi";
            editor.Drag(250);
            Check(editor.Scroll == 0 && editor.Caret == 2 && editor.Selection.End == 2,
                "external source shrink clamps scrolling and selection while capture continues");
            editor.WidgetDispose();
            Check(editor.State.TextInputStorage == (0, 0) && editor.State.ScrollStorage == (0, 0),
                "disposal releases editing and scrolling state while capture is held");
        }
        using var slow = new Editor(false);
        using var fast = new Editor(false);
        slow.Text = fast.Text = string.Concat(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 40));
        slow.Frame(); fast.Frame();
        slow.Press(33); fast.Press(33);
        slow.Delta = 1f / 30;
        fast.Delta = 1f / 60;
        for (var frame = 0; frame < 30; frame++) slow.Drag(250);
        for (var frame = 0; frame < 60; frame++) fast.Drag(250);
        check(slow.Scroll > 0 && MathF.Abs(slow.Scroll - fast.Scroll) < 0.01f,
            "single-line overflow drag covers the same distance at 30 and 60 frames per second");
    }

    private sealed class Editor : IDisposable
    {
        private readonly UiContext _context;
        private readonly UiInput<UiStandardTextInputBehavior, Presentation>? _standard;
        private readonly UiInput<UiFluentTextInputBehavior, Presentation>? _fluent;
        internal readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        internal string Text = "abcdefghijklmnopqrstuvwxyz";
        internal int Writes;
        internal int Requests;
        internal string Id { get; }
        internal bool Hidden;
        internal bool Disabled;
        internal float Delta = 1f / 60;
        internal UiRect? Clip;
        internal UiState State => _context.State;
        internal int Caret => State.GetCursor(Id, 0);
        internal UiTextSelection Selection => State.GetSelection(Id);
        internal float Scroll => State.GetScrollX(Id);
        internal bool ReadOnly
        {
            set
            {
                var options = new UiInputOptions(value ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None);
                if (_standard is not null) _standard.Options = options;
                if (_fluent is not null) _fluent.Options = options;
            }
        }
        internal Editor(bool fluent)
        {
            var glyphs = Enumerable.Range(32, 95).ToDictionary(code => code,
                code => new UiGlyphInfo(7, 0, -10, code == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
            _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255], glyphs,
                new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
            _context.SetRequestFrameCallback(() => Requests++);
            var binding = new UiBinding<string>(() => Text, next => { Text = next; Writes++; });
            if (fluent) _fluent = new("Drag", binding, 4096, 80);
            else _standard = new("Drag", binding, 4096, 80);
            Frame();
            Id = Presentation.Last.Id;
            Reset();
        }
        internal void Reset()
        {
            Frame();
            State.ActiveId = null;
            State.SetCursor(Id, 0);
            State.SetSelection(Id, default);
            State.SetScrollX(Id, 0);
            State.RegisterClick("reset-drag-click-count", new(-100, -100));
            Requests = 0;
        }
        internal void Press(float x) => Frame(Idle with { MousePosition = new(x, 30), LeftMouseDown = true, LeftMousePressed = true });
        internal void Drag(float x, float y = 30) => Frame(Idle with { MousePosition = new(x, y), LeftMouseDown = true });
        internal void Frame(UiInputState? input = null)
        {
            _context.NewFrame(new(Delta, new(320, 240), new(1, 1)), input ?? Idle, new(0, 0, 320, 240), UiTextSettings.Default);
            _context.Render(new Screen(ui =>
            {
                if (Hidden) return;
                ui.SetCursorScreenPos(new(20, 20));
                using var clip = new UiControlContext(ui).Clip(Clip ?? new(0, 0, 320, 240));
                ui.BeginDisabled(Disabled);
                try { if (_standard is not null) _standard.Render(ui); else _fluent!.Render(ui); }
                finally { ui.EndDisabled(); }
            }));
            using var draw = _context.GetDrawData();
        }
        internal void WidgetDispose() { _standard?.Dispose(); _fluent?.Dispose(); }
        public void Dispose() { WidgetDispose(); _context.Dispose(); }
    }
    private readonly struct Presentation : IUiInputPresentation
    {
        internal static UiInputEditResult Last;
        public static UiInputLayout Measure(UiControlContext context, string label, float width) =>
            new(new(width, 32), new(0, 0, width, 32), new(6, 6, width - 12, 20), default, 20);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit) => Last = edit;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
