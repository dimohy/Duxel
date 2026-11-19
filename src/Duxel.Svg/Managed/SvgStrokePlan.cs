using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Retains authored stroke geometry and validated style; tessellation follows the requested pixel resolution.</summary>
internal sealed class SvgStrokePlan
{
    private readonly SvgGeometry _source;
    private readonly float _width;
    private readonly string _cap;
    private readonly string _join;
    private readonly float _miterLimit;
    private readonly SvgDashPattern? _pattern;
    private readonly float? _pathLength;

    internal SvgStrokePlan(SvgGeometry source, float width, string cap, string join, float miterLimit,
        SvgDashPattern? pattern, float? pathLength)
    {
        SvgStroke.Validate(width, cap, join, miterLimit);
        if (pathLength is <= 0)
            throw new NotSupportedException("SVG stroke pathLength must be positive.");
        (_source, _width, _cap, _join, _miterLimit, _pattern, _pathLength) =
            (source, width, cap, join, miterLimit, pattern, pathLength);
    }

    internal SvgGeometry Outline(Matrix3x2 transform)
    {
        var scale = SvgStrokeTessellation.DeviceScale(transform);
        var tolerance = (float)(.5 / scale);
        if (!float.IsFinite(tolerance) || tolerance <= 0)
            throw new InvalidDataException("SVG stroke resolution exceeds the geometric range.");
        return SvgStroke.Outline(_source, transform, _width, _cap, _join, _miterLimit, _pattern, _pathLength, tolerance);
    }

    internal SvgGpuStrokeProgram CompileGpu(Matrix3x2 transform) =>
        SvgGpuStrokeProgram.Compile(_source, transform, _width, _cap, _join, _miterLimit, _pattern, _pathLength);

    // Fill coverage metadata must not classify the authored centerline as a filled stroke.
    // Only the stroke's own analytic geometry can bypass GPU stroke construction.
    internal SvgGeometry GpuSource => _source.WithAnalytic(SvgStroke.Analytic(_source, _width, _cap, _pattern));
    internal SvgAnalyticStroke? GpuRing => SvgAnalyticStroke.Create(_source, _width, _pattern);
}
