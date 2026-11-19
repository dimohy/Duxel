using System.Security.Cryptography;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    private static void CompareMarkdownTasks(string output, PresentationReuseTests.Fixture reference,
        PresentationReuseTests.Fixture candidate, List<TextAccessibilityPixelPair> pairs)
    {
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
                foreach (var width in new[] { 90f, 240f })
                    for (var variant = 0; variant < 3; variant++)
                    {
                        reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        reference.Renderer.SetMsaaSamples(samples);
                        candidate.Renderer.SetMsaaSamples(samples);
                        var isChecked = variant != 0;
                        var body = variant == 2 ? " אב גד 123" : " regular body";
                        using var viewer = Dux.MarkdownView("Native task comparison",
                            new UiState<string>((isChecked ? "- [x]" : "- [ ]") + body).Binding,
                            new(Width: width + 24, Height: 200, ShowBorder: false));
                        void Begin(UiImmediateContext ui)
                        {
                            ui.EnableRootViewportContentLayout(contentPadding: 0);
                            new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                        }
                        var expected = reference.Capture(ui =>
                        {
                            Begin(ui);
                            StyledParagraphTests.PaintMarkdownObjectReference(reference.Context, ui, "\uFFFC" + body,
                                0, new(16, 16), width, dark ? new(255, 255, 255) : new(36, 36, 36), (paint, bounds) =>
                                {
                                    var palette = UiFluentCheckboxPalette.Resolve(paint.FluentColorScheme, UiButtonVisualState.Rest, isChecked);
                                    UiFluentSquareCheckboxShape.Paint(paint, bounds, palette.Background, palette.Border);
                                    if (isChecked)
                                        UiFluentCheckboxCheck12.Paint(paint, new(bounds.X + 2, bounds.Y + 2, 12, 12), palette.Indicator, default);
                                });
                        });
                        for (var repeat = 0; repeat < 2; repeat++)
                        {
                            var actual = candidate.Capture(ui =>
                            {
                                Begin(ui);
                                ui.SetCursorScreenPos(new(8, 10));
                                viewer.Render(ui);
                            });
                            var name = $"markdown-task-{dark}-{samples}-{width}-{variant}-{repeat}";
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
