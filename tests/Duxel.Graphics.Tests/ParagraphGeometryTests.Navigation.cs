using Duxel.Core;

internal static partial class ParagraphGeometryTests
{
    private static void Navigation(Action<bool, string> check)
    {
        ParagraphNavigationInputTests.Run(check);
        SingleLineNavigationInputTests.Run(check);
        foreach (var source in new[] { "abc אב גד 123", "אבגד", "AV e\u0301 office", "A אב B גד C" })
            foreach (var styled in new[] { false, true })
            {
                var probes = DirectWriteLayoutOracle.GeometryProbes(source).Hits;
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                ControlFontFixture.Configure(context);
                using var inline = new UiInlineText([new(source)]);
                var plain = new UiVisualTextLines();
                context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)),
                    new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                    new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(new Screen(ui =>
                {
                    using var typography = ui.BeginControlTypography<UiRegularTypeface, UiFluentType300>(true);
                    var lines = plain;
                    if (styled)
                    {
                        inline.Measure(ui, new(400, 300));
                        lines = inline.VisualLines;
                    }
                    else
                        lines.Prepare(ui, source, 400, 300, 20, 0, wrap: false);
                    foreach (var right in new[] { true, false })
                    {
                        var index = lines.Hit(right ? -1 : lines.Width + 1, 1, 20);
                        var edges = probes.Select(probe => probe.CaretX).Distinct().Order().ToArray();
                        if (!right)
                            Array.Reverse(edges);
                        var matches = true;
                        foreach (var edge in edges.Skip(1))
                        {
                            var previousX = lines.X(index, 0);
                            var expected = probes.Where(probe => MathF.Abs(probe.CaretX - edge) < .002f
                                && (right ? probe.X < edge && probe.X > previousX : probe.X > edge && probe.X < previousX))
                                .OrderBy(probe => MathF.Abs(probe.X - edge)).First();
                            index = lines.MoveHorizontal(index, right);
                            matches &= index == expected.Position && MathF.Abs(lines.X(index, 0) - edge) < .002f;
                        }
                        var terminalX = lines.X(index, 0);
                        matches &= lines.MoveHorizontal(index, right) == index && lines.X(index, 0) == terminalX;
                        check(matches, $"Physical arrow steps and retained bidi affinity match independent native pointer edges : {source} / styled {styled} / right {right}");
                    }
                    if (source == "אבגד")
                    {
                        lines.ClearAffinity();
                        check(lines.CollapseHorizontal(0, source.Length, right: false) == source.Length
                            && lines.CollapseHorizontal(0, source.Length, right: true) == 0,
                            $"RTL selection collapses to its physical edge rather than logical minimum : styled {styled}");
                    }
                }));
                using var draw = context.GetDrawData();
            }
    }
}
