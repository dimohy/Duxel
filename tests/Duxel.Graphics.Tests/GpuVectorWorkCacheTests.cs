using Duxel.Core;
using Duxel.Svg;
using Duxel.Vulkan;

internal static class GpuVectorWorkCacheTests
{
    internal static void Run(string output, UiContext context, VulkanRendererBackend renderer,
        Func<Action<UiImmediateContext>, UiDrawData> build, Action<bool, string> check, int frameSlots)
    {
        using var store = new SvgStore();
        var scene = store.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "Browser", "Red_Gallardo.svg")).GpuScene;
        var texture = new UiTextureId(2_300_000_012);
        UiDrawData Build(int pan) => build(ui =>
        {
            context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture,
                UiTextureFormat.Rgba8Unorm, 64, 64, ReadOnlyMemory<byte>.Empty)
            {
                VectorScene = scene,
                VectorCamera = new(new(3056, 1488), new(1496 + pan, 712)),
                IsColorImage = true
            });
            ui.GetWindowDrawList().AddImage(texture, new(16, 16), new(80, 80), default, new(1, 1), new(255, 255, 255));
        });
        byte[] first;
        using (var data = Build(0))
            first = renderer.RenderVectorAndReadback(data, texture, 64, 64);
        for (var frame = 0; frame < frameSlots; frame++)
        {
            using var data = Build((frame + 1) & 1);
            check(renderer.TryRenderDrawData(data), "Vector work cache submits each early camera frame : " + frame);
        }
        check(renderer.VectorWorkDiagnostics.Reuses == 0,
            "Vector buffers retired in the current slot are not reused before its q+N fence deadline");
        for (var frame = 0; frame < 32; frame++)
        {
            using var data = Build(frame & 1);
            check(renderer.TryRenderDrawData(data), "Vector work cache warmup presents : " + frame);
        }
        var warm = renderer.VectorWorkDiagnostics;
        for (var frame = 0; frame < 16; frame++)
        {
            using var data = Build(frame & 1);
            check(renderer.TryRenderDrawData(data), "Vector work cache steady camera presents : " + frame);
        }
        var steady = renderer.VectorWorkDiagnostics;
        check(steady.Allocations == warm.Allocations && steady.Reuses > warm.Reuses,
            "Warmed camera movement reuses completed native vector buffers without new work allocations");
        check(steady.CachedBytes <= 64 * 1024 * 1024 && steady.CachedCount <= 256,
            "Completed vector workspace cache stays within its byte and entry budgets");
        byte[] replay;
        using (var data = Build(0))
            replay = renderer.RenderVectorAndReadback(data, texture, 64, 64);
        check(first.AsSpan().SequenceEqual(replay),
            "Recycled stroke, dependency and pixel work buffers preserve every original raw RGBA byte");
        File.WriteAllBytes(Path.Combine(output, "first.rgba"), first);
        File.WriteAllBytes(Path.Combine(output, "recycled.rgba"), replay);
        File.WriteAllLines(Path.Combine(output, "cache.txt"),
        [
            "Frame slots : " + frameSlots,
            "Warm allocations/reuses : " + warm.Allocations + " / " + warm.Reuses,
            "Steady allocations/reuses : " + steady.Allocations + " / " + steady.Reuses,
            "Cached/peak bytes : " + steady.CachedBytes + " / " + steady.PeakCachedBytes,
            "Cached/pending count : " + steady.CachedCount + " / " + steady.PendingCount
        ]);
        using (var data = build(_ => context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture,
            UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty))))
            renderer.RenderDrawData(data);
        renderer.ValidationDrainVectorWorkCache();
        check(renderer.VectorSceneDiagnostics is { Scenes: 0, Outputs: 0 }
            && renderer.VectorWorkDiagnostics is { CachedBytes: 0, CachedCount: 0, PendingCount: 0 },
            "Completed vector cache and pending retirements drain after output destruction and frame fences");
        check(store.Statistics is { XmlParses: 1, Rasterizations: 0 },
            "Native work reuse does not reparse SVG or rasterize it on the CPU");
    }
}
