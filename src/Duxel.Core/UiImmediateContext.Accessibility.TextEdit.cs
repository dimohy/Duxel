namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private bool ApplyAccessibilityTextEdit(string id, UiTextEditBuffer buffer, ref string text, ref int caret,
        ref UiTextSelection selection, UiRect bounds, int maxLength, bool readOnly, bool multiline = false)
    {
        if (_accessibility?.Take(id, UiAccessibilityAction.EditText) is not { } request)
            return false;
        void Reject(UiAccessibilityResult result) => request.Result = result;
        if (!IsControlEnabled)
        {
            Reject(UiAccessibilityResult.Disabled);
            return false;
        }
        var visible = IntersectRect(CurrentClipRect, bounds);
        if (visible.Width <= 0 || visible.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            Reject(UiAccessibilityResult.Unavailable);
            return false;
        }
        if (request.TextEdit is not { } edit || edit.Content.Text != text)
        {
            Reject(UiAccessibilityResult.InvalidArgument);
            return false;
        }
        if (edit.Kind != UiAccessibilityTextEditKind.Copy && readOnly)
        {
            Reject(UiAccessibilityResult.ReadOnly);
            return false;
        }
        if (edit.Kind != UiAccessibilityTextEditKind.Copy && _state.ActiveId == id && !string.IsNullOrEmpty(_imeHandler?.GetCompositionText()))
        {
            Reject(UiAccessibilityResult.Busy);
            return false;
        }
        var range = new UiTextSelection(edit.StartUtf16, edit.EndUtf16);
        if (buffer.AlignTextElements(range) != range)
        {
            Reject(UiAccessibilityResult.InvalidArgument);
            return false;
        }
        if (edit.Kind != UiAccessibilityTextEditKind.Replace && _clipboard is null)
        {
            Reject(UiAccessibilityResult.InvalidOperation);
            return false;
        }
        string replacement = edit.Replacement ?? "";
        try
        {
            if (edit.Kind == UiAccessibilityTextEditKind.Paste)
                replacement = _clipboard!.GetText();
            if (UiAccessibilityTree.ContainsControl(replacement, multiline))
            {
                Reject(UiAccessibilityResult.InvalidArgument);
                return false;
            }
            try
            {
                _ = new UiAccessibilityTextContent(replacement);
            }
            catch (ArgumentException) { Reject(UiAccessibilityResult.InvalidArgument); return false; }
            replacement = SanitizeText(replacement, multiline);
            if (edit.Kind != UiAccessibilityTextEditKind.Copy && (long)text.Length - (edit.EndUtf16 - edit.StartUtf16) + replacement.Length > maxLength)
            {
                Reject(UiAccessibilityResult.InvalidArgument);
                return false;
            }
            if (Volatile.Read(ref request.Canceled))
            {
                Reject(UiAccessibilityResult.Timeout);
                return false;
            }
            if (edit.Kind is UiAccessibilityTextEditKind.Copy or UiAccessibilityTextEditKind.Cut)
                _clipboard!.SetText(text[edit.StartUtf16..edit.EndUtf16]);
        }
        catch (TimeoutException) { Reject(UiAccessibilityResult.Timeout); return false; }
        catch (IOException) { Reject(UiAccessibilityResult.Failed); return false; }
        if (Volatile.Read(ref request.Canceled))
        {
            Reject(UiAccessibilityResult.Timeout);
            return false;
        }
        if (edit.Kind == UiAccessibilityTextEditKind.Copy)
        {
            request.Result = UiAccessibilityResult.Success;
            return false;
        }
        var previous = text;
        buffer.Replace(edit.StartUtf16, edit.EndUtf16 - edit.StartUtf16, replacement);
        text = buffer.Text;
        caret = edit.StartUtf16 + replacement.Length;
        selection = new(caret, caret);
        _state.SetCursor(id, caret);
        _state.SetSelection(id, selection);
        ResetCaretBlink();
        request.ExpectedText = text;
        request.Result = UiAccessibilityResult.Success;
        return text != previous;
    }
}
