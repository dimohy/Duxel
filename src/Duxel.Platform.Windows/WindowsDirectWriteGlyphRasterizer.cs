using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed class WindowsDirectWriteGlyphRasterizer : IPlatformGlyphBitmapRasterizer
{
    public static readonly WindowsDirectWriteGlyphRasterizer Instance = new();

    private const int DWriteFactoryTypeShared = 0;
    private const int DWriteRenderingModeGdiNatural = 3;
    private const int DWriteRenderingModeNaturalSymmetric = 5;
    private const int DWriteMeasuringModeGdiNatural = 2;
    private const int DWriteMeasuringModeNatural = 0;
    private const int DWriteTextureAliased1x1 = 0;
    private const int DWriteTextureClearType3x1 = 1;
    private const byte ClearTypeAlphaCutoff = 64;

    private static readonly Guid IdWriteFactoryGuid = new("B859EE5A-D838-4B5B-A2E8-1ADC7D93DB48");
    private static readonly object Sync = new();
    private static readonly object DiagnosticsSync = new();
    private static readonly Dictionary<string, nint> FontFaceCache = new(StringComparer.OrdinalIgnoreCase);

    private static nint _factory;

    public string CacheKeyTag => "dwrite";

    public bool HasGlyph(string fontPath, int codepoint)
        => TryResolveGlyphIndex(fontPath, codepoint, out _);

    public bool TryGetCodepointAdvance(string fontPath, int codepoint, int fontSize, float sourceScale, out float advance)
    {
        advance = 0f;
        if (!TryResolveGlyphIndex(fontPath, codepoint, out var glyphIndex)
            || glyphIndex <= 0
            || !TryGetFontFace(fontPath, out var fontFace)
            || fontFace == 0)
        {
            return false;
        }

        var emSize = MathF.Max(1f, fontSize * sourceScale);
        return TryGetGlyphAdvance(fontFace, (ushort)glyphIndex, emSize, out advance);
    }

    public bool TryRasterizeCodepoint(
        string fontPath,
        int codepoint,
        int fontSize,
        int oversample,
        float sourceScale,
        float renderScale,
        out GlyphRasterizationResult result)
    {
        result = default;
        if (!TryResolveGlyphIndex(fontPath, codepoint, out var glyphIndex))
        {
            return false;
        }

        return TryRasterize(fontPath, codepoint, glyphIndex, fontSize, oversample, sourceScale, renderScale, out result);
    }

    public bool TryRasterizeTextRun(
        string fontPath,
        string text,
        int fontSize,
        int oversample,
        float sourceScale,
        float renderScale,
        out TextRunRasterizationResult result)
    {
        lock (Sync)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(fontPath) || string.IsNullOrEmpty(text) || fontSize <= 0)
            {
                return false;
            }

            if (!TryGetFontFace(fontPath, out var fontFace) || fontFace == 0)
            {
                return false;
            }

            var oversampleFactor = Math.Max(1, oversample);
            var rasterizedEmSize = MathF.Max(1f, fontSize * sourceScale * oversampleFactor);
            var glyphIndices = new List<ushort>(text.Length);
            var glyphAdvances = new List<float>(text.Length);

            foreach (var rune in text.EnumerateRunes())
            {
                if (!TryResolveGlyphIndexFromFace(fontFace, rune.Value, out var glyphIndex))
                {
                    return false;
                }

                glyphIndices.Add(glyphIndex);
                if (!TryGetGlyphAdvance(fontFace, glyphIndex, rasterizedEmSize, out var advance))
                {
                    advance = rasterizedEmSize * 0.5f;
                }

                glyphAdvances.Add(advance);
            }

            if (glyphIndices.Count == 0)
            {
                return false;
            }

            var glyphIndicesArray = glyphIndices.ToArray();
            var glyphAdvancesArray = glyphAdvances.ToArray();
            var glyphIndicesHandle = GCHandle.Alloc(glyphIndicesArray, GCHandleType.Pinned);
            var glyphAdvancesHandle = GCHandle.Alloc(glyphAdvancesArray, GCHandleType.Pinned);
            nint glyphRunAnalysis = 0;

            try
            {
                var glyphRun = new DWriteGlyphRun
                {
                    FontFace = fontFace,
                    FontEmSize = rasterizedEmSize,
                    GlyphCount = (uint)glyphIndicesArray.Length,
                    GlyphIndices = glyphIndicesHandle.AddrOfPinnedObject(),
                    GlyphAdvances = glyphAdvancesHandle.AddrOfPinnedObject(),
                    GlyphOffsets = 0,
                    IsSideways = false,
                    BidiLevel = 0,
                };

                var createGlyphRunAnalysis = GetMethod<CreateGlyphRunAnalysisDelegate>(_factory, 23);
                var (renderingMode, measuringMode) = SelectRasterizationModes(rasterizedEmSize);
                var hr = createGlyphRunAnalysis(
                    _factory,
                    ref glyphRun,
                    1f,
                    0,
                    renderingMode,
                    measuringMode,
                    0f,
                    0f,
                    out glyphRunAnalysis);
                if (hr < 0 || glyphRunAnalysis == 0)
                {
                    return false;
                }

                if (!TryExtractAlphaTexture(glyphRunAnalysis, out var alpha, out var width, out var height, out var offsetX, out var offsetY))
                {
                    return false;
                }

                if (oversampleFactor > 1)
                {
                    alpha = Downsample(alpha, width, height, oversampleFactor, out width, out height);
                    offsetX = (int)MathF.Floor((float)offsetX / oversampleFactor);
                    offsetY = (int)MathF.Floor((float)offsetY / oversampleFactor);
                }

                var advanceSum = 0f;
                for (var i = 0; i < glyphAdvancesArray.Length; i++)
                {
                    advanceSum += glyphAdvancesArray[i];
                }

                var baseline = 0f;
                var getFontMetrics = GetMethod<GetFontMetricsDelegate>(fontFace, 8);
                getFontMetrics(fontFace, out var fontMetrics);
                if (fontMetrics.DesignUnitsPerEm > 0)
                {
                    baseline = (fontMetrics.Ascent * rasterizedEmSize) / fontMetrics.DesignUnitsPerEm;
                }

                result = new TextRunRasterizationResult(width, height, alpha, offsetX, offsetY, advanceSum, baseline);
                return true;
            }
            finally
            {
                glyphAdvancesHandle.Free();
                glyphIndicesHandle.Free();
                if (glyphRunAnalysis != 0)
                {
                    Release(glyphRunAnalysis);
                }
            }
        }
    }

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
        lock (Sync)
        {
            result = default;
            Log($"TryRasterize begin cp=U+{codepoint:X4} glyphIndex={glyphIndex} size={fontSize} oversample={oversample} scale={sourceScale:0.###}");
            if ((uint)codepoint > 0xFFFF)
            {
                Log($"TryRasterize skip invalid-codepoint cp=U+{codepoint:X4}");
                return false;
            }

            if (!TryGetFontFace(fontPath, out var fontFace))
            {
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
                var (renderingMode, measuringMode) = SelectRasterizationModes(rasterizedEmSize);
                var hr = createGlyphRunAnalysis(
                    _factory,
                    ref glyphRun,
                    1f,
                    0,
                    renderingMode,
                    measuringMode,
                    0f,
                    0f,
                    out glyphRunAnalysis);
                if (hr < 0 || glyphRunAnalysis == 0)
                {
                    Log($"TryRasterize fail create-analysis cp=U+{codepoint:X4} hr=0x{hr:X8}");
                    return false;
                }

                var getBounds = GetMethod<GetAlphaTextureBoundsDelegate>(glyphRunAnalysis, 3);
                var createAlphaTexture = GetMethod<CreateAlphaTextureDelegate>(glyphRunAnalysis, 4);

                static byte ConvertClearTypeToAlpha(byte r, byte g, byte b)
                    => (byte)((r + g + b) / 3);

                bool TryRasterizeWithTextureType(int textureType, out byte[] alpha, out int width, out int height, out int offsetX, out int offsetY)
                {
                    alpha = Array.Empty<byte>();
                    width = 0;
                    height = 0;
                    offsetX = 0;
                    offsetY = 0;

                    var textureHr = getBounds(glyphRunAnalysis, textureType, out var bounds);
                    if (textureHr < 0)
                    {
                        Log($"TryRasterize texture-bounds fail cp=U+{codepoint:X4} texType={textureType} hr=0x{textureHr:X8}");
                        return false;
                    }

                    width = Math.Max(0, bounds.Right - bounds.Left);
                    height = Math.Max(0, bounds.Bottom - bounds.Top);
                    offsetX = bounds.Left;
                    offsetY = bounds.Top;
                    if (width == 0 || height == 0)
                    {
                        Log($"TryRasterize texture-bounds empty cp=U+{codepoint:X4} texType={textureType} bounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom})");
                        return false;
                    }

                    if (textureType == DWriteTextureAliased1x1)
                    {
                        alpha = new byte[width * height];
                        var alphaHandle = GCHandle.Alloc(alpha, GCHandleType.Pinned);
                        try
                        {
                            textureHr = createAlphaTexture(
                                glyphRunAnalysis,
                                textureType,
                                ref bounds,
                                alphaHandle.AddrOfPinnedObject(),
                                (uint)alpha.Length);
                        }
                        finally
                        {
                            alphaHandle.Free();
                        }

                        if (textureHr < 0)
                        {
                            Log($"TryRasterize texture-alpha fail cp=U+{codepoint:X4} texType={textureType} hr=0x{textureHr:X8} size={alpha.Length}");
                            return false;
                        }

                        var hasCoverage = false;
                        for (var i = 0; i < alpha.Length; i++)
                        {
                            hasCoverage |= alpha[i] != 0;
                        }

                        return hasCoverage;
                    }

                    if (textureType != DWriteTextureClearType3x1)
                    {
                        Log($"TryRasterize unsupported texture type cp=U+{codepoint:X4} texType={textureType}");
                        return false;
                    }

                    var clearTypeAlpha = new byte[width * height * 3];
                    var clearTypeHandle = GCHandle.Alloc(clearTypeAlpha, GCHandleType.Pinned);
                    try
                    {
                        textureHr = createAlphaTexture(
                            glyphRunAnalysis,
                            textureType,
                            ref bounds,
                            clearTypeHandle.AddrOfPinnedObject(),
                            (uint)clearTypeAlpha.Length);
                    }
                    finally
                    {
                        clearTypeHandle.Free();
                    }

                    if (textureHr < 0)
                    {
                        Log($"TryRasterize texture-alpha fail cp=U+{codepoint:X4} texType={textureType} hr=0x{textureHr:X8} size={clearTypeAlpha.Length}");
                        return false;
                    }

                    alpha = new byte[width * height];
                    var hasClearTypeCoverage = false;
                    for (var i = 0; i < alpha.Length; i++)
                    {
                        var sourceIndex = i * 3;
                        var value = ConvertClearTypeToAlpha(
                            clearTypeAlpha[sourceIndex],
                            clearTypeAlpha[sourceIndex + 1],
                            clearTypeAlpha[sourceIndex + 2]);
                        if (value < ClearTypeAlphaCutoff)
                        {
                            value = 0;
                        }

                        alpha[i] = value;
                        hasClearTypeCoverage |= value != 0;
                    }

                    return hasClearTypeCoverage;
                }

                if (!TryRasterizeWithTextureType(DWriteTextureAliased1x1, out var alpha, out var width, out var height, out var offsetX, out var offsetY)
                    && !TryRasterizeWithTextureType(DWriteTextureClearType3x1, out alpha, out width, out height, out offsetX, out offsetY))
                {
                    Log($"TryRasterize fail no-coverage cp=U+{codepoint:X4} (ClearType/Aliased failed)");
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
                    HasPlacementMetrics: true,
                    OffsetX: offsetX,
                    OffsetY: offsetY,
                    Advance: TryGetGlyphAdvance(fontFace, (ushort)glyphIndex, rasterizedEmSize, out var advance)
                        ? advance
                        : width
                );
                Log($"TryRasterize success cp=U+{codepoint:X4} glyphIndex={glyphIndex} width={width} height={height} texType={DWriteTextureClearType3x1}");
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

    private static bool TryResolveGlyphIndex(string fontPath, int codepoint, out int glyphIndex)
    {
        glyphIndex = 0;
        if ((uint)codepoint > 0xFFFFu)
        {
            return false;
        }

        if (!TryGetFontFace(fontPath, out var fontFace) || fontFace == 0)
        {
            return false;
        }

        if (!TryResolveGlyphIndexFromFace(fontFace, codepoint, out var glyphIndex16))
        {
            return false;
        }

        glyphIndex = glyphIndex16;
        return true;
    }

    private static bool TryResolveGlyphIndexFromFace(nint fontFace, int codepoint, out ushort glyphIndex)
    {
        glyphIndex = 0;
        if ((uint)codepoint > 0xFFFFu || fontFace == 0)
        {
            return false;
        }

        var codepoints = new[] { (uint)codepoint };
        var glyphIndices = new ushort[1];
        var codepointsHandle = GCHandle.Alloc(codepoints, GCHandleType.Pinned);
        var glyphIndicesHandle = GCHandle.Alloc(glyphIndices, GCHandleType.Pinned);
        try
        {
            var getGlyphIndices = GetMethod<GetGlyphIndicesDelegate>(fontFace, 11);
            var hr = getGlyphIndices(fontFace, codepointsHandle.AddrOfPinnedObject(), 1u, glyphIndicesHandle.AddrOfPinnedObject());
            if (hr < 0)
            {
                Log($"TryResolveGlyphIndex fail cp=U+{codepoint:X4} hr=0x{hr:X8}");
                return false;
            }

            if (glyphIndices[0] == 0)
            {
                Log($"TryResolveGlyphIndex fail cp=U+{codepoint:X4} glyphIndex=0");
                return false;
            }

            glyphIndex = glyphIndices[0];
            return true;
        }
        finally
        {
            glyphIndicesHandle.Free();
            codepointsHandle.Free();
        }
    }

    private static bool TryExtractAlphaTexture(nint glyphRunAnalysis, out byte[] alpha, out int width, out int height, out int offsetX, out int offsetY)
    {
        alpha = Array.Empty<byte>();
        width = 0;
        height = 0;
        offsetX = 0;
        offsetY = 0;

        var getBounds = GetMethod<GetAlphaTextureBoundsDelegate>(glyphRunAnalysis, 3);
        var createAlphaTexture = GetMethod<CreateAlphaTextureDelegate>(glyphRunAnalysis, 4);

        static byte ConvertClearTypeToAlpha(byte r, byte g, byte b)
            => (byte)((r + g + b) / 3);

        bool TryRasterizeWithTextureType(int textureType, out byte[] alphaPixels, out int texWidth, out int texHeight, out int texOffsetX, out int texOffsetY)
        {
            alphaPixels = Array.Empty<byte>();
            texWidth = 0;
            texHeight = 0;
            texOffsetX = 0;
            texOffsetY = 0;

            var textureHr = getBounds(glyphRunAnalysis, textureType, out var bounds);
            if (textureHr < 0)
            {
                Log($"TryExtractAlphaTexture getBounds fail texType={textureType} hr=0x{textureHr:X8}");
                return false;
            }

            texWidth = Math.Max(0, bounds.Right - bounds.Left);
            texHeight = Math.Max(0, bounds.Bottom - bounds.Top);
            texOffsetX = bounds.Left;
            texOffsetY = bounds.Top;
            if (texWidth == 0 || texHeight == 0)
            {
                Log($"TryExtractAlphaTexture empty texType={textureType} bounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom})");
                return false;
            }

            if (textureType == DWriteTextureAliased1x1)
            {
                alphaPixels = new byte[texWidth * texHeight];
                var alphaHandle = GCHandle.Alloc(alphaPixels, GCHandleType.Pinned);
                try
                {
                    textureHr = createAlphaTexture(
                        glyphRunAnalysis,
                        textureType,
                        ref bounds,
                        alphaHandle.AddrOfPinnedObject(),
                        (uint)alphaPixels.Length);
                }
                finally
                {
                    alphaHandle.Free();
                }

                if (textureHr < 0)
                {
                    Log($"TryExtractAlphaTexture create fail texType={textureType} hr=0x{textureHr:X8} buffer={alphaPixels.Length} bounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom})");
                    return false;
                }

                for (var i = 0; i < alphaPixels.Length; i++)
                {
                    if (alphaPixels[i] != 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            if (textureType != DWriteTextureClearType3x1)
            {
                return false;
            }

            var clearTypeAlpha = new byte[texWidth * texHeight * 3];
            var clearTypeHandle = GCHandle.Alloc(clearTypeAlpha, GCHandleType.Pinned);
            try
            {
                textureHr = createAlphaTexture(
                    glyphRunAnalysis,
                    textureType,
                    ref bounds,
                    clearTypeHandle.AddrOfPinnedObject(),
                    (uint)clearTypeAlpha.Length);
            }
            finally
            {
                clearTypeHandle.Free();
            }

            if (textureHr < 0)
            {
                Log($"TryExtractAlphaTexture create fail texType={textureType} hr=0x{textureHr:X8} buffer={clearTypeAlpha.Length} bounds=({bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom})");
                return false;
            }

            alphaPixels = new byte[texWidth * texHeight];
            var hasCoverage = false;
            for (var i = 0; i < alphaPixels.Length; i++)
            {
                var sourceIndex = i * 3;
                var value = ConvertClearTypeToAlpha(
                    clearTypeAlpha[sourceIndex],
                    clearTypeAlpha[sourceIndex + 1],
                    clearTypeAlpha[sourceIndex + 2]);
                if (value < ClearTypeAlphaCutoff)
                {
                    value = 0;
                }

                alphaPixels[i] = value;
                hasCoverage |= value != 0;
            }

            return hasCoverage;
        }

        if (!TryRasterizeWithTextureType(DWriteTextureAliased1x1, out alpha, out width, out height, out offsetX, out offsetY)
            && !TryRasterizeWithTextureType(DWriteTextureClearType3x1, out alpha, out width, out height, out offsetX, out offsetY))
        {
            return false;
        }

        return true;
    }

    private static bool TryCreateFactory(out nint factory)
    {
        factory = 0;
        var iid = IdWriteFactoryGuid;
        var hr = DWriteCreateFactory(DWriteFactoryTypeShared, ref iid, out factory);
        Log($"TryCreateFactory hr=0x{hr:X8} factory=0x{factory.ToInt64():X}");
        return hr >= 0 && factory != 0;
    }

    private static (int RenderingMode, int MeasuringMode) SelectRasterizationModes(float emSize)
    {
        _ = emSize;
        return (DWriteRenderingModeGdiNatural, DWriteMeasuringModeGdiNatural);
    }

    private static bool TryGetGlyphAdvance(nint fontFace, ushort glyphIndex, float emSize, out float advance)
    {
        advance = 0f;
        if (fontFace == 0 || glyphIndex == 0 || emSize <= 0f)
        {
            return false;
        }

        var getFontMetrics = GetMethod<GetFontMetricsDelegate>(fontFace, 8);
        getFontMetrics(fontFace, out var fontMetrics);
        if (fontMetrics.DesignUnitsPerEm == 0)
        {
            return false;
        }

        var glyphIndices = new[] { glyphIndex };
        var glyphMetrics = new DWriteGlyphMetrics[1];
        var glyphIndicesHandle = GCHandle.Alloc(glyphIndices, GCHandleType.Pinned);
        var glyphMetricsHandle = GCHandle.Alloc(glyphMetrics, GCHandleType.Pinned);
        try
        {
            var getDesignGlyphMetrics = GetMethod<GetDesignGlyphMetricsDelegate>(fontFace, 10);
            var hr = getDesignGlyphMetrics(
                fontFace,
                glyphIndicesHandle.AddrOfPinnedObject(),
                1u,
                glyphMetricsHandle.AddrOfPinnedObject(),
                false);
            if (hr < 0)
            {
                return false;
            }

            advance = (glyphMetrics[0].AdvanceWidth * emSize) / fontMetrics.DesignUnitsPerEm;
            return advance > 0f;
        }
        finally
        {
            glyphMetricsHandle.Free();
            glyphIndicesHandle.Free();
        }
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
    private delegate int GetGlyphIndicesDelegate(nint self, nint codePoints, uint codePointCount, nint glyphIndices);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GetFontMetricsDelegate(nint self, out DWriteFontMetrics fontMetrics);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesignGlyphMetricsDelegate(nint self, nint glyphIndices, uint glyphCount, nint glyphMetrics, [MarshalAs(UnmanagedType.Bool)] bool isSideways);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct DWriteFontMetrics
    {
        public ushort DesignUnitsPerEm;
        public ushort Ascent;
        public ushort Descent;
        public short LineGap;
        public ushort CapHeight;
        public ushort XHeight;
        public short UnderlinePosition;
        public ushort UnderlineThickness;
        public short StrikethroughPosition;
        public ushort StrikethroughThickness;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWriteGlyphMetrics
    {
        public int LeftSideBearing;
        public uint AdvanceWidth;
        public int RightSideBearing;
        public int TopSideBearing;
        public uint AdvanceHeight;
        public int BottomSideBearing;
        public int VerticalOriginY;
    }

    public readonly record struct TextRunRasterizationResult(
        int Width,
        int Height,
        byte[] Alpha,
        int OffsetX,
        int OffsetY,
        float Advance,
        float Baseline);
}
