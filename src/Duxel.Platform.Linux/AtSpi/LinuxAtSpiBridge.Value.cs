using Duxel.Core;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private static GVariant RangeProperty(Node node, string property)
    {
        if (node.Element?.Node.Range is not { } range)
            throw Invalid("The accessible does not support numeric values.");
        return property switch
        {
            "MinimumValue" => GVariant.Double(range.Minimum),
            "MaximumValue" => GVariant.Double(range.Maximum),
            "MinimumIncrement" => GVariant.Double(range.SmallChange),
            "CurrentValue" => GVariant.Double(range.Value),
            "Text" => GVariant.String(node.Element.Value.Node.Value ?? ""),
            _ => throw Invalid("Unknown numeric value property: " + property)
        };
    }

    private void SetRangeValue(Node node, double value)
    {
        if (node.Element is not { } element || !element.Node.Range.HasValue)
            throw Invalid("The accessible does not support numeric values.");
        var result = _tree.RequestRangeValue(element.Node.Id, element.RuntimeId, value);
        if (result != UiAccessibilityResult.Success)
            throw new ProtocolError("org.freedesktop.DBus.Error.Failed", "Numeric value request rejected: " + result);
        Refresh();
    }

    private void PublishRangeChanges(Node previous, Node current)
    {
        if (previous.Element?.Node.Range is not { } old || current.Element?.Node.Range is not { } next)
            return;
        List<GVariant> changed = [];
        void Add(string name, GVariant value) => changed.Add(GVariant.DictEntry(GVariant.String(name), GVariant.Box(value)));
        if (old.Value != next.Value)
        {
            Event(current.Path, "PropertyChange", "accessible-value", 0, GVariant.Double(next.Value));
            Add("CurrentValue", GVariant.Double(next.Value));
        }
        if (old.Minimum != next.Minimum) Add("MinimumValue", GVariant.Double(next.Minimum));
        if (old.Maximum != next.Maximum) Add("MaximumValue", GVariant.Double(next.Maximum));
        if (old.SmallChange != next.SmallChange) Add("MinimumIncrement", GVariant.Double(next.SmallChange));
        if (previous.Element?.Node.Value != current.Element?.Node.Value) Add("Text", GVariant.String(current.Element?.Node.Value ?? ""));
        if (changed.Count == 0)
            return;
        using var parameters = GVariant.Tuple(GVariant.String(ValueInterface), GVariant.Array("{sv}", changed.ToArray()), Strings([]));
        Emit(current.Path, "org.freedesktop.DBus.Properties", "PropertiesChanged", parameters);
    }
}
