using System.Numerics;

namespace Duxel.Core;

public sealed record UiGpuVectorFilter(UiGpuVectorScene Scene, UiRect Bounds,
    UiVector2 Horizontal, UiVector2 Vertical, bool LinearRgb);

public sealed record UiGpuVectorPattern(UiGpuVectorScene Scene, Matrix3x2 Mapping);

public readonly record struct UiGpuVectorStroke(int Offset, int PrimitiveCount, float MaximumDifference, float MaximumRadius, UiRect Bounds,
    int WorkFactor = 1);

/// <summary>A pixel window into a retained scene's virtual camera viewport. No full-size image allocation is required.</summary>
public readonly record struct UiGpuVectorCamera(UiVector2 FullPixelSize, UiVector2 PixelOrigin, bool Stretch = false)
{
    public void Validate()
    {
        if (!float.IsFinite(FullPixelSize.X) || !float.IsFinite(FullPixelSize.Y) || FullPixelSize.X <= 0 || FullPixelSize.Y <= 0
            || !float.IsFinite(PixelOrigin.X) || !float.IsFinite(PixelOrigin.Y)
            || PixelOrigin.X != MathF.Truncate(PixelOrigin.X) || PixelOrigin.Y != MathF.Truncate(PixelOrigin.Y))
            throw new ArgumentOutOfRangeException(nameof(FullPixelSize));
    }
}

/// <summary>Immutable vector commands and paint data uploaded once per renderer. Contains no raster pixels.</summary>
public sealed class UiGpuVectorScene
{
    public IReadOnlyList<UiGpuVectorStroke> Strokes
    {
        get;
    }
    public UiVector2 Size
    {
        get;
    }
    public ReadOnlyMemory<Vector4> Data
    {
        get;
    }
    public int CommandCount
    {
        get;
    }
    public int StackCapacity
    {
        get;
    }
    public IReadOnlyList<UiGpuVectorFilter> Filters
    {
        get;
    }
    public IReadOnlyList<UiGpuVectorPattern> Patterns
    {
        get;
    }
    public int TileOffset
    {
        get;
    }
    public int TileColumns
    {
        get;
    }
    public int TileRows
    {
        get;
    }

