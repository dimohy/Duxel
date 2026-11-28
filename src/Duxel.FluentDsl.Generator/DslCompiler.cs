using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using YamlDotNet.Core;

namespace Duxel.FluentDsl.Generator;

internal enum DslType { String, Bool, Int, Float }

internal sealed record DslScreen(string Name, Mark Location, string Expression,
    IReadOnlyList<DslState> States, IReadOnlyList<string> Actions);

internal sealed record DslState(string Name, DslType Type, string Initial)
{
    internal string TypeName => Type.ToString().ToLowerInvariant();
}

internal sealed class DslCompiler(IReadOnlyCollection<string> typography)
{
    private static readonly string[] Widgets =
        [
            "Dux.Column", "Dux.Row", "Dux.Padding", "Dux.Wrap", "Dux.UniformGrid", "Dux.SizedBox",
            "Fluent.Text", "Fluent.Input", "Fluent.Textarea", "Fluent.Switch", "Fluent.Checkbox", "Fluent.Button",
            "Fluent.Slider", "Fluent.ProgressBar", "Fluent.Badge", "Fluent.Divider", "Fluent.Card"
        ];

    private readonly Dictionary<string, StateUse> _states = new(StringComparer.Ordinal);
    private readonly HashSet<string> _actions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _imports = new(StringComparer.Ordinal);

    internal DslScreen Compile(DslMap document)
    {
        document.Allow("schema", "screen", "using", "state", "view");
        var schema = document.Required("schema").Scalar();
        if (!schema.Plain || schema.Text != "1")
            throw new DslError(schema.Start, "The supported schema version is 1.");
        var screen = document.Required("screen").Scalar();
        var name = Identifier(screen);
        if (document.Optional("using") is { } imports)
        {
            foreach (var node in imports.List().Items)
            {
                var group = node.Scalar();
                if (group.Text is not ("Fluent" or "Dux"))
                    throw new DslError(group.Start, $"Unknown widget group '{group.Text}'.");
                if (!_imports.Add(group.Text))
                    throw new DslError(group.Start, $"Duplicate widget group '{group.Text}'.");
            }
        }
        if (document.Optional("state") is { } declarations)
        {
            foreach (var entry in declarations.Map().Items.Values)
                DeclareState(entry);
        }

        var expression = CompileWidget(document.Required("view").Map());
        var states = new List<DslState>(_states.Count);
        foreach (var entry in _states.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var (nameKey, use) = (entry.Key, entry.Value);
            if (!use.Introduced)
                throw new DslError(use.Location, $"Unknown state '{nameKey}'. Declare it in 'state' or introduce it with 'bind'.");
            if (nameKey == name + "State")
                throw new DslError(use.Location, $"State '{nameKey}' matches the name of its generated state type.");
            states.Add(new(nameKey, use.Type, use.Initial ?? DefaultValue(use.Type)));
        }
        return new(name, screen.Start, expression, states, _actions.OrderBy(action => action, StringComparer.Ordinal).ToArray());
    }

