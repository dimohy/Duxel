using System.Security.Cryptography;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    private static void CompareMarkdownEntities(string output, PresentationReuseTests.Fixture reference,
        PresentationReuseTests.Fixture candidate, List<TextAccessibilityPixelPair> pairs)
    {
        const string encoded = "AV &amp; &eacute; &fjlig; tail";
        const string decoded = "AV & é fj tail";
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
            {
                reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                reference.Renderer.SetMsaaSamples(samples);
                candidate.Renderer.SetMsaaSamples(samples);
                var source = new UiState<string>(encoded);
                using var viewer = Dux.MarkdownView("Entity paragraph comparison", source.Binding,
                    new(Width: 264, Height: 100, ShowBorder: false));
                void Begin(UiImmediateContext ui)
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                }
                var expected = reference.Capture(ui =>
                {
                    Begin(ui);
                    using var clip = new UiControlContext(ui).Clip(new(20, 20, 240, 80));
                    PaintNativeParagraphReference(reference.Context, ui, "", decoded, UiInlineTextWeight.Regular, 240, false,
                        dark ? new(255, 255, 255) : new(36, 36, 36));
                });
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    var actual = candidate.Capture(ui =>
                    {
                        Begin(ui);
                        ui.SetCursorScreenPos(new(8, 10));
                        viewer.Render(ui);
                    });
                    var name = $"markdown-entities-{dark}-{samples}-{repeat}";
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
