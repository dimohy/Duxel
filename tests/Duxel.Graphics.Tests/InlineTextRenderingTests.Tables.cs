using System.Security.Cryptography;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    private static void CompareMarkdownTables(string output, PresentationReuseTests.Fixture reference,
        PresentationReuseTests.Fixture candidate, List<TextAccessibilityPixelPair> pairs)
    {
        var headerWidth = DirectWriteLayoutOracle.CaretPositions("Segoe UI", "Header", 14,
            layout => DirectWriteLayoutOracle.SetFontWeight(layout, 0, 6, 700)).Max();
        var bodyWidth = DirectWriteLayoutOracle.CaretPositions("Segoe UI", "אב abc 123", 14,
            layout => DirectWriteLayoutOracle.SetFontWeight(layout, 0, 2, 700)).Max();
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
                foreach (var width in new[] { 180f, 240f })
                    foreach (var alignment in new[] { 0f, .5f, 1f })
                        foreach (var mixed in new[] { false, true })
                        {
                            reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                            candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                            reference.Renderer.SetMsaaSamples(samples);
                            candidate.Renderer.SetMsaaSamples(samples);
                            var separator = alignment == 0 ? ":---" : alignment == 1 ? "---:" : ":---:";
                            var table = "| Header |\n| " + separator + " |\n| **אב** abc 123 |";
                            using var viewer = Dux.MarkdownView("Native table comparison",
                                new UiState<string>(mixed ? "**אב** abc 123\n\n" + table + "\n\n**גד** tail" : table).Binding,
                                new(Width: width + 24, Height: mixed ? 220 : 140, ShowBorder: false));
                            void Begin(UiImmediateContext ui)
                            {
                                ui.EnableRootViewportContentLayout(contentPadding: 0);
                                new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                            }
                            var expected = reference.Capture(ui =>
                            {
                                Begin(ui);
                                var paint = new UiControlContext(ui);
                                using var contentClip = paint.Clip(new(20, 20, width, mixed ? 200 : 120));
                                var color = dark ? new UiColor(255, 255, 255) : new(36, 36, 36);
                                var border = UiFluentDividerPalette.Resolve(paint.FluentColorScheme, UiDividerAppearance.Default).Line;
                                if (mixed)
                                {
                                    PaintNativeParagraphReference(reference.Context, ui, "אב", " abc 123", UiInlineTextWeight.Bold,
                                        width, false, color, origin: new(20, 20));
                                    // Preserve the native paragraph's leading blank line and original fractional glyph origin.
                                    PaintNativeParagraphReference(reference.Context, ui, "", "\nגד tail", UiInlineTextWeight.Bold,
                                        width, false, color, layout => DirectWriteLayoutOracle.SetFontWeight(layout, 1, 2, 700),
                                        origin: new(20, 124));
                                }
                                for (var row = 0; row < 2; row++)
                                {
                                    var y = (mixed ? 60 : 20) + row * 32;
                                    using var clip = paint.Clip(new(20, y, width, 32));
                                    if (row == 0)
                                        paint.Frame(new(20, y, width, 32), dark ? new(45, 45, 45) : new(245, 245, 245), default, 0, 0);
                                    paint.Frame(new(20, y, width, 1), border, default, 0, 0);
                                    paint.Frame(new(20, y + 31, width, 1), border, default, 0, 0);
                                    paint.Frame(new(20, y, 1, 32), border, default, 0, 0);
                                    paint.Frame(new(20 + width - 1, y, 1, 32), border, default, 0, 0);
                                    var natural = row == 0 ? headerWidth : bodyWidth;
                                    var x = 30 + (width - 20 - natural) * alignment;
                                    PaintNativeParagraphReference(reference.Context, ui, row == 0 ? "Header" : "אב",
                                        row == 0 ? "" : " abc 123", UiInlineTextWeight.Bold, float.PositiveInfinity, false,
                                        color, origin: new(x, y + 6));
                                }
                            });
                            for (var repeat = 0; repeat < 2; repeat++)
                            {
                                var actual = candidate.Capture(ui =>
                                {
                                    Begin(ui);
                                    ui.SetCursorScreenPos(new(8, 10));
                                    viewer.Render(ui);
                                });
                                var name = $"markdown-table-{dark}-{samples}-{width}-{alignment}-{mixed}-{repeat}";
                                var equal = expected.AsSpan().SequenceEqual(actual);
                                pairs.Add(new(name, Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(actual)), equal));
                                if (!equal)
                                {
                                    File.WriteAllBytes(Path.Combine(output, name + ".reference.rgba"), expected);
                                    File.WriteAllBytes(Path.Combine(output, name + ".candidate.rgba"), actual);
                                }
                            }
                        }
    }
}
