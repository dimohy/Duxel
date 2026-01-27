#:property TargetFramework=net10.0
#:project ../../src/Dux.Platform.Glfw/Dux.Platform.Glfw.csproj

using System.Threading;
using Dux.Platform.Glfw;

var options = new GlfwPlatformBackendOptions(
    1280,
    720,
    "Dux GLFW Timing",
    true
);

using var platform = new GlfwPlatformBackend(options);

var lastTime = platform.TimeSeconds;
var frames = 0;
var accumulated = 0.0;

while (!platform.ShouldClose)
{
    platform.PollEvents();

    var now = platform.TimeSeconds;
    var delta = now - lastTime;
    lastTime = now;

    accumulated += delta;
    frames++;

    if (accumulated >= 1.0)
    {
        var fps = frames / accumulated;
        Console.WriteLine($"FPS: {fps:0.0}");
        accumulated = 0;
        frames = 0;
    }

    Thread.Sleep(1);
}
