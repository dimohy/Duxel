using System.ComponentModel;
using System.Diagnostics;

namespace Duxel.Gallery;

internal sealed record GalleryLinuxArguments(string[] Fonts, string? Verification, string? Output)
{
    internal static GalleryLinuxArguments Parse(string[] args)
    {
        var fontCount = args.Length switch
        {
            0 or 2 => 0,
            5 or 7 => 5,
            _ => throw Usage()
        };
        var verification = args.Length == fontCount ? null : args[fontCount];
        if (verification is not (null or "--verify" or "--verify-live-holidays" or "--verify-gpu-camera" or "--verify-gpu-camera-reuse" or "--verify-slider-performance" or
            "--verify-pointer-performance" or "--verify-idle-pointer" or "--verify-stream-pointer" or "--verify-presentation-reuse"))
            throw Usage();
        var output = verification is null ? null : args[^1];
        if (output is not null && string.IsNullOrWhiteSpace(output))
            throw Usage();

        var fonts = fontCount == 0 ? DefaultFonts() : args[..fontCount];
        foreach (var font in fonts)
            if (!File.Exists(font))
                throw new FileNotFoundException("The Gallery font file is unavailable.", font);
        return new(fonts, verification, output);
    }

    private static string[] DefaultFonts()
    {
        var regular = MatchFont("sans-serif:lang=en:weight=regular").File;
        var bold = MatchFont("sans-serif:lang=en:weight=bold").File;
        var korean = MatchFont("sans-serif:lang=ko:weight=regular");
        var koreanBold = MatchFont("sans-serif:lang=ko:weight=bold");
        var mono = MatchFont("monospace:lang=en:weight=regular").File;
        var koreanFiles = (korean.File, koreanBold.File);
        if (!korean.SupportsKorean || !koreanBold.SupportsKorean)
        {
            const string windowsKorean = "/mnt/c/Windows/Fonts/malgun.ttf";
            const string windowsKoreanBold = "/mnt/c/Windows/Fonts/malgunbd.ttf";
            if (Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") is not null
                && File.Exists(windowsKorean) && File.Exists(windowsKoreanBold))
                koreanFiles = (windowsKorean, windowsKoreanBold);
            else
                Console.Error.WriteLine("Fontconfig found no complete Korean font pair. Install a Korean desktop font such as Noto Sans CJK for Korean text.");
        }
        return [regular, bold, koreanFiles.Item1, koreanFiles.Item2, mono];
    }

    private static ArgumentException Usage() => new(
        "Omit font arguments to use installed fonts, or specify Latin regular/bold, Korean regular/bold, and monospace. " +
        "A verification flag requires a fresh output directory.");

    private static (string File, bool SupportsKorean) MatchFont(string pattern)
    {
        var start = new ProcessStartInfo("fc-match")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--format=%{file}\n%{lang}\n");
        start.ArgumentList.Add(pattern);
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start fc-match.");
            var errors = process.StandardError.ReadToEndAsync();
            var file = process.StandardOutput.ReadLine() ?? string.Empty;
            var languages = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0 || file.Length == 0)
                throw new InvalidOperationException($"Fontconfig could not match '{pattern}'. Install desktop fonts or supply five font paths. {errors.GetAwaiter().GetResult().Trim()}");
            return (file, languages.Split('|').Contains("ko", StringComparer.Ordinal));
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            throw new InvalidOperationException("Automatic Gallery fonts require fc-match. Install fontconfig or supply five explicit font paths.", ex);
        }
    }
}
