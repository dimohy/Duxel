using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);

if (args is [var svgReplacementOutput, "svg-replacement-pressure", "1" or "3"])
{
    SvgReplacementPressureTests.Run(svgReplacementOutput, int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));
    return;
}

if (args is [var svgGalleryProfileOutput, "gallery-svg-profile"])
{
    SvgGalleryFrameBenchmark.Run(svgGalleryProfileOutput);
    return;
}

if (args is [var svgPointerOutput, "gallery-svg-pointer", var svgPointerMode, .. var svgPointerOptions]
    && svgPointerMode is "camera" or "high-resolution" or "arc" or "primary"
    && svgPointerOptions is [] or ["highlight"] or ["win32"] or ["win32", "highlight"]
        or ["win32", "burst"] or ["win32", "burst", "highlight"] or ["win32", "burst", "unmerged"]
        or ["win32", "burst", "desktop"])
{
    SvgGalleryFrameBenchmark.MeasurePointerGestures(svgPointerOutput, svgPointerMode != "camera", svgPointerMode == "arc",
        svgPointerMode == "primary", svgPointerOptions.Contains("highlight"), svgPointerOptions.Contains("win32"),
        svgPointerOptions.Contains("burst"), !svgPointerOptions.Contains("unmerged"), svgPointerOptions.Contains("desktop"));
    return;
}

if (args is [var svgArcOutput, "gallery-svg-arc-scroll"])
{
    SvgGalleryFrameBenchmark.MeasureArcScroll(svgArcOutput);
    return;
}

if (args is [var svgCodeScrollOutput, "gallery-svg-code-scroll"])
{
    SvgGalleryFrameBenchmark.MeasureCodeScrollBursts(svgCodeScrollOutput);
    return;
}

if (args is [var svgLayerMixOutput, "gallery-svg-layer-mix"])
{
    SvgGalleryFrameBenchmark.MeasureLayerMix(svgLayerMixOutput);
    return;
}

if (args is [var coldWarmArcOutput, "gallery-svg-cold-warm", .. var coldWarmOptions]
    && coldWarmOptions is [] or ["input-profile"])
{
    var profileInput = coldWarmOptions.Length != 0;
    SvgGalleryFrameBenchmark.MeasurePointerGestures(Path.Combine(coldWarmArcOutput, "first"),
        highResolution: true, arc: true, nativeMessages: true, burst: true, profileInput: profileInput);
    // A new Gallery and Vulkan fixture start from the same state with the managed code already compiled.
    SvgGalleryFrameBenchmark.MeasurePointerGestures(Path.Combine(coldWarmArcOutput, "repeat"),
        highResolution: true, arc: true, nativeMessages: true, burst: true, profileInput: profileInput);
    return;
}

if (args is [var codeSelectionOutput, "gallery-code-selection", "arc" or "markdown", .. var codeSelectionOptions]
    && codeSelectionOptions is [] or ["input-profile"])
{
    CodeViewPerformanceTests.MeasureSelection(codeSelectionOutput, arc: args[2] == "arc",
        profileInput: codeSelectionOptions.Length != 0);
    return;
}

