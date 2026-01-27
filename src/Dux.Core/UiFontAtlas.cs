using System.Buffers;

namespace Dux.Core;

public readonly record struct UiGlyphInfo(
	float AdvanceX,
	float OffsetX,
	float OffsetY,
	float Width,
	float Height,
	UiRect UvRect
);

public readonly record struct UiFontRange(int Start, int End);

public readonly record struct UiFontSource(string FontPath, IReadOnlyList<int> Codepoints, int Priority = 0, float Scale = 1f);

public sealed class UiFontAtlas
{
	public int Width { get; }
	public int Height { get; }
	public UiTextureFormat Format { get; }
	public byte[] Pixels { get; }
	public IReadOnlyDictionary<int, UiGlyphInfo> Glyphs { get; }
	public IReadOnlyDictionary<uint, float> Kerning { get; }
	public float Ascent { get; }
	public float Descent { get; }
	public float LineGap { get; }
	public float LineHeight => Ascent - Descent + LineGap;
	public int FallbackCodepoint { get; }

	private int _lastGlyphCodepoint;
	private UiGlyphInfo _lastGlyph;
	private bool _lastGlyphValid;
	private uint _lastKerningKey;
	private float _lastKerningValue;
	private bool _lastKerningValid;

	public UiFontAtlas(int width, int height, UiTextureFormat format, byte[] pixels)
		: this(width, height, format, pixels, new Dictionary<int, UiGlyphInfo>(), new Dictionary<uint, float>(), 0, 0, 0, -1)
	{
	}

	public UiFontAtlas(
		int width,
		int height,
		UiTextureFormat format,
		byte[] pixels,
		IReadOnlyDictionary<int, UiGlyphInfo> glyphs,
		IReadOnlyDictionary<uint, float> kerning,
		float ascent,
		float descent,
		float lineGap,
		int fallbackCodepoint
	)
	{
		Width = width;
		Height = height;
		Format = format;
		Pixels = pixels;
		Glyphs = glyphs;
		Kerning = kerning;
		Ascent = ascent;
		Descent = descent;
		LineGap = lineGap;
		FallbackCodepoint = fallbackCodepoint;
	}

	public bool TryGetGlyph(char c, out UiGlyphInfo glyph) => TryGetGlyph((int)c, out glyph);

	public bool TryGetGlyph(int codepoint, out UiGlyphInfo glyph)
	{
		if (_lastGlyphValid && _lastGlyphCodepoint == codepoint)
		{
			glyph = _lastGlyph;
			return true;
		}

		if (Glyphs.TryGetValue(codepoint, out glyph))
		{
			_lastGlyphCodepoint = codepoint;
			_lastGlyph = glyph;
			_lastGlyphValid = true;
			return true;
		}

		return false;
	}

	public bool GetGlyphOrFallback(int codepoint, out UiGlyphInfo glyph)
	{
		if (_lastGlyphValid && _lastGlyphCodepoint == codepoint)
		{
			glyph = _lastGlyph;
			return true;
		}

		if (Glyphs.TryGetValue(codepoint, out glyph))
		{
			_lastGlyphCodepoint = codepoint;
			_lastGlyph = glyph;
			_lastGlyphValid = true;
			return true;
		}

		if (FallbackCodepoint >= 0 && Glyphs.TryGetValue(FallbackCodepoint, out glyph))
		{
			_lastGlyphCodepoint = codepoint;
			_lastGlyph = glyph;
			_lastGlyphValid = true;
			return true;
		}

		return false;
	}

	public float GetKerning(int left, int right)
	{
		var key = ((uint)left << 16) | (uint)right;
		if (_lastKerningValid && _lastKerningKey == key)
		{
			return _lastKerningValue;
		}

		if (Kerning.TryGetValue(key, out var value))
		{
			_lastKerningKey = key;
			_lastKerningValue = value;
			_lastKerningValid = true;
			return value;
		}

		_lastKerningKey = key;
		_lastKerningValue = 0f;
		_lastKerningValid = true;
		return 0f;
	}

	public UiTextureUpdate CreateTextureUpdate(UiTextureId textureId, UiTextureUpdateKind kind = UiTextureUpdateKind.Create) =>
		new(kind, textureId, Format, Width, Height, Pixels);

	public UiVector2 GetWhitePixelUv()
	{
		if (Width <= 0 || Height <= 0)
		{
			throw new InvalidOperationException("Font atlas size must be positive.");
		}

		return new UiVector2(0.5f / Width, 0.5f / Height);
	}
}

public static class UiFontAtlasBuilder
{
	private static readonly object CacheLock = new();
	private static readonly Dictionary<string, UiFontAtlas> AtlasCache = new();
	private static readonly Dictionary<string, CachedFont> FontCache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<GlyphCacheKey, GlyphBitmap> GlyphBitmapCache = new();
	private static readonly Queue<string> AtlasCacheOrder = new();
	private static readonly Queue<string> FontCacheOrder = new();
	private static readonly Queue<GlyphCacheKey> GlyphCacheOrder = new();
	private const int MaxAtlasCacheEntries = 8;
	private const int MaxFontCacheEntries = 32;
	private const int MaxGlyphCacheEntries = 4096;
	private const int DiskCacheVersion = 1;
	private static readonly byte[] DiskCacheMagic = System.Text.Encoding.ASCII.GetBytes("DUXFNT");
	private static readonly string DiskCacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dux", "FontAtlasCache");

	private readonly record struct CachedFont(TtfFont Font, DateTime LastWriteTimeUtc);
	private readonly record struct GlyphCacheKey(string FontPath, int Codepoint, int FontSize, int Oversample, float Scale);

	public static void ClearCache()
	{
		lock (CacheLock)
		{
			AtlasCache.Clear();
			FontCache.Clear();
			GlyphBitmapCache.Clear();
			AtlasCacheOrder.Clear();
			FontCacheOrder.Clear();
			GlyphCacheOrder.Clear();
		}
	}

	private static TtfFont GetCachedFont(string fontPath)
	{
		var lastWriteTime = File.GetLastWriteTimeUtc(fontPath);
		lock (CacheLock)
		{
			if (FontCache.TryGetValue(fontPath, out var cached) && cached.LastWriteTimeUtc == lastWriteTime)
			{
				return cached.Font;
			}
		}

		var data = File.ReadAllBytes(fontPath);
		var font = TtfFont.Parse(data);
		lock (CacheLock)
		{
			FontCache[fontPath] = new CachedFont(font, lastWriteTime);
			FontCacheOrder.Enqueue(fontPath);
			TrimCache(FontCache, FontCacheOrder, MaxFontCacheEntries);
		}

		return font;
	}

	private static bool TryGetCachedBitmap(GlyphCacheKey key, out GlyphBitmap bitmap)
	{
		lock (CacheLock)
		{
			return GlyphBitmapCache.TryGetValue(key, out bitmap);
		}
	}

	private static void StoreCachedBitmap(GlyphCacheKey key, GlyphBitmap bitmap)
	{
		lock (CacheLock)
		{
			if (!GlyphBitmapCache.ContainsKey(key))
			{
				GlyphCacheOrder.Enqueue(key);
			}
			GlyphBitmapCache[key] = bitmap;
			TrimCache(GlyphBitmapCache, GlyphCacheOrder, MaxGlyphCacheEntries);
		}
	}

	private static void StoreAtlasCache(string cacheKey, UiFontAtlas atlas)
	{
		lock (CacheLock)
		{
			if (!AtlasCache.ContainsKey(cacheKey))
			{
				AtlasCacheOrder.Enqueue(cacheKey);
			}
			AtlasCache[cacheKey] = atlas;
			TrimCache(AtlasCache, AtlasCacheOrder, MaxAtlasCacheEntries);
		}
	}

	private static void TrimCache<TKey, TValue>(Dictionary<TKey, TValue> cache, Queue<TKey> order, int maxEntries)
		where TKey : notnull
	{
		while (cache.Count > maxEntries && order.Count > 0)
		{
			var key = order.Dequeue();
			cache.Remove(key);
		}
	}

