using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Duxel.Core;

namespace Duxel.Gallery;

internal interface IGallerySourcePage
{
    bool HasInlineSamples => true;
    bool SynchronizesOwnSources => false;
    IReadOnlyList<IGallerySample> Samples { get; }
}
internal interface IGallerySample
{
    string Source { get; }
    UiCodeViewMetrics Metrics { get; }
    void SynchronizeSource();
}

internal static partial class GallerySample
{
    internal static GalleryValue<T> Define<T>(string name, Func<T> create,
        [CallerArgumentExpression(nameof(create))] string expression = "")
    {
        var body = LocalizeSource(expression.Trim());
        var source = body.StartsWith("() => ", StringComparison.Ordinal) && !body.AsSpan(6).TrimStart().StartsWith("{")
            ? $"var {name} = {body[6..].Trim()};"
            : $"var {name}Factory = {body};\nvar {name} = {name}Factory();";
        return new(create(), source);
    }
    // Inline literal translation keys in the displayed C#.
    [GeneratedRegex("""\bGalleryText\.Get\("(?<text>[^"\\\r\n]*)"\)""", RegexOptions.CultureInvariant)]
    private static partial Regex TranslationCall();

    internal static string LocalizeSource(string source) => TranslationCall().Replace(source,
        match => String(GalleryText.Get(match.Groups["text"].Value)));

    internal static string Boolean(bool value) => value ? "true" : "false";
    internal static string Number(float? value) => value is { } number
        ? number.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f" : "null";
    internal static string String(string? value)
    {
        if (value is null) return "null";
        var result = new System.Text.StringBuilder("\"");
        foreach (var character in value)
        {
            result.Append(character switch
            {
                '\\' => "\\\\",
                '\"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when NeedsUnicodeEscape(character) => "\\u" + ((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture),
                _ => character.ToString()
            });
        }
        return result.Append('\"').ToString();
    }
    private static bool NeedsUnicodeEscape(char character) => char.IsControl(character) || character is '\u2028' or '\u2029';

    internal static string DocumentString(string value)
    {
        if (!value.Contains('\n') || value.Any(character => NeedsUnicodeEscape(character) && character is not ('\n' or '\t')))
            return String(value);
        var longest = 0;
        var run = 0;
        foreach (var character in value)
        {
            run = character == '"' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        var delimiter = new string('"', Math.Max(3, longest + 1));
        return delimiter + "\n" + value + "\n" + delimiter;
    }
    internal static UiCodeView InlineCode(string source) => new(source,
        new(MaximumVisibleLines : 10, InitiallyExpanded : false, ShowToolbar : false), GalleryText.CodeViewLabels());

    internal static GallerySample<T> Create<T>(Func<T> create, string setup = "", string declarations = "",
        Func<UiWidget, UiWidget>? layout = null,
        [CallerArgumentExpression(nameof(create))] string expression = "",
        [CallerArgumentExpression(nameof(layout))] string layoutExpression = "") where T : UiWidget
    {
        var body = LocalizeSource(expression.Trim());
        var imports = (body.Contains("UiSvg", StringComparison.Ordinal) || setup.Contains("SvgStore", StringComparison.Ordinal)
            ? "using Duxel.Svg;\n" : string.Empty);
        var variable = layout is null ? "example" : "control";
        var source = imports + (imports.Length == 0 ? string.Empty : "\n") + (setup.Length == 0 ? string.Empty : setup.Trim() + "\n\n")
            + (body.StartsWith("() => ", StringComparison.Ordinal) && !body.AsSpan(6).TrimStart().StartsWith("{")
                ? "UiWidget " + variable + " = " + body[6..].Trim() + ";"
                : "Func<UiWidget> create = " + body + ";\nUiWidget " + variable + " = create();");
        if (layout is not null)
            source += "\n\nFunc<UiWidget, UiWidget> layout = " + LocalizeSource(layoutExpression) + ";\nUiWidget example = layout(control);";
        if (declarations.Length != 0) source += "\n\n" + declarations;
        var widget = create();
        return new(widget, InlineCode(source), layout?.Invoke(widget));
    }
}

// The caller owns the value; the declaration is captured from its actual factory.
internal readonly record struct GalleryValue<T>(T Value, string Source);

// Both children are owned immediately, including when a page is disposed before
// its first paint. Each source block follows exactly one persistent preview.
internal sealed class GallerySample<T>(T preview, UiCodeView code, UiWidget? previewLayout = null)
    : UiFlexWidget<UiVerticalAxis>([previewLayout ?? preview, code], new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min,
        CrossAxisAlignment: UiCrossAxisAlignment.Start)), IGallerySample where T : UiWidget
{
    private ISourceBinding? _sourceBinding;
    private string? _fullSource;
    internal T Preview => preview;
    internal UiCodeView CodeView => code;
    internal UiWidget PreviewLayout => previewLayout ?? preview;
    internal GallerySample<T> WithExcerpt(string excerpt)
    {
        if (_sourceBinding is not null || _fullSource is not null)
            throw new InvalidOperationException("An explicit excerpt requires a static Gallery example.");
        _fullSource = code.Code;
        code.Code = GallerySample.LocalizeSource(excerpt);
        return this;
    }
    internal GallerySample<T> BindSource<TState>(Func<TState> read, Func<TState, string> format)
    {
        _sourceBinding = new SourceBinding<TState>(read, format);
        SynchronizeSource();
        return this;
    }
    public void SynchronizeSource() => _sourceBinding?.Synchronize(code);
    private interface ISourceBinding
    {
        void Synchronize(UiCodeView target);
    }
    private sealed class SourceBinding<TState>(Func<TState> read, Func<TState, string> format) : ISourceBinding
    {
        private bool _initialized;
        private TState? _previous;
        public void Synchronize(UiCodeView target)
        {
            var state = read();
            if (_initialized && EqualityComparer<TState>.Default.Equals(_previous!, state)) return;
            target.Code = format(state);
            _previous = state;
            _initialized = true;
        }
    }
    public string Source => _fullSource ?? code.Code;
    public UiCodeViewMetrics Metrics => code.Metrics;
}