if (args is ["startup-retry", var retryPrimaryFont, var retrySecondaryFont])
{
    if (!OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("The startup retry probe requires Windows.");
    AppStartupRetryTests.Run(retryPrimaryFont, retrySecondaryFont, Duxel.Windows.App.DuxelWindowsApp.Run, hwnd =>
    {
        if (hwnd == 0 || TooltipEventsHost.IsWindow(hwnd))
            throw new InvalidOperationException("The application did not destroy its native window.");
        Console.WriteLine("Confirmed HWND teardown.");
    });
    return;
}

if (args is [var glyphReplayOutput, "glyph-texture-replay", var glyphInput, var glyphWidth, var glyphHeight])
{
    GlyphTextureReplayTests.Run(glyphReplayOutput, 3, glyphInput, glyphWidth, glyphHeight);
    return;
}

if (args is [var legacyMenuOutput, "legacy-menu-gpu"])
{
    Directory.CreateDirectory(legacyMenuOutput);
    var passed = new List<string>();
    LegacyMenuRenderingTests.Run(legacyMenuOutput, (condition, name) =>
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
        passed.Add(name);
    });
    File.WriteAllText(Path.Combine(legacyMenuOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var publicHolidayOutput, "holiday-provider-live"])
{
    Directory.CreateDirectory(publicHolidayOutput);
    var passed = new List<string>();
    void CheckLiveHoliday(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
        passed.Add(name);
    }
    HolidayProviderTests.RunLive(CheckLiveHoliday);
    TemporalPickerRenderingTests.RunLiveHolidayPage(publicHolidayOutput, CheckLiveHoliday);
    File.WriteAllText(Path.Combine(publicHolidayOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var hostPixelOutput, "presentation-reuse-host-pixels"])
{
    Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE", "1");
    Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", "100000");
    PresentationReuseTests.Run(hostPixelOutput, 3, captureHostPixels: true);
    return;
}

if (args is [var galleryTimePickerOutput, "uia-gallery-time-picker" or "win32-gallery-time-picker-wheel"
    or "uia-gallery-date-pickers", var galleryPid])
{
    Directory.CreateDirectory(galleryTimePickerOutput);
    var result = Path.Combine(galleryTimePickerOutput, "result.json");
    if (File.Exists(result))
        throw new IOException("Use a fresh Gallery UIA result path.");
    var pid = int.Parse(galleryPid, System.Globalization.CultureInfo.InvariantCulture);
    var window = AccessibilityClientTests.WindowForProcess(pid, "Duxel Gallery");
    if (window == 0)
        throw new InvalidOperationException("The specified process has no Duxel Gallery window.");
    var passed = new List<string>();
    AccessibilityClientTests.Run(window, (condition, name) =>
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
        passed.Add(name);
    }, galleryTimePicker: args[1] != "uia-gallery-date-pickers",
        galleryTimePickerWheel: args[1] == "win32-gallery-time-picker-wheel",
        galleryDatePickers: args[1] == "uia-gallery-date-pickers");
    using var stream = File.Open(result, FileMode.CreateNew);
    JsonSerializer.Serialize(stream, new GraphicsResult(RuntimeInformation.FrameworkDescription, args[1],
        passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult);
    return;
}

if (args is [var maskOutput, "opacity-mask-gpu" or "opacity-mask-gallery-only"])
{
    Directory.CreateDirectory(maskOutput);
    var passed = new List<string>();
    void CheckMask(bool condition, string name)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    }
    if (args[1] == "opacity-mask-gallery-only") OpacityMaskGalleryTests.Run(maskOutput, CheckMask);
    else HorizontalOpacityMaskRenderingTests.Run(maskOutput, CheckMask);
    File.WriteAllText(Path.Combine(maskOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var containerPixelOutput, "container-pixels" or "virtual-list-pixels" or "virtual-list-host-pixels" or "virtual-list-rtl-pixels"
    or "virtual-grid-pixels" or "virtual-grid-host-pixels" or "clip-host-pixels"])
{
    ContainerPixelTests.Run(containerPixelOutput, 3, args[1]);
    return;
}

if (args is [var treeOutput, "tree-gpu" or "tree-label-gpu"])
{
    TreeRenderingTests.RunMode(treeOutput, args[1], 1);
    return;
}

if (args is [var selectOutput, "select-gpu" or "select-gallery-only" or "tree-gallery-only" or "textfield-gallery-only" or "card-nudge-gallery-only" or "carousel-gallery-only" or "navigation-bar-gallery-only" or "provider-direction-only"])
{
    Directory.CreateDirectory(selectOutput);
    var passed = new List<string>();
    void CheckSelect(bool condition, string name)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    }
    if (args[1] == "select-gallery-only") SelectGalleryTests.Run(selectOutput, CheckSelect);
    else if (args[1] == "tree-gallery-only") TreeGalleryTests.Run(selectOutput, CheckSelect);
    else if (args[1] == "textfield-gallery-only") TextFieldTests.RunGallery(selectOutput, CheckSelect);
    else if (args[1] == "card-nudge-gallery-only") CardNudgeTests.RunGallery(selectOutput, CheckSelect);
    else if (args[1] == "carousel-gallery-only") CarouselTests.Run(selectOutput, CheckSelect);
    else if (args[1] == "navigation-bar-gallery-only") NavigationBarTests.Run(selectOutput, CheckSelect);
    else if (args[1] == "provider-direction-only") FluentProviderTests.RunDirection(selectOutput, CheckSelect);
    else SelectRenderingTests.Run(selectOutput, CheckSelect);
    File.WriteAllText(Path.Combine(selectOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var toastSurfaceOutput, "toast-surface-gpu"])
{
    Directory.CreateDirectory(toastSurfaceOutput);
    var passed = new List<string>();
    ToastSurfaceRenderingTests.Run(toastSurfaceOutput, (condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    });
    File.WriteAllText(Path.Combine(toastSurfaceOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "toast-surface-gpu", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var toastOutput, "toast-gallery-only"])
{
    Directory.CreateDirectory(toastOutput);
    var passed = new List<string>();
    ToastGalleryTests.Run(toastOutput, (condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    });
    File.WriteAllText(Path.Combine(toastOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "toast-gallery-only", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var cacheOutput, "segmented-cache-gpu"])
{
    Directory.CreateDirectory(cacheOutput);
    var passed = new List<string>();
    SegmentedControlCacheRenderingTests.Run(cacheOutput, (condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    });
    File.WriteAllText(Path.Combine(cacheOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "segmented-cache-gpu", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var segmentedOutput, "segmented-rendering-only" or "segmented-gallery-only"])
{
    Directory.CreateDirectory(segmentedOutput);
    var passed = new List<string>();
    SegmentedControlRenderingTests.Run(segmentedOutput, (condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    }, galleryOnly : args[1] == "segmented-gallery-only");
    File.WriteAllText(Path.Combine(segmentedOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "segmented-rendering-only", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var temporalOutput, "temporal-gallery-only" or "temporal-time-gallery-only" or "temporal-tail-gallery-only" or "temporal-diagnostics-only"])
{
    Directory.CreateDirectory(temporalOutput);
    var passed = new List<string>();
    TemporalPickerRenderingTests.Run(temporalOutput, (condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    }, args[1] == "temporal-time-gallery-only" ? Duxel.Gallery.TemporalPickerKind.Time
        : args[1] == "temporal-tail-gallery-only" ? Duxel.Gallery.TemporalPickerKind.DateTime : null,
        includeTail : args[1] == "temporal-tail-gallery-only", diagnosticsOnly : args[1] == "temporal-diagnostics-only");
    File.WriteAllText(Path.Combine(temporalOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "temporal-gallery-only", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var calendarOutput, "calendar-rendering-only" or "calendar-gallery-only"])
{
    Directory.CreateDirectory(calendarOutput);
    var passed = new List<string>();
    CalendarRenderingTests.Run(calendarOutput, (condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    }, galleryOnly : args[1] == "calendar-gallery-only");
    File.WriteAllText(Path.Combine(calendarOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var closeOutput, "gallery-close-latency" or "gallery-close-time-popup" or "gallery-uia-holidays", var galleryExecutable])
{
    GalleryCloseLatencyProbe.Run(closeOutput, galleryExecutable,
        openTimePopup : args[1] == "gallery-close-time-popup", verifyHolidays : args[1] == "gallery-uia-holidays");
    return;
}

if (args is [var headerOutput, "gallery-header-only"])
{
    Directory.CreateDirectory(headerOutput);
    var passed = new List<string>();
    GalleryPinnedHeaderTests.RunGpu(headerOutput, (condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException(name);
        passed.Add(name);
    });
    File.WriteAllText(Path.Combine(headerOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "gallery-header-only", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args is [var fullCodeOutput, "code-view-full-bench"])
{
    CodeViewPerformanceTests.Run(fullCodeOutput, fullSource : true);
    return;
}

if (args is [var codeOutput, "code-view-bench"])
{
    CodeViewPerformanceTests.Run(codeOutput);
    return;
}

if (args is [var hudOutput, "hud-gallery-only"])
{
    Directory.CreateDirectory(hudOutput);
    var passed = new List<string>();
    var failed = new List<string>();
    HeadsUpDisplayContentTests.RunGalleryGpu(hudOutput, (condition, name) =>
    {
        (condition ? passed : failed).Add(name);
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    });
    File.WriteAllLines(Path.Combine(hudOutput, "failures.txt"), failed);
    File.WriteAllText(Path.Combine(hudOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "hud-gallery-only", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failed.Count != 0) throw new InvalidOperationException("HUD Gallery checks failed : " + failed.Count);
    return;
}

if (args is [var galleryOutput, "gallery-visual-only"])
{
    Directory.CreateDirectory(galleryOutput);
    var passed = new List<string>();
    var failed = new List<string>();
    GalleryVisualTests.Run(galleryOutput, (condition, name) =>
    {
        (condition ? passed : failed).Add(name);
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    });
    File.WriteAllLines(Path.Combine(galleryOutput, "failures.txt"), failed);
    File.WriteAllText(Path.Combine(galleryOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "gallery-visual-only", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failed.Count != 0) throw new InvalidOperationException("Gallery GPU checks failed : " + failed.Count);
    return;
}

if (args is [var drawerOutput, "drawer-gpu"])
{
    var passed = new List<string>();
    var failed = new List<string>();
    DrawerRenderingTests.Run(drawerOutput, (condition, name) =>
    {
        (condition ? passed : failed).Add(name);
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    });
    File.WriteAllLines(Path.Combine(drawerOutput, "failures.txt"), failed);
    File.WriteAllText(Path.Combine(drawerOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "drawer-gpu", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failed.Count != 0) throw new InvalidOperationException("Drawer GPU checks failed : " + failed.Count);
    return;
}

if (args is [var cpuOutput, "input-batch-cpu"])
{
    Directory.CreateDirectory(cpuOutput);
    var result = InputBatchCpuDiagnostic.Run();
    File.WriteAllText(Path.Combine(cpuOutput, "result.json"), JsonSerializer.Serialize(result, GraphicsJson.Default.InputBatchCpuResult));
    if (result.Failures.Length != 0)
        throw new InvalidOperationException("Input batch CPU diagnostic correctness failures : " + result.Failures.Length);
    Console.WriteLine("Completed six fixed-workload CPU diagnostic cases.");
    return;
}

if (args is [var queuedInputOutput, "queued-input-only" or "input-order-probe" or "pending-frame-resources-only" or "ordered-input-batch-only" or "input-cache-gpu-only" or "app-ordered-input-only" or "native-ordered-input-only" or "native-ordered-app-only"])
{
    Directory.CreateDirectory(queuedInputOutput);
    var passed = new List<string>();
    var failed = new List<string>();
    void RecordInput(bool condition, string name)
    {
        (condition ? passed : failed).Add(name);
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    }
    if (args[1] == "input-cache-gpu-only")
        InputPresentationGpuTests.RunCacheChanges(RecordInput);
    else if (args[1] == "native-ordered-app-only")
        AppOrderedInputTests.Run(RecordInput, nativeCapture: true);
    else if (args[1] == "native-ordered-input-only")
        WindowsImeOwnershipTests.RunOrderedInput(RecordInput);
    else if (args[1] == "app-ordered-input-only")
        AppOrderedInputTests.Run(RecordInput);
    else if (args[1] == "ordered-input-batch-only")
    {
        OrderedInputBatchTests.Run(RecordInput);
        OrderedInputBatchTests.RunGpu(RecordInput);
    }
    else if (args[1] == "pending-frame-resources-only")
    {
        PendingFrameResourceTests.Run(RecordInput);
        PendingFrameResourceTests.RunGpu(RecordInput);
    }
    else if (args[1] == "input-order-probe")
        WindowsImeOwnershipTests.RunInputOrderProbe(RecordInput);
    else
        QueuedInputTests.Run(RecordInput);
    File.WriteAllLines(Path.Combine(queuedInputOutput, "failures.txt"), failed);
    File.WriteAllText(Path.Combine(queuedInputOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failed.Count != 0)
        throw new InvalidOperationException("Validation failures (" + args[1] + ") : " + failed.Count);
    return;
}

if (args is [var imeOwnerOutput, "ime-owner-only"])
{
    Directory.CreateDirectory(imeOwnerOutput);
    var imeOwnerPassed = new List<string>();
    var imeOwnerFailed = new List<string>();
    WindowsImeOwnershipTests.Run((condition, name) =>
    {
        (condition ? imeOwnerPassed : imeOwnerFailed).Add(name);
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    });
    File.WriteAllLines(Path.Combine(imeOwnerOutput, "failures.txt"), imeOwnerFailed);
    File.WriteAllText(Path.Combine(imeOwnerOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "ime-owner-only", imeOwnerPassed.Count, imeOwnerPassed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (imeOwnerFailed.Count != 0)
        throw new InvalidOperationException("Native IME routing failures : " + imeOwnerFailed.Count);
    return;
}

if (args is [var backgroundOutput, "text-background-gpu"])
{
    InlineTextRenderingTests.RunBackgroundComposition(backgroundOutput);
    return;
}

if (args is [var typographyOutput, "system-typography-only"])
{
    Directory.CreateDirectory(typographyOutput);
    var typographyChecks = new List<string>();
    SystemTypographyTests.Run((condition, name) =>
    {
        if (!condition)
            throw new InvalidOperationException(name);
        typographyChecks.Add(name);
        Console.WriteLine("PASS " + name);
    });
    File.WriteAllText(Path.Combine(typographyOutput, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "system-typography-only", typographyChecks.Count, typographyChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "presence-group-gpu")
{
    PresenceGroupRenderingTests.Run(Path.GetFullPath(args[0]));
    DynamicPresenceGroupRenderingTests.Run(Path.Combine(Path.GetFullPath(args[0]), "dynamic"));
    return;
}

if (args.Length == 2 && args[1] == "inline-alignment-gpu")
{
    InlineAlignmentRenderingTests.Run(Path.GetFullPath(args[0]));
    return;
}

if (args.Length == 2 && args[1] == "owned-inline-gpu")
{
    var ownedGpuOutput = Path.GetFullPath(args[0]);
    if (Directory.Exists(ownedGpuOutput))
        throw new IOException("Use a fresh owned-inline GPU cohort directory.");
    Directory.CreateDirectory(ownedGpuOutput);
    InlineTextRenderingTests.Run(Path.Combine(ownedGpuOutput, "owned-text"), owned: true);
    OwnedInlineRenderingTests.Run(Path.Combine(ownedGpuOutput, "owned-objects"));
    InlineTextRenderingTests.Run(Path.Combine(ownedGpuOutput, "literal-regression"));
    MessageBarRenderingTests.Run(Path.Combine(ownedGpuOutput, "messagebar-regression"));
    return;
}

if (args.Length == 2 && args[1] == "messagebar-gpu")
{
    MessageBarRenderingTests.Run(Path.GetFullPath(args[0]));
    return;
}

if (args.Length == 2 && args[1] == "inline-text-gpu")
{
    InlineTextRenderingTests.Run(Path.GetFullPath(args[0]));
    return;
}

if (args.Length == 2 && args[1] == "text-accessibility-gpu")
{
    TextAccessibilityRenderingTests.Run(Path.GetFullPath(args[0]));
    return;
}

if (args.Length == 2 && args[1] == "skeleton-gpu")
{
    SkeletonRenderingTests.Run(Path.GetFullPath(args[0]), 1);
    return;
}

if (args.Length == 2 && args[1] is "windows-shaping-only" or "paragraph-geometry-only" or "paragraph-font-context-only" or "styled-paragraph-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var passed = new List<string>();
    var failed = new List<string>();
    void RecordShapingCheck(bool condition, string name)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    }
    if (args[1] == "styled-paragraph-only")
        StyledParagraphTests.Run(RecordShapingCheck);
    else if (args[1] == "paragraph-font-context-only")
        ParagraphFontContextTests.Run(RecordShapingCheck);
    else if (args[1] == "paragraph-geometry-only")
        ParagraphGeometryTests.Run(RecordShapingCheck);
    else
        WindowsShapingTests.Run(destination, RecordShapingCheck);
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failed);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failed.Count != 0)
        throw new InvalidOperationException("Windows shaping qualification failures : " + failed.Count);
    return;
}

if (args.Length is 2 or 3 && args[1] == "observer-session-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var observerSessionChecks = new List<string>();
    if (args.Length == 3 && args[2] is not ("visible" or "selection"))
        throw new ArgumentException("Expected optional visible or selection session profile.");
    ObserverRenderingSessionTests.Run((condition, name) =>
    {
        if (!condition)
            throw new InvalidOperationException(name);
        observerSessionChecks.Add(name);
        Console.WriteLine("PASS " + name);
    }, keepRenderingVisible: args.Length == 3 && args[2] == "visible", exerciseSelection: args.Length == 3 && args[2] == "selection");
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "observer-session-only", observerSessionChecks.Count, observerSessionChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "small-font-bench")
{
    SmallFontBenchmark.Run(Path.GetFullPath(args[0]));
    return;
}

if (args.Length == 2 && args[1] == "gallery-icon-assets")
{
    GalleryIconAssets.Export(Path.GetFullPath(args[0]));
    return;
}

if ((args.Length == 2 || args is [_, "gallery-component-source-only", _]) && args[1] is "vector-background-only" or "svg-reference-contracts-only" or "svg-feature-composition-only" or "checkbox-contrast-only" or "styled-contrast-only" or "svg-feature-colors-only" or "vector-icon-aa-only" or "popover-only" or "popup-input-only" or "toolbar-only" or "searchbox-only" or "textfield-only" or "persona-only" or "composite-contracts-only" or "form-layout-only" or "tab-measurement-only" or "delegated-layout-only" or "owned-sizing-only" or "legacy-contracts-only" or "standalone-icon-only" or "image-widget-only" or "raster-lifetime-only" or "label-only" or "accordion-only" or "rounded-image-only" or "avatar-only" or "presence-only" or "circle-cutout-only" or "box-shadow-only" or "backdrop-only" or "paint-group-only" or "scroll-precision-only" or "nav-sections-gpu" or "fluent-catalog-only" or "fluent-provider-font-gallery-only" or "progress-indicator-motion-only" or "calendar-holiday-loading-only" or "holiday-provider-only" or "temporal-kind-only" or "temporal-picker-only" or "calendar-wheel-only" or "provider-font-override-only" or "opacity-mask-only" or "segmented-style-only" or "toast-factory-only" or "toast-paragraph-only" or "toast-host-ownership-only" or "toast-viewport-only" or "toast-modal-only" or "toast-host-activation-only" or "toast-composition-only" or "toast-surface-only" or "toast-controller-only" or "segmented-scroll-only" or "tab-scroll-only" or "segmented-controlled-only" or "segmented-control-only" or "calendar-widget-only" or "tree-label-only" or "tree-presentation-only" or "tree-virtual-selection-only" or "tree-native-selection-only" or "selection-projection-only" or "tree-widget-only" or "tree-model-only" or "select-only" or "calendar-model-only" or "hud-gallery-source-only" or "hud-portal-only" or "hud-content-only" or "activity-indicator-only" or "progress-indicator-only" or "shimmer-only" or "fluent-provider-fonts-only" or "fluent-provider-retention-only" or "fluent-provider-only" or "gallery-component-source-only" or "gallery-legacy-source-only" or "gallery-samples-only" or "tag-picker-only" or "card-only" or "breadcrumb-only" or "avatar-group-only" or "dropdown-only" or "combobox-only" or "tag-only" or "dialog-only" or "skeleton-only" or "text-accessibility-only" or "announcement-only" or "inline-text-only" or "messagebar-only" or "owned-inline-only" or "presence-group-only" or "messagebar-page-only" or "messagebar-semantics-only" or "inline-alignment-only" or "styled-source-regression-only" or "rating-only" or "animation-curve-only" or "widget-margin-only" or "widget-transform-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var passed = new List<string>();
    var failed = new List<string>();
    void CheckComposite(bool condition, string name)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    }
    if (args[1] == "nav-sections-gpu")
        NavSectionHeaderTests.RunGpu(destination, CheckComposite);
    else if (args[1] == "scroll-precision-only")
    {
        ScrollViewPrecisionTests.Run(CheckComposite);
        ScrollViewTests.Run(CheckComposite);
    }
    else if (args[1] == "inline-alignment-only")
        InlineAlignmentTests.Run(CheckComposite);
    else if (args[1] == "messagebar-semantics-only")
        MessageBarSemanticsTests.Run(CheckComposite);
    else if (args[1] == "messagebar-page-only")
    {
        MessageBarPageTests.Run(CheckComposite);
        MessageBarPageInteractionTests.Run(CheckComposite);
    }
    else if (args[1] == "presence-group-only")
    {
        PresenceGroupTests.Run(CheckComposite);
        DynamicPresenceGroupTests.Run(CheckComposite);
    }
    else if (args[1] == "owned-inline-only")
        OwnedInlineTests.Run(CheckComposite);
    else if (args[1] == "styled-source-regression-only")
    {
        MarkdownDocumentTests.Run(CheckComposite);
        MarkdownWidgetTests.Run(CheckComposite);
        MarkdownPointerTests.Run(CheckComposite);
        MarkdownLinkTests.Run(CheckComposite);
        MarkdownLinkNavigationTests.Run(CheckComposite);
        MarkdownImageTests.Run(destination, CheckComposite);
        MarkdownLifetimeTests.Run(CheckComposite);
        MarkdownTableTests.Run(CheckComposite);
    }
    else if (args[1] == "messagebar-only")
        MessageBarTests.Run(CheckComposite);
    else if (args[1] == "inline-text-only")
        InlineTextTests.Run(CheckComposite);
    else if (args[1] == "announcement-only")
        AnnouncementTests.Run(CheckComposite);
    else if (args[1] == "text-accessibility-only")
    {
        DirectTextLayoutTests.Run(CheckComposite);
        TextAccessibilityTests.Run(CheckComposite);
    }
    else if (args[1] == "skeleton-only")
        SkeletonTests.Run(CheckComposite);
    else if (args[1] == "vector-icon-aa-only")
    {
        VectorGlyphCoverageTests.Run(CheckComposite);
        RatingLabelTests.CaptureHover(destination, CheckComposite);
    }
    else if (args[1] == "svg-feature-colors-only")
        SvgFeatureColorTests.Run(destination, CheckComposite);
    else if (args[1] == "vector-background-only")
        VectorBackgroundTests.Run(destination, CheckComposite);
    else if (args[1] == "svg-reference-contracts-only")
        BrowserReferenceValidationTests.Run(CheckComposite);
    else if (args[1] == "svg-feature-composition-only")
        SvgFeatureCompositionTests.Run(destination, CheckComposite);
    else if (args[1] == "checkbox-contrast-only")
        CheckboxContrastTests.Run(destination, CheckComposite);
    else if (args[1] == "styled-contrast-only")
        StyledContrastTests.Run(CheckComposite);
    else if (args[1] == "rating-only")
    {
        ForcedColorTests.Run(CheckComposite);
        RatingTests.Run(CheckComposite);
        RatingDisplayTests.Run(CheckComposite);
        RatingLabelTests.Run(destination, CheckComposite);
    }
    else if (args[1] == "animation-curve-only")
        AnimationCurveTests.Run(CheckComposite);
    else if (args[1] == "dialog-only")
    {
        DialogTests.Run(CheckComposite);
        DialogMotionTests.Run(CheckComposite);
        DialogExitTests.Run(CheckComposite);
    }
    else if (args[1] == "widget-transform-only")
    {
        WidgetTransformTests.Run(CheckComposite);
        WidgetTransformPopupTests.Run(CheckComposite);
        LegacyWidgetPortalTests.Run(CheckComposite);
    }
    else if (args[1] == "widget-margin-only")
        WidgetMarginTests.Run(CheckComposite);
    else if (args[1] == "popover-only")
    {
        PopoverTests.Run(CheckComposite);
        PopoverFocusLeaveTests.Run(CheckComposite);
        PopupSurfaceTests.Run(CheckComposite);
        PopoverTriggerTests.Run(CheckComposite);
        PopoverMenuTests.Run(CheckComposite);
        PopoverMotionTests.Run(CheckComposite);
        PopoverMotionInputTests.Run(CheckComposite);
        PopoverPaintBoundsTests.Run(CheckComposite);
    }
    else if (args[1] == "popup-input-only")
        PopupInputModeTests.Run(CheckComposite);
    else if (args[1] == "toolbar-only")
        ToolbarTests.Run(CheckComposite);
    else if (args[1] == "textfield-only")
        TextFieldTests.Run(CheckComposite);
    else if (args[1] == "searchbox-only")
    {
        SearchBoxTests.Run(CheckComposite);
        SearchBoxPointerTests.Run(CheckComposite);
    }
    else if (args[1] == "persona-only")
        PersonaTests.Run(CheckComposite);
    else if (args[1] == "tag-only")
        TagTests.Run(CheckComposite);
    else if (args[1] == "combobox-only")
        ComboboxTests.Run(CheckComposite);
    else if (args[1] == "dropdown-only")
        DropdownTests.Run(CheckComposite);
    else if (args[1] == "avatar-group-only")
        AvatarGroupTests.Run(CheckComposite);
    else if (args[1] == "breadcrumb-only")
        BreadcrumbTests.Run(CheckComposite);
    else if (args[1] == "fluent-catalog-only")
        FluentCatalogTests.Run(CheckComposite);
    else if (args[1] == "fluent-provider-font-gallery-only")
        FluentProviderFontTests.RunGallery(CheckComposite);
    else if (args[1] == "progress-indicator-motion-only")
        ProgressIndicatorTests.RunReducedMotion(CheckComposite);
    else if (args[1] == "calendar-holiday-loading-only")
        CalendarHolidayLoadingTests.Run(CheckComposite);
    else if (args[1] == "holiday-provider-only")
        HolidayProviderTests.Run(CheckComposite);
    else if (args[1] == "temporal-kind-only")
        TemporalPickerTests.RunKind(CheckComposite);
    else if (args[1] == "temporal-picker-only")
        TemporalPickerTests.Run(CheckComposite);
    else if (args[1] == "calendar-wheel-only")
        CalendarWheelTests.Run(CheckComposite);
    else if (args[1] == "provider-font-override-only")
        FluentProviderFontTests.RunOverrides(CheckComposite);
    else if (args[1] == "opacity-mask-only")
    {
        HorizontalOpacityMaskTests.Run(CheckComposite);
        BackdropCompositeTests.Run(CheckComposite);
        PaintGroupTests.Run(CheckComposite);
    }
    else if (args[1] == "segmented-style-only")
        SegmentedControlStyleTests.Run(CheckComposite);
    else if (args[1] == "toast-factory-only")
        ToastFactoryTests.Run(CheckComposite);
    else if (args[1] == "toast-paragraph-only")
        ToastParagraphTests.Run(CheckComposite);
    else if (args[1] == "toast-host-ownership-only")
        ToastHostOwnershipTests.Run(CheckComposite);
    else if (args[1] == "toast-viewport-only")
        ToastViewportTests.Run(CheckComposite);
    else if (args[1] == "toast-modal-only")
        ToastModalTests.Run(CheckComposite);
    else if (args[1] == "toast-host-activation-only")
        WindowsImeOwnershipTests.RunToastHostActivation(CheckComposite);
    else if (args[1] == "toast-composition-only")
        ToastCompositionTests.Run(CheckComposite);
    else if (args[1] == "toast-surface-only")
        ToastSurfaceTests.Run(CheckComposite);
    else if (args[1] == "toast-controller-only")
        ToastControllerTests.Run(CheckComposite);
    else if (args[1] == "segmented-scroll-only")
        SegmentedControlScrollTests.Run(CheckComposite);
    else if (args[1] == "tab-scroll-only")
        TabScrollTests.Run(CheckComposite);
    else if (args[1] == "segmented-controlled-only")
        SegmentedControlTests.RunControlled(CheckComposite);
    else if (args[1] == "segmented-control-only")
        SegmentedControlTests.Run(CheckComposite);
    else if (args[1] == "calendar-widget-only")
        CalendarWidgetTests.Run(CheckComposite);
    else if (args[1] == "tree-label-only")
        TreeLabelTests.Run(CheckComposite);
    else if (args[1] == "tree-presentation-only")
        TreePresentationTests.Run(CheckComposite);
    else if (args[1] == "tree-virtual-selection-only")
        TreeVirtualSelectionTests.Run(CheckComposite);
    else if (args[1] == "tree-native-selection-only")
        TreeNativeSelectionTests.Run(CheckComposite);
    else if (args[1] == "selection-projection-only")
        SelectionProjectionTests.Run(CheckComposite);
    else if (args[1] == "tree-widget-only")
        TreeWidgetTests.Run(CheckComposite);
    else if (args[1] == "tree-model-only")
        TreeModelTests.Run(CheckComposite);
    else if (args[1] == "select-only")
        SelectTests.Run(CheckComposite);
    else if (args[1] == "calendar-model-only")
        CalendarModelTests.Run(CheckComposite);
    else if (args[1] == "hud-gallery-source-only")
        HeadsUpDisplayContentTests.Run(CheckComposite, galleryOnly : true);
    else if (args[1] == "hud-portal-only")
        HeadsUpDisplayPortalTests.Run(CheckComposite);
    else if (args[1] == "hud-content-only")
        HeadsUpDisplayContentTests.Run(CheckComposite);
    else if (args[1] == "activity-indicator-only")
        ActivityIndicatorTests.Run(CheckComposite);
    else if (args[1] == "progress-indicator-only")
        ProgressIndicatorTests.Run(CheckComposite);
    else if (args[1] == "shimmer-only")
        ShimmerTests.Run(CheckComposite);
    else if (args[1] == "fluent-provider-fonts-only")
        FluentProviderFontTests.Run(CheckComposite);
    else if (args[1] == "fluent-provider-retention-only")
        FluentProviderTests.RunRetention(CheckComposite);
    else if (args[1] == "fluent-provider-only")
        FluentProviderTests.Run(CheckComposite);
    else if (args[1] == "gallery-component-source-only")
        GallerySampleTests.RunComponentSources(CheckComposite, destination, args.Length == 3 ? args[2] : null);
    else if (args[1] == "gallery-legacy-source-only")
        GallerySampleTests.RunLegacySources(CheckComposite);
    else if (args[1] == "gallery-samples-only")
        GallerySampleTests.Run(CheckComposite, destination);
    else if (args[1] == "tag-picker-only")
        TagPickerTests.Run(CheckComposite);
    else if (args[1] == "card-only")
    {
        CardNudgeTests.Run(CheckComposite);
        CardWidgetTests.Run(CheckComposite);
        CardFocusTests.Run(CheckComposite);
        CardFooterTests.Run(CheckComposite);
        CardHeaderTests.Run(CheckComposite);
        CardFloatingActionTests.Run(CheckComposite);
        CardClippingTests.Run(CheckComposite);
        CardPreviewTests.Run(CheckComposite);
    }
    else if (args[1] == "paint-group-only")
    {
        PaintGroupTests.Run(CheckComposite);
        PaintGroupTextRetentionTests.Run(CheckComposite);
    }
    else if (args[1] == "backdrop-only")
        BackdropCompositeTests.Run(CheckComposite);
    else if (args[1] == "box-shadow-only")
        BoxShadowTests.Run(CheckComposite);
    else if (args[1] == "circle-cutout-only")
    {
        CircleCutoutTests.Run(CheckComposite);
        RoundedClipTests.Run(CheckComposite);
    }
    else if (args[1] == "presence-only")
        PresenceBadgeTests.Run(CheckComposite);
    else if (args[1] == "avatar-only")
        AvatarWidgetTests.Run(CheckComposite);
    else if (args[1] == "rounded-image-only")
        RoundedImageTests.Run(CheckComposite);
    else if (args[1] == "accordion-only")
        AccordionWidgetTests.Run(CheckComposite);
    else if (args[1] == "label-only")
    {
        LabelWidgetTests.Run(CheckComposite);
        InfoLabelTests.Run(CheckComposite);
    }
    else if (args[1] == "raster-lifetime-only")
        RasterLayerLifetimeTests.RunCpu(CheckComposite);
    else if (args[1] == "image-widget-only")
        ImageWidgetTests.Run(destination, CheckComposite);
    else if (args[1] == "standalone-icon-only")
        StandaloneIconTests.Run(CheckComposite);
    else if (args[1] == "legacy-contracts-only")
        LegacyCpuContractTests.Run(CheckComposite);
    else if (args[1] == "owned-sizing-only")
        OwnedChildSizingTests.Run(CheckComposite);
    else if (args[1] == "delegated-layout-only")
        DelegatedLayoutRetentionTests.Run(CheckComposite);
    else if (args[1] == "tab-measurement-only")
        TabMeasurementRetentionTests.Run(CheckComposite);
    else if (args[1] == "form-layout-only")
        FormLayoutRetentionTests.Run(CheckComposite);
    else
    {
        CompositeMeasurementContractTests.Run(CheckComposite);
        FluentCatalogTests.Run(CheckComposite);
        SpinNumberPolicyTests.Run(CheckComposite);
        SpinButtonTests.Run(CheckComposite);
        ListWidgetTests.Run(CheckComposite);
        ListLifetimeTests.Run(CheckComposite);
        GridAccessibilityTests.Run(CheckComposite);
        MarkdownDocumentTests.Run(CheckComposite);
        FontVariantTests.Run(CheckComposite);
        MarkdownWidgetTests.Run(CheckComposite);
        MarkdownPointerTests.Run(CheckComposite);
        MarkdownLinkTests.Run(CheckComposite);
        MarkdownLinkNavigationTests.Run(CheckComposite);
        MarkdownImageTests.Run(destination, CheckComposite);
        MarkdownLifetimeTests.Run(CheckComposite);
        MarkdownTableTests.Run(CheckComposite);
    }
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failed);
    if (failed.Count > 0)
        throw new InvalidOperationException("Composite measurement contract failures : " + failed.Count);
    return;
}

if (args.Length == 2 && args[1] is "toggle-accessibility-only" or "radio-accessibility-only" or "radio-compatibility-only" or "leaf-layout-only" or "content-layout-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var toggleChecks = new List<string>();
    void CheckToggle(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        toggleChecks.Add(name);
        Console.WriteLine("PASS " + name);
    }
    if (args[1] == "toggle-accessibility-only")
        ToggleAccessibilityTests.Run(CheckToggle);
    else if (args[1] == "radio-accessibility-only")
        RadioAccessibilityTests.Run(CheckToggle);
    else if (args[1] == "leaf-layout-only")
        LeafLayoutRetentionTests.Run(CheckToggle);
    else if (args[1] == "content-layout-only")
        ContentLayoutRetentionTests.Run(CheckToggle);
    else
    {
        RadioAllocationTests.Run(CheckToggle);
        RadioGroupWidgetTests.Run(CheckToggle);
    }
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], toggleChecks.Count, toggleChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "slider-range-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var rangeChecks = new List<string>();
    SliderRangeTests.Run((condition, name) =>
    {
        if (!condition)
            throw new InvalidOperationException(name);
        rangeChecks.Add(name);
        Console.WriteLine("PASS " + name);
    });
    LegacySliderRangeTests.Run((condition, name) =>
    {
        if (!condition)
            throw new InvalidOperationException(name);
        rangeChecks.Add(name);
        Console.WriteLine("PASS " + name);
    });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "slider-range-only", rangeChecks.Count, rangeChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] is "drawer-only" or "nav-only" or "legacy-gallery-only" or "observer-only" or "gallery-localization-only" or "tablist-only" or "overflow-only" or "menu-only" or "tab-overflow-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var passed = new List<string>();
    var failed = new List<string>();
    void CheckLegacy(bool condition, string name)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    }
    if (args[1] == "tab-overflow-only")
    {
        TabOverflowTests.Run(CheckLegacy);
        TabScrollTests.Run(CheckLegacy);
        FloatTransitionTests.Run(CheckLegacy);
    }
    else if (args[1] == "menu-only")
    {
        MenuTests.Run(CheckLegacy);
        FluentMenuTests.Run(CheckLegacy);
        File.WriteAllLines(Path.Combine(destination, "exposed-components.txt"), Fluent.ComponentNames.ToArray());
        File.WriteAllLines(Path.Combine(destination, "required-components.txt"), Fluent.RequiredComponentNames.ToArray());
        File.WriteAllLines(Path.Combine(destination, "missing-components.txt"), Fluent.MissingComponentNames.ToArray());
    }
    else if (args[1] == "overflow-only")
    {
        OverflowLayoutTests.Run(CheckLegacy);
        OverflowWidgetTests.Run(CheckLegacy);
        OverflowInteractionTests.Run(CheckLegacy);
    }
    else if (args[1] == "tablist-only")
    {
        TabListTests.Run(CheckLegacy);
        TabListNativeTests.Run(CheckLegacy);
        TabSelectionEventTests.Run(CheckLegacy);
        TabPanelTests.Run(CheckLegacy);
        RelationshipEventTests.Run(CheckLegacy);
        FocusEventTests.Run(CheckLegacy);
        TabRepresentationTests.Run(CheckLegacy);
        TabAppearanceReference.Run(CheckLegacy);
    }
    else if (args[1] == "drawer-only")
    {
        DrawerTests.Run(CheckLegacy);
    }
    else if (args[1] == "nav-only")
    {
        NavTests.Run(CheckLegacy);
        NavSecondaryActionTests.Run(CheckLegacy);
    }
    else if (args[1] == "gallery-localization-only")
    {
        GalleryLocalizationTests.Run(CheckLegacy);
        GalleryLocalizationTests.CaptureOverview(destination, CheckLegacy);
    }
    else if (args[1] == "observer-only")
    {
        SvgIconTests.Run(destination, CheckLegacy);
        ObserverGalleryTests.Run(CheckLegacy);
        NavTests.Run(CheckLegacy);
        NavSecondaryActionTests.Run(CheckLegacy);
        DrawerTests.Run(CheckLegacy);
        ObserverNarrowNavigationTests.Run(CheckLegacy);
        GalleryVisualTests.Run(destination, CheckLegacy);
    }
    else
    {
        TabLayoutTests.Run(CheckLegacy);
        LegacyTabAccessibilityTests.Run(CheckLegacy);
        LegacyGalleryTests.Run(CheckLegacy);
    }
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failed);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failed.Count != 0)
        throw new InvalidOperationException("Legacy gallery checks failed : " + failed.Count);
    return;
}

if (args.Length == 2 && args[1] == "multiline-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var passed = new List<string>();
    var failed = new List<string>();
    MultilineInputTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    MultilineContractTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    MultilineWrapTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    TextDragScrollTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    MultilineAccessibilityTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    FluentTextareaTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    TextEditIntegrityTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    MultilineHardLineTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    SingleLineElementTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    TextCommandTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    SingleLineDragTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    MultilineElementTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    MultilineDragTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    FluentTooltipFactoryTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    TooltipTargetTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    TooltipAccessibilityTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    TooltipEventTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    PopupPositioningTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    PopupCollisionTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    PopupCollisionTests.RunOffsets((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    PopupMeasuredTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    PopupAutoSizeTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    TooltipSurfaceTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        (condition ? passed : failed).Add(name);
    });
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failed);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "multiline-only", passed.Count, passed.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failed.Count > 0)
        throw new InvalidOperationException("Multiline checks failed : " + failed.Count);
    return;
}
if (args.Length == 2 && args[1] == "virtual-list-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var virtualChecks = new List<string>();
    var failures = new List<string>();
    void CheckVirtual(bool condition, string name)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            virtualChecks.Add(name);
        else
            failures.Add(name);
    }
    VirtualListTests.Run(CheckVirtual);
    VirtualGridTests.Run(CheckVirtual);
    DataGridTests.Run(CheckVirtual);
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "virtual-list-only", virtualChecks.Count, virtualChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Virtual list checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "scroll-view-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var scrollViewChecks = new List<string>();
    var failures = new List<string>();
    void CheckScrollView(bool condition, string name)
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            scrollViewChecks.Add(name);
        else
            failures.Add(name);
    }
    ScrollViewTests.Run(CheckScrollView);
    ScrollbarMotionTests.Run(CheckScrollView);
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "scroll-view-only", scrollViewChecks.Count, scrollViewChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Scroll view checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "scroll-routing-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var scrollChecks = new List<string>();
    var failures = new List<string>();
    ScrollRoutingTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            scrollChecks.Add(name);
        else
            failures.Add(name);
    });
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "scroll-routing-only", scrollChecks.Count, scrollChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Scroll routing checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "retained-text-maintenance")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    RetainedTextMaintenanceTests.Measure(destination);
    return;
}

if (args.Length == 2 && args[1] == "build-context-memory")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    ContainerWidgetTests.MeasureBuildContextStorage(destination);
    return;
}

