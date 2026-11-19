using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class RetainedTextMaintenanceTests
{
    internal static void VerifyProjectedTextAtlasReplacement(
        UiContext context, Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        ControlFontFixture.Configure(context);
        context.SetDirectTextBaseFontSize(16);
        var smallAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(scale: 1);
        var largeAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(scale: 3);
        var options = new UiLayerOptions(true, 1, default)
        {
            Scene3D = new(Matrix4x4.Identity, false),
            FontAtlasIndependent = true,
            ContentVersion = 1,
        };
        var builds = 0;
        void Paint(UiImmediateContext ui)
        {
            var draw = ui.GetWindowDrawList();
            draw.PushClipRect(new(14, 12, 144, 52));
            ui.SetCursorScreenPos(new(20, 17));
            ui.TextColored(new(0xFFFFFFFF), "ERD 한글");
            ui.SetCursorScreenPos(new(20, 41));
            ui.TextColored(new(0xFF80C5FF), "column_01");
            draw.PopClipRect();
        }
        void DrawRetained(UiImmediateContext ui)
        {
            if (ui.TryReplayLayer("projected-text-atlas", options))
                return;
            if (ui.BeginLayer("projected-text-atlas", options))
            {
                builds++;
                Paint(ui);
            }
            ui.EndLayer();
        }
        void DrawFresh(UiImmediateContext ui)
        {
            if (ui.BeginLayer("projected-text-fresh", options with { StaticCache = false, ContentVersion = null }))
                Paint(ui);
            ui.EndLayer();
        }

        context.SetFontAtlas(smallAtlas);
        var empty = capture("projected-atlas-empty", _ => { });
        var original = capture("projected-atlas-before", DrawRetained);
        check(!original.AsSpan().SequenceEqual(empty), "fixed-origin projected text has visible pixels");
        check(smallAtlas.LineHeight != largeAtlas.LineHeight, "projected text atlas fixture changes atlas line height");
        context.SetFontAtlas(largeAtlas);
        var retained = capture("projected-atlas-after", DrawRetained);
        check(builds == 1, "fixed-origin projected text reuses retained geometry after atlas replacement");
        var fresh = capture("projected-atlas-fresh", DrawFresh);
        check(retained.AsSpan().SequenceEqual(original) && retained.AsSpan().SequenceEqual(fresh),
            "projected text retains exact Vulkan pixels across atlas replacement and matches fresh paint");
    }

    internal static void VerifyAtlasReplacement(
        UiContext context, Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        context.SetDirectTextEnabled(false);
        context.SetDirectTextFallbackEnabled(false);
        var initialAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(scale: 1);
        var nextAtlas = UiFontAtlasBuilder.CreateBuiltInAscii5x7(scale: 3);
        var texture = new UiTextureId(1);
        var independent = new UiLayerOptions(true, 1, default) { FontAtlasIndependent = true, ContentVersion = 1 };
        var dependent = independent with { FontAtlasIndependent = false };
        var independentBuilds = 0;
        var parentBuilds = 0;
        var childBuilds = 0;

        void DrawLabel(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(14, 40));
            ui.Text("Atlas change");
        }

        void DrawRectangle(UiImmediateContext ui) =>
            ui.GetWindowDrawList().AddRectFilled(new(14, 12, 60, 16), new(0xFF468DE6), ui.WhiteTextureId);

        void DrawChild(UiImmediateContext ui)
        {
            if (ui.TryReplayLayer("atlas-dependent-child", dependent))
                return;
            if (ui.BeginLayer("atlas-dependent-child", dependent))
            {
                childBuilds++;
                DrawLabel(ui);
            }
            ui.EndLayer();
        }

        void DrawRetained(UiImmediateContext ui)
        {
            if (!ui.TryReplayLayer("atlas-independent-geometry", independent))
            {
                if (ui.BeginLayer("atlas-independent-geometry", independent))
                {
                    independentBuilds++;
                    DrawRectangle(ui);
                }
                ui.EndLayer();
            }
            if (ui.TryReplayLayer("atlas-independent-parent", independent))
                return;
            if (ui.BeginLayer("atlas-independent-parent", independent))
            {
                parentBuilds++;
                DrawChild(ui);
            }
            ui.EndLayer();
        }

        context.SetFontAtlas(initialAtlas);
        capture("atlas-prime-child", ui =>
        {
            ui.QueueTextureUpdate(initialAtlas.CreateTextureUpdate(texture));
            DrawChild(ui);
        });
        var before = capture("atlas-retained-before", DrawRetained);
        check(independentBuilds == 1 && parentBuilds == 1 && childBuilds == 1,
            "cached atlas child replays into an independent parent on the first capture");

        context.SetFontAtlas(nextAtlas);
        var after = capture("atlas-retained-after", ui =>
        {
            ui.QueueTextureUpdate(nextAtlas.CreateTextureUpdate(texture, UiTextureUpdateKind.Update));
            DrawRetained(ui);
        });
        check(independentBuilds == 1, "atlas replacement preserves explicitly independent geometry");
        check(parentBuilds == 2 && childBuilds == 2, "atlas replacement rebuilds text and its retaining parent");
        check(!before.AsSpan().SequenceEqual(after), "replacing the atlas changes rendered text pixels");
        var direct = capture("atlas-direct-after", ui => { DrawRectangle(ui); DrawLabel(ui); });
        check(after.AsSpan().SequenceEqual(direct), "reused geometry and rebuilt raster text match direct GPU output");
        check(after.AsSpan().SequenceEqual(capture("atlas-retained-steady", DrawRetained)),
            "retained geometry and text remain pixel stable after atlas replacement");
    }

    internal static void VerifyPixels(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        var labels=Enumerable.Range(0,600).Select(i=>"Pinned GPU text "+i).ToArray();
        long version=1;var builds=0;
        void Content(UiImmediateContext ui)
        {
            for(var i=0;i<labels.Length;i++)
            {ui.SetCursorScreenPos(new(20,i==0?20:60));ui.Text(labels[i]);}
        }
        void Retained(UiImmediateContext ui)
        {
            if(ui.BeginLayer("maintenance-parent",new(true,1,default){ContentVersion=version}))
            {
                if(ui.BeginLayer("maintenance-child",new(true,1,default){ContentVersion=version}))
                {builds++;Content(ui);}
                ui.EndLayer();
            }
            ui.EndLayer();
        }
        var reference=capture("cache-direct-reference",Content);
        var retained=capture("cache-retained",Retained);
        check(reference.AsSpan().SequenceEqual(retained),"600 nested retained text textures match direct repaint beyond the512-entry bound");
        for(var i=0;i<4;i++)
            check(retained.AsSpan().SequenceEqual(capture("cache-idle-"+i,Retained)),"unchanged nested text replay preserves every pixel across frame slots, phase "+i);
        check(builds==1,"unchanged nested GPU text content builds once");
        capture("cache-pressure",ui=>{Retained(ui);ui.SetCursorScreenPos(new(20,100));ui.Text("Unretained GPU pressure");});
        check(retained.AsSpan().SequenceEqual(capture("cache-after-pressure",Retained)),"evicting a newly added unretained texture preserves all pinned GPU pixels");
        labels[0]="Changed visible text";version++;
        var changed=capture("cache-changed",Retained);
        check(!retained.AsSpan().SequenceEqual(changed),"retained text mutation changes actual visible GPU pixels");
        var directChanged=capture("cache-changed-reference",Content);
        check(changed.AsSpan().SequenceEqual(directChanged),"dependency replacement matches direct repaint while nested parent retains child textures");
        capture("cache-remove-parent",ui=>{ui.RemoveLayer("maintenance-parent");});
        check(changed.AsSpan().SequenceEqual(capture("cache-child-survives",ui=>
        {if(ui.BeginLayer("maintenance-child",new(true,1,default){ContentVersion=version}))Content(ui);ui.EndLayer();})),
            "removing one owner preserves child-owned text textures");
        capture("cache-remove-child",ui=>ui.RemoveLayer("maintenance-child"));
        check(changed.AsSpan().SequenceEqual(capture("cache-after-release",Content)),"released text textures recreate without pixel loss");
    }

    internal static void Measure(string output)
    {
        const int count=2048,samples=1000;
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);context.SetDirectTextBaseFontSize(14);
        var labels=Enumerable.Range(0,count).Select(i=>"Retained text "+i).ToArray();
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        UiImmediateContext? current=null;var builds=0;var visible=true;long version=1;string? transient=null;
        var screen=new Screen(ui=>
        {
            current=ui;
            if(!visible){ui.RemoveLayer("text-maintenance");return;}
            if(ui.BeginLayer("text-maintenance",new(true,1,default){ContentVersion=version}))
            {
                builds++;
                foreach(var label in labels){ui.SetCursorScreenPos(new(20,20));ui.Text(label);}
            }
            ui.EndLayer();
            if(transient is not null){ui.SetCursorScreenPos(new(20,20));ui.Text(transient);}
        });
        void Begin()=>context.NewFrame(new(1f/60,new(320,240),new(1,1)),idle,new(0,0,320,240),UiTextSettings.Default);
        void End(){context.Render(screen);using var data=context.GetDrawData();}
        for(var i=0;i<96;i++){Begin();End();}
        var occupancy=current!.DirectTextCacheOccupancy;
        if(occupancy.Entries!=count||occupancy.Retained!=count||occupancy.Limit>=count||builds!=1)
            throw new InvalidOperationException($"Expected one retained raster-text build above the ordinary cache limit: {occupancy};builds={builds}");
        var ticks=new long[samples];var allocations=new long[samples];
        var gcBefore=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
        using var process=Process.GetCurrentProcess();process.Refresh();var privateBefore=process.PrivateMemorySize64;
        for(var i=0;i<samples;i++)
        {
            Begin();
            var bytes=GC.GetAllocatedBytesForCurrentThread();var start=Stopwatch.GetTimestamp();
            context.Render(screen);ticks[i]=Stopwatch.GetTimestamp()-start;allocations[i]=GC.GetAllocatedBytesForCurrentThread()-bytes;
            using var data=context.GetDrawData();
        }
        process.Refresh();var privateAfter=process.PrivateMemorySize64;
        var after=current.DirectTextCacheOccupancy;
        if(after.Entries!=count||after.Retained!=count||builds!=1)throw new InvalidOperationException("Replay lost a text dependency or rebuilt unchanged content.");
        var gcAfter=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
        var measuredBuilds=builds;
        transient="Unretained transient";Begin();End();transient=null;Begin();End();
        Begin();
        End();
        if(current.DirectTextCacheOccupancy.Entries!=count)throw new InvalidOperationException("A newly added unretained key escaped the cache bound.");
        labels[0]="Changed retained text";version++;Begin();End();Begin();End();
        if(builds!=2||current.DirectTextCacheOccupancy.Entries!=count||current.DirectTextCacheOccupancy.Retained!=count)
            throw new InvalidOperationException("Replacing a layer dependency did not release the previous text key.");
        visible=false;Begin();End();Begin();End();
        var released=current.DirectTextCacheOccupancy;
        if(released.Retained!=0||released.Entries>released.Limit)throw new InvalidOperationException("Removing the layer did not restore the ordinary cache bound.");
        for(var i=0;i<602;i++){Begin();End();}
        if(current.DirectTextCacheOccupancy.Entries!=0)throw new InvalidOperationException("Unretained idle text did not expire.");

        File.WriteAllLines(Path.Combine(output,"samples.csv"),new[]{"renderTicks,allocatedBytes"}.Concat(ticks.Select((t,i)=>$"{t},{allocations[i]}")));
        var result=new RetainedTextMaintenanceResult(RuntimeInformation.FrameworkDescription,count,samples,96,Stopwatch.Frequency,
            allocations.Sum(),allocations.Min(),allocations.Max(),ticks,measuredBuilds,gcBefore,gcAfter,privateBefore,privateAfter,
            "Actual Windows text backend;fixed retained scene;CPU Render phase including cache maintenance;no GPU presentation or app-FPS claim;no forcedGC;new unretained key,dependency replacement,release and idle expiry verified.");
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,RetainedTextMaintenanceJson.Default.RetainedTextMaintenanceResult));
        Console.WriteLine($"Retained={count};frames={samples};CPU render allocation={result.AllocatedBytes}B;range={result.MinAllocation}..{result.MaxAllocation};measured builds={measuredBuilds}");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}
internal sealed record RetainedTextMaintenanceResult(string Framework,int TextEntries,int Samples,int WarmFrames,long Frequency,
    long AllocatedBytes,long MinAllocation,long MaxAllocation,long[] RenderTicks,int ContentBuilds,int[] GcBefore,int[] GcAfter,
    long PrivateBefore,long PrivateAfter,string Scope);
[JsonSerializable(typeof(RetainedTextMaintenanceResult))]
[JsonSourceGenerationOptions(WriteIndented=true)]
internal partial class RetainedTextMaintenanceJson:JsonSerializerContext;
