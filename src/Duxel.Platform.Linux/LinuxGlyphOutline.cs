using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Duxel.Core;
using static Duxel.Platform.Linux.LinuxFontNative;

namespace Duxel.Platform.Linux;

// HarfBuzz coordinates use the font's independent 26.6 scale. Native callbacks
// only build immutable contours; exceptions are rethrown after native unwinding.
internal sealed unsafe class LinuxGlyphOutline(float tolerance)
{
    private readonly List<List<UiVector2>> _contours = [];
    private readonly List<UiVectorCurve> _curves = [];
    private List<UiVector2> _current = [];
    private readonly double _toleranceSquared = (double)tolerance * tolerance;
    private ExceptionDispatchInfo? _failure;

    internal static nint CreateFunctions()
    {
        if (hb_version_atleast(7, 0, 0) == 0)
            throw new NotSupportedException("Vector glyph extraction requires HarfBuzz 7 or later.");
        var functions = hb_draw_funcs_create();
        if (functions == hb_draw_funcs_get_empty())
            throw new OutOfMemoryException("HarfBuzz draw-functions allocation failed.");
        hb_draw_funcs_set_move_to_func(functions, &MoveTo, null, null);
        hb_draw_funcs_set_line_to_func(functions, &LineTo, null, null);
        hb_draw_funcs_set_quadratic_to_func(functions, &QuadraticTo, null, null);
        hb_draw_funcs_set_cubic_to_func(functions, &CubicTo, null, null);
        hb_draw_funcs_set_close_path_func(functions, &ClosePath, null, null);
        hb_draw_funcs_make_immutable(functions);
        return functions;
    }

    internal readonly record struct Extraction(List<List<UiVector2>> Contours, UiVectorCurve[] Curves);
    internal static Extraction Extract(nint font, uint glyph, nint functions, float tolerance)
    {
        var outline = new LinuxGlyphOutline(tolerance);
        var handle = GCHandle.Alloc(outline);
        try
        {
            hb_font_draw_glyph(font, glyph, functions, GCHandle.ToIntPtr(handle));
            outline._failure?.Throw();
            if (outline._current.Count != 0)
                throw new InvalidOperationException("A native glyph outline ended without closing its contour.");
            return new(outline._contours, outline._curves.ToArray());
        }
        finally { handle.Free(); }
    }

    private static UiVector2 Point(float x, float y) => new(x / 64, -y / 64);
    private static UiVector2 Mid(UiVector2 a, UiVector2 b) => new((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);

    private static void Dispatch(nint data, int operation, UiVector2 a = default, UiVector2 b = default, UiVector2 c = default)
    {
        var outline = (LinuxGlyphOutline)GCHandle.FromIntPtr(data).Target!;
        if (outline._failure is not null)
            return;
        try
        {
            switch (operation)
            {
                case 0:
                    if (outline._current.Count != 0)
                        throw new InvalidOperationException("A glyph moved without closing its previous contour.");
                    outline._current.Add(a);
                    break;
                case 1:
                    outline._curves.Add(UiVectorCurve.Line(outline._current[^1], a));
                    outline._current.Add(a);
                    break;
                case 2:
                    var start = outline._current[^1];
                    outline._curves.Add(new(start, new(start.X + (a.X - start.X) * (2f / 3), start.Y + (a.Y - start.Y) * (2f / 3)),
                        new(b.X + (a.X - b.X) * (2f / 3), b.Y + (a.Y - b.Y) * (2f / 3)), b));
                    outline.Quadratic(outline._current[^1], a, b, 0);
                    break;
                case 3:
                    outline._curves.Add(new(outline._current[^1], a, b, c));
                    outline.Cubic(outline._current[^1], a, b, c, 0);
                    break;
                case 4:
                    var contour = outline._current;
                    if (contour.Count > 1 && contour[0] != contour[^1])
                        outline._curves.Add(UiVectorCurve.Line(contour[^1], contour[0]));
                    if (contour.Count > 1 && contour[0] == contour[^1])
                        contour.RemoveAt(contour.Count - 1);
                    if (contour.Count >= 3)
                        outline._contours.Add(contour);
                    outline._current = [];
                    break;
            }
        }
        catch (Exception error) { outline._failure = ExceptionDispatchInfo.Capture(error); }
    }

    private void Quadratic(UiVector2 a, UiVector2 b, UiVector2 c, int depth)
    {
        if (DistanceSquared(b, a, c) <= _toleranceSquared)
        {
            _current.Add(c);
            return;
        }
        if (depth == 32)
            throw new InvalidOperationException("Quadratic glyph flattening cannot reach the requested precision.");
        var ab = Mid(a, b);
        var bc = Mid(b, c);
        var center = Mid(ab, bc);
        Quadratic(a, ab, center, depth + 1);
        Quadratic(center, bc, c, depth + 1);
    }

    private void Cubic(UiVector2 a, UiVector2 b, UiVector2 c, UiVector2 d, int depth)
    {
        if (Math.Max(DistanceSquared(b, a, d), DistanceSquared(c, a, d)) <= _toleranceSquared)
        {
            _current.Add(d);
            return;
        }
        if (depth == 32)
            throw new InvalidOperationException("Cubic glyph flattening cannot reach the requested precision.");
        var ab = Mid(a, b);
        var bc = Mid(b, c);
        var cd = Mid(c, d);
        var left = Mid(ab, bc);
        var right = Mid(bc, cd);
        var center = Mid(left, right);
        Cubic(a, ab, left, center, depth + 1);
        Cubic(center, right, cd, d, depth + 1);
    }

    private static double DistanceSquared(UiVector2 point, UiVector2 a, UiVector2 b)
    {
        var dx = (double)b.X - a.X;
        var dy = (double)b.Y - a.Y;
        var length = dx * dx + dy * dy;
        var t = length == 0 ? 0 : Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy) / length, 0, 1);
        var x = point.X - a.X - t * dx;
        var y = point.Y - a.Y - t * dy;
        return x * x + y * y;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void MoveTo(nint functions, nint data, void* state, float x, float y, void* user) => Dispatch(data, 0, Point(x, y));
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void LineTo(nint functions, nint data, void* state, float x, float y, void* user) => Dispatch(data, 1, Point(x, y));
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void QuadraticTo(nint functions, nint data, void* state, float cx, float cy, float x, float y, void* user) => Dispatch(data, 2, Point(cx, cy), Point(x, y));
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CubicTo(nint functions, nint data, void* state, float ax, float ay, float bx, float by, float x, float y, void* user) => Dispatch(data, 3, Point(ax, ay), Point(bx, by), Point(x, y));
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ClosePath(nint functions, nint data, void* state, void* user) => Dispatch(data, 4);
}