if (args.Length == 2 && args[1] == "single-child-memory")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    SingleChildLayoutTests.MeasureMemory(destination);
    return;
}

if (args.Length == 2 && args[1] == "constrained-leaves-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var leafChecks = new List<string>();
    LeafConstraintTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); leafChecks.Add(name); Console.WriteLine("PASS " + name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "constrained-leaves-only", leafChecks.Count, leafChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "input-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var inputChecks = new List<string>();
    InputAllocationTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); inputChecks.Add(name); Console.WriteLine("PASS " + name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "input-allocation-only", inputChecks.Count, inputChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "allocation-field-audit")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var observed = AllocationRoutingTests.InspectField();
    var lines = new[] { "Diagnostic only; not an acceptance test.", "Measured parent = " + observed.Measured, "Actual Field root = " + observed.Actual };
    File.WriteAllLines(Path.Combine(destination, "field-observation.txt"), lines);
    foreach (var line in lines)
        Console.WriteLine(line);
    return;
}

if (args.Length == 2 && args[1] == "field-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var fieldAllocationChecks = new List<string>();
    var failures = new List<string>();
    FieldAllocationTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            fieldAllocationChecks.Add(name);
        else
            failures.Add(name);
    });
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "field-allocation-only", fieldAllocationChecks.Count,
        fieldAllocationChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Field allocation checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "allocation-routing-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var routingChecks = new List<string>();
    var failures = new List<string>();
    AllocationRoutingTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            routingChecks.Add(name);
        else
            failures.Add(name);
    });
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "allocation-routing-only", routingChecks.Count,
        routingChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Allocation routing checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "badge-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var badgeAllocationChecks = new List<string>();
    var failures = new List<string>();
    BadgeAllocationTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            badgeAllocationChecks.Add(name);
        else
            failures.Add(name);
    });
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "badge-allocation-only", badgeAllocationChecks.Count,
        badgeAllocationChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Badge allocation checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "link-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var linkChecks = new List<string>();
    var failures = new List<string>();
    LinkAllocationTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            linkChecks.Add(name);
        else
            failures.Add(name);
    });
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "link-allocation-only", linkChecks.Count,
        linkChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Link allocation checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "divider-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var dividerChecks = new List<string>();
    var failures = new List<string>();
    DividerAllocationTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            dividerChecks.Add(name);
        else
            failures.Add(name);
    });
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "divider-allocation-only", dividerChecks.Count,
        dividerChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Divider allocation checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "text-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var textChecks = new List<string>();
    TextAllocationTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (!condition)
            throw new InvalidOperationException(name);
        textChecks.Add(name);
    });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "text-allocation-only", textChecks.Count,
        textChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "progress-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var progressChecks = new List<string>();
    var failures = new List<string>();
    ProgressAllocationTests.Run((condition, name) =>
    {
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
        if (condition)
            progressChecks.Add(name);
        else
            failures.Add(name);
    });
    File.WriteAllLines(Path.Combine(destination, "failures.txt"), failures);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "progress-allocation-only", progressChecks.Count,
        progressChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    if (failures.Count != 0)
        throw new InvalidOperationException("Progress allocation checks failed : " + failures.Count);
    return;
}

if (args.Length == 2 && args[1] == "spinner-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var spinnerChecks = new List<string>();
    SpinnerAllocationTests.Run((condition, name) => { Console.WriteLine((condition ? "PASS " : "FAIL ") + name); if (!condition) throw new InvalidOperationException(name); spinnerChecks.Add(name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "spinner-allocation-only", spinnerChecks.Count, spinnerChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "radio-allocation-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var radioChecks = new List<string>();
    RadioAllocationTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); radioChecks.Add(name); Console.WriteLine("PASS " + name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "radio-allocation-only", radioChecks.Count, radioChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "build-dependencies-memory")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    BuildDependencyMemoryTests.Measure(destination);
    return;
}
if (args.Length == 2 && args[1] == "widget-lifetime-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var lifetimeChecks = new List<string>();
    void Verify(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        lifetimeChecks.Add(name);
        Console.WriteLine("PASS " + name);
    }
    WidgetTreeThemeTests.Run(Verify);
    ControlOwnershipTests.Run(Verify);
    ButtonLifetimeTests.Run(Verify);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "widget-lifetime-only", lifetimeChecks.Count, lifetimeChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "layout-retention")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    LayoutRetentionTests.Measure(destination);
    return;
}

if (args.Length == 2 && args[1] == "leaf-layout-measurement")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    LeafLayoutRetentionTests.Measure(destination);
    return;
}

if (args.Length == 2 && args[1] == "containers-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var containerChecks = new List<string>();
    ContainerWidgetTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); containerChecks.Add(name); Console.WriteLine("PASS " + name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "containers-only", containerChecks.Count, containerChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "session-failure-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var failureChecks = new List<string>();
    RenderFailureSessionTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); failureChecks.Add(name); Console.WriteLine("PASS " + name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "session-failure-only", failureChecks.Count, failureChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] == "paragraph-geometry")
{
    ParagraphGeometrySnapshot.Write(Path.GetFullPath(args[0]));
    return;
}

if (args.Length >= 2 && args[1] == "gallery-resize")
{
    var resizeOutput = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(resizeOutput);
    using var gallery = new Duxel.Gallery.ObserverScreen(Duxel.Gallery.ObserverServices.CreateWindows())
    {
        SelectedPage = "경로",
        FreezeDiagnosticText = true
    };
    var resizeContent = new GalleryResizeContent(gallery);
    ResizeInteractionTests.Run((condition, name) =>
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
    }, 2, resizeOutput, cycles: 3, delayMs: 8, systemTitle: true, synchronous: true,
        diagnostics: false, content: resizeContent, vsync: false, contentReady: () => resizeContent.Ready);
    return;
}

