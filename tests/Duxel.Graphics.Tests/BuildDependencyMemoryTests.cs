using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class BuildDependencyMemoryTests
{
    internal static void Measure(string output)
    {
        const int count=10000;
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(default,false,false,false,false,false,false,0,0,[],[],default,default);
        long Frame(UiWidget[] views)
        {
            long allocated=0;
            context.NewFrame(new(1f/60,new(100,100),new(1,1)),idle,new(0,0,100,100),UiTextSettings.Default);
            context.Render(new Screen(ui=>
            {
                var start=GC.GetAllocatedBytesForCurrentThread();
                foreach(var view in views)view.Render(ui);
                allocated=GC.GetAllocatedBytesForCurrentThread()-start;
            }));
            using var data=context.GetDrawData();return allocated;
        }
        for(var i=0;i<96;i++)
        {
            var state=new UiState<int>(0);using var watched=new Watched(state);using var owned=new Owned();
            Watched[] watchWarm=[watched];Owned[] ownerWarm=[owned];
            Frame(watchWarm);Frame(watchWarm);state.Value=1;Frame(watchWarm);
            Frame(ownerWarm);Frame(ownerWarm);
        }
        var states=Enumerable.Range(0,count).Select(_=>new UiState<int>(0)).ToArray();
        var watchedViews=states.Select(state=>new Watched(state)).ToArray();
        var ownedViews=Enumerable.Range(0,count).Select(_=>new Owned()).ToArray();
        var watchFirst=Frame(watchedViews);var watchSteady=Frame(watchedViews);
        foreach(var state in states)state.Value=1;
        var watchRebuild=Frame(watchedViews);var ownedFirst=Frame(ownedViews);var ownedSteady=Frame(ownedViews);
        var watchBuilds=watchedViews.Sum(static w=>w.Builds);var ownedBuilds=ownedViews.Sum(static w=>w.Builds);
        foreach(var view in watchedViews)view.Dispose();foreach(var view in ownedViews)view.Dispose();
        var result=new BuildDependencyMemoryResult(RuntimeInformation.FrameworkDescription,count,96,watchFirst,watchSteady,watchRebuild,
            ownedFirst,ownedSteady,watchBuilds,ownedBuilds,
            "Current-thread first/steady/rebuilt Build allocation for distinct watched states and keyed child ownership;widget/state/array setup and disposal excluded;no forced GC;not Private/FPS.");
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,BuildDependencyMemoryJson.Default.BuildDependencyMemoryResult));
        if(watchBuilds!=count*2||ownedBuilds!=count||watchSteady!=0||watchRebuild!=0||ownedSteady!=0)
            throw new InvalidOperationException($"Dependency/ownership Build contract:watchBuilds={watchBuilds};ownedBuilds={ownedBuilds};watchSteady={watchSteady};watchRebuild={watchRebuild};ownedSteady={ownedSteady}.");
    }
    private sealed class Watched(UiState<int> state):UiWidget(UiWidgetIdentity.Parent)
    {
        internal int Builds;
        protected override IUiView Build(UiBuildContext context){Builds++;context.Watch(state);return Empty.Instance;}
    }
    private sealed class Owned:UiWidget
    {
        internal int Builds;
        internal Owned():base(UiWidgetIdentity.Parent) { }
        protected override IUiView Build(UiBuildContext context){Builds++;return context.Child("body",static()=>new Child());}
    }
    private sealed class Child:UiControlWidget
    {
        internal Child():base(UiWidgetIdentity.Parent) { }
        protected override void RenderControl(UiImmediateContext ui)=>ui.Dummy(new(1,1));
    }
    private sealed class Empty:IUiView
    {internal static readonly Empty Instance=new();public void Render(UiImmediateContext ui) { }}
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}
internal sealed record BuildDependencyMemoryResult(string Runtime,int Widgets,int Warmups,long WatchFirstBytes,long WatchSteadyBytes,
    long WatchRebuildBytes,long OwnedFirstBytes,long OwnedSteadyBytes,int WatchBuilds,int OwnedBuilds,string Scope);
[JsonSerializable(typeof(BuildDependencyMemoryResult))]
internal partial class BuildDependencyMemoryJson:JsonSerializerContext;
