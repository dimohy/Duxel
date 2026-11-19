using Duxel.Core;

internal static class FpsCounterTests
{
    internal static void Run(Action<bool, string> check)
    {
        var counter = new UiFpsCounter(3600);
        UiFpsSample sample = default;
        for (var frame = 0; frame < 10_000; frame++) sample = counter.Tick();
        check(sample.Frames == 10_000 && !sample.Updated,
            "wall-clock FPS counts every frame including multiple frames within one millisecond or clock tick");
        var explicitCounter = new UiFpsCounter(.25);
        for (var frame = 0; frame < 501; frame++)
        {
            var next = explicitCounter.Tick(.0005);
            if (next.Updated) sample = next;
        }
        check(sample.Updated && Math.Abs(sample.Fps - 2000) < .01f,
            "FPS samples can exceed 1000 without millisecond quantization");
        counter.Reset();
        check(counter.Tick(0).Frames == 0 && counter.CurrentFps == 0,
            "reset and invalid explicit zero-duration samples preserve the existing contract");
    }
}
