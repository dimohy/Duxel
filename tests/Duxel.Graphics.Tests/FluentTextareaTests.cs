using Duxel.Core;

internal static class FluentTextareaTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var clipboard = new Clipboard();
        context.SetClipboard(clipboard);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var text = "abc\nxy";
        var reads = 0;
        var writes = 0;
        var disabled = false;
        using var editor = Fluent.Textarea("Notes", new(() => { reads++; return text; }, next => { text = next; writes++; }), 256, 180, 90);
        using var field = Fluent.Field(editor);
        field.Width = 180;
        field.Options = new()
        {
            Label = "Notes",
            Hint = "Details",
            Required = true,
            ValidationMessage = "Review"
        };
        using var outside = Fluent.Button("Outside");
        void Frame(UiInputState? input = null, Action<UiImmediateContext>? probe = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 20));
                if (probe is not null)
                    probe(ui);
                else
                {
                    ui.BeginDisabled(disabled);
                    field.Render(ui);
                    ui.EndDisabled();
                    ui.SetCursorScreenPos(new(20, 190));
                    outside.Render(ui);
                }
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
            var cursor = ui.GetCursorScreenPos();
            check(editor.Measure(ui, new(320, 240)) == new UiVector2(180, 90) && reads == 0 && writes == 0 && ui.GetCursorScreenPos() == cursor,
                "generated Fluent Textarea measures metadata without reading its binding or advancing the cursor");
            var control = new UiControlContext(ui);
            CheckSize<UiFluentSmallTextareaSize>(control, 44, 8, 4, 16, check);
            CheckSize<UiFluentMediumTextareaSize>(control, 56, 12, 6, 20, check);
            CheckSize<UiFluentLargeTextareaSize>(control, 68, 14, 8, 22, check);
            var rejected = false;
            try
            {
                control.MeasureMultiline<UiFluentTextareaPresentation<UiFluentMediumTextareaSize, UiFluentUnderlineInput>>();
            }
            catch (NotSupportedException) { rejected = true; }
            check(rejected, "Textarea explicitly rejects the unsupported single-line underline appearance");
        });
        Frame();
        check(reads == 1 && writes == 0, "generated Fluent Textarea reads its binding once in a rendered frame");
        var id = tree.Root.First!;
        tree.TryGet(id, out var node);
        check(node.Node.Multiline && node.Node.Focusable && node.Node.ValuePattern && node.Node.Invalid && node.Node.Name == "Notes",
            "Fluent Textarea publishes implemented focus/value actions and inherited Field semantics");
        Key(UiKey.Tab);
        check(context.State.FocusedId == id && context.State.ActiveId == id && text == "abc\nxy" && writes == 0,
            "Tab focuses Fluent Textarea without inserting legacy tab spaces");
        Key(UiKey.End, KeyModifiers.Ctrl);
        Key(UiKey.Enter);
        Frame(idle with
        {
            CharEvents = [new('Q')]
        });
        check(text == "abc\nxy\nQ" && writes == 2, "keyboard-focused Fluent Textarea inserts real newlines and text once per edit");
        Key(UiKey.Escape);
        check(context.State.FocusedId == id && context.State.ActiveId == id, "Escape preserves Fluent Textarea keyboard focus");
        Key(UiKey.Tab);
        Frame(idle with
        {
            CharEvents = [new('!')]
        });
        check(context.State.ActiveId is null && context.State.FocusedId != id && text == "abc\nxy\nQ",
            "Tab leaves Fluent Textarea and the blurred editor does not consume characters");
        editor.Options = new()
        {
            Flags = UiInputTextFlags.ReadOnly | UiInputTextFlags.AutoSelectAll,
            Placeholder = "Hint"
        };
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(context.State.ActiveId == id && context.State.GetSelection(id) == new UiTextSelection(0, text.Length),
            "Shift Tab can select all of a readonly Fluent Textarea");
        Key(UiKey.C, KeyModifiers.Ctrl);
        check(clipboard.Text == text, "readonly Fluent Textarea supports actual selection copy");
        clipboard.Text = "replacement";
        var readonlyText = text;
        var readonlyWrites = writes;
        foreach (var key in new[] { UiKey.X, UiKey.V, UiKey.Z, UiKey.Y })
            Key(key, KeyModifiers.Ctrl);
        foreach (var key in new[] { UiKey.Backspace, UiKey.Delete, UiKey.Enter, UiKey.Tab })
        {
            if (key == UiKey.Tab)
                continue;
            Key(key);
        }
        Frame(idle with
        {
            CharEvents = [new('z')]
        });
        check(text == readonlyText && writes == readonlyWrites && clipboard.Text == "replacement",
            "readonly Fluent Textarea blocks cut,paste,undo,redo,deletion,newline and character mutations");
        context.State.SetRepeatTime(UiKey.Backspace, context.State.TimeSeconds - 1);
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Backspace, true, default)]
        });
        context.State.SetRepeatTime(UiKey.Backspace, context.State.TimeSeconds - 1);
        Frame();
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Backspace, false, default)]
        });
        check(text == readonlyText && writes == readonlyWrites, "readonly Fluent Textarea also blocks held-key deletion repeats");
        text = "one\r\ntwo\rthree\n";
        Frame();
        check(text == "one\r\ntwo\rthree\n" && writes == readonlyWrites,
            "readonly multiline rendering preserves the caller's CRLF,CR and LF model without normalization writes");
        tree.TryGet(id, out node);
        check(node.Node.ReadOnly && tree.RequestAction(id, node.RuntimeId, UiAccessibilityAction.SetValue,
            "blocked\nvalue", wait: false) == UiAccessibilityResult.ReadOnly,
            "readonly Fluent Textarea publishes and enforces its accessibility Value guard");
        var ime = new Ime();
        context.SetImeHandler(ime);
        ime.Pending = "commit";
        ime.Target = id;
        Frame();
        check(ime.Pending is null && ime.Owner is null && writes == readonlyWrites,
            "readonly Fluent Textarea drains committed IME input and releases composition ownership");
        editor.Options = new()
        {
            Invalid = true
        };
        Frame();
        check(ime.Owner == id && text == "one\ntwo\nthree\n", "leaving readonly restores IME and the existing editable normalization policy");
        Key(UiKey.Tab);
        tree.RequestAction(id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame();
        check(context.State.FocusedId == id && context.State.ActiveId == id && tree.Root.Focus == id,
            "Fluent Textarea accessibility Focus activates the real editor in the same frame");
        Key(UiKey.Tab);
        Frame(idle with
        {
            MousePosition = new(24, 26),
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            LeftMouseReleased = true
        });
        check(context.State.FocusedId == id && context.State.ActiveId == id,
            "Field label press focuses Fluent Textarea without being canceled by the outside-text click");
        disabled = true;
        Frame(idle with
        {
            CharEvents = [new('X')]
        });
        check(context.State.ActiveId is null && context.State.FocusedId != id,
            "disabled Fluent Textarea releases editing and common keyboard focus");
        disabled = false;
        text = string.Empty;
        editor.Options = new()
        {
            Placeholder = "a long hint that wraps over several visual lines"
        };
        Frame();
        var beforeHintWrites = writes;
        Frame();
        tree.TryGet(id, out node);
        check(text.Length == 0 && writes == beforeHintWrites && node.Node.Value == string.Empty && context.State.GetScrollY(id) == 0,
            "wrapped placeholder does not enter the model,accessibility value,history or scrollbar extent");
        editor.Options = new()
        {
            Invalid = true
        };
        Frame(probe: ui => editor.Render(ui));
        check(tree.TryGet(tree.Root.First!, out node) && node.Node.Invalid && !node.Node.Required,
            "standalone Textarea invalid metadata is published independently of Field validation");
        text = string.Join('\n', Enumerable.Repeat("scroll row", 40));
        Frame();
        tree.TryGet(id, out node);
        var bar = new UiVector2(node.Node.Bounds.X + node.Node.Bounds.Width - 5, node.Node.Bounds.Y + 5);
        Frame(idle with
        {
            MousePosition = bar,
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        var captured = context.State.ActiveId;
        Frame(idle with
        {
            MousePosition = bar with
            {
                Y = bar.Y + 40
            },
            LeftMouseDown = true
        });
        check(captured == id + "##scroll-v" && context.State.ActiveId == captured && context.State.GetScrollY(id) > 0,
            "Fluent keyboard focus does not steal owned scrollbar capture during actual pointer dragging");
        Frame(idle with
        {
            LeftMouseReleased = true
        });
        check(context.State.ActiveId != id + "##scroll-v", "Fluent Textarea releases scrollbar capture on pointer release");
        text = "select all";
        editor.Options = new()
        {
            Flags = UiInputTextFlags.ReadOnly | UiInputTextFlags.AutoSelectAll,
            Wrap = false
        };
        Frame();
        Key(UiKey.Tab);
        tree.TryGet(id, out node);
        Frame(idle with
        {
            MousePosition = new(node.Node.Bounds.X + 20, node.Node.Bounds.Y + 10),
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            LeftMouseReleased = true
        });
        check(context.State.GetSelection(id) == new UiTextSelection(0, text.Length),
            "pointer focus can select all while explicit Wrap=false preserves readonly navigation");
        var invalidFlagsRejected = false;
        try
        {
            editor.Options = new()
            {
                Flags = (UiInputTextFlags)8
            };
        }
        catch (ArgumentOutOfRangeException) { invalidFlagsRejected = true; }
        check(invalidFlagsRejected && editor.Options.Flags == (UiInputTextFlags.ReadOnly | UiInputTextFlags.AutoSelectAll),
            "multiline options reject unknown flags without replacing valid configuration");
        Frame(probe: ui =>
        {
            var raw = "";
            using var probe = new UiMultilineInput<UiStandardMultilineInputBehavior, ProbePresentation>("Probe",
                new(() => raw, next => raw = next), 32, 180, 90)
            {
                Options = new() { Placeholder = "Hint", Flags = UiInputTextFlags.ReadOnly, Wrap = true, Invalid = true }
            };
            using var allocation = Dux.SizedBox(probe, 100, 50);
            allocation.Render(ui);
            check(ProbePresentation.Last.Options.ReadOnly && ProbePresentation.Last.Options.Invalid && ProbePresentation.Last.Wrap,
                "immediate multiline options survive parent arrangement and reach the selected presentation");
        });
        field.Dispose();
        Frame(probe: _ => { });
        check(context.State.TextInputStorage == (0, 0) && context.State.ScrollStorage == (0, 0) && tree.Count == 0,
            "Fluent Textarea disposal releases owned editing,scrollbars,placeholder rows and accessibility publication");
    }

    private static void CheckSize<TSize>(UiControlContext context, float height, float horizontal, float vertical,
        float line, Action<bool, string> check) where TSize : IUiTextareaSize
    {
        using var typography = TSize.BeginText(context);
        var layout = UiFluentTextareaPresentation<TSize, UiFluentOutlineInput>.Measure(context, 180, 0);
        check(layout.Size == new UiVector2(180, height) && layout.LineHeight == line && layout.Wrap,
            "Textarea size uses its own two-line default root and typography : " + typeof(TSize).Name);
        check(layout.ContentBounds == new UiRect(1 + horizontal, 1 + vertical, 178 - 2 * horizontal, height - 4 - 2 * vertical),
            "Textarea size reserves explicit border,padding and focus-indicator geometry : " + typeof(TSize).Name);
    }
    private readonly struct ProbePresentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineInputLayout Last;
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) => new(new(0, 0, width, height), 20);
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) => new(new(0, 0, size.X, size.Y), 20);
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit) => Last = layout;
    }
    private sealed class Ime : IUiImeHandler
    {
        internal string? Pending, Target, Owner;
        public string? ConsumeCommittedText(string inputId)
        {
            if (inputId != Target)
                return null;
            var value = Pending;
            Pending = null;
            return value;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public string? GetCompositionText() => Owner is null ? null : "preview";
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
        }
        public void SetCompositionOwner(string? inputId) => Owner = inputId;
    }
    private sealed class Clipboard : IUiClipboard
    {
        internal string Text = "";
        public string GetText() => Text;
        public void SetText(string text) => Text = text;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
