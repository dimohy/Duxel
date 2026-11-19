namespace Duxel.Core;

internal readonly record struct UiMarkdownImageReference(UiSourceTextRange Source, string Destination, string AlternativeText);
internal readonly record struct UiStyledTextImage(UiImage? Image, string AlternativeText);

/// <summary>Owns current document image leases, with one resolver call per distinct destination.</summary>
internal sealed class UiMarkdownImageCache : IDisposable
{
    private readonly Dictionary<string, UiImage?> _resources = new(StringComparer.Ordinal);
    private Func<Uri, UiImageSource?>? _resolver;
    private Uri? _baseUri;
    private long _revision = -1;
    internal UiStyledTextImage[] Images { get; private set; } = [];

    internal bool Configure(Func<Uri, UiImageSource?>? resolver, Uri? baseUri)
    {
        if (_resolver == resolver && _baseUri == baseUri)
            return false;
        Invalidate();
        _resolver = resolver;
        _baseUri = baseUri;
        return true;
    }

    internal void Prepare(UiMarkdownDocument document)
    {
        if (_revision == document.Revision)
            return;
        var needed = new HashSet<string>(StringComparer.Ordinal);
        var images = new UiStyledTextImage[document.Images.Count];
        for (var i = 0; i < images.Length; i++)
        {
            var reference = document.Images[i];
            Uri.TryCreate(reference.Destination, UriKind.RelativeOrAbsolute, out var uri);
            if (uri is { IsAbsoluteUri: false } && _baseUri is { } baseUri)
                uri = new(baseUri, uri);
            var key = uri is { IsAbsoluteUri: true } ? uri.AbsoluteUri : reference.Destination;
            needed.Add(key);
            if (!_resources.TryGetValue(key, out var image))
            {
                UiImageSource? source = null;
                if (_resolver is not null && uri is not null)
                    source = _resolver(uri);
                image = source is null ? null : new(source);
                _resources.Add(key, image);
            }
            images[i] = new(image, reference.AlternativeText);
        }
        foreach (var key in _resources.Keys.Where(key => !needed.Contains(key)).ToArray())
        {
            _resources[key]?.Dispose();
            _resources.Remove(key);
        }
        Images = images;
        _revision = document.Revision;
    }

    internal void Invalidate()
    {
        foreach (var image in _resources.Values)
            image?.Dispose();
        _resources.Clear();
        Images = [];
        _revision = -1;
    }

    public void Dispose()
    {
        Invalidate();
        _resolver = null;
        _baseUri = null;
    }
}

public sealed partial class UiImmediateContext
{
    private static bool IsStyledImage(ReadOnlySpan<char> text, UiStyledTextStyle style) =>
        style.ImageIndex > 0 && text is ['\uFFFC'];

    private UiVector2 StyledImageSize(UiStyledTextImage image, float maximumWidth)
    {
        if (image.Image is { } picture)
        {
            var width = MathF.Min(picture.Source.Width, maximumWidth);
            return new(width, width * picture.Source.Height / picture.Source.Width);
        }
        return new(MathF.Min(maximumWidth, MathF.Max(48, MeasureTextWidthSpan(ImageAlternative(image)) + 16)), 32);
    }

    private static string ImageAlternative(UiStyledTextImage image) => image.AlternativeText.Length == 0
        ? "Image unavailable" : "Image unavailable: " + image.AlternativeText;

    private UiRect StyledImageBounds(UiStyledTextImage image, float maximumWidth, float x, float y, float lineHeight)
    {
        var size = StyledImageSize(image, maximumWidth);
        return new(x, y + (lineHeight - size.Y) / 2, size.X, size.Y);
    }

