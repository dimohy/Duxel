using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class ProjectedGlyphMaterialTests
{
    internal static void Run(string output, Action<bool, string> check,
        IPlatformVectorTextBackend backend, PlatformTextMeshRequest request, int slots, string? probeShaders = null)
    {
        Directory.CreateDirectory(output);
        var source = UiVectorGlyphSource.Create(backend.CreateTextMesh(request with { OutlineOnly = true }), 1);
        var mesh = backend.CreateTextMesh(request);
        var scene = source.Scene
            ?? throw new InvalidOperationException("Expected a drawable glyph with an enclosed counter.");
        var texture = new UiTextureId(2_300_000_023);
        var ink = new UiColor(245, 32, 20);
        var back = new UiColor(20, 72, 235);
        var transcript = new List<string> { "samples\ttexture_order_changed_pixels\ttriangle_order_changed_pixels\ttexture_vs_triangle_pixels\tclip_leak_pixels\tvisible_ink_pixels\tbackground_inside_glyph_bounds_pixels" };
        var probeTranscript = new List<string> { "samples\torder_changed_pixels\toutline_vs_triangle_pixels\tclip_leak_pixels\tvisible_ink_pixels\tbackground_inside_glyph_bounds_pixels" };
        var cameraTranscript = new List<string> { "samples\tcamera\torder_changed_pixels\toutline_vs_triangle_pixels\tinterior_difference_pixels\tmax_red_delta\tvisible_ink_pixels" };
        foreach (var samples in new[] { 1, 4 })
        {
            using var fixture = new PresentationReuseTests.Fixture(slots, false, msaaSamples: samples);
            var textureWidth = (int)MathF.Ceiling(scene.Size.X);
            var textureHeight = (int)MathF.Ceiling(scene.Size.Y);
            var start = new UiVector2(40.25f, 32.75f);
            const float scale = 2.5f;
            var end = new UiVector2(start.X + textureWidth * scale, start.Y + textureHeight * scale);
            var origin = new UiVector2(start.X - source.Bounds.X * scale, start.Y - source.Bounds.Y * scale);
            var clip = new UiRect(45, 38, 71, 48);
            using var probe = probeShaders is null ? null
                : fixture.Renderer.CreateProjectedGlyphProbe(scene, start, scale, ink,
                    File.ReadAllBytes(Path.Combine(probeShaders, "projected_glyph.vert.spv")),
                    File.ReadAllBytes(Path.Combine(probeShaders, "projected_glyph.frag.spv")));
            var probeDraw = probe is null ? null : new VulkanRendererBackend.ProjectedGlyphDraw(probe, clip);
            var (clippedPoints, clippedIndices) = probe is null ? (Array.Empty<UiVector2>(), Array.Empty<uint>())
                : ClipTriangleMesh(mesh.Glyphs.Span, origin, scale, clip);
            var background = new UiRect(16, 16, 260, 200);
            var frontProjection = Matrix4x4.CreateTranslation(0, 0, .2f);
            var backProjection = Matrix4x4.CreateTranslation(0, 0, .7f);
            var camera = Matrix4x4.Identity;
            byte[] Capture(string name, bool reverse, bool upload, bool glyph, bool triangles = false, bool outline = false,
                bool clipTrianglesInGeometry = false)
            {
                var pixels = fixture.Capture(ui =>
                {
                    if (upload)
                        fixture.Context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture,
                            UiTextureFormat.Rgba8Unorm, textureWidth, textureHeight, ReadOnlyMemory<byte>.Empty)
                        {
                            VectorScene = scene,
                            IsColorImage = true
                        });
                    if (reverse && glyph)
                        Front();
                    ui.BeginLayer("glyph-material-background", new(false, 1, default) { Scene3D = new(backProjection) });
                    ui.GetWindowDrawList().AddRectFilled(background, back, ui.WhiteTextureId);
                    ui.EndLayer();
                    if (!reverse && glyph)
                        Front();

                    void Front()
                    {
                        ui.BeginLayer("glyph-material-foreground", new(false, 1, default) { Scene3D = new(camera * frontProjection) });
                        var draw = ui.GetWindowDrawList();
                        if (outline)
                            draw.AddCallback(static (_, _) => throw new InvalidOperationException("Projected glyph command was not recorded."), probeDraw);
                        else if (clipTrianglesInGeometry)
                            draw.AppendVectorGlyph(clippedPoints, clippedIndices, default, default, 1, ink, ui.WhiteTextureId, draw.GetClipRect());
                        else if (triangles)
                        {
                            foreach (var item in mesh.Glyphs.Span)
                                draw.AppendVectorGlyph(item.Vertices.Span, item.Indices.Span, item.Offset, origin,
                                    scale, ink, ui.WhiteTextureId, clip);
                        }
                        else
                            draw.AddImage(texture, start, end, default, new(1, 1), ink, clip);
                        ui.EndLayer();
                    }
                });
                fixture.WriteCapturePng(Path.Combine(output, $"{samples}-{name}.png"), pixels);
                return pixels;
            }
            var backgroundOnly = Capture("background", false, false, false);
            var ordered = Capture("background-first", false, true, true);
            var reversed = Capture("glyph-first", true, false, true);
            var triangleOrdered = Capture("triangle-background-first", false, false, true, triangles: true);
            var triangleReversed = Capture("triangle-glyph-first", true, false, true, triangles: true);
            var outlineOrdered = probe is null ? null : Capture("outline-background-first", false, false, true, outline: true);
            var outlineReversed = probe is null ? null : Capture("outline-glyph-first", true, false, true, outline: true);
            var width = fixture.LastCaptureSize.Width;
            var height = fixture.LastCaptureSize.Height;
            var orderChanges = 0;
            var triangleOrderChanges = 0;
            var textureVsTriangles = 0;
            var leak = 0;
            var visibleInk = 0;
            var backgroundInside = 0;
            var probeOrderChanges = 0;
            var probeVsTriangles = 0;
            var probeLeak = 0;
            var probeInk = 0;
            var probeBackground = 0;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                var backgroundPixel = backgroundOnly.AsSpan(offset, 4);
                var forwardPixel = ordered.AsSpan(offset, 4);
                var reversePixel = reversed.AsSpan(offset, 4);
                var trianglePixel = triangleOrdered.AsSpan(offset, 4);
                var triangleReversePixel = triangleReversed.AsSpan(offset, 4);
                var clipContains = x >= clip.X && x < clip.X + clip.Width && y >= clip.Y && y < clip.Y + clip.Height;
                var differs = !forwardPixel.SequenceEqual(backgroundPixel);
                if (!forwardPixel.SequenceEqual(reversePixel))
                    orderChanges++;
                if (!trianglePixel.SequenceEqual(triangleReversePixel))
                    triangleOrderChanges++;
                if (!forwardPixel.SequenceEqual(trianglePixel))
                    textureVsTriangles++;
                if (!clipContains && (differs || !reversePixel.SequenceEqual(backgroundPixel)
                    || !trianglePixel.SequenceEqual(backgroundPixel) || !triangleReversePixel.SequenceEqual(backgroundPixel)))
                    leak++;
                if (clipContains && differs)
                    visibleInk++;
                if (clipContains && x >= start.X && x < end.X && y >= start.Y && y < end.Y
                    && !differs && reversePixel.SequenceEqual(backgroundPixel))
                    backgroundInside++;
                if (outlineOrdered is not null && outlineReversed is not null)
                {
                    var forwardOutline = outlineOrdered.AsSpan(offset, 4);
                    var reverseOutline = outlineReversed.AsSpan(offset, 4);
                    var hasInk = !forwardOutline.SequenceEqual(backgroundPixel);
                    if (!forwardOutline.SequenceEqual(reverseOutline)) probeOrderChanges++;
                    if (!forwardOutline.SequenceEqual(trianglePixel)) probeVsTriangles++;
                    if (!clipContains && hasInk) probeLeak++;
                    if (clipContains && hasInk) probeInk++;
                    if (clipContains && !hasInk && x > 67 && x < 83 && y > 55 && y < 75) probeBackground++;
                }
            }
            transcript.Add($"{samples}\t{orderChanges}\t{triangleOrderChanges}\t{textureVsTriangles}\t{leak}\t{visibleInk}\t{backgroundInside}");
            check(leak == 0 && visibleInk > 0 && backgroundInside > 0 && triangleOrderChanges == 0,
                $"Projected glyph depth fixture has visible ink, visible background, a bounded clip and an order-stable triangle control at {samples}× MSAA");
            if (probe is not null)
            {
                probeTranscript.Add($"{samples}\t{probeOrderChanges}\t{probeVsTriangles}\t{probeLeak}\t{probeInk}\t{probeBackground}");
                check(probeOrderChanges == 0 && probeLeak == 0 && probeInk > 0 && probeBackground > 0 && probeVsTriangles <= 10,
                    $"Direct outline depth follows analytic glyph coverage and the local clip at {samples}× MSAA");
                var anchor = new Vector3(-.65f, -.57f, 0);
                var perspective = Matrix4x4.Identity;
                perspective.M14 = .22f;
                foreach (var (name, motion) in new[]
                {
                    ("zoom-out", Matrix4x4.CreateScale(.18f, .18f, 1)),
                    ("zoom-in", Matrix4x4.CreateScale(2.1f, 2.1f, 1)),
                    ("perspective", Matrix4x4.CreateRotationZ(.18f) * perspective),
                })
                {
                    camera = Matrix4x4.CreateTranslation(-anchor) * motion * Matrix4x4.CreateTranslation(anchor);
                    var forward = Capture($"outline-{name}-background-first", false, false, true, outline: true);
                    var reverse = Capture($"outline-{name}-glyph-first", true, false, true, outline: true);
                    // Rasterize a geometrically clipped reference. The ordinary
                    // depth triangle shader clips at fragment frequency.
                    var triangle = Capture($"triangle-{name}", false, false, true, clipTrianglesInGeometry: true);
                    var orderChanged = CountChangedPixels(forward, reverse);
                    var (changed, interior, redDelta) = CompareEdges(forward, triangle, width, height);
                    var inkPixels = CountChangedPixels(forward, backgroundOnly);
                    cameraTranscript.Add($"{samples}\t{name}\t{orderChanged}\t{changed}\t{interior}\t{redDelta}\t{inkPixels}");
                    // The 225-level red difference between ink and background
                    // makes two of four samples a delta of at most 113.
                    check(orderChanged == 0 && inkPixels > 0 && interior == 0 && (samples == 1 || redDelta <= 113),
                        $"Retained projected glyphs preserve depth and triangle interiors after {name} at {samples}× MSAA");
                }
                camera = Matrix4x4.Identity;
                var returned = Capture("outline-zoom-return", false, false, true, outline: true);
                check(returned.AsSpan().SequenceEqual(outlineOrdered),
                    $"Returning the camera reproduces the original pixels with the same outline buffer at {samples}× MSAA");
            }
        }
        File.WriteAllLines(Path.Combine(output, "depth.tsv"), transcript);
        if (probeShaders is not null)
        {
            File.WriteAllLines(Path.Combine(output, "outline-depth.tsv"), probeTranscript);
            File.WriteAllLines(Path.Combine(output, "outline-camera.tsv"), cameraTranscript);
        }
    }

    private static int CountChangedPixels(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var count = 0;
        for (var offset = 0; offset < first.Length; offset += 4)
            if (!first.Slice(offset, 4).SequenceEqual(second.Slice(offset, 4))) count++;
        return count;
    }

    private static (int Changed, int Interior, int MaxRedDelta) CompareEdges(ReadOnlySpan<byte> outline,
        ReadOnlySpan<byte> triangles, int width, int height)
    {
        var changed = 0;
        var interior = 0;
        var maxRedDelta = 0;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            var expected = triangles.Slice(offset, 4);
            if (outline.Slice(offset, 4).SequenceEqual(expected))
                continue;
            changed++;
            maxRedDelta = Math.Max(maxRedDelta, Math.Abs(outline[offset] - triangles[offset]));
            var interiorPixel = true;
            for (var row = Math.Max(0, y - 1); row <= Math.Min(height - 1, y + 1) && interiorPixel; row++)
            for (var column = Math.Max(0, x - 1); column <= Math.Min(width - 1, x + 1) && interiorPixel; column++)
                interiorPixel = triangles.Slice((row * width + column) * 4, 4).SequenceEqual(expected);
            if (interiorPixel)
                interior++;
        }
        return (changed, interior, maxRedDelta);
    }

    private static (UiVector2[] Points, uint[] Indices) ClipTriangleMesh(ReadOnlySpan<PlatformTextGlyphMesh> glyphs,
        UiVector2 origin, float scale, UiRect clip)
    {
        var points = new List<UiVector2>();
        var indices = new List<uint>();
        Span<UiVector2> polygon = stackalloc UiVector2[8];
        Span<UiVector2> scratch = stackalloc UiVector2[8];
        Span<float> edges = [clip.X, clip.X + clip.Width, clip.Y, clip.Y + clip.Height];
        foreach (var glyph in glyphs)
        {
            var vertices = glyph.Vertices.Span;
            var triangles = glyph.Indices.Span;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                for (var vertex = 0; vertex < 3; vertex++)
                {
                    var local = vertices[(int)triangles[i + vertex]];
                    polygon[vertex] = new(origin.X + (local.X + glyph.Offset.X) * scale,
                        origin.Y + (local.Y + glyph.Offset.Y) * scale);
                }
                var count = 3;
                for (var edge = 0; edge < edges.Length && count > 0; edge++)
                {
                    var output = 0;
                    var previous = polygon[count - 1];
                    var previousDistance = SignedDistance(previous, edge, edges[edge]);
                    foreach (var current in polygon[..count])
                    {
                        var distance = SignedDistance(current, edge, edges[edge]);
                        if ((previousDistance < 0) != (distance < 0))
                        {
                            var t = previousDistance / (previousDistance - distance);
                            scratch[output++] = new(previous.X + t * (current.X - previous.X), previous.Y + t * (current.Y - previous.Y));
                        }
                        if (distance >= 0)
                            scratch[output++] = current;
                        previous = current;
                        previousDistance = distance;
                    }
                    scratch[..output].CopyTo(polygon);
                    count = output;
                }
                if (count < 3)
                    continue;
                var first = (uint)points.Count;
                foreach (var point in polygon[..count])
                    points.Add(point);
                for (uint vertex = 1; vertex + 1 < count; vertex++)
                {
                    indices.Add(first);
                    indices.Add(first + vertex);
                    indices.Add(first + vertex + 1);
                }
            }
        }
        return (points.ToArray(), indices.ToArray());
    }

    private static float SignedDistance(UiVector2 point, int edge, float coordinate)
        => edge switch
        {
            0 => point.X - coordinate,
            1 => coordinate - point.X,
            2 => point.Y - coordinate,
            _ => coordinate - point.Y,
        };
}
