using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class RetainedTextMaintenanceTests
{
    internal static void VerifyExtendedStaticGeometryReuse(int frameSlots, Action<bool, string> check)
    {
        using var reference = new PresentationReuseTests.Fixture(frameSlots, false, msaaSamples: 4);
        using var candidate = new PresentationReuseTests.Fixture(frameSlots, false, msaaSamples: 4);
        if (!candidate.Renderer.ValidationUsesRotatingStaticGeometry)
            return;
        reference.Context.SetDirectTextBaseFontSize(18.24f);
        candidate.Context.SetDirectTextBaseFontSize(18.24f);

        const string layerId = "static-geometry-reuse";
        string[] values = ["Frame CPU: 12.34 ms", "Frame CPU: 13.57 ms", "Frame CPU: 14.62 ms"];
        void Draw(UiImmediateContext ui, int revision, bool retained, int versionOffset = 0, int x = 16)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(x, 16));
            if (!retained)
            {
                ui.Text(values[revision]);
                return;
            }

            if (ui.BeginLayer(layerId, new(false, 1, default) { ContentVersion = revision + versionOffset }))
                ui.TextWithShortCache(values[revision]);
            ui.EndLayer();
        }

        var firstUploads = new List<UiTextureUpdate>();
        var first = candidate.Capture(ui => Draw(ui, 0, true), null, updates => firstUploads.AddRange(updates));
        check(firstUploads.Count(update => update.Kind == UiTextureUpdateKind.Create && update.Width == 256 && update.Height == 128) == 1,
            "versioned 18-pixel status geometry uses the shaped glyph atlas");
        var firstBuffer = candidate.Renderer.ValidationStaticVertexBuffer(layerId);
        check(firstBuffer != 0 && first.AsSpan().SequenceEqual(reference.Capture(ui => Draw(ui, 0, false))),
            "first resident status revision matches whole-line Vulkan pixels");
        check(candidate.Capture(ui => Draw(ui, 1, true)).AsSpan().SequenceEqual(reference.Capture(ui => Draw(ui, 1, false))),
            "changed resident status revision matches whole-line Vulkan pixels");
        check(candidate.Renderer.ValidationStaticVertexBuffer(layerId) != firstBuffer
            && candidate.Renderer.ValidationExtendedStaticGeometryReuse.Count == 1,
            "the first resident buffer stays distinct while it becomes eligible for reuse");
        var reservation = candidate.Renderer.ValidationExtendedStaticGeometryReuse;
        candidate.Renderer.ValidationPruneRetiredStaticGeometryAfterFrameGrace(elapsed: false);
        check(candidate.Renderer.ValidationExtendedStaticGeometryReuse.Count == 1,
            "small resident geometry stays available after the frame grace during the wall-clock grace");
        for (var draw = 0; draw <= frameSlots; draw++)
            check(candidate.Submit(ui => Draw(ui, 1, true)), "unchanged retained status submits while the old buffer ages");
        check(candidate.Capture(ui => Draw(ui, 2, true)).AsSpan().SequenceEqual(reference.Capture(ui => Draw(ui, 2, false)))
            && candidate.Renderer.ValidationStaticVertexBuffer(layerId) == firstBuffer,
            "an updated status reuses the original Vulkan buffer and preserves exact pixels");
        check(candidate.Renderer.ValidationExtendedStaticGeometryReuse == reservation,
            "rotating a reusable Vulkan buffer transfers its reservation to the retired buffer");
        candidate.Renderer.ValidationPruneRetiredStaticGeometryAfterFrameGrace(elapsed: true);
        check(candidate.Renderer.ValidationExtendedStaticGeometryReuse == (0, 0UL),
            "elapsed wall-clock and frame grace release the extended reuse budget");

        reference.Context.SetDirectTextBaseFontSize(16);
        candidate.Context.SetDirectTextBaseFontSize(16);
        check(candidate.Capture(ui => Draw(ui, 2, true, versionOffset: 1, x: 42)).AsSpan()
            .SequenceEqual(reference.Capture(ui => Draw(ui, 2, false, x: 42))),
            "the same atlas can render a smaller retained status at a new position");
        reference.Context.SetDirectTextBaseFontSize(18.24f);
        candidate.Context.SetDirectTextBaseFontSize(18.24f);
        check(candidate.Capture(ui => Draw(ui, 2, true, versionOffset: 2, x: 27)).AsSpan()
            .SequenceEqual(reference.Capture(ui => Draw(ui, 2, false, x: 27))),
            "returning to the 18-pixel status reuses its own masks and the current position");
    }

    internal static void VerifyExtendedStaticGeometryReuseBudget(int frameSlots, Action<bool, string> check)
    {
        void Verify(int layers, int quads, Func<(int Count, ulong Bytes), bool> expected, string description)
        {
            using var fixture = new PresentationReuseTests.Fixture(frameSlots, false);
            if (!fixture.Renderer.ValidationUsesRotatingStaticGeometry)
                return;

            var ids = Enumerable.Range(0, layers).Select(index => $"static-geometry-budget-{index}").ToArray();
            void Draw(UiImmediateContext ui, int revision)
            {
                foreach (var id in ids)
                {
                    if (ui.BeginLayer(id, new(false, 1, default) { ContentVersion = revision }))
                    {
                        var drawList = ui.GetWindowDrawList();
                        for (var index = 0; index < quads; index++)
                            drawList.AddImage(ui.WhiteTextureId, new(20 + revision, 20), new(23 + revision, 23), default,
                                new(1, 1), new(0xFFFFFFFF));
                    }
                    ui.EndLayer();
                }
            }

            check(fixture.Submit(ui => Draw(ui, 0)) && fixture.Submit(ui => Draw(ui, 1)),
                "both geometry budget revisions submit normally");
            check(expected(fixture.Renderer.ValidationExtendedStaticGeometryReuse), description);
            fixture.Renderer.ValidationPruneRetiredStaticGeometryAfterFrameGrace(elapsed: true);
            check(fixture.Renderer.ValidationExtendedStaticGeometryReuse == (0, 0UL),
                "expiring budget entries returns every reservation");
        }

        Verify(66, 1, state => state.Count == 64 && state.Bytes > 0, "the extended reuse cache admits at most 64 buffers");
        Verify(24, 128, state => state.Count == 19 && state.Bytes is > 240 * 1024 and <= 256 * 1024,
            "the extended reuse cache admits only items within the aggregate byte limit");
        Verify(1, 160, state => state == (0, 0UL), "a buffer over 16 KiB keeps the original frame-based grace");
    }

    internal static void VerifyShapedGlyphAtlas(int frameSlots, Action<bool, string> check)
    {
        using var reference = new PresentationReuseTests.Fixture(frameSlots, false);
        using var candidate = new PresentationReuseTests.Fixture(frameSlots, false);
        var values = Enumerable.Range(0, 24).Select(frame =>
            FormattableString.Invariant($"Frame CPU: {12 + frame / 10}.{(frame * 7 + 34) % 100:00} ms")).ToArray();
        var updates = new List<UiTextureUpdate>();
        byte[] Draw(PresentationReuseTests.Fixture fixture, string text, bool shaped, bool observe = false)
            => fixture.Capture(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(16, 16));
                if (shaped)
                    ui.TextWithShortCache(text);
                else
                    ui.Text(text);
            }, null, observe ? change => updates.AddRange(change) : null);

        var rendered = new List<byte[]>();
        foreach (var value in values)
        {
            var pixels = Draw(reference, value, false);
            rendered.Add(pixels);
            check(pixels.AsSpan().SequenceEqual(Draw(candidate, value, true, true)),
                "shaped numeric atlas matches direct text pixels: " + value);
        }
        var uploaded = updates.Where(update => update.TextureId.Value >= 1_100_000_000).ToArray();
        check(uploaded.Count(update => update.Kind == UiTextureUpdateKind.Create) == 1
            && uploaded.Any(update => update.Kind == UiTextureUpdateKind.Update)
            && uploaded.Select(update => update.TextureId).Distinct().Count() == 1,
            "new glyphs share a single texture and upload only missing atlas regions after creation");
        updates.Clear();
        for (var i = values.Length - 1; i >= 0; i--)
            check(rendered[i].AsSpan().SequenceEqual(Draw(candidate, values[i], true, true)),
                "known glyphs preserve exact draw output when status strings return");
        check(updates.All(update => update.RgbaPixels.IsEmpty), "known glyphs cause zero texture-pixel upload");

        var random = new Random(20261120);
        const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var foundFull = false;
        for (var attempt = 0; attempt < 24 && !foundFull; attempt++)
        {
            var text = "0 " + new string(Enumerable.Range(0, 21).Select(_ => characters[random.Next(characters.Length)]).ToArray());
            updates.Clear();
            var pressured = Draw(candidate, text, true, true);
            var creates = updates.Where(update => update.Kind == UiTextureUpdateKind.Create && update.TextureId.Value >= 1_100_000_000).ToArray();
            check(creates.All(update => update.Width != 256 || update.Height != 128), "atlas pressure does not allocate another page");
            if (creates.Length == 0)
                continue;
            check(pressured.AsSpan().SequenceEqual(Draw(reference, text, false)),
                "a run that cannot fit the atlas matches the existing DirectText raster path");
            foundFull = true;
        }
        check(foundFull, "varied glyph phases exercise the single-page atlas capacity");
        updates.Clear();
        check(rendered[0].AsSpan().SequenceEqual(Draw(candidate, values[0], true, true)) && updates.All(update => update.RgbaPixels.IsEmpty),
            "atlas pressure keeps previously published glyph coordinates and pixels valid");
    }

    internal static void VerifyShapedGlyphAtlasPacking(int frameSlots, Action<bool, string> check)
    {
        using var reference = new PresentationReuseTests.Fixture(frameSlots, false, msaaSamples: 4);
        using var candidate = new PresentationReuseTests.Fixture(frameSlots, false, msaaSamples: 4);
        reference.Context.SetDirectTextBaseFontSize(18.24f);
        candidate.Context.SetDirectTextBaseFontSize(18.24f);

        const string layerId = "glyph-atlas-packing";
        void Draw(UiImmediateContext ui, string[] values, int revision, bool retain)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var draw = !retain || ui.BeginLayer(layerId, new(false, 1, default) { ContentVersion = revision });
            if (draw)
            {
                for (var row = 0; row < values.Length; row++)
                {
                    ui.SetCursorScreenPos(new(16, 16 + row * 24));
                    if (retain)
                        ui.TextWithShortCache(values[row]);
                    else
                        ui.Text(values[row]);
                }
            }
            if (retain)
                ui.EndLayer();
        }

        var texturesCreated = new List<(int Width, int Height)>();
        for (var revision = 0; revision < 485; revision++)
        {
            string[] values =
            [
                FormattableString.Invariant($"Scene builds total: {2477 + revision * 17}"),
                FormattableString.Invariant($"Frame CPU: {12 + revision / 10}.{(revision * 7 + 34) % 100:00} ms"),
                FormattableString.Invariant($"GPU (delayed): {320 + revision / 10}.{(revision * 3 + 1) % 10} us")
            ];
            using var data = candidate.Build(ui => Draw(ui, values, revision, retain: true));
            foreach (var update in data.TextureUpdates)
                if (update.Kind == UiTextureUpdateKind.Create && update.TextureId.Value >= 1_100_000_000)
                    texturesCreated.Add((update.Width, update.Height));

            if (revision is 0 or 447 or 448 or 480 or 484)
                check(candidate.Render(data).AsSpan().SequenceEqual(reference.Capture(ui => Draw(ui, values, revision, retain: false))),
                    $"packed glyphs match the whole-line Vulkan pixels at status revision {revision}");
            else if (!candidate.RenderSubmit(data))
                throw new InvalidOperationException($"Packed status revision {revision} did not submit.");
        }

        check(texturesCreated.SequenceEqual(new[] { (256, 128) }),
            "all 485 status revisions fit one 256 by 128 glyph texture");
    }

    internal static void VerifyShortTextRetention(int frameSlots, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(frameSlots, false);
        var created = new List<UiTextureId>();
        var destroyed = new List<UiTextureId>();
        void Observe(UiPooledList<UiTextureUpdate> updates)
        {
            foreach (var update in updates)
            {
                if (update.TextureId.Value < 1_100_000_000)
                    continue;
                if (update.Kind == UiTextureUpdateKind.Create)
                    created.Add(update.TextureId);
                else if (update.Kind == UiTextureUpdateKind.Destroy)
                    destroyed.Add(update.TextureId);
            }
        }
        const string text = "가나다 Sample 123.45";
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        byte[] Draw(bool? shortCache) => fixture.Capture(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(16, 16));
            if (shortCache == true)
                ui.TextWithShortCache(text);
            else if (shortCache == false)
                ui.Text(text);
        }, idle, Observe);

        var regular = Draw(false);
        var shortened = Draw(true);
        check(created.Count == 2 && created[0] != created[1] && regular.AsSpan().SequenceEqual(shortened),
            "same text uses independent direct texture lifetimes and exact pixels in both policies");
        check(shortened.AsSpan().SequenceEqual(Draw(true)) && created.Count == 2,
            "short-cache text reuses its texture while it is still displayed");
        for (var frame = 0; frame < 23; frame++)
            Draw(null);
        check(destroyed.Count == 0, "short-cache text stays available during the idle grace period");
        for (var frame = 0; frame < 4; frame++)
            Draw(null);
        check(destroyed.SequenceEqual(new[] { created[1] }), "idle expiry retires only the short-cache texture");
        check(regular.AsSpan().SequenceEqual(Draw(false)) && created.Count == 2,
            "the normal cache still replays its original texture after short-cache expiry");
        check(shortened.AsSpan().SequenceEqual(Draw(true)) && created.Count == 3,
            "rasterizing an expired short-cache value reproduces exactly the same pixels");

        using var paint = new UiPaintGroup();
        byte[] DrawPaint() => fixture.Capture(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.PaintGroup(paint, new(0, 0, 250, 64), 1, _ =>
            {
                ui.SetCursorScreenPos(new(16, 16));
                ui.TextWithShortCache(text + " retained");
            });
        }, idle, Observe);
        var retained = DrawPaint();
        check(created.Count == 4, "paint group captures a direct texture using the short-cache policy");
        var retainedTexture = created[^1];
        for (var frame = 0; frame < 28; frame++)
            Draw(null);
        check(!destroyed.Contains(retainedTexture) && retained.AsSpan().SequenceEqual(DrawPaint()) && created.Count == 4,
            "a short-cache texture outlives idle expiry while retained paint still refers to it");
        paint.Dispose();
        Draw(null);
        check(destroyed.Contains(retainedTexture), "disposing retained paint makes its expired short-cache texture eligible for retirement");
    }

    internal static void VerifyVulkanTextTexturePool(int frameSlots, Action<bool, string> check)
    {
        const int countLimit = 2048;
        const ulong byteLimit = 16 * 1024 * 1024;
        foreach (var (width, height, label) in new[] { (1024, 128, "wide"), (1, 1, "small") })
        {
            using var fixture = new PresentationReuseTests.Fixture(frameSlots, false);
            var pixels = new byte[width * height * 4];
            Array.Fill(pixels, (byte)255);
            UiTextureId Id(int index) => new((nuint)(1_200_000_000 + index));
            void Submit() => check(fixture.Submit(static ui => ui.EnableRootViewportContentLayout(contentPadding: 0)),
                $"{label} text texture pool frame submits");
            void Update(UiTextureUpdateKind kind, int index) => fixture.Context.QueueTextureUpdate(new(kind, Id(index),
                UiTextureFormat.Rgba8Unorm, width, height, kind == UiTextureUpdateKind.Create ? pixels : ReadOnlyMemory<byte>.Empty));

            Update(UiTextureUpdateKind.Create, 0);
            Submit();
            var bytesPerImage = fixture.Renderer.ValidationTextureAllocationBytes(Id(0));
            var capacity = (int)Math.Min((ulong)countLimit, byteLimit / bytesPerImage);
            check(capacity > 0, $"{label} text image fits the idle pool budget");
            for (var i = 1; i <= capacity + 2; i++)
                Update(UiTextureUpdateKind.Create, i);
            Submit();
            for (var i = 0; i <= capacity + 2; i++)
                Update(UiTextureUpdateKind.Destroy, i);
            for (var i = 0; i <= frameSlots; i++)
                Submit();

            var full = fixture.Renderer.ValidationTextTexturePool;
            check(full == (capacity, (ulong)capacity * bytesPerImage, 0L),
                $"{label} text texture pool evicts excess retired images (count={capacity}, bytes={full.Bytes})");
            Update(UiTextureUpdateKind.Create, capacity + 3);
            Submit();
            check(fixture.Renderer.ValidationTextTexturePool == (capacity - 1, full.Bytes - bytesPerImage, 1L),
                $"{label} matching text upload reuses an image from the bounded pool");
        }
    }

    internal static void VerifyProjectedTextAtlasReplacement(
        UiContext context, Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        ControlFontFixture.Configure(context);
        context.SetDirectTextBaseFontSize(16);
        var smallAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(scale: 1);
        var largeAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(scale: 3);
        var options = new UiLayerOptions(true, 1, default)
        {
            Scene3D = new(Matrix4x4.Identity, false),
            FontAtlasIndependent = true,
            ContentVersion = 1,
        };
        var builds = 0;
        void Paint(UiImmediateContext ui)
        {
            var draw = ui.GetWindowDrawList();
            draw.PushClipRect(new(14, 12, 144, 52));
            ui.SetCursorScreenPos(new(20, 17));
            ui.TextColored(new(0xFFFFFFFF), "ERD 한글");
            ui.SetCursorScreenPos(new(20, 41));
            ui.TextColored(new(0xFF80C5FF), "column_01");
            draw.PopClipRect();
        }
        void DrawRetained(UiImmediateContext ui)
        {
            if (ui.TryReplayLayer("projected-text-atlas", options))
                return;
            if (ui.BeginLayer("projected-text-atlas", options))
            {
                builds++;
                Paint(ui);
            }
            ui.EndLayer();
        }
        void DrawFresh(UiImmediateContext ui)
        {
            if (ui.BeginLayer("projected-text-fresh", options with { StaticCache = false, ContentVersion = null }))
                Paint(ui);
            ui.EndLayer();
        }

        context.SetFontAtlas(smallAtlas);
        var empty = capture("projected-atlas-empty", _ => { });
        var original = capture("projected-atlas-before", DrawRetained);
        check(!original.AsSpan().SequenceEqual(empty), "fixed-origin projected text has visible pixels");
        check(smallAtlas.LineHeight != largeAtlas.LineHeight, "projected text atlas fixture changes atlas line height");
        context.SetFontAtlas(largeAtlas);
        var retained = capture("projected-atlas-after", DrawRetained);
        check(builds == 1, "fixed-origin projected text reuses retained geometry after atlas replacement");
        var fresh = capture("projected-atlas-fresh", DrawFresh);
        check(retained.AsSpan().SequenceEqual(original) && retained.AsSpan().SequenceEqual(fresh),
            "projected text retains exact Vulkan pixels across atlas replacement and matches fresh paint");
    }

    internal static void VerifyAtlasReplacement(
        UiContext context, Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        context.SetDirectTextEnabled(false);
        context.SetDirectTextFallbackEnabled(false);
        var initialAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(scale: 1);
        var nextAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(scale: 3);
        var texture = new UiTextureId(1);
        var independent = new UiLayerOptions(true, 1, default) { FontAtlasIndependent = true, ContentVersion = 1 };
        var dependent = independent with { FontAtlasIndependent = false };
        var independentBuilds = 0;
        var parentBuilds = 0;
        var childBuilds = 0;

        void DrawLabel(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(14, 40));
            ui.Text("Atlas change");
        }

        void DrawRectangle(UiImmediateContext ui) =>
            ui.GetWindowDrawList().AddRectFilled(new(14, 12, 60, 16), new(0xFF468DE6), ui.WhiteTextureId);

        void DrawChild(UiImmediateContext ui)
        {
            if (ui.TryReplayLayer("atlas-dependent-child", dependent))
                return;
            if (ui.BeginLayer("atlas-dependent-child", dependent))
            {
                childBuilds++;
                DrawLabel(ui);
            }
            ui.EndLayer();
        }

        void DrawRetained(UiImmediateContext ui)
        {
            if (!ui.TryReplayLayer("atlas-independent-geometry", independent))
            {
                if (ui.BeginLayer("atlas-independent-geometry", independent))
                {
                    independentBuilds++;
                    DrawRectangle(ui);
                }
                ui.EndLayer();
            }
            if (ui.TryReplayLayer("atlas-independent-parent", independent))
                return;
            if (ui.BeginLayer("atlas-independent-parent", independent))
            {
                parentBuilds++;
                DrawChild(ui);
            }
            ui.EndLayer();
        }

        context.SetFontAtlas(initialAtlas);
        capture("atlas-prime-child", ui =>
        {
            ui.QueueTextureUpdate(initialAtlas.CreateTextureUpdate(texture));
            DrawChild(ui);
        });
        var before = capture("atlas-retained-before", DrawRetained);
        check(independentBuilds == 1 && parentBuilds == 1 && childBuilds == 1,
            "cached atlas child replays into an independent parent on the first capture");

        context.SetFontAtlas(nextAtlas);
        var after = capture("atlas-retained-after", ui =>
        {
            ui.QueueTextureUpdate(nextAtlas.CreateTextureUpdate(texture, UiTextureUpdateKind.Update));
            DrawRetained(ui);
        });
        check(independentBuilds == 1, "atlas replacement preserves explicitly independent geometry");
        check(parentBuilds == 2 && childBuilds == 2, "atlas replacement rebuilds text and its retaining parent");
        check(!before.AsSpan().SequenceEqual(after), "replacing the atlas changes rendered text pixels");
        var direct = capture("atlas-direct-after", ui => { DrawRectangle(ui); DrawLabel(ui); });
        check(after.AsSpan().SequenceEqual(direct), "reused geometry and rebuilt raster text match direct GPU output");
        check(after.AsSpan().SequenceEqual(capture("atlas-retained-steady", DrawRetained)),
            "retained geometry and text remain pixel stable after atlas replacement");
    }

    internal static void VerifyPixels(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        var labels=Enumerable.Range(0,600).Select(i=>"Pinned GPU text "+i).ToArray();
        long version=1;var builds=0;
        void Content(UiImmediateContext ui)
        {
            for(var i=0;i<labels.Length;i++)
            {ui.SetCursorScreenPos(new(20,i==0?20:60));ui.Text(labels[i]);}
        }
        void Retained(UiImmediateContext ui)
        {
            if(ui.BeginLayer("maintenance-parent",new(true,1,default){ContentVersion=version}))
            {
                if(ui.BeginLayer("maintenance-child",new(true,1,default){ContentVersion=version}))
                {builds++;Content(ui);}
                ui.EndLayer();
            }
            ui.EndLayer();
        }
        var reference=capture("cache-direct-reference",Content);
        var retained=capture("cache-retained",Retained);
        check(reference.AsSpan().SequenceEqual(retained),"600 nested retained text textures match direct repaint beyond the512-entry bound");
        for(var i=0;i<4;i++)
            check(retained.AsSpan().SequenceEqual(capture("cache-idle-"+i,Retained)),"unchanged nested text replay preserves every pixel across frame slots, phase "+i);
        check(builds==1,"unchanged nested GPU text content builds once");
        capture("cache-pressure",ui=>{Retained(ui);ui.SetCursorScreenPos(new(20,100));ui.Text("Unretained GPU pressure");});
        check(retained.AsSpan().SequenceEqual(capture("cache-after-pressure",Retained)),"evicting a newly added unretained texture preserves all pinned GPU pixels");
        labels[0]="Changed visible text";version++;
        var changed=capture("cache-changed",Retained);
        check(!retained.AsSpan().SequenceEqual(changed),"retained text mutation changes actual visible GPU pixels");
        var directChanged=capture("cache-changed-reference",Content);
        check(changed.AsSpan().SequenceEqual(directChanged),"dependency replacement matches direct repaint while nested parent retains child textures");
        capture("cache-remove-parent",ui=>{ui.RemoveLayer("maintenance-parent");});
        check(changed.AsSpan().SequenceEqual(capture("cache-child-survives",ui=>
        {if(ui.BeginLayer("maintenance-child",new(true,1,default){ContentVersion=version}))Content(ui);ui.EndLayer();})),
            "removing one owner preserves child-owned text textures");
        capture("cache-remove-child",ui=>ui.RemoveLayer("maintenance-child"));
        check(changed.AsSpan().SequenceEqual(capture("cache-after-release",Content)),"released text textures recreate without pixel loss");
    }

    internal static void Measure(string output)
    {
        const int count=2048,samples=1000;
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);context.SetDirectTextBaseFontSize(14);
        var labels=Enumerable.Range(0,count).Select(i=>"Retained text "+i).ToArray();
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        UiImmediateContext? current=null;var builds=0;var visible=true;long version=1;string? transient=null;
        var screen=new Screen(ui=>
        {
            current=ui;
            if(!visible){ui.RemoveLayer("text-maintenance");return;}
            if(ui.BeginLayer("text-maintenance",new(true,1,default){ContentVersion=version}))
            {
                builds++;
                foreach(var label in labels){ui.SetCursorScreenPos(new(20,20));ui.Text(label);}
            }
            ui.EndLayer();
            if(transient is not null){ui.SetCursorScreenPos(new(20,20));ui.Text(transient);}
        });
        void Begin()=>context.NewFrame(new(1f/60,new(320,240),new(1,1)),idle,new(0,0,320,240),UiTextSettings.Default);
        void End(){context.Render(screen);using var data=context.GetDrawData();}
        for(var i=0;i<96;i++){Begin();End();}
        var occupancy=current!.DirectTextCacheOccupancy;
        if(occupancy.Entries!=count||occupancy.Retained!=count||occupancy.Limit>=count||builds!=1)
            throw new InvalidOperationException($"Expected one retained raster-text build above the ordinary cache limit: {occupancy};builds={builds}");
        var ticks=new long[samples];var allocations=new long[samples];
        var gcBefore=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
        using var process=Process.GetCurrentProcess();process.Refresh();var privateBefore=process.PrivateMemorySize64;
        for(var i=0;i<samples;i++)
        {
            Begin();
            var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
            context.Render(screen);ticks[i]=Stopwatch.GetTimestamp()-start;allocations[i]=GC.GetAllocatedBytesForCurrentThread()-bytes;
            using var data=context.GetDrawData();
        }
        process.Refresh();var privateAfter=process.PrivateMemorySize64;
        var after=current.DirectTextCacheOccupancy;
        if(after.Entries!=count||after.Retained!=count||builds!=1)throw new InvalidOperationException("Replay lost a text dependency or rebuilt unchanged content.");
        var gcAfter=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
        var measuredBuilds=builds;
        transient="Unretained transient";Begin();End();transient=null;Begin();End();
        Begin();
        End();
        if(current.DirectTextCacheOccupancy.Entries!=count)throw new InvalidOperationException("A newly added unretained key escaped the cache bound.");
        labels[0]="Changed retained text";version++;Begin();End();Begin();End();
        if(builds!=2||current.DirectTextCacheOccupancy.Entries!=count||current.DirectTextCacheOccupancy.Retained!=count)
            throw new InvalidOperationException("Replacing a layer dependency did not release the previous text key.");
        visible=false;Begin();End();Begin();End();
        var released=current.DirectTextCacheOccupancy;
        if(released.Retained!=0||released.Entries>released.Limit)throw new InvalidOperationException("Removing the layer did not restore the ordinary cache bound.");
        for(var i=0;i<602;i++){Begin();End();}
        if(current.DirectTextCacheOccupancy.Entries!=0)throw new InvalidOperationException("Unretained idle text did not expire.");

        File.WriteAllLines(Path.Combine(output,"samples.csv"),new[]{"renderTicks,allocatedBytes"}.Concat(ticks.Select((t,i)=>$"{t},{allocations[i]}")));
        var result=new RetainedTextMaintenanceResult(RuntimeInformation.FrameworkDescription,count,samples,96,Stopwatch.Frequency,
            allocations.Sum(),allocations.Min(),allocations.Max(),ticks,measuredBuilds,gcBefore,gcAfter,privateBefore,privateAfter,
            "Actual Windows text backend;fixed retained scene;CPU Render phase including cache maintenance;no GPU presentation or app-FPS claim;no forcedGC;new unretained key,dependency replacement,release and idle expiry verified.");
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,RetainedTextMaintenanceJson.Default.RetainedTextMaintenanceResult));
        Console.WriteLine($"Retained={count};frames={samples};CPU render allocation={result.AllocatedBytes}B;range={result.MinAllocation}..{result.MaxAllocation};measured builds={measuredBuilds}");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}
internal sealed record RetainedTextMaintenanceResult(string Framework,int TextEntries,int Samples,int WarmFrames,long Frequency,
    long AllocatedBytes,long MinAllocation,long MaxAllocation,long[] RenderTicks,int ContentBuilds,int[] GcBefore,int[] GcAfter,
    long PrivateBefore,long PrivateAfter,string Scope);
[JsonSerializable(typeof(RetainedTextMaintenanceResult))]
[JsonSourceGenerationOptions(WriteIndented=true)]
internal partial class RetainedTextMaintenanceJson:JsonSerializerContext;
