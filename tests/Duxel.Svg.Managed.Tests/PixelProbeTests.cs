using System.Text.Json;
using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class PixelProbeTests
{
    internal static void Run(Action<bool, string> check)
    {
        var document = XDocument.Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
              <g opacity=".5">
                <path id="triangle" fill="red" d="M0 0L16 0L0 16Z"/>
                <path id="outside" fill="blue" d="M12 12H16V16H12Z"/>
              </g>
            </svg>
            """);
        var scene = SvgManagedScene.Compile(document);
        using var stream = new MemoryStream();
        scene.WritePixelProbe(stream, document, 16, 16, 2, 2);
        using var json = JsonDocument.Parse(stream.ToArray());
        var root = json.RootElement;
        var leaves = root.GetProperty("leaves");
        check(leaves.GetArrayLength() == 1 && leaves[0].GetProperty("id").GetString() == "triangle",
            "Pixel probe / actual source ID excludes leaves outside the selected pixel");
        // The existing shader-color contract stores a half-alpha layer with a downward tie.
        check(root.GetProperty("sceneStraightRgba")[3].GetInt32() == 127
            && leaves[0].GetProperty("isolatedStraightRgba")[3].GetInt32() == 255,
            "Pixel probe / ancestor opacity is applied to the scene but not the isolated leaf");
        check(leaves[0].GetProperty("ancestorEffects").GetArrayLength() == 1,
            "Pixel probe / isolated leaves retain explicit ancestor-effect provenance");
        check(leaves[0].GetProperty("fillSampleMask").GetInt32() == 15
            && leaves[0].GetProperty("fixedPointFillSampleMask").GetInt32() == 15,
            "Pixel probe / known full triangle coverage matches the integer sample-mask calculation");
        using var invalid = new MemoryStream();
        var rejected = false;
        try
        {
            scene.WritePixelProbe(invalid, document, 16, 16, 16, 2);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected = true;
        }
        check(rejected && invalid.Length == 0,
            "Pixel probe / out-of-range coordinates reject before rendering or writing output");
        using var emptyCoverage = new MemoryStream();
        scene.WritePixelProbe(emptyCoverage, document, 16, 16, 14, 14);
        using var emptyJson = JsonDocument.Parse(emptyCoverage.ToArray());
        var triangle = emptyJson.RootElement.GetProperty("leaves").EnumerateArray()
            .Single(leaf => leaf.GetProperty("id").GetString() == "triangle");
        check(!triangle.GetProperty("contributes").GetBoolean()
            && triangle.GetProperty("fillSampleMask").GetInt32() == 0,
            "Pixel probe / zero-coverage bounding candidates remain visible to diagnose missed browser samples");
        var expanded = XDocument.Parse("""
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
              <defs><path id="shape" d="M0 0H16V16H0Z"/></defs><use href="#shape"/>
            </svg>
            """);
        using var expandedOutput = new MemoryStream();
        rejected = false;
        try
        {
            SvgManagedScene.Compile(expanded).WritePixelProbe(expandedOutput, expanded, 16, 16, 2, 2);
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        check(rejected, "Pixel probe / expanded use provenance rejects explicitly instead of attributing a wrong source leaf");
    }
}
