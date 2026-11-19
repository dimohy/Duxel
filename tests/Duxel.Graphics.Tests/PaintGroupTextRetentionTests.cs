using Duxel.Core;

internal static class PaintGroupTextRetentionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var bounds = new UiRect(0, 0, 160, 48);
        UiDrawData Frame(Action<UiImmediateContext>? draw = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw?.Invoke(ui); }));
            return context.GetDrawData();
        }
        using (var plain = new UiPaintGroup())
        using (var first = Frame(ui =>
        {
            ui.PaintGroup(plain, bounds, 1, paint => paint.Frame(bounds, new(255, 0, 0), default, 0, 0));
            check(ui.IsTextureRetained(ui.WhiteTextureId), "paint group texture retention works without a layer cache or any text");
        })) { }
        var callbacks = 0;
        var text = "Retained text";
        void PaintText(UiControlContext paint)
        {
            callbacks++;
            using var style = paint.BeginTypography<UiRegularTypeface, UiFluentType300>();
            paint.Text(text, new(2, 2), new(255, 255, 255));
        }
        using var firstOwner = new UiPaintGroup();
        using var secondOwner = new UiPaintGroup();
        UiTextureId source;
        using (var first = Frame(ui =>
        {
            ui.PaintGroup(firstOwner, bounds, 1, PaintText, rasterScale: .875f, textMode: UiPaintGroupTextMode.Projected);
            ui.PaintGroup(secondOwner, bounds, 1, PaintText, rasterScale: .875f, textMode: UiPaintGroupTextMode.Projected);
            check(ui.DirectTextCacheOccupancy.Retained == 1, "two paint owners share one retained text key");
        }))
        {
            var updates = first.TextureUpdates.Where(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup).ToArray();
            source = updates[0].PaintGroup!.Content.TextureDependencies.Single();
            check(updates.Length == 2 && updates[1].PaintGroup!.Content.TextureDependencies.Single() == source,
                "two paint captures share the actual source texture");
        }
        var destroyedWhileOwned = false;
        for (var i = 0; i < 605; i++)
        {
            using var aged = Frame();
            destroyedWhileOwned |= aged.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy && update.TextureId == source);
        }
        check(!destroyedWhileOwned && callbacks == 2, "cached paint text survives beyond600 idle frames without rerasterization or recapture");
        firstOwner.Dispose();
        using (var oneOwner = Frame(ui => check(ui.DirectTextCacheOccupancy.Retained == 1,
            "disposing one paint owner preserves the second owner's text reference")))
            check(!oneOwner.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy && update.TextureId == source),
                "the shared text texture stays live after one owner is disposed");
        text = "Replacement text";
        UiTextureId replacement;
        using (var changed = Frame(ui => ui.PaintGroup(secondOwner, bounds, 2, PaintText,
            rasterScale: .875f, textMode: UiPaintGroupTextMode.Projected)))
            replacement = changed.TextureUpdates.Single(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup)
                .PaintGroup!.Content.TextureDependencies.Single();
        check(replacement != source && callbacks == 3, "paint version replacement captures exactly one new text source");
        using (var releasedOld = Frame(ui => check(ui.DirectTextCacheOccupancy.Retained == 1,
            "paint version replacement releases old keys while retaining new keys")))
            check(releasedOld.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy && update.TextureId == source)
                && !releasedOld.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy && update.TextureId == replacement),
                "replaced stale text is reclaimed while its current replacement stays live");
        secondOwner.Dispose();
        var replacementDestroyed = false;
        for (var i = 0; i < 605; i++)
        {
            using var aged = Frame();
            replacementDestroyed |= aged.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.Destroy && update.TextureId == replacement);
        }
        using (var empty = Frame(ui => check(ui.DirectTextCacheOccupancy.Retained == 0 && ui.DirectTextCacheOccupancy.Entries == 0,
            "last paint owner disposal releases the retention fast path and permits complete text cache cleanup"))) { }
        check(replacementDestroyed, "current paint text is reclaimed after the last owner releases it");

        using var child = new UiPaintGroup();
        using var parent = new UiPaintGroup();
        using (var childFrame = Frame(ui => ui.PaintGroup(child, bounds, 1, PaintText))) { }
        using (var parentFrame = Frame(ui =>
        {
            ui.PaintGroup(parent, bounds, 1, paint => paint.PaintGroup(child, bounds, 1, PaintText));
            check(ui.DirectTextCacheOccupancy.Retained == 1, "a parent captures retained text dependencies from an already cached child group");
        })) { }
        parent.Dispose();
        using (var parentReleased = Frame(ui => check(ui.DirectTextCacheOccupancy.Retained == 1,
            "parent paint disposal does not drop the child's independent text retention"))) { }
        child.Dispose();
        using (var childReleased = Frame(ui =>
        {
            check(ui.DirectTextCacheOccupancy.Retained == 0, "nested paint retention is released after both owners dispose");
            check(context.State.NextScheduledFrameTimeSeconds is null, "text source ownership schedules no periodic frames");
        })) { }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
