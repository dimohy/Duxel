using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static class AllFeaturesSampleTests
{
    internal static void Run(Action<bool, string> check, string output, string assemblyPath, string planPath, bool inputOnly = false, bool overlayOnly = false, bool optimizationOnly = false, bool rootOnly=false)
    {
        var assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        var type = assembly.GetType("AllFeaturesScreen", throwOnError: true)!;
        var plan = JsonDocument.Parse(File.ReadAllText(planPath));
        var cases = plan.RootElement.GetProperty("views").EnumerateArray()
            .Select(view => (Field: type.GetField(view.GetProperty("field").GetString()!, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Declared showcase field is missing."),
                Title: view.GetProperty("title").GetString()!)).ToArray();
        check(cases.Length == 37 && cases.Select(entry => entry.Field.Name).Distinct().Count() == 37,
            "showcase matrix covers all 37 source-declared feature windows without duplicates");
        ExceptionDispatchInfo? failure = null;
        var owner = new Thread(() =>
        {
            try
            {
                var screen=(UiScreen)Activator.CreateInstance(type)!;
                type.GetProperty("UsePanels")!.SetValue(screen,!rootOnly);
                Render(check,output,screen,cases,inputOnly,overlayOnly,optimizationOnly,rootOnly);
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        }) { IsBackground = true, Name = "DuxelAllFeaturesOwner" };
        owner.Start();
        if (!owner.Join(TimeSpan.FromSeconds(120))) throw new TimeoutException("All-features GPU verification timed out.");
        failure?.Throw();
    }

    private static void Render(Action<bool, string> check, string output, UiScreen screen,
        (FieldInfo Field, string Title)[] cases, bool inputOnly, bool overlayOnly, bool optimizationOnly,bool rootOnly)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 1100, Height = 740, Title = "Duxel All Features GPU Verification", VSync = false,
            UseDuxelTitleBar = overlayOnly||rootOnly, DuxelTitleBarHeight = 48,
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new(),
        });
        using var renderer = new VulkanRendererBackend(platform, new VulkanRendererOptions(3, EnableVSync: false, MsaaSamples: 4));
        renderer.CreateDeviceObjects();
        var options = new DuxelAppOptions { Screen = screen };
        UiScreen activeScreen = overlayOnly||rootOnly ? new DuxelWindowChromeScreen(screen, platform, 48) : screen;
        // Match the real Windows app session's decoder registration before image widgets run.
        UiImageTexture.ImageDecoder = WindowsUiImageDecoder.Shared;
        var atlas = DuxelApp.BuildFontAtlas(options.Font, new HashSet<int>(Enumerable.Range(32, 95)), useStartupSettings: false);
        var white = new UiTextureId(2);
        using var context = new UiContext(atlas, new(1), white);
        DuxelApp.ConfigureContext(context, options, platform);
        context.SetDirectTextPrimaryFontPath(options.Font.PrimaryFontPath);
        context.SetDirectTextSecondaryFontPath(options.Font.SecondaryFontPath);
        context.SetDirectTextBaseFontSize(options.Font.FontSize);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextFallbackEnabled(false);
        var size = platform.FramebufferSize;
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var results = new List<object>();
        var firstFrame = true;
        var parityResults = new List<object>();
        var partialFrames = 0;
        var captureInFlight = false;
        var referenceCrops = new List<byte[]>();
        var retainedMetrics = default(UiRendererFrameMetrics);
        var retainedEmpty = false;
        var retainedPartial = false;
        long uiAllocatedBytes = 0;
        byte[] Step(UiInputState events)
        {
            platform.PollEvents();
            if (optimizationOnly)
            {
                var counter = (UiFpsCounter)screen.GetType().GetField("_fpsCounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(screen)!;
                counter.Reset();
                counter.GetType().GetField("_currentFps", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(counter, 3000f);
                context.State.NewFrameTimeMs = .01f;
                context.State.RenderTimeMs = .1f;
                context.State.SubmitTimeMs = .2f;
            }
            context.NewFrame(new(1f / 60, new(size.Width, size.Height), new(1, 1)), events,
                new(0, 0, size.Width, size.Height), UiTextSettings.Default);
            if (firstFrame)
            {
                context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, white, UiTextureFormat.Rgba8Unorm,
                    1, 1, new byte[] { 255, 255, 255, 255 }));
                context.QueueTextureUpdate(atlas.CreateTextureUpdate(new(1), UiTextureUpdateKind.Create));
                firstFrame = false;
            }
            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            context.Render(activeScreen);
            uiAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            using var data = context.GetDrawData();
            if (captureInFlight)
            {
                if (!renderer.TryRenderDrawData(data)) throw new InvalidOperationException("Asynchronous overlay frame was not presented.");
                return [];
            }
            var pixels = renderer.RenderAndReadback(data);
            if (optimizationOnly)
            {
                retainedMetrics = renderer.LastFrameMetrics;
                retainedEmpty = renderer.ValidationDamage().Empty;
                retainedPartial = renderer.ValidationDamage().Partial;
                renderer.ValidationFullRepaint = true;
                var full = renderer.RenderAndReadback(data);
                check(pixels.AsSpan().SequenceEqual(full), "ordinary widget retained pixels match full repaint on frame " + context.State.FrameCount);
            }
            if (overlayOnly)
            {
                var damage = renderer.ValidationDamage();
                if (damage.Partial) partialFrames++;
                renderer.ValidationFullRepaint = true;
                var reference = renderer.RenderAndReadback(data);
                var crop = new byte[400 * 48 * 4];
                for (var row = 0; row < 48; row++) reference.AsSpan(((48 + row) * size.Width + 700) * 4, 400 * 4).CopyTo(crop.AsSpan(row * 400 * 4));
                referenceCrops.Add(crop);
                var different = 0;
                var overlayPixels = 0;
                for (var offset = 0; offset < pixels.Length; offset += 4)
                    if (!pixels.AsSpan(offset, 4).SequenceEqual(reference.AsSpan(offset, 4))) different++;
                var overlayTop = overlayOnly ? 48 : 0;
                for (var y = overlayTop; y < overlayTop + 25; y++)
                    for (var x = 700; x < size.Width; x++)
                    {
                        var offset = (y * size.Width + x) * 4;
                        if (Math.Max(pixels[offset], Math.Max(pixels[offset + 1], pixels[offset + 2])) > 90) overlayPixels++;
                    }
                parityResults.Add(new { frame = parityResults.Count, partial = damage.Partial,
                    empty = damage.Empty, width = damage.Width, height = damage.Height, differentPixels = different, overlayPixels });
                File.WriteAllText(Path.Combine(output, "overlay-parity.json"), JsonSerializer.Serialize(parityResults, new JsonSerializerOptions { WriteIndented = true }));
                if (different != 0)
                {
                    File.WriteAllBytes(Path.Combine(output, "partial.rgba"), pixels);
                    File.WriteAllBytes(Path.Combine(output, "full.rgba"), reference);
                }
                check(different == 0, "changing real FPS/timing overlay matches full repaint on frame " + (parityResults.Count - 1));
                check(overlayPixels > 100, "FPS glyphs remain visible on frame " + (parityResults.Count - 1));
            }
            return pixels;
        }
        if (overlayOnly)
        {
            var counter = screen.GetType().GetField("_fpsCounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(screen)!;
            var fps = counter.GetType().GetField("_currentFps", BindingFlags.NonPublic | BindingFlags.Instance)!;
            input = input with { MousePosition = new(80, 100), LeftMouseDown = true };
            const int overlayFrameCount = 128;
            int[] values = [3001, 3010, 3099, 3100, 3999, 4000, 10000, 3042];
            for (var frame = 0; frame < overlayFrameCount; frame++)
            {
                ((UiFpsCounter)counter).Reset();
                fps.SetValue(counter, (float)values[(frame / 16) % values.Length]);
                context.State.NewFrameTimeMs = .01f * (((frame / 3) % 4));
                context.State.RenderTimeMs = .10f + .07f * (((frame / 3) % 4));
                context.State.SubmitTimeMs = .25f + .11f * (((frame / 3) % 4));
                Step(input);
            }
            if (Environment.GetEnvironmentVariable("DUXEL_VK_DAMAGE") != "0")
                check(partialFrames > 16, "overlay parity exercises actual retained partial repaint after glyph warmup");
            renderer.BeginValidationCapture(700, 48, 400, 48, overlayFrameCount);
            captureInFlight = true;
            for (var frame = 0; frame < overlayFrameCount; frame++)
            {
                ((UiFpsCounter)counter).Reset();
                fps.SetValue(counter, (float)values[(frame / 16) % values.Length]);
                context.State.NewFrameTimeMs = .01f * (((frame / 3) % 4));
                context.State.RenderTimeMs = .10f + .07f * (((frame / 3) % 4));
                context.State.SubmitTimeMs = .25f + .11f * (((frame / 3) % 4));
                Step(input);
            }
            var asynchronousFrames = renderer.EndValidationCapture();
            captureInFlight = false;
            var asyncResults = new List<object>();
            for (var frame = 0; frame < asynchronousFrames.Length; frame++)
            {
                var same = asynchronousFrames[frame].AsSpan().SequenceEqual(referenceCrops[frame]);
                asyncResults.Add(new { frame, matchesFullRepaint = same });
                File.WriteAllText(Path.Combine(output, "overlay-inflight.json"), JsonSerializer.Serialize(asyncResults, new JsonSerializerOptions { WriteIndented = true }));
                if (!same)
                {
                    File.WriteAllBytes(Path.Combine(output, "inflight.rgba"), asynchronousFrames[frame]);
                    File.WriteAllBytes(Path.Combine(output, "expected-crop.rgba"), referenceCrops[frame]);
                }
                check(same, "in-flight FPS overlay matches its full-repaint reference on frame " + frame);
            }
            return;
        }
        if (optimizationOnly)
        {
            var selected = new[] { "_showRadioButton", "_showButton", "_showCheckbox", "_showSlider", "_showInput", "_showColumns" };
            foreach (var field in selected)
            {
                foreach (var entry in cases) entry.Field.SetValue(screen, entry.Field.Name == field);
                for (var frame = 0; frame < 8; frame++) Step(input);
                // Columns intentionally displays the sample's animated progress value.
                // Preserve that workload and distinguish changed content from stable controls.
                var animated = field == "_showColumns";
                check(animated ? !retainedEmpty && retainedMetrics.DrawCalls > 0 : retainedEmpty && retainedMetrics.DrawCalls == 0,
                    (animated ? "animated ordinary widget repaints changed content: " : "stable ordinary widget suppresses GPU draws without an explicit BeginLayer: ") + field);
                if (!animated) check(retainedMetrics.DynamicUploadBytes == 0,
                    "stable ordinary widget skips streamed GPU geometry uploads: " + field);
                results.Add(new { field, retainedMetrics.DrawCalls, retainedMetrics.DynamicUploadBytes,
                    retainedMetrics.ResidentHits, uiAllocatedBytes, retainedEmpty, animated,
                    note = "functional readback diagnostic; not a throughput benchmark; FPS/timing labels fixed to isolate widget content" });
                File.WriteAllText(Path.Combine(output, "widget-optimization.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            }
            var scrollMouse = default(UiVector2);
            var scrollKey = string.Empty;
            activeScreen = new ActionScreen(ui =>
            {
                ui.SetNextWindowPos(new(20, 90));
                ui.SetNextWindowSize(new(500, 400));
                ui.BeginWindow("Ordinary widget scroll validation");
                var childOrigin = ui.GetCursorScreenPos();
                scrollMouse = new(childOrigin.X + 20, childOrigin.Y + 20);
                scrollKey = ui.GetID("optimization-scroll");
                if (ui.BeginChild("optimization-scroll", new(350, 120), true))
                    for (var row = 0; row < 28; row++) ui.Text("Scrollable ordinary text row " + row);
                ui.EndChild();
                ui.EndWindow();
            });
            byte[] beforeScroll = [];
            for (var frame = 0; frame < 4; frame++) beforeScroll = Step(input);
            var originalScroll = context.State.GetScrollY(scrollKey);
            Step(input with { MousePosition = scrollMouse, MouseWheel = -2 });
            var afterScroll = Step(input with { MousePosition = scrollMouse });
            var changedScroll = context.State.GetScrollY(scrollKey);
            check(changedScroll > originalScroll && !beforeScroll.AsSpan().SequenceEqual(afterScroll),
                "ordinary child consumes wheel input and repaints scrolled content under its clip");
            var previousSize = size;
            check(NativeTestMethods.SetWindowPos(platform.WindowHandle, 0, 0, 0, 900, 680, 0x0002 | 0x0004 | 0x0010),
                "ordinary widget validation resizes its own native surface");
            platform.PollEvents();
            size = platform.FramebufferSize;
            Step(input);
            check(size != previousSize && !retainedPartial, "native resize invalidates retained pixels before ordinary widget redraw");
            File.WriteAllText(Path.Combine(output, "scroll-resize.json"), JsonSerializer.Serialize(new
            {
                originalScroll, changedScroll, previousSize, size, fullRepaintOnResize = !retainedPartial,
                inputBoundary = "UiInputState wheel; own native validation window resized",
            }, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        foreach (var test in inputOnly ? [] : cases)
        {
            foreach (var entry in cases) entry.Field.SetValue(screen, entry.Field == test.Field);
            if(rootOnly)screen.GetType().GetField("_selectedPage",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(screen,test.Title);
            byte[] pixels = [];
            for (var frame = 0; frame < 4; frame++)
            {
                pixels = Step(input);
            }
            if(rootOnly)check(!context.State.TryGetWindowRect(test.Title,out _),"default page renders directly without an internal panel: "+test.Title);
            else check(context.State.TryGetWindowRect(test.Title, out var rect) && rect.Width > 0 && rect.Height > 0
                && context.State.GetWindowOpen(test.Title), "showcase panel lays out and remains open: " + test.Title);
            var colored = 0;
            for (var y = 120; y < Math.Min(size.Height, 700); y++)
                for (var x = 20; x < Math.Min(size.Width, 1080); x++)
                {
                    var offset = (y * size.Width + x) * 4;
                    var maximum = Math.Max(pixels[offset], Math.Max(pixels[offset + 1], pixels[offset + 2]));
                    var minimum = Math.Min(pixels[offset], Math.Min(pixels[offset + 1], pixels[offset + 2]));
                    if (maximum > 60 && maximum - minimum > 20) colored++;
                }
            check(colored > 250, "showcase window produces nonempty GPU content: " + test.Title);
            if (test.Field.Name == "_showImage")
            {
                var image = screen.GetType().GetField("_imgPlayer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(screen);
                var error = screen.GetType().GetField("_imgLoadError", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(screen);
                Console.WriteLine($"IMAGE loaded={image is not null} error={error ?? "none"}");
                check(image is not null && error is null, "remote image loads and image effects render without a hidden load failure");
            }
            var file = Path.Combine(output, test.Field.Name + ".ppm");
            using (var stream = File.Create(file))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{size.Width} {size.Height}\n255\n"));
                var rgb = new byte[size.Width * size.Height * 3];
                for (var i = 0; i < size.Width * size.Height; i++) pixels.AsSpan(i * 4, 3).CopyTo(rgb.AsSpan(i * 3, 3));
                stream.Write(rgb);
            }
            results.Add(new { field = test.Field.Name, title = test.Title, coloredPixels = colored, image = file,
                presentation = renderer.LastFrameMetrics.PresentationSequence });
            File.WriteAllText(Path.Combine(output, "views.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (!inputOnly) return;

        var inputResults = new List<object>();
        object? Value(string name) => screen.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(screen);
        void Select(string name)
        {
            foreach (var entry in cases) entry.Field.SetValue(screen, entry.Field.Name == name);
            for (var frame = 0; frame < 4; frame++) Step(input);
        }
        byte[] Click(float x, float y)
        {
            Step(input with { MousePosition = new(x, y), LeftMouseDown = true, LeftMousePressed = true });
            return Step(input with { MousePosition = new(x, y), LeftMouseReleased = true });
        }
        void Record(string name, bool success, object? before, object? after, byte[] pixels)
        {
            var file = Path.Combine(output, name + ".ppm");
            using var stream = File.Create(file);
            stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{size.Width} {size.Height}\n255\n"));
            for (var i = 0; i < pixels.Length; i += 4) stream.Write(pixels.AsSpan(i, 3));
            inputResults.Add(new { name, success, before, after, image = file, inputMode = "UiInputState through the real compiled sample and Vulkan" });
            File.WriteAllText(Path.Combine(output, "interactions.json"), JsonSerializer.Serialize(inputResults, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllLines(Path.Combine(output, "input-debug.log"), context.State.DebugLogEntries);
            check(success, "real showcase input changes application state: " + name);
        }
        Select("_showButton");
        var before = Value("_clickCount");
        var rendered = Click(60, 338);
        Record("button", (int)Value("_clickCount")! == (int)before! + 1, before, Value("_clickCount"), rendered);
        Select("_showCheckbox"); before = Value("_checkboxA"); rendered = Click(30, 330);
        Record("checkbox", !Equals(before, Value("_checkboxA")), before, Value("_checkboxA"), rendered);
        Select("_showSlider"); before = Value("_sliderFloat"); rendered = Click(350, 338);
        Record("slider", Math.Abs((float)before! - (float)Value("_sliderFloat")!) > .1f, before, Value("_sliderFloat"), rendered);
        Select("_showInput"); before = Value("_inputText"); Click(240, 338);
        // Windows inputs consume their IME commit queue instead of raw CharEvents.
        // Inject at that boundary on its owner thread, without OS input to the user's window.
        var ime = platform.ImeHandler ?? throw new InvalidOperationException("Windows IME handler is required.");
        var committed = (Dictionary<string, string>)ime.GetType().GetField("_committedByInput", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(ime)!;
        committed.Add(context.State.ActiveId ?? throw new InvalidOperationException("Text field did not acquire focus."), "Duxel QA 42");
        rendered = Step(input);
        Record("text", ((string)Value("_inputText")!).Contains("Duxel QA 42", StringComparison.Ordinal), before, Value("_inputText"), rendered);
        Select("_showCombo"); before = Value("_comboIdx"); Click(140, 366); rendered = Click(120, 448);
        Record("combo", (int)Value("_comboIdx")! == 1, before, Value("_comboIdx"), rendered);
        Select("_showPopup"); rendered = Click(60, 338);
        var bools = (Dictionary<string, bool>)typeof(UiState).GetField("_bools", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(context.State)!;
        var opened = bools.Any(pair => pair.Key.EndsWith("TestPopup##popup", StringComparison.Ordinal) && pair.Value);
        rendered = Click(1000, 650);
        var closed = !bools.Any(pair => pair.Key.EndsWith("TestPopup##popup", StringComparison.Ordinal) && pair.Value);
        Record("popup", opened && closed, opened, closed, rendered);
    }
}







