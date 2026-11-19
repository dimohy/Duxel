using Duxel.Core;

internal static class MultilineHardLineTests
{
    internal static void Run(Action<bool, string> check)
    {
        var glyphs = Enumerable.Range(32, 95).ToDictionary(code => code,
            code => new UiGlyphInfo(7, 0, -10, code == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
        glyphs[0x1f600] = new(14, 0, -10, 13, 12, new(0, 0, 1, 1));
        glyphs[0x301] = new(0, 0, -10, 0, 12, new(0, 0, 1, 1));
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
            glyphs, new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
        var clipboard = new Clipboard();
        context.SetClipboard(clipboard);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var text = "ab\r\nc\rd\n😀e\u0301\n";
        var reads = 0;
        var writes = 0;
        using var editor = new UiMultilineInput<UiStandardMultilineInputBehavior, Presentation>("Hard lines",
            new(() => { reads++; return text; }, next => { text = next; writes++; }), 4096, 49, 42)
        {
            Options = new() { Flags = UiInputTextFlags.ReadOnly, Wrap = false }
        };
        void Frame(UiInputState? input = null, Action<UiImmediateContext>? probe = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 20));
                if (probe is null)
                    editor.Render(ui);
                else
                    probe(ui);
            }));
            using var draw = context.GetDrawData();
        }
        void Key(UiKey key, KeyModifiers modifiers = default) => Frame(idle with
        {
            KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)],
            Modifiers = modifiers
        });
        Frame(probe: ui =>
        {
            check(editor.Measure(ui, new(320, 240)) == new UiVector2(49, 42) && reads == 0 && writes == 0,
                "nonwrapping readonly measurement does not read the source or realize hard lines");
        });
        Frame();
        var id = Presentation.Last.Id;
        var lines = Presentation.Last.VisualLines;
        check(lines?.Count == 5 && lines[0].Start == 0 && lines[0].End == 2
            && lines[1].Start == 4 && lines[1].End == 5 && lines[2].Start == 6 && lines[2].End == 7
            && lines[3].Start == 8 && lines[3].End == 12 && lines[4].Start == 13,
            "nonwrapping readonly CRLF,CR,LF and trailing empty lines retain original UTF16 source ranges");
        check(text == "ab\r\nc\rd\n😀e\u0301\n" && writes == 0 && Presentation.Last.ContentHeight == 100,
            "hard-line layout preserves the readonly model and counts every original hard break");
        var builds = lines?.Builds;
        Frame();
        check(lines is not null && ReferenceEquals(lines, Presentation.Last.VisualLines) && lines.Builds == builds,
            "unchanged nonwrapping hard-line data reuses its owned source and geometry key");
        context.State.ActiveId = id;
        context.State.SetCursor(id, 2);
        context.State.SetSelection(id, new(2, 2));
        Key(UiKey.RightArrow);
        check(Presentation.Last.CaretIndex == 4, "nonwrapping Right skips the entire CRLF element");
        Key(UiKey.LeftArrow);
        check(Presentation.Last.CaretIndex == 2, "nonwrapping Left skips the entire CRLF element");
        context.State.SetCursor(id, 3);
        context.State.SetSelection(id, new(3, 3));
        Frame();
        check(Presentation.Last.CaretIndex == 2, "nonwrapping caret synchronization snaps a source index inside CRLF to its boundary");
        context.State.SetCursor(id, 4);
        context.State.SetSelection(id, new(4, 4));
        Key(UiKey.End);
        check(Presentation.Last.CaretIndex == 5, "nonwrapping End stops before a standalone CR");
        Key(UiKey.DownArrow);
        Key(UiKey.Home);
        check(Presentation.Last.CaretIndex == 6, "nonwrapping vertical movement and Home use the next CR-delimited source line");
        context.State.SetCursor(id, 8);
        context.State.SetSelection(id, new(8, 8));
        Key(UiKey.RightArrow);
        check(Presentation.Last.CaretIndex == 10, "nonwrapping navigation keeps supplementary text elements whole");
        Key(UiKey.RightArrow, KeyModifiers.Shift);
        check(Presentation.Last.Selection == new UiTextSelection(10, 12), "nonwrapping Shift navigation selects the complete combining element");
        context.State.SetCursor(id, 6);
        context.State.SetSelection(id, new(6, 6));
        context.State.SetScrollY(id, 40);
        Frame(idle with
        {
            MousePosition = new(27, 24),
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            LeftMouseReleased = true
        });
        check(Presentation.Last.CaretIndex == 7, "scrolled nonwrapping pointer hits return the original CR-delimited line index");
        context.State.RegisterClick("reset-hard-line-clicks", new(-100, -100));
        for (var click = 0; click < 3; click++)
        {
            context.State.SetScrollY(id, 20);
            Frame(idle with
            {
                MousePosition = new(27, 24),
                LeftMousePressed = true,
                LeftMouseDown = true
            });
            Frame(idle with
            {
                LeftMouseReleased = true
            });
        }
        check(Presentation.Last.Selection == new UiTextSelection(4, 5),
            "nonwrapping triple click selects one CR-delimited source line");
        Key(UiKey.A, KeyModifiers.Ctrl);
        Key(UiKey.C, KeyModifiers.Ctrl);
        check(clipboard.Text == text && writes == 0, "readonly hard-line copy preserves every original CRLF and CR code unit");

        text = "wwwwww\nx\nx";
        context.State.ActiveId = null;
        Frame();
        var bar = context.GetStyleData().ScrollbarSize;
        check(Presentation.Last.ContentWidth == 42 && Presentation.Last.MaximumScrollX == 42 - (49 - bar)
            && Presentation.Last.MaximumScrollY == 60 - (42 - bar),
            "vertical overflow can introduce horizontal overflow and reserve both scrollbar gutters");
        text = "wwwwwwwwww\nx";
        Frame();
        check(Presentation.Last.MaximumScrollX == 70 - (49 - bar) && Presentation.Last.MaximumScrollY == 40 - (42 - bar),
            "horizontal overflow can introduce vertical overflow through the actual bottom gutter");
        context.State.ActiveId = id;
        context.State.SetCursor(id, 10);
        context.State.SetSelection(id, new(10, 10));
        Key(UiKey.End);
        check(Presentation.Last.ScrollX > 0 && Presentation.Last.MaximumScrollX > 0,
            "End reveals a nonwrapping caret that is already at its logical endpoint");
        context.State.SetScrollX(id, 0);
        context.State.SetCursor(id, 0);
        context.State.SetSelection(id, default);
        Key(UiKey.End);
        check(Presentation.Last.ScrollX > 0, "nonwrapping caret movement scrolls horizontally within its own hard line");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.End, true, default)]
        });
        context.State.SetScrollX(id, 0);
        context.State.SetRepeatTime(UiKey.End, context.State.TimeSeconds - 1);
        Frame();
        check(Presentation.Last.ScrollX > 0, "held navigation reveals a nonwrapping caret even when its source index does not change");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.End, false, default)]
        });
        editor.Options = editor.Options with
        {
            Wrap = true
        };
        Frame();
        check(Presentation.Last.VisualLines?.Count > 2 && Presentation.Last.MaximumScrollX == 0,
            "changing hard-line mode to wrapping rebuilds visual rows and removes horizontal overflow");
        editor.Options = editor.Options with
        {
            Wrap = false
        };
        Frame();
        check(Presentation.Last.VisualLines?.Count == 2 && Presentation.Last.MaximumScrollX > 0,
            "changing wrapping back to hard lines invalidates the owned mode key");
        context.State.ActiveId = id + "##scroll-h";
        text = "short";
        Frame();
        check(context.State.ActiveId is null && Presentation.Last.MaximumScrollX == 0 && Presentation.Last.MaximumScrollY == 0
            && context.State.GetScrollX(id) == 0 && context.State.GetScrollY(id) == 0,
            "hard-line source shrink releases captured horizontal scrollbars and clamps both offsets");
        editor.Options = default;
        Frame();
        check(Presentation.Last.VisualLines is null && lines?.Count == 0,
            "standard nonwrapped editable mode releases optional line data while preserving its legacy contract");
        editor.Dispose();
        check(context.State.TextInputStorage == (0, 0) && context.State.ScrollStorage == (0, 0),
            "hard-line editor disposal releases source,history and both scrollbar owners");
        var value = "😀e\u0301\nx";
        using var editable = new UiMultilineInput<UiFluentMultilineInputBehavior, Presentation>("Editable hard lines",
            new(() => value, next => value = next), 64, 49, 42)
        {
            Options = new() { Wrap = false }
        };
        Frame(probe: ui => editable.Render(ui));
        var editableId = Presentation.Last.Id;
        context.State.ActiveId = context.State.FocusedId = editableId;
        context.State.SetCursor(editableId, 2);
        context.State.SetSelection(editableId, new(2, 2));
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Backspace, true, default), new(UiKey.Backspace, false, default)]
        }, ui => editable.Render(ui));
        check(value == "e\u0301\nx", "Fluent nonwrapping editing deletes a supplementary element atomically");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl), new(UiKey.Z, false, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        }, ui => editable.Render(ui));
        check(value == "😀e\u0301\nx", "Fluent nonwrapping text-element deletion retains undo");
        value = "ab";
        context.State.SetCursor(editableId, 0);
        context.State.SetSelection(editableId, default);
        var ime = new Ime { Preview = "wwwwwwwwww\r\nq" };
        context.SetImeHandler(ime);
        Frame(probe: ui => editable.Render(ui));
        check(value == "ab" && Presentation.Last.CompositionText == "wwwwwwwwww\nq"
            && Presentation.Last.CompositionLines?.Count == 2 && Presentation.Last.MaximumScrollX > 0 && Presentation.Last.MaximumScrollY > 0,
            "nonwrapping IME preview uses normalized hard lines and both scrollbar extents without writing the model");
        ime.Preview = null;
        Frame(probe: ui => editable.Render(ui));
        check(Presentation.Last.CompositionLines is null && Presentation.Last.MaximumScrollX == 0 && Presentation.Last.MaximumScrollY == 0,
            "ending nonwrapping IME preview releases display rows and disappearing overflow");
        editable.Dispose();
        check(context.State.TextInputStorage == (0, 0) && context.State.ScrollStorage == (0, 0),
            "nonwrapping navigable editor disposal releases all source and preview ownership");
    }

    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            new(new(0, 0, width, height), 20)
            {
                Content = new(0, 0, width, height)
            };
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
            new(new(0, 0, size.X, size.Y), 20)
            {
                Content = new(0, 0, size.X, size.Y)
            };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
        {
            Last = edit;
            context.PaintEditableMultiline(layout, edit, new(new(240, 240, 240), new(60, 100, 160), new(255, 255, 255)));
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private sealed class Clipboard : IUiClipboard
    {
        internal string Text = "";
        public string GetText() => Text;
        public void SetText(string text) => Text = text;
    }
    private sealed class Ime : IUiImeHandler
    {
        internal string? Preview;
        public string? GetCompositionText() => Preview;
        public string? ConsumeCommittedText(string inputId) => null;
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public void SetCompositionOwner(string? inputId)
        {
        }
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
        }
    }
}
