using System.Buffers;
using System.Text;

namespace Duxel.Core;

/// <summary>Immutable text and Unicode scalar offsets shared by published text states.</summary>
public sealed class UiAccessibilityTextContent
{
    private readonly int[]? _utf16Offsets;
    public string Text
    {
        get;
    }
    public int CharacterCount
    {
        get;
    }
    public UiAccessibilityTextContent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        var count = 0;
        for (var offset = 0; offset < text.Length; count++)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(offset), out _, out var consumed) != OperationStatus.Done)
                throw new ArgumentException("Accessible text must contain valid UTF-16.", nameof(text));
            offset += consumed;
        }
        CharacterCount = count;
        if (count == text.Length)
            return;
        _utf16Offsets = new int[count + 1];
        var position = 0;
        for (var index = 0; index < count; index++)
        {
            _utf16Offsets[index] = position;
            Rune.DecodeFromUtf16(text.AsSpan(position), out _, out var consumed);
            position += consumed;
        }
        _utf16Offsets[count] = text.Length;
    }

    public int GetUtf16Offset(int characterOffset)
    {
        if ((uint)characterOffset > (uint)CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(characterOffset));
        return _utf16Offsets is null ? characterOffset : _utf16Offsets[characterOffset];
    }
    public int GetCharacterOffset(int utf16Offset)
    {
        if ((uint)utf16Offset > (uint)Text.Length)
            throw new ArgumentOutOfRangeException(nameof(utf16Offset));
        if (_utf16Offsets is null)
            return utf16Offset;
        var index = Array.BinarySearch(_utf16Offsets, utf16Offset);
        return index >= 0 ? index : throw new ArgumentException("A text offset cannot split a surrogate pair.", nameof(utf16Offset));
    }
    public int GetUnicodeScalar(int characterIndex)
    {
        if ((uint)characterIndex >= (uint)CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(characterIndex));
        return Rune.GetRuneAt(Text, GetUtf16Offset(characterIndex)).Value;
    }
    public string GetText(int startCharacterOffset, int endCharacterOffset)
    {
        var start = GetUtf16Offset(startCharacterOffset);
        var end = GetUtf16Offset(endCharacterOffset);
        if (end < start)
            throw new ArgumentException("Text ranges must be ordered.", nameof(endCharacterOffset));
        return Text.Substring(start, end - start);
    }
}

/// <summary>A published committed-text caret and selection; offsets use UTF-16.</summary>
public sealed record UiAccessibilityTextInfo
{
    public UiAccessibilityTextContent Content
    {
        get;
    }
    public int CaretUtf16
    {
        get;
    }
    public int AnchorUtf16
    {
        get;
    }
    public bool CanSetSelection
    {
        get;
    }
    public bool CanEditText
    {
        get;
    }
    public int CaretCharacterOffset => Content.GetCharacterOffset(CaretUtf16);
    public int AnchorCharacterOffset => Content.GetCharacterOffset(AnchorUtf16);
    public bool HasSelection => CaretUtf16 != AnchorUtf16;
    public int SelectionStartUtf16 => Math.Min(CaretUtf16, AnchorUtf16);
    public int SelectionEndUtf16 => Math.Max(CaretUtf16, AnchorUtf16);
    public UiAccessibilityTextInfo(UiAccessibilityTextContent content, int caretUtf16, int anchorUtf16, bool canSetSelection = false)
        : this(content, caretUtf16, anchorUtf16, canSetSelection, false)
    {
    }
    public UiAccessibilityTextInfo(UiAccessibilityTextContent content, int caretUtf16, int anchorUtf16, bool canSetSelection, bool canEditText)
    {
        ArgumentNullException.ThrowIfNull(content);
        content.GetCharacterOffset(caretUtf16);
        content.GetCharacterOffset(anchorUtf16);
        (Content, CaretUtf16, AnchorUtf16) = (content, caretUtf16, anchorUtf16);
        CanSetSelection = canSetSelection;
        CanEditText = canEditText;
    }
}

public sealed partial class UiAccessibilityTree
{
    /// <summary>Requests positions against the exact content instance of a published text state.</summary>
    public UiAccessibilityResult RequestTextSelection(string id, long runtimeId, UiAccessibilityTextInfo selection, bool wait = true)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return QueueAction(id, runtimeId, UiAccessibilityAction.SetTextSelection, null, null, wait, null, out _, textSelection: selection);
    }
}