if (args.Length is 3 or 4 or 5 && args[1] is "resize-stress" or "resize-system-stress")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var resizeChecks = new List<string>();
    if (args.Length == 5 && args[4] != "no-diagnostics")
        throw new ArgumentException("Expected no-diagnostics as the final resize option.");
    ResizeInteractionTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); resizeChecks.Add(name); Console.WriteLine("PASS " + name); }, int.Parse(args[2]), destination, args.Length >= 4 ? int.Parse(args[3]) : 30, 0, args[1] == "resize-system-stress", true, args.Length != 5);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, args[1], resizeChecks.Count, resizeChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 3 && args[1] == "scheduling-session")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var sessionChecks = new List<string>();
    FrameSchedulingSessionTests.Run(args[2], (condition, name) => { if (!condition) throw new InvalidOperationException(name); sessionChecks.Add(name); Console.WriteLine("PASS " + name); }, destination);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "scheduling-session-" + args[2], sessionChecks.Count, sessionChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "scheduling-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var schedulingChecks = new List<string>();
    FrameSchedulingTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); schedulingChecks.Add(name); Console.WriteLine("PASS " + name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "scheduling-only", schedulingChecks.Count, schedulingChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "spinner-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var spinnerChecks = new List<string>();
    SpinnerTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); spinnerChecks.Add(name); Console.WriteLine("PASS " + name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "spinner-only", spinnerChecks.Count, spinnerChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] is "accessibility-only" or "uia-client-pid" or "uia-field-client-pid"
    or "uia-lifetime-host" or "uia-lifetime-client-pid" or "uia-spinner-client-pid" or "uia-tooltip-client-pid" or "uia-skeleton-host" or "uia-announcement-host" or "uia-messagebar-host"
    or "uia-tooltip-events-host" or "uia-tooltip-events-client-pid" or "tooltip-host-visibility"
    or "uia-tooltip-host-visibility-client-pid" or "uia-tooltip-properties-host" or "uia-tooltip-properties-client-pid"
    or "uia-tooltip-shutdown-host" or "uia-tooltip-shutdown-client-pid" or "uia-tooltip-shutdown-cleanup-client-pid"
    or "uia-tooltip-scroll-host" or "uia-tooltip-scroll-client-pid" or "uia-tab-selection-host" or "uia-tab-selection-client-pid"
    or "uia-relationship-host" or "uia-relationship-client-pid" or "uia-focus-host" or "uia-focus-client-pid"
    or "uia-overflow-host" or "uia-overflow-client-pid" or "uia-menu-host" or "uia-menu-client-pid" or "uia-menu-fluent-host"
    or "uia-tab-overflow-host" or "uia-tab-overflow-client-pid" or "uia-menu-scroll-host" or "uia-menu-scroll-client-pid" or "uia-menu-expansion-client-pid"
    or "uia-menu-checkbox-host" or "uia-menu-checkbox-client-pid" or "uia-menu-radio-host" or "uia-menu-radio-client-pid"
    or "uia-menu-content-host" or "uia-menu-content-client-pid" or "uia-slider-range-host" or "uia-slider-range-client-pid"
    or "uia-slider-retirement-host" or "uia-slider-retirement-client-pid" or "uia-slider-legacy-host" or "uia-legacy-tab-host" or "uia-toggle-controls-host" or "uia-radio-controls-host" or "uia-label-host" or "uia-accordion-host" or "uia-popover-host" or "uia-dialog-host" or "uia-popup-input-host" or "uia-toolbar-host" or "uia-searchbox-host" or "uia-persona-host" or "uia-avatar-host" or "uia-presence-host" or "uia-card-host" or "uia-card-floating-host" or "uia-breadcrumb-host" or "uia-avatar-group-host" or "uia-dropdown-host" or "uia-combobox-host" or "uia-list-host" or "uia-list-retirement-host" or "uia-markdown-host")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var accessibilityChecks = new List<string>();
    void Verify(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        accessibilityChecks.Add(name);
        Console.WriteLine("PASS " + name);
    }
    if (args[1] == "accessibility-only")
        AccessibilityTests.Run(Verify);
    else if (args[1] == "uia-combobox-host")
        ComboboxAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-dropdown-host")
        DropdownAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-avatar-group-host")
        AvatarGroupAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-breadcrumb-host")
        BreadcrumbAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-card-host")
        CardAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-card-floating-host")
        CardAccessibilityNativeHost.Run(Verify, floatingAction: true);
    else if (args[1] == "uia-presence-host")
        PresenceAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-messagebar-host")
        AnnouncementAccessibilityNativeHost.RunMessageBars(Verify);
    else if (args[1] == "uia-announcement-host")
        AnnouncementAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-skeleton-host")
        SkeletonAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-dialog-host")
        DialogAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-popover-host")
        PopoverAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-popup-input-host")
        PopupInputAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-toolbar-host")
        ToolbarAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-markdown-host")
        MarkdownAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-searchbox-host")
        SearchBoxAccessibilityNativeHost.Run(Verify);
    else if (args[1] is "uia-list-host" or "uia-list-retirement-host" or "uia-markdown-host")
        ListAccessibilityNativeHost.Run(Verify, retirementOnly: args[1] == "uia-list-retirement-host");
    else if (args[1] == "uia-persona-host")
        PersonaAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-avatar-host")
        AvatarAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-accordion-host")
        AccordionAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-label-host")
        LabelAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-lifetime-host")
        AccessibilityLifetimeHost.Run(Verify);
    else if (args[1] == "uia-overflow-host")
        OverflowLifetimeHost.Run(Verify);
    else if (args[1] == "uia-menu-host")
        MenuNativeHost.Run(Verify);
    else if (args[1] == "uia-menu-fluent-host")
        MenuNativeHost.RunFluent(Verify);
    else if (args[1] == "uia-menu-scroll-host")
        MenuNativeHost.RunScroll(Verify);
    else if (args[1] == "uia-menu-checkbox-host")
        MenuNativeHost.RunCheckbox(Verify);
    else if (args[1] == "uia-menu-radio-host")
        MenuNativeHost.RunRadio(Verify);
    else if (args[1] == "uia-menu-content-host")
        MenuNativeHost.RunContent(Verify);
    else if (args[1] == "uia-slider-range-host")
        SliderRangeNativeHost.Run(Verify);
    else if (args[1] == "uia-slider-legacy-host")
        LegacySliderRangeNativeHost.Run(Verify);
    else if (args[1] == "uia-slider-retirement-host")
    {
        if (args.Length != 3)
            throw new ArgumentException("The retirement probe requires its coordination directory.");
        SliderRangeNativeHost.RunRetirementProbe(Verify, args[2]);
    }
    else if (args[1] == "uia-tab-overflow-host")
        TabOverflowNativeHost.Run(Verify);
    else if (args[1] == "uia-tab-selection-host")
        TabSelectionEventsHost.Run(Verify);
    else if (args[1] == "uia-legacy-tab-host")
        LegacyTabNativeHost.Run(Verify);
    else if (args[1] == "uia-toggle-controls-host")
        ToggleAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-radio-controls-host")
        RadioAccessibilityNativeHost.Run(Verify);
    else if (args[1] == "uia-relationship-host")
        RelationshipEventsHost.Run(Verify);
    else if (args[1] == "uia-focus-host")
    {
        if (args.Length != 3)
            throw new ArgumentException("The focus fixture needs its owned coordination directory.");
        FocusEventsHost.Run(Verify, args[2]);
    }
    else if (args[1] is "uia-tooltip-events-host" or "uia-tooltip-properties-host" or "uia-tooltip-scroll-host")
    {
        TooltipEventsHost.Run(Verify, textProperties: args[1] == "uia-tooltip-properties-host",
            scrollPattern: args[1] == "uia-tooltip-scroll-host");
    }
    else if (args[1] is "tooltip-host-visibility" or "uia-tooltip-shutdown-host")
    {
        var shutdown = args[1] == "uia-tooltip-shutdown-host";
        if (shutdown && args.Length != 3)
        {
            throw new ArgumentException("The shutdown fixture needs a coordination directory.");
        }
        TooltipEventsHost.RunHostVisibility(Verify, args.Length > 2 ? args[2] : null, shutdown);
    }
    else
    {
        var cleanup = args[1] == "uia-tooltip-shutdown-cleanup-client-pid";
        var shutdown = args[1] == "uia-tooltip-shutdown-client-pid";
        var hostVisibility = args[1] == "uia-tooltip-host-visibility-client-pid" || shutdown || cleanup;
        if (args.Length != (hostVisibility || args[1] is "uia-focus-client-pid" or "uia-slider-retirement-client-pid" ? 4 : 3))
        {
            throw new ArgumentException("UIA client requires the owned target process ID and the visibility fixture's coordination directory when applicable.");
        }
        var window = AccessibilityClientTests.WindowForProcess(int.Parse(args[2]), args[1] == "uia-focus-client-pid" ? FocusEventsHost.Title : null);
        if (window == 0)
            throw new InvalidOperationException("Test target has no native top-level window.");
        AccessibilityClientTests.Run(window, Verify, args[1] == "uia-field-client-pid", args[1] is "uia-lifetime-client-pid" or "uia-overflow-client-pid",
            args[1] == "uia-spinner-client-pid", args[1] == "uia-tooltip-client-pid", args[1] == "uia-tooltip-events-client-pid",
            hostVisibility ? args[3] : null, tooltipProperties: args[1] == "uia-tooltip-properties-client-pid",
            tooltipShutdown: shutdown || cleanup, requireShutdownEvent: !cleanup,
            tooltipScroll: args[1] == "uia-tooltip-scroll-client-pid", tabSelection: args[1] == "uia-tab-selection-client-pid",
            relationshipEvents: args[1] == "uia-relationship-client-pid", focusEvents: args[1] == "uia-focus-client-pid" ? args[3] : null,
            menu: args[1] == "uia-menu-client-pid", tabOverflow: args[1] == "uia-tab-overflow-client-pid",
            menuScroll: args[1] == "uia-menu-scroll-client-pid", menuExpansion: args[1] == "uia-menu-expansion-client-pid",
            menuCheckbox: args[1] == "uia-menu-checkbox-client-pid", menuRadio: args[1] == "uia-menu-radio-client-pid",
            menuContent: args[1] == "uia-menu-content-client-pid", sliderRange: args[1] == "uia-slider-range-client-pid",
            rangeProbeDirectory: args[1] == "uia-slider-retirement-client-pid" ? args[3] : null);
    }
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, args[1], accessibilityChecks.Count, accessibilityChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length == 2 && args[1] == "field-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var fieldChecks = new List<string>();
    FieldWidgetTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); fieldChecks.Add(name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(RuntimeInformation.FrameworkDescription, "field-only", fieldChecks.Count, fieldChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    Console.WriteLine($"Passed {fieldChecks.Count} Field checks.");
    return;
}
if (args.Length == 2 && args[1] == "badge-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var badgeChecks = new List<string>();
    BadgeWidgetTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); badgeChecks.Add(name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "badge-only", badgeChecks.Count, badgeChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    Console.WriteLine($"Passed {badgeChecks.Count} badge checks.");
    return;
}
if (args.Length == 2 && args[1] == "text-widget-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var textChecks = new List<string>();
    TextWidgetTests.Run((condition, name) => { if (!condition) throw new InvalidOperationException(name); textChecks.Add(name); });
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "text-widget-only", textChecks.Count, textChecks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length == 2 && args[1] == "catalog-only")
{
    var destination = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(destination);
    var catalog = new CatalogResult(RuntimeInformation.FrameworkDescription, Fluent.ComponentNames.ToArray(),
        Fluent.RequiredComponentNames.ToArray(), Fluent.MissingComponentNames.ToArray(), Fluent.IsCatalogScopeComplete);
    File.WriteAllText(Path.Combine(destination, "result.json"), JsonSerializer.Serialize(catalog, GraphicsJson.Default.CatalogResult));
    Console.WriteLine($"Catalog exposed={catalog.Exposed.Length},required={catalog.Required.Length},missing={catalog.Missing.Length},complete={catalog.ScopeComplete}");
    return;
}

if (args.Length == 3 && args[1] is "particle-replay" or "particle-bench")
{
    GpuParticleReplayTests.Run(args[0], args[2], args[1] == "particle-bench");
    return;
}

if (args.Length is < 1 or > 5 || args.Length >= 2 && args[1] is not ("presentation-reuse" or "gpu-vector-cache" or "gpu-svg-camera-present-bench" or "gpu-svg-camera-bench" or "gpu-stroke-domains" or "gpu-transparent-stroke-domains" or "gpu-transparent-layer-origin-tiled" or "gpu-layer-origin" or "gpu-transparent-layer-origin" or "gpu-clip-storage" or "gpu-transparent-clip-storage" or "gpu-shadow-pixels" or "gpu-transparent-shadow" or "gpu-boundary-hardware-local" or "gpu-boundary-hardware-approx" or "gpu-boundary-hardware" or "gpu-vector-readback" or "gpu-transparent-browser" or "gpu-transparent-gradients" or "gpu-transparent-opacity" or "gpu-transparent-color" or "gpu-color-storage" or "gpu-opacity-pixels" or "gpu-gradient-pixels" or "gpu-blend-backdrops" or "gpu-arc-geometry" or "gpu-transparent-arc-geometry" or "gpu-arc-opaque-target" or "gpu-svg-browser" or "gpu-svg-image-browser" or "gpu-svg-opaque-target-browser" or "gpu-dash-pixels" or "gpu-stroke-pixels" or "svg-font" or "gpu-svg-pixels" or "gpu-svg-camera" or "gpu-svg-zoom" or "gpu-vector" or "tooltip" or "multiline" or "packed-first" or "resize-input" or "resize-coherence" or "resize-capabilities" or "resize-capacity" or "particle-full" or "input-kernel" or "fluent-input" or "input-allocation" or "radio-allocation" or "radio-allocation-matched" or "spinner-allocation" or "progress-allocation" or "text-allocation" or "divider-allocation" or "link-allocation" or "badge-allocation" or "allocation-routing" or "field-allocation" or "field" or "measure" or "spinner" or "animation-visibility" or "containers" or "constrained-leaves" or "leaf-layout" or "content-layout" or "form-layout" or "tab-measurement" or "delegated-layout" or "owned-sizing" or "directtext-cache" or "primitive-commands" or "observer-gallery" or "observer-rendering" or "tablist" or "overflow" or "menu" or "tab-overflow" or "scrollbar-motion" or "fluent-provider-font-gpu" or "progress-indicator-motion-gpu" or "hud-portal-gpu" or "hud-content-gpu" or "activity-indicator-gpu" or "progress-indicator-gpu" or "shimmer-gpu" or "fluent-provider-gpu" or "tab-panel-boundary" or "slider-state" or "svg" or "observer-svg" or "widgets-no-resize" or "raster-lifetime" or "label" or "markdown" or "markdown-table" or "list" or "spinbutton" or "accordion" or "widget-transform" or "repaint" or "rounded-image" or "rounded-image-lifetime" or "image-shader-bench" or "avatar" or "observer-avatar" or "observer-widget-transform" or "observer-repaint" or "observer-measure-bench" or "presence" or "circle-cutout" or "box-shadow" or "backdrop" or "paint-group" or "card" or "breadcrumb" or "avatar-group" or "dropdown" or "combobox" or "tag" or "interaction-tag" or "persona" or "searchbox" or "toolbar" or "popup-input" or "dialog" or "popover"))
    if (args is not [_, "raster-text-scroll", _])
        throw new ArgumentException("Expected output directory, optional packed-first and optional frame resource count.");
var frameSlots = args.Length >= 3 ? int.Parse(args[2]) : 2;
foreach (var option in args.Skip(3))
    if (option is not ("core-present" or "msaa4")
        && !(args[1] is "box-shadow" or "backdrop" or "circle-cutout" && (option is "expect-shared-camera" or "expect-shared-basis" or "expect-generic"))
        && !(args[1] == "image-shader-bench" && option is "circle-mask" or "color-image" or "rounded-mask" or "projected-clip"))
        throw new ArgumentException("Unknown GPU validation option.");
if (args.Skip(3).Count(option => option.StartsWith("expect-", StringComparison.Ordinal)) > 1)
    throw new ArgumentException("Only one expected compiled family is allowed.");
var shadowFamilyExpectation = args.Skip(3).FirstOrDefault(option => option.StartsWith("expect-", StringComparison.Ordinal));
var shadowConfiguredFamily = Environment.GetEnvironmentVariable("DUXEL_VK_SHARED_LAYER_CAMERA") != "0" ? "expect-shared-camera"
    : Environment.GetEnvironmentVariable("DUXEL_VK_SHARED_LAYER_BASIS") != "0" ? "expect-shared-basis" : "expect-generic";
if (shadowFamilyExpectation is not null && shadowFamilyExpectation != shadowConfiguredFamily)
    throw new ArgumentException("GPU_VALIDATION_PIPELINE_FAMILY_MISMATCH : expected option differs from the configured renderer family.");
VulkanRendererBackend.ValidationImageAcquisitionOnly = args.Skip(3).Contains("core-present");
var rootMsaa4 = args.Skip(3).Contains("msaa4");
var widgetsWithoutResize = args.Length >= 2 && args[1] == "widgets-no-resize";
if (widgetsWithoutResize && rootMsaa4)
    throw new ArgumentException("widgets-no-resize cannot enable the deferred native resize/MSAA-capacity option.");
ButtonRenderingTests.ValidateReferences();
ButtonAppearanceTests.ValidateReferences();
FluentIconRenderingTests.ValidateAssets();
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
if (args.Length >= 3 && args[1] == "resize-input")
{
    var passed = 0;
    ResizeInteractionTests.Run((condition, label) => { if (!condition) throw new InvalidOperationException(label); passed++; Console.WriteLine(label); }, frameSlots, output);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "resize-input", passed, ["native sizing and post-resize button input; frame slots=" + frameSlots], 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE", "1");
Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", "100000");
if (args.Length >= 2 && args[1] == "presentation-reuse")
{
    PresentationReuseTests.Run(output, frameSlots);
    return;
}

if (args.Length >= 2 && args[1] is "gpu-svg-camera-present-bench" or "gpu-svg-camera-bench" or "image-shader-bench" or "repaint")
    Environment.SetEnvironmentVariable("DUXEL_VK_GPU_PROFILE", "1");
var browserGroup = args.Length >= 2 ? GpuSvgBrowserTests.ReferenceGroup(args[1]) : null;
var transparentBrowser = args.Length >= 2 && args[1].StartsWith("gpu-transparent-", StringComparison.Ordinal);
var browserReferences = browserGroup is not null ? GpuSvgBrowserTests.LoadReferences(browserGroup, transparentBrowser, imageOnly: args[1] is "gpu-svg-image-browser" or "gpu-svg-opaque-target-browser") : null;
var boundaryApprox = args.Length >= 2 && args[1] == "gpu-boundary-hardware-approx";
var boundaryLocal = args.Length >= 2 && args[1] == "gpu-boundary-hardware-local";
var boundaryHardware = boundaryLocal || boundaryApprox || args.Length >= 2 && args[1] == "gpu-boundary-hardware";
var boundaryReference = boundaryHardware ? GpuBoundaryHardwareTests.LoadReference() : null;
var matchedRadio = args.Length >= 2 && args[1] == "radio-allocation-matched";
var matchedRadioSize = new PlatformSize(400, 300);
var platformOptions = new WindowsPlatformBackendOptions
{
    Width = boundaryApprox ? 819 : boundaryHardware ? 611 : args.Length >= 2 && args[1] == "observer-measure-bench" ? 1600 : args.Length >= 2 && args[1] is "markdown" or "markdown-table" ? 760 : args.Length >= 2 && args[1] is "observer-gallery" or "observer-rendering" or "observer-svg" or "observer-avatar" or "observer-widget-transform" or "observer-repaint" or "observer-measure-bench" ? 1280 : args.Length >= 2 && args[1] is "leaf-layout" or "content-layout" or "form-layout" or "tab-measurement" or "delegated-layout" or "owned-sizing" ? 640 : 320,
    Height = boundaryApprox ? 410 : boundaryHardware ? 298 : args.Length >= 2 && args[1] == "observer-measure-bench" ? 1166 : args.Length >= 2 && args[1] is "markdown" or "markdown-table" ? 480 : args.Length >= 2 && args[1] is "observer-gallery" or "observer-rendering" or "observer-svg" or "observer-avatar" or "observer-widget-transform" or "observer-repaint" or "observer-measure-bench" ? 900 : args.Length >= 2 && args[1] is "leaf-layout" or "content-layout" or "form-layout" or "tab-measurement" or "delegated-layout" or "owned-sizing" ? 480 : 240,
    MinWidth = 100,
    MinHeight = 100,
    Title = "Duxel graphics validation",
    VSync = false,
    KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
    TrayOptions = new()
};
if (matchedRadio)
{
    platformOptions = platformOptions with
    {
        Width = matchedRadioSize.Width,
        Height = matchedRadioSize.Height
    };
}
using var platform = matchedRadio || args.Length >= 2 && args[1] == "primitive-commands"
    ? WindowsPlatformBackend.CreatePixelSizedValidationWindow(platformOptions)
    : new WindowsPlatformBackend(platformOptions);
if (matchedRadio && platform.FramebufferSize != matchedRadioSize)
    throw new InvalidOperationException($"The matched RadioGroup fixture requires {matchedRadioSize}, actual {platform.FramebufferSize}.");
if (boundaryHardware && platform.FramebufferSize != (boundaryApprox ? new Duxel.Core.PlatformSize(1024, 512) : new Duxel.Core.PlatformSize(764, 372)))
    throw new InvalidOperationException("BOUNDARY_HARDWARE_FRAMEBUFFER_CONTRACT_REQUIRES_125_PERCENT_DPI");
using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(2, EnableVSync: false, MsaaSamples: boundaryHardware ? 4 : 1, MaxFramesInFlight: frameSlots));
renderer.CreateDeviceObjects();
var size = platform.FramebufferSize;
var white = new UiTextureId(2);
var bounds = new UiRect(0, 0, size.Width, size.Height);
using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), white);
var input = new UiInputState(default, false, false, false, false, false, false, 0, 0, [], [], default, default);
var upload = true;
var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException(name);
    checks.Add(name);
}
float? captureDeltaSeconds = null;
var capturePixelSnap = true;
var captureTextScale = 1f;
Action<int>? captureMissingGlyphObserver = null;
var repaintDiagnosticCleanup = false;
UiDrawData Build(Action<UiImmediateContext> draw)
{
    platform.PollEvents();
    var deltaSeconds = captureDeltaSeconds ?? (args.Length >= 2 && args[1] is "observer-gallery" or "observer-rendering" or "observer-svg" or "observer-avatar" or "observer-widget-transform" or "observer-repaint" or "observer-measure-bench" ? 0f : 1f / 60);
    context.NewFrame(new UiFrameInfo(deltaSeconds, new(size.Width, size.Height), new(1, 1))
    {
        RepaintDiagnosticCleanup = repaintDiagnosticCleanup
    }, input, bounds, UiTextSettings.Default with
    {
        PixelSnap = capturePixelSnap,
        Scale = captureTextScale,
        MissingGlyphObserver = captureMissingGlyphObserver
    });
    if (upload)
    {
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, white, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 }));
        upload = false;
    }
    context.Render(new Screen(draw));
    return context.GetDrawData();
}
byte[] Capture(string name, Action<UiImmediateContext> draw)
{
    using var data = Build(draw);
    if (args.Length >= 2 && args[1] == "observer-repaint")
        File.WriteAllLines(Path.Combine(output, name + ".commands.txt"), data.DrawLists.SelectMany(list =>
            new[] { $"list : {list.StaticGeometryKey} / {list.GetStaticGeometryStamp()} / {list.Scene3D}" }
                .Concat(list.Commands.Select(command => command.ToString()))
                .Append("vertices : " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(list.Vertices.AsSpan()))))));
    var pixels = renderer.RenderAndReadback(data);
    context.State.RendererFrameMetrics = renderer.LastFrameMetrics;
    using var file = File.Create(Path.Combine(output, name + ".ppm"));
    file.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{size.Width} {size.Height}\n255\n"));
    for (var i = 0; i < pixels.Length; i += 4)
        file.Write(pixels.AsSpan(i, 3));
    return pixels;
}
byte[] CaptureGeometry(string name, Action<UiImmediateContext> draw)
{
    var previous = captureDeltaSeconds;
    captureDeltaSeconds = 0;
    try
    {
        return Capture(name, draw);
    }
    finally
    {
        captureDeltaSeconds = previous;
    }
}
if (args.Length >= 2 && args[1] == "resize-capabilities")
{
    var capabilities = renderer.ValidationPresentationScaling();
    File.WriteAllText(Path.Combine(output, "presentation-capabilities.txt"), capabilities);
    Console.WriteLine(capabilities);
    return;
}
Capture("initial", static _ => { });
if (boundaryHardware)
{
    GpuBoundaryHardwareTests.Run(output, Capture, Check, () => renderer.LastFrameMetrics, boundaryReference!, size.Width, size.Height, boundaryLocal ? new(215, 303) : default);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "gpu-vector-readback")
{
    GpuVectorReadbackTests.Run(context, renderer, Build, Capture, Check, size.Width);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "gpu-vector-readback", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "image-shader-bench")
{
    ImageShaderBenchmark.Run(output, renderer, Build, white, bounds,
        circleMask: args.Skip(3).Contains("circle-mask"),
        colorImages: args.Skip(3).Contains("color-image"),
        roundedMask: args.Skip(3).Contains("rounded-mask"),
        projectedClip: args.Skip(3).Contains("projected-clip"));
    return;
}
if (args.Length >= 2 && args[1] == "paint-group")
{
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        PaintGroupRenderingTests.Run(context, (name, draw) => Capture($"msaa{samples}-group-{name}", draw),
            Check, Build, renderer.RenderAndReadback, renderer.TryRenderDrawData, () => renderer.ValidationPaintGeometryEntries, size.Width, white, () => renderer.LastFrameMetrics, renderer.ValidationSwapchainImageCount);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "paint-group", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "backdrop")
{
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        BackdropCompositeRenderingTests.Run((name, draw) => Capture($"msaa{samples}-backdrop-{name}", draw),
            Check, size.Width, () => renderer.LastFrameMetrics, renderer.ValidationSwapchainImageCount,
            shadowFamilyExpectation ?? shadowConfiguredFamily);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "backdrop", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "circle-cutout")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        CircleCutoutRenderingTests.Run((name, draw) => Capture($"msaa{samples}-cutout-{name}", draw), Check, size.Width, white,
            () => renderer.LastFrameMetrics, renderer.ValidationSwapchainImageCount);
        RoundedClipRenderingTests.Run((name, draw) => Capture($"msaa{samples}-clip-{name}", draw), Check, size.Width, white,
            () => renderer.LastFrameMetrics, renderer.ValidationSwapchainImageCount, shadowFamilyExpectation ?? shadowConfiguredFamily);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "circle-cutout", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "raster-text-scroll")
{
    ControlFontFixture.Configure(context);
    context.SetDirectTextBaseFontSize(16);
    RasterTextScrollRenderingTests.Run(renderer, Capture, Check, size.Width, size.Height,
        value => capturePixelSnap = value, value => captureTextScale = value);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "raster-text-scroll", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "repaint")
{
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        RepaintDiagnosticsTests.Run((name, draw) => Capture($"msaa{samples}-repaint-{name}", draw), Check, white, size,
            () => renderer.LastFrameMetrics, cleanup => repaintDiagnosticCleanup = cleanup,
            () => renderer.ValidationFullRepaint = true, () => renderer.ValidationRepaintBorderPixels);
    }
    renderer.SetMsaaSamples(1);
    var textOptions = new Duxel.App.DuxelAppOptions { Screen = new Screen(static _ => { }) };
    context.SetPlatformTextBackend(platform.TextBackend);
    context.SetTypographyFonts(textOptions.Font.Typography);
    context.SetDirectTextPrimaryFontPath(textOptions.Font.PrimaryFontPath);
    context.SetDirectTextSecondaryFontPath(textOptions.Font.SecondaryFontPath);
    context.SetDirectTextBaseFontSize(16);
    context.SetDirectTextEnabled(true);
    context.SetDirectTextFallbackEnabled(false);
    context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
    SwitchAlignmentRenderingTests.Run(renderer, Capture, Check, size.Width, size.Height);
    TextUpdateDiagnostic.Run(output, renderer, Build);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "repaint", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "svg-font")
{
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        SvgFontRenderingTests.Run((name, draw) => Capture($"msaa{samples}-svg-font-{name}", draw),
            Check, size.Width, output, samples);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (browserReferences is not null)
{
    GpuSvgBrowserTests.Run(output, context, Capture, Check, size.Width, browserReferences!,
        transparentBrowser ? "Full-resolution GPU premultiplied RGBA8 storage; exact alpha; browser straight RGBA converted to premultiplied; no framebuffer composite or CPU raster"
            : args[1] == "gpu-arc-opaque-target" ? "Six independent arc/cubic/line fixtures; white target initialized before multisample resolve, no tolerance" : args[1] == "gpu-arc-geometry" ? "Six independent 64x64 arc/cubic/line fixtures; complete opaque browser RGBA, no tolerance" : args[1] == "gpu-blend-backdrops" ? "Twelve independent 64x64 gradient fixtures over authored opaque backgrounds; all RGBA channels, no tolerance" : args[1] == "gpu-gradient-pixels" ? "Independent full-resolution small gradient fixtures on white" : args[1] == "gpu-opacity-pixels" ? "Independent full-resolution opacity group fixtures on white" : args[1] == "gpu-color-storage" ? "Independent full-resolution shader-color storage SVG fixtures on white" : "Full original browser RGBA on white",
        transparentBrowser ? (_, draw, texture, width, height) =>
        {
            using var data = Build(draw);
            return renderer.RenderVectorAndReadback(data, texture, width, height);
        }
    : null,
        tileWidthLimit: args[1] == "gpu-transparent-layer-origin-tiled" ? 17 : 256,
        tileHeightLimit: args[1] == "gpu-transparent-layer-origin-tiled" ? 19 : 128,
        vectorBackground: args[1] is "gpu-svg-opaque-target-browser" or "gpu-arc-opaque-target" ? new(255, 255, 255) : default);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "gpu-svg-pixels" or "gpu-stroke-pixels" or "gpu-dash-pixels")
{
    GpuSvgPixelTests.Run(output, context, Capture, Check, size.Width, args[1] == "gpu-stroke-pixels", args[1] == "gpu-dash-pixels");
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "gpu-vector-cache")
{
    GpuVectorWorkCacheTests.Run(output, context, renderer, Build, Check, frameSlots);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "gpu-vector-cache", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "gpu-vector")
{
    GpuVectorSceneTests.Run(output, context, renderer, Capture, Check, size.Width);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "gpu-vector", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "gpu-svg-camera-bench" or "gpu-svg-camera-present-bench")
{
    GpuSvgCameraBenchmark.Run(output, context, renderer, Build, readback: args[1] == "gpu-svg-camera-bench");
    return;
}
if (args.Length >= 2 && args[1] is "gpu-svg-zoom" or "gpu-svg-camera")
{
    GpuSvgZoomTests.Run(output, context, renderer, Capture, Check, size.Width, size.Height, args[1] == "gpu-svg-camera");
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "widget-transform")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        WidgetTransformRenderingTests.Run((name, draw) => Capture($"msaa{samples}-widget-transform-{name}", draw), Check,
            size.Width, white, () => renderer.LastFrameMetrics);
        AffineClipDamageTests.Run(renderer.RenderAndReadback, () => renderer.LastFrameMetrics, Check, white, size.Width, size.Height);
        WidgetTransformPortalRenderingTests.Run((name, draw) => Capture($"msaa{samples}-portal-{name}", draw), Check, size.Width);
        LegacyWidgetPortalRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, size.Width);
        CameraPopupRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, size.Width);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "widget-transform", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "rounded-image")
{
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        RoundedImageRenderingTests.Run((name, draw) => Capture($"msaa{samples}-rounded-image-{name}", draw), Check, size.Width,
              () => (renderer.LastFrameMetrics.CompiledLayerDraws, renderer.LastFrameMetrics.CompiledLayerCommands), white);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "rounded-image", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "presence")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            PresenceBadgeRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-presence-{name}", draw), Check, size.Width,
                () => renderer.LastFrameMetrics, renderer.ValidationSwapchainImageCount);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "presence", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "box-shadow")
{
    var expectedFamily = shadowFamilyExpectation ?? shadowConfiguredFamily;
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            BoxShadowRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-shadow-{name}", draw), Check,
                size.Width, () => renderer.LastFrameMetrics, renderer.ValidationSwapchainImageCount, scheme, expectedFamily);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "box-shadow", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "dialog")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            DialogRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context, next => input = next);
            DialogMotionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context,
                next => input = next, next => captureDeltaSeconds = next);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "dialog", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "popover")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            PopoverRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context, next => input = next);
            PopoverMotionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context,
                next => input = next, next => captureDeltaSeconds = next);
            PopoverPaintBoundsTests.RunRendering((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context,
                next => input = next, next => captureDeltaSeconds = next);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "popover", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "popup-input")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            PopupInputRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "popup-input", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "toolbar")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            ToolbarRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "toolbar", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "searchbox")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            SearchBoxRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context, next => input = next);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "searchbox", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "persona")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            PersonaRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "persona", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "interaction-tag")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            InteractionTagRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context, next => input = next, size.Width, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "interaction-tag", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "tag")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            TagRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "tag", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "combobox")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            ComboboxRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context, next => input = next, size.Width);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "combobox", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "dropdown")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            DropdownRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context, size.Width);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "dropdown", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "avatar-group")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            AvatarGroupRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context, size.Width);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "avatar-group", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "breadcrumb")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            BreadcrumbRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context, size.Width);
            BreadcrumbTypographyRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "breadcrumb", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "card")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            CardRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-card-{name}", draw), Check, size.Width, scheme, context,
                (point, down, pressed) => input = input with { MousePosition = point, LeftMouseDown = down, LeftMousePressed = pressed, LeftMouseReleased = !down });
            CardSelectionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-card-{name}", draw), Check, size.Width, scheme, context,
                (point, down, pressed) => input = input with { MousePosition = point, LeftMouseDown = down, LeftMousePressed = pressed, LeftMouseReleased = !down });
            CardPreviewRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-card-{name}", draw), Check, size.Width);
            CardHeaderRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-card-{name}", draw), Check, size.Width);
            CardFloatingRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-card-{name}", draw), Check, size.Width, scheme, context);
            CardClippingRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check,
                size.Width, white, context, renderer.ValidationSwapchainImageCount);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "card", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "avatar")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            AvatarRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-avatar-{name}", draw), Check, size.Width,
                enabled => ControlFontFixture.Configure(context, korean: enabled), () => renderer.LastFrameMetrics);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "avatar", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "accordion")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            AccordionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-accordion-{name}", draw), Check, size.Width,
                value => input = value, context);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "accordion", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "markdown" or "markdown-table")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            if (args[1] == "markdown")
            {
                MarkdownRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-markdown-{name}", draw), Check,
                    next => input = next, context, scheme);
                var linkFrame = 0;
                MarkdownLinkTests.Run(draw => Capture($"msaa{samples}-{scheme}-markdown-link-{linkFrame++}", draw),
                    next => input = next, context, Check);
                MarkdownLinkNavigationRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-markdown-{name}", draw), Check,
                    next => input = next, context, scheme);
                MarkdownNestedScrollRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-markdown-{name}", draw), Check,
                    next => input = next, context, scheme);
                MarkdownImageRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-markdown-{name}", draw), Check,
                    next => input = next, context, scheme);
            }
        }
    }
    // IME-backed hosts consume committed text instead of raw character events.
    // Finish character-event profiles before attaching the injected IME fixture.
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            if (args[1] == "markdown")
            {
                MarkdownCompositionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-markdown-ime-{name}", draw), Check,
                    next => input = next, context, scheme);
                MarkdownSemanticRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-markdown-semantic-{name}", draw), Check,
                    next => input = next, context, scheme);
            }
            MarkdownTableRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-markdown-table-{name}", draw), Check,
                next => input = next, context, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "list")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            ListRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-list-{name}", draw), Check,
                next => input = next, context, scheme);
            ListAlignmentRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-list-align-{name}", draw), Check, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "list", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "spinbutton")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            SpinButtonRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-spinbutton-{name}", draw), Check,
                next => input = next, context, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "spinbutton", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "label")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            LabelRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-label-{name}", draw), Check, size.Width, scheme);
            InfoLabelRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-infolabel-{name}", draw), Check,
                next => input = next, context, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "label", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "raster-lifetime" or "rounded-image-lifetime")
{
    foreach (var svg in new[] { false, true })
    {
        using var frames = RasterLayerLifetimeTests.Build(Build, Check, svg, rounded: args[1] == "rounded-image-lifetime");
        byte[]? original = null;
        foreach (var item in frames.Items)
        {
            var pixels = renderer.RenderAndReadback(item.Data);
            var kind = svg ? "svg" : "image";
            using (var file = File.Create(Path.Combine(output, kind + "-" + item.Name + ".ppm")))
            {
                file.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{size.Width} {size.Height}\n255\n"));
                for (var index = 0; index < pixels.Length; index += 4)
                    file.Write(pixels.AsSpan(index, 3));
            }
            var at = (32 * size.Width + 32) * 4;
            if (item.Name == "queued-original")
            {
                original = pixels;
                Check(pixels[at] == 220 && pixels[at + 1] == 30 && pixels[at + 2] == 40,
                    kind + " queued original renders authored GPU pixels after later CPU retirement was already prepared");
                if (args[1] == "rounded-image-lifetime")
                {
                    var corner = (25 * size.Width + 25) * 4;
                    Check(pixels[corner] == 0 && pixels[corner + 1] == 0 && pixels[corner + 2] == 0,
                        kind + " queued rounded mask preserves transparent corners during ordered lifetime replay");
                }
            }
            else if (item.Name == "queued-protected-replay")
                Check(original is not null && pixels.AsSpan(at, 4).SequenceEqual(original.AsSpan(at, 4)),
                    kind + " retained GPU replay survives cache pressure and later CPU layer removal");
            else if (item.Name == "queued-after-removal")
                Check(original is not null && !pixels.AsSpan(at, 4).SequenceEqual(original.AsSpan(at, 4)),
                    kind + " ordered GPU retirement removes the obsolete image without replaying stale pixels");
        }
    }
    using (var replacement = RasterLayerLifetimeTests.BuildExclusiveReplacement(Build, Check, args[1] == "rounded-image-lifetime"))
    {
        var before = renderer.RenderAndReadback(replacement.Items[0].Data);
        var after = renderer.RenderAndReadback(replacement.Items[1].Data);
        var center = (40 * size.Width + 36) * 4;
        // The square vector stays centered in a 48x32 viewport, so its right edge is x=64.
        var added = (40 * size.Width + 60) * 4;
        Check(before.AsSpan(center, 4).SequenceEqual(after.AsSpan(center, 4)) && before[center] == 220
            && before[added] == 0 && after[added] == 220,
            $"the delayed SVG frame retains its original pixels and the replacement paints the widened area"
            + $" / center {Convert.ToHexString(before.AsSpan(center, 4))} → {Convert.ToHexString(after.AsSpan(center, 4))}"
            + $" / widened area {Convert.ToHexString(before.AsSpan(added, 4))} → {Convert.ToHexString(after.AsSpan(added, 4))}");
    }
    Check(renderer.ValidationFrameResourceCount == frameSlots, "raster lifetime actual frame resources : " + frameSlots);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "svg")
{
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        SvgRenderingTests.Run((name, draw) => Capture($"msaa{samples}-svg-{name}", draw), Check, size.Width, output, samples);
        SvgFontRenderingTests.Run((name, draw) => Capture($"msaa{samples}-svg-font-{name}", draw), Check, size.Width, output, samples);
        SvgFeatureRenderingTests.Run((name, draw) => Capture($"msaa{samples}-svg-features-{name}", draw), Check, size.Width, output, samples);
        ImageWidgetRenderingTests.Run((name, draw) => Capture($"msaa{samples}-image-widget-{name}", draw), Check, size.Width, samples);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "svg", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "slider-state")
{
    SliderWidgetTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            SliderStateRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), value => input = value,
                Check, size.Width, scheme);
        }
        input = input with
        {
            MousePosition = new(-100, -100)
        };
        SliderRenderingTests.Run((name, draw) => Capture($"msaa{samples}-prior-{name}", draw), Check);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "slider-state", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "hud-portal-gpu")
{
    ControlFontFixture.Configure(context);
    context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
    input = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        HeadsUpDisplayPortalTests.RunGpu((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, size, context,
            next => captureDeltaSeconds = next);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "hud-portal-gpu", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "hud-content-gpu")
{
    ControlFontFixture.Configure(context);
    input = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        HeadsUpDisplayContentTests.RunGpu((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, context);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "hud-content-gpu", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "activity-indicator-gpu")
{
    input = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        ActivityIndicatorTests.RunGpu((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, context);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "activity-indicator-gpu", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "progress-indicator-gpu" or "progress-indicator-motion-gpu")
{
    ControlFontFixture.Configure(context);
    input = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        if (args[1] == "progress-indicator-motion-gpu")
            ProgressIndicatorTests.RunReducedMotionGpu((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, context, size.Width);
        else
            ProgressIndicatorTests.RunGpu((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, context);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "shimmer-gpu")
{
    ControlFontFixture.Configure(context);
    input = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        ShimmerTests.RunGpu((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, context);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "shimmer-gpu", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "fluent-provider-gpu" or "fluent-provider-font-gpu")
{
    ControlFontFixture.Configure(context);
    input = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        if (args[1] == "fluent-provider-font-gpu")
            FluentProviderRenderingTests.RunFonts((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
        else
            FluentProviderRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, size.Width);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "tab-panel-boundary")
{
    ControlFontFixture.Configure(context);
    using var accessibility = new UiAccessibilityTree();
    context.SetAccessibilityTree(accessibility);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        TabPanelBoundaryRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), value => input = value,
            Check, accessibility, context.State, size.Width, size.Height);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "tab-panel-boundary", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "scrollbar-motion")
{
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            ScrollbarMotionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), value => input = value,
                Check, context, size.Width, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "scrollbar-motion", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "tab-overflow")
{
    ControlFontFixture.Configure(context);
    using var accessibility = new UiAccessibilityTree();
    context.SetAccessibilityTree(accessibility);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            TabOverflowRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), value => input = value,
                Check, accessibility, size.Width, scheme);
            TabScrollRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-scroll-{name}", draw), value => input = value,
                Check, accessibility, context, size.Width, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "tab-overflow", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "menu")
{
    ControlFontFixture.Configure(context);
    using var menuAccessibility = new UiAccessibilityTree();
    context.SetAccessibilityTree(menuAccessibility);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            MenuRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), value => input = value, Check, size.Width, scheme);
            LongMenuRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-long-{name}", draw),
                (name, draw) => CaptureGeometry($"msaa{samples}-{scheme}-long-{name}", draw), value => input = value, Check, menuAccessibility);
            MenuCheckboxRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-checkbox-{name}", draw), value => input = value,
                Check, menuAccessibility, size.Width);
            MenuRadioRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-radio-{name}", draw), value => input = value,
                Check, menuAccessibility, size.Width);
            MenuContentRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-content-{name}", draw), value => input = value,
                Check, menuAccessibility, size.Width, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "menu", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "overflow")
{
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        OverflowRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check, size.Width);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "overflow", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "tablist")
{
    ControlFontFixture.Configure(context);
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    using var accessibility = new UiAccessibilityTree();
    context.SetAccessibilityTree(accessibility);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            TabListRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, accessibility, size.Width, scheme);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "tablist", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "observer-gallery" or "observer-rendering" or "observer-svg" or "observer-avatar" or "observer-widget-transform" or "observer-repaint" or "observer-measure-bench")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    using var observer = new Duxel.Gallery.ObserverScreen(Duxel.Gallery.ObserverServices.CreateWindows());
    using var localObserverAccessibility = new UiAccessibilityTree();
    var observerAccessibility = args[1] == "observer-widget-transform" ? platform.AccessibilityTree : localObserverAccessibility;
    context.SetAccessibilityTree(observerAccessibility);
    var observerOptions = new Duxel.App.DuxelAppOptions { Screen = observer };
    context.SetPlatformTextBackend(platform.TextBackend);
    context.SetTypographyFonts(observerOptions.Font.Typography);
    context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
    context.SetDirectTextPrimaryFontPath(observerOptions.Font.PrimaryFontPath);
    context.SetDirectTextSecondaryFontPath(observerOptions.Font.SecondaryFontPath);
    context.SetDirectTextBaseFontSize(16);
    context.SetDirectTextEnabled(true);
    context.SetDirectTextFallbackEnabled(false);
    context.State.VSync = false;
    captureMissingGlyphObserver = static _ => { };
    if (args[1] == "observer-measure-bench")
    {
        captureMissingGlyphObserver = static _ => { };
        captureDeltaSeconds = 1f / 60;
        ObserverMeasurementDiagnostic.Run(output, observer, renderer, Build);
        return;
    }
    void RevealObserverElement(UiAccessibilityElement target, string captureName, UiAccessibilityScrollPlacement placement)
    {
        if (observerAccessibility.RequestScrollIntoView(target.Node.Id, target.RuntimeId, new(placement),
            wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Observer could not reveal " + target.Node.Name);
        Capture(captureName + "-plan", observer.Render);
        Capture(captureName, observer.Render);
        Check(observerAccessibility.TryGet(target.Node.Id, out var visible) && !visible.Node.Offscreen,
            "observer reveals the actual Gallery element : " + target.Node.Name);
    }
    var pageIndex = 0;
    var observerPages = args[1] == "observer-rendering" ? new[] { "렌더링" } : args[1] == "observer-avatar" ? new[] { "아바타" } : args[1] == "observer-svg" ? new[] { "SVG" }
        : args[1] == "observer-widget-transform" ? new[] { "위젯 배치" } : args[1] == "observer-repaint" ? new[] { "갱신 관찰" }
        : observer.PageTitles;
    foreach (var page in observerPages)
    {
        observer.SelectedPage = page;
        Capture("observer-" + pageIndex + "-warmup", observer.Render);
        if (page == "갱신 관찰")
        {
            var highlight = observerAccessibility.CaptureSnapshot().Elements.Single(element => element.Node.Name == "갱신 영역 강조" && element.Node.Role == UiAccessibilityRole.Switch);
            if (highlight.Node.ToggleState == UiAccessibilityToggleState.On
                && observerAccessibility.RequestAction(highlight.Node.Id, highlight.RuntimeId, UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Disabling repaint diagnostics for the unchanged-pixel oracle failed.");
            Capture("observer-repaint-normal-pixels", observer.Render);
        }
        if (page == "Markdown")
        {
            void InvokeMarkdown(string name)
            {
                UiAccessibilityElement? command = null;
                void Find(string? id)
                {
                    while (id is not null && observerAccessibility.TryGet(id, out var element))
                    {
                        if (element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button)
                            command = element;
                        Find(element.FirstChild);
                        id = element.Next;
                    }
                }
                Find(observerAccessibility.Root.First);
                var target = command ?? throw new InvalidOperationException("Markdown command missing: " + name);
                if (observerAccessibility.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Markdown command rejected: " + name);
            }
            InvokeMarkdown("이미지 예제");
            Capture("observer-markdown-images-plan", observer.Render);
            var images = Capture("observer-markdown-images", observer.Render);
            Check(observer.MarkdownPage.View.Document.Images.Count == 3,
                "observer GPU image example resolves real SVG, bitmap and unavailable image references");
            Check(observer.MarkdownPage.Samples.Single().Source.Contains("이미지와 SVG", StringComparison.Ordinal),
                "observer image sample publishes its updated C# source before the pixel replay check");
            var imageParseCount = observer.MarkdownPage.View.Document.ParseCount;
            var replayImages = Capture("observer-markdown-images-replay", observer.Render);
            Check(images.AsSpan(size.Width * 128 * 4).SequenceEqual(replayImages.AsSpan(size.Width * 128 * 4))
                && observer.MarkdownPage.View.Document.ParseCount == imageParseCount,
                "observer GPU image sample replays unchanged body pixels without reparsing Markdown");
            InvokeMarkdown("예제 복원");
            Capture("observer-markdown-images-restored", observer.Render);
            InvokeMarkdown("중첩 링크 예제");
            Capture("observer-markdown-nested-links", observer.Render);
            Check(observer.MarkdownPage.View.Document.Links.Count == 2,
                "observer GPU nested preview renders the shared document with first and last link targets");
        }
        if (page == "Popover")
        {
            UiAccessibilityElement FindPopover(string name)
            {
                UiAccessibilityElement? found = null;
                void Visit(string? id)
                {
                    while (id is not null && observerAccessibility.TryGet(id, out var e))
                    {
                        if (e.Node.Name == name && e.Node.Role == UiAccessibilityRole.Button)
                            found = e;
                        Visit(e.FirstChild);
                        id = e.Next;
                    }
                }
                Visit(observerAccessibility.Root.First);
                return found ?? throw new InvalidOperationException("Observer Popover command missing : " + name);
            }
            void InvokePopover(string name)
            {
                var e = FindPopover(name);
                if (observerAccessibility.RequestAction(e.Node.Id, e.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Observer Popover command rejected : " + name);
            }
            InvokePopover("팝업 화살표");
            InvokePopover("팝업 모션");
            Capture("observer-popover-arrow-option", observer.Render);
            InvokePopover("프로필 편집 열기");
            Capture("observer-popover-open", observer.Render);
            Check(observer.PopoverPage.Preview!.IsOpen, "observer actual Popover opens owned editor surface");
            InvokePopover("팝업 명령 실행");
            Capture("observer-popover-command", observer.Render);
            InvokePopover("프로필 편집 닫기");
            Capture("observer-popover-close", observer.Render);
            Check(!observer.PopoverPage.Preview.IsOpen, "observer actual Popover child closes surface");
            InvokePopover("팝업 열기 방식");
            Capture("observer-popover-hover-mode", observer.Render);
            InvokePopover("팝업 열기 방식");
            Capture("observer-popover-context-mode", observer.Render);
            var contextTrigger = FindPopover("프로필 편집 열기");
            if (observerAccessibility.RequestAction(contextTrigger.Node.Id, contextTrigger.RuntimeId, UiAccessibilityAction.Focus, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Observer context focus request rejected.");
            Capture("observer-popover-context-focus", observer.Render);
            var savedInput = input;
            input = savedInput with
            {
                KeyEvents = [new(UiKey.ContextMenu, true, default)]
            };
            Capture("observer-popover-context-key", observer.Render);
            input = savedInput with
            {
                KeyEvents = [new(UiKey.ContextMenu, false, default)]
            };
            Capture("observer-popover-context-release", observer.Render);
            input = savedInput;
            Check(observer.PopoverPage.Preview.IsOpen && observer.PopoverPage.Preview.Options.TriggerMode == UiPopoverTriggerMode.Context,
                "observer actual Menu key opens context mode with retained editor");
            InvokePopover("프로필 편집 닫기");
            Capture("observer-popover-context-closed", observer.Render);
            InvokePopover("팝업 열기 방식");
            Capture("observer-popover-trigger-restored", observer.Render);
            observer.PopoverPage.DecorationPreview!.Options = observer.PopoverPage.DecorationPreview.Options with
            {
                MotionEnabled = false
            };
            const string decorationTrigger = "넓은 장식 팝업 열기";
            RevealObserverElement(FindPopover(decorationTrigger), "observer-popover-decoration-trigger",
                UiAccessibilityScrollPlacement.Nearest);
            InvokePopover(decorationTrigger);
            Capture("observer-popover-custom-extent", observer.Render);
            Check(observer.PopoverPage.DecorationPreview.IsOpen, "observer actual custom paint-extent example opens");
            InvokePopover("장식 팝업 닫기");
            Capture("observer-popover-custom-closed", observer.Render);
            Check(!observer.PopoverPage.DecorationPreview.IsOpen, "observer actual custom paint-extent example closes");
        }
        if (page == "팝업 입력")
        {
            UiAccessibilityElement FindPopupInput(string name)
            {
                UiAccessibilityElement? found = null;
                void Visit(string? id)
                {
                    while (id is not null && observerAccessibility.TryGet(id, out var e))
                    {
                        if (e.Node.Name == name)
                            found = e;
                        Visit(e.FirstChild);
                        id = e.Next;
                    }
                }
                Visit(observerAccessibility.Root.First);
                return found ?? throw new InvalidOperationException("Observer popup provider missing : " + name);
            }
            void InvokePopupInput(string name)
            {
                var target = FindPopupInput(name);
                if (observerAccessibility.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Observer popup command rejected : " + name);
            }
            InvokePopupInput("입력 팝업 열기");
            Capture("observer-popup-input-open", observer.Render);
            Check(observer.PopupInputPage.Visible && FindPopupInput("팝업 바깥 명령").Node.Enabled,
                "observer actual nonmodal popup opens with live background controls");
            InvokePopupInput("팝업 안 명령");
            Capture("observer-popup-input-command", observer.Render);
            Check(observer.PopupInputPage.InsideCount == 1, "observer actual popup child command changes its model");
            InvokePopupInput("입력 팝업 닫기");
            Capture("observer-popup-input-close", observer.Render);
            Capture("observer-popup-input-closed", observer.Render);
            InvokePopupInput("팝업 입력 모드 전환");
            Capture("observer-popup-input-mode", observer.Render);
            InvokePopupInput("입력 팝업 열기");
            Capture("observer-popup-input-exclusive-warmup", observer.Render);
            Capture("observer-popup-input-exclusive", observer.Render);
            Check(observer.PopupInputPage.Visible && !FindPopupInput("팝업 바깥 명령").Node.Enabled,
                "observer actual exclusive popup publishes background keyboard isolation");
            InvokePopupInput("입력 팝업 닫기");
            Capture("observer-popup-input-exclusive-close", observer.Render);
            Capture("observer-popup-input-exclusive-closed", observer.Render);
            InvokePopupInput("팝업 입력 모드 전환");
            Capture("observer-popup-input-restored", observer.Render);
        }

        if (page == "측정 재사용")
            for (var prime = 0; prime < 3; prime++)
                Capture("observer-" + pageIndex + "-prime-" + prime, observer.Render);
        var expected = Capture("observer-" + pageIndex + "-reference", observer.Render);
        for (var frame = 0; frame < 3; frame++)
        {
            var actual = Capture("observer-" + pageIndex + "-replay-" + frame, observer.Render);
            var offset = size.Width * 128 * 4;
            Check(actual.AsSpan(offset).SequenceEqual(expected.AsSpan(offset)),
                "observer page body at fixed animation time remains stable across acquired image reuse : " + page + " / " + frame);
        }
        if (page == "인물 정보")
        {
            UiAccessibilityElement FindPersona(string name, UiAccessibilityRole role)
            {
                UiAccessibilityElement? result = null;
                void Visit(string? id)
                {
                    while (id is not null && observerAccessibility.TryGet(id, out var entry))
                    {
                        if (entry.Node.Role == role && entry.Node.Name == name)
                            result = entry;
                        Visit(entry.FirstChild);
                        id = entry.Next;
                    }
                }
                Visit(observerAccessibility.Root.First);
                return result ?? throw new InvalidOperationException(name);
            }
            var name = "디모이, 사용자 인터페이스 개발, Duxel 프로젝트, 한글·SVG·접근성, 사용 가능";
            var person = FindPersona(name, UiAccessibilityRole.Group);
            Check(!person.Node.Offscreen && !person.Node.Focusable, "observer actual Persona publishes complete visible person information");
            var avatar = Capture("observer-persona", observer.Render);
            var imageButton = FindPersona("인물 SVG 이미지", UiAccessibilityRole.Button);
            Check(observerAccessibility.RequestAction(imageButton.Node.Id, imageButton.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
                "observer actual Persona image button accepts native input");
            Capture("observer-persona-image-plan", observer.Render);
            var image = Capture("observer-persona-image", observer.Render);
            Check(!avatar.AsSpan(size.Width * 128 * 4).SequenceEqual(image.AsSpan(size.Width * 128 * 4)),
                "observer actual Persona SVG image updates visible pixels");
            var presenceButton = FindPersona("인물 상태 전용", UiAccessibilityRole.Button);
            Check(observerAccessibility.RequestAction(presenceButton.Node.Id, presenceButton.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
                "observer actual Persona presence-only button accepts native input");
            Capture("observer-persona-presence-plan", observer.Render);
            var presence = Capture("observer-persona-presence", observer.Render);
            Check(!image.AsSpan(size.Width * 128 * 4).SequenceEqual(presence.AsSpan(size.Width * 128 * 4))
                && FindPersona(name, UiAccessibilityRole.Group).RuntimeId == person.RuntimeId,
                "observer actual Persona mode changes visible content while preserving its provider identity");
            Check(presence.AsSpan(size.Width * 128 * 4).SequenceEqual(Capture("observer-persona-presence-replay", observer.Render).AsSpan(size.Width * 128 * 4)),
                "observer actual Persona retains exact body pixels across acquired images");
        }
        if (page == "선택 목록")
        {
            UiAccessibilityElement FindTagSample(string name, UiAccessibilityRole role)
            {
                UiAccessibilityElement? found = null;
                void Visit(string? id)
                {
                    while (id is not null && observerAccessibility.TryGet(id, out var item))
                    {
                        if (item.Node.Name == name && item.Node.Role == role)
                            found = item;
                        Visit(item.FirstChild);
                        id = item.Next;
                    }
                }
                Visit(observerAccessibility.Root.First);
                return found ?? throw new InvalidOperationException(name);
            }
            foreach (var name in new[] { "이미지", "SVG 아이콘" })
            {
                var option = FindTagSample(name, UiAccessibilityRole.Option);
                Check(observerAccessibility.RequestAction(option.Node.Id, option.RuntimeId, UiAccessibilityAction.AddToSelection, wait: false) == UiAccessibilityResult.Success,
                    "observer GPU selection accepts shared tag value : " + name);
            }
            Capture("observer-tags-select-plan", observer.Render);
            RevealObserverElement(FindTagSample("태그 크기 전환", UiAccessibilityRole.Button),
                "observer-tags-reveal", UiAccessibilityScrollPlacement.TopEdge);
            var selectedTags = Capture("observer-tags-selected", observer.Render);
            var tag = FindTagSample("해제 SVG 아이콘", UiAccessibilityRole.Button);
            Check(!tag.Node.Offscreen && selectedTags.AsSpan(size.Width * 128 * 4).SequenceEqual(
                Capture("observer-tags-selected-replay", observer.Render).AsSpan(size.Width * 128 * 4)),
                "observer actual selected SVG tags are visible and replay exact body pixels");
            var primary = FindTagSample("SVG 아이콘", UiAccessibilityRole.Button);
            Check(observerAccessibility.RequestAction(primary.Node.Id, primary.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Success,
                "observer actual interactive SVG tag accepts its independent primary Toggle");
            Capture("observer-tags-primary-plan", observer.Render);
            var inspectedTags = Capture("observer-tags-primary", observer.Render);
            Check(FindTagSample("SVG 아이콘", UiAccessibilityRole.Button).Node.ToggleState == UiAccessibilityToggleState.On
                && observer.DropdownPage.ActivatedTag == "svg" && !FindTagSample("태그 확인 : svg", UiAccessibilityRole.Label).Node.Offscreen
                && FindTagSample("확인할 기능", UiAccessibilityRole.ComboBox).Node.Value == "이미지, SVG 아이콘",
                "observer GPU primary action publishes accessible feedback without dismissing either selection");
            Check(!selectedTags.AsSpan(size.Width * 128 * 4).SequenceEqual(inspectedTags.AsSpan(size.Width * 128 * 4)),
                "observer actual primary action updates the visible feedback pixels");
            Check(inspectedTags.AsSpan(size.Width * 128 * 4).SequenceEqual(Capture("observer-tags-primary-replay", observer.Render).AsSpan(size.Width * 128 * 4)),
                "observer primary feedback retains exact body pixels across acquired images");
            selectedTags = inspectedTags;
            Check(observerAccessibility.RequestAction(tag.Node.Id, tag.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
                "observer actual selected SVG tag accepts dismissal");
            Capture("observer-tags-dismiss-plan", observer.Render);
            var dismissedTags = Capture("observer-tags-dismissed", observer.Render);
            Check(FindTagSample("확인할 기능", UiAccessibilityRole.ComboBox).Node.Value == "이미지"
                && !observerAccessibility.TryGet(tag.Node.Id, out _) && !selectedTags.AsSpan(size.Width * 128 * 4).SequenceEqual(dismissedTags.AsSpan(size.Width * 128 * 4)),
                "observer GPU tag dismissal changes actual list selection, provider lifetime and pixels");
            input = input with
            {
                MousePosition = new(500, 500),
                MouseWheel = -50
            };
            Capture("observer-media-scroll-plan", observer.Render);
            input = input with
            {
                MouseWheel = 0,
                MousePosition = new(-100, -100)
            };
            var mediaBefore = Capture("observer-media-members", observer.Render);
            var member = FindTagSample("디모이", UiAccessibilityRole.Button);
            Check(!member.Node.Offscreen && member.Node.Bounds.Height == 32,
                "observer actual Korean/SVG avatar media tags are reachable through the scroll viewport");
            Check(observerAccessibility.RequestAction(member.Node.Id, member.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Success,
                "observer actual media tag accepts independent native Toggle");
            Capture("observer-media-toggle-plan", observer.Render);
            var mediaSelected = Capture("observer-media-selected", observer.Render);
            Check(FindTagSample("디모이", UiAccessibilityRole.Button).Node.ToggleState == UiAccessibilityToggleState.On
                && !mediaBefore.AsSpan(size.Width * 128 * 4).SequenceEqual(mediaSelected.AsSpan(size.Width * 128 * 4)),
                "observer actual media selection changes native state and rendered body pixels");
            var inactiveButton = FindTagSample("담당자 비활성 표현", UiAccessibilityRole.Button);
            Check(observerAccessibility.RequestAction(inactiveButton.Node.Id, inactiveButton.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
                "observer media inactive control is an actual reachable action");
            Capture("observer-media-inactive-plan", observer.Render);
            var mediaInactive = Capture("observer-media-inactive", observer.Render);
            Check(!mediaInactive.AsSpan(size.Width * 128 * 4).SequenceEqual(mediaSelected.AsSpan(size.Width * 128 * 4))
                && mediaInactive.AsSpan(size.Width * 128 * 4).SequenceEqual(Capture("observer-media-inactive-replay", observer.Render).AsSpan(size.Width * 128 * 4)),
                "observer actual SVG-image and initial media reuse stable inactive composites");
        }
        if (page == "렌더링")
        {
            byte[] RevealRendering(string name, string capture)
            {
                var target = observerAccessibility.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Pane);
                Check(observerAccessibility.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
                    "actual rendering surface accepts ScrollIntoView : " + name);
                for (var settle = 0; settle < 3; settle++) Capture(capture + "-settle-" + settle, observer.Render);
                var node = observerAccessibility.CaptureSnapshot().Elements.Single(element => element.Node.Id == target.Node.Id).Node;
                Check(!node.Offscreen && node.Bounds.Height > 0, "rendering surface is visible before sampling : " + name);
                return Capture(capture, observer.Render);
            }
            observer.RenderingPage.ToggleCircleCutout();
            var coverage = RevealRendering("참조 합성 예제", "observer-rendering-coverage");
            var previewBounds = observer.RenderingPage.BackdropPreviewBounds;
            for (var sampleIndex = 0; sampleIndex < 3; sampleIndex++)
            {
                var pixel = ((int)(previewBounds.Y + 24) * size.Width + (int)(previewBounds.X + sampleIndex * 112 + 48)) * 4;
                Check(pixel >= 0 && pixel + 3 < coverage.Length
                    && Math.Abs(coverage[pixel] - (100 + 14 * sampleIndex)) <= 2
                    && Math.Abs(coverage[pixel + 1] - (160 - 40 * sampleIndex)) <= 2
                    && coverage[pixel + 2] == 200,
                    "observer Rendering tab displays actual prepared affine reference opacity sample : " + sampleIndex);
            }
            var textPixels = RevealRendering("투영 텍스트 예제", "observer-rendering-text");
            var projectedBounds = observer.RenderingPage.ProjectedTextBounds;
            var projectedGlyphPixels = 0;
            var projectedNeutral = true;
            for (var y = (int)projectedBounds.Y; y < (int)(projectedBounds.Y + projectedBounds.Height); y++)
                for (var x = (int)projectedBounds.X; x < (int)(projectedBounds.X + projectedBounds.Width); x++)
                {
                    var pixel = (y * size.Width + x) * 4;
                    projectedNeutral &= Math.Abs(textPixels[pixel] - textPixels[pixel + 1]) <= 1
                        && Math.Abs(textPixels[pixel + 1] - textPixels[pixel + 2]) <= 1;
                    if (textPixels[pixel] > 32) projectedGlyphPixels++;
                }
            Check(projectedNeutral && projectedGlyphPixels > 30,
                "observer Rendering tab displays actual final-resolution Korean and Latin text without RGB fringes");
            var runtime = RevealRendering("그룹 합성 예제", "observer-rendering-group");
            var runtimeBounds = observer.RenderingPage.RuntimePaintGroupBounds;
            var runtimeRed = ((int)(runtimeBounds.Y + 16) * size.Width + (int)(runtimeBounds.X + 8)) * 4;
            var runtimeGreen = ((int)(runtimeBounds.Y + 24) * size.Width + (int)(runtimeBounds.X + 40)) * 4;
            Check(runtimeGreen + 3 < runtime.Length && runtime[runtimeRed] == 204
                && runtime[runtimeRed + 2] == 51 && runtime[runtimeGreen] == 0
                && runtime[runtimeGreen + 1] == 204 && runtime[runtimeGreen + 2] == 51,
                "observer Rendering tab shows actual runtime offscreen overlap with group opacity applied once");
            var cutout = RevealRendering("정적 도형 예제", "observer-rendering-cutout");
            var cutoutBounds = observer.RenderingPage.StaticBounds;
            var holePixel = ((int)(cutoutBounds.Y + 72) * size.Width + (int)(cutoutBounds.X + 110)) * 4;
            Check(cutout[holePixel] == 25 && cutout[holePixel + 1] == 100 && cutout[holePixel + 2] == 85,
                "observer circular subtraction reveals the actual patterned surface behind the retained primitive");
            Check(observer.RenderingPage.CircleCutoutEnabled,
                "observer actual rendering page displays enabled circular subtraction");
            for (var replay = 0; replay < 3; replay++)
                Check(cutout.AsSpan(size.Width * 128 * 4).SequenceEqual(Capture("observer-rendering-cutout-replay-" + replay, observer.Render).AsSpan(size.Width * 128 * 4)),
                    "observer circular primitive and projected-plane pixels remain stable : " + replay);
            observer.RenderingPage.ToggleCircleCutout();
            observer.RenderingPage.ToggleRoundedClip();
            var rounded = RevealRendering("정적 도형 예제", "observer-rendering-rounded");
            cutoutBounds = observer.RenderingPage.StaticBounds;
            var corner = ((int)(cutoutBounds.Y + 28) * size.Width + (int)(cutoutBounds.X + 28)) * 4;
            Check(rounded.AsSpan(corner, 3).SequenceEqual(new byte[] { 25, 45, 80 }),
                "observer rounded clip removes actual retained foreground at its curved corner");
            for (var replay = 0; replay < 3; replay++)
                Check(rounded.AsSpan(size.Width * 128 * 4).SequenceEqual(Capture("observer-rendering-rounded-replay-" + replay,
                    observer.Render).AsSpan(size.Width * 128 * 4)), "observer rounded content and projected capture replay exact pixels : " + replay);
            var roundedGroup = RevealRendering("그룹 합성 예제", "observer-rendering-rounded-group");
            runtimeBounds = observer.RenderingPage.RuntimePaintGroupBounds;
            var groupCorner = ((int)(runtimeBounds.Y + 1) * size.Width + (int)(runtimeBounds.X + 1)) * 4;
            Check(roundedGroup.AsSpan(groupCorner, 3).SequenceEqual(new byte[] { 0, 0, 255 }),
                "observer rounded clip inside cached paint capture preserves the rear surface");
            observer.RenderingPage.ToggleRoundedClip();
            if (args[1] == "observer-rendering")
            {
                File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
                    RuntimeInformation.FrameworkDescription, "observer-rendering", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
                return;
            }
        }
        if (page == "카드")
        {
            UiAccessibilityElement FindCardSample(string name)
            {
                UiAccessibilityElement? found = null;
                void Visit(string? id)
                {
                    while (id is not null && observerAccessibility.TryGet(id, out var item))
                    {
                        if (item.Node.Name == name)
                            found = item;
                        Visit(item.FirstChild);
                        id = item.Next;
                    }
                }
                Visit(observerAccessibility.Root.First);
                return found ?? throw new InvalidOperationException(name);
            }
            var button = FindCardSample("둥근 카드 내부 실행");
            observerAccessibility.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false);
            Capture("observer-card-rounded-reveal-plan", observer.Render);
            var rounded = Capture("observer-card-rounded", observer.Render);
            var card = FindCardSample("둥근 경계 카드");
            var corner = ((int)(card.Node.Bounds.Y + 4) * size.Width + (int)(card.Node.Bounds.X + 4)) * 4;
            var rear = ((int)(card.Node.Bounds.Y + 4) * size.Width + (int)(card.Node.Bounds.X - 1)) * 4;
            Check(!FindCardSample("둥근 카드 내부 실행").Node.Offscreen && rounded.AsSpan(corner, 3).SequenceEqual(rounded.AsSpan(rear, 3)),
                "observer actual revealed zero-padding Card removes the button paint at its rounded corner");
            button = FindCardSample("둥근 카드 내부 실행");
            observerAccessibility.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            Capture("observer-card-rounded-invoke", observer.Render);
            Check(observer.CardPage.ClippedExecutions == 1, "observer GPU Card sample invokes its actual child binding once");
            var toggle = FindCardSample("둥근 모서리 : 켜짐");
            observerAccessibility.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            var square = Capture("observer-card-square", observer.Render);
            Check(!observer.CardPage.ClippingEnabled && !square.AsSpan(corner, 3).SequenceEqual(rounded.AsSpan(corner, 3)),
                "observer actual Card radius toggle reveals the previously clipped child corner pixels");
            Capture("observer-card-square-label-followup", observer.Render);
            toggle = FindCardSample("둥근 모서리 : 꺼짐");
            observerAccessibility.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            Capture("observer-card-rounded-restored", observer.Render);
            var media = FindCardSample("미리보기 카드");
            observerAccessibility.RequestAction(media.Node.Id, media.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false);
            Capture("observer-card-preview-reveal-plan", observer.Render);
            var mediaPixels = Capture("observer-card-preview", observer.Render);
            Check(!FindCardSample("미리보기 카드").Node.Offscreen && !FindCardSample("미리보기 실행").Node.Offscreen,
                "observer actual SVG Card Preview and Footer are reachable in the GPU scrolling page");
            Check(mediaPixels.AsSpan(size.Width * 128 * 4).SequenceEqual(
                Capture("observer-card-preview-replay", observer.Render).AsSpan(size.Width * 128 * 4)),
                "observer actual SVG Preview and logo preserve exact cached body pixels");
            var mediaWidth = FindCardSample("미리보기 카드").Node.Bounds.Width;
            var mediaToggle = FindCardSample("미리보기 폭 변경");
            observerAccessibility.RequestAction(mediaToggle.Node.Id, mediaToggle.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false);
            Capture("observer-card-preview-control-reveal-plan", observer.Render);
            Capture("observer-card-preview-control-reveal", observer.Render);
            mediaToggle = FindCardSample("미리보기 폭 변경");
            Check(!mediaToggle.Node.Offscreen && observerAccessibility.RequestAction(mediaToggle.Node.Id, mediaToggle.RuntimeId,
                UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
                "observer GPU Preview width control is visible and accepts its native Invoke request");
            Capture("observer-card-preview-wider-plan", observer.Render);
            var widerMedia = Capture("observer-card-preview-wider", observer.Render);
            Check(FindCardSample("미리보기 카드").Node.Bounds.Width == mediaWidth + 80
                && !widerMedia.AsSpan(size.Width * 128 * 4).SequenceEqual(mediaPixels.AsSpan(size.Width * 128 * 4)),
                "observer GPU Preview width control changes actual SVG layout and pixels");
            var minimumDemo = FindCardSample("최소 크기 카드");
            observerAccessibility.RequestAction(minimumDemo.Node.Id, minimumDemo.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false);
            Capture("observer-card-minimum-reveal-plan", observer.Render);
            var minimumPixels = Capture("observer-card-minimum", observer.Render);
            var minimumWidth = FindCardSample("최소 크기 카드").Node.Bounds.Width;
            var minimumControl = FindCardSample("부모 최소 폭 변경");
            Check(!minimumControl.Node.Offscreen && !FindCardSample("최소 크기 카드").Node.Offscreen,
                "observer GPU minimum-only Card and parent controls are visible after ScrollItem reveal");
            observerAccessibility.RequestAction(minimumControl.Node.Id, minimumControl.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            Capture("observer-card-minimum-wider-plan", observer.Render);
            var widerMinimum = Capture("observer-card-minimum-wider", observer.Render);
            Check(FindCardSample("최소 크기 카드").Node.Bounds.Width == minimumWidth + 60
                && !widerMinimum.AsSpan(size.Width * 128 * 4).SequenceEqual(minimumPixels.AsSpan(size.Width * 128 * 4)),
                "observer GPU parent minimum control changes actual Card width and SVG pixels");
            minimumControl = FindCardSample("최소 크기 카드 방향");
            observerAccessibility.RequestAction(minimumControl.Node.Id, minimumControl.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            Capture("observer-card-minimum-direction-plan", observer.Render);
            var verticalMinimum = Capture("observer-card-minimum-vertical", observer.Render);
            Check(!verticalMinimum.AsSpan(size.Width * 128 * 4).SequenceEqual(widerMinimum.AsSpan(size.Width * 128 * 4)),
                "observer GPU minimum Card direction changes actual media and Header placement");
            Check(verticalMinimum.AsSpan(size.Width * 128 * 4).SequenceEqual(
                Capture("observer-card-minimum-vertical-replay", observer.Render).AsSpan(size.Width * 128 * 4)),
                "observer GPU minimum Card preserves exact static pixels after direction changes");
            var scroll = observerAccessibility.CaptureSnapshot().Elements.Single(element =>
                element.Node is { Name : "갤러리 페이지", Role : UiAccessibilityRole.Pane, Scroll.VerticallyScrollable : true });
            Check(observerAccessibility.RequestScroll(scroll.Node.Id, scroll.RuntimeId,
                UiAccessibilityScrollCommand.ByPercent(-1, 0), wait : false) == UiAccessibilityResult.Success,
                "observer Card requests the shared Gallery page scroll owner");
            Capture("observer-card-scroll-top", observer.Render);
            Check(observerAccessibility.TryGet(scroll.Node.Id, out var top) && top.Node.Scroll?.VerticalScrollPercent == 0,
                "observer Card returns to the top of the shared Gallery page");
        }
        pageIndex++;
    }
    if (args[1] == "observer-avatar")
    {
        var beforeRing = Capture("observer-avatar-before-activity", observer.Render);
        var avatarPreview = observer.AvatarPage.Preview!;
        avatarPreview.Options = avatarPreview.Options with
        {
            ShowActivityRing = true
        };
        var withRing = Capture("observer-avatar-with-activity", observer.Render);
        var bodyOffset = size.Width * 128 * 4;
        Check(!beforeRing.AsSpan(bodyOffset).SequenceEqual(withRing.AsSpan(bodyOffset)),
            "observer Avatar activity option changes its actual initials and ring pixels");
        Check(withRing.AsSpan(bodyOffset).SequenceEqual(Capture("observer-avatar-activity-replay", observer.Render).AsSpan(bodyOffset)),
            "observer Avatar activity ring retains exact text and decoration pixels");
        Check(avatarPreview.ActivityText == "활동 중" && context.State.NextScheduledFrameTimeSeconds is null,
            "observer activity ring uses localized semantics without scheduling another frame");
        avatarPreview.Options = avatarPreview.Options with
        {
            Presence = new(UiPresenceStatus.Available)
        };
        avatarPreview.PresenceText = "사용 가능";
        var withPresence = Capture("observer-avatar-with-presence", observer.Render);
        Check(!withRing.AsSpan(bodyOffset).SequenceEqual(withPresence.AsSpan(bodyOffset)),
            "observer Avatar actual status overlay changes its ring and initials pixels");
        var avatarSvgDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
        using var avatarSvgStore = new Duxel.Svg.SvgStore();
        using var avatarSvgIcon = new UiSvgIcon(avatarSvgStore.Load(Path.Combine(avatarSvgDirectory, "regular.svg")),
            avatarSvgStore.Load(Path.Combine(avatarSvgDirectory, "filled.svg")), UiSvgIconColorMode.MultiplyForeground);
        avatarPreview.Initials = "";
        avatarPreview.Icon = avatarSvgIcon.MenuIcon;
        var svgPresence = Capture("observer-avatar-svg-presence", observer.Render);
        Check(!svgPresence.AsSpan(bodyOffset).SequenceEqual(withPresence.AsSpan(bodyOffset)),
            "observer Avatar borrowed SVG icon remains visible with the shared badge cutout");
        for (var replay = 0; replay < 3; replay++)
            Check(svgPresence.AsSpan(bodyOffset).SequenceEqual(Capture("observer-avatar-svg-presence-replay-" + replay, observer.Render).AsSpan(bodyOffset)),
                "observer Avatar SVG icon ring and presence retain exact pixels : " + replay);
        avatarPreview.Options = avatarPreview.Options with
        {
            ShowActivityShadow = true
        };
        var svgShadow = Capture("observer-avatar-svg-shadow", observer.Render);
        Check(!svgShadow.AsSpan(bodyOffset).SequenceEqual(svgPresence.AsSpan(bodyOffset))
            && svgShadow.AsSpan(bodyOffset).SequenceEqual(Capture("observer-avatar-svg-shadow-replay", observer.Render).AsSpan(bodyOffset)),
            "observer actual SVG Avatar combines stable activity shadow ring and status pixels");
        avatarPreview.Options = avatarPreview.Options with
        {
            IsInactive = true
        };
        var svgInactive = Capture("observer-avatar-svg-inactive", observer.Render);
        Check(!svgInactive.AsSpan(bodyOffset).SequenceEqual(svgShadow.AsSpan(bodyOffset))
            && context.State.NextScheduledFrameTimeSeconds is null,
            "observer actual inactive SVG Avatar hides activity decorations and schedules no periodic frame");
        for (var replay = 0; replay < 3; replay++)
            Check(svgInactive.AsSpan(bodyOffset).SequenceEqual(Capture("observer-avatar-svg-inactive-replay-" + replay, observer.Render).AsSpan(bodyOffset)),
                "observer actual inactive SVG Avatar retains exact group pixels : " + replay);
        avatarPreview.Initials = "김";
        avatarPreview.Name = "김민수";
        var koreanInactive = Capture("observer-avatar-korean-inactive", observer.Render);
        Check(!koreanInactive.AsSpan(bodyOffset).SequenceEqual(svgInactive.AsSpan(bodyOffset))
            && koreanInactive.AsSpan(bodyOffset).SequenceEqual(Capture("observer-avatar-korean-inactive-replay", observer.Render).AsSpan(bodyOffset)),
            "observer actual inactive Korean Avatar replaces SVG content with stable projected text pixels");
        avatarPreview.Options = avatarPreview.Options with
        {
            IsInactive = false
        };
        Check(!koreanInactive.AsSpan(bodyOffset).SequenceEqual(Capture("observer-avatar-restored", observer.Render).AsSpan(bodyOffset)),
            "observer restored Avatar paints its original ring shadow and full opacity again");
        avatarPreview.Icon = default;
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
            RuntimeInformation.FrameworkDescription, "observer-avatar", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
        return;
    }
    if (args[1] == "observer-repaint")
    {
        var highlight = observerAccessibility.CaptureSnapshot().Elements.Single(element => element.Node.Name == "갱신 영역 강조" && element.Node.Role == UiAccessibilityRole.Switch);
        if (observerAccessibility.RequestAction(highlight.Node.Id, highlight.RuntimeId, UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Enabling repaint diagnostics for the actual observer feature check failed.");
        Capture("observer-repaint-enabled", observer.Render);
        context.State.AdvanceTime(.5f);
        Capture("observer-repaint-animations-settle", observer.Render);
        context.State.AdvanceTime(.2f);
        for (var settle = 0; settle < 6; settle++)
            Capture("observer-repaint-settle-" + settle, observer.Render);
        Thread.Sleep(220);
        Capture("observer-repaint-idle", observer.Render);
        var settled = renderer.LastFrameMetrics;
        Check(!settled.RepaintHighlightActive && context.State.GetScheduledFrameWaitMilliseconds(context.State.TimeSeconds) == Timeout.Infinite,
            $"native observer repaint inspector returns to indefinite event wait after its real diagnostic expires : active={settled.RepaintHighlightActive}, repair={settled.RepaintBounds}, wait={context.State.GetScheduledFrameWaitMilliseconds(context.State.TimeSeconds)}, scheduled={context.State.NextScheduledFrameTimeSeconds}");
        var staticBuilds = observer.RepaintPage.StaticBuilds;
        var textBuilds = observer.RepaintPage.TextBuilds;
        var action = observerAccessibility.CaptureSnapshot().Elements.Single(e => e.Node.Name == "한 번 변경" && e.Node.Role == UiAccessibilityRole.Button);
        Check(observerAccessibility.RequestAction(action.Node.Id, action.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "native inspector changes actual Fluent content through the real accessibility action");
        Capture("observer-repaint-changed", observer.Render);
        Check(renderer.LastFrameMetrics.RepaintHighlightActive && observer.RepaintPage.StaticBuilds == staticBuilds && observer.RepaintPage.TextBuilds > textBuilds,
            "native inspector flashes actual damage while retaining unrelated panel construction");
        Check(renderer.LastFrameMetrics.RepaintBounds.Width < size.Width && renderer.LastFrameMetrics.RepaintBounds.Height < size.Height,
            "native inspector text texture creation avoids the previous full-window damage invalidation");
        var editor = observerAccessibility.CaptureSnapshot().Elements.Single(element => element.Node.Name == "갱신 관찰 입력" && element.Node.Role == UiAccessibilityRole.Edit).Node;
        var editorPoint = new UiVector2(editor.Bounds.X + 100, editor.Bounds.Y + editor.Bounds.Height / 2);
        input = input with
        {
            MousePosition = editorPoint,
            LeftMouseDown = true,
            LeftMousePressed = true
        };
        Capture("observer-repaint-editor-pressed", observer.Render);
        input = input with
        {
            LeftMouseDown = false,
            LeftMousePressed = false,
            LeftMouseReleased = true
        };
        Capture("observer-repaint-editor-released", observer.Render);
        input = input with
        {
            MousePosition = new(-100, -100),
            LeftMouseReleased = false
        };
        context.State.AdvanceTime(.7f);
        for (var settle = 0; settle < 12; settle++)
            Capture("observer-repaint-caret-settle-" + settle, observer.Render);
        var caretElapsed = context.State.TimeSeconds - context.State.CaretBlinkStartSeconds;
        context.State.AdvanceTime((float)((Math.Floor(caretElapsed / .5) + 1) * .5 - caretElapsed + .01));
        Capture("observer-repaint-caret-blink", observer.Render);
        var caretRepair = renderer.LastFrameMetrics.RepaintBounds;
        Check(caretRepair.Width is > 0 and <= 8 && caretRepair.Height is > 0 and <= 36,
            "native inspector caret blink repairs its narrow stroke rather than the following panels : " + caretRepair);
        UiAccessibilityElement NavigationEntry(string page) => observerAccessibility.CaptureSnapshot().Elements.Single(element =>
            element.Node.Name == observer.NavigationLabelForPage(page) && element.Node.Role == UiAccessibilityRole.Link);
        foreach (var destination in new[] { "Popover", "추가 정보" })
        {
            var entry = NavigationEntry(destination);
            Check(observerAccessibility.RequestAction(entry.Node.Id, entry.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
                "native rollover oracle reveals the actual left navigation item : " + destination);
            for (var settle = 0; settle < 3; settle++) Capture("observer-repaint-nav-reveal-" + destination + "-" + settle, observer.Render);
        }
        var hoverItem = NavigationEntry("Popover").Node;
        var nextHover = NavigationEntry("추가 정보").Node;
        Check(!hoverItem.Offscreen && !nextHover.Offscreen, "both native rollover targets are visible before pointer input");
        input = input with
        {
            MousePosition = new(hoverItem.Bounds.X + hoverItem.Bounds.Width / 2, hoverItem.Bounds.Y + hoverItem.Bounds.Height / 2)
        };
        for (var settle = 0; settle < 12; settle++)
            Capture("observer-repaint-nav-hover-settle-" + settle, observer.Render);
        input = input with
        {
            MousePosition = new(nextHover.Bounds.X + nextHover.Bounds.Width / 2, nextHover.Bounds.Y + nextHover.Bounds.Height / 2)
        };
        Capture("observer-repaint-nav-hover", observer.Render);
        var hoverRepair = renderer.LastFrameMetrics.RepaintBounds;
        var hoverLeft = MathF.Min(hoverItem.Bounds.X, nextHover.Bounds.X) - 3;
        var hoverTop = MathF.Min(hoverItem.Bounds.Y, nextHover.Bounds.Y) - 3;
        var hoverRight = MathF.Max(hoverItem.Bounds.X + hoverItem.Bounds.Width, nextHover.Bounds.X + nextHover.Bounds.Width) + 3;
        var hoverBottom = MathF.Max(hoverItem.Bounds.Y + hoverItem.Bounds.Height, nextHover.Bounds.Y + nextHover.Bounds.Height) + 3;
        Check(hoverRepair.Width > 0 && hoverRepair.Height > 0
            && hoverRepair.X >= hoverLeft && hoverRepair.Y >= hoverTop
            && hoverRepair.X + hoverRepair.Width <= hoverRight && hoverRepair.Y + hoverRepair.Height <= hoverBottom,
            "native left-navigation rollover repairs only the two actual item regions and preserves its body : " + hoverRepair);
        observer.SelectedPage = "Fluent";
        Capture("observer-repaint-global-fluent-warmup", observer.Render);
        Capture("observer-repaint-global-fluent", observer.Render);
        var currentNavigation = NavigationEntry("Fluent").Node;
        Check(renderer.LastFrameMetrics.RepaintHighlightActive && currentNavigation.IsCurrent == true
            && observerAccessibility.CaptureSnapshot().Elements.Any(element => element.Node.Name == observer.NavigationLabelForPage("Fluent")
                && element.Node.Role == UiAccessibilityRole.Pane && element.Node.LabeledBy == currentNavigation.Id),
            "native header repaint diagnostics remain active on the actual Fluent page after leaving the sample");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
            RuntimeInformation.FrameworkDescription, "observer-repaint", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
        return;
    }
    if (args[1] == "observer-widget-transform")
    {
        UiAccessibilityNode FindWidgetNode(string name, UiAccessibilityRole role) => observerAccessibility.CaptureSnapshot().Elements
            .Select(element => element.Node).Single(node => node.Name == name && node.Role == role);
        void ChangeWidgetRange(string name, double value)
        {
            var node = FindWidgetNode(name, UiAccessibilityRole.Slider);
            if (!observerAccessibility.TryGet(node.Id, out var entry)
                || observerAccessibility.RequestRangeValue(node.Id, entry.RuntimeId, value, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Native observer common transform range is unavailable.");
        }
        var before = Capture("observer-widget-transform-before", observer.Render);
        ChangeWidgetRange("공통 회전", 90);
        var turned = Capture("observer-widget-transform-quarter", observer.Render);
        var button = FindWidgetNode("변환 버튼 실행", UiAccessibilityRole.Button);
        Check(button.Bounds.Width == 32 && button.Bounds.Height == 140 && !before.AsSpan().SequenceEqual(turned),
            "native observer common rotation changes real button bounds and rendered page pixels");
        var pointer = button.ClickablePoint ?? throw new InvalidOperationException("Native rotated button has no visible clickable point.");
        input = input with
        {
            MousePosition = pointer,
            LeftMouseDown = true,
            LeftMousePressed = true
        };
        Capture("observer-widget-transform-pressed", observer.Render);
        input = input with
        {
            LeftMouseDown = false,
            LeftMousePressed = false,
            LeftMouseReleased = true
        };
        Capture("observer-widget-transform-released", observer.Render);
        Check(observer.WidgetLayoutPage.Activations == 1, "native observer actual rotated button activates once at its rendered point");
        input = input with
        {
            MousePosition = new(-100, -100),
            LeftMouseReleased = false
        };
        ChangeWidgetRange("공통 확대", 1.5);
        Capture("observer-widget-transform-scaled", observer.Render);
        var scaledButton = FindWidgetNode("변환 버튼 실행", UiAccessibilityRole.Button);
        Check(scaledButton.Bounds.Width == 48 && scaledButton.Bounds.Height == 210,
            "native observer common scale changes transformed layout and preserves the expected allocated extent");
        for (var settle = 0; settle < 24; settle++)
            Capture("observer-widget-transform-idle-" + settle, observer.Render);
        Check(context.State.NextScheduledFrameTimeSeconds is null,
            "native observer common transform page stops scheduling frames after interactions settle");
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
            RuntimeInformation.FrameworkDescription, "observer-widget-transform", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
        return;
    }
    if (args[1] == "observer-svg")
    {
        UiAccessibilityNode FindFeatureNode(UiAccessibilityRole role, string name = "문서 기능")
        {
            UiAccessibilityNode? found = null;
            void Visit(string? id)
            {
                while (id is not null && observerAccessibility.TryGet(id, out var element))
                {
                    if (element.Node.Name == name && element.Node.Role == role)
                        found = element.Node;
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(observerAccessibility.Root.First);
            return found ?? throw new InvalidOperationException("Observer SVG feature tab/panel is missing.");
        }
        byte[] SelectSvgTab(string name, string prefix, out UiAccessibilityNode tab)
        {
            tab = FindFeatureNode(UiAccessibilityRole.Tab, name);
            var pointer = new UiVector2(tab.Bounds.X + tab.Bounds.Width / 2, tab.Bounds.Y + tab.Bounds.Height / 2);
            input = input with
            {
                MousePosition = pointer,
                LeftMouseDown = true,
                LeftMousePressed = true
            };
            Capture(prefix + "-down", observer.Render);
            input = input with
            {
                LeftMouseDown = false,
                LeftMousePressed = false,
                LeftMouseReleased = true
            };
            Capture(prefix + "-up", observer.Render);
            input = input with
            {
                MousePosition = new(-100, -100),
                LeftMouseReleased = false
            };
            return Capture(prefix + "-reference", observer.Render);
        }
        var beforeFeatures = Capture("observer-features-before", observer.Render);
        var featurePixels = SelectSvgTab("문서 기능", "observer-features", out var featureTab);
        var bodyOffset = size.Width * 128 * 4;
        Check(FindFeatureNode(UiAccessibilityRole.TabPanel).LabeledBy == featureTab.Id &&
            !featurePixels.AsSpan(bodyOffset).SequenceEqual(beforeFeatures.AsSpan(bodyOffset)),
            "observer actual pointer-selected SVG feature tab publishes and paints its linked document panel");
        for (var replay = 0; replay < 3; replay++)
        {
            var actual = Capture("observer-features-replay-" + replay, observer.Render);
            Check(actual.AsSpan(bodyOffset).SequenceEqual(featurePixels.AsSpan(bodyOffset)),
                "observer SVG feature panel remains pixel-identical across acquired images : " + replay);
        }
        var beforeLayers = observer.SvgPage.Statistics!.Value;
        var layerPixels = SelectSvgTab("레이어·테두리", "observer-layers", out var layersTab);
        Check(FindFeatureNode(UiAccessibilityRole.TabPanel, "레이어·테두리").LabeledBy == layersTab.Id &&
            observer.SvgPage.Statistics!.Value.XmlParses == beforeLayers.XmlParses + 5,
            "observer layer gallery loads five independently captured source/reference pairs");
        var pair = observer.SvgPage.LayerPairBounds ?? throw new InvalidOperationException("Layer comparison image bounds missing.");
        var pairWidth = (int)pair.Reference.Width;
        var pairHeight = (int)pair.Reference.Height;
        var pairExact = pair.Reference.Width == pair.Vector.Width && pair.Reference.Height == pair.Vector.Height;
        for (var row = 0; row < pairHeight && pairExact; row++)
        {
            var referenceOffset = (((int)pair.Reference.Y + row) * size.Width + (int)pair.Reference.X) * 4;
            var vectorOffset = (((int)pair.Vector.Y + row) * size.Width + (int)pair.Vector.X) * 4;
            pairExact &= layerPixels.AsSpan(referenceOffset, pairWidth * 4).SequenceEqual(layerPixels.AsSpan(vectorOffset, pairWidth * 4));
        }
        Check(pairExact, "observer native reference and GPU widget preview match at the same default pixel resolution");
        var layersStatistics = observer.SvgPage.Statistics;
        Capture("observer-layers-replay", observer.Render);
        Check(observer.SvgPage.Statistics == layersStatistics,
            "observer layer gallery reuses compiled scenes without CPU SVG rasterization");
        var beforeDomains = observer.SvgPage.Statistics!.Value;
        SelectSvgTab("테두리 범위", "observer-stroke-domains", out var domainsTab);
        Check(FindFeatureNode(UiAccessibilityRole.TabPanel, "테두리 범위").LabeledBy == domainsTab.Id &&
            observer.SvgPage.Statistics!.Value.XmlParses == beforeDomains.XmlParses + 5,
            "observer stroke-domain gallery exposes five independent boundary controls including unresolved thin/scaled cases");
        var beforeClips = observer.SvgPage.Statistics!.Value;
        SelectSvgTab("클립·경계", "observer-clips", out var clipsTab);
        Check(FindFeatureNode(UiAccessibilityRole.TabPanel, "클립·경계").LabeledBy == clipsTab.Id &&
            observer.SvgPage.Statistics!.Value.XmlParses == beforeClips.XmlParses + 9,
            "observer clip gallery loads nine independent source/reference pairs on first selection");
        var clipsStatistics = observer.SvgPage.Statistics;
        Capture("observer-clips-replay", observer.Render);
        Check(observer.SvgPage.Statistics == clipsStatistics,
            "observer clip gallery reuses XML scenes and creates no CPU SVG rasters on replay");
        var beforeBrowser = observer.SvgPage.Statistics!.Value;
        var browserPixels = SelectSvgTab("브라우저 비교", "observer-browser", out var browserTab);
        Check(FindFeatureNode(UiAccessibilityRole.TabPanel, "브라우저 비교").LabeledBy == browserTab.Id &&
            observer.SvgPage.Statistics!.Value.XmlParses == beforeBrowser.XmlParses + 2 &&
            !browserPixels.AsSpan(bodyOffset).SequenceEqual(featurePixels.AsSpan(bodyOffset)),
            "observer GPU browser-comparison tab paints both original SVGs and their verified references");
        var browserStatistics = observer.SvgPage.Statistics;
        for (var replay = 0; replay < 3; replay++)
        {
            var actual = Capture("observer-browser-replay-" + replay, observer.Render);
            Check(actual.AsSpan(bodyOffset).SequenceEqual(browserPixels.AsSpan(bodyOffset)) && observer.SvgPage.Statistics == browserStatistics,
                "observer GPU browser comparison replays exact cached body pixels : " + replay);
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
            RuntimeInformation.FrameworkDescription, "observer-svg", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
        return;
    }
    observer.SelectedPage = "입력·IME";
    Capture("observer-panel-boundary-layout", observer.Render);
    var editingPanel = observerAccessibility.CaptureSnapshot().Elements.Single(element =>
        element.Node is { Role : UiAccessibilityRole.Pane, Focusable : true }
        && element.Node.Name == observer.SelectedNavigationLabel);
    Check(observerAccessibility.RequestAction(editingPanel.Node.Id, editingPanel.RuntimeId,
        UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.Success,
        "observer actual editing panel accepts keyboard-visible focus");
    var focusedPanel = Capture("observer-panel-boundary-focused", observer.Render);
    var panelBounds = editingPanel.Node.Bounds;
    bool PanelInk(int x, int y)
    {
        var offset = (y * size.Width + x) * 4;
        return focusedPanel[offset + 2] > focusedPanel[offset] + 20 && focusedPanel[offset + 1] > focusedPanel[offset] + 20;
    }
    const int galleryContentLeft = 8 + 280 + 20;
    Check(panelBounds.X == galleryContentLeft && panelBounds.X + panelBounds.Width == size.Width - 8
        && panelBounds.Y + panelBounds.Height == size.Height - 8,
        "observer editing panel reserves matching right and bottom content padding");
    Check(PanelInk(galleryContentLeft, 800) && PanelInk(size.Width - 9, 800)
        && PanelInk(900, (int)panelBounds.Y) && PanelInk(900, size.Height - 9),
        "observer actual editing panel paints every focus edge within its padded viewport");
    observer.SelectedPage = "기존 API";
    Capture("observer-before-immediate", observer.Render);
    var immediateTab = observerAccessibility.CaptureSnapshot().Elements.Single(element =>
        element.Node.Role == UiAccessibilityRole.Tab && element.Node.Name == "Immediate" && !element.Node.Offscreen).Node.Bounds;
    input = input with
    {
        MousePosition = new(immediateTab.X + immediateTab.Width / 2, immediateTab.Y + immediateTab.Height / 2),
        LeftMouseDown = true,
        LeftMousePressed = true
    };
    Capture("observer-immediate-down", observer.Render);
    input = input with
    {
        LeftMouseDown = false,
        LeftMousePressed = false,
        LeftMouseReleased = true
    };
    Capture("observer-immediate-up", observer.Render);
    Check(context.State.NextScheduledFrameTimeSeconds == context.State.TimeSeconds,
        "observer legacy Immediate requests its deferred body frame in event-wait mode");
    input = input with
    {
        LeftMouseReleased = false
    };
    var immediate = Capture("observer-immediate", observer.Render);
    Check(observerAccessibility.CaptureSnapshot().Elements.Any(element =>
        element.Node is { Name : "Immediate", Role : UiAccessibilityRole.Tab, Selected : true, Offscreen : false }),
        "observer legacy Immediate publishes selection after actual pointer press and release");
    var legacyRanges = new Dictionary<string, UiAccessibilityNode>(StringComparer.Ordinal);
    void CollectLegacyRanges(string? id)
    {
        while (id is not null && observerAccessibility.TryGet(id, out var element))
        {
            if (element.Node.Range is not null)
                legacyRanges.Add(element.Node.Name, element.Node);
            CollectLegacyRanges(element.FirstChild);
            id = element.Next;
        }
    }
    CollectLegacyRanges(observerAccessibility.Root.First);
    Check(legacyRanges["Scale"].Range is { Minimum: 0, Maximum: 1 } &&
        legacyRanges["VSliderFloat"].Range is { Vertical: true } && legacyRanges["VSliderInt"].Range is { Vertical: true, SmallChange: 1 },
        "actual pointer-selected Immediate body publishes horizontal, vertical and integer legacy numeric controls");
    var immediatePanel = observerAccessibility.CaptureSnapshot().Elements.Single(element =>
        element.Node.Role == UiAccessibilityRole.TabPanel && element.Node.Name == "Immediate").Node.Bounds;
    bool BasicsHeaderColor(int x, int y)
    {
        var pixel = (y * size.Width + x) * 4;
        return immediate[pixel] == 22 && immediate[pixel + 1] == 54 && immediate[pixel + 2] == 73;
    }
    var headerVisible = Enumerable.Range((int)immediatePanel.Y,
        (int)legacyRanges["Scale"].Bounds.Y - (int)immediatePanel.Y).Any(y =>
            BasicsHeaderColor((int)immediatePanel.X + 12, y) &&
            BasicsHeaderColor((int)(immediatePanel.X + immediatePanel.Width / 2), y));
    Check(headerVisible,
        "first selected Immediate frame contains the expected expanded Basics header");
    for (var frame = 0; frame < 4; frame++)
    {
        var replay = Capture("observer-immediate-settle-" + frame, observer.Render);
        var offset = size.Width * 128 * 4;
        Check(replay.AsSpan(offset).SequenceEqual(immediate.AsSpan(offset)),
            "Immediate first selected body matches acquired-image replay : " + frame);
    }
    observer.SelectedPage = "Fluent";
    Capture("observer-before-tab-examples", observer.Render);
    var groups = new Dictionary<string, UiAccessibilityElement>(StringComparer.Ordinal);
    Collect(observerAccessibility.Root.First);
    foreach (var appearance in Enum.GetValues<UiTabListAppearance>())
        RevealObserverElement(groups["표현 비교 : " + appearance], "observer-tab-example-" + appearance,
            UiAccessibilityScrollPlacement.Nearest);
    RevealObserverElement(groups["표현 비교 : SubtleCircular"], "observer-tab-example-pointer-target",
        UiAccessibilityScrollPlacement.Nearest);
    var examples = Capture("observer-tab-examples", observer.Render);
    observerAccessibility.TryGet(groups["표현 비교 : SubtleCircular"].FirstChild!, out var homeExample);
    observerAccessibility.TryGet(homeExample.Next!, out var documentExample);
    var documentCenter = new UiVector2(documentExample.Node.Bounds.X + documentExample.Node.Bounds.Width / 2,
        documentExample.Node.Bounds.Y + documentExample.Node.Bounds.Height / 2);
    input = input with
    {
        MousePosition = documentCenter,
        LeftMouseDown = true,
        LeftMousePressed = true
    };
    Capture("observer-tab-example-down", observer.Render);
    input = input with
    {
        LeftMouseDown = false,
        LeftMousePressed = false,
        LeftMouseReleased = true
    };
    Capture("observer-tab-example-up", observer.Render);
    input = input with
    {
        LeftMouseReleased = false,
        MousePosition = new(-100, -100)
    };
    var selectedExample = Capture("observer-tab-example-selected", observer.Render);
    observerAccessibility.TryGet(documentExample.Node.Id, out var liveDocumentExample);
    Check(liveDocumentExample.Node.Selected == true && !examples.AsSpan(size.Width * 128 * 4).SequenceEqual(selectedExample.AsSpan(size.Width * 128 * 4)),
        "observer GPU pointer selection changes the actual icon-only tab and rendered page body");
    Check(Capture("observer-tab-example-selected-replay", observer.Render).AsSpan(size.Width * 128 * 4)
        .SequenceEqual(selectedExample.AsSpan(size.Width * 128 * 4)),
        "observer GPU selected tab example remains stable on the next acquired image");
    void Collect(string? id)
    {
        while (id is not null && observerAccessibility.TryGet(id, out var element))
        {
            if (element.Node.Role == UiAccessibilityRole.TabList)
                groups[element.Node.Name] = element;
            Collect(element.FirstChild);
            id = element.Next;
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "observer-gallery", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "resize-coherence" or "resize-capacity")
{
    var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
    context.SetPlatformTextBackend(platform.TextBackend);
    context.SetDirectTextEnabled(true);
    context.SetDirectTextPrimaryFontPath(Path.Combine(fonts, "segoeui.ttf"));
    context.SetDirectTextBaseFontSize(18);
    context.SetTypographyFonts(new(Path.Combine(fonts, "segoeui.ttf"), Path.Combine(fonts, "seguisb.ttf"), Path.Combine(fonts, "segoeuib.ttf")));
    if (rootMsaa4)
    {
        renderer.SetMsaaSamples(4);
        VulkanRendererBackend.ValidationResizableMsaaCapacity = true;
    }
    if (args[1] == "resize-capacity")
        RootResizeRenderingTests.RunPresentationCapacity(renderer, platform, Capture, (w, h) => { size = new(w, h); bounds = new(0, 0, w, h); }, Check);
    else
        RootResizeRenderingTests.Run(renderer, platform, Build, Capture, (w, h) => { size = new(w, h); bounds = new(0, 0, w, h); }, Check, output);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    Console.WriteLine($"Passed {checks.Count} focused resize graphics checks.");
    return;
}
if (args.Length >= 2 && args[1] == "primitive-commands")
{
    ControlFontFixture.Configure(context);
    PrimitiveCommandTests.Run(Check);
    RoundedGradientTests.Run(renderer, Capture, Build, Check, white, bounds);
    OverlayPresentationTests.Run(renderer, Build, Capture, Check, white, bounds);
    ViewportClippingTests.Run(renderer, Capture, Check, white, size.Width, size.Height);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "primitive-commands", checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "directtext-cache")
{
    ControlFontFixture.Configure(context);
    DirectTextLayoutTests.Run(Check);
    RetainedTextMaintenanceTests.VerifyPixels(Capture, Check);
    DirectTextScaleRenderingTests.Run(Check, frameSlots, output);
    RetainedTextMaintenanceTests.VerifyAtlasReplacement(context, Capture, Check);
    RetainedTextMaintenanceTests.VerifyProjectedTextAtlasReplacement(context, Capture, Check);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "directtext-cache", checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "tooltip")
{
    ControlFontFixture.Configure(context);
    using var accessibility = new UiAccessibilityTree();
    context.SetAccessibilityTree(accessibility);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            FluentTooltipRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check);
            foreach (var vector in new[] { false, true })
            {
                TooltipTargetRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                    next => input = next, Check, vector, accessibility, context.State);
            }
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "tooltip", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "multiline")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        MultilineInputTests.Render((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
    }
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        var previousTheme = context.GetStyle();
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
            {
                MultilineContractTests.Render((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, vector);
                MultilineWrapRenderingTests.Run((name, draw) => CaptureGeometry($"msaa{samples}-{scheme}-{name}", draw), Check, context.State, context.GetStyleData().ScrollbarSize, vector);
            }
        }
        context.SetTheme(previousTheme);
    }
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        var previousTheme = context.GetStyle();
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                FluentTextareaRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, context.State, vector);
        }
        context.SetTheme(previousTheme);
    }
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        var previousTheme = context.GetStyle();
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                MultilineHardLineRenderingTests.Run((name, draw) => CaptureGeometry($"msaa{samples}-{scheme}-{name}", draw), Check,
                    context.State, context.GetStyleData().ScrollbarSize, vector);
        }
        context.SetTheme(previousTheme);
    }
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        var previousTheme = context.GetStyle();
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                MultilineCommandRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                    next => input = next, Check, context.State, vector);
        }
        context.SetTheme(previousTheme);
    }
    var elementFontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
    var elementEmojiFont = Path.Combine(elementFontDirectory, "seguiemj.ttf");
    if (!File.Exists(elementEmojiFont))
        throw new FileNotFoundException("Multiline element validation requires an explicit emoji font.", elementEmojiFont);
    context.SetTypographyFonts(new(Path.Combine(elementFontDirectory, "segoeui.ttf"), Path.Combine(elementFontDirectory, "seguisb.ttf"),
        Path.Combine(elementFontDirectory, "segoeuib.ttf"), SecondaryRegular: elementEmojiFont));
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        var previousTheme = context.GetStyle();
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                MultilineElementRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                    next => input = next, Check, context.State, vector);
        }
        context.SetTheme(previousTheme);
    }
    context.SetTypographyFonts(new(Path.Combine(elementFontDirectory, "segoeui.ttf"), Path.Combine(elementFontDirectory, "seguisb.ttf"),
        Path.Combine(elementFontDirectory, "segoeuib.ttf")));
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        var previousTheme = context.GetStyle();
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                MultilineDragRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                    next => input = next, Check, context.State, vector);
        }
        context.SetTheme(previousTheme);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "multiline", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "containers")
{
    var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
    context.SetPlatformTextBackend(platform.TextBackend);
    context.SetDirectTextEnabled(true);
    context.SetDirectTextPrimaryFontPath(Path.Combine(fonts, "segoeui.ttf"));
    context.SetDirectTextBaseFontSize(18);
    context.SetTypographyFonts(new(Path.Combine(fonts, "segoeui.ttf"), Path.Combine(fonts, "seguisb.ttf"), Path.Combine(fonts, "segoeuib.ttf")));
    ContainerWidgetTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        ContainerRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "containers", checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "constrained-leaves")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    LeafConstraintTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        LeafConstraintRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
        SliderRenderingTests.Run((name, draw) => Capture($"msaa{samples}-prior-{name}", draw), Check);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "constrained-leaves", checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "input-allocation")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    InputAllocationTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        InputAllocationRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "input-allocation", checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "allocation-routing")
{
    // Coordinate comparisons use one clock sample; dedicated motion profiles advance time.
    captureDeltaSeconds = 0;
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    AllocationRoutingTests.Run(Check);
    ScrollRoutingTests.Run(Check);
    LeafConstraintTests.Run(Check);
    InputAllocationTests.Run(Check);
    RadioAllocationTests.Run(Check);
    SpinnerAllocationTests.Run(Check);
    ProgressAllocationTests.Run(Check);
    TextAllocationTests.Run(Check);
    DividerAllocationTests.Run(Check);
    LinkAllocationTests.Run(Check);
    BadgeAllocationTests.Run(Check);
    FieldWidgetTests.Run(Check);
    ContainerWidgetTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light
                ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
            {
                AllocationRoutingRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, vector);
                ScrollRoutingRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                    Check, context.State, value => input = value, vector);
                ScrollViewRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                    Check, context.State, context.GetStyleData().ScrollbarSize, vector);
                VirtualListRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                    Check, context.State, vector);
            }
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "allocation-routing", checks.Count, checks.ToArray(),
        0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] == "field-allocation")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    FieldAllocationTests.Run(Check);
    FieldRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light
                ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                FieldAllocationRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, vector);
        }
    }
    foreach (var vector in new[] { false, true })
        FieldAllocationRenderingTests.VerifyRetained(Capture, Check, vector);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "field-allocation", checks.Count, checks.ToArray(),
        0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] == "badge-allocation")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    BadgeAllocationTests.Run(Check);
    BadgeRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light
                ? UiCompiledDesign.Windows11.Theme
                : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                BadgeAllocationRenderingTests.Run(
                    (name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, vector);
        }
    }
    foreach (var vector in new[] { false, true })
        BadgeAllocationRenderingTests.VerifyRetained(Capture, Check, vector);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "badge-allocation", checks.Count, checks.ToArray(),
        0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] == "link-allocation")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    LinkAllocationTests.Run(Check);
    LinkRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light
                ? UiCompiledDesign.Windows11.Theme
                : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                LinkAllocationRenderingTests.Run(
                    (name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, vector);
        }
    }
    foreach (var vector in new[] { false, true })
        LinkAllocationRenderingTests.VerifyRetained(Capture, Check, vector);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "link-allocation", checks.Count, checks.ToArray(),
        0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] == "divider-allocation")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    DividerAllocationTests.Run(Check);
    DividerRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light
                ? UiCompiledDesign.Windows11.Theme
                : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                DividerAllocationRenderingTests.Run(
                    (name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, vector);
        }
    }
    foreach (var vector in new[] { false, true })
        DividerAllocationRenderingTests.VerifyRetained(Capture, Check, vector);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "divider-allocation", checks.Count, checks.ToArray(),
        0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] == "text-allocation")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    TextAllocationTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light
                ? UiCompiledDesign.Windows11.Theme
                : UiCompiledDesign.Windows11Dark.Theme);
            foreach (var vector in new[] { false, true })
                TextAllocationRenderingTests.Run(
                    (name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check, vector);
        }
    }
    foreach (var vector in new[] { false, true })
        TextAllocationRenderingTests.VerifyRetained(Capture, Check, vector);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "text-allocation", checks.Count, checks.ToArray(),
        0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] == "progress-allocation")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ProgressAllocationTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light
                ? UiCompiledDesign.Windows11.Theme
                : UiCompiledDesign.Windows11Dark.Theme);
            ProgressAllocationRenderingTests.Run(
                (name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check);
        }
    }
    ProgressRenderingTests.Run(Capture, Build, Check, size.Width, size.Height);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "progress-allocation", checks.Count, checks.ToArray(),
        0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}

