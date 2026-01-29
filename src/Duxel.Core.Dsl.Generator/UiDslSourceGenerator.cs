using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Duxel.Core.Dsl.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class UiDslSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var uiFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".ui", StringComparison.OrdinalIgnoreCase))
            .Select((file, ct) => new UiDslSourceFile(file.Path, file.GetText(ct)?.ToString() ?? string.Empty));

        var uiDocs = uiFiles
            .Where(static file => !string.IsNullOrWhiteSpace(file.Content));

        context.RegisterSourceOutput(uiDocs, (spc, item) =>
        {
            var path = item.Path;
            var content = item.Content;

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
            var source = UiDslCompiler.Generate("Duxel.Generated.Ui", className, document);
            spc.AddSource($"{className}.g.cs", SourceText.From(source, Encoding.UTF8));
        });

        var registryItems = uiDocs
            .Select(static (file, _) => new UiDslRegistryItem(file.Path, UiDslNames.GetClassName(file.Path)))
            .Collect();

        context.RegisterSourceOutput(registryItems, (spc, items) =>
        {
            if (items.IsDefaultOrEmpty)
            {
                return;
            }

            var source = UiDslRegistryEmitter.Emit(items);
            spc.AddSource("UiDslGeneratedRegistry.g.cs", SourceText.From(source, Encoding.UTF8));

            var autoSource = UiDslAutoEmitter.Emit(items);
            spc.AddSource("UiDslAuto.g.cs", SourceText.From(autoSource, Encoding.UTF8));
        });
    }
}

internal readonly record struct UiDslSourceFile(string Path, string Content);
internal readonly record struct UiDslRegistryItem(string Path, string ClassName);

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

