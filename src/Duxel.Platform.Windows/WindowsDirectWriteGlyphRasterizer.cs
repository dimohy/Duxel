using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed class WindowsDirectWriteGlyphRasterizer : IPlatformGlyphBitmapRasterizer
{
    public static readonly WindowsDirectWriteGlyphRasterizer Instance = new();

    private const int DWriteFactoryTypeShared = 0;
    private const int DWriteRenderingModeNaturalSymmetric = 5;
    private const int DWriteMeasuringModeNatural = 0;
    private const int DWriteTextureClearType3x1 = 1;

    private static readonly Guid IdWriteFactoryGuid = new("B859EE5A-D838-4B5B-A2E8-1ADC7D93DB48");
    private static readonly object Sync = new();
    private static readonly object DiagnosticsSync = new();
    private static readonly Dictionary<string, nint> FontFaceCache = new(StringComparer.OrdinalIgnoreCase);

    private static nint _factory;
    private static int _failureCount;
    private static int _disabled;

    public string CacheKeyTag => "dwrite";

    public bool TryRasterize(
        string fontPath,
        int codepoint,
        int glyphIndex,
        int fontSize,
        int oversample,
        float sourceScale,
        float renderScale,
        out GlyphRasterizationResult result)
    {
        result = default;
        if (Volatile.Read(ref _disabled) != 0)
        {
            return false;
        }

        Log($"TryRasterize begin cp=U+{codepoint:X4} glyphIndex={glyphIndex} size={fontSize} oversample={oversample} scale={sourceScale:0.###}");
        if ((uint)codepoint > 0xFFFF)
        {
            Log($"TryRasterize skip invalid-codepoint cp=U+{codepoint:X4}");
            return false;
        }

        if (!TryGetFontFace(fontPath, out var fontFace))
        {
            MarkFailure($"font-face cp=U+{codepoint:X4}");
            Log($"TryRasterize fail font-face cp=U+{codepoint:X4} path={fontPath}");
            return false;
        }

        if (glyphIndex <= 0 || glyphIndex > ushort.MaxValue)
        {
            Log($"TryRasterize skip invalid-glyph-index cp=U+{codepoint:X4} glyphIndex={glyphIndex}");
            return false;
        }

        var glyphIndices = new[] { (ushort)glyphIndex };
        var glyphIndicesHandle = GCHandle.Alloc(glyphIndices, GCHandleType.Pinned);
        nint glyphRunAnalysis = 0;

        try
        {
            var oversampleFactor = Math.Max(1, oversample);
            var rasterizedEmSize = MathF.Max(1f, fontSize * sourceScale * oversampleFactor);

            var glyphRun = new DWriteGlyphRun
            {
                FontFace = fontFace,
                FontEmSize = rasterizedEmSize,
                GlyphCount = 1,
                GlyphIndices = glyphIndicesHandle.AddrOfPinnedObject(),
                GlyphAdvances = 0,
                GlyphOffsets = 0,
                IsSideways = false,
                BidiLevel = 0,
            };

            var createGlyphRunAnalysis = GetMethod<CreateGlyphRunAnalysisDelegate>(_factory, 23);
            var hr = createGlyphRunAnalysis(
                _factory,
                ref glyphRun,
                1f,
                0,
                DWriteRenderingModeNaturalSymmetric,
                DWriteMeasuringModeNatural,
                0f,
                0f,
                out glyphRunAnalysis);
            if (hr < 0 || glyphRunAnalysis == 0)
            {
                MarkFailure($"create-analysis cp=U+{codepoint:X4} hr=0x{hr:X8}");
                Log($"TryRasterize fail create-analysis cp=U+{codepoint:X4} hr=0x{hr:X8}");
                return false;
            }

            var getBounds = GetMethod<GetAlphaTextureBoundsDelegate>(glyphRunAnalysis, 3);
            hr = getBounds(glyphRunAnalysis, DWriteTextureClearType3x1, out var bounds);
            if (hr < 0)
            {
                MarkFailure($"bounds cp=U+{codepoint:X4} hr=0x{hr:X8}");
                Log($"TryRasterize fail bounds cp=U+{codepoint:X4} hr=0x{hr:X8}");
                return false;
            }

            var width = Math.Max(0, bounds.Right - bounds.Left);
            var height = Math.Max(0, bounds.Bottom - bounds.Top);
            if (width == 0 || height == 0)
            {
                MarkFailure($"empty-bounds cp=U+{codepoint:X4}");
                Log($"TryRasterize empty-bounds cp=U+{codepoint:X4} bounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom})");
                return false;
            }

            var clearTypeAlpha = new byte[width * height * 3];
            var clearTypeHandle = GCHandle.Alloc(clearTypeAlpha, GCHandleType.Pinned);
            try
            {
                var createAlphaTexture = GetMethod<CreateAlphaTextureDelegate>(glyphRunAnalysis, 4);
                hr = createAlphaTexture(
                    glyphRunAnalysis,
                    DWriteTextureClearType3x1,
                    ref bounds,
                    clearTypeHandle.AddrOfPinnedObject(),
                    (uint)clearTypeAlpha.Length);
                if (hr < 0)
                {
                    MarkFailure($"alpha-texture cp=U+{codepoint:X4} hr=0x{hr:X8}");
                    Log($"TryRasterize fail alpha-texture cp=U+{codepoint:X4} hr=0x{hr:X8} size={clearTypeAlpha.Length}");
                    return false;
                }
            }
            finally
            {
                clearTypeHandle.Free();
            }

            var alpha = new byte[width * height];
            var hasCoverage = false;
            for (var i = 0; i < alpha.Length; i++)
            {
                var sourceIndex = i * 3;
                var r = clearTypeAlpha[sourceIndex];
                var g = clearTypeAlpha[sourceIndex + 1];
                var b = clearTypeAlpha[sourceIndex + 2];
                var value = (byte)((54 * r + 183 * g + 19 * b) >> 8);
                alpha[i] = value;
                hasCoverage |= value != 0;
            }

            if (!hasCoverage)
            {
                MarkFailure($"empty-alpha cp=U+{codepoint:X4}");
                Log($"TryRasterize empty-alpha cp=U+{codepoint:X4} bounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom})");
                return false;
            }

            if (oversampleFactor > 1)
            {
                alpha = Downsample(alpha, width, height, oversampleFactor, out width, out height);
            }

            result = new GlyphRasterizationResult(
                width,
                height,
                alpha,
                HasPlacementMetrics: false,
                OffsetX: 0f,
                OffsetY: 0f,
                Advance: 0f
            );
            Interlocked.Exchange(ref _failureCount, 0);
            Log($"TryRasterize success cp=U+{codepoint:X4} glyphIndex={glyphIndex} width={width} height={height} bounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom})");
            return true;
        }
        finally
        {
            glyphIndicesHandle.Free();
            if (glyphRunAnalysis != 0)
            {
                Release(glyphRunAnalysis);
            }
        }
    }

    private static void MarkFailure(string reason)
    {
        var count = Interlocked.Increment(ref _failureCount);
        if (count < 16)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _disabled, 1, 0) == 0)
        {
            Log($"FuseOff after repeated failures: {reason}");
        }
    }

    private static byte[] Downsample(byte[] source, int sourceWidth, int sourceHeight, int oversample, out int targetWidth, out int targetHeight)
    {
        targetWidth = Math.Max(1, (sourceWidth + oversample - 1) / oversample);
        targetHeight = Math.Max(1, (sourceHeight + oversample - 1) / oversample);
        var output = new byte[targetWidth * targetHeight];

        for (var y = 0; y < targetHeight; y++)
        {
            for (var x = 0; x < targetWidth; x++)
            {
                var sum = 0;
                var count = 0;
                var startX = x * oversample;
                var startY = y * oversample;
                for (var oy = 0; oy < oversample; oy++)
                {
                    var sy = startY + oy;
                    if (sy >= sourceHeight)
                    {
                        break;
                    }

                    for (var ox = 0; ox < oversample; ox++)
                    {
                        var sx = startX + ox;
                        if (sx >= sourceWidth)
                        {
                            break;
                        }

                        sum += source[(sy * sourceWidth) + sx];
                        count++;
                    }
                }

                output[(y * targetWidth) + x] = count == 0 ? (byte)0 : (byte)(sum / count);
            }
        }

        return output;
    }

    private static bool TryGetFontFace(string fontPath, out nint fontFace)
    {
        fontFace = 0;

        lock (Sync)
        {
            if (FontFaceCache.TryGetValue(fontPath, out fontFace) && fontFace != 0)
            {
                return true;
            }

            if (_factory == 0 && !TryCreateFactory(out _factory))
            {
                Log("TryGetFontFace fail create-factory");
                return false;
            }

            var createFontFileReference = GetMethod<CreateFontFileReferenceDelegate>(_factory, 7);
            var hr = createFontFileReference(_factory, fontPath, 0, out var fontFile);
            if (hr < 0 || fontFile == 0)
            {
                Log($"TryGetFontFace fail file-ref path={fontPath} hr=0x{hr:X8}");
                return false;
            }

            try
            {
                var analyze = GetMethod<AnalyzeFontFileDelegate>(fontFile, 5);
                hr = analyze(fontFile, out var isSupported, out _, out var fontFaceType, out var numberOfFaces);
                if (hr < 0 || !isSupported || numberOfFaces == 0)
                {
                    Log($"TryGetFontFace fail analyze-file path={fontPath} hr=0x{hr:X8} supported={isSupported} faces={numberOfFaces}");
                    return false;
                }

                var createFontFace = GetMethod<CreateFontFaceDelegate>(_factory, 9);
                var files = new[] { fontFile };
                var filesHandle = GCHandle.Alloc(files, GCHandleType.Pinned);
                try
                {
                    hr = createFontFace(
                        _factory,
                        fontFaceType,
                        1,
                        filesHandle.AddrOfPinnedObject(),
                        0,
                        0,
                        out fontFace);
                    if (hr < 0 || fontFace == 0)
                    {
                        Log($"TryGetFontFace fail create-face path={fontPath} hr=0x{hr:X8}");
                        return false;
                    }
                }
                finally
                {
                    filesHandle.Free();
                }
            }
            finally
            {
                Release(fontFile);
            }

            FontFaceCache[fontPath] = fontFace;
            return true;
        }
    }

    private static bool TryCreateFactory(out nint factory)
    {
        factory = 0;
        var iid = IdWriteFactoryGuid;
        var hr = DWriteCreateFactory(DWriteFactoryTypeShared, ref iid, out factory);
        Log($"TryCreateFactory hr=0x{hr:X8} factory=0x{factory.ToInt64():X}");
        return hr >= 0 && factory != 0;
    }

    private static void Log(string message)
    {
        var diagnosticsLogPath = Environment.GetEnvironmentVariable("DUXEL_DWRITE_DIAG_LOG");
        if (string.IsNullOrWhiteSpace(diagnosticsLogPath))
        {
            return;
        }

        try
        {
            var line = $"{DateTime.UtcNow:O} [DWriteGlyph] {message}";
            lock (DiagnosticsSync)
            {
                var directory = Path.GetDirectoryName(diagnosticsLogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(diagnosticsLogPath, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private static void Release(nint comPtr)
    {
        if (comPtr == 0)
        {
            return;
        }

        var release = GetMethod<ReleaseDelegate>(comPtr, 2);
        _ = release(comPtr);
    }

    private static TDelegate GetMethod<TDelegate>(nint comPtr, int slot)
        where TDelegate : Delegate
    {
        var vtable = Marshal.ReadIntPtr(comPtr);
        var fn = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(fn);
    }

    [DllImport("dwrite.dll", EntryPoint = "DWriteCreateFactory", ExactSpelling = true)]
    private static extern int DWriteCreateFactory(int factoryType, ref Guid iid, out nint factory);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateFontFileReferenceDelegate(nint self, [MarshalAs(UnmanagedType.LPWStr)] string filePath, nint lastWriteTime, out nint fontFile);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateFontFaceDelegate(nint self, int fontFaceType, uint numberOfFiles, nint fontFiles, uint faceIndex, uint fontFaceSimulationFlags, out nint fontFace);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AnalyzeFontFileDelegate(nint self, [MarshalAs(UnmanagedType.Bool)] out bool isSupportedFontType, out int fontFileType, out int fontFaceType, out uint numberOfFaces);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateGlyphRunAnalysisDelegate(
        nint self,
        ref DWriteGlyphRun glyphRun,
        float pixelsPerDip,
        nint transform,
        int renderingMode,
        int measuringMode,
        float baselineOriginX,
        float baselineOriginY,
        out nint glyphRunAnalysis);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetAlphaTextureBoundsDelegate(nint self, int textureType, out DWriteRect textureBounds);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateAlphaTextureDelegate(nint self, int textureType, ref DWriteRect textureBounds, nint alphaTexture, uint bufferSize);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseDelegate(nint self);

    [StructLayout(LayoutKind.Sequential)]
    private struct DWriteGlyphRun
    {
        public nint FontFace;
        public float FontEmSize;
        public uint GlyphCount;
        public nint GlyphIndices;
        public nint GlyphAdvances;
        public nint GlyphOffsets;
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsSideways;
        public uint BidiLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWriteRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
