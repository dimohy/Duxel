using System.Security.Cryptography;
using System.Text.Json;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    internal static void Run(string output, bool owned = false)
    {
        if (Directory.Exists(output))
            throw new IOException("Use a fresh inline text GPU result directory.");
        Directory.CreateDirectory(output);
        var pairs = new List<TextAccessibilityPixelPair>();
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
                foreach (var rightToLeft in new[] { false, true })
                    for (var variant = 0; variant < 8; variant++)
                    {
                        reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        reference.Renderer.SetMsaaSamples(samples);
                        candidate.Renderer.SetMsaaSamples(samples);
                        var heading = variant switch
                        {
                            2 => "한글 제목 ",
                            3 => "Bold ",
                            4 => "abc אב ",
                            5 => "AV office ",
                            6 => "بِت ",
                            7 => "A \u202Bאב 정 ",
                            _ => "Title "
                        };
                        var body = variant switch
                        {
                            1 => "body body",
                            2 => "본문\n다음 줄",
                            4 => "גד regular 123",
                            5 => "e\u0301 A\u030a next",
                            6 => "لا 123",
                            7 => "גד\u202C Z",
                            _ => "regular body"
                        };
                        var weight = variant == 3 ? UiInlineTextWeight.Bold : UiInlineTextWeight.Semibold;
                        UiWidget Owned()
                        {
                            if (variant == 0)
                                return new UiInlineFlowWidget([
                                new UiInlineTextSpan("Ti", weight), new UiInlineTextSpan("tle ", weight), new UiInlineTextSpan(body)]);
                            var title = Fluent.MessageBarTitle(heading.TrimEnd(' '));
                            title.Weight = weight;
                            return Fluent.MessageBarBody(body, title);
                        }
                        using UiWidget text = owned ? Owned() : new UiInlineText(variant == 0
                            ? [new("Ti", weight), new("tle ", weight), new(body)]
                            : [new(heading, weight), new(body)])
                        {
                            RightToLeft = rightToLeft
                        };
                        if (text is UiInlineFlowWidget flow)
                            flow.RightToLeft = rightToLeft;
                        var width = variant >= 4 ? 90f : 240f;
                        void Begin(UiImmediateContext ui)
                        {
                            ui.EnableRootViewportContentLayout(contentPadding: 0);
                            new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                            ui.SetCursorScreenPos(new(20, 20));
                        }
                        float SpanWidth<TFace>(UiControlContext paint, string value)
                            where TFace : IUiTypographyTypeface
                        {
                            using var style = paint.BeginTypography<TFace, UiFluentType300>();
                            return paint.MeasureTextBlock(value).Size.X;
                        }
                        var expected = reference.Capture(ui =>
                        {
                            Begin(ui);
                            var paint = new UiControlContext(ui);
                            if (variant == 1)
                            {
                                var titleWidth = weight == UiInlineTextWeight.Bold
                                    ? SpanWidth<UiBoldTypeface>(paint, heading)
                                    : SpanWidth<UiSemiboldTypeface>(paint, heading);
                                width = titleWidth + SpanWidth<UiRegularTypeface>(paint, "body ") + .1f;
                            }
                            using var clip = paint.Clip(new(20, 20, width, 80));
                            PaintNativeParagraphReference(reference.Context, ui, heading, body, weight, width, rightToLeft);
                        });
                        using var assigned = Dux.SizedBox(text, width, 80);
                        for (var repeat = 0; repeat < 2; repeat++)
                        {
                            var actual = candidate.Capture(ui => { Begin(ui); assigned.Render(ui); });
                            var name = $"{dark}-{samples}-{rightToLeft}-{variant}-{repeat}";
                            var equal = expected.AsSpan().SequenceEqual(actual);
                            pairs.Add(new(name, Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(actual)), equal));
                            if (!equal)
                            {
                                File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                                File.WriteAllBytes(Path.Combine(output, name + ".candidate.rgba"), actual);
                            }
                        }
                    }
        if (!owned)
        {
            CompareMarkdownParagraph(output, reference, candidate, pairs);
            CompareMarkdownEntities(output, reference, candidate, pairs);
            CompareSingleLineInput(output, reference, candidate, pairs);
            CompareSingleLineInsertion(output, pairs);
            CompareSingleLineComposition(output, reference, candidate, pairs);
            CompareMarkdownImages(output, reference, candidate, pairs);
            CompareMarkdownTasks(output, reference, candidate, pairs);
            CompareMarkdownStructure(output, reference, candidate, pairs);
            CompareMarkdownTables(output, reference, candidate, pairs);
        }
        var result = new TextAccessibilityRenderResult(pairs.All(x => x.Equal), pairs.Count, pairs.ToArray());
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, TextAccessibilityRenderJson.Default.TextAccessibilityRenderResult));
        if (!result.Completed)
            throw new InvalidOperationException("Inline paragraph differs from independent whole native TextLayout pixels.");
    }
}
