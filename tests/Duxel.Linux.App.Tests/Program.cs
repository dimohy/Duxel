using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.App;
using Duxel.Core;
using Duxel.Linux.App;
using Duxel.Linux.Sample;
using Duxel.Platform.Linux;

if (args is ["startup-retry", var retryPrimaryFont, var retrySecondaryFont])
{
    var retryAccessibility = new LinuxAtSpiOptions { Enabled = false };
    AppStartupRetryTests.Run(retryPrimaryFont, retrySecondaryFont,
        (options, session) =>
        {
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException("The startup retry probe requires Linux/X11.");
            DuxelLinuxApp.Run(options, session, retryAccessibility);
        });
    return;
}

if (args is ["x11-ordered-input"])
{
    X11OrderedInputTests.Run();
    return;
}

if (args is ["desktop-typography", var typographyOutput])
{
    DesktopTypographyTests.Run(typographyOutput);
    return;
}

if (args is ["paragraph-source-map", var sourceMapOutput])
{
    ParagraphSourceMappingTests.Run(sourceMapOutput);
    return;
}

if (args is ["atspi-gallery-time-picker" or "atspi-gallery-date-pickers" or "atspi-gallery-holidays" or "atspi-gallery-rating" or "atspi-gallery-navigation-bar" or "atspi-gallery-navigation-repaint"
    or "atspi-gallery-provider-direction" or "atspi-gallery-tree" or "atspi-gallery-virtual-list" or "atspi-gallery-virtual-horizontal" or "atspi-gallery-virtual-rtl" or "atspi-gallery-virtual-grid"
    or "atspi-gallery-range-calendar" or "atspi-gallery-tooltip" or "atspi-gallery-carousel",
    var galleryPid, var galleryClientOutput])
{
    var probe = args[0] switch
    {
        "atspi-gallery-date-pickers" => AtSpiProbe.GalleryProbe.DatePickers,
        "atspi-gallery-holidays" => AtSpiProbe.GalleryProbe.Holidays,
        "atspi-gallery-rating" => AtSpiProbe.GalleryProbe.Rating,
        "atspi-gallery-navigation-bar" => AtSpiProbe.GalleryProbe.NavigationBar,
        "atspi-gallery-navigation-repaint" => AtSpiProbe.GalleryProbe.NavigationRepaint,
        "atspi-gallery-range-calendar" => AtSpiProbe.GalleryProbe.RangeCalendar,
        "atspi-gallery-tooltip" => AtSpiProbe.GalleryProbe.Tooltip,
        "atspi-gallery-carousel" => AtSpiProbe.GalleryProbe.Carousel,
        "atspi-gallery-provider-direction" => AtSpiProbe.GalleryProbe.ProviderDirection,
        "atspi-gallery-tree" => AtSpiProbe.GalleryProbe.Tree,
        "atspi-gallery-virtual-list" => AtSpiProbe.GalleryProbe.VirtualList,
        "atspi-gallery-virtual-horizontal" => AtSpiProbe.GalleryProbe.VirtualHorizontal,
        "atspi-gallery-virtual-rtl" => AtSpiProbe.GalleryProbe.VirtualRtl,
        "atspi-gallery-virtual-grid" => AtSpiProbe.GalleryProbe.VirtualGrid,
        _ => AtSpiProbe.GalleryProbe.TimePicker
    };
    NativeProbeFailure.Capture(galleryClientOutput, () => AtSpiProbe.RunGalleryControls(
        uint.Parse(galleryPid, System.Globalization.CultureInfo.InvariantCulture), galleryClientOutput, probe));
    return;
}

if (args is ["x11-gallery-time-picker-wheel" or "x11-gallery-scroll-view" or "x11-gallery-scroll-profile" or "x11-gallery-search-input" or "x11-gallery-pixels" or "x11-gallery-svg-pixels" or "x11-gallery-svg-thumb" or "x11-gallery-svg-layer-mix" or "x11-gallery-range-calendar" or "x11-gallery-range-calendar-pixels" or "x11-gallery-radio-group" or "x11-gallery-radio-allocation-pixels" or "x11-gallery-tree-actions" or "x11-gallery-tooltip" or "x11-gallery-provider-radio-direction" or "x11-gallery-fps" or "x11-gallery-drawer" or "x11-gallery-dialog" or "x11-gallery-card-nudge" or "x11-gallery-menu-subtext" or "x11-gallery-tag-picker" or "x11-gallery-select" or "x11-gallery-combobox" or "x11-gallery-dropdown",
    var wheelGalleryPid, var wheelClientOutput, var wheelWindow])
{
    NativeProbeFailure.Capture(wheelClientOutput, () => AtSpiProbe.RunGalleryControls(
        uint.Parse(wheelGalleryPid, System.Globalization.CultureInfo.InvariantCulture), wheelClientOutput,
        args[0] switch
        {
            "x11-gallery-time-picker-wheel" => AtSpiProbe.GalleryProbe.TimePicker,
            "x11-gallery-range-calendar" or "x11-gallery-range-calendar-pixels" => AtSpiProbe.GalleryProbe.RangeCalendar,
            "x11-gallery-radio-group" or "x11-gallery-radio-allocation-pixels" => AtSpiProbe.GalleryProbe.RadioGroup,
            "x11-gallery-menu-subtext" => AtSpiProbe.GalleryProbe.MenuSubText,
            "x11-gallery-tree-actions" => AtSpiProbe.GalleryProbe.Tree,
            "x11-gallery-tooltip" => AtSpiProbe.GalleryProbe.Tooltip,
            "x11-gallery-provider-radio-direction" => AtSpiProbe.GalleryProbe.ProviderDirection,
            "x11-gallery-svg-pixels" => AtSpiProbe.GalleryProbe.SvgPixels,
            "x11-gallery-svg-thumb" => AtSpiProbe.GalleryProbe.SvgThumb,
            "x11-gallery-svg-layer-mix" => AtSpiProbe.GalleryProbe.SvgLayerMix,
            "x11-gallery-fps" => AtSpiProbe.GalleryProbe.Fps,
            "x11-gallery-drawer" => AtSpiProbe.GalleryProbe.Drawer,
            "x11-gallery-dialog" => AtSpiProbe.GalleryProbe.Dialog,
            "x11-gallery-tag-picker" => AtSpiProbe.GalleryProbe.TagPicker,
            "x11-gallery-select" => AtSpiProbe.GalleryProbe.Select,
            "x11-gallery-combobox" => AtSpiProbe.GalleryProbe.Combobox,
            "x11-gallery-dropdown" => AtSpiProbe.GalleryProbe.Dropdown,
            "x11-gallery-card-nudge" => AtSpiProbe.GalleryProbe.CardNudge,
            "x11-gallery-search-input" or "x11-gallery-pixels" => AtSpiProbe.GalleryProbe.SearchInput,
            _ => AtSpiProbe.GalleryProbe.ScrollView
        },
        nativeWindow : nuint.Parse(wheelWindow, System.Globalization.CultureInfo.InvariantCulture),
        profileScroll : args[0] == "x11-gallery-scroll-profile",
        capturePixels : args[0] is "x11-gallery-pixels" or "x11-gallery-range-calendar-pixels" or "x11-gallery-radio-allocation-pixels"));
    return;
}

