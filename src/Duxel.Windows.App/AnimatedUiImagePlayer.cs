using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Duxel.Core;

[SupportedOSPlatform("windows")]
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

    public static AnimatedUiImagePlayer Load(string path, uint baseTextureId)
    {
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
            var bytes = item is null ? [] : (item.Value ?? []);
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
            // Property may not exist
        }

        return delays;
    }

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
