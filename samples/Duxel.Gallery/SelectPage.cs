using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class SelectPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "Select";
    public string Description => "Select one text item. Use arrow keys or type its name to change the selection while the list is closed.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Example(GalleryText.Get("Basic selection"), GalleryText.Get("Region"), null, new(), false),
            Example(GalleryText.Get("Size and appearance"), GalleryText.Get("Appearance"), "jp", new(Size : UiDropdownSize.Large, Appearance : UiInputAppearance.FilledLighter), false),
            Example(GalleryText.Get("Validation and direction"), GalleryText.Get("Region to validate"), "us", new(RightToLeft : true, Invalid : true), true));
        private UiWidget Example(string title, string label, string? initial, UiSelectOptions options, bool validation)
        {
            var value = new UiState<string?>(initial);
            UiSelectOption[] items = [new("kr", GalleryText.Get("Republic of Korea")), new("us", GalleryText.Get("United States")), new("jp", GalleryText.Get("Japan")), new("de", GalleryText.Get("Germany"), false)];
            var sourceOptions = string.Join(", ", items.Select(item =>
                $"new({GallerySample.String(item.Value)}, {GallerySample.String(item.Text)}, {GallerySample.Boolean(item.Enabled)})"));
            options = options with { ExpandLabel = GalleryText.Get("Open list") };
            var sample = GallerySample.Create(() => Fluent.Select(label, value.Binding, items, options));
            var preview = sample.Preview;
            sample.BindSource(() => (value.Value, preview.Options), current =>
                "var value = new UiState<string?>(" + GallerySample.String(current.Value) + ");\n"
                + "using var select = Fluent.Select(" + GallerySample.String(label) + ", value.Binding,\n"
                + "    [" + sourceOptions + "],\n"
                + "    new(Width : " + GallerySample.Number(current.Options.Width) + ", Size : UiDropdownSize." + current.Options.Size
                + ",\n        Appearance : UiInputAppearance." + current.Options.Appearance + ", RightToLeft : " + GallerySample.Boolean(current.Options.RightToLeft)
                + ", Invalid : " + GallerySample.Boolean(current.Options.Invalid)
                + ", ExpandLabel : " + GallerySample.String(current.Options.ExpandLabel) + "));\nselect.Render(ui);");
            Samples.Add(sample);
            return Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(title),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Format("Change size for {0}", title), () => preview.Options = preview.Options with { Size = (UiDropdownSize)(((int)preview.Options.Size + 1) % 3) }),
                    Fluent.Button(GalleryText.Format("Change appearance for {0}", title), () => preview.Options = preview.Options with { Appearance = (UiInputAppearance)(((int)preview.Options.Appearance + 1) % 4) }),
                    Fluent.Button(GalleryText.Format("Reverse direction for {0}", title), () => preview.Options = preview.Options with { RightToLeft = !preview.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Format("Toggle validation for {0}", title), () => preview.Options = preview.Options with { Invalid = !preview.Options.Invalid })),
                Fluent.Body1(() => GalleryText.Format("Selected value: {0}", value.Value ?? GalleryText.Get("None"))),
                Fluent.Caption1(validation ? GalleryText.Get("The error indicator leaves the options selectable. Use Field to associate help or validation text.")
                    : GalleryText.Get("See Dropdown for icons, descriptions, and multiple selection, or Combobox for search and free-form input.")),
                    sample);
        }
    }
}
