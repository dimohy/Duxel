namespace Duxel.Core;

internal sealed partial class UiVisualTextLines
{
    private PlatformTextParagraphLayout? _nativeParagraph;
    internal WholeParagraphMaskKey? WholeParagraphMask { get; private set; }
    internal float WholeParagraphOffsetY { get; private set; }
    private float _nativeParagraphScale;
    private float[] _nativeParagraphLineOffsets = [];
    private int[] _nativeParagraphRunStarts = [], _nativeParagraphPaintRuns = [];
    private PlatformTextMeshResult?[] _nativeParagraphMeshes = [];
    private System.Collections.BitArray? _emptyNativeParagraphRasters;
    internal PlatformTextParagraphLayout? NativeParagraph => _nativeParagraph;
    internal float NativeParagraphScale => _nativeParagraphScale;
    internal float NativeParagraphLineOffset(int line) => _nativeParagraphLineOffsets[line];
    internal ReadOnlySpan<int> NativeParagraphPaintRuns(int line) =>
        _nativeParagraphPaintRuns.AsSpan(_nativeParagraphRunStarts[line], _nativeParagraphRunStarts[line + 1] - _nativeParagraphRunStarts[line]);
    internal PlatformTextMeshResult? NativeParagraphMesh(int run) => _nativeParagraphMeshes[run];
    internal void RememberNativeParagraphMesh(int run, PlatformTextMeshResult mesh) => _nativeParagraphMeshes[run] = mesh;
    internal bool HasEmptyNativeParagraphRaster(int run) => _emptyNativeParagraphRasters?[run] == true;
    internal void RememberEmptyNativeParagraphRaster(int run) =>
        (_emptyNativeParagraphRasters ??= new(_nativeParagraph!.Runs.Length))[run] = true;

    private void ClearNativeParagraph()
    {
        _nativeParagraph = null;
        WholeParagraphMask = null;
        _nativeParagraphLineOffsets = [];
        _nativeParagraphRunStarts = [];
        _nativeParagraphPaintRuns = [];
        _nativeParagraphMeshes = [];
        _emptyNativeParagraphRasters = null;
    }

