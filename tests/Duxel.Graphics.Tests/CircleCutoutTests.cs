using Duxel.Core;

internal static class CircleCutoutTests
{
    internal static void Run(Action<bool, string> check)
    {
        var clip = new UiRect(0, 0, 240, 160);
        var builder = new UiDrawListBuilder(clip);
        var white = new UiTextureId(1);
        builder.AddRectFilled(new(0, 0, 16, 16), new(255, 0, 0), white);
        using (builder.CircleCutout(new(32, 40), 12))
        {
            builder.AddRectFilledRounded(new(20, 20, 80, 80), new(0, 255, 0), white, 8, clip);
            builder.AddImageRounded(new(3), new(20, 20, 80, 80), UiImageUvTransform.Identity, new(255, 255, 255), 4, clip);
            builder.AddImageQuad(new(3), new(2, 2), new(8, 2), new(8, 8), new(2, 8),
                new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(255, 255, 255));
        }
        builder.AddRectFilled(new(100, 0, 16, 16), new(255, 0, 0), white);
        var lists = builder.Build();
        try
        {
            var list = lists.Single();
            var masked = list.Commands.Where(c => c.CircleCutoutIndexPlusOne != 0).ToArray();
            check(masked.Length == 3 && list.Commands.First().CircleCutoutIndexPlusOne == 0
                && list.Commands.Last().CircleCutoutIndexPlusOne == 0,
                "circular cutout stamps solid, paired image and textured triangles only inside its scope");
            check(masked.Select(c => c.CircleCutoutIndexPlusOne).Distinct().Count() == 1,
                "same builder circle shares one32byte record across geometry kinds");
            var record = list.RectFilledPrimitives![(int)masked[0].CircleCutoutIndexPlusOne - 1];
            check(record.Rect == new UiRect(32, 40, 12, 0), "circle record packs local center and radius without changing the32byte layout");
            var firstStamp = UiDrawList.ComputeCommandStateStamp(list.Commands.AsSpan());
            var changed = list.Commands.AsSpan().ToArray();
            changed[1] = changed[1] with { CircleCutoutIndexPlusOne = 0 };
            check(firstStamp != UiDrawList.ComputeCommandStateStamp(changed), "cutout identity participates in retained command state");
            check(UiDrawList.ComputeCommandScheduleStamp(list.Commands.AsSpan()) != UiDrawList.ComputeCommandScheduleStamp(changed),
                "command schedule cannot reuse a different mask assignment");
            var appended = new UiDrawListBuilder(clip);
            appended.AddRectFilled(new(1, 1, 3, 3), new(255, 255, 255), white);
            appended.AddDrawList(list);
            var copies = appended.Build();
            try
            {
                var copy = copies.Single();
                check(copy.Commands.Where(c => c.CircleCutoutIndexPlusOne != 0).All(c =>
                    copy.RectFilledPrimitives![(int)c.CircleCutoutIndexPlusOne - 1].Rect == record.Rect),
                    "AddDrawList rebases mask indices along with mixed primitive records");
            }
            finally { foreach (var copy in copies) copy.ReleasePooled(); }
            var moved = new UiDrawListBuilder(clip);
            moved.Append(list, .5f, new(8, 9));
            var movedLists = moved.Build();
            try
            {
                check(movedLists.SelectMany(l => l.Commands).Where(c => c.CircleCutoutIndexPlusOne != 0)
                    .All(c => c.Translation == new UiVector2(8, 9) && c.Opacity == .5f),
                    "copied mask stays in local geometry space while translation and opacity remain command state");
            }
            finally { foreach (var movedList in movedLists) movedList.ReleasePooled(); }
            builder._ResetForNewFrame();
            using (builder.CircleCutout(new(60, 70), 8))
                builder.AddRectFilled(new(0, 0, 90, 90), new(255, 255, 255), white);
            var next = builder.Build();
            try
            {
                check(record == list.RectFilledPrimitives![(int)masked[0].CircleCutoutIndexPlusOne - 1],
                    "later builder reset and circle mutation cannot change a queued mask");
                var current = next.Single();
                var command = current.Commands.Single();
                check(current.RectFilledPrimitives![(int)command.CircleCutoutIndexPlusOne - 1].Rect == new UiRect(60, 70, 8, 0),
                    "reused builder storage publishes the new circle rather than a stale cached record");
            }
            finally { foreach (var item in next) item.ReleasePooled(); }
        }
        finally { foreach (var list in lists) list.ReleasePooled(); }
        var sourceBuilder = new UiDrawListBuilder(clip);
        sourceBuilder.AddRectFilled(new(20, 20, 80, 80), new(255, 0, 0), white);
        var sourceLists = sourceBuilder.Build();
        try
        {
            var source = sourceLists.Single() with { StaticGeometryKey = "cutout-retained-test" };
            var retained = new UiDrawListBuilder(clip);
            using (retained.CircleCutout(new(30, 30), 10)) retained.AppendRetainedStaticReference(source);
            var result = retained.Build();
            try
            {
                check(source.Commands.Single().CircleCutoutIndexPlusOne == 0,
                    "scoped retained append leaves source commands immutable");
                check(result.Single().StaticGeometryKey == source.StaticGeometryKey
                    && result.Single().Commands.Single().CircleCutoutIndexPlusOne != 0,
                    "scoped retained append preserves its static identity and publishes owned mask records");
                check(result.Single().StaticGeometryStamp != 0 && result.Single().CommandStateStamp != 0,
                    "scoped retained append publishes explicit changed-content and command-state stamps");
                var shifted = new UiDrawListBuilder(clip);
                using (shifted.CircleCutout(new(45, 30), 10)) shifted.AppendRetainedStaticReference(source);
                var shiftedResult = shifted.Build();
                try
                {
                    check(shiftedResult.Single().StaticGeometryStamp != result.Single().StaticGeometryStamp,
                        "outer mask movement changes the stamp of an otherwise immutable cached source");
                }
                finally { foreach (var item in shiftedResult) item.ReleasePooled(); }
            }
            finally { foreach (var item in result) item.ReleasePooled(); }
        }
        finally { foreach (var source in sourceLists) source.ReleasePooled(); }
        var channels = new UiDrawListBuilder(clip);
        channels.Split(2);
        using (channels.CircleCutout(new(30, 30), 10))
        {
            channels.AddRectFilled(new(0, 0, 70, 70), new(255, 0, 0), white);
            channels.SetCurrentChannel(1);
            channels.AddRectFilled(new(80, 0, 70, 70), new(0, 255, 0), white);
            channels.Merge();
        }
        var mergedChannels = channels.Build();
        try
        {
            check(mergedChannels.SelectMany(l => l.Commands).All(c => c.CircleCutoutIndexPlusOne != 0),
                "channel merging preserves the same active circle without treating it as nested subtraction");
        }
        finally { foreach (var list in mergedChannels) list.ReleasePooled(); }
        var deferredParent = new UiDrawListBuilder(clip);
        var deferredChild = new UiDrawListBuilder(clip);
        deferredChild.AddRectFilled(new(0, 0, 70, 70), new(255, 0, 0), white);
        using (deferredParent.CircleCutout(new(30, 30), 10)) deferredParent.AppendDeferredFrom(deferredChild);
        var deferred = deferredParent.Build();
        try
        {
            check(deferred.SelectMany(l => l.Commands).All(c => c.CircleCutoutIndexPlusOne != 0),
                "deferred child capture consumes the parent paint mask rather than bypassing it");
        }
        finally { foreach (var list in deferred) list.ReleasePooled(); }
        const int quads = 15001;
        var vertices = new UiDrawVertex[quads * 4];
        var indices = new uint[quads * 6];
        for (var quad = 0; quad < quads; quad++)
        {
            var first = (uint)(quad * 4);
            var offset = quad * 6;
            indices[offset] = first; indices[offset + 1] = first + 1; indices[offset + 2] = first + 2;
            indices[offset + 3] = first; indices[offset + 4] = first + 2; indices[offset + 5] = first + 3;
        }
        var large = new UiDrawList(UiPooledList<UiDrawVertex>.FromArray(vertices), UiPooledList<uint>.FromArray(indices),
            UiPooledList<UiDrawCommand>.FromArray([new(clip, white, 0, (uint)indices.Length, 0, CircleCutoutIndexPlusOne : 1)]),
            RectFilledPrimitives : UiPooledList<UiRectFilledPrimitive>.FromArray([new(new(32, 40, 12, 0), default)]));
        var split = new UiDrawListBuilder(clip);
        split.AddDrawList(large);
        var pieces = split.Build();
        try
        {
            check(pieces.Count > 1 && pieces.All(l => l.Commands.All(c => c.CircleCutoutIndexPlusOne != 0
                && l.RectFilledPrimitives![(int)c.CircleCutoutIndexPlusOne - 1].Rect == new UiRect(32, 40, 12, 0))),
                "oversized quad splitting copies and rebases the mask in every resulting geometry buffer");
        }
        finally { foreach (var piece in pieces) piece.ReleasePooled(); large.ReleasePooled(); }
        static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
        }
        check(Throws<ArgumentOutOfRangeException>(() => builder.CircleCutout(new(float.NaN, 0), 1)), "nonfinite circle center fails explicitly");
        check(Throws<ArgumentOutOfRangeException>(() => builder.CircleCutout(default, 0)), "empty circle fails explicitly");
        check(Throws<ArgumentOutOfRangeException>(() => builder.CircleCutout(default, -1)), "negative circle fails explicitly");
        using (builder.CircleCutout(default, 1))
            check(Throws<NotSupportedException>(() => builder.CircleCutout(default, 2)), "unsupported nested circular cutouts fail before changing active state");
    }
}