if (args.Length >= 2 && args[1] == "spinner-allocation")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    SpinnerAllocationTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            context.SetTheme(scheme == UiFluentColorScheme.Light
                ? UiCompiledDesign.Windows11.Theme
                : UiCompiledDesign.Windows11Dark.Theme);
            SpinnerAllocationRenderingTests.Run(
                (name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw), Check);
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "spinner-allocation", checks.Count, checks.ToArray(),
        0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "leaf-layout")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        LeafLayoutRetentionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "leaf-layout", checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "content-layout" or "form-layout" or "tab-measurement" or "delegated-layout" or "owned-sizing")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        if (args[1] == "owned-sizing")
            OwnedChildSizingRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
        else if (args[1] == "delegated-layout")
            DelegatedLayoutRetentionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
        else if (args[1] == "tab-measurement")
            TabMeasurementRetentionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
        else if (args[1] == "form-layout")
            FormLayoutRetentionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
        else
            ContentLayoutRetentionRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] is "radio-allocation" or "radio-allocation-matched")
{
    input = input with
    {
        MousePosition = new(-100, -100)
    };
    ControlFontFixture.Configure(context);
    if (matchedRadio)
    {
        Console.WriteLine(FormattableString.Invariant($"RadioGroup framebuffer={size.Width}x{size.Height}, hostScale={platform.ContentScale:R}"));
        context.SetDirectTextBaseFontSize(16);
        context.SetDirectTextFallbackEnabled(false);
    }
    RadioAllocationTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        RadioAllocationRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
    }
    RadioGroupRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "animation-visibility")
{
    FrameSchedulingTests.Run(Check);
    foreach (var samples in new[] { 1, 4 })
    {
        renderer.SetMsaaSamples(samples);
        AnimationVisibilityRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{name}", draw), Check);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "animation-visibility", checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    return;
}
if (args.Length >= 2 && args[1] == "particle-full")
{
    GpuParticleTests.Run(renderer, Build, Capture, Check, white, bounds, output);
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, "particle-full", checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    Console.WriteLine($"Passed {checks.Count} particle checks.");
    return;
}
if (args.Length >= 2 && args[1] is "input-kernel" or "fluent-input" or "field" or "measure" or "spinner")
{
    var directory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
    context.SetPlatformTextBackend(platform.TextBackend);
    context.SetDirectTextEnabled(true);
    context.SetDirectTextPrimaryFontPath(Path.Combine(directory, "segoeui.ttf"));
    context.SetDirectTextBaseFontSize(18);
    context.SetTypographyFonts(new(Path.Combine(directory, "segoeui.ttf"), Path.Combine(directory, "seguisb.ttf"), Path.Combine(directory, "segoeuib.ttf")));
    if (args[1] == "input-kernel")
    {
        InputKernelTests.Run(Check);
        InputKernelRenderingTests.Run(renderer, Capture, next => input = next, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
        var emojiFont = Path.Combine(directory, "seguiemj.ttf");
        if (!File.Exists(emojiFont))
            throw new FileNotFoundException("The Unicode vector fixture requires an explicitly selected emoji font.", emojiFont);
        context.SetTypographyFonts(new(Path.Combine(directory, "segoeui.ttf"), Path.Combine(directory, "seguisb.ttf"),
            Path.Combine(directory, "segoeuib.ttf"), SecondaryRegular: emojiFont));
        foreach (var samples in new[] { 1, 4 })
        {
            renderer.SetMsaaSamples(samples);
            var previousTheme = context.GetStyle();
            foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
            {
                context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
                foreach (var vector in new[] { false, true })
                    SingleLineElementRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                        next => input = next, Check, context.State, vector);
            }
            context.SetTheme(previousTheme);
        }
        foreach (var samples in new[] { 1, 4 })
        {
            renderer.SetMsaaSamples(samples);
            var previousTheme = context.GetStyle();
            foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
            {
                context.SetTheme(scheme == UiFluentColorScheme.Light ? UiCompiledDesign.Windows11.Theme : UiCompiledDesign.Windows11Dark.Theme);
                foreach (var vector in new[] { false, true })
                    SingleLineDragRenderingTests.Run((name, draw) => Capture($"msaa{samples}-{scheme}-{name}", draw),
                        next => input = next, Check, context.State, vector);
            }
            context.SetTheme(previousTheme);
        }
        context.SetTypographyFonts(new(Path.Combine(directory, "segoeui.ttf"), Path.Combine(directory, "seguisb.ttf"), Path.Combine(directory, "segoeuib.ttf")));
    }
    else if (args[1] == "measure")
    {
        WidgetMeasurementTests.Run(Check);
        WidgetMeasurementRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    }
    else if (args[1] == "field")
    {
        FieldWidgetTests.Run(Check);
        FieldRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    }
    else if (args[1] == "spinner")
    {
        SpinnerTests.Run(Check);
        SpinnerRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    }
    else
    {
        FluentInputTests.Run(Check);
        FluentInputRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
        InputAdornmentRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
        InputChildrenRenderingTests.Run(renderer, Capture, next => input = next, context.State, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
        RuntimeInformation.FrameworkDescription, args[1], checks.Count, checks.ToArray(), 0, 0, 0, 0, renderer.UsesPresentationFences), GraphicsJson.Default.GraphicsResult));
    Console.WriteLine($"Passed {checks.Count} {args[1]} checks.");
    return;
}
RoundedGradientTests.Run(renderer, Capture, Build, Check, white, bounds, args.Length >= 2);
ProgressRenderingTests.Run(Capture, Build, Check, size.Width, size.Height);
TextWidgetTests.Run(Check);
BadgeWidgetTests.Run(Check);
ButtonLifetimeTests.Run(Check);
ControlOwnershipTests.Run(Check);
ButtonTypographyTests.Run(Check);
ButtonIconTests.Run(Check);
ButtonAppearanceInputTests.Run(Check);
DividerWidgetTests.Run(Check);
var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
context.SetPlatformTextBackend(platform.TextBackend);
context.SetDirectTextEnabled(true);
context.SetDirectTextPrimaryFontPath(Path.Combine(fontDirectory, "segoeui.ttf"));
context.SetDirectTextBaseFontSize(18);
context.SetTypographyFonts(new(Path.Combine(fontDirectory, "segoeui.ttf"), Path.Combine(fontDirectory, "seguisb.ttf"), Path.Combine(fontDirectory, "segoeuib.ttf"),
    Path.Combine(fontDirectory, "malgun.ttf"), Path.Combine(fontDirectory, "malgunbd.ttf"), Path.Combine(fontDirectory, "malgunbd.ttf")));
