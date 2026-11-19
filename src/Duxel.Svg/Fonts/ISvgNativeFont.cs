using System.Numerics;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

internal readonly record struct SvgShapedGlyph(uint Glyph, uint Cluster, Vector2 Advance, Vector2 Offset, SvgGeometry Outline);
internal interface ISvgNativeFont : IDisposable
{
    int UnitsPerEm { get; }
    int Weight { get; }
    int Width { get; }
    bool Italic { get; }
    bool Oblique { get; }
    bool IsDisposed { get; }
    bool Contains(uint codepoint);
    SvgShapedGlyph[] Shape(string text, bool rightToLeft);
}
internal static class SvgNativeFont
{
    internal static ISvgNativeFont Create(byte[] bytes, int collectionIndex)
    {
        if (OperatingSystem.IsWindows()) return new SvgDirectWriteFont(bytes, collectionIndex);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) return new SvgHarfBuzzFont(bytes, collectionIndex);
        throw new PlatformNotSupportedException("SVG fonts require DirectWrite on Windows or HarfBuzz on Linux/macOS.");
    }
}
