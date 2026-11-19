using System.Text.Json;
using Duxel.Core;

internal static class GpuBoundaryHardwareTests
{
    internal static GpuSvgBrowserReference LoadReference() =>
        GpuSvgBrowserTests.LoadReferences("RootTransforms").Single(reference => reference.Id == "baked.image-canvas");

    internal static void Run(string output, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, Func<UiRendererFrameMetrics> metrics, GpuSvgBrowserReference reference,
        int width, int height, UiVector2 replayOffset)
    {
        var viewport = new UiRect(0, 0, width, height);
        using var stream = File.Create(Path.Combine(output, "hardware-comparison.json"));
        using var writer = new Utf8JsonWriter(stream, new() { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("scope", "Native Vulkan triangle rasterization at the frozen baked device coordinates; no SVG compute rendering");
        writer.WriteNumber("width", width);
        writer.WriteNumber("height", height);
        writer.WriteNumber("comparisonWidth", reference.Width);
        writer.WriteNumber("comparisonHeight", reference.Height);
        writer.WriteStartArray("replayOffset");
        writer.WriteNumberValue(replayOffset.X);
        writer.WriteNumberValue(replayOffset.Y);
        writer.WriteEndArray();
        writer.WriteStartArray("backgrounds");
        foreach (var light in new[] { false, true })
        {
            var background = light ? new UiColor(255, 255, 255) : new UiColor(0, 0, 0);
            var name = light ? "hardware-white" : "hardware-black";
            var pixels = capture(name, ui =>
            {
                var list = ui.GetWindowDrawList();
                list.PushClipRect(viewport, false);
                list.AddRectFilled(viewport, background, ui.WhiteTextureId);
                list.AddTriangleFilled(new(335.5761413574219f - replayOffset.X, 348.185546875f - replayOffset.Y),
                    new(313.6149597167969f - replayOffset.X, 303.0760803222656f - replayOffset.Y),
                    new(215.08641052246094f - replayOffset.X, 311.6825256347656f - replayOffset.Y), new(33, 33, 33), ui.WhiteTextureId);
                list.PopClipRect();
            });
            check(pixels.Length == width * height * 4 && metrics().MsaaSamples == 4,
                "Native boundary / full fixed viewport and actual 4x MSAA : " + name);
            var outside = (100 * width + 100) * 4;
            var inside = ((320 - (int)replayOffset.Y) * width + 290 - (int)replayOffset.X) * 4;
            check(pixels[outside] == (light ? 255 : 0) && pixels[inside] == 33,
                "Native boundary / known exterior and interior colors : " + name);
            File.WriteAllBytes(Path.Combine(output, name + ".rgba"), pixels);
            var critical = ((323 - (int)replayOffset.Y) * width + 323 - (int)replayOffset.X) * 4;
            writer.WriteStartObject();
            writer.WriteString("background", name);
            writer.WriteNumber("msaaSamples", metrics().MsaaSamples);
            writer.WriteStartArray("criticalRgba");
            foreach (var value in pixels.AsSpan(critical, 4))
                writer.WriteNumberValue(value);
            writer.WriteEndArray();
            if (light)
            {
                var expected = reference.Expected.Span;
                var different = 0;
                var maximum = 0;
                for (var y = 0; y < reference.Height; y++)
                for (var x = 0; x < reference.Width; x++)
                {
                    var localX = x - (int)replayOffset.X;
                    var localY = y - (int)replayOffset.Y;
                    var i = (localY * width + localX) * 4;
                    var expectedIndex = (y * reference.Width + x) * 4;
                    var error = 0;
                    for (var channel = 0; channel < 4; channel++)
                    {
                        // Replay restores the untouched white area above and left of this triangle's bounds.
                        var actual = localX < 0 || localY < 0 ? (byte)255 : pixels[i + channel];
                        error = Math.Max(error, Math.Abs(actual - expected[expectedIndex + channel]));
                    }
                    if (error != 0)
                        different++;
                    maximum = Math.Max(maximum, error);
                }
                writer.WriteNumber("differentPixels", different);
                writer.WriteNumber("maximumChannelError", maximum);
                writer.WriteBoolean("exactBrowserMatch", different == 0);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
