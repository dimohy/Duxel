using System.Numerics;
using Duxel.Core;
using Duxel.Platform.Linux;
using Duxel.Vulkan;

internal static unsafe partial class Program
{
    private static void RunVectorReadback(string output, string latinPath, string cjkPath, UiFontAtlas atlas, Action<bool, string> check)
    {
        foreach (var flightCount in new[] { 2, 3 })
        {
            using var platform = new LinuxX11PlatformBackend(new(500, 240, "Duxel owned Linux vector readback fixture"));
            using var renderer = new VulkanRendererBackend(platform, new(flightCount, EnableVSync: false, MsaaSamples: 1,
                DiagnosticsLog: Console.WriteLine));
            renderer.CreateDeviceObjects();
            using var context = new UiContext(atlas, new(1), new(2));
            var backend = (LinuxPlatformTextBackend)platform.TextBackend!;
            context.SetPlatformTextBackend(backend);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextFallbackEnabled(false);
            var started = false;
            for (var profile = 0; profile < 3; profile++)
            {
                var font = profile == 2 ? backend.GetFontVariantResource(latinPath, PlatformFontSimulation.Oblique) : latinPath;
                var secondary = profile == 2 ? backend.GetFontVariantResource(cjkPath, PlatformFontSimulation.Oblique) : cjkPath;
                var size = profile == 0 ? 36 : 64;
                var content = profile == 0 ? "O8B 정" : profile == 1 ? "한글 fi e\u0301" : "O8B 정세일";
                var matrix = profile == 1 ? Matrix4x4.CreateRotationZ(0.03f) : Matrix4x4.Identity;
                var origin = new UiVector2(12, 20);
                context.SetDirectTextPrimaryFontPath(font);
                context.SetDirectTextSecondaryFontPath(secondary);
                context.SetDirectTextBaseFontSize(size);
                var mesh = backend.CreateTextMesh(new(font, secondary, content, size));

                byte[] Capture(bool reference)
                {
                    platform.PollEvents();
                    context.NewFrame(new(1f / 60, new(500, 240), new(1, 1)),
                        new(new(-1, -1), false, false, false, false, false, false, 0, 0, [], [], default, default),
                        new(0, 0, 500, 240), UiTextSettings.Default);
                    if (!started)
                    {
                        context.QueueTextureUpdate(atlas.CreateTextureUpdate(new(1)));
                        context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(2), UiTextureFormat.Rgba8Unorm, 1, 1,
                            new byte[] { 255, 255, 255, 255 }));
                        started = true;
                    }
                    context.Render(new Screen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        ui.GetBackgroundDrawList().AddRectFilled(new(0, 0, 500, 240), new(0, 0, 0), new(2));
                        ui.BeginLayer("linux-vector-readback", new(false, 1, default)
                        {
                            Scene3D = new(matrix, false)
                        });
                        try
                        {
                            ui.SetCursorScreenPos(origin);
                            if (!reference)
                                ui.TextColored(new(255, 255, 255), content);
                            else
                            {
                                var draw = ui.GetWindowDrawList();
                                foreach (var glyph in mesh.Glyphs.Span)
                                {
                                    var indices = glyph.Indices.Span;
                                    UiVector2 At(uint index)
                                    {
                                        var point = glyph.Vertices.Span[(int)index];
                                        return new(origin.X + (point.X + glyph.Offset.X), origin.Y + (point.Y + glyph.Offset.Y));
                                    }
                                    for (var i = 0; i < indices.Length; i += 3)
                                        draw.AddTriangleFilled(At(indices[i]), At(indices[i + 1]), At(indices[i + 2]),
                                            new(255, 255, 255), new(2), new(0, 0, 500, 240));
                                }
                            }
                        }
                        finally { ui.EndLayer(); }
                    }));
                    using var data = context.GetDrawData();
                    return renderer.RenderAndReadback(data);
                }

                var actual = Capture(false);
                var expected = Capture(true);
                var filled = 0;
                for (var i = 0; i < actual.Length; i += 4)
                    if (actual[i] > 200 && actual[i + 1] > 200 && actual[i + 2] > 200)
                        filled++;
                check(actual.Length == 500 * 240 * 4 && filled > 20, $"Linux GPU readback contains visible vector text, flight={flightCount}, profile={profile}");
                WritePpm(Path.Combine(output, $"gpu-{flightCount}-{profile}-actual.ppm"), actual, 500, 240);
                WritePpm(Path.Combine(output, $"gpu-{flightCount}-{profile}-reference.ppm"), expected, 500, 240);
                File.WriteAllBytes(Path.Combine(output, $"gpu-{flightCount}-{profile}-actual.rgba"), actual);
                File.WriteAllBytes(Path.Combine(output, $"gpu-{flightCount}-{profile}-reference.rgba"), expected);
                check(actual.AsSpan().SequenceEqual(expected), $"Linux GPU indexed text exactly matches explicit reference triangles, flight={flightCount}, profile={profile}");
                if (flightCount == 3)
                    check(File.ReadAllBytes(Path.Combine(output, $"gpu-2-{profile}-actual.ppm")).AsSpan()
                        .SequenceEqual(File.ReadAllBytes(Path.Combine(output, $"gpu-3-{profile}-actual.ppm"))),
                        $"Linux vector pixels are identical across two and three frames in flight, profile={profile}");
            }
        }
    }

    private static void WritePpm(string file, byte[] rgba, int width, int height)
    {
        using var stream = File.Create(file);
        stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
        for (var i = 0; i < rgba.Length; i += 4)
            stream.Write(rgba.AsSpan(i, 3));
    }
}
