using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Duxel.Windows.App;

[SupportedOSPlatform("windows")]
public static class DuxelWindowsApp
{
    private const uint SwHide = 0;
    private const uint SwRestore = 9;

    [ModuleInitializer]
    internal static void RegisterPlatformRunner()
    {
        DuxelApp.RegisterRunner(Run);
    }

    public static void Run(DuxelAppOptions options)
    {
        Run(options, DuxelApp.PrimarySession);
    }

    public static void Run(DuxelAppOptions options, DuxelAppSession session)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(session);

        RunWithSession(options, session);
    }

    public static void ShowModal(Func<Action, DuxelAppOptions> optionsFactory, nint ownerWindowHandle = default)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);

        Exception? modalException = null;
        using var completed = new ManualResetEventSlim(false);

        if (ownerWindowHandle != nint.Zero)
        {
            _ = EnableWindow(ownerWindowHandle, false);
        }

        var modalThread = new Thread(() =>
        {
            WindowsPlatformBackend? platform = null;
            try
            {
                var session = new DuxelAppSession();
                var options = optionsFactory(session.Exit);
                var resolvedWindow = options.Window with
                {
                    OwnerWindowHandle = options.Window.OwnerWindowHandle != nint.Zero
                        ? options.Window.OwnerWindowHandle
                        : ownerWindowHandle,
                };

                var merged = options with { Window = resolvedWindow };
                var resolvedOptions = merged with
                {
                    KeyRepeatSettingsProvider = merged.KeyRepeatSettingsProvider ?? new WindowsKeyRepeatSettingsProvider(),
                    ImageDecoder = merged.ImageDecoder ?? WindowsUiImageDecoder.Shared,
                    ClipboardFactory = merged.Clipboard is null && merged.ClipboardFactory is null
                        ? static p => p is IWin32PlatformBackend ? new WindowsClipboard() : null
                        : merged.ClipboardFactory,
                };

                platform = new WindowsPlatformBackend(new WindowsPlatformBackendOptions(
                    resolvedOptions.Window.Width,
                    resolvedOptions.Window.Height,
                    resolvedOptions.Window.MinWidth,
                    resolvedOptions.Window.MinHeight,
                    resolvedOptions.Window.Title,
                    resolvedOptions.Window.VSync,
                    resolvedOptions.Window.Resizable,
                    resolvedOptions.Window.ShowMinimizeButton,
                    resolvedOptions.Window.ShowMaximizeButton,
                    resolvedOptions.Window.CenterOnScreen,
                    resolvedOptions.Window.CenterOnOwner,
                    resolvedOptions.Window.OwnerWindowHandle,
                    resolvedOptions.Window.IconPath,
                    resolvedOptions.Window.IconData,
                    resolvedOptions.Window.WindowCreated,
                    resolvedOptions.Window.Tray,
                    resolvedOptions.KeyRepeatSettingsProvider ?? new WindowsKeyRepeatSettingsProvider(),
                    session.RequestFrame
                ));

                session.RunCore(resolvedOptions, platform);
            }
            catch (Exception ex)
            {
                modalException = ex;
            }
            finally
            {
                // Re-enable owner for Exit() path (WM_CLOSE path already handles this).
                if (ownerWindowHandle != nint.Zero)
                {
                    _ = EnableWindow(ownerWindowHandle, true);
                }

                platform?.Dispose();
                completed.Set();
            }
        })
        {
            IsBackground = false,
            Name = "DuxelModal"
        };

        modalThread.SetApartmentState(ApartmentState.STA);
        modalThread.Start();
        completed.Wait();
        modalThread.Join();

        if (modalException is not null)
        {
            ExceptionDispatchInfo.Capture(modalException).Throw();
        }
    }

    public static Task ShowModalAsync(Func<Action, DuxelAppOptions> optionsFactory, nint ownerWindowHandle = default)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);

        Exception? modalException = null;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (ownerWindowHandle != nint.Zero)
        {
            _ = EnableWindow(ownerWindowHandle, false);
        }

        var modalThread = new Thread(() =>
        {
            WindowsPlatformBackend? platform = null;
            try
            {
                var session = new DuxelAppSession();
                var options = optionsFactory(session.Exit);
                var resolvedWindow = options.Window with
                {
                    OwnerWindowHandle = options.Window.OwnerWindowHandle != nint.Zero
                        ? options.Window.OwnerWindowHandle
                        : ownerWindowHandle,
                };

                var merged = options with { Window = resolvedWindow };
                var resolvedOptions = merged with
                {
                    KeyRepeatSettingsProvider = merged.KeyRepeatSettingsProvider ?? new WindowsKeyRepeatSettingsProvider(),
                    ImageDecoder = merged.ImageDecoder ?? WindowsUiImageDecoder.Shared,
                    ClipboardFactory = merged.Clipboard is null && merged.ClipboardFactory is null
                        ? static p => p is IWin32PlatformBackend ? new WindowsClipboard() : null
                        : merged.ClipboardFactory,
                };

                platform = new WindowsPlatformBackend(new WindowsPlatformBackendOptions(
                    resolvedOptions.Window.Width,
                    resolvedOptions.Window.Height,
                    resolvedOptions.Window.MinWidth,
                    resolvedOptions.Window.MinHeight,
                    resolvedOptions.Window.Title,
                    resolvedOptions.Window.VSync,
                    resolvedOptions.Window.Resizable,
                    resolvedOptions.Window.ShowMinimizeButton,
                    resolvedOptions.Window.ShowMaximizeButton,
                    resolvedOptions.Window.CenterOnScreen,
                    resolvedOptions.Window.CenterOnOwner,
                    resolvedOptions.Window.OwnerWindowHandle,
                    resolvedOptions.Window.IconPath,
                    resolvedOptions.Window.IconData,
                    resolvedOptions.Window.WindowCreated,
                    resolvedOptions.Window.Tray,
                    resolvedOptions.KeyRepeatSettingsProvider ?? new WindowsKeyRepeatSettingsProvider(),
                    session.RequestFrame
                ));

                session.RunCore(resolvedOptions, platform);
            }
            catch (Exception ex)
            {
                modalException = ex;
            }
            finally
            {
                // Re-enable owner for Exit() path (WM_CLOSE path already handles this).
                if (ownerWindowHandle != nint.Zero)
                {
                    _ = EnableWindow(ownerWindowHandle, true);
                }

                platform?.Dispose();

                if (modalException is not null)
                {
                    tcs.SetException(modalException);
                }
                else
                {
                    tcs.SetResult();
                }
            }
        })
        {
            IsBackground = false,
            Name = "DuxelModalAsync"
        };

        modalThread.SetApartmentState(ApartmentState.STA);
        modalThread.Start();

        return tcs.Task;
    }

    public static DuxelModelessWindow ShowModeless(Func<DuxelAppSession, DuxelAppOptions> optionsFactory, Action? closed = null)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);

        Exception? startupException = null;
        using var started = new ManualResetEventSlim(false);

        var session = new DuxelAppSession();
        DuxelModelessWindow? modelessWindow = null;

        var thread = new Thread(() =>
        {
            try
            {
                var options = optionsFactory(session);
                var originalWindowCreated = options.Window.WindowCreated;
                var resolvedWindow = options.Window with
                {
                    WindowCreated = windowHandle =>
                    {
                        modelessWindow?.AttachWindowHandle(windowHandle);
                        originalWindowCreated?.Invoke(windowHandle);
                        started.Set();
                    }
                };

                started.Set();
                RunWithSession(options with { Window = resolvedWindow }, session);
            }
            catch (Exception ex)
            {
                startupException = ex;
                started.Set();
            }
            finally
            {
                modelessWindow?.MarkClosed();
                closed?.Invoke();
            }
        })
        {
            IsBackground = true,
            Name = "DuxelModeless"
        };

        thread.SetApartmentState(ApartmentState.STA);
        modelessWindow = new DuxelModelessWindow(session, thread);
        thread.Start();
        started.Wait();

        if (startupException is not null)
        {
            ExceptionDispatchInfo.Capture(startupException).Throw();
        }

        return modelessWindow;
    }

    private static void RunWithSession(DuxelAppOptions options, DuxelAppSession session)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(session);

        var resolvedOptions = options with
        {
            KeyRepeatSettingsProvider = options.KeyRepeatSettingsProvider ?? new WindowsKeyRepeatSettingsProvider(),
            ImageDecoder = options.ImageDecoder ?? WindowsUiImageDecoder.Shared,
            ClipboardFactory = options.Clipboard is null && options.ClipboardFactory is null
                ? static platform => platform is IWin32PlatformBackend ? new WindowsClipboard() : null
                : options.ClipboardFactory,
        };

        var initialWidth = resolvedOptions.Window.Width;
        var initialHeight = resolvedOptions.Window.Height;

        using var platform = new WindowsPlatformBackend(new WindowsPlatformBackendOptions(
            initialWidth,
            initialHeight,
            resolvedOptions.Window.MinWidth,
            resolvedOptions.Window.MinHeight,
            resolvedOptions.Window.Title,
            resolvedOptions.Window.VSync,
            resolvedOptions.Window.Resizable,
            resolvedOptions.Window.ShowMinimizeButton,
            resolvedOptions.Window.ShowMaximizeButton,
            resolvedOptions.Window.CenterOnScreen,
            resolvedOptions.Window.CenterOnOwner,
            resolvedOptions.Window.OwnerWindowHandle,
            resolvedOptions.Window.IconPath,
            resolvedOptions.Window.IconData,
            resolvedOptions.Window.WindowCreated,
            resolvedOptions.Window.Tray,
            resolvedOptions.KeyRepeatSettingsProvider ?? new WindowsKeyRepeatSettingsProvider(),
            session.RequestFrame
        ));

        session.RunCore(resolvedOptions, platform);
    }

    public static void Exit()
    {
        DuxelApp.Exit();
    }

    public static void HideWindow(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return;
        }

        _ = ShowWindow(windowHandle, SwHide);
    }

    public static void RestoreWindow(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return;
        }

        _ = ShowWindow(windowHandle, SwRestore);
        TransferForegroundFocus(windowHandle);
    }

    /// <summary>
    /// Reliably transfers foreground focus to the target window from any thread.
    /// Attaches input queues so SetForegroundWindow succeeds cross-thread.
    /// </summary>
    private static void TransferForegroundFocus(nint targetWindowHandle)
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == nint.Zero || foregroundWindow == targetWindowHandle)
        {
            _ = SetForegroundWindow(targetWindowHandle);
            return;
        }

        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, nint.Zero);
        var currentThreadId = GetCurrentThreadId();

        if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
        {
            _ = AttachThreadInput(currentThreadId, foregroundThreadId, true);
            _ = SetForegroundWindow(targetWindowHandle);
            _ = BringWindowToTop(targetWindowHandle);
            _ = AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
        else
        {
            _ = SetForegroundWindow(targetWindowHandle);
            _ = BringWindowToTop(targetWindowHandle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, uint nCmdShow);

    [DllImport("user32.dll")]
    private static extern nint SetActiveWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, nint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