    private bool TryBuildNativeStyledParagraph(UiImmediateContext ui, string text, float width, bool wrap)
    {
        if (_styledSource is not (UiInlineTextSource or UiInlineFlowTextSource or UiMarkdownTextSource or UiStyledTextSliceSource or UiCodeDocument))
            return false;
        var projection = _styledSource.Projection;
        // Tables retain their separate cell layout and clipping contract.
        if (_styledSource.Tables.Any(table => _styledSource.ActiveBlock is not { } active
            || active.Start >= table.Source.End || active.End <= table.Source.Start))
            return false;
        var map = projection.SourceToDisplay;
        var rightToLeft = _styledSource is UiInlineTextSource { RightToLeft: true } or UiInlineFlowTextSource { RightToLeft: true };
        var plainInline = _styledSource is UiInlineTextSource { BaseStyle.Flags: UiStyledTextFlags.None };
        if (!ui.TryCreateNativeStyledParagraph(projection, width, wrap, rightToLeft, plainInline,
            out var paragraph, out var heights, out var scale, out var wholeRequest, _styledSource as UiCodeDocument))
        {
            if (rightToLeft)
                throw new NotSupportedException("Explicit paragraph direction requires a native paragraph backend and direct text.");
            return false;
        }
        if (!projection.NativeAtomsCanWrap)
            foreach (var atom in projection.Atoms)
            {
                var start = map[atom.Start];
                var end = map[atom.End];
                foreach (var line in paragraph!.Lines.Span)
                    if (line.Start > start && line.Start < end)
                        return false;
            }
        _nativeParagraph = paragraph;
        _emptyNativeParagraphRasters = null;
        _nativeParagraphScale = scale;
        _lines.Clear();
        ClearStyledTables();
        Width = 0;
        Height = 0;
        EffectiveWidth = width;
        var nativeLines = paragraph!.Lines.Span;
        _nativeParagraphLineOffsets = new float[nativeLines.Length];
        var styles = projection.Runs;
        for (var line = 0; line < nativeLines.Length; line++)
        {
            var row = nativeLines[line];
            var height = _lineHeight;
            for (var style = 0; style < styles.Length; style++)
                if (styles[style].Start < row.Start + row.Length && styles[style].Start + styles[style].Length > row.Start)
                    height = MathF.Max(height, heights[style]);
            // Native font metrics locate the shared baseline; Fluent typography owns the logical line box.
            var firstObject = FindNativeObject(paragraph.Objects.Span, row.Start);
            if (firstObject < paragraph.Objects.Length && paragraph.Objects.Span[firstObject].Start < row.Start + row.Length)
                height = MathF.Max(height, row.Height * scale);
            _nativeParagraphLineOffsets[line] = Height - row.Top * scale + (height - row.Height * scale) / 2;
            if (firstObject < paragraph.Objects.Length && paragraph.Objects.Span[firstObject].Start < row.Start + row.Length)
            {
                // Keep the UI's centered atomic alignment even when a tiny object has a baseline outside its bounds.
                var anchor = paragraph.Objects.Span[firstObject].Bounds;
                _nativeParagraphLineOffsets[line] = Height + height / 2 - (anchor.Y + anchor.Height / 2) * scale;
            }
            var formattedWidth = (wrap ? row.WidthWithoutTrailingWhitespace : row.Advance) * scale;
            var start = AtomicBoundary(SourceBoundary(map, row.Start, afterHidden: false), forward: true);
            var displayEnd = row.Start + row.Length;
            var softWrap = line + 1 < nativeLines.Length && nativeLines[line + 1].Start == displayEnd;
            var end = line + 1 == nativeLines.Length ? text.Length : SourceBoundary(map, displayEnd, afterHidden: !softWrap);
            end = AtomicBoundary(end, forward: true);
            _lines.Add(new(start, end, text.Substring(start, end - start), formattedWidth, Height, height));
            Width = MathF.Max(Width, formattedWidth);
            Height += height;
        }
        WholeParagraphMask = null;
        if (wholeRequest is not null && nativeLines.Length != 0)
        {
            var offset = _nativeParagraphLineOffsets[0];
            var sharesLinePlacement = true;
            for (var line = 1; line < _nativeParagraphLineOffsets.Length; line++)
                sharesLinePlacement &= _nativeParagraphLineOffsets[line] == offset;
            if (sharesLinePlacement)
            {
                WholeParagraphMask = new(wholeRequest);
                WholeParagraphOffsetY = offset;
            }
        }
        var runs = paragraph.Runs.Span;
        var runLines = new int[runs.Length];
        var starts = new int[nativeLines.Length + 1];
        _nativeParagraphMeshes = new PlatformTextMeshResult?[runs.Length];
        for (var run = 0; run < runs.Length; run++)
        {
            if (runs[run].Glyphs.IsEmpty)
            {
                runLines[run] = -1;
                continue;
            }
            var position = runs[run].Glyphs.Span[0].SourcePosition;
            var low = 0;
            var high = nativeLines.Length;
            while (low + 1 < high)
            {
                var middle = low + (high - low) / 2;
                if (position >= nativeLines[middle].Start)
                    low = middle;
                else
                    high = middle;
            }
            runLines[run] = low;
            starts[low + 1]++;
        }
        for (var line = 1; line < starts.Length; line++)
            starts[line] += starts[line - 1];
        var paintRuns = new int[starts[^1]];
        var next = (int[])starts.Clone();
        // Keep the backend's paint order inside each line, including bidi runs.
        for (var run = 0; run < runLines.Length; run++)
            if (runLines[run] >= 0)
                paintRuns[next[runLines[run]]++] = run;
        _nativeParagraphRunStarts = starts;
        _nativeParagraphPaintRuns = paintRuns;
        return true;
    }

    private static int SourceBoundary(int[] map, int display, bool afterHidden)
    {
        var low = 0;
        var high = map.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (map[middle] < display || afterHidden && map[middle] == display)
                low = middle + 1;
            else
                high = middle;
        }
        return afterHidden ? low - 1 : low;
    }

    internal UiRect NativeObjectBounds(int source)
        => NativeDisplayObjectBounds(_styledSource!.Projection.SourceToDisplay[source], FindLine(source));

    internal UiRect NativeDisplayObjectBounds(int display, int line)
    {
        var objects = _nativeParagraph!.Objects.Span;
        var low = FindNativeObject(objects, display);
        if (low == objects.Length || objects[low].Start != display)
            throw new InvalidOperationException("The native paragraph omitted an owned inline child.");
        var bounds = objects[low].Bounds;
        return new(bounds.X * _nativeParagraphScale,
            bounds.Y * _nativeParagraphScale + _nativeParagraphLineOffsets[line],
            bounds.Width * _nativeParagraphScale, bounds.Height * _nativeParagraphScale);
    }

    private static int FindNativeObject(ReadOnlySpan<PlatformTextParagraphObjectPlacement> objects, int source)
    {
        var low = 0;
        var high = objects.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (objects[middle].Start < source)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }
}

