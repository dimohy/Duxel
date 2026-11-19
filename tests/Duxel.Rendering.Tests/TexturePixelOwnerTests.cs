using System.Buffers;
using System.Runtime.InteropServices;
using Duxel.Core;

internal static class TexturePixelOwnerTests
{
    internal static void Run(Action<bool, string> check, IPlatformTextBackend backend, string fontPath)
    {
        var owner = new ObservedPixels(4);
        owner.Memory.Span.Fill(73);
        byte[] borrowed = [1, 2, 3, 4];
        UiTextureUpdate[] updates =
        [
            new(UiTextureUpdateKind.Create, new(20), UiTextureFormat.Rgba8Unorm, 1, 1, owner.Memory) { PixelOwner = owner },
            new(UiTextureUpdateKind.Create, new(21), UiTextureFormat.Rgba8Unorm, 1, 1, borrowed),
        ];
        var frame = new UiDrawData(new(32, 32), default, new(1, 1), 0, 0,
            UiPooledList<UiDrawList>.FromArray([], 0), UiPooledList<UiTextureUpdate>.RentAndCopy(updates));
        frame.CaptureTexturePayloads();
        borrowed.AsSpan().Fill(99);
        check(MemoryMarshal.TryGetArray(frame.TextureUpdates[0].RgbaPixels, out var segment)
            && ReferenceEquals(segment.Array, owner.Buffer) && owner.Disposals == 0,
            "exclusive pixels are retained without a second handoff copy or early return");
        check(frame.TextureUpdates[1].RgbaPixels.Span.SequenceEqual(new byte[] { 1, 2, 3, 4 }),
            "borrowed pixels remain isolated from producer mutation in a mixed owned frame");
        Task.Run(() =>
        {
            if (frame.TextureUpdates[0].RgbaPixels.Span[0] != 73) throw new InvalidOperationException("Early pixel return.");
            frame.Dispose();
        }).GetAwaiter().GetResult();
        frame.Dispose();
        check(owner.Disposals == 1, "consumer-thread frame disposal returns exclusive pixels exactly once");

        var replaced = new ObservedPixels(4);
        var replacement = new ObservedPixels(4);
        using (var context = Context())
        {
            context.QueueTextureUpdate(Update(replaced));
            context.QueueTextureUpdate(Update(replacement));
            context.QueueTextureUpdate(Update(replacement));
            check(replaced.Disposals == 1 && replacement.Disposals == 0,
                "coalesced texture updates return superseded owners and preserve the surviving owner");
            context.ClearRendererHandlers();
            check(replacement.Disposals == 1, "discarded queued updates return their pixel storage");
        }

        var rendered = new FakeTextBackend();
        var renderedContext = Context(rendered);
        Begin(renderedContext);
        renderedContext.Render(new TextScreen(measureOnly: false));
        var claimed = renderedContext.GetDrawData();
        check(claimed.TextureUpdates.Any(update => ReferenceEquals(update.PixelOwner, rendered.Last)),
            "raster storage transfers from the text cache to its texture-update frame");
        renderedContext.Dispose();
        check(rendered.Last!.Disposals == 0, "claimed frame pixels outlive their UI context");
        claimed.CaptureTexturePayloads();
        claimed.Dispose();
        check(rendered.Last.Disposals == 1, "claimed text frame returns storage after consumer completion");

        var unclaimed = new FakeTextBackend();
        using (var context = Context(unclaimed))
        {
            Begin(context);
            context.Render(new TextScreen(measureOnly: false));
            Begin(context);
            var pixels = unclaimed.Last!;
            check(pixels.Disposals == 0, "an unclaimed frame preserves its pending raster upload for the next frame");
            context.Render(new TextScreen(measureOnly: false));
            using (var next = context.GetDrawData())
                check(next.TextureUpdates.Any(update => ReferenceEquals(update.PixelOwner, pixels)),
                    "the next frame owns the original raster upload without a second rasterization");
            check(pixels.Disposals == 1, "consuming the carried upload returns the raster storage exactly once");
        }
        var measured = new FakeTextBackend();
        using (var context = Context(measured))
        {
            Begin(context);
            context.Render(new TextScreen(measureOnly: true));
            context.GetDrawData().Dispose();
            check(measured.Last!.Disposals == 0, "measurement-only raster storage stays owned by the cache");
            for (var i = 0; i < 640; i++)
            {
                Begin(context);
                context.Render(new EmptyScreen());
                context.GetDrawData().Dispose();
            }
            check(measured.Last.Disposals == 1, "stale measurement-only cache entries return their pixel storage");
        }
        var pending = new FakeTextBackend();
        using (var context = Context(pending))
        {
            Begin(context);
            context.Render(new TextScreen(measureOnly: true));
            context.GetDrawData().Dispose();
        }
        check(pending.Last!.Disposals == 1, "context disposal returns measurement-only pixel storage");

        var narrow = new FakeTextBackend(wide: false);
        using (var context = Context(narrow))
        {
            Begin(context);
            context.Render(new TextScreen(measureOnly: false));
            using var data = context.GetDrawData();
            data.CaptureTexturePayloads();
        }
        check(narrow.Last!.Disposals == 1, "atlas-eligible raster ownership is returned once after copying or frame consumption");

        var text = string.Join('\n', Enumerable.Repeat("Dynamic status: 1200 tables / 1000 FPS / vector layers", 24));
        check(backend.TryRasterize(new(fontPath, fontPath, text, 16), out var first)
            && first.RgbaPixels.Length > 85000 && first.PixelOwner is not null,
            "real multiline DirectWrite rasterization uses exclusive storage for a large bitmap");
        var expected = first.RgbaPixels.ToArray();
        MemoryMarshal.TryGetArray(first.RgbaPixels, out var firstArray);
        first.PixelOwner!.Dispose();
        check(backend.TryRasterize(new(fontPath, fontPath, text, 16), out var second)
            && second.RgbaPixels.Span.SequenceEqual(expected), "reused raster storage preserves every RGBA pixel");
        try
        {
            check(MemoryMarshal.TryGetArray(second.RgbaPixels, out var secondArray)
                && ReferenceEquals(firstArray.Array, secondArray.Array), "consecutive large multiline rasters reuse returned storage");
        }
        finally { second.PixelOwner?.Dispose(); }
    }

