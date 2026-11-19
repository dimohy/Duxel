using System.Numerics;
using Duxel.Core;
using Duxel.Svg;

internal static class GpuVectorDashContractTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var path = Path.Combine(output, "dash-contract.svg");
        File.WriteAllText(path, "<svg width='64' height='48'><path d='M4 36C15 -3 35 57 60 10' stroke='red' stroke-width='3' stroke-dasharray='4 3' fill='none'/></svg>");
        using var store = new SvgStore();
        var scene = store.Load(path).GpuScene;
        var source = scene.Data.ToArray();
        var start = scene.Strokes[0].Offset;
        var primitive = start + 3;
        var metadata = start + (int)source[primitive].X;
        var directory = metadata + 2 + (int)source[metadata].X;
        var payload = start + (int)source[directory].X;
        UiGpuVectorScene Create(Vector4[] data) => new(scene.Size, data, scene.CommandCount, scene.Filters,
            scene.TileOffset, scene.TileColumns, scene.TileRows, scene.Patterns);
        check(Create(source).Strokes.Count == 1 && store.Statistics.Rasterizations == 0,
            "GPU dash contract retains validated curves without CPU raster pixels");
        void Reject(string name, Action<Vector4[]> mutate)
        {
            var data = source.ToArray();
            mutate(data);
            var rejected = false;
            try
            {
                _ = Create(data);
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "GPU dash contract rejects before device submission : " + name);
        }
        Reject("metadata outside data", data => data[primitive].X = data.Length);
        Reject("empty pattern", data => data[metadata].X = 0);
        Reject("unknown cap", data => data[metadata].Y = 3);
        Reject("negative dash", data => data[metadata + 2].X = -1);
        Reject("zero period", data => { data[metadata + 2].X = 0; data[metadata + 3].X = 0; });
        Reject("path outside directory", data => data[primitive].Y = 1);
        Reject("curve payload outside data", data => data[directory].X = data.Length);
        Reject("curve span outside data", data => data[payload].X = data.Length);
        Reject("missing capacity plan", data => data[start + 2].W = 0);
    }
}
