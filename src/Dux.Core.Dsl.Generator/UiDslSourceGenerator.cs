using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Dux.Core.Dsl.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class UiDslSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var uiFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".ui", StringComparison.OrdinalIgnoreCase))
            .Select((file, ct) => (file.Path, file.GetText(ct)?.ToString() ?? string.Empty));

        context.RegisterSourceOutput(uiFiles, (spc, item) =>
        {
            var (path, content) = item;
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            UiDslDocument document;
            try
            {
                document = UiDslParser.Parse(content);
            }
            catch (Exception ex)
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DUXDSL001",
                        "UI DSL parse error",
                        "{0}",
                        "UI.DSL",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    Location.None,
                    ex.Message
                );
                spc.ReportDiagnostic(diag);
                return;
            }

            var className = UiDslNames.GetClassName(path);
            var source = UiDslCompiler.Generate("Dux.Generated.Ui", className, document);
            spc.AddSource($"{className}.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }
}

internal static class UiDslNames
{
    public static string GetClassName(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var sb = new StringBuilder();
        foreach (var ch in fileName)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        if (sb.Length == 0)
        {
            sb.Append("UiDoc");
        }

        sb.Append("Ui");
        return sb.ToString();
    }
}

internal sealed class UiDslDocument
{
    public UiDslDocument(IReadOnlyList<UiDslNode> roots) => Roots = roots;
    public IReadOnlyList<UiDslNode> Roots { get; }
}

internal sealed class UiDslNode
{
    public UiDslNode(string name, IReadOnlyList<string> args, List<UiDslNode> children)
    {
        Name = name;
        Args = args;
        Children = children;
    }

    public string Name { get; }
    public IReadOnlyList<string> Args { get; }
    public List<UiDslNode> Children { get; }
}

internal static class UiDslParser
{
    public static UiDslDocument Parse(string text)
    {
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

            var indent = CountIndent(raw, i + 1);
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

    private static int CountIndent(string line, int lineNumber)
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
                throw new InvalidOperationException($"Tabs are not allowed. Line {lineNumber}.");
            }

            break;
        }

        if (count % 2 != 0)
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

            if (ch == '#')
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

internal static class UiDslCompiler
{
    public static string Generate(string @namespace, string className, UiDslDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace " + @namespace + ";");
        sb.AppendLine();
        sb.AppendLine("public static partial class " + className);
        sb.AppendLine("{");
        sb.AppendLine("    public static void Render(Dux.Core.Dsl.IUiDslEmitter emitter)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(emitter);");

        foreach (var node in document.Roots)
        {
            EmitNode(sb, node, 2);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitNode(StringBuilder sb, UiDslNode node, int indent)
    {
        var pad = new string(' ', indent * 4);
        sb.Append(pad).Append("emitter.BeginNode(\"").Append(node.Name).Append("\", ");
        sb.Append("new string[] { ");
        for (var i = 0; i < node.Args.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append("\"").Append(node.Args[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"");
        }
        sb.AppendLine(" });");

        foreach (var child in node.Children)
        {
            EmitNode(sb, child, indent + 1);
        }

        sb.Append(pad).Append("emitter.EndNode(\"").Append(node.Name).AppendLine("\");");
    }
}
