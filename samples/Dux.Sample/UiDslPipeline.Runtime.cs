using System;
using System.IO;
using System.Threading;
using Dux.Core.Dsl;

internal static partial class UiDslPipeline
{
    private static partial Action<IUiDslEmitter> CreateRendererCore(string uiName, string uiPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uiName);
        ArgumentException.ThrowIfNullOrWhiteSpace(uiPath);

        var renderer = new UiDslHotReloadRenderer(uiPath);
        return renderer.Render;
    }

    private sealed class UiDslHotReloadRenderer
    {
        private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(120);
        private readonly string _uiPath;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _reloadTimer;
        private Action<IUiDslEmitter> _render;

        public UiDslHotReloadRenderer(string uiPath)
        {
            _uiPath = uiPath;
            _render = BuildRenderer(ReadFileText(uiPath));

            var directory = Path.GetDirectoryName(uiPath) ?? throw new InvalidOperationException("UI path has no directory.");
            var fileName = Path.GetFileName(uiPath);

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileChanged;

            _reloadTimer = new Timer(_ => Reload(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Render(IUiDslEmitter emitter)
        {
            var render = Volatile.Read(ref _render);
            render(emitter);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
            => _reloadTimer.Change(ReloadDelay, Timeout.InfiniteTimeSpan);

        private void Reload()
        {
            try
            {
                var text = ReadFileText(_uiPath);
                var next = BuildRenderer(text);
                Volatile.Write(ref _render, next);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _render, _ => throw new InvalidOperationException("UI DSL hot reload failed.", ex));
            }
        }

        private static Action<IUiDslEmitter> BuildRenderer(string dslText)
        {
            var doc = UiDslParser.Parse(dslText);
            return emitter => doc.Emit(emitter);
        }

        private static string ReadFileText(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
