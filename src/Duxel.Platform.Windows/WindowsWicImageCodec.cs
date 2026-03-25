using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Duxel.Core;

namespace Duxel.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsWicAnimationData
{
    public WindowsWicAnimationData(UiImageData[] frames, float[] durationsSec, bool isAnimated)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(durationsSec);

        if (frames.Length == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        if (frames.Length != durationsSec.Length)
        {
            throw new ArgumentException("Frame and duration counts must match.", nameof(durationsSec));
        }

        Frames = frames;
        DurationsSec = durationsSec;
        IsAnimated = isAnimated;
    }

    public UiImageData[] Frames { get; }
    public float[] DurationsSec { get; }
    public bool IsAnimated { get; }
}

[SupportedOSPlatform("windows")]
public static class WindowsWicImageCodec
{
    public static UiImageData DecodeSingleFrame(string path)
    {
        var animation = Decode(path);
        return animation.Frames[0];
    }

    public static WindowsWicAnimationData Decode(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsWicImageCodec requires Windows runtime.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Image file not found.", path);
        }

        using var comScope = ComScope.Initialize();

        nint factory = nint.Zero;
        nint decoder = nint.Zero;

        try
        {
            factory = CreateFactory();
            decoder = CreateDecoder(factory, path);

            WicNative.IWICBitmapDecoder_GetFrameCount(decoder, out var frameCount).ThrowIfFailed("Failed to read image frame count.");
            var frames = checked((int)Math.Max(frameCount, 1u));

            if (!string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase) || frames == 1)
            {
                return DecodeSingleFrameOnly(factory, decoder);
            }

