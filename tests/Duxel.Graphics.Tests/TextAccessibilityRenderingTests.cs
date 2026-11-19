using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class TextAccessibilityRenderingTests
{
    internal static void Run(string output)
    {
        if (Directory.Exists(output))
            throw new IOException("Use a fresh text accessibility GPU result directory.");
        Directory.CreateDirectory(output);
        var pairs = new List<TextAccessibilityPixelPair>();
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        using var tree = new UiAccessibilityTree();
        candidate.Context.SetAccessibilityTree(tree);
        foreach (var dark in new[] { false, true })
            foreach (var samples in new[] { 1, 4 })
            {
                reference.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                candidate.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                reference.Renderer.SetMsaaSamples(samples);
                candidate.Renderer.SetMsaaSamples(samples);
                using var controlText = Fluent.Body1("정세일 · wrapped English text");
                using var subjectText = Fluent.Body1("정세일 · wrapped English text");
                using var control = Dux.SizedBox(controlText, 100, 80);
                using var subject = Dux.SizedBox(subjectText, 100, 80);
                for (var variant = 0; variant < 6; variant++)
                {
                    controlText.Source = subjectText.Source = variant >= 3 ? "바뀐 한글 · Changed text" : "정세일 · wrapped English text";
                    control.Transform = subject.Transform = new(RotationDegrees: variant == 1 ? 45 : 0);
                    control.Margin = subject.Margin = variant == 2 ? new(7, 11, 13, 17) : default;
                    void Draw(UiImmediateContext ui, UiWidget text)
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(11, 33, 71), default, 0, 0);
                        ui.SetCursorScreenPos(new(20, 20));
                        using var clip = new UiControlContext(ui).Clip(variant == 4 ? new(30, 30, 60, 30) : new(0, 0, 320, 240));
                        text.Render(ui);
                    }
                    var expected = reference.Capture(ui => Draw(ui, control));
                    var actual = candidate.Capture(ui => Draw(ui, subject));
                    var name = $"{dark}-{samples}-{variant}";
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
            throw new InvalidOperationException("Publishing static text changed rendered pixels.");
    }
}

internal sealed record TextAccessibilityPixelPair(string Name, string ReferenceSha256, string CandidateSha256, bool Equal);
internal sealed record TextAccessibilityRenderResult(bool Completed, int ComparedPairs, TextAccessibilityPixelPair[] Pairs);
[JsonSerializable(typeof(TextAccessibilityRenderResult))]
internal partial class TextAccessibilityRenderJson : JsonSerializerContext;
