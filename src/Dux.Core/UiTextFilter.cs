using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dux.Core;

public sealed class UiTextFilter
{
    private readonly List<FilterTerm> _terms = [];
    private string _filter = string.Empty;

    private readonly record struct FilterTerm(string Text, bool Exclude);

    public bool IsActive => _terms.Count > 0;

    public string Filter
    {
        get => _filter;
        set
        {
            _filter = value ?? string.Empty;
            Build();
        }
    }

    public bool Draw(UiImmediateContext ui, string label = "Filter", float width = 0f)
    {
        ArgumentNullException.ThrowIfNull(ui);
        label ??= "Filter";

        var changed = ui.InputText(label, ref _filter, 256);
        if (changed)
        {
            Build();
        }

        return changed;
    }

    public bool PassFilter(string? text)
    {
        if (_terms.Count == 0)
        {
            return true;
        }

        text ??= string.Empty;
        var hasInclude = false;
        var matchedInclude = false;

        foreach (var term in _terms)
        {
            if (term.Exclude)
            {
                if (ContainsTerm(text, term.Text))
                {
                    return false;
                }
            }
            else
            {
                hasInclude = true;
                if (!matchedInclude && ContainsTerm(text, term.Text))
                {
                    matchedInclude = true;
                }
            }
        }

        return !hasInclude || matchedInclude;
    }

    public void Build()
    {
        _terms.Clear();
        if (string.IsNullOrWhiteSpace(_filter))
        {
            return;
        }

        foreach (var token in SplitTokens(_filter))
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var trimmed = token.Trim();
            var exclude = trimmed.StartsWith('-');
            var text = exclude ? trimmed[1..].Trim() : trimmed;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            _terms.Add(new FilterTerm(text, exclude));
        }
    }

    public void Clear()
    {
        _filter = string.Empty;
        _terms.Clear();
    }

    public void Append(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_filter))
        {
            _filter = string.Concat(_filter, ",", text.Trim());
        }
        else
        {
            _filter = text.Trim();
        }

        Build();
    }

    public void Appendf(string format, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(format);
        var text = string.Format(CultureInfo.InvariantCulture, format, args);
        Append(text);
    }

    public void Appendfv(string format, IReadOnlyList<object?> args)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(args);
        var text = string.Format(CultureInfo.InvariantCulture, format, args);
        Append(text);
    }

    private static bool ContainsTerm(string text, string term)
    {
        return text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static IEnumerable<string> SplitTokens(string filter)
    {
        var start = 0;
        for (var i = 0; i <= filter.Length; i++)
        {
            if (i == filter.Length || filter[i] == ',')
            {
                if (i > start)
                {
                    yield return filter[start..i];
                }
                start = i + 1;
            }
        }
    }
}
