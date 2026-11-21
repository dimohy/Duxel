using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ListPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private UiWidget? _root;
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples
    {
        get
        {
            _root ??= Create();
            return _samples;
        }
    }
    public string Title => "목록";
    public string Description => "Try a plain list, single and multiple selection, and inputs and buttons inside rows.";
    UiWidget IObserverNaturalPage.MeasureRoot => _root ??= Create();
    public void Render(UiImmediateContext ui) => (_root ??= Create()).Render(ui);
    public void Dispose() => _root?.Dispose();
    private UiWidget Create()
    {
        var selected = new UiState<string?>("seoul");
        var members = new UiState<string[]>(["seil"]);
        var note = new UiState<string>(GalleryText.Get("Prepare for the meeting"));
        var keep = new UiState<bool>(false);
        var commands = new UiState<int>(0);
        var plain = GallerySample.Create(() => Fluent.List(GalleryText.Get("General information"),
        [
            new("first", GalleryText.Get("First notice"), Fluent.Body1(GalleryText.Get("A plain list can contain any layout."))),
            new("second", GalleryText.Get("Second notice"), Fluent.Button(GalleryText.Get("Independent list button"), () => commands.Value++))
        ], new(Width : 520, Spacing : 8)));
        var plainFactory = plain.Source;
        plain.BindSource(() => commands.Value, value => $"var commands = new UiState<int>({value});\n\n" + plainFactory);
        var single = GallerySample.Create(() => Fluent.List(GalleryText.Get("Cities to visit"), selected.Binding,
        [
            new("seoul", GalleryText.Get("Seoul"), Fluent.Body1(GalleryText.Get("Seoul")), selectionLabel : GalleryText.Get("Select Seoul")),
            new("busan", GalleryText.Get("Busan"), Fluent.Body1(GalleryText.Get("Busan")), selectionLabel : GalleryText.Get("Select Busan")),
            new("jeju", GalleryText.Get("Jeju"), Fluent.Body1(GalleryText.Get("Jeju")), selectionLabel : GalleryText.Get("Select Jeju"))
        ], new(Width : 520)));
        var singleFactory = single.Source;
        single.BindSource(() => selected.Value, value => $"var selected = new UiState<string?>({GallerySample.String(value)});\n\n" + singleFactory);
        var composite = GallerySample.Create(() =>
        {
            void Act(UiListItemAction action)
            {
                commands.Value++;
                action.PreventSelection = keep.Value;
            }
            return Fluent.List(GalleryText.Get("Team members"), members.Binding,
            [
                new("seil", GalleryText.Get("Dimohy"), Dux.Row(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                    Fluent.Body1(GalleryText.Get("Dimohy")), Fluent.Input(GalleryText.Get("Note for Dimohy"), note.Binding, 100, 180),
                    Fluent.Button(GalleryText.Get("Run action for Dimohy"), () => commands.Value++)), Act, selectionLabel : GalleryText.Get("Select Dimohy")),
                new("minsu", GalleryText.Get("Minsoo Kim"), Dux.Row(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                    Fluent.Body1(GalleryText.Get("Minsoo Kim")), Fluent.Button(GalleryText.Get("Run action for Minsoo Kim"), () => commands.Value++)), Act, selectionLabel : GalleryText.Get("Select Minsoo Kim")),
                new("guest", GalleryText.Get("Guest with selection disabled"), Fluent.Body1(GalleryText.Get("Guest: row action only")), Act,
                    disabledSelection : true, selectionLabel : GalleryText.Get("Select guest"))
            ], new(Navigation : UiListNavigation.Composite, Width : 520, Spacing : 4));
        });
        var compositeFactory = composite.Source;
        composite.BindSource(() => (Members : members.Value, Memo : note.Value, Keep : keep.Value, Commands : commands.Value), state =>
            $"var members = new UiState<string[]>([{string.Join(", ", state.Members.Select(GallerySample.String))}]);\n"
            + $"var note = new UiState<string>({GallerySample.String(state.Memo)});\n"
            + $"var keep = new UiState<bool>({GallerySample.Boolean(state.Keep)});\n"
            + $"var commands = new UiState<int>({state.Commands});\n\n" + compositeFactory);
        _samples.AddRange([plain, single, composite]);
        return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            plain, Fluent.Subtitle1(GalleryText.Get("Single selection")), single,
            Fluent.Body1(() => GalleryText.Format("City to visit: {0}", GalleryText.Get(selected.Value switch
            {
                "seoul" => "Seoul", "busan" => "Busan", "jeju" => "Jeju", _ => "None selected"
            }))),
            Fluent.Subtitle1(GalleryText.Get("Composite list with multiple selection")), composite,
            Fluent.Checkbox(GalleryText.Get("Keep selection when activating a row"), keep.Binding),
            Fluent.Body1(() => GalleryText.Format("Selected: {0} / Actions: {1} / Note: {2}",
                members.Value.Length == 0 ? GalleryText.Get("None selected") : string.Join(", ", members.Value.Select(MemberName)),
                commands.Value, note.Value)),
            Fluent.Caption1(GalleryText.Get("Use Up and Down to move between rows. In the composite list, use Right to enter the row's widgets and Left or Escape to return to the row.")),
            Fluent.Caption1(GalleryText.Get("Press Space to toggle selection. Press Enter or click the row to run its action and select it. Clicking a checkbox or button within the row does not run the row action.")));
    }
    private static string MemberName(string id) => GalleryText.Get(id switch
    {
        "seil" => "Dimohy",
        "minsu" => "Minsoo Kim",
        "guest" => "Guest with selection disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    });
}
