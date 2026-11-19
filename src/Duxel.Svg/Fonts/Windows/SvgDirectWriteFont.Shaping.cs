using System.Numerics;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

internal sealed unsafe partial class SvgDirectWriteFont
{
    public SvgShapedGlyph[] Shape(string text, bool rightToLeft)
    {
        ObjectDisposedException.ThrowIf(_analyzer == 0, this);
        var shaped = Duxel.Interop.Windows.DirectWriteTextShaper.Shape(_factory, _analyzer, _face, text, UnitsPerEm, rightToLeft);
        return shaped.Select(glyph => new SvgShapedGlyph(glyph.Index, glyph.Cluster, new(glyph.Advance, 0),
            new(glyph.OffsetX, glyph.OffsetY), Outline(glyph.Index))).ToArray();
    }
    private SvgGeometry Outline(ushort glyph)
    {
        if (_outlines.TryGetValue(glyph, out var existing))
            return existing;
        var state = new OutlineState();
        var sink = Callback(state);
        try
        {
            var status = ((delegate* unmanaged[Stdcall]<nint, float, ushort*, float*, NativePoint*, uint, int, int, nint, int>)Method(_face, 14))
                (_face, UnitsPerEm, &glyph, null, null, 1, 0, 0, (nint)sink);
            state.Failure?.Throw();
            Check(status, "Read explicit glyph outline");
        }
        finally { ReleaseCallback(sink); }
        var geometry = new SvgGeometry(state.Segments.ToArray());
        _outlines.Add(glyph, geometry);
        return geometry;
    }
}
