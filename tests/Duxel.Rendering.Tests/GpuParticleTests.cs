using Duxel.Core;
using Duxel.Vulkan;
using System.Numerics;
using Duxel.Core.Simulation;
using System.Runtime.InteropServices;

internal static class GpuParticleTests
{
    internal static void Run(VulkanRendererBackend renderer, Func<Action<UiImmediateContext>, UiDrawData> build,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool,string> check, UiTextureId white, UiRect bounds,
        string? failureDirectory = null)
    {
        using var system = new UiGpuParticleSystem([
            new() { Position = new(60,60), Direction = new(1,0), SpeedScale = 1, SizeScale = 1, Color = 0xFF2080E0 },
            new() { Position = new(100,60), Direction = new(-1,0), SpeedScale = 1, SizeScale = 1, Color = 0xFF80E020 },
        ], 8);
        void Draw(UiImmediateContext ui, UiGpuParticleFrame frame)
        {
            ui.BeginLayer("gpu-particle-validation", new UiLayerOptions(false,1,default)
                { Scene3D = new UiLayer3D(Matrix4x4.Identity, false) { Viewport = bounds } });
            var draw = ui.GetWindowDrawList(); draw.PushTexture(white); draw.PushClipRect(bounds);
            draw.AddGpuParticles(frame);
            draw.PopClipRect(); draw.PopTexture(); ui.EndLayer();
        }
        foreach (var samples in new[] {1,4})
        {
            renderer.SetMsaaSamples(samples);
            var reference = capture($"gpu-particles-cpu-reference-{samples}", ui =>
            {
                var draw = ui.GetWindowDrawList(); draw.PushTexture(white); draw.PushClipRect(bounds);
                draw.AddRegularPolygons([new(new(60,60),12,5,0,new(0xFF2080E0)), new(new(100,60),12,5,0,new(0xFF80E020))]);
                draw.PopClipRect(); draw.PopTexture();
            });
            var gpu = capture($"gpu-particles-static-{samples}", ui => Draw(ui, new(system,bounds,0,12)));
            check(reference.AsSpan().SequenceEqual(gpu), $"GPU-generated polygon vertices preserve complete CPU reference image at {samples}x MSAA");
        }
        renderer.SetMsaaSamples(1);
        var initial = renderer.ValidationReadParticleState(system);
        check(initial[0].Position == new UiVector2(60,60) && initial[1].Position == new UiVector2(100,60),
            "GPU initial state layout and copy preserve positions");
        using var pair = new UiGpuParticleSystem([
            new() { Position = new(60,60), Direction = new(1,0), SpeedScale = 1, SizeScale = 1, Color = 0xFFFFFFFF },
            new() { Position = new(70,60), Direction = new(-1,0), SpeedScale = 1, SizeScale = 1, Color = 0xFFFFFFFF },
        ]);
        capture("gpu-particles-isolated-collision", ui => Draw(ui, new(pair,bounds,.01f,6,0,5,4.5f,1)));
        var state = renderer.ValidationReadParticleState(pair);
        check(MathF.Abs(state[0].Position.X - 59) < .00001f && MathF.Abs(state[1].Position.X - 71) < .00001f
            && state[0].Direction.X == -1 && state[1].Direction.X == 1 && state[0].HitResponse == 1 && state[1].HitResponse == 1,
            "GPU isolated collision matches ordered CPU separation and exchanged normal directions");
        var data = build(ui => Draw(ui, new(pair,bounds,0,6,0,5,4.5f,1)));
        try { renderer.RenderAndReadback(data); } finally { data.Dispose(); }
        check(renderer.LastFrameMetrics.GpuParticleDispatches == 0 && renderer.LastFrameMetrics.GpuParticleInitialUploadBytes == 0,
            "paused GPU simulation reuses generated geometry without compute dispatches or particle uploads");
        check(renderer.LastFrameMetrics.GpuParticleCount == 2, "GPU particle counters describe the submitted system");
        static UiGpuParticle Seed(int index) => new() { Position = new(30 + index % 5 * 32, 40 + index / 5 * 36),
            Direction = new(1,0), SpeedScale = 1, SizeScale = 1, Color = 0xFF5080A0, AngularSpeed = .5f };
        static bool Same(ReadOnlySpan<UiGpuParticle> left, ReadOnlySpan<UiGpuParticle> right) =>
            MemoryMarshal.AsBytes(left).SequenceEqual(MemoryMarshal.AsBytes(right));
        using var editable = new UiGpuParticleSystem([Seed(0),Seed(1),Seed(2)]);
        var editBounds = new UiRect(0,0,256,192);
        void RenderEdit(UiGpuParticleFrame frame)
        {
            var editData = build(ui => Draw(ui, frame));
            try { renderer.RenderAndReadback(editData); } finally { editData.Dispose(); }
        }
        var movingFrame = new UiGpuParticleFrame(editable,editBounds,.02f);
        RenderEdit(movingFrame);
        var beforeAppend = renderer.ValidationReadParticleState(editable);
        var queuedFrame = movingFrame with { DeltaSeconds = 0 };
        editable.Append([Seed(3)]);
        RenderEdit(queuedFrame);
        check(queuedFrame.Snapshot.Count == 3 && renderer.LastFrameMetrics.GpuParticleCount == 3
            && Same(renderer.ValidationReadParticleState(editable),beforeAppend),
            "queued GPU particle frames retain their captured population after a producer append");
        RenderEdit(new(editable,editBounds,0));
        var appended = renderer.ValidationReadParticleState(editable);
        check(appended.Length == 4 && Same(appended.AsSpan(0,3),beforeAppend)
            && Same(appended.AsSpan(3),new[] {Seed(3)}) && renderer.LastFrameMetrics.GpuParticleInitialUploadBytes == 48,
            "append within GPU capacity preserves every old state byte and uploads only the new body");
        editable.Append([Seed(4),Seed(5),Seed(6)]);
        RenderEdit(new(editable,editBounds,0));
        var grown = renderer.ValidationReadParticleState(editable);
        check(grown.Length == 7 && Same(grown.AsSpan(0,4),appended)
            && Same(grown.AsSpan(4),new[] {Seed(4),Seed(5),Seed(6)})
            && renderer.LastFrameMetrics.GpuParticleInitialUploadBytes == 3*48 + 8*10*3*4,
            "GPU capacity growth copies live state on device and uploads only new seeds plus indices");
        var replacementSeeds = editable.Initial.Span[2..].ToArray();
        editable.Truncate(2); editable.Append(replacementSeeds);
        RenderEdit(new(editable,editBounds,0));
        var replaced = renderer.ValidationReadParticleState(editable);
        check(replaced.Length == 7 && Same(replaced.AsSpan(0,2),grown.AsSpan(0,2))
            && Same(replaced.AsSpan(2),replacementSeeds) && renderer.LastFrameMetrics.GpuParticleInitialUploadBytes == 5*48,
            "coalesced truncate and append at unchanged count preserves identities only in the surviving prefix");
        editable.Truncate(3);
        RenderEdit(new(editable,editBounds,0));
        var truncated = renderer.ValidationReadParticleState(editable);
        check(truncated.Length == 3 && Same(truncated,replaced.AsSpan(0,3))
            && renderer.LastFrameMetrics.GpuParticleInitialUploadBytes == 0,
            "truncating GPU particles retains surviving motion without state uploads");
        using var resumedReferenceSystem = new UiGpuParticleSystem(truncated);
        var resumedReference = new ParticleJacobiSimulation(resumedReferenceSystem);
        resumedReference.Step(new(resumedReferenceSystem,editBounds,.01f));
        RenderEdit(new(editable,editBounds,.01f));
        var resumed = renderer.ValidationReadParticleState(editable);
        var resumedMatches = resumed.Length == resumedReference.Particles.Length;
        for (var i = 0; i < resumed.Length; i++)
        {
            var expected = resumedReference.Particles[i]; var actual = resumed[i];
            resumedMatches &= MathF.Abs(actual.Position.X-expected.Position.X) < .0001f
                && MathF.Abs(actual.Position.Y-expected.Position.Y) < .0001f
                && MathF.Abs(actual.Direction.X-expected.Direction.X) < .0001f
                && MathF.Abs(actual.Direction.Y-expected.Direction.Y) < .0001f
                && MathF.Abs(actual.Rotation-expected.Rotation) < .0001f
                && MathF.Abs(actual.AngularSpeed-expected.AngularSpeed) < .0001f;
        }
        check(resumedMatches && renderer.LastFrameMetrics.GpuParticleInitialUploadBytes == 0,
            "motion after append growth and truncation advances from preserved GPU state using the shared CPU contract");
        using var cpuEditable = new UiGpuParticleSystem([Seed(0),Seed(1),Seed(2)]);
        var cpuEdits = new ParticleJacobiSimulation(cpuEditable);
        cpuEdits.Step(new(cpuEditable,editBounds,.02f));
        var cpuBefore = cpuEdits.Particles.ToArray();
        cpuEditable.Append([Seed(3)]); cpuEdits.Step(new(cpuEditable,editBounds,0));
        check(cpuEdits.Particles.Length == 4 && Same(cpuEdits.Particles[..3],cpuBefore)
            && Same(cpuEdits.Particles[3..],new[] {Seed(3)}),
            "shared CPU model follows the same snapshot append contract while paused");
        using var glancing = new UiGpuParticleSystem([
            new() {Position=new(60,60),Direction=new(0,1),SpeedScale=1,SizeScale=1,Color=0xFFFFFFFF},
            new() {Position=new(70,60),Direction=new(-.0000001f,-1),SpeedScale=1,SizeScale=1,Color=0xFFFFFFFF},
        ]);
        var glancingFrame = new UiGpuParticleFrame(glancing,bounds,.01f,6,0,5,4.5f,1);
        var glancingReference = new ParticleJacobiSimulation(glancing);
        glancingReference.Step(glancingFrame);
        check(MathF.Abs(glancingReference.Particles[0].AngularSpeed) < .000001f
            && MathF.Abs(glancingReference.Particles[1].AngularSpeed) < .000001f,
            "near-tangential CPU contacts cannot generate finite spin from a vanishing normal impulse");
        RenderEdit(glancingFrame);
        var glancingActual = renderer.ValidationReadParticleState(glancing);
        check(MathF.Abs(glancingActual[0].AngularSpeed) < .000001f && MathF.Abs(glancingActual[1].AngularSpeed) < .000001f,
            "near-tangential GPU contacts bound spin by actual normal impulse strength");
        using var separated=new UiGpuParticleSystem([
            new(){Position=new(359.9092712402344f,175.3715362548828f),Direction=new(.8659496307373047f,.500131368637085f),SpeedScale=1,SizeScale=1.1911109685897827f,Color=0xFFFFFFFF},
            new(){Position=new(345.7333068847656f,180.32005310058594f),Direction=new(.44727298617362976f,-.894397497177124f),SpeedScale=1,SizeScale=1.311392903327942f,Color=0xFFFFFFFF}
        ]);
        var separatedFrame=new UiGpuParticleFrame(separated,new(0,0,1520,918.4625f),.01f,6,0,5,4.5f,1);
        var separatedCpu=new ParticleJacobiSimulation(separated);separatedCpu.Step(separatedFrame);
        check(MathF.Abs(separatedCpu.Particles[0].AngularSpeed)<.000001f&&MathF.Abs(separatedCpu.Particles[1].AngularSpeed)<.000001f,
            "squared CPU contact threshold does not turn an almost-separated pair into a finite second impulse");
        RenderEdit(separatedFrame);var separatedGpu=renderer.ValidationReadParticleState(separated);
        check(MathF.Abs(separatedGpu[0].AngularSpeed)<.000001f&&MathF.Abs(separatedGpu[1].AngularSpeed)<.000001f,
            "squared GPU contact threshold preserves the same near-separation decision despite square-root rounding");
        var random = new Random(1337);
        var many = new UiGpuParticle[5000];
        var worldBounds = new UiRect(0,0,1520,918.4625f);
        for (var i = 0; i < many.Length; i++)
        {
            var angle = (float)random.NextDouble() * MathF.Tau;
            many[i] = new() { Position = new(10 + (float)random.NextDouble() * 1500, 10 + (float)random.NextDouble() * 898),
                Direction = new(MathF.Cos(angle),MathF.Sin(angle)), SpeedScale = .6f + (float)random.NextDouble() * .9f,
                SizeScale = .6f + (float)random.NextDouble() * .8f, Rotation = angle,
                AngularSpeed = (float)random.NextDouble() * 3.6f - 1.8f, Color = 0xFF5080A0 };
        }
        using var largeSystem = new UiGpuParticleSystem(many);
        var referenceSolver = new ParticleJacobiSimulation(largeSystem);
        var largeFrame = new UiGpuParticleFrame(largeSystem,worldBounds,1f/60,6,160,5,4.5f,2);
        referenceSolver.Step(largeFrame);
        capture("gpu-particles-5000-collision", ui => Draw(ui,largeFrame));
        var largeState = renderer.ValidationReadParticleState(largeSystem);
        float maxPositionError = 0, maxDirectionError = 0, maxAngularError = 0;
        var valid = true;
        for (var i = 0; i < largeState.Length; i++)
        {
            var actual = largeState[i]; var expected = referenceSolver.Particles[i];
            maxPositionError = MathF.Max(maxPositionError, MathF.Max(MathF.Abs(actual.Position.X-expected.Position.X),MathF.Abs(actual.Position.Y-expected.Position.Y)));
            maxDirectionError = MathF.Max(maxDirectionError, MathF.Max(MathF.Abs(actual.Direction.X-expected.Direction.X),MathF.Abs(actual.Direction.Y-expected.Direction.Y)));
            maxAngularError = MathF.Max(maxAngularError,MathF.Abs(actual.AngularSpeed-expected.AngularSpeed));
            var radius = actual.SizeScale * 6;
            valid &= float.IsFinite(actual.Position.X) && float.IsFinite(actual.Position.Y)
                && float.IsFinite(actual.Direction.X) && float.IsFinite(actual.Direction.Y)
                && actual.Position.X >= radius - .0001f && actual.Position.Y >= radius - .0001f
                && actual.Position.X <= worldBounds.Width-radius+.0001f && actual.Position.Y <= worldBounds.Height-radius+.0001f;
        }
        Console.WriteLine($"GPU_PARTICLE_ERRORS position={maxPositionError:R} direction={maxDirectionError:R} angular={maxAngularError:R}");
        check(valid && maxPositionError < .005f && maxDirectionError < .005f && maxAngularError < .005f,
            "5000-body GPU Jacobi step matches shared CPU contract within 0.005 source pixels/vector units and preserves finite bounded state");
        var previous = largeState;
        var maximumStepPositionError = maxPositionError;
        var maximumStepDirectionError = maxDirectionError;
        var maximumStepAngularError = maxAngularError;
        var capturedFailure=false;
        for (var step = 0; step < 60; step++)
        {
            using var snapshotSystem = new UiGpuParticleSystem(previous);
            var snapshotReference = new ParticleJacobiSimulation(snapshotSystem);
            snapshotReference.Step(largeFrame with { System = snapshotSystem });
            var drawData = build(ui => Draw(ui,largeFrame));
            try { renderer.RenderAndReadback(drawData); } finally { drawData.Dispose(); }
            var actualState = renderer.ValidationReadParticleState(largeSystem);
            for (var i = 0; i < actualState.Length; i++)
            {
                var actual = actualState[i]; var expected = snapshotReference.Particles[i];
                var positionError=MathF.Max(MathF.Abs(actual.Position.X-expected.Position.X),MathF.Abs(actual.Position.Y-expected.Position.Y));
                var directionError=MathF.Max(MathF.Abs(actual.Direction.X-expected.Direction.X),MathF.Abs(actual.Direction.Y-expected.Direction.Y));
                var angularError=MathF.Abs(actual.AngularSpeed-expected.AngularSpeed);
                if(!capturedFailure && failureDirectory is not null && (positionError>=.005f || directionError>=.005f || angularError>=.005f))
                {
                    var directory=Path.Combine(failureDirectory,"gpu-particle-mismatch");
                    if(Directory.Exists(directory))throw new InvalidOperationException("A captured GPU mismatch already exists; use a fresh output directory to preserve it.");
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory,"input.bin"),MemoryMarshal.AsBytes(previous.AsSpan()).ToArray());
                    File.WriteAllBytes(Path.Combine(directory,"cpu.bin"),MemoryMarshal.AsBytes(snapshotReference.Particles).ToArray());
                    File.WriteAllBytes(Path.Combine(directory,"gpu.bin"),MemoryMarshal.AsBytes(actualState.AsSpan()).ToArray());
                    File.WriteAllText(Path.Combine(directory,"case.json"),System.Text.Json.JsonSerializer.Serialize(new GpuParticleFailureCase(
                        DateTime.UtcNow,step,i,positionError,directionError,angularError,
                        RuntimeInformation.FrameworkDescription,Marshal.SizeOf<UiGpuParticle>(),previous.Length,
                        largeFrame.Bounds,largeFrame.DeltaSeconds,largeFrame.Radius,largeFrame.Speed,largeFrame.Sides,
                        largeFrame.MaximumAngularSpeed,largeFrame.CollisionIterations,.005f,
                        "First observed mismatch; exact previous GPU snapshot, CPU expectation and GPU result; tolerance unchanged"),
                        GpuParticleFailureJson.Default.GpuParticleFailureCase));
                    capturedFailure=true;
                    Console.WriteLine($"GPU_PARTICLE_MISMATCH_CAPTURED step={step} body={i} path={directory}");
                }
                maximumStepPositionError = MathF.Max(maximumStepPositionError,MathF.Max(MathF.Abs(actual.Position.X-expected.Position.X),MathF.Abs(actual.Position.Y-expected.Position.Y)));
                maximumStepDirectionError = MathF.Max(maximumStepDirectionError,MathF.Max(MathF.Abs(actual.Direction.X-expected.Direction.X),MathF.Abs(actual.Direction.Y-expected.Direction.Y)));
                maximumStepAngularError = MathF.Max(maximumStepAngularError,MathF.Abs(actual.AngularSpeed-expected.AngularSpeed));
                var radius = 6*actual.SizeScale;
                if (!float.IsFinite(actual.Position.X) || !float.IsFinite(actual.Position.Y)
                    || !float.IsFinite(actual.Direction.X) || !float.IsFinite(actual.Direction.Y)
                    || actual.Position.X < radius-.0001f || actual.Position.Y < radius-.0001f
                    || actual.Position.X > worldBounds.Width-radius+.0001f || actual.Position.Y > worldBounds.Height-radius+.0001f)
                    throw new InvalidOperationException($"GPU_PARTICLE_STATE_INVALID step={step} body={i}");
            }
            previous = actualState;
        }
        Console.WriteLine($"GPU_PARTICLE_MULTISTEP_ERRORS position={maximumStepPositionError:R} direction={maximumStepDirectionError:R} angular={maximumStepAngularError:R}");
        check(maximumStepPositionError < .005f && maximumStepDirectionError < .005f && maximumStepAngularError < .005f,
            "60 further 5000-body GPU steps match CPU snapshot contract within unchanged 0.005 tolerances and remain finite and bounded");
    }
}
internal sealed record GpuParticleFailureCase(DateTime observedUtc,int step,int body,float positionError,float directionError,float angularError,
    string framework,int particleStride,int count,UiRect Bounds,float DeltaSeconds,float Radius,float Speed,int Sides,float MaximumAngularSpeed,
    int CollisionIterations,float tolerance,string scope);
[System.Text.Json.Serialization.JsonSerializable(typeof(GpuParticleFailureCase))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented=true)]
internal partial class GpuParticleFailureJson:System.Text.Json.Serialization.JsonSerializerContext;
