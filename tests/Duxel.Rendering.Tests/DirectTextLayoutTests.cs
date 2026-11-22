using Duxel.Core;
using Duxel.Platform.Windows;

internal static class DirectTextLayoutTests
{
    internal static void VerifyAdvanceContracts(Action<bool, string> check)
    {
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, -1f })
        {
            var backend = new ObservedBackend { LayoutAdvance = invalid };
            using var context = AdvanceContext(backend);
            using var button = Fluent.Button("Invalid layout");
            var rejected = false;
            try
            {
                AdvanceFrame(context, ui => { ui.SetCursorScreenPos(new(20, 2000)); button.Render(ui); });
            }
            catch (InvalidOperationException error) when (error.Message.Contains("logical advance", StringComparison.Ordinal)) { rejected = true; }
            check(rejected && backend.Layouts == 1 && backend.Rasters == 0,
                "Invalid layout advance fails before rasterization : " + invalid);
            backend.LayoutAdvance = 20;
            AdvanceFrame(context, ui => { ui.SetCursorScreenPos(new(20, 2000)); button.Render(ui); });
            check(backend.Layouts == 2 && backend.Rasters == 0,
                "Invalid layout metrics never enter widget-owned or context caches : " + invalid);
        }
        foreach (var layoutCapability in new[] { true, false })
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, -1f })
            {
                var backend = new ObservedBackend { RasterAdvance = invalid };
                using var context = AdvanceContext(layoutCapability ? backend : new RasterOnly(backend));
                var rejected = false;
                try
                {
                    AdvanceFrame(context, ui =>
                    {
                        if (layoutCapability)
                        {
                            ui.SetCursorScreenPos(new(20, 20));
                            ui.Text("Invalid raster");
                        }
                        else
                            ui.CalcTextSize("Invalid raster");
                    });
                }
                catch (InvalidOperationException error) when (error.Message.Contains("logical advance", StringComparison.Ordinal)) { rejected = true; }
                var owner = backend.Last;
                context.Dispose();
                check(rejected && owner is { Disposals: 1 },
                    "Invalid raster advance releases exclusive pixels exactly once : layout capability " + layoutCapability + " / " + invalid);
            }
    }
    private static UiContext AdvanceContext(IPlatformTextBackend backend)
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetPlatformTextBackend(backend);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath("fixture");
        context.SetTypographyFonts(new("fixture", "fixture", "fixture"));
        return context;
    }
    private static void AdvanceFrame(UiContext context, Action<UiImmediateContext> render)
    {
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
            new(default, false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(render));
        using var data = context.GetDrawData();
    }
    private sealed class RasterOnly(ObservedBackend backend) : IPlatformTextBackend, IPlatformFontMetricsBackend
    {
        public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result) => backend.TryMeasure(request, out result);
        public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result) => backend.TryRasterize(request, out result);
        public bool TryGetFontMetrics(string path, float size, out PlatformFontMetrics metrics) => backend.TryGetFontMetrics(path, size, out metrics);
    }
    internal static void Run(Action<bool, string> check)
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var backend = WindowsPlatformTextBackend.Instance;
        var samples = new[]{"", " ", "    ", "Hello 123", "jfy()", "AV To fi", "한글 abc", "abc 한글",
            "مرحبا", "Привет", "e\u0301", "\u200B", "\uFE0F", "😀", "a\tb", "a\r\nb", "\n\n", "a\n\nb"};
        var cases = 0;
        foreach (var font in new[] { "segoeui.ttf", "segoeuii.ttf", "malgun.ttf" })
            foreach (var size in new[] { 1f, 2f, 7f, 8f, 12.5f, 14f, 14.5f, 16f, 18f, 31.75f, 64f })
                foreach (var text in samples)
                {
                    var path = Path.Combine(fonts, font);
                    var secondary = Path.Combine(fonts, text == "😀" ? "seguiemj.ttf"
                        : text == "مرحبا" || font == "malgun.ttf" ? "segoeui.ttf" : "malgun.ttf");
                    var laidOut = backend.TryLayout(new(path, secondary, text, size), out var layout);
                    var painted = backend.TryRasterize(new(path, secondary, text, size), out var pixels);
                    try
                    {
                        if (laidOut != painted || laidOut && (layout.Width != pixels.Width || layout.Height != pixels.Height || layout.Baseline != pixels.Baseline ||
                            layout.FontAscent != pixels.FontAscent || layout.Advance != pixels.Advance))
                            throw new InvalidOperationException($"Text layout differs from raster: {font},size={size},text={text.Replace("\r", "\\r").Replace("\n", "\\n")}; layout={layout}; raster={pixels.Width}x{pixels.Height},baseline={pixels.Baseline},ascent={pixels.FontAscent},advance={pixels.Advance}");
                        cases++;
                    }
                    finally { pixels.PixelOwner?.Dispose(); }
                }
        check(cases == 594, "Windows pixel-free text layout exactly matches raster bounds/advance across594 font/size/Unicode/declared-secondary/multiline cases");
        VerifyDeferredPixels(check);
        VerifyRetainedMetrics(check);
        VerifyLayerAndBackendLifetime(check);
        VerifyActiveMeasureOnlyWorkingSet(check);
    }

    private static void VerifyActiveMeasureOnlyWorkingSet(Action<bool, string> check)
    {
        var backend = new ObservedBackend();
        using var context = AdvanceContext(backend);
        var limit = 0;
        var labels = Array.Empty<string>();
        AdvanceFrame(context, ui =>
        {
            limit = ui.DirectTextCacheOccupancy.Limit;
            labels = Enumerable.Range(0, limit + 16).Select(index => $"Document label {index}").ToArray();
        });

        for (var frame = 0; frame < 3; frame++)
            AdvanceFrame(context, ui =>
            {
                foreach (var label in labels)
                    ui.CalcTextSize(label);
            });
        check(backend.Layouts == labels.Length && backend.Rasters == 0,
            "Repeated document measurement keeps the active pixel-free working set above the idle cache limit");

        AdvanceFrame(context, _ => { });
        AdvanceFrame(context, ui => check(ui.DirectTextCacheOccupancy.Entries == limit,
            "Previously measured document labels return to the idle cache limit after leaving the working set"));
    }

    private static void VerifyLayerAndBackendLifetime(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var first = new ObservedBackend();
        context.SetPlatformTextBackend(first);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath("fixture");
        var builds = 0;
        var screen = new Screen(ui =>
        {
            if (ui.BeginLayer("owned-metrics", new(true, 1, default)
            {
                ContentVersion = 1
            }))
            {
                builds++;
                ui.SetCursorScreenPos(new(20, 20));
                ui.Text("Layer");
            }
            ui.EndLayer();
        });
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
                new(default, false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(screen);
            using var data = context.GetDrawData();
        }
        Frame();
        Frame();
        check(builds == 1, "static text layer replays before backend replacement");
        var second = new ObservedBackend();
        context.SetPlatformTextBackend(second);
        Frame();
        check(builds == 2, "backend replacement invalidates a retained text layer even when its content version stays unchanged");
        second.TextLayoutVersion++;
        Frame();
        check(builds == 3, "backend revision invalidates static text layers without replacing the backend instance");
        using var retained = Fluent.Button("Weak backend");
        var weak = RenderTemporaryBackend(retained);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        check(!weak.IsAlive, "widget-owned text metrics do not keep a disposed context's backend alive");
        GC.KeepAlive(retained);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference RenderTemporaryBackend(UiFluentButton button)
    {
        var backend = new ObservedBackend();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetPlatformTextBackend(backend);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath("fixture");
        context.SetTypographyFonts(new("fixture", "fixture", "fixture"));
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
            new(default, false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(ui => { ui.SetCursorScreenPos(new(20, 2000)); button.Render(ui); }));
        using var data = context.GetDrawData();
        return new(backend);
    }

    private static void VerifyRetainedMetrics(Action<bool, string> check)
    {
        var backend = new ObservedBackend();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetPlatformTextBackend(backend);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath("fixture");
        context.SetDirectTextBaseFontSize(14);
        var buttons = Enumerable.Range(0, 1000).Select(i => new UiButton<UiReleaseButtonBehavior, UiDefaultButtonPresentation>("Retained " + i)).ToArray();
        try
        {
            var screen = new Screen(ui => { for (var i = 0; i < buttons.Length; i++) { ui.SetCursorScreenPos(new(20, 2000 + i * 30)); buttons[i].Render(ui); } });
            void Frame()
            {
                context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
                    new(default, false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 320, 240), UiTextSettings.Default);
                context.Render(screen);
                using var data = context.GetDrawData();
            }
            Frame();
            Frame();
            Frame();
            check(backend.Layouts == 1000 && backend.Rasters == 0, "1000 retained controls reuse owned metrics beyond the512-entry context cache without rasterizing");
            buttons[0].Label = "Changed";
            Frame();
            check(backend.Layouts == 1001, "a retained label mutation invalidates only its own text metrics");
            context.SetDirectTextBaseFontSize(14.01f);
            Frame();
            check(backend.Layouts == 2001, "exact physical font size invalidates retained and context metrics within a quarter-pixel bucket");
            var replacement = new ObservedBackend();
            context.SetPlatformTextBackend(replacement);
            Frame();
            check(replacement.Layouts == 1000 && replacement.Rasters == 0, "backend replacement invalidates both owned and shared text metrics");
            replacement.TextLayoutVersion++;
            Frame();
            check(replacement.Layouts == 2000, "a same-instance backend revision invalidates owned and shared text metrics");
            context.SetDirectTextPrimaryFontPath("other-fixture");
            Frame();
            check(replacement.Layouts == 3000, "font-face changes invalidate each retained control's exact metrics");
            buttons[0].Label = new string('x', 300);
            Frame();
            check(buttons[0].RetainedTextLayoutCharacters == 0 && buttons.All(b => b.RetainedTextLayoutCharacters <= 1024),
                "widget text metrics have one bounded slot and release prior labels when admission limits are exceeded");
        }
        finally { foreach (var button in buttons) button.Dispose(); }
        check(buttons.All(b => b.RetainedTextLayoutCharacters == 0), "widget disposal releases all owned text metrics and font/string references");
    }

    private static void VerifyDeferredPixels(Action<bool, string> check)
    {
        var backend = new ObservedBackend();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetPlatformTextBackend(backend);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath("fixture");
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
                new(default, false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(render));
            using var data = context.GetDrawData();
        }
        Frame(ui => ui.CalcTextSize("retained"));
        check(backend.Layouts == 1 && backend.Rasters == 0, "DirectText measurement retains exact metrics without raster pixels");
        Frame(ui => { ui.SetCursorScreenPos(new(20, 2000)); ui.Text("retained"); });
        check(backend.Layouts == 1 && backend.Rasters == 0, "offscreen text reuses metrics and does not rasterize before clipping");
        Frame(ui => { ui.SetCursorScreenPos(new(20, 20)); ui.Text("retained"); });
        check(backend.Layouts == 1 && backend.Rasters == 1, "newly visible text lazily creates its unchanged raster once");
        Frame(ui => { ui.SetCursorScreenPos(new(30, 30)); ui.Text("retained"); });
        check(backend.Layouts == 1 && backend.Rasters == 1, "subsequent text movement reuses the owned raster and metrics");
        Frame(ui => { for (var i = 0; i < 1000; i++) ui.CalcTextSize("offscreen-" + i); });
        Frame(ui => { ui.SetCursorScreenPos(new(20, 20)); ui.Text("retained"); });
        check(backend.Rasters == 1, "measurement-only cache pressure preserves the visible text texture working set");
        backend.Mismatch = true;
        var rejected = false;
        try
        {
            Frame(ui => { ui.SetCursorScreenPos(new(20, 20)); ui.Text("different"); });
        }
        catch (InvalidOperationException e) when (e.Message.Contains("different layout and raster metrics", StringComparison.Ordinal)) { rejected = true; }
        check(rejected && backend.Last!.Disposed, "inconsistent backend metrics fail explicitly and release unclaimed pixels");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class ObservedBackend : IPlatformTextBackend, IPlatformTextLayoutBackend, IPlatformFontMetricsBackend
    {
        public bool TryGetFontMetrics(string fontPath, float fontSize, out PlatformFontMetrics metrics)
        {
            metrics = new(10, 2, 0);
            return true;
        }
        public ulong TextLayoutVersion
        {
            get; set;
        }
        internal int Layouts, Rasters; internal bool Mismatch; internal Pixels? Last;
        internal float LayoutAdvance = 20, RasterAdvance = 20;
        public bool TryLayout(in PlatformTextMeasureRequest request, out PlatformTextLayoutResult result)
        {
            Layouts++;
            result = new(20, 12, 10, 10, LayoutAdvance);
            return true;
        }
        public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result)
        {
            result = new(20, 12, 10);
            return true;
        }
        public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result)
        {
            Rasters++;
            Last = new();
            Last.Memory.Span.Fill(255);
            result = new(Mismatch ? 21 : 20, 12, Last.Memory, 10, 10, RasterAdvance)
            {
                PixelOwner = Last
            };
            return true;
        }
    }
    private sealed class Pixels : System.Buffers.IMemoryOwner<byte>
    {
        private readonly byte[] _bytes = new byte[21 * 12 * 4]; internal bool Disposed; internal int Disposals;
        public Memory<byte> Memory => _bytes;
        public void Dispose()
        {
            Disposals++;
            Disposed = true;
        }
    }
}