DefaultTextRenderingTests.Run(Capture, Check);
BadgeRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
DividerRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
ButtonRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
ButtonAppearanceTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
ButtonShapeTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
ButtonIconRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
FluentIconRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
SliderWidgetTests.Run(Check);
SliderRenderingTests.Run(Capture, Check);
if (!widgetsWithoutResize)
{
    WindowInteractionTests.Run(Check);
    WindowRenderingTests.Run(renderer, Capture, next => input = next, Check, size.Width, size.Height);
}
PanelTests.Run(Check);
DirectTextLayoutTests.Run(Check);
if (rootMsaa4) { renderer.SetMsaaSamples(4); VulkanRendererBackend.ValidationResizableMsaaCapacity = true; }
if (!widgetsWithoutResize)
    RootResizeRenderingTests.Run(renderer, platform, Build, Capture, (w, h) => { size = new(w, h); bounds = new(0, 0, w, h); }, Check);
DirectTextScaleRenderingTests.Run(Check, frameSlots, output);
SharedDepthAttachmentTests.Run(renderer, Build, Capture, Check, white, bounds);
CheckboxWidgetTests.Run(Check);
CheckboxRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
RadioGroupWidgetTests.Run(Check);
RadioGroupRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
ControlTextBlockRenderingTests.Run(renderer, Capture, Check, context.GetStyle, size.Width, size.Height);
SwitchWidgetTests.Run(Check);
SwitchRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
LinkWidgetTests.Run(Check);
LinkRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
FieldWidgetTests.Run(Check);
FieldRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
WidgetMeasurementTests.Run(Check);
WidgetMeasurementRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
InputKernelTests.Run(Check);
InputKernelRenderingTests.Run(renderer, Capture, next => input = next, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
FluentInputTests.Run(Check);
FluentInputRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
InputAdornmentRenderingTests.Run(renderer, Capture, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
InputChildrenRenderingTests.Run(renderer, Capture, next => input = next, context.State, Check, context.GetStyle, context.SetTheme, size.Width, size.Height);
Check(renderer.ValidationFrameResourceCount == frameSlots, "actual frame resources honor the independent frame limit " + frameSlots);
Check(!VulkanRendererBackend.ValidationImageAcquisitionOnly || !renderer.UsesPresentationFences, "explicit core presentation-capability validation does not enable maintenance features");
var mesh = RoundedGradientTests.MeshMetrics;
var packed = RoundedGradientTests.PackedMetrics;
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new GraphicsResult(
    RuntimeInformation.FrameworkDescription, widgetsWithoutResize ? "widgets-no-resize" : args.Length >= 2 ? "packed-first" : "mesh-first", checks.Count, checks.ToArray(),
    mesh.DrawCalls, packed.DrawCalls, mesh.DynamicUploadBytes, packed.DynamicUploadBytes, renderer.UsesPresentationFences,
    widgetsWithoutResize ? [nameof(RootResizeRenderingTests), nameof(WindowInteractionTests), nameof(WindowRenderingTests)] : null), GraphicsJson.Default.GraphicsResult));
Console.WriteLine($"Passed {checks.Count} graphics checks.");

internal sealed record GraphicsResult(string Framework, string Order, int Passed, string[] Checks,
    int MeshDrawCalls, int PackedDrawCalls, ulong MeshUploadBytes, ulong PackedUploadBytes, bool? UsesPresentationFences = null,
    string[]? SkippedTests = null);
internal sealed record CatalogResult(string Framework, string[] Exposed, string[] Required, string[] Missing, bool ScopeComplete);
[JsonSerializable(typeof(GraphicsResult))]
[JsonSerializable(typeof(CatalogResult))]
[JsonSerializable(typeof(InputBatchCpuResult))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "CheckboxPixelHashes")]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class GraphicsJson : JsonSerializerContext;
internal sealed class Screen(Action<UiImmediateContext> render) : UiScreen
{
    public override void Render(UiImmediateContext ui) => render(ui);
}



