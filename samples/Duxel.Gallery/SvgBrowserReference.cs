using Duxel.Core;

namespace Duxel.Gallery;

internal static class SvgBrowserReference
{
    internal static void Validate(string path, byte[] pixels, string sourceSha256, string rgbaSha256)
    {
        static bool Matches(byte[] bytes, string expected) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
            .Equals(expected, StringComparison.OrdinalIgnoreCase);
        if (!Matches(File.ReadAllBytes(path), sourceSha256) || !Matches(pixels, rgbaSha256))
            throw new InvalidDataException("Browser comparison artwork/reference changed : " + path);
    }
}
