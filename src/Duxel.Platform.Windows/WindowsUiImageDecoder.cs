using System;
using System.IO;
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
        if (!CanDecode(path))
        {
            throw new NotSupportedException($"Unsupported extension for WindowsUiImageDecoder: {Path.GetExtension(path)}");
        }

        return WindowsWicImageCodec.DecodeSingleFrame(path);
    }
}
