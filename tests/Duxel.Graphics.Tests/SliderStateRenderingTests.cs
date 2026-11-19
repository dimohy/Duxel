using System.Text.Json;
using Duxel.Core;

internal static class SliderStateRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, int width, UiFluentColorScheme scheme)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SliderPalette.json")));
        var value = new UiState<float>(35);
        using var slider = Fluent.Slider("Actual slider", value.Binding, 0, 100);
        slider.Size = new(200, 32);
        using var progress = Fluent.ProgressBar(() => new(.35f, 1));
        progress.Size = new(200, 4);
        var disabled = false;
        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 20));
            ui.BeginDisabled(disabled);
            slider.Render(ui);
            ui.EndDisabled();
            ui.SetCursorScreenPos(new(20, 90));
            progress.Render(ui);
        }
        UiInputState Input(UiVector2 pointer, bool down = false, bool pressed = false, bool released = false) =>
            new(pointer, down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        byte[]? unmarkedReference = null;
        foreach (var stepped in new[] { false, true })
        {
            slider.Options = slider.Options with { Step = stepped ? 1f : null };
            byte[]? baseline = null;
            foreach (var state in Enum.GetValues<UiButtonVisualState>())
            {
                var expected = fixture.RootElement.EnumerateArray().Single(row => row.GetProperty("scheme").GetString() == scheme.ToString()
                    && row.GetProperty("state").GetString() == state.ToString());
                var paint = UiFluentSliderPalette.Resolve(scheme, state);
                UiColor Color(string name)
                {
                    var bytes = Convert.FromHexString(expected.GetProperty(name).GetString()!);
                    return new(bytes[0], bytes[1], bytes[2]);
                }
                check(paint == new UiSliderPaint(Color("rail"), Color("fill"), Color("thumbBorder"), Color("surface")),
                    "Slider palette matches independent pinned token resolution : " + scheme + " / " + state);
                disabled = state == UiButtonVisualState.Disabled;
                var pointer = state == UiButtonVisualState.Rest ? new UiVector2(-100, -100) : new(93, 36);
                setInput(Input(pointer, down : state == UiButtonVisualState.Pressed, pressed : state == UiButtonVisualState.Pressed));
                var pixels = capture($"{stepped}-{state}", Render);
                if (!stepped && state == UiButtonVisualState.Rest) unmarkedReference = pixels;
                bool Pixel(int x, int y, string token)
                {
                    var color = Color(token).Rgba;
                    var index = (y * width + x) * 4;
                    return Math.Abs(pixels[index] - (byte)color) <= 1 && Math.Abs(pixels[index + 1] - (byte)(color >> 8)) <= 1
                        && Math.Abs(pixels[index + 2] - (byte)(color >> 16)) <= 1;
                }
                check(Pixel(93, 36, "fill"), "actual Slider thumb uses official state color : " + scheme + " / " + state + " / stepped " + stepped);
                if (!stepped)
                    check(Pixel(50, 36, "fill") && Pixel(180, 36, "rail"), "actual Slider filled and remaining rail use separate official colors : " + state);
                baseline ??= pixels;
                check(pixels.AsSpan(width * 90 * 4, width * 5 * 4).SequenceEqual(baseline.AsSpan(width * 90 * 4, width * 5 * 4)),
                    "fixed ProgressBar is unchanged by Slider hover/press/disabled : " + state);
                setInput(Input(new(-100, -100), released : true));
                capture($"{stepped}-{state}-release", Render);
            }
        }
        disabled = false;
        slider.Options = slider.Options with { Step = 1, ShowStepMarkers = false };
        setInput(Input(new(-100, -100)));
        var hiddenMarkers = capture("fine-step-hidden-markers", Render);
        check(hiddenMarkers.AsSpan().SequenceEqual(unmarkedReference),
            "fine-step Slider keeps the complete continuous rail when step markers are explicitly hidden : " + scheme);
        check(MathF.Abs(value.Value - 35) < .0001f, "hover/state qualification does not change the Slider model value");

        SliderDirectionPixelFixture.Run(capture, check, width, scheme);
    }

}
