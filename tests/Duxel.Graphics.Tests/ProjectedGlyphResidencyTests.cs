using System.Numerics;
using Duxel.Core;

internal static class ProjectedGlyphResidencyTests
{
    internal static void Run(string output, IPlatformVectorTextBackend backend, string fontPath,
        Action<bool, string> check)
    {
        Directory.CreateDirectory(output);
        using var fixture = new PresentationReuseTests.Fixture(3, false, msaaSamples: 4, useProjectedGlyphText: true);
        var renderer = fixture.Renderer;
        if (!renderer.ProjectedGlyphTextEnabled)
            throw new InvalidOperationException("Set DUXEL_VK_PROJECTED_GLYPHS=1 on a standard 4× MSAA device.");

        byte[] Text(string value, long revision, float zoom = 1, float opacity = 1)
            => fixture.Capture(ui =>
            {
                var options = new UiLayerOptions(false, opacity, default)
                {
                    ContentVersion = revision,
                    FontAtlasIndependent = true,
                    Scene3D = new(Matrix4x4.CreateScale(zoom, zoom, 1) * Matrix4x4.CreateTranslation(0, 0, .2f))
                };
                if (ui.BeginLayer("test-projected-label", options))
                {
                    ui.SetCursorScreenPos(new(120, 96));
                    ui.TextColored(new(232, 59, 26), value);
                }
                ui.EndLayer();
            });

        var first = Text("한글 A09", 1);
        var uploaded = renderer.ProjectedGlyphDiagnostics;
        var zoomed = Text("한글 A09", 1, 1.75f);
        var returned = Text("한글 A09", 1);
        var reused = renderer.ProjectedGlyphDiagnostics;
        check(uploaded.Uploads > 0 && uploaded.Draws > 0 && reused.Uploads == uploaded.Uploads
            && reused.Draws > uploaded.Draws && !first.AsSpan().SequenceEqual(zoomed) && first.AsSpan().SequenceEqual(returned),
            "Normal projected text reuses resident outlines across zoom and reproduces its starting pixels");
        _ = Text("한글 B42", 2);
        var revised = renderer.ProjectedGlyphDiagnostics;
        check(revised.Uploads > reused.Uploads && revised.Bytes == reused.Bytes,
            "Changed text adds its new outline to the existing GPU page");
        _ = Text("한글 B42", 2, opacity: .5f);
        check(renderer.ProjectedGlyphDiagnostics.Draws == revised.Draws,
            "A retained layer changing opacity rebuilds into the supported triangle path");

        var triangleBuilds = 0;
        (int Indices, byte[] Pixels) ReadTriangles(float zoom, bool reference)
        {
            var transform = Matrix4x4.CreateScale(zoom, zoom, 1) * Matrix4x4.CreateTranslation(0, 0, .2f);
            if (reference)
                transform.M14 = 1e-12f;
            var id = reference ? "triangle-detail-reference" : "triangle-detail";
            using var data = fixture.Build(ui =>
            {
                var options = new UiLayerOptions(false, .5f, default)
                {
                    ContentVersion = 1,
                    FontAtlasIndependent = true,
                    Scene3D = new(transform)
                };
                if (ui.TryReplayLayer(id, options))
                    return;
                if (ui.BeginLayer(id, options))
                {
                    triangleBuilds++;
                    ui.SetCursorScreenPos(new(100, 94));
                    ui.TextColored(new(255, 255, 255), "한글 Orders Q3 · 109");
                }
                ui.EndLayer();
            });
            return (data.TotalIndexCount, renderer.RenderAndReadback(data));
        }
        var coarseTriangles = ReadTriangles(.19f, false);
        var mediumTriangles = ReadTriangles(.79f, false);
        var heldTriangles = ReadTriangles(.99f, false);
        var detailedTriangles = ReadTriangles(.99f, true);
        check(triangleBuilds == 3 && coarseTriangles.Indices < mediumTriangles.Indices
            && heldTriangles.Indices == mediumTriangles.Indices && mediumTriangles.Indices < detailedTriangles.Indices,
            "Mixed-script triangle text uses the medium detail band and replays it at nearby zooms");
        var referenceCoverage = 0;
        var coverageDifference = 0;
        for (var i = 0; i < detailedTriangles.Pixels.Length; i += 4)
        {
            referenceCoverage += detailedTriangles.Pixels[i];
            coverageDifference += Math.Abs(detailedTriangles.Pixels[i] - heldTriangles.Pixels[i]);
        }
        check(referenceCoverage > 0 && coverageDifference * 10 <= referenceCoverage,
            "Medium detail retains mixed-script glyph coverage near its maximum scale");

        var native = backend.CreateTextMesh(new(fontPath, null, "O", 24) { OutlineOnly = true });
        var glyph = UiVectorGlyphSource.Create(native, 1).Scene ?? throw new InvalidOperationException("Expected a glyph scene.");
        var origin = new UiVector2(132, 96);
        var bounds = new UiRect(origin.X, origin.Y, glyph.Size.X, glyph.Size.Y);
        UiGpuVectorScene LargeScene()
        {
            // More than half a 4 MiB page, so each new scene forces a new page
            // after the first. Padding is unreachable by the actual glyph.
            var data = new Vector4[150_000];
            glyph.Data.Span.CopyTo(data);
            return new(glyph.Size, data, glyph.CommandCount);
        }
        void Draw(UiImmediateContext ui, UiGpuVectorScene scene)
        {
            ui.BeginLayer("test-projected-cache-load", new(false, 1, default)
            {
                Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .2f))
            });
            var draw = ui.GetWindowDrawList();
            draw.AddProjectedGlyphs(new(scene, origin, 1, new(232, 59, 26)), bounds, draw.GetClipRect());
            ui.EndLayer();
        }
        var originalScene = LargeScene();
        var before = fixture.Capture(ui => Draw(ui, originalScene));
        for (var i = 0; i < 13; i++)
        {
            var scene = LargeScene();
            using var data = fixture.Build(ui => Draw(ui, scene));
            if (!renderer.TryRenderDrawData(data))
                throw new InvalidOperationException("Cache pressure frame was not presented.");
        }
        var pressure = renderer.ProjectedGlyphDiagnostics;
        var reloaded = fixture.Capture(ui => Draw(ui, originalScene));
        var after = renderer.ProjectedGlyphDiagnostics;
        check(pressure.Evictions > 0 && pressure.Bytes <= 48 * 1024 * 1024 && after.Uploads == pressure.Uploads + 1
            && before.AsSpan().SequenceEqual(reloaded), "Fence-retired outline pages stay bounded and an evicted glyph redraws correctly");
        fixture.WriteCapturePng(Path.Combine(output, "resident-text.png"), first);
        fixture.WriteCapturePng(Path.Combine(output, "reloaded.png"), reloaded);
        File.WriteAllLines(Path.Combine(output, "residency.txt"),
            [$"First: {uploaded}", $"Reused: {reused}", $"Revised: {revised}", $"Pressure: {pressure}", $"Reloaded: {after}"]);

        var beforeBatch = renderer.ProjectedGlyphBatchDiagnostics;
        var batch = fixture.Capture(ui =>
        {
            ui.BeginLayer("test-projected-batch", new(false, 1, default)
            {
                Scene3D = new(Matrix4x4.CreateRotationZ(.06f) * Matrix4x4.CreateTranslation(0, 0, .25f))
            });
            var draw = ui.GetWindowDrawList();
            var clip = new UiRect(112, 75, 68, 55);
            Add(new(117, 80), new(232, 59, 26));
            Add(new(126, 82), new(32, 196, 219));
            draw.AddRectFilled(new(138, 86, 4, 24), new(4, 12, 2), ui.WhiteTextureId, clip);
            Add(new(144, 84), new(247, 200, 68));
            Add(new(154, 84), new(80, 240, 44), clip with { Width = 46 });
            ui.EndLayer();

            void Add(UiVector2 position, UiColor color, UiRect? localClip = null)
                => draw.AddProjectedGlyphs(new(glyph, position, 1, color),
                    new(position.X, position.Y, glyph.Size.X, glyph.Size.Y), localClip ?? clip);
        });
        var afterBatch = renderer.ProjectedGlyphBatchDiagnostics;
        var expectedBatches = afterBatch.Enabled ? 1 : 0;
        check(afterBatch.Draws - beforeBatch.Draws == expectedBatches && afterBatch.Runs - beforeBatch.Runs == 2 * expectedBatches,
            "Only consecutive projected glyphs with matching clip are submitted together");
        fixture.WriteCapturePng(Path.Combine(output, "projected-batch.png"), batch);

        foreach (var (name, second, scope, expected) in new[]
                 {
                     ("disjoint", 155f, 0, 1),
                     ("overlap", 119f, 0, 2),
                     ("depth-scope", 155f, 1, 2),
                 })
        {
            var beforePlanes = renderer.ProjectedGlyphPlaneBatchDiagnostics;
            var planePixels = fixture.Capture(ui =>
            {
                Plane("first", 112, .25f, 0, new(232, 59, 26));
                Plane("second", second, .35f, scope, new(32, 196, 219));

                void Plane(string id, float x, float depth, int depthScope, UiColor ink)
                {
                    ui.BeginLayer(id, new(false, 1, default)
                    {
                        Scene3D = new(Matrix4x4.CreateTranslation(0, 0, depth)) { DepthScopeId = depthScope }
                    });
                    var draw = ui.GetWindowDrawList();
                    var planeBounds = new UiRect(x, 80, 30, 36);
                    var placement = new UiVector2(x + 3, 83);
                    draw.AddRectFilled(planeBounds, new(60, 50, 45), ui.WhiteTextureId);
                    draw.AddProjectedGlyphs(new(glyph, placement, 1, ink),
                        new(placement.X, placement.Y, glyph.Size.X, glyph.Size.Y), planeBounds);
                    ui.EndLayer();
                }
            });
            var afterPlanes = renderer.ProjectedGlyphPlaneBatchDiagnostics;
            check(afterPlanes.Draws - beforePlanes.Draws == (afterPlanes.Enabled ? expected : 0)
                && afterPlanes.Runs - beforePlanes.Runs == (afterPlanes.Enabled ? 2 : 0),
                $"Plane batching respects the {name} case");
            fixture.WriteCapturePng(Path.Combine(output, $"planes-{name}.png"), planePixels);
        }
    }
}
