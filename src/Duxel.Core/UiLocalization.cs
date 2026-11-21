using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Duxel.Core;

/// <summary>An immutable, reusable catalog of application UI translations.</summary>
/// <remarks>The source string also serves as the key and the default display text.</remarks>
public sealed class UiLocalization
{
    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _translations;

    public UiLocalization(IEnumerable<(string CultureName, string Source, string Text)> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);
        var cultures = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (cultureName, source, text) in translations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
            ArgumentException.ThrowIfNullOrWhiteSpace(source);
            ArgumentNullException.ThrowIfNull(text);
            var name = CultureInfo.GetCultureInfo(cultureName).Name;
            if (!cultures.TryGetValue(name, out var entries))
            {
                entries = new(StringComparer.Ordinal);
                cultures.Add(name, entries);
            }

            if (!entries.TryAdd(source, text))
                throw new ArgumentException($"Duplicate translation for '{source}' in '{name}'.", nameof(translations));
        }

        _translations = cultures.ToFrozenDictionary(entry => entry.Key,
            entry => entry.Value.ToFrozenDictionary(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Looks up the requested culture and then its parent cultures, in order.</summary>
    public bool TryGetText(string source, CultureInfo culture, [NotNullWhen(true)] out string? text)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(culture);
        for (var current = culture; current.Name.Length != 0; current = current.Parent)
        {
            if (_translations.TryGetValue(current.Name, out var entries) && entries.TryGetValue(source, out text))
                return true;
        }

        text = null;
        return false;
    }

    public string GetText(string source, CultureInfo culture) =>
        TryGetText(source, culture, out var text) ? text : source;

    public string Format(string source, CultureInfo culture, params object?[] arguments) =>
        string.Format(culture, GetText(source, culture), arguments);
}
