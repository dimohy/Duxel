using System.Numerics;
using Duxel.Core;

internal static class GpuVectorRectangleContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        bool Accepts(Vector4 inner, float flags = 64, float count = 1, float offset = 6)
        {
            Vector4[] data = [new(0, 0, 32, 32), new(offset, count, 0, flags),
                new(0, 1, 0, 1), default, default, new(0, 0, 1, 0), inner];
            try
            {
                var scene = new UiGpuVectorScene(new(32, 32), data, 1);
                return scene.Strokes.Count == 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
        check(Accepts(new(8, 8, 24, 24)) && Accepts(default),
            "Analytic rectangular strokes accept a contained hole or solid center without generated stroke work");
        check(!Accepts(new(-1, 8, 24, 24)) && !Accepts(new(24, 8, 8, 24)) &&
            !Accepts(new(8, 8, 24, 24), flags: 65) && !Accepts(default, count: 2) &&
            !Accepts(default, offset: 7),
            "Rectangular stroke bounds, record count, flags and payload ranges reject before GPU submission");
    }
}
