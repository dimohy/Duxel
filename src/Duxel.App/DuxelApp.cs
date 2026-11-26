using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Duxel.Core;
using Duxel.Vulkan;

namespace Duxel.App;

public static class DuxelApp
{
    private static readonly DuxelAppSession s_primarySession = new();
    private static Action<DuxelAppOptions>? s_registeredRunner;

    public static bool IsExitRequested => s_primarySession.IsExitRequested;
    public static DuxelAppSession PrimarySession => s_primarySession;

    public static void RegisterRunner(Action<DuxelAppOptions> runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        var current = Interlocked.CompareExchange(ref s_registeredRunner, runner, null);
        if (current is null || current == runner)
        {
            return;
        }

        throw new InvalidOperationException("A Duxel platform runner is already registered.");
    }

    public static void RequestFrame()
    {
        s_primarySession.RequestFrame();
    }

    public static void Exit()
    {
        s_primarySession.Exit();
    }

    public static DuxelAppOptions Options(
        IUiView root,
        string title = "Duxel",
        int width = 1280,
        int height = 720,
        bool vsync = true)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Options(Dux.Screen(root), title, width, height, vsync);
    }

    public static DuxelAppOptions Options(
        UiScreen screen,
        string title = "Duxel",
        int width = 1280,
        int height = 720,
        bool vsync = true)
    {
        ArgumentNullException.ThrowIfNull(screen);
        return new DuxelAppOptions
        {
            Window = new DuxelWindowOptions
            {
                Title = title,
                Width = width,
                Height = height,
                VSync = vsync
            },
            Screen = screen
        };
    }

    public static DuxelAppOptions Options<TDesign>(
        IUiView root,
        string title = "Duxel",
        int width = 1280,
        int height = 720,
        bool vsync = true)
        where TDesign : IUiDesign
    {
        return Options(root, title, width, height, vsync) with
        {
            Design = TDesign.Create()
        };
    }

    public static DuxelAppOptions Options<TDesign>(
        UiScreen screen,
        string title = "Duxel",
        int width = 1280,
        int height = 720,
        bool vsync = true)
        where TDesign : IUiDesign
    {
        return Options(screen, title, width, height, vsync) with
        {
            Design = TDesign.Create()
        };
    }

    public static void Run(DuxelAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runner = Volatile.Read(ref s_registeredRunner);
        if (runner is null)
        {
            throw new InvalidOperationException(
                "No platform runner is registered. Add a platform package such as Duxel.Windows.App so that DuxelApp.Run can be routed."
            );
        }

        runner(options);
    }

    public static void RunCore(DuxelAppOptions options, IPlatformBackend platform)
    {
        s_primarySession.RunCore(options, platform);
    }

    internal static void EmitTrace(DuxelDebugOptions debug, ref int frameCounter, UiDrawData drawData, float fps)
    {
        frameCounter++;
        var log = debug.Log;
        if (log is null)
        {
            return;
        }

        var interval = Math.Max(1, debug.LogEveryNFrames);
        if (frameCounter % interval != 0)
        {
            return;
        }

        var handler = new DefaultInterpolatedStringHandler(64, 6, CultureInfo.InvariantCulture);
        handler.AppendLiteral("Frame ");
        handler.AppendFormatted(frameCounter);
        handler.AppendLiteral(": FPS=");
        handler.AppendFormatted(fps, "0.0");
        handler.AppendLiteral(", DrawLists=");
        handler.AppendFormatted(drawData.DrawLists.Count);
        handler.AppendLiteral(", Vertices=");
        handler.AppendFormatted(drawData.TotalVertexCount);
        handler.AppendLiteral(", Indices=");
        handler.AppendFormatted(drawData.TotalIndexCount);
        handler.AppendLiteral(", Textures=");
        handler.AppendFormatted(drawData.TextureUpdates.Count);
        log(handler.ToStringAndClear());
    }

    internal static bool IsShutdownTimeVulkanException(Exception ex)
    {
        if (ex is not InvalidOperationException)
        {
            return false;
        }

        var message = ex.Message;
        return message.Contains("SurfaceLost", StringComparison.OrdinalIgnoreCase)
            || message.Contains("OutOfDate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Vulkan call failed", StringComparison.OrdinalIgnoreCase);
    }

    internal static double ParseAutoExitSeconds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0d;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0d
            ? seconds
            : 0d;
    }

    private static bool ParseBooleanEnvironmentFlag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }

    internal static UiFontAtlas BuildFontAtlas(DuxelFontOptions options, HashSet<int> codepoints, bool useStartupSettings)
    {
        if (string.IsNullOrWhiteSpace(options.PrimaryFontPath))
        {
            throw new InvalidOperationException("Primary font path must be provided.");
        }

        if (!File.Exists(options.PrimaryFontPath))
        {
            throw new FileNotFoundException("Primary font file not found.", options.PrimaryFontPath);
        }

        if (!string.IsNullOrWhiteSpace(options.SecondaryFontPath) && !File.Exists(options.SecondaryFontPath))
        {
            throw new FileNotFoundException("Secondary font file not found.", options.SecondaryFontPath);
        }

        var ascii = new List<int>(codepoints.Count);
        var nonAscii = new List<int>();
        foreach (var codepoint in codepoints)
        {
            if (codepoint <= 0x7E)
            {
                ascii.Add(codepoint);
            }
            else
            {
                nonAscii.Add(codepoint);
            }
        }

        ascii.Sort();
        nonAscii.Sort();

        var fontSize = useStartupSettings ? options.StartupFontSize : options.FontSize;
        var atlasWidth = useStartupSettings ? options.StartupAtlasWidth : options.AtlasWidth;
        var atlasHeight = useStartupSettings ? options.StartupAtlasHeight : options.AtlasHeight;
        var padding = useStartupSettings ? options.StartupPadding : options.Padding;
        var oversample = useStartupSettings ? options.StartupOversample : options.Oversample;
        const bool includeKerning = false;

        if (!string.IsNullOrWhiteSpace(options.SecondaryFontPath) && nonAscii.Count > 0)
        {
            var sources = new List<UiFontSource>
            {
                new(options.PrimaryFontPath, ascii, 0, 1f),
                new(options.SecondaryFontPath, nonAscii, 1, options.SecondaryScale)
            };

            return UiFontAtlasBuilder.CreateFromTtfMerged(
                sources,
                fontSize,
                atlasWidth,
                atlasHeight,
                padding,
                oversample,
                includeKerning
            );
        }

        return UiFontAtlasBuilder.CreateFromTtf(
            options.PrimaryFontPath,
            ascii,
            fontSize,
            atlasWidth,
            atlasHeight,
            padding,
            oversample,
            includeKerning
        );
    }

    internal static void ConfigureContext(UiContext context, DuxelAppOptions options, IPlatformBackend platform)
    {
        context.SetDesign(ResolveEffectiveDesign(options, platform));

        context.SetPlatformTextBackend(platform.TextBackend);
        context.SetWindowTitleBarPlatform(platform.WindowTitleBar);
        if (platform.ImeHandler is { } imeHandler)
        {
            context.SetImeHandler(imeHandler);
        }

        if (options.Clipboard is not null)
        {
            context.SetClipboard(options.Clipboard);
        }
        else if (options.ClipboardFactory is not null)
        {
            var clipboard = options.ClipboardFactory(platform);
            if (clipboard is not null)
            {
                context.SetClipboard(clipboard);
            }
        }
    }

    internal static UiCompiledDesign ResolveEffectiveDesign(DuxelAppOptions options, IPlatformBackend platform)
    {
        var design = ResolveBaseDesign(options, platform);
        return options.RespectSystemForcedColors && platform is IPlatformThemeProvider { ForcedColors: { } palette }
            ? design with
            {
                Theme = palette.Apply(design.Theme)
            }
            : design;
    }

    private static UiCompiledDesign ResolveBaseDesign(DuxelAppOptions options, IPlatformBackend platform)
    {
        if (options.Design is { } design)
        {
            return design;
        }

        if (IsDefaultTheme(options.Theme) && platform is IPlatformThemeProvider themeProvider)
        {
            return themeProvider.ColorScheme is UiSystemColorScheme.Dark
                ? UiCompiledDesign.Windows11Dark
                : UiCompiledDesign.Windows11;
        }

        return UiCompiledDesign.Default with
        {
            Theme = options.Theme
        };
    }

    internal static bool UsesPlatformDefaultTheme(DuxelAppOptions options)
        => options.Design is null && IsDefaultTheme(options.Theme);

    private static bool IsDefaultTheme(UiTheme theme)
    {
        var defaultTheme = UiCompiledDesign.Default.Theme;
        return theme.ForcedColors == defaultTheme.ForcedColors && theme.WindowBg == defaultTheme.WindowBg
            && theme.TitleBgActive == defaultTheme.TitleBgActive
            && theme.Text == defaultTheme.Text
            && theme.Button == defaultTheme.Button
            && theme.InputBg == defaultTheme.InputBg
            && theme.CheckMark == defaultTheme.CheckMark;
    }

    private sealed class DefaultKeyRepeatSettingsProvider : IKeyRepeatSettingsProvider
    {
        public static DefaultKeyRepeatSettingsProvider Shared { get; } = new();

        private static readonly UiKeyRepeatSettings Settings = new(
            InitialDelaySeconds: 0.5,
            RepeatIntervalSeconds: 1.0 / 30.0
        );

        public UiKeyRepeatSettings GetSettings() => Settings;
    }

    internal static void ValidateOptions(DuxelAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Font.Typography);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Renderer.MaxFramesInFlight);
        if (options.Window.Width <= 0 || options.Window.Height <= 0)
        {
            throw new InvalidOperationException("Window size must be greater than zero.");
        }

        if (options.Font.InitialRanges.Count == 0)
        {
            throw new InvalidOperationException("At least one initial font range must be provided.");
        }
    }

}

