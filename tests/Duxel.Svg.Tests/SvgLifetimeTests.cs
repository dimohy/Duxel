using System.Runtime.CompilerServices;
using Duxel.Svg;
using Duxel.Svg.Managed;

internal static class SvgLifetimeTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var path = Path.Combine(output, "lifetime.svg");
        File.WriteAllText(path, "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='24'><path fill='#20c060' d='M0 0H32V24H0Z'/></svg>");
        var store = new SvgStore();
        var asset = store.Load(path);
        var scene = asset.Scene;
        var picture = scene;
        var owner = ObserveOwner(asset);
        store.Dispose();
        check(picture.IsDisposed, "SVG store disposal releases its compiled renderer");
        check(CollectOwner(owner), "a retained borrowed SVG asset cannot root the disposed backend owner");
        GC.KeepAlive(scene);
        GC.KeepAlive(asset);
        GC.KeepAlive(store);

        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        using var textStore = new SvgStore(fonts : new("UI", [new("UI", fontPath)]));
        const string text = "<svg xmlns='http://www.w3.org/2000/svg' width='120' height='40'><text x='4' y='30' font-size='24' fill='#20c060'>ABC</text></svg>";
        File.WriteAllText(path, text);
        var textAsset = textStore.Load(path);
        var initialRaster = textAsset.Rasterize(120, 40);
        var snapshot = initialRaster.Pixels.ToArray();
        var resource = textStore.FontCatalog!.Resources.Single();
        var closed = true;
        for (var revision = 0; revision < 32; revision++)
        {
            var retired = textAsset.Scene;
            File.WriteAllText(path, text.Replace("#20c060", revision % 2 == 0 ? "#e03020" : "#20c060", StringComparison.Ordinal));
            textStore.Reload(path);
            closed &= retired.IsDisposed && !textAsset.Scene.IsDisposed;
            textAsset.Rasterize(120, 40);
        }
        check(closed && textAsset.Revision == 32 && textStore.FontStatistics.LoadedFaces == 1,
            "thirty-two SVG text reloads close every retired compiled scene while retaining the registered font");
        check(initialRaster.Pixels.Span.SequenceEqual(snapshot),
            "retiring SVG text scenes preserves already published immutable raster snapshots");
        var committed = textAsset.Scene;
        File.WriteAllText(path, text.Replace("<text ", "<text font-family='Missing' ", StringComparison.Ordinal));
        var failed = false;
        try { textStore.Reload(path); } catch (InvalidDataException) { failed = true; }
        check(failed && ReferenceEquals(textAsset.Scene, committed) && !committed.IsDisposed &&
            !resource.IsDisposed,
            "failed SVG text reload keeps the committed renderer and registered native font handles alive");
        var textPicture = committed;
        var textOwner = ObserveOwner(textAsset);
        textStore.Dispose();
        textStore.Dispose();
        check(textPicture.IsDisposed && resource.IsDisposed &&
            textStore.FontStatistics.LoadedFaces == 0 && textStore.Statistics.CachedRasterBytes == 0,
            "idempotent SVG store disposal closes compiled scene and registered font handles and clears owned residency");
        check(CollectOwner(textOwner), "retained SVG text assets cannot root disposed backend font and render caches");
        GC.KeepAlive(committed);
        GC.KeepAlive(textAsset);
        GC.KeepAlive(textStore);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<SvgManagedScene> ObserveOwner(SvgAsset asset) => new(asset.Scene.Renderer);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CollectOwner(WeakReference<SvgManagedScene> owner)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return !owner.TryGetTarget(out _);
    }
}
