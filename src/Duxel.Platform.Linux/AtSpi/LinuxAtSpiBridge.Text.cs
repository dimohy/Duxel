using Duxel.Core;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private static UiAccessibilityTextInfo TextState(Node node) => node.Element?.Node.TextInfo
        ?? throw Invalid("The accessible does not publish text.");

    private static GVariant TextProperty(Node node, string property)
    {
        var text = TextState(node);
        return property switch
        {
            "CharacterCount" => GVariant.Int(text.Content.CharacterCount),
            "CaretOffset" => GVariant.Int(text.CaretCharacterOffset),
            _ => throw Invalid("Unknown text property: " + property)
        };
    }

    private GVariant TextMethod(Node node, string method, GVariant input)
    {
        var text = TextState(node);
        var content = text.Content;
        if (method == "GetNSelections")
            return GVariant.Tuple(GVariant.Int(text.HasSelection ? 1 : 0));
        if (method is "GetDefaultAttributes" or "GetDefaultAttributeSet")
            return GVariant.Tuple(TextAttributeSet(node, true));
        var first = Int(input, 0);
        switch (method)
        {
            case "GetAttributes":
            case "GetAttributeRun":
            case "GetAttributeValue":
                if ((uint)first > (uint)content.CharacterCount)
                    throw Invalid("The text attribute offset is out of bounds.");
                var appearance = TextAppearance(node);
                if (method == "GetAttributeValue")
                {
                    using var attributeName = input.Child(1);
                    return GVariant.Tuple(GVariant.String(FontAttributeValue(appearance, first, attributeName.AsString())));
                }
                var includeDefaults = false;
                if (method == "GetAttributeRun")
                {
                    using var flag = input.Child(1);
                    includeDefaults = flag.AsBool();
                }
                var attributeRange = AttributeRange(appearance, first);
                return GVariant.Tuple(FontAttributeSet(appearance, attributeRange.Attributes, includeDefaults), GVariant.Int(attributeRange.Start), GVariant.Int(attributeRange.End));
            case "GetTextBeforeOffset":
            case "GetTextAtOffset":
            case "GetTextAfterOffset":
                if ((uint)first > (uint)content.CharacterCount)
                    throw Invalid("The text offset is out of bounds.");
                var boundary = UInt(input, 1);
                var relative = method == "GetTextBeforeOffset" ? -1 : method == "GetTextAfterOffset" ? 1 : 0;
                var legacy = boundary switch
                {
                    0 => LinuxTextBoundarySequence.Character(content.CharacterCount, first, relative),
                    >= 1 and <= 4 => TextBoundaries(node).GetLegacyRange(first, boundary, relative),
                    5 or 6 => new LinuxTextBoundarySequence(TextLayout(node), boundary == 6).GetRange(first, relative),
                    _ => throw Invalid("Unknown text boundary type.")
                };
                return GVariant.Tuple(GVariant.String(content.GetText(legacy.Start, legacy.End)), GVariant.Int(legacy.Start), GVariant.Int(legacy.End));
            case "GetStringAtOffset":
                if ((uint)first > (uint)content.CharacterCount)
                    throw Invalid("The text offset is out of bounds.");
                var granularity = UInt(input, 1);
                var range = granularity switch
                {
                    0 => (Start: first, End: first < content.CharacterCount ? first + 1 : first),
                    1 or 2 or 4 => TextBoundaries(node).GetRange(first, granularity),
                    3 => TextLayout(node).GetLineRangeAt(first),
                    _ => throw new ProtocolError("org.freedesktop.DBus.Error.NotSupported", "This text granularity is not implemented.")
                };
                return GVariant.Tuple(GVariant.String(content.GetText(range.Start, range.End)), GVariant.Int(range.Start), GVariant.Int(range.End));
            case "GetCharacterExtents":
                if ((uint)first >= (uint)content.CharacterCount)
                    throw Invalid("The character offset is out of bounds.");
                return TextRectangle(TextBounds(node, TextLayout(node).GetCharacterExtents(first), UInt(input, 1)));
            case "GetRangeExtents":
                var rangeEnd = Int(input, 1);
                if (first < 0 || rangeEnd < first || rangeEnd > content.CharacterCount)
                    throw Invalid("The text range is out of bounds.");
                return TextRectangle(TextBounds(node, TextLayout(node).GetRangeExtents(first, rangeEnd), UInt(input, 2)));
            case "GetBoundedRanges":
                var width = Int(input, 2);
                var height = Int(input, 3);
                var xClip = UInt(input, 5);
                var yClip = UInt(input, 6);
                if (width < 0 || height < 0 || xClip > 3 || yClip > 3)
                    throw Invalid("Invalid text range rectangle or clipping type.");
                var origin = ClientPoint(node, first, Int(input, 1), UInt(input, 4));
                var bounded = TextLayout(node).GetBoundedRanges(new(origin.X, origin.Y, width / _window.ContentScale, height / _window.ContentScale),
                    (UiAccessibilityTextClip)xClip, (UiAccessibilityTextClip)yClip);
                return GVariant.Tuple(GVariant.Array("(iisv)", bounded.Select(value => GVariant.Tuple(
                    GVariant.Int(value.Start), GVariant.Int(value.End), GVariant.String(content.GetText(value.Start, value.End)),
                    GVariant.Box(GVariant.Int(0)))).ToArray()));
            case "ScrollSubstringTo":
            case "ScrollSubstringToPoint":
                var scrollEnd = Int(input, 1);
                if (first < 0 || scrollEnd < first || scrollEnd > content.CharacterCount)
                    return GVariant.Tuple(GVariant.Bool(false));
                UiAccessibilityTextScroll scroll;
                if (method == "ScrollSubstringTo")
                {
                    var placement = UInt(input, 2);
                    if (placement > 6)
                        return GVariant.Tuple(GVariant.Bool(false));
                    scroll = new(content, content.GetUtf16Offset(first), content.GetUtf16Offset(scrollEnd), (UiAccessibilityTextScrollPlacement)placement);
                }
                else
                    scroll = new(content, content.GetUtf16Offset(first), content.GetUtf16Offset(scrollEnd),
                        ClientPoint(node, Int(input, 3), Int(input, 4), UInt(input, 2)));
                var target = node.Element!.Value;
                var accepted = _tree.RequestTextScroll(target.Node.Id, target.RuntimeId, scroll) == UiAccessibilityResult.Success;
                if (accepted)
                    Refresh();
                return GVariant.Tuple(GVariant.Bool(accepted));
            case "GetOffsetAtPoint":
                var client = ClientPoint(node, first, Int(input, 1), UInt(input, 2));
                return GVariant.Tuple(GVariant.Int(TextLayout(node).GetOffsetAtPoint(client.X, client.Y)));
            case "GetText":
                var end = Int(input, 1);
                if (end == -1)
                    end = content.CharacterCount;
                if (first < 0 || end < first || end > content.CharacterCount)
                    throw Invalid("The text range is out of bounds.");
                return GVariant.Tuple(GVariant.String(content.GetText(first, end)));
            case "GetCharacterAtOffset":
                if ((uint)first >= (uint)content.CharacterCount)
                    throw Invalid("The character offset is out of bounds.");
                return GVariant.Tuple(GVariant.Int(content.GetUnicodeScalar(first)));
            case "GetSelection":
                if (first != 0 || !text.HasSelection)
                    throw Invalid("The text selection does not exist.");
                return GVariant.Tuple(GVariant.Int(Math.Min(text.AnchorCharacterOffset, text.CaretCharacterOffset)),
                    GVariant.Int(Math.Max(text.AnchorCharacterOffset, text.CaretCharacterOffset)));
            case "SetCaretOffset":
                return GVariant.Tuple(GVariant.Bool(SetTextSelection(node, text, first, first)));
            case "AddSelection":
                return GVariant.Tuple(GVariant.Bool(!text.HasSelection && SetTextSelection(node, text, first, Int(input, 1))));
            case "SetSelection":
                return GVariant.Tuple(GVariant.Bool(first == 0 && text.HasSelection
                    && SetTextSelection(node, text, Int(input, 1), Int(input, 2))));
            case "RemoveSelection":
                return GVariant.Tuple(GVariant.Bool(first == 0 && text.HasSelection
                    && SetTextSelection(node, text, text.CaretCharacterOffset, text.CaretCharacterOffset)));
            default:
                throw Invalid("Unknown text method: " + method);
        }
    }

    private static LinuxTextBoundaries TextBoundaries(Node node)
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var language = culture.Length == 0 ? "und" : culture;
        return node.TextBoundaries = LinuxTextBoundaries.Retain(node.TextBoundaries, TextState(node).Content, language);
    }

    private static UiAccessibilityTextLayout TextLayout(Node node) => node.Element?.Node.TextLayout
        ?? throw new ProtocolError("org.freedesktop.DBus.Error.NotSupported", "This text does not publish character geometry.");
    private PixelRect TextBounds(Node node, UiRect value, uint coordinates)
    {
        var screen = PixelBounds(ToScreen(value, _window));
        screen = screen with
        {
            Width = value.Width == 0 ? 0 : screen.Width,
            Height = value.Height == 0 ? 0 : screen.Height
        };
        var elementScreen = PixelBounds(Bounds(node, _window));
        var requested = Bounds(node, coordinates);
        return screen with
        {
            X = screen.X - elementScreen.X + requested.X,
            Y = screen.Y - elementScreen.Y + requested.Y
        };
    }
    private static GVariant TextRectangle(PixelRect box) => GVariant.Tuple(GVariant.Int(box.X), GVariant.Int(box.Y), GVariant.Int(box.Width), GVariant.Int(box.Height));

    private bool SetTextSelection(Node node, UiAccessibilityTextInfo current, int anchor, int caret)
    {
        if (!current.CanSetSelection || (uint)anchor > (uint)current.Content.CharacterCount
            || (uint)caret > (uint)current.Content.CharacterCount)
            return false;
        var element = node.Element!.Value;
        var desired = new UiAccessibilityTextInfo(current.Content, current.Content.GetUtf16Offset(caret), current.Content.GetUtf16Offset(anchor));
        if (_tree.RequestTextSelection(element.Node.Id, element.RuntimeId, desired) != UiAccessibilityResult.Success)
            return false;
        Refresh();
        return _nodes.TryGetValue(node.Path, out var updated) && updated.Element?.Node.TextInfo is { } actual
            && ReferenceEquals(actual.Content, current.Content) && actual.CaretUtf16 == desired.CaretUtf16 && actual.AnchorUtf16 == desired.AnchorUtf16;
    }

    private void PublishTextChanges(Node previous, Node current)
    {
        if (previous.Element?.Node.TextInfo is not { } old || current.Element?.Node.TextInfo is not { } next || ReferenceEquals(old, next))
            return;
        if (!ReferenceEquals(old.Content, next.Content) && old.Content.Text != next.Content.Text)
        {
            var start = 0;
            var oldEnd = old.Content.CharacterCount;
            var nextEnd = next.Content.CharacterCount;
            while (start < oldEnd && start < nextEnd && old.Content.GetUnicodeScalar(start) == next.Content.GetUnicodeScalar(start))
                start++;
            while (oldEnd > start && nextEnd > start && old.Content.GetUnicodeScalar(oldEnd - 1) == next.Content.GetUnicodeScalar(nextEnd - 1))
            {
                oldEnd--;
                nextEnd--;
            }
            if (oldEnd > start)
                Event(current.Path, "TextChanged", "delete", start, GVariant.String(old.Content.GetText(start, oldEnd)), oldEnd - start);
            if (nextEnd > start)
                Event(current.Path, "TextChanged", "insert", start, GVariant.String(next.Content.GetText(start, nextEnd)), nextEnd - start);
        }
        if (old.CaretCharacterOffset != next.CaretCharacterOffset)
            Event(current.Path, "TextCaretMoved", "", next.CaretCharacterOffset, GVariant.Int(0));
        if ((old.HasSelection || next.HasSelection) && (old.AnchorCharacterOffset != next.AnchorCharacterOffset || old.CaretCharacterOffset != next.CaretCharacterOffset))
            Event(current.Path, "TextSelectionChanged", "", 0, GVariant.Int(0));
    }
}