public sealed record class DuxelAppOptions
{
    public DuxelWindowOptions Window { get; init; } = new();
    public DuxelRendererOptions Renderer { get; init; } = new();
    public DuxelFontOptions Font { get; init; } = new();
    public DuxelFrameOptions Frame { get; init; } = new();
    public DuxelDebugOptions Debug { get; init; } = new();
    public UiTheme Theme { get; init; } = UiCompiledDesign.Default.Theme;
    public bool RespectSystemForcedColors { get; init; } = true;
    public UiCompiledDesign? Design
    {
        get; init;
    }

    public UiTextureId FontTextureId { get; init; } = new(1);
    public UiTextureId WhiteTextureId { get; init; } = new(2);

    public required UiScreen Screen
    {
        get; init;
    }

    public IUiClipboard? Clipboard
    {
        get; init;
    }
    public IUiImageDecoder? ImageDecoder
    {
        get; init;
    }
    public IKeyRepeatSettingsProvider? KeyRepeatSettingsProvider
    {
        get; init;
    }
    public Func<IPlatformBackend, IUiClipboard?>? ClipboardFactory
    {
        get; init;
    }
}

public sealed record class DuxelDebugOptions
{
    public Action<string>? Log
    {
        get; init;
    }
    public int LogEveryNFrames { get; init; } = 60;
    public bool LogStartupTimings { get; init; } = false;
}

