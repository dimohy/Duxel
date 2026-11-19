namespace Duxel.Core;

// Gaussian convolution of analytic rounded-rectangle horizontal intervals.
// Straight edges integrate exactly; corner intervals use 16-point Gauss-Legendre quadrature.
internal sealed class UiShadowRasterSource
{
    private readonly UiVector2 _body;
    private readonly float _radius;
    private readonly float _resolution;
    private readonly UiBoxShadowOptions _options;
    private readonly UiRect _paint;
    private static readonly (double Point, double Weight)[] Quadrature = CreateQuadrature();
    internal int Width
    {
        get;
    }
    internal int Height
    {
        get;
    }

    internal UiShadowRasterSource(UiVector2 body, float radius, float resolution, UiBoxShadowOptions options)
    {
        if (!float.IsFinite(resolution) || resolution <= 0)
            throw new ArgumentOutOfRangeException(nameof(resolution));
        (_body, _radius, _resolution, _options) = (body, radius, resolution, options);
        _paint = options.PaintBounds(new(0, 0, body.X, body.Y));
        Width = checked((int)MathF.Ceiling(_paint.Width * resolution));
        Height = checked((int)MathF.Ceiling(_paint.Height * resolution));
        if (Width <= 0 || Height <= 0 || (long)Width * Height > 4 * 1024 * 1024)
            throw new InvalidOperationException("A shadow raster must fit the 4,194,304-pixel preparation budget.");
    }
    internal bool Matches(UiVector2 body, float radius, float resolution, UiBoxShadowOptions options) =>
        _body == body && _radius == radius && _resolution == resolution && _options == options;

    internal UiSvgRaster Rasterize()
    {
        var pixels = new byte[checked(Width * Height * 4)];
        var radius = Math.Min(_radius, Math.Min(_body.X, _body.Y) * .5);
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var localX = _paint.X + (x + .5) / _resolution;
                var localY = _paint.Y + (y + .5) / _resolution;
                var outside = Math.Clamp(.5 + Distance(localX, localY, radius) * _resolution, 0, 1);
                if (outside == 0)
                    continue;
                var first = Alpha(_options.First, localX, localY, radius);
                var second = Alpha(_options.Second, localX, localY, radius) * (1 - first);
                var alpha = first + second;
                var a = (byte)Math.Clamp(Math.Round(alpha * outside * 255), 0, 255);
                if (a == 0)
                    continue;
                var offset = (y * Width + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    var one = (_options.First.Color.Rgba >> (channel * 8)) & 255;
                    var two = (_options.Second.Color.Rgba >> (channel * 8)) & 255;
                    pixels[offset + channel] = (byte)Math.Clamp(Math.Round((one * first + two * second) / alpha), 0, 255);
                }
                pixels[offset + 3] = a;
            }
        return new(Width, Height, pixels);
    }

    private double Distance(double x, double y, double radius)
    {
        var dx = Math.Abs(x - _body.X * .5) - (_body.X * .5 - radius);
        var dy = Math.Abs(y - _body.Y * .5) - (_body.Y * .5 - radius);
        return Math.Sqrt(Math.Max(dx, 0) * Math.Max(dx, 0) + Math.Max(dy, 0) * Math.Max(dy, 0))
            + Math.Min(Math.Max(dx, dy), 0) - radius;
    }
    private double Alpha(UiShadowLayer layer, double x, double y, double radius)
    {
        if (!layer.Visible)
            return 0;
        x -= layer.Offset.X;
        y -= layer.Offset.Y;
        var sigma = layer.BlurRadius * .5;
        var coverage = sigma == 0 ? Math.Clamp(.5 - Distance(x, y, radius) * _resolution, 0, 1)
            : Convolve(x, y, radius, sigma);
        return coverage * (layer.Color.Rgba >> 24) / 255 * layer.Opacity;
    }
    private double Convolve(double x, double y, double radius, double sigma)
    {
        var support = sigma * 4;
        if (x < -support || x > _body.X + support || y < -support || y > _body.Y + support)
            return 0;
        var horizontal = Cdf((_body.X - x) / sigma) - Cdf(-x / sigma);
        var result = horizontal * (Cdf((_body.Y - radius - y) / sigma) - Cdf((radius - y) / sigma));
        if (radius == 0)
            return result;
        result += Corner(0, radius, false);
        result += Corner(_body.Y - radius, _body.Y, true);
        return Math.Clamp(result, 0, 1);

        double Corner(double start, double end, bool bottom)
        {
            var low = Math.Max(start, y - support);
            var high = Math.Min(end, y + support);
            if (high <= low)
                return 0;
            var mid = (low + high) * .5;
            var half = (high - low) * .5;
            var integral = 0d;
            foreach (var (point, weight) in Quadrature)
            {
                var row = mid + half * point;
                var cornerY = (bottom ? _body.Y - row : row) - radius;
                var inset = radius - Math.Sqrt(Math.Max(0, radius * radius - cornerY * cornerY));
                var across = Cdf((_body.X - inset - x) / sigma) - Cdf((inset - x) / sigma);
                var delta = (row - y) / sigma;
                integral += weight * across * Math.Exp(-.5 * delta * delta) / (2.5066282746310005 * sigma);
            }
            return integral * half;
        }
    }

    // Standard normal CDF approximation; maximum absolute error below 7.5e-8.
    private static double Cdf(double x)
    {
        var t = 1 / (1 + .2316419 * Math.Abs(x));
        var tail = Math.Exp(-x * x * .5) * .3989422804014327 * t
            * (.319381530 + t * (-.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
        return x >= 0 ? 1 - tail : tail;
    }
    private static (double Point, double Weight)[] CreateQuadrature()
    {
        var result = new (double, double)[16];
        for (var i = 0; i < 8; i++)
        {
            var root = Math.Cos(Math.PI * (i + .75) / 16.5);
            double derivative;
            while (true)
            {
                var previous = 1d;
                var current = root;
                for (var n = 2; n <= 16; n++)
                    (previous, current) = (current, ((2 * n - 1) * root * current - (n - 1) * previous) / n);
                derivative = 16 * (root * current - previous) / (root * root - 1);
                var next = root - current / derivative;
                if (Math.Abs(next - root) < 1e-14)
                {
                    root = next;
                    break;
                }
                root = next;
            }
            var weight = 2 / ((1 - root * root) * derivative * derivative);
            result[i] = (-root, weight);
            result[15 - i] = (root, weight);
        }
        return result;
    }
}
