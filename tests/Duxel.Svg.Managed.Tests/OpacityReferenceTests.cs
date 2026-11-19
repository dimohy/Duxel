using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class OpacityReferenceTests
{
    internal static void Run(Action<bool, string> check)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", "Opacity", "alpha-references.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var reference in manifest.RootElement.GetProperty("references").EnumerateArray())
        {
            var source = reference.GetProperty("source").GetString()!;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
            if (!hash.Equals(reference.GetProperty("sourceSha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Opacity reference input hash changed.");
            var alphas = reference.GetProperty("alphas");
            if (alphas.GetArrayLength() != 1)
                throw new InvalidDataException("Opacity fixture must have uniform coverage and alpha.");
            var scene = SvgManagedScene.Compile(XDocument.Parse(source));
            var rgba = scene.Render(1, 1);
            check(rgba[3] == alphas[0].GetByte(),
                $"Native browser opacity alpha : {reference.GetProperty("opacity")} / {reference.GetProperty("paint").GetString()} / {reference.GetProperty("target").GetString()}");
        }
        RunFoldingReferences(check);
    }

    private static void RunFoldingReferences(Action<bool, string> check)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", "OpacityGroups");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "references.json")));
        var selected = 0;
        Dictionary<string, (byte[] Actual, byte[] Expected)> clipping = [];
        foreach (var reference in manifest.RootElement.GetProperty("references").EnumerateArray())
        {
            var file = reference.GetProperty("file").GetString()!;
            if (file is not ("single-solid.svg" or "nested-solid.svg" or "translucent-gradient.svg" or "nested-gradient.svg" or "overlapping-gradient.svg" or "single-gradient.svg" or "clipped-gradient.svg"))
                continue;
            selected++;
            var source = File.ReadAllBytes(Path.Combine(directory, file));
            var id = reference.GetProperty("id").GetString()!;
            var expected = File.ReadAllBytes(Path.Combine(directory, id + ".transparent.browser.rgba"));
            bool MatchesHash(byte[] bytes, string property) => Convert.ToHexString(SHA256.HashData(bytes))
                .Equals(reference.GetProperty(property).GetString(), StringComparison.OrdinalIgnoreCase);
            if (!MatchesHash(source, "sourceSha256") || !MatchesHash(expected, "transparentRgbaSha256"))
                throw new InvalidDataException("Opacity folding source or independent reference changed.");
            var width = reference.GetProperty("width").GetInt32();
            var height = reference.GetProperty("height").GetInt32();
            if (expected.Length != checked(width * height * 4))
                throw new InvalidDataException("Invalid opacity folding reference extent.");
            using var stream = new MemoryStream(source);
            var actual = SvgManagedScene.Compile(XDocument.Load(stream)).Render(width, height);
            if (file is "single-gradient.svg" or "clipped-gradient.svg")
            {
                if (width != 64 || height != 64)
                    throw new InvalidDataException("The clip invariance fixture extent changed.");
                clipping.Add(file, (actual, expected));
                continue;
            }
            if (file is "nested-gradient.svg" or "overlapping-gradient.svg")
            {
                // This interior probe isolates layer color transfer from the remaining gradient interpolation differences.
                var pixel = (16 * width + 16) * 4;
                static int Premultiplied(byte channel, byte alpha) => (channel * alpha + 127) / 255;
                var matches = actual[pixel + 3] == expected[pixel + 3];
                for (var channel = 0; channel < 3; channel++)
                    matches &= Premultiplied(actual[pixel + channel], actual[pixel + 3]) ==
                        Premultiplied(expected[pixel + channel], expected[pixel + 3]);
                check(matches, "Native browser layer opacity preserves premultiplied interior color : " + file);
            }
            else if (file == "translucent-gradient.svg")
            {
                var alphaMatches = true;
                for (var pixel = 3; pixel < actual.Length; pixel += 4)
                    alphaMatches &= actual[pixel] == expected[pixel];
                check(alphaMatches, "Native browser varying-alpha gradient preserves every folded opacity byte");
            }
            else
                check(actual.AsSpan().SequenceEqual(expected), "Native browser full opacity RGBA : " + file);
        }
        if (selected != 7)
            throw new InvalidDataException("An independent opacity folding reference is missing or duplicated.");
        var plain = clipping["single-gradient.svg"];
        var clipped = clipping["clipped-gradient.svg"];
        var matchesInterior = true;
        // The authored clip spans [8.25,55.75); these pixels have full geometric coverage.
        for (var y = 9; y < 55; y++)
        {
            var offset = (y * 64 + 9) * 4;
            var length = 46 * 4;
            if (!plain.Expected.AsSpan(offset, length).SequenceEqual(clipped.Expected.AsSpan(offset, length)))
                throw new InvalidDataException("Native clip references no longer preserve their interior paint.");
            matchesInterior &= plain.Actual.AsSpan(offset, length).SequenceEqual(clipped.Actual.AsSpan(offset, length));
        }
        check(matchesInterior, "A single paint's opacity is unchanged throughout a geometric clip's fully covered interior");
        var actualPremultiplied = new byte[clipped.Actual.Length];
        var expectedPremultiplied = new byte[clipped.Expected.Length];
        Duxel.Core.UiRgba8.Premultiply(clipped.Actual, actualPremultiplied);
        Duxel.Core.UiRgba8.Premultiply(clipped.Expected, expectedPremultiplied);
        check(actualPremultiplied.AsSpan().SequenceEqual(expectedPremultiplied),
            "Native clipped gradient preserves every premultiplied RGBA byte including fractional edges");
    }
}
