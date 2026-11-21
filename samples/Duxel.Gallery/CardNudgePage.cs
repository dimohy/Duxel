using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class CardNudgePage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "CardNudge";
    public string Description => "Dismiss or swipe the suggestion card to move the content below it upward.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private sealed class Content
    {
        private const string TitleText = "빠르게 찾아보세요";
        private const string Body = "즐겨찾기에 넣어 두면 자주 쓰는 예제를 바로 열 수 있습니다.";
        private const string Close = "안내 닫기";
        private const string Action = "즐겨찾기에 추가";
        private readonly UiState<bool> _open = new(true);
        private readonly UiState<bool> _saved = new(false);
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal UiWidget Root { get; }

        internal Content()
        {
            var sample = GallerySample.Create(() => Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.CardNudge(TitleText, Body, _open.Binding,
                    action: Fluent.Button(Action, () => { _saved.Value = true; _open.Value = false; }),
                    options: new(CloseLabel: Close)),
                Fluent.Body1(() => _saved.Value ? "즐겨찾기에 추가했습니다." : "최근에 살펴본 예제")));
            sample.BindSource(() => (_open.Value, _saved.Value), state =>
                $"var open = new UiState<bool>({GallerySample.Boolean(state.Item1)});\n"
                + $"var saved = new UiState<bool>({GallerySample.Boolean(state.Item2)});\n"
                + "using var preview = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),\n"
                + $"    Fluent.CardNudge({GallerySample.String(TitleText)}, {GallerySample.String(Body)}, open.Binding,\n"
                + $"        action: Fluent.Button({GallerySample.String(Action)}, () => {{ saved.Value = true; open.Value = false; }}),\n"
                + $"        options: new(CloseLabel: {GallerySample.String(Close)})),\n"
                + "    Fluent.Body1(() => saved.Value ? \"즐겨찾기에 추가했습니다.\" : \"최근에 살펴본 예제\"));\npreview.Render(ui);");
            Samples = [sample];
            Root = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Button("안내 다시 표시", () => { _saved.Value = false; _open.Value = true; }),
                sample);
        }
    }
}
