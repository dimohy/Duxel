using Duxel.Core;

internal static class SharedParagraphTextureTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiDrawData Frame(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(draw));
            return context.GetDrawData();
        }

        var reference = new byte[] { 10, 20, 30, 20, 40, 50, 60, 50 };
        var other = (byte[])reference.Clone();
        other[2]++;
        var ownerA = new Owner(reference);
        var ownerB = new Owner(reference);
        UiTextureId a = default, b = default, c = default, d = default;
        using (var data = Frame(ui =>
        {
            a = ui.ProbeSharedMask("a", 2, 1, 0, ownerA);
            b = ui.ProbeSharedMask("b", 2, 1, 55, ownerB);
            c = ui.ProbeSharedMask("c", 2, 1, 2, new Owner(other));
            d = ui.ProbeSharedMask("d", 1, 2, 3, new Owner(reference));
        }))
        {
            check(a == b && a != c && a != d, "only byte-identical images with equal dimensions share a texture");
            check(ownerA.Disposed && ownerB.Disposed, "both source buffers are returned");
            var updates = data.TextureUpdates.Where(update => update.TextureId == a).ToArray();
            check(updates.Length == 1 && updates[0].Kind == UiTextureUpdateKind.Create, "aliases queue one creation");
            check(updates[0].RgbaPixels.Span.SequenceEqual(reference), "upload snapshot survives pooled source disposal");
        }
        using (var data = Frame(ui => ui.ProbeRemoveSharedMask("a")))
            check(!data.TextureUpdates.Any(update => update.TextureId == a), "retiring first alias preserves the shared texture");
        using (var data = Frame(ui => ui.ProbeRemoveSharedMask("b")))
            check(data.TextureUpdates.Count(update => update.TextureId == a && update.Kind == UiTextureUpdateKind.Destroy) == 1,
                "retiring final alias queues one destruction");
        using (var data = Frame(ui => b = ui.ProbeSharedMask("e", 2, 1, 3, new Owner(reference))))
            check(b != a && data.TextureUpdates.Any(update => update.TextureId == b && update.Kind == UiTextureUpdateKind.Create),
                "recreating the pixels uses a fresh texture ID");
    }

    private sealed class Owner(byte[] source) : System.Buffers.IMemoryOwner<byte>
    {
        private readonly byte[] _buffer = (byte[])source.Clone();
        public Memory<byte> Memory => _buffer;
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            if (Disposed)
                throw new InvalidOperationException("Source disposed twice.");
            Disposed = true;
            _buffer.AsSpan().Fill(255);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> paint) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => paint(ui);
    }
}
