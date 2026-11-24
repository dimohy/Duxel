using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class OverflowExamples : UiWidget, IGallerySourcePage
{
    private readonly UiWidget _root;
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;
    internal OverflowExamples()
    {
        var counts = Enumerable.Range(0, 4).Select(_ => new UiState<int>(0)).ToArray();
        var buttonsDefinition = GallerySample.Define("buttons", () => Enumerable.Range(1, 4).Select(index =>
        {
            var count = counts[index - 1];
            var button = Fluent.Button(string.Format(GalleryText.Get("Overflow item {0}: {1}"), index, count.Value));
            button.Size = new(120, 32);
            button.Activated = () => button.Label = string.Format(GalleryText.Get("Overflow item {0}: {1}"), index, ++count.Value);
            return (UiWidget)button;
        }).ToArray());
        var buttons = buttonsDefinition.Value;
        var overflowOptions = new UiOverflowOptions(Spacing : 8);
        var overflowDefinition = GallerySample.Define("overflow", () => Dux.Overflow(buttons, Fluent.Badge(GalleryText.Get("Hidden")), overflowOptions));
        var overflow = overflowDefinition.Value;
        var priorityDefinition = GallerySample.Define<Action<bool>>("applyPriorities", () => (bool last) =>
            overflow.SetPriorities(last ? [0, 0, 0, 2] : [2, 0, 0, 0]));
        var applyPriorities = priorityDefinition.Value;
        var prioritizeLast = false;
        applyPriorities(prioritizeLast);
        var widthValue = 200f;
        var overflowSample = GallerySample.Create(() => Dux.SizedBox(overflow, width : widthValue, height : 48));
        var box = overflowSample.Preview;
        var overflowFactory = overflowSample.Source;
        overflowSample.BindSource(() => (overflow.Options, box.Options.Width, prioritizeLast,
            Counts : (counts[0].Value, counts[1].Value, counts[2].Value, counts[3].Value)), state =>
            $"UiState<int>[] counts = [new({state.Counts.Item1}), new({state.Counts.Item2}), new({state.Counts.Item3}), new({state.Counts.Item4})];\n"
            + $"var overflowOptions = new UiOverflowOptions(Spacing : {GallerySample.Number(state.Options.Spacing)}, MinimumVisible : {state.Options.MinimumVisible},\n"
            + $"    CollapseFrom : UiOverflowCollapseDirection.{state.Options.CollapseFrom}, RightToLeft : {GallerySample.Boolean(state.Options.RightToLeft)});\n"
            + $"float? widthValue = {GallerySample.Number(state.Width)};\nvar prioritizeLast = {GallerySample.Boolean(state.prioritizeLast)};\n\n"
            + buttonsDefinition.Source + "\n\n" + overflowDefinition.Source + "\n\n" + priorityDefinition.Source
            + "\napplyPriorities(prioritizeLast);\n\n" + overflowFactory);
        _samples.Add(overflowSample);
        var width = Fluent.Button(GalleryText.Format("Overflow width: {0}", 200));
        width.Activated = () =>
        {
            var next = box.Options.Width == 200 ? 560 : 200;
            box.Options = box.Options with { Width = next };
            width.Label = GalleryText.Format("Overflow width: {0}", next);
        };
        var priority = Fluent.Button(GalleryText.Format("Priority item: {0}", 1));
        priority.Activated = () =>
        {
            prioritizeLast = !prioritizeLast;
            applyPriorities(prioritizeLast);
            priority.Label = GalleryText.Format("Priority item: {0}", prioritizeLast ? 4 : 1);
        };
        var direction = Fluent.Button(GalleryText.Format("Overflow direction: {0}", "LTR"));
        direction.Activated = () =>
        {
            overflow.Options = overflow.Options with { RightToLeft = !overflow.Options.RightToLeft };
            direction.Label = GalleryText.Format("Overflow direction: {0}", overflow.Options.RightToLeft ? "RTL" : "LTR");
        };
        var lastAction = new UiState<string>(GalleryText.Get("Selection: none"));
        void RecordMenu<T, TState>(GallerySample<T> sample, string optionName, Func<UiMenuOptions> readOptions,
            Func<TState> readState, Func<TState, string> setup) where T : UiWidget
        {
            var factory = sample.Source;
            sample.BindSource(() => (Options : readOptions(), Action : lastAction.Value, State : readState()), state =>
                $"var lastAction = new UiState<string>({GallerySample.String(state.Action)});\n"
                + $"var {optionName} = {MenuOptions(state.Options)};\n" + setup(state.State) + "\n" + factory);
            _samples.Add(sample);
        }
        var commonOptions = new UiMenuOptions();
        var common = GallerySample.Create(() => Dux.Menu(GalleryText.Get("Open standard menu"),
            [new("first", GalleryText.Get("First action")), new("disabled", GalleryText.Get("Unavailable action"), false), new("last", GalleryText.Get("Last action"))],
            value => lastAction.Value = string.Format(GalleryText.Get("Selection: {0}"), value), commonOptions));
        RecordMenu(common, nameof(commonOptions), () => common.Preview.Options, () => 0, _ => string.Empty);
        var fluentOptions = UiFluentMenuPresentation.DefaultOptions;
        var fluent = GallerySample.Create(() => Fluent.Menu(GalleryText.Get("Open Fluent menu"),
            [new("first", GalleryText.Get("Fluent first action")), new("disabled", GalleryText.Get("Fluent unavailable action"), false), new("last", GalleryText.Get("Fluent last action"))],
            value => lastAction.Value = string.Format(GalleryText.Get("Fluent selection: {0}"), value), fluentOptions));
        RecordMenu(fluent, nameof(fluentOptions), () => fluent.Preview.Options, () => 0, _ => string.Empty);
        var longOptions = new UiMenuOptions(138, 32, 5, ItemSpacing : 2, MaximumWidth : 300) { MaximumHeight = 210 };
        var longMenu = GallerySample.Create(() => Fluent.Menu(GalleryText.Get("Long menu with typeahead"),
            [new("apple", "Apple"), new("apricot", "Apricot"), new("banana", "Banana"),
                .. Enumerable.Range(1, 24).Select(i => new UiMenuOption("item-" + i, string.Format(GalleryText.Get("Item {0}"), i))), new("last", GalleryText.Get("Zulu last action"))],
            value => lastAction.Value = string.Format(GalleryText.Get("Long menu selection: {0}"), value), longOptions));
        RecordMenu(longMenu, nameof(longOptions), () => longMenu.Preview.Options, () => 0, _ => string.Empty);
        var showDetails = new UiState<bool>(true);
        var showGrid = new UiState<bool>(false);
        var checkedOptions = UiFluentMenuPresentation.DefaultOptions;
        var checkedMenu = GallerySample.Create(() => Fluent.Menu(GalleryText.Get("Open menu with checkboxes"),
            [UiMenuOption.Checkbox("details", GalleryText.Get("Show details"), showDetails),
                UiMenuOption.Checkbox("grid", GalleryText.Get("Show grid"), showGrid),
                UiMenuOption.Checkbox("disabled", GalleryText.Get("Read-only option"), showGrid, false), new("done", GalleryText.Get("Finish setup"))],
            value => lastAction.Value = string.Format(GalleryText.Get("Checkbox menu selection: {0}"), value), checkedOptions));
        RecordMenu(checkedMenu, nameof(checkedOptions), () => checkedMenu.Preview.Options,
            () => (Details : showDetails.Value, Grid : showGrid.Value), state =>
            $"var showDetails = new UiState<bool>({GallerySample.Boolean(state.Details)});\n"
            + $"var showGrid = new UiState<bool>({GallerySample.Boolean(state.Grid)});\n");
        var sortState = new UiState<string?>("name");
        var viewState = new UiState<string?>("compact");
        var sortDefinition = GallerySample.Define("sortGroup", () => new UiMenuRadioGroup(GalleryText.Get("Sort order"), sortState));
        var viewDefinition = GallerySample.Define("viewGroup", () => new UiMenuRadioGroup(GalleryText.Get("View mode"), viewState));
        var sortGroup = sortDefinition.Value;
        var viewGroup = viewDefinition.Value;
        var radioOptions = UiFluentMenuPresentation.DefaultOptions;
        var radioMenu = GallerySample.Create(() => Fluent.Menu(GalleryText.Get("Open radio menu"),
            [UiMenuOption.Radio("name", GalleryText.Get("Sort by name"), sortGroup), UiMenuOption.Radio("date", GalleryText.Get("Sort by date"), sortGroup),
                UiMenuOption.Radio("disabled-sort", GalleryText.Get("Unavailable sort order"), sortGroup, false),
                UiMenuOption.Radio("compact", GalleryText.Get("Compact view"), viewGroup), UiMenuOption.Radio("wide", GalleryText.Get("Wide view"), viewGroup)],
            value => lastAction.Value = string.Format(GalleryText.Get("Radio menu selection: {0}"), value), radioOptions));
        RecordMenu(radioMenu, nameof(radioOptions), () => radioMenu.Preview.Options,
            () => (Sort : sortState.Value, View : viewState.Value), state =>
            $"var sortState = new UiState<string?>({GallerySample.String(state.Sort)});\n"
            + $"var viewState = new UiState<string?>({GallerySample.String(state.View)});\n"
            + sortDefinition.Source + "\n" + viewDefinition.Source + "\n");
        var iconDefinition = GallerySample.Define("menuIcon", () => UiMenuIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>());
        var menuIcon = iconDefinition.Value;
        var iconOptions = UiFluentMenuPresentation.DefaultOptions with { SubmenuPointerProtectionSeconds = .3 };
        var contentMenu = GallerySample.Create(() => Fluent.Menu(GalleryText.Get("Open menu with icons"),
            [new("new", GalleryText.Get("Select new item")) { Icon = menuIcon, SecondaryContent = GalleryText.Get("Template") },
                new("format", GalleryText.Get("Select format")) { SecondaryContent = "PNG",
                    SubText = GalleryText.Get("Use PNG for an image you can share.") },
                UiMenuOption.Checkbox("preview", GalleryText.Get("Show preview"), showDetails) with { Icon = menuIcon, SecondaryContent = GalleryText.Get("Selection indicator") },
                new("locked", GalleryText.Get("Unavailable item"), false) { Icon = menuIcon, SecondaryContent = GalleryText.Get("Locked") },
                UiMenuOption.Submenu("export", GalleryText.Get("Export"),
                    [new("export-pdf", GalleryText.Get("Export as PDF")), new("export-png", GalleryText.Get("Export as PNG"))])],
            value => lastAction.Value = string.Format(GalleryText.Get("Icon menu selection: {0}"), value), iconOptions));
        RecordMenu(contentMenu, nameof(iconOptions), () => contentMenu.Preview.Options,
            () => showDetails.Value, state =>
            $"var showDetails = new UiState<bool>({GallerySample.Boolean(state)});\n" + iconDefinition.Source + "\n");
        var menuDirection = Fluent.Button(GalleryText.Format("Menu direction: {0}", "LTR"));
        menuDirection.Activated = () =>
        {
            contentMenu.Preview.Options = contentMenu.Preview.Options with { RightToLeft = !contentMenu.Preview.Options.RightToLeft };
            menuDirection.Label = GalleryText.Format("Menu direction: {0}", contentMenu.Preview.Options.RightToLeft ? "RTL" : "LTR");
        };
        _root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Divider(GalleryText.Get("Shared Overflow layout")),
            Fluent.Body1(GalleryText.Get("Reduce the width to hide lower-priority items. Expand it again to restore the same buttons and counters.")),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8), width, priority, direction), overflowSample,
            Fluent.Body1(() => overflow.Result is { } result
                ? GalleryText.Format("Visible: {0} / Hidden: {1} / Fits: {2}", result.VisibleCount, result.HiddenCount, GalleryText.Get(result.Fits ? "On" : "Off")) : GalleryText.Get("Layout not measured yet")),
            Fluent.Caption1(GalleryText.Get("The badge shows how many items are hidden. The preceding example shows how choosing a hidden tab also changes its panel.")),
            Fluent.Divider(GalleryText.Get("Shared menu behavior")), Fluent.Body1(() => lastAction.Value), common, fluent, longMenu, checkedMenu,
            Fluent.Body1(() => GalleryText.Format("Details: {0} / Grid: {1}", GalleryText.Get(showDetails.Value ? "On" : "Off"), GalleryText.Get(showGrid.Value ? "On" : "Off"))),
            radioMenu, Fluent.Body1(() => GalleryText.Format("Sort: {0} / View: {1}",
                GalleryText.Get(sortState.Value == "name" ? "Name order" : "Date order"),
                GalleryText.Get(viewState.Value == "compact" ? "Compact" : "Wide"))),
            menuDirection, contentMenu,
            Fluent.Caption1(GalleryText.Get("Select with the arrow keys, Enter, or Space. Close with Escape, Tab, or an outside click. Styling, icons, and event behavior are still under review.")));
        OwnChild("root", _root);
    }
    private static string MenuOptions(UiMenuOptions options) =>
        $"new UiMenuOptions(MinimumWidth : {GallerySample.Number(options.MinimumWidth)}, ItemHeight : {GallerySample.Number(options.ItemHeight)},\n"
        + $"    Padding : {GallerySample.Number(options.Padding)}, RightToLeft : {GallerySample.Boolean(options.RightToLeft)}, ItemSpacing : {GallerySample.Number(options.ItemSpacing)},\n"
        + $"    MaximumWidth : {GallerySample.Number(options.MaximumWidth)}) {{ MaximumHeight = {GallerySample.Number(options.MaximumHeight)}"
        + (options.SubmenuPointerProtectionSeconds is { } seconds ? FormattableString.Invariant($", SubmenuPointerProtectionSeconds = {seconds:R}") : "")
        + " }";
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
