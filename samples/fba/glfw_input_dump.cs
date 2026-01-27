#:property TargetFramework=net10.0
#:project ../../src/Dux.Platform.Glfw/Dux.Platform.Glfw.csproj

using System.Threading;
using Dux.Platform.Glfw;

var options = new GlfwPlatformBackendOptions(
    1280,
    720,
    "Dux GLFW Input Dump",
    true
);

using var platform = new GlfwPlatformBackend(options);

while (!platform.ShouldClose)
{
    platform.PollEvents();

    var snapshot = platform.Input.Snapshot;

    if (snapshot.KeyEvents.Count > 0)
    {
        foreach (var keyEvent in snapshot.KeyEvents)
        {
            Console.WriteLine($"Key {keyEvent.Key} {(keyEvent.IsDown ? "Down" : "Up")} Mods={keyEvent.Modifiers}");
        }
    }

    if (snapshot.CharEvents.Count > 0)
    {
        foreach (var charEvent in snapshot.CharEvents)
        {
            Console.WriteLine($"Char U+{charEvent.CodePoint:X4}");
        }
    }

    Thread.Sleep(1);
}
