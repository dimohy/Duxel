using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;
using Duxel.Platform.Linux;
using Duxel.Vulkan;

internal static unsafe partial class Program
{
    private static void RunWidgetPixels(string destination, string mode, UiTypographyFonts? fonts = null)
    {
        var output = Path.GetFullPath(destination);
        if (Directory.Exists(output))
            throw new IOException("Result directory already exists: " + output);
        if (fonts is { } paths)
        {
            foreach (var path in new[] { paths.Regular, paths.Semibold, paths.Bold })
                if (!File.Exists(path))
                    throw new FileNotFoundException("The RadioGroup fixture requires explicit fonts.", path);
        }
        Directory.CreateDirectory(output);
        var matchedRadio = mode == "radio-allocation-matched";
        var expectedSize = matchedRadio ? new PlatformSize(400, 300) : new PlatformSize(320, 240);
        using var platform = new LinuxX11PlatformBackend(new(expectedSize.Width, expectedSize.Height, "Duxel widget pixel fixture", Visible : false));
        using var renderer = new VulkanRendererBackend(platform, new(2, EnableVSync : false, MsaaSamples : 1,
            DiagnosticsLog : Console.WriteLine));
        renderer.CreateDeviceObjects();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        if (fonts is { } typography)
        {
            context.SetPlatformTextBackend(platform.TextBackend!);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextFallbackEnabled(false);
            context.SetDirectTextBaseFontSize(16);
            context.SetDirectTextPrimaryFontPath(typography.Regular);
            context.SetTypographyFonts(typography);
        }
        var size = platform.FramebufferSize;
        if (size != expectedSize)
            throw new InvalidOperationException($"The widget fixture requires a {expectedSize.Width} × {expectedSize.Height} framebuffer.");
        var clip = new UiRect(0, 0, size.Width, size.Height);
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var upload = true;
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }

        byte[] Capture(string name, Action<UiImmediateContext> draw)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(size.Width, size.Height), new(1, 1)), input, clip, UiTextSettings.Default);
            if (upload)
            {
                context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(2), UiTextureFormat.Rgba8Unorm, 1, 1,
                    new byte[] { 255, 255, 255, 255 }));
                upload = false;
            }
            context.Render(new Screen(draw));
            using var frame = context.GetDrawData();
            var pixels = renderer.RenderAndReadback(frame);
            WritePpm(Path.Combine(output, name + ".ppm"), pixels, size.Width, size.Height);
            return pixels;
        }

        foreach (var samples in new[] { 1, 4 })
        {
            renderer.SetMsaaSamples(samples);
            if (fonts is not null)
                RadioAllocationRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
            else
                foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
                {
                    context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
                    SliderDirectionPixelFixture.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, size.Width, scheme);
                }
        }
        if (fonts is not null)
            RadioGroupRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);

        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxPlatformResult(
            RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(),
            mode, [], checks.Count, checks), LinuxValidationJson.Default.LinuxPlatformResult));
    }
}
