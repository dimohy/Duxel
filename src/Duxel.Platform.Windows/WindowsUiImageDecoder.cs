using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Duxel.Core;

namespace Duxel.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsUiImageDecoder : IUiImageDecoder
{
    public static WindowsUiImageDecoder Shared { get; } = new();

    public bool CanDecode(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp";
    }

    public UiImageData Decode(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsUiImageDecoder requires Windows runtime.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Image file not found.", path);
        }

        if (!CanDecode(path))
        {
            throw new NotSupportedException($"Unsupported extension for WindowsUiImageDecoder: {Path.GetExtension(path)}");
        }

        using var source = new Bitmap(path);
        using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        var width = bitmap.Width;
        var height = bitmap.Height;
        var rect = new Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var byteCount = checked(Math.Abs(stride) * height);
            var bgra = new byte[byteCount];
            Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);

            var rgba = new byte[checked(width * height * 4)];
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

            return new UiImageData(width, height, rgba);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
