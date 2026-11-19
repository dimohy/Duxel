using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    private static unsafe void CompareMarkdownStructure(string output, PresentationReuseTests.Fixture reference,
        PresentationReuseTests.Fixture candidate, List<TextAccessibilityPixelPair> pairs)
    {
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
                foreach (var width in new[] { 90f, 240f })
                    foreach (var rule in new[] { false, true })
                    {
                        reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                        reference.Renderer.SetMsaaSamples(samples);
                        candidate.Renderer.SetMsaaSamples(samples);
                        using var viewer = Dux.MarkdownView("Native structure comparison",
                            new UiState<string>(rule ? "---" : "> abc אב גד 123").Binding,
                            new(Width: width + 24, Height: 100, ShowBorder: false));
                        void Begin(UiImmediateContext ui)
                        {
                            ui.EnableRootViewportContentLayout(contentPadding: 0);
                            new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                        }
                        var expected = reference.Capture(ui =>
                        {
                            Begin(ui);
                            var paint = new UiControlContext(ui);
                            using var clip = paint.Clip(new(20, 20, width, 80));
                            if (rule)
                            {
                                var palette = UiFluentDividerPalette.Resolve(paint.FluentColorScheme, UiDividerAppearance.Default);
                                paint.Frame(new(20, 30, width, 1), palette.Line, default, 0, 0);
                            }
                            else
                                PaintNativeParagraphReference(reference.Context, ui, "", "│ abc אב גד 123",
                                    UiInlineTextWeight.Bold, width, false, dark ? new(255, 255, 255) : new(36, 36, 36),
                                    layout =>
                                    {
                                        // Segoe UI lacks U+2502. The fixture explicitly selects Malgun Gothic as its secondary face.
                                        // The following space stays in that face, as in the explicit secondary-face fixture intervals.
                                        fixed (char* family = "Malgun Gothic")
                                            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, char*, NativeRange, int>)NativeMethod(layout, 31))
                                                (layout, family, new(0, 2)));
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
                            var name = $"markdown-structure-{dark}-{samples}-{width}-{rule}-{repeat}";
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
