using System.Numerics;
using Duxel.Core;
using Duxel.Svg;

internal static class GpuVectorConicContractTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var store = new SvgStore();
        UiGpuVectorScene Load(string name, string style)
        {
            var path = Path.Combine(output, "conic-contract-" + name + ".svg");
            File.WriteAllText(path, "<svg width='64' height='64'><path d='M8 32A24 18 0 0 1 56 32' " + style + "/></svg>");
            return store.Load(path).GpuScene;
        }
        bool Accepts(UiGpuVectorScene source, Action<Vector4[]> mutate)
        {
            var data = source.Data.ToArray();
            mutate(data);
            try
            {
                _ = new UiGpuVectorScene(source.Size, data, source.CommandCount, source.Filters,
                    source.TileOffset, source.TileColumns, source.TileRows, source.Patterns);
                return true;
            }
            catch (ArgumentException) { return false; }
        }
        var fill = Load("fill", "fill='red'");
        var fillData = fill.Data.ToArray();
        var command = Enumerable.Range(0, fill.CommandCount).First(index =>
            ((int)fillData[index * 6 + 1].W & 4) != 0 && ((int)fillData[index * 6 + 1].W & 8) == 0) * 6;
        var intervals = (int)fillData[command + 1].X + 2;
        check(Accepts(fill, _ => { }), "GPU conic contract / canonical rational fills validate without CPU rasterization");
        check(!Accepts(fill, data => data[intervals].X = 2), "GPU conic contract / unknown fill curve kind rejects before GPU work");
        check(!Accepts(fill, data => data[intervals].W = 3), "GPU conic contract / excessive monotone roots reject before GPU work");
        check(!Accepts(fill, data => { data[intervals].W = 1; data[intervals].Y = 2; }),
            "GPU conic contract / out-of-range monotone roots reject before GPU work");
        var stroke = Load("stroke", "fill='none' stroke='red' stroke-width='3'");
        var strokeData = stroke.Data.ToArray();
        var first = stroke.Strokes[0].Offset + 3;
        var primitive = Enumerable.Range(0, stroke.Strokes[0].PrimitiveCount).First(index => strokeData[first + index * 5 + 2].X == 7);
        var weights = first + primitive * 5 + 4;
        check(Accepts(stroke, _ => { }), "GPU conic contract / retained rational stroke primitives validate");
        check(!Accepts(stroke, data => data[weights].Y = 0), "GPU conic contract / zero stroke weight rejects before GPU work");
        check(!Accepts(stroke, data => data[weights].X = -1), "GPU conic contract / negative endpoint weight rejects before GPU work");
        var dash = Load("dash", "fill='none' stroke='red' stroke-width='3' stroke-dasharray='4 3'");
        var dashData = dash.Data.ToArray();
        var start = dash.Strokes[0].Offset;
        var metadata = start + (int)dashData[start + 3].X;
        var directory = metadata + 2 + (int)dashData[metadata].X;
        var payload = start + (int)dashData[directory].X;
        var curveKind = payload + 3;
        check(Accepts(dash, _ => { }) && store.Statistics.Rasterizations == 0,
            "GPU conic contract / dash source curves preserve weights without CPU pixels");
        check(!Accepts(dash, data => data[curveKind].X = 3), "GPU conic contract / unknown dash curve kind rejects before GPU work");
        check(!Accepts(dash, data => data[curveKind].Z = 0), "GPU conic contract / missing dash control weight rejects before GPU work");
    }
}
