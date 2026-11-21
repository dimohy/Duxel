using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duxel.App;
using Duxel.Core;
using Duxel.Gallery;
using Duxel.Platform.Windows;

internal static class CodeViewPerformanceTests
{
    internal static void MeasureSelection(string output, bool arc, bool profileInput)
    {
        Directory.CreateDirectory(output);
        using var tree = new UiAccessibilityTree();
        using var fixture = new PresentationReuseTests.Fixture(3, true, PresentationTestPlatform.Create(1280, 900), msaaSamples: 4);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11);
        fixture.Context.SetAccessibilityTree(tree);
        fixture.Context.InputProfilingEnabled = profileInput;
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows())
            { FreezeDiagnosticText = true, SelectedPage = arc ? "SVG" : "Markdown", ProfileRenderSections = profileInput };
        if (arc)
            gallery.SvgPage.ConfigureArcPreview();

        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        Submit(idle);
        // SVG owns its document viewport, while Markdown uses the gallery page viewport.
        var paneName = arc ? "observer-svg-page" : GalleryText.Get("Gallery page");
        var codeName = GalleryText.CodeViewLabels().AccessibleName;
        var pane = tree.CaptureSnapshot().Elements.Single(element =>
            element.Node.Name == paneName && element.Node.Role == UiAccessibilityRole.Pane);
        var top = pane.Node.Bounds.Y;
        var bottom = top + pane.Node.Bounds.Height;
        if (arc)
        {
            var wheel = idle with { MousePosition = new(800, bottom - 60), MouseWheel = -1 };
            for (var frame = 0; frame < 24 && VisibleCode()?.ClipBounds.Height is not >= 420; frame++)
                Submit(wheel);
        }
        else
        {
            if (tree.RequestScroll(pane.Node.Id, pane.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 100), wait: false)
                != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Could not reveal the Markdown source code.");
            Submit(idle);
        }

        for (var frame = 0; frame < 96; frame++)
            Submit(idle);
        var visible = VisibleCode() ?? throw new InvalidOperationException("No code view is visible.");
        var bounds = visible.ClipBounds;
        if (bounds.Height < 350)
            throw new InvalidOperationException($"Only {bounds.Height} pixels of source code are visible.");
        var anchor = new UiVector2(bounds.X + 40, bounds.Y + 18);
        var near = new UiVector2(anchor.X + 30, anchor.Y + 64);
        var far = new UiVector2(bounds.X + 180, bounds.Y + bounds.Height - 36);
        var drag = idle with { MousePosition = anchor, LeftMouseDown = true };
        Submit(drag with { LeftMousePressed = true });
        using var process = Process.GetCurrentProcess();
        using var samples = new StreamWriter(Path.Combine(output, "selection.tsv"));
        samples.WriteLine("mode\tframe\trequested_steps\tprocessed_steps\tselection_length\tui_ms\trenderer_ms\tallocated\tprivate_bytes\tworking_set\trepair_width\trepair_height\tframe_id\tpresentation_sequence\treused");
        using var stages = profileInput ? new StreamWriter(Path.Combine(output, "selection-stages.tsv")) : null;
        stages?.WriteLine("frame_id\tpreparation_ms\tcomposition_ms\tfinalization_ms\tinput_navigation_ms\tinput_options_ms\tinput_page_ms\tpresent_navigation_ms\tpresent_options_ms\tpresent_page_ms");
        (long Navigation, long Options, long Page) inputSections = default, presentSections = default;
        Action<UiImmediateContext> render = profileInput ? DrawProfiled : gallery.Render;
        foreach (var mode in new[] { 1, 16 })
        {
            var inputs = new UiInputState[mode];
            for (var frame = -24; frame < 160; frame++)
            {
                for (var step = 0; step < mode; step++)
                    inputs[step] = drag with
                    {
                        MousePosition = frame % 2 == 0
                            ? (step % 2 == 0 && step != mode - 1 ? near : far)
                            : (step % 2 == 0 && step != mode - 1 ? far : near)
                    };
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                inputSections = presentSections = default;
                using var draw = fixture.Build(render, inputs[0], orderedInputs: inputs);
                var composed = Stopwatch.GetTimestamp();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (!fixture.RenderSubmit(draw))
                    throw new InvalidOperationException("The renderer rejected the code selection frame.");
                var submitted = Stopwatch.GetTimestamp();
                var selection = tree.CaptureSnapshot().Elements.Single(element =>
                    element.Node.TextLayout?.Content == visible.Content).Node.TextInfo
                    ?? throw new InvalidOperationException("Code view did not publish selection information.");
                var length = Math.Abs(selection.CaretUtf16 - selection.AnchorUtf16);
                if ((frame % 2 == 0 && length < 300) || (frame % 2 != 0 && length >= 300))
                    throw new InvalidOperationException($"Unexpected code selection length {length} at {mode}/{frame}.");
                if (frame < 0)
                    continue;
                if (frame % 16 == 0)
                    process.Refresh();
                var steps = fixture.Context.InputProcessingMetrics;
                var metrics = fixture.Renderer.LastFrameMetrics;
                var repair = metrics.RepaintBounds;
                samples.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{mode}\t{frame}\t{steps.RequestedSteps}\t{steps.CompletedSteps}\t{length}\t{Stopwatch.GetElapsedTime(start, composed).TotalMilliseconds:F4}\t{Stopwatch.GetElapsedTime(composed, submitted).TotalMilliseconds:F4}\t{allocated}\t{process.PrivateMemorySize64}\t{process.WorkingSet64}\t{repair.Width}\t{repair.Height}\t{draw.DrawDataFrameId}\t{metrics.PresentationSequence}\t{metrics.PresentationReused}"));
                if (stages is not null)
                {
                    var scale = 1000d / Stopwatch.Frequency;
                    stages.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{draw.DrawDataFrameId}\t{steps.PreparationMilliseconds:F4}\t{steps.CompositionMilliseconds:F4}\t{steps.FinalizationMilliseconds:F4}\t{inputSections.Navigation * scale:F4}\t{inputSections.Options * scale:F4}\t{inputSections.Page * scale:F4}\t{presentSections.Navigation * scale:F4}\t{presentSections.Options * scale:F4}\t{presentSections.Page * scale:F4}"));
                }
            }
        }
        var outside = drag with { MousePosition = new(anchor.X, -12) };
        var crossing = new[] { drag, outside, outside with { MousePosition = new(anchor.X + 20, -16) },
            drag with { MousePosition = near } };
        using (var draw = fixture.Build(gallery.Render, drag, orderedInputs: crossing))
        {
            if (fixture.Context.InputProcessingMetrics.CompletedSteps != crossing.Length || !fixture.RenderSubmit(draw))
                throw new InvalidOperationException("Code selection lost a pointer step crossing the window bounds.");
        }
        fixture.WriteCapturePng(Path.Combine(output, "selected.png"), fixture.Capture(gallery.Render,
            drag with { MousePosition = near, LeftMouseDown = false, LeftMouseReleased = true }));

        void DrawProfiled(UiImmediateContext ui)
        {
            gallery.Render(ui);
            var sections = gallery.RenderSectionTicks;
            if (ui.IsPresentationPass)
                presentSections = sections;
            else
                inputSections = (inputSections.Navigation + sections.Navigation, inputSections.Options + sections.Options,
                    inputSections.Page + sections.Page);
        }

        UiAccessibilityTextLayout? VisibleCode() => tree.CaptureSnapshot().Elements
            .Where(element => element.Node.Name == codeName && element.Node is { Role: UiAccessibilityRole.Edit, ReadOnly: true })
            .Select(element => element.Node.TextLayout).OfType<UiAccessibilityTextLayout>()
            .MaxBy(layout => layout.ClipBounds.Height);

        void Submit(UiInputState input)
        {
            using var draw = fixture.Build(gallery.Render, input);
            if (!fixture.RenderSubmit(draw))
                throw new InvalidOperationException("The renderer rejected the code selection setup frame.");
        }
    }
    internal static void Run(string output, bool fullSource = false)
    {
        Directory.CreateDirectory(output);
        if (File.Exists(Path.Combine(output, "result.json"))) throw new InvalidOperationException("Use a fresh code benchmark output.");
        var width = fullSource ? 1654 : 1280;
        var height = fullSource ? 1040 : 900;
        var page = fullSource ? "Markdown" : "경로";
        var session = new DuxelAppSession();
        using var observer = new ObserverScreen(ObserverServices.CreateWindows()) { SelectedPage = page, FreezeDiagnosticText = true };
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = width, Height = height, Title = "Duxel owned source layout measurement", VSync = false,
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new(), FrameInvalidated = session.RequestFrame
        });
        var screen = new Screen(observer, session, platform.AccessibilityTree, fullSource);
        session.RunCore(new()
        {
            Screen = screen, Design = UiCompiledDesign.Windows11Dark,
            Window = new() { Width = width, Height = height, VSync = false },
            Font = new() { FastStartup = false },
            Frame = new() { EnableIdleFrameSkip = true, UseEventDrivenPlatformLoop = true,
                IsAnimationActiveProvider = () => !observer.UpdateOnlyOnChange }
        }, platform);
        if (screen.Samples.Count != 256 || observer.UpdateOnlyOnChange || screen.Presentations == 0)
            throw new InvalidOperationException("Native code measurement did not complete its continuous-rendering sample.");
        var sorted = screen.Samples.Select(sample => sample.Milliseconds).Order().ToArray();
        File.WriteAllLines(Path.Combine(output, "frames.csv"), new[] { "ui_ms,allocated_bytes" }.Concat(screen.Samples.Select(sample =>
            sample.Milliseconds.ToString("R", CultureInfo.InvariantCulture) + "," + sample.Allocated.ToString(CultureInfo.InvariantCulture))));
        using var stream = File.Create(Path.Combine(output, "result.json"));
        using var json = new Utf8JsonWriter(stream, new() { Indented = true });
        json.WriteStartObject();
        json.WriteString("Scope", "Windows Gallery steady UI composition");
        json.WriteString("Page", page);
        json.WriteString("Framework", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        json.WriteString("SourceSha256", screen.InitialViewport.SourceSha256);
        json.WriteBoolean("FullSourceViewport", fullSource);
        json.WriteNumber("SourceViewportCoverage", screen.InitialViewport.Coverage);
        json.WriteNumber("VisibleSourceLines", screen.InitialViewport.VisibleLines);
        json.WriteNumber("SourceLineCount", screen.InitialViewport.SourceLines);
        json.WriteNumber("PageScrollPercent", screen.InitialViewport.ScrollPercent);
        json.WriteNumber("Width", width);
        json.WriteNumber("Height", height);
        json.WriteNumber("Samples", sorted.Length);
        json.WriteNumber("MeasuredWallMs", screen.MeasuredWallMs);
        json.WriteNumber("MeasuredPresentations", screen.Presentations - screen.FirstPresentation);
        json.WriteString("ObservedUiRate", observer.PerformanceText);
        json.WriteNumber("MeanUiMs", sorted.Average());
        json.WriteNumber("MeanCompleteUiMs", screen.Samples.Average(sample => sample.CompleteUiMs));
        json.WriteNumber("MeanSubmitMs", screen.Samples.Average(sample => sample.SubmitMs));
        json.WriteNumber("MeanNewFrameMs", screen.Samples.Average(sample => sample.NewFrameMs));
        json.WriteNumber("MedianUiMs", sorted[sorted.Length / 2]);
        json.WriteNumber("P95UiMs", sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]);
        json.WriteNumber("P99UiMs", sorted[(int)Math.Ceiling(sorted.Length * .99) - 1]);
        json.WriteNumber("MeanAllocatedBytes", screen.Samples.Average(sample => sample.Allocated));
        json.WriteNumber("MeasurementBuilds", screen.After.Measure - screen.Before.Measure);
        json.WriteNumber("PaintBuilds", screen.After.Paint - screen.Before.Paint);
        json.WriteNumber("SyntaxParses", screen.After.Parse - screen.Before.Parse);
        json.WriteNumber("ParagraphRasters", screen.RasterAfter.Rasters - screen.RasterBefore.Rasters);
        json.WriteNumber("EmptyParagraphRasters", screen.RasterAfter.EmptyRasters - screen.RasterBefore.EmptyRasters);
        json.WriteNumber("ParagraphEvictions", screen.RasterAfter.Evictions - screen.RasterBefore.Evictions);
        json.WriteNumber("EmptyParagraphEvictions", screen.RasterAfter.EmptyEvictions - screen.RasterBefore.EmptyEvictions);
        json.WriteNumber("Presentations", screen.Presentations);
        json.WriteNumber("ContentScale", platform.ContentScale);
        json.WriteNumber("TextScale", platform.TextScale);
        json.WriteNumber("FramebufferWidth", platform.FramebufferSize.Width);
        json.WriteNumber("FramebufferHeight", platform.FramebufferSize.Height);
        json.WriteEndObject();
    }

    private readonly record struct SourceViewport(string SourceSha256, float Coverage, int VisibleLines, int SourceLines,
        double ScrollPercent);

    private sealed class Screen(ObserverScreen observer, DuxelAppSession session, UiAccessibilityTree tree, bool fullSource) : UiScreen
    {
        internal readonly List<(double Milliseconds, long Allocated, float CompleteUiMs, float SubmitMs, float NewFrameMs)> Samples = [];
        internal (int Measure, int Paint, int Parse) Before, After;
        internal (int Rasters, int EmptyRasters, int Evictions, int EmptyEvictions) RasterBefore, RasterAfter;
        internal long Presentations, FirstPresentation;
        internal double MeasuredWallMs;
        internal SourceViewport InitialViewport;
        private long _measurementStarted;
        private bool _scrollQueued;
        private bool _sourceReady;
        private double? _started;
        private bool _toggleQueued;
        public override void Render(UiImmediateContext ui)
        {
            _started ??= ui.GetTime();
            var diagnosticsBefore = observer.CodeDiagnostics;
            var elapsedSinceStart = ui.GetTime() - _started.Value;
            var sampleFrame = elapsedSinceStart >= .8 && _sourceReady;
            if (sampleFrame && Samples.Count == 0)
            {
                _measurementStarted = Stopwatch.GetTimestamp();
                FirstPresentation = (long)ui.GetRendererFrameMetrics().PresentationSequence;
                RasterBefore = ui.ValidationParagraphRasterCounts;
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            observer.Render(ui);
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Presentations = (long)ui.GetRendererFrameMetrics().PresentationSequence;
            if (!_toggleQueued && Presentations > 0)
            {
                var toggle = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "이벤트 대기 모드" && element.Node.Role == UiAccessibilityRole.Switch);
                if (tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Toggle, wait : false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Continuous-rendering switch rejected the code benchmark action.");
                _toggleQueued = true;
            }
            if (fullSource && _toggleQueued && !_scrollQueued && Presentations > 1)
            {
                var pane = GetGalleryPane(tree.CaptureSnapshot().Elements);
                if (pane.Node.Scroll is not { VerticallyScrollable : true })
                    throw new InvalidOperationException("The full-source Gallery example must scroll vertically.");
                var request = tree.RequestScroll(pane.Node.Id, pane.RuntimeId,
                    UiAccessibilityScrollCommand.ByPercent(-1, 100), wait : false);
                if (request != UiAccessibilityResult.Success)
                    throw new InvalidOperationException($"Full-source scroll request failed : {request}.");
                _scrollQueued = true;
            }
            if (!_sourceReady && _toggleQueued && !observer.UpdateOnlyOnChange && (!fullSource || _scrollQueued))
                _sourceReady = TryReadSourceViewport(out InitialViewport);
            if (!_sourceReady && elapsedSinceStart > 8)
                throw new InvalidOperationException("The required Gallery source viewport never became visible.");
            if (sampleFrame)
            {
                if (Samples.Count == 0) Before = diagnosticsBefore;
                Samples.Add((elapsed, allocated, ui.GetRenderTimeMs(), ui.GetSubmitTimeMs(), ui.GetNewFrameTimeMs()));
                After = observer.CodeDiagnostics;
                if (Samples.Count == 256)
                {
                    RasterAfter = ui.ValidationParagraphRasterCounts;
                    if (fullSource && (RasterBefore.EmptyRasters == 0 || RasterAfter.EmptyRasters != RasterBefore.EmptyRasters))
                        throw new InvalidOperationException("The stable code viewport did not reuse its warmed empty text rasters.");
                    MeasuredWallMs = Stopwatch.GetElapsedTime(_measurementStarted).TotalMilliseconds;
                    if (!TryReadSourceViewport(out var finalViewport) || finalViewport != InitialViewport)
                        throw new InvalidOperationException("The source viewport changed during the measurement.");
                    session.Exit();
                    return;
                }
            }
            ui.RequestFrameAt(ui.GetTime());
        }

        private static UiAccessibilityElement GetGalleryPane(IReadOnlyList<UiAccessibilityElement> nodes) =>
            nodes.Single(element => element.Node.Name == "갤러리 페이지" && element.Node.Role == UiAccessibilityRole.Pane);

        private bool TryReadSourceViewport(out SourceViewport viewport)
        {
            viewport = default;
            var nodes = tree.CaptureSnapshot().Elements;
            var pane = GetGalleryPane(nodes).Node;
            var scroll = pane.Scroll?.VerticalScrollPercent ?? -1;
            var source = nodes.SingleOrDefault(element => element.Node is
                { Name : "C# 예제 소스", Role : UiAccessibilityRole.Edit, ReadOnly : true });
            if (source.Node.TextLayout is not { } layout || layout.ClipBounds.Height <= 0 || pane.Bounds.Height <= 0)
                return false;
            var coverage = layout.ClipBounds.Height / pane.Bounds.Height;
            if (fullSource && (scroll < 99.9 || coverage < .7f))
                return false;
            var visibleLines = layout.GetBoundedRanges(layout.ClipBounds)
                .Select(range => layout.GetLineRangeAt(range.Start).Start).Distinct().Count();
            if (visibleLines < (fullSource ? 24 : 1))
                return false;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(layout.Content.Text)));
            viewport = new(hash, coverage, visibleLines, layout.LineCount, scroll);
            return true;
        }
    }
}
