using System.Runtime.InteropServices;

internal static unsafe partial class DirectWriteLayoutOracle
{
    internal readonly record struct HitProbe(float X, int Position, float CaretX, bool Trailing = false);
    internal readonly record struct RangeProbe(int Start, int End, (float Left, float Width)[] Fragments);
    internal static float TrailingCaretPosition(string text, int sourceEnd, Action<nint>? configureLayout = null)
    {
        var result = 0f;
        Draw("Segoe UI", text, false, layout =>
        {
            float x = 0, y = 0;
            HitMetrics metrics = default;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, HitMetrics*, int>)Method(layout, 65))
                (layout, checked((uint)(sourceEnd - 1)), 1, &x, &y, &metrics));
            result = x;
        }, 14, configureLayout);
        return result;
    }

    internal static (HitProbe[] Hits, RangeProbe[] Ranges) GeometryProbes(string text, Action<nint>? configureLayout = null)
    {
        var carets = CaretPositions("Segoe UI", text, 14, configureLayout);
        var hits = new List<HitProbe>();
        var ranges = new List<RangeProbe>();
        Draw("Segoe UI", text, false, layout =>
        {
            for (var x = .25f; x < carets.Max(); x += 1.25f)
            {
                int trailing = 0, inside = 0;
                HitMetrics metrics = default;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, float, float, int*, int*, HitMetrics*, int>)Method(layout, 64))
                    (layout, x, 1, &trailing, &inside, &metrics));
                var position = checked((int)(metrics.Position + (trailing != 0 ? metrics.Length : 0)));
                var right = ((metrics.BidiLevel & 1) == 0) == (trailing != 0);
                hits.Add(new(x, position, metrics.Left + (right ? metrics.Width : 0), trailing != 0));
            }
            for (var start = 0; start < text.Length; start++)
                for (var end = start + 1; end <= text.Length; end++)
                {
                    uint count = 0;
                    var status = ((delegate* unmanaged[Stdcall]<nint, uint, uint, float, float, HitMetrics*, uint, uint*, int>)Method(layout, 66))
                        (layout, (uint)start, (uint)(end - start), 0, 0, null, 0, &count);
                    if (status != unchecked((int)0x8007007A))
                        Marshal.ThrowExceptionForHR(status);
                    var metrics = new HitMetrics[count];
                    fixed (HitMetrics* output = metrics)
                        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, uint, float, float, HitMetrics*, uint, uint*, int>)Method(layout, 66))
                            (layout, (uint)start, (uint)(end - start), 0, 0, output, count, &count));
                    ranges.Add(new(start, end, metrics.Select(metric => (metric.Left, metric.Width)).ToArray()));
                }
        }, 14, configureLayout);
        return (hits.ToArray(), ranges.ToArray());
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HitMetrics
    {
        internal uint Position, Length;
        internal float Left, Top, Width, Height;
        internal uint BidiLevel;
        internal int IsText, IsTrimmed;
    }

    // The system layout is independent of the product's prefix-width implementation.
    internal static float[] CaretPositions(string family, string text, float size, Action<nint>? configureLayout = null)
    {
        var positions = new float[text.Length + 1];
        Draw(family, text, false, layout =>
        {
            for (var index = 0; index <= text.Length; index++)
            {
                float x = 0, y = 0;
                HitMetrics metrics = default;
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, int, float*, float*, HitMetrics*, int>)Method(layout, 65))
                    (layout, (uint)index, 0, &x, &y, &metrics));
                positions[index] = x;
            }
        }, size, configureLayout);
        return positions;
    }
}