public sealed record class DuxelWindowOptions
{
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public int MinWidth { get; init; } = 0;
    public int MinHeight { get; init; } = 0;
    public string Title { get; init; } = "Duxel";
    public bool VSync { get; init; } = true;
    public bool Resizable { get; init; } = true;
    public bool ShowMinimizeButton { get; init; } = true;
    public bool ShowMaximizeButton { get; init; } = true;
    public bool CenterOnScreen { get; init; } = true;
    public bool CenterOnOwner
    {
        get; init;
    }
    public nint OwnerWindowHandle
    {
        get; init;
    }
    public string? IconPath
    {
        get; init;
    }
    public ReadOnlyMemory<byte> IconData
    {
        get; init;
    }
    public Action<nint>? WindowCreated
    {
        get; init;
    }
    public bool IntegrateSystemChrome { get; init; } = true;
    public DuxelTitleBarMode TitleBarMode { get; init; } = DuxelTitleBarMode.Default;
    public bool UseDuxelTitleBar
    {
        get; init;
    }
    public float DuxelTitleBarHeight { get; init; } = 48f;
    public DuxelTrayOptions Tray { get; init; } = new();
}

public sealed record class DuxelRendererOptions
{
    public int MinImageCount { get; init; } = 3;
    public int MaxFramesInFlight { get; init; } = 2;
    public DuxelPerformanceProfile Profile { get; init; } = DuxelPerformanceProfile.Display;
    public int MsaaSamples { get; init; } = 0;
    public bool FontLinearSampling { get; init; } = false;
    public DuxelTextRenderingMode TextRendering { get; init; } = DuxelTextRenderingMode.DirectText;
}

