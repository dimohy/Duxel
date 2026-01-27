using System.Text;

namespace Dux.Core.Dsl;

public sealed class UiDslCompiler
{
    public string GenerateCSharp(string @namespace, string className, UiDslDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        ArgumentNullException.ThrowIfNull(document);

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
