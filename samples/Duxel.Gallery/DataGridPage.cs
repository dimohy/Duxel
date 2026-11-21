using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class DataGridPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiWidget _root;
    public string Title => "DataGrid";
    public string Description => "Select a column header to sort. Double-click a file name or press F2 to edit, then press Enter to save or Esc to cancel.";
    public IReadOnlyList<IGallerySample> Samples { get; }
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal DataGridPage()
    {
        var sample = GallerySample.Create(() =>
        {
            string[] owners = [GalleryText.Get("Hana Kim"), GalleryText.Get("Seo-jun Lee"), GalleryText.Get("Ji-woo Park"), GalleryText.Get("Min-seo Jeong")];
            string[] statuses = [GalleryText.Get("Draft"), GalleryText.Get("In review"), GalleryText.Get("Shared")];
            var fileName = GalleryText.Get("Design review {0:D4}.pdf");
            var files = Enumerable.Range(1, 2048).Select(i => new FileRow(
                $"file-{i}", string.Format(fileName, i), owners[i % owners.Length],
                statuses[i % statuses.Length], 24 + i * 53 % 4800)).ToArray();
            var grid = Fluent.DataGrid(files, file => file.Id,
            [
                new(GalleryText.Get("File name"), file => file.Name, 3, (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name),
                    Edit: (FileRow file, string text, out FileRow replacement) =>
                    {
                        var name = text.Trim();
                        replacement = file with { Name = name };
                        return name.Length != 0;
                    }),
                new(GalleryText.Get("Owner"), file => file.Owner, 2, (a, b) => StringComparer.CurrentCulture.Compare(a.Owner, b.Owner)),
                new(GalleryText.Get("Status"), file => file.Status, 1.5f),
                new(GalleryText.Get("Size"), file => $"{file.SizeKb:N0} KB", 1.3f, (a, b) => a.SizeKb.CompareTo(b.SizeKb),
                    UiItemHorizontalAlign.Right)
            ], new(Width: 820, Height: 360, Selection: UiDataGridSelection.Multiple));
            grid.AccessibleName = GalleryText.Get("Design review files");
            return grid;
        }, declarations: "sealed record FileRow(string Id, string Name, string Owner, string Status, int SizeKb);");
        Samples = [sample];
        var grid = sample.Preview;
        _root = Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Body1(() => GalleryText.Format("{0:N0} files · {1:N0} selected", grid.RowCount, grid.SelectedCount)), sample);
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();

    private sealed record FileRow(string Id, string Name, string Owner, string Status, int SizeKb);
}
