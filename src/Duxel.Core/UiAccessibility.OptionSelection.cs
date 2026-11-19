namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    internal void SetSelectionPolicy(string id, bool multiple, bool required = false)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                CanSelectMultiple = multiple,
                SelectionRequired = required
            };
    }
    internal void SetControlledElement(string id, string target)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                ControlledElementId = target
            };
    }
    internal void SetControlValue(string id, string value, UiAccessibilityTextInfo? textInfo = null, UiAccessibilityTextLayout? textLayout = null)
    {
        if (textInfo is not null && !string.Equals(value, textInfo.Content.Text, StringComparison.Ordinal))
            throw new InvalidOperationException("The staged text state must match its value projection.");
        if (textLayout is not null && !ReferenceEquals(textLayout.Content, textInfo?.Content))
            throw new InvalidOperationException("The staged text layout must match its content instance.");
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                Value = value,
                TextInfo = textInfo,
                TextLayout = textLayout,
                TextAppearance = ReferenceEquals(entry.Staged.TextAppearance?.Content, textInfo?.Content) ? entry.Staged.TextAppearance : null
            };
    }
}
