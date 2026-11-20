using Duxel.Core;

namespace Duxel.App;

public sealed partial class DuxelAppSession
{
    private IPlatformFileDialogs? _fileDialogs;
    private IPlatformEventWakeBackend? _wakeFileDialogs;
    private FileDialogWork? _fileDialogRequest;

    /// <summary>Shows a native dialog on this session's owner thread. No file is opened, created or written by Duxel.</summary>
    public Task<IReadOnlyList<string>> ShowFileDialogAsync(UiFileDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_platformWakeGate)
        {
            if (_fileDialogs is null || _wakeFileDialogs is null)
                return Task.FromException<IReadOnlyList<string>>(new NotSupportedException("This session has no native file dialog service."));
            if (_fileDialogRequest is not null)
                return Task.FromException<IReadOnlyList<string>>(new InvalidOperationException("A file dialog is already open."));
            var work = new FileDialogWork(request);
            _fileDialogRequest = work;
            _wakeFileDialogs.WakeEvents();
            return work.Completion.Task;
        }
    }

    private void DispatchFileDialog()
    {
        FileDialogWork? work;
        lock (_platformWakeGate)
            work = _fileDialogRequest;
        if (work is null || work.UseManagedChooser)
            return;
        IReadOnlyList<string>? paths = null;
        Exception? failure = null;
        try { paths = _fileDialogs!.ShowFileDialog(work.Request); }
        catch (UiNativeFileDialogUnavailableException)
        {
            lock (_platformWakeGate)
                work.UseManagedChooser = true;
            RequestFrame();
            return;
        }
        catch (Exception exception) { failure = exception; }
        lock (_platformWakeGate)
        {
            _fileDialogRequest = null;
            if (failure is null)
                work.Completion.SetResult(paths!);
            else
                work.Completion.SetException(failure);
        }
        RequestFrame();
    }

    private sealed class FileDialogWork(UiFileDialogRequest request)
    {
        internal UiFileDialogRequest Request { get; } = request;
        internal TaskCompletionSource<IReadOnlyList<string>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool UseManagedChooser;
    }

    private sealed class FileDialogScreen(UiScreen content, DuxelAppSession session) : UiScreen, IDisposable
    {
        private FileDialogWork? _work;
        private DuxelFileChooser? _chooser;

        public override void Render(UiImmediateContext ui)
        {
            content.Render(ui);
            FileDialogWork? next;
            lock (session._platformWakeGate)
                next = session._fileDialogRequest is { UseManagedChooser: true } work ? work : null;
            if (!ReferenceEquals(_work, next))
            {
                _chooser?.Dispose();
                _chooser = null;
                _work = next;
                if (next is not null)
                    _chooser = new(next.Request, paths => Complete(next, paths), session.RequestFrame);
            }
            _chooser?.Render(ui);
        }

        private void Complete(FileDialogWork work, IReadOnlyList<string> paths)
        {
            lock (session._platformWakeGate)
            {
                if (!ReferenceEquals(session._fileDialogRequest, work))
                    return;
                session._fileDialogRequest = null;
                work.Completion.SetResult(paths);
            }
            session.RequestFrame();
        }

        public void Dispose() => _chooser?.Dispose();
    }
}
