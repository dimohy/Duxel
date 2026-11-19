using System.Numerics;
using Duxel.Core;

internal static class WidgetTransformRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, UiTextureId white, Func<UiRendererFrameMetrics> metrics)
    {
        var viewport = new UiRect(0, 0, stride, 240);
        using var paint = new Paint(white);
        void Background(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(viewport, new(0, 0, 0), white, viewport);
        }
        void Draw(UiImmediateContext ui, UiWidget widget)
        {
            Background(ui);
            ui.SetCursorScreenPos(new(40, 40));
            widget.Render(ui);
        }
        static bool Red(byte[] pixels, int at) => pixels[at] == 255 && pixels[at + 1] == 0 && pixels[at + 2] == 0;
        static bool Black(byte[] pixels, int at) => pixels[at] == 0 && pixels[at + 1] == 0 && pixels[at + 2] == 0;
        int At(int x, int y) => (y * stride + x) * 4;
        paint.Transform = new(RotationDegrees: 90);
        var quarter = capture("quarter", ui => Draw(ui, paint));
        var exact = true;
        for (var y = 0; y < 240; y++)
            for (var x = 0; x < stride; x++)
                exact &= x >= 40 && x < 70 && y >= 40 && y < 120 ? Red(quarter, At(x, y)) : Black(quarter, At(x, y));
        check(exact, "GPU common quarter rotation matches the full independent solid rectangle oracle");
        paint.Transform = new(RotationDegrees: 45);
        var diagonal = capture("diagonal", ui => Draw(ui, paint));
        var root = MathF.Sqrt(2);
        var inside = true;
        var outside = true;
        var checkedInside = 0;
        var checkedOutside = 0;
        for (var y = 0; y < 150; y++)
            for (var x = 0; x < 150; x++)
            {
                var px = x + .5f - 40 - 30 / root;
                var py = y + .5f - 40;
                var u = (px + py) / root;
                var v = (-px + py) / root;
                if (u > 2 && u < 78 && v > 2 && v < 28)
                {
                    checkedInside++;
                    inside &= Red(diagonal, At(x, y));
                }
                if (u < -2 || u > 82 || v < -2 || v > 32)
                {
                    checkedOutside++;
                    outside &= Black(diagonal, At(x, y));
                }
            }
        check(checkedInside > 1000 && inside && checkedOutside > 10000 && outside,
            "GPU arbitrary widget rotation preserves solid interiors and empty bounding corners");
        paint.Transform = new(RotationDegrees: 90);
        paint.Radius = 10;
        var rounded = capture("rounded", ui => Draw(ui, paint));
        check(Black(rounded, At(40, 40)) && Red(rounded, At(55, 80)),
            "GPU widget source corners rotate with the content rather than the layout envelope");
        paint.Radius = 0;
        paint.Retained = true;
        var retained = capture("retained-first", ui => Draw(ui, paint));
        var replay = capture("retained-replay", ui => Draw(ui, paint));
        check(retained.AsSpan().SequenceEqual(replay), "GPU common transforms preserve actual static-layer replay pixels");
        paint.Transform = new(RotationDegrees: 180);
        var movedReplay = capture("retained-rotated", ui => Draw(ui, paint));
        check(Red(movedReplay, At(80, 55)) && Black(movedReplay, At(55, 100)),
            "GPU common transform changes reposition retained layer content without stale scene matrices");
        using var overflow = new Paint(white) { Size = new(80, 80), Overflow = true, Transform = new(RotationDegrees: -45, Mode: UiWidgetTransformMode.Visual) };
        using var parent = Dux.Container(overflow, new(Width: 80, Height: 80));
        parent.Transform = new(RotationDegrees: 45);
        var clipped = capture("nested-parent-clip", ui => Draw(ui, parent));
        var parentInside = true;
        var parentOutside = true;
        var parentInterior = 0;
        var parentExterior = 0;
        for (var y = 0; y < 180; y++)
            for (var x = 0; x < 180; x++)
            {
                var px = x + .5f - 40 - 80 / root;
                var py = y + .5f - 40;
                var u = (px + py) / root;
                var v = (-px + py) / root;
                if (u > 2 && u < 78 && v > 2 && v < 78)
                {
                    parentInterior++;
                    parentInside &= Red(clipped, At(x, y));
                }
                if (u < -2 || u > 82 || v < -2 || v > 82)
                {
                    parentExterior++;
                    parentOutside &= Black(clipped, At(x, y));
                }
            }
        check(parentInterior > 5000 && parentInside && parentExterior > 20000 && parentOutside,
            "GPU nested inverse rotations preserve the exact parent clip instead of its inverse axis-aligned envelope");
        parent.Transform = new(RotationDegrees: 30);
        overflow.Transform = new(RotationDegrees: -30, Mode: UiWidgetTransformMode.Visual);
        var rotatedClip = capture("nested-parent-clip-changed", ui => Draw(ui, parent));
        check(!clipped.AsSpan().SequenceEqual(rotatedClip),
            "GPU inverse child rotation does not conceal a changed parent clip");
        parent.Transform = new(RotationDegrees: 45);
        overflow.Transform = new(RotationDegrees: -45, Mode: UiWidgetTransformMode.Visual);
        for (var replayIndex = 0; replayIndex < 12; replayIndex++)
            check(clipped.AsSpan().SequenceEqual(capture("nested-parent-clip-restored-" + replayIndex, ui => Draw(ui, parent))),
                "GPU dynamic inverse-rotation clip round trip preserves acquired-image history : " + replayIndex);
        paint.Retained = false;
        paint.Overflow = true;
        paint.Transform = new(RotationDegrees: 45);
        var roundParent = capture("affine-rounded-parent", ui =>
        {
            Background(ui);
            using var mask = ui.GetWindowDrawList().RoundedClip(new(50, 50, 60, 60), 20);
            ui.SetCursorScreenPos(new(40, 40));
            paint.Render(ui);
        });
        check(Black(roundParent, At(50, 50)) && Red(roundParent, At(80, 80)) && Black(roundParent, At(45, 80)),
            "GPU affine parent masks preserve rounded clipping independently of child rotation");
        var circleParent = capture("affine-circle-parent", ui =>
        {
            Background(ui);
            using var mask = ui.GetWindowDrawList().CircleCutout(new(80, 80), 24);
            ui.SetCursorScreenPos(new(40, 40));
            paint.Render(ui);
        });
        check(Black(circleParent, At(80, 80)) && Red(circleParent, At(45, 80)) && Red(circleParent, At(80, 45)),
            "GPU affine parent masks preserve the circular cutout rather than rotating it with the child");
        var composedParent = capture("affine-composed-parent", ui =>
        {
            Background(ui);
            var list = ui.GetWindowDrawList();
            using var roundedMask = list.RoundedClip(new(50, 50, 60, 60), 20);
            using var circleMask = list.CircleCutout(new(80, 80), 10);
            ui.SetCursorScreenPos(new(40, 40));
            paint.Render(ui);
        });
        check(Black(composedParent, At(50, 50)) && Black(composedParent, At(80, 80)) && Red(composedParent, At(80, 55)),
            "GPU affine shape records compose parent links across rectangular, rounded and circle masks");
        paint.Overflow = false;
        paint.Transform = new(RotationDegrees: 90);
        var camera = capture("inherited-camera", ui =>
        {
            Background(ui);
            ui.BeginLayer("widget-transform-external-camera", new UiLayerOptions(false, 1, default)
            {
                Scene3D = new UiLayer3D(Matrix4x4.CreateTranslation(.25f, 0, 0), false)
            });
            try
            {
                ui.SetCursorScreenPos(new(40, 40));
                paint.Render(ui);
            }
            finally { ui.EndLayer(); }
        });
        check(Red(camera, At(95, 80)) && Black(camera, At(55, 80)),
            "GPU widget drawing composes with an existing parent camera exactly once");
        using var button = Fluent.Button("Rotate");
        button.Size = new(80, 30);
        button.Transform = new(RotationDegrees: 45);
        var actualButton = capture("actual-button", ui => Draw(ui, button));
        check(Black(actualButton, At(41, 41)) && !Black(actualButton, At(78, 78)),
            "GPU actual Fluent button paints inside its common rotated shape");
        check(metrics().DrawCalls > 0 && !metrics().VSyncEnabled, "GPU widget transform checks submit actual VSync-off draws");
    }

    private sealed class Paint(UiTextureId white) : UiControlWidget
    {
        internal UiVector2 Size = new(80, 30);
        internal float Radius;
        internal bool Overflow;
        internal bool Retained;
        protected override UiVector2 MeasureContent(UiControlContext context) => Size;
        protected override void RenderControl(UiImmediateContext ui)
        {
            var position = ui.GetCursorScreenPos();
            ui.Dummy(Size);
            if (Retained && !ui.BeginLayer("widget-transform-test-content", staticCache: true))
            {
                ui.EndLayer();
                return;
            }
            try
            {
                var bounds = Overflow ? new UiRect(position.X - 100, position.Y - 100, 300, 300)
                    : new UiRect(position.X, position.Y, Size.X, Size.Y);
                ui.GetWindowDrawList().AddRectFilledRounded(bounds, new(255, 0, 0), white, Radius,
                    new UiControlContext(ui).ClipRect);
            }
            finally { if (Retained) ui.EndLayer(); }
        }
    }
}
