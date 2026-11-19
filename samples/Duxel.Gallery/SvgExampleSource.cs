using Duxel.Core;

namespace Duxel.Gallery;

internal static class SvgExampleSource
{
    internal static readonly string[] AssetNames = ["artwork.svg", "regular.svg", "filled.svg", "text.svg", "features.svg"];
    private static readonly string AssetSetup = "string[] _paths = [\n"
        + string.Join(",\n", AssetNames.Select(file => $"    Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Svg\", {GallerySample.String(file)})")) + "];\n";
    internal static string Factory(string name, string body, bool images = false, bool icons = false,
        string? setup = null, string declarations = "")
    {
        const string svgImport = "using Duxel.Svg;\n\n";
        if (body.StartsWith(svgImport, StringComparison.Ordinal)) body = body[svgImport.Length..];
        var tuple = icons ? "SvgStore Resources, UiSvgIcon Icon, UiWidget Preview" : "SvgStore Resources, UiWidget Preview";
        var header = $"static ({tuple}) {name}(SvgFontOptions svgFonts"
            + (images ? ", IUiImageDecoder imageDecoder" : string.Empty) + ")\n{\n";
        var resources = "var _store = new SvgStore(fonts : svgFonts);\n";
        if (images) resources += "var _decoder = new SvgImageDecoder(_store, imageDecoder);\n";
        var lifetime = icons ? "// Dispose Preview, then Icon, then Resources." : "// Dispose Preview first, then Resources.";
        return svgImport + header
            + string.Join("\n", (resources + (setup ?? AssetSetup) + "\n" + body).Split('\n').Select(line => "    " + line))
            + "\n}\n\n" + lifetime + (declarations.Length == 0 ? string.Empty : "\n\n" + declarations);
    }
    internal static string Transform(string name, float zoom, float rotation, UiEdgeInsets margin, float? radius)
        => $"{name}.Zoom = {GallerySample.Number(zoom)};\n{name}.RotationDegrees = {GallerySample.Number(rotation)};\n"
            + $"{name}.Margin = new({GallerySample.Number(margin.Left)}, {GallerySample.Number(margin.Top)}, {GallerySample.Number(margin.Right)}, {GallerySample.Number(margin.Bottom)});\n"
            + (radius is { } value ? $"{name}.CornerRadius = {GallerySample.Number(value)};\n" : string.Empty);
}
