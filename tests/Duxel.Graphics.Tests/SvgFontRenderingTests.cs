using Duxel.Core;
using Duxel.Svg;
using System.Security.Cryptography;

internal static class SvgFontRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int width, string output, int samples)
    {
        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        var source = Path.Combine(output, "gpu-font-" + samples + ".svg");
        const string document = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='80' font-family='UI'><text x='8' y='52' font-size='36' fill='#e03020'>ABC</text></svg>";
        File.WriteAllText(source, document);
        using var store = new SvgStore(fonts : new("UI", [new("UI", fontPath)]));
        using var text = new UiSvg(store.Load(source));
        check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fontPath))) == "704EADA12961A27FD271F7D3B09E2066520E758390C1776744EFF430FEE30F6E",
            "SVG GPU font matches the preserved independent native-outline fixture");
        using var referenceStore = new SvgStore();
        var reference = referenceStore.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SvgFontOutlineReference.svg"));
        var rgba = reference.Rasterize(200, 80).Pixels.ToArray();
        var expected = new UiImageTexture(new((nuint)(80_020_000 + samples)), 200, 80, rgba);
        var queued = default(UiSvgTextureStatistics);
        void Draw(UiImmediateContext ui)
        {
            var draw = ui.GetWindowDrawList();
            draw.AddRectFilled(new(16, 16, 200, 80), new(20, 50, 80), ui.WhiteTextureId);
            draw.AddRectFilled(new(16, 116, 200, 80), new(20, 50, 80), ui.WhiteTextureId);
            ui.SetCursorScreenPos(new(16, 16));
            text.Render(ui);
            expected.Prepare(ui, UiImageEffects.Default);
            ui.SetCursorScreenPos(new(16, 116));
            ui.Image(expected.TextureId, new(200, 80));
            queued = ui.SvgTextureStatistics;
        }
        var first = capture("text-reference", Draw);
        var opaque = true;
        var exterior = true;
        for (var y = 0; y < 80; y++)
            for (var x = 0; x < 200; x++)
            {
                var actualIndex = ((16 + y) * width + 16 + x) * 4;
                var expectedIndex = ((116 + y) * width + 16 + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                    opaque &= Math.Abs(first[actualIndex + channel] - first[expectedIndex + channel]) <= 1;
            }
        foreach (var point in new[] { new UiVector2(18, 18), new UiVector2(210, 90) })
        {
            var index = ((int)point.Y * width + (int)point.X) * 4;
            exterior &= first[index] == 20 && first[index + 1] == 50 && first[index + 2] == 80;
        }
        check(opaque && exterior, "SVG text GPU full pixels match the independent native outline with the same explicit font and backdrop at MSAA " + samples);
        var fontWork = store.FontStatistics;
        var uploads = queued.GpuRenderRequests;
        for (var replay = 0; replay < 3; replay++)
        {
            var pixels = capture("text-replay-" + replay, Draw);
            check(pixels.AsSpan().SequenceEqual(first) && store.FontStatistics == fontWork && queued.GpuRenderRequests == uploads,
                "SVG text GPU replay reuses pixels, fonts and texture upload at MSAA " + samples + " / " + replay);
        }
        text.Zoom = 1.5f;
        capture("text-enlarged", Draw);
        check(store.Statistics is { XmlParses : 1, SceneCompiles : 1, Rasterizations : 0 } &&
            store.FontStatistics == fontWork && queued.GpuRenderRequests == uploads + 1,
            "SVG text GPU enlargement rasterizes its retained scene without XML or font resolution at MSAA " + samples);
        text.Zoom = 1;
        var returned = capture("text-returned", Draw);
        check(returned.AsSpan().SequenceEqual(first) && queued.GpuRenderRequests == uploads + 1,
            "SVG text GPU zoom return reuses its exact cached original at MSAA " + samples);
    }
}
