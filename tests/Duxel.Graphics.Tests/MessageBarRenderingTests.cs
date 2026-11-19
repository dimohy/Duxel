using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class MessageBarReference
{
    internal static MessageBarReferenceFixture Load()
        => JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MessageBarReference.json")),
            MessageBarReferenceJson.Default.MessageBarReferenceFixture)
            ?? throw new InvalidOperationException("MessageBar color reference is missing.");
    internal static UiColor Color(string hex)
    {
        var rgb = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }
    internal static void Check(Action<bool, string> check)
    {
        var reference = Load();
        check(reference.SourceCommit == "f9720c35ba893c9630f4c0389d07dc6d6fd6068d" && reference.Colors.Length == 8,
            "MessageBar palette oracle pins complete source aliases,global colors and eight intent/scheme cases");
        foreach (var entry in reference.Colors)
        {
            var actual = UiFluentMessageBarPresentation.ResolvePalette(Enum.Parse<UiFluentColorScheme>(entry.Scheme),
                Enum.Parse<UiMessageBarIntent>(entry.Intent));
            check(actual == new UiMessageBarPalette(Color(entry.Background), Color(entry.Border), Color(entry.Foreground), Color(entry.Icon)),
                $"MessageBar actual palette matches independent official source tokens : {entry.Scheme}/{entry.Intent}");
        }
    }
}

internal static class MessageBarRenderingTests
{
    internal static void Run(string output)
    {
        if (Directory.Exists(output))
            throw new IOException("Use a fresh MessageBar GPU result directory.");
        Directory.CreateDirectory(output);
        var colors = MessageBarReference.Load().Colors;
        var pairs = new List<TextAccessibilityPixelPair>();
        using var reference = new PresentationReuseTests.Fixture(1, false);
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        foreach (var entry in colors)
            foreach (var shape in new[] { UiMessageBarShape.Rounded, UiMessageBarShape.Square })
                foreach (var samples in new[] { 1, 4 })
                {
                    var scheme = Enum.Parse<UiFluentColorScheme>(entry.Scheme);
                    var intent = Enum.Parse<UiMessageBarIntent>(entry.Intent);
                    var design = scheme == UiFluentColorScheme.Dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11;
                    reference.Context.SetDesign(design);
                    candidate.Context.SetDesign(design);
                    reference.Renderer.SetMsaaSamples(samples);
                    candidate.Renderer.SetMsaaSamples(samples);
                    using var bar = Fluent.MessageBar("Status", Fluent.Body1("Message"),
                        options: new(intent, Shape: shape, Width: 280));
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
                        paint.Frame(new(20, 20, 280, 36), MessageBarReference.Color(entry.Background),
                            MessageBarReference.Color(entry.Border), shape == UiMessageBarShape.Square ? 0 : 4, 1);
                        // The expected glyph is pinned independently of the product's intent switch.
                        ui.PaintFontIcon<UiFluentFilledIconFont>(entry.Glyph, new(33, 28, 20, 20), MessageBarReference.Color(entry.Icon));
                        using var style = paint.BeginTypography<UiRegularTypeface, UiFluentType300>();
                        var body = paint.MeasureTextBlock("Message");
                        paint.TypographyTextBlock(body, new(61, 28, body.Size.X, 20), MessageBarReference.Color(entry.Foreground));
                    });
                    for (var repeat = 0; repeat < 2; repeat++)
                    {
                        var actual = candidate.Capture(ui => { Begin(ui); bar.Render(ui); });
                        var name = $"{entry.Scheme}-{entry.Intent}-{shape}-{samples}-{repeat}";
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
            throw new InvalidOperationException("MessageBar differs from independent source color/icon/geometry rendering.");
    }
}

internal sealed record MessageBarReferenceFixture(string SourceCommit, MessageBarColorReference[] Colors);
internal sealed record MessageBarColorReference(string Scheme, string Intent, string Background, string Border,
    string Foreground, string Icon, string Glyph);
[JsonSerializable(typeof(MessageBarReferenceFixture))]
internal partial class MessageBarReferenceJson : JsonSerializerContext;
