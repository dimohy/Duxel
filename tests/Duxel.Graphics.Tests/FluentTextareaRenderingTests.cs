using System.Numerics;
using Duxel.Core;

internal static class FluentTextareaRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, UiState state, bool vector)
    {
        Sizes<UiFluentOutlineInput>(capture, check, state, vector);
        Sizes<UiFluentFilledDarkerInput>(capture, check, state, vector);
        Sizes<UiFluentFilledLighterInput>(capture, check, state, vector);
    }
    private static void Sizes<TAppearance>(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, UiState state, bool vector) where TAppearance : IUiInputAppearance
    {
        Cases<UiFluentSmallTextareaSize, TAppearance>(capture, check, state, vector, 44, 8, 4, 16);
        Cases<UiFluentMediumTextareaSize, TAppearance>(capture, check, state, vector, 56, 12, 6, 20);
        Cases<UiFluentLargeTextareaSize, TAppearance>(capture, check, state, vector, 68, 14, 8, 22);
    }
    private static void Cases<TSize, TAppearance>(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, UiState state, bool vector, float height, float horizontal, float vertical, float lineHeight)
        where TSize : IUiTextareaSize where TAppearance : IUiInputAppearance
    {
        foreach (var mode in new[] { "text", "placeholder", "invalid", "disabled", "focused", "readonly" })
        {
            var value = mode == "placeholder" ? "" : mode == "readonly" ? "Hi\r\nthere" : "Hi";
            using var editor = new UiMultilineInput<UiFluentMultilineInputBehavior, Probe<TSize, TAppearance>>(
                "GPU Textarea", new(() => value, next => value = next), 256, 180)
            {
                Options = new() { Placeholder = "Hint", Invalid = mode == "invalid",
                    Flags = mode == "readonly" ? UiInputTextFlags.ReadOnly : default }
            };
            var key = "textarea-" + typeof(TSize).Name + "-" + TAppearance.Kind + "-" + mode + "-" + vector;
            var id = "";
            void Scene(UiImmediateContext ui, Action draw)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("textarea-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try
                {
                    ui.SetCursorScreenPos(new(20, 20));
                    draw();
                }
                finally
                {
                    if (vector)
                        ui.EndLayer();
                }
            }
            void Actual(UiImmediateContext ui, bool prime = false) => Scene(ui, () =>
            {
                var focused = !prime && mode == "focused";
                state.FocusedId = focused ? id : null;
                state.ActiveId = focused ? id : null;
                if (!prime)
                {
                    state.SetCursor(id, 0);
                    state.SetSelection(id, default);
                    state.SetScrollY(id, 0);
                    state.SetScrollX(id, 0);
                }
                new UiControlContext(ui).RestartInputCaretBlink();
                ui.BeginDisabled(mode == "disabled");
                editor.Render(ui);
                ui.EndDisabled();
            });
            capture(key + "-prime", ui => Actual(ui, true));
            id = Probe<TSize, TAppearance>.Id;
            var expected = capture(key + "-reference", ui => Scene(ui, () =>
            {
                state.FocusedId = null;
                state.ActiveId = null;
                var control = new UiControlContext(ui);
                using var typography = TSize.BeginText(control);
                var root = new UiRect(20, 20, 180, height);
                var content = new UiRect(21 + horizontal, 21 + vertical, 178 - 2 * horizontal, height - 4 - 2 * vertical);
                using var rootClip = control.Clip(root);
                var palette = UiFluentInputPalette.Resolve(control.FluentColorScheme,
                    mode == "disabled" ? UiButtonVisualState.Disabled : mode == "focused" ? UiButtonVisualState.Pressed : UiButtonVisualState.Rest,
                    TAppearance.Kind, mode == "invalid");
                TAppearance.Paint(control, root, palette, mode == "focused");
                using var textClip = control.Clip(content);
                var block = control.MeasureTextBlock(mode == "placeholder" ? "Hint" : mode == "readonly" ? "Hi\nthere" : "Hi");
                control.TypographyTextBlock(block, content, mode == "placeholder" ? palette.Placeholder : palette.Text);
                if (mode == "focused")
                    control.Frame(new(MathF.Round(content.X), MathF.Round(content.Y) + 2, 1, lineHeight - 4),
                        palette.Text, new(0, 0, 0, 0), 0, 0);
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                var actual = capture(key + "-" + frame, ui => Actual(ui));
                check(actual.AsSpan().SequenceEqual(expected), "Textarea matches independent root,insets,text,placeholder and caret : " + key + " / " + frame);
            }
        }
    }
    private readonly struct Probe<TSize, TAppearance> : IUiMultilineInputPresentation
        where TSize : IUiTextareaSize where TAppearance : IUiInputAppearance
    {
        internal static string Id = "";
        public static UiControlTypographyScope BeginText(UiControlContext context) => TSize.BeginText(context);
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            UiFluentTextareaPresentation<TSize, TAppearance>.Measure(context, width, height);
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
            UiFluentTextareaPresentation<TSize, TAppearance>.Arrange(context, natural, size);
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
        {
            Id = edit.Id;
            UiFluentTextareaPresentation<TSize, TAppearance>.Paint(context, layout, edit);
        }
    }
}