    private static UiTextureUpdate Update(ObservedPixels pixels)
        => new(UiTextureUpdateKind.Create, new(31), UiTextureFormat.Rgba8Unorm, 1, 1, pixels.Memory) { PixelOwner = pixels };
    private static UiContext Context(FakeTextBackend? backend = null)
    {
        var atlas = new UiFontAtlas(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
            new Dictionary<int, UiGlyphInfo>(), new Dictionary<uint, float>(), 18, 0, 0, -1);
        var context = new UiContext(atlas, new(1), new(2));
        if (backend is not null)
        {
            context.SetPlatformTextBackend(backend);
            context.SetDirectTextPrimaryFontPath("fixture");
            context.SetDirectTextEnabled(true);
        }
        return context;
    }
    private static void Begin(UiContext context) => context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
        new(default, false, false, false, false, false, false, 0, 0, [], [], default, default),
        new(0, 0, 320, 240), UiTextSettings.Default);
    private sealed class TextScreen(bool measureOnly) : UiScreen
    {
        public override void Render(UiImmediateContext ui)
        {
            if (measureOnly) ui.CalcTextSize("owned pixels");
            else ui.Text("owned pixels");
        }
    }
    private sealed class EmptyScreen : UiScreen { public override void Render(UiImmediateContext ui) { } }
    private sealed class FakeTextBackend(bool wide = true) : IPlatformTextBackend
    {
        internal ObservedPixels? Last;
        public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result)
        { result = new(2, 2, 1); return true; }
        public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result)
        {
            var width = wide ? 2048 : 2;
            Last = new(width * 2 * 4);
            Last.Memory.Span.Fill(255);
            result = new(width, 2, Last.Memory, 1, 1, width) { PixelOwner = Last };
            return true;
        }
    }
    private sealed class ObservedPixels(int size) : IMemoryOwner<byte>
    {
        internal readonly byte[] Buffer = new byte[size];
        internal int Disposals;
        public Memory<byte> Memory => Disposals == 0 ? Buffer : throw new ObjectDisposedException(nameof(ObservedPixels));
        public void Dispose() { Interlocked.Increment(ref Disposals); Buffer.AsSpan().Fill(0xCD); }
    }
}
