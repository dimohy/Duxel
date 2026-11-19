using System.Numerics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;
using Duxel.App;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("artifacts/rendering-validation");
ButtonAppearanceTests.ValidateReferences();
FluentIconRenderingTests.ValidateAssets();
Directory.CreateDirectory(output);
Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE", "1");
Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", "100000");
var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    checks.Add(name);
    Console.WriteLine($"PASS {name}");
}
if(args.Length>1&&args[1]=="--accessibility-only")
{
    AccessibilityTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {scope="accessibility-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));return;
}
if(args.Length>2&&(args[1]=="--uia-client"||args[1]=="--uia-client-pid"))
{
    var window=args[1]=="--uia-client-pid"?AccessibilityClientTests.WindowForProcess(int.Parse(args[2])):nint.Parse(args[2]);
    if(window==0)throw new InvalidOperationException("Test target has no native top-level window.");
    AccessibilityClientTests.Run(window,Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {scope="external-uia-client",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));return;
}
if(args.Length>1&&args[1]=="--measure-only")
{
    WidgetMeasurementTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {scope="measure-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));return;
}
if(args.Length>1&&args[1]=="--field-only")
{
    FieldWidgetTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {scope="field-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--fluent-input-only")
{
    FluentInputTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="fluent-input-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--input-kernel-only")
{
    InputKernelTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="input-kernel-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--link-only")
{
    LinkWidgetTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="link-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--switch-only")
{
    SwitchWidgetTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="switch-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--radio-only")
{
    RadioGroupWidgetTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="radio-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--checkbox-only")
{
    CheckboxWidgetTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="checkbox-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--widget-tree-only")
{
    WidgetTreeThemeTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    { scope="widget-tree-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks },new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--button-typography-only")
{
    ButtonTypographyTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="button-typography-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--button-appearance-input-only")
{
    ButtonAppearanceInputTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="button-appearance-input-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--control-ownership-only")
{
    ControlOwnershipTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="control-ownership-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--button-lifetime-only")
{
    ButtonLifetimeTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="button-lifetime-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--divider-only")
{
    DividerWidgetTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="divider-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--text-widget-only")
{
    TextWidgetTests.Run(Check);
    TypographyTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    { scope="text-widget-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks },new JsonSerializerOptions {WriteIndented=true}));
    return;
}
if(args.Length>1&&args[1]=="--badge-only")
{
    BadgeWidgetTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    { scope="badge-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks },new JsonSerializerOptions {WriteIndented=true}));
    Console.WriteLine($"Passed {checks.Count} badge checks.");
    return;
}
if(args.Length>1&&args[1]=="--resize-input-only")
{
    ResizeInteractionTests.Run(Check,args.Length>2?int.Parse(args[2]):2,output);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {passed=checks.Count,checks}));
    return;
}
if(args.Length>1&&args[1]=="--directtext-layout-only")
{
    DirectTextLayoutTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {passed=checks.Count,checks}));
    return;
}
if(args.Length>1&&args[1] is "--gallery-drag-only" or "--gallery-resize-only")
{
    if(args.Length!=4)throw new ArgumentException("Expected output, mode, native gallery path and logical title Y.");
    GalleryDragProcessTests.Run(output,args[2],int.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture),args[1]=="--gallery-resize-only");
    return;
}
if(args.Length>1&&args[1]=="--window-interaction-only")
{
    WindowInteractionTests.Run(Check);
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {passed=checks.Count,checks}));
    return;
}
if (args.Length > 1 && args[1] is "--all-features-root-only" or "--all-features-only" or "--all-features-input-only" or "--all-features-overlay-only" or "--widget-optimization-only")
{
    if (args.Length != 4) throw new ArgumentException("Expected output, mode, compiled sample assembly and source coverage plan.");
    AllFeaturesSampleTests.Run(Check, output, args[2], args[3], args[1] == "--all-features-input-only", args[1] == "--all-features-overlay-only", args[1] == "--widget-optimization-only",args[1]=="--all-features-root-only");
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
    {
        scope = args[1] == "--widget-optimization-only" ? "widget-optimization-only" : "all-features-only", framework = RuntimeInformation.FrameworkDescription, passed = checks.Count, checks,
    }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
FpsCounterTests.Run(Check);
SpinnerTests.Run(Check);
if (args.Length > 1 && args[1] == "--fps-counter-only")
{
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed = checks.Count, checks }));
    return;
}
TextRunCacheTests.Run(Check);
if (args.Length > 1 && args[1] == "--text-run-cache-only")
{
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
    {
        scope = "text-run-cache-only", framework = RuntimeInformation.FrameworkDescription,
        passed = checks.Count, checks,
    }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
ShaderArtifactTests.Run(Check);
BadgeWidgetTests.Run(Check);
DirectTextLayoutTests.Run(Check);
WidgetTreeThemeTests.Run(Check);
ButtonLifetimeTests.Run(Check);
ControlOwnershipTests.Run(Check);
ButtonTypographyTests.Run(Check);
ButtonIconTests.Run(Check);
ButtonAppearanceInputTests.Run(Check);
TextWidgetTests.Run(Check);
DividerWidgetTests.Run(Check);
TypographyTests.Run(Check);
ControlKeyboardTests.Run(Check);
ProgressWidgetTests.Run(Check);
ProgressMotionTests.Run(Check);
SliderWidgetTests.Run(Check);
WindowInteractionTests.Run(Check);
PanelTests.Run(Check);
if (args.Length > 1 && args[1] == "--widget-tree-theme-only")
{
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
    {
        scope = "widget-tree-theme-only", framework = RuntimeInformation.FrameworkDescription,
        passed = checks.Count, checks,
    }, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
PolygonCoreTests.Run(Check);

if (args.Length > 1 && args[1] == "--polygon-core-only")
{
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed = checks.Count, checks }));
    return;
}

if (args.Length > 1 && args[1] == "--sample-window-only")
{
    var fixturePath = Environment.GetEnvironmentVariable("DUXEL_VALIDATION_SAMPLE_ASSEMBLY")
        ?? throw new ArgumentException("A declared sample fixture assembly is required.");
    SampleWindowTests.Run(Check, output, fixturePath);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
    {
        scope = "sample-window-only", framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        passed = checks.Count, checks,
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"PASS focused sample window {checks.Count} checks");
    return;
}

Check(VulkanRendererBackend.ValidationFrameBindingsMatch(),
    "dense frame bindings preserve sparse dictionary lookup semantics across grow, shrink and clear");
AxisAlignedCullingTests.Run(Check);

var workloadClock = new BenchWorkloadClock(1000, 6);
Check(!workloadClock.Advance(0, 1000).Running && !workloadClock.Advance(999, 1000).Running
    && !workloadClock.Started, "workload simulation stays frozen throughout preparation");
var firstWorkloadStep = workloadClock.Advance(2500, 1000);
Check(firstWorkloadStep.Running && firstWorkloadStep.DeltaSeconds == 0
    && workloadClock.StartTimestampTicks == 2500 && workloadClock.DeadlineTimestampTicks == 8500,
    "late preparation does not consume any requested workload duration or enter its first delta");
Check(workloadClock.Advance(2510, 1000).DeltaSeconds == .01,
    "workload delta starts at its own epoch rather than application startup");
Check(workloadClock.Contains(2500) && workloadClock.Contains(8499)
    && !workloadClock.Contains(2499) && !workloadClock.Contains(8500),
    "workload source timestamps use an inclusive start and exclusive deadline");
Check(workloadClock.Advance(8500, 1000).Complete && workloadClock.Advance(9000, 1000).Complete,
    "workload completion is exact at the deadline and remains terminal");
var reversedClockRejected = false;
try { workloadClock.Advance(8999, 1000); }
catch (ArgumentOutOfRangeException) { reversedClockRejected = true; }
Check(reversedClockRejected, "workload timestamp regression is rejected explicitly");
var zeroEpochClock = new BenchWorkloadClock(1000, 1);
Check(zeroEpochClock.Advance(0, 0).Running && zeroEpochClock.Contains(0)
    && zeroEpochClock.Advance(1000, 0).Complete, "zero timestamp is a valid epoch rather than a not-started sentinel");

UiRendererFrameMetrics PresentationSample(ulong sequence) => default(UiRendererFrameMetrics) with
{
    PresentationSequence = sequence, DrawDataFrameId = sequence + 1000,
    PresentationTimestampTicks = checked((long)sequence * 1000), PresentationIntervalSeconds = .001,
};
var presentationHistory = new UiPresentationHistory(4);
for (var sequence = 1UL; sequence <= 3; sequence++) presentationHistory.Publish(PresentationSample(sequence));
var historyBuffer = new UiRendererFrameMetrics[4];
Check(presentationHistory.Latest.PresentationSequence == 3
    && presentationHistory.Drain(historyBuffer.AsSpan(0, 2)) == 2
    && historyBuffer[0].PresentationSequence == 1 && historyBuffer[1].PresentationSequence == 2
    && historyBuffer[1].DrawDataFrameId == 1002,
    "presentation history preserves every accepted frame when rendering advances ahead of UI consumption");
Check(presentationHistory.Drain(historyBuffer) == 1 && historyBuffer[0].PresentationSequence == 3,
    "partial presentation drains preserve FIFO order across reads");
presentationHistory.Publish(PresentationSample(3) with { DrawCalls = 7 });
Check(presentationHistory.Drain(historyBuffer) == 0 && presentationHistory.Latest.DrawCalls == 7,
    "duplicate presentation sequences refresh the latest snapshot without duplicating a timing sample");
for (var sequence = 4UL; sequence <= 9; sequence++) presentationHistory.Publish(PresentationSample(sequence));
Check(presentationHistory.OverwrittenSamples == 2 && presentationHistory.Drain(Span<UiRendererFrameMetrics>.Empty) == 0
    && presentationHistory.Drain(historyBuffer) == 4 && historyBuffer[0].PresentationSequence == 6
    && historyBuffer[3].PresentationSequence == 9,
    "bounded presentation history reports overwritten samples and preserves its newest complete FIFO window");
var historyRegressionRejected = false;
try { presentationHistory.Publish(PresentationSample(8)); }
catch (InvalidOperationException) { historyRegressionRejected = true; }
Check(historyRegressionRejected && presentationHistory.Latest.PresentationSequence == 9,
    "presentation sequence regression is rejected instead of corrupting feedback order");
var concurrentHistory = new UiPresentationHistory(512);
var historyProducerDone = 0;
var historyReadCount = 0;
var historyProducer = Task.Run(() =>
{
    try { for (var sequence = 1UL; sequence <= 500; sequence++) concurrentHistory.Publish(PresentationSample(sequence)); }
    finally { Volatile.Write(ref historyProducerDone, 1); }
});
var historyConsumer = Task.Run(() =>
{
    var buffer = new UiRendererFrameMetrics[7];
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (true)
    {
        var count = concurrentHistory.Drain(buffer);
        for (var i = 0; i < count; i++)
        {
            historyReadCount++;
            if (buffer[i].PresentationSequence != (ulong)historyReadCount || buffer[i].DrawDataFrameId != (ulong)historyReadCount + 1000)
                throw new InvalidOperationException("Concurrent presentation feedback was torn or reordered.");
        }
        if (count == 0 && Volatile.Read(ref historyProducerDone) != 0) break;
        if (DateTime.UtcNow >= deadline) throw new TimeoutException("Concurrent presentation feedback did not drain.");
        if (count == 0) Thread.Yield();
    }
});
Check(Task.WhenAll(historyProducer, historyConsumer).Wait(TimeSpan.FromSeconds(6))
    && historyReadCount == 500 && concurrentHistory.OverwrittenSamples == 0,
    "renderer publication and UI drains preserve 500 source identities without torn metrics or lost samples");
var historyState = new UiState();
historyState.RendererFrameMetrics = PresentationSample(1);
historyState.RendererFrameMetrics = PresentationSample(2);
var detachedHistoryReader = historyState.PresentationReader;
historyState.ReleasePooledBuffers();
Check(detachedHistoryReader.Drain(historyBuffer) == 2 && historyBuffer[0].PresentationSequence == 1
    && historyBuffer[1].DrawDataFrameId == 1002 && detachedHistoryReader.Latest.PresentationSequence == 2,
    "a presentation reader retains final timing data independently of UI state buffer disposal");

UiDrawData EmptySubmission(ulong id) => new(default, default, new(1, 1), 0, 0,
    UiPooledList<UiDrawList>.RentAndCopy(ReadOnlySpan<UiDrawList>.Empty),
    UiPooledList<UiTextureUpdate>.RentAndCopy(ReadOnlySpan<UiTextureUpdate>.Empty)) { DrawDataFrameId = id };
DuxelRenderWork Submission(UiDrawData data) => new(data, false, 1, default, 0, 0, 0, 0, 0, 0, AllocationSample: null);
var texturePixels = new byte[] { 11, 22, 33, 44, 55, 66, 77, 88 };
var textureFrame = EmptySubmission(1) with
{
    TextureUpdates = UiPooledList<UiTextureUpdate>.RentAndCopy(new UiTextureUpdate[]
    {
        new(UiTextureUpdateKind.Create, new(7), UiTextureFormat.Rgba8Unorm, 1, 1, texturePixels.AsMemory(0, 4)),
        new(UiTextureUpdateKind.Update, new(7), UiTextureFormat.Rgba8Unorm, 1, 1, texturePixels.AsMemory(4, 4)),
        new(UiTextureUpdateKind.Destroy, new(8), UiTextureFormat.Rgba8Unorm, 1, 1, ReadOnlyMemory<byte>.Empty),
    }.AsSpan()),
};
textureFrame.CaptureTexturePayloads();
texturePixels.AsSpan().Fill(255);
textureFrame.CaptureTexturePayloads();
Check(textureFrame.TextureUpdates[0].RgbaPixels.Span.SequenceEqual(new byte[] { 11, 22, 33, 44 })
    && textureFrame.TextureUpdates[1].RgbaPixels.Span.SequenceEqual(new byte[] { 55, 66, 77, 88 })
    && textureFrame.TextureUpdates[2].RgbaPixels.IsEmpty,
    "texture handoff captures independent create/update slices once and preserves empty destroy payloads");
var scaledTextureFrame = textureFrame.ScaleClipRects(new(2, 2));
textureFrame.Dispose();
Check(scaledTextureFrame.TextureUpdates[0].RgbaPixels.Span.SequenceEqual(new byte[] { 11, 22, 33, 44 }),
    "scaled frame texture payload survives original captured frame disposal");
scaledTextureFrame.Dispose();