    private UiRect MarkdownImageBounds(UiVisualTextLines visual, in UiVisualTextImage image, UiVector2 origin)
    {
        if (visual.NativeParagraph is not null)
        {
            var bounds = visual.NativeObjectBounds(image.Source.Start);
            return bounds with
            {
                X = origin.X + bounds.X,
                Y = origin.Y + bounds.Y
            };
        }
        using var typography = BeginStyledTypography(image.Style);
        var line = visual.ImageLine(image.Source.Start);
        if (visual.TryComposedObjectBounds(image.Source.Start, line, out var nativeBounds))
            return nativeBounds with
            {
                X = origin.X + nativeBounds.X,
                Y = origin.Y + nativeBounds.Y
            };
        var maximumWidth = visual.EffectiveWidth;
        var x = visual.X(image.Source.Start, line);
        if (visual.StyledTableRows.TryGetValue(line, out var table))
        {
            maximumWidth = float.PositiveInfinity;
            if (image.Source.Start == table.Source.Source.Start)
                x = table.CellX[0];
        }
        return StyledImageBounds(image.Image, maximumWidth, origin.X + x,
            origin.Y + visual.Y(line), visual.LineHeight(line));
    }

    private void PaintMarkdownImageSelection(UiVisualTextLines visual, UiTextSelection selection,
        UiVector2 origin, UiRect clip, UiColor color, int compositionStart, int compositionLength)
    {
        var (start, end) = GetSelectionRange(selection);
        var tint = new UiColor((color.Rgba & 0x00FFFFFFu) | (Math.Min(color.Rgba >> 24, 96u) << 24));
        var composition = new UiCompositionTextMap(compositionStart, compositionLength);
        for (var index = 0; index < visual.ImageCount; index++)
        {
            if (visual.ProjectedImage(index) is not { } image
                || end <= composition.Source(image.Source.Start) || start >= composition.Source(image.Source.End))
                continue;
            var bounds = MarkdownImageBounds(visual, image, origin);
            if (IntersectRect(clip, bounds) is { Width: > 0, Height: > 0 })
                _builder.AddRectFilled(bounds, tint, _whiteTexture, clip);
        }
    }

    private void CaptureMarkdownImages(string root, in UiMultilineEditResult edit, UiMarkdownLinkOwner? links)
    {
        if (_accessibility is null || (edit.CompositionLines ?? edit.VisualLines) is not { StyledSource: UiMarkdownTextSource source } visual)
            return;
        var content = edit.VisualContent!.Value;
        var widget = _accessibilityWidget;
        _accessibilityWidget = null;
        using var clipped = new UiControlContext(this).Clip(content);
        var linkIndex = 0;
        try
        {
            for (var index = 0; index < source.Document.Images.Count; index++)
            {
                if (visual.ProjectedImage(index) is not { } projected)
                    continue;
                var reference = projected.Source;
                var image = projected.Image;
                if (image.Image is not null && image.AlternativeText.Length == 0)
                    continue;
                var bounds = MarkdownImageBounds(visual, projected, new(content.X - edit.ScrollX, content.Y - edit.ScrollY));
                var parent = root;
                if (links is not null)
                {
                    // The document compiler emits both reference lists in source order.
                    while (linkIndex < links.Entries.Length && links.Entries[linkIndex].Link.Label.End <= reference.Start)
                        linkIndex++;
                    if (linkIndex < links.Entries.Length && links.Entries[linkIndex].Link.Label.Start <= reference.Start
                        && links.Entries[linkIndex].Link.Label.End >= reference.End)
                        parent = links.Entries[linkIndex].Id;
                }
                ExposeAccessibility(root + "##markdown-image/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    parent, UiAccessibilityRole.Image, image.Image is null ? ImageAlternative(image) : image.AlternativeText,
                    bounds, readOnly: true, focusable: false, valuePattern: false, literalLabel: true,
                    imageInfo: image.Image is null ? null : new(bounds, image.AlternativeText));
            }
        }
        finally { _accessibilityWidget = widget; }
    }

    private void PaintStyledImage(UiStyledTextImage image, UiRect bounds, UiRect clip, bool dark)
    {
        var context = new UiControlContext(this);
        using var clipped = context.Clip(IntersectRect(clip, bounds));
        if (image.Image is { } picture)
            PaintImage(picture, bounds, new(0xFFFFFFFF), 0, UiImageFit.Stretch);
        else
        {
            _builder.AddRectFilled(bounds, dark ? new(48, 48, 48) : new(242, 242, 242), _whiteTexture, IntersectRect(clip, bounds));
            PaintInputRun(ImageAlternative(image), new(bounds.X + 8, bounds.Y + 6),
                dark ? new(220, 220, 220) : new(55, 55, 55), IntersectRect(clip, bounds), typography: true);
        }
    }
}