	private static string GetDiskCachePath(string cacheKey)
	{
		var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cacheKey)));
		return Path.Combine(DiskCacheRoot, $"{hash}.bin");
	}

	private static bool TryLoadAtlasFromDisk(string cacheKey, out UiFontAtlas atlas)
	{
		var path = GetDiskCachePath(cacheKey);
		if (!File.Exists(path))
		{
			atlas = null!;
			return false;
		}

		using var stream = File.OpenRead(path);
		using var reader = new BinaryReader(stream);
		var magic = reader.ReadBytes(DiskCacheMagic.Length);
		if (!magic.AsSpan().SequenceEqual(DiskCacheMagic))
		{
			throw new InvalidDataException("Invalid font atlas cache header.");
		}

		var version = reader.ReadInt32();
		if (version != DiskCacheVersion)
		{
			throw new InvalidDataException("Unsupported font atlas cache version.");
		}

		var width = reader.ReadInt32();
		var height = reader.ReadInt32();
		var format = (UiTextureFormat)reader.ReadInt32();
		var fallback = reader.ReadInt32();
		var ascent = reader.ReadSingle();
		var descent = reader.ReadSingle();
		var lineGap = reader.ReadSingle();
		var pixelLength = reader.ReadInt32();
		var pixels = reader.ReadBytes(pixelLength);

		var glyphCount = reader.ReadInt32();
		var glyphs = new Dictionary<int, UiGlyphInfo>(glyphCount);
		for (var i = 0; i < glyphCount; i++)
		{
			var codepoint = reader.ReadInt32();
			var advance = reader.ReadSingle();
			var offsetX = reader.ReadSingle();
			var offsetY = reader.ReadSingle();
			var widthF = reader.ReadSingle();
			var heightF = reader.ReadSingle();
			var uvX = reader.ReadSingle();
			var uvY = reader.ReadSingle();
			var uvW = reader.ReadSingle();
			var uvH = reader.ReadSingle();
			glyphs[codepoint] = new UiGlyphInfo(advance, offsetX, offsetY, widthF, heightF, new UiRect(uvX, uvY, uvW, uvH));
		}

		var kerningCount = reader.ReadInt32();
		var kerning = new Dictionary<uint, float>(kerningCount);
		for (var i = 0; i < kerningCount; i++)
		{
			var key = reader.ReadUInt32();
			var value = reader.ReadSingle();
			kerning[key] = value;
		}

		atlas = new UiFontAtlas(width, height, format, pixels, glyphs, kerning, ascent, descent, lineGap, fallback);
		return true;
	}

	private static void SaveAtlasToDisk(string cacheKey, UiFontAtlas atlas)
	{
		Directory.CreateDirectory(DiskCacheRoot);
		var path = GetDiskCachePath(cacheKey);
		using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
		using var writer = new BinaryWriter(stream);
		writer.Write(DiskCacheMagic);
		writer.Write(DiskCacheVersion);
		writer.Write(atlas.Width);
		writer.Write(atlas.Height);
		writer.Write((int)atlas.Format);
		writer.Write(atlas.FallbackCodepoint);
		writer.Write(atlas.Ascent);
		writer.Write(atlas.Descent);
		writer.Write(atlas.LineGap);
		writer.Write(atlas.Pixels.Length);
		writer.Write(atlas.Pixels);
		writer.Write(atlas.Glyphs.Count);
		foreach (var entry in atlas.Glyphs)
		{
			writer.Write(entry.Key);
			writer.Write(entry.Value.AdvanceX);
			writer.Write(entry.Value.OffsetX);
			writer.Write(entry.Value.OffsetY);
			writer.Write(entry.Value.Width);
			writer.Write(entry.Value.Height);
			writer.Write(entry.Value.UvRect.X);
			writer.Write(entry.Value.UvRect.Y);
			writer.Write(entry.Value.UvRect.Width);
			writer.Write(entry.Value.UvRect.Height);
		}
		writer.Write(atlas.Kerning.Count);
		foreach (var entry in atlas.Kerning)
		{
			writer.Write(entry.Key);
			writer.Write(entry.Value);
		}
	}

	public static bool InvalidateCacheForSources(IReadOnlyList<UiFontSource> sources, int fontSize, int atlasWidth, int atlasHeight, int padding, int oversample)
	{
		if (sources.Count == 0)
		{
			return false;
		}

		var orderedSources = new List<UiFontSource>(sources.Count);
		for (var i = 0; i < sources.Count; i++)
		{
			orderedSources.Add(sources[i]);
		}
		orderedSources.Sort(static (left, right) => left.Priority.CompareTo(right.Priority));
		var cacheKey = BuildCacheKey(orderedSources, fontSize, atlasWidth, atlasHeight, padding, oversample);
		lock (CacheLock)
		{
			return AtlasCache.Remove(cacheKey);
		}
	}

	public static UiFontAtlas CreateFromTtf(
		string fontPath,
		int fontSize = 18,
		int atlasWidth = 512,
		int atlasHeight = 512,
		int padding = 1,
		int oversample = 2
	)
	{
		var ranges = new[] { new UiFontRange(32, 126) };
		return CreateFromTtf(fontPath, ranges, fontSize, atlasWidth, atlasHeight, padding, oversample);
	}

	public static UiFontAtlas CreateFromTtf(
		string fontPath,
		IReadOnlyList<UiFontRange> ranges,
		int fontSize = 18,
		int atlasWidth = 512,
		int atlasHeight = 512,
		int padding = 1,
		int oversample = 2
	)
	{
		if (string.IsNullOrWhiteSpace(fontPath))
		{
			throw new InvalidOperationException("Font path must be provided.");
		}

		if (ranges.Count == 0)
		{
			throw new InvalidOperationException("At least one font range must be provided.");
		}

		var codepoints = new List<int>();
		foreach (var range in ranges)
		{
			var start = Math.Max(0, range.Start);
			var end = Math.Max(start, range.End);
			if (end > char.MaxValue)
			{
				throw new InvalidOperationException("Font ranges beyond U+FFFF are not supported yet.");
			}
			for (var c = start; c <= end; c++)
			{
				codepoints.Add(c);
			}
		}

		return CreateFromTtf(fontPath, codepoints, fontSize, atlasWidth, atlasHeight, padding, oversample);
	}

	public static UiFontAtlas CreateFromTtf(
		string fontPath,
		IReadOnlyList<int> codepoints,
		int fontSize = 18,
		int atlasWidth = 512,
		int atlasHeight = 512,
		int padding = 1,
		int oversample = 2
	)
	{
		return CreateFromTtfMerged(
			new[] { new UiFontSource(fontPath, codepoints) },
			fontSize,
			atlasWidth,
			atlasHeight,
			padding,
			oversample
		);
	}

	public static UiFontAtlas CreateFromTtfMerged(
		IReadOnlyList<UiFontSource> sources,
		int fontSize = 18,
		int atlasWidth = 512,
		int atlasHeight = 512,
		int padding = 1,
		int oversample = 2
	)
	{
		if (sources.Count == 0)
		{
			throw new InvalidOperationException("At least one font source must be provided.");
		}

		if (oversample < 1)
		{
			throw new InvalidOperationException("Oversample must be >= 1.");
		}

		var orderedSources = new List<UiFontSource>(sources.Count);
		for (var i = 0; i < sources.Count; i++)
		{
			orderedSources.Add(sources[i]);
		}
		orderedSources.Sort(static (left, right) => left.Priority.CompareTo(right.Priority));
		var cacheKey = BuildCacheKey(orderedSources, fontSize, atlasWidth, atlasHeight, padding, oversample);
		lock (CacheLock)
		{
			if (AtlasCache.TryGetValue(cacheKey, out var cached))
			{
				return cached;
			}
		}
		if (TryLoadAtlasFromDisk(cacheKey, out var diskCached))
		{
			StoreAtlasCache(cacheKey, diskCached);
			return diskCached;
		}
		var fonts = new List<TtfFont>(orderedSources.Count);
		var renderScales = new List<float>(orderedSources.Count);
		var pixelScales = new List<float>(orderedSources.Count);
		var ascent = 0f;
		var descent = 0f;
		var lineGap = 0f;

		for (var s = 0; s < orderedSources.Count; s++)
		{
			var source = orderedSources[s];
			if (string.IsNullOrWhiteSpace(source.FontPath))
			{
				throw new InvalidOperationException("Font path must be provided.");
			}

			var font = GetCachedFont(source.FontPath);
			var renderScale = font.GetScaleForPixelHeight((int)(fontSize * source.Scale * oversample));
			var pixelScale = font.GetScaleForPixelHeight((int)(fontSize * source.Scale));
			fonts.Add(font);
			renderScales.Add(renderScale);
			pixelScales.Add(pixelScale);

			if (s == 0)
			{
				ascent = font.Ascender * pixelScale;
				descent = font.Descent * pixelScale;
				lineGap = font.LineGap * pixelScale;
			}
		}

		var pixels = new byte[atlasWidth * atlasHeight * 4];
		EnsureAtlasHasWhitePixel(atlasWidth, atlasHeight, pixels);
		EnsureAtlasHasWhitePixel(atlasWidth, atlasHeight, pixels);
		var glyphs = new Dictionary<int, UiGlyphInfo>();
		var kerning = new Dictionary<uint, float>();
		var assignedFont = new Dictionary<int, int>();
		var perFontCodepoints = new List<List<int>>(fonts.Count);
		for (var i = 0; i < fonts.Count; i++)
		{
			perFontCodepoints.Add([]);
		}

		var x = padding;
		var y = padding;
		var rowHeight = 0;

		for (var sourceIndex = 0; sourceIndex < orderedSources.Count; sourceIndex++)
		{
			var source = orderedSources[sourceIndex];
			var font = fonts[sourceIndex];
			var renderScale = renderScales[sourceIndex];
			var pixelScale = pixelScales[sourceIndex];
			var codepoints = NormalizeCodepoints(source.Codepoints);

			foreach (var codepoint in codepoints)
			{
				if (assignedFont.ContainsKey(codepoint))
				{
					continue;
				}
				if (codepoint < 0 || codepoint > char.MaxValue)
				{
					throw new InvalidOperationException("Codepoints beyond U+FFFF are not supported yet.");
				}

				var glyphIndex = font.GetGlyphIndex((char)codepoint);
				var glyph = font.GetGlyph(glyphIndex);
				var advance = font.GetAdvanceWidth(glyphIndex) * pixelScale;
				var offsetX = glyph.XMin * pixelScale;
				var offsetY = -glyph.YMax * pixelScale;
				if (codepoint == ' ')
				{
					glyphs[codepoint] = new UiGlyphInfo(advance, 0, 0, 0, 0, new UiRect(0, 0, 0, 0));
					assignedFont[codepoint] = sourceIndex;
					perFontCodepoints[sourceIndex].Add(codepoint);
					continue;
				}
				if (glyph.Contours.Count is 0)
				{
					glyphs[codepoint] = new UiGlyphInfo(advance, offsetX, offsetY, 0, 0, new UiRect(0, 0, 0, 0));
					assignedFont[codepoint] = sourceIndex;
					perFontCodepoints[sourceIndex].Add(codepoint);
					continue;
				}

				var glyphCacheKey = new GlyphCacheKey(source.FontPath, codepoint, fontSize, oversample, source.Scale);
				if (!TryGetCachedBitmap(glyphCacheKey, out var bitmap))
				{
					bitmap = glyph.Rasterize(renderScale);
					if (oversample > 1)
					{
						bitmap = Downsample(bitmap, oversample);
					}
					StoreCachedBitmap(glyphCacheKey, bitmap);
				}
				var glyphWidth = bitmap.Width + padding;
				var glyphHeight = bitmap.Height + padding;

				if (x + glyphWidth > atlasWidth)
				{
					x = padding;
					y += rowHeight + padding;
					rowHeight = 0;
				}

				if (y + glyphHeight > atlasHeight)
				{
					throw new InvalidOperationException("Font atlas size is insufficient for glyph set.");
				}

				BlitAlpha(pixels, atlasWidth, atlasHeight, x, y, bitmap);

				var uv = new UiRect(
					x / (float)atlasWidth,
					y / (float)atlasHeight,
					bitmap.Width / (float)atlasWidth,
					bitmap.Height / (float)atlasHeight
				);
				glyphs[codepoint] = new UiGlyphInfo(advance, offsetX, offsetY, bitmap.Width, bitmap.Height, uv);
				assignedFont[codepoint] = sourceIndex;
				perFontCodepoints[sourceIndex].Add(codepoint);

				x += glyphWidth + padding;
				rowHeight = Math.Max(rowHeight, glyphHeight);
			}
		}

		if (fonts.Count > 0)
		{
			var font = fonts[0];
			var pixelScale = pixelScales[0];
			var list = perFontCodepoints[0];
			for (var i = 0; i < list.Count; i++)
			{
				var left = list[i];
				var leftGlyphIndex = font.GetGlyphIndex((char)left);
				for (var j = 0; j < list.Count; j++)
				{
					var right = list[j];
					var rightGlyphIndex = font.GetGlyphIndex((char)right);
					var kern = font.GetKerning(leftGlyphIndex, rightGlyphIndex);
					if (kern == 0)
					{
						continue;
					}
					var key = ((uint)left << 16) | (uint)right;
					kerning[key] = kern * pixelScale;
				}
			}
		}

		var fallback = ResolveFallbackCodepoint(glyphs);
		var atlas = new UiFontAtlas(atlasWidth, atlasHeight, UiTextureFormat.Rgba8Unorm, pixels, glyphs, kerning, ascent, descent, lineGap, fallback);
		StoreAtlasCache(cacheKey, atlas);
		SaveAtlasToDisk(cacheKey, atlas);
		return atlas;
	}

	private static string BuildCacheKey(
		IReadOnlyList<UiFontSource> sources,
		int fontSize,
		int atlasWidth,
		int atlasHeight,
		int padding,
		int oversample
	)
	{
		var builder = new System.Text.StringBuilder();
		builder.Append(fontSize).Append('|')
			.Append(atlasWidth).Append('|')
			.Append(atlasHeight).Append('|')
			.Append(padding).Append('|')
			.Append(oversample);
		for (var i = 0; i < sources.Count; i++)
		{
			var source = sources[i];
			builder.Append('|').Append(source.FontPath)
				.Append('|').Append(GetFileStamp(source.FontPath))
				.Append('|').Append(source.Priority)
				.Append('|').Append(source.Scale)
				.Append('|').Append(HashCodepoints(source.Codepoints));
		}
		return builder.ToString();
	}

	private static long GetFileStamp(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return 0;
		}

		try
		{
			var info = new System.IO.FileInfo(path);
			return info.Exists ? info.LastWriteTimeUtc.Ticks ^ info.Length : 0;
		}
		catch
		{
			return 0;
		}
	}

	private static int HashCodepoints(IReadOnlyList<int> codepoints)
	{
		var hash = new HashCode();
		if (codepoints.Count == 0)
		{
			return hash.ToHashCode();
		}

		var count = codepoints.Count;
		var pool = ArrayPool<int>.Shared;
		var buffer = pool.Rent(count);
		try
		{
			for (var i = 0; i < count; i++)
			{
				buffer[i] = codepoints[i];
			}

			Array.Sort(buffer, 0, count);
			var rangeStart = buffer[0];
			var rangeEnd = buffer[0];
			for (var i = 1; i < count; i++)
			{
				var value = buffer[i];
				if (value == rangeEnd || value == rangeEnd + 1)
				{
					rangeEnd = value;
					continue;
				}
				hash.Add(rangeStart);
				hash.Add(rangeEnd);
				rangeStart = value;
				rangeEnd = value;
			}
			hash.Add(rangeStart);
			hash.Add(rangeEnd);
			return hash.ToHashCode();
		}
		finally
		{
			pool.Return(buffer, clearArray: false);
		}
	}

	private static List<int> NormalizeCodepoints(IReadOnlyList<int> codepoints)
	{
		if (codepoints.Count == 0)
		{
			return [];
		}

		var count = codepoints.Count;
		var pool = ArrayPool<int>.Shared;
		var buffer = pool.Rent(count);
		try
		{
			for (var i = 0; i < count; i++)
			{
				buffer[i] = codepoints[i];
			}

			Array.Sort(buffer, 0, count);
			var uniqueCount = 0;
			var last = 0;
			for (var i = 0; i < count; i++)
			{
				var value = buffer[i];
				if (uniqueCount == 0 || value != last)
				{
					buffer[uniqueCount++] = value;
					last = value;
				}
			}

			var list = new List<int>(uniqueCount);
			for (var i = 0; i < uniqueCount; i++)
			{
				list.Add(buffer[i]);
			}

			return list;
		}
		finally
		{
			pool.Return(buffer, clearArray: false);
		}
	}

	private static GlyphBitmap Downsample(GlyphBitmap source, int oversample)
	{
		var targetWidth = Math.Max(1, (source.Width + oversample - 1) / oversample);
		var targetHeight = Math.Max(1, (source.Height + oversample - 1) / oversample);
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
					if (sy >= source.Height)
					{
						break;
					}
					for (var ox = 0; ox < oversample; ox++)
					{
						var sx = startX + ox;
						if (sx >= source.Width)
						{
							break;
						}
						sum += source.Alpha[(sy * source.Width) + sx];
						count++;
					}
				}

				output[(y * targetWidth) + x] = count == 0 ? (byte)0 : (byte)(sum / count);
			}
		}

		return new GlyphBitmap(targetWidth, targetHeight, output);
	}

	public static UiFontAtlas CreateBuiltInAscii5x7(
		int scale = 2,
		int padding = 1,
		int columns = 16
	)
	{
		const int glyphWidth = 5;
		const int glyphHeight = 7;
		const int firstChar = 32;
		const int lastChar = 126;
		var glyphCount = lastChar - firstChar + 1;

		var cellWidth = (glyphWidth * scale) + (padding * 2);
		var cellHeight = (glyphHeight * scale) + (padding * 2);
		var rows = (int)Math.Ceiling(glyphCount / (double)columns);
		var atlasWidth = cellWidth * columns;
		var atlasHeight = cellHeight * rows;

		var pixels = new byte[atlasWidth * atlasHeight * 4];
		var glyphs = new Dictionary<int, UiGlyphInfo>(glyphCount);
		var kerning = new Dictionary<uint, float>();
		var glyphPixelWidth = glyphWidth * scale;
		var glyphPixelHeight = glyphHeight * scale;

		for (var i = 0; i < glyphCount; i++)
		{
			var codepoint = firstChar + i;
			var col = i % columns;
			var row = i / columns;
			var baseX = (col * cellWidth) + padding;
			var baseY = (row * cellHeight) + padding;

			var pattern = GetGlyphPattern((char)codepoint);
			DrawPattern(pixels, atlasWidth, atlasHeight, baseX, baseY, scale, pattern);

			var uv = new UiRect(
				baseX / (float)atlasWidth,
				baseY / (float)atlasHeight,
				glyphPixelWidth / (float)atlasWidth,
				glyphPixelHeight / (float)atlasHeight
			);
			var offsetY = -glyphPixelHeight;
			var advance = glyphPixelWidth + padding;
			glyphs[codepoint] = new UiGlyphInfo(advance, 0, offsetY, glyphPixelWidth, glyphPixelHeight, uv);
		}

		var fallback = ResolveFallbackCodepoint(glyphs);
		return new UiFontAtlas(atlasWidth, atlasHeight, UiTextureFormat.Rgba8Unorm, pixels, glyphs, kerning, glyphPixelHeight, 0, padding, fallback);
	}

	private static void EnsureAtlasHasWhitePixel(int width, int height, byte[] pixels)
	{
		if (width <= 0 || height <= 0)
		{
			throw new InvalidOperationException("Font atlas size must be positive.");
		}

		if (pixels.Length < 4)
		{
			throw new InvalidOperationException("Font atlas buffer must contain at least one pixel.");
		}

		pixels[0] = 255;
		pixels[1] = 255;
		pixels[2] = 255;
		pixels[3] = 255;
	}

	private static int ResolveFallbackCodepoint(IReadOnlyDictionary<int, UiGlyphInfo> glyphs)
	{
		if (glyphs.ContainsKey('?'))
		{
			return '?';
		}
		if (glyphs.ContainsKey(' '))
		{
			return ' ';
		}
		foreach (var key in glyphs.Keys)
		{
			return key;
		}
		return -1;
	}

	private static void DrawPattern(byte[] pixels, int width, int height, int x, int y, int scale, byte[] pattern)
	{
		for (var row = 0; row < pattern.Length; row++)
		{
			var bits = pattern[row];
			for (var col = 0; col < 5; col++)
			{
				if (((bits >> (4 - col)) & 0x1) == 0)
				{
					continue;
				}

				var startX = x + (col * scale);
				var startY = y + (row * scale);

				for (var sy = 0; sy < scale; sy++)
				{
					for (var sx = 0; sx < scale; sx++)
					{
						var px = startX + sx;
						var py = startY + sy;
						if ((uint)px >= (uint)width || (uint)py >= (uint)height)
						{
							continue;
						}

						var index = (py * width + px) * 4;
						pixels[index + 0] = 255;
						pixels[index + 1] = 255;
						pixels[index + 2] = 255;
						pixels[index + 3] = 255;
					}
				}
			}
		}
	}

	private static byte[] GetGlyphPattern(char c)
	{
		if (c == ' ')
		{
			return EmptyGlyph;
		}

		if (Glyphs.TryGetValue(c, out var glyph))
		{
			return glyph;
		}

		if (Glyphs.TryGetValue(char.ToUpperInvariant(c), out glyph))
		{
			return glyph;
		}

		return UnknownGlyph;
	}

	private static readonly byte[] EmptyGlyph = [
		0b00000,
		0b00000,
		0b00000,
		0b00000,
		0b00000,
		0b00000,
		0b00000
	];

	private static readonly byte[] UnknownGlyph = [
		0b11111,
		0b10001,
		0b10101,
		0b10101,
		0b10101,
		0b10001,
		0b11111
	];

	private static readonly Dictionary<char, byte[]> Glyphs = new()
	{
		['0'] = [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110],
		['1'] = [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
		['2'] = [0b01110, 0b10001, 0b00001, 0b00110, 0b01000, 0b10000, 0b11111],
		['3'] = [0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110],
		['4'] = [0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010],
		['5'] = [0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110],
		['6'] = [0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110],
		['7'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000],
		['8'] = [0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110],
		['9'] = [0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100],
		['A'] = [0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
		['B'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
		['C'] = [0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110],
		['D'] = [0b11100, 0b10010, 0b10001, 0b10001, 0b10001, 0b10010, 0b11100],
		['E'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111],
		['F'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000],
		['G'] = [0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110],
		['H'] = [0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
		['I'] = [0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
		['J'] = [0b00111, 0b00010, 0b00010, 0b00010, 0b10010, 0b10010, 0b01100],
		['K'] = [0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001],
		['L'] = [0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111],
		['M'] = [0b10001, 0b11011, 0b10101, 0b10001, 0b10001, 0b10001, 0b10001],
		['N'] = [0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001],
		['O'] = [0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
		['P'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000],
		['Q'] = [0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101],
		['R'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001],
		['S'] = [0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110],
		['T'] = [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
		['U'] = [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
		['V'] = [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100],
		['W'] = [0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010],
		['X'] = [0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001],
		['Y'] = [0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100],
		['Z'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111],
		['a'] = [0b00000, 0b00000, 0b01110, 0b00001, 0b01111, 0b10001, 0b01111],
		['b'] = [0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b10001, 0b11110],
		['c'] = [0b00000, 0b00000, 0b01110, 0b10001, 0b10000, 0b10001, 0b01110],
		['d'] = [0b00001, 0b00001, 0b01111, 0b10001, 0b10001, 0b10001, 0b01111],
		['e'] = [0b00000, 0b00000, 0b01110, 0b10001, 0b11111, 0b10000, 0b01110],
		['f'] = [0b00110, 0b01001, 0b01000, 0b11100, 0b01000, 0b01000, 0b01000],
		['g'] = [0b00000, 0b00000, 0b01111, 0b10001, 0b10001, 0b01111, 0b00001],
		['h'] = [0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b10001, 0b10001],
		['i'] = [0b00100, 0b00000, 0b01100, 0b00100, 0b00100, 0b00100, 0b01110],
		['j'] = [0b00010, 0b00000, 0b00110, 0b00010, 0b00010, 0b10010, 0b01100],
		['k'] = [0b10000, 0b10000, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010],
		['l'] = [0b11000, 0b01000, 0b01000, 0b01000, 0b01000, 0b01000, 0b11100],
		['m'] = [0b00000, 0b00000, 0b11010, 0b10101, 0b10101, 0b10101, 0b10101],
		['n'] = [0b00000, 0b00000, 0b11110, 0b10001, 0b10001, 0b10001, 0b10001],
		['o'] = [0b00000, 0b00000, 0b01110, 0b10001, 0b10001, 0b10001, 0b01110],
		['p'] = [0b00000, 0b00000, 0b11110, 0b10001, 0b10001, 0b11110, 0b10000],
		['q'] = [0b00000, 0b00000, 0b01111, 0b10001, 0b10001, 0b01111, 0b00001],
		['r'] = [0b00000, 0b00000, 0b10110, 0b11001, 0b10000, 0b10000, 0b10000],
		['s'] = [0b00000, 0b00000, 0b01111, 0b10000, 0b01110, 0b00001, 0b11110],
		['t'] = [0b01000, 0b01000, 0b11100, 0b01000, 0b01000, 0b01001, 0b00110],
		['u'] = [0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
		['v'] = [0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100],
		['w'] = [0b00000, 0b00000, 0b10001, 0b10001, 0b10101, 0b10101, 0b01010],
		['x'] = [0b00000, 0b00000, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001],
		['y'] = [0b00000, 0b00000, 0b10001, 0b10001, 0b10001, 0b01111, 0b00001],
		['z'] = [0b00000, 0b00000, 0b11111, 0b00010, 0b00100, 0b01000, 0b11111],
	};

	private static void BlitAlpha(byte[] pixels, int width, int height, int x, int y, GlyphBitmap bitmap)
	{
		for (var row = 0; row < bitmap.Height; row++)
		{
			for (var col = 0; col < bitmap.Width; col++)
			{
				var alpha = bitmap.Alpha[(row * bitmap.Width) + col];
				if (alpha == 0)
				{
					continue;
				}

				var px = x + col;
				var py = y + row;
				if ((uint)px >= (uint)width || (uint)py >= (uint)height)
				{
					continue;
				}

				var index = (py * width + px) * 4;
				pixels[index + 0] = 255;
				pixels[index + 1] = 255;
				pixels[index + 2] = 255;
				pixels[index + 3] = alpha;
			}
		}
	}
}

internal sealed class TtfFont
{
	private readonly byte[] _data;
	private readonly Dictionary<uint, TtfTable> _tables;
	private readonly ushort _unitsPerEm;
	private readonly short _ascender;
	private readonly short _descender;
	private readonly short _lineGap;
	private readonly short _indexToLocFormat;
	private readonly int _numGlyphs;
	private readonly int _numHMetrics;
	private readonly uint[] _glyphOffsets;
	private readonly ushort[] _advanceWidths;
	private readonly short[] _leftSideBearings;
	private readonly CmapFormat4 _cmap;
	private readonly Dictionary<uint, short> _kernPairs;

	private TtfFont(
		byte[] data,
		Dictionary<uint, TtfTable> tables,
		ushort unitsPerEm,
		short ascender,
		short descender,
		short lineGap,
		short indexToLocFormat,
		int numGlyphs,
		int numHMetrics,
		uint[] glyphOffsets,
		ushort[] advanceWidths,
		short[] leftSideBearings,
		CmapFormat4 cmap,
		Dictionary<uint, short> kernPairs
	)
	{
		_data = data;
		_tables = tables;
		_unitsPerEm = unitsPerEm;
		_ascender = ascender;
		_descender = descender;
		_lineGap = lineGap;
		_indexToLocFormat = indexToLocFormat;
		_numGlyphs = numGlyphs;
		_numHMetrics = numHMetrics;
		_glyphOffsets = glyphOffsets;
		_advanceWidths = advanceWidths;
		_leftSideBearings = leftSideBearings;
		_cmap = cmap;
		_kernPairs = kernPairs;
	}

	public short Ascender => _ascender;
	public short Descent => _descender;
	public short LineGap => _lineGap;

	public static TtfFont Parse(byte[] data)
	{
		var reader = new TtfReader(data);
		reader.Skip(4);
		var numTables = reader.ReadU16();
		reader.Skip(6);

		var tables = new Dictionary<uint, TtfTable>();
		for (var i = 0; i < numTables; i++)
		{
			var tag = reader.ReadU32();
			reader.Skip(4);
			var offset = reader.ReadU32();
			var length = reader.ReadU32();
			tables[tag] = new TtfTable(offset, length);
		}

		var head = reader.Slice(TableTag("head"));
		head.Seek(18);
		var unitsPerEm = head.ReadU16();
		head.Seek(50);
		var indexToLocFormat = head.ReadS16();

		var hhea = reader.Slice(TableTag("hhea"));
		hhea.Seek(4);
		var ascender = hhea.ReadS16();
		var descender = hhea.ReadS16();
		var lineGap = hhea.ReadS16();
		hhea.Seek(34);
		var numHMetrics = hhea.ReadU16();

		var maxp = reader.Slice(TableTag("maxp"));
		maxp.Seek(4);
		var numGlyphs = maxp.ReadU16();

		var hmtx = reader.Slice(TableTag("hmtx"));
		var advanceWidths = new ushort[numGlyphs];
		var leftSideBearings = new short[numGlyphs];

		ushort lastAdvance = 0;
		for (var i = 0; i < numGlyphs; i++)
		{
			if (i < numHMetrics)
			{
				lastAdvance = hmtx.ReadU16();
				leftSideBearings[i] = hmtx.ReadS16();
				advanceWidths[i] = lastAdvance;
			}
			else
			{
				leftSideBearings[i] = hmtx.ReadS16();
				advanceWidths[i] = lastAdvance;
			}
		}

		var loca = reader.Slice(TableTag("loca"));
		var glyphOffsets = new uint[numGlyphs + 1];
		if (indexToLocFormat == 0)
		{
			for (var i = 0; i < glyphOffsets.Length; i++)
			{
				glyphOffsets[i] = (uint)(loca.ReadU16() * 2);
			}
		}
		else
		{
			for (var i = 0; i < glyphOffsets.Length; i++)
			{
				glyphOffsets[i] = loca.ReadU32();
			}
		}

		var cmap = CmapFormat4.Parse(reader.Slice(TableTag("cmap")));
		var kernPairs = ParseKernPairs(reader, tables);

		return new TtfFont(
			data,
			tables,
			unitsPerEm,
			ascender,
			descender,
			lineGap,
			indexToLocFormat,
			numGlyphs,
			numHMetrics,
			glyphOffsets,
			advanceWidths,
			leftSideBearings,
			cmap,
			kernPairs
		);
	}

	private static Dictionary<uint, short> ParseKernPairs(TtfReader reader, Dictionary<uint, TtfTable> tables)
	{
		if (!tables.TryGetValue(TableTag("kern"), out var kernTable))
		{
			return new Dictionary<uint, short>();
		}

		var kern = reader.SliceAt((int)kernTable.Offset);
		var version = kern.ReadU16();
		var nTables = kern.ReadU16();
		if (version != 0 || nTables == 0)
		{
			return new Dictionary<uint, short>();
		}

		var pairs = new Dictionary<uint, short>();
		for (var t = 0; t < nTables; t++)
		{
			var subtableStart = kern.Position;
			kern.ReadU16();
			var length = kern.ReadU16();
			var coverage = kern.ReadU16();
			var format = (coverage >> 8) & 0xFF;
			if (format == 0)
			{
				var nPairs = kern.ReadU16();
				kern.ReadU16();
				kern.ReadU16();
				kern.ReadU16();

				for (var i = 0; i < nPairs; i++)
				{
					var left = kern.ReadU16();
					var right = kern.ReadU16();
					var value = kern.ReadS16();
					var key = ((uint)left << 16) | right;
					pairs[key] = value;
				}
			}

			kern.Seek(subtableStart + length);
		}

		return pairs;
	}

	public int GetGlyphIndex(char c) => _cmap.Map(c);

	public int GetAdvanceWidth(int glyphIndex) => _advanceWidths[glyphIndex];

	public int GetLeftSideBearing(int glyphIndex) => _leftSideBearings[glyphIndex];

	public int GetKerning(int leftGlyphIndex, int rightGlyphIndex)
	{
		var key = ((uint)leftGlyphIndex << 16) | (uint)rightGlyphIndex;
		return _kernPairs.TryGetValue(key, out var value) ? value : 0;
	}

	public float GetScaleForPixelHeight(int pixelHeight) => pixelHeight / (float)_unitsPerEm;

	public TtfGlyph GetGlyph(int glyphIndex) => GetGlyphInternal(glyphIndex, 0);

	private TtfGlyph GetGlyphInternal(int glyphIndex, int depth)
	{
		if ((uint)glyphIndex >= (uint)_numGlyphs)
		{
			return TtfGlyph.Empty;
		}

		if (depth > 8)
		{
			throw new InvalidOperationException("Glyph recursion too deep.");
		}

		var glyf = new TtfReader(_data, (int)_tables[TableTag("glyf")].Offset);
		var glyphOffset = _glyphOffsets[glyphIndex];
		var glyphLength = _glyphOffsets[glyphIndex + 1] - glyphOffset;
		if (glyphLength == 0)
		{
			return TtfGlyph.Empty;
		}

		glyf.Skip((int)glyphOffset);
		var numberOfContours = glyf.ReadS16();
		var xMin = glyf.ReadS16();
		var yMin = glyf.ReadS16();
		var xMax = glyf.ReadS16();
		var yMax = glyf.ReadS16();

		if (numberOfContours < 0)
		{
			return ParseCompoundGlyph(glyf, depth, xMin, yMin, xMax, yMax);
		}

		return ParseSimpleGlyph(glyf, numberOfContours, xMin, yMin, xMax, yMax);
	}

	private TtfGlyph ParseSimpleGlyph(TtfReader glyf, short numberOfContours, short xMin, short yMin, short xMax, short yMax)
	{
		var endPts = new ushort[numberOfContours];
		for (var i = 0; i < numberOfContours; i++)
		{
			endPts[i] = glyf.ReadU16();
		}

		var instructionLength = glyf.ReadU16();
		glyf.Skip(instructionLength);

		var pointCount = endPts[^1] + 1;
		var flags = new byte[pointCount];
		for (var i = 0; i < pointCount; i++)
		{
			var flag = glyf.ReadU8();
			flags[i] = flag;
			if ((flag & 0x08) != 0)
			{
				var repeat = glyf.ReadU8();
				for (var r = 0; r < repeat; r++)
				{
					flags[++i] = flag;
				}
			}
		}

		var points = new TtfPoint[pointCount];
		var x = 0;
		for (var i = 0; i < pointCount; i++)
		{
			var flag = flags[i];
			if ((flag & 0x02) != 0)
			{
				var dx = glyf.ReadU8();
				x += (flag & 0x10) != 0 ? dx : -dx;
			}
			else if ((flag & 0x10) == 0)
			{
				x += glyf.ReadS16();
			}

			points[i].X = x;
		}

		var y = 0;
		for (var i = 0; i < pointCount; i++)
		{
			var flag = flags[i];
			if ((flag & 0x04) != 0)
			{
				var dy = glyf.ReadU8();
				y += (flag & 0x20) != 0 ? dy : -dy;
			}
			else if ((flag & 0x20) == 0)
			{
				y += glyf.ReadS16();
			}

			points[i].Y = y;
			points[i].OnCurve = (flag & 0x01) != 0;
		}

		var contours = new List<List<TtfPoint>>();
		var contourStart = 0;
		for (var c = 0; c < numberOfContours; c++)
		{
			var end = endPts[c];
			var contour = new List<TtfPoint>();
			for (var i = contourStart; i <= end; i++)
			{
				contour.Add(points[i]);
			}
			contourStart = end + 1;
			contours.Add(contour);
		}

		return new TtfGlyph(xMin, yMin, xMax, yMax, contours);
	}

	private TtfGlyph ParseCompoundGlyph(TtfReader glyf, int depth, short xMin, short yMin, short xMax, short yMax)
	{
		const ushort MoreComponents = 0x0020;
		const ushort ArgsAreWords = 0x0001;
		const ushort ArgsAreXY = 0x0002;
		const ushort WeHaveScale = 0x0008;
		const ushort WeHaveXYScale = 0x0040;
		const ushort WeHave2x2 = 0x0080;
		const ushort WeHaveInstructions = 0x0100;

		var contours = new List<List<TtfPoint>>();
		var compoundPoints = new List<TtfPoint>();
		var minX = int.MaxValue;
		var minY = int.MaxValue;
		var maxX = int.MinValue;
		var maxY = int.MinValue;
		ushort flags;

		static TtfPoint GetPointAt(IReadOnlyList<List<TtfPoint>> sourceContours, int index)
		{
			var cursor = 0;
			for (var i = 0; i < sourceContours.Count; i++)
			{
				var contour = sourceContours[i];
				if (index < cursor + contour.Count)
				{
					return contour[index - cursor];
				}
				cursor += contour.Count;
			}
			throw new InvalidOperationException("Compound glyph point index out of range.");
		}

		do
		{
			flags = glyf.ReadU16();
			var componentGlyphIndex = glyf.ReadU16();
			int arg1;
			int arg2;
			if ((flags & ArgsAreWords) != 0)
			{
				if ((flags & ArgsAreXY) != 0)
				{
					arg1 = glyf.ReadS16();
					arg2 = glyf.ReadS16();
				}
				else
				{
					arg1 = glyf.ReadU16();
					arg2 = glyf.ReadU16();
				}
			}
			else
			{
				if ((flags & ArgsAreXY) != 0)
				{
					arg1 = glyf.ReadS8();
					arg2 = glyf.ReadS8();
				}
				else
				{
					arg1 = glyf.ReadU8();
					arg2 = glyf.ReadU8();
				}
			}

			var a = 1f;
			var b = 0f;
			var c = 0f;
			var d = 1f;
			if ((flags & WeHaveScale) != 0)
			{
				var scale = ReadF2Dot14(glyf);
				a = scale;
				d = scale;
			}
			else if ((flags & WeHaveXYScale) != 0)
			{
				a = ReadF2Dot14(glyf);
				d = ReadF2Dot14(glyf);
			}
			else if ((flags & WeHave2x2) != 0)
			{
				a = ReadF2Dot14(glyf);
				b = ReadF2Dot14(glyf);
				c = ReadF2Dot14(glyf);
				d = ReadF2Dot14(glyf);
			}

			var component = GetGlyphInternal(componentGlyphIndex, depth + 1);

			var dx = 0f;
			var dy = 0f;
			if ((flags & ArgsAreXY) != 0)
			{
				dx = arg1;
				dy = arg2;
			}
			else
			{
				if (compoundPoints.Count is 0)
				{
					throw new InvalidOperationException("Compound glyph point matching requires existing parent points.");
				}
				if (component.Contours.Count is 0)
				{
					throw new InvalidOperationException("Compound glyph component has no points for matching.");
				}

				if ((uint)arg1 >= (uint)compoundPoints.Count)
				{
					throw new InvalidOperationException("Compound glyph parent point index out of range.");
				}

				var parentPoint = compoundPoints[arg1];
				var componentPoint = GetPointAt(component.Contours, arg2);
				var compX = (componentPoint.X * a) + (componentPoint.Y * c);
				var compY = (componentPoint.X * b) + (componentPoint.Y * d);
				dx = parentPoint.X - compX;
				dy = parentPoint.Y - compY;
			}

			foreach (var contour in component.Contours)
			{
				var transformed = new List<TtfPoint>(contour.Count);
				foreach (var point in contour)
				{
					var x = (point.X * a) + (point.Y * c) + dx;
					var y = (point.X * b) + (point.Y * d) + dy;
					var t = new TtfPoint { X = (int)MathF.Round(x), Y = (int)MathF.Round(y), OnCurve = point.OnCurve };
					transformed.Add(t);
					compoundPoints.Add(t);
					minX = Math.Min(minX, t.X);
					minY = Math.Min(minY, t.Y);
					maxX = Math.Max(maxX, t.X);
					maxY = Math.Max(maxY, t.Y);
				}
				contours.Add(transformed);
			}
		}
		while ((flags & MoreComponents) != 0);

		if ((flags & WeHaveInstructions) != 0)
		{
			var instructionsLength = glyf.ReadU16();
			glyf.Skip(instructionsLength);
		}

		if (contours.Count is 0)
		{
			return TtfGlyph.Empty;
		}

		var finalXMin = (short)(minX == int.MaxValue ? xMin : minX);
		var finalYMin = (short)(minY == int.MaxValue ? yMin : minY);
		var finalXMax = (short)(maxX == int.MinValue ? xMax : maxX);
		var finalYMax = (short)(maxY == int.MinValue ? yMax : maxY);
		return new TtfGlyph(finalXMin, finalYMin, finalXMax, finalYMax, contours);
	}

	private static float ReadF2Dot14(TtfReader reader)
	{
		var raw = reader.ReadS16();
		return raw / 16384f;
	}

	private static uint TableTag(string tag)
	{
		return ((uint)tag[0] << 24) | ((uint)tag[1] << 16) | ((uint)tag[2] << 8) | tag[3];
	}
}

internal readonly record struct TtfTable(uint Offset, uint Length);

internal sealed class TtfReader
{
	private readonly byte[] _data;
	private readonly int _baseOffset;
	private int _offset;

	public TtfReader(byte[] data, int baseOffset = 0, int offset = 0)
	{
		_data = data;
		_baseOffset = baseOffset;
		_offset = offset;
	}

	public byte[] RawData => _data;
	public int BaseOffset => _baseOffset;
	public int Position => _offset;

	public void Skip(int count) => _offset += count;

	public void Seek(int offset) => _offset = offset;

	public ushort ReadU16()
	{
		var value = (ushort)((_data[_baseOffset + _offset] << 8) | _data[_baseOffset + _offset + 1]);
		_offset += 2;
		return value;
	}

	public short ReadS16() => unchecked((short)ReadU16());

	public uint ReadU32()
	{
		var a = _data[_baseOffset + _offset];
		var b = _data[_baseOffset + _offset + 1];
		var c = _data[_baseOffset + _offset + 2];
		var d = _data[_baseOffset + _offset + 3];
		_offset += 4;
		return ((uint)a << 24) | ((uint)b << 16) | ((uint)c << 8) | d;
	}

	public byte ReadU8() => _data[_baseOffset + _offset++];
	public sbyte ReadS8() => unchecked((sbyte)ReadU8());

	public TtfReader Slice(uint tag)
	{
		var offset = 12;
		var numTables = (ushort)((_data[_baseOffset + 4] << 8) | _data[_baseOffset + 5]);
		for (var i = 0; i < numTables; i++)
		{
			var recordOffset = _baseOffset + offset + (i * 16);
			var recordTag = (uint)(_data[recordOffset] << 24 | _data[recordOffset + 1] << 16 | _data[recordOffset + 2] << 8 | _data[recordOffset + 3]);
			if (recordTag == tag)
			{
				var tableOffset = (uint)(_data[recordOffset + 8] << 24 | _data[recordOffset + 9] << 16 | _data[recordOffset + 10] << 8 | _data[recordOffset + 11]);
				return new TtfReader(_data, (int)tableOffset);
			}
		}

		throw new InvalidOperationException($"Missing TTF table {tag}.");
	}

	public TtfReader SliceAt(int offset) => new(_data, _baseOffset + offset);
}

internal struct TtfPoint
{
	public int X;
	public int Y;
	public bool OnCurve;
}

internal sealed class TtfGlyph
{
	public static readonly TtfGlyph Empty = new(0, 0, 0, 0, []);

	public int XMin { get; }
	public int YMin { get; }
	public int XMax { get; }
	public int YMax { get; }
	public IReadOnlyList<List<TtfPoint>> Contours { get; }

	public TtfGlyph(int xMin, int yMin, int xMax, int yMax, IReadOnlyList<List<TtfPoint>> contours)
	{
		XMin = xMin;
		YMin = yMin;
		XMax = xMax;
		YMax = yMax;
		Contours = contours;
	}

	public GlyphBitmap Rasterize(float scale)
	{
		var width = Math.Max(1, (int)MathF.Ceiling((XMax - XMin) * scale));
		var height = Math.Max(1, (int)MathF.Ceiling((YMax - YMin) * scale));
		var alpha = new byte[width * height];

		var edges = BuildEdges(scale);
		RasterizeEdges(alpha, width, height, edges);
		return new GlyphBitmap(width, height, alpha);
	}

	private List<Edge> BuildEdges(float scale)
	{
		var edges = new List<Edge>();
		foreach (var contour in Contours)
		{
			if (contour.Count == 0)
			{
				continue;
			}

			var count = contour.Count;
			var first = contour[0];
			var last = contour[^1];
			var prev = first.OnCurve
				? first
				: (last.OnCurve ? last : MidPoint(last, first));
			var startPoint = prev;

			var i = 0;
			while (i < count)
			{
				var curr = contour[i];
				var next = contour[(i + 1) % count];
				if (curr.OnCurve)
				{
					edges.Add(new Edge(prev, curr, scale, XMin, YMax));
					prev = curr;
					i++;
					continue;
				}

				if (next.OnCurve)
				{
					AddQuadratic(edges, prev, curr, next, scale, XMin, YMax);
					prev = next;
					i += 2;
					continue;
				}

				var mid = MidPoint(curr, next);
				AddQuadratic(edges, prev, curr, mid, scale, XMin, YMax);
				prev = mid;
				i++;
			}

			if (!PointsEqual(prev, startPoint))
			{
				edges.Add(new Edge(prev, startPoint, scale, XMin, YMax));
			}
		}

		return edges;
	}

	private static bool PointsEqual(TtfPoint a, TtfPoint b) => a.X == b.X && a.Y == b.Y;

	private static void AddQuadratic(List<Edge> edges, TtfPoint p0, TtfPoint p1, TtfPoint p2, float scale, int xMin, int yMax)
	{
		var steps = EstimateQuadraticSteps(p0, p1, p2, scale);
		var prev = new TtfPointF(p0.X, p0.Y);
		for (var i = 1; i <= steps; i++)
		{
			var t = i / (float)steps;
			var a = LerpF(p0, p1, t);
			var b = LerpF(p1, p2, t);
			var p = LerpF(a, b, t);
			edges.Add(new Edge(prev, p, scale, xMin, yMax));
			prev = p;
		}
	}

	private static int EstimateQuadraticSteps(TtfPoint p0, TtfPoint p1, TtfPoint p2, float scale)
	{
		var d0 = Distance(p0, p1);
		var d1 = Distance(p1, p2);
		var length = (d0 + d1) * scale;
		var steps = (int)MathF.Ceiling(length / 4f);
		return Math.Clamp(steps, 8, 64);
	}

	private static float Distance(TtfPoint a, TtfPoint b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return MathF.Sqrt((dx * dx) + (dy * dy));
	}

	private static TtfPointF LerpF(TtfPoint a, TtfPoint b, float t) => new(
		a.X + (b.X - a.X) * t,
		a.Y + (b.Y - a.Y) * t
	);

	private static TtfPointF LerpF(TtfPointF a, TtfPointF b, float t) => new(
		a.X + (b.X - a.X) * t,
		a.Y + (b.Y - a.Y) * t
	);

	private static TtfPoint MidPoint(TtfPoint a, TtfPoint b) => new()
	{
		X = (a.X + b.X) / 2,
		Y = (a.Y + b.Y) / 2,
		OnCurve = true
	};

	private void RasterizeEdges(byte[] alpha, int width, int height, List<Edge> edges)
	{
		const int samples = 8;
		var invSamples = 1f / (samples * samples);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var coverage = 0f;
				for (var sy = 0; sy < samples; sy++)
				{
					for (var sx = 0; sx < samples; sx++)
					{
						var px = x + (sx + 0.5f) / samples;
						var py = y + (sy + 0.5f) / samples;
						if (IsInside(px, py, edges))
						{
							coverage += invSamples;
						}
					}
				}

				alpha[(y * width) + x] = (byte)Math.Clamp((int)(coverage * 255f), 0, 255);
			}
		}
	}

	private static bool IsInside(float x, float y, List<Edge> edges)
	{
		var winding = 0;
		foreach (var edge in edges)
		{
			winding += edge.WindingAt(x, y);
		}

		return winding != 0;
	}
}

internal readonly record struct GlyphBitmap(int Width, int Height, byte[] Alpha);

internal readonly record struct TtfPointF(float X, float Y);

internal readonly record struct Edge
{
	private readonly float _x0;
	private readonly float _y0;
	private readonly float _x1;
	private readonly float _y1;

	public Edge(TtfPoint p0, TtfPoint p1, float scale, int xMin, int yMax)
	{
		_x0 = (p0.X - xMin) * scale;
		_y0 = (yMax - p0.Y) * scale;
		_x1 = (p1.X - xMin) * scale;
		_y1 = (yMax - p1.Y) * scale;
	}

	public Edge(TtfPointF p0, TtfPointF p1, float scale, int xMin, int yMax)
	{
		_x0 = (p0.X - xMin) * scale;
		_y0 = (yMax - p0.Y) * scale;
		_x1 = (p1.X - xMin) * scale;
		_y1 = (yMax - p1.Y) * scale;
	}

	public bool Intersects(float x, float y)
	{
		if ((_y0 > y && _y1 > y) || (_y0 < y && _y1 < y) || _y0 == _y1)
		{
			return false;
		}

		var t = (y - _y0) / (_y1 - _y0);
		var ix = _x0 + t * (_x1 - _x0);
		return ix > x;
	}

	public int WindingAt(float x, float y)
	{
		if (((_y0 <= y) && (_y1 > y)) || ((_y1 <= y) && (_y0 > y)))
		{
			var t = (y - _y0) / (_y1 - _y0);
			var ix = _x0 + t * (_x1 - _x0);
			if (x < ix)
			{
				return _y1 > _y0 ? 1 : -1;
			}
		}

		return 0;
	}
}

internal sealed class CmapFormat4
{
	private readonly ushort[] _endCode;
	private readonly ushort[] _startCode;
	private readonly short[] _idDelta;
	private readonly ushort[] _idRangeOffset;
	private readonly int _idRangeOffsetStart;
	private readonly byte[] _data;

	private CmapFormat4(ushort[] endCode, ushort[] startCode, short[] idDelta, ushort[] idRangeOffset, int idRangeOffsetStart, byte[] data)
	{
		_endCode = endCode;
		_startCode = startCode;
		_idDelta = idDelta;
		_idRangeOffset = idRangeOffset;
		_idRangeOffsetStart = idRangeOffsetStart;
		_data = data;
	}

	public static CmapFormat4 Parse(TtfReader cmap)
	{
		cmap.Seek(2);
		var numTables = cmap.ReadU16();
		var chosenOffset = -1;
		var chosenPriority = int.MaxValue;

		for (var i = 0; i < numTables; i++)
		{
			var platform = cmap.ReadU16();
			var encoding = cmap.ReadU16();
			var offset = cmap.ReadU32();
			var probeReader = cmap.SliceAt((int)offset);
			var probeFormat = probeReader.ReadU16();
			if (probeFormat != 4)
			{
				continue;
			}

			var priority = (platform, encoding) switch
			{
				(3, 1) => 0,
				(0, 3) => 1,
				(0, 4) => 2,
				(0, 0) => 3,
				(3, 0) => 4,
				_ => int.MaxValue,
			};
			if (priority >= chosenPriority)
			{
				continue;
			}

			chosenPriority = priority;
			chosenOffset = (int)offset;
		}

		if (chosenOffset < 0)
		{
			throw new InvalidOperationException("No supported cmap found.");
		}

		var formatReader = cmap.SliceAt(chosenOffset);
		var format = formatReader.ReadU16();
		if (format != 4)
		{
			throw new InvalidOperationException("Only cmap format 4 is supported.");
		}

		formatReader.Skip(2);
		formatReader.Skip(2);
		var segCountX2 = formatReader.ReadU16();
		var segCount = segCountX2 / 2;
		formatReader.Skip(6);

		var endCode = new ushort[segCount];
		for (var i = 0; i < segCount; i++)
		{
			endCode[i] = formatReader.ReadU16();
		}

		formatReader.Skip(2);
		var startCode = new ushort[segCount];
		for (var i = 0; i < segCount; i++)
		{
			startCode[i] = formatReader.ReadU16();
		}

		var idDelta = new short[segCount];
		for (var i = 0; i < segCount; i++)
		{
			idDelta[i] = formatReader.ReadS16();
		}

		var idRangeOffsetStart = formatReader.Position;
		var idRangeOffset = new ushort[segCount];
		for (var i = 0; i < segCount; i++)
		{
			idRangeOffset[i] = formatReader.ReadU16();
		}

		return new CmapFormat4(endCode, startCode, idDelta, idRangeOffset, cmap.BaseOffset + chosenOffset + idRangeOffsetStart, cmap.RawData);
	}

	public int Map(char c)
	{
		var code = (ushort)c;
		for (var i = 0; i < _endCode.Length; i++)
		{
			if (code < _startCode[i] || code > _endCode[i])
			{
				continue;
			}

			if (_idRangeOffset[i] == 0)
			{
				return (code + _idDelta[i]) & 0xFFFF;
			}

			var offset = _idRangeOffset[i] + (code - _startCode[i]) * 2;
			var glyphIndexOffset = _idRangeOffsetStart + (i * 2) + offset;
			if (glyphIndexOffset + 1 >= _data.Length)
			{
				return 0;
			}

			var glyphIndex = (ushort)((_data[glyphIndexOffset] << 8) | _data[glyphIndexOffset + 1]);
			if (glyphIndex == 0)
			{
				return 0;
			}

			return (glyphIndex + _idDelta[i]) & 0xFFFF;
		}

		return 0;
	}
}

