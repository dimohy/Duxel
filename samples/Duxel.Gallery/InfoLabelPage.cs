using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class InfoLabelPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "추가 정보";
    public string Description => "라벨 옆 정보 버튼을 누르거나 키보드로 열어 보세요. 한글 접근성 이름과 라벨에 연결된 입력창도 확인할 수 있습니다.";
    internal UiFluentInfoLabel? Preview => _content?.Preview;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal UiFluentInfoLabel Preview { get; }
        internal UiWidget Root { get; }
        internal Content()
        {
            var name = new UiState<string>("디모이");
            var commands = 0;
            var input = Fluent.Input("이름 입력", name.Binding, 100, 280);
            var text = "사용자 이름";
            var options = new UiLabelOptions(Required: true);
            var sample = GallerySample.Create(() => Fluent.InfoLabel(text, Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Body1("다른 사용자에게 표시할 이름입니다. 한글과 영문을 사용할 수 있습니다."),
                Fluent.Button("정보 안에서 실행", () => commands++)), input,
                options, informationLabel: "추가 정보"),
                layout: label => Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min), label, input));
            Preview = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => (Name: name.Value, Commands: commands, Preview.Options, Preview.Text), state =>
                FormattableString.Invariant($$"""
                    var name = new UiState<string>({{GallerySample.String(state.Name)}});
                    var commands = {{state.Commands}};
                    var text = {{GallerySample.String(state.Text)}};
                    var options = new UiLabelOptions(
                        Required: {{GallerySample.Boolean(state.Options.Required)}},
                        Disabled: {{GallerySample.Boolean(state.Options.Disabled)}},
                        Size: UiLabelSize.{{state.Options.Size}},
                        Semibold: {{GallerySample.Boolean(state.Options.Semibold)}});
                    var input = Fluent.Input("이름 입력", name.Binding, 100, 280);

                    """) + factory);
            Samples = [sample];
            Root = Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("정보 크기 전환", () => Preview.Options = Preview.Options with
                    {
                        Size = (UiLabelSize)(((int)Preview.Options.Size + 1) % 3)
                    }),
                    Fluent.Button("정보 라벨 전환", () => Preview.Text = Preview.Text == "사용자 이름" ? "디모이 표시 이름" : "사용자 이름")),
                Fluent.Body1(() => $"입력 : {name.Value} / 팝업 안에서 실행 : {commands}"),
                Fluent.Caption1("라벨을 누르면 입력창으로 포커스가 이동합니다. 정보 버튼으로 설명을 열고, Esc를 누르거나 다른 컨트롤로 포커스를 옮겨 닫으세요."),
                Fluent.Caption1("기본 정보 아이콘은 Fluent 아이콘 폰트의 윤곽선(regular)과 채움(filled) 모양을 사용합니다. Icon과 Image에는 SVG도 사용할 수 있습니다."),
                sample);
        }
    }
}
