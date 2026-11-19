using System.Numerics;
using Duxel.Core;

internal static class TooltipTargetRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check, bool vector, UiAccessibilityTree accessibility, UiState state)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var mode in new[] { "normal", "handover", "escape", "clipped", "disabled", "omitted", "pending", "nested", "snapshot" })
        {
            UiWidget target = new UiButton<UiReleaseButtonBehavior, Button>("First") { Size = new(80, 24) };
            if (mode == "nested")
            {
                target = new UiTooltip<UiHoverFocusTooltipBehavior, Surface>(target, "Second", new() { ShowDelaySeconds = 0 });
            }
            using var first = new UiTooltip<UiHoverFocusTooltipBehavior, Surface>(
                target, "First",
                new() { ShowDelaySeconds = mode == "pending" ? 0.25 : 0 });
            using var second = new UiTooltip<UiHoverFocusTooltipBehavior, Surface>(
                new UiButton<UiReleaseButtonBehavior, Button>("Second") { Size = new(80, 24) }, "Second");
            var key = "tooltip-target-" + mode + "-" + vector;
            var open = mode is "normal" or "handover" or "clipped" or "disabled" or "nested" or "snapshot";
            void Scene(UiImmediateContext ui, bool literal, bool initial = false)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var control = new UiControlContext(ui);
                if (vector)
                {
                    ui.BeginLayer("tooltip-target-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                }
                using (control.Clip(mode == "clipped" ? new(20, 60, 10, 24) : new(0, 0, 320, 240)))
                {
                    ui.BeginDisabled(mode == "disabled");
                    try
                    {
                        if (mode != "omitted" || initial)
                        {
                            if (literal)
                            {
                                control.Frame(new(20, 60, 80, 24), new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
                            }
                            else
                            {
                                ui.SetCursorScreenPos(new(20, 60));
                                if (mode == "snapshot")
                                {
                                    first.Text = "First";
                                }
                                first.Render(ui);
                                if (mode == "snapshot")
                                {
                                    first.Text = "Second";
                                }
                            }
                        }
                        if (literal)
                        {
                            control.Frame(new(140, 60, 80, 24), new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
                        }
                        else
                        {
                            ui.SetCursorScreenPos(new(140, 60));
                            second.Render(ui);
                        }
                    }
                    finally
                    {
                        ui.EndDisabled();
                    }
                }
                if (vector)
                {
                    ui.EndLayer();
                }
                if (literal && open)
                {
                    control.Frame(new(mode == "handover" ? 140 : 20, 20, 80, 28),
                        mode is "handover" or "nested" ? new(255, 180, 200) : new(220, 240, 255), new(0, 0, 0, 0), 4, 0);
                }
            }
            setInput(idle);
            var expected = capture(key + "-expected", ui => Scene(ui, true));
            setInput(idle with { MousePosition = new(25, 70) });
            capture(key + "-prime", ui => Scene(ui, false, true));
            for (var frame = 0; frame < 4; frame++)
            {
                setInput(idle with
                {
                    MousePosition = mode == "handover" ? new(150, 70) : new(25, 70),
                    KeyEvents = mode == "escape" && frame == 0 ? [new(UiKey.Escape, true, KeyModifiers.None)] : []
                });
                var actual = capture(key + "-" + frame, ui => Scene(ui, false));
                check(actual.AsSpan().SequenceEqual(expected), "tooltip single surface matches independent literal : " + key + " / " + frame);
            }
        }
        setInput(idle);
        if (!vector)
        {
            PopupPositioningTests.Render(capture, setInput, check);
            PopupCollisionTests.Render(capture, setInput, check);
            PopupCollisionTests.RenderOffsets(capture, setInput, check);
            PopupMeasuredTests.Render(capture, setInput, check);
            PopupAutoSizeTests.Render(capture, setInput, check, accessibility, state);
            TooltipSurfaceTests.Render(capture, setInput, check);
            TooltipSurfaceTests.RenderControlled(capture, setInput, check);
            TooltipSurfaceTests.RenderCss(capture, check);
        }
    }

    private readonly struct Button : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => requestedSize;
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiButtonInteraction interaction) => context.Frame(bounds, new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
    }
    private readonly struct Surface : IUiTooltipPresentation
    {
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => new(new(target.X, target.Y - 40, 80, 28), UiPopupPosition.Above);
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout) =>
            context.Frame(layout.Bounds, text == "Second" ? new(255, 180, 200) : new(220, 240, 255), new(0, 0, 0, 0), 4, 0);
    }
}
