using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.InteropServices;

internal static partial class AccessibilityTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var tree=new UiAccessibilityTree();
        static UiAccessibilityNode Node(string id,string parent="")=>new(id,parent,UiAccessibilityRole.Button,id,new(0,0,100,32),true,false,false);
        tree.BeginFrame();tree.Add(Node("parent"));tree.Add(Node("child","parent"));tree.EndFrame();
        check(tree.Count==2&&tree.TryGet("parent",out var parent)&&parent.FirstChild=="child"&&parent.LastChild=="child","UIA publishes real parent and child navigation");
        tree.TryGet("child",out var child);var revision=tree.Revision;
        tree.BeginFrame();tree.Add(Node("parent"));tree.Add(Node("child","parent"));tree.EndFrame();
        check(tree.Revision==revision,"unchanged UIA frames preserve publication revision");
        tree.BeginFrame();tree.Add(Node("temporary"));tree.AbortFrame();
        check(tree.Count==2&&!tree.TryGet("temporary",out _)&&tree.TryGet("child",out _),"failed render preserves last published UIA state");
        tree.BeginFrame();tree.EndFrame();
        check(tree.Count==0&&tree.RequestAction("child",child.RuntimeId,UiAccessibilityAction.Invoke,wait:false)==UiAccessibilityResult.Unavailable,"removed UIA elements reject stale actions");
        tree.BeginFrame();tree.Add(Node("child"));tree.EndFrame();tree.TryGet("child",out var replacement);
        check(replacement.RuntimeId!=child.RuntimeId,"recreated IDs receive new UIA runtime identity");
        check(tree.RequestAction("child",replacement.RuntimeId,UiAccessibilityAction.Invoke)==UiAccessibilityResult.Busy,"synchronous actions cannot deadlock the render writer");
        check(tree.RequestAction("child",replacement.RuntimeId,UiAccessibilityAction.Invoke,wait:false)==UiAccessibilityResult.Success,"UIA Invoke queues without executing on provider threads");
        tree.BeginFrame();tree.Add(Node("child"));var invoke=tree.Take("child",UiAccessibilityAction.Invoke);
        check(invoke is not null&&tree.Take("child",UiAccessibilityAction.Invoke) is null,"one queued Invoke is consumed exactly once");
        invoke!.Result=UiAccessibilityResult.Success;tree.EndFrame();
        check(invoke.Completion.Task.Result==UiAccessibilityResult.Success,"action completion follows frame publication");
        tree.BeginFrame();tree.Add(Node("edit") with{Role=UiAccessibilityRole.Edit,Value="old",MaximumLength=16});tree.EndFrame();tree.TryGet("edit",out var edit);
        check(tree.RequestAction("edit",edit.RuntimeId,UiAccessibilityAction.SetValue,"bad\0",false)==UiAccessibilityResult.InvalidArgument,"single-line UIA values reject control characters explicitly");
        tree.RequestAction("edit",edit.RuntimeId,UiAccessibilityAction.SetValue,"new",false);
        tree.RequestAction("edit",edit.RuntimeId,UiAccessibilityAction.Focus,wait:false);
        tree.BeginFrame();tree.Add(edit.Node);
        check(tree.Take("edit",UiAccessibilityAction.Focus) is null,"per-element UIA requests preserve arrival order");
        var set=tree.Take("edit",UiAccessibilityAction.SetValue);set!.Result=UiAccessibilityResult.Success;
        var focus=tree.Take("edit",UiAccessibilityAction.Focus);focus!.Result=UiAccessibilityResult.Success;tree.EndFrame();
        tree.BeginFrame();tree.Add(edit.Node with{ReadOnly=true});tree.EndFrame();
        check(tree.RequestAction("edit",edit.RuntimeId,UiAccessibilityAction.SetValue,"x",false)==UiAccessibilityResult.ReadOnly,"UIA read-only input refuses SetValue");
        tree.BeginFrame();tree.Add(edit.Node with{Enabled=false});tree.EndFrame();
        check(tree.RequestAction("edit",edit.RuntimeId,UiAccessibilityAction.SetValue,"x",false)==UiAccessibilityResult.Disabled,"disabled UIA input refuses SetValue");
        tree.BeginFrame();tree.Add(edit.Node with{Offscreen=true});tree.EndFrame();
        check(tree.RequestAction("edit",edit.RuntimeId,UiAccessibilityAction.Focus,wait:false)==UiAccessibilityResult.Unavailable,"clipped UIA input refuses focus");
        var nodes=Enumerable.Range(0,10000).Select(i=>Node("many-"+i)).ToArray();
        void Many(){tree.BeginFrame();foreach(var n in nodes)tree.Add(n);tree.EndFrame();}
        for(var i=0;i<16;i++)Many();
        var before=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<8;i++)Many();var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        check(allocated==0,"10,000 unchanged UIA nodes allocate zero bytes after warmup");
        tree.BeginFrame();tree.Add(Node("after-burst"));tree.EndFrame();
        check(tree.Count==1,"UIA burst removal releases all removed nodes");
        using(var queuedTree=new UiAccessibilityTree())
        using(var queued=new ManualResetEventSlim())
        {
            queuedTree.BeginFrame();queuedTree.Add(Node("waiting"));queuedTree.EndFrame();queuedTree.TryGet("waiting",out var waiting);
            queuedTree.SetRequestFrameCallback(queued.Set);
            var action=Task.Run(()=>queuedTree.RequestAction("waiting",waiting.RuntimeId,UiAccessibilityAction.Invoke));
            check(queued.Wait(TimeSpan.FromSeconds(2)),"provider thread wakes the render producer for queued actions");
            queuedTree.BeginFrame();queuedTree.Add(Node("waiting"));queuedTree.EndFrame();queuedTree.Dispose();
            check(action.GetAwaiter().GetResult()==UiAccessibilityResult.Unavailable,"disposal completes unconsumed actions without waiting for timeout");
        }
        tree.Dispose();check(!tree.TryGet("after-burst",out _)&&tree.IsDisposed,"retained UIA readers remain safe after tree disposal");
        Integration(check);
        ProviderAbi(check);
        ConcurrentReaders(check);
        ProviderStorage(check);
        EventSnapshotLifetime(check);
    }
    private static void EventSnapshotLifetime(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel event snapshot lifetime fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var host = new WindowsAccessibility(platform.WindowHandle);
        var tree = host.Tree;
        var events = new List<UiAccessibilityEvent>();
        tree.AutomationEvent += events.Add;
        tree.BeginFrame();
        tree.Add(new("target", "", UiAccessibilityRole.Button, "Target", new(10, 20, 80, 32), true, false, false));
        tree.Add(new("tooltip", "target", UiAccessibilityRole.Tooltip, "Last help", new(10, 60, 120, 40),
            true, false, false, Focusable : false));
        tree.EndFrame();
        tree.TryGet("tooltip", out var published);
        var live = host.Find("tooltip")!;
        var snapshot = host.CaptureEventSource(published, closed : true);
        var rootSnapshot = new UiaEventRootProvider(host);
        snapshot.GetBounds(out var before);
        host.Dispose();
        platform.Dispose();
        check(events.Count == 2
            && events[0] is { Kind : UiAccessibilityEventKind.TooltipOpened, DuringDisposal : false }
            && events[1] is { Kind : UiAccessibilityEventKind.TooltipClosed, DuringDisposal : true },
            "managed Tooltip events identify opening during publication and closing during disposal");
        check(live.GetPropertyValue(30005, out _) == UiaNative.Unavailable,
            "window teardown invalidates live providers without invalidating event snapshots");
        check(snapshot.GetProviderOptions(out var options) == 0 && options == 34,
            "retained event snapshots remain COM-callable after tree and HWND disposal");
        snapshot.GetPropertyValue(30005, out var name);
        try
        {
            check(name.Type == 8 && Marshal.PtrToStringBSTR(name.Data.Pointer) == "Last help",
                "post-shutdown event cache reads the original tooltip name");
        }
        finally
        {
            Marshal.ThrowExceptionForHR(UiaNative.VariantClear(ref name));
        }
        snapshot.GetPropertyValue(30022, out var offscreen);
        check(offscreen.Type == 11 && offscreen.Data.Boolean != 0,
            "post-shutdown event cache reads the closed offscreen state");
        check(snapshot.GetBounds(out var after) == 0 && before.Equals(after),
            "post-shutdown event bounds use the captured screen rectangle without HWND access");
        check(snapshot.GetPatternProvider(10000, out var pattern) == 0 && pattern == 0
            && snapshot.SetFocus() == UiaNative.Unavailable,
            "event snapshots cannot invoke or focus disposed controls");
        check(snapshot.Navigate(0, out var parent) == 0 && parent != 0,
            "post-shutdown event ancestry remains available to UIA cache construction");
        Marshal.Release(parent);
        check(rootSnapshot.GetHostProvider(out var nativeHost) == 0 && nativeHost != 0,
            "post-shutdown event root retains its native host interface without querying the destroyed HWND");
        Marshal.Release(nativeHost);
        check(snapshot.GetFragmentRoot(out var root) == 0 && root != 0,
            "post-shutdown event ancestry returns its retained fragment root");
        Marshal.Release(root);
    }
    private static void ProviderStorage(Action<bool,string> check)
    {
        using var host=new WindowsAccessibility(0);
        const int count=4096;var nodes=Enumerable.Range(0,count).Select(i=>new UiAccessibilityNode("provider-"+i,"",UiAccessibilityRole.Button,"Provider",new(0,0,80,32),true,false,false)).ToArray();
        host.Tree.BeginFrame();foreach(var node in nodes)host.Tree.Add(node);host.Tree.EndFrame();
        var providers=nodes.Select(node=>host.Find(node.Id)!).ToArray();
        check(host.Storage.Cached==count&&ReferenceEquals(providers[0],host.Find(nodes[0].Id)),"provider identity cache reuses actual provider instances for live elements");
        host.Tree.BeginFrame();host.Tree.Add(nodes[0]);host.Tree.EndFrame();
        check(host.Storage.Cached==1&&host.Storage.Capacity<=256&&host.Storage.Retired==0,"removal eagerly prunes 4095 provider IDs and shrinks burst capacity without a future client query");
        check(providers[^1].GetPropertyValue(30005,out _)==UiaNative.Unavailable,"retained removed provider is unavailable while the surviving provider stays live");
        check(providers[0].GetPropertyValue(30003,out var type)==0&&type.Type==3&&type.Data.Integer==50000,"provider pruning preserves the live control contract");
    }
    private static void ConcurrentReaders(Action<bool,string> check)
    {
        using var tree=new UiAccessibilityTree();
        var ids=Enumerable.Range(0,64).Select(i=>"concurrent-"+i).ToArray();
        void Frame(int version)
        {
            var text=version.ToString(System.Globalization.CultureInfo.InvariantCulture);tree.BeginFrame();
            foreach(var id in ids)tree.Add(new(id,"",UiAccessibilityRole.Edit,id,new(version,0,64,32),true,false,false,text,MaximumLength:32));
            tree.EndFrame();
        }
        Frame(0);
        using var ready=new CountdownEvent(4);using var start=new ManualResetEventSlim();
        var stop=0;long reads=0;
        var readers=Enumerable.Range(0,4).Select(worker=>Task.Factory.StartNew(()=>
        {
            ready.Signal();start.Wait();long count=0;
            while(Volatile.Read(ref stop)==0)
            {
                if(tree.TryGet(ids[worker],out var entry))
                {
                    if(entry.Node.Id!=ids[worker]||entry.Node.Name!=ids[worker]||entry.Node.Value!=entry.Node.Bounds.X.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        throw new InvalidOperationException("Concurrent UIA reader observed a partial entry.");
                    count++;
                }
                _=tree.Root;_=tree.Count;
            }
            Interlocked.Add(ref reads,count);
        },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default)).ToArray();
        try
        {
            if(!ready.Wait(TimeSpan.FromSeconds(5)))throw new InvalidOperationException("UIA readers failed to start.");
            start.Set();for(var frame=1;frame<=1000;frame++)Frame(frame);
            tree.Dispose();
        }
        finally{Volatile.Write(ref stop,1);start.Set();}
        check(Task.WaitAll(readers,TimeSpan.FromSeconds(5))&&reads>0,"four concurrent UIA readers observe complete entries across 1000 publications and disposal");
        check(tree.Count==0&&tree.Root==(null,null,null),"concurrent readers remain usable after producer shutdown and disposal");
    }
    private static void Integration(Action<bool,string> check)
    {
        using var tree=new UiAccessibilityTree();
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);context.SetAccessibilityTree(tree);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var text="old";var writes=0;var clicks=0;
        using var input=Fluent.Input("Hidden##editor",new(()=>text,v=>{text=v;writes++;}),32);
        using var clear=Fluent.Button("##clear",()=>{text="";clicks++;});clear.AccessibleName="Clear";
        input.AfterSlot=new("clear",clear,new(24,24));
        using var field=Fluent.Field(input);field.Width=260;field.Options=new(){Label="Account",Hint="Enter account",Required=true,ValidationMessage="Missing"};
        void Step(){context.NewFrame(new(1f/60,new(640,480),new(1,1)),idle,new(0,0,640,480),UiTextSettings.Default);context.Render(new Screen(field));using var draw=context.GetDrawData();}
        Step();var first=tree.Root.First!;tree.TryGet(first,out var edit);tree.TryGet(edit.FirstChild!,out var button);
        check(edit.Node.Role==UiAccessibilityRole.Edit&&edit.Node.Name=="Account"&&edit.Node.Required&&edit.Node.Invalid&&edit.Node.Hint=="Enter account","actual Field publishes name,required,validation and hint on primary input");
        check(button.Node.Name=="Clear"&&button.Node.ParentId==first&&!button.Node.Required&&button.Node.Hint is null,"actual adornment button has independent semantics and real parent");
        tree.RequestAction(first,edit.RuntimeId,UiAccessibilityAction.SetValue,"updated",false);Step();
        check(text=="updated"&&writes==1&&tree.TryGet(first,out var updated)&&updated.Node.Value==text,"UIA SetValue uses actual input binding exactly once");
        tree.RequestAction(button.Node.Id,button.RuntimeId,UiAccessibilityAction.Invoke,wait:false);Step();Step();
        check(text==""&&clicks==1&&tree.TryGet(first,out updated)&&updated.Node.Value=="","UIA Invoke uses actual generated button callback and refreshes input state");
        tree.RequestAction(first,edit.RuntimeId,UiAccessibilityAction.Focus,wait:false);Step();
        check(context.State.FocusedId==first&&tree.Root.Focus==first,"UIA focus uses the common control focus path");
        using var custom=new UiButton<NoNavigation,UiFluentButtonPresentation>("Custom");
        context.NewFrame(new(1f/60,new(640,480),new(1,1)),idle,new(0,0,640,480),UiTextSettings.Default);context.Render(new Screen(custom));using(var data=context.GetDrawData()){}
        tree.TryGet(tree.Root.First!,out var customNode);
        check(!customNode.Node.Focusable&&tree.RequestAction(customNode.Node.Id,customNode.RuntimeId,UiAccessibilityAction.Focus,wait:false)==UiAccessibilityResult.InvalidArgument,"custom behavior exposes only its declared focus capability");
        using var unowned=Fluent.Button("##unowned");
        void Unowned(){context.NewFrame(new(1f/60,new(640,480),new(1,1)),idle,new(0,0,640,480),UiTextSettings.Default);context.Render(new Screen(unowned));using var draw=context.GetDrawData();}
        Unowned();var original=tree.Root.First;unowned.AccessibleName="Unowned icon";Unowned();tree.TryGet(tree.Root.First!,out var unownedNode);
        check(unownedNode.Node.Name=="Unowned icon"&&tree.Root.First==original,"explicit name works on an unowned Parent factory without changing input identity");
        context.SetAccessibilityTree(null);Step();check(tree.Count==1,"detached UIA tree is no longer modified by rendering");
    }
    private sealed class Screen(UiWidget widget):UiScreen
    {public override void Render(UiImmediateContext ui)=>widget.Render(ui);}
    private readonly struct NoNavigation:IUiButtonBehavior
    {public static UiButtonInteraction Process(UiControlContext context,string id,UiRect bounds)=>default;}
    private static unsafe void ProviderAbi(Action<bool,string> check)
    {
        using var host=new WindowsAccessibility(0);
        host.Tree.BeginFrame();host.Tree.Add(new("edit","",UiAccessibilityRole.Edit,"Account",new(0,0,80,32),true,false,false,"hello",MaximumLength:32));host.Tree.EndFrame();
        var provider=host.Find("edit")!;var simple=UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            var table=*(nint**)simple;
            UiaVariant name=default;var hr=((delegate* unmanaged[Stdcall]<nint,int,UiaVariant*,int>)table[5])(simple,30005,&name);
            try{check(hr==0&&name.Type==8&&Marshal.PtrToStringBSTR(name.Data.Pointer)=="Account","generated COM simple-provider vtable returns owned BSTR property");}
            finally{UiaNative.VariantClear(ref name);}
            var fragmentId=new Guid("f7063da8-8359-439c-9297-bbc5299a7d87");
            hr=Marshal.QueryInterface(simple,in fragmentId,out var fragment);
            check(hr==0&&fragment!=0,"generated COM class exposes inherited fragment interface");
            try
            {
                var fragmentTable=*(nint**)fragment;nint runtime=0;
                hr=((delegate* unmanaged[Stdcall]<nint,nint*,int>)fragmentTable[4])(fragment,&runtime);
                check(hr==0&&runtime!=0,"generated COM fragment returns native SAFEARRAY runtime ID");
                // The client owns the SAFEARRAY returned by GetRuntimeId.
                Marshal.ThrowExceptionForHR(DestroyArray(runtime));
            }
            finally{Marshal.Release(fragment);}
            nint pattern=0;hr=((delegate* unmanaged[Stdcall]<nint,int,nint*,int>)table[4])(simple,10002,&pattern);
            check(hr==0&&pattern!=0,"generated COM ValuePattern is available for real input");
            try
            {
                var valueTable=*(nint**)pattern;nint value=0;
                hr=((delegate* unmanaged[Stdcall]<nint,nint*,int>)valueTable[4])(pattern,&value);
                try{check(hr==0&&Marshal.PtrToStringBSTR(value)=="hello","generated COM ValuePattern vtable returns input text");}finally{Marshal.FreeBSTR(value);}
                host.Dispose();
                hr=((delegate* unmanaged[Stdcall]<nint,int,UiaVariant*,int>)table[5])(simple,30005,&name);
                check(hr==UiaNative.Unavailable,"retained COM provider reports element unavailable after disposal");
            }
            finally{Marshal.Release(pattern);}
        }
        finally{Marshal.Release(simple);}
        check(Marshal.SizeOf<UiaVariant>()==(IntPtr.Size==8?24:16),"UIA VARIANT layout matches native pointer architecture");
    }
    [LibraryImport("oleaut32.dll",EntryPoint="SafeArrayDestroy")]
    private static partial int DestroyArray(nint value);
}
