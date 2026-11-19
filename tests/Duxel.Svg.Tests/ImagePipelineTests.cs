using Duxel.Core;

internal static class ImagePipelineTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var input = new byte[] { 224, 48, 32, 0, 32, 128, 224, 128 };
        var image = new UiImageTexture(new(2_200_000_001), 2, 1, input);
        input[0] = 0;
        UiTextureUpdate Frame(UiImageEffects effects)
        {
            context.NewFrame(new(1f / 60, new(320, 200), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 200), UiTextSettings.Default);
            context.Render(new Screen(ui => image.Prepare(ui, effects)));
            using var draw = context.GetDrawData();
            return draw.TextureUpdates.Single();
        }
        var first = Frame(UiImageEffects.Default);
        check(first.IsColorImage && first.RgbaPixels.Span.SequenceEqual(new byte[] { 224, 48, 32, 0, 32, 128, 224, 128 }),
            "ordinary image preparation preserves source alpha and copied ownership with explicit color sampling");
        var inverted = Frame(UiImageEffects.Default with { Invert = true });
        check(inverted.RgbaPixels.Span[0] == 31 && inverted.RgbaPixels.Span[3] == 0 && inverted.RgbaPixels.Span[7] == 128,
            "ordinary image RGB effects preserve transparent and partial-alpha coverage");
        check(first.RgbaPixels.Span[0] == 224 && first.RgbaPixels.Span[3] == 0,
            "changing ordinary image effects does not mutate pixels held by an earlier queued frame");
        var restored = Frame(UiImageEffects.Default);
        check(restored.RgbaPixels.Span.SequenceEqual(first.RgbaPixels.Span),
            "returning to default image effects restores immutable original RGBA without accumulated processing");
        context.NewFrame(new(1f / 60, new(320, 200), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 320, 200), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            ui.QueueTextureUpdate(first with { TextureId = new(2_200_000_009) });
            ui.QueueTextureUpdate(first with { TextureId = new(2_200_000_009), IsColorImage = false });
        }));
        using var merged = context.GetDrawData();
        check(merged.TextureUpdates.Single().IsColorImage,
            "whole-image update coalescing preserves an earlier explicit color-image classification");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