if (args is ["atspi-messagebar", var messageBarParent, var messageBarClientOutput, _])
{
    NativeProbeFailure.Capture(messageBarClientOutput, () => AtSpiProbe.RunAnnouncements(
        uint.Parse(messageBarParent, System.Globalization.CultureInfo.InvariantCulture), messageBarClientOutput, automatic: true));
    return;
}
if (args is ["messagebar-native", var messageBarOutput, var messageBarLatin, var messageBarKorean])
{
    AnnouncementAccessibilityLinuxHost.RunMessageBars(messageBarOutput, messageBarLatin, messageBarKorean);
    return;
}

if (args is ["atspi-announcement", var announcementParent, var announcementClientOutput, _])
{
    NativeProbeFailure.Capture(announcementClientOutput, () => AtSpiProbe.RunAnnouncements(
        uint.Parse(announcementParent, System.Globalization.CultureInfo.InvariantCulture), announcementClientOutput));
    return;
}
if (args is ["announcement-native", var announcementOutput, var announcementLatin, var announcementKorean])
{
    AnnouncementAccessibilityLinuxHost.Run(announcementOutput, announcementLatin, announcementKorean);
    return;
}

if (args is ["atspi-skeleton", var skeletonParent, var skeletonClientOutput, _])
{
    NativeProbeFailure.Capture(skeletonClientOutput, () => AtSpiProbe.RunSkeleton(
        uint.Parse(skeletonParent, System.Globalization.CultureInfo.InvariantCulture), skeletonClientOutput));
    return;
}
if (args is ["skeleton-native", var skeletonOutput, var skeletonLatin, var skeletonKorean])
{
    SkeletonAccessibilityLinuxHost.Run(skeletonOutput, skeletonLatin, skeletonKorean);
    return;
}

if (args is ["result-contract", var resultContractOutput])
{
    LinuxProbeResultContracts.Run(resultContractOutput);
    return;
}

if (args is ["atspi-dialog", var dialogParentId, var dialogClientOutput, var dialogXWindow])
{
    NativeProbeFailure.Capture(dialogClientOutput, () => AtSpiProbe.RunDialog(
        uint.Parse(dialogParentId, System.Globalization.CultureInfo.InvariantCulture), dialogClientOutput,
        nuint.Parse(dialogXWindow, System.Globalization.CultureInfo.InvariantCulture)));
    return;
}
if (args is ["dialog-native", var dialogNativeOutput, var dialogLatin, var dialogKorean])
{
    DialogAccessibilityLinuxHost.Run(dialogNativeOutput, dialogLatin, dialogKorean);
    return;
}

if (args is [var imageComponentMode, var imageComponentParent, var imageComponentOutput, _]
    && imageComponentMode is "atspi-image-component-view-local" or "atspi-image-component-view-nested" or "atspi-image-component-view-rtl"
        or "atspi-image-component-editor-local" or "atspi-image-component-editor-nested" or "atspi-image-component-editor-rtl")
{
    NativeProbeFailure.Capture(imageComponentOutput, () => AtSpiProbe.RunImageComponent(
        uint.Parse(imageComponentParent, System.Globalization.CultureInfo.InvariantCulture), imageComponentOutput,
        !imageComponentMode.EndsWith("-local", StringComparison.Ordinal), imageComponentMode.EndsWith("-rtl", StringComparison.Ordinal)));
    return;
}

if (args is ["atspi-component-scroll", var componentParent, var componentOutput, _])
{
    NativeProbeFailure.Capture(componentOutput, () => AtSpiProbe.RunComponentScroll(uint.Parse(componentParent, System.Globalization.CultureInfo.InvariantCulture), componentOutput));
    return;
}

if (args is ["atspi-attributes", var attributeParent, var attributeOutput, _, _])
{
    NativeProbeFailure.Capture(attributeOutput, () => AtSpiProbe.RunAttributes(uint.Parse(attributeParent, System.Globalization.CultureInfo.InvariantCulture), attributeOutput));
    return;
}

if (args is ["owned-managed-failure", _, var failureContractOutput, _, _])
{
    NativeProbeFailure.Capture(failureContractOutput, () => throw new InvalidOperationException("controlled owned probe assertion"));
    return;
}

