using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Duxel.Core;

public readonly record struct UiImageEffects(
    bool Grayscale,
    bool Invert,
    float Brightness,
    float Contrast,
    int Pixelate)
{
    public static UiImageEffects Default => new(false, false, 1f, 1f, 1);

    public static UiImageEffects Create(bool grayscale, bool invert, float brightness, float contrast, int pixelate)
        => new(grayscale, invert, Quantize(brightness), Quantize(contrast), Math.Clamp(pixelate, 1, 24));

    private static float Quantize(float value)
        => MathF.Round(value * 1000f) / 1000f;
}

public readonly record struct UiImageData(int Width, int Height, byte[] RgbaPixels);

public interface IUiImageDecoder
{
    bool CanDecode(string path);
    UiImageData Decode(string path);
}

public sealed class UiImageTexture
{
    public static IUiImageDecoder? ImageDecoder { get; set; }

    private readonly UiTextureId _textureId;
    private readonly byte[] _sourceRgba;
    private readonly byte[] _workingRgba;
    private bool _textureCreated;
    private UiImageEffects _lastEffects;

    public UiImageTexture(UiTextureId textureId, int width, int height, byte[] rgbaPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(rgbaPixels);

        var expected = checked(width * height * 4);
        if (rgbaPixels.Length != expected)
        {
            throw new ArgumentException("RGBA pixel length does not match width/height.", nameof(rgbaPixels));
        }

        _textureId = textureId;
        Width = width;
        Height = height;
        _sourceRgba = new byte[rgbaPixels.Length];
        _workingRgba = new byte[rgbaPixels.Length];
        Array.Copy(rgbaPixels, _sourceRgba, rgbaPixels.Length);
        Array.Copy(rgbaPixels, _workingRgba, rgbaPixels.Length);
        _lastEffects = UiImageEffects.Default;
    }

    public int Width { get; }
    public int Height { get; }

    public static UiImageTexture LoadFromFile(string path, UiTextureId textureId)
    {
        var (width, height, rgba) = LoadRgba(path);
        return new UiImageTexture(textureId, width, height, rgba);
    }

    public void Prepare(UiImmediateContext ui, in UiImageEffects effects)
    {
        if (!_textureCreated || !_lastEffects.Equals(effects))
        {
            ApplyEffects(_sourceRgba, _workingRgba, Width, Height, effects);
            var kind = _textureCreated ? UiTextureUpdateKind.Update : UiTextureUpdateKind.Create;
            ui.QueueTextureUpdate(new UiTextureUpdate(
                kind,
                _textureId,
                UiTextureFormat.Rgba8Unorm,
                Width,
                Height,
                _workingRgba));
            _textureCreated = true;
            _lastEffects = effects;
        }
    }

    public void DrawInCurrentRegion(UiImmediateContext ui, float zoom, float rotationDeg, float alpha)
    {
        var drawList = ui.GetWindowDrawList();
        var canvasPos = ui.GetCursorScreenPos();
        var canvasSize = ui.GetContentRegionAvail();
        if (canvasSize.X <= 0f || canvasSize.Y <= 0f)
        {
            return;
        }

        var canvasRect = new UiRect(canvasPos.X, canvasPos.Y, canvasSize.X, canvasSize.Y);

        drawList.PushTexture(ui.WhiteTextureId);
        drawList.PushClipRect(canvasRect, true);
        DrawCheckerBackground(drawList, canvasRect);

        if (Width > 0 && Height > 0)
        {
            var center = new UiVector2(canvasRect.X + canvasRect.Width * 0.5f, canvasRect.Y + canvasRect.Height * 0.5f);
            var width = Width * zoom;
            var height = Height * zoom;
            var radians = rotationDeg * (MathF.PI / 180f);
            var cos = MathF.Cos(radians);
            var sin = MathF.Sin(radians);
            var halfW = width * 0.5f;
            var halfH = height * 0.5f;
            var p0 = RotateAround(center, -halfW, -halfH, cos, sin);
            var p1 = RotateAround(center, halfW, -halfH, cos, sin);
            var p2 = RotateAround(center, halfW, halfH, cos, sin);
            var p3 = RotateAround(center, -halfW, halfH, cos, sin);

            var alphaByte = (uint)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255);
            var tint = new UiColor((alphaByte << 24) | 0x00FFFFFFu);
            drawList.AddImageQuad(
                _textureId,
                p0, p1, p2, p3,
                new UiVector2(0f, 0f),
                new UiVector2(1f, 0f),
                new UiVector2(1f, 1f),
                new UiVector2(0f, 1f),
                tint);
        }

