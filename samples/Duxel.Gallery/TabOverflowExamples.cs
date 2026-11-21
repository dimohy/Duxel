using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TabOverflowExamples : UiWidget, IGallerySourcePage
{
    private readonly UiState<string?> _value = new("home");
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal TabOverflowExamples()
    {
        var counts = Enumerable.Range(0, 5).Select(_ => new UiState<int>(0)).ToArray();
        var tabOptions = new UiTabListOptions { Overflow = new(Label : GalleryText.Get("more tabs")) };
        var itemsDefinition = GallerySample.Define("items", () => new UiTabOption[]
        {
            new("home", GalleryText.Get("Home")), new("document", GalleryText.Get("Documents")),
            new("analysis", GalleryText.Get("Analysis")), new("settings", GalleryText.Get("Settings"), false),
            new("help", GalleryText.Get("Help"))
        });
        var items = itemsDefinition.Value;
        var tabsDefinition = GallerySample.Define("tabs", () => Fluent.TabList(GalleryText.Get("Tab overflow comparison"),
            _value.Binding, items, tabOptions));
        var tabs = tabsDefinition.Value;
        var widthValue = 160f;
        var heightValue = 40f;
        var boxDefinition = GallerySample.Define("box", () => Dux.SizedBox(tabs, width : widthValue, height : heightValue));
        var box = boxDefinition.Value;
        var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            box, new TabOverflowBodies(tabs, items, counts),
            Fluent.Caption1(() => string.Format(GalleryText.Get("Selected value: {0}"),
                items.FirstOrDefault(item => item.Value == _value.Value).Label ?? GalleryText.Get("None selected")))));
        var factory = sample.Source;
        var declaration = GallerySample.LocalizeSource(GalleryPageSource.Declaration(nameof(TabOverflowBodies)));
        sample.BindSource(() => (_value.Value, tabs.Options, box.Options.Width, box.Options.Height,
            Counts : (counts[0].Value, counts[1].Value, counts[2].Value, counts[3].Value, counts[4].Value)), state =>
        {
            var options = state.Options;
            var overflow = options.Overflow is { } settings
                ? $"new UiTabListOverflowOptions({settings.MinimumVisible}, {GallerySample.String(settings.Label)})" : "null";
            return $"var _value = new UiState<string?>({GallerySample.String(state.Value)});\n"
                + $"UiState<int>[] counts = [new({state.Counts.Item1}), new({state.Counts.Item2}), new({state.Counts.Item3}), new({state.Counts.Item4}), new({state.Counts.Item5})];\n"
                + $"var tabOptions = new UiTabListOptions(Size : UiTabListSize.{options.Size}, Vertical : {GallerySample.Boolean(options.Vertical)},\n"
                + $"    SelectOnFocus : {GallerySample.Boolean(options.SelectOnFocus)}, RightToLeft : {GallerySample.Boolean(options.RightToLeft)},\n"
                + $"    ReserveSelectedSpace : {GallerySample.Boolean(options.ReserveSelectedSpace)}, Appearance : UiTabListAppearance.{options.Appearance}, Overflow : {overflow});\n"
                + $"float? widthValue = {GallerySample.Number(state.Width)};\nfloat? heightValue = {GallerySample.Number(state.Height)};\n\n"
                + itemsDefinition.Source + "\n" + tabsDefinition.Source + "\n\n" + boxDefinition.Source + "\n\n" + factory + "\n\n" + declaration;
        });
        Samples = [sample];
        var width = Fluent.Button(GalleryText.Format("Tab width: {0}", 160));
        width.Activated = () =>
        {
            var next = box.Options.Width == 160 ? 520 : 160;
            box.Options = box.Options with { Width = next };
            width.Label = GalleryText.Format("Tab width: {0}", next);
        };
        var direction = Fluent.Button(GalleryText.Format("Tab direction: {0}", "LTR"));
        direction.Activated = () =>
        {
            tabs.Options = tabs.Options with { RightToLeft = !tabs.Options.RightToLeft };
            direction.Label = GalleryText.Format("Tab direction: {0}", tabs.Options.RightToLeft ? "RTL" : "LTR");
        };
        var orientation = Fluent.Button(GalleryText.Format("Tab orientation: {0}", GalleryText.Get("Horizontal")));
        orientation.Activated = () =>
        {
            var vertical = !tabs.Options.Vertical;
            tabs.Options = tabs.Options with { Vertical = vertical };
            box.Options = box.Options with { Height = vertical ? 110 : 40 };
            orientation.Label = GalleryText.Format("Tab orientation: {0}", GalleryText.Get(vertical ? "Vertical" : "Horizontal"));
        };
        _root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Divider(GalleryText.Get("Tab overflow and panels")),
            Fluent.Body1(GalleryText.Get("Choose a hidden tab from the overflow menu to switch its header and panel together. The panel keeps its counter.")),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8), width, direction, orientation), sample);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}