using (var workerEntered = new ManualResetEventSlim())
using (var releaseWorker = new ManualResetEventSlim())
using (var thirdSubmitEntered = new ManualResetEventSlim())
{
    var renderedIds = new List<ulong>();
    var queuedFrames = new[] { EmptySubmission(1), EmptySubmission(2), EmptySubmission(3) };
    var producerThreadId = Environment.CurrentManagedThreadId;
    var consumerThreadId = 0;
    var queue = new DuxelRenderQueue(work =>
    {
        consumerThreadId = Environment.CurrentManagedThreadId;
        if (work.Data.DrawDataFrameId == 1)
        {
            workerEntered.Set();
            if (!releaseWorker.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Queue worker release");
        }
        renderedIds.Add(work.Data.DrawDataFrameId);
    }, () => { });
    queue.Submit(Submission(queuedFrames[0]));
    Check(workerEntered.Wait(TimeSpan.FromSeconds(5)), "render worker executes independently of the UI producer");
    queue.Submit(Submission(queuedFrames[1]));
    var thirdProducer = Task.Run(() =>
    {
        thirdSubmitEntered.Set();
        queue.Submit(Submission(queuedFrames[2]));
    });
    Check(thirdSubmitEntered.Wait(TimeSpan.FromSeconds(5)) && !thirdProducer.Wait(50),
        "one queued frame applies backpressure while another frame executes");
    var admissionWait = Task.Run(queue.WaitForCapacity);
    Check(!admissionWait.Wait(50), "producer admission waits before sampling input or constructing a third queued CPU frame");
    releaseWorker.Set();
    Check(thirdProducer.Wait(TimeSpan.FromSeconds(5)), "backpressure wakes the producer when a queue slot becomes free");
    Check(admissionWait.Wait(TimeSpan.FromSeconds(5)), "producer admission resumes when pending work advances to execution");
    queue.Dispose();
    Check(renderedIds.SequenceEqual(new ulong[] { 1, 2, 3 }) && producerThreadId != consumerThreadId
        && queuedFrames.All(frame => frame.DrawLists.Count == 0 && frame.TextureUpdates.Count == 0),
        "queue shutdown joins the worker, drains every frame in order and releases transferred ownership");
    var rejectedFrame = EmptySubmission(4);
    var rejected = false;
    try { queue.Submit(Submission(rejectedFrame)); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected && rejectedFrame.DrawDataFrameId == 4,
        "submission after completion is rejected and leaves ownership with the producer");
    rejectedFrame.Dispose();
}
using (var workerEntered = new ManualResetEventSlim())
using (var releaseWorker = new ManualResetEventSlim())
using (var failureNotified = new ManualResetEventSlim())
{
    var expectedError = new InvalidOperationException("intentional render worker failure");
    var failedFrames = new[] { EmptySubmission(10), EmptySubmission(11) };
    var queue = new DuxelRenderQueue(_ =>
    {
        workerEntered.Set();
        if (!releaseWorker.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Queue failure release");
        throw expectedError;
    }, failureNotified.Set);
    queue.Submit(Submission(failedFrames[0]));
    if (!workerEntered.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Queue failure start");
    queue.Submit(Submission(failedFrames[1]));
    releaseWorker.Set();
    Check(failureNotified.Wait(TimeSpan.FromSeconds(5)), "render failure wakes the application shutdown path");
    var propagated = false;
    try { queue.Dispose(); }
    catch (InvalidOperationException ex) when (ReferenceEquals(ex, expectedError)) { propagated = true; }
    Check(propagated && failedFrames.All(frame => frame.DrawLists.Count == 0 && frame.TextureUpdates.Count == 0),
        "render failure preserves the original exception and releases executing and queued frames before join returns");
}
var callbackFrame = EmptySubmission(20) with
{
    DrawLists = UiPooledList<UiDrawList>.RentAndCopy(new[]
    {
        new UiDrawList(UiPooledList<UiDrawVertex>.FromArray([]), UiPooledList<uint>.FromArray([]),
            UiPooledList<UiDrawCommand>.FromArray([new(default, default, 0, 0, 0, Callback: (_, _) => { })])),
    }.AsSpan()),
};
using (var queue = new DuxelRenderQueue(_ => { }, () => { }))
{
    Check(queue.Submit(Submission(callbackFrame)), "callback frames require a producer barrier before the next UI frame");
    queue.WaitForIdle();
}

// The GPU stream must stay byte-identical to position.xy, uv.xy, RGBA.
var vertex = new UiDrawVertex(new(1.25f, -2.5f), new(.25f, .75f), new(0xAB123456));
var bytes = MemoryMarshal.AsBytes(new[] { vertex }.AsSpan());
Check(bytes.Length == 20 && BitConverter.ToSingle(bytes[..4]) == 1.25f
    && BitConverter.ToSingle(bytes.Slice(4, 4)) == -2.5f
    && BitConverter.ToSingle(bytes.Slice(8, 4)) == .25f
    && BitConverter.ToSingle(bytes.Slice(12, 4)) == .75f
    && BitConverter.ToUInt32(bytes.Slice(16, 4)) == 0xAB123456, "vertex ABI: 20 bytes, exact fields");

using var platform = new WindowsPlatformBackend(new WindowsPlatformBackendOptions
{
    Width = args.Contains("--coverage-benchmark", StringComparer.Ordinal) ? 1280 : 320,
    Height = args.Contains("--coverage-benchmark", StringComparer.Ordinal) ? 800 : 240,
    MinWidth = 100, MinHeight = 100,
    Title = "Duxel 3D rendering validation", VSync = false, Resizable = true,
    KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
    TrayOptions = new(),
});
platform.PollEvents();
platform.WakeEvents();
platform.PollEvents();
var preSignaledWait = Stopwatch.StartNew();
platform.WaitEvents(1000);
Check(preSignaledWait.ElapsedMilliseconds < 500,
    "platform wake remains latched before waiting and is not consumed by message polling");
var crossThreadSignal = 0;
var crossThreadWake = Task.Run(() =>
{
    Thread.Sleep(40);
    Volatile.Write(ref crossThreadSignal, 1);
    platform.WakeEvents();
});
var crossThreadWait = Stopwatch.StartNew();
do { platform.WaitEvents(1000); }
while (Volatile.Read(ref crossThreadSignal) == 0 && crossThreadWait.ElapsedMilliseconds < 1000);
Check(crossThreadWake.Wait(TimeSpan.FromSeconds(2)) && crossThreadWait.ElapsedMilliseconds < 500,
    "worker event wakes the native message wait without polling for a timeout");
platform.PollEvents();
Check(NativeTestMethods.PostMessageW(platform.WindowHandle, 0x0200, 0, (nint)(48 | 64 << 16)),
    "test mouse message posts to its own window without moving global input");
unsafe
{
    var messageStorage = stackalloc byte[64];
    Check(NativeTestMethods.PeekMessageW((nint)messageStorage, platform.WindowHandle, 0x0200, 0x0200, 0),
        "test observes a queued mouse message without removing it");
}
var oldInputWait = Stopwatch.StartNew();
platform.WaitEvents(1000);
Check(oldInputWait.ElapsedMilliseconds < 500 && platform.Input.Snapshot.MousePosition == new UiVector2(48, 64),
    "native wait dispatches previously observed unread input instead of waiting for another message");

using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(2, EnableVSync: false, MsaaSamples: 1));
renderer.CreateDeviceObjects();
var size = platform.FramebufferSize;
var width = size.Width;
var height = size.Height;
var white = new UiTextureId(2);
var atlas = new UiFontAtlas(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
    new Dictionary<int, UiGlyphInfo>(), new Dictionary<uint, float>(), 18, 0, 0, -1);
var ui = new UiContext(atlas, new(1), white);
var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
var rect = new UiRect(0, 0, width, height);
bool uploadWhite = true;
UiDrawData Build(Action<UiImmediateContext> draw)
{
    platform.PollEvents();
    ui.NewFrame(new(1f / 60, new(width, height), new(1, 1)), input, rect, UiTextSettings.Default);
    if (uploadWhite)
    {
        ui.QueueTextureUpdate(new UiTextureUpdate(UiTextureUpdateKind.Create, white,
            UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 }));
        uploadWhite = false;
    }
    ui.Render(new ActionScreen(draw));
    return ui.GetDrawData();
}
byte[] Capture(string name, Action<UiImmediateContext> draw) => CaptureWithInspection(name, draw, null);
byte[] CaptureWithInspection(string name, Action<UiImmediateContext> draw, Action<UiDrawData>? inspect)
{
    var data = Build(draw);
    try
    {
        inspect?.Invoke(data);
        var previousSequence = renderer.LastFrameMetrics.PresentationSequence;
        var pixels = renderer.RenderAndReadback(data);
        if (previousSequence == 0) Check(renderer.LastFrameMetrics.PresentationSequence == 1
            && renderer.LastFrameMetrics.PresentationTimestampTicks > 0
            && renderer.LastFrameMetrics.PresentationIntervalSeconds == 0,
            "first accepted presentation starts at sequence one with no fabricated interval");
        using var file = File.Create(Path.Combine(output, name + ".ppm"));
        file.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
        for (var i = 0; i < pixels.Length; i += 4) file.Write(pixels.AsSpan(i, 3));
        return pixels;
    }
    finally { data.ReleasePooled(); }
}
UiDrawList TransferTriangle(UiImmediateContext ui, UiVector2 a, UiVector2 b, UiVector2 c, UiColor color)
{
    var source = new UiDrawListBuilder(rect, retainScratchCapacity: false, poolScratchBuffers: false);
    source.AddTriangleFilled(a, b, c, color);
    var lists = source.Build();
    var original = lists[0];
    lists.Return();
    ui.GetWindowDrawList().AppendLayerReference(original, 1, default, scene3D: null);
    return original;
}
UiColor Red = new(0xFF0000FF), Blue = new(0xFFFF0000), Green = new(0xFF00FF00);
var wrappedHeight = 0f;
var lineHeight = 0f;
var rightColumnX = 0f;
var expectedRightColumnX = 0f;
using (var wrappedColumns = Build(ui =>
{
    ui.SetNextWindowPos(new(0, 0));
    ui.SetNextWindowSize(new(320, 220));
    ui.BeginWindow("Wrap columns");
    ui.Columns(2);
    ui.SetColumnWidth(0, 100);
    var start = ui.GetCursorScreenPos();
    lineHeight = ui.GetFrameHeight();
    ui.TextWrapped("A long sentence must wrap inside the current narrow column instead of flowing over the adjacent column.");
    wrappedHeight = ui.GetCursorScreenPos().Y - start.Y;
    expectedRightColumnX = start.X + ui.GetColumnWidth(0);
    ui.NextColumn();
    rightColumnX = ui.GetCursorScreenPos().X;
    ui.Text("Right");
    ui.Columns(1);
    ui.EndWindow();
}))
{
    Check(wrappedHeight > lineHeight * 2, "TextWrapped uses the active column width without requiring an explicit wrap stack");
    Check(rightColumnX >= expectedRightColumnX, "wrapped text preserves the adjacent column origin");
}
// The layout-only frame is never submitted; keep its initial texture upload pending.
uploadWhite = true;
OverlayPresentationTests.Run(renderer, Build, Capture, Check, white, rect);
DamageRenderingTests.Run(renderer, Capture, Build, Check, white, rect);
if(!args.Contains("--legacy-rendering-reference",StringComparer.Ordinal))
    RoundedGradientTests.Run(renderer,Capture,Build,Check,white,rect);
if(args.Length>1&&args[1]=="--rounded-gradient-only")
{
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="rounded-gradient-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks}));
    return;
}
if(!args.Contains("--legacy-rendering-reference",StringComparer.Ordinal))
    ProgressRenderingTests.Run(Capture,Build,Check,width,height);
SliderRenderingTests.Run(Capture,Check);
if(args.Length>1&&args[1]=="--progress-only")
{
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
    {scope="progress-only",framework=RuntimeInformation.FrameworkDescription,passed=checks.Count,checks}));
    return;
}
UiIdentifierTests.Run(Check);
if (args.Length > 1 && args[1] == "--overlays-only")
{
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed = checks.Count, checks }));
    return;
}
GpuParticleTests.Run(renderer, Build, Capture, Check, white, rect, output);
if (args.Length > 1 && args[1] == "--gpu-particles-only")
{
    File.WriteAllText(Path.Combine(output,"result.json"), JsonSerializer.Serialize(new {passed=checks.Count,checks}));
    return;
}
var polygonViewport = new UiRect(24, 24, width - 48, height - 48);
var polygonPixelShapes = Enumerable.Range(0, 30).Select(i => new UiRegularPolygon(
    new(24 + i % 6 * 42, 24 + i / 6 * 38), 15, 3 + i % 10, i * .17f,
    new UiColor(i % 2 == 0 ? 0xFF2080E0u : 0x8020E080u))).ToArray();
void PolygonPixelScene(UiImmediateContext c, bool canvas, bool retained)
{
    var shouldDraw = !canvas || c.BeginLayer("polygon-pixel-canvas", new UiLayerOptions(false, 1, default)
        { Scene3D = new UiLayer3D(Matrix4x4.Identity, false) { Viewport = polygonViewport }, ContentVersion = retained ? 1 : null });
    if (shouldDraw)
    {
        var draw = c.GetWindowDrawList();
        draw.PushTexture(white); draw.PushClipRect(polygonViewport);
        draw.AddRegularPolygons(polygonPixelShapes);
        draw.PopClipRect(); draw.PopTexture();
    }
    if (canvas) c.EndLayer();
}
foreach (var samples in new[] { 1, 4 })
{
    renderer.SetMsaaSamples(samples);
    var direct = Capture($"polygon-direct-{samples}", c => PolygonPixelScene(c, false, false));
    var canvas = Capture($"polygon-canvas-{samples}", c => PolygonPixelScene(c, true, false));
    Check(direct.AsSpan().SequenceEqual(canvas), $"polygon canvas preserves complete clipped opaque and translucent image at {samples}x MSAA");
    Capture($"polygon-resident-first-{samples}", c => PolygonPixelScene(c, true, true));
    var replay = Capture($"polygon-resident-replay-{samples}", c => PolygonPixelScene(c, true, true));
    Check(direct.AsSpan().SequenceEqual(replay) && renderer.ValidationUploads.DynamicBytes == 0,
        $"retained polygon canvas preserves pixels without dynamic geometry upload at {samples}x MSAA");
}
renderer.SetMsaaSamples(1);
void Layer(UiImmediateContext context, string id, Matrix4x4 transform, UiColor color, UiRect? bounds = null, bool writeDepth = true)
{
    Check(context.BeginLayer(id, new(false, 1, default) { Scene3D = new(transform, writeDepth) }), "dynamic layer builds: " + id);
    context.GetWindowDrawList().AddRectFilled(bounds ?? new(width * .1f, height * .1f, width * .8f, height * .8f), color, white);
    context.EndLayer();
}
bool ColorAt(byte[] image, float x, float y, UiColor expected, int tolerance = 2)
{
    var index = ((int)(height * y) * width + (int)(width * x)) * 4;
    var value = expected.Rgba;
    return Math.Abs(image[index] - (byte)value) <= tolerance
        && Math.Abs(image[index + 1] - (byte)(value >> 8)) <= tolerance
        && Math.Abs(image[index + 2] - (byte)(value >> 16)) <= tolerance;
}

var front = Matrix4x4.CreateTranslation(0, 0, .2f);
var back = Matrix4x4.CreateTranslation(0, 0, .7f);
if (args.Contains("--single-sample-coverage-probe", StringComparer.Ordinal))
{
    var first = Capture("rounded-depth-1x", c => RoundedDepth(c, false));
    var second = Capture("rounded-depth-1x-reversed", c => RoundedDepth(c, true));
    var changedPixels = 0;
    for (var i = 0; i < first.Length; i += 4)
        if (!first.AsSpan(i, 4).SequenceEqual(second.AsSpan(i, 4))) changedPixels++;
    File.WriteAllText(Path.Combine(output, "single-sample-coverage.json"), JsonSerializer.Serialize(new
    {
        msaa = 1, changedPixels, orderIndependent = changedPixels == 0,
        scope = "Full-image opaque rounded-plane analytic edge coverage,opposite depth submission order",
    }, new JsonSerializerOptions { WriteIndented = true }));
    Check(changedPixels == 0, "MSAA1 opaque analytic edges preserve full-image depth order");
    return;
}
if (args.Contains("--coverage-benchmark", StringComparer.Ordinal))
{
    CoverageBenchmark.Run(renderer, Build, width, height, white, output);
    return;
}
var ordered = Capture("depth-forward", c => { Layer(c, "front", front, Red); Layer(c, "back", back, Blue); });
using (var reservationUi = new UiContext(atlas, new(1), white))
{
    byte[]? firstPixels = null;
    for (var frame = 0; frame < 2; frame++)
    {
        reservationUi.NewFrame(new(1f / 60, new(width, height), new(1, 1)), input, rect, UiTextSettings.Default);
        reservationUi.Render(new ActionScreen(c =>
        {
            if (c.BeginLayer("reservation.retained", new(true, 1, default) { Scene3D = new(front) }))
                for (var i = 0; i < 2000; i++)
                    c.GetWindowDrawList().AddTriangleFilled(new(10, 10), new(30, 10), new(10, 30), Red);
            c.EndLayer();
            c.GetWindowDrawList().AddTriangleFilled(new(40, 40), new(60, 40), new(40, 60), Green);
        }));
        var data = reservationUi.GetDrawData();
        try
        {
            Check(data.TotalVertexCount >= 6003 && data.TotalIndexCount >= 6003,
                "retained geometry remains included in public scene totals on frame " + frame);
            int Reservation(string field) => (int)typeof(UiContext).GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(reservationUi)!;
            Check(Reservation("_reserveVertices") <= 512 && Reservation("_reserveIndices") <= 512
                && Reservation("_reserveCommands") <= 64,
                "transient builder reservation excludes borrowed scene geometry on frame " + frame);
            var pixels = renderer.RenderAndReadback(data);
            if (firstPixels is null) firstPixels = pixels;
            else Check(pixels.AsSpan().SequenceEqual(firstPixels),
                "bounded transient reservation preserves retained replay GPU pixels");
        }
        finally { data.ReleasePooled(); }
    }
}
var reversed = Capture("depth-reversed", c => { Layer(c, "back", back, Blue); Layer(c, "front", front, Red); });
Check(ordered.AsSpan().SequenceEqual(reversed), "opaque depth is independent of submission order, full image");
Check(ColorAt(ordered, .5f, .5f, Red), "front surface wins");

var savedInput = input;
input = input with { MiddleMouseDown = true, MiddleMousePressed = true, Modifiers = KeyModifiers.Ctrl };
var middleFrame = Build(c => Check(c.IsMouseDown(2) && c.IsMouseClicked(2) && c.IsAnyMouseDown()
    && c.GetKeyModifiers() == KeyModifiers.Ctrl && ui.GetIO().MiddleMouseDown,
    "platform middle button and modifiers reach immediate queries and IO"));
