using Duxel.Core;
using BaseInput = Duxel.Core.UiFluentInputPresentation<Duxel.Core.UiFluentMediumInputSize, Duxel.Core.UiFluentOutlineInput>;

internal static class InputChildrenTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = "abc";
        var edits = 0;
        var itemRectsCorrect = true;
        var disabled = false;
        var rootFocused = false;
        var beforeCalls = 0;
        var afterCalls = 0;
        using var field = new ChildTestInput("Value", new(() => value, v => { value = v; edits++; }), 64, 240);
        var before = new ChildButton("?", () => beforeCalls++) { Size = new(24, 24) };
        var after = new ChildButton("x", () => afterCalls++) { Size = new(24, 24) };
        var beforeSpec = new UiInputSlot("help", before, new(24, 24));
        var afterSpec = new UiInputSlot("clear", after, new(24, 24));
        field.SetSlots(beforeSpec, afterSpec);
        (int Builders, int Depth) capture = default;
        void Step(UiInputState? input = null, UiWidget? content = null)
        {
            context.NewFrame(new(1f / 60, new(1200, 240), new(1, 1)), input ?? idle, new(0, 0, 1200, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.SetCursorScreenPos(new(10, 10));
                ui.BeginDisabled(disabled);
                (content ?? field).Render(ui);
                ui.EndDisabled();
                rootFocused = ui.IsItemFocused();
                capture = ui.InputSlotCaptureStorage;
                if (content is null)
                    itemRectsCorrect &= ui.GetItemRectMin() == new UiVector2(10, 10) && ui.GetItemRectMax() == new UiVector2(250, 42);
            }));
            using var data = context.GetDrawData();
        }
        void Key(UiKey key, KeyModifiers mods = default)
        {
            Step(idle with
            {
                KeyEvents = [new(key, true, mods)]
            });
            Step(idle with
            {
                KeyEvents = [new(key, false, mods)]
            });
        }
        Step();
        check(ChildInputPresentation.Layout.HasBeforeChild && ChildInputPresentation.Layout.HasAfterChild, "public presentation layout identifies the owned child slots");
        Key(UiKey.Tab);
        var identity = context.State.FocusedId;
        check(before.Focused && !rootFocused && !after.Focused && ChildInputPresentation.Last.FocusWithin && !ChildInputPresentation.Last.Active, "Tab reaches the real prefix button before the editor and paints focus-within");
        Key(UiKey.Enter);
        check(beforeCalls == 1, "prefix button uses the existing Enter behavior exactly once");
        Key(UiKey.Tab);
        check(rootFocused && context.State.ActiveId is not null && !before.Focused, "second Tab reaches the shared text editor");
        Key(UiKey.End);
        Step(idle with
        {
            CharEvents = [new('d')]
        });
        check(value == "abcd" && edits == 1, "editor between real child widgets retains normal input and one callback");
        Key(UiKey.Tab);
        check(after.Focused && !rootFocused && ChildInputPresentation.Last.FocusWithin, "third Tab reaches the suffix button without losing parent focus-within");
        Step(idle with
        {
            KeyEvents = [new(UiKey.Space, true, default)]
        });
        check(afterCalls == 0, "suffix Space press does not activate early");
        Step(idle with
        {
            KeyEvents = [new(UiKey.Space, false, default)]
        });
        check(afterCalls == 1, "suffix Space release activates once");
        Key(UiKey.Tab, KeyModifiers.Shift);
        var ime = new Ime();
        context.SetImeHandler(ime);
        Step();
        check(ime.Owner == context.State.ActiveId && ime.Owner is not null, "focused parent editor owns IME before a child click");
        Step(idle with
        {
            MousePosition = new(before.Bounds.X + 5, before.Bounds.Y + 5),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        check(before.Focused && ime.Owner is null && ChildInputPresentation.Last.FocusWithin && !ChildInputPresentation.Last.Active, "child press releases editor IME and updates focus-within in the same frame");
        Step(idle with
        {
            MousePosition = new(before.Bounds.X + 5, before.Bounds.Y + 5),
            LeftMouseReleased = true
        });
        check(beforeCalls == 2, "real prefix pointer release activates once");
        using (var other = Fluent.Input("Other", new(() => value, v => value = v), 64, 240))
        {
            var rejected = false;
            try
            {
                other.BeforeSlot = beforeSpec;
            }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected && other.BeforeSlot is null && !before.Disposed, "foreign slot ownership is rejected atomically before rendering");
        }
        using (var unsupported = new UiInput<UiStandardTextInputBehavior, UiContextInputPresentation>("Legacy", new(() => value, v => value = v), 64, 100))
        {
            var candidate = new ChildButton("candidate", null);
            var rejected = false;
            try
            {
                unsupported.BeforeSlot = new("candidate", candidate, new(24, 24));
            }
            catch (NotSupportedException) { rejected = true; }
            unsupported.Dispose();
            check(rejected && !candidate.Disposed && unsupported.BeforeSlot is null, "unsupported presentations reject child adoption instead of silently ignoring it");
            candidate.Dispose();
        }
        using (var ancestor = Fluent.Input("Ancestor", new(() => value, v => value = v), 64, 100))
        {
            var descendant = Fluent.Input("Descendant", new(() => value, v => value = v), 64, 60);
            ancestor.BeforeSlot = new("descendant", descendant, new(60, 32));
            var rejected = false;
            try
            {
                descendant.AfterSlot = new("ancestor", ancestor, new(100, 34));
            }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected && descendant.AfterSlot is null && ancestor.BeforeSlot?.Content == descendant, "ancestor ownership cycles are rejected before rendering and preserve the valid tree");
        }
        var replacement = new ChildButton("new", null);
        var invalid = false;
        try
        {
            field.BeforeSlot = new("help", replacement, new(24, 24));
        }
        catch (InvalidOperationException) { invalid = true; }
        check(invalid && field.BeforeSlot == beforeSpec && !replacement.Disposed, "a retained slot key cannot silently replace its widget instance");
        replacement.Dispose();
        invalid = false;
        try
        {
            field.SetSlots(beforeSpec, new("help", after, new(24, 24)));
        }
        catch (InvalidOperationException) { invalid = true; }
        check(invalid && field.BeforeSlot == beforeSpec && field.AfterSlot == afterSpec, "duplicate slot keys leave the current owned children unchanged");
        disabled = true;
        var calls = beforeCalls + afterCalls;
        Step(idle with
        {
            MousePosition = new(after.Bounds.X + 5, after.Bounds.Y + 5),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Step(idle with
        {
            LeftMouseReleased = true
        });
        check(beforeCalls + afterCalls == calls && !ChildInputPresentation.Last.FocusWithin, "parent disabled state blocks actual child controls");
        disabled = false;
        Step();
        Step(idle with
        {
            MousePosition = new(after.Bounds.X + 5, after.Bounds.Y + 5),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        field.AfterSlot = null;
        Step(idle with
        {
            LeftMouseReleased = true
        });
        check(after.Disposed && afterCalls == 1, "removing a pressed slot disposes its child without a late callback");
        field.SetSlots(null, beforeSpec);
        Step();
        Step(idle with
        {
            MousePosition = new(before.Bounds.X + 5, before.Bounds.Y + 5),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        check(context.State.FocusedId == identity && !before.Disposed, "moving a keyed widget between slots preserves its input identity");
        Step(idle with
        {
            LeftMouseReleased = true
        });
        var fault = new FailingChild();
        field.SetSlots(null, new("fault", fault, new(24, 24)));
        var failed = false;
        try
        {
            Step();
        }
        catch (InvalidOperationException error) when (error.Message == "Expected child render failure") { failed = true; }
        field.SetSlots(null, null);
        Step();
        check(failed && fault.Disposed && capture.Depth == 0, "child failure restores capture/layout state and removal releases the failed child");
        using (var mutation = new MutatingInput(new(() => value, v => value = v)))
        {
            var retained = new ChildButton("retained", null) { Size = new(24, 24) };
            var spec = new UiInputSlot("retained", retained, new(24, 24));
            mutation.BeforeSlot = spec;
            var rejected = false;
            try
            {
                Step(content: mutation);
            }
            catch (InvalidOperationException error) when (error.Message == "Build must not invalidate itself.") { rejected = true; }
            check(rejected && mutation.BeforeSlot == spec && !retained.Disposed, "Build-time slot clearing is rejected before mutating the owned graph");
            Step(content: mutation);
        }
        var pending = new ChildButton("pending", null);
        var unused = Fluent.Input("Unused", new(() => value, v => value = v), 64, 100);
        unused.AfterSlot = new("pending", pending, new(24, 24));
        unused.Dispose();
        check(pending.Disposed, "disposing before first render releases already adopted slot widgets");
        UiFluentInput nested = Fluent.Input("Leaf", new(() => value, v => value = v), 64, 80);
        float nestedWidth = 80, nestedHeight = 32;
        for (var i = 0; i < 10; i++)
        {
            var parent = Fluent.Input("Nested", new(() => value, v => value = v), 64, nestedWidth + 80);
            parent.BeforeSlot = new("nested", nested, new(nestedWidth, nestedHeight));
            nested = parent;
            nestedWidth += 80;
            nestedHeight += 2;
        }
        Step(content: nested);
        check(capture == (8, 0), "nested real widgets use at most eight retained capture builders and return to depth zero");
        nested.Dispose();
        check(itemRectsCorrect, "all child interactions preserve the parent input item rectangle");
        field.Dispose();
        check(before.Disposed && context.State.TextInputStorage == (0, 0), "input disposal releases its owned child graph and editing storage");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private sealed class Ime : IUiImeHandler
    {
        internal string? Owner; public void SetCompositionOwner(string? id) => Owner = id;
        public void SetCaretRect(UiRect caret, UiRect input, float height, float width)
        {
        }
        public string? GetCompositionText() => null;
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeCommittedText(string id) => null; public string? ConsumeRecentCommittedText() => null;
    }
    private sealed class MutatingInput(UiBinding<string> value) : ChildTestInput("Mutation", value, 64, 200)
    {
        private bool _attempt = true;
        protected override IUiView Build(UiBuildContext context)
        {
            if (_attempt)
            {
                _attempt = false;
                SetSlots(null, null);
            }
            return base.Build(context);
        }
    }
    private sealed class FailingChild : UiWidget
    {
        internal bool Disposed; protected override IUiView Build(UiBuildContext context) => throw new InvalidOperationException("Expected child render failure");
        protected override void OnDispose() => Disposed = true;
    }
}
internal sealed class ChildButton(string text, Action? action) : UiFluentButton(text, action)
{
    internal UiRect Bounds; internal bool Focused, Disposed;
    protected override void RenderControl(UiImmediateContext ui)
    {
        base.RenderControl(ui);
        var min = ui.GetItemRectMin();
        var max = ui.GetItemRectMax();
        Bounds = new(min.X, min.Y, max.X - min.X, max.Y - min.Y);
        Focused = ui.IsItemFocused();
    }
    protected override void OnDispose()
    {
        Disposed = true;
        base.OnDispose();
    }
}
internal readonly struct ChildInputPresentation : IUiInputPresentation
{
    internal static UiInputEditResult Last; internal static UiInputLayout Layout;
    public static bool SupportsChildren => true;
    public static UiControlTypographyScope BeginText(UiControlContext context) => BaseInput.BeginText(context);
    public static UiInputLayout Measure(UiControlContext context, string label, float width) => BaseInput.Measure(context, label, width);
    public static UiInputLayout MeasureContent(UiControlContext context, in UiInputLayout local, UiInputOptions options) => BaseInput.MeasureContent(context, local, options);
    public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit)
    {
        Last = edit;
        Layout = layout;
        BaseInput.Paint(context, label, layout, edit);
    }
}
[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior), typeof(ChildInputPresentation))]
internal partial class ChildTestInput;
