using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TextFieldPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "TextField";
    public string Description => "Enter a short value and try the clear button and error display.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min),
            Example(GalleryText.Get("Name"), GalleryText.Get("Dimoi"), GalleryText.Get("Enter the name to display in the app."), false),
            Example(GalleryText.Get("Email"), "example", GalleryText.Get("Example: name@example.com"), true));

        private UiWidget Example(string label, string initial, string hint, bool validate)
        {
            var value = new UiState<string>(initial);
            var sample = GallerySample.Create(() => Fluent.TextField(label, value.Binding,
                inputOptions: new(Placeholder: label, ClearLabel: GalleryText.Format("Clear {0}", label)),
                field: new() { Hint = hint, Required = validate }));
            var input = sample.Preview;
            sample.BindSource(() => (value.Value, input.InputOptions, input.Options), state =>
                $"var text = new UiState<string>({GallerySample.String(state.Value)});\n"
                + $"using var field = Fluent.TextField({GallerySample.String(label)}, text.Binding,\n"
                + $"    inputOptions: new(Placeholder: {GallerySample.String(state.InputOptions.Placeholder)}, ReadOnly: {GallerySample.Boolean(state.InputOptions.ReadOnly)},\n"
                + $"        RightToLeft: {GallerySample.Boolean(state.InputOptions.RightToLeft)}, ClearLabel: {GallerySample.String(state.InputOptions.ClearLabel)}),\n"
                + $"    field: new() {{ Hint = {GallerySample.String(hint)}, Required = {GallerySample.Boolean(validate)},\n"
                + $"        ValidationMessage = {GallerySample.String(state.Options.ValidationMessage)} }});\nfield.Render(ui);");
            Samples.Add(sample);
            return Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Subtitle2(validate ? GalleryText.Get("Validate after typing") : GalleryText.Get("Short text")),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Format("Toggle read-only for {0}", label), () => input.InputOptions = input.InputOptions with { ReadOnly = !input.InputOptions.ReadOnly }),
                    Fluent.Button(GalleryText.Format("Reverse direction for {0}", label), () => input.InputOptions = input.InputOptions with { RightToLeft = !input.InputOptions.RightToLeft }),
                    validate ? Fluent.Button(GalleryText.Get("Check email"), () =>
                    {
                        var at = value.Value.IndexOf('@');
                        input.Options = input.Options with
                        {
                            ValidationMessage = at > 0 && at < value.Value.Length - 1 ? null : GalleryText.Get("Enter text before and after the @ sign.")
                        };
                    }) : Fluent.Button(GalleryText.Get("Reset name"), () => value.Value = initial)),
                sample);
        }
    }
}
