namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    internal void SetOrientation(string id, UiAccessibilityOrientation orientation)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                Orientation = orientation
            };
    }
}