middleFrame.ReleasePooled();
input = savedInput with { MiddleMouseReleased = true };
Build(c => Check(!c.IsMouseDown(2) && c.IsMouseReleased(2), "middle release reaches immediate queries")).ReleasePooled();
input = savedInput;
ui.AddMouseButtonEvent(2, true);
Build(c => Check(c.IsMouseDown(2) && c.IsMouseClicked(2), "queued middle press is delivered")).ReleasePooled();
ui.AddMouseButtonEvent(2, false);
Build(c => Check(!c.IsMouseDown(2) && c.IsMouseReleased(2), "queued middle release is delivered")).ReleasePooled();
ui.AddMouseButtonEvent(2, true);
ui.ClearEventsQueue();
Build(c => Check(!c.IsMouseDown(2) && !c.IsMouseClicked(2), "clearing queued input discards pending middle press")).ReleasePooled();

var crossA = Matrix4x4.CreateRotationY(.65f) * Matrix4x4.CreateTranslation(0, 0, .5f);
var crossB = Matrix4x4.CreateRotationY(-.65f) * Matrix4x4.CreateTranslation(0, 0, .5f);
var crossed = Capture("crossed", c => { Layer(c, "a", crossA, Red); Layer(c, "b", crossB, Blue); });
var crossedReverse = Capture("crossed-reversed", c => { Layer(c, "b", crossB, Blue); Layer(c, "a", crossA, Red); });
Check(crossed.AsSpan().SequenceEqual(crossedReverse), "intersecting planes: full-image submission-order equivalence");
Check(ColorAt(crossed, .35f, .5f, Blue) && ColorAt(crossed, .65f, .5f, Red), "intersecting planes: opposite visibility on each side");

var holes = Capture("geometry-gap", c =>
{
    c.BeginLayer("frame", new(false, 1, default) { Scene3D = new(front) });
    var d = c.GetWindowDrawList();
    d.AddRectFilled(new(width * .1f, height * .1f, width * .8f, height * .15f), Green, white);
    d.AddRectFilled(new(width * .1f, height * .75f, width * .8f, height * .15f), Green, white);
    d.AddRectFilled(new(width * .1f, height * .25f, width * .15f, height * .5f), Green, white);
    d.AddRectFilled(new(width * .75f, height * .25f, width * .15f, height * .5f), Green, white);
    c.EndLayer();
    Layer(c, "background", back, Blue);
    // Ordinary 2D overlay remains above the 3D scene.
    c.GetWindowDrawList().AddRectFilled(new(width * .45f, height * .7f, width * .1f, height * .05f), Red, white);
});
Check(ColorAt(holes, .5f, .5f, Blue) && ColorAt(holes, .15f, .5f, Green), "empty geometry reveals rear layer without offscreen compositing");
Check(ColorAt(holes, .5f, .72f, Red), "2D overlay coexists with 3D depth");

var view = Matrix4x4.CreateLookAt(new(0, 0, 3), Vector3.Zero, Vector3.UnitY);
var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, (float)width / height, .1f, 10);
var nearImage = Capture("perspective-near", c => Layer(c, "camera", view * projection, Red));
var farImage = Capture("perspective-far", c => Layer(c, "camera", Matrix4x4.CreateTranslation(0, 0, -2) * view * projection, Red));
int ColoredPixels(byte[] image) { var count = 0; for (var i = 0; i < image.Length; i += 4) if (image[i] > 200) count++; return count; }
Check(ColoredPixels(nearImage) > ColoredPixels(farImage) * 2 && ColoredPixels(farImage) > 100, "perspective camera shrinks more distant geometry");
Capture("perspective-behind", c => Layer(c, "behind-camera", Matrix4x4.CreateTranslation(0, 0, 5) * view * projection, Red));
Check(renderer.LastFrameMetrics.DrawCalls == 0, "geometry wholly behind the perspective camera submits no draws");
var nearCrossing = Capture("perspective-near-crossing", c => Layer(c, "near-crossing",
    Matrix4x4.CreateRotationY(1.2f) * Matrix4x4.CreateTranslation(0, 0, -.15f) * projection, Red));
Check(ColoredPixels(nearCrossing) > 100 && renderer.LastFrameMetrics.DrawCalls > 0,
    "geometry crossing the perspective near plane remains visible");

var alphaTexture = new UiTextureId(3);
ui.QueueTextureUpdate(new(UiTextureUpdateKind.Create, alphaTexture, UiTextureFormat.Rgba8Unorm, 4, 1,
    new byte[] { 255, 255, 255, 0, 255, 255, 255, 0, 255, 255, 255, 255, 255, 255, 255, 255 }));
var alphaImage = Capture("texture-alpha", c =>
{
    c.BeginLayer("alpha-texture", new(false, 1, default) { Scene3D = new(front) });
    c.GetWindowDrawList().AddImage(alphaTexture, new(width * .1f, height * .1f), new(width * .9f, height * .9f), new(0, 0), new(1, 1), Red);
    c.EndLayer();
    Layer(c, "behind-alpha", back, Blue);
});
Check(ColorAt(alphaImage, .25f, .5f, Blue) && ColorAt(alphaImage, .75f, .5f, Red), "texture alpha zero leaves depth unwritten; opaque texels occlude");
var blended = Capture("partial-alpha", c =>
{
    Layer(c, "blend-background", back, Blue);
    Layer(c, "blend-front", front, new(0x800000FF), writeDepth: false);
});
var blendExpected = renderer.ValidationSrgb ? new UiColor(0xFFBB00BC) : new UiColor(0xFF7F0080);
Check(ColorAt(blended, .5f, .5f, blendExpected, 3), "partial alpha uses read-only depth and the selected attachment encoding");

var clipped = Capture("transformed-local-clip", c =>
{
    c.BeginLayer("local-clip", new(false, 1, default) { Scene3D = new(Matrix4x4.CreateTranslation(.6f, 0, .3f)) });
    var draw = c.GetWindowDrawList();
    draw.PushClipRect(new(width * .2f, height * .2f, width * .2f, height * .6f), false);
    draw.AddRectFilled(new(width * .1f, height * .1f, width * .8f, height * .8f), Red, white);
    draw.PopClipRect();
    c.EndLayer();
});
Check(ColorAt(clipped, .6f, .5f, Red) && ColorAt(clipped, .3f, .5f, new(0xFF000000))
    && ColorAt(clipped, .8f, .5f, new(0xFF000000)), "local clipping moves with the 3D plane");
var staticBuilds = 0;
void StaticScene(UiImmediateContext c, float x)
{
    if (c.BeginLayer("static-camera", new(true, 1, default) { Scene3D = new(Matrix4x4.CreateTranslation(x, 0, .3f)) }))
    {
        staticBuilds++;
        c.GetWindowDrawList().AddRectFilled(new(width * 1.1f, height * .3f, width * .2f, height * .3f), Red, white);
    }
    c.EndLayer();
}
var staticA = Capture("static-camera-a", c => StaticScene(c, -1.6f));
var staticB = Capture("static-camera-b", c => StaticScene(c, -1.2f));
Check(staticBuilds == 1 && ColorAt(staticA, .4f, .5f, Red) && ColorAt(staticB, .6f, .5f, Red),
    "static geometry remains reusable while the 3D camera changes");

var residentBuilds = new Dictionary<string, int>();
void Resident(UiImmediateContext c, string id, UiLayer3D scene, UiRect bounds, UiColor color, long revision = 0)
{
    if (c.BeginLayer(id, new(false, 1, default) { Scene3D = scene, ContentVersion = revision }))
    {
        residentBuilds[id] = residentBuilds.GetValueOrDefault(id) + 1;
        c.GetWindowDrawList().AddRectFilled(bounds, color, white);
    }
    c.EndLayer();
}
void VisibilityScene(UiImmediateContext c, float translation)
{
    Resident(c, "visibility-resident", new(Matrix4x4.CreateTranslation(translation, 0, .3f)),
        new(width * .25f, height * .25f, width * .5f, height * .5f), Red);
}
var visibilityOriginal = Capture("visibility-original", c => VisibilityScene(c, 0));
Check(renderer.LastFrameMetrics.DrawCalls > 0, "visible resident geometry submits GPU draws");
Capture("visibility-hidden", c => VisibilityScene(c, 4));
Check(renderer.LastFrameMetrics.DrawCalls == 0, "offscreen resident geometry submits no GPU draws");
Check(renderer.LastFrameMetrics.CulledLayerLists == 1 && renderer.LastFrameMetrics.LayerTransformPushes == 0,
    "offscreen layer is rejected before any GPU transform state is pushed");
for (var frame = 0; frame < 200; frame++)
{
    var data = Build(c => VisibilityScene(c, 4));
    try { renderer.RenderAndReadback(data); } finally { data.ReleasePooled(); }
}
var visibilityReturned = Capture("visibility-returned", c => VisibilityScene(c, 0));
var visibilityUploads = renderer.ValidationUploads;
Check(visibilityOriginal.AsSpan().SequenceEqual(visibilityReturned)
    && residentBuilds["visibility-resident"] == 1 && visibilityUploads.Creates == 0
    && visibilityUploads.Updates == 0 && visibilityUploads.DynamicBytes == 0 && visibilityUploads.StagingBytes == 0,
    "offscreen resident geometry survives 200 frames and returns with identical pixels and zero uploads");
Capture("visibility-dynamic-hidden", c => Layer(c, "visibility-dynamic", Matrix4x4.CreateTranslation(-4, 0, .3f), Blue));
Check(renderer.LastFrameMetrics.DrawCalls == 0, "offscreen dynamic geometry submits no GPU draws");
var dynamicOffsets = Capture("culled-dynamic-offsets", c =>
{
    c.BeginLayer("culled-mixed-stream", new(false, 1, default) { Scene3D = new(Matrix4x4.CreateTranslation(4, 0, .3f)) });
    var draw = c.GetWindowDrawList();
    draw.Append(new UiDrawVertex[] { new(new(20, 20), default, Red), new(new(40, 20), default, Red), new(new(20, 40), default, Red) },
        new uint[] { 0, 0, 0 }, new UiDrawCommand[] { new(rect, white, 0, 3, 0, Bounds: rect, HasBounds: true) });
    draw.AddRectFilledRounded(new(20, 20, 30, 30), Red, white, 5, draw.GetClipRect());
    draw.AddCircleFilled(new(35, 35), 10, Red, white);
    c.EndLayer();
    c.BeginLayer("visible-mixed-stream", new(false, 1, default) { Scene3D = new(front) });
    draw = c.GetWindowDrawList();
    draw.AddTriangleFilled(new(width * .2f, height * .2f), new(width * .6f, height * .2f), new(width * .2f, height * .6f), Blue);
    draw.AddRectFilledRounded(new(width * .65f, height * .65f, width * .25f, height * .25f), Green, white, 5, draw.GetClipRect());
    c.EndLayer();
});
Check(ColorAt(dynamicOffsets, .3f, .3f, Blue) && ColorAt(dynamicOffsets, .75f, .75f, Green)
    && renderer.LastFrameMetrics.CulledLayerLists == 1 && renderer.LastFrameMetrics.LayerTransformPushes == 1,
    "culled dynamic lists preserve vertex, index, rectangle and circle offsets for the next visible list");
var translatedBounds = Capture("translated-layer-bounds", c =>
{
    if (c.BeginLayer("translated-bounds", new(false, 1, new(width * .5f, 0)) { Scene3D = new(front), ContentVersion = 0 }))
        c.GetWindowDrawList().AddRectFilled(new(-width * .35f, height * .25f, width * .2f, height * .5f), Blue, white);
    c.EndLayer();
});
Check(ColorAt(translatedBounds, .25f, .5f, Blue), "whole-layer visibility includes per-command translation before projection");
Capture("unknown-command-bounds", c =>
{
    c.BeginLayer("unknown-bounds", new(false, 1, default) { Scene3D = new(Matrix4x4.CreateTranslation(4, 0, .3f)) });
    c.GetWindowDrawList().Append(new UiDrawVertex[] { new(new(20, 20), default, Red), new(new(40, 20), default, Red), new(new(20, 40), default, Red) },
        new uint[] { 0, 1, 2 }, new UiDrawCommand[] { new(rect, white, 0, 3, 0) });
    c.EndLayer();
});
Check(renderer.LastFrameMetrics.CulledLayerLists == 0 && renderer.LastFrameMetrics.DrawCalls == 1,
    "unknown command bounds preserve conservative GPU submission");
var visibilityEdge = Capture("visibility-edge", c => Layer(c, "visibility-edge", front, Green,
    new(-width * .25f, height * .25f, width * .5f, height * .5f)));
Check(ColorAt(visibilityEdge, .1f, .5f, Green) && renderer.LastFrameMetrics.DrawCalls > 0,
    "partly visible bounds crossing the frustum edge are retained");
var widgetLeft = new UiRect(width * .0625f, height / 12f, width * .375f, height * 5f / 6f);
var widgetRight = new UiRect(width * .5625f, height / 12f, width * .375f, height * 5f / 6f);
var widgetThenWorld = Capture("widget-then-global-depth", c =>
{
    Resident(c, "scope-widget-first", new(front) { Viewport = widgetLeft }, widgetLeft, Red);
    Resident(c, "scope-global-last", new(back), rect, Blue);
});
Check(ColorAt(widgetThenWorld, .25f, .5f, Blue) && ColorAt(widgetThenWorld, .75f, .5f, Blue),
    "global scope submitted after a widget has independent depth across the full display");
void WidgetScene(UiImmediateContext c, float zoom, long revision = 0)
{
    Resident(c, "resident-world", new(front), rect, Red);
    Resident(c, "resident-left", new(Matrix4x4.CreateScale(zoom, zoom, 1) * back) { Viewport = widgetLeft },
        widgetLeft, revision == 0 ? Blue : Green, revision);
    Resident(c, "resident-right", new(back) { Viewport = widgetRight }, widgetRight, Green);
}
var widgetsA = Capture("widgets-a", c => WidgetScene(c, 1));
Check(ColorAt(widgetsA, .25f, .5f, Blue) && ColorAt(widgetsA, .75f, .5f, Green),
    "widget viewports isolate their depth from the nearer global scene");
var widgetsB = Capture("widgets-b", c => WidgetScene(c, .5f));
Check(ColorAt(widgetsB, .25f, .5f, Blue) && ColorAt(widgetsB, .1f, .5f, Red)
    && ColorAt(widgetsB, .75f, .5f, Green), "widget zoom changes only its own projected content");
var outsideUnchanged = true;
for (var y = 0; y < height; y++)
for (var x = 0; x < width; x++)
{
    if (x >= widgetLeft.X && x < widgetLeft.X + widgetLeft.Width && y >= widgetLeft.Y && y < widgetLeft.Y + widgetLeft.Height) continue;
    var offset = (y * width + x) * 4;
    if (!widgetsA.AsSpan(offset, 4).SequenceEqual(widgetsB.AsSpan(offset, 4))) outsideUnchanged = false;
}
Check(outsideUnchanged, "all pixels outside the zoomed widget remain identical");
var cameraUploads = renderer.ValidationUploads;
Check(residentBuilds.Values.All(v => v == 1) && cameraUploads.StagingBytes == 0 && cameraUploads.DynamicBytes == 0
    && cameraUploads.Creates == 0 && cameraUploads.Updates == 0, "camera-only widget frame rebuilds no geometry and uploads zero geometry bytes");

// Overview batching keeps stable vector instances together and moves only the
// active instance through its matrix. Compare against individual 3D layers.
var overviewViewport = new UiRect(10, 10, width - 20, height - 20);
void OverviewShape(UiImmediateContext c, int index, float offset)
{
    var draw = c.GetWindowDrawList();
    draw.AddRectFilledRounded(new(20 + index % 4 * 65 + offset, 25 + index / 4 * 45, 38, 28),
        index % 2 == 0 ? Green : Red, white, 5, draw.GetClipRect());
}
void OverviewReference(UiImmediateContext c, float offset)
{
    for (var i = 0; i < 16; i++)
    {
        if (i == 1) continue;
        c.BeginLayer("overview.reference." + i, new(false, 1, default)
        { Scene3D = new(back) { Viewport = overviewViewport } });
        OverviewShape(c, i, 0);
        c.EndLayer();
    }
    c.BeginLayer("overview.reference.active", new(false, 1, default)
    { Scene3D = new(front) { Viewport = overviewViewport } });
    OverviewShape(c, 1, offset);
    c.EndLayer();
}
var overviewBuilds = 0;
void OverviewBatched(UiImmediateContext c, float offset, bool moving)
{
    if (c.BeginLayer("overview.stable", new(false, 1, default)
        { Scene3D = new(back) { Viewport = overviewViewport }, ContentVersion = moving ? 0 : 1 }))
    {
        overviewBuilds++;
        for (var i = 0; i < 16; i++)
        {
            if (i == 1) continue;
            OverviewShape(c, i, 0);
        }
    }
    c.EndLayer();
    if (c.BeginLayer("overview.active", new(false, 1, default)
        { Scene3D = new(Matrix4x4.CreateTranslation(moving ? offset * 2 / overviewViewport.Width : 0, 0, 0) * front)
          { Viewport = overviewViewport }, ContentVersion = moving ? 0 : 1 }))
    {
        overviewBuilds++;
        OverviewShape(c, 1, moving ? 0 : offset);
    }
    c.EndLayer();
}
var compiledLayers = Environment.GetEnvironmentVariable("DUXEL_VK_COMPILED_LAYERS") != "0";
var expectedOverviewDraws = compiledLayers ? 1 : 2;
var overviewReference = Capture("overview-reference", c => OverviewReference(c, 18));
var overviewBatched = Capture("overview-batched", c => OverviewBatched(c, 18, true));
Check(overviewReference.AsSpan().SequenceEqual(overviewBatched) && renderer.LastFrameMetrics.DrawCalls == expectedOverviewDraws,
    "batched analytic overview matches separate vector layers with the exact expected draw count");
