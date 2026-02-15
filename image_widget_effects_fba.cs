// FBA: 웹 이미지 로드 + Image 위젯/드로우리스트 기반 효과 테스트
#:property TargetFramework=net10.0
#:package Duxel.Windows.App@*-*

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Duxel.App;
using Duxel.Core;

var imagePath = Environment.GetEnvironmentVariable("DUXEL_IMAGE_PATH");
imagePath ??= string.Empty;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Image Effects Test (FBA)",
        Width = 1320,
        Height = 860,
        VSync = false
    },
    Font = new DuxelFontOptions
    {
        InitialGlyphs = ImageEffectsScreen.GlyphStrings
    },
    Screen = new ImageEffectsScreen(imagePath)
});

public sealed class ImageEffectsScreen : UiScreen
{
    public static readonly string[] GlyphStrings =
    [
        "Duxel Image Effects Test (FBA)",
        "Image Effects",
        "Source",
        "Zoom",
        "Rotation",
        "Alpha",
        "Brightness",
        "Contrast",
        "Pixelate",
        "Grayscale",
        "Invert",
        "Reset",
        "Canvas"
    ];

    private readonly SampleImageOption[] _imageOptions;
    private int _selectedImageIndex;
    private AnimatedUiImagePlayer? _player;
    private string? _loadError;
    private bool _loadAttempted;

    private float _zoom = 1f;
    private float _rotationDeg;
    private float _alpha = 1f;
    private float _brightness = 1f;
    private float _contrast = 1f;
    private int _pixelate = 1;
    private bool _grayscale;
    private bool _invert;

    public ImageEffectsScreen(string sourcePath)
    {
        _imageOptions = WebImageAssets.BuildOptions(sourcePath);
        _selectedImageIndex = 0;
    }

    public override void Render(UiImmediateContext ui)
    {
        EnsureImageLoaded();

        var fx = UiImageEffects.Create(_grayscale, _invert, _brightness, _contrast, _pixelate);
        _player?.Prepare(ui, fx);

        ui.SetNextWindowPos(new UiVector2(16f, 16f));
        var viewport = ui.GetMainViewport();
        ui.SetNextWindowSize(new UiVector2(viewport.Size.X - 32f, viewport.Size.Y - 32f));
        ui.BeginWindow("Image Effects");

        ui.SeparatorText("Format");
        for (var i = 0; i < _imageOptions.Length; i++)
        {
            if (i > 0)
            {
                ui.SameLine();
            }

            var option = _imageOptions[i];
            if (ui.RadioButton(option.Label, _selectedImageIndex == i))
            {
                _selectedImageIndex = i;
                ReloadSelectedImage();
            }
        }

        var selected = _imageOptions[_selectedImageIndex];
        ui.TextV("Source: {0}", selected.Path);
        if (_player is null)
        {
            ui.TextV("Load failed: {0}", _loadError ?? "Unknown error");
            ui.EndWindow();
            return;
        }

        ui.TextV("Image: {0}x{1}", _player.Width, _player.Height);
        ui.TextV("Animated GIF: {0}", _player.IsAnimatedGif ? "ON" : "OFF");

        ui.SliderFloat("Zoom", ref _zoom, 0.1f, 4f, 0f, "0.00x");
        ui.SliderFloat("Rotation", ref _rotationDeg, -180f, 180f, 0f, "0.0 deg");
        ui.SliderFloat("Alpha", ref _alpha, 0f, 1f, 0f, "0.00");
        ui.SliderFloat("Brightness", ref _brightness, 0.2f, 2.2f, 0f, "0.00");
        ui.SliderFloat("Contrast", ref _contrast, 0.2f, 2.2f, 0f, "0.00");
        ui.SliderInt("Pixelate", ref _pixelate, 1, 24);

        ui.Checkbox("Grayscale", ref _grayscale);
        ui.SameLine();
        ui.Checkbox("Invert", ref _invert);

        if (ui.Button("Reset"))
        {
            _zoom = 1f;
            _rotationDeg = 0f;
            _alpha = 1f;
            _brightness = 1f;
            _contrast = 1f;
            _pixelate = 1;
            _grayscale = false;
            _invert = false;
        }

        ui.SeparatorText("Canvas");
        var canvasSize = ui.GetContentRegionAvail();
        if (ui.BeginChild("Canvas", canvasSize, true))
        {
            _player.DrawInCurrentRegion(ui, _zoom, _rotationDeg, _alpha);
            ui.EndChild();
        }

        ui.EndWindow();
    }

