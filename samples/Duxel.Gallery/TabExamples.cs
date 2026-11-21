using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TabExamples : UiWidget, IGallerySourcePage
{
    private readonly UiState<string?> _selection = new("home");
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }

    internal TabExamples()
    {
        var homeDefinition = GallerySample.Define("home", () => UiTabIcon.Create<HomeTabIcon, HomeTabFilledIcon>());
        var documentDefinition = GallerySample.Define("document", () => UiTabIcon.Create<DocumentTabIcon, DocumentTabFilledIcon>());
        var settingsDefinition = GallerySample.Define("settings", () => UiTabIcon.Create<SettingsTabIcon, SettingsTabFilledIcon>());
        var home = homeDefinition.Value;
        var document = documentDefinition.Value;
        var settings = settingsDefinition.Value;
        var iconsSource = homeDefinition.Source + "\n" + documentDefinition.Source + "\n" + settingsDefinition.Source;
        var declarations = GalleryPageSource.Declaration("TabExampleIcons");
        var examples = new List<UiWidget>
        {
            Fluent.Divider(GalleryText.Get("Tab styles and icons")),
            Fluent.Body1(GalleryText.Get("The tab lists share one selection. Use the arrow keys to move focus, then select a tab. Watch its icon switch between outline and filled styles.")),
            Fluent.Body1(() => GalleryText.Format("Shared selection: {0}", GalleryText.Get(_selection.Value switch
            {
                "home" => "Home", "document" => "Documents", "settings" => "Settings", _ => "None selected"
            })))
        };
        var samples = new List<IGallerySample>();
        foreach (var appearance in Enum.GetValues<UiTabListAppearance>())
        {
            var iconOnly = appearance == UiTabListAppearance.SubtleCircular;
            var size = appearance == UiTabListAppearance.Transparent ? UiTabListSize.Small
                : appearance == UiTabListAppearance.FilledCircular ? UiTabListSize.Large : UiTabListSize.Medium;
            var sample = GallerySample.Create(() => Fluent.TabList(string.Format(GalleryText.Get("Style comparison: {0}"), appearance), _selection.Binding, new UiTabOption[]
            {
                new("home", GalleryText.Get("Home"), Icon : home, IconOnly : iconOnly),
                new("document", GalleryText.Get("Documents"), Icon : document, IconOnly : iconOnly),
                new("settings", GalleryText.Get("Settings"), false, settings, iconOnly)
            }, new(size, Appearance : appearance)));
            var factory = sample.Source;
            sample.BindSource(() => _selection.Value, value => $"var _selection = new UiState<string?>({GallerySample.String(value)});\n"
                + $"var appearance = UiTabListAppearance.{appearance};\nvar size = UiTabListSize.{size};\nbool iconOnly = {GallerySample.Boolean(iconOnly)};\n\n"
                + iconsSource + "\n\n" + factory + "\n\n" + declarations);
            samples.Add(sample);
            examples.Add(Fluent.Subtitle2($"{appearance} / {size}"));
            examples.Add(sample);
        }
        examples.Add(Fluent.Caption1(GalleryText.Get("Icon-only tabs still have accessible names and share the selected value. The Settings tab is disabled.")));
        Samples = samples;
        _root = new UiColumn(examples.ToArray(), new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min));
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
