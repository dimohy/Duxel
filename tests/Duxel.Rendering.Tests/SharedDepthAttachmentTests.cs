using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class SharedDepthAttachmentTests
{
    public static void Run(VulkanRendererBackend renderer,
        Func<Action<UiImmediateContext>, UiDrawData> build,
        Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, UiTextureId white, UiRect bounds)
    {
        foreach (var samples in new[] { 1, 4 })
        {
            renderer.SetMsaaSamples(samples);
            void Scene(UiImmediateContext ui, bool reverse)
            {
                void Plane(string key, float depth, UiColor color)
                {
                    ui.BeginLayer(key, new(false, 1, default)
                    { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, depth)) });
                    ui.GetWindowDrawList().AddRectFilled(new(bounds.Width * .15f,
                        bounds.Height * .15f, bounds.Width * .7f, bounds.Height * .7f), color, white);
                    ui.EndLayer();
                }
                Plane("shared-depth.red", reverse ? .7f : .2f, new(0xFF0000FF));
                Plane("shared-depth.blue", reverse ? .2f : .7f, new(0xFFFF0000));
            }
            var red = capture($"shared-depth-reference-red-{samples}", ui => Scene(ui, false));
            var blue = capture($"shared-depth-reference-blue-{samples}", ui => Scene(ui, true));
            check(!red.AsSpan().SequenceEqual(blue), $"depth reference changes visible front plane at MSAA{samples}");
            for (var pass = 0; pass < 2; pass++)
            {
                // Submit changing depth without synchronous readback between frames.
                // A stale attachment or missing cross-frame dependency must not
                // preserve the previous frame's nearer plane.
                for (var frame = 0; frame < 12; frame++)
                {
                    var reverse = ((frame + pass) & 1) != 0;
                    using var data = build(ui => Scene(ui, reverse));
                    check(renderer.TryRenderDrawData(data), $"queued changing-depth frame MSAA{samples}/{pass}/{frame}");
                }
                var finalReverse = pass != 0;
                var actual = capture($"shared-depth-after-queue-{samples}-{pass}", ui => Scene(ui, finalReverse));
                check(actual.AsSpan().SequenceEqual(finalReverse ? blue : red),
                    $"queued depth clears preserve exact whole image at MSAA{samples}/{pass}");
                check(renderer.ValidationDepthAttachmentCount == 1,
                    $"one queue-owned depth attachment at MSAA{samples}/{pass}");
            }
        }
        renderer.SetMsaaSamples(1);
    }
}
