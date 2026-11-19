using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class OverlayPresentationTests
{
    internal static void Run(VulkanRendererBackend renderer, Func<Action<UiImmediateContext>, UiDrawData> build,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiTextureId white, UiRect bounds)
    {
        var transient = new PooledBuffer<int>();
        transient.SetCount(65_536);
        transient[0] = 456;
        transient[65_535] = 123;
        var transferred = transient.TransferToPooledList(retainCapacity: false);
        check(transient.Capacity <= 64 && transferred.Count == 65_536 && transferred[65_535] == 123,
            "transient geometry transfer preserves owned data without renting a second large scratch buffer");
        transient.Add(-1);
        check(transferred[0] == 456 && transferred[65_535] == 123,
            "small replacement scratch never aliases transferred geometry");
        transferred.Return();
        var retained = new PooledBuffer<int>();
        retained.SetCount(1024);
        var retainedData = retained.TransferToPooledList();
        check(retained.Capacity >= 1024,
            "normal frame builders retain their working capacity after ownership transfer");
        retainedData.Return();
        retained.TrimExcess();
        var resident = new PooledBuffer<int>(pooled: false);
        resident.EnsureCapacity(1030);
        resident.SetCount(1030);
        resident[1029] = 321;
        check(resident.Capacity == 1030, "known resident geometry reserves exact storage without shared-pool bucket rounding");
        var residentOwner = resident.TransferToPooledList(retainCapacity: false);
        resident.Add(-1);
        check(resident.Capacity == 16 && residentOwner[1029] == 321,
            "resident storage transfer preserves exclusive ownership with small independent scratch");
        residentOwner.Return();
        var red = new UiColor(0xFF0000FF);
        var blue = new UiColor(0xFFFF0000);
        var translucent = new UiColor(0x8000FF00);
        void Content(UiImmediateContext ui, bool overlay)
        {
            ui.GetWindowDrawList().AddRectFilled(bounds, red, white);
            var builder = overlay ? ui.GetForegroundDrawList() : ui.GetWindowDrawList();
            builder.AddRectFilled(new(20, 20, 80, 80), blue, white);
            builder.AddCircleFilled(new(100, 90), 35, translucent, white);
        }
        foreach (var samples in new[] { 1, 4 })
        {
            renderer.SetMsaaSamples(samples);
            var reference = capture($"overlay-reference-{samples}", ui => Content(ui, false));
            var actual = capture($"overlay-dynamic-{samples}", ui => Content(ui, true));
            check(actual.AsSpan().SequenceEqual(reference), $"automatic foreground preserves opaque, translucent and curved pixels at {samples}x MSAA");
            check(renderer.LastFrameMetrics.DepthAllocatedBytes == 0, $"screen presentation creates no depth attachments at {samples}x MSAA");
            for (var i = 0; i < 4; i++) capture($"overlay-warm-{samples}", ui => Content(ui, true));
            check(renderer.LastFrameMetrics.ResidentHits > 0, $"automatic foreground reuses resident GPU geometry at {samples}x MSAA");
        }
        renderer.SetMsaaSamples(1);

        void Swatches(UiImmediateContext ui, bool overlay)
        {
            var builder = overlay ? ui.GetForegroundDrawList() : ui.GetWindowDrawList();
            for (var i = 0; i < 100; i++)
                builder.AddRectFilled(new(10 + i * 1.25f, 20, .9f, 18), new((byte)(i * 7), (byte)(i * 13), (byte)(i * 23)), white);
        }
        var swatchReference = capture("overlay-swatches-reference", ui => Swatches(ui, false));
        capture("overlay-swatches-changing", ui => Swatches(ui, true));
        var swatchResident = capture("overlay-swatches-resident", ui => Swatches(ui, true));
        check(swatchReference.AsSpan().SequenceEqual(swatchResident), "resident sharp color swatches preserve the streamed image without triangle expansion");
        var compactData = build(ui => Swatches(ui, true));
        try
        {
            renderer.RenderAndReadback(compactData);
            var shape = renderer.ValidationResidentShape(compactData.DrawLists.Single(list => list.Scene3D.HasValue));
            check(shape == (0, 0, 100, false), "automatic presentation keeps sharp rectangles in compact 32-byte GPU instances");
        }
        finally { compactData.ReleasePooled(); }

        void TextCoverage(UiImmediateContext ui, bool overlay)
        {
            ui.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(1), UiTextureFormat.Rgba8Unorm, 1, 1,
                new byte[] { 255, 0, 0, 0 }));
            var builder = overlay ? ui.GetForegroundDrawList() : ui.GetWindowDrawList();
            builder.AddImage(new(1), new(20, 20), new(100, 100), default, new(1, 1), new(0xFFFFFFFF));
        }
        var clearTypeReference = capture("overlay-cleartype-reference", ui => TextCoverage(ui, false));
        var clearTypeActual = capture("overlay-cleartype-dynamic", ui => TextCoverage(ui, true));
        check(clearTypeReference.AsSpan().SequenceEqual(clearTypeActual), "screen-anchored dynamic overlays preserve native RGB subpixel coverage");

        void Palettes(UiImmediateContext ui, float firstX)
        {
            foreach (var id in new[] { "isolated-first", "isolated-second" })
            {
                ui.OpenPopup(id);
                ui.SetNextPopupPosition(new(id == "isolated-first" ? firstX : 120, 10));
                if (ui.BeginPopup(id)) ui.EndPopup();
            }
        }
        var palettes = build(ui => Palettes(ui, 10));
        var unchangedPalette = palettes.DrawLists[1];
        var movedPalettes = build(ui => Palettes(ui, 20));
        check(palettes.DrawLists.Count == 2 && ReferenceEquals(unchangedPalette, movedPalettes.DrawLists[1])
            && !ReferenceEquals(palettes.DrawLists[0], movedPalettes.DrawLists[0]),
            "moving one popup retains another popup in the same tier without rebuilding its snapshot");
        check(movedPalettes.DrawLists[0].StaticGeometryKey is null && movedPalettes.DrawLists[0].Scene3D.HasValue,
            "changing popup keeps its dynamic plane while avoiding per-move resident GPU allocation");
        var settledPalettes = build(ui => Palettes(ui, 20));
        check(settledPalettes.DrawLists[0].StaticGeometryKey is not null,
            "a popup returns to GPU residency as soon as its content becomes stable");
        palettes.ReleasePooled();
        movedPalettes.ReleasePooled();
        settledPalettes.ReleasePooled();

        var first = build(ui => ui.GetForegroundDrawList().AddRectFilled(new(20, 20, 80, 80), red, white));
        var firstList = first.DrawLists.Single(list => list.Scene3D.HasValue);
        var firstPixels = renderer.RenderAndReadback(first);
        var second = build(ui => ui.GetForegroundDrawList().AddRectFilled(new(20, 20, 80, 80), red, white));
        check(ReferenceEquals(firstList, second.DrawLists.Single(list => list.Scene3D.HasValue)), "unchanged overlay reuses its immutable CPU snapshot");
        var third = build(ui => ui.GetForegroundDrawList().AddRectFilled(new(30, 30, 90, 90), blue, white));
        check(firstList.RectFilledPrimitives!.Count > 0 && firstList.RectFilledPrimitives[0].Color == red,
            "changed overlay preserves an earlier queued frame's geometry");
        var oldAgain = renderer.RenderAndReadback(first);
        check(oldAgain.AsSpan().SequenceEqual(firstPixels), "queued overlay frame remains pixel-identical after a later content change");
        first.ReleasePooled();
        check(firstList.RectFilledPrimitives.Count > 0, "another queued reader keeps shared overlay geometry alive");
        second.ReleasePooled();
        check(firstList.RectFilledPrimitives.Count == 0, "last queued reader releases replaced overlay geometry");
        third.ReleasePooled();
        var fourth = build(_ => { });
        check(fourth.DrawLists.Count == 0 && fourth.LayerFrameLease is null, "closed overlays submit no stale geometry or source lease");
        fourth.ReleasePooled();

        var callbacks = 0;
        for (var frame = 0; frame < 2; frame++)
        {
            var callbackData = build(ui => ui.GetForegroundDrawList().AddCallback((_, _) => callbacks++));
            try { renderer.RenderAndReadback(callbackData); }
            finally { callbackData.ReleasePooled(); }
        }
        check(callbacks == 2, "foreground callbacks execute every frame instead of becoming cached side effects");

        void LegacyMinimumScope(UiImmediateContext ui, bool rear)
        {
            ui.BeginLayer("legacy-minimum-front", new(false, 1, default)
                { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .2f)) { DepthScopeId = int.MinValue } });
            ui.GetWindowDrawList().AddRectFilled(bounds, blue, white);
            ui.EndLayer();
            if (!rear) return;
            ui.BeginLayer("legacy-minimum-rear", new(false, 1, default)
                { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .8f), WriteDepth: false) { DepthScopeId = int.MinValue } });
            ui.GetWindowDrawList().AddRectFilled(bounds, red, white);
            ui.EndLayer();
        }
        var legacyMinimumReference = capture("legacy-minimum-scope-reference", ui => LegacyMinimumScope(ui, false));
        var legacyMinimumActual = capture("legacy-minimum-scope-read", ui => LegacyMinimumScope(ui, true));
        check(legacyMinimumActual.AsSpan().SequenceEqual(legacyMinimumReference),
            "existing minimum-integer depth scopes retain depth-read occlusion semantics");

        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), white);
        void PopupFrame(UiInputState input, bool open)
        {
            context.NewFrame(new(1f / 60, new(bounds.Width, bounds.Height), new(1, 1)), input, bounds, UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                if (open) ui.OpenPopup("interactive");
                if (ui.BeginPopup("interactive")) { ui.MenuItem("Action"); ui.EndPopup(); }
            }));
        }
        var neutral = new UiInputState(new(20, 20), false, false, false, false, false, false, 0, 0, [], [], default, default);
        PopupFrame(neutral, true);
        var popupFrame = context.GetDrawData();
        check(popupFrame.DrawLists.Any(list => list.Scene3D.HasValue && list.StaticGeometryKey is not null),
            "ordinary popup APIs automatically use resident dynamic planes");
        popupFrame.ReleasePooled();
        PopupFrame(neutral with { MousePosition = new(bounds.Width - 1, bounds.Height - 1), LeftMousePressed = true, LeftMouseDown = true }, false);
        var closed = context.GetDrawData();
        check(closed.DrawLists.Count == 0, "outside click still closes an automatic dynamic popup");
        closed.ReleasePooled();

        context.State.AddPopupBlockingRect(new(10, 10, 60, 60), 2);
        context.State.AddPopupBlockingRect(new(15, 15, 40, 40), 5);
        context.State.AddPopupBlockingRect(new(100, 100, 20, 20), 9);
        context.NewFrame(new(1f / 60, new(bounds.Width, bounds.Height), new(1, 1)), neutral, bounds, UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            check(ui.GetHoveredPopupTier() == 5 && ui.GetHoveredPopupTier() == 5,
                "one popup hit query retains the highest intersecting tier across repeated widget queries");
        }));
        context.GetDrawData().ReleasePooled();
        context.NewFrame(new(1f / 60, new(bounds.Width, bounds.Height), new(1, 1)), neutral with { MousePosition = new(110, 110) }, bounds, UiTextSettings.Default);
        context.Render(new Screen(ui => check(ui.GetHoveredPopupTier() == int.MinValue,
            "popup hit query resets on the next frame after blocking rectangles close")));
        context.GetDrawData().ReleasePooled();
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
