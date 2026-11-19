using System.Text.Json;
using System.Security.Cryptography;
using Duxel.Core;

/// <summary>Exports the exact uploaded coverage geometry for independent browser comparison.</summary>
internal static class VectorGlyphBrowserFixture
{
    internal static byte[] LoadReference()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FontVectorBrowser");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reference.json")));
        var metadata = manifest.RootElement;
        if (metadata.GetProperty("width").GetInt32() != 400 || metadata.GetProperty("height").GetInt32() != 300)
            throw new InvalidDataException("Font browser reference requires the original 400x300 canvas.");
        foreach (var font in metadata.GetProperty("fonts").EnumerateArray())
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "FluentIcons", font.GetProperty("name").GetString()!);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!string.Equals(hash, font.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Font browser reference does not match the deployed font.");
        }
        var pixels = File.ReadAllBytes(Path.Combine(root, "reference.rgba"));
        if (pixels.Length != 400 * 300 * 4 || !string.Equals(
            Convert.ToHexString(SHA256.HashData(pixels)), metadata.GetProperty("rgbaSha256").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Font browser reference pixels are incomplete or changed.");
        return pixels;
    }

    internal static int CountDifferences(byte[] pixels, int width, int height, byte[] reference)
    {
        if (width < 170 || height < 65 || pixels.Length != width * height * 4)
            throw new InvalidDataException("Font browser capture does not contain the fixed reference region.");
        var differences = 0;
        for (var y = 25; y < 65; y++)
            for (var x = 20; x < 170; x++)
                if (!pixels.AsSpan((y * width + x) * 4, 4).SequenceEqual(reference.AsSpan((y * 400 + x) * 4, 4)))
                    differences++;
        return differences;
    }

    internal static void Write(UiDrawData data, string path)
    {
        using var file = File.Create(path);
        using var writer = new Utf8JsonWriter(file, new()
        {
            Indented = true
        });
        writer.WriteStartObject();
        writer.WriteStartArray("scenes");
        foreach (var update in data.TextureUpdates)
        {
            if (update.VectorScene is not { } scene)
                continue;
            writer.WriteStartObject();
            writer.WriteNumber("width", update.Width);
            writer.WriteNumber("height", update.Height);
            writer.WriteStartArray("data");
            foreach (var value in scene.Data.Span)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(value.X);
                writer.WriteNumberValue(value.Y);
                writer.WriteNumberValue(value.Z);
                writer.WriteNumberValue(value.W);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteStartArray("vertices");
        foreach (var list in data.DrawLists)
            foreach (var vertex in list.Vertices)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(vertex.Position.X);
                writer.WriteNumberValue(vertex.Position.Y);
                writer.WriteEndArray();
            }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
