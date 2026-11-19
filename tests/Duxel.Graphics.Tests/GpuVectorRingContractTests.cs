using System.Numerics;
using Duxel.Core;

internal static class GpuVectorRingContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        Vector4[] Data() => [new(0, 0, 32, 32), new(6, 2, 0, 128), new(0, 1, 0, 1),
            default, default, new(0, 0, 1, 0), new(0, 0, 32, 32), new(8, 8, 1, 0), new(0, 1, 0, 0),
            new(3, 3, 26, 26), new(5, 5, 1, 0), new(0, 1, 0, 0)];
        bool Accepts(Vector4[] data)
        {
            try
            {
                return new UiGpuVectorScene(new(32, 32), data, 1).Strokes.Count == 0;
            }
            catch (ArgumentException) { return false; }
        }
        var valid = Data();
        check(Accepts(valid), "Analytic rings / nested rounded shapes require no generated stroke work");
        var solid = Data();
        solid[1].Y = 1;
        check(Accepts(solid), "Analytic rings / a thick stroke with no hole keeps one closed outer shape");
        foreach (var (index, field, value) in new[] { (1, 0, 7f), (1, 1, 3f), (1, 3, 129f),
            (7, 0, -1f), (7, 0, 17f), (7, 2, 0f), (9, 0, -1f), (11, 3, 1f) })
        {
            var invalid = Data();
            invalid[index][field] = value;
            check(!Accepts(invalid), $"Analytic rings / malformed payload rejects before GPU submission at {index},{field}");
        }
    }
}