    private void EnsureImageLoaded()
    {
        if (_loadAttempted)
        {
            return;
        }

        ReloadSelectedImage();
        _loadAttempted = true;
    }

    private void ReloadSelectedImage()
    {
        var selected = _imageOptions[_selectedImageIndex];
        _player = null;
        _loadError = null;

        _loadAttempted = true;
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                _loadError = "이 샘플의 웹 이미지/GIF 재생은 Windows 런타임에서 지원됩니다.";
                return;
            }

            _player = AnimatedUiImagePlayer.Load(selected.Path, 7001);
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
        }
    }

}

public readonly record struct SampleImageOption(string Label, string Path);

public static class WebImageAssets
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public static SampleImageOption[] BuildOptions(string customPath)
    {
        var options = new List<SampleImageOption>(4);
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            options.Add(new SampleImageOption("Custom", customPath));
        }

        var cacheRoot = Path.Combine(Path.GetTempPath(), "Duxel", "fba-image-cache");
        Directory.CreateDirectory(cacheRoot);

        var pngPath = Path.Combine(cacheRoot, "web-sample.png");
        var jpgPath = Path.Combine(cacheRoot, "web-sample.jpg");
        var gifPath = Path.Combine(cacheRoot, "web-sample.gif");

        DownloadIfMissing(
            pngPath,
            "https://upload.wikimedia.org/wikipedia/commons/4/47/PNG_transparency_demonstration_1.png",
            "https://dummyimage.com/800x450/4a90e2/ffffff.png&text=Duxel+PNG");

        DownloadIfMissing(
            jpgPath,
            "https://picsum.photos/800/450.jpg",
            "https://dummyimage.com/800x450/e26a4a/ffffff.jpg&text=Duxel+JPG");

        DownloadIfMissing(
            gifPath,
            "https://upload.wikimedia.org/wikipedia/commons/d/de/Ajax-loader.gif",
            "https://dummyimage.com/800x450/5a9f5a/ffffff.gif&text=Duxel+GIF");

        options.Add(new SampleImageOption("Web PNG", pngPath));
        options.Add(new SampleImageOption("Web JPG", jpgPath));
        options.Add(new SampleImageOption("Web GIF", gifPath));

        return options.ToArray();
    }

    private static void DownloadIfMissing(string outputPath, params string[] urls)
    {
        if (File.Exists(outputPath))
        {
            return;
        }

        Exception? lastError = null;
        for (var i = 0; i < urls.Length; i++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, urls[i]);
                request.Headers.UserAgent.ParseAdd("Duxel-FBA/1.0");
                using var response = Client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                using var stream = response.Content.ReadAsStream();
                using var fs = File.Create(outputPath);
                stream.CopyTo(fs);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Failed to download image from all candidates. Last error: {lastError?.Message}", lastError);
    }
}

public sealed class AnimatedUiImagePlayer
{
    private readonly UiImageTexture[] _frames;
    private readonly float[] _durationsSec;
    private readonly bool _isAnimatedGif;
    private int _frameIndex;
    private double _accumulator;
    private long _lastTicks;

    private AnimatedUiImagePlayer(UiImageTexture[] frames, float[] durationsSec, bool isAnimatedGif)
    {
        _frames = frames;
        _durationsSec = durationsSec;
        _isAnimatedGif = isAnimatedGif;
        _lastTicks = Stopwatch.GetTimestamp();
    }

    public bool IsAnimatedGif => _isAnimatedGif;
    public int Width => _frames[0].Width;
    public int Height => _frames[0].Height;