    private void DeclareState(DslEntry entry)
    {
        var name = Identifier(entry.Key);
        DslValue initial;
        DslType type;
        if (entry.Value is DslMap declaration)
        {
            declaration.Allow("type", "initial");
            var typeNode = declaration.Required("type").Scalar();
            type = typeNode.Text switch
            {
                "string" => DslType.String,
                "bool" => DslType.Bool,
                "int" => DslType.Int,
                "float" => DslType.Float,
                _ => throw new DslError(typeNode.Start, $"Unsupported state type '{typeNode.Text}'.")
            };
            initial = declaration.Required("initial").Scalar();
        }
        else
        {
            initial = entry.Value.Scalar();
            type = initial.Plain && initial.Text is "true" or "false" ? DslType.Bool
                : initial.Plain && int.TryParse(initial.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
                    ? DslType.Int : DslType.String;
        }
        var use = UseState(entry.Key, type, introduce : true);
        use.Initial = type switch
        {
            DslType.String => Literal(initial.Text),
            DslType.Bool when initial.Plain && initial.Text is "true" or "false" => initial.Text,
            DslType.Int when initial.Plain && int.TryParse(initial.Text, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value) => value.ToString(CultureInfo.InvariantCulture),
            DslType.Float => FloatLiteral(FiniteNumber(initial)),
            _ => throw new DslError(initial.Start, $"Initial value does not match the type of '{name}'.")
        };
    }

    private string CompileWidget(DslMap widget)
    {
        if (widget.Items.Count != 1)
            throw new DslError(widget.Start, "A widget must have exactly one widget name.");
        var entry = widget.Items.Values.Single();
        var name = ResolveWidget(entry.Key);
        var properties = entry.Value.Map();
        switch (name)
        {
            case "Dux.Column":
            case "Dux.Row":
                properties.Allow("spacing", "children");
                var spacing = Number(properties.Optional("spacing"), "0f");
                return Factory(name, $"new global::Duxel.Core.UiFlexOptions(Spacing : {spacing}), {Children(properties)}");
            case "Dux.Padding":
                properties.Allow("all", "child");
                var padding = Number(properties.Required("all"), "0f");
                return Factory(name, $"{Child(properties)}, new global::Duxel.Core.UiEdgeInsets({padding})");
            case "Dux.Wrap":
                properties.Allow("spacing", "runSpacing", "children");
                return Factory(name, $"new global::Duxel.Core.UiWrapOptions("
                    + $"{Number(properties.Optional("spacing"), "0f")}, {Number(properties.Optional("runSpacing"), "0f")}), {Children(properties)}");
            case "Dux.UniformGrid":
                properties.Allow("columns", "spacing", "rowHeight", "children");
                var columns = PositiveInteger(properties.Required("columns"), "columns");
                var gap = Number(properties.Optional("spacing"), "0f");
                var height = Number(properties.Optional("rowHeight"), "null");
                return Factory(name, $"new global::Duxel.Core.UiUniformGridOptions({columns}, {gap}, {gap}, "
                    + $"MainAxisExtent : {height}), {Children(properties)}");
            case "Dux.SizedBox":
                properties.Allow("width", "height", "child");
                return Factory(name, $"{Child(properties)}, {Number(properties.Optional("width"), "null")}, "
                    + Number(properties.Optional("height"), "null"));
            case "Fluent.Text":
                properties.Allow("style", "text");
                var style = properties.Optional("style")?.Scalar();
                var styleName = style?.Text ?? "Body1";
                if (!typography.Contains(styleName))
                    throw new DslError(style?.Start ?? entry.Key.Start, $"Unknown Fluent text style '{styleName}'.");
                return Factory(name, $"global::Duxel.Core.Text.{styleName}, {Text(properties.Required("text"))}");
            case "Fluent.Input":
                properties.Allow("label", "value", "maxLength", "width");
                return Factory(name, TextInput(properties));
            case "Fluent.Textarea":
                properties.Allow("label", "value", "maxLength", "width", "height");
                return Factory(name, $"{TextInput(properties)}, {Number(properties.Optional("height"), "0f")}");
            case "Fluent.Switch":
            case "Fluent.Checkbox":
                properties.Allow("label", "value");
                return Factory(name, $"{Label(properties)}, {Binding(properties, DslType.Bool)}");
            case "Fluent.Button":
                properties.Allow("label", "onClick");
                var action = Identifier(properties.Required("onClick").Scalar());
                _actions.Add(action);
                return Factory(name, $"{Label(properties)}, () => {ActionParameter(action)}(state)");
            case "Fluent.Slider":
                properties.Allow("label", "value", "minimum", "maximum", "step");
                var minimum = FiniteNumber(properties.Required("minimum"));
                var maximumNode = properties.Required("maximum");
                var maximum = FiniteNumber(maximumNode);
                if (maximum <= minimum || float.IsInfinity(maximum - minimum))
                    throw new DslError(maximumNode.Start, "'maximum' must be greater than 'minimum' and the range must be finite.");
                return Factory(name, $"{Label(properties)}, {Binding(properties, DslType.Float)}, "
                    + $"{FloatLiteral(minimum)}, {FloatLiteral(maximum)}, {PositiveNumber(properties.Optional("step"), "null", "step")}");
            case "Fluent.ProgressBar":
                properties.Allow("value", "maximum", "height");
                var max = PositiveNumber(properties.Optional("maximum"), "100f", "maximum");
                var progress = properties.Required("value");
                var bar = Factory(name, progress is DslValue
                    ? $"{Number(progress, "0f")}, {max}"
                    : $"() => new global::Duxel.Core.UiProgressValue({ReadValue(progress, DslType.Float)}, {max})");
                // Getter-based bars need a tight height to measure without reading application state.
                var barHeight = PositiveNumber(properties.Optional("height"), "2f", "height");
                return Factory("Dux.SizedBox", $"{bar}, height : {barHeight}");
            case "Fluent.Badge":
                properties.Allow("text");
                return Factory(name, Text(properties.Required("text")));
            case "Fluent.Divider":
                properties.Allow("text");
                return Factory(name, properties.Optional("text") is { } caption ? Text(caption) : Literal(""));
            case "Fluent.Card":
                properties.Allow("title", "child");
                return Factory(name, $"{Literal(properties.Required("title").Scalar().Text)}, {Child(properties)}");
            default:
                throw new InvalidOperationException($"The widget '{name}' has no compiler rule.");
        }
    }

    private string ResolveWidget(DslValue node)
    {
        if (node.Text.Contains("."))
        {
            if (Widgets.Contains(node.Text))
                return node.Text;
            throw new DslError(node.Start, $"Unknown widget '{node.Text}'.");
        }

        var matches = _imports.Select(group => $"{group}.{node.Text}").Where(Widgets.Contains)
            .OrderBy(candidate => candidate, StringComparer.Ordinal).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new DslError(node.Start, $"Unknown widget '{node.Text}'. Add its group to 'using' or use a qualified widget name."),
            _ => throw new DslError(node.Start, $"Ambiguous widget '{node.Text}'. Use {string.Join(" or ", matches)}.")
        };
    }

