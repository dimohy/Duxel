using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ToolbarPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "도구 모음";
    public string Description => "도구 모음의 가로·세로 배치, 방향키 이동과 토글 상태를 확인합니다.";
    internal UiFluentToolbar? Preview => _content?.Toolbar;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        private int _count;
        private readonly UiState<bool> _bold = new(false);
        internal UiFluentToolbar Toolbar
        {
            get;
        }
        internal UiWidget Root
        {
            get;
        }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal Content()
        {
            var toolbarOptions = new UiToolbarOptions();
            var sample = GallerySample.Create(() =>
            {
                static UiWidget Action(string text, Action activated) => new UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation<UiFluentSubtleButtonAppearance>>(text, activated);
                return Fluent.Toolbar("문서 도구", [Action("새 문서", () => _count++), Action("저장", () => _count++),
                    Fluent.ToggleButton("굵게", _bold.Binding), Action("인쇄", () => _count++)], toolbarOptions);
            });
            Toolbar = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => (Count : _count, Bold : _bold.Value, Toolbar.Options), state =>
                $"var _count = {state.Count};\nvar _bold = new UiState<bool>({GallerySample.Boolean(state.Bold)});\n"
                + $"var toolbarOptions = new UiToolbarOptions(Size : UiToolbarSize.{state.Options.Size}, Vertical : {GallerySample.Boolean(state.Options.Vertical)}, RightToLeft : {GallerySample.Boolean(state.Options.RightToLeft)});\n\n" + factory);
            Samples = [sample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("도구 모음 방향", () => Toolbar.Options = Toolbar.Options with { Vertical = !Toolbar.Options.Vertical }),
                    Fluent.Button("도구 모음 크기", () => Toolbar.Options = Toolbar.Options with { Size = (UiToolbarSize)(((int)Toolbar.Options.Size + 1) % 3) }),
                    Fluent.Button("도구 모음 RTL", () => Toolbar.Options = Toolbar.Options with { RightToLeft = !Toolbar.Options.RightToLeft })),
                Fluent.Button("도구 모음 다음", () => _count++),
                Fluent.Body1(() => $"실행 횟수 : {_count} / 굵게 : {_bold.Value} / 세로 : {Toolbar.Options.Vertical} / 크기 : {Toolbar.Options.Size}"),
                Fluent.Text("Tab으로 들어간 뒤 방향키·Home·End로 이동하세요. Tab은 다음 컨트롤로 나갑니다."),
                Fluent.Text("Enter·Space로 실제 버튼과 토글을 조작합니다. 다른 페이지로 이동했다 돌아와도 상태는 유지됩니다."),
                sample));
        }
        public void Dispose() => Root.Dispose();
    }
}
