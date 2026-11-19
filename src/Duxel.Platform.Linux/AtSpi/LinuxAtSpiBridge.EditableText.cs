using System.Text;
using Duxel.Core;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private GVariant EditableTextMethod(Node node, string method, GVariant input)
    {
        var text = TextState(node);
        if (!text.CanEditText)
            throw Invalid("The accessible does not expose range editing.");
        var start = 0;
        var end = text.Content.CharacterCount;
        var kind = UiAccessibilityTextEditKind.Replace;
        string? replacement = "";
        switch (method)
        {
            case "SetTextContents":
                using (var value = input.Child(0)) replacement = value.AsString();
                break;
            case "InsertText":
                start = end = Int(input, 0);
                using (var value = input.Child(1))
                {
                    if (!TryUtf8Prefix(value.AsString(), Int(input, 2), out replacement))
                        return GVariant.Tuple(GVariant.Bool(false));
                }
                break;
            case "DeleteText":
                start = Int(input, 0); end = Int(input, 1);
                break;
            case "CopyText":
            case "CutText":
                kind = method == "CopyText" ? UiAccessibilityTextEditKind.Copy : UiAccessibilityTextEditKind.Cut;
                start = Int(input, 0); end = Int(input, 1); replacement = null;
                break;
            case "PasteText":
                kind = UiAccessibilityTextEditKind.Paste;
                start = end = Int(input, 0); replacement = null;
                break;
            default:
                throw Invalid("Unknown editable text method: " + method);
        }
        var result = UiAccessibilityResult.InvalidArgument;
        if (start >= 0 && end >= start && end <= text.Content.CharacterCount)
        {
            var element = node.Element!.Value;
            var edit = new UiAccessibilityTextEdit(text.Content, kind, text.Content.GetUtf16Offset(start), text.Content.GetUtf16Offset(end), replacement);
            result = _tree.RequestTextEdit(element.Node.Id, element.RuntimeId, edit);
            Refresh();
        }
        if (method == "CopyText")
        {
            if (result != UiAccessibilityResult.Success)
                throw new ProtocolError("org.freedesktop.DBus.Error.Failed", "Text copy rejected: " + result);
            return GVariant.Tuple();
        }
        return GVariant.Tuple(GVariant.Bool(result == UiAccessibilityResult.Success));
    }

    private static bool TryUtf8Prefix(string text, int byteLength, out string? prefix)
    {
        prefix = null;
        if (byteLength < 0) return false;
        var bytes = 0;
        var utf16 = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > byteLength - bytes)
            {
                if (bytes != byteLength) return false;
                break;
            }
            bytes += rune.Utf8SequenceLength;
            utf16 += rune.Utf16SequenceLength;
        }
        prefix = utf16 == text.Length ? text : text[..utf16];
        return true;
    }
}
