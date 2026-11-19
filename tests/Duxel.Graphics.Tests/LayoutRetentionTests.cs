using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class LayoutRetentionTests
{
    internal static void Measure(string output)
    {
        const int count=10000,warmups=96,samples=1000;
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var buttons=new MeasuredButton[count*2];
        for(var i=0;i<buttons.Length;i++)buttons[i]=new(){Size=new(1,1)};
        using var wrap=new UiWrap(buttons.Take(count).Cast<UiWidget>().ToArray());
        using var grid=new UiGrid(buttons.Skip(count).Cast<UiWidget>().ToArray(),new(100));
        var constraints=UiBoxConstraints.Loose(new(100,100));
        long bytes=0;var ticks=new long[samples];UiVector2 wrapSize=default,gridSize=default;
        void Frame(int sample)
        {
            context.NewFrame(new(1f/60,new(100,100),new(1,1)),idle,new(0,0,100,100),UiTextSettings.Default);
            context.Render(new Screen(ui=>
            {
                var allocated=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
                wrapSize=wrap.MeasureConstrained(ui,constraints);gridSize=grid.MeasureConstrained(ui,constraints);
                var elapsed=Stopwatch.GetTimestamp()-start;var delta=GC.GetAllocatedBytesForCurrentThread()-allocated;
                if(sample>=0){ticks[sample]=elapsed;bytes+=delta;}
            }));
            using var data=context.GetDrawData();
        }
        for(var i=0;i<warmups;i++)Frame(-1);
        var beforeCalls=buttons.Sum(static b=>(long)b.Measurements);var beforeGc=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
        using var process=Process.GetCurrentProcess();process.Refresh();var beforePrivate=process.PrivateMemorySize64;var beforeWs=process.WorkingSet64;
        for(var i=0;i<samples;i++)Frame(i);
        process.Refresh();var calls=buttons.Sum(static b=>(long)b.Measurements)-beforeCalls;
        if(wrapSize!=new UiVector2(100,100)||gridSize!=new UiVector2(100,100))throw new InvalidOperationException("The complete benchmark layout extent changed.");
        Array.Sort(ticks);
        double Us(long value)=>value*1_000_000d/Stopwatch.Frequency;
        var result=new LayoutRetentionResult(RuntimeInformation.FrameworkDescription,count,count,warmups,samples,bytes,calls,
            Us(ticks[samples/2]),Us(ticks[(int)(samples*.95)-1]),Us(ticks[(int)(samples*.99)-1]),
            beforePrivate,process.PrivateMemorySize64,beforeWs,process.WorkingSet64,
            GC.CollectionCount(0)-beforeGc[0],GC.CollectionCount(1)-beforeGc[1],GC.CollectionCount(2)-beforeGc[2]);
        File.WriteAllText(Path.Combine(output,"measurement.json"),JsonSerializer.Serialize(result,LayoutRetentionJson.Default.LayoutRetentionResult));
    }
    private sealed class MeasuredButton:UiButton<UiReleaseButtonBehavior,UiFluentButtonPresentation>
    {
        internal int Measurements;
        internal MeasuredButton():base(string.Empty) { }
        protected override UiVector2 MeasureContent(UiControlContext context){Measurements++;return base.MeasureContent(context);}
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}
internal sealed record LayoutRetentionResult(string Runtime,int WrapChildren,int GridChildren,int Warmups,int Samples,long AllocatedBytes,long ChildMeasurements,
    double MedianUs,double P95Us,double P99Us,long PrivateBefore,long PrivateAfter,long WorkingSetBefore,long WorkingSetAfter,int Gen0,int Gen1,int Gen2);
[JsonSerializable(typeof(LayoutRetentionResult))]
internal partial class LayoutRetentionJson:JsonSerializerContext;
