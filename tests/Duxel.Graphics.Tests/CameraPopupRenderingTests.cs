using System.Numerics;
using Duxel.Core;

internal static class CameraPopupRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int width)
    {
        const string text = "Camera tooltip";
        var reference = capture("camera-tooltip-reference", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetTooltip(text);
        });
        var cameras = new[] { Matrix4x4.CreateTranslation(.25f, 0, 0), Matrix4x4.CreateRotationZ(.3f) };
        for (var camera = 0; camera < cameras.Length; camera++)
            foreach (var nested in new[] { false, true })
                foreach (var transformed in new[] { false, true })
                {
                    using var widget = new Target(text) { Transform = new(RotationDegrees: 90) };
                    var actual = capture($"camera-tooltip-{camera}-{nested}-{transformed}", ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        ui.BeginLayer("camera-tooltip-parent", new UiLayerOptions(false, 1, default) { Scene3D = new(cameras[camera], false) });
                        try
                        {
                            if (nested)
                                ui.BeginLayer("camera-tooltip-inner", new UiLayerOptions(false, 1, default));
                            try
                            {
                                if (transformed)
                                    widget.Render(ui);
                                else
                                    ui.SetTooltip(text);
                            }
                            finally { if (nested) ui.EndLayer(); }
                        }
                        finally { ui.EndLayer(); }
                    });
                    check(reference.AsSpan().SequenceEqual(actual),
                        $"screen Tooltip exactly matches ordinary pixels under a parent camera : camera={camera}, nested={nested}, widget={transformed}");
                }
        var body = capture("camera-body-control", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.BeginLayer("camera-body-control", new UiLayerOptions(false, 1, default) { Scene3D = new(cameras[0], false) });
            try
            {
                new UiControlContext(ui).Frame(new(24, 140, 30, 20), new(0, 0, 255), default, 0, 0);
            }
            finally { ui.EndLayer(); }
        });
        var shift = (int)(width * .125f);
        check(body[(150 * width + 30) * 4 + 2] == 0 && body[(150 * width + 30 + shift) * 4 + 2] == 255,
            "parent camera still transforms ordinary content independently of its screen popup");
        var rebuilt = false;
        var versioned = false;
        void Retained(UiImmediateContext ui, bool popup, string cacheId)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.BeginLayer("camera-retained-parent", new UiLayerOptions(false, 1, default) { Scene3D = new(cameras[0], false) });
            try
            {
                if (popup)
                {
                    ui.OpenPopup("camera-retained-popup");
                    ui.SetNextPopupPosition(new(80, 60));
                    if (!ui.BeginPopup("camera-retained-popup"))
                        throw new InvalidOperationException("Expected open test popup.");
                }
                try
                {
                    // Keep literal content coordinates and text identical; only the parent text mode changes.
                    ui.SetCursorScreenPos(new(100, 100));
                    rebuilt = ui.BeginLayer(cacheId, new UiLayerOptions(!versioned, 1, default) { ContentVersion = versioned ? 1 : null });
                    try
                    {
                        if (rebuilt)
                            ui.Text("Retained camera");
                    }
                    finally { ui.EndLayer(); }
                }
                finally { if (popup) ui.EndPopup(); }
            }
            finally { ui.EndLayer(); }
        }
        foreach (var withVersion in new[] { false, true })
        {
            versioned = withVersion;
            var cacheId = "camera-mode-switch-" + versioned;
            var beforeMode = capture("camera-cache-vector-" + versioned, ui => Retained(ui, false, cacheId));
            var popupReference = capture("camera-cache-popup-reference-" + versioned, ui => Retained(ui, true, "camera-mode-reference-" + versioned));
            var popupActual = capture("camera-cache-popup-reused-" + versioned, ui => Retained(ui, true, cacheId));
            check(rebuilt && popupReference.AsSpan().SequenceEqual(popupActual),
                "retained layer rebuilds when a screen portal changes its inherited text mode : versioned=" + versioned);
            var popupReplay = capture("camera-cache-popup-stable-" + versioned, ui => Retained(ui, true, cacheId));
            check(!rebuilt && popupReference.AsSpan().SequenceEqual(popupReplay),
                "retained screen text reuses unchanged geometry after the mode transition : versioned=" + versioned);
            var restored = capture("camera-cache-vector-restored-" + versioned, ui => Retained(ui, false, cacheId));
            check(rebuilt && beforeMode.AsSpan().SequenceEqual(restored),
                "retained text returning to its parent camera restores exact vector pixels : versioned=" + versioned);
        }
    }

    private sealed class Target(string text) : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => new(80, 30);
        protected override void RenderControl(UiImmediateContext ui) => ui.SetTooltip(text);
    }
}
