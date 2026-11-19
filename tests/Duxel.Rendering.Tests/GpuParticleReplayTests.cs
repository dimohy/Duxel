using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Core.Simulation;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static class GpuParticleReplayTests
{
    internal static void Run(string output,string inputDirectory,bool benchmark=false)
    {
        output=Path.GetFullPath(output);inputDirectory=Path.GetFullPath(inputDirectory);
        if(Directory.Exists(output))throw new InvalidOperationException("Preserve replay results; select a new output directory.");
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(inputDirectory,"case.json")));
        var metadata=document.RootElement;var input=File.ReadAllBytes(Path.Combine(inputDirectory,"input.bin"));
        var stride=Marshal.SizeOf<UiGpuParticle>();var count=metadata.GetProperty("count").GetInt32();
        if(metadata.GetProperty("particleStride").GetInt32()!=stride||input.Length!=checked(count*stride))throw new InvalidDataException("Particle replay layout/count mismatch.");
        if(metadata.GetProperty("tolerance").GetSingle()!=.005f)throw new InvalidDataException("Replay must retain the original0.005 tolerances.");
        var bounds=metadata.GetProperty("Bounds");
        using var system=new UiGpuParticleSystem(MemoryMarshal.Cast<byte,UiGpuParticle>(input).ToArray());
        var frame=new UiGpuParticleFrame(system,new(bounds.GetProperty("X").GetSingle(),bounds.GetProperty("Y").GetSingle(),bounds.GetProperty("Width").GetSingle(),bounds.GetProperty("Height").GetSingle()),
            metadata.GetProperty("DeltaSeconds").GetSingle(),metadata.GetProperty("Radius").GetSingle(),metadata.GetProperty("Speed").GetSingle(),
            metadata.GetProperty("Sides").GetInt32(),metadata.GetProperty("MaximumAngularSpeed").GetSingle(),metadata.GetProperty("CollisionIterations").GetInt32());
        frame.Validate();
        var cpu=benchmark?null:new ParticleJacobiSimulation(system);cpu?.Step(frame);
        Directory.CreateDirectory(output);
        using var platform=new WindowsPlatformBackend(new(){Width=320,Height=240,Title="Duxel owned particle snapshot replay",VSync=false,
            KeyRepeatSettingsProvider=new WindowsKeyRepeatSettingsProvider(),TrayOptions=new()});
        using var renderer=new VulkanRendererBackend(platform,new(2,EnableVSync:false,MsaaSamples:benchmark?4:1));renderer.CreateDeviceObjects();
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Create,new(2),UiTextureFormat.Rgba8Unorm,1,1,new byte[]{255,255,255,255}));
        var size=platform.FramebufferSize;
        var screen=new Screen(frame);
        if(benchmark)
        {
            void Render()
            {
                platform.PollEvents();
                context.NewFrame(new(1f/60,new(size.Width,size.Height),new(1,1)),new(default,false,false,false,false,false,false,0,0,[],[],default,default),new(0,0,size.Width,size.Height),UiTextSettings.Default);
                context.Render(screen);using var drawData=context.GetDrawData();
                if(!renderer.TryRenderDrawData(drawData))throw new InvalidOperationException("The fixed-size particle benchmark did not submit a frame.");
            }
            const int warmupFrames=4096;
            const int measuredFrames=8192;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            for(var index=0;index<warmupFrames;index++)Render();
            renderer.ValidationWaitForGraphicsFrames();
            var warmupSeconds=clock.Elapsed.TotalSeconds;
            var recorder=new BenchFrameRecorder(65536);clock.Restart();var previous=clock.ElapsedTicks;
            for(var index=0;index<measuredFrames;index++)
            {
                Render();var now=clock.ElapsedTicks;recorder.Record((double)(now-previous)/System.Diagnostics.Stopwatch.Frequency);previous=now;
            }
            var drain=System.Diagnostics.Stopwatch.StartNew();renderer.ValidationWaitForGraphicsFrames();drain.Stop();
            using(var writer=new StreamWriter(Path.Combine(output,"frames.csv")))
            {writer.WriteLine("frameSeconds");foreach(var value in recorder.FrameSeconds)writer.WriteLine(value.ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
            var report=new ParticleBenchmarkReport(RuntimeInformation.FrameworkDescription,Convert.ToHexString(SHA256.HashData(input)),
                VulkanRendererBackend.ValidationParticleShaderSha256,count,frame.CollisionIterations,4,size.Width,size.Height,platform.ContentScale,warmupFrames,warmupSeconds,drain.Elapsed.TotalMilliseconds,recorder.Calculate(),
                "Fixed initial state,unchanged5000 bodies/solver settings; MSAA4,VSync off,hidden owned surface. GPU simulation+render submission only; not foreground app FPS or correctness proof.");
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(report,ParticleReplayJson.Default.ParticleBenchmarkReport));
            Console.WriteLine($"Particle benchmark: frames={report.Stats.Samples},median={report.Stats.MedianFrameMs:R}ms,p95={report.Stats.P95FrameMs:R}ms");
            return;
        }
        context.NewFrame(new(1f/60,new(size.Width,size.Height),new(1,1)),new(default,false,false,false,false,false,false,0,0,[],[],default,default),new(0,0,size.Width,size.Height),UiTextSettings.Default);
        context.Render(screen);using var data=context.GetDrawData();renderer.RenderAndReadback(data);
        var gpu=renderer.ValidationReadParticleState(system);
        File.WriteAllBytes(Path.Combine(output,"cpu.bin"),MemoryMarshal.AsBytes(cpu!.Particles).ToArray());
        File.WriteAllBytes(Path.Combine(output,"gpu.bin"),MemoryMarshal.AsBytes(gpu.AsSpan()).ToArray());
        float position=0,direction=0,angular=0;int? first=null;var finite=true;
        for(var i=0;i<count;i++)
        {
            var expected=cpu.Particles[i];var actual=gpu[i];
            var p=MathF.Max(MathF.Abs(expected.Position.X-actual.Position.X),MathF.Abs(expected.Position.Y-actual.Position.Y));
            var d=MathF.Max(MathF.Abs(expected.Direction.X-actual.Direction.X),MathF.Abs(expected.Direction.Y-actual.Direction.Y));
            var a=MathF.Abs(expected.AngularSpeed-actual.AngularSpeed);
            if(!float.IsFinite(p)||!float.IsFinite(d)||!float.IsFinite(a)){finite=false;first??=i;continue;}
            position=MathF.Max(position,p);direction=MathF.Max(direction,d);angular=MathF.Max(angular,a);
            if(p>=.005f||d>=.005f||a>=.005f)first??=i;
        }
        var passed=finite&&first is null;
        var result=new ParticleReplayReport(RuntimeInformation.FrameworkDescription,inputDirectory,Convert.ToHexString(SHA256.HashData(input)),
            VulkanRendererBackend.ValidationParticleShaderSha256,count,frame.CollisionIterations,.005f,finite,position,direction,angular,first,passed);
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,ParticleReplayJson.Default.ParticleReplayReport));
        Console.WriteLine($"Snapshot replay: passed={passed},position={position:R},direction={direction:R},angular={angular:R},first={first}");
        if(!passed)throw new InvalidOperationException("Captured particle snapshot exceeds the unchanged comparison tolerances.");
    }
    private sealed class Screen(UiGpuParticleFrame frame):UiScreen
    {public override void Render(UiImmediateContext ui)=>ui.GetWindowDrawList().AddGpuParticles(frame);}
}
internal sealed record ParticleReplayReport(string Framework,string InputDirectory,string InputSha256,string ShaderSha256,int Count,int CollisionIterations,
    float Tolerance,bool Finite,float PositionError,float DirectionError,float AngularError,int? FirstMismatch,bool Passed);
internal sealed record ParticleBenchmarkReport(string Framework,string InputSha256,string ShaderSha256,int Count,int CollisionIterations,int MsaaSamples,
    int Width,int Height,float PlatformScale,int WarmupFrames,double WarmupSeconds,double FinalDrainMilliseconds,BenchFrameStatistics Stats,string Scope);
[JsonSerializable(typeof(ParticleReplayReport))]
[JsonSerializable(typeof(ParticleBenchmarkReport))]
[JsonSourceGenerationOptions(WriteIndented=true)]
internal partial class ParticleReplayJson:JsonSerializerContext;
