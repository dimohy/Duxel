using Duxel.Core;

internal static class ParagraphNavigationInputTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var wrap in new[] { false, true })
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            var text = "אבגד";
            using var editor = new UiMultilineInput<UiFluentMultilineInputBehavior, Presentation>("Native arrows",
                new(() => text, next => text = next), 256, 240, 120)
            {
                Options = new() { Wrap = wrap }
            };
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Frame(UiInputState input)
            {
                context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), input, new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); editor.Render(ui); }));
                using var data = context.GetDrawData();
            }
            Frame(idle);
            var id = Presentation.Last.Id;
            void Reset(int start, int? end = null, string source = "אבגד")
            {
                text = source;
                context.State.ActiveId = null;
                Frame(idle);
                context.State.ActiveId = context.State.FocusedId = id;
                context.State.SetCursor(id, end ?? start);
                context.State.SetSelection(id, new(start, end ?? start));
            }
            void Key(UiKey key, KeyModifiers modifiers = default) => Frame(idle with
            {
                KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)]
            });
            void Verify(bool condition, string name) => check(condition, $"Actual native multiline arrows : wrap {wrap} / {name}");
            Reset(2);
            Key(UiKey.RightArrow);
            Verify(Presentation.Last.CaretIndex == 1, "Right moves physically right through Hebrew");
            Key(UiKey.LeftArrow, KeyModifiers.Shift);
            Verify(Presentation.Last.Selection == new UiTextSelection(1, 2), "Shift Left extends a complete source selection");
            Reset(0, 4);
            Key(UiKey.LeftArrow);
            Verify(Presentation.Last.CaretIndex == 4 && !Presentation.Last.Selection.HasSelection, "Left collapses selection at the physical left edge");
            Reset(0, 4);
            Key(UiKey.RightArrow);
            Verify(Presentation.Last.CaretIndex == 0 && !Presentation.Last.Selection.HasSelection, "Right collapses selection at the physical right edge");
            Reset(2);
            Frame(idle with
            {
                KeyEvents = [new(UiKey.RightArrow, true, default)]
            });
            context.State.SetRepeatTime(UiKey.RightArrow, context.State.TimeSeconds - 1);
            Frame(idle);
            Verify(Presentation.Last.CaretIndex == 0, "Held arrows use the same physical movement contract");
            Frame(idle with
            {
                KeyEvents = [new(UiKey.RightArrow, false, default)]
            });
            Reset(2);
            Key(UiKey.Delete);
            Verify(text == "אבד", "Delete keeps logical source order");
            Reset(2);
            Key(UiKey.Backspace);
            Verify(text == "אגד", "Backspace keeps logical source order");
            Reset(2, source: "ab\ncd");
            Key(UiKey.RightArrow);
            Verify(Presentation.Last.CaretIndex == 3, "Right crosses a hard line break");
            Key(UiKey.LeftArrow);
            Verify(Presentation.Last.CaretIndex == 2, "Left crosses back over a hard line break");
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
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
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit) => Last = edit;
    }
}
