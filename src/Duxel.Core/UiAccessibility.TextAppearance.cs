namespace Duxel.Core;

public readonly record struct UiAccessibilityTextFontRun(int Start, int End, PlatformFontMetadata Font);
internal readonly record struct UiAccessibilityTextFontKey(UiLayoutMeasurementKey Measurement, string Language, bool Fonts, UiAccessibilityTextSize? Size);

/// <summary>Immutable uniform or sparse source-text paint. It excludes selection and placeholders.</summary>
public sealed partial record UiAccessibilityTextAppearance
{
    private readonly UiAccessibilityTextFontRun[] _fonts;
    public UiAccessibilityTextSize? Size
    {
        get;
    }
    public int FontRunCount => _fonts.Length;
    public UiAccessibilityTextFontRun GetFontRun(int index)
    {
        if ((uint)index >= (uint)_fonts.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _fonts[index];
    }
    public (int Start, int End, PlatformFontMetadata? Font) GetFontRangeAt(int offset)
    {
        if ((uint)offset > (uint)Content.CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(offset));
        var lookup = offset == Content.CharacterCount && offset > 0 ? offset - 1 : offset;
        var low = 0;
        var high = _fonts.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_fonts[middle].Start <= lookup)
                low = middle + 1;
            else
                high = middle;
        }
        if (low > 0 && lookup < _fonts[low - 1].End)
        {
            var run = _fonts[low - 1];
            return (run.Start, run.End, run.Font);
        }
        return (low > 0 ? _fonts[low - 1].End : 0, low < _fonts.Length ? _fonts[low].Start : Content.CharacterCount, null);
    }
    public UiAccessibilityTextContent Content
    {
        get;
    }
    private readonly UiColor _foreground;
    public UiColor Foreground => HasUniformPaint ? _foreground
        : throw new InvalidOperationException("Styled text has per-source paint ranges rather than one foreground.");
    public UiAccessibilityTextAppearance(UiAccessibilityTextContent content, UiColor foreground)
        : this(content, foreground, Array.Empty<PlatformTextFontRun>()) { }
    public UiAccessibilityTextAppearance(UiAccessibilityTextContent content, UiColor foreground, IReadOnlyList<PlatformTextFontRun> fonts)
        : this(content, foreground, fonts, null) { }
    public UiAccessibilityTextAppearance(UiAccessibilityTextContent content, UiColor foreground, IReadOnlyList<PlatformTextFontRun> fonts, UiAccessibilityTextSize? size)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fonts);
        size?.Validate();
        Size = size;
        Content = content;
        _foreground = foreground;
        _fonts = new UiAccessibilityTextFontRun[fonts.Count];
        var end = 0;
        for (var i = 0; i < fonts.Count; i++)
        {
            var run = fonts[i];
            if (run.StartUtf16 < end || run.EndUtf16 <= run.StartUtf16 || run.EndUtf16 > content.Text.Length || run.Font is null)
                throw new ArgumentException("Font runs must be ordered, nonoverlapping source intervals.", nameof(fonts));
            run.Font.Validate();
            _fonts[i] = new(content.GetCharacterOffset(run.StartUtf16), content.GetCharacterOffset(run.EndUtf16), run.Font);
            end = run.EndUtf16;
        }
    }
    private UiAccessibilityTextAppearance(UiAccessibilityTextAppearance previous, UiColor foreground)
        => (Content, _foreground, _fonts, Size) = (previous.Content, foreground, previous._fonts, previous.Size);
    internal UiAccessibilityTextAppearance WithForeground(UiColor foreground) => HasUniformPaint ? new(this, foreground)
        : throw new InvalidOperationException("Uniform color replacement cannot discard styled source ranges.");
}

public sealed partial class UiAccessibilityTree
{
    private string _fontNameLanguage = "und";
    public string FontNameLanguage
    {
        get => Volatile.Read(ref _fontNameLanguage);
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            PlatformFontMetadata.ValidateNameLanguage(value);
            if (value == FontNameLanguage)
                return;
            Volatile.Write(ref _fontNameLanguage, value);
            _wake?.Invoke();
        }
    }
    private Entry GetEntry(string id)
    {
        if (_entries.TryGetValue(id, out var entry))
            return entry;
        entry = new(checked(++_nextRuntimeId));
        _entries.Add(id, entry);
        return entry;
    }
    internal void CaptureTextAppearance(string id, UiAccessibilityTextContent content, UiColor foreground,
        UiLayoutMeasurementKey measurement, IPlatformTextFontRunBackend? backend, PlatformTextMeasureRequest? request, UiAccessibilityTextSize? size)
    {
        if (!_building)
            throw new InvalidOperationException("Text paint capture requires an active accessibility frame.");
        var entry = GetEntry(id);
        entry.StyledPaintKey = null;
        if ((backend is null) != (request is null))
            throw new InvalidOperationException("Font capture requires both a backend and its exact request.");
        var key = new UiAccessibilityTextFontKey(measurement, FontNameLanguage, backend is not null, size);
        if (entry.TextAppearance is not { } previous || !ReferenceEquals(previous.Content, content) || entry.TextFontKey != key)
        {
            var fonts = backend is not null && request.HasValue ? backend.GetFontRuns(request.Value, key.Language) : [];
            entry.TextAppearance = new(content, foreground, fonts, size);
            entry.TextFontKey = key;
        }
        else if (previous.Foreground != foreground)
            entry.TextAppearance = previous.WithForeground(foreground);
        entry.TextPaintGeneration = _generation;
        if (entry.Seen == _generation && ReferenceEquals(entry.Staged.TextInfo?.Content, content))
            entry.Staged = entry.Staged with
            {
                TextAppearance = entry.TextAppearance
            };
    }
}