var overviewMoved = Capture("overview-moved", c => OverviewBatched(c, 34, true));
Check(overviewBuilds == 2 && renderer.ValidationUploads.StagingBytes == 0 && renderer.ValidationUploads.DynamicBytes == 0,
    "overview instance motion rebuilds and uploads no geometry");
var overviewMovedReference = Capture("overview-moved-reference", c => OverviewReference(c, 34));
Check(overviewMoved.AsSpan().SequenceEqual(overviewMovedReference), "overview matrix motion matches direct vector positions");
var overviewStopped = Capture("overview-stopped", c => OverviewBatched(c, 34, false));
Check(overviewStopped.AsSpan().SequenceEqual(overviewMoved) && overviewBuilds == 4,
    "overview geometry rebake after motion preserves final pixels");
void OverviewEngine(UiImmediateContext c, float offset, float cameraScale = 1)
{
    for (var i = 0; i < 16; i++)
    {
        if (i == 1) continue;
        if (c.BeginLayer("overview.engine." + i, new(false, 1, default)
            { Scene3D = new(Matrix4x4.CreateScale(cameraScale, cameraScale, 1) * back) { Viewport = overviewViewport }, ContentVersion = 0 })) OverviewShape(c, i, 0);
        c.EndLayer();
    }
    if (c.BeginLayer("overview.engine.active", new(false, 1, default)
        { Scene3D = new(Matrix4x4.CreateTranslation(offset * 2 / overviewViewport.Width, 0, 0)
            * Matrix4x4.CreateScale(cameraScale, cameraScale, 1) * front)
          { Viewport = overviewViewport }, ContentVersion = 0 })) OverviewShape(c, 1, 0);
    c.EndLayer();
}
var overviewAutomatic = Capture("overview-engine", c => OverviewEngine(c, 18));
Check(overviewAutomatic.AsSpan().SequenceEqual(overviewReference)
    && renderer.LastFrameMetrics.DrawCalls == expectedOverviewDraws && renderer.LastFrameMetrics.BatchedSourceLists == 15
    && renderer.LastFrameMetrics.RebuiltBatchGroups == 1,
    "engine automatically batches separate resident layers with exact vector pixels and draw count");
var overviewAutomaticMoved = Capture("overview-engine-moved", c => OverviewEngine(c, 34));
Check(overviewAutomaticMoved.AsSpan().SequenceEqual(overviewMovedReference)
    && renderer.LastFrameMetrics.RebuiltBatchGroups == 0
    && renderer.ValidationUploads.StagingBytes == 0 && renderer.ValidationUploads.DynamicBytes == 0,
    "engine batch cache survives object matrix changes without regeneration or uploads");
var overviewCamera = Capture("overview-engine-camera", c => OverviewEngine(c, 34, .65f));
Check(!overviewCamera.AsSpan().SequenceEqual(overviewAutomaticMoved)
    && renderer.LastFrameMetrics.RebuiltBatchGroups == 0
    && renderer.ValidationUploads.StagingBytes == 0 && renderer.ValidationUploads.DynamicBytes == 0,
    "shared camera motion preserves automatically batched geometry with zero repacking and uploads");
var widgetsC = Capture("widgets-content-change", c => WidgetScene(c, .5f, 1));
var changedUploads = renderer.ValidationUploads;
Check(residentBuilds["resident-left"] == 2 && residentBuilds["resident-right"] == 1
    && changedUploads.StagingBytes > 0 && changedUploads.Hits >= 2 && ColorAt(widgetsC, .25f, .5f, Green),
    "only the changed content revision rebuilds and uploads geometry");

void MixedPlanes(UiImmediateContext c, bool resident, float motion = 0, float camera = 1, float rotation = 0)
{
    for (var i = 0; i < 8; i++)
    {
        var model = Matrix4x4.CreateTranslation((i % 4) * .4f - .65f + (i == 3 ? motion : 0),
            (i / 4) * .7f - .35f, .8f - i * .05f);
        var transform = Matrix4x4.CreateRotationZ(i == 3 ? rotation : 0) * model * Matrix4x4.CreateScale(camera, camera, 1);
        if (c.BeginLayer((resident ? "compiled.mixed." : "direct.mixed.") + i,
            new(false, .65f + i * .04f, new(1.25f, -.75f))
            { Scene3D = new(transform, WriteDepth: false), ContentVersion = resident ? 0 : null }))
        {
            var draw = c.GetWindowDrawList();
            draw.PushClipRect(new(width * .4f, height * .35f, width * .18f, height * .25f));
            draw.AddRectFilledRounded(new(width * .4f, height * .35f, width * .2f, height * .3f), Red, white, 7, draw.GetClipRect());
            draw.AddTriangleFilled(new(width * .42f, height * .36f), new(width * .56f, height * .38f), new(width * .44f, height * .55f), Blue);
            draw.AddCircleFilled(new(width * .53f, height * .48f), 8, Green, white);
            draw.AddRect(new(width * .42f, height * .37f, width * .12f, height * .18f), new(0xFFCCFFFF), 5, 1.5f);
            draw.PopClipRect();
        }
        c.EndLayer();
    }
}
var mixedDirect = Capture("compiled-mixed-direct", c => MixedPlanes(c, false));
ui.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new UiTextureId(4), UiTextureFormat.Rgba8Unorm, 1, 1,
    new byte[] { 255, 0, 0, 255 }));
void RefreshStaticMaterial(UiImmediateContext c, bool changed)
{
    if (changed) c.MarkLayerDirty("static-material-first");
    for (var i = 0; i < 2; i++)
    {
        if (c.BeginLayer(i == 0 ? "static-material-first" : "static-material-second",
            new(true, 1, default) { Scene3D = new(i == 0 ? front : back) }))
        {
            var texture = changed && i == 0 ? new UiTextureId(4) : white;
            var x = width * (i == 0 ? .1f : .6f);
            c.GetWindowDrawList().AddImage(texture, new(x, height * .2f), new(x + width * .2f, height * .5f), default, new(1, 1), new(0xFFFFFFFF));
        }
        c.EndLayer();
    }
}
Capture("static-material-before", c => RefreshStaticMaterial(c, false));
var materialChanged = Capture("static-material-after", c => RefreshStaticMaterial(c, true));
Check(ColorAt(materialChanged, .2f, .3f, Red) && ColorAt(materialChanged, .7f, .3f, new(0xFFFFFFFF))
    && renderer.ValidationUploads.StagingBytes == 0,
    "same-size static material refresh separates newly different textures without changing geometry");
var mixedCompiled = Capture("compiled-mixed-resident", c => MixedPlanes(c, true));
Check(mixedDirect.AsSpan().SequenceEqual(mixedCompiled),
    "different matrices, analytic fills and strokes, triangles, local clips, opacity and translation preserve every pixel");
if (compiledLayers) Check(renderer.LastFrameMetrics.CompiledLayerDraws == 1 && renderer.LastFrameMetrics.CompiledLayerCommands >= 32,
    "different model matrices and primitive kinds share one real indexed GPU draw");
for (var i = 0; i < 4; i++) Capture("compiled-mixed-warm", c => MixedPlanes(c, true));
if (compiledLayers) Check(renderer.LastFrameMetrics.CompiledIndexUploadBytes == 0 && renderer.LastFrameMetrics.LayerStateUploadBytes == 0,
    "unchanged compiled geometry and descriptors upload zero bytes after frame-slot warmup");
var mixedMotionDirect = Capture("compiled-mixed-motion-direct", c => MixedPlanes(c, false, .12f, .9f));
var mixedMotion = Capture("compiled-mixed-motion", c => MixedPlanes(c, true, .12f, .9f));
Check(mixedMotionDirect.AsSpan().SequenceEqual(mixedMotion) && !mixedMotion.AsSpan().SequenceEqual(mixedCompiled)
    && renderer.LastFrameMetrics.CompiledIndexUploadBytes == 0 && renderer.ValidationUploads.StagingBytes == 0
    && renderer.ValidationUploads.DynamicBytes == 0,
    "camera and one object motion preserve exact pixels with no index or geometry reupload");
var sharedBasisEnabled = compiledLayers && Environment.GetEnvironmentVariable("DUXEL_VK_SHARED_LAYER_BASIS") != "0";
for (var i = 0; i < 4; i++) Capture("shared-basis-warm", c => MixedPlanes(c, true, .12f, .9f));
if (sharedBasisEnabled) Check(renderer.LastFrameMetrics.SharedBasisLayerDraws == 1,
    "translated planes share their matrix basis in one indexed draw");
var rotatedBasisReference = Capture("shared-basis-rotated-reference", c => MixedPlanes(c, false, .12f, .9f, .14f));
for (var i = 0; i < 4; i++)
{
    var actual = Capture("shared-basis-rotated", c => MixedPlanes(c, true, .12f, .9f, .14f));
    Check(rotatedBasisReference.AsSpan().SequenceEqual(actual) && renderer.LastFrameMetrics.SharedBasisLayerDraws == 0,
        "rotation restores the full matrix stream for every frame slot: " + i);
}
var restoredBasisReference = Capture("shared-basis-restored-reference", c => MixedPlanes(c, false, .12f, .9f));
for (var i = 0; i < 4; i++)
{
    var actual = Capture("shared-basis-restored", c => MixedPlanes(c, true, .12f, .9f));
    Check(restoredBasisReference.AsSpan().SequenceEqual(actual) && (!sharedBasisEnabled || renderer.LastFrameMetrics.SharedBasisLayerDraws == 1),
        "restored shared basis preserves exact pixels for every frame slot: " + i);
}
void TopologyMotion(UiImmediateContext c, int revision)
{
    if (c.BeginLayer("compiled.vertex-motion", new(false, 1, default) { Scene3D = new(front), ContentVersion = revision }))
        c.GetWindowDrawList().AddTriangleFilled(new(20 + revision * 8, 30), new(90, 30), new(45, 100), Green);
    c.EndLayer();
}
void CameraPlanes(UiImmediateContext c, bool resident, float pan = 0, float modelMotion = 0, bool rotateLast = false, bool reverse = false)
{
    var camera = Matrix4x4.CreateScale(.5f, .5f, 1) * Matrix4x4.CreateTranslation(pan, .0625f, .375f);
    for (var ordinal = 0; ordinal < 8; ordinal++)
    {
        var i = reverse ? 7 - ordinal : ordinal;
        var model = new UiLayerPlaneModel(new(i == 0 ? modelMotion : 0, 0), i * .015625f);
        var scene = new UiLayer3D(camera) { PlaneModel = model };
        if (rotateLast && i == 7)
            scene = scene with { Transform = Matrix4x4.CreateRotationZ(.125f) * scene.GetEffectiveTransform(rect), PlaneModel = null };
        if (!resident) scene = scene with { Transform = scene.GetEffectiveTransform(rect), PlaneModel = null };
        if (c.BeginLayer((resident ? "camera.planes." : "camera.reference.") + i,
            new(false, 1, default) { Scene3D = scene, ContentVersion = resident ? 0 : null }))
        {
            var draw = c.GetWindowDrawList();
            var x = 10 + (i % 4) * 65f; var y = 10 + (i / 4) * 90f;
            draw.PushClipRect(new(x, y, 48, 58));
            draw.AddRectFilledRounded(new(x, y, 48, 58), Blue, white, 4, draw.GetClipRect());
            draw.AddTriangleFilled(new(x + 3, y + 4), new(x + 36, y + 8), new(x + 7, y + 45), Green);
            draw.PopClipRect();
        }
        c.EndLayer();
    }
}
var planeDirect = Capture("camera-model-reference", c => CameraPlanes(c, false));
var planeResident = Capture("camera-model-resident", c => CameraPlanes(c, true));
Check(planeDirect.AsSpan().SequenceEqual(planeResident), "separate camera and plane models preserve reference pixels");
var sharedCameraEnabled = compiledLayers && Environment.GetEnvironmentVariable("DUXEL_VK_SHARED_LAYER_CAMERA") != "0";
var descriptorReuseEnabled = compiledLayers && Environment.GetEnvironmentVariable("DUXEL_VK_DESCRIPTOR_REUSE") != "0";
var commandSpansEnabled = descriptorReuseEnabled && Environment.GetEnvironmentVariable("DUXEL_VK_COMMAND_SPANS") != "0";
var layerBoundsReuseEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_LAYER_BOUNDS_REUSE") != "0";
Check(VulkanRendererBackend.ValidationCommandSpanProfilesMatch(), "compiled span profiling preserves all kind transitions across every split and empty command");
if (sharedCameraEnabled) Check(renderer.LastFrameMetrics.SharedCameraLayerDraws == 1,
    "eight independent plane depths use one shared camera draw");
for (var i = 0; i < 4; i++) Capture("camera-model-warm", c => CameraPlanes(c, true));
for (var i = 1; i <= 4; i++)
{
    var movingCamera = Capture("camera-model-pan", c => CameraPlanes(c, true, i * .03125f));
    if (sharedCameraEnabled) Check(renderer.LastFrameMetrics.LayerStateUploadBytes == 0
        && renderer.LastFrameMetrics.CompiledIndexUploadBytes == 0 && renderer.ValidationUploads.StagingBytes == 0
        && renderer.ValidationUploads.DynamicBytes == 0 && renderer.LastFrameMetrics.LayerTransformPushes == 1
        && !movingCamera.AsSpan().SequenceEqual(planeResident),
        "camera-only motion uploads zero per-plane state or geometry for slot " + i);
    if (descriptorReuseEnabled) Check(renderer.LastFrameMetrics.CompiledDescriptorBuilds == 0,
        "camera-only motion reuses every command descriptor for slot " + i);
    if (layerBoundsReuseEnabled) Check(renderer.LastFrameMetrics.LayerBoundsCommandVisits == 0,
        "camera-only motion reuses aggregate command bounds for slot " + i);
    if (commandSpansEnabled) Check(renderer.LastFrameMetrics.CompiledCommandVisits == 0
        && renderer.LastFrameMetrics.CompiledLayerCommands > 0,
        "fully visible warmed camera planes skip CPU command visits for slot " + i);
}
var unknownCommands = Build(c => CameraPlanes(c, true, .125f));
var unknownLists = UiPooledList<UiDrawList>.FromArray(unknownCommands.DrawLists.Select(list => list with { CommandStateStamp = 0 }).ToArray());
try
{
    renderer.RenderAndReadback(unknownCommands with { DrawLists = unknownLists });
    Check(renderer.LastFrameMetrics.LayerBoundsCommandVisits > 0,
        "unknown command stamps recompute bounds rather than retaining an earlier range");
    if (compiledLayers) Check(renderer.LastFrameMetrics.CompiledDescriptorBuilds > 0,
        "commands without an explicit state stamp remain validated on every frame");
}
finally { unknownLists.Return(); unknownCommands.ReleasePooled(); }
for (var i = 0; i < 4; i++) Capture("camera-model-motion-warm", c => CameraPlanes(c, true, .125f));
Capture("camera-model-one-moving", c => CameraPlanes(c, true, .125f, 16));
if (sharedCameraEnabled) Check(renderer.LastFrameMetrics.LayerStateUploadBytes == 16
    && renderer.LastFrameMetrics.CompiledIndexUploadBytes == 0 && renderer.ValidationUploads.StagingBytes == 0,
    "one moving plane uploads only its 16-byte model, with no geometry or index upload");