public enum DuxelPerformanceProfile
{
    Display,
    Render,
}

public enum DuxelTextRenderingMode
{
    Auto,
    DirectText,
    Atlas,
}

public sealed record class DuxelFontOptions
{
    public UiTypographyFonts Typography
    {
        get; init;
    } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguisb.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeuib.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgunbd.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgunbd.ttf"))
    {
        Monospace = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf")
    };
    public string PrimaryFontPath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
    /// <summary>Secondary TrueType-outline font used by the bitmap atlas.</summary>
    public string? SecondaryFontPath { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf");
    /// <summary>Secondary font for direct text. When omitted, direct text uses <see cref="SecondaryFontPath"/>.</summary>
    public string? DirectTextSecondaryFontPath { get; init; }
    public float SecondaryScale { get; init; } = 1f;
    public bool FastStartup { get; init; } = true;
    public bool UseBuiltInAsciiAtStartup { get; init; } = true;
    public int StartupBuiltInScale { get; init; } = 2;
    public int StartupBuiltInColumns { get; init; } = 16;
    public int StartupFontSize { get; init; } = 16;
    public int StartupAtlasWidth { get; init; } = 512;
    public int StartupAtlasHeight { get; init; } = 512;
    public int StartupPadding { get; init; } = 1;
    public int StartupOversample { get; init; } = 1;
    public IReadOnlyList<UiFontRange> StartupRanges { get; init; } = [new UiFontRange(0x20, 0x7E)];
    public IReadOnlyList<string> StartupGlyphs { get; init; } = [];
    public int FontSize { get; init; } = 16;
    public int AtlasWidth { get; init; } = 1024;
    public int AtlasHeight { get; init; } = 1024;
    public int Padding { get; init; } = 2;
    public int Oversample { get; init; } = 2;
    public IReadOnlyList<UiFontRange> InitialRanges { get; init; } = [new UiFontRange(0x20, 0x7E)];
}

public sealed record class DuxelFrameOptions
{
    /// <summary>Overlap UI production and rendering with one queued frame. Callback frames remain serialized.</summary>
    public bool EnableRenderPipeline
    {
        get; init;
    }
    /// <summary>Wait for platform events while UI/render threads run. Requires IPlatformEventWakeBackend.</summary>
    public bool UseEventDrivenPlatformLoop
    {
        get; init;
    }
    public float LineHeightScale { get; init; } = 1.2f;
    public bool PixelSnap { get; init; } = true;
    public bool UseBaseline { get; init; } = true;
    public double FontRebuildMinIntervalSeconds { get; init; } = 0.25;
    public int FontRebuildBatchSize { get; init; } = 16;
    public bool EnableIdleFrameSkip { get; init; } = true;
    public int IdleSleepMilliseconds { get; init; } = 2;
    public int IdleWakeCheckMilliseconds { get; init; } = 1000;
    public int IdleEventWaitMilliseconds { get; init; } = 0;
    public IReadOnlyDictionary<string, int> WindowTargetFps { get; init; } = new Dictionary<string, int>();
    public Func<bool>? IsAnimationActiveProvider
    {
        get; init;
    }
}


