using System.Numerics;
using System.Security.Cryptography;
using Duxel.Core;

internal static class InputPresentationGpuTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunCacheChanges(check);
        InputLayerMetadataTests.RunGpu(check);
        var clip = new UiRect(0, 0, 320, 240);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var slots in new[] { 1, 3 })
            foreach (var scene in new[] { "paint-group-layer", "transformed-drawing", "image" })
            {
                using var reference = new PresentationReuseTests.Fixture(slots, false);
                using var candidate = new PresentationReuseTests.Fixture(slots, true);
                var frame = reference.FrameInfo;
                using var groupA = new UiPaintGroup();
                using var groupB = new UiPaintGroup();
                using var imageA = Fluent.Image(new(new(2, 2, [255, 50, 20, 255, 20, 180, 50, 255, 20, 60, 230, 255, 230, 180, 20, 255])));
                using var imageB = Fluent.Image(new(new(2, 2, [255, 50, 20, 255, 20, 180, 50, 255, 20, 60, 230, 255, 230, 180, 20, 255])));
                imageA.Size = imageB.Size = new(48, 40);
                imageA.CornerRadius = imageB.CornerRadius = 6;
                var versions = new int[2];
                void Draw(UiImmediateContext ui, int side)
                {
                    var version = versions[side];
                    if (scene == "paint-group-layer")
                    {
                        if (ui.BeginLayer("phase-gpu", new(true, 1, default)
                        {
                            ContentVersion = version
                        }))
                            ui.PaintGroup(side == 0 ? groupA : groupB, new(30, 30, 60, 60), (ulong)version,
                                control => control.Frame(new(35, 35, 40, 40), new((byte)(50 + version * 40), 90, 180), default, 6, 0));
                        ui.EndLayer();
                    }
                    else if (scene == "transformed-drawing")
                    {
                        var child = new UiDrawListBuilder(clip);
                        child._SetDrawListSharedData(ui.GetDrawListSharedData());
                        child.AddRectFilled(new(30, 30, 70, 30), new(50, 130, 210), ui.WhiteTextureId, clip);
                        ui.GetWindowDrawList().AppendTransformedWidget(child,
                            Matrix3x2.CreateRotation(version * .2f, new Vector2(65, 45)), new(320, 240), clip);
                        child._ClearFreeMemory();
                    }
                    else if (version is 1 or 3)
                    {
                        ui.SetCursorScreenPos(new(30 + version * 10, 50));
                        (side == 0 ? imageA : imageB).Render(ui);
                    }
                }
                for (var warmup = 0; warmup < slots + 1; warmup++)
                {
                    using var a = reference.Build(ui => Draw(ui, 0), idle);
                    reference.Render(a);
                    using var b = candidate.Build(ui => Draw(ui, 1), idle);
                    candidate.Render(b);
                }
                byte[] expected = [];
                for (var step = 0; step < 3; step++)
                {
                    reference.Context.NewFrame(step == 0 ? frame : frame with
                    {
                        DeltaTime = 0
                    }, idle, clip, UiTextSettings.Default);
                    reference.Context.Render(new Screen(ui => { versions[0]++; Draw(ui, 0); }));
                    using var data = reference.Context.GetDrawData();
                    expected = reference.Render(data);
                }
                candidate.Context.NewInputBatch(frame, [idle, idle, idle], clip, UiTextSettings.Default);
                candidate.Context.Render(new Screen(ui => { versions[1]++; Draw(ui, 1); }));
                using var final = candidate.Context.GetDrawData();
                var actual = candidate.Render(final);
                var prefix = $"Input presentation GPU / slots={slots}, scene={scene}";
                check(versions[0] == 3 && versions[1] == 3 && candidate.Context.InputProcessingMetrics.BuiltDrawings == 1,
                    prefix + " / identical logical state with one candidate drawing");
                check(actual.AsSpan().SequenceEqual(expected), prefix + " / sequential full repaint equals one final candidate readback"
                    + " / expected SHA256=" + Convert.ToHexString(SHA256.HashData(expected)) + "; actual SHA256=" + Convert.ToHexString(SHA256.HashData(actual)));
            }
    }
    internal static void RunCacheChanges(Action<bool, string> check)
    {
        var clip = new UiRect(0, 0, 320, 240);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var slots in new[] { 1, 3 })
            foreach (var scene in new[] { "content", "clip", "opacity", "translation", "scene-transform", "nested-content", "texture-update" })
            {
                using var reference = new PresentationReuseTests.Fixture(slots, false);
                using var candidate = new PresentationReuseTests.Fixture(slots, true);
                var frame = reference.FrameInfo;
                var values = new int[2];
                var captures = new int[2];
                var textures = new[]
                {
                    new UiImageTexture(new(900100), 2, 2, [220, 70, 30, 255, 30, 180, 90, 255, 40, 80, 210, 255, 230, 180, 20, 255]),
                    new UiImageTexture(new(900100), 2, 2, [220, 70, 30, 255, 30, 180, 90, 255, 40, 80, 210, 255, 230, 180, 20, 255])
                };
                var changesContent = scene is "content" or "nested-content";
                void Draw(UiImmediateContext ui, int side)
                {
                    var value = values[side];
                    if (scene == "texture-update")
                        textures[side].Prepare(ui, UiImageEffects.Create(false, false, 1 - value * .18f, 1, 1));
                    var paint = new UiControlContext(ui);
                    using var parentClip = paint.Clip(scene == "clip" ? new(20 + value * 12, 20, 80 - value * 10, 70) : clip);
                    var options = new UiLayerOptions(side == 1, scene == "opacity" ? 1 - value * .2f : 1,
                        scene == "translation" ? new(value * 12, value * 7) : default)
                    {
                        ContentVersion = side == 1 ? changesContent ? value : 0 : null,
                        Scene3D = scene == "scene-transform" ? new UiLayer3D(Matrix4x4.CreateRotationZ(value * .13f)) : null
                    };
                    var nested = scene == "nested-content";
                    var drawParent = !nested || ui.BeginLayer("cache-parent", options);
                    if (drawParent)
                    {
                        if (ui.BeginLayer("cache-child", options))
                        {
                            captures[side]++;
                            paint.Frame(new(20, 25, 110, 75), new((byte)(40 + (changesContent ? value * 45 : 0)), 110, 200), default, 7, 0);
                            paint.Frame(new(80, 65, 75, 60), new(220, 80, 35, 170), default, 3, 0);
                            if (scene == "texture-update")
                                ui.GetWindowDrawList().AddImage(textures[side].TextureId, new(35, 35), new(110, 95), default, new(1, 1), new(255, 255, 255));
                        }
                        ui.EndLayer();
                    }
                    if (nested)
                        ui.EndLayer();
                }
                byte[] previous = [];
                for (var warmup = 0; warmup < slots + 1; warmup++)
                {
                    using var a = reference.Build(ui => Draw(ui, 0), idle);
                    previous = reference.Render(a);
                    using var b = candidate.Build(ui => Draw(ui, 1), idle);
                    candidate.Render(b);
                }
                foreach (var sequence in new[] { new[] { 1, 2, 3 }, new[] { 2, 0, 1 }, new[] { 3, 2, 0 } })
                {
                    var before = captures[1];
                    byte[] expected = [];
                    foreach (var value in sequence)
                    {
                        values[0] = value;
                        reference.Context.NewFrame(frame with
                        {
                            DeltaTime = 0
                        }, idle, clip, UiTextSettings.Default);
                        reference.Context.Render(new Screen(ui => Draw(ui, 0)));
                        using var data = reference.Context.GetDrawData();
                        expected = reference.Render(data);
                    }
                    var step = 0;
                    var unpublishedReplayOwners = 0;
                    candidate.Context.NewInputBatch(frame, [idle, idle, idle], clip, UiTextSettings.Default);
                    candidate.Context.Render(new Screen(ui =>
                    {
                        values[1] = sequence[step++];
                        Draw(ui, 1);
                        if (!ui.IsPresentationPass)
                        {
                            // Observe and return only unpublished replay owners, before the input output is discarded.
                            if (ui.TakeLayerFrameLease() is { } lease)
                            {
                                unpublishedReplayOwners++;
                                lease.Return();
                            }
                            if (ui.TakeFrameSceneLeases() is { } scenes)
                            {
                                unpublishedReplayOwners += scenes.Count;
                                foreach (var sceneLease in scenes)
                                    sceneLease.Return();
                                scenes.Return();
                            }
                        }
                    }));
                    using var final = candidate.Context.GetDrawData();
                    var actual = candidate.Render(final);
                    var prefix = $"Input cache GPU / slots={slots}, scene={scene}, final={sequence[^1]}";
                    check(step == 3 && values[0] == values[1] && candidate.Context.InputProcessingMetrics is { Completed: true, CompletedSteps: 3, BuiltDrawings: 1 }
                        && (scene != "texture-update" || final.TextureUpdates.Any(update => update.TextureId == textures[1].TextureId && update.Kind == UiTextureUpdateKind.Update)),
                        prefix + " / every state transition precedes one final drawing");
                    check(actual.AsSpan().SequenceEqual(expected) && !expected.AsSpan().SequenceEqual(previous),
                        prefix + " / visible final change equals uncached sequential full repaint / expected SHA256="
                            + Convert.ToHexString(SHA256.HashData(expected)) + "; actual SHA256=" + Convert.ToHexString(SHA256.HashData(actual)));
                    check(captures[1] - before == (changesContent ? 3 : 0),
                        prefix + $" / content changes traverse each event; presentation-only changes reuse geometry / captures={captures[1] - before}");
                    check(unpublishedReplayOwners == 0,
                        prefix + $" / input-only replay acquires no presentation owners / actual={unpublishedReplayOwners}");
                    var settledCaptures = captures[1];
                    using var settled = candidate.Build(ui => Draw(ui, 1), idle);
                    var settledPixels = candidate.Render(settled);
                    check(captures[1] == settledCaptures && settledPixels.AsSpan().SequenceEqual(expected),
                        prefix + " / later ordinary frame replays a nonempty final cache with identical pixels");
                    previous = expected;
                }
            }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