    private string Text(DslNode node)
    {
        if (node is DslValue literal)
            return Literal(literal.Text);
        return $"() => {ReadValue(node, DslType.String)}";
    }

    private string ReadValue(DslNode node, DslType type)
    {
        var map = node.Map();
        map.Allow("read");
        var reference = map.Required("read").Scalar();
        UseState(reference, type, introduce : false);
        return $"state.{reference.Text}.Value";
    }

    private string Binding(DslMap properties, DslType type)
    {
        var mapping = properties.Required("value").Map();
        mapping.Allow("bind");
        var reference = mapping.Required("bind").Scalar();
        UseState(reference, type, introduce : true);
        return $"state.{reference.Text}.Binding";
    }

    private StateUse UseState(DslValue source, DslType type, bool introduce)
    {
        var name = Identifier(source);
        if (!_states.TryGetValue(name, out var use))
        {
            use = new(type, source.Start);
            _states.Add(name, use);
        }
        else if (use.Type != type)
        {
            throw new DslError(source.Start, $"State '{name}' is used as {type}, but line {use.Location.Line} "
                + $"column {use.Location.Column} requires {use.Type}.");
        }
        use.Introduced |= introduce;
        return use;
    }

    private static string Label(DslMap properties) => Literal(properties.Required("label").Scalar().Text);
    private string Child(DslMap properties) => CompileWidget(properties.Required("child").Map());
    private string Children(DslMap properties)
    {
        var children = properties.Required("children").List().Items.Select(child => CompileWidget(child.Map()));
        return $"new global::Duxel.Core.UiWidget[] {{ {string.Join(", ", children)} }}";
    }

    private string TextInput(DslMap properties)
    {
        var length = properties.Optional("maxLength");
        var limit = length is null ? "4096" : PositiveInteger(length, "maxLength");
        return $"{Label(properties)}, {Binding(properties, DslType.String)}, {limit}, {Number(properties.Optional("width"), "0f")}";
    }

    private static string Factory(string name, string arguments) => $"global::Duxel.Core.{name}({arguments})";
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote : true);
    internal static string ActionParameter(string name) => "on" + name;

    private static string Identifier(DslValue value)
    {
        if (!SyntaxFacts.IsValidIdentifier(value.Text) || SyntaxFacts.GetKeywordKind(value.Text) != SyntaxKind.None
            || value.Text.StartsWith("@", StringComparison.Ordinal))
            throw new DslError(value.Start, $"'{value.Text}' is not a usable C# identifier.");
        return value.Text;
    }

    private static string Number(DslNode? node, string fallback)
    {
        if (node is null)
            return fallback;
        var value = FiniteNumber(node);
        if (value < 0)
            throw new DslError(node.Start, "Expected a non-negative finite number.");
        return FloatLiteral(value);
    }

    private static string PositiveNumber(DslNode? node, string fallback, string property)
    {
        if (node is null)
            return fallback;
        var value = FiniteNumber(node);
        if (value <= 0)
            throw new DslError(node.Start, $"'{property}' must be a positive number.");
        return FloatLiteral(value);
    }

    private static string PositiveInteger(DslNode node, string property)
    {
        var scalar = node.Scalar();
        if (!scalar.Plain || !int.TryParse(scalar.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1)
            throw new DslError(scalar.Start, $"'{property}' must be a positive integer.");
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static float FiniteNumber(DslNode node)
    {
        var scalar = node.Scalar();
        if (!scalar.Plain || !float.TryParse(scalar.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || float.IsNaN(value) || float.IsInfinity(value))
            throw new DslError(scalar.Start, "Expected a finite number.");
        return value;
    }

    private static string FloatLiteral(float value) => value.ToString("R", CultureInfo.InvariantCulture) + "f";

    private static string DefaultValue(DslType type) => type switch
    {
        DslType.String => "\"\"",
        DslType.Bool => "false",
        DslType.Int => "0",
        DslType.Float => "0f",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private sealed class StateUse(DslType type, Mark location)
    {
        internal DslType Type { get; } = type;
        internal Mark Location { get; } = location;
        internal bool Introduced { get; set; }
        internal string? Initial { get; set; }
    }
}
