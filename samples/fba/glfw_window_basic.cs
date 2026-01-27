#:property TargetFramework=net10.0
#:project ../../src/Dux.Platform.Glfw/Dux.Platform.Glfw.csproj

using System.Threading;
using Dux.Platform.Glfw;

var options = new GlfwPlatformBackendOptions(
    1280,
    720,
    "Dux GLFW Basic",
    true
);

using var platform = new GlfwPlatformBackend(options);

while (!platform.ShouldClose)
{
    platform.PollEvents();
    Thread.Sleep(1);
}