public sealed partial class UiImmediateContext
{
    private UiCodeLineLayoutCache? _codeLineLayouts;

    internal bool TryCreateNativeStyledParagraph(UiStyledTextProjection projection, float width, bool wrap, bool rightToLeft,
        bool plainInline, out PlatformTextParagraphLayout? paragraph, out float[] heights, out float scale,
        out PlatformTextParagraphRequest? wholeRequest, UiCodeDocument? code = null)
    {
        paragraph = null;
        wholeRequest = null;
        heights = [];
        scale = 1f / _contentScale;
        if (_platformTextBackend is not IPlatformTextParagraphBackend backend
            || !ShouldUseDirectTextForText(projection.Text, _textSettings))
            return false;
        var styles = projection.Runs;
        var native = new PlatformTextParagraphStyle[Math.Max(1, styles.Length)];
        List<PlatformTextParagraphObject>? objects = null;
        Dictionary<PlatformTextParagraphStyle, PlatformTextParagraphLine>? metrics = null;
        heights = new float[native.Length];
        for (var index = 0; index < native.Length; index++)
        {
            var style = styles.Length == 0 ? default : styles[index];
            using var typography = BeginStyledTypography(style.Style);
            native[index] = new(style.Start, style.Length, _directTextPrimaryFontPath!, _directTextSecondaryFontPath,
                ResolveDirectTextFontSize(_textSettings))
            {
                PaintGroup = UiStyledTextPalette.ForegroundGroup(style.Style)
            };
            heights[index] = typography.LineHeight;
            var image = IsStyledImage(projection.Text.AsSpan(style.Start, style.Length), style.Style);
            var task = IsStyledTask(projection.Text.AsSpan(style.Start, style.Length), style.Style);
            if (style.Style.ObjectIndex > 0 || image || task)
            {
                objects ??= new(projection.Objects.Count + projection.Images.Count);
                metrics ??= new();
                var font = native[index] with
                {
                    Start = 0,
                    Length = 1
                };
                if (!metrics.TryGetValue(font, out var measurement))
                {
                    measurement = backend.CreateParagraph(new(" ", [font]), IsVectorTextLayer()).Lines.Span[0];
                    metrics.Add(font, measurement);
                }
                var side = 16 * _textSettings.Scale;
                var size = task ? new UiVector2(side, side)
                    : image ? StyledImageSize(projection.Images[style.Style.ImageIndex - 1], width)
                    : projection.Objects[style.Style.ObjectIndex - 1].Size;
                var objectHeight = size.Y * _contentScale;
                objects.Add(new(style.Start, size.X * _contentScale, objectHeight,
                    measurement.Baseline + (objectHeight - measurement.Height) / 2));
            }
        }
        var physicalWidth = wrap ? width * _contentScale : float.PositiveInfinity;
        var vector = IsVectorTextLayer();
        if (code is not null)
        {
            long started = 0;
            BeginValidationParagraphTiming(ref started);
            var cache = _codeLineLayouts ??= new();
            SelectValidationCodeLineLayoutCache(ref cache);
            paragraph = code.ParagraphLayouts.Prepare(backend, projection.Text, native, physicalWidth, vector, LayoutMeasurementKey, cache);
            EndValidationParagraphTiming(started, raster: false);
        }
        else
        {
            var request = new PlatformTextParagraphRequest(projection.Text, native, objects is null ? [] : objects.ToArray(),
                physicalWidth, rightToLeft ? PlatformTextParagraphDirection.RightToLeft : PlatformTextParagraphDirection.LeftToRight);
            paragraph = backend.CreateParagraph(request, vector);
            if (plainInline && !vector && scale == 1 && objects is null && backend is IPlatformTextWholeParagraphRasterizer
                && paragraph.Width <= DirectTextAtlasPageSize - 2 && paragraph.Height <= DirectTextAtlasPageSize - 2)
                wholeRequest = request;
        }
        if (paragraph.Carets.Length != projection.Text.Length + 1)
            throw new InvalidOperationException("A paragraph backend omitted original source caret positions.");
        if (paragraph.Objects.Length != (objects?.Count ?? 0))
            throw new InvalidOperationException("A paragraph backend omitted declared inline objects.");
        return true;
    }

