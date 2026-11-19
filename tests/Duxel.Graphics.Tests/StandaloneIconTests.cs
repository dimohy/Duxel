using Duxel.Core;

internal static class StandaloneIconTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var painter = UiMenuIcon.Create<Probe, Probe>();
        using var icon = Fluent.Icon(painter, new(24, 20), new(10, 20, 30));
        void Frame(Action<UiImmediateContext> action)
        {
            context.NewFrame(new(1f / 60, new(320, 200), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 200), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                action(ui);
            }));
            using var data = context.GetDrawData();
        }
        Frame(ui =>
        {
            var before = Probe.Paints;
            check(icon.Measure(ui, new(300, 100)) == new UiVector2(24, 20) && Probe.Paints == before,
                "standalone icon natural measurement never invokes its painter");
            check(icon.MeasureConstrained(ui, new(40, 40, 30, 30)) == new UiVector2(40, 30),
                "standalone icon obeys explicit parent constraints");
            icon.Render(ui);
        });
        check(Probe.Bounds.Width == 24 && Probe.Bounds.Height == 20 && Probe.Color == icon.Color,
            "compiled icon receives allocated bounds and explicit foreground");
        check(Probe.Interaction == default && Fluent.ComponentNames.Contains("Icon"),
            "decorative icon is exposed without simulated hover or activation");
        var stable = requests;
        Frame(icon.Render);
        Frame(icon.Render);
        check(requests == stable, "unchanged standalone icon painting does not request another frame");
        icon.Size = new(32, 32);
        icon.Color = new(30, 40, 50);
        check(requests > stable, "icon property changes wake event-wait rendering");
        stable = requests;
        icon.Size = icon.Size;
        icon.Color = icon.Color;
        icon.Icon = icon.Icon;
        check(requests == stable, "identical icon properties do not request frames");
        Frame(icon.Render);
        check(Probe.Bounds.Width == 32 && Probe.Color == icon.Color, "new size and foreground reach the next paint");
        using (var allocated = Dux.SizedBox(Fluent.Icon(painter, new(20, 20), icon.Color), 64, 48))
        {
            Frame(allocated.Render);
            check(Probe.Bounds.Width == 64 && Probe.Bounds.Height == 48,
                $"standalone icon paints the actual parent allocation instead of its natural size : {Probe.Bounds.Width} x {Probe.Bounds.Height}");
        }
        static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
        }
        check(Throws<ArgumentException>(() => icon.Icon = default), "empty painters are rejected before mutation");
        check(Throws<ArgumentOutOfRangeException>(() => icon.Size = new(float.NaN, 20)) &&
            Throws<ArgumentOutOfRangeException>(() => icon.Size = new(0, 20)), "invalid icon extents are rejected");
        var document = new Document();
        using var svgPainter = new UiSvgIcon(document);
        using var svg = Fluent.Icon(svgPainter.MenuIcon, new(20, 20), new(255, 255, 255));
        Frame(svg.Render);
        Frame(svg.Render);
        check(document.GpuRequests == 1 && document.Rasters == 0, "standalone SVG reuses its GPU scene across frames without CPU pixels");
        stable = requests;
        document.Reload();
        check(requests > stable, "borrowed SVG source changes wake the standalone icon");
        Frame(svg.Render);
        check(document.GpuRequests == 2 && document.Rasters == 0, "SVG revision invalidation replaces the standalone GPU output");
        svg.Dispose();
        using var survivor = Fluent.Icon(svgPainter.MenuIcon, new(20, 20), new(255, 255, 255));
        Frame(survivor.Render);
        check(document.GpuRequests == 2 && document.Rasters == 0, "disposing a standalone widget does not dispose its borrowed SVG painter");
        icon.Dispose();
        check(Throws<ObjectDisposedException>(() => icon.Color = new(1, 2, 3)), "disposed icon rejects property changes");
        check(icon.Icon.IsEmpty, "disposed standalone icon releases its borrowed painter reference");
    }

    private readonly struct Probe : IUiButtonIcon
    {
        internal static int Paints;
        internal static UiRect Bounds;
        internal static UiColor Color;
        internal static UiButtonInteraction Interaction;
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
        {
            Paints++;
            Bounds = bounds;
            Color = color;
            Interaction = interaction;
        }
    }

    private sealed class Document : IUiGpuSvgDocument
    {
        private readonly UiGpuVectorScene _scene = new(new(20, 20), [], 0);
        internal int GpuRequests;
        public UiGpuVectorScene GpuScene { get { GpuRequests++; return _scene; } }
        public UiVector2 IntrinsicSize => new(20, 20);
        public long Revision { get; private set; }
        public event Action? Changed;
        internal int Rasters;
        internal void Reload() { Revision++; Changed?.Invoke(); }
        public UiSvgRaster Rasterize(int width, int height)
        {
            Rasters++;
            return new(width, height, new byte[width * height * 4]);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> action) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => action(ui);
    }
}
