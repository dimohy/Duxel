using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class InfoLabelPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "추가 정보";
    public string Description => "Open the info button next to the label with a pointer or keyboard. Inspect the accessible name and the associated input field.";
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
            var name = new UiState<string>(GalleryText.Get("Dimoi"));
            var commands = 0;
            var inputLabel = GalleryText.Get("Name field");
            var input = Fluent.Input(inputLabel, name.Binding, 100, 280);
            var text = GalleryText.Get("User name");
            var alternate = GalleryText.Get("Dimoi display name");
            var options = new UiLabelOptions(Required: true);
            var sample = GallerySample.Create(() => Fluent.InfoLabel(text, Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("This name is visible to other users. You can enter it in your own language.")),
                Fluent.Button(GalleryText.Get("Run command in info popover"), () => commands++)), input,
                options, informationLabel: GalleryText.Get("Show more information")),
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
                    var input = Fluent.Input({{GallerySample.String(inputLabel)}}, name.Binding, 100, 280);

                    """) + factory);
            Samples = [sample];
            Root = Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change info label size"), () => Preview.Options = Preview.Options with
                    {
                        Size = (UiLabelSize)(((int)Preview.Options.Size + 1) % 3)
                    }),
                    Fluent.Button(GalleryText.Get("Change info label text"), () => Preview.Text = Preview.Text == text ? alternate : text)),
                Fluent.Body1(() => GalleryText.Format("Input: {0} / Commands in popover: {1}", name.Value, commands)),
                Fluent.Caption1(GalleryText.Get("Select the label to focus the input field. Open the explanation with the info button and close it with Esc or by moving focus to another control.")),
                Fluent.Caption1(GalleryText.Get("The default info icon uses the regular and filled glyphs from the Fluent icon font. Icon and Image can also use SVG.")),
                sample);
        }
    }
}
