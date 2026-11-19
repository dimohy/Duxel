using System.Numerics;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class RenderingPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiWidget _root;
    private readonly OpacityMaskSample _opacityMask;
    private readonly UiBackdropImage _backdropPreview;
    private readonly UiPaintGroup _projectedTextGroup, _runtimeGroup;
    private readonly Action<UiControlContext> _projectedTextPaint, _runtimePaint;
    private UiRect _projectedTextSourceBounds, _runtimeBounds, _lastBounds, _lastProjectedBounds;
    private long _revision = 1, _drawnStaticRevision = -1, _drawnProjectedRevision = -1, _staticVersion, _projectedVersion;
    private bool _animate, _circleCutout, _roundedClip;
    private readonly UiFluentButton _roundedClipButton, _cutoutButton;
    private int _staticBuilds;
    internal UiRect ProjectedTextBounds { get; private set; }
    internal UiRect RuntimePaintGroupBounds => _runtimeBounds;
    internal bool RoundedClipEnabled => _roundedClip;
    internal bool CircleCutoutEnabled => _circleCutout;
    internal int StaticBuilds => _staticBuilds;
    internal UiRect StaticBounds => _lastBounds;
    internal UiRect BackdropPreviewBounds { get; private set; }
    public IReadOnlyList<IGallerySample> Samples { get; }
    public string Title => "렌더링";
    public string Description => "바뀌지 않는 도형을 재사용하는 방식과 기본 도형 그리기, 독립적인 3D 투영을 살펴보세요. 원형으로 자르거나 모서리를 둥글게 자른 모습, 투명한 간격도 비교할 수 있습니다. 애니메이션을 켰을 때만 프레임을 요청합니다.";
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal RenderingPage()
    {
        var backdrop = GallerySample.Define("_backdropPreview", () => new UiBackdropImage(
            new(new(1, 1, [128, 0, 0, 0])), new(new(1, 1, [128, 127, 255, 255]))));
        _backdropPreview = backdrop.Value;
        var runtimeGroup = GallerySample.Define("_runtimeGroup", () => new UiPaintGroup());
        var textGroup = GallerySample.Define("_projectedTextGroup", () => new UiPaintGroup());
        _runtimeGroup = runtimeGroup.Value;
        _projectedTextGroup = textGroup.Value;
        var textPaint = GallerySample.Define<Action<UiControlContext>>("_projectedTextPaint", () => (UiControlContext paint) =>
        {
            using var textStyle = paint.BeginTypography<UiRegularTypeface, UiFluentType300>();
            paint.Text("한글 Aa", new(_projectedTextSourceBounds.X + 2, _projectedTextSourceBounds.Y + 2), new(255, 255, 255));
        });
        _projectedTextPaint = textPaint.Value;
        var runtimePaint = GallerySample.Define<Action<UiControlContext>>("_runtimePaint", () => (UiControlContext paint) =>
        {
            using var clip = _roundedClip ? paint.RoundedClip(new(_runtimeBounds.X + 4, _runtimeBounds.Y + 4, 72, 40), 12) : default;
            paint.Frame(_runtimeBounds, new(255, 0, 0), default, 0, 0);
            paint.Frame(new(_runtimeBounds.X + 20, _runtimeBounds.Y + 8, 40, 32), new(0, 255, 0), default, 0, 0);
        });
        _runtimePaint = runtimePaint.Value;
        var staticDraw = GallerySample.Define<Action<UiImmediateContext>>("renderStatic", () => (UiImmediateContext ui) =>
        {
            var origin = ui.GetCursorScreenPos();
            var bounds = new UiRect(origin.X, origin.Y, MathF.Min(1000, ui.GetContentRegionAvail().X), 180);
            if (_lastBounds != bounds || _drawnStaticRevision != _revision)
            {
                _lastBounds = bounds;
                _drawnStaticRevision = _revision;
                _staticVersion++;
            }
            ui.PushClipRect(bounds, true);
            if (ui.BeginLayer("observer-static-primitives", new(false, 1, default) { ContentVersion = _staticVersion }))
            {
                _staticBuilds++;
                var draw = ui.GetWindowDrawList();
                draw.AddRectFilled(bounds, new(25, 45, 80), ui.WhiteTextureId, bounds);
                draw.AddRectFilled(new(bounds.X + 100, bounds.Y, 100, bounds.Height), new(25, 100, 85), ui.WhiteTextureId, bounds);
                using var rounded = _roundedClip ? draw.RoundedClip(new(bounds.X + 24, bounds.Y + 24, MathF.Max(0, bounds.Width - 48), 132), 48) : default;
                using var cutout = _circleCutout ? draw.CircleCutout(new(bounds.X + 110, bounds.Y + 72), 28) : default;
                draw.AddRectFilledRounded(new(bounds.X + 12, bounds.Y + 12, 200, 120), new(30, 90, (byte)(120 + _revision % 100)), ui.WhiteTextureId, 14, bounds);
                draw.AddCircleFilled(new(bounds.X + 310, bounds.Y + 72), 52, new(20, 160, 200), ui.WhiteTextureId, bounds, 48);
                draw.AddTriangleFilled(new(bounds.X + 420, bounds.Y + 130), new(bounds.X + 490, bounds.Y + 20), new(bounds.X + 560, bounds.Y + 130), new(220, 130, 30), ui.WhiteTextureId, bounds);
                draw.AddLine(new(bounds.X + 610, bounds.Y + 25), new(bounds.X + 850, bounds.Y + 125), new(180, 100, 220), 5, ui.WhiteTextureId);
            }
            ui.EndLayer();
            ui.PopClipRect();
            ui.Dummy(new(bounds.Width, bounds.Height));
        });
        var renderStatic = staticDraw.Value;
        var staticSample = GallerySample.Create(() => Dux.SizedBox(
            new UiViewHost(new UiView(renderStatic)) { AccessibleName = "정적 도형 예제" }, width : 1000, height : 180));
        var staticFactory = staticSample.Source;
        staticSample.BindSource(() => (_revision, _circleCutout, _roundedClip), state =>
            Settings(state._revision, state._circleCutout, state._roundedClip)
            + "UiRect _lastBounds = default;\nlong _drawnStaticRevision = -1, _staticVersion = 0;\nvar _staticBuilds = 0;\n\n"
            + staticDraw.Source + "\n\n" + staticFactory);
        var projection = GallerySample.Define<Action<UiImmediateContext>>("renderProjection", () => (UiImmediateContext ui) =>
        {
            var origin = ui.GetCursorScreenPos();
            var viewport = new UiRect(origin.X, origin.Y, 600, 240);
            if (_lastProjectedBounds != viewport || _drawnProjectedRevision != _revision)
            {
                _lastProjectedBounds = viewport;
                _drawnProjectedRevision = _revision;
                _projectedVersion++;
            }
            var angle = _animate ? (float)ui.GetTime() * .5f : .35f;
            var matrix = Matrix4x4.CreateRotationY(angle)
                * Matrix4x4.CreateLookAt(new(0, 0, 2.8f), Vector3.Zero, Vector3.UnitY)
                * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, viewport.Width / viewport.Height, .1f, 30);
            var scene = new UiLayer3D(matrix) { Viewport = viewport };
            if (ui.BeginLayer("observer-projected-primitives", new(false, 1, default) { Scene3D = scene, ContentVersion = _projectedVersion }))
            {
                var draw = ui.GetWindowDrawList();
                using var rounded = _roundedClip ? draw.RoundedClip(new(viewport.X + 120, viewport.Y + 60, 320, 120), 40) : default;
                using var cutout = _circleCutout ? draw.CircleCutout(new(viewport.X + 240, viewport.Y + 120), 34) : default;
                draw.AddRectFilledRounded(new(viewport.X + 100, viewport.Y + 50, 280, 140), new(30, 150, 210), ui.WhiteTextureId, 18, viewport);
                draw.AddCircleFilled(new(viewport.X + 420, viewport.Y + 120), 60, new(230, 130, 40), ui.WhiteTextureId, viewport, 48);
            }
            ui.EndLayer();
            ui.Dummy(new(viewport.Width, viewport.Height));
            if (_animate)
                ui.RequestFrameAt(ui.GetTime() + 1d / 60);
        });
        var renderProjection = projection.Value;
        var projectedSample = GallerySample.Create(() => Dux.SizedBox(
            new UiViewHost(new UiView(renderProjection)) { AccessibleName = "3D 투영 예제" }, width : 600, height : 240));
        var projectedFactory = projectedSample.Source;
        projectedSample.BindSource(() => (_revision, _circleCutout, _roundedClip, _animate), state =>
            "using System.Numerics;\n\n" + Settings(state._revision, state._circleCutout, state._roundedClip)
            + $"var _animate = {GallerySample.Boolean(state._animate)};\nUiRect _lastProjectedBounds = default;\n"
            + "long _drawnProjectedRevision = -1, _projectedVersion = 0;\n\n" + projection.Source + "\n\n" + projectedFactory);
        var coverage = GallerySample.Define<Action<UiImmediateContext>>("renderCoverage", () => (UiImmediateContext ui) =>
        {
            var origin = ui.GetCursorScreenPos();
            BackdropPreviewBounds = new(origin.X, origin.Y, 320, 48);
            var draw = ui.GetWindowDrawList();
            for (var index = 0; index < 3; index++)
            {
                var rectangle = new UiRect(origin.X + index * 112, origin.Y, 96, 48);
                draw.AddRectFilled(rectangle, new(100, 160, 200), ui.WhiteTextureId);
                ui.BackdropImage(_backdropPreview, rectangle, index * .5f);
            }
            ui.Dummy(new(320, 48));
        });
        var renderCoverage = coverage.Value;
        var coverageSample = GallerySample.Create(() => Dux.SizedBox(
            new UiViewHost(new UiView(renderCoverage)) { AccessibleName = "참조 합성 예제" }, width : 320, height : 48));
        var coverageFactory = coverageSample.Source;
        coverageSample.BindSource(() => 0, _ => "using " + backdrop.Source + "\nUiRect BackdropPreviewBounds = default;\n\n"
            + coverage.Source + "\n\n" + coverageFactory);
        var runtime = GallerySample.Define<Action<UiImmediateContext>>("renderRuntime", () => (UiImmediateContext ui) =>
        {
            var origin = ui.GetCursorScreenPos();
            _runtimeBounds = new(origin.X, origin.Y, 80, 48);
            ui.GetWindowDrawList().AddRectFilled(_runtimeBounds, new(0, 0, 255), ui.WhiteTextureId);
            ui.PaintGroup(_runtimeGroup, _runtimeBounds, _roundedClip ? 2UL : 1UL, _runtimePaint, .8f);
            ui.Dummy(new(80, 48));
        });
        var renderRuntime = runtime.Value;
        var runtimeSample = GallerySample.Create(() => Dux.SizedBox(
            new UiViewHost(new UiView(renderRuntime)) { AccessibleName = "그룹 합성 예제" }, width : 80, height : 48));
        var runtimeFactory = runtimeSample.Source;
        runtimeSample.BindSource(() => _roundedClip, value => $"var _roundedClip = {GallerySample.Boolean(value)};\n"
            + "using " + runtimeGroup.Source + "\nUiRect _runtimeBounds = default;\n\n" + runtimePaint.Source + "\n\n" + runtime.Source + "\n\n" + runtimeFactory);
        var text = GallerySample.Define<Action<UiImmediateContext>>("renderText", () => (UiImmediateContext ui) =>
        {
            var origin = ui.GetCursorScreenPos();
            _projectedTextSourceBounds = new(origin.X, origin.Y, 96, 48);
            ProjectedTextBounds = new(origin.X, origin.Y, 84, 42);
            ui.GetWindowDrawList().AddRectFilled(ProjectedTextBounds, new(16, 16, 16), ui.WhiteTextureId);
            ui.PaintGroup(_projectedTextGroup, _projectedTextSourceBounds, 1, _projectedTextPaint,
                destination : ProjectedTextBounds, rasterScale : .875f, textMode : UiPaintGroupTextMode.Projected);
            ui.Dummy(new(84, 42));
        });
        var renderText = text.Value;
        var textSample = GallerySample.Create(() => Dux.SizedBox(
            new UiViewHost(new UiView(renderText)) { AccessibleName = "투영 텍스트 예제" }, width : 84, height : 42));
        var textFactory = textSample.Source;
        textSample.BindSource(() => 0, _ => "using " + textGroup.Source
            + "\nUiRect _projectedTextSourceBounds = default, ProjectedTextBounds = default;\n\n"
            + textPaint.Source + "\n\n" + text.Source + "\n\n" + textFactory);
        _opacityMask = new();
        Samples = [staticSample, projectedSample, coverageSample, runtimeSample, textSample, _opacityMask.Sample];
        var animation = Fluent.Button("애니메이션 : 꺼짐");
        animation.Activated = () =>
        {
            _animate = !_animate;
            animation.Label = _animate ? "애니메이션 : 켜짐" : "애니메이션 : 꺼짐";
            _revision++;
        };
        _cutoutButton = Fluent.Button("원형 잘라내기 : 꺼짐", ToggleCircleCutout);
        _roundedClipButton = Fluent.Button("둥근 클리핑 : 꺼짐", ToggleRoundedClip);
        _root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing : 16, RunSpacing : 8), Fluent.Button("정적 내용 변경", () => _revision++), animation, _cutoutButton, _roundedClipButton),
            Fluent.Body1("입력이나 상단 수치가 바뀌어 새 프레임을 그리더라도, 버전이 같으면 이전에 만든 도형 그리기 명령을 재사용합니다. 아래 횟수는 이 명령을 만든 횟수이며, 전체 CPU 작업량이나 GPU 사용량을 뜻하지 않습니다."),
            Fluent.Body1(() => $"기하 버전 : {_revision} / 정적 그리기 본문 실행 : {_staticBuilds}"), staticSample,
            Fluent.Subtitle2("독립 뷰포트의 3D 투영"), projectedSample,
            Fluent.Body1("글자 가장자리의 RGB 커버리지 합성을 비교합니다. 검정·흰색 기준 이미지에 불투명도 0 / 0.5 / 1을 한 번씩 적용합니다."), coverageSample,
            Fluent.Body1("실제 겹친 도형을 GPU로 캡처해 불투명도 0.8을 한 번 적용합니다."), runtimeSample,
            Fluent.Body1("글자를 0.875배로 줄인 크기에 맞춰 캡처합니다."), textSample,
            _opacityMask.View));
    }
    private static string Settings(long revision, bool cutout, bool rounded) =>
        $"long _revision = {revision};\nvar _circleCutout = {GallerySample.Boolean(cutout)};\nvar _roundedClip = {GallerySample.Boolean(rounded)};\n";
    internal void ToggleCircleCutout()
    {
        _circleCutout = !_circleCutout;
        _cutoutButton.Label = _circleCutout ? "원형 잘라내기 : 켜짐" : "원형 잘라내기 : 꺼짐";
        _revision++;
    }
    internal void ToggleRoundedClip()
    {
        _roundedClip = !_roundedClip;
        _roundedClipButton.Label = _roundedClip ? "둥근 클리핑 : 켜짐" : "둥근 클리핑 : 꺼짐";
        _revision++;
    }
    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose()
    {
        _root.Dispose();
        _opacityMask.Dispose();
        _projectedTextGroup.Dispose();
        _runtimeGroup.Dispose();
        _backdropPreview.Dispose();
    }
}
