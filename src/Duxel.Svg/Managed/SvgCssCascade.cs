using System.Xml.Linq;

namespace Duxel.Svg.Managed;

internal static class SvgCssCascade
{
    private sealed record Rule(string Selector, int Specificity, int Order, (string Name, string Value, bool Important)[] Values);
    internal static XDocument Apply(XDocument source)
    {
        var document = new XDocument(source);
        List<Rule> rules = [];
        bool RenderedNamespace(XElement element) => element.AncestorsAndSelf().All(parent => parent.Name.NamespaceName is "" or "http://www.w3.org/2000/svg")
            && !element.Ancestors().Any(parent => parent.Name.LocalName == "metadata");
        foreach (var sheet in document.Descendants().Where(e => e.Name.LocalName == "style" && RenderedNamespace(e)).ToArray())
        {
            var text = WithoutComments(sheet.Value);
            var start = 0;
            while (start < text.Length)
            {
                if (string.IsNullOrWhiteSpace(text[start..])) break;
                var open = text.IndexOf('{', start);
                var close = text.IndexOf('}', open + 1);
                if (open < 0 || close < 0 || text.AsSpan(start, open - start).Contains('@'))
                    throw new NotSupportedException("Unsupported or incomplete SVG stylesheet rule.");
                var values = Declarations(text[(open + 1)..close]);
                foreach (var selector in text[start..open].Split(',', StringSplitOptions.TrimEntries))
                {
                    if (selector.Length == 0 || selector.Any(c => c is ':' or '[' or ']' or '+' or '~' or '|'))
                        throw new NotSupportedException("Unsupported SVG CSS selector: " + selector);
                    var specificity = selector.Count(c => c == '#') * 100 + selector.Count(c => c == '.') * 10;
                    foreach (var part in selector.Split([' ', '>'], StringSplitOptions.RemoveEmptyEntries))
                        if (part[0] is not ('#' or '.' or '*')) specificity++;
                    rules.Add(new(selector, specificity, rules.Count + 1, values));
                }
                start = close + 1;
            }
            sheet.Remove();
        }
        foreach (var element in document.Descendants().Where(RenderedNamespace))
        {
            Dictionary<string, (string Value, bool Important, int Specificity, int Order)> selected = new(StringComparer.Ordinal);
            foreach (var attribute in element.Attributes())
                if (!attribute.IsNamespaceDeclaration && attribute.Name.NamespaceName.Length == 0)
                    selected[attribute.Name.LocalName] = (attribute.Value, false, 0, 0);
            void Select((string Name, string Value, bool Important) declaration, int specificity, int order)
            {
                if (selected.TryGetValue(declaration.Name, out var old) &&
                    (old.Important && !declaration.Important || old.Important == declaration.Important &&
                    (old.Specificity > specificity || old.Specificity == specificity && old.Order > order))) return;
                selected[declaration.Name] = (declaration.Value, declaration.Important, specificity, order);
            }
            foreach (var rule in rules)
                if (Matches(element, rule.Selector))
                    foreach (var value in rule.Values) Select(value, rule.Specificity, rule.Order);
            if (element.Attribute("style") is { } inline)
                foreach (var declaration in Declarations(inline.Value)) Select(declaration, 1000, int.MaxValue);
            foreach (var pair in selected)
                if (pair.Key is not ("style" or "href")) element.SetAttributeValue(pair.Key, pair.Value.Value);
            element.Attribute("style")?.Remove();
        }
        return document;
    }
    private static bool Matches(XElement element, string selector)
    {
        var text = selector.Trim();
        var end = text.Length;
        while (end > 0 && !char.IsWhiteSpace(text[end - 1]) && text[end - 1] != '>') end--;
        if (!Simple(element, text[end..])) return false;
        var remaining = text[..end].TrimEnd();
        if (remaining.Length == 0) return true;
        if (remaining.EndsWith('>')) return element.Parent is { } parent && Matches(parent, remaining[..^1]);
        return element.Ancestors().Any(parent => Matches(parent, remaining));
    }
    private static bool Simple(XElement element, string selector)
    {
        var offset = 0;
        while (offset < selector.Length)
        {
            var prefix = selector[offset];
            var start = offset;
            if (prefix is '#' or '.') offset++;
            while (offset < selector.Length && selector[offset] is not ('#' or '.')) offset++;
            var value = selector[(prefix is '#' or '.' ? start + 1 : start)..offset];
            if (value.Length == 0) throw new InvalidDataException("Incomplete SVG CSS selector.");
            if (prefix == '#' && (string?)element.Attribute("id") != value) return false;
            if (prefix == '.' && !((string?)element.Attribute("class") ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(value, StringComparer.Ordinal)) return false;
            if (prefix is not ('#' or '.') && value != "*" && element.Name.LocalName != value) return false;
        }
        return true;
    }
    private static (string Name, string Value, bool Important)[] Declarations(string source)
    {
        var text = WithoutComments(source);
        List<(string, string, bool)> result = [];
        var start = 0;
        var quote = '\0';
        var depth = 0;
        for (var index = 0; index <= text.Length; index++)
        {
            var c = index == text.Length ? ';' : text[index];
            if (c == '\\') { index++; continue; }
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '\'' or '"') { quote = c; continue; }
            if (c == '(') depth++;
            if (c == ')') depth--;
            if (depth < 0) throw new InvalidDataException("Invalid SVG CSS parentheses.");
            if (c != ';' || depth != 0) continue;
            var declaration = text[start..index].Trim();
            start = index + 1;
            if (declaration.Length == 0) continue;
            var colon = declaration.IndexOf(':');
            if (colon <= 0) throw new InvalidDataException("SVG CSS declarations require a property and value.");
            var value = declaration[(colon + 1)..].Trim();
            var important = value.EndsWith("!important", StringComparison.OrdinalIgnoreCase);
            if (important) value = value[..^10].TrimEnd();
            result.Add((declaration[..colon].Trim(), value, important));
        }
        if (quote != '\0' || depth != 0) throw new InvalidDataException("Unterminated SVG CSS property value.");
        return result.ToArray();
    }
    private static string WithoutComments(string source)
    {
        var output = new System.Text.StringBuilder(source.Length);
        var quote = '\0';
        for (var index = 0; index < source.Length; index++)
        {
            var c = source[index];
            if (c == '\\' && index + 1 < source.Length) { output.Append(c).Append(source[++index]); continue; }
            if (quote == '\0' && c == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0) throw new InvalidDataException("Unterminated SVG CSS comment.");
                output.Append(' ');
                index = end + 1;
                continue;
            }
            if (c is '\'' or '"') { if (quote == '\0') quote = c; else if (quote == c) quote = '\0'; }
            output.Append(c);
        }
        return output.ToString();
    }
}
