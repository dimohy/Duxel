using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class CoverageOracleTests
{
    internal static void Run(Action<bool, string> check, string group = "Coverage")
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", group);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            var source = File.ReadAllBytes(Path.Combine(directory, name + ".svg"));
            var expected = File.ReadAllBytes(Path.Combine(directory, name + ".browser.rgba"));
            if (Convert.ToHexString(SHA256.HashData(source)) != item.GetProperty("sourceSha256").GetString() ||
                Convert.ToHexString(SHA256.HashData(expected)) != item.GetProperty("rgbaSha256").GetString())
                throw new InvalidDataException("Independent coverage oracle bytes changed: " + name);
            var width = item.GetProperty("width").GetInt32();
            var height = item.GetProperty("height").GetInt32();
            if (expected.Length != checked(width * height * 4))
                throw new InvalidDataException("Invalid coverage oracle dimensions.");
            using var stream = new MemoryStream(source);
            var actual = SvgManagedScene.Compile(XDocument.Load(stream)).Render(width, height);
            check(actual.AsSpan().SequenceEqual(expected), "Full RGBA matches independently captured browser fixture: " + name);
        }
    }
}
