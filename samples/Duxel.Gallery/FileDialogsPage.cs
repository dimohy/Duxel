using Duxel.App;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class FileDialogsPage : IObserverPage, IObserverNaturalPage
{
    private readonly UiWidget _root;
    private readonly DuxelAppSession? _session;
    private Task<IReadOnlyList<string>>? _pending;

    public string Title => "파일 대화 상자";
    public string Description => "운영체제의 대화 상자에서 파일이나 폴더를 선택하고 반환된 경로를 확인합니다.";
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal FileDialogsPage(DuxelAppSession? session)
    {
        _session = session;
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var start = Directory.Exists(directory) ? directory : null;
        var allFiles = new UiFileDialogFilter(GalleryText.Get("모든 파일"), "*");
        var textFiles = new UiFileDialogFilter(GalleryText.Get("텍스트 파일"), "*.txt");
        var open = new UiFileDialogRequest(UiFileDialogKind.OpenFile, GalleryText.Get("열 파일 선택"), start,
            allowMultiple: true, filters: [allFiles, textFiles]);
        var save = new UiFileDialogRequest(UiFileDialogKind.SaveFile, GalleryText.Get("저장할 파일 선택"), start,
            suggestedFileName: GalleryText.Get("메모.txt"), defaultExtension: "txt", filters: [textFiles]);
        var folder = new UiFileDialogRequest(UiFileDialogKind.SelectFolder, GalleryText.Get("폴더 선택"), start);
        _root = Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                Fluent.Button(GalleryText.Get("파일 열기"), () => Show(open)),
                Fluent.Button(GalleryText.Get("다른 이름으로 저장"), () => Show(save)),
                Fluent.Button(GalleryText.Get("폴더 선택"), () => Show(folder))),
            Fluent.Body1(Status));
    }

    private void Show(UiFileDialogRequest request)
    {
        if (_session is not null && _pending?.IsCompleted != false)
            _pending = _session.ShowFileDialogAsync(request);
    }

    private string Status()
    {
        if (_session is null)
            return GalleryText.Get("실행 중인 앱에서 운영체제 대화 상자를 열 수 있습니다.");
        if (_pending is null)
            return GalleryText.Get("선택한 경로가 여기에 표시됩니다.");
        if (!_pending.IsCompleted)
            return GalleryText.Get("대화 상자가 열려 있습니다.");
        if (!_pending.IsCompletedSuccessfully)
            return GalleryText.Get("대화 상자를 열 수 없습니다.") + " " + _pending.Exception?.GetBaseException().Message;
        var paths = _pending.Result;
        if (paths.Count == 0)
            return GalleryText.Get("선택을 취소했습니다.");
        var first = string.Join("\n", paths.Take(8));
        return paths.Count > 8 ? first + "\n" + GalleryText.Format("외 {0:N0}개", paths.Count - 8) : first;
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();
}
