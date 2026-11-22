using Duxel.Core;

internal static class OrderedInputBatchTests
{
    private static readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    private static readonly UiFrameInfo FrameInfo = new(1f / 60, new(400, 300), new(1, 1));

    internal static void Run(Action<bool, string> check)
    {
        InputEventTimingTests.Run(check);
        KeyRepeatSourceTests.Run(check);
        InputProcessingMetricsTests.Run(check);
        UnpublishedDrawingTests.Run(check);
        InputPresentationPhaseTests.Run(check);
        InteractivePresentationPhaseTests.Run(check);
        InputLayerMetadataTests.Run(check);
        VerifySliderMoveCoalescing(check);
        foreach (var kind in new[] { "Input", "Textarea", "Markdown" })
            foreach (var reverse in new[] { false, true })
                foreach (var charFirst in new[] { false, true })
                {
                    using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                    ControlFontFixture.Configure(context);
                    var first = "a";
                    var second = "b";
                    var binding = new UiBinding<string>(() => first, text => first = text);
                    using UiWidget a = kind switch
                    {
                        "Input" => Fluent.Input(string.Empty, binding, 256, 240),
                        "Textarea" => Fluent.Textarea(string.Empty, binding, 256, 240, 90),
                        "Markdown" => new UiMarkdownEditor("Ordered A", binding, new() { Width = 240, Height = 90 }),
                        _ => throw new InvalidOperationException("Unknown ordered consumer.")
                    };
                    using var b = Fluent.Input(string.Empty, new UiBinding<string>(() => second, text => second = text), 256, 240);
                    var renders = 0;
                    var screen = new Screen(ui =>
                    {
                        renders++;
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        void DrawA()
                        {
                            ui.SetCursorScreenPos(default);
                            a.Render(ui);
                        }
                        void DrawB()
                        {
                            ui.SetCursorScreenPos(new(0, 150));
                            b.Render(ui);
                        }
                        if (reverse)
                        {
                            DrawB();
                            DrawA();
                        }
                        else
                        {
                            DrawA();
                            DrawB();
                        }
                    });
                    void Single(UiInputState input)
                    {
                        context.NewFrame(FrameInfo, input, new(0, 0, 400, 300), UiTextSettings.Default);
                        context.Render(screen);
                        using var data = context.GetDrawData();
                    }
                    Single(Idle);
                    Single(Idle with
                    {
                        MousePosition = new(200, 15),
                        LeftMouseDown = true,
                        LeftMousePressed = true
                    });
                    Single(Idle with
                    {
                        MousePosition = new(200, 15),
                        LeftMouseReleased = true
                    });
                    var click = Idle with
                    {
                        MousePosition = new(200, 160),
                        LeftMouseDown = true,
                        LeftMousePressed = true
                    };
                    var character = Idle with
                    {
                        CharEvents = [new('X')]
                    };
                    UiInputState[] steps = charFirst ? [character, click] : [click, character with { MousePosition = click.MousePosition, LeftMouseDown = true }];
                    var before = renders;
                    var beforeTime = context.State.TimeSeconds;
                    context.NewInputBatch(FrameInfo, steps, new(0, 0, 400, 300), UiTextSettings.Default);
                    context.Render(screen);
                    var prefix = $"Ordered Core : {kind}, reverse={reverse}, charFirst={charFirst}";
                    using (context.GetDrawData())
                    {
                        check(first == (charFirst ? "aX" : "a") && second == (charFirst ? "b" : "bX"), prefix + $" / actual={first}|{second}");
                        check(renders - before == steps.Length, prefix + " / each logical input step is consumed once before one final draw result");
                        check(Math.Abs(context.State.TimeSeconds - beforeTime - FrameInfo.DeltaTime) < 0.0000001,
                            prefix + " / host time advances once for the complete batch");
                    }
                    first = "a";
                    Single(Idle);
                    Single(Idle with
                    {
                        MousePosition = new(200, 15),
                        LeftMouseDown = true,
                        LeftMousePressed = true
                    });
                    Single(Idle with
                    {
                        LeftMouseReleased = true
                    });
                    var start = Idle with
                    {
                        MousePosition = new(6, 15),
                        LeftMouseDown = true,
                        LeftMousePressed = true
                    };
                    UiInputState[] sameControl = charFirst ? [character, start] : [start, character with { MousePosition = start.MousePosition, LeftMouseDown = true }];
                    context.NewInputBatch(FrameInfo, sameControl, new(0, 0, 400, 300), UiTextSettings.Default);
                    context.Render(screen);
                    var id = context.State.ActiveId ?? throw new InvalidOperationException("Missing ordered editor focus.");
                    using (context.GetDrawData())
                    {
                        check(first == (charFirst ? "aX" : "Xa") && context.State.GetCursor(id, -1) == (charFirst ? 0 : 1),
                            prefix + " / moving the same editor's caret preserves the insertion order");
                    }
                    first = "ab";
                    Single(Idle with
                    {
                        LeftMouseReleased = true
                    });
                    context.State.SetCursor(id, 2);
                    context.State.SetSelection(id, new(2, 2));
                    var selectAll = Idle with
                    {
                        KeyEvents = [new(UiKey.A, true, KeyModifiers.Ctrl)],
                        Modifiers = KeyModifiers.Ctrl
                    };
                    var z = Idle with
                    {
                        CharEvents = [new('Z')]
                    };
                    context.NewInputBatch(FrameInfo, charFirst ? [z, selectAll] : [selectAll, z], new(0, 0, 400, 300), UiTextSettings.Default);
                    context.Render(screen);
                    using var keyResult = context.GetDrawData();
                    check(first == (charFirst ? "abZ" : "Z") && context.State.GetSelection(id).HasSelection == charFirst,
                        prefix + " / Ctrl+A and text preserve their cross-channel order");
                }

        using var empty = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var rejected = false;
        try
        {
            empty.NewInputBatch(FrameInfo, [], new(0, 0, 400, 300), UiTextSettings.Default);
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected && empty.State.TimeSeconds == 0, "empty batches fail before advancing host time");

        using var actions = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(actions);
        var calls = string.Empty;
        using var buttonA = Fluent.Button("Action A", () => calls += "A");
        using var buttonB = Fluent.Button("Action B", () => calls += "B");
        var actionScreen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(default);
            buttonA.Render(ui);
            ui.SetCursorScreenPos(new(0, 100));
            buttonB.Render(ui);
        });
        actions.NewFrame(FrameInfo, Idle, new(0, 0, 400, 300), UiTextSettings.Default);
        actions.Render(actionScreen);
        using (actions.GetDrawData())
        {
        }
        actions.NewInputBatch(FrameInfo,
        [
            Idle with { MousePosition = new(20, 15), LeftMouseDown = true, LeftMousePressed = true },
            Idle with { MousePosition = new(20, 15), LeftMouseReleased = true },
            Idle with { MousePosition = new(20, 115), LeftMouseDown = true, LeftMousePressed = true },
            Idle with { MousePosition = new(20, 115), LeftMouseReleased = true }
        ], new(0, 0, 400, 300), UiTextSettings.Default);
        actions.Render(actionScreen);
        using (actions.GetDrawData())
            check(calls == "AB", "two complete pointer clicks in one batch invoke both actual buttons once in order");

        var attempts = 0;
        var fail = new Screen(_ => { if (++attempts == 1) throw new InvalidOperationException("Owned batch callback probe"); });
        actions.NewInputBatch(FrameInfo, [Idle, Idle], new(0, 0, 400, 300), UiTextSettings.Default);
        var callbackFailed = false;
        try
        {
            actions.Render(fail);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Owned batch callback probe") { callbackFailed = true; }
        actions.NewFrame(FrameInfo, Idle, new(0, 0, 400, 300), UiTextSettings.Default);
        actions.Render(fail);
        using (actions.GetDrawData())
            check(callbackFailed && attempts == 2, "a failed input callback is surfaced and a later frame does not replay the abandoned batch");
    }

    private static void VerifySliderMoveCoalescing(Action<bool, string> check)
    {
        foreach (var coalesce in new[] { false, true })
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            var value = new UiState<float>(0);
            using var slider = Fluent.Slider("Position", value.Binding, 0, 100, 1);
            slider.Size = new(220, 32);
            slider.Options = slider.Options with { CoalescePointerMoves = coalesce };
            var observed = new List<(float X, float Value, bool Dragging)>();
            var screen = new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                slider.Render(ui);
                observed.Add((ui.GetMousePos().X, value.Value, slider.IsPointerDragging));
            });
            UiInputState Move(float x) => Idle with { MousePosition = new(x, 16), LeftMouseDown = true };
            context.NewInputBatch(FrameInfo,
                [Move(30) with { LeftMousePressed = true }, Move(50), Move(70),
                    Move(70) with { KeyEvents = [new(UiKey.RightArrow, true, default)] },
                    Move(90), Move(110), Move(140),
                    Move(140) with { LeftMouseDown = false, LeftMouseReleased = true }, Move(160) with { LeftMouseDown = false }],
                new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using var data = context.GetDrawData();
            float[] expected = coalesce ? [30, 70, 70, 140, 140, 160] : [30, 50, 70, 70, 90, 110, 140, 140, 160];
            var keyboard = observed.FindIndex(1, item => item.X == 70);
            check(observed.Select(item => item.X).SequenceEqual(expected) && keyboard >= 0
                && observed[keyboard].Value == 30 && observed[keyboard + 1].Value == 31
                && observed[^3] == (140, 65, true) && observed[^2] == (140, 65, false)
                && observed[^1] == (160, 65, false) && context.InputProcessingMetrics.BuiltDrawings == 1,
                $"ordered slider keeps keyboard, final drag position and release, opt-in={coalesce}");
            context.NewInputBatch(FrameInfo, [Move(30) with { LeftMousePressed = true }],
                new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using var pressed = context.GetDrawData();
            observed.Clear();
            context.NewInputBatch(FrameInfo, [Move(50), Move(70), Move(90), Move(120)],
                new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using var dragged = context.GetDrawData();
            expected = coalesce ? [120] : [50, 70, 90, 120];
            check(observed.Select(item => item.X).SequenceEqual(expected) && observed[^1] == (120, 55, true)
                && context.InputProcessingMetrics.BuiltDrawings == 1,
                $"a previously captured slider starts a pure move batch at the final position when opted in, opt-in={coalesce}");
        }
    }

    internal static void RunGpu(Action<bool, string> check)
    {
        InputPresentationGpuTests.Run(check);
        foreach (var frameSlots in new[] { 1, 3 })
            foreach (var charFirst in new[] { false, true })
            {
                using var reference = new PresentationReuseTests.Fixture(frameSlots, false);
                using var candidate = new PresentationReuseTests.Fixture(frameSlots, true);
                var first = new[] { "a", "a" };
                var second = new[] { "b", "b" };
                using var ra = Fluent.Input(string.Empty, new UiBinding<string>(() => first[0], text => first[0] = text), 256, 240);
                using var rb = Fluent.Input(string.Empty, new UiBinding<string>(() => second[0], text => second[0] = text), 256, 240);
                using var ca = Fluent.Input(string.Empty, new UiBinding<string>(() => first[1], text => first[1] = text), 256, 240);
                using var cb = Fluent.Input(string.Empty, new UiBinding<string>(() => second[1], text => second[1] = text), 256, 240);
                void Draw(UiImmediateContext ui, UiWidget a, UiWidget b)
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    ui.GetWindowDrawList().AddRectFilled(new(0, 0, 320, 240), new(11, 33, 71), ui.WhiteTextureId);
                    ui.SetCursorScreenPos(new(20, 20));
                    a.Render(ui);
                    ui.SetCursorScreenPos(new(20, 120));
                    b.Render(ui);
                }
                var expectedScreen = new Screen(ui => Draw(ui, ra, rb));
                var actualScreen = new Screen(ui => Draw(ui, ca, cb));
                foreach (var input in new[]
                {
                    Idle,
                    Idle with { MousePosition = new(200, 30), LeftMouseDown = true, LeftMousePressed = true },
                    Idle with { MousePosition = new(200, 30), LeftMouseReleased = true }
                })
                {
                    using var expectedWarm = reference.Build(ui => Draw(ui, ra, rb), input);
                    reference.Render(expectedWarm);
                    using var actualWarm = candidate.Build(ui => Draw(ui, ca, cb), input);
                    candidate.Render(actualWarm);
                }
                var click = Idle with
                {
                    MousePosition = new(200, 130),
                    LeftMouseDown = true,
                    LeftMousePressed = true
                };
                var text = Idle with
                {
                    CharEvents = [new('X')]
                };
                UiInputState[] steps = charFirst ? [text, click] : [click, text with { MousePosition = click.MousePosition, LeftMouseDown = true }];
                var frame = reference.FrameInfo;
                byte[] expected = [];
                for (var i = 0; i < steps.Length; i++)
                {
                    reference.Context.NewFrame(i == 0 ? frame : frame with
                    {
                        DeltaTime = 0
                    }, steps[i], new(0, 0, 320, 240), UiTextSettings.Default);
                    reference.Context.Render(expectedScreen);
                    using var data = reference.Context.GetDrawData();
                    expected = reference.Render(data);
                }
                candidate.Context.NewInputBatch(frame, steps, new(0, 0, 320, 240), UiTextSettings.Default);
                candidate.Context.Render(actualScreen);
                using var final = candidate.Context.GetDrawData();
                var actual = candidate.Render(final);
                var prefix = $"Ordered GPU : slots={frameSlots}, charFirst={charFirst}";
                check(first[0] == first[1] && second[0] == second[1] && first[1] == (charFirst ? "aX" : "a") && second[1] == (charFirst ? "b" : "bX"),
                    prefix + " / sequential and batched consumers preserve the expected bound source");
                check(actual.AsSpan().SequenceEqual(expected), prefix + " / one final retained GPU readback matches sequential full-repaint pixels"
                    + " / expected SHA256=" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expected))
                    + "; actual SHA256=" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual)));
            }
    }
}
