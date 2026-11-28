using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Duxel.FluentDsl.Generator;

internal abstract record DslNode(Mark Start)
{
    internal DslMap Map() => this as DslMap ?? throw new DslError(Start, "Expected a mapping.");
    internal DslList List() => this as DslList ?? throw new DslError(Start, "Expected a sequence.");
    internal DslValue Scalar() => this as DslValue ?? throw new DslError(Start, "Expected a scalar value.");
}

internal sealed record DslValue(string Text, bool Plain, Mark Start) : DslNode(Start);
internal sealed record DslList(IReadOnlyList<DslNode> Items, Mark Start) : DslNode(Start);
internal sealed record DslMap(IReadOnlyDictionary<string, DslEntry> Items, Mark Start) : DslNode(Start)
{
    internal DslNode Required(string name) => Optional(name)
        ?? throw new DslError(Start, $"Missing '{name}'.");

    internal DslNode? Optional(string name) => Items.TryGetValue(name, out var entry) ? entry.Value : null;

    internal void Allow(params string[] names)
    {
        foreach (var entry in Items.Values)
        {
            if (!names.Contains(entry.Key.Text))
                throw new DslError(entry.Key.Start, $"Unknown property '{entry.Key.Text}'.");
        }
    }
}

internal sealed record DslEntry(DslValue Key, DslNode Value);

internal sealed class DslError(Mark mark, string message) : Exception(message)
{
    internal Mark Mark { get; } = mark;
}

internal static class DslYaml
{
    internal static DslMap Parse(string text)
    {
        var parser = new Parser(new StringReader(text));
        parser.Consume<StreamStart>();
        parser.Consume<DocumentStart>();
        var result = Read(parser, 0).Map();
        parser.Consume<DocumentEnd>();
        parser.Consume<StreamEnd>();
        return result;
    }

    private static DslNode Read(IParser parser, int depth)
    {
        var current = parser.Current!;
        if (depth > 64)
            throw new DslError(current.Start, "The view exceeds the maximum YAML nesting depth.");
        if (current is NodeEvent node && (!node.Anchor.IsEmpty || !node.Tag.IsEmpty) || current is AnchorAlias)
            throw new DslError(current.Start, "YAML anchors, aliases and explicit tags are unsupported.");
        switch (current)
        {
            case Scalar scalar:
                parser.Consume<Scalar>();
                return new DslValue(scalar.Value, scalar.Style == ScalarStyle.Plain, scalar.Start);
            case MappingStart map:
                parser.Consume<MappingStart>();
                var entries = new Dictionary<string, DslEntry>(StringComparer.Ordinal);
                while (!parser.TryConsume<MappingEnd>(out _))
                {
                    var key = Read(parser, depth + 1).Scalar();
                    if (entries.ContainsKey(key.Text))
                        throw new DslError(key.Start, $"Duplicate key '{key.Text}'.");
                    entries.Add(key.Text, new(key, Read(parser, depth + 1)));
                }
                return new DslMap(entries, map.Start);
            case SequenceStart sequence:
                parser.Consume<SequenceStart>();
                var items = new List<DslNode>();
                while (!parser.TryConsume<SequenceEnd>(out _))
                    items.Add(Read(parser, depth + 1));
                return new DslList(items, sequence.Start);
            default:
                throw new DslError(current.Start, "Expected a YAML value.");
        }
    }
}
