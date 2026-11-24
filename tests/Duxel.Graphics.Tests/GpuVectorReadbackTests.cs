using System.Numerics;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static class GpuVectorReadbackTests
{
    internal static void RunProjectedGlyphMaterial(string output, Action<bool, string> check, string? probeShaders = null)
    {
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var request = new PlatformTextMeshRequest(Path.Combine(fonts, "malgun.ttf"), null, "ㅇㅇ", 32);
        ProjectedGlyphMaterialTests.Run(output, check, WindowsPlatformTextBackend.Instance, request, 1, probeShaders);
    }

    internal static void RunGlyphCamera(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, false);
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var request = new PlatformTextMeshRequest(Path.Combine(fonts, "segoeui.ttf"),
            Path.Combine(fonts, "malgun.ttf"), "한글 Hangul 09 한글 · ₩", 32, .25f);
        GlyphCameraTests.Run(output, fixture, WindowsPlatformTextBackend.Instance, request, check);
    }

    internal static void Run(UiContext context, VulkanRendererBackend renderer,
        Func<Action<UiImmediateContext>, UiDrawData> build,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int snapshotWidth)
    {
        var texture = new UiTextureId(2_300_000_019);
        var scene = GpuVectorTestScenes.Rectangles(new(8, 8), (new(0, 0, 8, 8), new Vector4(.25f, .5f, .75f, .5f)));
        void Draw(UiImmediateContext ui)
        {
            var list = ui.GetWindowDrawList();
            list.AddRectFilled(new(16, 16, 8, 8), new(0, 0, 0, 255), ui.WhiteTextureId);
            list.AddImage(texture, new(16, 16), new(24, 24), default, new(1, 1), new(255, 255, 255));
        }
        byte[] Read(bool update)
        {
            using var data = build(ui =>
            {
                if (update)
                    context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture, UiTextureFormat.Rgba8Unorm, 8, 8, ReadOnlyMemory<byte>.Empty)
                    {
                        VectorScene = scene
                    });
                Draw(ui);
            });
            return renderer.RenderVectorAndReadback(data, texture, 8, 8);
        }
        var first = Read(true);
        var exact = first.Length == 8 * 8 * 4;
        for (var pixel = 0; pixel < first.Length; pixel += 4)
            exact &= first[pixel] == 32 && first[pixel + 1] == 64 && first[pixel + 2] == 96 && first[pixel + 3] == 128;
        check(exact, "Direct vector readback preserves premultiplied RGBA and alpha without swapchain swizzling");
        var diagnostics = renderer.VectorSceneDiagnostics;
        check(first.AsSpan().SequenceEqual(Read(false)), "Direct vector readback reuses an unchanged output");
        check(renderer.VectorSceneDiagnostics == diagnostics, "Readback creates no vector scene upload or compute redispatch");

        void Reject(UiTextureId id, int width, int height, Action<UiImmediateContext> draw, string name)
        {
            using var data = build(draw);
            var rejected = false;
            try
            {
                _ = renderer.RenderVectorAndReadback(data, id, width, height);
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Vector readback rejects before frame submission : " + name);
        }
        Reject(texture, 7, 8, Draw, "wrong current extent");
        Reject(new(2_300_000_020), 8, 8, Draw, "unknown texture");
        Reject(texture, 0, 8, Draw, "empty extent");
        Reject(texture, 8, 8, ui => context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, texture,
            UiTextureFormat.Rgba8Unorm, 8, 8, new byte[8 * 8 * 4])), "non-vector replacement");
        var window = capture("readback-window-restored", Draw);
        var index = (18 * snapshotWidth + 18) * 4;
        check(window[index] == 32 && window[index + 1] == 64 && window[index + 2] == 96,
            "Framebuffer readback restores its format after vector capture and rejected requests");
        capture("readback-destroy", _ => context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture,
            UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty)));
        check(renderer.VectorSceneDiagnostics is { Scenes: 0, Outputs: 0 }, "Readback releases the output with its ordinary texture lifetime");
    }
}