if (args is ["atspi-client", var parentPid, var probeOutput, var nativeWindow, var privateSelection])
{
    NativeProbeFailure.Capture(probeOutput, () => AtSpiProbe.Run(uint.Parse(parentPid, System.Globalization.CultureInfo.InvariantCulture), probeOutput,
        nuint.Parse(nativeWindow, System.Globalization.CultureInfo.InvariantCulture), privateSelection));
    return;
}
if (args is [var textMode, var focusedOutput, var focusedPrimary, var focusedSecondary]
    && (textMode is "image-component-model" or "component-scroll-model" or "text-scroll-common-regression" or "text-scroll-virtual" or "text-scroll-variable" or "text-scroll-popup" or "text-layout-composition" or "markdown-image-accessibility" or "markdown-image-keyboard"))
{
    if (!OperatingSystem.IsLinux())
        throw new PlatformNotSupportedException("The focused text layout fixture requires Linux fonts.");
    if (Directory.Exists(focusedOutput))
        throw new IOException("Use a fresh focused result directory.");
    Directory.CreateDirectory(focusedOutput);
    var focusedChecks = new List<string>();
    void FocusedCheck(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        focusedChecks.Add(name);
        Console.WriteLine("PASS " + name);
    }
    if (textMode == "image-component-model")
        MarkdownImageComponentScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
    else if (textMode == "component-scroll-model")
        ComponentScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
    else if (textMode == "text-scroll-common-regression")
    {
        TextRangeScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
        SingleLineTextScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
        foreach (var (outerRtl, innerRtl) in new[] { (false, false), (true, true), (true, false), (false, true) })
            AncestorTextScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck, outerRtl, innerRtl);
        VirtualTextScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
        VirtualTextScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck, variableExtents: true);
        PopoverTextScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
        PopoverTextScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck, trapFocus: true);
    }
    else if (textMode is "text-scroll-virtual" or "text-scroll-variable")
        VirtualTextScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck, variableExtents: textMode == "text-scroll-variable");
    else if (textMode == "text-scroll-popup")
    {
        PopoverTextScrollTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
        PopoverTextScrollTests.Run(focusedPrimary, focusedSecondary,
            (condition, name) => FocusedCheck(condition, "Modal: " + name), trapFocus: true);
    }
    else if (textMode == "markdown-image-accessibility")
        MarkdownImageAccessibilityTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
    else if (textMode == "markdown-image-keyboard")
        MarkdownImageKeyboardTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
    else
        CompositionLayoutTests.Run(focusedPrimary, focusedSecondary, FocusedCheck);
    File.WriteAllText(Path.Combine(focusedOutput, "result.json"), JsonSerializer.Serialize(new LinuxChecksResult(RuntimeInformation.FrameworkDescription, textMode, focusedChecks.Count, focusedChecks), LinuxProbeResultsJson.Default.LinuxChecksResult));
    return;
}
if (args is ["atspi-absent", var retiredPid, var retiredOutput])
{
    AtSpiProbe.AssertAbsent(uint.Parse(retiredPid, System.Globalization.CultureInfo.InvariantCulture), retiredOutput);
    return;
}
if (args is ["atspi-markdown", var markdownPid, var markdownOutput, var markdownWindow])
{
    AtSpiProbe.RunMarkdown(uint.Parse(markdownPid, System.Globalization.CultureInfo.InvariantCulture), markdownOutput,
        nuint.Parse(markdownWindow, System.Globalization.CultureInfo.InvariantCulture));
    return;
}
if (args is ["atspi-range-policy", var policyPid, var policyOutput])
{
    AtSpiProbe.VerifyRangePolicy(uint.Parse(policyPid, System.Globalization.CultureInfo.InvariantCulture), policyOutput);
    return;
}
if (args is ["atspi-ancestor", var ancestorPid, var ancestorOutput, _])
{
    AtSpiProbe.RunAncestor(uint.Parse(ancestorPid, System.Globalization.CultureInfo.InvariantCulture), ancestorOutput);
    return;
}
if (args is ["atspi-ancestor-rtl", var rtlPid, var rtlOutput, _])
{
    AtSpiProbe.RunAncestor(uint.Parse(rtlPid, System.Globalization.CultureInfo.InvariantCulture), rtlOutput, rightToLeft: true);
    return;
}
if (args is ["atspi-virtual-text", var virtualPid, var virtualNativeOutput, _])
{
    AtSpiProbe.RunAncestor(uint.Parse(virtualPid, System.Globalization.CultureInfo.InvariantCulture), virtualNativeOutput, virtualized: true);
    return;
}
if (args is ["atspi-variable-text", var variablePid, var variableNativeOutput, _])
{
    AtSpiProbe.RunAncestor(uint.Parse(variablePid, System.Globalization.CultureInfo.InvariantCulture), variableNativeOutput,
        virtualized: true, variableExtents: true);
    return;
}
if (args is [var popupProfile, var popupPid, var popupOutput, _]
    && popupProfile is "atspi-popup-single" or "atspi-popup-multiline" or "atspi-popup-markdown")
{
    AtSpiProbe.RunAncestor(uint.Parse(popupPid, System.Globalization.CultureInfo.InvariantCulture), popupOutput,
        popupMode: popupProfile["atspi-popup-".Length..]);
    return;
}
if (args is ["atspi-markdown-images", var imagePid, var imageOutput, var imageWindow])
{
    AtSpiProbe.RunImages(uint.Parse(imagePid, System.Globalization.CultureInfo.InvariantCulture), imageOutput,
        nuint.Parse(imageWindow, System.Globalization.CultureInfo.InvariantCulture));
    return;
}
if (args is ["transport-failure", _, var failureOutput])
{
    TransportFailureTests.Run(failureOutput);
    return;
}
if (args is ["pango-failure", _, var pangoFailureOutput])
{
    TransportFailureTests.Run(pangoFailureOutput, missingPango: true);
    return;
}

if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("The application fixture requires actual Linux/X11.");
var variableNativeOnly = args.Length == 4 && args[0] == "text-scroll-variable-native";
var componentNativeOnly = args.Length == 4 && args[0] == "component-scroll-native";
var imageComponentNativeOnly = args.Length == 4 && args[0] == "image-component-native";
var popupNativeOnly = args.Length == 4 && args[0] == "text-scroll-popup-native";
var imageNativeOnly = args.Length == 4 && args[0] == "markdown-image-accessibility-native";
var attributesOnly = args.Length == 4 && args[0] == "text-attributes-native";
var probeFailureContract = args.Length == 4 && args[0] == "probe-failure-contract";
var policyOnly = args.Length == 4 && args[0] == "range-selection-policy-native";
var nativeClientOnly = attributesOnly || args.Length == 4 && args[0] == "native-client-only";
if (imageComponentNativeOnly || componentNativeOnly || variableNativeOnly || popupNativeOnly || imageNativeOnly || nativeClientOnly || probeFailureContract || policyOnly)
    args = args[1..];
if (args.Length != 3)
    throw new ArgumentException("Specify a fresh result directory and explicit Latin/Korean font paths.");
var output = Path.GetFullPath(args[0]);
if (Directory.Exists(output))
    throw new IOException("Result directory already exists: " + output);
