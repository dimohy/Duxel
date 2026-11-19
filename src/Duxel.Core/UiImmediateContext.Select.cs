namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private bool ProcessClosedSelectNavigation(UiOptionSelection selection, UiDropdownOptions options)
    {
        var handled = false;
        var index = selection.FirstSelected();
        foreach (var key in _keyEvents)
        {
            if (key.Key == UiKey.Space && key.Modifiers == 0 && selection.HasSearch(GetTime()))
            {
                // A space inside an active name search is text, not a delayed button activation.
                CancelControlSpace();
                handled = true;
                continue;
            }
            if (!key.IsDown || key.Modifiers != 0) continue;
            var direction = key.Key is UiKey.UpArrow or UiKey.PageUp ? -1 : 1;
            if (key.Key is UiKey.Home or UiKey.End)
            {
                index = key.Key == UiKey.Home ? Array.FindIndex(selection.Options, static item => item.Enabled)
                    : Array.FindLastIndex(selection.Options, static item => item.Enabled);
            }
            else if (key.Key is UiKey.UpArrow or UiKey.DownArrow or UiKey.PageUp or UiKey.PageDown)
            {
                var steps = key.Key is UiKey.PageUp or UiKey.PageDown
                    ? Math.Max(1, (int)((options.MaximumPopupHeight ?? _displaySize.Y * .8f) - 8) / 32) : 1;
                for (var step = 0; step < steps; step++)
                {
                    var next = index < 0 && direction < 0 ? selection.Options.Length - 1 : index + direction;
                    while (next >= 0 && next < selection.Options.Length && !selection.Options[next].Enabled) next += direction;
                    if (next < 0 || next >= selection.Options.Length) break;
                    index = next;
                }
            }
            else continue;
            handled = true;
            selection.ResetSearch();
            if (index >= 0) selection.Select(index, false);
        }
        foreach (var character in _charEvents)
        {
            if (!System.Text.Rune.IsValid(character.CodePoint)) continue;
            var rune = new System.Text.Rune((int)character.CodePoint);
            if (System.Text.Rune.IsControl(rune) || System.Text.Rune.IsWhiteSpace(rune) && !selection.HasSearch(GetTime())) continue;
            index = selection.Search(rune.ToString(), GetTime(), index);
            if (index >= 0) selection.Select(index, false);
            handled = true;
        }
        if (handled) _requestFrame?.Invoke();
        return handled;
    }
}
