using System.Security.Cryptography;
using System.Text.Json;
using Duxel.Core;

internal static class InlineAlignmentRenderingTests
{
    internal static void Run(string output)
    {
        if (Directory.Exists(output))
            throw new IOException("Use a fresh alignment GPU directory.");
        Directory.CreateDirectory(output);
        var pairs = new List<TextAccessibilityPixelPair>();
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
                foreach (var alignment in Enum.GetValues<UiItemHorizontalAlign>())
                {
                    reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    reference.Renderer.SetMsaaSamples(samples);
                    candidate.Renderer.SetMsaaSamples(samples);
                    using var flow = new UiInlineFlowWidget([new UiInlineTextSpan("Long literal\nshort")])
                    {
                        HorizontalAlignment = alignment
                    };
                    using var box = Dux.SizedBox(flow, 240, 100);
                    void Begin(UiImmediateContext ui)
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                        ui.SetCursorScreenPos(new(20, 20));
                    }
                    var expected = reference.Capture(ui =>
                    {
                        Begin(ui);
                        var paint = new UiControlContext(ui);
                        using var clip = paint.Clip(new(20, 20, 240, 100));
                        using var typography = paint.BeginTypography<UiRegularTypeface, UiFluentType300>();
                        var y = 20f;
                        foreach (var line in new[] { "Long literal", "short" })
                        {
                            var block = paint.MeasureTextBlock(line);
                            var x = alignment switch
                            {
                                UiItemHorizontalAlign.Center => 20 + (240 - block.Size.X) / 2,
                                UiItemHorizontalAlign.Right => 260 - block.Size.X,
                                _ => 20
                            };
                            paint.TypographyTextBlock(block, new(x, y, block.Size.X, 20), paint.Theme.Text);
                            y += 20;
                        }
                    });
                    for (var repeat = 0; repeat < 2; repeat++)
                    {
                        var actual = candidate.Capture(ui => { Begin(ui); box.Render(ui); });
                        var name = $"{dark}-{samples}-{alignment}-{repeat}";
                        var equal = expected.AsSpan().SequenceEqual(actual);
                        pairs.Add(new(name, Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(actual)), equal));
                        if (!equal)
                        {
                            File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                            File.WriteAllBytes(Path.Combine(output, name + ".candidate.rgba"), actual);
                        }
                    }
                }
        var result = new TextAccessibilityRenderResult(pairs.All(x => x.Equal), pairs.Count, pairs.ToArray());
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, TextAccessibilityRenderJson.Default.TextAccessibilityRenderResult));
        if (!result.Completed)
            throw new InvalidOperationException("Aligned inline pixels differ from independently placed physical text lines.");
    }
}
