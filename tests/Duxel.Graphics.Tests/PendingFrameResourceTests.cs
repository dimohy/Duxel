using System.Buffers;
using Duxel.Core;

internal static class PendingFrameResourceTests
{
    private static readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    private static readonly UiFrameInfo Frame = new(1f / 60, new(320, 240), new(1, 1));

    internal static void Run(Action<bool, string> check)
    {
        foreach (var boundary in new[] { "Render", "NewFrame", "EndFrame" })
        {
            using var context = CreateContext();
            var owner = new CountedPixels();
            Begin(context);
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(90), UiTextureFormat.Rgba8Unorm, 1, 1, owner.Memory)
            {
                PixelOwner = owner
            });
            context.Render(new Screen(static _ => { }));
            if (boundary == "NewFrame")
                Begin(context);
            else if (boundary == "EndFrame")
            {
                context.EndFrame();
                Begin(context);
            }
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Update, new(90), UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 0, 0, 255, 255 }));
            context.Render(new Screen(static _ => { }));
            var data = context.GetDrawData();
            check(owner.Disposals == 0, boundary + " replacement retains unpublished pixel storage until final frame disposal");
            check(data.TextureUpdates.Count == 2 && data.TextureUpdates[0].Kind == UiTextureUpdateKind.Create && data.TextureUpdates[1].Kind == UiTextureUpdateKind.Update,
                boundary + " replacement preserves texture creation before a subsequent update");
            data.Dispose();
            check(owner.Disposals == 1, boundary + " replacement releases transferred pixel ownership exactly once");
        }

        using (var context = CreateContext())
        {
            var owner = new CountedPixels();
            Begin(context);
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(91), UiTextureFormat.Rgba8Unorm, 1, 1, owner.Memory)
            {
                PixelOwner = owner
            });
            context.Render(new Screen(static _ => { }));
            Begin(context);
            context.Dispose();
            check(owner.Disposals == 1, "disposing the context releases unpublished carried pixel storage exactly once");
        }

        using (var context = CreateContext())
        {
            Begin(context);
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(92), UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 0, 0, 255 }));
            context.Render(new Screen(static _ => { }));
            using var claimed = context.GetDrawData();
            Begin(context);
            context.Render(new Screen(static _ => { }));
            using var next = context.GetDrawData();
            check(claimed.TextureUpdates.Count == 1 && next.TextureUpdates.Count == 0, "claimed frame remains exclusively owned by its consumer and is never replayed");
        }

        using (var context = CreateContext())
        {
            var first = new CountedPixels();
            var second = new CountedPixels();
            Begin(context);
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(93), UiTextureFormat.Rgba8Unorm, 1, 1, first.Memory)
            {
                PixelOwner = first
            });
            context.Render(new Screen(static _ => { }));
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, new(93), UiTextureFormat.Rgba8Unorm, 1, 1, ReadOnlyMemory<byte>.Empty));
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(93), UiTextureFormat.Rgba8Unorm, 1, 1, second.Memory)
            {
                PixelOwner = second
            });
            Begin(context);
            context.Render(new Screen(static _ => { }));
            var data = context.GetDrawData();
            check(data.TextureUpdates.Count == 3 && data.TextureUpdates[0].Kind == UiTextureUpdateKind.Create
                && data.TextureUpdates[1].Kind == UiTextureUpdateKind.Destroy && data.TextureUpdates[2].Kind == UiTextureUpdateKind.Create,
                "carried creation remains before destroy and recreation queued ahead of NewFrame");
            data.Dispose();
            check(first.Disposals == 1 && second.Disposals == 1, "destroy and recreation preserve both exclusive pixel lifetimes");
        }

        using (var context = CreateContext())
        {
            var owner = new CountedPixels();
            Begin(context);
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(94), UiTextureFormat.Rgba8Unorm, 1, 1, owner.Memory)
            {
                PixelOwner = owner
            });
            context.Render(new Screen(static _ => { }));
            Begin(context);
            context.ClearRendererHandlers();
            context.Render(new Screen(static _ => { }));
            using var data = context.GetDrawData();
            check(owner.Disposals == 1 && data.TextureUpdates.Count == 0,
                "explicit renderer cleanup releases carried pixel storage and leaves no pending resource command");
        }
    }

    internal static void RunGpu(Action<bool, string> check)
    {
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, false);
        using var expectedImage = Fluent.Image(new(new(1, 1, new byte[] { 228, 42, 17, 255 })));
        using var actualImage = Fluent.Image(new(new(1, 1, new byte[] { 228, 42, 17, 255 })));
        expectedImage.Size = actualImage.Size = new(64, 64);
        void Draw(UiImmediateContext ui, UiWidget image)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, 320, 240), new(11, 33, 71), ui.WhiteTextureId);
            ui.SetCursorScreenPos(new(20, 20));
            image.Render(ui);
        }
        var expected = reference.Capture(ui => Draw(ui, expectedImage));
        var sample = (40 * 320 + 40) * 4;
        check(expected.Length == 320 * 240 * 4 && Math.Abs(expected[sample] - 228) <= 1
            && Math.Abs(expected[sample + 1] - 42) <= 1 && Math.Abs(expected[sample + 2] - 17) <= 1,
            "direct GPU reference contains the opaque source image rather than an empty background");
        Begin(candidate.Context);
        candidate.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(2), UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 }));
        candidate.Context.Render(new Screen(ui => Draw(ui, actualImage)));
        Begin(candidate.Context);
        candidate.Context.Render(new Screen(ui => Draw(ui, actualImage)));
        using var data = candidate.Context.GetDrawData();
        var hasWhite = data.TextureUpdates.Any(update => update.TextureId == new UiTextureId(2) && update.Kind == UiTextureUpdateKind.Create);
        var hasImage = data.TextureUpdates.Any(update => update.TextureId != new UiTextureId(2) && update.Kind == UiTextureUpdateKind.Create);
        check(hasWhite && hasImage, "unpublished Image frame retains both white and cached image initialization for GPU submission");
        if (!hasWhite || !hasImage)
        {
            check(false, "GPU comparison cannot submit the candidate because its required texture creation was discarded");
            return;
        }
        var actual = candidate.Render(data);
        check(actual.AsSpan().SequenceEqual(expected), "one final GPU submission after a replaced Image frame matches the directly rendered reference pixels"
            + " / expected SHA256=" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expected))
            + "; actual SHA256=" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual)));
    }

    private static UiContext CreateContext() => new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
    private static void Begin(UiContext context) => context.NewFrame(Frame, Idle, new(0, 0, 320, 240), UiTextSettings.Default);
    private sealed class CountedPixels : IMemoryOwner<byte>
    {
        private readonly byte[] _pixels = [255, 0, 0, 255];
        internal int Disposals;
        public Memory<byte> Memory => _pixels;
        public void Dispose() => Disposals++;
    }
}