    private bool TryPaintWholeInlineParagraph(UiVisualTextLines lines, UiVector2 origin, UiColor color, UiRect clip,
        ReadOnlySpan<float> lineOffsets)
    {
        if (lines.WholeParagraphMask is not { } mask || IsVectorTextLayer()
            || _platformTextBackend is not IPlatformTextWholeParagraphRasterizer rasterizer)
            return false;
        // A single mask applies opacity once. Translucent runs retain their individual blending.
        var foreground = StyledTextColor(default, color, new UiControlContext(this).FluentColorScheme == UiFluentColorScheme.Dark);
        if ((foreground.Rgba >> 24) != 255)
            return false;
        var horizontalOffset = lineOffsets.IsEmpty ? 0 : lineOffsets[0];
        foreach (var offset in lineOffsets)
            if (offset != horizontalOffset)
                return false;
        var x = origin.X + horizontalOffset;
        var y = origin.Y + lines.WholeParagraphOffsetY;
        // At a half-pixel tie, rounding a translated bitmap can depend on the parity of its ink origin.
        if (_textSettings.PixelSnap && (x - MathF.Floor(x) == .5f || y - MathF.Floor(y) == .5f))
            return false;
        if (!IsPresentationPass)
            return true;
        var key = new DirectTextCacheKey(string.Empty, null, string.Empty, 0, _directTextBackendGeneration)
        {
            WholeParagraph = mask
        };
        TrackLayerTextDependency(key);
        _capturingPaintGroup?.PendingTextKeys.Add(key);
        if (!_directTextCache.TryGetValue(key, out var entry))
        {
            var coverage = rasterizer.RasterizeWholeParagraphCoverage(mask.Request);
            if (UseR8ParagraphCoverage)
                entry = StoreParagraphRaster(key, coverage.Width, coverage.Height, coverage.Pixels,
                    coverage.Origin, default, null, UiTextureFormat.R8CoverageUnorm);
            else
                entry = StoreParagraphRaster(key, coverage.ToRgba(), default);
        }
        entry = UseParagraphRaster(key, entry);
        if (entry.TextureId is { } texture && entry.Width > 0 && entry.Height > 0)
        {
            var position = RasterTextOrigin(x + entry.OriginX, y - entry.Baseline, _textSettings.PixelSnap);
            _builder.AddTextImage(texture, position, new(position.X + entry.Width, position.Y + entry.Height),
                new(entry.U0, entry.V0), new(entry.U1, entry.V1), foreground, clip);
        }
        return true;
    }

    private DirectTextCacheEntry StoreParagraphRaster(in DirectTextCacheKey key, PlatformTextParagraphRaster pixels,
        UiVector2 referenceOrigin)
        => StoreParagraphRaster(key, pixels.Width, pixels.Height, pixels.RgbaPixels, pixels.Origin,
            referenceOrigin, pixels.PixelOwner, UiTextureFormat.Rgba8Unorm);

