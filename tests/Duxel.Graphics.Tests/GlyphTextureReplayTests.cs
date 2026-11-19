using System.Globalization;
using Duxel.Core;

internal static class GlyphTextureReplayTests
{
    internal static void Run(string output, int frameSlots, string input, string widthArg, string heightArg)
    {
        var width = int.Parse(widthArg, CultureInfo.InvariantCulture);
        var height = int.Parse(heightArg, CultureInfo.InvariantCulture);
        if (width is <= 0 or > 128 || height is <= 0 or > 96)
            throw new ArgumentOutOfRangeException(nameof(input), "The input texture must fit the replay scene.");
        var source = File.ReadAllBytes(input);
        if (source.Length != checked(width * height * 4))
            throw new InvalidDataException("The texture must contain exactly width × height RGBA bytes.");

        if (Directory.Exists(output))
            throw new IOException("Glyph replay requires a fresh output directory.");
        Directory.CreateDirectory(output);
        using var fixture = new PresentationReuseTests.Fixture(frameSlots, false);
        if (fixture.FrameInfo.DisplaySize != new UiVector2(320, 240))
            throw new InvalidOperationException("Glyph replay requires a physical 320×240 framebuffer.");

        var texture = new UiTextureId(77);
        fixture.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, texture, UiTextureFormat.Rgba8Unorm,
            width, height, source));
        foreach (var samples in new[] { 1, 4 })
        {
            fixture.Renderer.SetMsaaSamples(samples);
            var background = fixture.Capture(PaintBackground);
            foreach (var (name, position) in new (string, UiVector2)[] { ("aligned", new(20, 16)), ("fractional", new(20.25f, 16.5f)) })
            {
                var pixels = fixture.Capture(ui =>
                {
                    PaintBackground(ui);
                    ui.GetWindowDrawList().AddImage(texture, position, new(position.X + width, position.Y + height),
                        default, new(1, 1), new(255, 255, 255));
                });
                if (pixels.AsSpan().SequenceEqual(background))
                    throw new InvalidOperationException("The supplied glyph texture did not change any framebuffer pixel.");
                File.WriteAllBytes(Path.Combine(output, $"msaa{samples}-{name}.rgba"), pixels);
            }
        }

        static void PaintBackground(UiImmediateContext ui) => new UiControlContext(ui).Frame(
            new(0, 0, 320, 240), new(26, 32, 40), default, 0, 0);
    }
}
