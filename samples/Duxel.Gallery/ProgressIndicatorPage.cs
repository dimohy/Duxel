using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ProgressIndicatorPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "ProgressIndicator";
    public string Description => "작업 이름과 설명을 진행 막대와 함께 표시합니다. 진행률을 모를 때는 기다리는 상태를 보여줍니다.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing : 20, MainAxisSize : UiMainAxisSize.Min),
            Example("파일 업로드", .25f, "선택한 파일을 서버로 보내고 있습니다."),
            Example("서버 연결", null, "서버의 응답을 기다립니다. 진행 막대를 숨겨도 작업 이름과 이 설명은 유지됩니다."));
        private UiWidget Example(string label, float? value, string description)
        {
            var options = new UiProgressIndicatorOptions(label, description, value, Width : 360, ValueText : "처리 중");
            var sample = GallerySample.Create(() => Fluent.ProgressIndicator(options));
            var preview = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => preview.Options, state =>
                "var options = new UiProgressIndicatorOptions(Label : " + GallerySample.String(state.Label)
                + ",\n    Description : " + GallerySample.String(state.Description)
                + ",\n    PercentComplete : " + GallerySample.Number(state.PercentComplete)
                + ", ProgressHidden : " + GallerySample.Boolean(state.ProgressHidden)
                + ",\n    BarHeight : " + GallerySample.Number(state.BarHeight) + ", Width : " + GallerySample.Number(state.Width)
                + ", ReducedMotion : " + GallerySample.Boolean(state.ReducedMotion)
                + ",\n    ValueText : " + GallerySample.String(state.ValueText) + ");\n\n" + factory);
            Samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(label + " 진행률 증가", () => preview.Options = preview.Options with
                    { PercentComplete = ((preview.Options.PercentComplete ?? 0) + .25f) % 1.25f }),
                    Fluent.Button(label + " 대기 전환", () => preview.Options = preview.Options with
                    { PercentComplete = preview.Options.PercentComplete is null ? .5f : null }),
                    Fluent.Button(label + " 막대 표시 전환", () => preview.Options = preview.Options with
                    { ProgressHidden = !preview.Options.ProgressHidden }),
                    Fluent.Button(label + " 모션 줄이기 전환", () => preview.Options = preview.Options with
                    { ReducedMotion = !preview.Options.ReducedMotion })), sample);
        }
    }
}
