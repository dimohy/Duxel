using Duxel.Core;

internal static class LegacySliderRangeTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        var horizontal = 10f;
        var stepped = 10f;
        var vertical = 10f;
        var integer = 10;
        var verticalInteger = 10;
        var angle = 0f;
        var disabled = false;
        var clipped = false;
        var maximum = 100f;
        var changed = 0;
        var events = 0;
        tree.RangeChanged += _ => events++;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui =>
        {
            ui.BeginDisabled(disabled);
            ui.SetCursorScreenPos(new(8, clipped ? 800 : 8));
            if (ui.SliderFloat("Continuous##stable", ref horizontal, 0, maximum)) changed++;
            ui.SetCursorScreenPos(new(8, 50));
            if (ui.SliderFloat("Keyboard step", ref stepped, 0, 100, 5, "0.00")) changed++;
            ui.SetCursorScreenPos(new(8, 90));
            if (ui.VSliderFloat("Vertical", new(40, 100), ref vertical, 0, 100)) changed++;
            ui.SetCursorScreenPos(new(8, 205));
            if (ui.SliderInt("Integer", ref integer, 0, 100)) changed++;
            ui.SetCursorScreenPos(new(8, 245));
            if (ui.VSliderInt("Vertical integer", new(40, 100), ref verticalInteger, 0, 100)) changed++;
            ui.SetCursorScreenPos(new(8, 355));
            if (ui.SliderAngle("Angle", ref angle, -180, 180)) changed++;
            ui.EndDisabled();
        });
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            for (var id = tree.Root.First; id is not null;)
            {
                if (!tree.TryGet(id, out var element)) throw new InvalidOperationException("Missing published legacy Slider.");
                if (element.Node.Name == name) return element;
                id = element.Next;
            }
            throw new InvalidOperationException("Legacy Slider not found : " + name);
        }
        UiAccessibilityResult Request(string name, double value)
        {
            var element = Find(name);
            return tree.RequestRangeValue(element.Node.Id, element.RuntimeId, value, wait : false);
        }
        Frame();
        var continuous = Find("Continuous");
        check(continuous.Node.Role == UiAccessibilityRole.Slider && continuous.Node.Range == new UiAccessibilityRangeInfo(10, 0, 100, 0, 10) &&
            !continuous.Node.SupportsInvoke && !continuous.Node.ValuePattern,
            "legacy continuous Slider publishes numeric semantics and strips hidden label identity");
        check(Find("Vertical").Node.Range is { Vertical : true, SmallChange : 0 } &&
            Find("Integer").Node.Range is { SmallChange : 1 } && Find("Vertical integer").Node.Range is { SmallChange : 1, Vertical : true },
            "legacy vertical and integer controls publish their actual orientation and numeric change contract");
        var revision = tree.Revision;
        Frame();
        check(tree.Revision == revision && events == 0 && changed == 0, "unchanged legacy Slider frames emit no range event or change result");
        check(Request("Continuous", 12.25) == UiAccessibilityResult.Success && Request("Keyboard step", 12.25) == UiAccessibilityResult.Success &&
            Request("Vertical", 16.75) == UiAccessibilityResult.Success && Request("Integer", 12.5) == UiAccessibilityResult.Success &&
            Request("Vertical integer", 13.5) == UiAccessibilityResult.Success && Request("Angle", 90) == UiAccessibilityResult.Success,
            "all legacy numeric requests enter the existing ordered typed queue");
        Frame();
        check(horizontal == 12.25f && stepped == 12.25f && vertical == 16.75f && integer == 12 && verticalInteger == 14 &&
            MathF.Abs(angle - MathF.PI / 2) < .000001f && changed == 6,
            "legacy mutations preserve continuous floats, ties-to-even integer conversion and degree-to-radian caller values");
        check(Find("Integer").Node.Range?.Value == integer && Find("Vertical integer").Node.Range?.Value == verticalInteger &&
            Find("Angle").Node.Range?.Value == 90 && events == 6,
            "legacy publication exposes committed integer values and displayed angle units rather than intermediate floats");
        Request("Continuous", 12.25); Request("Integer", 12.5); Frame();
        check(changed == 6 && events == 6, "idempotent legacy requests including rounded integer values produce no duplicate changes");
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1, 101 })
            check(Request("Continuous", invalid) == UiAccessibilityResult.InvalidArgument, "legacy range rejects invalid numeric input : " + invalid);
        Request("Continuous", 20); disabled = true; Frame();
        check(horizontal == 12.25f && Request("Continuous", 20) == UiAccessibilityResult.Disabled,
            "legacy numeric changes revalidate live disabled state before touching the caller value");
        disabled = false; Frame(); Request("Continuous", 20); clipped = true; Frame();
        check(horizontal == 12.25f && Find("Continuous").Node.Offscreen,
            "legacy numeric changes reject a control clipped after request acceptance");
        clipped = false; Frame(); Request("Continuous", 80); maximum = 50; Frame();
        check(horizontal == 12.25f && Find("Continuous").Node.Range?.Maximum == 50,
            "legacy numeric changes revalidate a range narrowed after queue acceptance");
        Request("Continuous", 20); Request("Continuous", 30); Frame();
        check(horizontal == 20, "legacy Slider consumes at most one ordered mutation per frame");
        Frame(); check(horizontal == 30, "legacy Slider retains the next mutation for the following frame");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
