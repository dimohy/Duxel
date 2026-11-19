using System.Buffers;
using System.Reflection;
using Duxel.Core;

internal static class InputKernelTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunObserverOrder(check);
        RunIme(check);
        var items = typeof(UiTextHistory).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var poisoned = ArrayPool<string>.Shared.Rent(8);
        Array.Fill(poisoned, "previous unrelated owner");
        ArrayPool<string>.Shared.Return(poisoned, false);
        var history = new UiTextHistory("first");
        var first = (string[])items.GetValue(history)!;
        check(first.Skip(1).All(x => x is null), "input history admission clears references left in unused pooled slots");
        for (var i = 0; i <= first.Length; i++)
            history.Push("value" + i);
        check(first.All(x => x is null), "growing input history clears all references before returning the old array");
        check(history.Undo(out _) && history.Redo(out var restored) && restored == "value" + first.Length, "pool cleanup preserves undo and redo contents");
        var last = (string[])items.GetValue(history)!;
        history.ReleasePooled();
        history.ReleasePooled();
        check(last.All(x => x is null), "input history disposal returns an entirely cleared array without double release");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var clipboard = new Clipboard();
        context.SetClipboard(clipboard);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = "start";
        var writes = 0;
        var disabled = false;
        using var widget = new InheritedInput(new(() => value, next => { value = next; writes++; }));
        void Step(UiInputState? input = null, Action<UiImmediateContext>? render = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(render ?? (ui => { ui.SetCursorScreenPos(new(10, 10)); ui.BeginDisabled(disabled); widget.Render(ui); ui.EndDisabled(); })));
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
        Step();
        check(writes == 0, "generated retained input does not write its binding during idle frames");
        Step(idle with
        {
            MousePosition = new(25, 45),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Step(idle with
        {
            MousePosition = new(25, 45),
            LeftMouseReleased = true
        });
        var identity = context.State.ActiveId;
        check(identity is not null, "alternate input layout routes pointer focus to its field below the label");
        Key(UiKey.End);
        Step(idle with
        {
            CharEvents = [new('!')]
        });
        check(value == "start!" && writes == 1 && widget.Changes == 1, "generated inherited input consumes shared editing and callback exactly once");
        Key(UiKey.A, KeyModifiers.Ctrl);
        Key(UiKey.C, KeyModifiers.Ctrl);
        check(clipboard.Text == "start!", "input selection and clipboard copy preserve existing editing semantics");
        clipboard.Text = "new";
        Key(UiKey.V, KeyModifiers.Ctrl);
        check(value == "new", "input paste replaces selected text through shared buffer");
        Key(UiKey.Z, KeyModifiers.Ctrl);
        check(value == "start!", "input undo survives separated presentation");
        Key(UiKey.Y, KeyModifiers.Ctrl);
        check(value == "new", "input redo survives separated presentation");
        widget.MaxLength = 4;
        Key(UiKey.A, KeyModifiers.Ctrl);
        Step(idle with
        {
            CharEvents = [new('a'), new('b'), new('c'), new('d'), new('e')]
        });
        check(value == "abcd" && writes == 5, "single-frame character sequence respects max length and one binding callback");
        widget.Label = "Renamed";
        widget.MaxLength = 8;
        Key(UiKey.End);
        Step(idle with
        {
            CharEvents = [new('e')]
        });
        check(context.State.ActiveId == identity && value == "abcde", "renaming retained input preserves editing identity and selection history");
        disabled = true;
        Step(idle with
        {
            CharEvents = [new('Z')]
        });
        check(value == "abcde" && writes == 6 && context.State.ActiveId is null, "disabling an already active input prevents edits and relinquishes text focus");
        check(context.State.TextInputStorage == (1, 1), "one retained input owns one edit buffer and one history");
        widget.Dispose();
        check(context.State.TextInputStorage == (0, 0), "disposing retained input releases common engine edit/history entries");
        var rejected = false;
        try
        {
            _ = widget.Value;
        }
        catch (ObjectDisposedException) { rejected = true; }
        check(rejected, "disposed input rejects binding access");
        disabled = false;
        var direct = "direct";
        using var owner = new UiTextInputOwner();
        Step(render: ui => ui.InputText<UiStandardTextInputBehavior, UiContextInputPresentation>("Direct", ref direct, 32, owner: owner));
        check(context.State.TextInputStorage == (1, 1), "direct generic input uses the same explicitly owned editing storage");
        owner.Dispose();
        check(context.State.TextInputStorage == (0, 0), "direct input owner releases history independently of presentation");
        rejected = false;
        try
        {
            Step(render: ui => ui.InputText<UiStandardTextInputBehavior, UiContextInputPresentation>("Direct", ref direct, 32, owner: owner));
        }
        catch (ObjectDisposedException) { rejected = true; }
        check(rejected && context.State.TextInputStorage == (0, 0), "disposed input owner is rejected before creating editing state");
        rejected = false;
        try
        {
            Step(render: ui => ui.InputText<UiStandardTextInputBehavior, InvalidInputPresentation>("Invalid", ref direct, 32));
        }
        catch (InvalidOperationException e) when (e.Message == "Input presentation returned invalid layout.") { rejected = true; }
        check(rejected && context.State.TextInputStorage == (0, 0), "invalid presentation fails before edit buffers are allocated");
    }
    private static void RunObserverOrder(Action<bool, string> check)
    {
        int[] Observe(int mode)
        {
            var atlas = new UiFontAtlas(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
                new Dictionary<int, UiGlyphInfo> { { 63, new(8, 0, 0, 8, 14, new(0, 0, 1, 1)) } }, new Dictionary<uint, float>(), 14, -4, 0, 63);
            using var context = new UiContext(atlas, new(1), new(2));
            var observations = new List<int>();
            var input = new UiInputState(new(90, 20), true, false, true, false, false, false, 0, 0, [], [], default, default);
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default with
            {
                MissingGlyphObserver = observations.Add
            });
            context.Render(new Screen(ui =>
            {
                ui.SetCursorScreenPos(new(10, 10));
                ui.PushItemWidth(140);
                try
                {
                    var value = "content";
                    if (mode == 0)
                        ui.InputTextLegacyReference("Label", ref value, 64);
                    else if (mode == 1)
                        ui.InputText("Label", ref value, 64);
                    else
                        ui.InputText<UiStandardTextInputBehavior, LateLabelInputPresentation>("Label", ref value, 64);
                }
                finally { ui.PopItemWidth(); }
            }));
            context.GetDrawData().ReleasePooled();
            return observations.ToArray();
        }
        var before = Observe(0);
        var after = Observe(1);
        var late = Observe(2);
        var same = before.Length > 0 && before.AsSpan().SequenceEqual(after);
        check(same, "input extraction preserves actual missing-glyph observer count and order against the frozen reference" +
            (same ? string.Empty : $" (reference: {string.Join(',', before)}; current: {string.Join(',', after)})"));
        check(!before.AsSpan().SequenceEqual(late), "observer-order regression distinguishes a late-label painter even when final geometry matches");
    }
    internal static void RunIme(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var ime = new Ime();
        context.SetImeHandler(ime);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = string.Empty;
        var writes = 0;
        using var widget = new CompiledTestInput("IME", new(() => value, next => { value = next; writes++; }), 64, 180);
        void Step(UiInputState input)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.SetCursorScreenPos(new(10, 10)); widget.Render(ui); }));
            using var data = context.GetDrawData();
        }
        ime.Composition = "한";
        Step(idle with
        {
            MousePosition = new(25, 45),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        check(ime.Owner is not null && ime.CaretCalls > 0 && writes == 0, "separated input presentation preserves IME owner and caret without committing composition");
        ime.Pending = "글";
        ime.Composition = null;
        Step(idle with
        {
            CharEvents = [new('Z')]
        });
        check(value == "글" && writes == 1, "IME commit changes the binding once and does not duplicate simultaneous character events");
        ime.ThrowOnRelease = true;
        var rejected = false;
        try
        {
            widget.Dispose();
        }
        catch (AggregateException error) when (error.InnerExceptions.Count == 1 && error.InnerExceptions[0] is InvalidOperationException failure && failure.Message == "Expected IME release failure") { rejected = true; }
        finally { ime.ThrowOnRelease = false; }
        check(rejected && context.State.TextInputStorage == (0, 0), "IME release failures remain visible after editing storage has been released");
        rejected = false;
        try
        {
            _ = widget.Value;
        }
        catch (ObjectDisposedException) { rejected = true; }
        check(rejected, "IME release failure cannot retain the widget binding");
    }
    private sealed class Ime : IUiImeHandler
    {
        internal string? Owner, Composition, Pending; internal int CaretCalls; internal bool ThrowOnRelease;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
            CaretCalls++;
        }
        public string? GetCompositionText() => Composition;
        public void SetCompositionOwner(string? id)
        {
            if (id is null && ThrowOnRelease)
                throw new InvalidOperationException("Expected IME release failure");
            Owner = id;
        }
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
        internal string Text = string.Empty; public string GetText() => Text; public void SetText(string text) => Text = text;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private sealed class InheritedInput(UiBinding<string> value) : CompiledTestInput("Name", value, 16, 180)
    {
        internal int Changes; protected override void OnChanged(string value)
        {
            Changes++;
            base.OnChanged(value);
        }
    }
}

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiStandardTextInputBehavior), typeof(AlternateInputPresentation))]
internal partial class CompiledTestInput;

