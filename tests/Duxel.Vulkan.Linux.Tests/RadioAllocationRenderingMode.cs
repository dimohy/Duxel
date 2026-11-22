using System.Runtime.InteropServices;

internal static partial class RadioAllocationRenderingTests
{
    internal static void RunMode(string output, int slots)
    {
        Directory.CreateDirectory(output);
        using var fixture = new PresentationReuseTests.Fixture(slots, reuse: true);
        foreach (var samples in new[] { 1, 4 })
        {
            fixture.Renderer.SetMsaaSamples(samples);
            Run((name, draw) =>
            {
                var pixels = fixture.Capture(draw);
                fixture.WriteCapturePng(Path.Combine(output, $"msaa{samples}-{name}.png"), pixels);
                return pixels;
            }, (condition, name) =>
            {
                if (!condition)
                    throw new InvalidOperationException($"MSAA {samples}: {name}");
                Console.WriteLine($"PASS MSAA {samples}: {name}");
            });
        }
        Console.WriteLine(RuntimeInformation.FrameworkDescription);
    }
}
