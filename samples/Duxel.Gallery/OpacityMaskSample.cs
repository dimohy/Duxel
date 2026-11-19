using Duxel.Core;

namespace Duxel.Gallery;

/// <summary>Owns a cached source; controls change only its output mask, camera and rear surface.</summary>
internal sealed class OpacityMaskSample : IDisposable
{
    private readonly UiPaintGroup _group;
    private readonly UiRect _sourceBounds = new(0, 0, 640, 96);
    private UiHorizontalOpacityMask _mask = new(.25f, .25f);
    private UiImageUvTransform _uv = UiImageUvTransform.Identity;
    private ulong _version = 1;
    private bool _light;
    private int _captureBuilds;
    internal UiWidget View { get; }
    internal IGallerySample Sample { get; }
    internal UiRect Bounds { get; private set; }
    internal int CaptureBuilds => _captureBuilds;
    internal UiHorizontalOpacityMask Mask { get => _mask; set => _mask = value.Validate(); }
    internal UiImageUvTransform Camera { get => _uv; set => _uv = value.Validate(); }
    internal bool LightBackground { get => _light; set => _light = value; }
    internal void InvalidateContent() => _version++;

    internal OpacityMaskSample()
    {
        var group = GallerySample.Define("_group", () => new UiPaintGroup());
        _group = group.Value;
        var content = GallerySample.Define<Action<UiControlContext>>("PaintContent", () => (UiControlContext paint) =>
        {
            _captureBuilds++;
            paint.Frame(_sourceBounds, new(20, 80, 145, 180), default, 12, 0);
            paint.Frame(new(140, 8, 220, 80), new(0, 125, 90), default, 8, 0);
            using var typography = paint.BeginTypography<UiRegularTypeface, UiFluentType300>();
            paint.Text("Duxel · 한글 · Alpha", new(20, 32), new(255, 255, 255));
            paint.Text("Cached source", new(380, 32), new(255, 255, 255));
        });
        var PaintContent = content.Value;
        var drawing = GallerySample.Define<Action<UiImmediateContext>>("Draw", () => (UiImmediateContext ui) =>
        {
            var origin = ui.GetCursorScreenPos();
            var bounds = new UiRect(origin.X, origin.Y, MathF.Min(640, ui.GetContentRegionAvail().X), 96);
            var draw = ui.GetWindowDrawList();
            var dark = _light ? new UiColor(190, 190, 190) : new(20, 20, 20);
            var light = _light ? new UiColor(245, 245, 245) : new(65, 65, 65);
            for (var x = 0f; x < bounds.Width; x += 32)
                draw.AddRectFilled(new(bounds.X + x, bounds.Y, MathF.Min(32, bounds.Width - x), bounds.Height),
                    ((int)(x / 32) & 1) == 0 ? dark : light, ui.WhiteTextureId);
            ui.PushClipRect(bounds, true);
            ui.PaintGroup(_group, _sourceBounds, _version, PaintContent, _uv, _mask,
                destination : bounds, textMode : UiPaintGroupTextMode.Projected);
            ui.PopClipRect();
            ui.Dummy(new(bounds.Width, bounds.Height));
        });
        var Draw = drawing.Value;
        Action<UiImmediateContext> render = ui =>
        {
            Bounds = new(ui.GetCursorScreenPos().X, ui.GetCursorScreenPos().Y, MathF.Min(640, ui.GetContentRegionAvail().X), 96);
            Draw(ui);
        };
        var sample = GallerySample.Create(() => Dux.SizedBox(new UiViewHost(new UiView(render))
            { AccessibleName = "양끝 투명 마스크와 캐시 카메라" }, width : 640, height : 96));
        var factory = sample.Source.Replace("new UiView(render)", "new UiView(Draw)", StringComparison.Ordinal);
        sample.BindSource(() => (_mask, _uv, _light, _version), state =>
            "using " + group.Source + "\nvar _sourceBounds = new UiRect(0, 0, 640, 96);\nulong _version = "
            + state._version.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "UL; // Increment when content or font settings change.\nvar _captureBuilds = 0;\n"
            + "var _mask = new UiHorizontalOpacityMask(" + GallerySample.Number(state._mask.LeftFadeFraction) + ", "
            + GallerySample.Number(state._mask.RightFadeFraction) + ");\n"
            + "var _uv = new UiImageUvTransform(new(" + GallerySample.Number(state._uv.Origin.X) + ", " + GallerySample.Number(state._uv.Origin.Y)
            + "), new(" + GallerySample.Number(state._uv.AxisX.X) + ", " + GallerySample.Number(state._uv.AxisX.Y)
            + "), new(" + GallerySample.Number(state._uv.AxisY.X) + ", " + GallerySample.Number(state._uv.AxisY.Y) + "));\n"
            + "var _light = " + GallerySample.Boolean(state._light) + ";\n\n" + content.Source + "\n\n" + drawing.Source + "\n\n" + factory);
        Sample = sample;
        View = Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Subtitle2("양끝 투명 마스크와 캐시 카메라"),
            Fluent.Body1("배경을 바꾸거나 확대해도 이전에 준비한 내용을 재사용합니다. 양끝은 배경색으로 덮지 않고 실제로 투명해집니다."),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button("양끝", () => Mask = new(.25f, .25f)),
                Fluent.Button("왼쪽", () => Mask = new(.25f, 0)),
                Fluent.Button("오른쪽", () => Mask = new(0, .25f)),
                Fluent.Button("마스크 없음", () => Mask = default),
                Fluent.Button("배경 전환", () => _light = !_light),
                Fluent.Button("카메라 확대 전환", () => Camera = _uv == UiImageUvTransform.Identity
                    ? new(new(.25f, .25f), new(.5f, 0), new(0, .5f)) : UiImageUvTransform.Identity),
                Fluent.Button("내용 다시 준비", InvalidateContent)),
            Fluent.Caption1(() => "내용 준비 횟수 : " + _captureBuilds), sample);
    }
    public void Dispose()
    {
        View.Dispose();
        _group.Dispose();
    }
}