Directory.CreateDirectory(output);
var checks = new List<string>();
var observations = new List<LinuxApplicationObservation>();
void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
    checks.Add(message);
    Console.WriteLine("PASS " + message);
}

void NativeProbe(string mode, string suffix, bool unreachableBus = false, nuint? nativeWindow = null, string? privateSelection = null, Action<string>? observe = null)
{
    var start = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    start.ArgumentList.Add(mode);
    start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    start.ArgumentList.Add(Path.Combine(output, mode + "-" + suffix + ".json"));
    if (nativeWindow.HasValue)
        start.ArgumentList.Add(nativeWindow.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    if (privateSelection is not null)
        start.ArgumentList.Add(privateSelection);
    if (unreachableBus)
        start.Environment["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=/tmp/duxel-fixture-no-bus-" + Guid.NewGuid().ToString("N");
    using var probe = Process.Start(start) ?? throw new InvalidOperationException("The owned native probe did not start.");
    var stdout = probe.StandardOutput.ReadToEndAsync();
    var stderr = probe.StandardError.ReadToEndAsync();
    var deadline = Stopwatch.StartNew();
    var ended = false;
    ExceptionDispatchInfo? observationFailure = null;
    try
    {
        while (!(ended = probe.WaitForExit(10)) && deadline.ElapsedMilliseconds < 12000)
            observe?.Invoke(Path.Combine(output, mode + "-" + suffix + ".json"));
    }
    catch (Exception error) { observationFailure = ExceptionDispatchInfo.Capture(error); }
    if (!ended)
    {
        if (!probe.HasExited)
            probe.Kill(entireProcessTree: true);
        probe.WaitForExit();
    }
    File.WriteAllText(Path.Combine(output, mode + "-" + suffix + ".stdout.log"), stdout.GetAwaiter().GetResult());
    File.WriteAllText(Path.Combine(output, mode + "-" + suffix + ".stderr.log"), stderr.GetAwaiter().GetResult());
    observationFailure?.Throw();
    if (!ended)
        throw new TimeoutException("The owned native probe exceeded its bounded run.");
    if (probe.ExitCode != 0)
    {
        var failure = NativeProbeFailure.Read(Path.Combine(output, mode + "-" + suffix + ".json"));
        if (failure is not null)
            throw new NativeProbeClientException(mode, probe.ExitCode, failure);
        throw new InvalidOperationException("The owned native probe failed: " + mode + ";exit=" + probe.ExitCode);
    }
}

if (policyOnly)
{
    RunRangeSelectionPolicy();
    return;
}

if (probeFailureContract)
{
    var captured = false;
    try
    {
        NativeProbe("owned-managed-failure", "contract", nativeWindow: 0, privateSelection: "owned-fixture");
    }
    catch (NativeProbeClientException error) when (error.Failure is
    { Kind: "managed-client-exception", ExceptionType: "System.InvalidOperationException", Message: "controlled owned probe assertion" })
    {
        captured = true;
    }
    Check(captured, "The parent preserves the managed child assertion instead of classifying exit134 as an unexplained native crash");
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(
        new NativeFailureContractResult(checks.Count, true), NativeProbeFailureJson.Default.NativeFailureContractResult));
    return;
}

if (!imageComponentNativeOnly && !componentNativeOnly && !variableNativeOnly && !popupNativeOnly && !imageNativeOnly && !nativeClientOnly)
{
    var boundaryCheckStart = checks.Count;
    var boundaryVersion = NativeBoundaryTests.Run(Check);
    NativeLegacyBoundaryTests.Run(Check);
    NativeLegacyLineTests.Run(args[1], args[2], Check);
    StyledTextLayoutTests.Run(args[1], args[2], Check);
    CompositionLayoutTests.Run(args[1], args[2], Check);
    UnwrappedTextLayoutTests.Run(args[1], args[2], Check);
    TextRangeScrollTests.Run(args[1], args[2], Check);
    TextBoundedRangeTests.Run(args[1], args[2], Check);
    SingleLineTextScrollTests.Run(args[1], args[2], Check);
    AncestorTextScrollTests.Run(args[1], args[2], Check);
    foreach (var (outerRtl, innerRtl) in new[] { (true, true), (true, false), (false, true) })
        AncestorTextScrollTests.Run(args[1], args[2], (condition, name) => Check(condition,
            $"RTL outer={outerRtl}, inner={innerRtl}: {name}"), outerRtl, innerRtl);
    VirtualTextScrollTests.Run(args[1], args[2], Check);
    VirtualTextScrollTests.Run(args[1], args[2], (condition, name) => Check(condition, "Variable extent: " + name), variableExtents: true);
    PopoverTextScrollTests.Run(args[1], args[2], Check);
    PopoverTextScrollTests.Run(args[1], args[2], (condition, name) => Check(condition, "Modal: " + name), trapFocus: true);
    MarkdownImageAccessibilityTests.Run(args[1], args[2], Check);
    MarkdownImageKeyboardTests.Run(args[1], args[2], Check);
    File.WriteAllText(Path.Combine(output, "boundary-contract.json"), JsonSerializer.Serialize(new LinuxBoundaryResult(boundaryVersion, checks.Count - boundaryCheckStart, checks.Skip(boundaryCheckStart).ToArray()), LinuxProbeResultsJson.Default.LinuxBoundaryResult));

    var fontCheckStart = checks.Count;
    var fontVersion = NativeFontRunTests.Run(args[1], args[2], Check);
    File.WriteAllText(Path.Combine(output, "font-run-contract.json"), JsonSerializer.Serialize(new LinuxFontResult(fontVersion, checks.Count - fontCheckStart, checks.Skip(fontCheckStart).ToArray()), LinuxProbeResultsJson.Default.LinuxFontResult));
}

DuxelAppOptions Options(LinuxDemoScreen screen, Action<nint> created, UiScreen? view = null) => DuxelApp.Options(view ?? screen,
    "Duxel owned Linux application fixture", LinuxDemoScreen.WindowWidth, LinuxDemoScreen.WindowHeight, vsync: false) with
{
    Window = new()
    {
        Width = LinuxDemoScreen.WindowWidth,
        Height = LinuxDemoScreen.WindowHeight,
        Title = "Duxel owned Linux application fixture",
        VSync = false,
        WindowCreated = created
    },
    Font = new()
    {
        PrimaryFontPath = args[1],
        SecondaryFontPath = args[2],
        Typography = new(args[1], args[1], args[1], args[2], args[2], args[2])
        {
            Monospace = args[1]
        },
        FastStartup = false,
        FontSize = 18
    },
    Frame = new()
    {
        UseEventDrivenPlatformLoop = true
    },
    Debug = new()
    {
        LogEveryNFrames = 1,
        Log = Console.WriteLine
    }
};

var validationSession = new DuxelAppSession();
using var validationScreen = new LinuxDemoScreen(validationSession);
if (imageComponentNativeOnly || componentNativeOnly || variableNativeOnly || popupNativeOnly || imageNativeOnly)
{
    foreach (var registered in new[] { false, true })
    {
        if (imageComponentNativeOnly)
        {
            foreach (var editor in new[] { false, true })
                foreach (var (nested, rtl) in new[] { (false, false), (true, false), (true, true) })
                {
                    using var surface = new MarkdownImageComponentScrollTests.Scene(editor, nested, rtl);
                    var name = "image-component-" + (editor ? "editor-" : "view-") + (nested ? rtl ? "rtl" : "nested" : "local");
                    RunOwnedTextProbe(registered, surface, surface.FirstFrame, "atspi-" + name, name);
                }
        }
        else if (componentNativeOnly)
        {
            using var surface = new ComponentScrollTests.Scene();
            RunOwnedTextProbe(registered, surface, surface.FirstFrame, "atspi-component-scroll", "component-scroll");
        }
        else if (variableNativeOnly)
        {
            using var surface = new VirtualTextProbeScreen(variableExtents: true);
            RunOwnedTextProbe(registered, surface, surface.FirstFrame, "atspi-variable-text", "variable-text");
        }
        else if (popupNativeOnly)
            foreach (var mode in new[] { "single", "multiline", "markdown" })
            {
                using var surface = new PopoverTextScrollTests.NativeScreen(mode);
                RunOwnedTextProbe(registered, surface, surface.FirstFrame, "atspi-popup-" + mode, "popup-" + mode);
            }
        else
        {
            using var surface = new MarkdownImageAccessibilityTests.NativeScreen();
            RunOwnedTextProbe(registered, surface, surface.FirstFrame, "atspi-markdown-images", "markdown-images");
        }
    }
    File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxChecksResult(RuntimeInformation.FrameworkDescription, imageComponentNativeOnly ? "image-component-native" : componentNativeOnly ? "component-scroll-native" : variableNativeOnly ? "text-scroll-variable-native" : popupNativeOnly ? "text-scroll-popup-native" : "markdown-image-accessibility-native", checks.Count, checks), LinuxProbeResultsJson.Default.LinuxChecksResult));
    return;
}
var createdCount = 0;
var valid = Options(validationScreen, _ => createdCount++);
var missingRejected = false;
try
{
    DuxelLinuxApp.Run(valid with
    {
        Font = valid.Font with
        {
            PrimaryFontPath = Path.Combine(output, "missing.ttf")
        }
    }, validationSession);
}
catch (FileNotFoundException) { missingRejected = true; }
Check(missingRejected && createdCount == 0, "Missing explicit fonts fail before creating a window");
var chromeRejected = false;
try
{
    DuxelLinuxApp.Run(valid with
    {
        Window = valid.Window with
        {
            UseDuxelTitleBar = true
        }
    }, validationSession);
}
catch (NotSupportedException) { chromeRejected = true; }
Check(chromeRejected && createdCount == 0, "Unsupported native chrome fails before creating a window");
var timeoutRejected = false;
try
{
    DuxelLinuxApp.Run(valid, validationSession, new()
    {
        CallTimeoutMilliseconds = 0
    });
}
catch (ArgumentOutOfRangeException) { timeoutRejected = true; }
Check(timeoutRejected && createdCount == 0, "Invalid AT-SPI timeout fails before creating a window");

