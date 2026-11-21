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
        private readonly UiState<bool> _open = new(true);
        private readonly UiState<bool> _saved = new(false);
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal UiWidget Root { get; }

        internal Content()
        {
            var action = GalleryText.Get("Add to favorites");
            var close = GalleryText.Get("Dismiss nudge");
            var body = GalleryText.Get("Add frequently used examples to your favorites to open them quickly.");
            var titleText = GalleryText.Get("Find your favorites quickly");
            var savedCaption = GalleryText.Get("Added to favorites.");
            var recentCaption = GalleryText.Get("Recently viewed examples");
            var sample = GallerySample.Create(() => Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.CardNudge(titleText, body, _open.Binding,
                    action: Fluent.Button(action, () => { _saved.Value = true; _open.Value = false; }),
                    options: new(CloseLabel: close)),
                Fluent.Body1(() => _saved.Value ? savedCaption : recentCaption)));
            sample.BindSource(() => (_open.Value, _saved.Value), state =>
                $"var open = new UiState<bool>({GallerySample.Boolean(state.Item1)});\n"
                + $"var saved = new UiState<bool>({GallerySample.Boolean(state.Item2)});\n"
                + "using var preview = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),\n"
                + $"    Fluent.CardNudge({GallerySample.String(titleText)}, {GallerySample.String(body)}, open.Binding,\n"
                + $"        action: Fluent.Button({GallerySample.String(action)}, () => {{ saved.Value = true; open.Value = false; }}),\n"
                + $"        options: new(CloseLabel: {GallerySample.String(close)})),\n"
                + $"    Fluent.Body1(() => saved.Value ? {GallerySample.String(savedCaption)} : {GallerySample.String(recentCaption)}));\npreview.Render(ui);");
            Samples = [sample];
            Root = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Button(GalleryText.Get("Show nudge again"), () => { _saved.Value = false; _open.Value = true; }),
                sample);
        }
    }
}
