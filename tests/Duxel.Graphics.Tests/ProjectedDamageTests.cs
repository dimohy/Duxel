using System.Numerics;
using Duxel.Core;

internal static class ProjectedDamageTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        if (Environment.GetEnvironmentVariable("DUXEL_VK_PROJECTED_DAMAGE") != "1")
            throw new InvalidOperationException("Run this probe with DUXEL_VK_PROJECTED_DAMAGE=1.");
        var frames = new List<string> { "samples\tframe\tzoom\tpartial_area\tfull_area\tequal" };
        var glyphTexture = new UiTextureId(2_300_000_030);
        foreach (var samples in new[] { 1, 4 })
        {
            using var control = new PresentationReuseTests.Fixture(3, false, msaaSamples: samples);
            using var candidate = new PresentationReuseTests.Fixture(3, true, msaaSamples: samples);
            var equal = true;
            var moveRepairs = 0;
            var cameraRepair = 0f;
            for (var frame = 0; frame < 24; frame++)
            {
                var zoom = frame < 16 ? .95f : 1.2f;
                var x = 3 * (frame % 6);
                var bytes = control.Capture(Draw);
                var actual = candidate.Capture(Draw);
                var same = bytes.AsSpan().SequenceEqual(actual);
                equal &= same;
                var size = candidate.LastCaptureSize;
                var repair = candidate.Renderer.LastFrameMetrics.RepaintBounds;
                var area = repair.Width * repair.Height;
                var full = (float)(size.Width * size.Height);
                if (frame is > 4 and < 16 && area > 0 && area < .4f * full)
                    moveRepairs++;
                if (frame == 16)
                    cameraRepair = area / full;
                frames.Add(FormattableString.Invariant($"{samples}\t{frame}\t{zoom}\t{area}\t{full}\t{same}"));
                if (!same)
                {
                    control.WriteCapturePng(Path.Combine(output, $"{samples}-{frame}-full.png"), bytes);
                    candidate.WriteCapturePng(Path.Combine(output, $"{samples}-{frame}-repair.png"), actual);
                }

                void Draw(UiImmediateContext ui)
                {
                    if (ui.IsPresentationPass && frame is 0 or 10)
                        ui.QueueTextureUpdate(new(frame == 0 ? UiTextureUpdateKind.Create : UiTextureUpdateKind.Update,
                            glyphTexture, UiTextureFormat.Rgba8Unorm, 1, 1,
                            frame == 0 ? new byte[] { 244, 190, 80, 255 } : new byte[] { 76, 204, 150, 255 }));
                    var camera = Matrix4x4.CreateScale(zoom, zoom, 1) * Matrix4x4.CreateTranslation(0, 0, .55f);
                    var scene = new UiLayer3D(camera) { Viewport = new(12.25f, 18.5f, 292, 190), PlaneModel = new(default) };
                    Plane("depth-background", new(8, 12, 300, 202), new(25, 44, 120), scene);
                    Plane("stationary-left", new(35, 47, 65, 90), new(50, 155, 210),
                        scene with { Transform = camera * Matrix4x4.CreateTranslation(0, 0, -.12f) });
                    Plane("moving-table", new(72, 64, 78, 62), new(215, 78, 48),
                        scene with { PlaneModel = new(new(x, x * .25f), -.25f) });
                    Plane("stationary-front", new(112, 78, 34, 75), new(65, 180, 89),
                        scene with { PlaneModel = new(default, -.32f) }, frame < 8 ? 1 : .8f);
                    if (ui.BeginLayer("retained-texture", new(false, 1, default)
                        { Scene3D = scene with { PlaneModel = new(default, -.36f) }, ContentVersion = 1 }))
                        ui.GetWindowDrawList().AddImage(glyphTexture, new(143, 85), new(166, 111), default, new(1, 1), new(255, 255, 255));
                    ui.EndLayer();
                    if (ui.BeginLayer("moving-connector", new(false, 1, default)
                        { Scene3D = scene with { PlaneModel = new(default, -.18f), WriteDepth = false }, ContentVersion = x }))
                    {
                        ui.GetWindowDrawList().AddRectFilled(new(145 + x, 96, 88 - x, 3), new(233, 191, 70), ui.WhiteTextureId);
                    }
                    ui.EndLayer();
                    var miniMap = new UiLayer3D(Matrix4x4.CreateScale(.24f, .24f, 1) * Matrix4x4.CreateTranslation(0, 0, .5f))
                    {
                        Viewport = new(244.5f, 33.25f, 56, 57),
                        PlaneModel = new(default)
                    };
                    Plane("map-background", new(146, -66, 250, 250), new(43, 70, 100), miniMap);
                    Plane("map-moving-table", new(223, 37, 56, 58), new(220, 85, 52),
                        miniMap with { PlaneModel = new(new(x, 0), -.15f) });
                    ui.GetWindowDrawList().AddRectFilled(new(9, 6, 54, 7), new(240, 239, 235), ui.WhiteTextureId);

                    void Plane(string id, UiRect rect, UiColor color, UiLayer3D projection, float opacity = 1)
                    {
                        if (ui.BeginLayer(id, new(false, opacity, default) { Scene3D = projection, ContentVersion = 1 }))
                            ui.GetWindowDrawList().AddRectFilled(rect, color, ui.WhiteTextureId);
                        ui.EndLayer();
                    }
                }
            }
            check(equal, $"Projected partial damage matches 24 full Vulkan frames at {samples}× MSAA");
            check(moveRepairs >= 8, $"Projected fixed-camera moves repair less than 40% of the frame at {samples}× MSAA");
            check(cameraRepair == 1, $"Zoom changes invalidate the projected image history at {samples}× MSAA");
        }
        File.WriteAllLines(Path.Combine(output, "projected-damage.tsv"), frames);
    }
}
