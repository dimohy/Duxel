using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TreePage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "Tree";
    public string Description => "Expand a folder, select its children together, or activate a button within a row.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min),
            Example(GalleryText.Get("Folder and file selection"), false), Example(GalleryText.Get("Button within a row"), true));

        private UiWidget Example(string title, bool rowActions)
        {
            var expanded = new UiState<string[]>(["folder"]);
            var selected = new UiState<string[]>(["notes"]);
            var opens = new UiState<int>(0);
            var projectLabel = GalleryText.Get("Project");
            var notesLabel = GalleryText.Get("Documents to read before starting Dimohy's project");
            var reportLabel = GalleryText.Get("Report");
            var openLabel = GalleryText.Get("Open");
            UiTreeItem[] items =
            [
                new("folder", null, projectLabel),
                new("notes", "folder", notesLabel),
                rowActions
                    ? new("report", "folder", reportLabel, Dux.Row(new(Spacing: 8),
                        Fluent.Body1(reportLabel), Fluent.Button(openLabel, () => opens.Value++)))
                    : new("report", "folder", reportLabel)
            ];
            var sample = GallerySample.Create(() => Fluent.Tree(title, expanded.Binding, selected.Binding, items,
                new UiTreeOptions(Width: 320) { RowActions = rowActions }));
            var tree = sample.Preview;
            sample.BindSource(() => (Expanded: expanded.Value, Selected: selected.Value, Opens: rowActions ? opens.Value : 0, tree.Options), state =>
            {
                static string Strings(string[] values) => "[" + string.Join(", ", values.Select(GallerySample.String)) + "]";
                var options = state.Options;
                return $"var expanded = new UiState<string[]>({Strings(state.Expanded)});\n"
                    + $"var selected = new UiState<string[]>({Strings(state.Selected)});\n"
                    + (rowActions ? $"var opens = new UiState<int>({state.Opens});\n" : "")
                    + $"using var tree = Fluent.Tree({GallerySample.String(title)}, expanded.Binding, selected.Binding,\n"
                    + $"    [new(\"folder\", null, {GallerySample.String(projectLabel)}),\n"
                    + $"     new(\"notes\", \"folder\", {GallerySample.String(notesLabel)}),\n"
                    + (rowActions
                        ? $"     new(\"report\", \"folder\", {GallerySample.String(reportLabel)}, Dux.Row(new(Spacing: 8),\n"
                            + $"         Fluent.Body1({GallerySample.String(reportLabel)}), Fluent.Button({GallerySample.String(openLabel)}, () => opens.Value++)))],\n"
                        : $"     new(\"report\", \"folder\", {GallerySample.String(reportLabel)})],\n")
                    + $"    new UiTreeOptions(Width: 320, RightToLeft: {GallerySample.Boolean(options.RightToLeft)}, Size: UiTreeSize.{options.Size})\n"
                    + $"    {{ RowActions = {GallerySample.Boolean(rowActions)}, TextOverflow = UiTreeTextOverflow.{options.TextOverflow},\n"
                    + $"      ReducedMotion = {GallerySample.Boolean(options.ReducedMotion)}, AlignSelectorToTop = {GallerySample.Boolean(options.AlignSelectorToTop)} }});\ntree.Render(ui);";
            });
            Samples.Add(sample);
            return Dux.Column(new(Spacing: 10, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Subtitle2(title),
                rowActions ? Fluent.Body1(GalleryText.Get("On the Report row, press F2 to move to the Open button. Press Escape to return to the row."))
                    : Fluent.Body1(GalleryText.Get("Select the checkbox next to Project to select all its children.")),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change size"), () => tree.Options = tree.Options with
                    { Size = tree.Options.Size == UiTreeSize.Small ? UiTreeSize.Medium : UiTreeSize.Small }),
                    Fluent.Button(GalleryText.Get("Toggle line wrapping"), () => tree.Options = tree.Options with
                    { TextOverflow = tree.Options.TextOverflow == UiTreeTextOverflow.Wrap ? UiTreeTextOverflow.Ellipsis : UiTreeTextOverflow.Wrap }),
                    Fluent.Button(GalleryText.Get("Change direction"), () => tree.Options = tree.Options with { RightToLeft = !tree.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Get("Toggle reduced motion"), () => tree.Options = tree.Options with { ReducedMotion = !tree.Options.ReducedMotion }),
                    Fluent.Button(GalleryText.Get("Align checkbox to top"), () => tree.Options = tree.Options with { AlignSelectorToTop = !tree.Options.AlignSelectorToTop })),
                rowActions ? Fluent.Body1(() => GalleryText.Format("Report opens: {0}", opens.Value)) : Fluent.Body1(() => GalleryText.Format("Selected files: {0}", selected.Value.Length)),
                sample);
        }
    }
}
