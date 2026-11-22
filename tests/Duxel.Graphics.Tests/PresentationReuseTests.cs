using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Vulkan;

internal static class PresentationReuseTests
{
    internal static void Run(string output, int frameSlots, bool captureHostPixels = false)
    {
        Directory.CreateDirectory(output);
        if (Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("Presentation reuse requires a fresh result directory.");
        var checks = new List<string>();
        var pairs = new List<PresentationPixelPair>();
        var acquired = new HashSet<int>();
        var sparseDamage = new List<SparseDamageSample>();
        string? failure = null;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
        }
        try
        {
            using var control = new Fixture(frameSlots, false);
            using var candidate = new Fixture(frameSlots, true);
            foreach (var samples in new[] { 1, 4 })
            {
                control.Renderer.SetMsaaSamples(samples);
                candidate.Renderer.SetMsaaSamples(samples);
                VerifyReplayTextMode(control, output, samples, Check);
                byte[]? lastReferencePixels = null, previousReferencePixels = null;
                void Pair(string name, Action<UiImmediateContext> draw, bool? expectReuse = null)
                {
                    var before = candidate.Renderer.LastFrameMetrics.PresentationSequence;
                    var prefix = $"msaa{samples}-{name}";
                    var captureTextInput = captureHostPixels && samples == 1 && name == "readable-text-preflight";
                    var expected = control.Capture(draw, null, captureTextInput
                        ? textures => WriteHostTextInputs(output, prefix, textures)
                        : null);
                    previousReferencePixels = lastReferencePixels;
                    lastReferencePixels = expected;
                    var actual = candidate.Capture(draw);
                    var equal = expected.AsSpan().SequenceEqual(actual);
                    pairs.Add(new(prefix, Convert.ToHexString(SHA256.HashData(expected)),
                        Convert.ToHexString(SHA256.HashData(actual)), candidate.Renderer.LastFrameMetrics.PresentationReused));
                    if (!equal)
                    {
                        File.WriteAllBytes(Path.Combine(output, prefix + ".control.rgba"), expected);
                        File.WriteAllBytes(Path.Combine(output, prefix + ".candidate.rgba"), actual);
                    }
                    if (captureHostPixels && name is "readable-text-preflight" or "sparse-warmup-0" or "primitive-batch-edge-1"
                        or "primitive-batch-vertical-1" or "primitive-batch-radius-clamp-1")
                        File.WriteAllBytes(Path.Combine(output, prefix + ".host.rgba"), expected);
                    Check(equal, prefix + " matches independent always-present raw pixels");
                    var metrics = candidate.Renderer.LastFrameMetrics;
                    Check(candidate.Renderer.ValidationDamageOwnershipIsConsistent()
                        && control.Renderer.ValidationDamageOwnershipIsConsistent(), prefix + " retains exact snapshot reader ownership");
                    Check(metrics.DrawDataFrameId == candidate.LastFrameId, prefix + " acknowledges the current CPU frame");
                    if (expectReuse is { } reuse)
                        Check(metrics.PresentationReused == reuse, prefix + " has the required reuse outcome");
                    Check(metrics.PresentationSequence == before + (metrics.PresentationReused ? 0ul : 1ul),
                        prefix + " counts only actual presentations");
                    if (metrics.PresentationReused)
                        Check(metrics.DynamicUploadBytes == 0 && metrics.StagingUploadBytes == 0 && metrics.DrawCalls == 0
                            && metrics.RepaintBounds == default && double.IsNaN(metrics.GpuRenderMicroseconds),
                            prefix + " reports no fictitious GPU work or repaired pixels");
                    else
                        acquired.Add(candidate.Renderer.ValidationLastAcquiredImageIndex);
                }

                Pair("readable-text-preflight", ui => Scene(ui, 0), false);
                var visibleTextPixels = 0;
                for (var y = 0; y < 64; y++)
                    for (var x = 0; x < 120; x++)
                    {
                        var at = (y * 320 + x) * 4;
                        if (lastReferencePixels![at] > 100 && lastReferencePixels[at + 1] > 100 && lastReferencePixels[at + 2] > 100)
                            visibleTextPixels++;
                    }
                if (visibleTextPixels <= 30)
                {
                    File.WriteAllBytes(Path.Combine(output, $"msaa{samples}-text-preflight.rgba"), lastReferencePixels!);
                    var details = string.Empty;
                    using var diagnosis = control.Build(ui =>
                    {
                        details = $"fontSize={ui.GetFontSize()},lineHeight={ui.GetFont().LineHeight},measure={ui.CalcTextSize("FPS 0")},color={new UiControlContext(ui).Theme.Text}";
                        Scene(ui, 0);
                    });
                    details += Environment.NewLine + string.Join(Environment.NewLine,
                        diagnosis.DrawLists.SelectMany(list => list.Commands).Select(command => command.ToString()));
                    details += Environment.NewLine + string.Join(Environment.NewLine,
                        diagnosis.TextureUpdates.Select(update => $"texture={update.TextureId},size={update.Width}x{update.Height},bytes={update.RgbaPixels.Length}"));
                    File.WriteAllText(Path.Combine(output, $"msaa{samples}-text-preflight.txt"), details);
                }
                Check(visibleTextPixels > 30, $"msaa{samples} independently verifies readable counter text before cadence validation");

                // Small changes surround a large, unchanged translucent command.
                // That command affects the final pixels, but its entire bounds
                // must not enlarge damage when its position/state stay unchanged.
                var initialized = new HashSet<int>();
                for (var warmup = 0; warmup < 8; warmup++)
                {
                    candidate.Renderer.ValidationNativePresentationSequence++;
                    Pair("sparse-warmup-" + warmup, ui => SparseScene(ui, 0), false);
                    initialized.Add(candidate.Renderer.ValidationLastAcquiredImageIndex);
                }
                var bounded = 0;
                for (var frame = 1; frame <= 24; frame++)
                {
                    var tick = frame;
                    Pair("sparse-change-" + frame, ui => SparseScene(ui, tick), false);
                    var image = candidate.Renderer.ValidationLastAcquiredImageIndex;
                    var known = initialized.Contains(image);
                    var bounds = candidate.Renderer.LastFrameMetrics.RepaintBounds;
                    sparseDamage.Add(new(samples, frame, image, known, bounds.X, bounds.Y, bounds.Width, bounds.Height));
                    if (known)
                    {
                        bounded++;
                        Check(bounds.Width * bounds.Height <= 320 * 240 / 10f,
                            $"msaa{samples}-sparse-{frame} repairs only the small changed bounds; actual={bounds}");
                    }
                    initialized.Add(image);
                }
                Check(bounded >= 20, $"msaa{samples} verifies sufficient initialized-image sparse repairs");
                for (var frame = 0; frame < 12; frame++)
                {
                    var reverse = frame % 2 == 0;
                    Pair("sparse-reorder-" + frame, ui => SparseScene(ui, 0, reverse), false);
                }
                for (var frame = 0; frame < 12; frame++)
                {
                    var inserted = frame % 2 == 0;
                    Pair("sparse-insert-remove-" + frame, ui => SparseScene(ui, 0, insert: inserted), false);
                }
                for (var frame = 0; frame < 24; frame++)
                {
                    var tick = frame;
                    Pair("sparse-edit-insert-remove-" + frame,
                        ui => SparseScene(ui, tick, insert: tick % 2 == 0), false);
                    var bounds = candidate.Renderer.LastFrameMetrics.RepaintBounds;
                    Check(bounds.Width * bounds.Height <= 320 * 240 / 10f,
                        $"msaa{samples}-structural-sparse-{frame} excludes the unchanged middle; actual={bounds}");
                }
                for (var frame = 0; frame < 8; frame++)
                {
                    candidate.Renderer.ValidationNativePresentationSequence++;
                    Pair("primitive-batch-warmup-" + frame, ui => PrimitiveBatchScene(ui, 0), false);
                }
                for (var frame = 1; frame <= 24; frame++)
                {
                    var tick = frame;
                    Pair("primitive-batch-edge-" + frame, ui => PrimitiveBatchScene(ui, tick), false);
                    var bounds = candidate.Renderer.LastFrameMetrics.RepaintBounds;
                    Check(bounds.Width * bounds.Height <= 1024,
                        $"msaa{samples}-primitive-batch-{frame} repairs fill edges and thumb, excluding128 stationary records; actual={bounds}");
                }
                for (var frame = 0; frame < 24; frame++)
                {
                    var tick = frame;
                    Pair("primitive-batch-vertical-" + frame, ui => PrimitiveBatchScene(ui, tick, vertical: true), false);
                }
                for (var frame = 0; frame < 18; frame++)
                {
                    var tick = frame;
                    Pair("primitive-batch-radius-clamp-" + frame, ui => PrimitiveBatchScene(ui, tick, tiny: true), false);
                }

                for (var frame = 0; frame < 64; frame++)
                {
                    var tick = frame;
                    var previousHash = frame == 0 ? null : pairs[^1].ControlSha256;
                    Pair("async-" + frame, ui => Scene(ui, tick), frame == 0 ? false : frame % 3 != 0 && frame % 16 != 0);
                    if (frame != 0)
                    {
                        if ((pairs[^1].ControlSha256 != previousHash) != (frame % 3 == 0 || frame % 16 == 0))
                        {
                            File.WriteAllBytes(Path.Combine(output, $"msaa{samples}-cadence-{frame}.previous.rgba"), previousReferencePixels!);
                            File.WriteAllBytes(Path.Combine(output, $"msaa{samples}-cadence-{frame}.current.rgba"), lastReferencePixels!);
                        }
                        Check((pairs[^1].ControlSha256 != previousHash) == (frame % 3 == 0 || frame % 16 == 0),
                            $"msaa{samples}-async-{frame} reference pixels visibly follow the independent16/3 cadence");
                    }
                }
                Pair("static-settle", ui => Scene(ui, 63), true);
                candidate.Renderer.ValidationNativePresentationSequence++;
                Pair("native-invalidation", ui => Scene(ui, 63), false);
                Pair("native-invalidation-settled", ui => Scene(ui, 63), true);
                var unchangedWithHighlights = candidate.Capture(ui =>
                {
                    ui.SetRepaintHighlights(true);
                    Scene(ui, 63);
                });
                Check(candidate.Renderer.LastFrameMetrics is { PresentationReused: true, RepaintHighlightActive: false }
                    && unchangedWithHighlights.AsSpan().SequenceEqual(lastReferencePixels),
                    "highlighting does not force a repair or add an outline when presentation is reused");
                Pair("diagnostic-disabled-with-unchanged-content", ui =>
                {
                    ui.SetRepaintHighlights(false);
                    Scene(ui, 63);
                }, true);

                using (var affine = new DamageAffinePaint())
                {
                    void DrawAffine(UiImmediateContext ui)
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        var context = new UiControlContext(ui);
                        context.Frame(new(0, 0, 320, 240), new(24, 24, 24), default, 0, 0);
                        context.Frame(new(8, 12, 32, 180), new(150, 80, 50), default, 0, 0);
                        ui.SetCursorScreenPos(new(95, 70));
                        using var clip = context.Clip(new(75, 55, 150, 150));
                        affine.Render(ui);
                    }
                    for (var warmup = 0; warmup < 12; warmup++)
                    {
                        candidate.Renderer.ValidationNativePresentationSequence++;
                        Pair("affine-damage-warmup-" + warmup, DrawAffine, false);
                    }
                    var partial = 0;
                    foreach (var angle in new[] { 23f, 57f, 117f, 251f, 0f })
                    {
                        affine.Transform = new(Scale: 1.5f, RotationDegrees: angle);
                        Pair("affine-damage-" + angle, DrawAffine, false);
                        var bounds = candidate.Renderer.LastFrameMetrics.RepaintBounds;
                        if (bounds.Width > 0)
                        {
                            partial++;
                            Check(bounds.X >= 73 && bounds.Y >= 53 && bounds.Width < 155 && bounds.Height < 155,
                                "affine damage stays inside the actual parent clip: " + bounds);
                        }
                    }
                    Check(partial >= 3, "affine transform repairs previously initialized images without repainting the window");
                    Pair("affine-damage-still", DrawAffine, true);
                }

                foreach (var variant in new[] { 0, 1, 1, 2, 2, 0, 0 })
                    Pair("clip-opacity-order-" + pairs.Count, ui =>
                    {
                        var paint = new UiControlContext(ui);
                        using var clip = paint.Clip(new(10 + variant * 4, 10, 120, 100));
                        var a = new UiRect(10, 20, 80, 70);
                        var b = new UiRect(50, 40, 80, 70);
                        if (variant == 2)
                        {
                            paint.Frame(b, new(0, 80, 240, 190), default, 8, 0);
                            paint.Frame(a, new(240, 40, 0, 180), default, 8, 0);
                        }
                        else
                        {
                            paint.Frame(a, new(240, 40, 0, variant == 1 ? (byte)80 : (byte)180), default, 8, 0);
                            paint.Frame(b, new(0, 80, 240, 190), default, 8, 0);
                        }
                    });

                foreach (var variant in new[] { 0, 0, 1, 1, 2, 2, 0, 0 })
                    Pair("retained-opacity-translation-" + pairs.Count, ui =>
                    {
                        if (ui.BeginLayer("reuse-retained", new(true, variant == 1 ? .45f : .8f,
                            new(variant == 2 ? 20 : 0, 0))
                        {
                            ContentVersion = 1
                        }))
                            new UiControlContext(ui).Frame(new(15, 20, 100, 80), new(210, 40, 90), default, 8, 0);
                        ui.EndLayer();
                    });

                candidate.Renderer.ValidationBeforePresentation = () =>
                {
                    candidate.Renderer.ValidationBeforePresentation = null;
                    candidate.Renderer.ValidationNativePresentationSequence++;
                };
                Pair("racing-native-invalidation", ui => Scene(ui, 126), false);
                Pair("racing-native-invalidation-repair", ui => Scene(ui, 126), false);
                Pair("racing-native-invalidation-settled", ui => Scene(ui, 126), true);

                foreach (var variant in new[] { 0, 0, 1, 1, 2, 2, 0, 0 })
                    Pair("static-stream-return-" + pairs.Count, ui =>
                    {
                        if (ui.BeginLayer("reuse-stream-return", new(variant != 1, 1, default)
                        {
                            ContentVersion = variant == 1 ? null : 1
                        }))
                            new UiControlContext(ui).Frame(new(12, 20, 90, 60), new(40, 180, 110), default, 6, 0);
                        ui.EndLayer();
                    });

                foreach (var variant in new[] { 0, 0, 1, 1, 2, 2, 0, 0 })
                    Pair("nested-rounded-subtraction-mask-" + pairs.Count, ui =>
                    {
                        var paint = new UiControlContext(ui);
                        using var rounded = paint.RoundedClip(new(10, 10, 120, 110), variant == 1 ? 24 : 6);
                        using var subtraction = paint.CircleCutout(new(65 + variant * 6, 65), variant == 2 ? 21 : 13);
                        paint.Frame(new(0, 0, 150, 140), new(40, 130, 220), default, 0, 0);
                    });

                using (var particles = new UiGpuParticleSystem(new UiGpuParticle[]
                {
                    new() { Position = new(40, 40), Direction = new(1, 0), SpeedScale = 1, SizeScale = 1, Color = new UiColor(230, 40, 60).Rgba },
                    new() { Position = new(120, 80), Direction = new(0, 1), SpeedScale = 1, SizeScale = 1, Color = new UiColor(30, 210, 80).Rgba }
                }))
                {
                    var particleFrame = new UiGpuParticleFrame(particles, new(0, 0, 320, 240), 1f / 60, Speed: 80);
                    string? previousParticleHash = null;
                    for (var tick = 0; tick < 8; tick++)
                    {
                        Pair("gpu-animation-" + tick, ui => ui.GetWindowDrawList().AddGpuParticles(particleFrame), false);
                        Check(candidate.Renderer.LastFrameMetrics.GpuParticleDispatches > 0,
                            "constant CPU particle commands still execute GPU simulation");
                        if (previousParticleHash is not null)
                            Check(pairs[^1].ControlSha256 != previousParticleHash, "independent GPU particle reference actually advances");
                        previousParticleHash = pairs[^1].ControlSha256;
                    }
                }

                var texture = new UiTextureId(77);
                foreach (var color in new[] { new UiColor(240, 20, 30), new UiColor(20, 230, 40) })
                {
                    var bytes = new byte[] { (byte)(color.Rgba & 255), (byte)((color.Rgba >> 8) & 255), (byte)((color.Rgba >> 16) & 255), 255 };
                    control.Context.QueueTextureUpdate(new(control.TextureCreated ? UiTextureUpdateKind.Update : UiTextureUpdateKind.Create,
                        texture, UiTextureFormat.Rgba8Unorm, 1, 1, bytes));
                    candidate.Context.QueueTextureUpdate(new(candidate.TextureCreated ? UiTextureUpdateKind.Update : UiTextureUpdateKind.Create,
                        texture, UiTextureFormat.Rgba8Unorm, 1, 1, bytes));
                    control.TextureCreated = candidate.TextureCreated = true;
                    Pair("texture-revision-" + pairs.Count, ui => ui.Image(texture, new(80, 60)), false);
                    Pair("texture-revision-settled-" + pairs.Count, ui => ui.Image(texture, new(80, 60)), true);
                }
                var callbacks = 0;
                for (var frame = 0; frame < 3; frame++)
                    Pair("callback-" + frame, ui =>
                    {
                        Scene(ui, 0);
                        ui.GetWindowDrawList().AddCallback((_, _) => callbacks++);
                    }, false);
                Check(callbacks == 6, $"msaa{samples} executes every callback on both renderers");

                // Keep three CPU frames alive while replacing and replaying the same retained layer.
                const string layerId = "revision-ownership";
                void DrawRevision(UiImmediateContext ui, int revision, float opacity, bool retained)
                {
                    if (ui.BeginLayer(layerId, new(false, opacity, default) { ContentVersion = retained ? revision : null }))
                        ui.GetWindowDrawList().AddRectFilled(new(20 + revision * 10, 30, 80, 40), new(220, 100, 40), ui.WhiteTextureId);
                    ui.EndLayer();
                }
                var expectedOld = control.Capture(ui => DrawRevision(ui, 0, 1, false));
                var expectedReplay = control.Capture(ui => DrawRevision(ui, 1, .4f, false));
                Check(!expectedOld.AsSpan().SequenceEqual(expectedReplay), "retained revision reference images visibly differ");
                using (var oldFrame = candidate.Build(ui => DrawRevision(ui, 0, 1, true)))
                using (var replacement = candidate.Build(ui => DrawRevision(ui, 1, 1, true)))
                using (var replay = candidate.Build(ui => DrawRevision(ui, 1, .4f, true)))
                {
                    var lifetime = oldFrame.LayerFrameLease!.Value.Owner;
                    var oldList = oldFrame.DrawLists.Single(list => list.StaticGeometryKey?.StartsWith("duxel.layer.static:revision-ownership:", StringComparison.Ordinal) == true);
                    var replacementList = replacement.DrawLists.Single(list => list.StaticGeometryKey == oldList.StaticGeometryKey);
                    Check(ReferenceEquals(oldList.StaticGeometryKey, replacementList.StaticGeometryKey)
                          && !ReferenceEquals(oldList.Commands, replacementList.Commands),
                        "layer revisions share the geometry key while each held frame owns independent commands");
                    Check(lifetime.State == (3, 1), "three borrowed frames keep the replaced retained source alive");
                    replacement.Dispose();
                    Check(lifetime.State == (2, 1), "returning a newer frame first keeps the older revision alive");
                    Check(candidate.Render(oldFrame).AsSpan().SequenceEqual(expectedOld), "queued old revision preserves geometry and commands");
                    oldFrame.Dispose();
                    Check(lifetime.State == (1, 0), "returning the last old frame retires its source with a newer frame alive");
                    Check(candidate.Render(replay).AsSpan().SequenceEqual(expectedReplay), "replayed revision preserves the updated opacity and bounds");
                }

                // Submit changing frames without intervening readback, exercising
                // configured frame slots before comparing the final acquired image.
                for (var batch = 0; batch < 8; batch++)
                {
                    for (var subframe = 0; subframe < 4; subframe++)
                    {
                        var tick = batch * 4 + subframe;
                        Check(control.Submit(ui => Scene(ui, tick)), "control batch submission accepted");
                        Check(candidate.Submit(ui => Scene(ui, tick)), "candidate batch submission accepted");
                    }
                    var final = batch * 4 + 3;
                    Pair("queued-batch-" + batch, ui => Scene(ui, final));
                }
                var beforeFailure = candidate.Renderer.LastFrameMetrics.PresentationSequence;
                candidate.Renderer.ValidationAcquireNotReadyAttempts = 1;
                Check(!candidate.Submit(ui => Scene(ui, 120)), "failed acquisition is not accepted as reuse");
                Check(candidate.Renderer.ValidationDamageOwnershipIsConsistent(), "failed acquisition releases new snapshots without losing retained readers");
                Check(candidate.Renderer.LastFrameMetrics.PresentationSequence == beforeFailure,
                    "failed acquisition preserves the actual presentation sequence");
                Pair("failed-acquire-retry", ui => Scene(ui, 120), false);
                Pair("numeric-change-with-identical-pixels", ui => Scene(ui, 121), true);
                candidate.Renderer.ValidationRejectNextPresentation = true;
                Check(!candidate.Submit(ui => Scene(ui, 123)), "rejected presentation requires a fresh retry");
                Pair("failed-present-retry", ui => Scene(ui, 123), false);
            }
            foreach (var samples in new[] { 1, 4 })
            {
                using var groupControl = new Fixture(frameSlots, false);
                using var groupCandidate = new Fixture(frameSlots, true);
                groupControl.Renderer.SetMsaaSamples(samples);
                groupCandidate.Renderer.SetMsaaSamples(samples);
                var references = new List<(string Name, byte[] Pixels)>();
                var referenceIndex = 0;
                void RunGroups(Fixture fixture, bool compare)
                {
                    PaintGroupRenderingTests.Run(fixture.Context, (name, draw) =>
                    {
                        var pixels = fixture.Capture(draw);
                        if (!compare)
                            references.Add((name, pixels));
                        else
                        {
                            Check(referenceIndex < references.Count && references[referenceIndex].Name == name,
                                "paint-group independent capture order matches");
                            var expected = references[referenceIndex].Pixels;
                            var equal = expected.AsSpan().SequenceEqual(pixels);
                            if (!equal)
                            {
                                File.WriteAllBytes(Path.Combine(output, $"group-{samples}-{referenceIndex}.control.rgba"), expected);
                                File.WriteAllBytes(Path.Combine(output, $"group-{samples}-{referenceIndex}.candidate.rgba"), pixels);
                            }
                            Check(equal, "paint-group " + name + " matches independent raw pixels");
                            pairs.Add(new($"msaa{samples}-paint-group-{referenceIndex++}-{name}",
                                Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(pixels)),
                                fixture.Renderer.LastFrameMetrics.PresentationReused));
                        }
                        return pixels;
                    }, Check, fixture.Build, fixture.Render, fixture.RenderSubmit,
                        () => fixture.Renderer.ValidationPaintGeometryEntries, (int)fixture.FrameInfo.DisplaySize.X, new(2),
                        () => fixture.Renderer.LastFrameMetrics, fixture.Renderer.ValidationSwapchainImageCount,
                        fixture.Renderer.ValidationPaintTargetGenerations,
                        forceFullReference: !compare);
                    Check(fixture.Renderer.ValidationDamageOwnershipIsConsistent(), "paint-group lifetime preserves snapshot reader ownership");
                }
                RunGroups(groupControl, false);
                RunGroups(groupCandidate, true);
                Check(referenceIndex == references.Count, "all independent paint-group captures were compared");
            }
        }
        catch (Exception error)
        {
            failure = error.ToString();
            throw;
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(
                new PresentationReuseResult(RuntimeInformation.FrameworkDescription, failure is null, failure,
                    frameSlots, acquired.Order().ToArray(), checks.Count, checks.ToArray(), pairs.ToArray(), sparseDamage.ToArray()),
                PresentationReuseJson.Default.PresentationReuseResult));
        }
    }

    private static void WriteHostTextInputs(string output, string prefix, UiPooledList<UiTextureUpdate> textures)
    {
        var count = 0;
        foreach (var texture in textures.AsSpan())
        {
            if (texture.RgbaPixels.IsEmpty || (texture.Width == 1 && texture.Height == 1)) continue;
            var file = $"{prefix}.input-{++count}-{texture.Format}-{texture.Width}x{texture.Height}.bin";
            File.WriteAllBytes(Path.Combine(output, file), texture.RgbaPixels.ToArray());
        }
        if (count == 0)
            throw new InvalidOperationException("The text preflight did not produce any glyph pixel uploads.");
    }

    private static void Scene(UiImmediateContext ui, int frame)
    {
        ui.SetCursorScreenPos(new(12, 10));
        ui.TextColored(new(255, 255, 255), "FPS " + frame / 16);
        ui.SetCursorScreenPos(new(12, 42));
        ui.TextColored(new(255, 255, 255), "NF " + frame / 3);
        new UiControlContext(ui).Frame(new(12, 80, 100, 40),
            frame / 3 % 2 == 0 ? new(30, 110, 220) : new(220, 90, 30), default, 5, 0);
    }

    private static void SparseScene(UiImmediateContext ui, int frame, bool reverse = false, bool insert = false)
    {
        var paint = new UiControlContext(ui);
        void First()
        {
            using var clip = paint.Clip(new(16, 26, 28, 28));
            paint.Frame(new(20, 30, 20, 20), frame % 2 == 0 ? new(230, 30, 40) : new(30, 190, 230), default, 0, 0);
        }
        void Last()
        {
            using var clip = paint.Clip(new(56, 36, 28, 28));
            paint.Frame(new(60, 40, 20, 20), frame % 2 == 0 ? new(40, 210, 70) : new(220, 160, 30), default, 0, 0);
        }
        if (reverse)
            Last();
        else
            First();
        paint.Frame(new(0, 0, 320, 240), new(30, 40, 50, 100), default, 0, 0);
        if (insert)
        {
            using var clip = paint.Clip(new(96, 76, 20, 20));
            paint.Frame(new(100, 80, 12, 12), new(190, 30, 160), default, 0, 0);
        }
        if (reverse)
            First();
        else
            Last();
    }

    private static void PrimitiveBatchScene(UiImmediateContext ui, int frame, bool vertical = false, bool tiny = false)
    {
        var paint = new UiControlContext(ui);
        var width = tiny ? ((frame % 6) switch
        {
            0 => .25f,
            1 => 4.125f,
            2 => 15.625f,
            3 => 16.375f,
            4 => 94.625f,
            _ => 90.375f
        })
            : frame % 2 == 0 ? 90.375f : 94.625f;
        paint.Frame(new(0, 0, 320, 240), new(30, 40, 50, 180), default, 0, 0);
        paint.Frame(new(20.25f, 30.125f, vertical ? 20.5f : width, vertical ? width : 20.5f), new(40, 170, 230, 170), default, 8, 0);
        for (var i = 0; i < 128; i++)
            paint.Frame(new(4 + i % 32 * 9.5f, 80 + i / 32 * 40, 2, 12), new(120, 130, 150, 180), default, 0, 0);
        paint.Frame(vertical ? new(18.25f, 30.125f + width - 7, 24.5f, 14)
            : new(20.25f + width - 7, 28.125f, 14, 24.5f), new(220, 230, 240, 220), default, 7, 0);
    }

    private static void VerifyReplayTextMode(Fixture fixture, string output, int samples, Action<bool, string> check)
    {
        var flat = new UiLayerOptions(false, 1, default) { ContentVersion = 1 };
        var camera = new UiLayer3D(System.Numerics.Matrix4x4.Identity, false);
        var empty = fixture.Capture(_ => { });
        foreach (var inherited in new[] { false, true })
        {
            var prefix = $"msaa{samples}-replay-text-mode-{(inherited ? "inherited" : "explicit")}";
            (byte[] Pixels, bool Hit) Capture(bool projected, bool direct)
            {
                var hit = false;
                var pixels = fixture.Capture(ui =>
                {
                    var parent = projected && inherited;
                    if (parent)
                        ui.BeginLayer(prefix + "-parent", new UiLayerOptions(false, 1, default) { Scene3D = camera });
                    var options = flat with { Scene3D = projected && !inherited ? camera : null };
                    var id = prefix + (direct ? "-direct" : "-scoped");
                    hit = direct && ui.TryReplayLayer(id, options);
                    if (!hit)
                    {
                        if (ui.BeginLayer(id, options))
                        {
                            ui.SetCursorScreenPos(new(20, 24));
                            ui.TextColored(new(0xFFFFFFFF), "Layer 한글");
                        }
                        ui.EndLayer();
                    }
                    if (parent)
                        ui.EndLayer();
                });
                return (pixels, hit);
            }

            byte[]? flatPixels = null;
            for (var step = 0; step < 5; step++)
            {
                var projected = step is 1 or 2;
                var expected = Capture(projected, false).Pixels;
                var (actual, hit) = Capture(projected, true);
                var same = actual.AsSpan().SequenceEqual(expected);
                if (!same)
                {
                    File.WriteAllBytes(Path.Combine(output, $"{prefix}-{step}.scoped.rgba"), expected);
                    File.WriteAllBytes(Path.Combine(output, $"{prefix}-{step}.direct.rgba"), actual);
                }
                if (!projected)
                    flatPixels = expected;
                check(!expected.AsSpan().SequenceEqual(empty), $"{prefix}-{step} draws visible text");
                if (projected)
                    check(!expected.AsSpan().SequenceEqual(flatPixels), $"{prefix}-{step} uses distinct vector text pixels");
                check(same, $"{prefix}-{step} direct and scoped replay render the same text pixels");
                check(hit == (step is 2 or 4), $"{prefix}-{step} only replays when the text mode is unchanged");
            }
        }
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly IPlatformBackend _platform;
        private VulkanRendererBackend? _renderer;
        internal readonly UiContext Context;
        private bool _upload = true;
        private readonly bool _forceFullRepaint;
        internal bool TextureCreated;
        internal ulong LastFrameId;
        internal (int Width, int Height) LastCaptureSize { get; private set; }
        internal void WriteCapturePng(string path, byte[] pixels)
        {
            var (width, height) = LastCaptureSize;
            if (pixels.Length != checked(width * height * 4)) throw new InvalidOperationException("Capture dimensions do not match native framebuffer bytes.");
            BrowserPng.Write(path, width, height, pixels);
        }
        internal VulkanRendererBackend Renderer => _renderer ?? throw new ObjectDisposedException(nameof(Fixture));
        internal UiFrameInfo FrameInfo => new(1f / 60, new(_platform.FramebufferSize.Width, _platform.FramebufferSize.Height), new(1, 1));

        internal Fixture(int frameSlots, bool reuse, IPlatformBackend? platform = null, int msaaSamples = 1,
            bool useR8ParagraphCoverage = false)
        {
            Context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
                new Dictionary<int, UiGlyphInfo>(), new Dictionary<uint, float>(), 16, 0, 0, -1), new(1), new(2))
            {
                UseR8ParagraphCoverage = useR8ParagraphCoverage
            };
            _forceFullRepaint = !reuse;
            _platform = platform ?? PresentationTestPlatform.Create();
            try
            {
                _renderer = new(_platform, new(2, EnableVSync: false, MsaaSamples: msaaSamples, MaxFramesInFlight: frameSlots));
                _renderer.ValidationNativePresentationSequence = reuse ? 0 : null;
                _renderer.ValidationReuseReadback = reuse;
                _renderer.CreateDeviceObjects();
                PresentationTestPlatform.ConfigureFonts(Context, _platform);
                // The placeholder atlas and direct-text base both describe16px.
                // Context-font scopes must not reduce the test to1px glyphs.
                Context.SetDirectTextBaseFontSize(16);
            }
            catch { Dispose(); throw; }
        }

        internal UiDrawData Build(Action<UiImmediateContext> draw) => Build(draw, null);
        internal UiDrawData Build(Action<UiImmediateContext> draw, UiInputState? input, bool pollEvents = true,
            IReadOnlyList<UiInputState>? orderedInputs = null, double idleWaitSeconds = 0)
        {
            if (pollEvents)
                _platform.PollEvents();
            var size = _platform.FramebufferSize;
            var clip = new UiRect(0, 0, size.Width, size.Height);
            var frameInfo = FrameInfo with { IdleWaitSeconds = idleWaitSeconds };
            if (orderedInputs is { Count: > 0 })
                Context.NewInputBatch(frameInfo, orderedInputs, clip, UiTextSettings.Default);
            else
                Context.NewFrame(frameInfo,
                    input ?? new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                    clip, UiTextSettings.Default);
            if (_upload)
            {
                Context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(2), UiTextureFormat.Rgba8Unorm, 1, 1,
                    new byte[] { 255, 255, 255, 255 }));
                _upload = false;
            }
            Context.Render(new Screen(draw));
            var data = Context.GetDrawData();
            LastFrameId = data.DrawDataFrameId;
            return data;
        }

        internal byte[] Capture(Action<UiImmediateContext> draw) => Capture(draw, null);
        internal byte[] Capture(Action<UiImmediateContext> draw, UiInputState? input,
            Action<UiPooledList<UiTextureUpdate>>? observeTextureUploads = null)
        {
            using var data = Build(draw, input);
            observeTextureUploads?.Invoke(data.TextureUpdates);
            var size = FrameInfo.DisplaySize;
            var pixels = Render(data);
            LastCaptureSize = ((int)size.X, (int)size.Y);
            if (pixels.Length != checked(LastCaptureSize.Width * LastCaptureSize.Height * 4))
                throw new InvalidOperationException("Native Vulkan readback size differs from its frame dimensions.");
            return pixels;
        }
        internal bool Submit(Action<UiImmediateContext> draw)
        {
            using var data = Build(draw);
            return RenderSubmit(data);
        }
        internal byte[] Render(UiDrawData data)
        {
            Renderer.ValidationFullRepaint = _forceFullRepaint;
            return Renderer.RenderAndReadback(data);
        }
        internal bool RenderSubmit(UiDrawData data)
        {
            Renderer.ValidationFullRepaint = _forceFullRepaint;
            return Renderer.TryRenderDrawData(data);
        }
        public void Dispose()
        {
            _renderer?.Dispose();
            _renderer = null;
            Context.Dispose();
            _platform.Dispose();
        }
    }
    private sealed class DamageAffinePaint : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => new(80, 45);
        protected override void RenderControl(UiImmediateContext ui)
        {
            var origin = ui.GetCursorScreenPos();
            var context = new UiControlContext(ui);
            context.Frame(new(origin.X, origin.Y, 80, 45), new(60, 160, 240, 230), default, 6, 0);
            context.Frame(new(origin.X + 10, origin.Y + 5, 22, 32), new(220, 140, 70, 180), default, 3, 0);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}

internal sealed record PresentationPixelPair(string Name, string ControlSha256, string CandidateSha256, bool Reused);
internal sealed record PresentationReuseResult(string Framework, bool Completed, string? Failure, int FrameSlots,
    int[] AcquiredImages, int Passed, string[] Checks, PresentationPixelPair[] Pairs, SparseDamageSample[] SparseDamage);
internal sealed record SparseDamageSample(int MsaaSamples, int Frame, int Image, bool Initialized,
    float X, float Y, float Width, float Height);
[JsonSerializable(typeof(PresentationReuseResult))]
internal partial class PresentationReuseJson : JsonSerializerContext;
