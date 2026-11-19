namespace Duxel.Core;

public enum UiAccessibilityTextEditKind
{
    Replace, Copy, Cut, Paste
}

/// <summary>A source-guarded UTF16 range operation. Paste uses an insertion range.</summary>
public sealed class UiAccessibilityTextEdit
{
    public UiAccessibilityTextContent Content
    {
        get;
    }
    public UiAccessibilityTextEditKind Kind
    {
        get;
    }
    public int StartUtf16
    {
        get;
    }
    public int EndUtf16
    {
        get;
    }
    public string? Replacement
    {
        get;
    }
    public UiAccessibilityTextEdit(UiAccessibilityTextContent content, UiAccessibilityTextEditKind kind,
        int startUtf16, int endUtf16, string? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if ((uint)kind > (uint)UiAccessibilityTextEditKind.Paste)
            throw new ArgumentOutOfRangeException(nameof(kind));
        content.GetCharacterOffset(startUtf16);
        content.GetCharacterOffset(endUtf16);
        if (endUtf16 < startUtf16 || kind == UiAccessibilityTextEditKind.Paste && startUtf16 != endUtf16)
            throw new ArgumentException("The edit range is invalid.", nameof(endUtf16));
        if (kind == UiAccessibilityTextEditKind.Replace)
        {
            ArgumentNullException.ThrowIfNull(replacement);
            _ = new UiAccessibilityTextContent(replacement);
        }
        else if (replacement is not null)
            throw new ArgumentException("Only Replace accepts replacement text.", nameof(replacement));
        (Content, Kind, StartUtf16, EndUtf16, Replacement) = (content, kind, startUtf16, endUtf16, replacement);
    }
}

public sealed partial class UiAccessibilityTree
{
    public UiAccessibilityResult RequestTextEdit(string id, long runtimeId, UiAccessibilityTextEdit edit, bool wait = true)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return QueueAction(id, runtimeId, UiAccessibilityAction.EditText, null, null, wait, null, out _, textEdit: edit);
    }
}