var generalCameraReference = Capture("camera-model-general-reference", c => CameraPlanes(c, false, .125f, 16, true));
for (var i = 0; i < 4; i++)
{
    var actual = Capture("camera-model-general", c => CameraPlanes(c, true, .125f, 16, true));
    Check(generalCameraReference.AsSpan().SequenceEqual(actual) && renderer.LastFrameMetrics.SharedCameraLayerDraws == 0,
        "mixed general 3D transforms restore full matrices for slot " + i);
}
var restoredCameraReference = Capture("camera-model-restored-reference", c => CameraPlanes(c, false, .125f, 16));
for (var i = 0; i < 4; i++)
{
    var actual = Capture("camera-model-restored", c => CameraPlanes(c, true, .125f, 16));
    Check(restoredCameraReference.AsSpan().SequenceEqual(actual)
        && (!sharedCameraEnabled || renderer.LastFrameMetrics.SharedCameraLayerDraws == 1),
        "restored plane model stream preserves exact pixels for slot " + i);
}
var topologyA = Capture("compiled-topology-before", c => TopologyMotion(c, 0));
var replayApiBuilds = 0;
var replayApiHits = 0;
void ReplayApi(UiImmediateContext c, string id, bool direct, long version, float pan, UiColor color)
{
    var options = new UiLayerOptions(false, .75f, default)
    { ContentVersion = version, Scene3D = new(Matrix4x4.CreateTranslation(pan, 0, .2f)) };
    if (direct && c.TryReplayLayer(id, options)) { replayApiHits++; return; }
    if (c.BeginLayer(id, options))
    {
        replayApiBuilds++;
        c.GetWindowDrawList().AddCircleFilled(new(70, 60), 20, color, white);
    }
    c.EndLayer();
}
var scopedReplay = Capture("replay-api-scoped", c => ReplayApi(c, "replay.api.scoped", false, 0, .125f, Red));
Capture("replay-api-initial", c => ReplayApi(c, "replay.api.direct", true, 0, 0, Red));
var replayBuildsBefore = replayApiBuilds;
var directReplay = Capture("replay-api-direct", c => ReplayApi(c, "replay.api.direct", true, 0, .125f, Red));
Check(directReplay.AsSpan().SequenceEqual(scopedReplay) && replayApiBuilds == replayBuildsBefore && replayApiHits == 1,
    "direct retained replay preserves camera and opacity pixels without a BeginLayer/EndLayer scope");
var replayFrameA = Build(c => ReplayApi(c, "replay.api.direct", true, 0, .0625f, Red));
var replaySourceA = replayFrameA.DrawLists.First(list => list.Scene3D.HasValue);
var replaySceneA = replaySourceA.Scene3D;
var replayFrameB = Build(c => ReplayApi(c, "replay.api.direct", true, 0, .1875f, Red));
var replaySourceB = replayFrameB.DrawLists.First(list => list.Scene3D.HasValue);
Check(replaySourceA.Scene3D == replaySceneA && replaySourceA.Scene3D != replaySourceB.Scene3D
    && !ReferenceEquals(replaySourceA, replaySourceB) && ReferenceEquals(replaySourceA.Vertices, replaySourceB.Vertices),
    "overlapping direct replay frames retain separate camera wrappers and share immutable geometry");
replayFrameB.ReleasePooled(); replayFrameA.ReleasePooled();
var revisedScopedReplay = Capture("replay-api-revision-reference", c => ReplayApi(c, "replay.api.scoped", false, 1, .125f, Green));
var revisedDirectReplay = Capture("replay-api-revision", c => ReplayApi(c, "replay.api.direct", true, 1, .125f, Green));
Check(revisedDirectReplay.AsSpan().SequenceEqual(revisedScopedReplay) && replayApiBuilds == replayBuildsBefore + 2,
    "direct replay misses on a changed content version and normal scopes rebuild the current geometry");
Capture("replay-api-dirty", c => { c.MarkLayerDirty("replay.api.direct"); ReplayApi(c, "replay.api.direct", true, 1, .125f, Blue); });
Check(replayApiBuilds == replayBuildsBefore + 3, "dirty retained content cannot be replayed through the direct API");
Capture("replay-api-empty", c => { if (c.BeginLayer("replay.api.empty", new(false, 1, default) { ContentVersion = 0 })) { } c.EndLayer(); });
Capture("replay-api-empty-hit", c =>
{
    Check(c.TryReplayLayer("replay.api.empty", new(false, 1, default) { ContentVersion = 0 }),
        "direct replay accepts a retained empty layer without manufacturing geometry");
    var invalidReplayRejected = false;
    try { c.TryReplayLayer("replay.api.empty", new(false, 1, default) { ContentVersion = 0, Scene3D = new(Matrix4x4.CreateScale(float.NaN)) }); }
    catch (ArgumentException) { invalidReplayRejected = true; }
    Check(invalidReplayRejected, "direct replay validates a changed camera even when the retained layer is empty");
    Check(!c.TryReplayLayer("replay.api.empty", new(false, 1, default)), "dynamic layers without retained content versions keep normal scopes");
    c.RemoveLayer("replay.api.direct");
    Check(!c.TryReplayLayer("replay.api.direct", new(false, 1, default) { ContentVersion = 1 }),
        "removed retained layers cannot be replayed through stale cache entries");
});
void NestedReplay(UiImmediateContext c, bool direct)
{
    c.BeginLayer("replay.api.parent", new(false, .5f, new(3, 4)));
    var child = new UiLayerOptions(true, .75f, new(10, 5));
    if (!direct || !c.TryReplayLayer("replay.api.static-child", child))
    {
        if (c.BeginLayer("replay.api.static-child", child)) c.GetWindowDrawList().AddCircleFilled(new(60, 40), 15, Blue, white);
        c.EndLayer();
    }
    c.EndLayer();
}
var nestedScopedReplay = Capture("replay-api-nested-scoped", c => NestedReplay(c, false));
var nestedDirectReplay = Capture("replay-api-nested-direct", c => NestedReplay(c, true));
Check(nestedDirectReplay.AsSpan().SequenceEqual(nestedScopedReplay),
    "nested direct static replay preserves parent and child opacity and translation");
var revisionFrameA = Build(c => ReplayApi(c, "replay.api.version-lifetime", true, 0, .0625f, Red));
var revisionSourceA = revisionFrameA.DrawLists.First(list => list.Scene3D.HasValue);
var revisionCirclesA = revisionSourceA.RectFilledPrimitives!.AsSpan().ToArray();
var revisionCommandsA = revisionSourceA.Commands.AsSpan().ToArray();
var revisionFrameB = Build(c => ReplayApi(c, "replay.api.version-lifetime", true, 1, .1875f, Green));
try
{
    Check(revisionSourceA.RectFilledPrimitives!.Count == revisionCirclesA.Length
        && revisionSourceA.RectFilledPrimitives.AsSpan().SequenceEqual(revisionCirclesA)
        && revisionSourceA.Commands.AsSpan().SequenceEqual(revisionCommandsA),
        "retained content revision preserves the older borrowed frame's geometry and commands");
    var oldPixels = renderer.RenderAndReadback(revisionFrameA);
    var newPixels = renderer.RenderAndReadback(revisionFrameB);
    var oldReference = Capture("lifetime-version-old-reference", c => ReplayApi(c, "lifetime.old-reference", true, 0, .0625f, Red));
    var newReference = Capture("lifetime-version-new-reference", c => ReplayApi(c, "lifetime.new-reference", true, 1, .1875f, Green));
    Check(oldPixels.AsSpan().SequenceEqual(oldReference) && newPixels.AsSpan().SequenceEqual(newReference),
        "overlapping content versions retain exact independent GPU output");
    revisionFrameA.ReleasePooled();
    Check(revisionSourceA.RectFilledPrimitives.Count == 0
        && revisionFrameB.DrawLists.First(list => list.Scene3D.HasValue).RectFilledPrimitives!.Count > 0,
        "retired geometry drains after its last reader while a newer frame remains borrowed");
}
finally { revisionFrameB.ReleasePooled(); revisionFrameA.ReleasePooled(); }
void LifetimeContent(UiImmediateContext c, string id, UiColor color, bool staticCache, float opacity = 1, float pan = 0)
{
    var options = new UiLayerOptions(staticCache, opacity, default)
    { ContentVersion = staticCache ? null : 0, Scene3D = new(Matrix4x4.CreateTranslation(pan, 0, .2f)) };
    if (c.TryReplayLayer(id, options)) return;
    if (c.BeginLayer(id, options)) c.GetWindowDrawList().AddCircleFilled(new(70, 60), 20, color, white);
    c.EndLayer();
}
var staticLifetimeA = Build(c => LifetimeContent(c, "lifetime.static", Red, true));
var staticLifetimeSourceA = staticLifetimeA.DrawLists.First(list => list.Scene3D.HasValue);
var staticLifetimeCirclesA = staticLifetimeSourceA.RectFilledPrimitives!.AsSpan().ToArray();
var staticLifetimeB = Build(c => { c.MarkLayerDirty("lifetime.static"); LifetimeContent(c, "lifetime.static", Green, true); });
try
{
    Check(staticLifetimeSourceA.RectFilledPrimitives!.AsSpan().SequenceEqual(staticLifetimeCirclesA)
        && !ReferenceEquals(staticLifetimeSourceA.RectFilledPrimitives, staticLifetimeB.DrawLists.First(list => list.Scene3D.HasValue).RectFilledPrimitives),
        "dirty static cache uses new geometry while an earlier frame still references its source");
}
finally { staticLifetimeB.ReleasePooled(); staticLifetimeA.ReleasePooled(); }
var opacityLifetimeA = Build(c => LifetimeContent(c, "lifetime.opacity", Blue, false, .75f));
var opacityLifetimeSourceA = opacityLifetimeA.DrawLists.First(list => list.Scene3D.HasValue);
var opacityLifetimeCommandsA = opacityLifetimeSourceA.Commands.AsSpan().ToArray();
var opacityLifetimeB = Build(c => LifetimeContent(c, "lifetime.opacity", Blue, false, .25f));
try
{
    Check(opacityLifetimeSourceA.Commands.AsSpan().SequenceEqual(opacityLifetimeCommandsA)
        && !ReferenceEquals(opacityLifetimeSourceA.Commands, opacityLifetimeB.DrawLists.First(list => list.Scene3D.HasValue).Commands),
        "opacity changes copy command state instead of mutating an earlier borrowed frame");
}
finally { opacityLifetimeB.ReleasePooled(); opacityLifetimeA.ReleasePooled(); }
Capture("lifetime-scaled-warm", c => LifetimeContent(c, "lifetime.scaled", Red, false));
ui.QueueTextureUpdate(new(UiTextureUpdateKind.Update, white, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 }));
var scaledLifetimeOriginal = Build(c => LifetimeContent(c, "lifetime.scaled", Red, false, pan: .0625f));
var scaledLifetimeSource = scaledLifetimeOriginal.DrawLists.First(list => list.Scene3D.HasValue);
var scaledLifetimeCopy = scaledLifetimeOriginal.ScaleClipRects(new(1, 1));
Check(scaledLifetimeCopy.DrawDataFrameId == scaledLifetimeOriginal.DrawDataFrameId && scaledLifetimeCopy.DrawDataFrameId > 0,
    "independently owned clip copies preserve their originating UI draw-data identity");
var scaledLifetimeSurvivor = scaledLifetimeOriginal.ScaleClipRects(new(1, 1));
var scaledLifetimeNext = Build(c => { c.MarkLayerDirty("lifetime.scaled"); LifetimeContent(c, "lifetime.scaled", Green, false); });
try
{
    scaledLifetimeCopy.ReleasePooled();
    Check(scaledLifetimeSource.RectFilledPrimitives!.Count > 0 && scaledLifetimeOriginal.TextureUpdates.Count == 1
        && scaledLifetimeOriginal.FrameSceneLeases is { Count: > 0 } && scaledLifetimeOriginal.FrameSceneLeases[0].InUse,
        "disposing a scaled deep copy does not release the original frame lease or texture-update container");
    scaledLifetimeOriginal.ReleasePooled();
    var scaledSurvivorImage = renderer.RenderAndReadback(scaledLifetimeSurvivor);
    var scaledSurvivorReference = Capture("lifetime-scaled-reference", c => LifetimeContent(c, "lifetime.scaled-reference", Red, false, pan: .0625f));
    Check(scaledSurvivorImage.AsSpan().SequenceEqual(scaledSurvivorReference),
        "scaled geometry and texture updates remain independent after the original frame is released");
}
finally { scaledLifetimeSurvivor.ReleasePooled(); scaledLifetimeCopy.ReleasePooled(); scaledLifetimeNext.ReleasePooled(); scaledLifetimeOriginal.ReleasePooled(); }
var emptyCommandSource = new UiDrawList(UiPooledList<UiDrawVertex>.RentAndCopy((ReadOnlySpan<UiDrawVertex>)[vertex]),
    UiPooledList<uint>.RentAndCopy(ReadOnlySpan<uint>.Empty), UiPooledList<UiDrawCommand>.RentAndCopy(ReadOnlySpan<UiDrawCommand>.Empty));
var emptyCommandScaled = emptyCommandSource.ScaleClipRects(new(1, 1));
emptyCommandScaled.ReleasePooled();
Check(emptyCommandSource.Vertices.Count == 1 && !ReferenceEquals(emptyCommandSource, emptyCommandScaled),
    "scaling a command-empty draw list still owns independent geometry containers");
emptyCommandSource.ReleasePooled();
var emptyDrawData = new UiDrawData(new(width, height), default, new(1, 1), 0, 0,
    UiPooledList<UiDrawList>.RentAndCopy(ReadOnlySpan<UiDrawList>.Empty),
    UiPooledList<UiTextureUpdate>.RentAndCopy((ReadOnlySpan<UiTextureUpdate>)[new(UiTextureUpdateKind.Update, white,
        UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 })]));
var emptyDrawDataScaled = emptyDrawData.ScaleClipRects(new(1, 1));
emptyDrawDataScaled.ReleasePooled();
Check(!ReferenceEquals(emptyDrawData, emptyDrawDataScaled) && emptyDrawData.TextureUpdates.Count == 1,
    "scaling a draw-list-empty frame does not share ownership of its texture updates");
emptyDrawData.ReleasePooled();
using (var lifetimeUi = new UiContext(atlas, new(1), white))
{
    void PrepareLifetimeFrame()
    {
        lifetimeUi.NewFrame(new(1f / 60, new(width, height), new(1, 1)), input, rect, UiTextSettings.Default);
        lifetimeUi.Render(new ActionScreen(c => LifetimeContent(c, "lifetime.context", Red, false)));
    }
    PrepareLifetimeFrame();
    var claimed = lifetimeUi.GetDrawData();
    var lifetime = claimed.LayerFrameLease!.Value.Owner;
    var ownedSource = claimed.DrawLists.First(list => list.Scene3D.HasValue);
    claimed.ReleasePooled();
    PrepareLifetimeFrame();
    Check(lifetime.State.Frames == 1, "unclaimed draw data owns one retained-source frame lease");
    lifetimeUi.NewFrame(new(1f / 60, new(width, height), new(1, 1)), input, rect, UiTextSettings.Default);
    Check(lifetime.State.Frames == 0, "starting a frame releases unclaimed draw data");
    try
    {
        lifetimeUi.Render(new ActionScreen(c => { LifetimeContent(c, "lifetime.context", Red, false); throw new InvalidOperationException("expected abandoned render"); }));
    }
    catch (InvalidOperationException error) when (error.Message == "expected abandoned render") { }
    lifetimeUi.EndFrame();
    Check(lifetime.State.Frames == 0, "ending an abandoned render returns its pending source lease");
    PrepareLifetimeFrame();
    var surviving = lifetimeUi.GetDrawData();
    lifetimeUi.Dispose();
    Check(ownedSource.RectFilledPrimitives!.Count > 0 && lifetime.State.Frames == 1,
        "disposing the UI context defers geometry still owned by a claimed frame");
    surviving.ReleasePooled();
    Check(ownedSource.RectFilledPrimitives.Count == 0 && lifetime.State == (0, 0),
        "last claimed frame release drains retired sources after context disposal");
}
void BoundsRevision(UiImmediateContext c, bool resident, bool visible)
{
    if (c.BeginLayer(resident ? "bounds.revision" : "bounds.reference", new(false, 1, default)
        { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .2f)), ContentVersion = resident ? (visible ? 1 : 0) : null }))
        c.GetWindowDrawList().AddCircleFilled(new(visible ? 70 : width * 4 + 70, 60), 20, Red, white);
    c.EndLayer();
}
for (var i = 0; i < 4; i++) Capture("bounds-hidden-warm", c => BoundsRevision(c, true, false));
if (layerBoundsReuseEnabled) Check(renderer.LastFrameMetrics.LayerBoundsCommandVisits == 0
    && renderer.LastFrameMetrics.CulledLayerLists == 1, "fully hidden resident geometry also retains aggregate bounds");
