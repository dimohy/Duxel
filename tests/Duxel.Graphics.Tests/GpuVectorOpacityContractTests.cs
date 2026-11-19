using Duxel.Core;

internal static class GpuVectorOpacityContractTests
{
    internal static void Run(UiGpuVectorScene scene, Action<bool, string> check)
    {
        var original = scene.Data.ToArray();
        var command = Enumerable.Range(0, scene.CommandCount)
            .First(index => original[index * 6 + 1].Z == 1) * 6;
        bool Accepts(float flags)
        {
            var data = original.ToArray();
            data[command + 3].W = flags;
            try
            {
                _ = new UiGpuVectorScene(scene.Size, data, scene.CommandCount, scene.Filters,
                    scene.TileOffset, scene.TileColumns, scene.TileRows, scene.Patterns);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
        check(new float[] { 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14 }.All(Accepts),
            "Vector material accepts independent spread, dither and element-opacity fields");
        check(new float[] { -1, 3, 7, 11, 15, 16, 12.5f }.All(flags => !Accepts(flags)),
            "Vector material rejects unknown spread, reserved bits and fractional flags before submission");
    }
}
