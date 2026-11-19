namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    /// <summary>Reads text state already produced in this render frame, using the current ID scope.</summary>
    public UiAccessibilityTextInfo? GetAccessibilityTextInfo(string id)
    {
        EnsureNotMeasuring();
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _accessibility is { } tree && tree.TryGetStaged(ResolveId(id), out var node) ? node.TextInfo : null;
    }
    /// <summary>Reads immutable paint metadata already produced for this ID in the current render frame.</summary>
    public UiAccessibilityTextAppearance? GetAccessibilityTextAppearance(string id)
    {
        EnsureNotMeasuring();
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _accessibility is { } tree && tree.TryGetStaged(ResolveId(id), out var node) ? node.TextAppearance : null;
    }
    private UiAccessibilityTextInfo? CaptureAccessibilityText(string id, string text, int caret, int anchor, bool canEditText = true)
    {
        if (_accessibility is not { } tree)
            return null;
        var previous = tree.TryGet(id, out var published) ? published.Node.TextInfo : null;
        var content = previous is not null && string.Equals(previous.Content.Text, text, StringComparison.Ordinal)
            ? previous.Content : new UiAccessibilityTextContent(text);
        return previous is { CanSetSelection: true } && ReferenceEquals(previous.Content, content)
            && previous.CaretUtf16 == caret && previous.AnchorUtf16 == anchor && previous.CanEditText == canEditText
            ? previous : new(content, caret, anchor, canSetSelection: true, canEditText: canEditText);
    }

    private void ApplyAccessibilityTextSelection(string id, UiTextEditBuffer buffer, UiRect bounds, ref int caret,
        ref UiTextSelection selection, UiVisualTextLines? visual = null)
    {
        if (_accessibility?.Take(id, UiAccessibilityAction.SetTextSelection) is not { } request)
            return;
        if (!IsControlEnabled)
        {
            request.Result = UiAccessibilityResult.Disabled;
            return;
        }
        var visible = IntersectRect(CurrentClipRect, bounds);
        if (visible.Width <= 0 || visible.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            request.Result = UiAccessibilityResult.Unavailable;
            return;
        }
        if (request.TextSelection is not { } desired || !string.Equals(desired.Content.Text, buffer.Text, StringComparison.Ordinal))
        {
            request.Result = UiAccessibilityResult.InvalidArgument;
            return;
        }
        var next = new UiTextSelection(desired.AnchorUtf16, desired.CaretUtf16);
        var aligned = visual is null ? buffer.AlignTextElements(next) : visual.AlignSelection(next);
        if (aligned != next)
        {
            request.Result = UiAccessibilityResult.InvalidArgument;
            return;
        }
        caret = desired.CaretUtf16;
        selection = next;
        _state.SetCursor(id, caret);
        _state.SetSelection(id, selection);
        _accessibility.ClearInputTextScroll(id);
        ResetCaretBlink();
        request.Result = UiAccessibilityResult.Success;
    }
}
