using System.Globalization;
using System.Text;

namespace Duxel.Svg;

internal sealed class SvgFontCatalog : IDisposable
{
    private readonly Dictionary<string, List<ISvgNativeFont>> _families = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ISvgNativeFont, Dictionary<int, bool>> _coverage = [];
    private readonly List<ISvgNativeFont> _resources = [];
    private long _requests, _glyphChecks;
    private bool _disposed;
    internal string DefaultFamily { get; }
    internal SvgFontStatistics Statistics => new(_resources.Count, _requests, _glyphChecks);
    internal IReadOnlyList<ISvgNativeFont> Resources => _resources;
    internal SvgFontCatalog(SvgFontOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DefaultFamily);
        ArgumentNullException.ThrowIfNull(options.Faces);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumFontBytes);
        DefaultFamily = options.DefaultFamily;
        Dictionary<(string, int), ISvgNativeFont> loaded = [];
        try
        {
            foreach (var source in options.Faces.ToArray())
            {
                ArgumentNullException.ThrowIfNull(source);
                ArgumentException.ThrowIfNullOrWhiteSpace(source.Family);
                ArgumentException.ThrowIfNullOrWhiteSpace(source.Path);
                ArgumentOutOfRangeException.ThrowIfNegative(source.CollectionIndex);
                if (source.Family.StartsWith("__duxel_svg_font_", StringComparison.Ordinal))
                    throw new ArgumentException("Font aliases cannot use the reserved SVG font prefix.");
                var path = Path.GetFullPath(source.Path);
                var key = (OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path, source.CollectionIndex);
                if (!loaded.TryGetValue(key, out var font))
                {
                    using var stream = File.OpenRead(path);
                    if (stream.Length > options.MaximumFontBytes) throw new InvalidDataException("SVG font exceeds its configured byte limit.");
                    var bytes = new byte[checked((int)stream.Length)];
                    stream.ReadExactly(bytes);
                    font = SvgNativeFont.Create(bytes, source.CollectionIndex);
                    loaded.Add(key, font);
                    _resources.Add(font);
                    _coverage.Add(font, []);
                }
                if (!_families.TryGetValue(source.Family, out var family)) _families.Add(source.Family, family = []);
                if (family.Any(f => f.Weight == font.Weight && f.Width == font.Width && f.Italic == font.Italic && f.Oblique == font.Oblique))
                    throw new ArgumentException("Duplicate SVG font family/style: " + source.Family);
                family.Add(font);
            }
            if (!_families.ContainsKey(DefaultFamily)) throw new ArgumentException("The default SVG font family must be explicitly registered.");
        }
        catch { Dispose(); throw; }
    }
    internal List<(string Text, ISvgNativeFont Font)> Runs(string text, string? family, int weight = 400, int width = 5, string style = "normal")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _requests++;
        if (style is not ("normal" or "italic" or "oblique")) throw new InvalidDataException("Invalid SVG font style.");
        List<ISvgNativeFont> candidates = [];
        foreach (var name in Families(family ?? DefaultFamily))
            if (_families.TryGetValue(name, out var faces))
                foreach (var face in faces)
                    if (face.Weight == weight && face.Width == width && (style == "italic") == face.Italic && (style == "oblique") == face.Oblique)
                        candidates.Add(face);
        if (candidates.Count == 0) throw new InvalidDataException("SVG font family/style is not registered: " + family);
        List<(string Text, ISvgNativeFont Font)> result = [];
        var elements = StringInfo.GetTextElementEnumerator(text);
        ISvgNativeFont? current = null;
        var start = 0;
        while (elements.MoveNext())
        {
            var cluster = elements.GetTextElement();
            ISvgNativeFont? selected = null;
            foreach (var candidate in candidates)
            {
                var covered = true;
                foreach (var rune in cluster.EnumerateRunes())
                {
                    if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format || rune.Value is '\r' or '\n' or '\t' ||
                        rune.Value is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF) continue;
                    if (!_coverage[candidate].TryGetValue(rune.Value, out var present))
                    {
                        present = candidate.Contains((uint)rune.Value);
                        _coverage[candidate].Add(rune.Value, present);
                        _glyphChecks++;
                    }
                    if (!present) { covered = false; break; }
                }
                if (covered) { selected = candidate; break; }
            }
            if (selected is null) throw new InvalidDataException("Registered SVG font stack cannot render text cluster: " + cluster);
            if (ReferenceEquals(current, selected)) continue;
            if (current is not null) result.Add((text[start..elements.ElementIndex], current));
            current = selected;
            start = elements.ElementIndex;
        }
        if (current is not null) result.Add((text[start..], current));
        return result;
    }
    private static IEnumerable<string> Families(string value)
    {
        var start = 0;
        var quote = '\0';
        for (var index = 0; index <= value.Length; index++)
        {
            if (index < value.Length)
            {
                var c = value[index];
                if (c == '\\') { index++; continue; }
                if (c is '\'' or '"') { if (quote == '\0') quote = c; else if (quote == c) quote = '\0'; }
                if (c != ',' || quote != '\0') continue;
            }
            var name = value[start..index].Trim();
            if (name.Length > 1 && name[0] is '\'' or '"' && name[^1] == name[0]) name = name[1..^1];
            if (name.Length > 0) yield return Managed.SvgCssEscapes.Decode(name);
            start = index + 1;
        }
        if (quote != '\0') throw new InvalidDataException("Unterminated SVG font-family string.");
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var font in _resources) font.Dispose();
        _resources.Clear();
        _coverage.Clear();
        _families.Clear();
    }
}
