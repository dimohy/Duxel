using Duxel.Core;

internal static class ScrollViewPrecisionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var scale in new[] { 1f, 1.25f, 2f })
        foreach (var axis in Enum.GetValues<UiScrollAxis>())
        foreach (var height in new[] { 719.35f, 755.2f, 858.4f })
        {
            using var child = new Probe();
            using var scroll = Dux.ScrollView(child, new(axis, 300.25f, height));
            scroll.Offset = new(float.MaxValue, float.MaxValue);
            var origin = new UiVector2(13.125f, 63.8f);
            UiRect expected = default;
            context.NewFrame(new(.016f, new(1400, 1200), new(scale, scale)), idle, new(0, 0, 1400, 1200), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(origin);
                var thickness = context.GetStyleData().ScrollbarSize;
                var viewport = new UiRect(origin.X, origin.Y,
                    300.25f - (axis != UiScrollAxis.Horizontal ? thickness : 0),
                    height - (axis != UiScrollAxis.Vertical ? thickness : 0));
                using (new UiControlContext(ui).Clip(viewport)) expected = new UiControlContext(ui).ClipRect;
                scroll.Render(ui);
            }));
            using var data = context.GetDrawData();
            check(child.Clip == expected, $"fractional {axis} end-scroll keeps its exact viewport clip at scale{scale} / height{height}");
        }
    }
    private sealed class Probe : UiControlWidget
    {
        internal UiRect Clip;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(18000.35f, 14000.33f));
        protected override void RenderControl(UiImmediateContext ui)
        {
            var context = new UiControlContext(ui);
            ui.AllocateLayoutBox(context.LayoutConstraints.Constrain(new(18000.35f, 14000.33f)));
            Clip = context.ClipRect;
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
