using Duxel.Core;

internal static class FluentInputTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var clipboard = new Clipboard();
        context.SetClipboard(clipboard);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = "abc";
        var writes = 0;
        var disabled = false;
        var focused = false;
        using var field = Fluent.Input("Name", new(() => value, next => { value = next; writes++; }), 64, 180);
        using var outside = Fluent.Button("Outside");
        void Step(UiInputState? input = null, Action<UiImmediateContext>? render = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(render ?? (ui =>
            {
                ui.SetCursorScreenPos(new(10, 10));
                ui.BeginDisabled(disabled);
                field.Render(ui);
                ui.EndDisabled();
                focused = ui.IsItemFocused();
                ui.SetCursorScreenPos(new(10, 70));
                outside.Render(ui);
            })));
            using var data = context.GetDrawData();
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Step(idle with
            {
                KeyEvents = [new(key, true, modifiers)]
            });
            Step(idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
        }
        Step();
        Key(UiKey.Tab);
        check(focused && context.State.ActiveId is not null, "Tab focuses Fluent Input and starts shared editing");
        Key(UiKey.End);
        Step(idle with
        {
            CharEvents = [new('d')]
        });
        check(value == "abcd" && writes == 1, "keyboard-focused Fluent Input edits and notifies once");
        Key(UiKey.Enter);
        check(focused && context.State.ActiveId is not null, "Enter preserves focus in Fluent single-line Input");
        Key(UiKey.Tab);
        check(!focused && context.State.ActiveId is null, "Tab moves from Input to the next control");
        Step(idle with
        {
            CharEvents = [new('Z')]
        });
        check(value == "abcd" && writes == 1, "blurred Input does not consume character events");
        field.Options = new(UiInputTextFlags.ReadOnly | UiInputTextFlags.AutoSelectAll, "Hint");
        Key(UiKey.Tab, KeyModifiers.Shift);
        var id = context.State.ActiveId!;
        check(focused && context.State.GetSelection(id) == new UiTextSelection(0, 4), "keyboard focus can select the complete readonly value");
        Key(UiKey.C, KeyModifiers.Ctrl);
        check(clipboard.Text == "abcd", "readonly Input still supports selection and copy");
        clipboard.Text = "replacement";
        foreach (var key in new[] { UiKey.X, UiKey.V, UiKey.Z, UiKey.Y })
            Key(key, KeyModifiers.Ctrl);
        Key(UiKey.Backspace);
        Key(UiKey.Delete);
        Step(idle with
        {
            CharEvents = [new('Q')]
        });
        check(value == "abcd" && writes == 1 && clipboard.Text == "replacement", "readonly rejects cut/paste/undo/redo/delete/characters without side effects");
        Key(UiKey.Home);
        check(context.State.GetCursor(id, 99) == 0, "readonly Input preserves caret navigation");
        field.Options = default;
        Key(UiKey.End);
        Step(idle with
        {
            CharEvents = [new('e')]
        });
        check(value == "abcde" && writes == 2, "removing readonly restores normal controlled editing");
        disabled = true;
        Step(idle with
        {
            CharEvents = [new('Q')]
        });
        check(value == "abcde" && !focused && context.State.ActiveId is null, "disabled Fluent Input leaves focus and rejects editing");
        var rejected = false;
        try
        {
            field.Options = new((UiInputTextFlags)8);
        }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected && field.Options.Flags == UiInputTextFlags.None, "unknown Input option bits fail before replacing valid options");
        var legacy = "legacy";
        rejected = false;
        try
        {
            Step(render: ui => ui.InputText("Legacy", ref legacy, 64, (UiInputTextFlags)8));
        }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected && legacy == "legacy", "legacy flags overload rejects unknown bits explicitly");
        disabled = false;
        field.Options = new(UiInputTextFlags.AutoSelectAll);
        Step();
        Step(idle with
        {
            MousePosition = new(20, 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        check(context.State.GetSelection(context.State.ActiveId!) == new UiTextSelection(0, 5), "pointer focus applies AutoSelectAll before editing");
        field.Dispose();
        check(context.State.TextInputStorage == (0, 0), "default Fluent Input uses owned editing lifetime");
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
            foreach (var appearance in Enum.GetValues<UiInputAppearance>())
            {
                var off = UiFluentInputPalette.Resolve(scheme, UiButtonVisualState.Disabled, appearance, true);
                check((off.Background.Rgba >> 24) == 0 && (off.Focus.Rgba >> 24) == 0, "disabled Input remains transparent and has no focus stroke: " + scheme + "/" + appearance);
                var rest = UiFluentInputPalette.Resolve(scheme, UiButtonVisualState.Rest, appearance, true);
                var expected = scheme == UiFluentColorScheme.Light ? new UiColor(209, 52, 56) : new UiColor(227, 125, 128);
                check(rest.Border == expected && rest.Bottom == expected, "Input invalid border resolves pinned red primary/tint30: " + scheme + "/" + appearance);
            }
        RunIme(check);
        InputAdornmentTests.Run(check);
        InputChildrenTests.Run(check);
    }
    private static void RunIme(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var ime = new Ime();
        context.SetImeHandler(ime);
        var value = "ab";
        var writes = 0;
        var disabled = false;
        using var field = Fluent.Input("IME", new(() => value, next => { value = next; writes++; }), 64, 180);
        using var next = Fluent.Button("Next");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Step(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.SetCursorScreenPos(new(10, 10)); ui.BeginDisabled(disabled); field.Render(ui); ui.EndDisabled(); ui.SetCursorScreenPos(new(10, 70)); next.Render(ui); }));
            using var data = context.GetDrawData();
        }
        void Tab(KeyModifiers modifiers = default)
        {
            Step(idle with
            {
                KeyEvents = [new(UiKey.Tab, true, modifiers)]
            });
            Step(idle with
            {
                KeyEvents = [new(UiKey.Tab, false, modifiers)]
            });
        }
        Step();
        Tab();
        check(ime.Owner == context.State.ActiveId && ime.Owner is not null, "keyboard focus assigns the IME owner");
        ime.Pending = "c";
        Step(idle with
        {
            CharEvents = [new('Z')]
        });
        check(value == "abc" && writes == 1, "Fluent Input commits IME once without duplicate character events");
        Tab();
        check(ime.Owner is null, "Tab blur releases the IME owner");
        field.Options = new(UiInputTextFlags.ReadOnly);
        Tab(KeyModifiers.Shift);
        check(ime.Owner is null, "readonly keyboard focus does not enable IME composition");
        field.Options = default;
        Step();
        check(ime.Owner is not null, "leaving readonly restores IME ownership");
        disabled = true;
        Step();
        check(ime.Owner is null && context.State.ActiveId is null, "disabling an active navigable Input releases the IME owner");
    }
    private sealed class Ime : IUiImeHandler
    {
        internal string? Owner, Pending;
        public void SetCaretRect(UiRect caret, UiRect input, float height, float width)
        {
        }
        public string? GetCompositionText() => null;
        public void SetCompositionOwner(string? id) => Owner = id;
        public string? ConsumeCommittedText(string id)
        {
            if (Owner != id)
                return null;
            var value = Pending;
            Pending = null;
            return value;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
    }
    private sealed class Clipboard : IUiClipboard
    {
        internal string Text = ""; public string GetText() => Text; public void SetText(string text) => Text = text;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
