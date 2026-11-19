using System.Security.Cryptography;
using System.Text.Json;
using Duxel.Core;

internal static class OwnedInlineRenderingTests
{
    internal static void Run(string output)
    {
        if (Directory.Exists(output))
            throw new IOException("Use a fresh owned inline GPU result directory.");
        Directory.CreateDirectory(output);
        var pairs = new List<TextAccessibilityPixelPair>();
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
                foreach (var rightToLeft in new[] { false, true })
                    foreach (var width in new[] { 90f, 240f })
                        for (var variant = 0; variant < 3; variant++)
                        {
                            reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                            candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                            reference.Renderer.SetMsaaSamples(samples);
                            candidate.Renderer.SetMsaaSamples(samples);
                            UiFluentMessageBarTitle Title()
                            {
                                var value = Fluent.MessageBarTitle("Title");
                                value.Margin = new(7, 11, 13, 17);
                                value.Transform = new(RotationDegrees: variant == 1 ? 90 : variant == 2 ? 45 : 0,
                                    Scale: variant == 2 ? 1.5f : 1);
                                return value;
                            }
                            using var controlTitle = Title();
                            using var subjectTitle = Title();
                            using var body = Fluent.MessageBarBody("regular body", subjectTitle);
                            body.RightToLeft = rightToLeft;
                            using var subject = Dux.SizedBox(body, width, 160);
                            void Begin(UiImmediateContext ui)
                            {
                                ui.EnableRootViewportContentLayout(contentPadding: 0);
                                new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                                ui.SetCursorScreenPos(new(20, 20));
                            }
                            var expected = reference.Capture(ui =>
                            {
                                Begin(ui);
                                var extent = controlTitle.MeasureConstrained(ui, new(0, width, 0, float.PositiveInfinity));
                                StyledParagraphTests.PaintOwnedObjectReference(reference.Context, ui, controlTitle, extent, width, rightToLeft);
                            });
                            for (var repeat = 0; repeat < 2; repeat++)
                            {
                                var actual = candidate.Capture(ui => { Begin(ui); subject.Render(ui); });
                                var name = $"{dark}-{samples}-{rightToLeft}-{width}-{variant}-{repeat}";
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
            throw new InvalidOperationException("Owned inline objects differ from independent widget placement.");
    }
}
