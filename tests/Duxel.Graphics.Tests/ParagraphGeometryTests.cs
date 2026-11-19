using System.Globalization;
using Duxel.Core;

internal static partial class ParagraphGeometryTests
{
    internal static void Run(Action<bool, string> check)
    {
        Navigation(check);
        foreach (var source in new[] { "AV", "office", "abc אב 123" })
        {
            var expected = DirectWriteLayoutOracle.CaretPositions("Segoe UI", source, 14);
            var probes = DirectWriteLayoutOracle.GeometryProbes(source);
            foreach (var styled in new[] { false, true })
            {
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                ControlFontFixture.Configure(context);
                using var inline = new UiInlineText([new(source)]);
                var plain = new UiVisualTextLines();
                var boundaries = StringInfo.ParseCombiningCharacters(source).Append(source.Length).ToArray();
                var actual = new float[boundaries.Length];
                var builds = -1;
                var retained = true;
                var hitMatches = true;
                var rangeMatches = true;
                for (var frame = 0; frame < 2; frame++)
                {
                    context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)),
                        new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                        new(0, 0, 400, 300), UiTextSettings.Default);
                    context.Render(new Screen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        using var typography = ui.BeginControlTypography<UiRegularTypeface, UiFluentType300>(true);
                        var lines = plain;
                        if (styled)
                        {
                            inline.Measure(ui, new(400, 300));
                            lines = inline.VisualLines;
                        }
                        else
                            lines.Prepare(ui, source, 400, 300, 20, 0, wrap: false);
                        for (var index = 0; index < boundaries.Length; index++)
                            actual[index] = lines.X(boundaries[index], 0);
                        retained &= frame == 0 || builds == lines.Builds;
                        builds = lines.Builds;
                        foreach (var probe in probes.Hits)
                        {
                            var hit = lines.Hit(probe.X, 1, 20);
                            hitMatches &= hit == probe.Position && MathF.Abs(lines.X(hit, 0) - probe.CaretX) < .002f;
                        }
                        foreach (var probe in probes.Ranges)
                        {
                            var actualFragments = lines.SelectionFragments(probe.Start, probe.End, 0);
                            var edges = actualFragments.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width })
                                .Concat(probe.Fragments.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width }))
                                .Distinct().Order().ToArray();
                            for (var edge = 1; edge < edges.Length; edge++)
                            {
                                if (edges[edge] - edges[edge - 1] < .002f)
                                    continue;
                                var x = (edges[edge] + edges[edge - 1]) / 2;
                                rangeMatches &= actualFragments.Any(fragment => x > fragment.Left && x < fragment.Left + fragment.Width)
                                    == probe.Fragments.Any(fragment => x > fragment.Left && x < fragment.Left + fragment.Width);
                            }
                        }
                    }));
                    using var draw = context.GetDrawData();
                }
                var equal = boundaries.Select((position, index) => MathF.Abs(expected[position] - actual[index]) < .002f).All(value => value);
                Console.WriteLine($"Source : {source} / Styled : {styled} / Expected : {string.Join(", ", expected)} / Actual : {string.Join(", ", actual)}");
                check(equal, $"Complete-row native caret geometry matches actual {(styled ? "inline" : "plain")} flow : {source}");
                check(retained, $"Unchanged paragraph geometry retains layout : styled {styled} / {source}");
                check(hitMatches, $"Native click positions and bidi caret affinity match actual flow : styled {styled} / {source}");
                check(rangeMatches, $"Every nonempty native selection range matches actual visual fragments : styled {styled} / {source}");
            }
        }
        foreach (var spans in new[] { new[] { "A", "V" }, new[] { "abc ", "אב", " 123" } })
        {
            var source = string.Concat(spans);
            var nativeRanges = DirectWriteLayoutOracle.GeometryProbes(source).Ranges;
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            using var flow = new UiInlineFlowWidget(spans.Select(span => (UiWidget)new UiInlineTextSpan(span)).ToArray());
            var correct = true;
            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                flow.Measure(ui, new(400, 300));
                var start = 0;
                for (var index = 0; index < spans.Length; index++)
                {
                    var end = start + spans[index].Length;
                    var native = nativeRanges.Single(range => range.Start == start && range.End == end).Fragments;
                    var left = native.Min(fragment => fragment.Left);
                    var right = native.Max(fragment => fragment.Left + fragment.Width);
                    var actual = flow.ChildBounds(index);
                    correct &= MathF.Abs(actual.X - left) < .002f && MathF.Abs(actual.Width - (right - left)) < .002f;
                    start = end;
                }
            }));
            using var draw = context.GetDrawData();
            check(correct, $"Actual owned inline child bounds match native source-range geometry : {source}");
        }
        var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        var tab = Duxel.Platform.Windows.WindowsPlatformTextBackend.Instance.CreateLineGeometry(new(font, null, "\tAV", 14), false);
        var spaces = DirectWriteLayoutOracle.CaretPositions("Segoe UI", "    AV", 14);
        check(new[] { 0, 4, 5, 6 }.Select((position, index) => MathF.Abs(tab.CaretX(index) - spaces[position]) < .002f).All(value => value),
            "Expanded tab geometry preserves original UTF16 caret boundaries");
        var tabFragments = tab.SelectionFragments(0, 1);
        check(tabFragments is [{ Left: 0 }] && MathF.Abs(tabFragments[0].Width - spaces[4]) < .002f
            && tab.HitTest(spaces[4] * .25f) == 0 && tab.HitTest(spaces[4] * .75f) == 1,
            "Expanded tab remains one selectable source grapheme");
        var rejectsHole = false;
        try
        {
            _ = new PlatformTextLineGeometry(1, new float[] { 0, 1 }, []);
        }
        catch (ArgumentException) { rejectsHole = true; }
        check(rejectsHole, "Incomplete backend line geometry fails before entering retained layout");

        foreach (var count in new[] { 64, 65, 4097 })
        {
            var characters = new PlatformTextCharacterGeometry[count];
            var carets = new float[count + 1];
            for (var index = 0; index < count; index++)
            {
                carets[index] = count - index;
                characters[index] = new(index, 1, count - index - 1, 1, 1);
            }
            var line = new PlatformTextLineGeometry(count, carets, characters);
            check(line.SelectionFragments(0, count) is [{ Left: 0 } fragment] && fragment.Width == count
                && line.SelectionFragments(0, count - 1) is [{ Left: 1 } partial] && partial.Width == count - 1,
                $"Selection merges reversed visual runs at a buffer boundary : {count} characters");
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