foreach (var registered in new[] { false, true })
{
    var session = registered ? DuxelApp.PrimarySession : new DuxelAppSession();
    using var screen = new LinuxDemoScreen(session);
    var selection = "_DUXEL_APP_CLIPBOARD_" + Guid.NewGuid().ToString("N");
    using var clipboard = new LinuxX11Clipboard(new()
    {
        Selection = selection
    });
    using var clipboardPeer = new LinuxX11Clipboard(new()
    {
        Selection = selection
    });
    var nativeTextEntered = false;
    var copied = false;
    var pasted = false;
    var unicodeTextState = false;
    Task? driver = null;
    ExceptionDispatchInfo? driverFailure = null;
    double idleCpuMilliseconds = 0;
    var idleFrames = -1;
    var workerFrames = 0;
    var nativeCloseSent = false;
    var capturedColors = 0;
    var imagePixelsMatch = false;
    var transformedSvgReused = false;
    var nativePointerClicked = false;
    var nativeAccessibility = false;
    var nativeAccessibilityWindowFocus = false;
    var firstPresented = 0;
    void Wait(Func<bool> condition, string phase)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds >= 8000)
                throw new TimeoutException("Application fixture phase: " + phase);
            Thread.Sleep(5);
        }
    }
    var paintProbe = new TextPaintProbe(screen, session);
    var options = Options(screen, handle =>
    {
        driver = Task.Run(() =>
        {
            using var peer = new NativePeer((nuint)handle);
            try
            {
                peer.BeginNativeObservation();
                Wait(() => screen.RenderCount > 0, "first real UI frame");
                if (attributesOnly)
                {
                    Wait(() => Volatile.Read(ref firstPresented) == 1, "first presented attribute frame");
                    NativeProbe("atspi-attributes", registered.ToString(), nativeWindow: (nuint)handle, privateSelection: selection);
                    session.Exit();
                    return;
                }
                string? previousCaptureState = null;
                Wait(() =>
                {
                    var ready = peer.CanCapture(LinuxDemoScreen.WindowWidth, LinuxDemoScreen.WindowHeight, out var state);
                    if (state != previousCaptureState)
                    {
                        Console.WriteLine("Capture precondition: " + state);
                        previousCaptureState = state;
                    }
                    return ready;
                }, "mapped, fully visible XGetImage rectangle");
                Wait(() => Volatile.Read(ref firstPresented) == 1, "the first completed Vulkan frame presentation");
                var settle = Stopwatch.StartNew();
                var start = 0;
                var prepared = false;
                while (true)
                {
                    if (settle.ElapsedMilliseconds >= 8000)
                        throw new TimeoutException("The shared app loop did not reach idle.");
                    peer.DrainNativeObservation();
                    start = screen.RenderCount;
                    var cpu = Process.GetCurrentProcess().TotalProcessorTime;
                    Thread.Sleep(400);
                    idleCpuMilliseconds = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds;
                    idleFrames = screen.RenderCount - start;
                    var nativeEvents = peer.DrainNativeObservation();
                    Console.WriteLine("Idle observation: registered=" + registered + ";frames=" + idleFrames + ";nativeEvents=" + string.Join(',', nativeEvents));
                    if (nativeEvents.Contains(17))
                        throw new InvalidOperationException("The owned X11 fixture window retired while its application session was running.");
                    if (nativeEvents.Length != 0)
                    {
                        prepared = false;
                        continue;
                    }
                    if (!prepared)
                    {
                        prepared = idleFrames == 0;
                        continue;
                    }
                    if (idleFrames != 0)
                        throw new InvalidOperationException("Native-event-free idle UI rendered without an invalidation.");
                    break;
                }
                capturedColors = peer.Capture(Path.Combine(output, registered ? "registered-window.ppm" : "session-window.ppm"), LinuxDemoScreen.WindowWidth, LinuxDemoScreen.WindowHeight, pixels =>
                {
                    bool Pixel(int x, int y, byte r, byte g, byte b)
                    {
                        var i = (y * LinuxDemoScreen.WindowWidth + x) * 3;
                        return Math.Abs(pixels[i] - r) <= 1 && Math.Abs(pixels[i + 1] - g) <= 1 && Math.Abs(pixels[i + 2] - b) <= 1;
                    }
                    imagePixelsMatch = Pixel(448, 85, 230, 50, 70) && Pixel(550, 85, 22, 125, 189) && Pixel(550, 150, 22, 125, 189);
                });
                screen.ChangeFromWorker(42);
                Wait(() => screen.RenderCount > start, "worker invalidation");
                workerFrames = screen.RenderCount - start;
                peer.Pointer(6, 45, 85);
                peer.Pointer(4, 45, 85, 1);
                Thread.Sleep(80);
                peer.Pointer(5, 45, 85, 1);
                Wait(() => screen.Clicks == 1, "native button click");
                nativePointerClicked = true;
                peer.Pointer(6, 130, 140);
                peer.Pointer(4, 130, 140, 1);
                Thread.Sleep(80);
                peer.Pointer(5, 130, 140, 1);
                Thread.Sleep(80);
                peer.Key(true, 0x78);
                peer.Key(false, 0x78);
                Wait(() => screen.Value.Contains('x'), "native committed text in the shared input widget");
                nativeTextEntered = true;
                void Shortcut(nuint key)
                {
                    var frame = screen.RenderCount;
                    peer.Key(true, 0xffe3);
                    peer.Key(true, key, 4);
                    Wait(() => screen.RenderCount > frame, "native Ctrl shortcut frame");
                    Thread.Sleep(60);
                    peer.Key(false, key, 4);
                    peer.Key(false, 0xffe3, 4);
                    Thread.Sleep(60);
                }
                Shortcut(0x61);
                Shortcut(0x63);
                Wait(() => clipboardPeer.GetText() == "x", "real Ctrl+C publishes selected input");
                copied = true;
                clipboardPeer.SetText("붙여넣기 😀");
                Shortcut(0x76);
                Wait(() => screen.Value == "붙여넣기 😀", "real Ctrl+V replaces the selected input");
                pasted = true;
                Wait(() => screen.InputTextInfo is
                {
                    Content.Text: "붙여넣기 😀", Content.CharacterCount: 6,
                    CaretUtf16: 7, AnchorUtf16: 7, CaretCharacterOffset: 6, AnchorCharacterOffset: 6
                },
                    "actual Korean and emoji text state uses Unicode scalar offsets");
                unicodeTextState = true;
                var imagesBeforeRotation = screen.ImageStatistics;
                peer.Pointer(6, 480, 155);
                peer.Pointer(4, 480, 155, 1);
                Thread.Sleep(80);
                peer.Pointer(5, 480, 155, 1);
                Wait(() => screen.SvgRotation == 15, "native SVG transform button");
                NativeProbe("atspi-client", registered.ToString(), nativeWindow: (nuint)handle, privateSelection: selection, observe: paintProbe.Observe);
                var imagesAfterRotation = screen.ImageStatistics;
                transformedSvgReused = imagesBeforeRotation is
                    { SourceReads: 1, XmlParses: 1, SceneCompiles: 1, Rasterizations: 1, GpuSceneCompiles: 1 }
                    && imagesAfterRotation.SourceReads == imagesBeforeRotation.SourceReads
                    && imagesAfterRotation.XmlParses == imagesBeforeRotation.XmlParses
                    && imagesAfterRotation.SceneCompiles == imagesBeforeRotation.SceneCompiles
                    && imagesAfterRotation.Rasterizations == imagesBeforeRotation.Rasterizations
                    && imagesAfterRotation.GpuSceneCompiles == imagesBeforeRotation.GpuSceneCompiles;
                Console.WriteLine($"SVG rotation statistics, registered={registered}, before={imagesBeforeRotation}, after={imagesAfterRotation}");
                Wait(() => screen.Clicks == 2, "external native accessibility invocation");
                Wait(peer.HasNativeFocus, "real X11 keyboard focus after the AT-SPI component request");
                nativeAccessibilityWindowFocus = true;
                nativeAccessibility = true;
                if (registered)
                {
                    peer.Close();
                    nativeCloseSent = true;
                }
                else
                    session.Exit();
            }
            catch (Exception error)
            {
                driverFailure = ExceptionDispatchInfo.Capture(error);
                session.Exit();
            }
        });
    }, paintProbe);
    options = registered ? options with
    {
        ClipboardFactory = _ => clipboard
    }
        : options with
        {
            Clipboard = clipboard
        };
    options = options with
    {
        Debug = options.Debug with
        {
            LogStartupTimings = true,
            Log = message =>
            {
                Console.WriteLine(message);
                if (message.StartsWith("StartupTiming[FirstFramePresented]=", StringComparison.Ordinal))
                    Volatile.Write(ref firstPresented, 1);
            }
        }
    };
    var watch = Stopwatch.StartNew();
    try
    {
        if (registered)
            DuxelApp.Run(options);
        else
            DuxelLinuxApp.Run(options, session);
    }
    catch (Exception error)
    {
        Console.Error.WriteLine("APP_OWNER_FAILURE: " + error);
        throw;
    }
    finally
    {
        session.Exit();
        driver?.GetAwaiter().GetResult();
    }
    driverFailure?.Throw();
    if (nativeClientOnly)
    {
        Check(driver is { IsCompletedSuccessfully: true }, "The independent ordinary native client and owned driver exit successfully");
        if (attributesOnly && !registered)
            continue;
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxChecksResult(RuntimeInformation.FrameworkDescription, attributesOnly ? "text-attributes-native" : "native-client-only", checks.Count, checks), LinuxProbeResultsJson.Default.LinuxChecksResult));
        return;
    }
    NativeProbe("atspi-absent", registered.ToString());
    Check(true, "The owned application is removed from the native registry after its session closes, registered=" + registered);
    Check(driver is not null && driver.IsCompletedSuccessfully, "The owned native driver terminates, registered=" + registered);
    Check(screen.RenderCount > 0 && idleFrames == 0, "The shared app loop stops producing unchanged frames, registered=" + registered);
    Check(capturedColors > 10, "The actual product window has nonblank rendered content, registered=" + registered);
    Check(imagePixelsMatch, "Actual window pixels match opaque PNG, SVG bitmap and SVG widget colors, registered=" + registered);
    Check(transformedSvgReused, "A native SVG rotation reuses the parsed GPU scene and creates no new bitmap raster, registered=" + registered);
    Check(workerFrames > 0 && screen.ExternalValue == 42, "A worker RequestFrame resumes the sleeping UI, registered=" + registered);
    Check(nativePointerClicked, "Real X11 pointer events activate the shared button exactly once, registered=" + registered);
    Check(nativeAccessibility && screen.Clicks == 2, "An independent native AT-SPI client navigates, invokes and focuses the real UI, registered=" + registered);
    Check(nativeAccessibilityWindowFocus, "An AT-SPI focus request also acquires actual X11 keyboard focus, registered=" + registered);
    Check(nativeTextEntered, "Real XIM committed input edits the shared text widget, registered=" + registered);
    Check(copied && pasted, "Native Ctrl+C/Ctrl+V round-trip Unicode through the shared input and private selection, registered=" + registered);
    Check(unicodeTextState, "The real Linux editor publishes Korean/emoji text with coherent scalar and UTF16 caret offsets, registered=" + registered);
    clipboard.SetText("caller still owns this service");
    Check(clipboardPeer.GetText() == "caller still owns this service", "The runner preserves caller-supplied clipboard lifetime, registered=" + registered);
    Check(registered ? nativeCloseSent : !nativeCloseSent, "The requested native-close/session-exit path completes, registered=" + registered);
    observations.Add(new(registered, screen.RenderCount, idleFrames, workerFrames, idleCpuMilliseconds, screen.Clicks, screen.Value, nativeCloseSent, capturedColors, imagePixelsMatch, transformedSvgReused, nativeAccessibility, nativeAccessibilityWindowFocus, screen.ImageStatistics, copied, pasted, unicodeTextState, false, false, watch.ElapsedMilliseconds));
}
using (var disabled = new LinuxX11PlatformBackend(new(64, 64, "Owned accessibility-disabled fixture", Visible: false)
{
    Accessibility = new()
    {
        Enabled = false
    }
}))
{
    Check(disabled.AccessibilityBusName is null, "Explicitly disabled AT-SPI does not create a native bus provider");
    NativeProbe("atspi-absent", "disabled");
}
NativeProbe("transport-failure", "unreachable", unreachableBus: true);
Check(true, "Unreachable-bus startup fails explicitly, joins its threads and retains no per-attempt descriptors");
NativeProbe("pango-failure", "dependency");
Check(true, "An unavailable Pango import fails before native bus creation and retains no owner threads or per-attempt descriptors");
RunRangeSelectionPolicy();
void RunRangeSelectionPolicy()
{
    using (var policy = new LinuxX11PlatformBackend(new(64, 64, "Owned numeric policy fixture", Visible: false)
    {
        Accessibility = new()
    }))
    {
        var tree = policy.AccessibilityTree;
        PublishRangeSelectionFixture(tree, required: false);
        var phase = 0;
        NativeProbe("atspi-range-policy", "contracts", observe : result =>
        {
            if (phase == 0 && File.Exists(result + ".required-on"))
            {
                PublishRangeSelectionFixture(tree, required: true);
                phase = 1;
            }
            if (phase == 1 && File.Exists(result + ".required-off"))
            {
                PublishRangeSelectionFixture(tree, required: false);
                phase = 2;
            }
        });
        Check(phase == 2, "The host published both required-state transitions");
        Check(true, "Native numeric policy rejects readonly and disabled writes and omits unsupported Value interfaces");
    }
    NativeProbe("atspi-absent", "range-policy");
}
static void PublishRangeSelectionFixture(UiAccessibilityTree tree, bool required)
{
    NumericPublicationFixture.BeginFrame(tree);
    NumericPublicationFixture.Add(tree, new("readonly", "", UiAccessibilityRole.ProgressBar, "readonly numeric fixture", new(0, 0, 60, 10), true, false, false,
        Value: "절반", ReadOnly: true, Focusable: false, ValuePattern: false)
    {
        Range = new(50, 0, 100, 0, 0)
    });
    NumericPublicationFixture.Add(tree, new("disabled", "", UiAccessibilityRole.Slider, "disabled numeric fixture", new(0, 20, 60, 10), false, false, false,
        Focusable: false, ValuePattern: false)
    {
        Range = new(25, 0, 100, 1, 10)
    });
    NumericPublicationFixture.Add(tree, new("plain", "", UiAccessibilityRole.Label, "nonnumeric fixture", new(0, 40, 60, 10), true, false, false,
        Focusable: false, ValuePattern: false));
    NumericPublicationFixture.Add(tree, new("mandatory", "", UiAccessibilityRole.RadioGroup, "mandatory selection fixture", new(0, 0, 60, 10), true, false, false,
        Focusable: false, ValuePattern: false)
    {
        SelectionRequired = true,
        Required = required
    });
    NumericPublicationFixture.Add(tree, new("mandatory-first", "mandatory", UiAccessibilityRole.RadioButton, "mandatory choice", new(0, 0, 30, 10), true, false, false,
        ValuePattern: false)
    {
        Selected = true
    });
    NumericPublicationFixture.Add(tree, new("mandatory-second", "mandatory", UiAccessibilityRole.RadioButton, "other choice", new(30, 0, 30, 10), true, false, false,
        ValuePattern: false)
    {
        Selected = false
    });
    NumericPublicationFixture.Add(tree, new("disabled-selection", "", UiAccessibilityRole.ListBox, "disabled selection fixture", new(0, 0, 60, 10), false, false, false,
        Focusable: false, ValuePattern: false)
    {
        CanSelectMultiple = true
    });
    NumericPublicationFixture.Add(tree, new("disabled-option", "disabled-selection", UiAccessibilityRole.Option, "disabled choice", new(0, 0, 60, 10), false, false, false,
        ValuePattern: false)
    {
        Selected = true
    });
    NumericPublicationFixture.Add(tree, new("bulk", "", UiAccessibilityRole.ListBox, "bulk preflight fixture", new(0, 0, 60, 10), true, false, false,
        Focusable: false, ValuePattern: false)
    {
        CanSelectMultiple = true
    });
    NumericPublicationFixture.Add(tree, new("bulk-first", "bulk", UiAccessibilityRole.Option, "eligible choice", new(0, 0, 30, 10), true, false, false,
        ValuePattern: false)
    {
        Selected = false
    });
    NumericPublicationFixture.Add(tree, new("bulk-second", "bulk", UiAccessibilityRole.Option, "ineligible choice", new(30, 0, 30, 10), false, false, false,
        ValuePattern: false)
    {
        Selected = false
    });
    NumericPublicationFixture.Add(tree, new("mandatory-multi", "", UiAccessibilityRole.ListBox, "mandatory multiple fixture", new(0, 0, 60, 10), true, false, false,
        Focusable: false, ValuePattern: false)
    {
        CanSelectMultiple = true,
        SelectionRequired = true
    });
    NumericPublicationFixture.Add(tree, new("mandatory-multi-a", "mandatory-multi", UiAccessibilityRole.Option, "required option A", new(0, 0, 30, 10), true, false, false,
        ValuePattern: false)
    {
        Selected = true
    });
    NumericPublicationFixture.Add(tree, new("mandatory-multi-b", "mandatory-multi", UiAccessibilityRole.Option, "required option B", new(30, 0, 30, 10), true, false, false,
        ValuePattern: false)
    {
        Selected = true
    });
    NumericPublicationFixture.EndFrame(tree, null, false);
}
void RunOwnedTextProbe(bool registered, UiScreen surface, ManualResetEventSlim firstFrame, string mode, string name)
{
    var session = registered ? DuxelApp.PrimarySession : new DuxelAppSession();
    Task? driver = null;
    ExceptionDispatchInfo? failure = null;
    var options = Options(validationScreen, handle => driver = Task.Run(() =>
    {
        try
        {
            if (!firstFrame.Wait(8000))
                throw new TimeoutException("The owned text surface did not render its first frame: " + name);
            NativeProbe(mode, registered.ToString(), nativeWindow: (nuint)handle);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { session.Exit(); }
    }), surface);
    try
    {
        if (registered)
            DuxelApp.Run(options);
        else
            DuxelLinuxApp.Run(options, session);
    }
    finally
    {
        session.Exit();
        driver?.GetAwaiter().GetResult();
    }
    failure?.Throw();
    Check(driver is { IsCompletedSuccessfully: true }, "Independent native " + name + " fixture completes, registered=" + registered);
    NativeProbe("atspi-absent", name + "-" + registered);
    Check(true, "Owned " + name + " window leaves no native application registration, registered=" + registered);
}
foreach (var registered in new[] { false, true })
{
    using var markdown = new MarkdownProbeScreen();
    RunOwnedTextProbe(registered, markdown, markdown.FirstFrame, "atspi-markdown", "Markdown");
    using var ancestor = new AncestorProbeScreen();
    RunOwnedTextProbe(registered, ancestor, ancestor.FirstFrame, "atspi-ancestor", "ancestor");
    using var rtl = new AncestorProbeScreen(rightToLeft: true);
    RunOwnedTextProbe(registered, rtl, rtl.FirstFrame, "atspi-ancestor-rtl", "ancestor-rtl");
    using var virtualText = new VirtualTextProbeScreen();
    RunOwnedTextProbe(registered, virtualText, virtualText.FirstFrame, "atspi-virtual-text", "virtual-text");
    using var variableText = new VirtualTextProbeScreen(variableExtents: true);
    RunOwnedTextProbe(registered, variableText, variableText.FirstFrame, "atspi-variable-text", "variable-text");
    foreach (var mode in new[] { "single", "multiline", "markdown" })
    {
        using var popup = new PopoverTextScrollTests.NativeScreen(mode);
        RunOwnedTextProbe(registered, popup, popup.FirstFrame, "atspi-popup-" + mode, "popup-" + mode);
    }
    using var images = new MarkdownImageAccessibilityTests.NativeScreen();
    RunOwnedTextProbe(registered, images, images.FirstFrame, "atspi-markdown-images", "markdown-images");
}
var validationHooks = typeof(Duxel.Vulkan.VulkanRendererBackend).GetMethod("RenderAndReadback",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is not null;
Check(!validationHooks, "The real app uses the product renderer without validation hooks");
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxApplicationResult(RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(), checks.Count, checks, observations, validationHooks), LinuxProbeResultsJson.Default.LinuxApplicationResult));
