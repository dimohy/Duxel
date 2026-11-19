using Duxel.Core;

internal static class RoundedClipTests
{
    internal static void Run(Action<bool, string> check)
    {
        var viewport = new UiRect(0, 0, 240, 160);
        var texture = new UiTextureId(1);
        var builder = new UiDrawListBuilder(viewport);
        var outer = new UiRect(12, 16, 120, 100);
        var inner = new UiRect(28, 20, 100, 80);
        using (builder.RoundedClip(outer, 24))
        {
            builder.AddRectFilled(viewport, new(255, 0, 0), texture);
            using (builder.RoundedClip(inner, 18))
            {
                builder.AddRectFilled(viewport, new(0, 255, 0), texture);
                builder.AddImageRounded(texture, viewport, UiImageUvTransform.Identity, new(255, 255, 255), 4, viewport);
            }
            builder.AddRectFilled(viewport, new(0, 0, 255), texture);
        }
        builder.AddRectFilled(viewport, new(255, 255, 255), texture);
        var sources = builder.Build();
        static List<UiRectFilledPrimitive> Chain(UiDrawList list, UiDrawCommand command)
        {
            var result = new List<UiRectFilledPrimitive>();
            var records = list.RectFilledPrimitives!;
            for (var index = (int)command.CircleCutoutIndexPlusOne - 1; index >= 0;)
            {
                var item = records[index]; result.Add(item);
                if (item.BorderThickness >= 0 || item.Color.Rgba == 0) break;
                var delta = unchecked((int)item.Color.Rgba);
                if (delta >= 0 || index + delta < 0) throw new InvalidOperationException("Invalid test mask link.");
                index += delta;
            }
            return result;
        }
        try
        {
            var source = sources.Single();
            var commands = source.Commands.Where(command => command.ElementCount != 0).ToArray();
            check(Chain(source, commands[0]).Single().Rect == outer && commands[0].ClipRect == outer,
                "rounded scope preserves actual curved bounds and narrows rectangular scissor");
            check(Chain(source, commands[1]).Count == 2 && Chain(source, commands[2]).Count == 2,
                "nested rounded scopes retain both ancestors on solid and paired image commands");
            check(Chain(source, commands[1]).Any(x => x.Rect == inner && x.CornerRadius == 18),
                "inner rounded mask retains its independent corner radius");
            check(Chain(source, commands[3]).Single().Rect == outer && commands[4].CircleCutoutIndexPlusOne == 0,
                "closing nested scopes restores parent and then unmasked command state");
            foreach (var kind in new[] { "copy", "append", "static", "retained" })
            {
                var target = new UiDrawListBuilder(viewport);
                target.AddRectFilled(new(0, 0, 1, 1), new(255, 0, 0), texture);
                var cached = source with { StaticGeometryKey = "rounded-clip-test" };
                using (target.RoundedClip(new(0, 0, 140, 140), 30))
                {
                    if (kind == "copy") target.AddDrawList(source);
                    else if (kind == "append") target.Append(source, .5f, new(8, 9));
                    else if (kind == "static") target.AppendStaticReference(cached);
                    else target.AppendRetainedStaticReference(cached);
                }
                var copies = target.Build();
                try
                {
                    var lists = copies.Where(x => x.Commands.Any(c => c.CircleCutoutIndexPlusOne != 0)).ToArray();
                    var all = lists.SelectMany(list => list.Commands.Where(c => c.CircleCutoutIndexPlusOne != 0)
                        .Select(c => (List: list, Command: c))).ToArray();
                    check(all.Length == 5 && all.All(x => Chain(x.List, x.Command).Any(r => r.CornerRadius == 30)),
                        "outer rounded mask wraps every copied or retained command without changing source : " + kind);
                    check(all.Any(x => Chain(x.List, x.Command).Count == 3),
                        "relative ancestor links survive mixed primitive rebasing : " + kind);
                    if (kind == "append")
                        check(all.All(x => Chain(x.List, x.Command).Any(r => r.CornerRadius == 30 && r.Rect.X == -8 && r.Rect.Y == -9)),
                            "outer rounded scope converts its bounds to translated command local coordinates");
                }
                finally { foreach (var list in copies) list.ReleasePooled(); }
            }
            check(source.Commands.Last().CircleCutoutIndexPlusOne == 0 && Chain(source, commands[1]).Count == 2,
                "outer wrapping leaves retained source geometry and masks immutable");
        }
        finally { foreach (var list in sources) list.ReleasePooled(); }
        var channels = new UiDrawListBuilder(viewport);
        channels.Split(2);
        using (channels.RoundedClip(outer, 24))
        {
            channels.AddRectFilled(viewport, new(255, 0, 0), texture);
            channels.SetCurrentChannel(1);
            using (channels.RoundedClip(inner, 18)) channels.AddRectFilled(viewport, new(0, 255, 0), texture);
            channels.Merge();
        }
        var merged = channels.Build();
        try { check(merged.SelectMany(list => list.Commands.Where(c => c.ElementCount != 0).Select(c => Chain(list, c).Count))
            .SequenceEqual(new[] { 1, 2 }), "channel merging keeps nested rounded ancestry without duplicating outer masks"); }
        finally { foreach (var list in merged) list.ReleasePooled(); }
        var repeated = new UiDrawListBuilder(viewport);
        using (repeated.RoundedClip(outer, 24))
        using (repeated.RoundedClip(outer, 24))
        {
            for (var index = 0; index < 32; index++) repeated.AddRectFilled(new(32, 32, 2, 2), new(255, 0, 0), texture);
        }
        var shared = repeated.Build();
        try { check(shared.Sum(list => list.RectFilledPrimitives!.Count) == 33,
            "identical nested clip scopes share one auxiliary record across32primitives"); }
        finally { foreach (var list in shared) list.ReleasePooled(); }
        var invalid = new UiDrawListBuilder(viewport);
        foreach (var radius in new[] { -1f, float.NaN, float.PositiveInfinity })
        {
            var rejected = false;
            try { using var scope = invalid.RoundedClip(viewport, radius); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "rounded scope rejects invalid radius before changing scope state : " + radius);
        }
        var a = invalid.RoundedClip(viewport, 200);
        var b = invalid.RoundedClip(viewport, 4);
        var wrongOrder = false;
        try { a.Dispose(); } catch (InvalidOperationException) { wrongOrder = true; }
        check(wrongOrder, "rounded scopes reject out-of-order disposal without dropping the active scope");
        b.Dispose(); a.Dispose();
        using (invalid.RoundedClip(viewport, 200)) invalid.AddRectFilled(viewport, new(255, 0, 0), texture);
        var clamped = invalid.Build();
        try { check(Chain(clamped.Single(), clamped.Single().Commands.Single(command => command.ElementCount != 0)).Single().CornerRadius == 80,
            "rounded scope clamps radius to actual half extent using existing analytic shape policy"); }
        finally { foreach (var list in clamped) list.ReleasePooled(); }
    }
}