    private DirectTextCacheEntry StoreParagraphRaster(in DirectTextCacheKey key, int width, int height,
        ReadOnlyMemory<byte> pixels, UiVector2 origin, UiVector2 referenceOrigin, System.Buffers.IMemoryOwner<byte>? pixelOwner,
        UiTextureFormat format)
    {
        var transferred = false;
        try
        {
            ObserveValidationParagraphRaster(pixels.IsEmpty);
            var bytesPerPixel = format == UiTextureFormat.R8CoverageUnorm ? 1 : 4;
            if (width < 0 || height < 0 || pixels.Length != checked(width * height * bytesPerPixel)
                || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y))
                throw new InvalidOperationException("Paragraph paint returned invalid raster bounds or storage.");
            var entry = new DirectTextCacheEntry(null, width, height, 0, -origin.Y, 0, _state.FrameCount,
                pixels, PixelOwner: pixelOwner, OriginX: origin.X, ParagraphOrigin: referenceOrigin, TextureFormat: format);
            StoreDirectTextEntry(key, entry);
            transferred = true;
            return entry;
        }
        finally
        {
            if (!transferred)
                pixelOwner?.Dispose();
        }
    }

    private DirectTextCacheEntry UseParagraphRaster(in DirectTextCacheKey key, DirectTextCacheEntry entry)
    {
        entry = entry with { LastUsedFrame = _state.FrameCount, ParagraphLastUsedSeconds = _state.TimeSeconds };
        StoreDirectTextEntry(key, entry);
        _directTextPaintedKeys.Add(key);
        return EnsureDirectTextTexture(key, entry);
    }

    private void PaintNativeStyledParagraphLine(UiVisualTextLines visual, int line, UiVector2 origin, UiColor color, UiRect clip,
        UiStyledTextPaint paint = default)
    {
        if (!IsPresentationPass)
            return;
        if (_platformTextBackend is not IPlatformTextParagraphBackend backend)
            throw new InvalidOperationException("The prepared paragraph's paint backend is no longer available.");
        var paragraph = visual.NativeParagraph!;
        var scale = visual.NativeParagraphScale;
        var runs = paragraph.Runs.Span;
        var source = visual.StyledSource!;
        var styles = source.Projection.Runs;
        var hasDecorations = source is not UiCodeDocument;
        var dark = new UiControlContext(this).FluentColorScheme == UiFluentColorScheme.Dark;
        if (hasDecorations && paint.PaintContent)
            PaintStyledRule(visual, visual[line], origin, clip, dark);
        if (hasDecorations && paint.PaintBackground)
            PaintNativeParagraphDecorations(visual, line, origin, color, clip, dark, background: true, paint);
        if (!paint.PaintContent)
            return;
        var position = origin with { Y = origin.Y + visual.NativeParagraphLineOffset(line) };
        foreach (var index in visual.NativeParagraphPaintRuns(line))
        {
            var run = runs[index];
            var foreground = paint.ForegroundOverride ?? StyledTextColor(styles[run.StyleIndex].Style, color, dark);
            if (IsVectorTextLayer())
            {
                var mesh = visual.NativeParagraphMesh(index);
                if (mesh is null)
                {
                    mesh = backend.CreateParagraphRunMesh(run, .001f / WidgetRasterScale);
                    visual.RememberNativeParagraphMesh(index, mesh.Value);
                }
                RenderVectorText(_builder, mesh.Value, position, foreground, clip);
                continue;
            }
            // A native run with no ink has no texture or paint dependency. Retain that
            // fact with its layout so scrolling cannot evict it from the shared cache.
            if (visual.HasEmptyNativeParagraphRaster(index))
                continue;
            var key = new DirectTextCacheKey(run.FontPath, null, string.Empty, run.FontSize, _directTextBackendGeneration)
            {
                ParagraphRun = run
            };
            if (!_directTextCache.TryGetValue(key, out var entry))
            {
                long started = 0;
                BeginValidationParagraphTiming(ref started);
                var raster = backend.RasterizeParagraphRun(run);
                EndValidationParagraphTiming(started, raster: true);
                entry = StoreParagraphRaster(key, raster, run.Origin);
            }
            if (entry.Width == 0 || entry.Height == 0)
            {
                visual.RememberEmptyNativeParagraphRaster(index);
                continue;
            }
            TrackLayerTextDependency(key);
            _capturingPaintGroup?.PendingTextKeys.Add(key);
            entry = UseParagraphRaster(key, entry);
            if (entry.TextureId is { } texture && entry.Width > 0 && entry.Height > 0)
            {
                var placement = new UiVector2(run.Origin.X - entry.ParagraphOrigin.X, run.Origin.Y - entry.ParagraphOrigin.Y);
                var rasterOrigin = RasterTextOrigin(position.X + (entry.OriginX + placement.X) * scale,
                    position.Y + (placement.Y - entry.Baseline) * scale, _textSettings.PixelSnap);
                var left = rasterOrigin.X;
                var top = rasterOrigin.Y;
                _builder.AddTextImage(texture, new(left, top), new(left + entry.Width * scale, top + entry.Height * scale),
                    new(entry.U0, entry.V0), new(entry.U1, entry.V1), foreground, clip);
            }
        }
        if (!hasDecorations)
            return;
        PaintNativeParagraphDecorations(visual, line, origin, color, clip, dark, background: false, paint);
        var row = paragraph.Lines.Span[line];
        foreach (var style in styles)
        {
            if (style.Start < row.Start || style.Start >= row.Start + row.Length)
                continue;
            var text = source.Projection.Text.AsSpan(style.Start, style.Length);
            var task = IsStyledTask(text, style.Style);
            if (!task && !IsStyledImage(text, style.Style))
                continue;
            using var typography = BeginStyledTypography(style.Style);
            var bounds = visual.NativeDisplayObjectBounds(style.Start, line);
            bounds = bounds with
            {
                X = origin.X + bounds.X,
                Y = origin.Y + bounds.Y
            };
            if (task)
                PaintStyledTask(bounds, style.Style, clip);
            else
                PaintStyledImage(source.Projection.Images[style.Style.ImageIndex - 1], bounds, clip, dark);
        }
    }

    partial void ObserveValidationParagraphRaster(bool empty);
    partial void BeginValidationParagraphTiming(ref long timestamp);
    partial void SelectValidationCodeLineLayoutCache(ref UiCodeLineLayoutCache? cache);
    partial void EndValidationParagraphTiming(long timestamp, bool raster);
}
