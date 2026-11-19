using Duxel.Core;

internal static class HorizontalOpacityMaskTests
{
    internal static void Run(Action<bool, string> check)
    {
        var mask = new UiHorizontalOpacityMask(.25f, .25f);
        check(mask.Evaluate(0) == 0 && mask.Evaluate(.125f) == .5f && mask.Evaluate(.25f) == 1
            && mask.Evaluate(.5f) == 1 && mask.Evaluate(.875f) == .5f && mask.Evaluate(1) == 0,
            "horizontal opacity mask defines exact two-sided linear ramps and a unit plateau");
        check(new UiHorizontalOpacityMask(1, 0).Evaluate(.5f) == .5f
            && new UiHorizontalOpacityMask(0, 1).Evaluate(.5f) == .5f && default(UiHorizontalOpacityMask).Evaluate(0) == 1,
            "one-sided full-width ramps and the identity mask remain explicit");
        foreach (var invalid in new[] { new UiHorizontalOpacityMask(float.NaN, 0), new(-.1f, 0), new(.6f, .6f), new(0, float.PositiveInfinity) })
        {
            try { invalid.Validate(); check(false, "invalid mask fails before rendering : " + invalid); }
            catch (ArgumentOutOfRangeException) { check(true, "invalid mask fails before rendering : " + invalid); }
        }
        var bounds = new UiRect(0, 0, 128, 32);
        var builder = new UiDrawListBuilder(bounds);
        builder.AddBackdropComposite(new(3), new(4), bounds, mask, .6f);
        builder.AddImage(new(3), new(0, 0), new(16, 16), new(0, 0), new(1, 1), new(25, 50, 75));
        var lists = builder.Build();
        try
        {
            var list = lists.Single();
            check(list.Vertices.Count == 16 && list.Commands[0].ElementCount == 18,
                "two-sided fade uses exactly three interpolation quads without per-pixel geometry");
            check(list.Commands[0].WhiteBackdropTextureId == new UiTextureId(4) && list.Commands[0].Opacity == .6f
                && !list.Commands[^1].IsBackdropComposite && list.Commands[^1].Opacity == 1,
                "masked composition preserves source pairing and restores ordinary image material state");
            check(list.Vertices.Take(12).Select(vertex => vertex.Color.Rgba >> 24)
                .SequenceEqual(new uint[] { 0, 255, 255, 0, 255, 255, 255, 255, 255, 0, 0, 255 }),
                "only vertex alpha carries the exact ramp endpoints");
            check(list.Vertices.Skip(12).All(vertex => vertex.Color == new UiColor(25, 50, 75)),
                "ordinary image vertices retain their original uniform color");
        }
        finally { foreach (var list in lists) list.ReleasePooled(); }
        var reference = new UiDrawListBuilder(bounds);
        var identity = new UiDrawListBuilder(bounds);
        reference.AddBackdropComposite(new(3), new(4), bounds, .6f);
        identity.AddBackdropComposite(new(3), new(4), bounds, default(UiHorizontalOpacityMask), .6f);
        var before = reference.Build();
        var after = identity.Build();
        try
        {
            check(before.Single().Vertices.SequenceEqual(after.Single().Vertices)
                && before.Single().Indices.SequenceEqual(after.Single().Indices)
                && before.Single().Commands.SequenceEqual(after.Single().Commands),
                "identity mask preserves the existing composite geometry and commands exactly");
        }
        finally
        {
            foreach (var list in before) list.ReleasePooled();
            foreach (var list in after) list.ReleasePooled();
        }
    }
}
