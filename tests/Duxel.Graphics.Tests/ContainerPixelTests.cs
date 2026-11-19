using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class ContainerPixelTests
{
    internal static void Run(string output, int slots, string mode)
    {
        var virtualList = mode is "virtual-list-pixels" or "virtual-list-host-pixels" or "virtual-list-rtl-pixels";
        var gridHostPixels = mode == "virtual-grid-host-pixels";
        var virtualGrid = mode == "virtual-grid-pixels" || gridHostPixels;
        var clipHostPixels = mode == "clip-host-pixels";
        if (Directory.Exists(output))
            throw new IOException("Container pixel validation requires a fresh output directory.");
        Directory.CreateDirectory(output);

        using var fixture = new PresentationReuseTests.Fixture(slots, true);
        var checks = new List<string>();
        var captures = new List<ContainerPixelCapture>();
        var containerChecks = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
        }

        foreach (var samples in new[] { 1, 4 })
        {
            fixture.Renderer.SetMsaaSamples(samples);
            foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
            {
                fixture.Context.SetDesign(scheme == UiFluentColorScheme.Light
                    ? UiCompiledDesign.Windows11 : UiCompiledDesign.Windows11Dark);
                var prefix = $"msaa{samples}-{scheme}-";
                byte[] Capture(string name, Action<UiImmediateContext> draw)
                {
                    var pixels = fixture.Capture(draw);
                    captures.Add(new(prefix + name, Convert.ToHexString(SHA256.HashData(pixels))));
                    return pixels;
                }

                byte[] CaptureGridHost(string name, Action<UiImmediateContext> draw)
                {
                    var pixels = Capture(name, draw);
                    if (name == "grid-clear" || name.StartsWith("grid-fit-start-rtlFalse-narrowFalse-", StringComparison.Ordinal)
                        || name.StartsWith("grid-indexed-billion-tail-rtlTrue-narrowTrue-", StringComparison.Ordinal)
                        || name.StartsWith("grid-fixed-billion-tail-rtlFalse-narrowFalse-", StringComparison.Ordinal))
                        WriteRawCapture(prefix + name, pixels);
                    return pixels;
                }

                void WriteRawCapture(string name, byte[] pixels)
                {
                    var size = fixture.LastCaptureSize;
                    File.WriteAllBytes(Path.Combine(output, $"{name}-{size.Width}x{size.Height}.rgba"), pixels);
                }

                void CheckScenario(bool condition, string name) => Check(condition, prefix + name);
                byte[] CaptureTraced(string name, Action<UiImmediateContext> draw)
                {
                    var path = Path.Combine(output, prefix + name);
                    using var trace = File.CreateText(path + ".scissor.tsv");
                    trace.WriteLine("clipX\tclipY\tclipWidth\tclipHeight\ttranslationX\ttranslationY\tX\tY\tWidth\tHeight\tareaX\tareaY\tareaWidth\tareaHeight");
                    fixture.Renderer.ValidationScissor = (command, scissor, area) =>
                    {
                        var clip = command.ClipRect;
                        var offset = command.Translation;
                        trace.WriteLine(FormattableString.Invariant(
                            $"{clip.X:R}\t{clip.Y:R}\t{clip.Width:R}\t{clip.Height:R}\t{offset.X:R}\t{offset.Y:R}\t{scissor.Offset.X}\t{scissor.Offset.Y}\t{scissor.Extent.Width}\t{scissor.Extent.Height}\t{area.Offset.X}\t{area.Offset.Y}\t{area.Extent.Width}\t{area.Extent.Height}"));
                    };
                    try
                    {
                        var pixels = Capture(name, draw);
                        WriteRawCapture(prefix + name, pixels);
                        return pixels;
                    }
                    finally
                    {
                        fixture.Renderer.ValidationScissor = null;
                    }
                }
                void CaptureHost(string name, Action<UiImmediateContext> draw) => CaptureTraced(name, draw);
                if (clipHostPixels)
                {
                    CaptureClipScenes(CaptureHost);
                    continue;
                }
                if (gridHostPixels)
                    CaptureGridHost("grid-clear", ui => ui.EnableRootViewportContentLayout(contentPadding : 0));
                if (!virtualList && !virtualGrid)
                {
                    var before = checks.Count;
                    ContainerContractRenderingTests.Run(Capture, CheckScenario);
                    containerChecks += checks.Count - before;
                }
                foreach (var vector in new[] { false, true })
                {
                    if (virtualList)
                        VirtualListRenderingTests.Run(Capture, CheckScenario, fixture.Context.State, vector,
                            mode == "virtual-list-host-pixels" ? CaptureHost : null, rightToLeft : mode == "virtual-list-rtl-pixels");
                    else if (virtualGrid)
                        VirtualGridRenderingTests.Run(gridHostPixels ? CaptureGridHost : Capture, CheckScenario, fixture.Context.State,
                            fixture.Context.GetStyleData().ScrollbarSize, vector, gridHostPixels ? CaptureHost : null);
                    else
                        ScrollViewRenderingTests.Run(Capture, CheckScenario, fixture.Context.State,
                            fixture.Context.GetStyleData().ScrollbarSize, vector);
                }
                if (gridHostPixels)
                    CaptureBorderScenes(CaptureTraced, (int)fixture.FrameInfo.DisplaySize.X, samples == 1 ? CheckScenario : null);
                Console.WriteLine($"PASS {samples}× MSAA / {scheme}");
            }
        }

        using var result = File.Create(Path.Combine(output, "result.json"));
        JsonSerializer.Serialize(result, new ContainerPixelResult(Environment.Version.ToString(), slots, captures.Count,
            virtualGrid ? checks.Count : containerChecks, virtualList || virtualGrid ? 0 : checks.Count - containerChecks,
            virtualList ? checks.Count : 0,
            checks.ToArray(), captures.ToArray()), ContainerPixelJson.Default.ContainerPixelResult);
    }

    private static void CaptureBorderScenes(Func<string, Action<UiImmediateContext>, byte[]> capture,
        int width, Action<bool, string>? check)
    {
        foreach (var vector in new[] { false, true })
        foreach (var clipped in new[] { false, true })
        foreach (var fractional in new[] { false, true })
        {
            var name = $"grid-border-vector{vector}-clipped{clipped}-fractional{fractional}";
            var pixels = capture(name, ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("grid-border", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try
                {
                    var context = new UiControlContext(ui);
                    var y = fractional ? 40.75f : 40f;
                    // Adjacent bordered cells at x20; isolated border at x68; fill-only controls at x116.
                    for (var column = 0; column < 3; column++)
                    {
                        var top = new UiRect(20 + 48 * column, y - 32f, 32, 32);
                        var bottom = top with { Y = y };
                        if (column != 1)
                            Paint(top, new(91, 92, 160), column < 2);
                        Paint(bottom, new(108, 134, 160), column < 2);
                    }

                    void Paint(UiRect bounds, UiColor fill, bool border)
                    {
                        using var clip = clipped ? context.Clip(bounds) : default;
                        context.Frame(bounds, fill, new(220, 210, 190), 0, border ? 1 : 0);
                    }
                }
                finally
                {
                    if (vector)
                        ui.EndLayer();
                }
            });
            if (check is not null && vector && clipped && fractional)
                CheckFractionalBorder(pixels, width, check);
        }
    }

    private static void CheckFractionalBorder(byte[] pixels, int width, Action<bool, string> check)
    {
        var border = (R: 220, G: 210, B: 190);
        var fill = (R: 108, G: 134, B: 160);
        var clear = (R: 0, G: 0, B: 0);
        // The isolated rectangle spans y=[40.75, 72.75). At the first interior pixel centre,
        // a one-pixel border covers 75% and fill covers 25%; the last centre has 75% border coverage.
        CheckRow(41, Blend(border, fill, .75f), 1, "leading border blends with fill");
        CheckRow(72, Blend(border, clear, .75f), 1, "trailing border keeps fractional coverage");
        CheckRow(40, clear, 0, "leading pixel outside clip stays clear");
        CheckRow(73, clear, 0, "trailing pixel outside clip stays clear");

        static (int R, int G, int B) Blend((int R, int G, int B) foreground, (int R, int G, int B) background, float coverage)
        {
            int Mix(int front, int back) => (int)MathF.Round(front * coverage + back * (1f - coverage));
            return (Mix(foreground.R, background.R), Mix(foreground.G, background.G), Mix(foreground.B, background.B));
        }

        void CheckRow(int y, (int R, int G, int B) expected, int tolerance, string label)
        {
            const int x = 83;
            var i = (y * width + x) * 4;
            check(Math.Abs(pixels[i] - expected.R) <= tolerance && Math.Abs(pixels[i + 1] - expected.G) <= tolerance
                && Math.Abs(pixels[i + 2] - expected.B) <= tolerance && pixels[i + 3] == 255,
                $"projected fractional clip: {label}; pixel ({x},{y}) actual {pixels[i]},{pixels[i + 1]},{pixels[i + 2]}, expected {expected.R},{expected.G},{expected.B}");
        }
    }

    private static void CaptureClipScenes(Action<string, Action<UiImmediateContext>> capture)
    {
        capture("clip-clear", ui => ui.EnableRootViewportContentLayout(contentPadding : 0));
        foreach (var edge in new[] { 20, 21 })
        foreach (var inset in new[] { .25f, 4.25f })
        foreach (var mesh in new[] { false, true })
        foreach (var horizontal in new[] { false, true })
        {
            var clip = new UiRect(horizontal ? edge : 20, horizontal ? 20 : edge, 88, 84);
            var bounds = horizontal ? clip with { X = clip.X - inset } : clip with { Y = clip.Y - inset };
            var name = FormattableString.Invariant($"clip-{edge}-{inset}-{(mesh ? "mesh" : "frame")}-{(horizontal ? "x" : "y")}");
            capture(name, ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var context = new UiControlContext(ui);
                using var clipping = context.Clip(clip);
                var fill = new UiColor(130, 80, 145);
                if (mesh)
                {
                    var topLeft = new UiVector2(bounds.X, bounds.Y);
                    var topRight = new UiVector2(bounds.X + bounds.Width, bounds.Y);
                    var bottomLeft = new UiVector2(bounds.X, bounds.Y + bounds.Height);
                    var bottomRight = new UiVector2(topRight.X, bottomLeft.Y);
                    context.Triangle(topLeft, topRight, bottomRight, fill);
                    context.Triangle(topLeft, bottomRight, bottomLeft, fill);
                }
                else
                {
                    context.Frame(bounds, fill, new(220, 210, 190), 0, 1);
                }
            });
        }
    }
}

internal sealed record ContainerPixelResult(string Runtime, int FrameSlots, int Captures,
    int ContainerChecks, int ScrollChecks, int VirtualListChecks, string[] PassedChecks, ContainerPixelCapture[] CaptureHashes);

internal sealed record ContainerPixelCapture(string Name, string Sha256);

[JsonSerializable(typeof(ContainerPixelResult))]
internal partial class ContainerPixelJson : JsonSerializerContext;
