using Duxel.Core;
using System.Numerics;

internal static class MultilineContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var first = CreateContext();
        using var second = CreateContext();
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var text = "one\ntwo";
        var reads = 0;
        var writes = 0;
        var disabled = false;
        var editor = new UiMultilineInput<UiStandardMultilineInputBehavior, ContractPresentation>("Contract",
            new(() =>
            {
                reads++;
                return text;
            }, value =>
            {
                writes++;
                text = value;
            }), 4096, 180, 90);
        using var parent = Dux.SizedBox(editor, 120, 60);
        void Frame(UiContext context, UiInputState input, Action<UiImmediateContext>? probe = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 20));
                if (probe is not null)
                    probe(ui);
                else
                {
                    ui.BeginDisabled(disabled);
                    try
                    {
                        parent.Render(ui);
                    }
                    finally
                    {
                        ui.EndDisabled();
                    }
                }
            }));
            using var data = context.GetDrawData();
            foreach (var list in data.DrawLists)
                foreach (var command in list.Commands)
                    if (command.ClipRect.Width < 0 || command.ClipRect.Height < 0)
                        throw new InvalidOperationException("Multiline generated negative clip dimensions.");
        }
        Frame(first, idle, ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            var measured = parent.MeasureConstrained(ui, new());
            check(measured == new UiVector2(120, 60), "compiled multiline measures its assigned parent root");
            check(reads == 0 && writes == 0 && cursor == ui.GetCursorScreenPos(),
                "compiled multiline pure measurement leaves binding and cursor untouched");
            parent.Render(ui);
            check(ui.GetItemRectMin() == new UiVector2(20, 20) && ui.GetItemRectSize() == measured,
                "compiled multiline advances exactly one assigned root");
        });
        check(reads == 1 && writes == 0, "compiled multiline reads its binding once during rendering");
        check(ContractPresentation.LastLayout.Field == new UiRect(20, 20, 120, 60)
            && ContractPresentation.LastLayout.ContentBounds == new UiRect(28, 26, 104, 48),
            "compiled multiline preserves presentation content insets under parent allocation");
        check(ContractPresentation.LastLayout.LineHeight == 20,
            "compiled multiline uses the typography line box instead of glyph ink height");
        Frame(first, idle with
        {
            MousePosition = new(30, 30),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(first, idle with
        {
            LeftMouseReleased = true
        });
        var id = ContractPresentation.LastEdit.Id;
        check(first.State.ActiveId == id, "compiled multiline retains the actual pointer editor identity");
        disabled = true;
        Frame(first, idle with
        {
            KeyEvents = [new(UiKey.Enter, true, default)],
            CharEvents = [new('z')]
        });
        check(text == "one\ntwo" && writes == 0 && first.State.ActiveId is null,
            "disabling a focused compiled multiline editor releases focus and blocks text edits");
        disabled = false;
        Frame(first, idle with
        {
            MousePosition = new(30, 30),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(first, idle with
        {
            LeftMouseReleased = true
        });
        Frame(first, idle with
        {
            KeyEvents = [new(UiKey.End, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        Frame(first, idle with
        {
            KeyEvents = [new(UiKey.Enter, true, default)],
            CharEvents = [new('x')]
        });
        check(text == "one\ntwo\nx" && writes == 1, "compiled multiline delivers a complete edit to its binding once");
        parent.Options = new(140, 80);
        Frame(first, idle with
        {
            KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        check(text == "one\ntwo" && writes == 2 && ContractPresentation.LastEdit.Id == id && first.State.ActiveId == id,
            "compiled multiline allocation changes preserve editor focus and undo");
        check(ContractPresentation.LastLayout.Field == new UiRect(20, 20, 140, 80),
            "compiled multiline live allocation changes update the actual field");
        text = string.Join('\n', Enumerable.Repeat(new string('w', 60), 20));
        Frame(first, idle);
        check(ContractPresentation.LastEdit.MaximumScrollY > 0 && ContractPresentation.LastEdit.MaximumScrollX > 0,
            "compiled multiline overflow creates both owned scrollbar axes");
        disabled = true;
        first.State.ActiveId = id + "##scroll-v";
        first.State.SetScrollY(id, 0);
        first.State.SetScrollX(id, 0);
        Frame(first, idle with
        {
            MousePosition = new(155, 40),
            LeftMousePressed = true,
            LeftMouseDown = true,
            MouseWheel = -1,
            MouseWheelHorizontal = -1
        });
        check(first.State.ActiveId is null && first.State.GetScrollX(id) == 0 && first.State.GetScrollY(id) == 0,
            "disabled compiled multiline scrollbars release capture and reject wheel and pointer input");
        disabled = false;
        first.State.SetScrollY(id, 100);
        first.State.SetScrollX(id, 100);
        first.State.ActiveId = id + "##scroll-v";
        text = "short";
        Frame(first, idle);
        check(first.State.GetScrollX(id) == 0 && first.State.GetScrollY(id) == 0 && first.State.ActiveId is null,
            "compiled multiline shrinking content clamps offsets and releases a disappearing captured scrollbar");
        first.State.SetScrollY(id + "##dragAutoScrollTime", 1);
        Frame(second, idle);
        check(first.State.TextInputStorage == (0, 0) && first.State.ScrollStorage == (0, 0),
            "compiled multiline context transfer releases old editor and scrollbar storage including drag timing");
        parent.Options = new(0, 0);
        Frame(second, idle);
        check(ContractPresentation.LastLayout.Size == default,
            "compiled multiline accepts a zero parent allocation without negative draw geometry");
        parent.Options = new(140, 80);
        var ime = new QueuedIme();
        second.SetImeHandler(ime);
        Frame(second, idle);
        second.State.ActiveId = id;
        var beforeCommit = writes;
        ime.Queue(id, "한");
        Frame(second, idle with
        {
            MousePosition = new(280, 220),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        check(text.Contains('한') && writes == beforeCommit + 1 && second.State.ActiveId is null,
            "compiled multiline applies an already committed IME value while pointer focus leaves the editor");
        var committed = text;
        beforeCommit = writes;
        disabled = true;
        ime.Queue(id, "금");
        Frame(second, idle);
        disabled = false;
        Frame(second, idle);
        check(text == committed && writes == beforeCommit && ime.Pending is null,
            "disabled compiled multiline discards committed IME input without replaying it after enabling");
        parent.Dispose();
        check(second.State.TextInputStorage == (0, 0) && second.State.ScrollStorage == (0, 0),
            "compiled multiline root disposal releases the remaining context storage");
        var rejected = false;
        try
        {
            _ = editor.Value;
        }
        catch (ObjectDisposedException)
        {
            rejected = true;
        }
        check(rejected && editor.Label.Length == 0, "compiled multiline disposal releases binding and label ownership");
    }

    internal readonly struct ContractPresentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineInputLayout LastLayout;
        internal static UiMultilineEditResult LastEdit;
        public static UiControlTypographyScope BeginText(UiControlContext context) =>
            context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            Layout(width > 0 ? width : 180, height > 0 ? height : 90, context.TypographyLineHeight);
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
            Layout(size.X, size.Y, natural.LineHeight);
        private static UiMultilineInputLayout Layout(float width, float height, float lineHeight) =>
            new(new(0, 0, width, height), lineHeight)
            {
                Content = new(8, 6, MathF.Max(0, width - 16), MathF.Max(0, height - 12))
            };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
        {
            LastLayout = layout;
            LastEdit = edit;
            context.Frame(layout.Field, new(30, 50, 80), new(100, 160, 210), 0, 1);
            context.PaintEditableMultiline(layout, edit,
                new(new(240, 230, 180), new(80, 120, 180), new(255, 255, 255))
                {
                    Typography = true
                });
        }
    }

    internal static void Render(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, bool vector)
    {
        var cases = new (string Name, UiVector2 Size, bool Narrow)[]
        {
            ("allocated", new(120, 60), false),
            ("changed", new(140, 80), false),
            ("clipped", new(120, 60), true),
            ("tiny", new(1, 1), false),
            ("zero", default, false)
        };
        foreach (var item in cases)
        {
            var text = "one\ntwo";
            using var editor = new UiMultilineInput<UiStandardMultilineInputBehavior, ContractPresentation>(
                "GPU", new(() => text, value => text = value), 1024, 180, 90);
            using var parent = Dux.SizedBox(editor, item.Size.X, item.Size.Y);
            void Scene(UiImmediateContext ui, Action draw)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                if (vector)
                    ui.BeginLayer("multiline-contract-vector", new(false, 1, default)
                    {
                        Scene3D = new(Matrix4x4.Identity, false)
                    });
                try
                {
                    ui.SetCursorScreenPos(new(20, 20));
                    using var clip = new UiControlContext(ui).Clip(item.Narrow ? new(35, 30, 70, 35) : new(0, 0, 320, 240));
                    draw();
                }
                finally
                {
                    if (vector)
                        ui.EndLayer();
                }
            }
            var key = "multiline-contract-" + item.Name + "-" + vector;
            var expected = capture(key + "-reference", ui => Scene(ui, () =>
            {
                var context = new UiControlContext(ui);
                var root = new UiRect(20, 20, item.Size.X, item.Size.Y);
                using var clip = context.Clip(root);
                context.Frame(root, new(30, 50, 80), new(100, 160, 210), 0, 1);
                if (item.Name == "tiny")
                {
                    context.Frame(root, context.Theme.ScrollbarBg, new(0, 0, 0, 0), 0, 0);
                    return;
                }
                ui.SetCursorScreenPos(new(28, 26));
                ui.Text<UiFluentTypographyPresentation<UiRegularTypeface, UiFluentType300>>("one", new(240, 230, 180));
                ui.SetCursorScreenPos(new(28, 46));
                ui.Text<UiFluentTypographyPresentation<UiRegularTypeface, UiFluentType300>>("two", new(240, 230, 180));
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                var actual = capture(key + "-" + frame, ui => Scene(ui, () => parent.Render(ui)));
                check(actual.AsSpan().SequenceEqual(expected),
                    "compiled multiline palette/typography matches independent frame and text : " + key + " / " + frame);
            }
        }
    }

    private static UiContext CreateContext()
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        return context;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private sealed class QueuedIme : IUiImeHandler
    {
        private string? _id;
        internal string? Pending
        {
            get; private set;
        }
        internal void Queue(string id, string text)
        {
            _id = id;
            Pending = text;
        }
        public string? ConsumeCommittedText(string inputId)
        {
            if (inputId != _id)
                return null;
            var text = Pending;
            Pending = null;
            return text;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public string? GetCompositionText() => null;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
        }
        public void SetCompositionOwner(string? inputId)
        {
        }
    }
}
