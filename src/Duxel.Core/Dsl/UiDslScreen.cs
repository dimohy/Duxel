using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Duxel.Core.Dsl;

/// <summary>
/// A <see cref="UiScreen"/> that renders a DSL <c>.ui</c> file and optionally hot-reloads
/// a <c>.duxel-theme</c> file. In managed builds the files are watched and reloaded automatically.
/// In NativeAOT builds, pre-compiled source-generated renderers and themes are used instead.
/// </summary>
public sealed class UiDslScreen : UiScreen, IDisposable
{
    private readonly Action<IUiDslEmitter> _dslRender;
    private readonly UiDslState _dslState;
    private readonly IUiDslEventSink? _eventSink;
    private readonly IUiDslValueSource? _valueSource;

    // Theme hot-reload state
    private sealed record ThemeBox(UiTheme Theme);
    private ThemeBox? _pendingTheme;
    private readonly IDisposable? _themeWatcher = null;

    /// <summary>Optional trace callback for diagnostic logging.</summary>
    public Action<string>? Trace { get; set; }

    /// <summary>
    /// Queues a theme to be applied at the next frame. Thread-safe.
    /// </summary>
    public void RequestTheme(UiTheme theme)
    {
        Volatile.Write(ref _pendingTheme, new ThemeBox(theme));
    }

    /// <summary>
    /// Creates a DSL screen with hot-reload support (managed) or source-generated rendering (NativeAOT).
    /// </summary>
    /// <param name="uiPath">Relative path to the <c>.ui</c> file (e.g. <c>"Ui/Main.ui"</c>).</param>
    /// <param name="themePath">Optional relative path to a <c>.duxel-theme</c> file.</param>
    /// <param name="eventSink">Optional DSL event sink for button/checkbox callbacks.</param>
    /// <param name="valueSource">Optional external value source for DSL bindings.</param>
    public UiDslScreen(
        string uiPath,
        string? themePath = null,
        IUiDslEventSink? eventSink = null,
        IUiDslValueSource? valueSource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uiPath);

        _dslState = new UiDslState();
        _eventSink = eventSink;
        _valueSource = valueSource;

#if DUX_NATIVEAOT
        _dslRender = UiDslAuto.Render(uiPath);

        if (themePath is not null)
        {
            _pendingTheme = new ThemeBox(UiThemeAuto.Resolve(themePath));
        }
#else
        var resolvedUiPath = UiDslSourcePathResolver.ResolveFromProjectRoot(uiPath);
        _dslRender = UiDslPipeline.CreateHotReloadRenderer(resolvedUiPath);

