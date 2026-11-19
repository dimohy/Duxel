using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class GpuStrokeClassificationTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var shape in new[] {
            "<rect x='8' y='8' width='40' height='32' rx='8'", "<circle cx='32' cy='24' r='16'",
            "<ellipse cx='32' cy='24' rx='24' ry='16'" })
        {
            var scene = SvgManagedScene.Compile(XDocument.Parse($"<svg width='64' height='48'>{shape} fill='red' stroke='blue' stroke-width='2'/></svg>")).CompileGpu();
            var expectedStroke = shape.StartsWith("<ellipse", StringComparison.Ordinal) ? 32 : 128;
            check(((int)scene.Data[1].W & 8) != 0 && (int)scene.Data[7].W == expectedStroke,
                "GPU stroke classification preserves analytic fill and independent stroke geometry: " + shape);
        }
        var capsule = SvgManagedScene.Compile(XDocument.Parse("<svg width='64' height='48'><path d='M8 24H56' fill='none' stroke='blue' stroke-width='8' stroke-linecap='round'/></svg>")).CompileGpu();
        check(((int)capsule.Data[1].W & 8) != 0,
            "GPU stroke classification retains the genuine analytic round-cap line optimization");
    }
}
