namespace Duxel.Core;

internal readonly record struct UiVisualTextImage(UiSourceTextRange Source, UiStyledTextStyle Style, UiStyledTextImage Image);

internal sealed partial class UiVisualTextLines
{
    internal int ImageCount => _styledSource is UiMarkdownTextSource source ? source.Document.Images.Count : 0;

    internal UiVisualTextImage? ProjectedImage(int index)
    {
        if (_styledSource is not UiMarkdownTextSource source)
            return null;
        var reference = source.Document.Images[index];
        var projection = source.Projection;
        var start = projection.SourceToDisplay[reference.Source.Start];
        if (projection.SourceToDisplay[reference.Source.End] - start != 1 || projection.Text[start] != '\uFFFC')
            return null;
        var style = projection.StyleAt(start);
        return style.ImageIndex == index + 1 ? new(reference.Source, style, projection.Images[index]) : null;
    }

    internal int ImageLine(int sourceIndex)
    {
        var low = 0;
        var high = Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_lines[middle].End <= sourceIndex)
                low = middle + 1;
            else
                high = middle;
        }
        if (low == Count)
            throw new InvalidOperationException("A projected image must belong to a rendered source row.");
        return low;
    }

}
