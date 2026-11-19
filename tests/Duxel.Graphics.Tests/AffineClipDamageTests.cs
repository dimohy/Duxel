using Duxel.Core;

internal static class AffineClipDamageTests
{
    internal static void Run(Func<UiDrawData, byte[]> render, Func<UiRendererFrameMetrics> metrics,
        Action<bool, string> check, UiTextureId white, int width, int height)
    {
        static UiPooledList<T> Items<T>(params T[] values) => new(values, values.Length, false);
        var viewport = new UiRect(0, 0, width, height);
        var foreground = new UiRect(30, 30, 140, 140);
        int At(int x, int y) => (y * width + x) * 4;
        foreach (var kind in new[] { -3f, -4f, -5f })
        {
            byte[] Draw(float translation)
            {
                // Only the second record changes. Geometry, command and mask head stay identical.
                var mask = kind == -5 ? new UiRect(90, 90, 30, 0) : new UiRect(60, 60, 60, 60);
                using var data = new UiDrawData(new(width, height), default, new(1, 1), 0, 0,
                    Items(new UiDrawList(Items<UiDrawVertex>(), Items<uint>(), Items(
                        new UiDrawCommand(viewport, white, 0, 1, 0, Kind: UiDrawCommandKind.RectFilledPrimitives, Bounds: viewport, HasBounds: true),
                        new UiDrawCommand(viewport, white, 1, 1, 0, Kind: UiDrawCommandKind.RectFilledPrimitives, Bounds: foreground, HasBounds: true, CircleCutoutIndexPlusOne: 3)),
                        RectFilledPrimitives: Items(
                            new UiRectFilledPrimitive(viewport, new(0, 0, 0)),
                            new UiRectFilledPrimitive(foreground, new(255, 0, 0)),
                            new UiRectFilledPrimitive(mask, default, kind == -4 ? 10 : 0, kind),
                            new UiRectFilledPrimitive(new(1, 0, 0, 1), default, translation, 0)))), Items<UiTextureUpdate>());
                return render(data);
            }
            var initial = Draw(0);
            for (var frame = 0; frame < 12; frame++)
                Draw(0);
            check(metrics().DrawCalls == 0, "unchanged affine mask settles without drawing : " + kind);
            var changed = Draw(20);
            check(metrics().DrawCalls > 0 && metrics().RepaintBounds.Width <= 144 && metrics().RepaintBounds.Height <= 144,
                "matrix-only affine clip damage stays within affected command bounds : " + kind);
            var beforeRed = initial[At(110, 90)] == 255 && initial[At(50, 90)] == 0;
            var afterRed = changed[At(110, 90)] == 0 && changed[At(50, 90)] == 255;
            check(kind == -5 ? !beforeRed && !afterRed
                    && initial[At(110, 90)] == 0 && initial[At(50, 90)] == 255
                    && changed[At(110, 90)] == 255 && changed[At(50, 90)] == 0
                : beforeRed && afterRed,
                "matrix-only affine mask change repairs newly exposed and hidden pixels : " + kind);
            for (var frame = 0; frame < 12; frame++)
                check(changed.AsSpan().SequenceEqual(Draw(20)), "affine mask update survives acquired-image history : " + kind + "/" + frame);
            check(metrics().DrawCalls == 0, "updated affine mask returns to zero draws after all images settle : " + kind);
            check(initial.AsSpan().SequenceEqual(Draw(0)), "affine mask matrix round trip restores exact pixels : " + kind);
        }
    }
}
