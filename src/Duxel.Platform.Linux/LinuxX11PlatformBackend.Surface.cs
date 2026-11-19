using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxX11PlatformBackend
{
    private sealed class SurfaceSource(LinuxX11PlatformBackend owner) : IVulkanSurfaceSource, IDisposable
    {
        private nint _presentationDisplay;
        private static readonly string[] Extensions = ["VK_KHR_surface", "VK_KHR_xlib_surface"];
        public IReadOnlyList<string> RequiredInstanceExtensions => Extensions;

        public nuint CreateSurface(nuint instanceHandle)
        {
            owner.CheckOwner();
            if (owner._window == 0 || instanceHandle == 0)
                throw new InvalidOperationException("X11 Vulkan surface requires a live window and instance.");
            var function = GetInstanceProcAddr(instanceHandle, "vkCreateXlibSurfaceKHR");
            if (function == 0)
                throw new NotSupportedException("VK_KHR_xlib_surface was not enabled on the Vulkan instance.");
            if (_presentationDisplay == 0)
            {
                // Commit window creation before a second client references its XID.
                // WSI owns a separate connection so its internal reads/locks cannot
                // consume or delay the owner thread's native input event queue.
                XSync(owner._display, 0);
                _presentationDisplay = XOpenPresentationDisplay(X11Native.XDisplayString(owner._display));
                if (_presentationDisplay == 0)
                    throw new InvalidOperationException("The Vulkan presentation X11 connection could not be opened.");
            }
            var create = (delegate* unmanaged<nuint, SurfaceInfo*, void*, nuint*, int>)function;
            var info = new SurfaceInfo { Type = 1000004000, Display = _presentationDisplay, Window = owner._window };
            nuint surface;
            var result = create(instanceHandle, &info, null, &surface);
            if (result != 0)
                throw new InvalidOperationException("vkCreateXlibSurfaceKHR failed: " + result);
            return surface;
        }

        public void Dispose()
        {
            if (_presentationDisplay == 0)
                return;
            X11Native.XCloseDisplay(_presentationDisplay);
            _presentationDisplay = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SurfaceInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
        internal nint Display;
        internal nuint Window;
    }

    [LibraryImport("libvulkan.so.1", EntryPoint = "vkGetInstanceProcAddr", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetInstanceProcAddr(nuint instance, string name);
    [LibraryImport("libX11.so.6")] private static partial int XSync(nint display, int discard);
    [LibraryImport("libX11.so.6", EntryPoint = "XOpenDisplay")] private static partial nint XOpenPresentationDisplay(nint name);
}
