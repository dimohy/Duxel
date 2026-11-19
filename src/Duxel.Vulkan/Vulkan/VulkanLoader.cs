using System.Reflection;
using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

public sealed unsafe partial class Vk
{
    private const string LibraryName = "Duxel.Vulkan.Loader";
    private static readonly Lazy<nint> Loader = new(static () =>
        NativeLibrary.Load(OperatingSystem.IsWindows() ? "vulkan-1.dll"
            : OperatingSystem.IsLinux() ? "libvulkan.so.1"
            : throw new PlatformNotSupportedException("Duxel's Vulkan loader supports Windows and Linux.")));

    static Vk() => NativeLibrary.SetDllImportResolver(typeof(Vk).Assembly, ResolveLibrary);

    private static nint ResolveLibrary(string name, Assembly assembly, DllImportSearchPath? searchPath) =>
        name == LibraryName ? Loader.Value : 0;
}
