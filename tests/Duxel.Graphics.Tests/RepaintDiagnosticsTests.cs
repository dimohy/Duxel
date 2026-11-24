using System.Diagnostics;
using System.Numerics;
using Duxel.Core;

internal static class RepaintDiagnosticsTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiTextureId white, PlatformSize framebuffer, Func<UiRendererFrameMetrics> metrics, Action<bool> cleanupMode,
        Action forceFullRepair, Func<long> borderPixels)
    {
        var stride = framebuffer.Width;
        var viewport = new UiRect(0, 0, stride, framebuffer.Height);
        var color = new UiColor(255, 0, 0);
        var enabled = false;
        var projected = false;
        var textured = false;
        var caret = false;
        var hovered = false;
        var texture = new UiTextureId(98_000_001);
        UiTextureUpdate? upload = null;
        void Draw(UiImmediateContext ui)
        {
            ui.SetRepaintHighlights(enabled);
            if (upload is { } update)
            {
                ui.QueueTextureUpdate(update);
                upload = null;
            }
            var list = ui.GetWindowDrawList();
            list.SetLayerScene3D(projected ? new(Matrix4x4.CreateRotationZ(.1f), false) : null);
            list.AddRectFilled(viewport, new(0, 0, 0), white, viewport);
            if (hovered)
                list.AddRectFilledRounded(new(12, 8, 60, 20), new(80, 80, 80), white, 4, new(12, 8, 60, 20));
            var changing = new UiRect(60, 60, 40, 30);
            list.AddRectFilled(changing, color, white, changing);
            if (caret)
                list.AddImage(white, new(150, 70), new(151, 86), new(0, 0), new(1, 1), new(255, 255, 255), new(150, 70, 1, 16));
            if (textured)
                list.AddImage(texture, new(180, 60), new(220, 90), new(0, 0), new(1, 1), new(255, 255, 255), new(180, 60, 40, 30));
        }
        var reference = capture("reference", Draw);
        for (var i = 0; i < 12; i++)
            capture("warmup-" + i, Draw);
        check(metrics().RepaintBounds == default && metrics().DrawCalls == 0,
            "identical scene settles to no actual content repair or GPU draws");
        enabled = true;
        color = new(0, 0, 255);
        var highlighted = capture("changed", Draw);
        var changed = metrics();
        var bounds = changed.RepaintBounds;
        check(bounds.Width > 0 && bounds.Width < stride && bounds.Height < framebuffer.Height && changed.RepaintHighlightActive,
            "repaint highlight reports actual acquired-image repair rather than a simulated widget state : " + bounds);
        var green = 0;
        var minX = stride;
        var minY = framebuffer.Height;
        var maxX = -1;
        var maxY = -1;
        for (var i = 0; i < highlighted.Length; i += 4)
            if (highlighted[i + 1] == 255 && highlighted[i] < 40 && highlighted[i + 2] < 100)
            {
                green++;
                var x = i / 4 % stride;
                var y = i / 4 / stride;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        check(green > 50 && highlighted[(75 * stride + 80) * 4 + 2] == 255,
            "native diagnostic outline flashes while preserving changed content pixels");
        check(new UiRect(minX, minY, maxX - minX + 1, maxY - minY + 1) == bounds,
            "green outline pixels enclose exactly the actual Vulkan repair rectangle");
        for (var i = 0; i < 12; i++)
            capture("settle-" + i, Draw);
        check(metrics().RepaintBounds == default && metrics().DrawCalls == 0,
            "once every image has the current outline, unchanged frames neither repaint the scene nor redraw the outline");
        using var stateContext = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        stateContext.State.RendererFrameMetrics = metrics();
        check(stateContext.State.GetScheduledFrameWaitMilliseconds(0) is >= 0 and <= 200,
            "event-wait loop sleeps to the real renderer diagnostic expiry without a continuous animation");
        Thread.Sleep(220);
        byte[] clean = [];
        for (var i = 0; i < 12; i++)
            clean = capture("expired-" + i, Draw);
        check(!metrics().RepaintHighlightActive && metrics().RepaintBounds == default && metrics().DrawCalls == 0,
            "diagnostic expiry cleans all acquired images then returns to zero draws");
        enabled = false;
        var without = capture("disabled", Draw);
        check(clean.AsSpan().SequenceEqual(without), "diagnostic cleanup restores exact normal renderer pixels");
        stateContext.State.RendererFrameMetrics = metrics();
        check(stateContext.State.GetScheduledFrameWaitMilliseconds(0) == Timeout.Infinite,
            "finished diagnostics leave no idle wakeup deadline");
        color = new(255, 0, 0);
        var restored = capture("restored", Draw);
        check(reference.AsSpan().SequenceEqual(restored), "content changes and diagnostic toggles preserve exact original pixels");
        textured = true;
        upload = new(UiTextureUpdateKind.Create, texture, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 0, 0, 255 });
        for (var i = 0; i < 12; i++)
            capture("texture-warm-" + i, Draw);
        upload = new(UiTextureUpdateKind.Update, texture, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 0, 0, 255, 255 });
        var pixels = capture("texture-updated", Draw);
        check(metrics().RepaintBounds.Width <= 44 && metrics().RepaintBounds.Height <= 34 && pixels[(75 * stride + 200) * 4 + 2] == 255,
            "same-geometry texture updates repair their actual sampling region without invalidating the full scene");
        for (var i = 0; i < 12; i++)
            capture("texture-updated-settle-" + i, Draw);
        var unused = new UiTextureId(98_000_002);
        upload = new(UiTextureUpdateKind.Create, unused, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 0, 255, 0, 255 });
        capture("unused-created", Draw);
        check(metrics().RepaintBounds == default && metrics().DrawCalls == 0,
            "creating an unreferenced texture does not repaint unrelated screen content");
        upload = new(UiTextureUpdateKind.Destroy, unused, UiTextureFormat.Rgba8Unorm, 1, 1, ReadOnlyMemory<byte>.Empty);
        capture("unused-destroyed", Draw);
        check(metrics().RepaintBounds == default && metrics().DrawCalls == 0,
            "retiring an unreferenced texture does not invalidate unrelated acquired-image snapshots");
        caret = true;
        capture("caret-added", Draw);
        check(metrics().RepaintBounds == new UiRect(148, 68, 5, 20),
            "caret insertion repairs only its coverage and preserves following rebased indexed geometry");
        for (var i = 0; i < 12; i++)
            capture("caret-settle-" + i, Draw);
        caret = false;
        capture("caret-removed", Draw);
        check(metrics().RepaintBounds == new UiRect(148, 68, 5, 20),
            "caret removal does not mistake the unchanged command suffix for modified content");
        for (var i = 0; i < 12; i++)
            capture("caret-off-settle-" + i, Draw);
        hovered = true;
        capture("hover-added", Draw);
        check(metrics().RepaintBounds == new UiRect(10, 6, 64, 24),
            "hover decoration insertion leaves later panels and primitive stream offsets outside damage");
        for (var i = 0; i < 12; i++)
            capture("hover-settle-" + i, Draw);
        hovered = false;
        capture("hover-removed", Draw);
        check(metrics().RepaintBounds == new UiRect(10, 6, 64, 24),
            "hover decoration removal clears only its former visible coverage");
        enabled = true;
        forceFullRepair();
        var fullOutline = capture("full-outline", Draw);
        check(metrics().RepaintBounds == viewport && fullOutline[1] == 255,
            "a genuine whole-image repair produces an outline around the entire framebuffer");
        color = new(0, 0, 255);
        var borderRepaired = false;
        for (var i = 0; i < 8; i++)
        {
            var localOutline = capture("remove-full-outline-" + i, Draw);
            if (!metrics().RepaintDiagnosticRedraw || localOutline[1] == 255)
                continue;
            borderRepaired = true;
            check(borderPixels() <= 8L * (stride + framebuffer.Height) && localOutline[(75 * stride + 80) * 4 + 2] == 255,
                "restoring a whole-window outline redraws thin border strips and preserves the changed scene pixels");
        }
        check(borderRepaired, "at least one acquired image restores the previous whole-window outline");
        enabled = projected = true;
        capture("conservative-scene", Draw);
        Thread.Sleep(220);
        cleanupMode(true);
        try
        {
            capture("conservative-cleanup", Draw);
        }
        finally { cleanupMode(false); }
        stateContext.State.RendererFrameMetrics = metrics();
        check(!metrics().RepaintHighlightActive && stateContext.State.GetScheduledFrameWaitMilliseconds(0) == Timeout.Infinite,
            "diagnostic-only expiry does not restart its pulse when conservative projected scenes require a full repaint");

        var pointerX = 10f;
        const float left = 96;
        const float width = Duxel.App.DuxelCaptionButtonRenderer.ButtonWidth;
        void DrawCaption(UiImmediateContext ui)
        {
            ui.SetRepaintHighlights(false);
            foreach (var button in Enum.GetValues<Duxel.App.DuxelCaptionButtonKind>())
            {
                var x = left + (int)button * width;
                Duxel.App.DuxelCaptionButtonRenderer.Draw(ui, new(x, 0, width, 28), button,
                    isMaximized : false, hovered : pointerX >= x && pointerX < x + width, pressed : false);
            }
        }
        for (var i = 0; i < 12; i++) capture("caption-rest-" + i, DrawCaption);
        pointerX = 18;
        capture("caption-pointer-moves-outside", DrawCaption);
        check(metrics().RepaintBounds == default && metrics().DrawCalls == 0,
            "moving outside the caption buttons draws none of their glyphs");
        pointerX = left + width * 2 + 12;
        var hoveredClose = capture("caption-close-hover", DrawCaption);
        check(hoveredClose.AsSpan((stride + 193) * 4, 3).SequenceEqual(new byte[] { 232, 17, 35 })
            && hoveredClose.AsSpan((26 * stride + 238) * 4, 3).SequenceEqual(new byte[] { 232, 17, 35 }),
            "Close hover fills opposite corners of its caption button cell");
        var close = metrics().RepaintBounds;
        check(close.Width > 0 && close.X >= left + width * 2 - 2 && close.X + close.Width <= left + width * 3 + 2,
            "hovering Close repairs only the Close button : " + close);
        for (var i = 0; i < 12; i++) capture("caption-close-settle-" + i, DrawCaption);
        pointerX += 5;
        capture("caption-pointer-moves-within-close", DrawCaption);
        check(metrics().RepaintBounds == default && metrics().DrawCalls == 0,
            "moving inside the same caption button performs zero content redraws");
        pointerX = left + width + 10;
        capture("caption-close-to-maximize", DrawCaption);
        var transfer = metrics().RepaintBounds;
        check(transfer.Width > 0 && transfer.X >= left + width - 2 && transfer.X + transfer.Width <= left + width * 3 + 2,
            "moving between two caption buttons leaves Minimize unchanged : " + transfer);
    }
}