            return DecodeGifAnimation(factory, decoder, frames);
        }
        finally
        {
            Release(decoder);
            Release(factory);
        }
    }

    private static WindowsWicAnimationData DecodeSingleFrameOnly(nint factory, nint decoder)
    {
        nint frame = nint.Zero;

        try
        {
            WicNative.IWICBitmapDecoder_GetFrame(decoder, 0, out frame).ThrowIfFailed("Failed to load image frame.");
            var image = ReadBitmapSource(factory, frame);
            return new WindowsWicAnimationData([image], [0.1f], false);
        }
        finally
        {
            Release(frame);
        }
    }

    private static WindowsWicAnimationData DecodeGifAnimation(nint factory, nint decoder, int frameCount)
    {
        TryReadCanvasSize(decoder, out var canvasWidth, out var canvasHeight);

        var frames = new GifFrame[frameCount];
        var maxRight = canvasWidth;
        var maxBottom = canvasHeight;

        for (var i = 0; i < frameCount; i++)
        {
            frames[i] = ReadGifFrame(factory, decoder, i);
            maxRight = Math.Max(maxRight, frames[i].Left + frames[i].Width);
            maxBottom = Math.Max(maxBottom, frames[i].Top + frames[i].Height);
        }

        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            canvasWidth = maxRight;
            canvasHeight = maxBottom;
        }

        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            throw new InvalidDataException("GIF canvas size is invalid.");
        }

        var canvas = new byte[checked(canvasWidth * canvasHeight * 4)];
        var outputFrames = new UiImageData[frameCount];
        var durations = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var frame = frames[i];
            byte[]? previousCanvas = null;
            if (frame.DisposalMethod == GifDisposalRestorePrevious)
            {
                previousCanvas = new byte[canvas.Length];
                Array.Copy(canvas, previousCanvas, canvas.Length);
            }

            CompositeFrame(canvas, canvasWidth, canvasHeight, frame);

            var snapshot = new byte[canvas.Length];
            Array.Copy(canvas, snapshot, canvas.Length);
            outputFrames[i] = new UiImageData(canvasWidth, canvasHeight, snapshot);
            durations[i] = frame.DelaySec;

            switch (frame.DisposalMethod)
            {
                case GifDisposalRestoreBackground:
                    ClearRect(canvas, canvasWidth, canvasHeight, frame.Left, frame.Top, frame.Width, frame.Height);
                    break;
                case GifDisposalRestorePrevious:
                    if (previousCanvas is null)
                    {
                        throw new InvalidOperationException("Previous GIF canvas state was not captured.");
                    }

                    Array.Copy(previousCanvas, canvas, canvas.Length);
                    break;
            }
        }

        return new WindowsWicAnimationData(outputFrames, durations, true);
    }

    private static GifFrame ReadGifFrame(nint factory, nint decoder, int frameIndex)
    {
        nint frame = nint.Zero;
        nint metadataReader = nint.Zero;

        try
        {
            WicNative.IWICBitmapDecoder_GetFrame(decoder, (uint)frameIndex, out frame).ThrowIfFailed($"Failed to load GIF frame {frameIndex}.");
            var image = ReadBitmapSource(factory, frame);

            WicNative.IWICBitmapFrameDecode_GetMetadataQueryReader(frame, out metadataReader)
                .ThrowIfFailed($"Failed to load GIF metadata for frame {frameIndex}.");

            var left = ReadMetadataUInt32(metadataReader, "/imgdesc/Left") ?? 0u;
            var top = ReadMetadataUInt32(metadataReader, "/imgdesc/Top") ?? 0u;
            var width = ReadMetadataUInt32(metadataReader, "/imgdesc/Width") ?? (uint)image.Width;
            var height = ReadMetadataUInt32(metadataReader, "/imgdesc/Height") ?? (uint)image.Height;
            var delayTicks = ReadMetadataUInt32(metadataReader, "/grctlext/Delay") ?? 10u;
            var disposal = (byte)(ReadMetadataUInt32(metadataReader, "/grctlext/Disposal") ?? 0u);

            return new GifFrame(
                checked((int)left),
                checked((int)top),
                checked((int)width),
                checked((int)height),
                image.RgbaPixels,
                Math.Max(0.02f, delayTicks / 100f),
                disposal);
        }
        finally
        {
            Release(metadataReader);
            Release(frame);
        }
    }

    private static UiImageData ReadBitmapSource(nint factory, nint bitmapSource)
    {
        WicNative.IWICBitmapSource_GetPixelFormat(bitmapSource, out var sourcePixelFormat)
            .ThrowIfFailed("Failed to read image pixel format.");

        var pixelFormat = WicNative.GuidWicPixelFormat32bppBGRA;

        if (sourcePixelFormat == pixelFormat)
        {
            return Read32bppBgraBitmapSource(bitmapSource);
        }

        if (sourcePixelFormat == WicNative.GuidWicPixelFormat24bppBGR)
        {
            return ReadPacked24bppBgrBitmapSource(bitmapSource);
        }

        if (sourcePixelFormat == WicNative.GuidWicPixelFormat32bppBGR)
        {
            return Read32bppBgrBitmapSource(bitmapSource);
        }

        nint convertedSource = nint.Zero;

        try
        {
            convertedSource = CreateFormatConverter(factory, bitmapSource, pixelFormat, sourcePixelFormat);

            WicNative.IWICBitmapSource_GetSize(convertedSource, out var width, out var height)
                .ThrowIfFailed("Failed to read image dimensions.");

            if (width == 0 || height == 0)
            {
                throw new InvalidDataException("Decoded image size is invalid.");
            }

            var stride = checked((int)width * 4);
            var byteCount = checked(stride * (int)height);
            var bgra = new byte[byteCount];

            CopyPixels(convertedSource, stride, bgra, "Failed to copy image pixels.");

            return new UiImageData(checked((int)width), checked((int)height), ConvertBgraToRgba(bgra));
        }
        finally
        {
            Release(convertedSource);
        }
    }

    private static UiImageData Read32bppBgraBitmapSource(nint bitmapSource)
    {
        WicNative.IWICBitmapSource_GetSize(bitmapSource, out var width, out var height)
            .ThrowIfFailed("Failed to read image dimensions.");

        if (width == 0 || height == 0)
        {
            throw new InvalidDataException("Decoded image size is invalid.");
        }

        var stride = checked((int)width * 4);
        var byteCount = checked(stride * (int)height);
        var bgra = new byte[byteCount];

        CopyPixels(bitmapSource, stride, bgra, "Failed to copy image pixels.");

        return new UiImageData(checked((int)width), checked((int)height), ConvertBgraToRgba(bgra));
    }

    private static UiImageData ReadPacked24bppBgrBitmapSource(nint bitmapSource)
    {
        WicNative.IWICBitmapSource_GetSize(bitmapSource, out var width, out var height)
            .ThrowIfFailed("Failed to read image dimensions.");

        if (width == 0 || height == 0)
        {
            throw new InvalidDataException("Decoded image size is invalid.");
        }

        var sourceStride = checked((int)width * 3);
        var sourceByteCount = checked(sourceStride * (int)height);
        var bgr = new byte[sourceByteCount];

        CopyPixels(bitmapSource, sourceStride, bgr, "Failed to copy packed 24bpp BGR image pixels.");

        var rgba = new byte[checked((int)width * (int)height * 4)];
        for (var y = 0; y < (int)height; y++)
        {
            var srcRow = y * sourceStride;
            var dstRow = y * checked((int)width * 4);

            for (var x = 0; x < (int)width; x++)
            {
                var src = srcRow + (x * 3);
                var dst = dstRow + (x * 4);
                rgba[dst + 0] = bgr[src + 2];
                rgba[dst + 1] = bgr[src + 1];
                rgba[dst + 2] = bgr[src + 0];
                rgba[dst + 3] = byte.MaxValue;
            }
        }

        return new UiImageData(checked((int)width), checked((int)height), rgba);
    }

    private static UiImageData Read32bppBgrBitmapSource(nint bitmapSource)
    {
        WicNative.IWICBitmapSource_GetSize(bitmapSource, out var width, out var height)
            .ThrowIfFailed("Failed to read image dimensions.");

        if (width == 0 || height == 0)
        {
            throw new InvalidDataException("Decoded image size is invalid.");
        }

        var sourceStride = checked((int)width * 4);
        var sourceByteCount = checked(sourceStride * (int)height);
        var bgr = new byte[sourceByteCount];

        CopyPixels(bitmapSource, sourceStride, bgr, "Failed to copy 32bpp BGR image pixels.");

        var rgba = new byte[checked((int)width * (int)height * 4)];
        for (var y = 0; y < (int)height; y++)
        {
            var srcRow = y * sourceStride;
            var dstRow = y * checked((int)width * 4);

            for (var x = 0; x < (int)width; x++)
            {
                var src = srcRow + (x * 4);
                var dst = dstRow + (x * 4);
                rgba[dst + 0] = bgr[src + 2];
                rgba[dst + 1] = bgr[src + 1];
                rgba[dst + 2] = bgr[src + 0];
                rgba[dst + 3] = byte.MaxValue;
            }
        }

        return new UiImageData(checked((int)width), checked((int)height), rgba);
    }

    private static nint CreateFormatConverter(nint factory, nint bitmapSource, Guid pixelFormat, Guid sourcePixelFormat)
    {
        WicNative.IWICImagingFactory_CreateFormatConverter(factory, out var converter)
            .ThrowIfFailed("Failed to create a WIC format converter.");

        if (converter == nint.Zero)
        {
            throw new InvalidOperationException("WIC format converter creation returned a null pointer.");
        }

        try
        {
            WicNative.IWICFormatConverter_Initialize(
                    converter,
                    bitmapSource,
                    ref pixelFormat,
                    WicBitmapDitherTypeNone,
                    nint.Zero,
                    0d,
                    WicBitmapPaletteTypeCustom)
                .ThrowIfFailed($"Failed to convert image from {sourcePixelFormat:D} into 32bpp BGRA.");

            var convertedSource = converter;
            converter = nint.Zero;
            return convertedSource;
        }
        finally
        {
            Release(converter);
        }
    }

    private static void CopyPixels(nint bitmapSource, int stride, byte[] buffer, string errorMessage)
    {
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            WicNative.IWICBitmapSource_CopyPixels(bitmapSource, nint.Zero, (uint)stride, (uint)buffer.Length, handle.AddrOfPinnedObject())
                .ThrowIfFailed(errorMessage);
        }
        finally
        {
            handle.Free();
        }
    }

    private static void TryReadCanvasSize(nint decoder, out int width, out int height)
    {
        width = 0;
        height = 0;

        nint metadataReader = nint.Zero;

        try
        {
            var hr = WicNative.IWICBitmapDecoder_GetMetadataQueryReader(decoder, out metadataReader);
            if (hr < 0)
            {
                return;
            }

            width = checked((int)(ReadMetadataUInt32(metadataReader, "/logscrdesc/Width") ?? 0u));
            height = checked((int)(ReadMetadataUInt32(metadataReader, "/logscrdesc/Height") ?? 0u));
        }
        finally
        {
            Release(metadataReader);
        }
    }

    private static uint? ReadMetadataUInt32(nint metadataReader, string query)
    {
        var hr = WicNative.IWICMetadataQueryReader_GetMetadataByName(metadataReader, query, out var value);
        if (hr < 0)
        {
            return null;
        }

        try
        {
            return value.vt switch
            {
                VariantTypeUi1 => value.bVal,
                VariantTypeUi2 => value.uiVal,
                VariantTypeUi4 => value.ulVal,
                VariantTypeI1 => unchecked((byte)value.cVal),
                VariantTypeI2 => unchecked((ushort)value.iVal),
                VariantTypeI4 => unchecked((uint)value.lVal),
                _ => throw new InvalidDataException($"Unsupported WIC metadata variant type: 0x{value.vt:X4}")
            };
        }
        finally
        {
            Ole32.PropVariantClear(ref value).ThrowIfFailed($"Failed to clear metadata variant '{query}'.");
        }
    }

    private static void CompositeFrame(byte[] canvas, int canvasWidth, int canvasHeight, GifFrame frame)
    {
        var frameRowStride = checked(frame.Width * 4);

        for (var y = 0; y < frame.Height; y++)
        {
            var dstY = frame.Top + y;
            if ((uint)dstY >= (uint)canvasHeight)
            {
                continue;
            }

            var srcRow = y * frameRowStride;
            for (var x = 0; x < frame.Width; x++)
            {
                var dstX = frame.Left + x;
                if ((uint)dstX >= (uint)canvasWidth)
                {
                    continue;
                }

                var src = srcRow + (x * 4);
                var alpha = frame.Rgba[src + 3];
                if (alpha == 0)
                {
                    continue;
                }

                var dst = ((dstY * canvasWidth) + dstX) * 4;
                if (alpha == byte.MaxValue)
                {
                    canvas[dst + 0] = frame.Rgba[src + 0];
                    canvas[dst + 1] = frame.Rgba[src + 1];
                    canvas[dst + 2] = frame.Rgba[src + 2];
                    canvas[dst + 3] = byte.MaxValue;
                    continue;
                }

                var srcAlpha = alpha / 255f;
                var dstAlpha = canvas[dst + 3] / 255f;
                var outAlpha = srcAlpha + (dstAlpha * (1f - srcAlpha));
                if (outAlpha <= 0f)
                {
                    canvas[dst + 0] = 0;
                    canvas[dst + 1] = 0;
                    canvas[dst + 2] = 0;
                    canvas[dst + 3] = 0;
                    continue;
                }

                canvas[dst + 0] = BlendChannel(frame.Rgba[src + 0], canvas[dst + 0], srcAlpha, dstAlpha, outAlpha);
                canvas[dst + 1] = BlendChannel(frame.Rgba[src + 1], canvas[dst + 1], srcAlpha, dstAlpha, outAlpha);
                canvas[dst + 2] = BlendChannel(frame.Rgba[src + 2], canvas[dst + 2], srcAlpha, dstAlpha, outAlpha);
                canvas[dst + 3] = (byte)Math.Clamp((int)MathF.Round(outAlpha * 255f), 0, 255);
            }
        }
    }

    private static void ClearRect(byte[] canvas, int canvasWidth, int canvasHeight, int left, int top, int width, int height)
    {
        var x0 = Math.Max(0, left);
        var y0 = Math.Max(0, top);
        var x1 = Math.Min(canvasWidth, left + width);
        var y1 = Math.Min(canvasHeight, top + height);

        for (var y = y0; y < y1; y++)
        {
            var row = y * canvasWidth;
            for (var x = x0; x < x1; x++)
            {
                var offset = (row + x) * 4;
                canvas[offset + 0] = 0;
                canvas[offset + 1] = 0;
                canvas[offset + 2] = 0;
                canvas[offset + 3] = 0;
            }
        }
    }

    private static byte[] ConvertBgraToRgba(byte[] bgra)
    {
        var rgba = new byte[bgra.Length];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            rgba[i + 0] = bgra[i + 2];
            rgba[i + 1] = bgra[i + 1];
            rgba[i + 2] = bgra[i + 0];
            rgba[i + 3] = bgra[i + 3];
        }

        return rgba;
    }

    private static byte BlendChannel(byte src, byte dst, float srcAlpha, float dstAlpha, float outAlpha)
    {
        var value = ((src * srcAlpha) + (dst * dstAlpha * (1f - srcAlpha))) / outAlpha;
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }

    private static nint CreateFactory()
    {
        var clsid = WicNative.ClsidWicImagingFactory;
        var iid = WicNative.IidIwicImagingFactory;
        Ole32.CoCreateInstance(ref clsid, nint.Zero, Ole32.ClsctxInprocServer, ref iid, out var factory)
            .ThrowIfFailed("Failed to create WIC imaging factory.");

        if (factory == nint.Zero)
        {
            throw new InvalidOperationException("WIC imaging factory creation returned a null pointer.");
        }

        return factory;
    }

    private static nint CreateDecoder(nint factory, string path)
    {
        WicNative.IWICImagingFactory_CreateDecoderFromFilename(
                factory,
                path,
                nint.Zero,
                GenericRead,
                WicDecodeMetadataCacheOnLoad,
                out var decoder)
            .ThrowIfFailed($"Failed to create a WIC decoder for '{path}'.");

        if (decoder == nint.Zero)
        {
            throw new InvalidOperationException("WIC decoder creation returned a null pointer.");
        }

        return decoder;
    }

    private static void Release(nint comObject)
    {
        if (comObject != nint.Zero)
        {
            _ = Marshal.Release(comObject);
        }
    }

    private const uint GenericRead = 0x80000000;
    private const uint WicDecodeMetadataCacheOnLoad = 0x1;
    private const uint WicBitmapDitherTypeNone = 0;
    private const uint WicBitmapPaletteTypeCustom = 0;

    private const byte GifDisposalRestoreBackground = 2;
    private const byte GifDisposalRestorePrevious = 3;

    private const ushort VariantTypeI1 = 16;
    private const ushort VariantTypeUi1 = 17;
    private const ushort VariantTypeI2 = 2;
    private const ushort VariantTypeUi2 = 18;
    private const ushort VariantTypeI4 = 3;
    private const ushort VariantTypeUi4 = 19;

    private readonly record struct GifFrame(int Left, int Top, int Width, int Height, byte[] Rgba, float DelaySec, byte DisposalMethod);

    private sealed class ComScope : IDisposable
    {
        private readonly bool _shouldUninitialize;

        private ComScope(bool shouldUninitialize)
        {
            _shouldUninitialize = shouldUninitialize;
        }

        public static ComScope Initialize()
        {
            var hr = Ole32.CoInitializeEx(nint.Zero, Ole32.CoinitApartmentThreaded);
            if (hr < 0 && hr != Ole32.RpcEChangedMode)
            {
                hr.ThrowIfFailed("Failed to initialize COM for WIC.");
            }

            return new ComScope(hr >= 0);
        }

        public void Dispose()
        {
            if (_shouldUninitialize)
            {
                Ole32.CoUninitialize();
            }
        }
    }
}

