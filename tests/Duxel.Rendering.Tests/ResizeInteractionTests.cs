using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class ResizeInteractionTests
{
    internal static unsafe void Run(Action<bool,string> check,int frameSlots,string? output=null,int cycles=3,int delayMs=8,bool systemTitle=false,bool synchronous=false,bool diagnostics=true,UiScreen? content=null,bool vsync=true,Func<bool>? contentReady=null,bool verifyResizeWake=false)
    {
        if(cycles is <1 or >30)throw new ArgumentOutOfRangeException(nameof(cycles),"Resize qualification supports1..30cycles.");
        Duxel.Vulkan.VulkanRendererBackend.ValidationResizeDiagnostics=synchronous&&diagnostics;
        Duxel.Vulkan.VulkanRendererBackend.ValidationSwapchainCreates=0;
        // Four WSI entry phases; budget two attempts per requested size plus startup.
        Duxel.Vulkan.VulkanRendererBackend.ValidationResizeEventBudget=128+cycles*48*8;
        Duxel.Vulkan.VulkanRendererBackend.ValidationResizeEventPath=synchronous&&diagnostics&&output is not null?Path.Combine(output,"wsi-entries.log"):null;
        // Keep the bounded, immediately flushed trace open. Reopening it for
        // every WSI entry adds filesystem work to the very transaction measured.
        using var eventWriter=Duxel.Vulkan.VulkanRendererBackend.ValidationResizeEventPath is {} eventPath
            ?new StreamWriter(eventPath,append:false){AutoFlush=true}:null;
        Duxel.Vulkan.VulkanRendererBackend.ValidationResizeEventWriter=eventWriter;
        using var passWriter=output is not null?new StreamWriter(Path.Combine(output,"passes.jsonl"),append:false){AutoFlush=true}:null;
        var passJson=new ResizeTraceJson(new System.Text.Json.JsonSerializerOptions{WriteIndented=false});
        var session=new DuxelAppSession();
        using var platform=new WindowsPlatformBackend(new(){Width=640,Height=480,MinWidth=100,MinHeight=100,
            Title="Duxel owned resize regression",UseDuxelTitleBar=!systemTitle,DuxelTitleBarHeight=48,IntegrateSystemChrome=true,VSync=vsync,
            Resizable=true,ShowMinimizeButton=true,ShowMaximizeButton=true,
            KeyRepeatSettingsProvider=new WindowsKeyRepeatSettingsProvider(),TrayOptions=new(),FrameInvalidated=session.RequestFrame});
        using var screen=new ResizeScreen(platform,content);ExceptionDispatchInfo? failure=null;
        using var sizeSampled = new ManualResetEventSlim(false);
        var pauseAfterSizeSample = 0;
        bool PauseAtFrameRequest()
        {
            if (Interlocked.Exchange(ref pauseAfterSizeSample, 0) != 0)
            {
                sizeSampled.Set();
                if (!SpinWait.SpinUntil(() => platform.ValidationResizePresentationWaitActive, TimeSpan.FromSeconds(2)))
                    throw new TimeoutException("The resize commit did not reach its presentation wait.");
            }
            return false;
        }
        using var observation=synchronous&&diagnostics&&output is not null?new Timer(_=>
            File.AppendAllText(Path.Combine(output,"live-waits.log"),$"{DateTime.UtcNow:O} phase={Duxel.Vulkan.VulkanRendererBackend.ValidationWaitPhase} {Duxel.Vulkan.VulkanRendererBackend.ValidationResizeSnapshot}{Environment.NewLine}"),null,1000,1000):null;
        var passes=new List<ResizePass>();
        using var observedProcess=Process.GetCurrentProcess();
        var controller=new Thread(()=>
        {
            try
            {
                void Wait(Func<bool> condition,string label)
                {
                    var timer=Stopwatch.StartNew();
                    while(!condition()) {if(timer.Elapsed.TotalSeconds>5)throw new TimeoutException($"{label}: frames={screen.Frames},ui={screen.Width}x{screen.Height},platform={platform.WindowSize},scale={platform.ContentScale},resize={platform.IsInteractingResize},sequence={platform.InteractiveResizeSequence},clicks={screen.Clicks},mouse={screen.MouseX},{screen.MouseY},down={screen.MouseDown},releases={screen.Releases},buttonCenterPx={screen.ButtonX},{screen.ButtonY},presentations={screen.Presentations},retired={screen.RetiredSwapchains},pendingTargets={screen.PendingTargets},pendingBytes={screen.PendingTargetBytes},msaaBytes={screen.MsaaBytes}");Thread.Sleep(5);}
                }
                Wait(()=>platform.ValidationWindowShown,"first frame presented and native window shown");
                // Background launchers can supply SW_HIDE for the first ShowWindow.
                // This owned live-resize test needs a real visible client surface.
                _=ShowWindow(platform.WindowHandle,4);
                Wait(()=>IsWindowVisible(platform.WindowHandle),"owned resize window is actually visible");
                Wait(()=>contentReady?.Invoke()??true,"gallery source viewport ready");
                if (verifyResizeWake)
                {
                    Thread.Sleep(300);
                    if (SendMessageTimeoutW(platform.WindowHandle, 0x231, 0, 0, 2, 3000, out _) == 0)
                        throw new TimeoutException("Enter idle resize.");
                    var size = platform.FramebufferSize;
                    var expectedSequence = platform.InteractiveResizeSequence + 1;
                    Volatile.Write(ref pauseAfterSizeSample, 1);
                    session.RequestFrame();
                    if (!sizeSampled.Wait(TimeSpan.FromSeconds(3)))
                        throw new TimeoutException("The render loop did not sample the previous extent.");

                    // Equal extents let the old frame present successfully, so Vulkan
                    // has no reason to retry even though a newer size commit is waiting.
                    var packedSize = (nint)((size.Height << 16) | size.Width);
                    if (SendMessageTimeoutW(platform.WindowHandle, 0x0005, 0, packedSize, 2, 3000, out _) == 0)
                        throw new TimeoutException("An idle size commit was not acknowledged after its wake was consumed.");
                    check(platform.ValidationPresentedResizeSequence >= expectedSequence,
                        "A size commit after frame sampling is presented even when its wake was already consumed");
                    if (SendMessageTimeoutW(platform.WindowHandle, 0x232, 0, 0, 2, 3000, out _) == 0)
                        throw new TimeoutException("Exit idle resize.");
                }
                for(var pass=0;pass<cycles;pass++)
                {
                    Console.WriteLine($"Resize phase: pass {pass} start");
                    if(SendMessageTimeoutW(platform.WindowHandle,0x231,0,0,2,3000,out _)==0)throw new TimeoutException("enter resize");
                    var presentedBefore=Volatile.Read(ref screen.Presentations);
                    var traceClock=Stopwatch.StartNew();
                    double sizingMs=0,sizeMs=0,peakSizeMs=0;
                    for(var step=0;step<48;step++)
                    {
                        var offset=step<24?step:47-step;
                        var w=680+offset*17;var h=520+offset*11;
                        if(!GetWindowRect(platform.WindowHandle,out var rect))throw new InvalidOperationException("owned window rect");
                        rect.Right=rect.Left+w;rect.Bottom=rect.Top+h;
                        var callStart=Stopwatch.GetTimestamp();
                        if(SendMessageTimeoutW(platform.WindowHandle,0x214,8,(nint)(&rect),2,3000,out _)==0)throw new TimeoutException("sizing handshake");
                        sizingMs+=Stopwatch.GetElapsedTime(callStart).TotalMilliseconds;
                        var flags=0x0002u|0x0004u|0x0010u|(synchronous?0u:0x4000u);
                        callStart=Stopwatch.GetTimestamp();
                        if(!SetWindowPos(platform.WindowHandle,0,0,0,w,h,flags))throw new InvalidOperationException("resize queue");
                        var sizeCallMs=Stopwatch.GetElapsedTime(callStart).TotalMilliseconds;
                        sizeMs+=sizeCallMs;peakSizeMs=Math.Max(peakSizeMs,sizeCallMs);
                        if(systemTitle&&synchronous)
                        {
                            var committedSequence=platform.InteractiveResizeSequence;
                            if(platform.ValidationPresentedResizeSequence<committedSequence)
                                throw new InvalidOperationException("Native size returned before its committed frame was submitted.");
                        }
                        if(delayMs>0)Thread.Sleep(delayMs);
                    }
                    Wait(()=>Volatile.Read(ref screen.Presentations)>presentedBefore+1,"presentations continue during continuous native sizing");
                    if(SendMessageTimeoutW(platform.WindowHandle,0x232,0,0,2,3000,out _)==0)throw new TimeoutException("exit resize");
                    observedProcess.Refresh();
                    passes.Add(new(pass,traceClock.Elapsed.TotalMilliseconds,Volatile.Read(ref screen.Presentations)-presentedBefore,observedProcess.PrivateMemorySize64)
                    {RetiredSwapchains=screen.RetiredSwapchains,PendingTargets=screen.PendingTargets,PendingTargetBytes=screen.PendingTargetBytes,MsaaBytes=screen.MsaaBytes,
                        SizingMilliseconds=sizingMs,SizeMilliseconds=sizeMs,PeakSizeMilliseconds=peakSizeMs,
                        Gc0=GC.CollectionCount(0),Gc1=GC.CollectionCount(1),Gc2=GC.CollectionCount(2),GcPauseMilliseconds=GC.GetTotalPauseDuration().TotalMilliseconds,
                        AllocatedBytes=GC.GetTotalAllocatedBytes(),CpuMilliseconds=observedProcess.TotalProcessorTime.TotalMilliseconds,
                        SwapchainCreates=Volatile.Read(ref Duxel.Vulkan.VulkanRendererBackend.ValidationSwapchainCreates)});
                    passWriter?.WriteLine(System.Text.Json.JsonSerializer.Serialize(passes[^1],passJson.ResizePass));
                    // Input goes only to the window owned by this regression test.
                    if(synchronous)
                    {
                        var tree=platform.AccessibilityTree;var elementId=tree.Root.First;var invoked=false;
                        while(elementId is not null&&tree.TryGet(elementId,out var element))
                        {
                            if(element.Node.Name=="Resize action")
                            {
                                var result=tree.RequestAction(elementId,element.RuntimeId,UiAccessibilityAction.Invoke);
                                if(result!=UiAccessibilityResult.Success)
                                    throw new InvalidOperationException($"resize automation action was not processed: {result}; frames={screen.Frames}, presentations={screen.Presentations}, resize={platform.IsInteractingResize}, sequence={platform.InteractiveResizeSequence}");
                                invoked=true;break;
                            }
                            elementId=element.Next;
                        }
                        if(!invoked)throw new InvalidOperationException("resize action is absent from the published accessibility tree");
                        var expectedAction=pass+1;Wait(()=>Volatile.Read(ref screen.Clicks)==expectedAction,"real action callback after resize");
                        Console.WriteLine($"Resize phase: pass {pass} action complete");continue;
                    }
                    var buttonPoint=(nint)((Volatile.Read(ref screen.ButtonY)<<16)|Volatile.Read(ref screen.ButtonX));
                    Volatile.Write(ref screen.Held,0);
                    PostMessageW(platform.WindowHandle,0x200,0,buttonPoint);
                    PostMessageW(platform.WindowHandle,0x201,1,buttonPoint);
                    Wait(()=>Volatile.Read(ref screen.Held)>0,"button press after resize");
                    PostMessageW(platform.WindowHandle,0x202,0,buttonPoint);
                    var expected=pass+1;Wait(()=>Volatile.Read(ref screen.Clicks)==expected,"button release after resize");
                    Volatile.Write(ref screen.Held,0);
                    Console.WriteLine($"Resize phase: pass {pass} input complete");
                }
            }
            catch(Exception error){Console.Error.WriteLine($"{error}; Vulkan phase={Duxel.Vulkan.VulkanRendererBackend.ValidationWaitPhase}");failure=ExceptionDispatchInfo.Capture(error);}
            finally{Console.WriteLine("Resize phase: requesting session exit");session.Exit();}
        }){IsBackground=true,Name="OwnedResizeController"};
        controller.Start();
        session.RunCore(new(){Screen=screen,Window=new(){Width=640,Height=480,VSync=vsync,UseDuxelTitleBar=!systemTitle},
            Renderer=new(){MaxFramesInFlight=frameSlots},
            Font=new(){FastStartup=!verifyResizeWake},
            Frame=new(){UseEventDrivenPlatformLoop=verifyResizeWake, IsAnimationActiveProvider=verifyResizeWake ? PauseAtFrameRequest : null}},platform);
        Console.WriteLine("Resize phase: session disposed");
        if(!controller.Join(10000))throw new TimeoutException("resize controller exit");
        if(output is not null)File.WriteAllText(Path.Combine(output,"resize-trace.json"),System.Text.Json.JsonSerializer.Serialize(
            new ResizeTrace(frameSlots,48,delayMs,vsync,passes.ToArray(),"Committed native size messages; actual presentation/resource counters. Not input-to-photon, foreground or visual proof."),ResizeTraceJson.Default.ResizeTrace));
        failure?.Throw();
        check(platform.ValidationResizeCommits>0,"interactive WM_SIZE commits were observed");
        check(platform.ValidationUnpresentedResizeCommits==0,"committed resize repaint returns only after its extent was presented");
        check(screen.SentMessageProbes>0,"render thread can send a synchronous native message while WM_SIZE waits for presentation");
        if(systemTitle)check(platform.ValidationResizeSystemMessages>0,"native resize wait dispatches posted subsystem messages without consuming queued keyboard or mouse input");
        foreach(var pass in passes)Console.WriteLine($"Resize pass{pass.Pass}: {pass.Presentations} presentations / {pass.Milliseconds:0.0}ms; VSync on,48 size updates; VSync={vsync}.");
        check(screen.Clicks==cycles,"native resize cycles preserve root button input with "+frameSlots+" frame resource slots");
    }
    private sealed class ResizeScreen:UiScreen,IDisposable
    {
        private readonly WindowsPlatformBackend platform;
        private readonly UiFluentButton _action;
        private readonly UiScreen? _content;
        internal ResizeScreen(WindowsPlatformBackend platform,UiScreen? content)
        {this.platform=platform;_content=content;_action=Fluent.Button("Resize action",()=>Interlocked.Increment(ref Clicks));_action.Size=new(160,40);}
        public void Dispose()=>_action.Dispose();
        internal int Frames,Width,Height,Clicks,Held,ButtonX,ButtonY,SentMessageProbes,MouseDown,Releases;
        internal float MouseX,MouseY;
        internal long Presentations;
        internal int RetiredSwapchains,PendingTargets;
        internal ulong PendingTargetBytes,MsaaBytes;
        public override void Render(UiImmediateContext ui)
        {
            if(SentMessageProbes==0&&platform.ValidationResizePresentationWaitActive)
            {
                if(!PostMessageW(platform.WindowHandle,0x047B,0,0))throw new InvalidOperationException("Could not queue the resize subsystem probe.");
                if(SendMessageTimeoutW(platform.WindowHandle,0,0,0,2,1000,out _)==0)
                    throw new TimeoutException("UI thread did not dispatch a sent message during resize presentation.");
                Interlocked.Increment(ref SentMessageProbes);
            }
            if(_content is not null)
            {
                _content.Render(ui);
                ui.SetCursorScreenPos(new(0,0));
            }
            _action.Render(ui);
            if(ui.IsItemActive()&&ui.IsMouseDown(0))Volatile.Write(ref Held,1);
            var mouse=ui.GetMousePos();Volatile.Write(ref MouseX,mouse.X);Volatile.Write(ref MouseY,mouse.Y);
            Volatile.Write(ref MouseDown,ui.IsMouseDown(0)?1:0);
            if(ui.IsMouseReleased(0))Interlocked.Increment(ref Releases);
            var itemMin=ui.GetItemRectMin();var itemMax=ui.GetItemRectMax();
            var scale=platform.ContentScale;
            Volatile.Write(ref ButtonX,(int)MathF.Round((itemMin.X+itemMax.X)*0.5f*scale));
            Volatile.Write(ref ButtonY,(int)MathF.Round((itemMin.Y+itemMax.Y)*0.5f*scale));
            var viewport=ui.GetMainViewport();var size=new UiVector2(viewport.Size.X,viewport.Pos.Y+viewport.Size.Y);
            Volatile.Write(ref Width,(int)size.X);Volatile.Write(ref Height,(int)size.Y);
            var metrics=ui.GetRendererFrameMetrics();
            RetiredSwapchains=metrics.RetiredSwapchains;PendingTargets=metrics.PendingRenderTargets;PendingTargetBytes=metrics.PendingRenderTargetBytes;MsaaBytes=metrics.MsaaAllocatedBytes;
            Volatile.Write(ref Presentations,(long)metrics.PresentationSequence);
            Interlocked.Increment(ref Frames);
        }
    }
    [LibraryImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [LibraryImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool ShowWindow(nint window,int command);
    [LibraryImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool IsWindowVisible(nint window);
    [LibraryImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool PostMessageW(nint window,uint message,nuint wParam,nint lParam);
    [StructLayout(LayoutKind.Sequential)]private struct Rect {internal int Left,Top,Right,Bottom;}
    [LibraryImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool GetWindowRect(nint window,out Rect rect);
    [LibraryImport("user32.dll",EntryPoint="SendMessageTimeoutW",SetLastError=true)]
    private static partial nint SendMessageTimeoutW(nint window,uint message,nuint wParam,nint lParam,uint flags,uint timeout,out nuint result);
}

internal sealed record ResizePass(int Pass,double Milliseconds,long Presentations,long PrivateBytes)
{public int RetiredSwapchains{get;init;}public int PendingTargets{get;init;}public ulong PendingTargetBytes{get;init;}public ulong MsaaBytes{get;init;}
 public double SizingMilliseconds{get;init;}public double SizeMilliseconds{get;init;}public double PeakSizeMilliseconds{get;init;}
 public int Gc0{get;init;}public int Gc1{get;init;}public int Gc2{get;init;}public double GcPauseMilliseconds{get;init;}public long AllocatedBytes{get;init;}public double CpuMilliseconds{get;init;}public int SwapchainCreates{get;init;}}
internal sealed record ResizeTrace(int FrameSlots,int UpdatesPerPass,int UpdateIntervalMs,bool VSync,ResizePass[] Passes,string Scope);
[System.Text.Json.Serialization.JsonSerializable(typeof(ResizeTrace))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented=true)]
internal partial class ResizeTraceJson:System.Text.Json.Serialization.JsonSerializerContext;
