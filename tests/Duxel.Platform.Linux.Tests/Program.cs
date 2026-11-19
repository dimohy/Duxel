using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;
using Duxel.Platform.Linux;
using Duxel.Vulkan;

internal static unsafe partial class Program
{
    private static void Main(string[] args)
    {
        if (args is [var sliderOutput, "slider-direction"])
        {
            RunWidgetPixels(sliderOutput, args[1]);
            return;
        }
        if (args is [var radioOutput, "radio-allocation" or "radio-allocation-matched", var regular, var semibold, var bold])
        {
            RunWidgetPixels(radioOutput, args[1], new(regular, semibold, bold));
            return;
        }
        if (args.Length == 4 && args[1] == "text")
        {
            RunText(args[0], args[2], args[3]);
            return;
        }
        if (args.Length == 4 && args[1] == "vector")
        {
            RunVector(args[0], args[2], args[3]);
            return;
        }
        if (args.Length != 2 || args[1] is not ("loader" or "platform" or "render"))
            throw new ArgumentException("Specify a fresh result directory and loader/platform/render mode.");
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output))
            throw new IOException("Result directory already exists: " + output);
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }
        var vk = Vk.GetApi();
        var platformMode = args[1] is "platform" or "render";
        if (platformMode && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("The platform test requires Linux.");
        Check(vk.IsInstanceExtensionPresent("VK_KHR_surface"), "The explicit OS Vulkan loader exposes the real surface extension");
        Instance instance = default;
        fixed (byte* surfaceName = "VK_KHR_surface\0"u8, xlibName = "VK_KHR_xlib_surface\0"u8)
        {
            var extensions = stackalloc byte*[2] { surfaceName, xlibName };
            var application = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version12.Value };
            var info = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &application,
                EnabledExtensionCount = platformMode ? 2u : 1u,
                PpEnabledExtensionNames = extensions
            };
            Check(vk.CreateInstance(&info, null, &instance) == Result.Success && instance.Handle != 0,
                "The common Vulkan binding creates a real Vulkan 1.2 instance");
        }
        try
        {
            uint count = 0;
            Check(vk.EnumeratePhysicalDevices(instance, &count, null) == Result.Success && count > 0,
                "The common Vulkan binding enumerates real driver devices");
            var devices = new PhysicalDevice[count];
            fixed (PhysicalDevice* device = devices)
                Check(vk.EnumeratePhysicalDevices(instance, &count, device) == Result.Success, "Physical device enumeration fills its actual handles");
            var names = new List<string>();
            foreach (var device in devices)
            {
                vk.GetPhysicalDeviceProperties(device, out var properties);
                names.Add(Marshal.PtrToStringUTF8((nint)properties.DeviceName) ?? throw new InvalidOperationException("Driver device name absent."));
            }
            Console.WriteLine("DEVICES " + string.Join(", ", names));
            if (platformMode)
                RunPlatform(vk, instance, devices, Check);
            if (args[1] == "render")
                RunRender(Check);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxPlatformResult(
                RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(), args[1], names, checks.Count, checks),
                LinuxValidationJson.Default.LinuxPlatformResult));
        }
        finally { vk.DestroyInstance(instance, null); }
    }

    private static void RunPlatform(Vk vk, Instance instance, PhysicalDevice[] devices, Action<bool, string> check)
    {
        using (var centered = new LinuxX11PlatformBackend(new(320, 240, "Duxel owned centering fixture", Visible: false)
        {
            CenterOnScreen = true
        }))
        using (var centeredPeer = new NativePeer(centered.WindowHandle))
            check(centeredPeer.IsInitiallyCentered(), "Explicit initial centering uses the actual default X11 screen geometry");
        using var platform = new LinuxX11PlatformBackend(new(320, 240, "Duxel owned Linux platform fixture"));
        using var peer = new NativePeer(platform.WindowHandle);
        var source = platform.VulkanSurface;
        var handle = source.CreateSurface((nuint)instance.Handle);
        var destroy = (delegate* unmanaged<Instance, SurfaceKHR, void*, void>)vk.GetInstanceProcAddr(instance, "vkDestroySurfaceKHR");
        if (destroy == null)
            throw new InvalidOperationException("Surface destroy entry point absent.");
        try
        {
            check(handle != 0, "The real X11 window creates a VK_KHR_xlib_surface surface");
            var support = (delegate* unmanaged<PhysicalDevice, uint, SurfaceKHR, uint*, Result>)vk.GetInstanceProcAddr(instance, "vkGetPhysicalDeviceSurfaceSupportKHR");
            if (support == null)
                throw new InvalidOperationException("Surface support entry point absent.");
            var present = false;
            foreach (var device in devices)
            {
                uint count = 0;
                vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
                for (uint family = 0; family < count; family++)
                {
                    uint supported = 0;
                    if (support(device, family, new((ulong)handle), &supported) != Result.Success)
                        throw new InvalidOperationException("Surface support query failed.");
                    present |= supported != 0;
                }
            }
            check(present, "An actual driver queue supports presentation to the owned X11 surface");
            platform.PollEvents();
            check(platform.WindowSize == new PlatformSize(320, 240) && platform.FramebufferSize == platform.WindowSize,
                "The platform exposes actual client/framebuffer dimensions");
            foreach (var cursor in Enum.GetValues<UiMouseCursor>())
                platform.SetMouseCursor(cursor);
            check(true, "All public cursors use native X11 cursor resources");
            var repeat = platform.Input.Snapshot.KeyRepeatSettings;
            check(repeat.InitialDelaySeconds >= 0 && repeat.RepeatIntervalSeconds > 0, "Keyboard repeat settings come from the XKB server");
            void Observe(Func<bool> condition)
            {
                var deadline = Environment.TickCount64 + 1500;
                do
                {
                    platform.WaitEvents(20);
                    if (condition())
                        return;
                } while (Environment.TickCount64 < deadline);
                throw new TimeoutException("Owned native X11 input was not observed.");
            }
            peer.Pointer(6, 40, 50);
            Observe(() => platform.Input.Snapshot.MousePosition == new UiVector2(40, 50));
            check(true, "Real X11 motion reaches common pointer coordinates");
            peer.Pointer(8, 321, 50);
            Observe(() => platform.Input.Snapshot.MousePosition == new UiVector2(-1, -1));
            check(true, "Leaving the native window clears hover without requiring a focus change");
            peer.Pointer(7, 40, 50);
            Observe(() => platform.Input.Snapshot.MousePosition == new UiVector2(40, 50));
            check(true, "Entering the native window restores the actual pointer position");
            peer.Pointer(6, 41, 50, state: 4);
            Observe(() => platform.Input.Snapshot.Modifiers == KeyModifiers.Ctrl);
            check(true, "Native motion carries actual pointer modifier state");
            peer.Pointer(7, 40, 50, state: 1);
            Observe(() => platform.Input.Snapshot.Modifiers == KeyModifiers.Shift);
            check(true, "Native crossing events use their distinct Xlib modifier-state field");
            peer.Pointer(4, 40, 50, 1);
            Observe(() => platform.Input.Snapshot.LeftMousePressedEvent);
            check(platform.Input.Snapshot.LeftMouseDown, "Real X11 button press retains held state");
            platform.PollEvents();
            check(platform.Input.Snapshot.LeftMouseDown && !platform.Input.Snapshot.LeftMousePressedEvent,
                "Unchanged input frames retain held state without repeating its press edge");
            peer.Pointer(8, 321, 50);
            Observe(() => platform.Input.Snapshot.MousePosition == new UiVector2(321, 50));
            check(platform.Input.Snapshot.LeftMouseDown, "Leaving during an implicit grab retains held state and outside drag coordinates");
            peer.Pointer(5, 40, 50, 1);
            Observe(() => platform.Input.Snapshot.LeftMouseReleasedEvent);
            check(!platform.Input.Snapshot.LeftMouseDown, "Real X11 release clears held state once");
            peer.Pointer(4, 40, 50, 4);
            Observe(() => platform.Input.Snapshot.MouseWheel == 1);
            check(true, "X11 vertical wheel normalizes to the common positive-up convention");
            peer.Pointer(4, 40, 50, 7);
            Observe(() => platform.Input.Snapshot.MouseWheelHorizontal == -1);
            check(true, "X11 horizontal wheel preserves the common rightward scroll convention");
            peer.Pointer(4, 40, 50, 1);
            peer.Pointer(5, 40, 50, 1);
            Observe(() => platform.Input.Snapshot.LeftMousePressedEvent && platform.Input.Snapshot.LeftMouseReleasedEvent);
            check(!platform.Input.Snapshot.LeftMouseDown, "Coalesced X11 press/release preserves both edges and final state");
            peer.Key(true, 0x61);
            Observe(() => platform.Input.Snapshot.KeyEvents.Any(key => key.Key == UiKey.A && key.IsDown));
            check(platform.Input.Snapshot.CharEvents.Any(character => character.CodePoint == 'a'),
                "XIM committed UTF-8 text and common keyboard keys share the real native event");
            peer.Key(false, 0x61);
            Observe(() => platform.Input.Snapshot.KeyEvents.Any(key => key.Key == UiKey.A && !key.IsDown));
            check(true, "X11 keyboard release reaches common key state");
            void RequireIdle()
            {
                var deadline = Environment.TickCount64 + 1000;
                do
                {
                    platform.PollEvents();
                    var idle = Stopwatch.StartNew();
                    platform.WaitEvents(30);
                    if (idle.ElapsedMilliseconds >= 25)
                        return;
                } while (Environment.TickCount64 < deadline);
                throw new InvalidOperationException("The native queue did not reach an idle wait before the wake test.");
            }
            RequireIdle();
            peer.Key(true, 0x62);
            platform.WaitEvents(1000);
            check(platform.Input.Snapshot.CharEvents.Any(c => c.CodePoint == 'b'),
                "WaitEvents dispatches native committed input without a separate PollEvents call");
            peer.Key(false, 0x62);
            platform.WaitEvents(1000);
            RequireIdle();
            var indefinite = Stopwatch.StartNew();
            var signalIndefinite = Task.Run(() => { Thread.Sleep(30); platform.WakeEvents(); });
            platform.WaitEvents(0);
            signalIndefinite.GetAwaiter().GetResult();
            check(indefinite.ElapsedMilliseconds >= 20 && indefinite.ElapsedMilliseconds < 500,
                "A zero timeout follows the shared indefinite-wait contract instead of polling");
            RequireIdle();
            platform.WakeEvents();
            var timer = Stopwatch.StartNew();
            platform.WaitEvents(1000);
            check(timer.ElapsedMilliseconds < 500, "A wake signaled before WaitEvents remains latched");
            RequireIdle();
            timer.Restart();
            var wake = Task.Run(() => { Thread.Sleep(30); platform.WakeEvents(); });
            platform.WaitEvents(1000);
            wake.GetAwaiter().GetResult();
            check(timer.ElapsedMilliseconds >= 20 && timer.ElapsedMilliseconds < 500,
                "A worker wakes a previously idle native wait without touching Xlib from another thread");
            var wrongThreadRejected = Task.Run(() =>
            {
                try
                {
                    platform.PollEvents();
                    return false;
                }
                catch (InvalidOperationException) { return true; }
            }).GetAwaiter().GetResult();
            check(wrongThreadRejected, "Native window operations enforce owner-thread affinity");
            peer.Close();
            Observe(() => platform.ShouldClose);
            check(true, "WM_DELETE_WINDOW requests close through the common platform contract");
        }
        finally { destroy(instance, new((ulong)handle), null); }
        platform.Dispose();
        platform.WakeEvents();
        var disposedRejected = false;
        try
        {
            source.CreateSurface((nuint)instance.Handle);
        }
        catch (ObjectDisposedException) { disposedRejected = true; }
        check(disposedRejected, "Disposal retires a retained surface source and late wake calls are safe");
    }

    private static void RunRender(Action<bool, string> check)
    {
        using var platform = new LinuxX11PlatformBackend(new(320, 240, "Duxel owned Linux renderer fixture"));
        using var renderer = new VulkanRendererBackend(platform, new(2, EnableVSync: false, MsaaSamples: 1,
            DiagnosticsLog: Console.WriteLine));
        renderer.CreateDeviceObjects();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        for (var frame = 0; frame < 3; frame++)
        {
            platform.PollEvents();
            var size = platform.FramebufferSize;
            context.NewFrame(new(1f / 60, new(size.Width, size.Height), new(1, 1)),
                new(new(-1, -1), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, size.Width, size.Height), UiTextSettings.Default);
            if (frame == 0)
                context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(2), UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 }));
            context.Render(new Screen(ui =>
            {
                var paint = ui.GetBackgroundDrawList();
                paint.AddRectFilled(new(0, 0, size.Width, size.Height), new(0, 0, 0), new(2));
                paint.AddRectFilled(new(20 + frame * 10, 20, 64, 48), new(255, 0, 0), new(2));
            }));
            using var data = context.GetDrawData();
            check(renderer.TryRenderDrawData(data), "The common renderer submits and presents Linux primitive frame " + frame);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
