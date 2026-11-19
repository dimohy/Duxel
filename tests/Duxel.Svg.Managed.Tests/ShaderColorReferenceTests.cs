using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class ShaderColorReferenceTests
{
    internal static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", "ColorStorage");
        CheckStorage(directory, false, check);
        CheckStorage(Path.Combine(directory, "AlphaStorage"), true, check);
        CheckSvg(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", "ShadowAlpha"), true, check, expectedCount: 9);
        var clips = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", "ClipStorage");
        CheckSvg(clips, true, check, expectedCount: 9, premultiplied: true);
        CheckSvg(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", "OpacityGroups"), true, check,
            "fill-stroke.svg", premultiplied: true);
        var layers = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", "LayerOrigin");
        CheckSvg(layers, true, check, expectedCount: 5, premultiplied: true);
        CheckSvg(directory, false, check);
        // Guard the two corrected transparent regressions; --browser-color-storage qualifies every case.
        CheckSvg(Path.Combine(directory, "Transparent"), true, check, "half-gray.svg");
        CheckSvg(Path.Combine(directory, "Transparent"), true, check, "half-alpha.svg");
    }

    private static void CheckStorage(string directory, bool alpha, Action<bool, string> check)
    {
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "storage.json")));
        var input = File.ReadAllBytes(Path.Combine(directory, "unorm-input.f32"));
        var expected = File.ReadAllBytes(Path.Combine(directory, "unorm-output.rgba"));
        VerifyHash(input, metadata.RootElement.GetProperty("inputSha256").GetString()!);
        VerifyHash(expected, metadata.RootElement.GetProperty("rgbaSha256").GetString()!);
        var count = checked(metadata.RootElement.GetProperty("width").GetInt32() * metadata.RootElement.GetProperty("height").GetInt32());
        if (input.Length != checked(count * 4) || expected.Length != input.Length)
            throw new InvalidDataException("Invalid independent color-storage reference extent.");
        var different = 0;
        for (var index = 0; index < count; index++)
        {
            var value = BinaryPrimitives.ReadSingleLittleEndian(input.AsSpan(index * 4));
            if (!float.IsFinite(value) || value < 0 || value > 1)
                throw new InvalidDataException("Invalid independent shader-color input.");
            var color = SvgShaderColor.Quantize(alpha ? new(0, 0, 0, value) : new(value, value, value, 1));
            if (SvgCompositing.Channel(color.X) != expected[index * 4]
                || SvgCompositing.Channel(color.Y) != expected[index * 4 + 1]
                || SvgCompositing.Channel(color.Z) != expected[index * 4 + 2]
                || SvgCompositing.Channel(color.W) != expected[index * 4 + 3])
                different++;
        }
        check(different == 0, $"Independent browser shader storage / {(alpha ? "alpha" : "RGB")} : {count} float inputs, {different} differences");
    }

    private static void CheckSvg(string directory, bool transparent, Action<bool, string> check, string? sourceName = null, int expectedCount = 3, bool premultiplied = false)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "references.json")));
        var selected = 0;
        foreach (var reference in manifest.RootElement.GetProperty("references").EnumerateArray())
        {
            var file = reference.GetProperty("file").GetString()!;
            if (sourceName is not null && file != sourceName)
                continue;
            selected++;
            var source = File.ReadAllBytes(Path.Combine(directory, file));
            var id = reference.GetProperty("id").GetString()!;
            var suffix = transparent ? ".transparent.browser.rgba" : ".browser.rgba";
            var expected = File.ReadAllBytes(Path.Combine(directory, id + suffix));
            VerifyHash(source, reference.GetProperty("sourceSha256").GetString()!);
            VerifyHash(expected, reference.GetProperty(transparent ? "transparentRgbaSha256" : "rgbaSha256").GetString()!);
            var width = reference.GetProperty("width").GetInt32();
            var height = reference.GetProperty("height").GetInt32();
            if (expected.Length != checked(width * height * 4))
                throw new InvalidDataException("Invalid browser SVG storage reference extent.");
            using var stream = new MemoryStream(source);
            var actual = SvgManagedScene.Compile(XDocument.Load(stream)).Render(width, height, transparent ? default : Vector4.One);
            if (premultiplied)
            {
                Duxel.Core.UiRgba8.Premultiply(actual, actual);
                Duxel.Core.UiRgba8.Premultiply(expected, expected);
            }
            check(actual.AsSpan().SequenceEqual(expected), "Independent browser SVG " + (premultiplied ? "premultiplied " : "") + "RGBA : " + (transparent ? "transparent / " : "opaque / ") + file);
        }
        if (selected != (sourceName is null ? expectedCount : 1))
            throw new InvalidDataException("A selected independent SVG color reference is missing or duplicated.");
    }

    private static void VerifyHash(byte[] bytes, string hash)
    {
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Independent browser color-storage bytes changed.");
    }
}
