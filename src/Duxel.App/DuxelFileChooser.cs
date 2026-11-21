using System.Globalization;
using System.IO.Enumeration;
using Duxel.Core;

namespace Duxel.App;

/// <summary>Chooses paths exposed to the running process when the desktop picker is unavailable.</summary>
internal sealed class DuxelFileChooser : IDisposable
{
    private readonly UiFileDialogRequest _request;
    private readonly Action _requestFrame;
    private readonly UiFluentDialog _dialog;
    private readonly UiVirtualList<Rows> _list;
    private readonly UiFluentButton _accept;
    private readonly UiState<string> _location = new("");
    private readonly UiState<string> _fileName = new("");
    private readonly UiState<string> _message = new("");
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly CultureInfo _culture;
    private (string Directory, Task<Entry[]> Task)? _loading;
    private Entry[] _entries = [];
    private string _directory;
    private string? _replacePath;
    private int _filter;
    private IReadOnlyList<string> _result = Array.Empty<string>();
    private bool FoldersOnly => _request.Kind == UiFileDialogKind.SelectFolder;
    private bool Saving => _request.Kind == UiFileDialogKind.SaveFile;
    private string AcceptLabel => Saving ? Text("Save") : FoldersOnly ? Text("Select Folder") : Text("Open");

    internal DuxelFileChooser(UiFileDialogRequest request, Action<IReadOnlyList<string>> complete, Action requestFrame)
    {
        (_request, _requestFrame) = (request, requestFrame);
        _culture = CultureInfo.ReadOnly(request.Culture ?? CultureInfo.CurrentUICulture);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _directory = Directory.Exists(home) ? home : Directory.GetCurrentDirectory();
        _fileName.Value = request.SuggestedFileName ?? "";
        _list = Dux.VirtualList(new Rows(this), 0, new(ItemExtent: 36, Width: 540, Height: 228));
        _list.AccessibleName = Text("Files and folders");
        var path = Fluent.Input(Text("Current folder"), _location.Binding, 4096, 380);
        var children = new List<UiWidget>
        {
            Dux.Row(new(Spacing: 8),
                Dux.Expanded(path),
                Fluent.Button(Text("Go"), () => Load(_location.Value)),
                Fluent.Button(Text("Up"), () => Load(Directory.GetParent(_directory)?.FullName ?? _directory))),
            _list
        };
        if (!FoldersOnly && !request.AllowMultiple)
            children.Add(Fluent.Input(Text("File name"), new UiBinding<string>(() => _fileName.Value, name =>
            {
                _fileName.Value = name;
                ResetReplace();
            }), 4096, 440));
        if (request.Filters.Count > 0)
        {
            var options = request.Filters.Select((filter, index) =>
                new UiSelectOption(index.ToString(CultureInfo.InvariantCulture), filter.Name)).ToArray();
            var selected = new UiBinding<string?>(() => _filter.ToString(CultureInfo.InvariantCulture), key =>
            {
                if (key is not null && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var next) && next != _filter)
                {
                    _filter = next;
                    Load(_directory);
                }
            });
            children.Add(Fluent.Select(Text("File type"), selected, options, new(ExpandLabel : Text("Show options"))));
        }
        children.Add(Fluent.Body1(() => _message.Value));
        _accept = Fluent.Button(AcceptLabel, Accept);
        _dialog = Fluent.Dialog(request.Title ?? AcceptLabel,
            new UiColumn(children.ToArray(), new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min)),
            [_accept, Fluent.Button(Text("Cancel"), Cancel)],
            new(LightDismiss: false, MotionEnabled: false));
        _dialog.OpenChanged = open => { if (!open) complete(_result); };
        _dialog.Show();
        Load(request.InitialDirectory ?? _directory);
    }

    internal void Render(UiImmediateContext ui)
    {
        if (_loading is { } load && load.Task.IsCompleted)
        {
            _loading = null;
            if (load.Task.IsCompletedSuccessfully)
            {
                _directory = load.Directory;
                _location.Value = _directory;
                _entries = load.Task.Result;
                _selected.Clear();
                _list.ItemCount = _entries.Length;
                _list.Offset = 0;
                _list.Refresh();
                _message.Value = _entries.Length == 0 ? Text("No items to show.") : "";
            }
            else
                _message.Value = Text("Could not open the folder.")
                    + " " + load.Task.Exception?.GetBaseException().Message;
        }
        _dialog.Render(ui);
    }

    private void Load(string destination)
    {
        try
        {
            var path = Path.GetFullPath(destination, _directory);
            ResetReplace();
            var filter = _request.Filters.Count > 0 ? _request.Filters[_filter] : null;
            var task = Task.Run(() => ReadDirectory(path, filter, FoldersOnly));
            _loading = (path, task);
            _message.Value = Text("Reading the folder.");
            _ = task.ContinueWith((_, wake) => ((Action)wake!)(), _requestFrame,
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        catch (Exception error) when (error is ArgumentException or IOException or NotSupportedException)
        {
            _message.Value = Text("Could not open the folder.") + " " + error.Message;
        }
    }

    private static Entry[] ReadDirectory(string path, UiFileDialogFilter? filter, bool foldersOnly)
    {
        return new DirectoryInfo(path).EnumerateFileSystemInfos()
            .Select(info => new Entry(info.FullName, info.Name, (info.Attributes & FileAttributes.Directory) != 0))
            .Where(entry => entry.Folder || !foldersOnly
                && (filter is null || filter.Patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, entry.Name, ignoreCase: false))))
            .OrderByDescending(entry => entry.Folder)
            .ThenBy(entry => entry.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    private void Accept()
    {
        if (_loading is not null)
            return;
        if (FoldersOnly)
        {
            if (!Directory.Exists(_directory) || _selected.Any(path => !Directory.Exists(path)))
            {
                _message.Value = Text("A selected folder no longer exists.");
                return;
            }
            Close(_selected.Count == 0 ? [_directory] : SelectedPaths());
            return;
        }
        if (_request.AllowMultiple)
        {
            if (_selected.Count == 0 || _selected.Any(path => !File.Exists(path)))
            {
                _message.Value = Text("Select the files to open.");
                return;
            }
            Close(SelectedPaths());
            return;
        }
        var name = _fileName.Value.Trim();
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            _message.Value = Text("Enter a file name.");
            return;
        }
        if (Saving && _request.DefaultExtension is { } extension && !Path.HasExtension(name))
            name += "." + extension;
        var target = Path.Combine(_directory, name);
        if ((!Saving && !File.Exists(target)) || (Saving && (Directory.Exists(target) || !Directory.Exists(_directory))))
        {
            _message.Value = Text("This file path cannot be selected.");
            return;
        }
        if (Saving && File.Exists(target) && _replacePath != target)
        {
            _replacePath = target;
            _accept.Label = Text("Replace");
            _message.Value = Text("This file already exists. Select Replace to use this path.");
            return;
        }
        Close([target]);
    }

    private void ResetReplace()
    {
        _replacePath = null;
        _accept.Label = AcceptLabel;
    }

    private void Select(Entry entry, bool value)
    {
        if (value) _selected.Add(entry.Path);
        else _selected.Remove(entry.Path);
        _message.Value = _selected.Count == 0 ? "" : string.Format(_culture, Text("{0:N0} selected"), _selected.Count);
    }

    private string[] SelectedPaths() => _entries.Where(entry => _selected.Contains(entry.Path)).Select(entry => entry.Path).ToArray();

    private void Cancel() => Close([]);

    private void Close(string[] paths)
    {
        _result = Array.AsReadOnly(paths);
        _dialog.Close();
    }

    private string Text(string source) => FileChooserLocalization.GetText(source, _culture);
    public void Dispose() => _dialog.Dispose();

    private sealed record Entry(string Path, string Name, bool Folder);

    private sealed class Rows(DuxelFileChooser owner) : IUiVirtualListSource
    {
        public string GetKey(int index) => owner._entries[index].Path;

        public UiWidget Create(int index)
        {
            var entry = owner._entries[index];
            if (entry.Folder)
            {
                var open = Fluent.Button(string.Format(owner._culture, owner.Text("Open folder {0}"), entry.Name), () => owner.Load(entry.Path));
                return owner.FoldersOnly && owner._request.AllowMultiple
                    ? Dux.Row(new(Spacing: 8), Check(entry), open)
                    : open;
            }
            return owner._request.AllowMultiple ? Check(entry) : Fluent.Button(entry.Name, () =>
            {
                owner.ResetReplace();
                owner._fileName.Value = entry.Name;
            });
        }

        private UiWidget Check(Entry entry) => Fluent.Checkbox(entry.Name, new UiBinding<bool>(
            () => owner._selected.Contains(entry.Path), selected => owner.Select(entry, selected)));

        public void Bind(UiWidget widget, int index) { }
    }
}
