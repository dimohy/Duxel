using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static partial class InlineTextRenderingTests
{
    internal static void RunBackgroundComposition(string output)
    {
        Directory.CreateDirectory(output);
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        var results = new List<TextBackgroundPair>();
        var backgrounds = new UiColor[] { new(32, 32, 32), new(245, 245, 245), new(11, 33, 71) };
        var foregrounds = new UiColor[] { new(255, 255, 255), new(36, 36, 36), new(100, 180, 240) };
        foreach (var samples in new[] { 1, 4 })
            foreach (var background in backgrounds)
                foreach (var foreground in foregrounds)
                    foreach (var opacity in new byte[] { 255, 140 })
                    {
                        reference.Renderer.SetMsaaSamples(samples);
                        candidate.Renderer.SetMsaaSamples(samples);
                        var color = new UiColor((foreground.Rgba & 0xFFFFFF) | ((uint)opacity << 24));
                        using var text = new UiInlineText([new("Agyp office")], color: color);
                        using var subject = Dux.SizedBox(text, 240, 80);
                        void Begin(UiImmediateContext ui)
                        {
                            ui.EnableRootViewportContentLayout(contentPadding: 0);
                            new UiControlContext(ui).Frame(new(0, 0, 320, 240), background, default, 0, 0);
                            ui.SetCursorScreenPos(new(20, 20));
                        }
                        var expected = reference.Capture(ui =>
                        {
                            Begin(ui);
                            PaintNativeParagraphReference(reference.Context, ui, "", "Agyp office", UiInlineTextWeight.Regular,
                                240, false, color, bakedBackground: background);
                        });
                        var actual = candidate.Capture(ui => { Begin(ui); subject.Render(ui); });
                        var maximum = 0;
                        for (var index = 0; index < actual.Length; index++)
                            maximum = Math.Max(maximum, Math.Abs(expected[index] - actual[index]));
                        var name = $"MSAA{samples}-background{background.Rgba:X8}-ink{color.Rgba:X8}";
                        results.Add(new(name, maximum, maximum <= 1));
                        if (maximum > 1)
                        {
                            File.WriteAllBytes(Path.Combine(output, name + ".expected.rgba"), expected);
                            File.WriteAllBytes(Path.Combine(output, name + ".actual.rgba"), actual);
                        }
                    }
        var result = new TextBackgroundResult(results.All(value => value.Passed), results.Count, results.ToArray());
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(result, TextBackgroundJson.Default.TextBackgroundResult));
        if (!result.Completed)
            throw new InvalidOperationException("GPU text composition differs from independent native-mask CPU composition.");
    }
}
internal sealed record TextBackgroundPair(string Name, int MaximumChannelError, bool Passed);
internal sealed record TextBackgroundResult(bool Completed, int ComparedPairs, TextBackgroundPair[] Pairs);
[JsonSerializable(typeof(TextBackgroundResult))]
internal partial class TextBackgroundJson : JsonSerializerContext;
