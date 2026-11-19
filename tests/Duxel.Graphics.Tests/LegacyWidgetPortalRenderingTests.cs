using Duxel.Core;

internal static class LegacyWidgetPortalRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int width)
    {
        using var owner = new Owner { Transform = new(RotationDegrees: 90) };
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(40, 40));
            owner.Render(ui);
        }
        var pixels = capture("legacy-popup", Draw);
        bool Red(int x, int y) => pixels[(y * width + x) * 4] == 255 && pixels[(y * width + x) * 4 + 1] == 0;
        check(Red(162, 152) && Red(217, 167) && !Red(162, 177),
            "GPU legacy popup paints upright content in explicit display coordinates under a rotated owner");
        for (var frame = 0; frame < 12; frame++)
            check(pixels.AsSpan().SequenceEqual(capture("legacy-popup-replay-" + frame, Draw)),
                "GPU legacy popup preserves acquired-image replay pixels : " + frame);
        owner.Tooltip = true;
        var reference = capture("legacy-tooltip-reference", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetTooltip("Legacy tip");
        });
        check(reference.AsSpan().SequenceEqual(capture("legacy-tooltip-transformed-owner", Draw)),
            "GPU SetTooltip under a rotated owner exactly matches the ordinary screen tooltip");
        using var recovery = new RecoveryOwner { Transform = new(RotationDegrees: 90) };
        void Recovery(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(40, 40));
            recovery.Render(ui);
        }
        var beforeError = capture("legacy-recovery-before", Recovery);
        recovery.Fail = true;
        var caught = false;
        try
        {
            capture("legacy-recovery-interrupted", Recovery);
        }
        catch (InterruptedPopupException) { caught = true; }
        recovery.Fail = false;
        check(caught && beforeError.AsSpan().SequenceEqual(capture("legacy-recovery-after", Recovery)),
            "interrupted popup cannot leak its screen text mode into the next transformed frame");
    }
    private sealed class Owner : UiControlWidget
    {
        private bool _opened;
        internal bool Tooltip;
        protected override UiVector2 MeasureContent(UiControlContext context) => new(80, 30);
        protected override void RenderControl(UiImmediateContext ui)
        {
            ui.Dummy(new(80, 30));
            if (Tooltip)
            {
                ui.SetTooltip("Legacy tip");
                return;
            }
            if (!_opened)
            {
                ui.OpenPopup("native-legacy-portal");
                _opened = true;
            }
            ui.SetNextPopupPosition(new(100, 80));
            if (ui.BeginPopup("native-legacy-portal"))
            {
                new UiControlContext(ui).Frame(new(160, 150, 60, 20), new(255, 0, 0), default, 0, 0);
                ui.EndPopup();
            }
        }
    }
    private sealed class InterruptedPopupException : Exception;
    private sealed class RecoveryOwner : UiControlWidget
    {
        internal bool Fail;
        protected override UiVector2 MeasureContent(UiControlContext context) => new(80, 30);
        protected override void RenderControl(UiImmediateContext ui)
        {
            if (Fail)
            {
                ui.OpenPopup("interrupted-screen-portal");
                if (ui.BeginPopup("interrupted-screen-portal"))
                    throw new InterruptedPopupException();
            }
            ui.Text("Recovery");
        }
    }
}