internal static class UiDslRegistryEmitter
{
    public static string Emit(ImmutableArray<UiDslRegistryItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Duxel.Generated.Ui;");
        sb.AppendLine();
        sb.AppendLine("public static partial class UiDslGeneratedRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly global::System.Collections.Generic.Dictionary<string, global::System.Action<global::Duxel.Core.Dsl.IUiDslEmitter>> s_renderers = new()");
        sb.AppendLine("    {");

        for (var i = 0; i < items.Length; i++)
        {
            var className = items[i].ClassName;
            sb.Append("        [\"").Append(className).Append("\"] = ").Append(className).Append(".Render");
            sb.AppendLine(",");
        }

        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    public static global::System.Action<global::Duxel.Core.Dsl.IUiDslEmitter> GetRenderer(string name)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (TryGetRenderer(name, out var render))");
        sb.AppendLine("        {");
        sb.AppendLine("            return render;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        throw new global::System.InvalidOperationException($\"Unknown DSL document: {name}.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static bool TryGetRenderer(string name, out global::System.Action<global::Duxel.Core.Dsl.IUiDslEmitter> render)");
        sb.AppendLine("        => s_renderers.TryGetValue(name, out render);");
        sb.AppendLine();
        sb.AppendLine("    public static string[] Names { get; } = new string[]");
        sb.AppendLine("    {");

        for (var i = 0; i < items.Length; i++)
        {
            sb.Append("        \"").Append(items[i].ClassName).Append("\"");
            sb.AppendLine(",");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }
}

internal static class UiDslAutoEmitter
{
    public static string Emit(ImmutableArray<UiDslRegistryItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace Duxel.Generated.Ui;");
        sb.AppendLine();
        sb.AppendLine("internal static class UiDslAutoRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");
        sb.AppendLine("#if DUX_NATIVEAOT");
        sb.AppendLine("        global::Duxel.Core.Dsl.UiDslAuto.RegisterResolver(ResolveAot);");
        sb.AppendLine("#else");
        sb.AppendLine("        global::Duxel.Core.Dsl.UiDslAuto.RegisterResolver(ResolveRuntime);");
        sb.AppendLine("#endif");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static global::System.Action<global::Duxel.Core.Dsl.IUiDslEmitter> ResolveAot(string uiFile)");
        sb.AppendLine("    {");
        sb.AppendLine("        var fileName = GetFileName(uiFile);");
        sb.AppendLine("        return fileName switch");
        sb.AppendLine("        {");
        for (var i = 0; i < items.Length; i++)
        {
            var fileName = Path.GetFileName(items[i].Path);
            var className = items[i].ClassName;
            sb.Append("            \"").Append(fileName).Append("\" => ");
            sb.Append("global::Duxel.Core.Dsl.UiDslPipeline.CreateGeneratedRenderer(\"")
                .Append(className)
                .Append("\", global::Duxel.Generated.Ui.UiDslGeneratedRegistry.GetRenderer)");
            sb.AppendLine(",");
        }
        sb.AppendLine("            _ => throw new global::System.InvalidOperationException($\"Unknown UI file: {fileName}.\")");
        sb.AppendLine("        };\n");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static global::System.Action<global::Duxel.Core.Dsl.IUiDslEmitter> ResolveRuntime(string uiFile)");
        sb.AppendLine("    {");
        sb.AppendLine("        var fileName = GetFileName(uiFile);");
        sb.AppendLine("        var relative = global::System.IO.Path.Combine(\"Ui\", fileName);");
        sb.AppendLine("        var uiPath = global::Duxel.Core.Dsl.UiDslSourcePathResolver.ResolveFromProjectRoot(relative);");
        sb.AppendLine("        return global::Duxel.Core.Dsl.UiDslPipeline.CreateHotReloadRenderer(uiPath);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static string GetFileName(string uiFile)");
        sb.AppendLine("    {");
        sb.AppendLine("        global::System.ArgumentException.ThrowIfNullOrWhiteSpace(uiFile);");
        sb.AppendLine("        var fileName = global::System.IO.Path.GetFileName(uiFile);");
        sb.AppendLine("        if (string.IsNullOrWhiteSpace(fileName))");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new global::System.InvalidOperationException(\"UI file name is invalid.\");");
        sb.AppendLine("        }");
        sb.AppendLine("        return fileName;\n");
        sb.AppendLine("    }");
        sb.AppendLine("}");
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

    public string Name { get; private set; }
    public IReadOnlyList<string> Args { get; }
    public List<UiDslNode> Children { get; }

    internal void NormalizeName(string name)
    {
        Name = name;
    }
}

internal static class UiDslParser
{
    private static readonly Dictionary<string, string> BeginAliases = new(StringComparer.Ordinal)
    {
        ["Group"] = "BeginGroup",
        ["Child"] = "BeginChild",
        ["Menu"] = "BeginMenu",
        ["MenuBar"] = "BeginMenuBar",
        ["MainMenuBar"] = "BeginMainMenuBar",
        ["Popup"] = "BeginPopup",
        ["PopupModal"] = "BeginPopupModal",
        ["PopupContextItem"] = "BeginPopupContextItem",
        ["PopupContextWindow"] = "BeginPopupContextWindow",
        ["PopupContextVoid"] = "BeginPopupContextVoid",
        ["TabBar"] = "BeginTabBar",
        ["TabItem"] = "BeginTabItem",
        ["Table"] = "BeginTable",
        ["Tooltip"] = "BeginTooltip",
        ["ItemTooltip"] = "BeginItemTooltip",
        ["DragDropSource"] = "BeginDragDropSource",
        ["DragDropTarget"] = "BeginDragDropTarget",
        ["Disabled"] = "BeginDisabled",
        ["MultiSelect"] = "BeginMultiSelect",
    };

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

        NormalizeBeginAliases(roots);
        return new UiDslDocument(roots);
    }

    private static void NormalizeBeginAliases(List<UiDslNode> roots)
    {
        foreach (var node in roots)
        {
            NormalizeNode(node);
        }
    }

    private static void NormalizeNode(UiDslNode node)
    {
        if (TryNormalizeName(node, out var normalized))
        {
            node.NormalizeName(normalized);
        }

        foreach (var child in node.Children)
        {
            NormalizeNode(child);
        }
    }

    private static bool TryNormalizeName(UiDslNode node, out string normalized)
    {
        normalized = node.Name;
        if (normalized.StartsWith("Begin", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized is "Combo")
        {
            if (node.Children.Count > 0)
            {
                normalized = "BeginCombo";
                return true;
            }

            return false;
        }

        if (normalized is "ListBox")
        {
            if (node.Children.Count > 0)
            {
                normalized = "BeginListBox";
                return true;
            }

            return false;
        }

        if (BeginAliases.TryGetValue(normalized, out var alias))
        {
            normalized = alias;
            return true;
        }

        return false;
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
        sb.AppendLine("    public static void Render(Duxel.Core.Dsl.IUiDslEmitter emitter)");
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

