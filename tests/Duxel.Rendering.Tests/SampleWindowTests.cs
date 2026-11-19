using System.Reflection;
using System.Runtime.ExceptionServices;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static class SampleWindowTests
{
    internal static void Run(Action<bool, string> check, string output, string assemblyPath)
    {
        // Load only the declared, locally built sample class. Its application
        // entry point is never invoked; the current test host owns all rendering.
        var assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        var type = assembly.GetType("Dynamic3DLayersScreen", throwOnError: true)!;
        check(typeof(UiScreen).IsAssignableFrom(type), "status fixture is the real compiled sample screen type");
        var oldOutput = Environment.GetEnvironmentVariable("DUXEL_3D_BENCH_OUT");
        ExceptionDispatchInfo? failure = null;
        Environment.SetEnvironmentVariable("DUXEL_3D_BENCH_OUT", null);
        try
        {
            var owner = new Thread(() =>
            {
                try { Render(check, output, (UiScreen)Activator.CreateInstance(type)!); }
                catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            }) { IsBackground = true, Name = "DuxelSampleWindowOwner" };
            owner.Start();
            if (!owner.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("Sample window GPU readback timed out.");
            failure?.Throw();
        }
        finally { Environment.SetEnvironmentVariable("DUXEL_3D_BENCH_OUT", oldOutput); }
    }

    private static void Render(Action<bool, string> check, string output, UiScreen screen)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 1280, Height = 800, Title = "Duxel dynamic 3D layers", VSync = false,
            UseDuxelTitleBar = true, DuxelTitleBarHeight = 48, IntegrateSystemChrome = true,
            ShowMinimizeButton = true, ShowMaximizeButton = true, Resizable = true,
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new(),
        });
        using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(3, EnableVSync: false, MsaaSamples: 1));
        renderer.CreateDeviceObjects();
        var white = new UiTextureId(2);
        var options = new DuxelAppOptions { Screen = screen };
        var atlas = DuxelApp.BuildFontAtlas(options.Font, new HashSet<int>(Enumerable.Range(32, 95)), useStartupSettings: false);
        using var context = new UiContext(atlas, new(1), white);
        DuxelApp.ConfigureContext(context, options, platform);
        context.SetDirectTextPrimaryFontPath(options.Font.PrimaryFontPath);
        context.SetDirectTextSecondaryFontPath(options.Font.SecondaryFontPath);
        context.SetDirectTextBaseFontSize(16);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextFallbackEnabled(false);
        context.SetContentScale(1);
        var chrome = new DuxelWindowChromeScreen(screen, platform, 48);
        var size = platform.FramebufferSize;
        check(size.Width == 1280 && size.Height == 800, "real sample readback uses the unresized 1280x800 client");
        var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
        for (var frame = 0; frame < 3; frame++)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(size.Width, size.Height), new(1, 1)), input,
                new(0, 0, size.Width, size.Height), UiTextSettings.Default);
            if (frame == 0)
            {
                context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, white, UiTextureFormat.Rgba8Unorm,
                    1, 1, new byte[] { 255, 255, 255, 255 }));
                context.QueueTextureUpdate(atlas.CreateTextureUpdate(new(1), UiTextureUpdateKind.Create));
            }
            context.Render(chrome);
            using var data = context.GetDrawData();
            var pixels = renderer.RenderAndReadback(data);
            check(data.DrawLists.Any(list => list.Scene3D.HasValue) && renderer.LastFrameMetrics.DrawCalls > 0,
                "real sample includes dynamic 3D layers and native-text chrome on GPU frame " + frame);
            var coloredPixels = 0;
            for (var y = 170; y < 740; y++)
                for (var x = 300; x < 1000; x++)
                {
                    var offset = (y * size.Width + x) * 4;
                    var maximum = Math.Max(pixels[offset], Math.Max(pixels[offset + 1], pixels[offset + 2]));
                    var minimum = Math.Min(pixels[offset], Math.Min(pixels[offset + 1], pixels[offset + 2]));
                    if (maximum > 64 && maximum - minimum > 32) coloredPixels++;
                }
            Console.WriteLine($"SAMPLE frame={frame} coloredPixels={coloredPixels}");
            check(coloredPixels > 5000, "real sample renders colored 3D geometry outside the status panel on GPU frame " + frame);
            var path = Path.Combine(output, "actual-sample-status-" + frame + ".ppm");
            using var stream = File.Create(path);
            stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{size.Width} {size.Height}\n255\n"));
            var rgb = new byte[size.Width * size.Height * 3];
            for (var i = 0; i < size.Width * size.Height; i++)
                pixels.AsSpan(i * 4, 3).CopyTo(rgb.AsSpan(i * 3, 3));
            stream.Write(rgb);
        }
    }
}