var boundsReference = Capture("bounds-visible-reference", c => BoundsRevision(c, false, true));
var boundsUpdated = Capture("bounds-visible-update", c => BoundsRevision(c, true, true));
Check(boundsUpdated.AsSpan().SequenceEqual(boundsReference) && renderer.LastFrameMetrics.LayerBoundsCommandVisits > 0,
    "geometry moving from outside to inside invalidates bounds before GPU culling");
for (var i = 0; i < 4; i++) Capture("bounds-order-warm", c => CameraPlanes(c, true));
var boundsOrderReference = Capture("bounds-order-reference", c => CameraPlanes(c, false, reverse: true));
for (var i = 0; i < 4; i++) Capture("bounds-order-rewarm", c => CameraPlanes(c, true));
var boundsOrderReversed = Capture("bounds-order-reversed", c => CameraPlanes(c, true, reverse: true));
Check(boundsOrderReference.AsSpan().SequenceEqual(boundsOrderReversed)
    && renderer.LastFrameMetrics.LayerBoundsCommandVisits > 0,
    "reordered resident sources do not inherit another draw-list position's bounds");
var boundsCommand = default(UiDrawCommand) with { ElementCount = 3, HasBounds = true, Bounds = new(1, 2, 3, 4) };
Check(UiDrawList.ComputeCommandStateStamp([boundsCommand]) != UiDrawList.ComputeCommandStateStamp([boundsCommand with { Bounds = new(5, 2, 3, 4) }])
    && UiDrawList.ComputeCommandStateStamp([boundsCommand]) != UiDrawList.ComputeCommandStateStamp([boundsCommand with { HasBounds = false }]),
    "command state identity includes bounds coordinates and whether bounds are known");
void HiddenCommandState(UiImmediateContext c, bool resident, float opacity, bool reveal)
{
    var transform = Matrix4x4.CreateTranslation(reveal ? -8f : 0, 0, .2f);
    if (c.BeginLayer(resident ? "descriptor.hidden-state" : "descriptor.hidden-reference",
        new(false, opacity, default) { Scene3D = new(transform), ContentVersion = resident ? 0 : null }))
    {
        var draw = c.GetWindowDrawList();
        draw.AddRectFilledRounded(new(20, 20, 30, 30), Blue, white, 3, draw.GetClipRect());
        draw.AddCircleFilled(new(width * 4 + 50, 40), 15, Green, white);
    }
    c.EndLayer();
}
for (var i = 0; i < 4; i++) Capture("descriptor-hidden-initial", c => HiddenCommandState(c, true, 1, false));
for (var i = 0; i < 4; i++) Capture("descriptor-hidden-opacity", c => HiddenCommandState(c, true, .5f, false));
var hiddenStateReference = Capture("descriptor-hidden-reference", c => HiddenCommandState(c, false, .5f, true));
var hiddenStateRevealed = Capture("descriptor-hidden-revealed", c => HiddenCommandState(c, true, .5f, true));
Check(hiddenStateRevealed.AsSpan().SequenceEqual(hiddenStateReference),
    "a command changed while hidden retains correct opacity when the camera reveals it");
if (descriptorReuseEnabled) Check(renderer.LastFrameMetrics.CompiledDescriptorBuilds == 0,
    "hidden command state was populated before its source cache became current");
byte[] DescriptorContextFrame(bool resident, bool hideSecond, float displayOffset)
{
    var frameData = Build(c =>
    {
        for (var i = 0; i < 2; i++)
        {
            var transform = Matrix4x4.CreateTranslation(i == 1 && hideSecond ? 8 : 0, 0, i == 0 ? .2f : .7f);
            var scene = new UiLayer3D(transform) { Viewport = rect };
            if (c.BeginLayer((resident ? "descriptor.context." : "descriptor.context.reference.") + i,
                new(false, 1, default) { Scene3D = scene, ContentVersion = resident ? 0 : null }))
                c.GetWindowDrawList().AddRectFilledRounded(new(40 + i * 100, 30, 40, 40),
                    i == 0 ? Blue : Green, white, 3, c.GetWindowDrawList().GetClipRect());
            c.EndLayer();
        }
    });
    try { return renderer.RenderAndReadback(frameData with { DisplayPos = new(displayOffset, 0) }); }
    finally { frameData.ReleasePooled(); }
}
for (var i = 0; i < 4; i++) DescriptorContextFrame(true, false, 0);
for (var i = 0; i < 4; i++) DescriptorContextFrame(true, true, 20);
var changedContextReference = DescriptorContextFrame(false, false, 20);
var changedContextActual = DescriptorContextFrame(true, false, 20);
Check(changedContextActual.AsSpan().SequenceEqual(changedContextReference),
    "display-context changes invalidate descriptors of hidden members before they return");
if (descriptorReuseEnabled) Check(renderer.LastFrameMetrics.CompiledDescriptorBuilds == 1,
    "only the returning member rebuilds descriptors after the context invalidation");
void PlaneWidget(UiImmediateContext c, string id, UiRect viewport, float zoom, bool resident)
{
    var scene = new UiLayer3D(Matrix4x4.CreateScale(zoom, zoom, 1) * back)
    { Viewport = viewport, PlaneModel = new(new(20, 10), -.125f) };
    if (!resident) scene = scene with { Transform = scene.GetEffectiveTransform(rect), PlaneModel = null };
    if (c.BeginLayer(id, new(false, 1, default) { Scene3D = scene, ContentVersion = resident ? 0 : null }))
    {
        var draw = c.GetWindowDrawList();
        draw.AddRectFilledRounded(new(viewport.X, viewport.Y, viewport.Width * .75f, viewport.Height * .8f),
            Green, white, 8, draw.GetClipRect());
    }
    c.EndLayer();
}
var planeWidgetsReference = Capture("camera-model-widgets-reference", c =>
{
    PlaneWidget(c, "widget.reference.left", widgetLeft, .5f, false);
    PlaneWidget(c, "widget.reference.right", widgetRight, .75f, false);
});
var planeWidgets = Capture("camera-model-widgets", c =>
{
    PlaneWidget(c, "widget.camera.left", widgetLeft, .5f, true);
    PlaneWidget(c, "widget.camera.right", widgetRight, .75f, true);
});
Check(planeWidgetsReference.AsSpan().SequenceEqual(planeWidgets),
    "independent widget cameras normalize pixel models and preserve viewport clipping");
var changedPlaneWidget = Capture("camera-model-widget-left-zoom", c =>
{
    PlaneWidget(c, "widget.camera.left", widgetLeft, 1.25f, true);
    PlaneWidget(c, "widget.camera.right", widgetRight, .75f, true);
});
var unchangedRightWidget = true;
for (var y = (int)widgetRight.Y; y < (int)(widgetRight.Y + widgetRight.Height); y++)
    for (var x = (int)widgetRight.X; x < (int)(widgetRight.X + widgetRight.Width); x++)
        unchangedRightWidget &= changedPlaneWidget.AsSpan((y * width + x) * 4, 4)
            .SequenceEqual(planeWidgets.AsSpan((y * width + x) * 4, 4));
Check(unchangedRightWidget && !changedPlaneWidget.AsSpan().SequenceEqual(planeWidgets),
    "changing only the left plane camera preserves every right widget pixel");
var modelPicking = new UiLayer3D(Matrix4x4.CreateScale(.5f, .5f, 1) * back)
{ Viewport = widgetLeft, PlaneModel = new(new(20, 10), -.125f) };
var modelCenter = new UiVector2(widgetLeft.X + widgetLeft.Width * .5f, widgetLeft.Y + widgetLeft.Height * .5f);
Check(modelPicking.TryMapScreenPoint(new(modelCenter.X + 10, modelCenter.Y + 5), rect, out var modelPicked)
    && MathF.Abs(modelPicked.X - modelCenter.X) < .01f && MathF.Abs(modelPicked.Y - modelCenter.Y) < .01f,
    "picking uses the same independent camera, source-pixel translation and plane depth");
var modelClipped = Capture("camera-model-depth-clipped", c =>
{
    c.BeginLayer("camera.depth-clipped", new(false, 1, default)
    { Scene3D = new(front) { PlaneModel = new(default, 1f) }, ContentVersion = 0 });
    c.GetWindowDrawList().AddRectFilled(rect, Green, white);
    c.EndLayer();
});
Check(renderer.LastFrameMetrics.CulledLayerLists == 1 && renderer.LastFrameMetrics.CompiledLayerDraws == 0,
    "plane depth offsets participate in homogeneous frustum culling");
void ReturningModel(UiImmediateContext c, float translation)
{
    if (c.BeginLayer("camera.returning-model", new(false, 1, default)
        { Scene3D = new(front) { PlaneModel = new(new(translation, 0)) }, ContentVersion = 0 }))
        c.GetWindowDrawList().AddRectFilled(new(-width * 4, 20, 40, 40), Green, white);
    c.EndLayer();
}
Capture("camera-model-hidden", c => ReturningModel(c, 0));
var returningModel = Capture("camera-model-returned", c => ReturningModel(c, width * 4 + 20));
Check(ColorAt(returningModel, 30f / width, 30f / height, Green)
    && renderer.ValidationUploads.StagingBytes == 0 && renderer.LastFrameMetrics.CulledLayerLists == 0,
    "pixel model translation returns a hidden retained plane without geometry upload");
void PerspectivePlane(UiImmediateContext c, bool resident)
{
    var camera = Matrix4x4.CreateRotationY(.2f) * Matrix4x4.CreateTranslation(0, 0, -2)
        * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, (float)width / height, .1f, 10f);
    var scene = new UiLayer3D(camera) { PlaneModel = new(new(16, 8), -.015625f) };
    if (!resident) scene = scene with { Transform = scene.GetEffectiveTransform(rect), PlaneModel = null };
    if (c.BeginLayer(resident ? "camera.perspective" : "camera.perspective.reference",
        new(false, 1, default) { Scene3D = scene, ContentVersion = resident ? 0 : null }))
        c.GetWindowDrawList().AddRectFilled(new(width * .3f, height * .3f, width * .4f, height * .4f), Blue, white);
    c.EndLayer();
}
var perspectiveModelReference = Capture("camera-model-perspective-reference", c => PerspectivePlane(c, false));
var perspectiveModel = Capture("camera-model-perspective", c => PerspectivePlane(c, true));
Check(perspectiveModel.AsSpan().SequenceEqual(perspectiveModelReference) && ColorAt(perspectiveModel, .5f, .5f, Blue),
    "shared perspective camera projects translated models with the same reference pixels");
var topologyB = Capture("compiled-topology-motion", c => TopologyMotion(c, 1));
Check(!topologyA.AsSpan().SequenceEqual(topologyB) && renderer.LastFrameMetrics.CompiledIndexUploadBytes == 0
    && renderer.ValidationUploads.StagingBytes > 0,
    "vertex-only content revision uploads changed geometry while reusing compiled index topology");

void ManyPlanes(UiImmediateContext c, bool resident)
{
    for (var i = 0; i < 257; i++)
    {
        var transform = Matrix4x4.CreateTranslation(i % 16 * 18f * 2 / width, i / 16 * 13f * 2 / height, .2f + i * .001f);
        if (c.BeginLayer((resident ? "compiled.many." : "direct.many.") + i,
            new(false, 1, default) { Scene3D = new(transform), ContentVersion = resident ? 0 : null }))
            c.GetWindowDrawList().AddTriangleFilled(new(1, 1), new(13, 2), new(3, 11), i % 2 == 0 ? Green : Red);
        c.EndLayer();
    }
}
var manyDirect = Capture("compiled-many-direct", c => ManyPlanes(c, false));
var manyCompiled = Capture("compiled-many-resident", c => ManyPlanes(c, true));
Check(manyDirect.AsSpan().SequenceEqual(manyCompiled), "257 different planes preserve all pixels across the descriptor group boundary");
if (compiledLayers) Check(renderer.LastFrameMetrics.CompiledLayerDraws == 2 && renderer.LastFrameMetrics.CompiledLayerCommands == 257,
    "257 independent model matrices use two real GPU draws with the highest guaranteed 24-bit index");
if (sharedBasisEnabled) Check(renderer.LastFrameMetrics.LayerTransformPushes == 1,
    "257 planes share one camera basis push across two compiled groups");
var manyThenDirectReference = Capture("many-then-direct-reference", c =>
{
    ManyPlanes(c, false);
    Layer(c, "following-direct-reference", Matrix4x4.CreateScale(.8f, .9f, 1) * front, Blue, new(30, 40, 50, 30));
});
var manyThenDirect = Capture("many-then-direct", c =>
{
    ManyPlanes(c, true);
    Layer(c, "following-direct", Matrix4x4.CreateScale(.8f, .9f, 1) * front, Blue, new(30, 40, 50, 30));
});
Check(manyThenDirect.AsSpan().SequenceEqual(manyThenDirectReference),
    "direct drawing following shared compiled groups restores its own transform and exact pixels");
void ChangingConnectionPrefix(UiImmediateContext c, int revision)
{
    if (c.BeginLayer("changing-connection-prefix", new(false, 1, default) { Scene3D = new(back), ContentVersion = revision }))
    {
        c.GetWindowDrawList().AddRectFilledRounded(new(4, 4, 12, 8), Blue, white, 2, rect);
        if (revision != 0) c.GetWindowDrawList().AddCircleFilled(new(24, 8), 4, Green, white);
    }
    c.EndLayer();
    ManyPlanes(c, true);
}
Capture("connection-prefix-initial", c => ChangingConnectionPrefix(c, 0));
var prefixChanged = Capture("connection-prefix-changed", c => ChangingConnectionPrefix(c, 1));
if (compiledLayers) Check(renderer.LastFrameMetrics.CompiledIndexUploadBytes <= 64,
    "changing preceding connection topology never recompiles the 257 unchanged mesh planes; bytes=" + renderer.LastFrameMetrics.CompiledIndexUploadBytes);
var prefixChangedReference = Capture("connection-prefix-reference", c =>
{
    c.BeginLayer("changing-connection-reference", new(false, 1, default) { Scene3D = new(back) });
    c.GetWindowDrawList().AddRectFilledRounded(new(4, 4, 12, 8), Blue, white, 2, rect);
    c.GetWindowDrawList().AddCircleFilled(new(24, 8), 4, Green, white);
    c.EndLayer();
    ManyPlanes(c, false);
});
Check(prefixChanged.AsSpan().SequenceEqual(prefixChangedReference),
    "stable group boundaries preserve exact pixels after preceding connection changes");
void LongCommandList(UiImmediateContext c, bool resident)
{
    if (c.BeginLayer(resident ? "compiled.long-list" : "direct.long-list",
        new(false, 1, default) { Scene3D = new(front), ContentVersion = resident ? 0 : null }))
    {
        var draw = c.GetWindowDrawList();
        for (var i = 0; i < 400; i++)
        {
            var x = 5 + i % 20 * 14;
            var y = 5 + i / 20 * 11;
            if (i % 2 == 0) draw.AddRectFilledRounded(new(x, y, 9, 8), Blue, white, 2, rect);
            else draw.AddCircleFilled(new(x + 4, y + 4), 4, Green, white);
        }
    }
    c.EndLayer();
}
var longDirect = Capture("compiled-long-list-direct", c => LongCommandList(c, false));
var longCompiled = Capture("compiled-long-list-resident", c => LongCommandList(c, true));
Check(longDirect.AsSpan().SequenceEqual(longCompiled), "one long mixed primitive list preserves every pixel after internal partitioning");
if (compiledLayers) Check(renderer.LastFrameMetrics.CompiledLayerDraws == 2 && renderer.LastFrameMetrics.CompiledLayerCommands == 400,
    "400 alternating shape commands in one source list use two real indexed GPU draws");
var pickingScene = new UiLayer3D(Matrix4x4.CreateScale(.5f, .5f, 1) * back) { Viewport = widgetLeft };
var pickScreen = new UiVector2(widgetLeft.X + widgetLeft.Width * .625f, widgetLeft.Y + widgetLeft.Height * .5f);
Check(pickingScene.TryMapScreenPoint(pickScreen, rect, out var mapped)
    && MathF.Abs(mapped.X - (widgetLeft.X + widgetLeft.Width * .75f)) < .01f
    && MathF.Abs(mapped.Y - pickScreen.Y) < .01f, "widget picking inverse-projects through the same camera");
Check(!pickingScene.TryMapScreenPoint(new(widgetRight.X + 1, widgetRight.Y + 1), rect, out _),
    "widget picking rejects points in another viewport");

ui.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new UiTextureId(1), UiTextureFormat.Rgba8Unorm, 1, 1,
    new byte[] { 255, 0, 0, 0 }));