[StructLayout(LayoutKind.Explicit)]
internal struct WicPropVariant
{
    [FieldOffset(0)]
    public ushort vt;
    [FieldOffset(8)]
    public sbyte cVal;
    [FieldOffset(8)]
    public byte bVal;
    [FieldOffset(8)]
    public short iVal;
    [FieldOffset(8)]
    public ushort uiVal;
    [FieldOffset(8)]
    public int lVal;
    [FieldOffset(8)]
    public uint ulVal;
}

internal static partial class WicNative
{
    internal static readonly Guid ClsidWicImagingFactory = new("CACAF262-9370-4615-A13B-9F5539DA4C0A");
    internal static readonly Guid IidIwicImagingFactory = new("EC5EC8A9-C395-4314-9C77-54D7A935FF70");
    internal static readonly Guid GuidWicPixelFormat24bppBGR = new("6FDDC324-4E03-4BFE-B185-3D77768DC90C");
    internal static readonly Guid GuidWicPixelFormat32bppBGR = new("6FDDC324-4E03-4BFE-B185-3D77768DC90F");
    internal static readonly Guid GuidWicPixelFormat32bppBGRA = new("6FDDC324-4E03-4BFE-B185-3D77768DC900");

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICImagingFactory_CreateDecoderFromFilename_Proxy", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int IWICImagingFactory_CreateDecoderFromFilename(
        nint @this,
        string filename,
        nint vendor,
        uint desiredAccess,
        uint metadataOptions,
        out nint decoder);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICBitmapDecoder_GetFrameCount_Proxy")]
    internal static partial int IWICBitmapDecoder_GetFrameCount(nint @this, out uint frameCount);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICBitmapDecoder_GetFrame_Proxy")]
    internal static partial int IWICBitmapDecoder_GetFrame(nint @this, uint index, out nint frame);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICBitmapDecoder_GetMetadataQueryReader_Proxy")]
    internal static partial int IWICBitmapDecoder_GetMetadataQueryReader(nint @this, out nint metadataReader);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICBitmapFrameDecode_GetMetadataQueryReader_Proxy")]
    internal static partial int IWICBitmapFrameDecode_GetMetadataQueryReader(nint @this, out nint metadataReader);

    [DllImport("windowscodecs.dll", EntryPoint = "IWICMetadataQueryReader_GetMetadataByName_Proxy", CharSet = CharSet.Unicode)]
    internal static extern int IWICMetadataQueryReader_GetMetadataByName(nint @this, string name, out WicPropVariant value);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICBitmapSource_GetSize_Proxy")]
    internal static partial int IWICBitmapSource_GetSize(nint @this, out uint width, out uint height);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICBitmapSource_GetPixelFormat_Proxy")]
    internal static partial int IWICBitmapSource_GetPixelFormat(nint @this, out Guid pixelFormat);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICBitmapSource_CopyPixels_Proxy")]
    internal static partial int IWICBitmapSource_CopyPixels(nint @this, nint rect, uint stride, uint bufferSize, nint buffer);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICImagingFactory_CreateFormatConverter_Proxy")]
    internal static partial int IWICImagingFactory_CreateFormatConverter(nint @this, out nint converter);

    [LibraryImport("windowscodecs.dll", EntryPoint = "IWICFormatConverter_Initialize_Proxy")]
    internal static partial int IWICFormatConverter_Initialize(
        nint @this,
        nint source,
        ref Guid dstFormat,
        uint dither,
        nint palette,
        double alphaThresholdPercent,
        uint paletteTranslate);
}

internal static partial class Ole32
{
    [LibraryImport("ole32.dll")]
    internal static partial void CoUninitialize();

    [DllImport("ole32.dll")]
    internal static extern int PropVariantClear(ref WicPropVariant pvar);
}

internal static class HResultExtensions
{
    internal static void ThrowIfFailed(this int hr, string message)
    {
        if (hr < 0)
        {
            throw new COMException(message, hr);
        }
    }
}