        drawList.PopClipRect();
        drawList.PopTexture();
    }

    private static (int Width, int Height, byte[] Rgba) LoadRgba(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Image file not found.", path);
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".ppm" => LoadPpm(path),
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => LoadWithExternalDecoder(path),
            _ => throw new NotSupportedException($"Unsupported image extension: {ext}")
        };
    }

    private static (int Width, int Height, byte[] Rgba) LoadWithExternalDecoder(string path)
    {
        var decoder = ImageDecoder;
        if (decoder is null)
        {
            throw new InvalidOperationException(
                "No platform image decoder is registered. Register IUiImageDecoder before loading PNG/JPG/GIF/BMP.");
        }

        if (!decoder.CanDecode(path))
        {
            throw new NotSupportedException($"No registered decoder can decode image: {path}");
        }

        var decoded = decoder.Decode(path);
        if (decoded.Width <= 0 || decoded.Height <= 0)
        {
            throw new InvalidDataException("Decoded image size is invalid.");
        }

        if (decoded.RgbaPixels is null)
        {
            throw new InvalidDataException("Decoded RGBA buffer is null.");
        }

        var expected = checked(decoded.Width * decoded.Height * 4);
        if (decoded.RgbaPixels.Length != expected)
        {
            throw new InvalidDataException("Decoded RGBA buffer length does not match image size.");
        }

        return (decoded.Width, decoded.Height, decoded.RgbaPixels);
    }

    private static (int Width, int Height, byte[] Rgba) LoadPpm(string path)
    {
        using var stream = File.OpenRead(path);
        if (!TryReadToken(stream, out var magic))
        {
            throw new InvalidDataException("PPM header is empty.");
        }

        if (!TryReadToken(stream, out var widthToken)
            || !TryReadToken(stream, out var heightToken)
            || !TryReadToken(stream, out var maxValToken))
        {
            throw new InvalidDataException("PPM header is incomplete.");
        }

        var width = int.Parse(widthToken, CultureInfo.InvariantCulture);
        var height = int.Parse(heightToken, CultureInfo.InvariantCulture);
        var maxVal = int.Parse(maxValToken, CultureInfo.InvariantCulture);

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Invalid image size in PPM.");
        }

        if (maxVal <= 0 || maxVal > 255)
        {
            throw new InvalidDataException("Unsupported max value in PPM. Only <=255 is supported.");
        }

        var pixelCount = checked(width * height);
        var rgba = new byte[pixelCount * 4];

        if (string.Equals(magic, "P6", StringComparison.Ordinal))
        {
            var rgbBytes = new byte[pixelCount * 3];
            var read = 0;
            while (read < rgbBytes.Length)
            {
                var n = stream.Read(rgbBytes, read, rgbBytes.Length - read);
                if (n <= 0)
                {
                    throw new EndOfStreamException("Unexpected EOF while reading P6 pixel data.");
                }

                read += n;
            }

            for (var i = 0; i < pixelCount; i++)
            {
                var s = i * 3;
                var d = i * 4;
                rgba[d + 0] = rgbBytes[s + 0];
                rgba[d + 1] = rgbBytes[s + 1];
                rgba[d + 2] = rgbBytes[s + 2];
                rgba[d + 3] = 255;
            }

            return (width, height, rgba);
        }

        if (!string.Equals(magic, "P3", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported PPM magic: {magic}");
        }

        for (var i = 0; i < pixelCount; i++)
        {
            if (!TryReadToken(stream, out var rTok)
                || !TryReadToken(stream, out var gTok)
                || !TryReadToken(stream, out var bTok))
            {
                throw new EndOfStreamException("Unexpected EOF while reading P3 pixel data.");
            }

            var d = i * 4;
            rgba[d + 0] = (byte)Math.Clamp(int.Parse(rTok, CultureInfo.InvariantCulture), 0, 255);
            rgba[d + 1] = (byte)Math.Clamp(int.Parse(gTok, CultureInfo.InvariantCulture), 0, 255);
            rgba[d + 2] = (byte)Math.Clamp(int.Parse(bTok, CultureInfo.InvariantCulture), 0, 255);
            rgba[d + 3] = 255;
        }

        return (width, height, rgba);
    }

    private static bool TryReadToken(Stream stream, out string token)
    {
        token = string.Empty;
        var sb = new StringBuilder();
        var inToken = false;

        while (true)
        {
            var b = stream.ReadByte();
            if (b < 0)
            {
                token = sb.ToString();
                return token.Length > 0;
            }

            var c = (char)b;
            if (!inToken)
            {
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                if (c == '#')
                {
                    while (true)
                    {
                        var cb = stream.ReadByte();
                        if (cb < 0 || cb == '\n')
                        {
                            break;
                        }
                    }

                    continue;
                }

                inToken = true;
                sb.Append(c);
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                token = sb.ToString();
                return token.Length > 0;
            }

            if (c == '#')
            {
                token = sb.ToString();
                while (true)
                {
                    var cb = stream.ReadByte();
                    if (cb < 0 || cb == '\n')
                    {
                        break;
                    }
                }

                return token.Length > 0;
            }

            sb.Append(c);
        }
    }

    private static void ApplyEffects(byte[] src, byte[] dst, int width, int height, UiImageEffects effects)
    {
        Array.Copy(src, dst, src.Length);

        if (effects.Pixelate > 1)
        {
            ApplyPixelate(dst, width, height, effects.Pixelate);
        }

        for (var i = 0; i < dst.Length; i += 4)
        {
            var r = dst[i + 0];
            var g = dst[i + 1];
            var b = dst[i + 2];

            if (effects.Grayscale)
            {
                var l = (int)MathF.Round(r * 0.2126f + g * 0.7152f + b * 0.0722f);
                r = g = b = (byte)Math.Clamp(l, 0, 255);
            }

            if (effects.Invert)
            {
                r = (byte)(255 - r);
                g = (byte)(255 - g);
                b = (byte)(255 - b);
            }

            var rf = ((r - 128f) * effects.Contrast + 128f) * effects.Brightness;
            var gf = ((g - 128f) * effects.Contrast + 128f) * effects.Brightness;
            var bf = ((b - 128f) * effects.Contrast + 128f) * effects.Brightness;

            dst[i + 0] = (byte)Math.Clamp((int)MathF.Round(rf), 0, 255);
            dst[i + 1] = (byte)Math.Clamp((int)MathF.Round(gf), 0, 255);
            dst[i + 2] = (byte)Math.Clamp((int)MathF.Round(bf), 0, 255);
            dst[i + 3] = 255;
        }
    }

    private static void ApplyPixelate(byte[] rgba, int width, int height, int block)
    {
        for (var y = 0; y < height; y += block)
        {
            for (var x = 0; x < width; x += block)
            {
                var idx = ((y * width) + x) * 4;
                var r = rgba[idx + 0];
                var g = rgba[idx + 1];
                var b = rgba[idx + 2];

                var yMax = Math.Min(height, y + block);
                var xMax = Math.Min(width, x + block);
                for (var yy = y; yy < yMax; yy++)
                {
                    var row = yy * width;
                    for (var xx = x; xx < xMax; xx++)
                    {
                        var p = (row + xx) * 4;
                        rgba[p + 0] = r;
                        rgba[p + 1] = g;
                        rgba[p + 2] = b;
                    }
                }
            }
        }
    }

    private static UiVector2 RotateAround(UiVector2 center, float localX, float localY, float cos, float sin)
    {
        var x = localX * cos - localY * sin;
        var y = localX * sin + localY * cos;
        return new UiVector2(center.X + x, center.Y + y);
    }

    private static void DrawCheckerBackground(UiDrawListBuilder drawList, UiRect rect)
    {
        const float cell = 18f;
        var c0 = new UiColor(0xFF2C2C2C);
        var c1 = new UiColor(0xFF3A3A3A);

        var y = rect.Y;
        var row = 0;
        while (y < rect.Y + rect.Height)
        {
            var x = rect.X;
            var col = 0;
            while (x < rect.X + rect.Width)
            {
                var w = MathF.Min(cell, rect.X + rect.Width - x);
                var h = MathF.Min(cell, rect.Y + rect.Height - y);
                var color = ((row + col) & 1) == 0 ? c0 : c1;
                drawList.AddRectFilled(new UiRect(x, y, w, h), color);
                x += cell;
                col++;
            }

            y += cell;
            row++;
        }
    }
}