var textCoverage = Capture("projected-font-coverage", c =>
{
    c.BeginLayer("font-coverage", new(false, 1, default) { Scene3D = new(front, WriteDepth: false) });
    c.GetWindowDrawList().AddImage(new(1), new(width * .2f, height * .2f), new(width * .8f, height * .8f), default, new(1, 1), new(0xFFFFFFFF));
    c.EndLayer();
});
var gray = renderer.ValidationSrgb ? new UiColor(0xFF9C9C9C) : new UiColor(0xFF555555);
Check(ColorAt(textCoverage, .5f, .5f, gray, 3), "projected font coverage stays achromatic instead of stretching RGB subpixels");

UiDrawList? residentOriginal = null;
var retainedOwnership = Build(c =>
{
    if (c.BeginLayer("retained-ownership", new(false, 1, default) { Scene3D = new(front), ContentVersion = 0 }))
    {
        residentOriginal = TransferTriangle(c, new(10, 10), new(20, 10), new(10, 20), Red);
    }
    c.EndLayer();
});
Check(ReferenceEquals(retainedOwnership.DrawLists[0].Vertices, residentOriginal!.Vertices)
    && ReferenceEquals(retainedOwnership.DrawLists[0].Indices, residentOriginal.Indices), "versioned content captures original geometry without copying");
retainedOwnership.ReleasePooled();
Check(residentOriginal.Vertices.Count == 3, "resident buffers survive frame release");
var removed = Build(c => c.RemoveLayer("retained-ownership"));
removed.ReleasePooled();
Check(residentOriginal.Vertices.Count == 0, "removing a layer returns its owned resident buffers");

var emptyBuilds = 0;
for (var emptyFrame = 0; emptyFrame < 2; emptyFrame++)
{
    var empty = Build(c =>
    {
        if (c.BeginLayer("empty-resident", new(false, 1, default) { ContentVersion = 0 })) emptyBuilds++;
        c.EndLayer();
    });
    empty.ReleasePooled();
}
Check(emptyBuilds == 1, "an empty content revision is retained without rebuilding forever");

ui.SetPlatformTextBackend(platform.TextBackend);
TexturePixelOwnerTests.Run(Check, platform.TextBackend!, "C:/Windows/Fonts/segoeui.ttf");
ui.SetDirectTextEnabled(true);
ui.SetDirectTextBaseFontSize(18);
ui.SetDirectTextPrimaryFontPath("C:/Windows/Fonts/segoeui.ttf");
ui.SetTypographyFonts(new("C:/Windows/Fonts/segoeui.ttf","C:/Windows/Fonts/seguisb.ttf","C:/Windows/Fonts/segoeuib.ttf",
    "C:/Windows/Fonts/malgun.ttf","C:/Windows/Fonts/malgunbd.ttf","C:/Windows/Fonts/malgunbd.ttf"));
if(!args.Contains("--legacy-rendering-reference",StringComparer.Ordinal))
{
    DefaultTextRenderingTests.Run(Capture,Check);
    BadgeRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    DividerRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    ButtonRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    ButtonAppearanceTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    ButtonShapeTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    ButtonIconRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    FluentIconRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    CheckboxWidgetTests.Run(Check);
    CheckboxRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    RadioGroupWidgetTests.Run(Check);
    RadioGroupRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    ControlTextBlockRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,size.Width,size.Height);
    SwitchWidgetTests.Run(Check);
    SwitchRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    LinkWidgetTests.Run(Check);
    LinkRenderingTests.Run(renderer,Capture,Check,ui.GetStyle,ui.SetTheme,size.Width,size.Height);
    InputKernelTests.Run(Check);
    FluentInputTests.Run(Check);
    FieldWidgetTests.Run(Check);
    WidgetMeasurementTests.Run(Check);
}
var sharedGlyphMesh = ((IPlatformVectorTextBackend)platform.TextBackend!).CreateTextMesh(
    new("C:/Windows/Fonts/segoeui.ttf", null, "BB", 18));
var sharedGlyphs = sharedGlyphMesh.Glyphs.Span;
Check(sharedGlyphs.Length == 2 && sharedGlyphs[0].Vertices.Equals(sharedGlyphs[1].Vertices)
    && sharedGlyphs[0].Indices.Equals(sharedGlyphs[1].Indices), "repeated vector glyphs share immutable geometry buffers");
var glyphUniqueVertices = sharedGlyphs[0].Vertices.Length;
var glyphTriangleIndices = sharedGlyphs[0].Indices.Length;
var glyphExpandedGpuBytes = glyphTriangleIndices * 24L;
var glyphIndexedGpuBytes = glyphUniqueVertices * 20L + glyphTriangleIndices * 4L;
Check(glyphIndexedGpuBytes < glyphExpandedGpuBytes, "indexed glyph storage reduces bytes without changing tessellation");
Console.WriteLine($"GLYPH vertices={glyphUniqueVertices} indices={glyphTriangleIndices} expandedBytes={glyphExpandedGpuBytes} indexedBytes={glyphIndexedGpuBytes}");
var textBuilds = 0;
// Text can span several flushed draw lists; all must keep their widget viewport.
var textViewport = new UiRect(width * .25f, height * .25f, width * .5f, height * .5f);
var flushedTextLists = 0;
var widgetText = CaptureWithInspection("vector-text-widget-clip", c =>
{
    c.BeginLayer("wide-widget-text", new(false, 1, default)
    {
        Scene3D = new(Matrix4x4.CreateScale(3, 3, 1) * front) { Viewport = textViewport },
        ContentVersion = 0,
    });
    c.SetCursorScreenPos(new(textViewport.X + textViewport.Width * .4f, textViewport.Y + textViewport.Height * .4f));
    c.TextColored(new(0xFFFFFFFF), new string('B', 300));
    c.EndLayer();
}, data => flushedTextLists = data.DrawLists.Count(list => list.Scene3D.HasValue && list.Indices.Count > 0));
var outsideTextPixels = 0;
for (var y = 0; y < height; y++)
for (var x = 0; x < width; x++)
{
    if (x >= textViewport.X && x < textViewport.X + textViewport.Width
        && y >= textViewport.Y && y < textViewport.Y + textViewport.Height) continue;
    if (widgetText[(y * width + x) * 4] > 10) outsideTextPixels++;
}
var visibleTextPixels = ColoredPixels(widgetText);
Check(flushedTextLists > 1 && visibleTextPixels > 20 && outsideTextPixels == 0,
    $"large vector text remains inside its widget viewport after zoom and draw-list flushes (lists={flushedTextLists}, visible={visibleTextPixels}, outside={outsideTextPixels})");

var cardText = Capture("vector-text-card-clip", c =>
{
    c.BeginLayer("card-text-clip", new(false, 1, default)
        { Scene3D = new(Matrix4x4.CreateScale(3, 3, 1) * front) { Viewport = textViewport } });
    var draw = c.GetWindowDrawList();
    draw.PushClipRect(new(textViewport.X + textViewport.Width * .4f, textViewport.Y + textViewport.Height * .4f,
        textViewport.Width * .2f, textViewport.Height * .2f));
    c.SetCursorScreenPos(new(textViewport.X + textViewport.Width * .4f, textViewport.Y + textViewport.Height * .4f));
    c.TextColored(new(0xFFFFFFFF), "LONG FIELD CONTENT THAT MUST STAY INSIDE ITS CARD");
    draw.PopClipRect();
    c.EndLayer();
});
var outsideCardPixels = 0;
for (var y = 0; y < height; y++)
for (var x = 0; x < width; x++)
{
    if (x >= textViewport.X + textViewport.Width * .2f && x < textViewport.X + textViewport.Width * .8f
        && y >= textViewport.Y + textViewport.Height * .2f && y < textViewport.Y + textViewport.Height * .8f) continue;
    if (cardText[(y * width + x) * 4] > 10) outsideCardPixels++;
}
Check(ColoredPixels(cardText) > 20 && outsideCardPixels == 0,
    "vector text obeys the card's local clip as well as the widget viewport");

void ResidentText(UiImmediateContext c)
{
    if (c.BeginLayer("resident-native-text", new(false, 1, default) { Scene3D = new(front), ContentVersion = 0 }))
    {
        textBuilds++;
        c.SetCursorScreenPos(new(width * .2f, height * .4f));
        c.TextColored(new UiColor(0xFFFFFFFF), "Resident camera text");
    }
    c.EndLayer();
}
var nativeTextBefore = Capture("resident-native-text-before", ResidentText);
Check(ColoredPixels(nativeTextBefore) > 40, "native text is visible on a projected plane");
HashSet<UiTextureId> residentTextTextures;
using(var residentFrame=Build(ResidentText))
    residentTextTextures=residentFrame.DrawLists.SelectMany(list=>list.Commands).Select(command=>command.TextureId).ToHashSet();
for (var frame = 0; frame < 650; frame++)
{
    var textFrame = Build(ResidentText);
    try
    {
        if (textFrame.TextureUpdates.Any(t => t.Kind == UiTextureUpdateKind.Destroy&&residentTextTextures.Contains(t.TextureId)))
            throw new InvalidOperationException("A resident text resource was destroyed.");
    }
    finally { textFrame.ReleasePooled(); }
}
var nativeTextAfter = Capture("resident-native-text-after", ResidentText);
Check(textBuilds == 1 && nativeTextBefore.AsSpan().SequenceEqual(nativeTextAfter),
    "resident text survives past the text cache idle limit without rebuilding or rerasterizing");

// A camera zoom must rasterize the vector boundary at the final resolution,
// matching a directly enlarged vector instead of magnifying an old image.
var vectorBuilds = 0;
var vectorScale = 1f;
void ResidentVector(UiImmediateContext c)
{
    if (c.BeginLayer("resident-vector-zoom", new(false, 1, default)
        { ContentVersion = 0, Scene3D = new(Matrix4x4.CreateScale(vectorScale, vectorScale, 1) * front) }))
    {
        vectorBuilds++;
        c.GetWindowDrawList().AddRectFilledRounded(new(width * .45f, height * .45f, width * .1f, height * .1f),
            new(0xFFFFFFFF), white, height * .025f, rect);
    }
    c.EndLayer();
}
Capture("vector-zoom-1x", ResidentVector);
vectorScale = 8;
var zoomedVector = Capture("vector-zoom-8x", ResidentVector);
Check(vectorBuilds == 1 && renderer.ValidationUploads.StagingBytes == 0
    && renderer.ValidationUploads.DynamicBytes == 0,
    "8x vector camera zoom reuses geometry without upload");
var directVector = Capture("vector-direct-8x", c =>
{
    c.BeginLayer("direct-vector-zoom", new(false, 1, default) { Scene3D = new(front) });
    c.GetWindowDrawList().AddRectFilledRounded(new(width * .1f, height * .1f, width * .8f, height * .8f),
        new(0xFFFFFFFF), white, height * .2f, rect);
    c.EndLayer();
});
var vectorDifferenceCount = 0;
for (var pixel = 0; pixel < zoomedVector.Length; pixel += 4)
    if (Math.Abs(zoomedVector[pixel] - directVector[pixel]) > 2) vectorDifferenceCount++;
Check(vectorDifferenceCount <= 8, "8x camera zoom matches a directly enlarged analytic vector boundary");

var entityAFront = false;
var entityBuilds = 0;
void OverlappingEntities(UiImmediateContext c)
{
    if (c.BeginLayer("entity-a", new(false, 1, default)
        { ContentVersion = 0, Scene3D = new(Matrix4x4.CreateTranslation(0, 0, entityAFront ? .2f : .6f)) }))
    {
        entityBuilds++;
        c.GetWindowDrawList().AddRectFilled(new(width * .2f, height * .2f, width * .6f, height * .6f), Red, white);
        c.SetCursorScreenPos(new(width * .3f, height * .3f));
        c.TextColored(new(0xFFFFFFFF), "SELECTED");
    }
    c.EndLayer();
    if (c.BeginLayer("entity-b", new(false, 1, default) { ContentVersion = 0, Scene3D = new(front with { M43 = .3f }) }))
    {
        entityBuilds++;
        c.GetWindowDrawList().AddRectFilled(new(width * .2f, height * .2f, width * .6f, height * .6f), Blue, white);
    }
    c.EndLayer();
}
var entityBefore = Capture("entity-before-raise", OverlappingEntities);
entityAFront = true;
var entityAfter = Capture("entity-after-raise", OverlappingEntities);
Check(ColorAt(entityBefore, .5f, .5f, Blue) && ColorAt(entityAfter, .5f, .5f, Red)
    && entityBuilds == 2 && renderer.ValidationUploads.StagingBytes == 0 && renderer.ValidationUploads.DynamicBytes == 0,
    "raising an entity changes real depth without rebuilding or uploading either entity");

