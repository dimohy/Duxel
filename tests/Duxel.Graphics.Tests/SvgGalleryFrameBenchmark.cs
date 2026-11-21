using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Gallery;

internal static class SvgGalleryFrameBenchmark
{
    internal static void MeasureLayerMix(string output)
    {
        Directory.CreateDirectory(output);
        var platform = PresentationTestPlatform.Create(1280, 900, orderedInput: true);
        using var tree = new UiAccessibilityTree();
        using var fixture = new PresentationReuseTests.Fixture(3, true, platform, msaaSamples: 4);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11);
        fixture.Context.SetAccessibilityTree(tree);
        fixture.Context.InputProfilingEnabled = true;
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows()) { SelectedPage = "SVG" };
        using var log = new StreamWriter(Path.Combine(output, "mixed.tsv")) { AutoFlush = true };
        log.WriteLine("phase\tinput\tpasses\tui_ms\trenderer_ms\tallocated\toffset\tselection\tvector\tfps_label\tactive_code");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var initialLabel = gallery.PerformanceText;
        // Simulate the host initializing for longer than one FPS sample window.
        Thread.Sleep(550);
        Draw("open", [idle]);
        if (gallery.PerformanceText != initialLabel)
            throw new InvalidOperationException("The first Gallery frame measured time before its render loop started.");
        var tab = tree.CaptureSnapshot().Elements.Single(e => e.Node is { Name : "레이어·테두리", Role : UiAccessibilityRole.Tab });
        if (tree.RequestAction(tab.Node.Id, tab.RuntimeId, UiAccessibilityAction.Select, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Could not select the Layer and Borders tab.");
        Draw("open-layer", [idle]);
        for (var i = 0; i < 16 && Code()?.TextLayout?.ClipBounds.Height is not >= 380; i++)
            Draw("reveal", [idle with { MousePosition = new(800, 680), MouseWheel = -1 }]);

        CheckIdleReading(excludeIdle : false);
        CheckIdleReading(excludeIdle : true);

        for (var cycle = 0; cycle < 12; cycle++)
        {
            var bounds = Code()?.TextLayout?.ClipBounds ?? throw new InvalidOperationException("No layer source is in view.");
            var center = new UiVector2(bounds.X + 96, bounds.Y + bounds.Height / 2);
            Post(0x0200, center, 0);
            platform.PollEvents();
            Wheel("scroll-down", -5);
            Wheel("scroll-up", 5);
            Draw("settle-position", [idle]);
            if (cycle % 3 == 0)
                DragThumb();
            var view = Code() ?? throw new InvalidOperationException("No code after scroll.");
            bounds = view.TextLayout!.ClipBounds;
            var origin = new UiVector2(bounds.X + 90 + cycle * 12, bounds.Y + 50);
            Post(0x0200, origin, 0);
            Post(0x0201, origin, 1);
            Native("press");
            if (!fixture.Context.State.OwnsTextDrag(view.Id))
                throw new InvalidOperationException($"Selection press at {origin} did not capture {view.Id} in {bounds}; active={fixture.Context.State.ActiveId}.");
            for (var i = 0; i < 64; i++)
            {
                var targetY = cycle % 2 == 0 ? bounds.Y + bounds.Height - 30 : bounds.Y - 6;
                Post(0x0200, new(origin.X + i % 3, targetY + i % 7), 1);
            }
            Native("select");
            if (!tree.TryGet(view.Id, out var selected) || selected.Node.TextInfo is not { } info
                || info.CaretUtf16 == info.AnchorUtf16)
                throw new InvalidOperationException("The mixed replay did not select code.");
            Post(0x0202, origin, 0);
            Native("release");
        }
        fixture.WriteCapturePng(Path.Combine(output, "end.png"), fixture.Capture(gallery.Render, idle));

        void Wheel(string phase, short delta)
        {
            for (var i = 0; i < 64; i++)
                Send(0x020A, (nuint)((uint)(ushort)delta << 16), 0);
            Native(phase);
        }

        void DragThumb()
        {
            var pane = Pane();
            var scroll = pane.Scroll ?? throw new InvalidOperationException("The SVG document has no scrollbar.");
            var track = scroll.Viewport.Y + 4;
            var handle = MathF.Max(16, track * track / scroll.Content.Y);
            var thumb = new UiVector2(MathF.Round(pane.Bounds.X + pane.Bounds.Width - 5),
                MathF.Round(pane.Bounds.Y + (float)(scroll.Offset.Y / scroll.VerticalMaximum) * (track - handle) + handle / 2));
            Post(0x0200, thumb, 0);
            Post(0x0201, thumb, 1);
            Native("thumb-press");
            var travel = MathF.Min(track * .35f, (track - handle) * .5f);
            for (var i = 0; i < 64; i++)
                Post(0x0200, thumb with { Y = thumb.Y + travel * (i + 1) / 64 }, 1);
            Native("thumb-down");
            if (Pane().Scroll!.Value.Offset.Y - scroll.Offset.Y < scroll.VerticalMaximum * .20f)
                throw new InvalidOperationException("The mixed replay did not move the scrollbar through enough of the document.");
            for (var i = 0; i < 64; i++)
                Post(0x0200, thumb with { Y = thumb.Y + travel * (63 - i) / 64 }, 1);
            Native("thumb-return");
            Post(0x0202, thumb, 0);
            Native("thumb-release");
            if (MathF.Abs(Pane().Scroll!.Value.Offset.Y - scroll.Offset.Y) > .05f)
                throw new InvalidOperationException("Returning the thumb did not restore the same document position.");
        }

        void Native(string phase)
        {
            platform.PollEvents();
            Draw(phase, platform.Input.Snapshot.OrderedInputs!.ToArray());
        }

        void CheckIdleReading(bool excludeIdle)
        {
            var label = gallery.PerformanceText;
            var started = Stopwatch.GetTimestamp();
            while (gallery.PerformanceText == label)
            {
                if (Stopwatch.GetElapsedTime(started).TotalSeconds > 2)
                    throw new InvalidOperationException("The Gallery did not publish a fresh FPS sample.");
                Thread.Sleep(5);
                Draw("sample", [idle]);
            }
            label = gallery.PerformanceText;
            started = Stopwatch.GetTimestamp();
            Thread.Sleep(2500);
            var wait = Stopwatch.GetElapsedTime(started).TotalSeconds;
            Draw(excludeIdle ? "after-idle" : "including-idle", [idle], excludeIdle ? wait : 0);
            if ((gallery.PerformanceText == label) != excludeIdle)
                throw new InvalidOperationException("The Gallery FPS sample did not distinguish an idle wait.");
        }

        void Draw(string phase, UiInputState[] input, double idleWaitSeconds = 0)
        {
            if (input.Length == 0)
                throw new InvalidOperationException($"No native input for {phase}.");
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            using var draw = fixture.Build(gallery.Render, input[0], pollEvents : false, orderedInputs : input, idleWaitSeconds : idleWaitSeconds);
            var built = Stopwatch.GetTimestamp();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            if (!fixture.RenderSubmit(draw))
                throw new InvalidOperationException($"Vulkan did not accept {phase}.");
            var submitted = Stopwatch.GetTimestamp();
            var state = Pane();
            var code = Code();
            var text = code?.TextInfo;
            var activeCode = code is { } visible && fixture.Context.State.ActiveId == visible.Id;
            log.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{phase}\t{input.Length}\t{fixture.Context.InputProcessingMetrics.CompletedSteps}\t{Stopwatch.GetElapsedTime(start, built).TotalMilliseconds:F3}\t{Stopwatch.GetElapsedTime(built, submitted).TotalMilliseconds:F3}\t{allocated}\t{state.Scroll?.Offset.Y}\t{(text is null ? 0 : Math.Abs(text.CaretUtf16 - text.AnchorUtf16))}\t{draw.TextureUpdates.Count(u => u.Kind == UiTextureUpdateKind.RenderVectorScene)}\t{gallery.PerformanceText}\t{activeCode}"));
        }

        UiAccessibilityNode Pane() => tree.CaptureSnapshot().Elements.SingleOrDefault(e => e.Node is { Name : "observer-svg-page" }).Node;

        UiAccessibilityNode? Code()
        {
            var node = tree.CaptureSnapshot().Elements.Where(e => e.Node is { Name : "C# 예제 소스", TextLayout : not null })
                .MaxBy(e => e.Node.TextLayout!.ClipBounds.Height).Node;
            return node.TextLayout is null ? null : node;
        }

        void Post(uint message, UiVector2 point, nuint buttons)
            => Send(message, buttons, unchecked((nint)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16))));

        void Send(uint message, nuint wParam, nint lParam)
        {
            if (!PresentationTestPlatform.PostMessage(platform.WindowHandle, message, wParam, lParam))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    internal static void MeasureCodeScrollBursts(string output)
    {
        Directory.CreateDirectory(output);
        using var log = new StreamWriter(Path.Combine(output, "bursts.tsv"));
        log.WriteLine("tab\tframe\trequested\tcompleted\tms\tallocated\toffset\tvector_requests\trenderer_ms\tframe_id\ttext_rasters\tempty_text_rasters");
        using var thumbLog = new StreamWriter(Path.Combine(output, "thumb.tsv"));
        thumbLog.WriteLine("tab\tframe\trequested\tcompleted\tui_ms\trenderer_ms\tallocated\toffset\tcontent_lag\trepaint_width\trepaint_height\tvector_requests\tframe_id\ttext_rasters\tempty_text_rasters");
        foreach (var label in new[] { "테두리 범위", "호·곡선 비교" })
        {
            var platform = PresentationTestPlatform.Create(1280, 900, orderedInput: true);
            using var tree = new UiAccessibilityTree();
            using var fixture = new PresentationReuseTests.Fixture(3, true, platform);
            fixture.Context.SetDesign(UiCompiledDesign.Windows11);
            fixture.Context.SetAccessibilityTree(tree);
            fixture.Context.InputProfilingEnabled = true;
            using var gallery = new ObserverScreen(ObserverServices.CreateWindows())
                { FreezeDiagnosticText = true, SelectedPage = "SVG" };
            (int Rasters, int EmptyRasters, int Evictions, int EmptyEvictions) paragraphRasters = default;
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false,
                0, 0, [], [], default, default);
            Submit(idle);
            var tab = tree.CaptureSnapshot().Elements.Single(element =>
                element.Node.Name == label && element.Node.Role == UiAccessibilityRole.Tab);
            if (tree.RequestAction(tab.Node.Id, tab.RuntimeId, UiAccessibilityAction.Select, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Could not select the actual SVG comparison tab.");
            Submit(idle);
            Submit(idle);
            var verticalViewports = tree.CaptureSnapshot().Elements
                .Select(element => element.Node)
                .Where(node => (node.Name is "갤러리 페이지" or "observer-svg-page")
                    && node.Scroll is { VerticalMaximum: > 0 })
                .Select(node => node.Name)
                .ToArray();
            if (verticalViewports is not ["observer-svg-page"])
                throw new InvalidOperationException("Expected a single SVG document scrollbar; found: "
                    + string.Join(", ", verticalViewports));
            if (ScrollPane().Scroll is { HorizontalMaximum: > 0 })
                throw new InvalidOperationException("The SVG document scrollbar must not create horizontal overflow.");
            Console.WriteLine($"PASS {label}: single SVG document scrollbar");
            for (var frame = 0; frame < 32 && VisibleCode()?.ClipBounds.Height is not >= 500; frame++)
                Submit(idle with { MousePosition = new(800, 680), MouseWheel = -1 });
            for (var frame = 0; frame < 8; frame++)
                Submit(idle);
            var code = VisibleCode() ?? throw new InvalidOperationException("No SVG source is visible.");
            var clip = code.ClipBounds;
            if (clip.Height < 500)
                throw new InvalidOperationException("The test needs a viewport dominated by source code.");
            var x = (ushort)(clip.X + clip.Width / 2);
            var y = (ushort)(clip.Y + clip.Height / 2);
            Post(0x0200, 0, unchecked((nint)((uint)x | ((uint)y << 16))));
            platform.PollEvents();
            for (var frame = -4; frame < 16; frame++)
            {
                const int packetCount = 64;
                var delta = frame % 2 == 0 ? (short)-5 : (short)5;
                for (var i = 0; i < packetCount; i++)
                    Post(0x020A, (nuint)((uint)(ushort)delta << 16), 0);
                platform.PollEvents();
                var batch = platform.Input.Snapshot.OrderedInputs!.Where(input => input.MouseWheel != 0).ToArray();
                if (batch.Length != packetCount || batch.Any(input => input.MousePosition != new UiVector2(x, y)))
                    throw new InvalidOperationException($"The Win32 wheel burst did not reach the source block: {batch.Length}.");
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                var before = paragraphRasters;
                using var draw = fixture.Build(Draw, batch[0], pollEvents: false, orderedInputs: batch);
                var composed = Stopwatch.GetTimestamp();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (!fixture.RenderSubmit(draw))
                    throw new InvalidOperationException("Vulkan rejected the code scroll frame.");
                var submitted = Stopwatch.GetTimestamp();
                var metrics = fixture.Context.InputProcessingMetrics;
                var offset = ScrollPane().Scroll!.Value.Offset.Y;
                var vectorRequests = draw.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.RenderVectorScene);
                if (frame >= 0)
                    log.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{label}\t{frame}\t{metrics.RequestedSteps}\t{metrics.CompletedSteps}\t{Stopwatch.GetElapsedTime(start, composed).TotalMilliseconds:F4}\t{allocated}\t{offset:R}\t{vectorRequests}\t{Stopwatch.GetElapsedTime(composed, submitted).TotalMilliseconds:F4}\t{draw.DrawDataFrameId}\t{paragraphRasters.Rasters - before.Rasters}\t{paragraphRasters.EmptyRasters - before.EmptyRasters}"));
            }
            Submit(idle);
            fixture.WriteCapturePng(Path.Combine(output, label + ".png"), fixture.Capture(gallery.Render, idle));

            var scrollOffset = ScrollPane().Scroll!.Value.Offset.Y;
            var alternating = Enumerable.Range(0, 16).Select(i => idle with
                { MousePosition = new(x, y), MouseWheel = (i % 2 == 0 ? -5 : 5) / 120f }).ToArray();
            using (var draw = fixture.Build(gallery.Render, alternating[0], orderedInputs: alternating))
            {
                if (!fixture.RenderSubmit(draw) || fixture.Context.InputProcessingMetrics.CompletedSteps != alternating.Length
                    || MathF.Abs(ScrollPane().Scroll!.Value.Offset.Y - scrollOffset) > .05f)
                    throw new InvalidOperationException("Wheel direction changes must preserve order and the original offset.");
            }

            var pane = ScrollPane();
            var scroll = pane.Scroll!.Value;
            var trackExtent = scroll.Viewport.Y + 4;
            var thumbExtent = MathF.Max(16, trackExtent * trackExtent / scroll.Content.Y);
            var thumb = new UiVector2(pane.Bounds.X + pane.Bounds.Width - 5,
                pane.Bounds.Y + (float)(scroll.Offset.Y / scroll.VerticalMaximum) * (trackExtent - thumbExtent) + thumbExtent / 2);
            thumb = new UiVector2(MathF.Round(thumb.X), MathF.Round(thumb.Y));
            PostPointer(0x0200, new(x, y), 0);
            PostPointer(0x0201, new(x, y), 1);
            platform.PollEvents();
            Submit(platform.Input.Snapshot.OrderedInputs!.Last(input => input.LeftMousePressed));
            PostPointer(0x0202, new(x, y), 0);
            platform.PollEvents();
            Submit(platform.Input.Snapshot.OrderedInputs!.Last(input => input.LeftMouseReleased));
            PostPointer(0x0200, thumb, 0);
            PostPointer(0x0201, thumb, 1);
            platform.PollEvents();
            var nativePress = platform.Input.Snapshot.OrderedInputs!.Last(input => input.LeftMousePressed);
            if (nativePress.MousePosition != thumb)
                throw new InvalidOperationException("The native press did not land on the scrollbar thumb.");
            Submit(nativePress);
            var trackedCode = tree.CaptureSnapshot().Elements
                .Where(element => element.Node.Name == "C# 예제 소스" && element.Node.TextLayout is not null)
                .MaxBy(element => element.Node.TextLayout!.ClipBounds.Height).Node;
            var referenceY = trackedCode.TextLayout!.Origin.Y + ScrollPane().Scroll!.Value.Offset.Y;
            for (var frame = -4; frame < 16; frame++)
            {
                var direction = frame % 2 == 0 ? 1 : -1;
                for (var i = 0; i < 64; i++)
                {
                    var yMove = i == 63 ? 8 : 1 + i % 7;
                    PostPointer(0x0200, thumb with { Y = thumb.Y + direction * yMove }, 1);
                }
                platform.PollEvents();
                var inputs = platform.Input.Snapshot.OrderedInputs!.Where(input => input.LeftMouseDown && !input.LeftMousePressed).ToArray();
                if (inputs.Length != 64)
                    throw new InvalidOperationException($"Expected 64 native scrollbar moves, received {inputs.Length}.");
                var bytes = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                var before = paragraphRasters;
                using var draw = fixture.Build(Draw, inputs[0], pollEvents: false, orderedInputs: inputs);
                var composed = Stopwatch.GetTimestamp();
                bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
                if (!fixture.RenderSubmit(draw))
                    throw new InvalidOperationException("Vulkan rejected a native scrollbar drag frame.");
                var submitted = Stopwatch.GetTimestamp();
                var metrics = fixture.Context.InputProcessingMetrics;
                var area = fixture.Renderer.LastFrameMetrics.RepaintBounds;
                var offset = ScrollPane().Scroll!.Value.Offset.Y;
                var requests = draw.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.RenderVectorScene);
                if (MathF.Abs(offset - scroll.Offset.Y) < 1)
                    throw new InvalidOperationException("The scrollbar did not move the document.");
                if (!tree.TryGet(trackedCode.Id, out var currentCode) || currentCode.Node.TextLayout is not { } textLayout)
                    throw new InvalidOperationException("The scrolled code block disappeared from the document.");
                var lag = MathF.Abs(offset + textLayout.Origin.Y - referenceY);
                if (frame >= 0)
                    thumbLog.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{label}\t{frame}\t{metrics.RequestedSteps}\t{metrics.CompletedSteps}\t{Stopwatch.GetElapsedTime(start, composed).TotalMilliseconds:F4}\t{Stopwatch.GetElapsedTime(composed, submitted).TotalMilliseconds:F4}\t{bytes}\t{offset:R}\t{lag:F4}\t{area.Width}\t{area.Height}\t{requests}\t{draw.DrawDataFrameId}\t{paragraphRasters.Rasters - before.Rasters}\t{paragraphRasters.EmptyRasters - before.EmptyRasters}"));
            }
            var regrabPoint = thumb with { Y = thumb.Y - 8 };
            var owner = fixture.Context.State.ActiveId;
            PostPointer(0x0202, regrabPoint, 0);
            PostPointer(0x0201, regrabPoint, 1);
            platform.PollEvents();
            var regrab = platform.Input.Snapshot.OrderedInputs!.ToArray();
            if (regrab is not [{ LeftMouseReleased: true }, { LeftMousePressed: true }])
                throw new InvalidOperationException("Expected an immediate Win32 thumb release and re-press in one batch.");
            using (var draw = fixture.Build(gallery.Render, regrab[0], pollEvents: false, orderedInputs: regrab))
                if (!fixture.RenderSubmit(draw))
                    throw new InvalidOperationException("Vulkan rejected the scrollbar re-grab frame.");
            if (owner is null || fixture.Context.State.ActiveId != owner
                || fixture.Context.State.PreciseScrollbarDrag is not { Id: var active } || active != owner)
                throw new InvalidOperationException("The SVG document did not recapture its scrollbar thumb.");
            PostPointer(0x0202, regrabPoint, 0);
            platform.PollEvents();
            Submit(platform.Input.Snapshot.OrderedInputs!.Last(input => input.LeftMouseReleased));
            Console.WriteLine($"PASS {label}: immediate scrollbar re-grab");
            fixture.WriteCapturePng(Path.Combine(output, label + "-thumb.png"), fixture.Capture(gallery.Render, idle));

            UiAccessibilityNode ScrollPane() => tree.CaptureSnapshot().Elements
                .Single(element => element.Node.Name == "observer-svg-page").Node;

            UiAccessibilityTextLayout? VisibleCode() => tree.CaptureSnapshot().Elements
                .Where(element => element.Node.Name == "C# 예제 소스")
                .Select(element => element.Node.TextLayout).OfType<UiAccessibilityTextLayout>()
                .MaxBy(layout => layout.ClipBounds.Height);

            void Submit(UiInputState input)
            {
                using var draw = fixture.Build(Draw, input);
                if (!fixture.RenderSubmit(draw))
                    throw new InvalidOperationException("Vulkan rejected the scroll setup frame.");
            }

            void Draw(UiImmediateContext ui)
            {
                gallery.Render(ui);
                paragraphRasters = ui.ValidationParagraphRasterCounts;
            }

            void Post(uint message, nuint wParam, nint lParam)
            {
                if (!PresentationTestPlatform.PostMessage(platform.WindowHandle, message, wParam, lParam))
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            void PostPointer(uint message, UiVector2 point, nuint buttons)
                => Post(message, buttons, unchecked((nint)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16))));
        }
    }

    internal static void MeasureArcScroll(string output)
    {
        Directory.CreateDirectory(output);
        using var tree = new UiAccessibilityTree();
        using var fixture = new PresentationReuseTests.Fixture(3, true, PresentationTestPlatform.Create(1280, 900));
        fixture.Context.SetDesign(UiCompiledDesign.Windows11);
        fixture.Context.SetAccessibilityTree(tree);
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows())
            { FreezeDiagnosticText = true, SelectedPage = "SVG" };
        gallery.SvgPage.ConfigureArcPreview();
        var pointer = new UiInputState(new(840, 620), false, false, false, false, false, false,
            0, 0, [], [], default, default);
        using var log = new StreamWriter(Path.Combine(output, "arc.tsv"));
        log.WriteLine("phase\tframe\tui_ms\trenderer_ms\tallocated\tcode_visible\tvector_requests\treused\trepair_width\trepair_height\trepair_x\trepair_y");
        for (var frame = 0; frame < 10; frame++)
            Frame("reach", frame, pointer with { MouseWheel = -1 });
        for (var frame = 0; frame < 192; frame++)
        {
            var wheel = frame % 24 < 12 ? -.35f : .35f;
            Frame(frame < 96 ? "warmup" : "scroll", frame, pointer with { MouseWheel = wheel });
        }
        var toggle = tree.CaptureSnapshot().Elements.Single(element =>
            element.Node.Name == "갱신 영역 강조" && element.Node.Role == UiAccessibilityRole.Switch);
        if (tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Could not enable the actual Gallery repaint diagnostic.");
        for (var frame = 0; frame < 10; frame++)
            Frame("outline", frame, pointer);
        Thread.Sleep(250);
        for (var frame = 0; frame < 12; frame++)
            Frame("settle", frame, pointer);
        for (var frame = 0; frame < 3; frame++)
            Frame("highlight-scroll", frame, pointer with { MouseWheel = -.35f });

        void Frame(string phase, int frame, UiInputState input)
        {
            var bytes = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            using var draw = fixture.Build(gallery.Render, input);
            var composed = Stopwatch.GetTimestamp();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (!fixture.RenderSubmit(draw))
                throw new InvalidOperationException("The renderer rejected the arc scroll frame.");
            var submitted = Stopwatch.GetTimestamp();
            var metrics = fixture.Renderer.LastFrameMetrics;
            var visibleCode = tree.CaptureSnapshot().Elements.Count(element =>
                element.Node.Name == "C# 예제 소스" && element.Node.TextLayout is { ClipBounds.Height: > 0 });
            var vectorRequests = draw.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.RenderVectorScene);
            log.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{phase}\t{frame}\t{Stopwatch.GetElapsedTime(start, composed).TotalMilliseconds:F4}\t{Stopwatch.GetElapsedTime(composed, submitted).TotalMilliseconds:F4}\t{allocated}\t{visibleCode}\t{vectorRequests}\t{metrics.PresentationReused}\t{metrics.RepaintBounds.Width}\t{metrics.RepaintBounds.Height}\t{metrics.RepaintBounds.X}\t{metrics.RepaintBounds.Y}"));
        }
    }

    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        using var result = new StreamWriter(Path.Combine(output, "frames.tsv"));
        result.WriteLine("mode\tframe\tzoom\trotation\tcomposition_ms\trenderer_ms\tgpu_slot_ms\tvector_updates\tvector_pixels\tvector_dispatches\tmanaged_bytes\tsyntax_parses\txml_parses\tgpu_compiles\tcpu_rasters");
        using var fixture = new PresentationReuseTests.Fixture(3, true, PresentationTestPlatform.Create(1280, 900));
        fixture.Context.SetDesign(UiCompiledDesign.Windows11);
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows())
            { FreezeDiagnosticText = true, SelectedPage = "SVG" };
        var page = gallery.SvgPage;
        foreach (var mode in new[] { "static", "zoom", "rotate", "combined" })
        {
            for (var frame = -16; frame < 40; frame++)
            {
                var step = (frame + 16) % 40;
                var zoom = mode switch
                {
                    "zoom" or "combined" => 1 + step * .05f,
                    "rotate" => 2.5f,
                    _ => 1
                };
                var rotation = mode is "rotate" or "combined" ? step * 12 % 180 : 0;
                var allocations = GC.GetAllocatedBytesForCurrentThread();
                var dispatches = fixture.Renderer.VectorSceneDiagnostics.Dispatches;
                var start = Stopwatch.GetTimestamp();
                page.ConfigureBrowserPreview(zoom, rotation, highResolution: true);
                using var draw = fixture.Build(gallery.Render);
                var composed = Stopwatch.GetTimestamp();
                fixture.RenderSubmit(draw);
                var submitted = Stopwatch.GetTimestamp();
                if (frame < 0)
                    continue;
                var stats = page.Statistics ?? throw new InvalidOperationException("The SVG page did not render.");
                var parses = page.Samples.Sum(sample => sample.Metrics.SyntaxParses);
                var updates = 0;
                var pixels = 0L;
                foreach (var update in draw.TextureUpdates.AsSpan())
                {
                    if (update.Kind != UiTextureUpdateKind.RenderVectorScene)
                        continue;
                    updates++;
                    pixels += (long)update.Width * update.Height;
                }
                var gpu = fixture.Renderer.LastFrameMetrics.GpuRenderMicroseconds / 1000;
                result.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{mode}\t{frame}\t{zoom}\t{rotation}\t{Stopwatch.GetElapsedTime(start, composed).TotalMilliseconds:F4}\t{Stopwatch.GetElapsedTime(composed, submitted).TotalMilliseconds:F4}\t{gpu:F4}\t{updates}\t{pixels}\t{fixture.Renderer.VectorSceneDiagnostics.Dispatches - dispatches}\t{GC.GetAllocatedBytesForCurrentThread() - allocations}\t{parses}\t{stats.XmlParses}\t{stats.GpuSceneCompiles}\t{stats.Rasterizations}"));
            }
        }
        fixture.Context.State.AdvanceTime(.2f);
        using var settled = fixture.Build(gallery.Render);
        fixture.RenderSubmit(settled);
        const string latestZoom = "vector.Zoom = 1.75f;";
        var sources = page.Samples.Count(sample => sample.Source.Contains(latestZoom, StringComparison.Ordinal));
        if (sources != 2 || page.BrowserPreviews.Any(vector => vector.Zoom != 1.75f || vector.UseFixedResolutionPreview))
            throw new InvalidOperationException("The final zoom and both displayed SVG source examples must agree.");
        File.WriteAllText(Path.Combine(output, "settled.txt"), "Both browser sources show vector.Zoom = 1.75f; both previews restored exact resolution.");

        using var scroll = new StreamWriter(Path.Combine(output, "scroll.tsv"));
        scroll.WriteLine("frame\tframe_ms\tvector_updates\tpreview");
        var wheel = new UiInputState(new(800, 650), false, false, false, false, false, false,
            0, 0, [], [], default, default) with { MouseWheel = -.5f };
        for (var frame = 0; frame <= 12; frame++)
        {
            var start = Stopwatch.GetTimestamp();
            using var draw = fixture.Build(gallery.Render, frame == 12 ? wheel with { MouseWheel = 0 } : wheel);
            fixture.RenderSubmit(draw);
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var updates = 0;
            foreach (var update in draw.TextureUpdates.AsSpan())
                if (update.Kind == UiTextureUpdateKind.RenderVectorScene)
                    updates++;
            scroll.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{frame}\t{elapsed:F4}\t{updates}\t{page.BrowserPreviews.All(vector => vector.UseFixedResolutionPreview)}"));
        }
        fixture.Context.State.AdvanceTime(.2f);
        using var scrollSettled = fixture.Build(gallery.Render);
        fixture.RenderSubmit(scrollSettled);
        if (page.BrowserPreviews.Any(vector => vector.UseFixedResolutionPreview))
            throw new InvalidOperationException("The scroll idle deadline must restore exact camera resolution.");

        MeasurePointerGestures(output, highResolution: true);
    }

    internal static void MeasurePointerGestures(string output, bool highResolution, bool arc = false, bool primary = false,
        bool highlight = false, bool nativeMessages = false, bool burst = false, bool coalescePointerMoves = true,
        bool desktop = false, bool profileInput = false)
    {
        Directory.CreateDirectory(output);
        using var tree = new UiAccessibilityTree();
        var platform = PresentationTestPlatform.Create(1280, 900, orderedInput: nativeMessages);
        using var fixture = new PresentationReuseTests.Fixture(3, true, platform,
            msaaSamples: highlight || nativeMessages ? 4 : 1);
        using var desktopProbe = desktop ? new DesktopPresentationProbe(platform.WindowHandle) : null;
        using var desktopResults = desktop ? new StreamWriter(Path.Combine(output, "desktop.tsv")) : null;
        desktopResults?.WriteLine("phase\tframe_id\tposted_ticks\tobserved_ticks\tmessage_to_observed_ms\tsubmit_to_observed_ms");
        fixture.Context.InputProfilingEnabled = profileInput;
        using var inputResults = profileInput ? new StreamWriter(Path.Combine(output, "input-stages.tsv")) : null;
        inputResults?.WriteLine("phase\tframe_id\trequested\tcompleted\tpreparation_ms\tcomposition_ms\tfinalization_ms\telapsed_ms\tallocated_bytes");
        using var sectionResults = profileInput ? new StreamWriter(Path.Combine(output, "gallery-sections.tsv")) : null;
        sectionResults?.WriteLine("phase\tframe_id\tinput_navigation_us\tinput_options_us\tinput_page_us\tpresent_navigation_us\tpresent_options_us\tpresent_page_us");
        fixture.Context.SetDesign(UiCompiledDesign.Windows11);
        fixture.Context.SetAccessibilityTree(tree);
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows())
            { FreezeDiagnosticText = true, SelectedPage = "SVG", ProfileRenderSections = profileInput };
        var page = gallery.SvgPage;
        if (!primary)
            page.ConfigureBrowserPreview(1, highResolution: highResolution);
        if (arc)
            page.ConfigureArcPreview(coalescePointerMoves);
        IReadOnlyList<UiSvg> ComparisonPreviews() => arc ? page.ArcPreviews : page.BrowserPreviews;
        UiSvg Preview() => primary ? page.Preview! : ComparisonPreviews()[0];
        (int Rasters, int EmptyRasters, int Evictions, int EmptyEvictions) paragraphRasters = default;
        (long Navigation, long Options, long Page) inputSections = default, presentSections = default;
        void Draw(UiImmediateContext ui)
        {
            gallery.Render(ui);
            if (profileInput)
            {
                var sections = gallery.RenderSectionTicks;
                if (ui.IsPresentationPass)
                    presentSections = sections;
                else
                    inputSections = (inputSections.Navigation + sections.Navigation,
                        inputSections.Options + sections.Options, inputSections.Page + sections.Page);
            }
            desktopProbe?.Paint(ui);
            paragraphRasters = ui.ValidationParagraphRasterCounts;
        }
        using var result = new StreamWriter(Path.Combine(output, "pointer.tsv"));
        result.WriteLine("phase\tzoom\trotation\tpreview\tvector_updates\tvector_pixels\tcomposition_ms\trenderer_ms\tframe_id\tpresentation_sequence\treused\trepair_x\trepair_y\trepair_width\trepair_height\tdraws\tprojected\thighlight\tdiagnostic_redraw\tpost_to_input_ms\tpost_to_present_ms\tordered_steps\tcode_measurement_builds\tcode_paint_builds\tcode_parses\tparagraph_rasters\tempty_paragraph_rasters\tui_allocated_bytes\tcompose_start_ticks\tcompose_end_ticks");
        for (var frame = 0; frame < 96; frame++)
            Render("warmup");
        if (desktopProbe is not null)
        {
            platform.NotifyFirstFramePresented();
            platform.PollEvents();
        }

        if (highlight)
        {
            var toggle = tree.CaptureSnapshot().Elements.Single(element =>
                element.Node.Role == UiAccessibilityRole.Switch && element.Node.Name == "갱신 영역 강조");
            if (tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Toggle, wait: false)
                != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Could not toggle Gallery repaint diagnostics.");
            Render("highlight-enabled");
        }

        Drag("zoom", "SVG 확대");
        var zoom = Preview().Zoom;
        if (zoom <= 1)
            throw new InvalidOperationException("The zoom gesture did not change the Gallery SVG.");
        Drag("rotate", "SVG 회전");
        var rotation = Preview().RotationDegrees;
        if (rotation <= 0)
            throw new InvalidOperationException("The rotation gesture did not change the Gallery SVG.");
        fixture.Context.State.AdvanceTime(.2f);
        Render("idle");
        if (!primary && ComparisonPreviews().Any(vector => vector.UseFixedResolutionPreview == highResolution
            || vector.Zoom != zoom || vector.RotationDegrees != rotation))
            throw new InvalidOperationException("The SVG comparison did not settle at the final transform.");
        if (arc)
        {
            var work = page.Samples.Where(sample =>
                sample.Source.Contains("static (SvgStore Resources, UiWidget Preview) CreateBrowserComparison(", StringComparison.Ordinal)).ToArray();
            var finalZoom = $"vector.Zoom = {GallerySample.Number(zoom)};";
            var finalRotation = $"vector.RotationDegrees = {GallerySample.Number(rotation)};";
            bool HasFinalTransform(IGallerySample sample) => sample.Source.Contains(finalZoom, StringComparison.Ordinal)
                && sample.Source.Contains(finalRotation, StringComparison.Ordinal);
            if (work.Length != 6 || work.Any(HasFinalTransform))
                throw new InvalidOperationException("The first settled frame must defer all six source updates.");
            for (var completed = 0; completed < work.Length;)
            {
                if (fixture.Context.State.NextScheduledFrameTimeSeconds is not { } due || due > fixture.Context.State.TimeSeconds)
                    throw new InvalidOperationException("Pending sources must request the next frame in event-wait mode.");
                Render("source-settle");
                completed += 2;
                if (work.Count(HasFinalTransform) != completed)
                    throw new InvalidOperationException("Each settled frame must synchronize two additional Arc comparison sources.");
            }
        }
        var pixels = fixture.Capture(gallery.Render);
        fixture.WriteCapturePng(Path.Combine(output, "settled.png"), pixels);

        void Drag(string phase, string label)
        {
            var slider = tree.CaptureSnapshot().Elements.Single(element =>
                element.Node.Role == UiAccessibilityRole.Slider && element.Node.Name == label).Node.Bounds;
            UiInputState Pointer(float portion, bool down, bool pressed = false, bool released = false) =>
                new(new(slider.X + slider.Width * portion, slider.Y + slider.Height / 2),
                    down, false, pressed, released, false, false, 0, 0, [], [], default, default);
            Render(phase + "-press", Pointer(.25f, down: true, pressed: true));
            var finalPortion = .70f;
            if (burst)
            {
                var (low, high) = phase == "rotate" ? (.18f, .76f) : (.04f, .96f);
                for (var step = 0; step < 33; step++)
                {
                    finalPortion = step % 2 == 0 ? high : low;
                    Render(phase + "-move", Pointer(finalPortion, down: true),
                        Enumerable.Range(0, 15).Select(index =>
                            Pointer(index % 2 == 0 ? .12f : .88f, down: true)).ToArray());
                }
            }
            else
            {
                for (var step = 1; step <= 16; step++)
                    Render(phase + "-move", Pointer(.25f + .45f * step / 16, down: true));
            }
            fixture.Context.State.AdvanceTime(.2f);
            Render(phase + "-hold", Pointer(finalPortion, down: true));
            Render(phase + "-release", Pointer(finalPortion, down: false, released: true));
        }

        void Render(string phase, UiInputState? input = null, UiInputState[]? movesBeforeTarget = null)
        {
            var isPosted = nativeMessages && input.HasValue;
            var prior = isPosted ? (Preview().Zoom, Preview().RotationDegrees) : default;
            var previousSequence = fixture.Renderer.LastFrameMetrics.PresentationSequence;
            long posted = 0, captured = 0;
            IReadOnlyList<UiInputState>? orderedInputs = null;
            if (isPosted)
            {
                var target = input!.Value;
                var x = checked((short)MathF.Round(target.MousePosition.X));
                var y = checked((short)MathF.Round(target.MousePosition.Y));
                const uint mouseMove = 0x0200, mouseDown = 0x0201, mouseUp = 0x0202;
                var message = target.LeftMousePressed ? mouseDown : target.LeftMouseReleased ? mouseUp : mouseMove;
                var position = new UiVector2(x, y);
                // A stationary hold carries no new Win32 pointer event.
                if (phase.EndsWith("-hold", StringComparison.Ordinal))
                    isPosted = false;
                else
                {
                    posted = Stopwatch.GetTimestamp();
                    if (movesBeforeTarget is not null)
                    {
                        foreach (var move in movesBeforeTarget)
                        {
                            var moveX = checked((short)MathF.Round(move.MousePosition.X));
                            if (!PresentationTestPlatform.PostMessage(platform.WindowHandle, mouseMove, 1u,
                                unchecked((nint)((uint)(ushort)moveX | ((uint)(ushort)y << 16)))))
                                throw new Win32Exception(Marshal.GetLastPInvokeError());
                        }
                    }
                    if (!PresentationTestPlatform.PostMessage(platform.WindowHandle, message, target.LeftMouseDown ? 1u : 0u,
                        unchecked((nint)((uint)(ushort)x | ((uint)(ushort)y << 16)))))
                        throw new Win32Exception(Marshal.GetLastPInvokeError());
                    platform.PollEvents();
                    var inputs = platform.Input.Snapshot.OrderedInputs!;
                    var targetInput = inputs.Last(item => item.MousePosition == position
                        && item.LeftMouseDown == target.LeftMouseDown
                        && item.LeftMousePressed == target.LeftMousePressed
                        && item.LeftMouseReleased == target.LeftMouseReleased
                        && item.EventTimeSeconds.HasValue);
                    input = targetInput;
                    if (movesBeforeTarget is not null)
                    {
                        orderedInputs = inputs.Where(item => item.MousePosition.Y == y && item.LeftMouseDown
                            && !item.LeftMousePressed && !item.LeftMouseReleased && item.EventTimeSeconds.HasValue).ToArray();
                        if (orderedInputs.Count != movesBeforeTarget.Length + 1 || orderedInputs[^1] != targetInput)
                            throw new InvalidOperationException($"Expected {movesBeforeTarget.Length + 1} ordered pointer moves, received {orderedInputs.Count}.");
                    }
                    captured = Stopwatch.GetTimestamp();
                }
            }
            var beforeRasters = paragraphRasters;
            var measureDesktop = desktopProbe is not null && isPosted && phase.EndsWith("-move", StringComparison.Ordinal);
            if (measureDesktop)
                desktopProbe!.Alternate();
            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            inputSections = presentSections = default;
            using var draw = fixture.Build(Draw, input, pollEvents: !isPosted, orderedInputs: orderedInputs);
            if (arc && orderedInputs is { } batch)
            {
                var processing = fixture.Context.InputProcessingMetrics;
                if (processing.RequestedSteps != batch.Count
                    || processing.CompletedSteps != (coalescePointerMoves ? 2 : batch.Count)
                    || processing.BuiltDrawings != 1)
                    throw new InvalidOperationException("The ordered pointer batch produced an unexpected number of input passes.");
            }
            var composed = Stopwatch.GetTimestamp();
            var uiAllocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            if (!fixture.RenderSubmit(draw))
                throw new InvalidOperationException($"The renderer rejected the {phase} frame.");
            var submitted = Stopwatch.GetTimestamp();
            if (desktopProbe is not null && (measureDesktop || phase.EndsWith("-press", StringComparison.Ordinal)))
            {
                var observed = desktopProbe.WaitForMarker(draw.FramebufferScale);
                if (measureDesktop)
                    desktopResults!.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"{phase}\t{draw.DrawDataFrameId}\t{posted}\t{observed}\t{Stopwatch.GetElapsedTime(posted, observed).TotalMilliseconds:F4}\t{Stopwatch.GetElapsedTime(submitted, observed).TotalMilliseconds:F4}"));
            }
            var updates = 0;
            long pixels = 0;
            foreach (var update in draw.TextureUpdates.AsSpan())
            {
                if (update.Kind != UiTextureUpdateKind.RenderVectorScene)
                    continue;
                updates++;
                pixels += (long)update.Width * update.Height;
            }
            var metrics = fixture.Renderer.LastFrameMetrics;
            var vector = Preview();
            var repair = metrics.RepaintBounds;
            var projected = draw.DrawLists.Count(list => list.Scene3D is not null);
            var toInput = isPosted ? Stopwatch.GetElapsedTime(posted, captured).TotalMilliseconds : double.NaN;
            var toPresent = double.NaN;
            if (isPosted && phase.EndsWith("-move", StringComparison.Ordinal))
            {
                if ((vector.Zoom, vector.RotationDegrees) == prior || metrics.PresentationReused
                    || metrics.DrawDataFrameId != draw.DrawDataFrameId || metrics.PresentationSequence != previousSequence + 1
                    || metrics.PresentationTimestampTicks < captured || metrics.PresentationTimestampTicks > submitted)
                    throw new InvalidOperationException($"The {phase} message did not produce a distinct SVG transform and presentation.");
                toPresent = Stopwatch.GetElapsedTime(posted, metrics.PresentationTimestampTicks).TotalMilliseconds;
            }
            if (inputResults is not null)
            {
                var intake = fixture.Context.InputProcessingMetrics;
                inputResults.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{phase}\t{draw.DrawDataFrameId}\t{intake.RequestedSteps}\t{intake.CompletedSteps}\t{intake.PreparationMilliseconds:F4}\t{intake.CompositionMilliseconds:F4}\t{intake.FinalizationMilliseconds:F4}\t{intake.ElapsedMilliseconds:F4}\t{intake.AllocatedBytesOnRenderThread}"));
            }
            if (sectionResults is not null)
            {
                const double microsecondsPerSecond = 1_000_000d;
                var scale = microsecondsPerSecond / Stopwatch.Frequency;
                sectionResults.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{phase}\t{draw.DrawDataFrameId}\t{inputSections.Navigation * scale:F1}\t{inputSections.Options * scale:F1}\t{inputSections.Page * scale:F1}\t{presentSections.Navigation * scale:F1}\t{presentSections.Options * scale:F1}\t{presentSections.Page * scale:F1}"));
            }
            var code = default(UiCodeViewMetrics);
            foreach (var sample in page.Samples)
            {
                var item = sample.Metrics;
                code = new(code.MeasurementBuilds + item.MeasurementBuilds, code.PaintBuilds + item.PaintBuilds,
                    code.SyntaxParses + item.SyntaxParses);
            }
            result.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{phase}\t{vector.Zoom}\t{vector.RotationDegrees}\t{vector.UseFixedResolutionPreview}\t{updates}\t{pixels}\t{Stopwatch.GetElapsedTime(start, composed).TotalMilliseconds:F4}\t{Stopwatch.GetElapsedTime(composed, submitted).TotalMilliseconds:F4}\t{draw.DrawDataFrameId}\t{metrics.PresentationSequence}\t{metrics.PresentationReused}\t{repair.X}\t{repair.Y}\t{repair.Width}\t{repair.Height}\t{metrics.DrawCalls}\t{projected}\t{metrics.RepaintHighlightActive}\t{metrics.RepaintDiagnosticRedraw}\t{toInput:F4}\t{toPresent:F4}\t{orderedInputs?.Count ?? 1}\t{code.MeasurementBuilds}\t{code.PaintBuilds}\t{code.SyntaxParses}\t{paragraphRasters.Rasters - beforeRasters.Rasters}\t{paragraphRasters.EmptyRasters - beforeRasters.EmptyRasters}\t{uiAllocated}\t{start}\t{composed}"));
        }
    }
}
