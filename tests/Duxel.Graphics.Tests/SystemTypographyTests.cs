using Duxel.Core;
using Duxel.Platform.Windows;

internal static class SystemTypographyTests
{
    internal static void Run(Action<bool, string> check)
    {
        using (var native = new WindowsSystemTextScale(() => { }))
        {
            Console.WriteLine($"Native UISettings.TextScaleFactor : {native.Scale:R}");
            check(native.Scale >= 1 && native.Scale <= 2.25f, "Native UISettings returns the documented accessibility scale");
            check(!native.Refresh(), "Unchanged typography does not request another update");
        }
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var state = new UiState<bool>(false);
        using var widget = new SwitchPaint0("이벤트 대기 모드 Agyp", state.Binding);
        UiVector2 Frame(float scale, float dpi)
        {
            context.SetContentScale(dpi);
            context.NewFrame(new(1f / 60, new(1000, 600), new(dpi, dpi)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 1000, 600), UiTextSettings.Default with
                {
                    Scale = scale
                });
            var size = default(UiVector2);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); widget.Render(ui); size = ui.GetItemRectSize(); }));
            using var data = context.GetDrawData();
            return size;
        }
        foreach (var dpi in new[] { .75f, 1f, 1.25f, 2f })
        {
            var baseline = Frame(1, dpi);
            foreach (var scale in new[] { 1.14f, 1.5f, 2.25f })
            {
                var size = Frame(scale, dpi);
                check(size.X > baseline.X && size.Y > baseline.Y, $"System text scale grows live Switch layout : text={scale}, DPI={dpi}");
                check(Frame(scale, dpi) == size, $"Unchanged scaled Switch retains stable measurement : text={scale}, DPI={dpi}");
            }
            check(Frame(1, dpi) == baseline, $"Returning text scale to 100% restores the original layout : DPI={dpi}");
            foreach (var snap in new[] { true, false })
            {
                context.NewFrame(new(1f / 60, new(1000, 600), new(dpi, dpi)),
                    new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                    new(0, 0, 1000, 600), UiTextSettings.Default with
                    {
                        PixelSnap = snap
                    });
                context.Render(new Screen(ui =>
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    ui.SetCursorScreenPos(new(12.37f, 18.61f));
                    ui.RenderTypography<UiRegularTypeface, UiFluentType300>("한글 Agyp", new(255, 255, 255));
                }));
                using var data = context.GetDrawData();
                var vertices = data.DrawLists.SelectMany(list => list.Vertices).ToArray();
                var aligned = vertices.All(vertex => MathF.Abs(vertex.Position.X * dpi - MathF.Round(vertex.Position.X * dpi)) < .0001f
                    && MathF.Abs(vertex.Position.Y * dpi - MathF.Round(vertex.Position.Y * dpi)) < .0001f);
                check(vertices.Length > 0 && aligned == snap, $"Raster placement respects PixelSnap on the physical grid : enabled={snap}, DPI={dpi}");
            }
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
