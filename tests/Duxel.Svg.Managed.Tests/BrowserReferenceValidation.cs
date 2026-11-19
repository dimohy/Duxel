using System.Text.Json;

internal static class BrowserReferenceValidation
{
    internal static void Validate(JsonElement reference)
    {
        var id = reference.GetProperty("id").GetString();
        var conditions = reference.GetProperty("conditions");
        var dpr = conditions.GetProperty("dpr").GetDouble();
        if (!double.IsFinite(dpr) || Math.Abs(dpr - 1) > .00001)
            throw new InvalidDataException("BROWSER_REFERENCE_DPR_NOT_ONE : " + id);
        var mode = reference.GetProperty("mode").GetString();
        if (mode == "image-canvas")
            return;
        if (mode != "direct-svg")
            throw new InvalidDataException("BROWSER_REFERENCE_UNKNOWN_MODE : " + id);
        var rectangle = conditions.GetProperty("rectangle");
        const double tolerance = 1d / 65536;
        var x = rectangle.GetProperty("x").GetDouble();
        var y = rectangle.GetProperty("y").GetDouble();
        var width = rectangle.GetProperty("width").GetDouble();
        var height = rectangle.GetProperty("height").GetDouble();
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height)
            || Math.Abs(x) > tolerance || Math.Abs(y) > tolerance
            || Math.Abs(width - reference.GetProperty("width").GetInt32()) > tolerance
            || Math.Abs(height - reference.GetProperty("height").GetInt32()) > tolerance)
            throw new InvalidDataException("BROWSER_REFERENCE_CSS_VIEWPORT_NOT_PIXEL_ALIGNED : " + id);
    }
}
