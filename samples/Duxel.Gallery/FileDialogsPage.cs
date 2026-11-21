using Duxel.App;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class FileDialogsPage : IObserverPage, IObserverNaturalPage
{
    private readonly UiWidget _root;
    private readonly DuxelAppSession? _session;
    private Task<IReadOnlyList<string>>? _pending;

    public string Title => "파일 대화 상자";
    public string Description => "Choose a file or folder and inspect the returned path.";
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal FileDialogsPage(DuxelAppSession? session)
    {
        _session = session;
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var start = Directory.Exists(directory) ? directory : null;
        var allFiles = new UiFileDialogFilter(GalleryText.Get("All files"), "*");
        var textFiles = new UiFileDialogFilter(GalleryText.Get("Text files"), "*.txt");
        var open = new UiFileDialogRequest(UiFileDialogKind.OpenFile, GalleryText.Get("Choose files to open"), start,
            allowMultiple: true, filters: [allFiles, textFiles]);
        var save = new UiFileDialogRequest(UiFileDialogKind.SaveFile, GalleryText.Get("Choose where to save"), start,
            suggestedFileName: GalleryText.Get("Notes.txt"), defaultExtension: "txt", filters: [textFiles]);
        var folder = new UiFileDialogRequest(UiFileDialogKind.SelectFolder, GalleryText.Get("Select folder"), start);
        _root = Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                Fluent.Button(GalleryText.Get("Open files"), () => Show(open)),
                Fluent.Button(GalleryText.Get("Save as"), () => Show(save)),
                Fluent.Button(GalleryText.Get("Select folder"), () => Show(folder))),
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
            return GalleryText.Get("Open the system dialog from the running app.");
        if (_pending is null)
            return GalleryText.Get("Selected paths will appear here.");
        if (!_pending.IsCompleted)
            return GalleryText.Get("The dialog is open.");
        if (!_pending.IsCompletedSuccessfully)
            return GalleryText.Get("Could not open the dialog.") + " " + _pending.Exception?.GetBaseException().Message;
        var paths = _pending.Result;
        if (paths.Count == 0)
            return GalleryText.Get("Selection canceled.");
        var first = string.Join("\n", paths.Take(8));
        return paths.Count > 8 ? first + "\n" + GalleryText.Format("And {0:N0} more", paths.Count - 8) : first;
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();
}
