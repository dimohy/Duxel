using System.Globalization;
using System.Text.Json;

internal static class BrowserReferenceValidationTests
{
    internal static void Run(Action<bool, string> check)
    {
        (string Label, string Mode, double Dpr, double X, double Y, double Width, double Height, bool Accepted)[] cases =
        [
            ("aligned direct SVG", "direct-svg", 1, 0, 0, 764, 372, true),
            ("nominal DPR one", "direct-svg", .9999999739883236, 0, 0, 764, 372, true),
            ("observed legacy fractional width", "direct-svg", 1, 0, 0, 763.9912109375, 372, false),
            ("observed legacy fractional height", "direct-svg", 1, 0, 0, 764, 371.99560546875, false),
            ("shifted left edge", "direct-svg", 1, .25, 0, 764, 372, false),
            ("shifted top edge", "direct-svg", 1, 0, .25, 764, 372, false),
            ("different DPR", "direct-svg", 2, 0, 0, 764, 372, false),
            ("unknown capture path", "unknown", 1, 0, 0, 764, 372, false),
            ("Canvas backing pixels independent of DOM rectangle", "image-canvas", 1, .25, .25, 763.9912109375, 371.99560546875, true),
            ("Canvas still requires DPR one", "image-canvas", 2, 0, 0, 764, 372, false)
        ];
        foreach (var item in cases)
        {
            var json = string.Create(CultureInfo.InvariantCulture,
                $$$$"""{"id":"contract","mode":"{{{{item.Mode}}}}","width":764,"height":372,"conditions":{"dpr":{{{{item.Dpr}}}},"rectangle":{"x":{{{{item.X}}}},"y":{{{{item.Y}}}},"width":{{{{item.Width}}}},"height":{{{{item.Height}}}}}}} """);
            using var document = JsonDocument.Parse(json);
            var accepted = true;
            try
            {
                BrowserReferenceValidation.Validate(document.RootElement);
            }
            catch (InvalidDataException)
            {
                accepted = false;
            }
            check(accepted == item.Accepted, "Browser reference viewport / " + item.Label);
        }
        var rejected = false;
        try
        {
            GpuSvgBrowserTests.LoadReferences();
        }
        catch (InvalidDataException exception) when (exception.Message.StartsWith("BROWSER_REFERENCE_CSS_VIEWPORT_NOT_PIXEL_ALIGNED", StringComparison.Ordinal))
        {
            rejected = true;
        }
        check(rejected, "Browser reference viewport / actual GPU consumer rejects the preserved unaligned direct references before device creation");
        var images = GpuSvgBrowserTests.LoadReferences(imageOnly: true);
        check(images.Length == 2 && images.All(reference => reference.Id.EndsWith(".image-canvas", StringComparison.Ordinal)),
            "Browser reference viewport / actual GPU consumer retains both complete original Canvas references");
    }
}
