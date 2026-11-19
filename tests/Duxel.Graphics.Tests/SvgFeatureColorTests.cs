using Duxel.Core;
using Duxel.Svg;
using System.Security.Cryptography;
using System.Text.Json;

internal static class SvgFeatureColorTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        GpuVectorRingContractTests.Run(check);
        VectorSceneShaderContractTests.Run(check);
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "features.svg");
        var referenceRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SvgFeaturesBrowser");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(referenceRoot, "reference.json")));
        var reference = File.ReadAllBytes(Path.Combine(referenceRoot, "reference.rgba"));
        if (manifest.RootElement.GetProperty("width").GetInt32() != 320 || manifest.RootElement.GetProperty("height").GetInt32() != 160
            || reference.Length != 320 * 160 * 4
            || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))) != manifest.RootElement.GetProperty("sourceSha256").GetString()
            || Convert.ToHexString(SHA256.HashData(reference)) != manifest.RootElement.GetProperty("rgbaSha256").GetString())
            throw new InvalidDataException("SVG feature browser reference does not match the original source or pixels.");
        using var store = new SvgStore();
        var asset = store.Load(sourcePath);
        check(asset.GpuScene.Strokes.Count == 1,
            "SVG feature colors / rounded primitive borders share analytic coverage while the dashed arrow retains its GPU stroke program");
        using var widget = new UiSvg(asset) { Size = new(320, 160) };
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), new(41, 41, 41), ui.WhiteTextureId);
            ui.SetCursorScreenPos(default);
            widget.Render(ui);
        }
        using var data = fixture.Build(Draw);
        var pixels = fixture.Render(data);
        var width = (int)fixture.FrameInfo.DisplaySize.X;
        BrowserPng.Write(Path.Combine(output, "features-gpu.png"), width, (int)fixture.FrameInfo.DisplaySize.Y, pixels);
        File.WriteAllBytes(Path.Combine(output, "features-gpu.rgba"), pixels);
        bool Pixel(int x, int y, byte r, byte g, byte b)
        {
            var at = (y * width + x) * 4;
            return pixels[at] == r && pixels[at + 1] == g && pixels[at + 2] == b;
        }
        check(Pixel(20, 20, 54, 197, 240) && Pixel(28, 20, 20, 80, 107),
            "SVG feature colors / rounded rectangle stroke preserves both authored stripe colors inside the border");
        var gradient = (64 * width + 126) * 4;
        check(!(pixels[gradient] == 179 && pixels[gradient + 1] == 217 && pixels[gradient + 2] == 255),
            "SVG feature colors / radial gradient interior is not overwritten by the pale-blue border paint");
        check(Pixel(128, 64, pixels[gradient], pixels[gradient + 1], pixels[gradient + 2]) == false,
            "SVG feature colors / radial gradient varies across its unobstructed interior");
        check(Pixel(156, 64, 255, 255, 255) && Pixel(260, 64, 41, 41, 41) && Pixel(225, 64, 54, 197, 240),
            "SVG feature colors / visible symbol and luminance-mask cutout preserve their authored colors");
        foreach (var (x, y) in new[] { (20, 20), (28, 20), (68, 90), (76, 90), (123, 64), (128, 64), (156, 100), (178, 103) })
            check(pixels.AsSpan((y * width + x) * 4, 4).SequenceEqual(reference.AsSpan((y * 320 + x) * 4, 4)),
                $"SVG feature colors / independently captured browser interior pixel matches exactly at {x},{y}");
        var differing = 0;
        var maximum = 0;
        for (var y = 0; y < 160; y++)
            for (var x = 0; x < 320; x++)
            {
                var error = 0;
                for (var c = 0; c < 4; c++)
                    error = Math.Max(error, Math.Abs(pixels[(y * width + x) * 4 + c] - reference[(y * 320 + x) * 4 + c]));
                if (error != 0)
                    differing++;
                maximum = Math.Max(maximum, error);
            }
        File.WriteAllText(Path.Combine(output, "browser-comparison.json"),
            $"{{\"ComparedPixels\":51200,\"DifferingPixels\":{differing},\"MaximumChannelDifference\":{maximum},\"NativeWidth\":{width},\"FullPixelEquivalenceClaimed\":false}}");
    }
}
