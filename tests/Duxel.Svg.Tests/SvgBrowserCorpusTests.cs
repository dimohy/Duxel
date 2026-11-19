using Duxel.Svg;

internal static class SvgBrowserCorpusTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var store = new SvgStore();
        foreach (var name in new[] { "Red_Gallardo", "TheStructorr_Lamborghini_Gallardo" })
        {
            var asset = store.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", name + ".svg"));
            var before = store.Statistics;
            var raster = asset.Rasterize(384, 192);
            check(raster.Pixels.Span.ContainsAnyExcept((byte)0), "Public SVG pipeline renders unmodified complex browser corpus: " + name);
            check(ReferenceEquals(raster, asset.Rasterize(384, 192)), "Complex SVG reuses its immutable raster: " + name);
            asset.Rasterize(768, 384);
            check(store.Statistics.XmlParses == before.XmlParses && store.Statistics.SceneCompiles == before.SceneCompiles,
                "Complex SVG enlargement does not repeat XML or paint template compilation: " + name);
        }
        check(store.Statistics is { XmlParses: 2, SceneCompiles: 2, Rasterizations: 4 }, "Complex browser corpus compiles exactly one retained scene per original");
    }
}
