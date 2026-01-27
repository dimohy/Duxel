using System.Text;

namespace Dux.Core.Dsl;

public sealed class UiDslParserOptions
{
    public int IndentSize { get; init; } = 2;
    public bool AllowTabs { get; init; } = false;
}

public static class UiDslParser
{
    public static UiDslDocument Parse(string text, UiDslParserOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        options ??= new UiDslParserOptions();

        var roots = new List<UiDslNode>();
        var stack = new Stack<(int Indent, UiDslNode Node)>();

        var lines = SplitLines(text);
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = StripComment(lines[i]);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var indent = CountIndent(raw, options, i + 1);
            var content = raw[indent..];
            var tokens = Tokenize(content);
            if (tokens.Count == 0)
            {
                continue;
            }

            var name = tokens[0];
            var args = tokens.Count > 1 ? tokens[1..] : [];
            var node = new UiDslNode(name, args, []);

            while (stack.Count > 0 && indent <= stack.Peek().Indent)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack.Peek().Node.Children.Add(node);
            }

            stack.Push((indent, node));
        }

        return new UiDslDocument(roots);
    }

    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }
        return lines;
    }

    private static int CountIndent(string line, UiDslParserOptions options, int lineNumber)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ')
            {
                count++;
                continue;
            }

            if (ch == '\t')
            {
                if (!options.AllowTabs)
                {
                    throw new InvalidOperationException($"Tabs are not allowed. Line {lineNumber}.");
                }

                count += options.IndentSize;
                continue;
            }

            break;
        }

        if (count % options.IndentSize != 0)
        {
            throw new InvalidOperationException($"Invalid indentation. Line {lineNumber}.");
        }

        return count;
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }

            if (inString)
            {
                continue;
            }

            if (ch == '#' )
            {
                return line[..i];
            }

            if (ch == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                if (inString && i > 0 && text[i - 1] == '\\')
                {
                    sb[^1] = '"';
                    continue;
                }

                inString = !inString;
                continue;
            }

            if (!inString && char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }

        return tokens;
    }
}
