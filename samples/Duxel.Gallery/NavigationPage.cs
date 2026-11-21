using Duxel.Core;

namespace Duxel.Gallery;

[UiFluentIcon("pin", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct NavPinIcon;
[UiFluentIcon("pin", UiFluentIconStyle.Filled, 20)]
internal readonly partial struct NavPinFilledIcon;

internal sealed class NavigationPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    public IReadOnlyList<IGallerySample> Samples { get; }
    private readonly UiState<string?> _selected = new("overview");
    private readonly UiFluentNav _preview;
    private readonly UiWidget _root;
    private int _pinActions;
    internal int PinActions => _pinActions;
    internal UiFluentNav Preview => _preview;
    public string Title => "탐색 메뉴";
    public string Description => "Try collapsing groups, selecting a page, keyboard navigation, and right-to-left layout.";
    internal NavigationPage()
    {
        var options = new UiNavOptions();
        var sample = GallerySample.Create(() => Fluent.Nav(GalleryText.Get("Navigation example"), _selected, new UiNavGroup[]
        {
            UiNavGroup.SectionHeader("work", GalleryText.Get("Workspace")),
            new("start", GalleryText.Get("Get started"), new UiNavItem[] { new("overview", GalleryText.Get("Overview"), SecondaryAction : new(GalleryText.Get("Pin overview"), () => _pinActions++, UiMenuIcon.Create<NavPinIcon, NavPinFilledIcon>())), new("activity", GalleryText.Get("Recent activity"), Badge : new(GalleryText.Get("What's new"))) },
                UiMenuIcon.Create<HomeTabIcon, HomeTabFilledIcon>()),
            UiNavGroup.SectionHeader("library", GalleryText.Get("Library")),
            new("documents", GalleryText.Get("Documents"), new UiNavItem[] { new("documents-all", GalleryText.Get("Long project document names wrap to fit the available width")),
                new("disabled", GalleryText.Get("Unavailable"), false) }, UiMenuIcon.Create<GalleryContentIcon, GalleryContentIcon>())
        }, options));
        _preview = sample.Preview;
        var factory = sample.Source;
        sample.BindSource(() => (_selected.Value, _pinActions, _preview.Options), state =>
            "var _selected = new UiState<string?>(" + GallerySample.String(state.Value) + ");\n"
            + FormattableString.Invariant($"var _pinActions = {state._pinActions};\nvar options = new UiNavOptions(Width: {state.Options.Width}f, RightToLeft: {GallerySample.Boolean(state.Options.RightToLeft)});\n\n") + factory
            + "\n\n" + IconDeclarations);
        Samples = [sample];
        _root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Use Up and Down or Home and End to move, and Enter to select a page. Use Left and Right or click a group to collapse or expand it.")),
            Fluent.Caption1(GalleryText.Get("Section headings organize the menu. You cannot select, collapse, or focus them.")),
            Fluent.Button(GalleryText.Get("Reverse layout direction"), () => _preview.Options = _preview.Options with { RightToLeft = !_preview.Options.RightToLeft }),
            Fluent.Body1(() => GalleryText.Format("Current page: {0}", SelectedPageName())),
            Fluent.Body1(() => GalleryText.Format("Overview pin actions: {0}", _pinActions)),
            Fluent.Caption1(GalleryText.Get("Hover over Overview to reveal its pin button. Press Tab or a horizontal arrow key to reach it. Right-click or press Shift+F10 to run the same action from the menu.")),
            Fluent.Caption1(GalleryText.Get("Collapsing a group keeps the current selection. A highlighted collapsed group contains the current page.")),
            sample));
    }
    private string SelectedPageName() => GalleryText.Get(_selected.Value switch
    {
        "overview" => "Overview",
        "activity" => "Recent activity",
        "documents-all" => "Long project document names wrap to fit the available width",
        _ => "None selected"
    });
    private const string IconDeclarations = """
        [UiFluentIcon("pin", UiFluentIconStyle.Regular, 20)] readonly partial struct NavPinIcon;
        [UiFluentIcon("pin", UiFluentIconStyle.Filled, 20)] readonly partial struct NavPinFilledIcon;
        [UiFluentIcon("home")] readonly partial struct HomeTabIcon;
        [UiFluentIcon("home", UiFluentIconStyle.Filled)] readonly partial struct HomeTabFilledIcon;
        [UiFluentIcon("apps", UiFluentIconStyle.Regular, 20)] readonly partial struct GalleryContentIcon;
        """;
    UiWidget IObserverNaturalPage.MeasureRoot => _root;
    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();
}