using (var queuedWorkerEntered = new ManualResetEventSlim())
using (var queuedWorkerRelease = new ManualResetEventSlim())
{
    var queuedPixels = new List<byte[]>();
    var queuedSourceIds = new List<ulong>();
    var queuedMetricsIds = new List<ulong>();
    using var queue = new DuxelRenderQueue(work =>
    {
        if (queuedPixels.Count == 0)
        {
            queuedWorkerEntered.Set();
            if (!queuedWorkerRelease.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("GPU queue release");
        }
        queuedPixels.Add(renderer.RenderAndReadback(work.Data));
        queuedSourceIds.Add(work.Data.DrawDataFrameId);
        queuedMetricsIds.Add(renderer.LastFrameMetrics.DrawDataFrameId);
    }, () => { });
    UiDrawData QueuedRetained(long revision, UiColor color) => Build(c =>
    {
        Resident(c, "queued-retained-update", new(front), new(width * .1f, height * .1f, width * .8f, height * .8f), color, revision);
    });
    var firstQueued = QueuedRetained(1, Red);
    queue.Submit(Submission(firstQueued));
    if (!queuedWorkerEntered.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("GPU queue start");
    queue.Submit(Submission(QueuedRetained(2, Green)));
    var thirdQueued = QueuedRetained(3, Blue);
    queuedWorkerRelease.Set();
    queue.Submit(Submission(thirdQueued));
    queue.WaitForIdle();
    Check(queuedPixels.Count == 3 && ColorAt(queuedPixels[0], .5f, .5f, Red)
        && ColorAt(queuedPixels[1], .5f, .5f, Green) && ColorAt(queuedPixels[2], .5f, .5f, Blue),
        "queued Vulkan frames preserve three retained geometry revisions while UI advances ahead of rendering");
    Check(queuedSourceIds.SequenceEqual(queuedMetricsIds) && queuedSourceIds.Distinct().Count() == 3,
        "overlapped GPU submissions preserve source identity through accepted presentation metrics");
    var beforeEmptySequence = renderer.LastFrameMetrics.PresentationSequence;
    var queuedEmpty = Build(_ => { });
    var hasNoGeometry = queuedEmpty.TotalVertexCount == 0 && queuedEmpty.TotalIndexCount == 0;
    queue.Submit(Submission(queuedEmpty));
    queue.WaitForIdle();
    var emptyPixels = queuedPixels[^1];
    var uniformClear = true;
    for (var pixel = 4; pixel < emptyPixels.Length; pixel += 4)
        if (!emptyPixels.AsSpan(pixel, 4).SequenceEqual(emptyPixels.AsSpan(0, 4))) { uniformClear = false; break; }
    Check(hasNoGeometry && uniformClear && renderer.LastFrameMetrics.PresentationSequence == beforeEmptySequence + 1,
        "queued zero-geometry Vulkan frame clears and presents instead of withholding first-frame or resize acknowledgement");
    var queuedCallbacks = 0;
    var queuedCallback = Build(c =>
    {
        c.GetWindowDrawList().AddRectFilled(new(10, 10, 10, 10), Red, white);
        c.GetWindowDrawList().AddCallback((_, _) => queuedCallbacks++);
    });
    Check(queue.Submit(Submission(queuedCallback)), "actual Vulkan callback submission requires the producer completion barrier");
    queue.WaitForIdle();
    Check(queuedCallbacks == 1, "actual Vulkan callback finishes once before the producer completion barrier returns");
    UiDrawCallback repeatedCallback = (_, _) => queuedCallbacks++;
    var callbackOnly = Build(c =>
    {
        c.GetWindowDrawList().AddCallback(repeatedCallback);
        c.GetWindowDrawList().AddCallback(repeatedCallback);
    });
    Check(queue.Submit(Submission(callbackOnly)), "callback-only frames survive builder flush and require a completion barrier");
    queue.WaitForIdle();
    Check(queuedCallbacks == 3, "adjacent identical callbacks execute twice instead of being merged or removed");
    var callbackLayerBuilds = 0;
    for (var frame = 0; frame < 2; frame++)
    {
        var cachedCallbackOnly = Build(c =>
        {
            if (c.BeginLayer("callback-only-resident", new(false, 1, default) { ContentVersion = 0 }))
            {
                callbackLayerBuilds++;
                c.GetWindowDrawList().AddCallback(repeatedCallback);
            }
            c.EndLayer();
        });
        if (!queue.Submit(Submission(cachedCallbackOnly))) throw new InvalidOperationException("Retained callback barrier missing");
        queue.WaitForIdle();
    }
    Check(queuedCallbacks == 5 && callbackLayerBuilds == 1,
        "a callback-only retained layer replays its hook every frame without rebuilding content");
}

var dynamicA = Capture("dynamic-a", c => Layer(c, "changing", front, Red));
var dynamicB = Capture("dynamic-b", c => Layer(c, "changing", Matrix4x4.CreateTranslation(.3f, 0, .4f), Green));
Check(!dynamicA.AsSpan().SequenceEqual(dynamicB) && ColorAt(dynamicB, .5f, .5f, Green), "same dynamic layer changes geometry color and transform on the next frame");
for (var frame = 0; frame < 32; frame++)
{
    var previousPresentation = renderer.LastFrameMetrics;
    var dynamicData = Build(c => Layer(c, "changing", Matrix4x4.CreateRotationY(frame * .01f) * front,
        (frame & 1) == 0 ? Red : Green));
    try
    {
        Check(renderer.TryRenderDrawData(dynamicData), "in-flight dynamic frame " + frame);
        var presentation = renderer.LastFrameMetrics;
        Check(presentation.PresentationSequence == previousPresentation.PresentationSequence + 1
            && presentation.DrawDataFrameId == dynamicData.DrawDataFrameId && presentation.DrawDataFrameId > 0
            && presentation.PresentationTimestampTicks > previousPresentation.PresentationTimestampTicks
            && (previousPresentation.PresentationSequence == 0
                ? presentation.PresentationIntervalSeconds == 0
                : presentation.PresentationIntervalSeconds > 0), "accepted presentation advances sequence and timestamp " + frame);
        if (frame == 0)
        {
            Check(!renderer.TryRenderDrawData(dynamicData with { DisplaySize = default })
                && renderer.LastFrameMetrics.PresentationSequence == presentation.PresentationSequence
                && renderer.LastFrameMetrics.PresentationTimestampTicks == presentation.PresentationTimestampTicks,
                "zero-size skipped render does not advance presentation or duplicate its timestamp");
        }
    }
    finally { dynamicData.ReleasePooled(); }
}
renderer.SetMsaaSamples(4);
void RoundedDepth(UiImmediateContext c, bool reverse)
{
    void RoundedFront()
    {
        c.BeginLayer("rounded-depth-front", new(false, 1, default) { Scene3D = new(front) });
        c.GetWindowDrawList().AddRectFilledRounded(new(width * .2f + .27f, height * .2f + .31f,
            width * .6f, height * .6f), Red, white, 17.3f, rect);
        c.EndLayer();
    }
    if (reverse) RoundedFront();
    Layer(c, "rounded-depth-back", back, Blue);
    if (!reverse) RoundedFront();
}
var roundedDepth = Capture("rounded-depth-msaa", c => RoundedDepth(c, false));
var roundedDepthReverse = Capture("rounded-depth-msaa-reversed", c => RoundedDepth(c, true));
Check(roundedDepth.AsSpan().SequenceEqual(roundedDepthReverse),
    "opaque rounded-plane coverage preserves full-image depth order under MSAA4");
for (var kind = 0; kind < 2; kind++)
{
    void ShapeDepth(UiImmediateContext c, bool reverse)
    {
        void ShapeFront()
        {
            c.BeginLayer("coverage-shape-front", new(false, 1, default) { Scene3D = new(front) });
            var draw = c.GetWindowDrawList();
            if (kind == 0)
                draw.AddCircleFilled(new(width * .5f + .27f, height * .5f + .31f), 47.3f, Red, white);
            else
                draw.AddRectFilledRounded(new(width * .2f + .27f, height * .2f + .31f, width * .6f, height * .6f),
                    Red, Green, white, 17.3f, 2.7f, rect);
            c.EndLayer();
        }
        if (reverse) ShapeFront();
        Layer(c, "coverage-shape-back", back, Blue);
        if (!reverse) ShapeFront();
    }
    var shape = Capture($"coverage-shape-{kind}", c => ShapeDepth(c, false));
    var shapeReverse = Capture($"coverage-shape-{kind}-reversed", c => ShapeDepth(c, true));
    Check(shape.AsSpan().SequenceEqual(shapeReverse) && ColorAt(shape, .5f, .5f, Red)
        && ColorAt(shape, .11f, .11f, Blue), $"MSAA4 analytic shape {kind} preserves color and gaps in both depth orders");
}
var blendedMsaa = Capture("partial-alpha-msaa", c =>
{
    Layer(c, "alpha-msaa-back", back, Blue);
    Layer(c, "alpha-msaa-front", front, new(0x800000FF), writeDepth: false);
});
Check(ColorAt(blendedMsaa, .5f, .5f, blendExpected, 3),
    "MSAA4 depth-read material retains continuous alpha blending instead of sample-mask opacity");
var msaaImage = Capture("msaa-depth", c => { Layer(c, "back", back, Blue); Layer(c, "front", front, Red); });
Check(ColorAt(msaaImage, .5f, .5f, Red), "MSAA color and depth attachments share a sample count");
renderer.SetMsaaSamples(1);
var singleSampleDepth = Capture("rounded-depth-1x", c => RoundedDepth(c, false));
var singleSampleDepthReverse = Capture("rounded-depth-1x-reversed", c => RoundedDepth(c, true));
Check(singleSampleDepth.AsSpan().SequenceEqual(singleSampleDepthReverse),
    "single-sample opaque rounded coverage preserves full-image depth order");

// Observe actual frame-owned storage before and after EndLayer.
UiDrawList? original = null;
var ownership = Build(c =>
{
    c.BeginLayer("ownership", new(false, .5f, new(3, 4)));
    original = TransferTriangle(c, new(20, 20), new(40, 20), new(20, 40), Red);
    c.EndLayer();
});
try
{
    var transferred = ownership.DrawLists[0];
    Check(ReferenceEquals(original!.Vertices, transferred.Vertices) && ReferenceEquals(original.Indices, transferred.Indices), "dynamic layer transfers original vertex/index buffers");
    Check(transferred.Commands[0].Translation == new UiVector2(3, 4) && transferred.Commands[0].Opacity == .5f, "dynamic transfer preserves command transform and opacity");
}
finally { ownership.ReleasePooled(); }
// Render after buffer return to expose accidental reuse or double return.
Capture("after-ownership", c => Layer(c, "last", front, Green));
Check(NativeTestMethods.SetWindowPos(platform.WindowHandle, 0, 0, 0, 420, 320, 0x0002 | 0x0004 | 0x0010), "native resize request");
platform.PollEvents();
var resized = platform.FramebufferSize;
Check(resized.Width != width && resized.Height != height, "platform observes resized framebuffer");
width = resized.Width;
height = resized.Height;
rect = new(0, 0, width, height);
// Recreate through the real resize path before requesting a sized readback buffer.
var resizeData = Build(c => Layer(c, "resize", front, Red));
try { Check(renderer.TryRenderDrawData(resizeData), "resize recreates depth resources"); }
finally { resizeData.ReleasePooled(); }
var resizeImage = Capture("after-resize", c => Layer(c, "resize", front, Green));
Check(ColorAt(resizeImage, .5f, .5f, Green), "depth rendering survives swapchain resize");
UiDrawData LeaseFrame(float x) => Build(c => Resident(c, "frame-lease", new(Matrix4x4.CreateTranslation(x, 0, .3f)),
    new(20, 20, 30, 30), Red));
UiDrawList LeaseList(UiDrawData data) => data.DrawLists.First(l => l.StaticGeometryKey?.Contains("frame-lease:", StringComparison.Ordinal) == true);
LeaseFrame(0).ReleasePooled();
var leaseFrameA = LeaseFrame(.1f);
var leaseFrameB = LeaseFrame(.2f);
UiDrawData? leaseFrameC = null, leaseFrameD = null;
try
{
    var leaseA = LeaseList(leaseFrameA);
    var leaseB = LeaseList(leaseFrameB);
    var sceneA = leaseA.Scene3D;
    var sceneB = leaseB.Scene3D;
    Check(!ReferenceEquals(leaseA, leaseB) && sceneA != sceneB,
        "outstanding frame wrappers have independent camera snapshots");
    var copiedScene = new UiLayer3D(Matrix4x4.CreateTranslation(.9f, 0, .3f));
    var publicCopy = leaseA with { Scene3D = copiedScene };
    publicCopy.ReleasePooled();
    leaseFrameC = LeaseFrame(.3f);
    Check(!ReferenceEquals(leaseA, LeaseList(leaseFrameC)) && leaseA.Scene3D == sceneA,
        "releasing a public record copy cannot return the original frame lease");
    Task.Run(leaseFrameA.ReleasePooled).GetAwaiter().GetResult();
    leaseFrameD = LeaseFrame(.4f);
    Check(ReferenceEquals(leaseA, LeaseList(leaseFrameD)) && leaseB.Scene3D == sceneB
        && publicCopy.Scene3D == copiedScene,
        "released wrapper is reused after cross-thread return without mutating other frame snapshots");
}
finally
{
    leaseFrameA.ReleasePooled(); leaseFrameB.ReleasePooled();
    leaseFrameC?.ReleasePooled(); leaseFrameD?.ReleasePooled();
}
var warmLeaseFrame = LeaseFrame(.5f);
var warmLeaseReference = LeaseList(warmLeaseFrame);
warmLeaseFrame.ReleasePooled();
var expectedRenderFailure = false;
try
{
    Build(c =>
    {
        Resident(c, "frame-lease", new(Matrix4x4.CreateTranslation(.55f, 0, .3f)), new(20, 20, 30, 30), Red);
        throw new InvalidOperationException("intentional frame failure");
    });
}
catch (InvalidOperationException ex) when (ex.Message == "intentional frame failure") { expectedRenderFailure = true; }
Check(expectedRenderFailure, "failed frame propagates the original render error");
var recoveredLeaseFrame = LeaseFrame(.6f);
try { Check(ReferenceEquals(warmLeaseReference, LeaseList(recoveredLeaseFrame)), "next frame returns abandoned scene leases before renting replacements"); }
finally { recoveredLeaseFrame.ReleasePooled(); }
void RetainedParent(UiImmediateContext c)
{
    if (c.BeginLayer("frame-lease-parent", new(false, 1, default) { Scene3D = new(front), ContentVersion = 0 }))
        Resident(c, "frame-lease", new(Matrix4x4.CreateTranslation(.6f, 0, .3f)), new(20, 20, 30, 30), Red);
    c.EndLayer();
}
var parentLeaseBefore = Capture("frame-lease-parent-before", RetainedParent);
LeaseFrame(.9f).ReleasePooled();
var parentLeaseAfter = Capture("frame-lease-parent-after", RetainedParent);
Check(parentLeaseBefore.AsSpan().SequenceEqual(parentLeaseAfter),
    "retained parent preserves child scene snapshot after the child's frame wrapper is reused");

var lifetimeData = Build(c =>
{
    Resident(c, "batch-life-a", new(front), new(20, 20, 30, 30), Red);
    Resident(c, "batch-life-b", new(front), new(60, 20, 30, 30), Green);
});
try
{
    using var batcher = new UiRetainedLayerBatcher();
    using (var borrowed = batcher.Prepare(lifetimeData))
    {
        Check(batcher.RebuiltGroups == 1 && batcher.BatchedSourceLists == 2,
            "common layer batcher builds shared geometry from independent resident lists");
        var nestedRejected = false;
        try { batcher.Prepare(lifetimeData); } catch (InvalidOperationException) { nestedRejected = true; }
        Check(nestedRejected, "batcher rejects overlapping borrowed frames");
    }
    using (var borrowed = batcher.Prepare(lifetimeData))
        Check(batcher.RebuiltGroups == 0, "batcher reuses its geometry on consecutive frames");
    UiDrawList previousBatchWrapper;
    UiDrawList previousBatchCopy;
    UiLayer3D? previousBatchScene;
    using (var borrowed = batcher.Prepare(lifetimeData))
    {
        previousBatchWrapper = borrowed.DrawData.DrawLists[0];
        previousBatchCopy = previousBatchWrapper with { };
        previousBatchScene = previousBatchWrapper.Scene3D;
    }
    var cameraInput = Build(c =>
    {
        var movedScene = new UiLayer3D(Matrix4x4.CreateTranslation(.1f, 0, .2f));
        Resident(c, "batch-life-a", movedScene, new(20, 20, 30, 30), Red);
        Resident(c, "batch-life-b", movedScene, new(60, 20, 30, 30), Green);
    });
    try
    {
        using (var movedBatch = batcher.Prepare(cameraInput))
            Check(ReferenceEquals(previousBatchWrapper, movedBatch.DrawData.DrawLists[0])
                && movedBatch.DrawData.DrawLists[0].Scene3D != previousBatchScene
                && lifetimeData.DrawLists[0].Scene3D == previousBatchScene && batcher.RebuiltGroups == 0,
                "exclusive batch replay wrappers update cameras without changing source snapshots or geometry");
        Check(previousBatchCopy.Scene3D == previousBatchScene,
            "public record copies keep their camera snapshot when an exclusive batch wrapper is reused");
    }
    finally { cameraInput.ReleasePooled(); }
    var commandOnlyLists = lifetimeData.DrawLists.AsSpan().ToArray();
    var commandOnlySource = commandOnlyLists[0];
    var commandOnlyCommands = commandOnlySource.Commands.AsSpan().ToArray();
    var commandOnlyIndex = Array.FindIndex(commandOnlyCommands, command => command.ElementCount > 0);
    commandOnlyCommands[commandOnlyIndex] = commandOnlyCommands[commandOnlyIndex] with { Opacity = .25f };
    commandOnlyLists[0] = commandOnlySource with
    {
        Commands = UiPooledList<UiDrawCommand>.FromArray(commandOnlyCommands), CommandStateStamp = 0,
    };
    var commandOnlyData = lifetimeData with { DrawLists = UiPooledList<UiDrawList>.FromArray(commandOnlyLists) };
    using (var commandOnlyBatch = batcher.Prepare(commandOnlyData))
    {
        Check(commandOnlyLists[0].GetStaticGeometryStamp() == commandOnlySource.GetStaticGeometryStamp()
            && batcher.RebuiltGroups == 1, "command-only changes invalidate a retained batch with identical geometry");
        Check(commandOnlyBatch.DrawData.DrawLists.SelectMany(list => list.Commands).Any(command => command.Opacity == .25f),
            "retained batch rebuild preserves updated draw commands");
    }
    var empty = lifetimeData with { DrawLists = UiPooledList<UiDrawList>.FromArray([]) };
    for (var i = 0; i < 181; i++) batcher.Prepare(empty).Dispose();
    using (var returned = batcher.Prepare(lifetimeData))
        Check(batcher.RebuiltGroups == 1 && returned.DrawData.DrawLists.Count > 0,
            "expired batch geometry is rebuilt from still-valid source ownership");
}
finally { lifetimeData.ReleasePooled(); }
Capture("remove-cached-seed", c => Resident(c, "remove-cached", new(front), new(20, 20, 30, 30), Red));
Capture("remove-cached-empty", c =>
{
    var skipped = !c.BeginLayer("remove-cached", new(false, 1, default) { Scene3D = new(front), ContentVersion = 0 });
    c.RemoveLayer("remove-cached");
    c.EndLayer();
    Check(skipped, "cached layer can be removed between skipped BeginLayer and EndLayer");
});
Check(renderer.LastFrameMetrics.DrawCalls == 0, "removed cached layer is not replayed from its stored entry");
ui.Dispose();
EventLoopTests.Run(Check, output);
CpuTimingTests.Run(Check);
CompiledCapacityTests.Run(Check);
if (Environment.GetEnvironmentVariable("DUXEL_VALIDATION_SAMPLE_ASSEMBLY") is { Length: > 0 } sampleAssembly)
    SampleWindowTests.Run(Check, output, sampleAssembly);
WindowRenderingTests.Run(renderer,Capture,next=>input=next,Check,width,height);
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { framework = RuntimeInformation.FrameworkDescription, passed = checks.Count, checks, width, height, glyphUniqueVertices, glyphTriangleIndices, glyphExpandedGpuBytes, glyphIndexedGpuBytes }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS all {checks.Count} checks");

sealed class ActionScreen(Action<UiImmediateContext> draw) : UiScreen
{
    public override void Render(UiImmediateContext ui) => draw(ui);
}

static partial class NativeTestMethods
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PeekMessageW(nint message, nint window, uint min, uint max, uint remove);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
