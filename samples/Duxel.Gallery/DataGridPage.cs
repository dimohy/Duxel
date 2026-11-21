using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class DataGridPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiWidget _root;
    public string Title => "DataGrid";
    public string Description => "열 제목을 눌러 정렬합니다. 파일 이름을 두 번 누르거나 F2를 눌러 편집하고, Enter로 저장하거나 Esc로 취소합니다.";
    public IReadOnlyList<IGallerySample> Samples { get; }
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal DataGridPage()
    {
        var sample = GallerySample.Create(() =>
        {
            string[] owners = [GalleryText.Get("김하나"), GalleryText.Get("이서준"), GalleryText.Get("박지우"), GalleryText.Get("정민서")];
            string[] statuses = [GalleryText.Get("초안"), GalleryText.Get("검토 중"), GalleryText.Get("공유됨")];
            var fileName = GalleryText.Get("디자인 검토 {0:D4}.pdf");
            var files = Enumerable.Range(1, 2048).Select(i => new FileRow(
                $"file-{i}", string.Format(fileName, i), owners[i % owners.Length],
                statuses[i % statuses.Length], 24 + i * 53 % 4800)).ToArray();
            var grid = Fluent.DataGrid(files, file => file.Id,
            [
                new(GalleryText.Get("파일 이름"), file => file.Name, 3, (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name),
                    Edit: (FileRow file, string text, out FileRow replacement) =>
                    {
                        var name = text.Trim();
                        replacement = file with { Name = name };
                        return name.Length != 0;
                    }),
                new(GalleryText.Get("담당자"), file => file.Owner, 2, (a, b) => StringComparer.CurrentCulture.Compare(a.Owner, b.Owner)),
                new(GalleryText.Get("상태"), file => file.Status, 1.5f),
                new(GalleryText.Get("크기"), file => $"{file.SizeKb:N0} KB", 1.3f, (a, b) => a.SizeKb.CompareTo(b.SizeKb),
                    UiItemHorizontalAlign.Right)
            ], new(Width: 820, Height: 360, Selection: UiDataGridSelection.Multiple));
            grid.AccessibleName = GalleryText.Get("디자인 검토 파일");
            return grid;
        }, declarations: "sealed record FileRow(string Id, string Name, string Owner, string Status, int SizeKb);");
        Samples = [sample];
        var grid = sample.Preview;
        _root = Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Body1(() => GalleryText.Format("파일 {0:N0}개 · 선택 {1:N0}개", grid.RowCount, grid.SelectedCount)), sample);
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();

    private sealed record FileRow(string Id, string Name, string Owner, string Status, int SizeKb);
}
