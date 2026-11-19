using Duxel.Core;
using Duxel.Vulkan;

internal static class DamageRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Func<Action<UiImmediateContext>, UiDrawData> build, Action<bool, string> check,
        UiTextureId white, UiRect bounds)
    {
        foreach (var samples in new[] { 1, 4 })
        {
            renderer.SetMsaaSamples(samples);
            void Scene(UiImmediateContext ui, int frame)
            {
                var builder = ui.GetWindowDrawList();
                builder.AddRectFilled(bounds, new(0xFF202020), white);
                // Texture boundaries keep independent spatial commands in the base list.
                builder.AddCircleFilled(new(170, 100), 24, new(0xFF0080FF), white);
                var foreground = ui.GetForegroundDrawList();
                foreground.AddRectFilled(new(15 + frame * 3.25f, 24, 45, 50), new(0x800000FF), white);
                if (frame % 3 != 0)
                    foreground.AddCircleFilled(new(70, 65), 23.5f, new(0x8000FF00), white);
            }
            for (var frame = 0; frame < 9; frame++)
            {
                var value = frame;
                renderer.ValidationInvalidateDamage();
                var expected = capture($"damage-full-{samples}-{frame}", ui => Scene(ui, value));
                for (var repeat = 0; repeat < 4; repeat++)
                {
                    var actual = capture($"damage-partial-{samples}-{frame}-{repeat}", ui => Scene(ui, value));
                    check(actual.AsSpan().SequenceEqual(expected),
                        $"retained acquired image preserves movement, removal and alpha at {samples}x frame {frame}/{repeat}");
                }
                check(renderer.ValidationDamage().Empty && renderer.LastFrameMetrics.DrawCalls == 0
                    && renderer.LastFrameMetrics.DynamicUploadBytes == 0,
                    $"unchanged acquired image requires no draws or streamed uploads at {samples}x frame {frame}");
            }
            renderer.SetClearColor(new(0xFF301020));
            var changed = capture($"damage-clear-{samples}", ui => Scene(ui, 8));
            check(!renderer.ValidationDamage().Partial, $"clear color change invalidates retained image at {samples}x");
            renderer.ValidationInvalidateDamage();
            var clearReference = capture($"damage-clear-full-{samples}", ui => Scene(ui, 8));
            check(changed.AsSpan().SequenceEqual(clearReference), $"clear color invalidation preserves pixels at {samples}x");
            renderer.SetClearColor(new(0xFF000000));
            void ChangingBytes(UiImmediateContext ui, int value)
            {
                var builder = ui.GetWindowDrawList();
                builder.AddRectFilled(new(15, 15, 45, 45), new((byte)value, 40, 90), white);
            }
            var capacity = (Commands: 0, Bytes: 0L, ReadersValid: false);
            for (var frame = 0; frame < 32; frame++)
            {
                var value = frame + 1;
                var actual = capture($"damage-bytes-{samples}-{frame}", ui => ChangingBytes(ui, value));
                var memory = renderer.ValidationDamageMemory();
                check(memory.ReadersValid, $"damage snapshot readers match acquired-image lifetimes at {samples}x/{frame}");
                if (frame == 15) capacity = memory;
                if (frame > 15) check(memory.Commands <= capacity.Commands && memory.Bytes <= capacity.Bytes,
                    $"continuous geometry changes reuse bounded snapshot storage at {samples}x/{frame}");
                if (frame == 31)
                {
                    renderer.ValidationInvalidateDamage();
                    var expected = capture($"damage-bytes-full-{samples}", ui => ChangingBytes(ui, value));
                    check(actual.AsSpan().SequenceEqual(expected), $"changed primitive bytes with unchanged command state repaint correctly at {samples}x");
                }
            }
        }
        renderer.SetMsaaSamples(1);
        void UploadContent(UiImmediateContext ui)
            => ui.GetForegroundDrawList().AddRectFilled(new(10, 10, 60, 40), new(0xFF8090FF), white);
        var expectedUpload = capture("damage-staging-start", UploadContent);
        using (var data = build(UploadContent))
        {
            renderer.RenderAndReadback(data);
            check(renderer.ValidationStagingCapacity > 0, "resident upload creates reusable staging storage");
            for (var frame = 0; frame < 184; frame++) renderer.RenderAndReadback(data);
            check(renderer.ValidationStagingCapacity == 0, "completed idle upload staging is reclaimed after the common cache grace period");
            check(renderer.RenderAndReadback(data).AsSpan().SequenceEqual(expectedUpload),
                "reclaiming upload storage preserves resident geometry and retained pixels");
        }
        var reuploaded = capture("damage-staging-recreated", ui =>
        {
            ui.QueueTextureUpdate(new(UiTextureUpdateKind.Update, white, UiTextureFormat.Rgba8Unorm, 1, 1,
                new byte[] { 255, 255, 255, 255 }));
            UploadContent(ui);
        });
        check(renderer.ValidationStagingCapacity > 0 && reuploaded.AsSpan().SequenceEqual(expectedUpload),
            "a new upload recreates reclaimed staging and preserves texture/pixel contracts");
        void Clipped(UiImmediateContext ui, float clipWidth)
            => ui.GetWindowDrawList().AddRectFilled(new(20, 20, 120, 80), new(0xFF20B0E0), white, new(20, 20, clipWidth, 80));
        for (var frame = 0; frame < 4; frame++) capture("damage-clip-warm-" + frame, ui => Clipped(ui, 40));
        var changedClip = capture("damage-clip-changed", ui => Clipped(ui, 90));
        check(!renderer.ValidationDamage().Empty && renderer.LastFrameMetrics.DynamicUploadBytes > 0,
            "clip-only change resumes streamed uploads after an unchanged acquired image");
        renderer.ValidationInvalidateDamage();
        var fullClip = capture("damage-clip-full", ui => Clipped(ui, 90));
        check(changedClip.AsSpan().SequenceEqual(fullClip), "clip-only invalidation preserves the full-repaint image");
    }
}
