using System.Numerics;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static class SwitchAlignmentRenderingTests
{
    internal static void RunPointer(Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        fixture.Context.SetTheme(UiCompiledDesign.Windows11.Theme);
        var value = new UiState<bool>(true);
        using var control = Fluent.Switch("Wide setting label", value.Binding);
        control.ReducedMotion = true;
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(20, 20));
            control.Render(ui);
        }
        var rest = fixture.Capture(Draw, input);
        var overLabel = fixture.Capture(Draw, input with { MousePosition = new(95, 38) });
        var overTrack = fixture.Capture(Draw, input with { MousePosition = new(44, 38) });
        var overPadding = fixture.Capture(Draw, input with { MousePosition = new(24, 24) });
        check(rest.AsSpan().SequenceEqual(overLabel), "Switch label hover preserves resting track pixels");
        check(!rest.AsSpan().SequenceEqual(overTrack) && overTrack.AsSpan().SequenceEqual(overPadding),
            "Switch track and surrounding input padding have the same hover pixels");
        var pressed = fixture.Capture(Draw, input with { MousePosition = new(44, 38), LeftMousePressed = true, LeftMouseDown = true });
        check(!pressed.AsSpan().SequenceEqual(overTrack) && value.Value, "Switch track press paints an active state and waits for release");
        fixture.Capture(Draw, input with { MousePosition = new(-1, -1), LeftMouseReleased = true });
        check(value.Value, "Switch release outside cancels track activation");
        var labelPressed = fixture.Capture(Draw, input with { MousePosition = new(95, 38), LeftMousePressed = true, LeftMouseDown = true });
        check(rest.AsSpan().SequenceEqual(labelPressed) && value.Value, "Switch label press preserves resting pixels and waits for release");
        fixture.Capture(Draw, input with { MousePosition = new(95, 38), LeftMouseReleased = true });
        check(!value.Value, "Switch label release still toggles the value");

        VerifyPlacement<UiFluentMediumSwitchSize, UiSwitchLabelBefore>(UiFlowDirection.LeftToRight,
            (min, max) => (new(min.X + 12, min.Y + 16), new(max.X - 20, min.Y + 16)), "label before");
        VerifyPlacement<UiFluentMediumSwitchSize, UiSwitchLabelBefore>(UiFlowDirection.RightToLeft,
            (min, max) => (new(max.X - 12, min.Y + 16), new(min.X + 20, min.Y + 16)), "label before in RTL");
        VerifyPlacement<UiFluentSmallSwitchSize, UiSwitchLabelAbove>(UiFlowDirection.LeftToRight,
            (min, max) => (new(min.X + 12, min.Y + 10), new(max.X - 12, max.Y - 12)), "small label above");

        void VerifyPlacement<TSize, TLabel>(UiFlowDirection direction,
            Func<UiVector2, UiVector2, (UiVector2 Label, UiVector2 Track)> pointerLocations, string name)
            where TSize : IUiSwitchSize where TLabel : IUiSwitchLabelPosition
        {
            using var variant = new UiToggle<bool, UiBooleanToggleValue, UiSpaceButtonBehavior,
                UiFluentSwitchPresentation<TSize, TLabel>>("Wide setting label", new UiState<bool>(true).Binding);
            variant.ReducedMotion = true;
            UiVector2 min = default, max = default;
            void DrawVariant(UiImmediateContext ui)
            {
                using var scope = ui.BeginFluentProvider(null, direction: direction);
                ui.SetCursorScreenPos(new(20, 20));
                variant.Render(ui);
                min = ui.GetItemRectMin();
                max = ui.GetItemRectMax();
            }
            var normal = fixture.Capture(DrawVariant, input);
            var (label, track) = pointerLocations(min, max);
            var labelPixels = fixture.Capture(DrawVariant, input with { MousePosition = label });
            var trackPixels = fixture.Capture(DrawVariant, input with { MousePosition = track });
            check(normal.AsSpan().SequenceEqual(labelPixels) && !normal.AsSpan().SequenceEqual(trackPixels),
                $"Switch follows the input region for {name}");
        }
    }

    internal static void RunWrappedLabels(Action<bool, string> check, string output)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        fixture.Context.SetTheme(UiCompiledDesign.Windows11.Theme);
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        Verify<UiFluentMediumSwitchSize, UiSwitchLabelAfter>(122, 76, UiFlowDirection.LeftToRight, "medium label after", "after");
        Verify<UiFluentMediumSwitchSize, UiSwitchLabelBefore>(122, 76, UiFlowDirection.LeftToRight, "medium label before", "before");
        Verify<UiFluentSmallSwitchSize, UiSwitchLabelBefore>(110, 64, UiFlowDirection.RightToLeft, "small RTL label before");
        Verify<UiFluentMediumSwitchSize, UiSwitchLabelAbove>(70, 76, UiFlowDirection.LeftToRight, "medium label above", "above");

        void Verify<TSize, TLabel>(float width, float expectedHeight, UiFlowDirection direction, string name, string? capturePlacement = null)
            where TSize : IUiSwitchSize where TLabel : IUiSwitchLabelPosition
        {
            var value = new UiState<bool>(false);
            var control = new WrappedSwitchProbe<TSize, TLabel>("Setting Setting Setting", value.Binding) { ReducedMotion = true };
            using var owner = Dux.SizedBox(control, width);
            UiVector2 min = default, max = default;
            byte[] Frame(UiInputState state) => fixture.Capture(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                using var provider = ui.BeginFluentProvider(null, direction: direction);
                if (capturePlacement is not null)
                {
                    var displaySize = fixture.FrameInfo.DisplaySize;
                    var viewport = new UiRect(0, 0, displaySize.X, displaySize.Y);
                    ui.GetWindowDrawList().AddRectFilled(viewport, new(255, 255, 255), ui.WhiteTextureId, viewport);
                }
                ui.SetCursorScreenPos(new(20, 20));
                owner.Render(ui);
                min = control.Min;
                max = control.Max;
            }, state);

            var wrapped = Frame(input);
            Save("narrow-off", wrapped);
            var height = max.Y - min.Y;
            check(height == expectedHeight + (typeof(TLabel) == typeof(UiSwitchLabelAbove) ? TSize.TrackSize.Y : 0),
                $"Switch auto-wrap reserves the full three-line height for {name}: {height}");
            control.Label = "Setting\nSetting\nSetting";
            var explicitLines = Frame(input);
            if (typeof(TLabel) != typeof(UiSwitchLabelBefore))
                check(wrapped.AsSpan().SequenceEqual(explicitLines), $"Switch auto-wrap and explicit lines paint identical pixels for {name}");
            else
                check(max.Y - min.Y == height, $"Switch auto-wrap and explicit lines have the same measured height for {name}");
            control.Label = "Setting Setting Setting";
            owner.Options = new(280, null);
            var unwrapped = Frame(input);
            Save("wide-off", unwrapped);
            if (capturePlacement == "after")
            {
                fixture.WriteCapturePng(Path.Combine(output, "switch-label-122.png"), wrapped);
                fixture.WriteCapturePng(Path.Combine(output, "switch-label-280.png"), unwrapped);
            }
            check(max.Y - min.Y < height, $"Switch reflows its retained measurement when widened for {name}");

            owner.Options = new(width, null);
            Frame(input);
            var lastLine = new UiVector2((min.X + max.X) / 2,
                typeof(TLabel) == typeof(UiSwitchLabelAbove) ? min.Y + 50 : max.Y - 15);
            Frame(input with { MousePosition = lastLine, LeftMousePressed = true, LeftMouseDown = true });
            Frame(input with { MousePosition = lastLine, LeftMouseReleased = true });
            check(value.Value, $"Switch last label line is clickable for {name}");
            if (capturePlacement is not null)
            {
                var narrowOn = Frame(input);
                Save("narrow-on", narrowOn);
                if (capturePlacement == "before")
                    check(HorizontalChangeBounds(wrapped, narrowOn, fixture.LastCaptureSize.Width) == (94, 134),
                        "Switch label before keeps its allocated width when wrapping, with track edges at x=94 and x=134");
                owner.Options = new(280, null);
                Save("wide-on", Frame(input));
            }

            void Save(string state, byte[] pixels)
            {
                if (capturePlacement is not null)
                    fixture.WriteCapturePng(Path.Combine(output, $"switch-{capturePlacement}-{state}.png"), pixels);
            }
        }
    }

    private static (int Left, int Right) HorizontalChangeBounds(byte[] before, byte[] after, int width)
    {
        var left = width;
        var right = 0;
        for (var offset = 0; offset < before.Length; offset += 4)
        {
            if (before.AsSpan(offset, 4).SequenceEqual(after.AsSpan(offset, 4)))
                continue;
            var x = offset / 4 % width;
            left = Math.Min(left, x);
            right = Math.Max(right, x + 1);
        }
        return (left, right);
    }

    private sealed class WrappedSwitchProbe<TSize, TLabel>(string label, UiBinding<bool> value)
        : UiToggle<bool, UiBooleanToggleValue, UiSpaceButtonBehavior, UiFluentSwitchPresentation<TSize, TLabel>>(label, value)
        where TSize : IUiSwitchSize where TLabel : IUiSwitchLabelPosition
    {
        public UiVector2 Min { get; private set; }
        public UiVector2 Max { get; private set; }

        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            Min = ui.GetItemRectMin();
            Max = ui.GetItemRectMax();
        }
    }

    internal static void RunMotion(Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        fixture.Context.SetTheme(UiCompiledDesign.Windows11.Theme);
        var value = new UiState<bool>(false);
        using var control = Fluent.Switch("Setting", value.Binding);
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        byte[] Frame(UiInputState state) => fixture.Capture(ui =>
        {
            ui.SetCursorScreenPos(new(20, 20));
            control.Render(ui);
        }, state);
        bool Pending() => fixture.Context.State.NextScheduledFrameTimeSeconds.HasValue;

        var off = Frame(input);
        value.Value = true;
        var start = Frame(input);
        check(off.AsSpan().SequenceEqual(start) && Pending(), "Switch state change begins at the previous pixels and schedules motion");
        for (var n = 0; n < 5; n++)
            Frame(input);
        var midpoint = Frame(input);
        check(!off.AsSpan().SequenceEqual(midpoint) && Pending(), "Switch shows intermediate pixels at 100ms");
        for (var n = 0; n < 5; n++)
            Frame(input);
        check(Pending(), "Switch transition remains active immediately before 200ms");
        var on = Frame(input);
        check(!midpoint.AsSpan().SequenceEqual(on) && !Pending(), "Switch reaches its last pixels at 200ms and stops scheduling");

        var hover = input with { MousePosition = new(44, 38) };
        var hoverStart = Frame(hover);
        check(hoverStart.AsSpan().SequenceEqual(on) && Pending(), "Switch hover color transition starts at the current paint");
        for (var n = 0; n < 6; n++)
            Frame(hover);
        var returnStart = Frame(input);
        check(!returnStart.AsSpan().SequenceEqual(on) && Pending(), "Leaving the track retargets the current hover color");
        control.ReducedMotion = true;
        var reduced = Frame(input);
        check(reduced.AsSpan().SequenceEqual(on) && !Pending(), "Switch reduced motion settles an in-progress color transition immediately");
        value.Value = false;
        check(Frame(input).AsSpan().SequenceEqual(off) && !Pending(), "Switch reduced motion applies the whole Off state without scheduling");
    }

    internal static void RunSystemMotion(Action<bool, string> check)
    {
        var platform = PresentationTestPlatform.Create(320, 240);
        using var fixture = new PresentationReuseTests.Fixture(1, true, platform);
        var systemPreference = WindowsSystemTheme.GetPrefersReducedMotion();
        check(platform.PrefersReducedMotion == systemPreference, "Windows backend initializes from the current client-area animation preference");
        check(PresentationTestPlatform.PostMessage(platform.WindowHandle, 0x031A, 0, 0),
            "Windows test window accepts WM_THEMECHANGED without editing system settings");
        platform.PollEvents();
        check(platform.PrefersReducedMotion == WindowsSystemTheme.GetPrefersReducedMotion(),
            "Windows backend refreshes the animation preference on the shared system appearance message path");

        fixture.Context.SetTheme(UiCompiledDesign.Windows11.Theme);
        var value = new UiState<bool>(false);
        using var control = Fluent.Switch("Setting", value.Binding);
        var captures = 0;
        byte[] Frame(bool reduced)
        {
            using var data = fixture.Build(ui =>
            {
                if (ui.BeginLayer("system-motion", new(true, 1, default) { ContentVersion = value.Value ? 1 : 0 }))
                {
                    captures++;
                    ui.SetCursorScreenPos(new(20, 20));
                    control.Render(ui);
                }
                ui.EndLayer();
            }, null, prefersReducedMotion: reduced);
            return fixture.Render(data);
        }
        var off = Frame(false);
        value.Value = true;
        check(Frame(false).AsSpan().SequenceEqual(off) && fixture.Context.State.NextScheduledFrameTimeSeconds.HasValue,
            "A retained Switch starts motion while the host allows it");
        var before = captures;
        var on = Frame(true);
        check(!on.AsSpan().SequenceEqual(off) && captures > before && !fixture.Context.State.NextScheduledFrameTimeSeconds.HasValue,
            "Host reduced motion recaptures a retained Switch at its final pixels without a new deadline");
        Frame(false);
        value.Value = false;
        check(Frame(false).AsSpan().SequenceEqual(on) && fixture.Context.State.NextScheduledFrameTimeSeconds.HasValue,
            "Turning off host reduced motion permits future Switch transitions");
        control.ReducedMotion = true;
        value.Value = true;
        check(Frame(false).AsSpan().SequenceEqual(on) && !fixture.Context.State.NextScheduledFrameTimeSeconds.HasValue,
            "Explicit Switch reduced motion still applies when host animations are enabled");
    }

    internal static void RunForced(UiContext uiContext, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int width, int height)
    {
        var colors = new UiForcedColorPalette(new(5, 10, 15), new(240, 230, 220), new(110, 120, 130),
            new(10, 90, 180), new(250, 240, 230), new(30, 200, 220), new(20, 30, 40), new(210, 220, 230));
        var background = new UiColor(31, 37, 43);
        var ordinary = UiCompiledDesign.Windows11Dark.Theme;
        uiContext.SetTheme(colors.Apply(ordinary));
        var bounds = new UiRect(20, 20, 160, 40);
        foreach (var enabled in new[] { true, false })
            foreach (var value in new[] { false, true })
            {
                var name = $"switch-forced-{enabled}-{value}";
                var pixels = capture(name, ui =>
                {
                    var viewport = new UiRect(0, 0, width, height);
                    ui.GetWindowDrawList().AddRectFilled(viewport, background, ui.WhiteTextureId, viewport);
                    ui.BeginDisabled(!enabled);
                    var context = new UiControlContext(ui);
                    using var clip = context.Clip(bounds);
                    using var text = UiFluentMediumSwitchSize.BeginText(context, true);
                    var textSize = ui.CalcTextSize("Label");
                    var layout = UiSwitchLabelAfter.Measure(UiFluentMediumSwitchSize.TrackSize, textSize, true);
                    UiFluentSwitchPresentation<UiFluentMediumSwitchSize, UiSwitchLabelAfter>.Paint(
                        context, "Label", textSize, bounds, layout, new() { FocusVisible = enabled }, value);
                    ui.EndDisabled();
                });
                var foreground = enabled ? colors.CanvasText : colors.GrayText;
                var thumb = enabled && value ? colors.Canvas : foreground;
                var fill = value ? enabled ? colors.Highlight : colors.Canvas : background;
                bool Matches(int x, int y, UiColor color)
                {
                    var at = (y * width + x) * 4;
                    var rgba = color.Rgba;
                    return pixels[at] == (byte)rgba && pixels[at + 1] == (byte)(rgba >> 8) && pixels[at + 2] == (byte)(rgba >> 16);
                }
                check(Matches(48, 37, fill), $"{name} track interior");
                check(Matches(value ? 58 : 38, 38, thumb), $"{name} thumb");
                check(Matches(47, 28, enabled && value ? colors.Canvas : foreground), $"{name} track border");
                check(Matches(96, 22, enabled ? colors.Highlight : background), $"{name} keyboard outline survives the widget allocation clip");
                var hasLabelInk = Enumerable.Range(29, 18).Any(y => Enumerable.Range(80, 40).Any(x => Matches(x, y, foreground)));
                check(hasLabelInk, $"{name} label");
            }
        uiContext.SetTheme(ordinary);
    }

    internal static void Run(VulkanRendererBackend renderer, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int width, int height)
    {
        var labels = new[] { "토글 버튼과 값 공유", "Agyp", "한글 English", "V-Sync", "이벤트 대기 모드" };
        foreach (var samples in new[] { 1, 4 })
            foreach (var vector in new[] { false, true })
                for (var style = 0; style < 4; style++)
                    foreach (var label in labels)
                    {
                        renderer.SetMsaaSamples(samples);
                        var value = new UiState<bool>(false);
                        using UiWidget widget = style switch
                        {
                            0 => new SwitchPaint0(label, value.Binding),
                            1 => new SwitchPaint1(label, value.Binding),
                            2 => new SwitchPaint3(label, value.Binding),
                            _ => new SwitchPaint4(label, value.Binding)
                        };
                        var root = default(UiRect);
                        var pixels = capture($"switch-align-{samples}-{vector}-{style}-{Array.IndexOf(labels, label)}", ui =>
                        {
                            var viewport = new UiRect(0, 0, width, height);
                            ui.GetWindowDrawList().AddRectFilled(viewport, new(32, 32, 32), ui.WhiteTextureId, viewport);
                            if (vector)
                                ui.BeginLayer("switch-alignment-vector", new UiLayerOptions(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                            try
                            {
                                ui.SetCursorScreenPos(new(16, 16));
                                widget.Render(ui);
                                var position = ui.GetItemRectMin();
                                var size = ui.GetItemRectSize();
                                root = new(position.X, position.Y, size.X, size.Y);
                            }
                            finally { if (vector) ui.EndLayer(); }
                        });
                        var trackWidth = style < 2 ? 40 : 32;
                        var trackHeight = style < 2 ? 20 : 16;
                        var before = style % 2 == 1;
                        var left = before ? (int)root.X + 8 : (int)root.X + 8 + trackWidth + 6;
                        var right = before ? (int)(root.X + root.Width) - 8 - trackWidth - 6 : width;
                        var min = height;
                        var max = -1;
                        for (var y = 0; y < height; y++)
                            for (var x = left; x < right; x++)
                            {
                                var at = (y * width + x) * 4;
                                // Include antialiased ink support rather than just bright glyph interiors.
                                if (pixels[at] <= 32 || pixels[at + 1] <= 32 || pixels[at + 2] <= 32)
                                    continue;
                                min = Math.Min(min, y);
                                max = Math.Max(max, y);
                            }
                        var glyphCenter = (min + max + 1) * .5;
                        var trackCenter = root.Y + 8 + trackHeight * .5;
                        check(max >= min && Math.Abs(glyphCenter - trackCenter) <= .5,
                            $"actual Switch glyph ink centers on its track within pixel-grid parity : MSAA{samples}, vector={vector}, style={style}, label={label}, glyph={glyphCenter}, track={trackCenter}");
                    }
        renderer.SetMsaaSamples(1);
        foreach (var vector in new[] { false, true })
            foreach (var label in new[] { "품질 우선", "한글 Agyp" })
                foreach (var radio in new[] { false, true })
                {
                    using UiWidget widget = radio
                        ? Fluent.RadioGroup(string.Empty, new UiState<string?>("a"), new UiRadioOption[] { new("a", label) }, 220)
                        : Fluent.Checkbox(label, new UiState<bool>(false));
                    var pixels = capture($"selector-align-{radio}-{vector}-{label}", ui =>
                    {
                        var viewport = new UiRect(0, 0, width, height);
                        ui.GetWindowDrawList().AddRectFilled(viewport, new(32, 32, 32), ui.WhiteTextureId, viewport);
                        if (vector)
                            ui.BeginLayer("selector-align-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                        try
                        {
                            ui.SetCursorScreenPos(new(16, 16));
                            widget.Render(ui);
                        }
                        finally { if (vector) ui.EndLayer(); }
                    });
                    var min = height;
                    var max = -1;
                    for (var y = 16; y < 56; y++)
                        for (var x = 48; x < 240; x++)
                        {
                            var at = (y * width + x) * 4;
                            if (pixels[at] <= 32 || pixels[at + 1] <= 32 || pixels[at + 2] <= 32)
                                continue;
                            min = Math.Min(min, y);
                            max = Math.Max(max, y);
                        }
                    var center = (min + max + 1) * .5;
                    check(max >= min && Math.Abs(center - 32) <= .5,
                        $"selector glyph ink centers on its indicator : radio={radio}, vector={vector}, label={label}, center={center}");
                }
    }
}
