using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal static class DropdownGallerySource
{
    internal static string Array(IEnumerable<string> values) => "[" + string.Join(", ", values.Select(GallerySample.String)) + "]";
    private static string Number(float? value) => value is { } number ? number.ToString("R", CultureInfo.InvariantCulture) + "f" : "null";
    internal static string Options(UiDropdownOptions value) => $"new UiDropdownOptions(Width : {Number(value.Width)},\n"
        + $"    Size : UiDropdownSize.{value.Size}, Appearance : UiInputAppearance.{value.Appearance},\n"
        + $"    Placeholder : {GallerySample.String(value.Placeholder)}, RightToLeft : {GallerySample.Boolean(value.RightToLeft)},\n"
        + $"    MaximumPopupHeight : {Number(value.MaximumPopupHeight)}, Invalid : {GallerySample.Boolean(value.Invalid)},\n"
        + $"    SelectedCountFormat : {GallerySample.String(value.SelectedCountFormat)}, ExpandLabel : {GallerySample.String(value.ExpandLabel)})";
    internal static string Options(UiComboboxOptions value) => $"new UiComboboxOptions(Field : {(value.Field is { } field ? Options(field) : "null")},\n"
        + $"    MaxLength : {value.MaxLength}, Freeform : {GallerySample.Boolean(value.Freeform)}, EmptyText : {GallerySample.String(value.EmptyText)})";
    internal static string Options(UiTagOptions value) => $"new UiTagOptions(Size : UiTagSize.{value.Size},\n"
        + $"    Appearance : UiTagAppearance.{value.Appearance}, Shape : UiTagShape.{value.Shape},\n"
        + $"    RightToLeft : {GallerySample.Boolean(value.RightToLeft)}, DismissLabel : {GallerySample.String(value.DismissLabel)}, Selected : {GallerySample.Boolean(value.Selected)})";

    internal const string IconSetup = """
        using Duxel.Svg;

        using var store = new SvgStore();
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
        using var _icon = new UiSvgIcon(store.Load(Path.Combine(directory, "regular.svg")),
            store.Load(Path.Combine(directory, "filled.svg")), UiSvgIconColorMode.MultiplyForeground);
        """;

    internal static string AvatarSetup(bool inactive) => $$"""
        using Duxel.Svg;

        using var store = new SvgStore();
        var artwork = store.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "artwork.svg"));
        var size = artwork.IntrinsicSize;
        var raster = artwork.Rasterize(checked((int)MathF.Ceiling(size.X)), checked((int)MathF.Ceiling(size.Y)));
        using var _memberAvatar = Fluent.Avatar("디모이", new(Color : UiAvatarColor.Brand,
            Presence : new(), IsInactive : {{GallerySample.Boolean(inactive)}}));
        using var _imageAvatar = Fluent.Avatar("SVG 이미지", new(Presence : new(UiPresenceStatus.Busy),
            IsInactive : {{GallerySample.Boolean(inactive)}}));
        _imageAvatar.Source = new UiImageSource(raster.CopyImageData());
        """;
}