    [SupportedOSPlatform("windows")]
    public static AnimatedUiImagePlayer Load(string path, uint baseTextureId)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("AnimatedUiImagePlayer requires Windows runtime.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Image file not found.", path);
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".gif")
        {
            var single = UiImageTexture.LoadFromFile(path, new UiTextureId((nuint)baseTextureId));
            return new AnimatedUiImagePlayer([single], [0.1f], false);
        }

        using var image = Image.FromFile(path);
        var dimensionIds = image.FrameDimensionsList;
        if (dimensionIds is null || dimensionIds.Length == 0)
        {
            var single = UiImageTexture.LoadFromFile(path, new UiTextureId((nuint)baseTextureId));
            return new AnimatedUiImagePlayer([single], [0.1f], false);
        }

        var dimension = new FrameDimension(dimensionIds[0]);
        var frameCount = Math.Max(1, image.GetFrameCount(dimension));
        if (frameCount == 1)
        {
            var single = UiImageTexture.LoadFromFile(path, new UiTextureId((nuint)baseTextureId));
            return new AnimatedUiImagePlayer([single], [0.1f], false);
        }

        var frameDelays = ReadGifFrameDelays(image, frameCount);
        var frames = new UiImageTexture[frameCount];
        var durations = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            image.SelectActiveFrame(dimension, i);
            var rgba = ToRgba32(image, out var width, out var height);
            frames[i] = new UiImageTexture(new UiTextureId((nuint)(baseTextureId + (uint)i)), width, height, rgba);
            durations[i] = frameDelays[i];
        }

        return new AnimatedUiImagePlayer(frames, durations, true);
    }

    public void Prepare(UiImmediateContext ui, in UiImageEffects effects)
    {
        if (_isAnimatedGif && _frames.Length > 1)
        {
            AdvanceFrame();
        }

        _frames[_frameIndex].Prepare(ui, effects);
    }

    public void DrawInCurrentRegion(UiImmediateContext ui, float zoom, float rotationDeg, float alpha)
    {
        _frames[_frameIndex].DrawInCurrentRegion(ui, zoom, rotationDeg, alpha);
    }

    private void AdvanceFrame()
    {
        var now = Stopwatch.GetTimestamp();
        var deltaSec = (now - _lastTicks) / (double)Stopwatch.Frequency;
        _lastTicks = now;
        _accumulator += deltaSec;

        while (_accumulator >= _durationsSec[_frameIndex])
        {
            _accumulator -= _durationsSec[_frameIndex];
            _frameIndex = (_frameIndex + 1) % _frames.Length;
        }
    }

    [SupportedOSPlatform("windows")]
    private static float[] ReadGifFrameDelays(Image image, int frameCount)
    {
        const int frameDelayPropertyId = 0x5100;
        var delays = new float[frameCount];
        for (var i = 0; i < delays.Length; i++)
        {
            delays[i] = 0.1f;
        }

        try
        {
            var item = image.GetPropertyItem(frameDelayPropertyId);
            var bytes = item is null ? Array.Empty<byte>() : (item.Value ?? Array.Empty<byte>());
            for (var i = 0; i < frameCount; i++)
            {
                var offset = i * 4;
                if (offset + 3 >= bytes.Length)
                {
                    break;
                }

                var ticks = BitConverter.ToInt32(bytes, offset);
                var sec = Math.Max(0.02f, ticks / 100f);
                delays[i] = sec;
            }
        }
        catch
        {
        }

        return delays;
    }

    [SupportedOSPlatform("windows")]
    private static byte[] ToRgba32(Image source, out int width, out int height)
    {
        using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        width = bitmap.Width;
        height = bitmap.Height;
        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        if (data is null)
        {
            throw new InvalidOperationException("Bitmap.LockBits returned null.");
        }

        try
        {
            var stride = data.Stride;
            var bgra = new byte[Math.Abs(stride) * height];
            Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);

            var rgba = new byte[width * height * 4];
            var sourceRowStart = stride >= 0 ? 0 : (height - 1) * (-stride);
            var sourceRowStep = stride >= 0 ? stride : -stride;

            for (var y = 0; y < height; y++)
            {
                var srcRow = sourceRowStart + (y * sourceRowStep);
                var dstRow = y * width * 4;
                for (var x = 0; x < width; x++)
                {
                    var src = srcRow + (x * 4);
                    var dst = dstRow + (x * 4);
                    rgba[dst + 0] = bgra[src + 2];
                    rgba[dst + 1] = bgra[src + 1];
                    rgba[dst + 2] = bgra[src + 0];
                    rgba[dst + 3] = bgra[src + 3];
                }
            }

            return rgba;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
