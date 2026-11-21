using Duxel.Core;

internal static class PaintGroupTests
{
    internal static void Run(Action<bool, string> check)
    {
        var bounds = new UiRect(0, 0, 80, 80);
        var builder = new UiDrawListBuilder(bounds);
        builder.AddRectFilled(new(0, 0, 40, 40), new(255, 0, 0), new(2));
        var lists = builder.Build();
        var content = new UiPaintGroupContent(bounds, new(1, 1), lists);
        foreach (var list in lists)
            list.ReleasePooled();
        var original = content.DrawLists[0].Commands[0];
        builder._ResetForNewFrame();
        builder.AddRectFilled(new(0, 0, 40, 40), new(0, 255, 0), new(2));
        check(content.DrawLists[0].Commands[0] == original && content.DrawLists[0].RectFilledPrimitives![0].Color == new UiColor(255, 0, 0), "paint content copies source geometry before a producer can reuse buffers");
        var lease = content.Acquire();
        var frame = new UiDrawData(new(80, 80), default, new(1, 1), 0, 0,
            UiPooledList<UiDrawList>.RentAndCopy(Array.Empty<UiDrawList>()),
            UiPooledList<UiTextureUpdate>.RentAndCopy(new UiTextureUpdate[]
            {
                new(UiTextureUpdateKind.RenderPaintGroup, new(3), UiTextureFormat.Rgba8Unorm, 80, 80, ReadOnlyMemory<byte>.Empty)
                { PaintGroup = lease, WhitePaintTexture = new(4) }
            }));
        var scaled = frame.ScaleClipRects(new(1, 1));
        content.Dispose();
        frame.Dispose();
        check(scaled.TextureUpdates[0].PaintGroup!.Content.DrawLists[0].Commands[0] == original,
            "a scaled queued frame owns a separate geometry lease after source frame and producer disposal");
        scaled.Dispose();
        var rejected = false;
        try
        {
            using var dead = content.Acquire();
        }
        catch (ObjectDisposedException) { rejected = true; }
        check(rejected, "returned pooled paint storage cannot be resurrected after its last lease");
        var captureBuilder = new UiDrawListBuilder(bounds);
        captureBuilder.AddRectFilled(new(0, 0, 40, 40), new(0, 255, 0), new(2));
        var captured = captureBuilder.Build();
        var capturedRectangles = captured[0].RectFilledPrimitives;
        using var transferred = UiPaintGroupContent.ConsumeCapture(bounds, new(1, 1), captured, UiPaintGroupTextMode.PhysicalLcd);
        using var transferredLease = transferred.Acquire();
        transferred.Dispose();
        captureBuilder._ResetForNewFrame();
        captureBuilder.AddRectFilled(new(0, 0, 20, 20), new(0, 0, 255), new(2));
        check(ReferenceEquals(transferredLease.Content.DrawLists[0].RectFilledPrimitives, capturedRectangles)
            && capturedRectangles![0].Color == new UiColor(0, 255, 0),
            "internal capture transfers buffer ownership and keeps queued geometry independent of the builder");
        captureBuilder.DiscardDeferred();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var group = new UiPaintGroup();
        var callbacks = 0;
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        UiDrawData Paint(ulong version, float x = 0, float opacity = .8f)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.PaintGroup(group, new(x, 0, 80, 80), version, paint =>
                {
                    callbacks++;
                    paint.Frame(new(x, 0, 80, 80), new(255, 0, 0), default, 0, 0);
                }, opacity);
            }));
            return context.GetDrawData();
        }
        using var first = Paint(1);
        using var moved = Paint(1, 20, .4f);
        check(callbacks == 1 && first.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup) == 1
            && moved.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup) == 0,
            "paint version cache skips callbacks and render requests when only placement and group opacity change");
        using var changed = Paint(2, 20);
        check(callbacks == 2 && changed.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup) == 1,
            "explicit content-version changes capture exactly once");
        check(requests == 0 && context.State.NextScheduledFrameTimeSeconds is null,
            "paint capture and cached compositing do not schedule periodic frames");
        check(first.TextureUpdates.Single(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup).PaintGroup!.Content.Bounds.X == 0,
            "already-queued paint content remains at its original immutable source coordinates after newer captures");
        rejected = false;
        try
        {
            using var oversized = new UiPaintGroupContent(new(0, 0, 5000, 5000), new(1, 1), Array.Empty<UiDrawList>());
        }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "paint target budget is checked before copying geometry or allocating GPU storage");
        var sourceScale = 0f;
        var sourceMode = UiPaintGroupTextMode.PhysicalLcd;
        using var projected = new UiPaintGroup();
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 320, 240), UiTextSettings.Default);
        var restoredScale = 0f;
        context.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var before = ui.ContentScale;
            ui.PaintGroup(projected, bounds, 1, paint => sourceScale = ui.ContentScale,
                rasterScale: .875f, textMode: UiPaintGroupTextMode.Projected);
            restoredScale = ui.ContentScale / before;
        }));
        using var projectedFrame = context.GetDrawData();
        var projectedContent = projectedFrame.TextureUpdates.Single(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup).PaintGroup!.Content;
        sourceMode = projectedContent.TextMode;
        check(projectedContent.PixelWidth == 70 && projectedContent.PixelHeight == 70 && sourceScale == .875f,
            "projected capture rasterizes at final physical resolution rather than scaling a full-resolution image twice");
        check(sourceMode == UiPaintGroupTextMode.Projected && restoredScale == 1,
            "projected text mode is immutable snapshot metadata and capture DPI is restored for subsequent controls");
        var invalidLists = builder.Build();
        try
        {
            rejected = false;
            try
            {
                using var invalid = new UiPaintGroupContent(bounds, new(1, 1),
                new[] { invalidLists[0] with { Scene3D = new(System.Numerics.Matrix4x4.Identity) } });
            }
            catch (NotSupportedException) { rejected = true; }
            check(rejected, "paint sources reject three-dimensional scenes before geometry allocation");
            invalidLists[0].Commands.AsWritableSpan()[0] = invalidLists[0].Commands[0] with
            {
                Callback = static (_, _) => { }
            };
            rejected = false;
            try
            {
                using var invalid = new UiPaintGroupContent(bounds, new(1, 1), invalidLists);
            }
            catch (NotSupportedException) { rejected = true; }
            check(rejected, "external draw callbacks cannot be silently skipped or executed twice in capture");
        }
        finally { foreach (var list in invalidLists) list.ReleasePooled(); }
        var budgetCallbacks = 0;
        rejected = false;
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            try
            {
                ui.PaintGroup(group, new(0, 0, 5000, 5000), 3, _ => budgetCallbacks++);
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
        }));
        using var invalidFrame = context.GetDrawData();
        check(rejected && budgetCallbacks == 0, "public capture rejects oversized targets before invoking the paint callback");
        using var sameFrame = new UiPaintGroup();
        var definitionCallbacks = 0;
        var changedInFrame = false;
        context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.PaintGroup(sameFrame, bounds, 1, _ => definitionCallbacks++);
            try
            {
                ui.PaintGroup(sameFrame, bounds, 2, _ => definitionCallbacks++);
            }
            catch (InvalidOperationException) { changedInFrame = true; }
            sameFrame.Dispose();
        }));
        using var disposedInsideFrame = context.GetDrawData();
        var sourceUpdate = disposedInsideFrame.TextureUpdates.Single(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup);
        check(changedInFrame && definitionCallbacks == 1,
            "same-frame definition changes fail explicitly before overwriting an earlier paint operation");
        check(!disposedInsideFrame.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy
            && (update.TextureId == sourceUpdate.TextureId || update.TextureId == sourceUpdate.WhitePaintTexture)),
            "disposing a paint owner before frame sealing does not destroy the same frame's output");


    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