        if (themePath is not null)
        {
            var resolvedThemePath = UiDslSourcePathResolver.ResolveFromProjectRoot(themePath);
            _themeWatcher = new ThemeFileWatcher(resolvedThemePath, theme =>
            {
                Trace?.Invoke($"Theme loaded from: {resolvedThemePath}");
                Volatile.Write(ref _pendingTheme, new ThemeBox(theme));
            }, msg => Trace?.Invoke(msg));
        }
#endif
    }

    public override void Render(UiImmediateContext ui)
    {
        // Apply pending theme from hot-reload watcher
        var pendingBox = Interlocked.Exchange(ref _pendingTheme, null);
        if (pendingBox is not null)
        {
            Trace?.Invoke("Theme hot-reload applied.");
            ui.RequestTheme(pendingBox.Theme);
        }

        // Create minimal render context for DSL dispatcher (event routing + state only)
        var ctx = new UiDslRenderContext(
            _dslState,
            null,
            default,
            0f,
            default,
            default,
            default,
            UiStyle.Default,
            default,
            default,
            false, false, false, false, false, false,
            0f, 0f,
            Array.Empty<UiKeyEvent>(),
            Array.Empty<UiCharEvent>(),
            null,
            default,
            default,
            null,
            0, 0, 0,
            _eventSink,
            _valueSource
        );

        var emitter = new ScreenEmitter(ui, ctx);
        _dslRender(emitter);
    }

    public void Dispose()
    {
        (_themeWatcher as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Lightweight <see cref="IUiDslEmitter"/> that delegates to the screen's
    /// <see cref="UiImmediateContext"/> via <see cref="UiDslWidgetDispatcher"/>.
    /// </summary>
    private sealed class ScreenEmitter(UiImmediateContext ui, UiDslRenderContext ctx) : IUiDslEmitter
    {
        private readonly UiDslRuntimeState _runtimeState = new();
        private int _skipDepth;

        // ForEach buffering state
        private List<(bool IsBegin, string Name, IReadOnlyList<string> Args)>? _forEachBuffer;
        private int _forEachBufferDepth;
        private int _forEachStart;
        private int _forEachEnd;
        private string _forEachIndexVar = "_index";

        public void BeginNode(string name, IReadOnlyList<string> args)
        {
            // If buffering for ForEach, capture all child nodes
            if (_forEachBuffer is not null)
            {
                _forEachBuffer.Add((true, name, args));
                if (UiDslWidgetDispatcher.IsContainer(name))
                    _forEachBufferDepth++;
                return;
            }

            if (_skipDepth > 0)
            {
                if (UiDslWidgetDispatcher.IsContainer(name))
                {
                    _skipDepth++;
                }
                return;
            }

            // ForEach: start buffering children
            if (name is "ForEach")
            {
                ParseForEachArgs(args);
                _forEachBuffer = [];
                _forEachBufferDepth = 0;

                var stylePushCount = UiDslStyleHelper.PushInlineStyles(ui, args);
                _runtimeState.StyleColorPushCounts.Push(stylePushCount);
                return;
            }

            var stylePushCount2 = UiDslStyleHelper.PushInlineStyles(ui, args);

            var result = UiDslWidgetDispatcher.BeginOrInvoke(ui, ctx, _runtimeState, name, args);
            if (result == UiDslBeginResult.SkipChildren)
            {
                _skipDepth = 1;
            }

            if (UiDslWidgetDispatcher.IsContainer(name))
            {
                _runtimeState.StyleColorPushCounts.Push(stylePushCount2);
            }
            else
            {
                if (stylePushCount2 > 0)
                {
                    ui.PopStyleColor(stylePushCount2);
                }
            }
        }

        public void EndNode(string name)
        {
            // If buffering for ForEach, capture EndNode
            if (_forEachBuffer is not null)
            {
                if (_forEachBufferDepth > 0)
                {
                    _forEachBuffer.Add((false, name, Array.Empty<string>()));
                    if (UiDslWidgetDispatcher.IsContainer(name))
                        _forEachBufferDepth--;
                    return;
                }

                // EndNode("ForEach") — replay buffer for each index
                var buffer = _forEachBuffer;
                _forEachBuffer = null;

                for (int i = _forEachStart; i <= _forEachEnd; i++)
                {
                    ctx.State.SetInt(_forEachIndexVar, i);
                    foreach (var (isBegin, nodeName, nodeArgs) in buffer)
                    {
                        if (isBegin)
                        {
                            var processed = SubstituteTemplates(nodeArgs, i, _forEachIndexVar);
                            BeginNode(nodeName, processed);
                        }
                        else
                        {
                            EndNode(nodeName);
                        }
                    }
                }

                // Pop ForEach style colors
                if (_runtimeState.StyleColorPushCounts.Count > 0)
                {
                    var count = _runtimeState.StyleColorPushCounts.Pop();
                    if (count > 0)
                        ui.PopStyleColor(count);
                }
                return;
            }

            if (_skipDepth > 0)
            {
                if (UiDslWidgetDispatcher.IsContainer(name))
                {
                    _skipDepth--;

                    // Pop style count pushed during BeginNode for this skipped container
                    if (_runtimeState.StyleColorPushCounts.Count > 0)
                    {
                        var count = _runtimeState.StyleColorPushCounts.Pop();
                        if (count > 0)
                            ui.PopStyleColor(count);
                    }
                }
                return;
            }

            UiDslWidgetDispatcher.End(ui, ctx, _runtimeState, name);

            if (UiDslWidgetDispatcher.IsContainer(name) && _runtimeState.StyleColorPushCounts.Count > 0)
            {
                var count = _runtimeState.StyleColorPushCounts.Pop();
                if (count > 0)
                {
                    ui.PopStyleColor(count);
                }
            }
        }

        private void ParseForEachArgs(IReadOnlyList<string> args)
        {
            _forEachStart = 0;
            _forEachEnd = 0;
            _forEachIndexVar = "_index";

            foreach (var arg in args)
            {
                if (arg.StartsWith("Range=", StringComparison.OrdinalIgnoreCase))
                {
                    var range = arg.AsSpan(6);
                    var sep = range.IndexOf(',');
                    if (sep > 0)
                    {
                        int.TryParse(range[..sep], NumberStyles.Integer, CultureInfo.InvariantCulture, out _forEachStart);
                        int.TryParse(range[(sep + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out _forEachEnd);
                    }
                    else
                    {
                        int.TryParse(range, NumberStyles.Integer, CultureInfo.InvariantCulture, out _forEachEnd);
                        _forEachStart = 0;
                    }
                }
                else if (arg.StartsWith("Count=", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(arg.AsSpan(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count);
                    _forEachStart = 0;
                    _forEachEnd = count - 1;
                }
                else if (arg.StartsWith("Var=", StringComparison.OrdinalIgnoreCase))
                {
                    _forEachIndexVar = arg[4..];
                }
            }
        }

        private static IReadOnlyList<string> SubstituteTemplates(IReadOnlyList<string> args, int index, string varName)
        {
            var placeholder = $"{{{varName}}}";
            var hasTemplate = false;
            foreach (var arg in args)
            {
                if (arg.Contains(placeholder, StringComparison.Ordinal))
                {
                    hasTemplate = true;
                    break;
                }
            }

            if (!hasTemplate) return args;

            var result = new string[args.Count];
            var indexStr = index.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < args.Count; i++)
            {
                result[i] = args[i].Replace(placeholder, indexStr, StringComparison.Ordinal);
            }
            return result;
        }
    }

#if !DUX_NATIVEAOT
    /// <summary>
    /// Watches a <c>.duxel-theme</c> file for changes and invokes a callback with the new theme.
    /// </summary>
    private sealed class ThemeFileWatcher : IDisposable
    {
        private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(120);
        private readonly string _themePath;
        private readonly Action<UiTheme> _onThemeLoaded;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _reloadTimer;
        private readonly Action<string>? _trace;

        public ThemeFileWatcher(string themePath, Action<UiTheme> onThemeLoaded, Action<string>? trace = null)
        {
            _themePath = Path.GetFullPath(themePath);
            _onThemeLoaded = onThemeLoaded;
            _trace = trace;

            // Load initial theme
            LoadTheme();

            var directory = Path.GetDirectoryName(_themePath)
                ?? throw new InvalidOperationException("Theme path has no directory.");
            var fileName = Path.GetFileName(_themePath);

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileChanged;

            _reloadTimer = new Timer(_ => LoadTheme(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            _trace?.Invoke($"Theme file change detected: {e.ChangeType} {e.FullPath}");
            _reloadTimer.Change(ReloadDelay, Timeout.InfiniteTimeSpan);
        }

        private void LoadTheme()
        {
            try
            {
                using var stream = new FileStream(_themePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();
                var def = UiThemeParser.Parse(text);
                var theme = UiThemeCompiler.Apply(def);
                _onThemeLoaded(theme);
            }
            catch (Exception ex)
            {
                _trace?.Invoke($"Theme load failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _reloadTimer.Dispose();
        }
    }
#endif
}
