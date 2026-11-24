namespace Duxel.Core;

// A selected-token editor shares the ordinary combobox's popup, IME and
// accessibility machinery, but completes an accepted choice as one transaction.
internal sealed class UiComboboxCompletion(UiTagGroupInputState previousGroup)
{
    internal readonly UiTagGroupInputState PreviousGroup = previousGroup;
    internal UiRect Anchor;
    internal string? EditorId;
    internal bool FocusRequested;
    internal UiWidget? SecondaryAction { get; init; }
    internal UiRect SecondaryBounds;
    internal bool SecondaryFocused;
}
