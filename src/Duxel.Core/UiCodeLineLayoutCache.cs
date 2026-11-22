namespace Duxel.Core;

// Only line-local layout is shared. Document positions and paint-style slots
// still belong to the CodeView that assembles the result.
internal sealed class UiCodeLineLayoutCache
{
    private const int Capacity = 512;
    private const int MaximumCacheableLineLength = 256;
    private readonly Dictionary<Key, PlatformTextParagraphLayout> _layouts = [];
    private readonly Queue<Key> _insertionOrder = new();
    private UiLayoutMeasurementKey _measurementKey;

    internal PlatformTextParagraphLayout? FindNatural(string text, PlatformTextParagraphStyle[] styles,
        bool vector, UiLayoutMeasurementKey measurementKey)
        => _measurementKey == measurementKey && _layouts.TryGetValue(new(text, styles, float.PositiveInfinity, vector), out var natural)
            ? natural : null;

    internal PlatformTextParagraphLayout Create(IPlatformTextParagraphBackend backend, string text,
        PlatformTextParagraphStyle[] styles, float width, bool vector, UiLayoutMeasurementKey measurementKey,
        out bool built, bool reuseWidthFit)
    {
        if (text.Length > MaximumCacheableLineLength)
        {
            built = true;
            return backend.CreateParagraph(new(text, styles, width), vector);
        }
        if (_measurementKey != measurementKey)
        {
            _layouts.Clear();
            _insertionOrder.Clear();
            _measurementKey = measurementKey;
        }
        var key = new Key(text, styles, width, vector);
        if (_layouts.TryGetValue(key, out var layout))
        {
            built = false;
            return layout;
        }

        layout = backend.CreateParagraph(new(text, styles, width), vector);
        if (reuseWidthFit && float.IsFinite(width) && FitsWidth(layout, width))
        {
            key = key with { Width = float.PositiveInfinity };
            if (_layouts.ContainsKey(key))
            {
                built = true;
                return layout;
            }
        }
        if (_layouts.Count == Capacity)
            _layouts.Remove(_insertionOrder.Dequeue());
        _layouts.Add(key, layout);
        _insertionOrder.Enqueue(key);
        built = true;
        return layout;
    }

    internal static bool FitsWidth(PlatformTextParagraphLayout layout, float width)
        => layout.Lines.Length == 1 && layout.Width <= width;

    private readonly record struct Key(string Text, PlatformTextParagraphStyle[] Styles, float Width, bool Vector)
    {
        public bool Equals(Key other) => Text == other.Text && Width == other.Width && Vector == other.Vector
            && Styles.AsSpan().SequenceEqual(other.Styles);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Text, StringComparer.Ordinal);
            hash.Add(Width);
            hash.Add(Vector);
            foreach (var style in Styles)
                hash.Add(style);
            return hash.ToHashCode();
        }
    }
}
