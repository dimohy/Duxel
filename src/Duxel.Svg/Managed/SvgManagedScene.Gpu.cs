using System.Numerics;

namespace Duxel.Svg.Managed;

internal sealed record SvgGpuFilter(SvgGpuScene Scene, Vector4 Bounds, Vector2 Horizontal, Vector2 Vertical, bool LinearRgb);
internal sealed record SvgGpuPattern(SvgGpuScene Scene, Matrix3x2 Mapping);
internal sealed record SvgGpuScene(float Width, float Height, Vector4[] Data, int CommandCount, SvgGpuFilter[] Filters,
    int TileOffset, int TileColumns, int TileRows, SvgGpuPattern[] Patterns);
internal readonly record struct SvgGpuPaintData(int Kind, Vector4 Field, Vector4 Parameters,
    Matrix3x2 Coordinates, SvgGradientStop[] Stops, float Opacity = 1, SvgGpuScene? Pattern = null, SvgDecodedImage? Image = null);

internal sealed partial class SvgManagedScene
{
    private sealed class GpuFilterPaint(int index) : ISvgPaint
    {
        public Vector4 Sample(Vector2 point) => throw new InvalidOperationException("A GPU filter cannot be sampled on the CPU.");
        public SvgGpuPaintData CompileGpu() => new(5, default, new(index, 0, 0, 0), Matrix3x2.Identity, []);
    }
    internal SvgGpuScene CompileGpu()
    {
        List<Vector4[]> commands = [];
        List<SvgGpuFilter> filters = [];
        List<SvgGpuPattern> patterns = [];
        List<(int Command, SvgGpuCurve[] Edges, SvgGpuPaintData Paint, SvgAnalyticRRect? Analytic, Matrix3x2 Frame, SvgGpuStrokeProgram? Stroke, SvgAnalyticStroke? Ring)> paths = [];
        void Operation(float kind, float opacity = 1, Vector4? bounds = null)
        {
            var command = new Vector4[6];
            command[5] = new(0, 0, opacity, kind);
            if (bounds is { } rectangle)
            {
                command[0] = rectangle;
                command[1].X = 1;
            }
            commands.Add(command);
        }
        void AddPath(SvgGeometry geometry, ISvgPaint paint, bool evenOdd, Matrix3x2 transform, SvgGpuStrokeProgram? stroke = null,
            SvgAnalyticStroke? ring = null)
        {
            var edges = SvgGpuCurve.Compile(geometry, transform);
            if (edges.Length == 0 && stroke is null && geometry.Analytic is null ||
                stroke is { Rectangle: null } && stroke.Data[1].W == 0)
                return;
            var color = paint.CompileGpu();
            if (!Matrix3x2.Invert(transform, out var inverse))
                return;
            color = color with
            {
                Coordinates = inverse * color.Coordinates
            };
            var min = new Vector2(float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity);
            foreach (var edge in edges)
            {
                min = Vector2.Min(min, edge.Minimum);
                max = Vector2.Max(max, edge.Maximum);
            }
            if (geometry.Analytic is { } analytic)
            {
                var analyticBox = analytic.Rectangle;
                var analyticFrame = analytic.Frame * transform;
                min = new(float.PositiveInfinity);
                max = new(float.NegativeInfinity);
                Vector2[] corners = [new(analyticBox.X, analyticBox.Y), new(analyticBox.X + analyticBox.Z, analyticBox.Y),
                    new(analyticBox.X + analyticBox.Z, analyticBox.Y + analyticBox.W), new(analyticBox.X, analyticBox.Y + analyticBox.W)];
                foreach (var point in corners)
                {
                    var mapped = Vector2.Transform(point, analyticFrame);
                    min = Vector2.Min(min, mapped);
                    max = Vector2.Max(max, mapped);
                }
            }
            var command = new Vector4[6];
            command[0] = new(min.X, min.Y, max.X, max.Y);
            var rectangle = edges.Length == 4 && edges.All(edge => edge.Intervals.X == 0 &&
                (edge.Start.X == edge.End.X || edge.Start.Y == edge.End.Y)) &&
                edges.Select(edge => edge.Start).Distinct().Count() == 4;
            command[1] = new(0, edges.Length, color.Kind, (evenOdd ? 1 : 0) + 4 +
                (geometry.Analytic is not null ? 8 : rectangle ? 16 : 0));
            if (stroke is not null)
            {
                command[0] = stroke.Bounds;
                command[1].W = stroke.Rectangle is not null ? 64 : stroke.IsRectangle ? 16 : 32;
            }
            if (ring is not null)
                command[1].W = 128;
            command[2] = color.Field;
            command[3] = color.Parameters;
            command[4] = new(color.Coordinates.M11, color.Coordinates.M12, color.Coordinates.M21, color.Coordinates.M22);
            command[5] = new(color.Coordinates.M31, color.Coordinates.M32, color.Opacity, 0);
            paths.Add((commands.Count, edges, color, geometry.Analytic, geometry.Analytic is { } shape ? shape.Frame * transform : default, stroke, ring));
            commands.Add(command);
        }
        void Visit(Node node, Matrix3x2 parent)
        {
            if (!node.Visible || node.Opacity == 0)
                return;
            var transform = node.Transform * parent;
            if (transform.GetDeterminant() == 0)
                return;
            var group = node.Opacity != 1 || node.Clip is not null || node.Mask is not null;
            if (group)
                Operation(1, bounds: node.Opacity != 1 && node.Filter is null ? PaintBounds(node, parent) : null);
            if (node.Filter is { } filter)
            {
                var bounds = TransformBounds(filter.Region.Bounds, transform);
                if (bounds.Z > 0 && bounds.W > 0)
                {
                    var source = new SvgManagedScene(node with
                    {
                        Transform = Matrix3x2.Identity,
                        Filter = null,
                        Clip = null,
                        Mask = null,
                        Opacity = 1
                    },
                        Width, Height, transform).CompileGpu();
                    var index = filters.Count;
                    filters.Add(new(source, bounds, new Vector2(transform.M11, transform.M12) * filter.Deviation.X,
                        new Vector2(transform.M21, transform.M22) * filter.Deviation.Y, filter.LinearRgb));
                    var rectangle = new SvgGeometry([new(SvgSegmentKind.Move, new(bounds.X, bounds.Y)),
                        new(SvgSegmentKind.Line, new(bounds.X + bounds.Z, bounds.Y)), new(SvgSegmentKind.Line, new(bounds.X + bounds.Z, bounds.Y + bounds.W)),
                        new(SvgSegmentKind.Line, new(bounds.X, bounds.Y + bounds.W)), new(SvgSegmentKind.Close, new(bounds.X, bounds.Y))]);
                    AddPath(rectangle, new GpuFilterPaint(index), false, Matrix3x2.Identity);
                }
            }
            else
            {
                if (node.Fill is not null)
                    AddPath(node.Fill, node.FillColor!, node.EvenOdd, transform);
                if (node.Stroke is not null)
                {
                    var outline = node.Stroke.GpuSource;
                    if (node.Stroke.GpuRing is { } ring)
                        AddPath(outline.WithAnalytic(ring.Outer), node.StrokeColor!, false, transform, ring: ring);
                    else
                        AddPath(outline, node.StrokeColor!, false, transform,
                            outline.Analytic is null ? node.Stroke.CompileGpu(transform) : null);
                }
                foreach (var child in node.Children)
                    Visit(child, transform);
            }
            void ApplyMask(Node mask, bool luminance, bool preserveSamples = false)
            {
                Operation(1);
                Visit(mask, transform);
                Operation(preserveSamples ? 3 : luminance ? 4 : 6);
            }
            Vector4? compositeClip = null;
            if (TryCompositeClip(node, transform, out var clipRectangle))
                compositeClip = clipRectangle;
            if (node.Clip is not null && compositeClip is null)
                ApplyMask(node.Clip, false, preserveSamples: true);
            if (node.Mask is not null)
                ApplyMask(node.Mask, node.LuminanceMask);
            if (group)
                Operation(2, node.Opacity, compositeClip);
        }
        Visit(_root, _view);
        Operation(5);
        var commandCount = commands.Count;
        var data = new List<Vector4>(commandCount * 6);
        foreach (var command in commands)
            data.AddRange(command);
        void AppendCurve(SvgGpuCurve curve)
        {
            data.Add(new(curve.Start.X, curve.Start.Y, curve.Control1.X, curve.Control1.Y));
            data.Add(new(curve.Control2.X, curve.Control2.Y, curve.End.X, curve.End.Y));
            data.Add(curve.Intervals);
        }
        var strokeIndex = 0;
        foreach (var (index, edges, paint, analytic, frame, stroke, ring) in paths)
        {
            var header = index * 6;
            var specification = data[header + 1];
            specification.X = data.Count;
            data[header + 1] = specification;
            var bounds = data[header];
            if (ring is not null)
            {
                void Shape(SvgAnalyticRRect shape)
                {
                    data.Add(shape.Rectangle);
                    data.Add(new(shape.Radii.X, shape.Radii.Y, frame.M11, frame.M12));
                    data.Add(new(frame.M21, frame.M22, frame.M31, frame.M32));
                }
                Shape(ring.Outer);
                if (ring.Inner is { } inner)
                    Shape(inner);
                specification.Y = ring.Inner is null ? 1 : 2;
                data[header + 1] = specification;
            }
            else if (stroke is not null)
            {
                if (stroke.Rectangle is { } rectangleStroke)
                {
                    var inner = rectangleStroke.Inner ?? Vector4.Zero;
                    data.Add(new(inner.X, inner.Y, inner.X + inner.Z, inner.Y + inner.W));
                    specification.Y = 1;
                }
                else if (stroke.IsRectangle)
                    specification.Y = 0;
                else
                {
                    data.AddRange(stroke.Data);
                    specification.Y = strokeIndex++;
                }
                data[header + 1] = specification;
            }
            else if (analytic is { } shape)
            {
                data.Add(shape.Rectangle);
                data.Add(new(shape.Radii.X, shape.Radii.Y, frame.M11, frame.M12));
                data.Add(new(frame.M21, frame.M22, frame.M31, frame.M32));
                specification.Y = 1;
                data[header + 1] = specification;
            }
            else if (edges.Length > 32 && bounds.W > bounds.Y)
            {
                const int bands = 64;
                var table = data.Count;
                for (var band = 0; band < bands; band++)
                    data.Add(default);
                for (var band = 0; band < bands; band++)
                {
                    var top = bounds.Y + (bounds.W - bounds.Y) * band / bands;
                    var bottom = bounds.Y + (bounds.W - bounds.Y) * (band + 1) / bands;
                    var start = data.Count;
                    foreach (var edge in edges)
                    {
                        if (edge.Maximum.Y < top || edge.Minimum.Y > bottom)
                            continue;
                        AppendCurve(edge);
                    }
                    data[table + band] = new(start, (data.Count - start) / 3, 0, 0);
                }
                specification.Y = bands;
                specification.W += 2;
                data[header + 1] = specification;
            }
            else
                foreach (var edge in edges)
                    AppendCurve(edge);
            if (paint.Pattern is { } pattern)
            {
                if (!Matrix3x2.Invert(paint.Coordinates, out var mapping))
                    throw new InvalidDataException("A GPU pattern requires an invertible mapping.");
                var patternParameters = data[header + 3];
                patternParameters.X = filters.Count + patterns.Count;
                data[header + 3] = patternParameters;
                patterns.Add(new(pattern, mapping));
            }
            if (paint.Kind is 0 or 3 or 5)
                continue;
            var parameters = data[header + 3];
            if (paint.Image is { } image)
            {
                parameters.Z = data.Count;
                data[header + 3] = parameters;
                for (var i = 0; i < image.Rgba.Length; i += 4)
                {
                    var color = new Vector4(image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]) / 255;
                    data.Add(SvgCompositing.Premultiply(color));
                }
                continue;
            }
            parameters.Y = data.Count;
            parameters.Z = paint.Stops.Length;
            data[header + 3] = parameters;
            foreach (var stop in paint.Stops)
            {
                data.Add(new(stop.Offset, 0, 0, 0));
                data.Add(stop.Color);
            }
        }
        var tileOffset = 0;
        var columns = 0;
        var rows = 0;
        if (commands.Count > 32)
        {
            columns = rows = 16;
            var added = new List<Vector4>();
            var starts = new int[columns * rows];
            for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    starts[row * columns + column] = commandCount + added.Count / 6;
                    var left = Width * column / columns;
                    var top = Height * row / rows;
                    var right = Width * (column + 1) / columns;
                    var bottom = Height * (row + 1) / rows;
                    var selected = new List<int>();
                    var groups = new Stack<(int Start, bool Paint)>();
                    void Painted()
                    {
                        if (groups.Count == 0)
                            return;
                        var parent = groups.Pop();
                        groups.Push((parent.Start, true));
                    }
                    for (var i = 0; i < commands.Count; i++)
                    {
                        var header = i * 6;
                        var bounds = data[header];
                        var operation = data[header + 5].W;
                        if (operation == 0)
                        {
                            if (((int)data[header + 1].W & 8) == 0 &&
                                (bounds.X > right || bounds.Z < left || bounds.Y > bottom || bounds.W < top))
                                continue;
                            Painted();
                        }
                        if (operation == 1)
                            groups.Push((selected.Count, false));
                        if (operation is 2 or 3 or 4 or 6)
                        {
                            var group = groups.Pop();
                            if (operation == 2 && !group.Paint)
                            {
                                selected.RemoveRange(group.Start, selected.Count - group.Start);
                                continue;
                            }
                            if (operation == 2)
                                Painted();
                        }
                        selected.Add(i);
                    }
                    foreach (var selectedCommand in selected)
                        for (var slot = 0; slot < 6; slot++)
                            added.Add(data[selectedCommand * 6 + slot]);
                }
            data.InsertRange(commandCount * 6, added);
            commandCount += added.Count / 6;
            var relocatedBands = new HashSet<int>();
            for (var i = 0; i < commandCount; i++)
            {
                var header = i * 6;
                if (data[header + 5].W != 0)
                    continue;
                var specification = data[header + 1];
                specification.X += added.Count;
                data[header + 1] = specification;
                if (((int)specification.W & 2) != 0 && relocatedBands.Add((int)specification.X))
                    for (var band = 0; band < (int)specification.Y; band++)
                    {
                        var entry = data[(int)specification.X + band];
                        entry.X += added.Count;
                        data[(int)specification.X + band] = entry;
                    }
                var parameters = data[header + 3];
                if (specification.Z is 1 or 2)
                    parameters.Y += added.Count;
                if (specification.Z == 4)
                    parameters.Z += added.Count;
                data[header + 3] = parameters;
            }
            tileOffset = data.Count;
            foreach (var start in starts)
                data.Add(new(start, 0, 0, 0));
        }
        return new(Width, Height, data.ToArray(), commandCount, filters.ToArray(), tileOffset, columns, rows, patterns.ToArray());
    }
}
