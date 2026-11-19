using Duxel.Core;

internal static class MultilineInputTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var value = "alpha\nbeta";
        var changed = false;
        var clip = new UiRect(0, 0, 320, 240);
        var nested = false;
        var id = "##multiline";
        var lineHeight = 0f;
        UiVector2 fieldMin = default;
        UiVector2 fieldSize = default;
        void Frame(UiInputState input, Action<UiImmediateContext>? probe = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                using var typography = new UiControlContext(ui).BeginTypography<UiRegularTypeface, UiFluentType300>();
                ui.SetCursorScreenPos(new(20, 20));
                if (probe is not null)
                {
                    probe(ui);
                    return;
                }
                ui.PushClipRect(clip, true);
                if (nested)
                    ui.BeginChild("multiline-outer", new(180, 160));
                ui.SetNextItemWidth(140);
                changed = ui.InputTextMultiline(id, ref value, 1024, 80f);
                lineHeight = ui.GetTextLineHeight();
                fieldMin = ui.GetItemRectMin();
                fieldSize = ui.GetItemRectSize();
                if (nested)
                {
                    ui.Dummy(new(600, 600));
                    ui.EndChild();
                }
                ui.PopClipRect();
            }));
            using var data = context.GetDrawData();
        }
        Frame(idle);
        check(!changed && value == "alpha\nbeta", "multiline idle preserves the bound text");
        Frame(idle with { MousePosition = new(30, 30), LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { LeftMouseReleased = true });
        check(context.State.ActiveId == id, "multiline pointer assigns the real editor owner");
        Frame(idle with { KeyEvents = [new(UiKey.End, true, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)], CharEvents = [new('x')] });
        check(changed && value == "alpha\nbeta\nx", "multiline Enter and text edit preserve logical newlines");
        Frame(idle with { KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        check(changed && value == "alpha\nbeta", "multiline undo restores the pre-edit text");
        Frame(idle with { KeyEvents = [new(UiKey.Y, true, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        check(changed && value == "alpha\nbeta\nx", "multiline redo restores the complete edit");
        Frame(idle with { KeyEvents = [new(UiKey.Escape, true, default)] });
        check(context.State.ActiveId is null, "multiline Escape releases editor focus");
        value = string.Join('\n', Enumerable.Repeat(new string('w', 80), 20));
        Frame(idle);
        context.State.SetScrollX(id, 0);
        context.State.SetScrollY(id, 0);
        Frame(idle with { MousePosition = new(40, 40), MouseWheel = -1, MouseWheelHorizontal = -1 });
        Console.WriteLine($"Multiline wheel offsets : {context.State.GetScrollX(id)}, {context.State.GetScrollY(id)}; line : {lineHeight}; field : {fieldMin} / {fieldSize}; length : {value.Length}");
        check(context.State.GetScrollY(id) > 0 && context.State.GetScrollX(id) > 0,
            "multiline owns both movable wheel axes");
        nested = true;
        Frame(idle);
        context.State.SetScrollY(id, 0);
        context.State.SetScrollX(id, 0);
        context.State.SetScrollY("multiline-outer", 30);
        context.State.SetScrollX("multiline-outer", 30);
        Frame(idle with { MousePosition = new(40, 40), MouseWheel = 1, MouseWheelHorizontal = 1 });
        check(context.State.GetScrollY(id) == 0 && context.State.GetScrollY("multiline-outer") < 30,
            "multiline upper boundary passes vertical wheel to its parent");
        check(context.State.GetScrollX(id) == 0 && context.State.GetScrollX("multiline-outer") < 30,
            "multiline left boundary passes horizontal wheel to its parent");
        nested = false;
        context.State.SetScrollX(id, 0);
        context.State.SetScrollY(id, 0);
        clip = new(0, 0, 10, 10);
        Frame(idle with { MousePosition = new(40, 40), MouseWheel = -1, MouseWheelHorizontal = -1 });
        check(context.State.GetScrollX(id) == 0 && context.State.GetScrollY(id) == 0,
            "clipped multiline content cannot consume either wheel axis");
        foreach (var height in new[] { float.NaN, float.PositiveInfinity, 0f })
        {
            Frame(idle, ui =>
            {
                var cursor = ui.GetCursorScreenPos();
                var previous = value;
                var rejected = false;
                try
                {
                    ui.InputTextMultiline("##invalid-height", ref value, 1024, height);
                }
                catch (ArgumentOutOfRangeException)
                {
                    rejected = true;
                }
                check(rejected && cursor == ui.GetCursorScreenPos() && value == previous
                    && !context.State.HasTextInputStorage("##invalid-height"),
                    "invalid multiline height fails before allocation and input storage : " + height);
            });
        }
        foreach (var invalid in new[]
        {
            new UiMultilineInputLayout(new(0, 0, float.NaN, 80), 20),
            new UiMultilineInputLayout(new(0, 0, 140, -1), 20),
            new UiMultilineInputLayout(new(0, 0, 140, 80), 0),
            new UiMultilineInputLayout(new(0, 0, 140, 80), float.PositiveInfinity)
        })
        {
            Frame(idle, ui =>
            {
                var previous = value;
                var rejected = false;
                try
                {
                    ui.EditMultilineInput("Invalid", "##invalid-layout", ref value, 1024, invalid);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
                check(rejected && value == previous && !context.State.HasTextInputStorage("##invalid-layout"),
                    "invalid prepared multiline geometry fails before input storage : " + invalid);
            });
        }
    }

    internal static void Render(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        foreach (var scene in new[] { "empty", "text", "overflow", "clip" })
        {
            var value = scene == "empty" ? string.Empty : scene == "overflow"
                ? string.Join('\n', Enumerable.Repeat(new string('w', 60), 12)) : "alpha\nbeta\ngamma";
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                using var typography = new UiControlContext(ui).BeginTypography<UiRegularTypeface, UiFluentType300>();
                ui.SetCursorScreenPos(new(20, 20));
                ui.PushClipRect(scene == "clip" ? new(35, 30, 80, 40) : new(0, 0, 320, 240), true);
                ui.SetNextItemWidth(140);
                ui.InputTextMultiline("##gpu-" + scene, ref value, 2048, 80f);
                ui.PopClipRect();
            }
            var expected = capture("multiline-" + scene, Draw);
            for (var frame = 0; frame < 4; frame++)
            {
                check(expected.AsSpan().SequenceEqual(capture("multiline-" + scene + "-reuse-" + frame, Draw)),
                    "legacy multiline GPU geometry survives acquired frame reuse : " + scene + " / " + frame);
            }
        }
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
