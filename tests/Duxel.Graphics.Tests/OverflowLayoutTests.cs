using Duxel.Core;

internal static class OverflowLayoutTests
{
    internal static void Run(Action<bool, string> check)
    {
        UiOverflowItem[] items = [new(60), new(60, 2), new(60)];
        var visible = new bool[4];
        visible[3] = true;
        var result = UiOverflowLayout.Resolve(items, 150, 30, visible, new(Spacing : 10));
        check(visible.AsSpan(0, 3).SequenceEqual(new[] { false, true, false }) && visible[3]
            && result == new UiOverflowResult(1, 2, 60, 100, 30, false, true),
            "overflow keeps the higher-priority item, reserves one indicator gap and leaves unused output untouched");
        result = UiOverflowLayout.Resolve(items, 200, 30, visible, new(Spacing : 10));
        check(result.VisibleCount == 3 && !result.IsOverflowing && result.IndicatorExtent == 0,
            "an exact fit keeps every item without reserving a hidden indicator");
        result = UiOverflowLayout.Resolve(items, 10, 30, visible);
        check(result.HiddenCount == 3 && result.IndicatorExtent == 10 && result.IndicatorClipped && result.Fits,
            "a viewport smaller than the indicator explicitly reports clipped indicator-only layout");
        result = UiOverflowLayout.Resolve(items, 10, 30, visible, new(MinimumVisible : 2));
        check(result.VisibleCount == 2 && result.HiddenCount == 1 && !result.Fits,
            "minimum-visible policy reports unmet capacity rather than silently hiding required children");
        result = UiOverflowLayout.Resolve(items, 10, 30, visible, new(MinimumVisible : 20));
        check(result.VisibleCount == 3 && !result.IsOverflowing && result.IndicatorExtent == 0 && !result.Fits,
            "a minimum larger than item count keeps all available items without inventing hidden content");
        UiOverflowItem[] extremes = [new(float.MaxValue, -1), new(1, int.MaxValue), new(2, int.MinValue)];
        result = UiOverflowLayout.Resolve(extremes, 5, 1, visible);
        check(result.VisibleCount == 1 && visible[1] && result.ContentExtent == 1,
            "removing a huge low-priority extent preserves the small retained suffix without cancellation");

        var random = new Random(1781);
        var oraclePassed = true;
        for (var iteration = 0; iteration < 1200; iteration++)
        {
            var input = Enumerable.Range(0, random.Next(0, 9)).Select(_ => new UiOverflowItem(random.Next(0, 40), random.Next(-2, 3))).ToArray();
            var capacity = random.Next(0, 160);
            var indicator = random.Next(0, 40);
            var options = new UiOverflowOptions(random.Next(0, 9), random.Next(0, 5),
                random.Next(2) == 0 ? UiOverflowCollapseDirection.End : UiOverflowCollapseDirection.Start, random.Next(2) == 0);
            var actual = new bool[input.Length];
            var resolved = UiOverflowLayout.Resolve(input, capacity, indicator, actual, options);
            var expected = Oracle(input, capacity, indicator, options);
            oraclePassed &= actual.SequenceEqual(expected.Visible) && resolved == expected.Result;
        }
        check(oraclePassed, "1200 deterministic layouts match independent recomputation of priority prefixes, minima and gaps");
        var invalidRejected = 0;
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, -1f })
        {
            visible.AsSpan().Fill(true);
            try { UiOverflowLayout.Resolve(new UiOverflowItem[] { new(invalid) }, 10, 1, visible); }
            catch (ArgumentOutOfRangeException) { invalidRejected++; }
            if (!visible.All(value => value)) throw new InvalidOperationException("Invalid overflow input mutated visibility.");
        }
        check(invalidRejected == 3, "invalid item extents fail before mutating caller visibility");
        var shortRejected = false;
        try { UiOverflowLayout.Resolve(items, 10, 1, Span<bool>.Empty); }
        catch (ArgumentException) { shortRejected = true; }
        check(shortRejected, "visibility storage is validated before layout writes");

        foreach (var count in new[] { 16, 512 })
        {
            var input = Enumerable.Range(0, count).Select(i => new UiOverflowItem(10, i % 7)).ToArray();
            var output = new bool[count];
            for (var i = 0; i < 32; i++) UiOverflowLayout.Resolve(input, 80, 20, output);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 32; i++) UiOverflowLayout.Resolve(input, 80, 20, output);
            check(GC.GetAllocatedBytesForCurrentThread() == before,
                "overflow resolver allocates zero managed bytes after warmup : " + count);
        }
    }

    private static (bool[] Visible, UiOverflowResult Result) Oracle(UiOverflowItem[] items, float capacity, float indicator, UiOverflowOptions options)
    {
        var removal = Enumerable.Range(0, items.Length).OrderBy(i => items[i].Priority)
            .ThenBy(i => options.CollapseFrom == UiOverflowCollapseDirection.End ? -i : i).ToArray();
        var visible = Enumerable.Repeat(true, items.Length).ToArray();
        for (var cut = 0; ; cut++)
        {
            if (cut != 0) visible[removal[cut - 1]] = false;
            var retained = Enumerable.Range(0, items.Length).Where(i => visible[i]).ToArray();
            var content = retained.Sum(i => (double)items[i].Extent) + Math.Max(0, retained.Length - 1) * (double)options.Spacing;
            var trigger = cut == 0 ? 0 : MathF.Min(indicator, capacity);
            var occupied = content + trigger + (cut != 0 && retained.Length != 0 ? options.Spacing : 0);
            if (occupied <= capacity || retained.Length <= options.MinimumVisible)
                return (visible, new(retained.Length, cut, content, occupied, trigger, cut != 0 && trigger < indicator, occupied <= capacity));
        }
    }
}
