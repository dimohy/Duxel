// FBA: 웹 이미지 로드 + Image 위젯/드로우리스트 기반 효과 테스트
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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

