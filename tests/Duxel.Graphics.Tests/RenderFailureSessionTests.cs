using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.InteropServices;

internal static partial class RenderFailureSessionTests
{
    internal static void Run(Action<bool,string> check)
    {
        var session=new DuxelAppSession();var log=new List<string>();
        using var platform=new WindowsPlatformBackend(new(){Width=320,Height=240,Title="Duxel owned render failure regression",
            UseDuxelTitleBar=false,VSync=false,KeyRepeatSettingsProvider=new WindowsKeyRepeatSettingsProvider(),TrayOptions=new(),FrameInvalidated=session.RequestFrame});
        var observed=false;
        try
        {
            session.RunCore(new(){Screen=new BrokenScreen(),Window=new(){UseDuxelTitleBar=false,VSync=false},
                Font=new(){FastStartup=false,SecondaryFontPath=null},Frame=new(){UseEventDrivenPlatformLoop=true,EnableRenderPipeline=true},
                Debug=new(){Log=log.Add}},platform);
        }
        catch(RenderProbeException){observed=true;}
        check(observed,"a render-thread exception propagates to the session caller after cleanup");
        check(log.Any(value=>value.Contains("RenderThreadException",StringComparison.Ordinal)),"the render failure retains its diagnostic instead of looking like a successful smoke run");
        var exitSession=new DuxelAppSession();
        using var exitPlatform=new WindowsPlatformBackend(new(){Width=320,Height=240,Title="Duxel owned shutdown message regression",
            VSync=false,KeyRepeatSettingsProvider=new WindowsKeyRepeatSettingsProvider(),TrayOptions=new(),FrameInvalidated=exitSession.RequestFrame});
        var exitScreen=new ExitScreen(exitSession,exitPlatform);
        exitSession.RunCore(new(){Screen=exitScreen,Window=new(){VSync=false},Font=new(){FastStartup=false,SecondaryFontPath=null}},exitPlatform);
        check(exitScreen.Dispatched,"shutdown dispatches a synchronous render-thread window message after cancellation instead of blocking in Join");
        foreach (var pipeline in new[] { false, true })
        {
            RunCloseOnFreshOwner(check, pipeline, restore : false);
            RunCloseOnFreshOwner(check, pipeline, restore : true);
        }
    }
    private static void RunCloseOnFreshOwner(Action<bool, string> check, bool pipeline, bool restore)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        // WM_QUIT belongs to an owner thread; each independent native session gets its own queue.
        var owner = new Thread(() =>
        {
            try { RunNativeClose(check, pipeline, restore); }
            catch (Exception error) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error); }
        }) { IsBackground = true, Name = "OwnedCloseSessionOwner" };
        owner.Start();
        if (!owner.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("Native close session owner did not stop.");
        failure?.Throw();
    }
    private static void RunNativeClose(Action<bool, string> check, bool pipeline, bool restore)
    {
        var session = new DuxelAppSession();
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned zero-size shutdown regression", VSync = false,
            UseDuxelTitleBar = false, KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new(), FrameInvalidated = session.RequestFrame
        });
        using var screen = new ClosingScreen();
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var controller = new Thread(() =>
        {
            try
            {
                void Wait(Func<bool> condition, string label)
                {
                    var elapsed = System.Diagnostics.Stopwatch.StartNew();
                    while (!condition())
                    {
                        if (elapsed.Elapsed.TotalSeconds > 5) throw new TimeoutException(label);
                        Thread.Sleep(5);
                    }
                }
                Wait(() => Interlocked.Read(ref screen.Presented) > 0, "first shutdown-probe presentation");
                platform.MinimizeWindow();
                Wait(() => platform.WindowSize.Width == 0 || platform.FramebufferSize.Width == 0, "minimized zero-sized snapshot");
                Thread.Sleep(100);
                var paused = Volatile.Read(ref screen.Frames);
                session.RequestFrame();
                Thread.Sleep(100);
                check(Volatile.Read(ref screen.Frames) == paused,
                    "zero-sized client waits without invoking layout / pipeline " + pipeline + " / restore " + restore);
                if (restore)
                {
                    if (SendMessageTimeoutW(platform.WindowHandle, 0x0112, 0xF120, 0, 2, 1000, out _) == 0)
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "Restore message failed.");
                    session.RequestFrame();
                    Wait(() => Volatile.Read(ref screen.Frames) > paused, "restored client resumes rendering");
                }
                platform.CloseWindow();
            }
            catch (Exception error) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error); }
            finally { session.Exit(); }
        }) { IsBackground = true, Name = "OwnedZeroSizeCloseController" };
        controller.Start();
        try
        {
            session.RunCore(new()
            {
                Screen = screen,
                Window = new() { Width = 640, Height = 480, VSync = false, UseDuxelTitleBar = false },
                Font = new() { FastStartup = false, SecondaryFontPath = null },
                Frame = new() { EnableIdleFrameSkip = true, UseEventDrivenPlatformLoop = true, EnableRenderPipeline = pipeline }
            }, platform);
        }
        finally
        {
            session.Exit();
            if (!controller.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Close controller did not stop.");
        }
        failure?.Throw();
        check(platform.ShouldClose, "native close returns normally after zero-size lifecycle / pipeline " + pipeline + " / restore " + restore);
    }
    private sealed class ClosingScreen : UiScreen, IDisposable
    {
        private readonly UiWidget _row = Dux.Row(new(), Fluent.Button("Visible client"));
        internal int Frames;
        internal long Presented;
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 8);
            _row.Render(ui);
            Interlocked.Increment(ref Frames);
            Interlocked.Exchange(ref Presented, (long)ui.GetRendererFrameMetrics().PresentationSequence);
            new UiControlContext(ui).RequestAnimation();
        }
        public void Dispose() => _row.Dispose();
    }
    private sealed class RenderProbeException:Exception;
    private sealed class BrokenScreen:UiScreen
    {public override void Render(UiImmediateContext ui)=>throw new RenderProbeException();}
    private sealed class ExitScreen(DuxelAppSession session,WindowsPlatformBackend platform):UiScreen
    {
        internal bool Dispatched;
        public override void Render(UiImmediateContext ui)
        {
            var initialCancellations=platform.ValidationResizeCancellations;
            session.Exit();
            var elapsed=System.Diagnostics.Stopwatch.StartNew();
            // Exit cancels once; RunCore's finally cancels again before joining.
            // Send only after the UI owner has reached that shutdown boundary.
            while(platform.ValidationResizeCancellations<initialCancellations+2)
            {if(elapsed.Elapsed.TotalSeconds>2)throw new TimeoutException("Session did not begin shutdown.");Thread.Yield();}
            if(SendMessageTimeoutW(platform.WindowHandle,0,0,0,2,1000,out _)==0)
                throw new TimeoutException("Shutdown did not dispatch the render-thread window message.");
            Dispatched=true;
        }
    }
    [LibraryImport("user32.dll",EntryPoint="SendMessageTimeoutW",SetLastError=true)]
    private static partial nint SendMessageTimeoutW(nint window,uint message,nuint wParam,nint lParam,uint flags,uint timeout,out nuint result);
}
