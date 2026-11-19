using System.Security.Cryptography;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    private static void CompareMarkdownImages(string output, PresentationReuseTests.Fixture reference,
        PresentationReuseTests.Fixture candidate, List<TextAccessibilityPixelPair> pairs)
    {
        var pixels = new byte[120 * 60 * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 230;
            pixels[index + 3] = 255;
        }
        var image = new UiImageSource(new(120, 60, pixels));
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
                foreach (var width in new[] { 60f, 240f })
                    foreach (var surrounding in new[] { (Before: "left ", After: " right"), (Before: "אב ", After: " גד"), (Before: "a", After: "b") })
                    {
                        reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        reference.Renderer.SetMsaaSamples(samples);
                        candidate.Renderer.SetMsaaSamples(samples);
                        var source = new UiState<string>(surrounding.Before + "![red](memory:red)" + surrounding.After);
                        using var viewer = Dux.MarkdownView("Native image comparison", source.Binding,
                            new(Width: width + 24, Height: 200, ShowBorder: false));
                        viewer.ResolveImage = _ => image;
                        void Begin(UiImmediateContext ui)
                        {
                            ui.EnableRootViewportContentLayout(contentPadding: 0);
                            new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                        }
                        var expected = reference.Capture(ui =>
                        {
                            Begin(ui);
                            StyledParagraphTests.PaintMarkdownObjectReference(reference.Context, ui,
                                surrounding.Before + "\uFFFC" + surrounding.After, surrounding.Before.Length,
                                new(MathF.Min(120, width), MathF.Min(120, width) / 2), width,
                                dark ? new(255, 255, 255) : new(36, 36, 36),
                                (paint, bounds) => paint.Frame(bounds, new(230, 0, 0), default, 0, 0));
                        });
                        for (var repeat = 0; repeat < 2; repeat++)
                        {
                            var actual = candidate.Capture(ui =>
                            {
                                Begin(ui);
                                ui.SetCursorScreenPos(new(8, 10));
                                viewer.Render(ui);
                            });
                            var name = $"markdown-image-{dark}-{samples}-{width}-{surrounding.Before.Length}-{repeat}";
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