internal readonly struct AlternateInputPresentation : IUiInputPresentation
{
    public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static UiInputLayout Measure(UiControlContext context, string label, float width) => new(new(width, 64), new(0, 24, width, 32), new(8, 30, width - 16, 20), default, 20);
    public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit)
    {
        context.TypographyButtonText(label, new(layout.LabelPosition.X, layout.LabelPosition.Y, layout.Size.X, 20), context.MeasureText(label), context.Theme.Text);
        context.Frame(layout.Field, new(32, 50, 64), edit.Active ? new(80, 200, 255) : new(120, 140, 160), 8, 2);
        context.PaintEditableText(layout, edit, new(new(240, 250, 255), new(30, 90, 160), new(255, 220, 100)));
    }
}
internal readonly struct InvalidInputPresentation : IUiInputPresentation
{
    public static UiInputLayout Measure(UiControlContext context, string label, float width) => new(new(float.NaN, 20), default, default, default, 20);
    public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit) => throw new InvalidOperationException("Unreachable paint");
}

internal readonly struct LateLabelInputPresentation : IUiInputPresentation
{
    public static UiInputLayout Measure(UiControlContext context, string label, float width) => UiContextInputPresentation.Measure(context, label, width);
    public static UiInputLayout Place(UiControlContext context, in UiInputLayout layout, UiVector2 origin) => UiContextInputPresentation.Place(context, layout, origin);
    public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit)
    {
        context.PaintContextInputLabel(label, layout);
        context.PaintContextInput(layout, edit);
    }
}