    public UiGpuVectorScene(UiVector2 size, ReadOnlySpan<Vector4> data, int commandCount, IReadOnlyList<UiGpuVectorFilter>? filters = null,
        int tileOffset = 0, int tileColumns = 0, int tileRows = 0, IReadOnlyList<UiGpuVectorPattern>? patterns = null)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (commandCount < 0 || (long)commandCount * 6 > data.Length || data.Length > 16_000_000)
            throw new ArgumentOutOfRangeException(nameof(commandCount));
        foreach (var value in data)
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) || !float.IsFinite(value.W))
                throw new ArgumentException("Vector scene data must be finite.", nameof(data));
        var depth = 0;
        var dataLength = data.Length;
        int Index(float value)
        {
            if (value < 0 || value > dataLength || value != MathF.Truncate(value))
                throw new ArgumentException("Vector scene indices must address the immutable data.", nameof(data));
            return (int)value;
        }
        var strokes = new SortedDictionary<int, UiGpuVectorStroke>();
        for (var i = 0; i < commandCount; i++)
        {
            var command = i * 6;
            var extra = data[command + 5];
            if (extra.Z < 0 || extra.Z > 1)
                throw new ArgumentException("Vector opacity must lie in the unit interval.", nameof(data));
            if (extra.W == 1)
            {
                if (++depth >= 64)
                    throw new ArgumentException("Vector compositing exceeds the shader stack.", nameof(data));
                continue;
            }
            if (extra.W == 5)
            {
                if (depth != 0)
                    throw new ArgumentException("A vector subprogram must balance its compositing groups.", nameof(data));
                continue;
            }
            if (extra.W is 2 or 3 or 4 or 6)
            {
                if (--depth < 0)
                    throw new ArgumentException("Unbalanced vector compositing group.", nameof(data));
                continue;
            }
            if (extra.W != 0)
                throw new ArgumentException("Unknown vector operation.", nameof(data));
            var specification = data[command + 1];
            var start = Index(specification.X);
            var count = Index(specification.Y);
            var flags = (int)specification.W;
            var stride = (flags & 4) != 0 ? 3 : 1;
            var banded = (flags & 2) != 0;
            if (((flags & 32) == 0 && (long)start + (long)count * (banded ? 1 : stride) > data.Length) || specification.Z is < 0 or > 5 || specification.Z != MathF.Truncate(specification.Z)
                || specification.W < 0 || specification.W > 255 || specification.W != flags)
                throw new ArgumentException("Invalid vector path or paint.", nameof(data));
            if ((flags & 128) != 0)
            {
                if (flags != 128 || count is < 1 or > 2 || start < commandCount * 6 || start + count * 3L > data.Length)
                    throw new ArgumentException("Analytic rings require one or two local rounded-shape records.", nameof(data));
                for (var shape = 0; shape < count; shape++)
                {
                    var at = start + shape * 3;
                    var rectangle = data[at];
                    var radii = data[at + 1];
                    var rest = data[at + 2];
                    var determinant = radii.Z * rest.Y - radii.W * rest.X;
                    if (rectangle.Z <= 0 || rectangle.W <= 0 || radii.X < 0 || radii.Y < 0 ||
                        radii.X > rectangle.Z / 2 || radii.Y > rectangle.W / 2 || determinant == 0 || !float.IsFinite(determinant))
                        throw new ArgumentException("Analytic ring shapes require valid radii and an invertible frame.", nameof(data));
                    if (shape == 1 && (rectangle.X < data[start].X || rectangle.Y < data[start].Y ||
                        rectangle.X + rectangle.Z > data[start].X + data[start].Z || rectangle.Y + rectangle.W > data[start].Y + data[start].W ||
                        radii.Z != data[start + 1].Z || radii.W != data[start + 1].W || rest != data[start + 2]))
                        throw new ArgumentException("Analytic ring holes must share the outer frame and contained bounds.", nameof(data));
                }
            }
            if ((flags & 64) != 0)
            {
                if (flags != 64 || count != 1 || start >= data.Length)
                    throw new ArgumentException("Rectangular vector strokes require one inner-bound record.", nameof(data));
                var outer = data[command];
                var inner = data[start];
                if (outer.Z <= outer.X || outer.W <= outer.Y || inner.Z < inner.X || inner.W < inner.Y ||
                    inner.Z > inner.X && inner.W > inner.Y &&
                    (inner.X < outer.X || inner.Y < outer.Y || inner.Z > outer.Z || inner.W > outer.W))
                    throw new ArgumentException("Invalid rectangular vector stroke bounds.", nameof(data));
            }
            if ((flags & 32) != 0)
            {
                if (flags != 32 || start + 3L > data.Length)
                    throw new ArgumentException("Invalid retained vector stroke header.", nameof(data));
                var primitiveCount = Index(data[start + 1].W);
                if (primitiveCount == 0 || start + 3L + primitiveCount * 5L > data.Length || data[start + 1].Z <= 0
                    || data[start + 2].X < 0 || data[start + 2].Y < 0)
                    throw new ArgumentException("Invalid retained vector stroke program.", nameof(data));
                for (var primitive = 0; primitive < primitiveCount; primitive++)
                {
                    var at = start + 3 + primitive * 5;
                    var kind = data[at + 2].X;
                    if (kind < 0 || kind > 7 || kind != MathF.Truncate(kind))
                        throw new ArgumentException("Unknown vector stroke primitive.", nameof(data));
                    if (kind == 7 && (data[at + 4].X <= 0 || data[at + 4].Y <= 0 || data[at + 4].Z <= 0))
                        throw new ArgumentException("Rational vector strokes require positive homogeneous weights.", nameof(data));
                    if (kind == 6)
                    {
                        if (data[start + 2].W < 1)
                            throw new ArgumentException("Vector dashes require a positive work factor.", nameof(data));
                        var metadata = checked(start + Index(data[at].X));
                        if (metadata + 2L > data.Length)
                            throw new ArgumentException("Invalid vector dash header.", nameof(data));
                        var style = data[metadata];
                        var options = data[metadata + 1];
                        var patternCount = Index(style.X);
                        var pathCount = Index(options.Z);
                        var directory = metadata + 2L + patternCount;
                        if (patternCount == 0 || patternCount % 2 != 0 || pathCount == 0 || directory + pathCount > data.Length
                            || style.Y is < 0 or > 2 || style.Y != MathF.Truncate(style.Y)
                            || style.Z is < 0 or > 2 || style.Z != MathF.Truncate(style.Z) || style.W < 1 || options.Y < 0
                            || Index(data[at].Y) >= pathCount)
                            throw new ArgumentException("Invalid vector dash style or directory.", nameof(data));
                        double period = 0;
                        for (var dash = 0; dash < patternCount; dash++)
                        {
                            var length = data[metadata + 2 + dash].X;
                            if (length < 0)
                                throw new ArgumentException("Vector dash lengths must be nonnegative.", nameof(data));
                            period += length;
                        }
                        if (period == 0)
                            throw new ArgumentException("Vector dash period must be positive.", nameof(data));
                        for (var path = 0; path < pathCount; path++)
                        {
                            var payload = checked(start + Index(data[(int)directory + path].X));
                            if (payload >= data.Length || payload + 1L + Index(data[payload].X) * 3L > data.Length
                                || data[payload].Y is not (0 or 1))
                                throw new ArgumentException("Invalid vector dash curve payload.", nameof(data));
                            for (var curve = 0; curve < Index(data[payload].X); curve++)
                            {
                                var curveKind = data[payload + 1 + curve * 3 + 2];
                                if (curveKind.X is not (0 or 1 or 2)
                                    || curveKind.X == 2 && (curveKind.Y <= 0 || curveKind.Z <= 0 || curveKind.W <= 0))
                                    throw new ArgumentException("Invalid rational vector dash curve.", nameof(data));
                            }
                        }
                    }
                }
                var workFactor = data[start + 2].W;
                if (workFactor < 0 || workFactor > 1_000_000 || workFactor != MathF.Truncate(workFactor))
                    throw new ArgumentException("Invalid vector stroke work factor.", nameof(data));
                var strokeBounds = data[command];
                var stroke = new UiGpuVectorStroke(start, primitiveCount, data[start + 2].X, data[start + 2].Y,
                    new(strokeBounds.X, strokeBounds.Y, strokeBounds.Z - strokeBounds.X, strokeBounds.W - strokeBounds.Y),
                    Math.Max(1, (int)workFactor));
                if (strokes.TryGetValue(count, out var existing) && existing != stroke)
                    throw new ArgumentException("Conflicting retained vector stroke identity.", nameof(data));
                strokes[count] = stroke;
            }
            if ((flags & 8) != 0 && ((flags & 4) == 0 || banded || (flags & 16) != 0 || count != 1))
                throw new ArgumentException("Analytic vector shapes require one local-frame record.", nameof(data));
            if (banded)
            {
                if (count == 0 || data[command].W <= data[command].Y)
                    throw new ArgumentException("Vector edge bands require a positive vertical range.", nameof(data));
                for (var band = 0; band < count; band++)
                {
                    if ((long)Index(data[start + band].X) + (long)Index(data[start + band].Y) * stride > data.Length)
                        throw new ArgumentException("Invalid vector edge band.", nameof(data));
                    if ((flags & 4) != 0)
                        ValidateCurveRecords(data, Index(data[start + band].X), Index(data[start + band].Y));
                }
            }
            else if ((flags & 4) != 0 && (flags & 8) == 0)
                ValidateCurveRecords(data, start, count);
            if (specification.Z is 1 or 2)
            {
                var gradient = data[command + 3];
                // Low two bits select spread; bits 2/3 enable dither/element-opacity storage.
                if ((long)Index(gradient.Y) + 2L * Index(gradient.Z) > data.Length
                    || gradient.W is < 0 or > 14 || ((int)gradient.W & 3) == 3 || gradient.W != MathF.Truncate(gradient.W))
                    throw new ArgumentException("Invalid vector gradient.", nameof(data));
            }
            if (specification.Z == 3 && (Index(data[command + 3].X) < (filters?.Count ?? 0)
                    || Index(data[command + 3].X) >= (filters?.Count ?? 0) + (patterns?.Count ?? 0)
                    || data[command + 2].Z <= 0 || data[command + 2].W <= 0))
                throw new ArgumentException("Vector patterns must reference a retained pattern surface.", nameof(data));
            if (specification.Z == 4)
            {
                var image = data[command + 3];
                if (Index(image.X) == 0 || Index(image.Y) == 0 || (long)Index(image.Z) + (long)Index(image.X) * Index(image.Y) > data.Length)
                    throw new ArgumentException("Invalid vector image.", nameof(data));
            }
            if (specification.Z == 5 && Index(data[command + 3].X) >= (filters?.Count ?? 0))
                throw new ArgumentException("Invalid vector filter dependency.", nameof(data));
        }
        if (depth != 0)
            throw new ArgumentException("Unbalanced vector compositing group.", nameof(data));
        if (!strokes.Keys.SequenceEqual(Enumerable.Range(0, strokes.Count)))
            throw new ArgumentException("Retained stroke identities must be contiguous.", nameof(data));
        Strokes = Array.AsReadOnly(strokes.Values.ToArray());
        if (tileOffset < 0 || tileColumns < 0 || tileRows < 0 || (long)tileOffset + (long)tileColumns * tileRows > data.Length
            || (tileColumns == 0) != (tileRows == 0))
            throw new ArgumentException("Invalid vector tile index.", nameof(tileOffset));
        for (var i = 0; i < tileColumns * tileRows; i++)
            if (Index(data[tileOffset + i].X) >= commandCount || Index(data[tileOffset + i].X) != 0
                && data[(Index(data[tileOffset + i].X) - 1) * 6 + 5].W != 5)
                throw new ArgumentException("Invalid vector tile program.", nameof(tileOffset));
        TileOffset = tileOffset;
        TileColumns = tileColumns;
        TileRows = tileRows;
        Size = size;
        var instructions = data.ToArray();
        Data = instructions;
        CommandCount = commandCount;
        Filters = Array.AsReadOnly(filters?.ToArray() ?? []);
        Patterns = Array.AsReadOnly(patterns?.ToArray() ?? []);
        foreach (var pattern in Patterns)
        {
            var m = pattern.Mapping;
            if (pattern.Scene is null || !float.IsFinite(m.M11) || !float.IsFinite(m.M12) || !float.IsFinite(m.M21)
                || !float.IsFinite(m.M22) || !float.IsFinite(m.M31) || !float.IsFinite(m.M32) || m.GetDeterminant() == 0)
                throw new ArgumentException("Vector patterns require a finite invertible mapping.", nameof(patterns));
        }
        foreach (var filter in Filters)
        {
            if (filter.Scene is null || !float.IsFinite(filter.Bounds.X) || !float.IsFinite(filter.Bounds.Y)
                || !float.IsFinite(filter.Bounds.Width) || !float.IsFinite(filter.Bounds.Height) || filter.Bounds.Width <= 0 || filter.Bounds.Height <= 0
                || !float.IsFinite(filter.Horizontal.X) || !float.IsFinite(filter.Horizontal.Y)
                || !float.IsFinite(filter.Vertical.X) || !float.IsFinite(filter.Vertical.Y))
                throw new ArgumentException("Vector filters require finite geometry and deviations.", nameof(filters));
        }
        var currentDepth = 0;
        var required = 1;
        for (var i = 0; i < commandCount; i++)
        {
            var operation = instructions[i * 6 + 5].W;
            if (operation == 1)
                required = Math.Max(required, ++currentDepth + 1);
            else if (operation is 2 or 3 or 4 or 6)
                currentDepth--;
        }
        var capacity = 1;
        while (capacity < required)
            capacity *= 2;
        StackCapacity = capacity;
    }

    private static void ValidateCurveRecords(ReadOnlySpan<Vector4> data, int start, int count)
    {
        for (var curve = 0; curve < count; curve++)
        {
            var intervals = data[start + curve * 3 + 2];
            if (intervals.X > 0 && intervals.X != 1 || intervals.W is < 0 or > 2 || intervals.W != MathF.Truncate(intervals.W)
                || intervals.W > 0 && intervals.Y is < 0 or > 1
                || intervals.W > 1 && (intervals.Z is < 0 or > 1 || intervals.Z < intervals.Y)
                || intervals.X == 0 && intervals.W != 0)
                throw new ArgumentException("Invalid retained curve kind or monotone intervals.", nameof(data));
        }
    }
}
