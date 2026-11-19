using Duxel.Core;
using Duxel.Gallery;

internal static class GallerySampleTests
{
    internal static void Run(Action<bool, string> check, string? output = null)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new CardPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.NewFrame(new(.016f, new(1280, 50000), new(1, 1)), idle, new(0, 0, 1280, 50000), UiTextSettings.Default);
        context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); frame.Render(ui); }));
        using var data = context.GetDrawData();
        var nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
        var sources = nodes.Where(node => node.Name == "C# 예제 소스" && node.Role == UiAccessibilityRole.Edit).ToArray();
        var expected = 6 + Enum.GetValues<UiCardSize>().Length * Enum.GetValues<UiCardAppearance>().Length;
        check(page.Samples.Count == expected && sources.Length == expected,
            "every Card sample including each size/appearance variant owns a separate source block");
        check(sources.All(source => source.ReadOnly && page.Samples.Any(sample => sample.Source == source.Value)),
            "each Card source block is the unchanged caller-captured factory for its preview");
        var first = nodes.Single(node => node.Name == "작업 카드");
        var second = nodes.Single(node => node.Name == "떠 있는 동작 카드");
        check(first.Bounds.Width == 320, "attaching source preserves the original preview width");
        Console.WriteLine($"Card source gap : {sources[0].Bounds.Y - first.Bounds.Y - first.Bounds.Height}; card : {first.Bounds}; source : {sources[0].Bounds}");
        check(sources[0].Bounds.Y >= first.Bounds.Y + first.Bounds.Height
            && sources[0].Bounds.Y - first.Bounds.Y - first.Bounds.Height <= 24,
            "the first Card's source follows its shadow within normal component spacing");
        check(second.Bounds.Y >= sources[0].Bounds.Y + sources[0].Bounds.Height
            && sources[1].Bounds.Y >= second.Bounds.Y + second.Bounds.Height,
            "Card one, source one, Card two and source two appear in that order");
        check(!nodes.Any(node => node.Name is "C# 소스" or "줄바꿈" or "줄 번호" or "전체 표시"),
            "inline samples omit the code heading and all three option toggles");
        check(nodes.Count(node => node.Name == "소스 복사" && node.Role == UiAccessibilityRole.Button) == expected,
            "every inline code block retains its own accessible copy action");
        check(sources.All(source => !source.Value!.Contains("using Duxel.Core;", StringComparison.Ordinal)),
            "Gallery inline snippets omit the repeated Core import without changing UiCodeView");
        ExportSources(output, "Card", sources);
        RunRating(check);
        RunAvatars(check);
        RunSearch(check);
        RunLayout(check, output);
        RunNumericAndToolbar(check);
        RunAvatarGroup(check);
        RunPersona(check);
        RunAccordion(check);
        RunLists(check);
        RunDropdownSources(check);
        RunPopoverSources(check);
        RunDrawerSources(check, output);
        RunSkeletonSources(check);
        RunDialogSources(check);
        RunMessageBarSources(check);
        RunVirtualSources(check);
        RunWidgetLayoutSources(check);
        RunIntegrationSources(check);
        RunRenderingSources(check);
        RunViewHostSemantics(check);
        RunPinnedGalleryHeaders(check);
        RunPopupInputSources(check);
        RunTooltipSources(check);
        RunEditingSources(check);
        RunMeasurementSources(check);
        RunRepaintSources(check);
        RunMarkdownSources(check, output);
        RunControlsSources(check);
        RunPrimarySvgSources(check);
        RunSvgBrowserSources(check);
        RunLegacySources(check);
    }

    private static void ExportSources(string? output, string name, params UiAccessibilityNode[] sources)
    {
        if (output is null) return;
        var folder = Path.Combine(output, "sources");
        Directory.CreateDirectory(folder);
        for (var index = 0; index < sources.Length; index++)
        {
            var filename = $"{name}-{index + 1:D2}.cs.txt";
            File.WriteAllText(Path.Combine(folder, filename), sources[index].Value
                ?? throw new InvalidOperationException($"The displayed source for {filename} has no value."));
        }
    }
    private static void RunRating(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var rating = new RatingPage();
        using var page = new WidgetPage("평점", "현재 평가 설정을 코드에 반영합니다.", rating);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 14000), new(1, 1)), input ?? idle, new(0, 0, 1280, 14000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); frame.Render(ui); }));
            using var data = context.GetDrawData();
        }
        void Click(string name, UiAccessibilityRole role)
        {
            var node = tree.CaptureSnapshot().Elements.First(element => element.Node.Name == name && element.Node.Role == role).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        string[] Sources() => tree.CaptureSnapshot().Elements
            .Where(element => element.Node.Role == UiAccessibilityRole.Edit && element.Node.Name == "C# 예제 소스")
            .Select(element => element.Node.Value!).ToArray();
        Frame(); Frame();
        check(Sources().Length == 5 && rating.Samples.Count == 5,
            "each whole, half, wrapped, full-display and compact Rating owns a source block");
        Click("브랜드", UiAccessibilityRole.RadioButton);
        check(Sources().All(source => source.Contains("Color = UiRatingColor.Brand", StringComparison.Ordinal)),
            "changing Rating color through its actual radio control updates every corresponding snippet");
        Click("원", UiAccessibilityRole.RadioButton);
        Click("오른쪽에서 왼쪽 순서", UiAccessibilityRole.Switch);
        check(Sources().All(source => source.Contains("Shape = UiRatingShape.Circle", StringComparison.Ordinal)
            && source.Contains("RightToLeft = true", StringComparison.Ordinal)),
            "Rating shape and direction changes appear in the code shown below each sample");
        Click("리뷰 수 늘리기", UiAccessibilityRole.Button);
        check(Sources().Count(source => source.Contains("new UiState<int>(1235)", StringComparison.Ordinal)) == 2,
            "review count changes update both RatingDisplay code blocks");
        Click("4", UiAccessibilityRole.RadioButton);
        check(Sources().All(source => source.Contains("new UiState<float>(4f)", StringComparison.Ordinal)),
            "choosing a Rating value updates the shared state initializer in every sample's code");
        var before = rating.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame(); Frame();
        check(before.SequenceEqual(rating.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "unchanged sample state reuses syntax without regenerating or reparsing its source");
        check(Sources().All(source => !source.Contains("using Duxel.Core;", StringComparison.Ordinal)),
            "live Rating snippets also omit the common Core import");
        var header = "using System;\nusing Duxel.Core;\n\nvar text = \"using Duxel.Core;\";";
        check(GalleryPageSource.OmitCommonImport(header) == "using System;\n\nvar text = \"using Duxel.Core;\";",
            "Gallery import omission leaves other imports and source string literals untouched");
    }
    private static void RunAvatars(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new AvatarPage(ObserverServices.CreateWindows());
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.NewFrame(new(.016f, new(1280, 30000), new(1, 1)), idle, new(0, 0, 1280, 30000), UiTextSettings.Default);
        context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); frame.Render(ui); }));
        using var data = context.GetDrawData();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Role == UiAccessibilityRole.Edit
            && element.Node.Name == "C# 예제 소스").ToArray();
        check(page.Samples.Count == 16 && sources.Length == 16,
            "Avatar preview, presence and all14 supported sizes each own an adjacent source block");
        check(sources.Skip(2).Select(element => element.Node.Value!).Distinct().Count() == 14,
            "each Avatar size snippet includes its actual enum value rather than an unresolved loop variable");
    }
    private static void RunSearch(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new SearchBoxPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 2400), new(1, 1)), input ?? idle, new(0, 0, 1280, 2400), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); frame.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Click(string name)
        {
            var node = Find(name, UiAccessibilityRole.Button).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        string Source() => Find("C# 예제 소스", UiAccessibilityRole.Edit).Node.Value!;
        Frame(); Frame();
        var input = Find("검색어", UiAccessibilityRole.SearchBox).Node;
        var source = Find("C# 예제 소스", UiAccessibilityRole.Edit).Node;
        check(source.Bounds.Y >= input.Bounds.Y + input.Bounds.Height && source.Bounds.Y - input.Bounds.Y - input.Bounds.Height < 32
            && Source().Contains("Fluent.SearchBox(", StringComparison.Ordinal) && !Source().Contains("class SearchBoxPage", StringComparison.Ordinal),
            "SearchBox exposes its compact Fluent factory snippet immediately below the actual input");
        var editor = Find("검색어", UiAccessibilityRole.SearchBox);
        const string query = "디모이 \"quoted\" \\path";
        check(tree.RequestAction(editor.Node.Id, editor.RuntimeId, UiAccessibilityAction.SetValue, query, wait: false) == UiAccessibilityResult.Success,
            "Gallery SearchBox accepts an actual native value update");
        Frame(); Frame();
        check(Source().Contains("\\\"quoted\\\"", StringComparison.Ordinal) && Source().Contains("\\\\path", StringComparison.Ordinal),
            "SearchBox code reflects the current query with valid escaped C# string literals");
        Click("검색 읽기 전용");
        Click("검색 방향");
        Click("검색 변경 거부");
        check(Source().Contains("ReadOnly: true", StringComparison.Ordinal)
            && Source().Contains("RightToLeft: true", StringComparison.Ordinal)
            && Source().Contains("var rejectChanges = true;", StringComparison.Ordinal),
            "SearchBox code follows current read-only, direction and controlled-rejection settings");
        var before = page.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(page.Samples[0].Metrics.SyntaxParses == before,
            "stable SearchBox state retains its parsed source");
        check(GallerySample.String("a\"\\\n\t\u0001") == "\"a\\\"\\\\\\n\\t\\u0001\"",
            "Gallery C# literals escape quotes, slashes and control characters without changing the input");
        check(GallerySample.String("a\u2028b\u2029c") == "\"a\\u2028b\\u2029c\"",
            "Gallery C# literals escape Unicode line and paragraph separators");
        check(GallerySample.DocumentString("a\nb\u2028c\u2029d") == "\"a\\nb\\u2028c\\u2029d\"",
            "Gallery document literals preserve Unicode line and paragraph separators as explicit escapes");
    }
    private static void RunLayout(Action<bool, string> check, string? output)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var layout = new LayoutPage();
        using var page = new WidgetPage("컨테이너", "기본 배치와 대응 코드", layout);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 18000), new(1, 1)), input ?? idle, new(0, 0, 1280, 18000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityNode Find(string name) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name).Node;
        void Click(string name)
        {
            var node = Find(name);
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        var previewNames = new[] { "배치 카운터 : 0", "Column 두 번째", "Wrap 6", "셀 6", "오버레이 이동",
            "Align 위치", "Center 가운데", "Padding + ConstrainedBox", "Container 여백" };
        check(layout.Samples.Count == 10 && sources.Length == 10, "each of ten practical container previews owns its source block");
        ExportSources(output, "Containers", sources);
        for (var index = 0; index < previewNames.Length; index++)
        {
            var preview = Find(previewNames[index]);
            check(sources[index].Bounds.Y >= preview.Bounds.Y + preview.Bounds.Height
                && sources[index].Bounds.Y - preview.Bounds.Y - preview.Bounds.Height <= 64,
                "container source immediately follows its actual preview : " + previewNames[index]);
        }
        check(sources.All(source => !source.Value!.Contains("class LayoutPage", StringComparison.Ordinal)
            && !source.Value.Contains("using Duxel.Core;", StringComparison.Ordinal)),
            "container source contains the relevant factory rather than Gallery implementation scaffolding");
        Click("배치 카운터 : 0");
        check(layout.Samples[0].Source.Contains("var count = 1;", StringComparison.Ordinal),
            "actual container counter interaction updates its source state without replacing the control");
        Click("열 수 : 3");
        check(layout.Samples[3].Source.Contains("new UiUniformGridOptions(2,", StringComparison.Ordinal)
            && Find("셀 6").Bounds.Y > Find("셀 1").Bounds.Y,
            "grid column-count pointer changes update both layout and its corresponding code");
        Click("오버레이 이동");
        check(layout.Samples[4].Source.Contains("var right = 180f;", StringComparison.Ordinal),
            "positioned preview emits its actual changed right edge in the source");
        Click("정렬 전환");
        check(layout.Samples[5].Source.Contains("new UiAlignment(1f, 1f)", StringComparison.Ordinal),
            "alignment changes appear in the matching container source");
        var scrollSample = (GallerySample<UiScrollView>)layout.Samples[9];
        var scrollFrame = tree.CaptureSnapshot().Elements;
        var viewport = scrollFrame.Single(element => element.Node.Name == "Scroll view"
            && element.Node.Bounds.Width == 320 && element.Node.Bounds.Height == 140).Node.Bounds;
        var scrollSource = scrollFrame.Single(element => element.Node.Name == "C# 예제 소스"
            && element.Node.Value == scrollSample.Source).Node;
        var sourceGap = scrollSource.Bounds.Y - viewport.Y - viewport.Height;
        check(sourceGap is >= 0 and <= 16
            && scrollSample.Source.Contains("UiScrollAxis.Vertical", StringComparison.Ordinal),
            $"the ScrollView source follows its viewport and starts with vertical scrolling (gap : {sourceGap})");
        Click("스크롤 방향 : 세로");
        check(scrollSample.Preview.Options.Axis == UiScrollAxis.Both
            && scrollSample.Source.Contains("UiScrollAxis.Both", StringComparison.Ordinal),
            "the ScrollView direction control updates the real preview and the matching source");
        var point = new UiVector2(viewport.X + viewport.Width / 2, viewport.Y + viewport.Height / 2);
        var scrollParses = scrollSample.Metrics.SyntaxParses;
        Frame(idle with { MousePosition = point, MouseWheel = -1, MouseWheelHorizontal = -1 });
        Frame();
        check(scrollSample.Preview.Offset.X > 0 && scrollSample.Preview.Offset.Y > 0
            && scrollSample.Metrics.SyntaxParses == scrollParses,
            "the Gallery preview consumes both wheel axes without parsing source again");
        Click("스크롤 방향 : 가로·세로");
        check(scrollSample.Preview.Offset.X == 0 && scrollSample.Source.Contains("UiScrollAxis.Vertical", StringComparison.Ordinal),
            "returning to vertical removes the horizontal offset and restores the displayed option");
        var parses = layout.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(layout.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "unchanged container settings retain all parsed source blocks");
    }
    private static void RunNumericAndToolbar(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var numeric = new SpinButtonPage();
        using var numericFrame = new GalleryPageFrame(numeric);
        using var toolbar = new ToolbarPage();
        using var toolbarFrame = new GalleryPageFrame(toolbar);
        var current = numericFrame;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 12000), new(1, 1)), input ?? idle, new(0, 0, 1280, 12000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); current.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        UiAccessibilityNode[] Sources() => tree.CaptureSnapshot().Elements
            .Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Select(element => element.Node).ToArray();
        void Click(string name)
        {
            var node = Find(name, UiAccessibilityRole.Button).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        check(numeric.Samples.Count == 2 && Sources().Length == 2,
            "quantity and custom-unit SpinButton previews have independent code blocks");
        for (var index = 0; index < 2; index++)
        {
            var input = Find(index == 0 ? "수량" : "무게 (kg)", UiAccessibilityRole.SpinButton).Node;
            var source = Sources()[index];
            check(source.Bounds.Y >= input.Bounds.Y + input.Bounds.Height && source.Bounds.Y - input.Bounds.Y - input.Bounds.Height <= 24,
                "SpinButton source follows its actual editor with normal spacing : " + index);
        }
        Click("수량 늘리기");
        check(numeric.Samples[0].Source.Contains("new UiState<double?>(4d)", StringComparison.Ordinal),
            "quantity stepper input updates the current numeric initializer below it");
        Click("읽기 전용 전환"); Click("크기 전환"); Click("모양 전환");
        check(numeric.Samples[0].Source.Contains("ReadOnly : true", StringComparison.Ordinal)
            && numeric.Samples[0].Source.Contains("Size : UiSpinButtonSize.Small", StringComparison.Ordinal)
            && numeric.Samples[0].Source.Contains("Appearance : UiInputAppearance.Underline", StringComparison.Ordinal),
            "SpinButton code tracks its actual read-only, size and appearance settings");
        var mass = Find("무게 (kg)", UiAccessibilityRole.SpinButton);
        check(tree.RequestAction(mass.Node.Id, mass.RuntimeId, UiAccessibilityAction.SetValue, "2.5 kg", wait : false) == UiAccessibilityResult.Success,
            "custom-unit Gallery SpinButton accepts a native edit request");
        Frame(); Frame();
        check(numeric.Samples[1].Source.Contains("new UiState<double?>(2.5d)", StringComparison.Ordinal)
            && numeric.Samples[1].Source.Contains("using System.Globalization;", StringComparison.Ordinal)
            && numeric.Samples[1].Source.Contains("input.Parser =", StringComparison.Ordinal),
            "unit parsing keeps the current value and complete formatter/parser in its own source");
        Click("수량 비우기");
        check(numeric.Samples[0].Source.Contains("new UiState<double?>(null)", StringComparison.Ordinal),
            "clearing the quantity is represented as a nullable initializer rather than zero");
        current = toolbarFrame;
        Frame(); Frame();
        var bar = Find("문서 도구", UiAccessibilityRole.Toolbar).Node;
        check(toolbar.Samples.Count == 1 && Sources().Length == 1 && Sources()[0].Bounds.Y >= bar.Bounds.Y + bar.Bounds.Height
            && Sources()[0].Bounds.Y - bar.Bounds.Y - bar.Bounds.Height <= 24,
            "Toolbar places its compact factory immediately below the actual toolbar");
        Click("도구 모음 방향"); Click("도구 모음 크기"); Click("도구 모음 RTL");
        Click("새 문서"); Click("굵게");
        var code = toolbar.Samples[0].Source;
        check(code.Contains("Fluent.Toolbar(", StringComparison.Ordinal) && code.Contains("Vertical : true", StringComparison.Ordinal)
            && code.Contains("UiToolbarSize.Small", StringComparison.Ordinal) && code.Contains("RightToLeft : true", StringComparison.Ordinal)
            && code.Contains("new UiState<bool>(true)", StringComparison.Ordinal) && code.Contains("var _count = 1;", StringComparison.Ordinal),
            "Toolbar factory source reflects orientation, size, direction, child toggle and action count");
        var toolbarParses = toolbar.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(toolbar.Samples[0].Metrics.SyntaxParses == toolbarParses, "unchanged Toolbar settings reuse parsed source");
        current = numericFrame;
        Frame(); Frame();
        check(numeric.Samples[0].Source.Contains("ReadOnly : true", StringComparison.Ordinal)
            && numeric.Samples[1].Source.Contains("new UiState<double?>(2.5d)", StringComparison.Ordinal),
            "numeric sample models and current code survive switching to another page and back");
        var numericParses = numeric.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(numericParses.SequenceEqual(numeric.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "unchanged numeric settings retain both parsed code blocks");
    }
    private static void RunAvatarGroup(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new AvatarGroupPage(ObserverServices.CreateWindows());
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 4000), new(1, 1)), input ?? idle, new(0, 0, 1280, 4000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityNode Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role).Node;
        void Click(string name, UiAccessibilityRole role = UiAccessibilityRole.Button)
        {
            var node = Find(name, role);
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        var preview = page.Preview;
        var group = Find("문서 공동 작업자", UiAccessibilityRole.AvatarGroup);
        var source = Find("C# 예제 소스", UiAccessibilityRole.Edit);
        check(page.Samples.Count == 1 && source.Bounds.Y >= group.Bounds.Y + group.Bounds.Height
            && source.Bounds.Y - group.Bounds.Y - group.Bounds.Height <= 24,
            "AvatarGroup source immediately follows the real group instead of a page-sized footer");
        var code = page.Samples[0].Source;
        check(code.Contains("Fluent.AvatarGroup(", StringComparison.Ordinal)
            && code.Contains("using Duxel.Svg;", StringComparison.Ordinal)
            && code.Contains("raster.CopyImageData()", StringComparison.Ordinal)
            && code.Contains("members.ToDictionary", StringComparison.Ordinal)
            && !code.Contains("class AvatarGroupPage", StringComparison.Ordinal)
            && !code.Contains("_services", StringComparison.Ordinal),
            "AvatarGroup compact code includes its members, resource setup and selection callback without Gallery scaffolding");
        foreach (var layout in new[] { UiAvatarGroupLayout.Stack, UiAvatarGroupLayout.Pie, UiAvatarGroupLayout.Spread })
        {
            Click("그룹 배치 변경");
            check(preview!.Options.Layout == layout
                && page.Samples[0].Source.Contains("Layout : UiAvatarGroupLayout." + layout, StringComparison.Ordinal),
                "AvatarGroup pointer layout changes appear in the matching source : " + layout);
        }
        Click("그룹 크기 변경"); Click("그룹 표시 수 변경"); Click("그룹 방향 전환");
        code = page.Samples[0].Source;
        check(ReferenceEquals(preview, page.Preview) && code.Contains("Size : UiAvatarSize.Size64", StringComparison.Ordinal)
            && code.Contains("MaximumInlineItems : 2", StringComparison.Ordinal)
            && code.Contains("RightToLeft : true", StringComparison.Ordinal),
            "AvatarGroup size, overflow count and direction synchronize without replacing the persistent widget");
        Click("숨긴 구성원 (5)");
        Click("Ada Lovelace", UiAccessibilityRole.MenuItem);
        check(page.Samples[0].Source.Contains("var selection = \"Ada Lovelace\";", StringComparison.Ordinal)
            && !preview!.IsOverflowOpen,
            "actual overflow member selection updates the source initializer and closes its popup");
        var parses = page.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(page.Samples[0].Metrics.SyntaxParses == parses,
            "idle AvatarGroup source reuses its syntax parse");
    }
    private static void RunPersona(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new PersonaPage(ObserverServices.CreateWindows());
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 4000), new(1, 1)), input ?? idle, new(0, 0, 1280, 4000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Click(string name)
        {
            var node = Find(name, UiAccessibilityRole.Button).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        var preview = page.Preview;
        var group = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Group
            && element.Node.Name.StartsWith("디모이,", StringComparison.Ordinal)).Node;
        var code = Find("C# 예제 소스", UiAccessibilityRole.Edit).Node;
        check(page.Samples.Count == 1 && code.Bounds.Y >= group.Bounds.Y + group.Bounds.Height
            && code.Bounds.Y - group.Bounds.Y - group.Bounds.Height <= 24,
            "Persona source follows its actual text/media preview with normal spacing");
        check(code.Value!.Contains("Fluent.Persona(", StringComparison.Ordinal)
            && !code.Value.Contains("class PersonaPage", StringComparison.Ordinal),
            "Persona code shows its factory instead of Gallery implementation scaffolding");
        var name = Find("인물 이름", UiAccessibilityRole.Edit);
        check(tree.RequestAction(name.Node.Id, name.RuntimeId, UiAccessibilityAction.SetValue, "디모이 \"D\"", wait : false)
            == UiAccessibilityResult.Success, "Persona name accepts native input");
        Frame(); Frame();
        check(page.Samples[0].Source.Contains("디모이 \\\"D\\\"", StringComparison.Ordinal),
            "Persona current name is encoded as a valid C# string literal");
        Click("인물 크기"); Click("인물 텍스트 위치"); Click("인물 정렬"); Click("인물 방향");
        Click("인물 정보 줄 수"); Click("인물 상태 전용"); Click("인물 미디어 표시"); Click("인물 비활성 표현");
        var source = page.Samples[0].Source;
        check(ReferenceEquals(preview, page.Preview) && source.Contains("UiPersonaSize.ExtraSmall", StringComparison.Ordinal)
            && source.Contains("UiPersonaTextPosition.Before", StringComparison.Ordinal)
            && source.Contains("UiPersonaTextAlignment.Center", StringComparison.Ordinal)
            && source.Contains("PresenceOnly : true", StringComparison.Ordinal)
            && source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && source.Contains("ShowMedia : false", StringComparison.Ordinal)
            && source.Contains("IsInactive : true", StringComparison.Ordinal)
            && source.Contains("SecondaryText : null", StringComparison.Ordinal),
            "Persona pointer controls synchronize all six options, one-line content and inactive avatar without replacing it");
        Click("인물 SVG 이미지");
        check(page.Samples[0].Source.Contains("raster.CopyImageData()", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("((UiFluentPersona)example).Avatar.Source", StringComparison.Ordinal),
            "Persona image mode includes equivalent one-time SVG resource loading and assignment");
        Click("인물 이니셜");
        check(!page.Samples[0].Source.Contains("raster.CopyImageData()", StringComparison.Ordinal)
            && preview!.Avatar.Source is null,
            "returning to Persona initials removes obsolete image setup from its code");
        var parses = page.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(page.Samples[0].Metrics.SyntaxParses == parses, "unchanged Persona state reuses its parsed code");
    }
    private static void RunAccordion(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var accordion = new AccordionPage();
        using var page = new WidgetPage("접기·펼치기", "본문 상태와 소스", accordion);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 4000), new(1, 1)), input ?? idle, new(0, 0, 1280, 4000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Click(string name)
        {
            var node = Find(name, UiAccessibilityRole.Button).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        var preview = Find("접기·펼치기 예제", UiAccessibilityRole.Accordion).Node;
        var source = Find("C# 예제 소스", UiAccessibilityRole.Edit).Node;
        check(accordion.Samples.Count == 1 && source.Bounds.Y >= preview.Bounds.Y + preview.Bounds.Height
            && source.Bounds.Y - preview.Bounds.Y - preview.Bounds.Height <= 24,
            "Accordion source immediately follows its collapsed preview");
        Click("입력 내용 보존");
        var memo = Find("접기 예제 메모", UiAccessibilityRole.Edit);
        check(tree.RequestAction(memo.Node.Id, memo.RuntimeId, UiAccessibilityAction.SetValue, "새 메모 \"D\"", wait : false)
            == UiAccessibilityResult.Success, "Accordion body accepts a native memo edit");
        Frame(); Frame();
        check(accordion.Samples[0].Source.Contains("ImmutableArray.Create<string>(\"입력\")", StringComparison.Ordinal)
            && accordion.Samples[0].Source.Contains("새 메모 \\\"D\\\"", StringComparison.Ordinal),
            "Accordion source follows opened panels and escaped memo input");
        Click("여러 항목 열기 : 꺼짐"); Click("본문 버튼과 키보드 이동"); Click("본문 클릭 : 0");
        Click("모두 접기 : 허용"); Click("화살표 : 앞"); Click("헤더 크기 : Medium");
        var code = accordion.Samples[0].Source;
        check(code.Contains("Fluent.Accordion(", StringComparison.Ordinal)
            && code.Contains("Multiple : true", StringComparison.Ordinal)
            && code.Contains("Collapsible : false", StringComparison.Ordinal)
            && code.Contains("UiAccordionChevronPosition.End", StringComparison.Ordinal)
            && code.Contains("UiAccordionSize.Small", StringComparison.Ordinal),
            "Accordion current options appear in its captured factory source");
        check(code.Contains("var clicks = 1;", StringComparison.Ordinal),
            "Accordion nested button count appears in its source initializer");
        check(code.Contains("ImmutableArray.Create<string>(\"버튼\", \"입력\")", StringComparison.Ordinal),
            "Accordion source preserves the control's ordinal ordering of open panels");
        var parses = accordion.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(accordion.Samples[0].Metrics.SyntaxParses == parses, "idle Accordion source retains its syntax parse");
    }
    private static void RunLists(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new ListPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)), input ?? idle, new(0, 0, 1280, 6000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Click(string name, UiAccessibilityRole role = UiAccessibilityRole.Button)
        {
            var node = Find(name, role).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        var previews = new[] { ("일반 안내", UiAccessibilityRole.List), ("방문 도시", UiAccessibilityRole.ListBox),
            ("함께 작업하는 팀", UiAccessibilityRole.Grid) };
        check(page.Samples.Count == 3 && sources.Length == 3, "plain, single-select and composite List each own a separate source block");
        for (var index = 0; index < previews.Length; index++)
        {
            var preview = Find(previews[index].Item1, previews[index].Item2).Node;
            check(sources[index].Bounds.Y >= preview.Bounds.Y + preview.Bounds.Height
                && sources[index].Bounds.Y - preview.Bounds.Y - preview.Bounds.Height <= 24,
                "List source follows its own actual preview : " + previews[index].Item1);
        }
        check(page.Samples.All(sample => sample.Source.Contains("Fluent.List(", StringComparison.Ordinal)
            && !sample.Source.Contains("class ListPage", StringComparison.Ordinal)),
            "List sources are captured factories without Gallery page scaffolding");
        Click("목록 안 독립 버튼");
        check(page.Samples[0].Source.Contains("new UiState<int>(1)", StringComparison.Ordinal),
            "plain List child action updates its source counter initializer");
        Click("부산 선택", UiAccessibilityRole.Checkbox);
        check(page.Samples[1].Source.Contains("new UiState<string?>(\"busan\")", StringComparison.Ordinal),
            "single List selection updates its own source binding initializer");
        Click("김민수 선택", UiAccessibilityRole.Checkbox);
        check(page.Samples[2].Source.Split('\n')[0].Contains("\"minsu\"", StringComparison.Ordinal)
            && Find("김민수 선택", UiAccessibilityRole.Checkbox).Node.ToggleState == UiAccessibilityToggleState.On,
            "composite List checkmark selection appears in its array initializer");
        var memo = Find("디모이 메모", UiAccessibilityRole.Edit);
        check(tree.RequestAction(memo.Node.Id, memo.RuntimeId, UiAccessibilityAction.SetValue, "새 메모 \"D\"", wait : false)
            == UiAccessibilityResult.Success, "composite List accepts a native child edit");
        Frame(); Frame();
        Click("행 실행 시 선택 유지", UiAccessibilityRole.Checkbox);
        Click("디모이에게 실행");
        var code = page.Samples[2].Source;
        check(code.Contains("새 메모 \\\"D\\\"", StringComparison.Ordinal)
            && code.Contains("new UiState<bool>(true)", StringComparison.Ordinal)
            && code.Contains("new UiState<int>(2)", StringComparison.Ordinal)
            && code.Contains("action.PreventSelection = keep.Value", StringComparison.Ordinal),
            "composite List source follows memo, nested action and selection-retention state with its full handler");
        var parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "all three stable List sources retain their syntax parses");
    }
    private static void RunDropdownSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new DropdownPage(ObserverServices.CreateWindows());
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 16000), new(1, 1)), input ?? idle, new(0, 0, 1280, 16000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Click(string name, UiAccessibilityRole role = UiAccessibilityRole.Button)
        {
            var node = Find(name, role).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        void Escape()
        {
            Frame(idle with { KeyEvents = [new(UiKey.Escape, true, default)] });
            Frame(idle with { KeyEvents = [new(UiKey.Escape, false, default)] });
            Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        check(page.Samples.Count == 7 && sources.Length == 7,
            "Dropdown, Combobox and all four Tag examples each own a source block");
        var inputs = new[] { "작업 위치", "확인할 기능", "도시 검색" };
        for (var index = 0; index < inputs.Length; index++)
        {
            var preview = Find(inputs[index], UiAccessibilityRole.ComboBox).Node;
            check(sources[index].Bounds.Y >= preview.Bounds.Y + preview.Bounds.Height
                && sources[index].Bounds.Y - preview.Bounds.Y - preview.Bounds.Height <= 72,
                "field label/hint and source immediately follow their actual input : " + inputs[index]);
        }
        check(page.Samples.All(sample => !sample.Source.Contains("class DropdownPage", StringComparison.Ordinal)
            && !sample.Source.Contains("using Duxel.Core;", StringComparison.Ordinal)),
            "all seven Dropdown sources omit Gallery scaffolding and the repeated Core import");
        check(page.Samples[1].Source.Contains("var featureItems = new UiSelectOption[]", StringComparison.Ordinal)
            && page.Samples[3].Source.Contains("var featureItems = new UiSelectOption[]", StringComparison.Ordinal)
            && page.Samples[4].Source.Contains("var featureItems = new UiSelectOption[]", StringComparison.Ordinal),
            "shared option declarations are captured from one actual value factory and reused by all dependent examples");
        Click("작업 위치", UiAccessibilityRole.ComboBox); Click("한국", UiAccessibilityRole.Option);
        check(page.Samples[0].Source.Contains("string? _country = \"kr\";", StringComparison.Ordinal),
            "Dropdown pointer selection updates its captured binding initializer");
        Click("확인할 기능", UiAccessibilityRole.ComboBox);
        Click("이미지", UiAccessibilityRole.Option); Click("SVG 아이콘", UiAccessibilityRole.Option); Escape();
        check(new[] { 1, 3, 4 }.All(index => page.Samples[index].Source
            .Contains("string[] _features = [\"image\", \"svg\"];", StringComparison.Ordinal)),
            "multiple Dropdown and both feature Tag groups show the same current canonical selection");
        Click("선택 표시 크기"); Click("선택 외관 변경"); Click("선택 방향 전환"); Click("선택 폭 변경"); Click("검증 상태 전환");
        check(new[] { 0, 1, 2 }.All(index => page.Samples[index].Source.Contains("Width : 140f", StringComparison.Ordinal)
            && page.Samples[index].Source.Contains("UiDropdownSize.Small", StringComparison.Ordinal)
            && page.Samples[index].Source.Contains("UiInputAppearance.Underline", StringComparison.Ordinal)
            && page.Samples[index].Source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && page.Samples[index].Source.Contains("Invalid : true", StringComparison.Ordinal)),
            "all three input examples synchronize current size, appearance, width, direction and invalid state");
        var editor = Find("도시 검색", UiAccessibilityRole.ComboBox);
        check(tree.RequestAction(editor.Node.Id, editor.RuntimeId, UiAccessibilityAction.SetValue, "서울 \"D\"", wait : false)
            == UiAccessibilityResult.Success, "Gallery Combobox accepts native query input");
        Frame(); Frame(); Escape();
        check(page.Samples[2].Source.Contains("서울 \\\"D\\\"", StringComparison.Ordinal)
            && page.Samples[2].Source.Contains("editor.OpenChanged = _ => OpenChanges++", StringComparison.Ordinal),
            "Combobox source includes current query and its real controlled-open transition callback");
        Click("태그 크기 전환"); Click("SVG 아이콘");
        check(page.Samples[3].Source.Contains("UiTagSize.Small", StringComparison.Ordinal)
            && page.Samples[3].Source.Contains("string? ActivatedTag = \"svg\";", StringComparison.Ordinal)
            && page.Samples[3].Source.Contains("string[] _emphasizedFeatures = [\"svg\"];", StringComparison.Ordinal),
            "interaction Tag source follows size, primary activation and emphasized selection");
        Click("강조 선택"); Click("태그 변경 허용 전환"); Click("강조 선택");
        check(page.Samples[5].Source.Contains("var _tagSelected = true;", StringComparison.Ordinal)
            && page.Samples[5].Source.Contains("var _allowTagSelection = false;", StringComparison.Ordinal),
            "controlled InteractionTag rejection is represented by its current accepted value and guard");
        Click("담당자 태그 크기"); Click("담당자 태그 방향·모양"); Click("담당자 비활성 표현");
        check(page.Samples[6].Source.Contains("UiTagSize.Small", StringComparison.Ordinal)
            && page.Samples[6].Source.Contains("UiTagShape.Rounded", StringComparison.Ordinal)
            && page.Samples[6].Source.Contains("IsInactive : true", StringComparison.Ordinal)
            && page.Samples[6].Source.Contains("raster.CopyImageData()", StringComparison.Ordinal)
            && page.Samples[6].Source.Contains("UiTagMedia.Avatar", StringComparison.Ordinal),
            "member Tag source includes current options and complete borrowed-avatar SVG resources");
        var parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "all seven stable Dropdown-page sources retain their syntax parses");
    }
    private static void RunPopoverSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new PopoverPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)), input ?? idle, new(0, 0, 1280, 6000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Click(string name)
        {
            var node = Find(name, UiAccessibilityRole.Button).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        check(page.Samples.Count == 2 && sources.Length == 2, "Fluent and custom-surface Popover previews each own a source block");
        var triggers = new[] { "프로필 편집 열기", "넓은 장식 팝업 열기" };
        for (var index = 0; index < triggers.Length; index++)
        {
            var trigger = Find(triggers[index], UiAccessibilityRole.Button).Node;
            check(sources[index].Bounds.Y >= trigger.Bounds.Y + trigger.Bounds.Height
                && sources[index].Bounds.Y - trigger.Bounds.Y - trigger.Bounds.Height <= 24,
                "Popover source follows its own real trigger : " + triggers[index]);
        }
        var declaration = GalleryPageSource.Declaration(nameof(GalleryPopoverDecoration));
        check(page.Samples[1].Source.EndsWith(declaration, StringComparison.Ordinal)
            && declaration.StartsWith("internal readonly struct GalleryPopoverDecoration", StringComparison.Ordinal)
            && !page.Samples[1].Source.Contains("namespace Duxel.Gallery;", StringComparison.Ordinal)
            && !page.Samples[1].Source.Contains("using Duxel.Core;", StringComparison.Ordinal),
            "custom Popover source includes its actual compiled presentation declaration without namespace or page scaffolding");
        Click("팝업 모션"); Click("프로필 편집 열기");
        var name = Find("이름", UiAccessibilityRole.Edit);
        check(tree.RequestAction(name.Node.Id, name.RuntimeId, UiAccessibilityAction.SetValue, "디모이 \"P\"", wait : false)
            == UiAccessibilityResult.Success, "Gallery Popover accepts native content input");
        Frame(); Frame(); Click("팝업 명령 실행");
        check(page.Samples[0].Source.Contains("디모이 \\\"P\\\"", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("var _count = 1;", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("var _open = true;", StringComparison.Ordinal),
            "Fluent Popover source follows edited text, inner command count and controlled open state");
        Click("프로필 편집 닫기");
        check(page.Samples[0].Source.Contains("var _open = false;", StringComparison.Ordinal),
            "closing the Fluent Popover updates its source without discarding content state");
        Click("넓은 장식 팝업 열기");
        context.State.AdvanceTime(.5f); Frame();
        var custom = page.Samples[1].Source;
        check(page.DecorationPreview!.IsOpen && custom.IndexOf(").Show();", StringComparison.Ordinal) >= 0
            && custom.IndexOf(").Show();", StringComparison.Ordinal) < custom.IndexOf("internal readonly struct", StringComparison.Ordinal),
            "open custom Popover source places its Show statement before the compiled type declaration");
        Click("장식 팝업 닫기");
        context.State.AdvanceTime(.5f); Frame();
        check(!page.Samples[1].Source.Contains(").Show();", StringComparison.Ordinal), "custom Popover close removes its source Show request");
        Click("팝업 포커스 제한"); Click("포커스 이탈 시 닫기"); Click("팝업 화살표"); Click("팝업 크기"); Click("팝업 외관");
        var code = page.Samples[0].Source;
        check(code.Contains("TrapFocus : true", StringComparison.Ordinal) && code.Contains("CloseOnFocusLeave = true", StringComparison.Ordinal)
            && code.Contains("WithArrow : true", StringComparison.Ordinal) && code.Contains("UiPopoverSize.Small", StringComparison.Ordinal)
            && code.Contains("UiPopoverAppearance.Inverted", StringComparison.Ordinal) && code.Contains("MotionEnabled : false", StringComparison.Ordinal),
            "all Fluent Popover appearance, motion and focus switches update the corresponding source options");
        foreach (var mode in new[] { UiPopoverTriggerMode.Hover, UiPopoverTriggerMode.Context, UiPopoverTriggerMode.Click })
        {
            Click("팝업 열기 방식");
            check(page.Preview!.Options.TriggerMode == mode && page.Samples[0].Source.Contains("TriggerMode : UiPopoverTriggerMode." + mode, StringComparison.Ordinal),
                "Popover source follows actual trigger-mode cycling : " + mode);
        }
        page.Preview!.Options = page.Preview.Options with
        {
            Positioning = new()
            {
                Position = UiPopupPosition.BelowEnd, Gap = 6,
                OverflowBoundaryPadding = new(1, 2, 3, 4),
                FallbackPositions = [UiPopupPosition.AboveEnd]
            }
        };
        Frame(); Frame();
        check(page.Samples[0].Source.Contains("Position = UiPopupPosition.BelowEnd", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("Gap = 6f", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("new UiPopupBoundaryPadding(1f, 2f, 3f, 4f)", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("ImmutableArray.Create(UiPopupPosition.AboveEnd)", StringComparison.Ordinal),
            "nondefault logical positioning is retained in the equivalent Popover source");
        Click("팝업 닫기 거부"); Click("프로필 편집 열기"); Click("프로필 편집 닫기");
        check(page.Preview.IsOpen && page.Samples[0].Source.Contains("var _rejectClose = true;", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("var _open = true;", StringComparison.Ordinal),
            "Popover rejected close preserves actual open state and represents its authoritative guard in code");
        var parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "stable Popover state reuses both parsed snippets and its captured declaration");
    }
    private static void RunDrawerSources(Action<bool, string> check, string? output)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new DrawerPage();
        page.Overlay.Options = page.Overlay.Options with { MotionEnabled = false };
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)), input ?? idle, new(0, 0, 1280, 6000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name);
        void Click(string name)
        {
            var node = Find(name).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); context.State.AdvanceTime(.5f); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        var trigger = Find("보조 패널 열기").Node;
        check(page.Samples.Count == 2 && sources.Length == 2 && sources[0].Bounds.Y >= trigger.Bounds.Y + trigger.Bounds.Height
            && sources[0].Bounds.Y - trigger.Bounds.Y - trigger.Bounds.Height <= 24,
            "overlay Drawer trigger and inline Drawer each own a source block");
        ExportSources(output, "Drawer", sources);
        var inlineBounds = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "인라인 패널"
            && element.Node.Role == UiAccessibilityRole.Group).Node.Bounds;
        check(sources[1].Bounds.Y >= inlineBounds.Y + inlineBounds.Height
            && sources[1].Bounds.Y - inlineBounds.Y - inlineBounds.Height <= 24,
            "inline Drawer source immediately follows its settled panel allocation");
        check(page.Samples[0].Source.Contains("Fluent.Drawer(", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("var renderOverlays = (UiImmediateContext ui) => _overlay.Render(ui);", StringComparison.Ordinal),
            "overlay Drawer code includes the actual captured factory and its required after-body renderer");
        Click("패널 위치 변경"); Click("패널 크기 변경"); Click("패널 모달"); Click("패널 오른쪽에서 왼쪽");
        check(page.Samples[0].Source.Contains("UiDrawerPosition.Bottom", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("UiDrawerSize.Medium", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("Modal : false", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("RightToLeft : true", StringComparison.Ordinal),
            "Drawer position, extent preset, modal and direction switches synchronize their source options");
        Click("보조 패널 열기");
        var memo = Find("패널 메모");
        check(tree.RequestAction(memo.Node.Id, memo.RuntimeId, UiAccessibilityAction.SetValue, "보존 \"D\"", wait : false)
            == UiAccessibilityResult.Success, "overlay Drawer accepts native memo editing");
        Frame(); Frame();
        check(page.Samples.All(sample => sample.Source.Contains("보존 \\\"D\\\"", StringComparison.Ordinal))
            && page.Samples[0].Source.Contains("\n_overlay.Show();", StringComparison.Ordinal),
            "shared Drawer memo appears in both independent snippets and overlay open state in its own snippet");
        Click("패널 저장");
        check(page.Saves == 1 && !page.Overlay.IsOpen && page.Samples[0].Source.Contains("var _saves = 1;", StringComparison.Ordinal)
            && !page.Samples[0].Source.Contains("\n_overlay.Show();", StringComparison.Ordinal),
            "Drawer save retains memo/count and removes its source open request");
        Click("인라인 닫기"); context.State.AdvanceTime(.5f); Frame();
        check(page.Samples[1].Source.Contains("new UiState<bool>(false)", StringComparison.Ordinal),
            "inline Drawer close updates the source's authoritative visibility binding");
        var parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)), "idle Drawer sources reuse parsed code");
    }
    private static void RunSkeletonSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var skeleton = new SkeletonPage();
        using var page = new WidgetPage("스켈레톤", "두 크기의 로딩과 대응 소스", skeleton);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)), input ?? idle, new(0, 0, 1280, 6000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        void Click(string name)
        {
            var node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        var previews = tree.CaptureSnapshot().Elements.Where(element => element.Node.Role == UiAccessibilityRole.ProgressBar
            && element.Node.Name == "프로필 불러오는 중").Select(element => element.Node).ToArray();
        check(skeleton.Samples.Count == 2 && sources.Length == 2 && previews.Length == 2,
            "both Skeleton sizes own independent source blocks");
        for (var index = 0; index < previews.Length; index++)
            check(sources[index].Bounds.Y >= previews[index].Bounds.Y + previews[index].Bounds.Height
                && sources[index].Bounds.Y - previews[index].Bounds.Y - previews[index].Bounds.Height <= 24,
                "Skeleton source follows its actual size preview : " + index);
        check(skeleton.Samples[0].Source.Contains("preview(48)", StringComparison.Ordinal)
            && skeleton.Samples[1].Source.Contains("preview(96)", StringComparison.Ordinal)
            && skeleton.Samples.All(sample => sample.Source.Contains("Fluent.Skeleton(", StringComparison.Ordinal)),
            "Skeleton code captures the real shared factory and each concrete size");
        Click("펄스"); Click("반투명"); Click("모션 줄이기"); Click("애니메이션 정지");
        check(skeleton.Samples.All(sample => sample.Source.Contains("var pulse = true;", StringComparison.Ordinal)
            && sample.Source.Contains("var translucent = true;", StringComparison.Ordinal)
            && sample.Source.Contains("var reduced = true;", StringComparison.Ordinal)
            && sample.Source.Contains("var paused = true;", StringComparison.Ordinal)),
            "Skeleton sources reflect all actual watched motion and appearance switches");
        Click("로딩 중");
        check(skeleton.Samples.All(sample => sample.Source.Contains("var loading = false;", StringComparison.Ordinal))
            && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.ProgressBar),
            "loaded Skeleton examples show actual content and matching conditional source");
        var parses = skeleton.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(skeleton.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "stable Skeleton samples do not rebuild their parsed source");
    }
    private static void RunDialogSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new DialogPage();
        using var frame = new GalleryPageFrame(page);
        page.Preview!.Options = page.Preview.Options with { MotionEnabled = false };
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)), input ?? idle, new(0, 0, 1280, 6000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Click(string name)
        {
            var node = Find(name, UiAccessibilityRole.Button).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        var trigger = Find("경고 열기", UiAccessibilityRole.Button).Node;
        var progress = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.ProgressBar).Node;
        check(page.Samples.Count == 2 && sources.Length == 2, "Dialog and motion-curve examples own independent source blocks");
        check(sources[0].Bounds.Y >= trigger.Bounds.Y + trigger.Bounds.Height && sources[0].Bounds.Y - trigger.Bounds.Y - trigger.Bounds.Height <= 24,
            "Dialog source immediately follows its actual mode-open controls");
        check(sources[1].Bounds.Y >= progress.Bounds.Y + progress.Bounds.Height && sources[1].Bounds.Y - progress.Bounds.Y - progress.Bounds.Height <= 24,
            "motion source immediately follows its actual curve controls and progress preview");
        Click("모달 열기"); Click("저장");
        check(page.Preview.IsOpen && page.Samples[0].Source.Contains("이름을 입력해야 저장할 수 있습니다.", StringComparison.Ordinal),
            "Dialog validation stays open and updates the displayed message initializer");
        var input = Find("이름", UiAccessibilityRole.Edit);
        check(tree.RequestAction(input.Node.Id, input.RuntimeId, UiAccessibilityAction.SetValue, "디모이 \"D\"", wait : false)
            == UiAccessibilityResult.Success, "Dialog accepts native name input");
        Frame(); Frame(); Click("저장");
        check(!page.Preview.IsOpen && page.Samples[0].Source.Contains("디모이 \\\"D\\\"", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("저장됨 :", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("new UiState<bool>(false)", StringComparison.Ordinal),
            "Dialog save preserves the edited name and closed authoritative state in its source");
        check(Find("모달 열기", UiAccessibilityRole.Button).Node.Focused, "Dialog save restores focus to its real opener");
        Click("비모달 열기");
        check(Find("이름", UiAccessibilityRole.Edit).Node.Value == "디모이 \"D\""
            && page.Samples[0].Source.Contains("UiDialogType.NonModal", StringComparison.Ordinal),
            "reopening nonmodal Dialog retains input and emits the current dialog type");
        Click("취소"); Click("경고 열기");
        Frame(idle with { MousePosition = new(1200, 5800), LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = new(1200, 5800), LeftMouseReleased = true });
        Frame();
        check(page.Preview.IsOpen && page.Samples[0].Source.Contains("UiDialogType.Alert", StringComparison.Ordinal),
            "Alert Dialog retains its actual outside-click policy and corresponding source type");
        Click("취소"); Click("등장 곡선");
        check(context.State.NextScheduledFrameTimeSeconds is not null && page.MotionValue == 0
            && page.Samples[1].Source.Contains("var _target = 1f;", StringComparison.Ordinal)
            && page.Samples[1].Source.Contains("var prepareMotion =", StringComparison.Ordinal),
            "curve action starts continuously at its current value and emits the finite transition target and preparation hook");
        var parses = page.Samples[1].Metrics.SyntaxParses;
        var value = page.MotionValue;
        context.State.AdvanceTime(.05f); Frame();
        check(page.MotionValue > value && page.Samples[1].Metrics.SyntaxParses == parses,
            "animated value changes do not reparse unchanged motion configuration source");
        context.State.AdvanceTime(.3f); Frame(); Frame();
        check(page.MotionValue == 1 && context.State.NextScheduledFrameTimeSeconds is null,
            "motion preview settles exactly and stops scheduling frames with inline code present");
        Click("퇴장 곡선");
        check(page.Samples[1].Source.Contains("new UiCubicBezier(0.8d, 0d, 0.78d, 1d)", StringComparison.Ordinal)
            && page.Samples[1].Source.Contains("var _target = 0f;", StringComparison.Ordinal),
            "retargeted exit curve appears in its own equivalent source");
        context.State.AdvanceTime(.3f); Frame(); Frame();
        check(page.MotionValue == 0 && context.State.NextScheduledFrameTimeSeconds is null,
            "reverse motion also returns to unscheduled event wait");
    }
    private static void RunMessageBarSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new MessageBarPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 12000), new(1, 1)), idle, new(0, 0, 1280, 12000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        void Action(string name, UiAccessibilityAction action = UiAccessibilityAction.Invoke)
        {
            var element = tree.CaptureSnapshot().Elements.First(element => element.Node.Name == name
                && element.Node.Role == (action == UiAccessibilityAction.Toggle ? UiAccessibilityRole.Switch : UiAccessibilityRole.Button));
            check(tree.RequestAction(element.Node.Id, element.RuntimeId, action, wait : false) == UiAccessibilityResult.Success,
                "MessageBar actual control accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        var messageBounds = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "메시지 Error"
            && element.Node.Role == UiAccessibilityRole.Group).Node.Bounds;
        check(page.Samples.Count == 2 && sources.Length == 2, "fixed and keyed MessageBar examples have separate source blocks");
        var end = messageBounds.Y + messageBounds.Height;
        check(sources[0].Bounds.Y >= end && sources[0].Bounds.Y - end <= 24,
            "fixed MessageBar source follows its four actual intent previews");
        check(page.Samples[0].Source.Contains("Fluent.MessageBar(", StringComparison.Ordinal)
            && page.Samples[1].Source.Contains("var createMessage = (string key) =>", StringComparison.Ordinal)
            && page.Samples[1].Source.Contains("var removeDynamic = (string key) =>", StringComparison.Ordinal),
            "both MessageBar sources capture their actual factories and dynamic callbacks");
        Action("동작 줄이기", UiAccessibilityAction.Toggle);
        Action("여러 행 배치", UiAccessibilityAction.Toggle);
        Action("각진 모서리", UiAccessibilityAction.Toggle);
        Action("오른쪽에서 왼쪽 배치", UiAccessibilityAction.Toggle);
        Action("차분한 공지로 고정", UiAccessibilityAction.Toggle);
        check(page.Samples.All(sample => sample.Source.Contains("UiMessageBarLayoutMode.MultiLine", StringComparison.Ordinal)
            && sample.Source.Contains("UiMessageBarShape.Square", StringComparison.Ordinal)
            && sample.Source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && sample.Source.Contains("UiAnnouncementPriority.Polite", StringComparison.Ordinal)
            && sample.Source.Contains("ReducedMotion = true", StringComparison.Ordinal)),
            "both snippets follow actual layout, shape, direction, announcement and motion switches");
        Action("닫기");
        check(!page.FixedMessages.IsVisible(0) && page.Samples[0].Source.Contains("SetVisible(0, false)", StringComparison.Ordinal),
            "fixed dismissal and its source keep the same controlled visibility");
        Action("모두 다시 열기"); Action("재시도");
        check(page.FixedMessages.IsVisible(0) && !page.Samples[0].Source.Contains("SetVisible(0, false)", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("new UiState<int>(1)", StringComparison.Ordinal),
            "reopen and retry callbacks retain their live state in the fixed source");
        var baseline = page.AnnouncementCount;
        Action("메시지 추가"); Action("메시지 추가");
        check(page.RequestedDynamicCount == 2 && page.PhysicalDynamicCount == 2 && page.AnnouncementCount == baseline + 2,
            "keyed insertions still mount and announce exactly once each with inline code present");
        check(page.Samples[1].Source.Contains("new string[] { \"message-1\",\"message-2\" }", StringComparison.Ordinal)
            && page.Samples[1].Source.Contains("var _nextMessage = 2L;", StringComparison.Ordinal),
            "dynamic source reconstructs its actual keys and future-key counter");
        Action("순서 뒤집기");
        check(page.DynamicMessages.Items[0].Key == "message-2" && page.AnnouncementCount == baseline + 2
            && page.Samples[1].Source.Contains("new string[] { \"message-2\",\"message-1\" }", StringComparison.Ordinal),
            "reordering updates source order without remount announcements");
        check(page.DynamicMessages.Children.ToArray().Select((message, index) =>
                ((UiFluentMessageBar)message).LastLayout.Size.Y == page.DynamicMessages.LastBounds[index].Height).All(value => value),
            "same-count reordered messages retain each owned child's actual height rather than the previous slot's allocation");
        Action("첫 메시지 삭제");
        check(page.RequestedDynamicCount == 1 && page.PhysicalDynamicCount == 1 && page.DynamicMessages.Items[0].Key == "message-1",
            "first keyed removal settles the actual requested and physical collection");
        Action("삭제");
        check(page.RequestedDynamicCount == 0 && page.PhysicalDynamicCount == 0
            && !page.Samples[1].Source.Contains("foreach (var key", StringComparison.Ordinal),
            "the actual per-message delete callback removes its item and source initializer");
        Action("표시할 때 자동 공지", UiAccessibilityAction.Toggle); Action("메시지 추가");
        check(page.AnnouncementCount == baseline + 2 && page.Samples.All(sample => sample.Source.Contains("DisableAnnouncement = true", StringComparison.Ordinal))
            && page.DynamicMessages.Items[0].Key == "message-3",
            "disabled announcements remain silent and stable keys are never reused after deletion");
        context.State.AdvanceTime(1);
        Frame(); Frame();
        var parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses))
            && context.State.NextScheduledFrameTimeSeconds is null,
            "settled MessageBar sources reuse syntax and return to unscheduled event wait");
    }
    private static void RunVirtualSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new VirtualPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)), idle, new(0, 0, 1280, 6000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Invoke(string name)
        {
            var element = Find(name, UiAccessibilityRole.Button);
            check(tree.RequestAction(element.Node.Id, element.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "VirtualList actual navigation accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var code = Find("C# 예제 소스", UiAccessibilityRole.Edit).Node;
        var viewport = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "가상 목록 바깥 스크롤").Node;
        check(page.Samples.Count == 1 && code.Bounds.Y >= viewport.Bounds.Y + viewport.Bounds.Height
            && code.Bounds.Y - viewport.Bounds.Y - viewport.Bounds.Height <= 24,
            "the billion-row virtual preview has its own immediately adjacent source");
        var source = page.Samples[0].Source;
        check(source.Contains("Dux.VirtualList(_source, 1_000_000_000, listOptions)", StringComparison.Ordinal)
            && source.Contains("internal sealed class ObserverRows", StringComparison.Ordinal)
            && source.Contains("internal sealed class ObserverRowExtents", StringComparison.Ordinal)
            && source.Contains("IUiVirtualListAutomationSource", StringComparison.Ordinal),
            "VirtualList source includes the actual captured factory, keyed row lifetime and variable extent implementation");
        var input = Find("행 값 : 0", UiAccessibilityRole.Edit);
        check(tree.RequestAction(input.Node.Id, input.RuntimeId, UiAccessibilityAction.SetValue, "디모이 \"가상 행\"", wait : false)
            == UiAccessibilityResult.Success, "the actual virtual row accepts edited input");
        Frame(); Frame();
        check(page.Samples[0].Source.Contains("_source.SetValue(0, \"디모이 \\\"가상 행\\\"\");", StringComparison.Ordinal),
            "edited virtual values appear as escaped keyed model initialization in the snippet");
        var parses = page.Samples[0].Metrics.SyntaxParses;
        Invoke("끝");
        check(page.Offset > 1_000_000 && page.RealizedCount <= 64 && page.LiveCount <= 64,
            "actual end navigation remains bounded despite a billion logical rows");
        Invoke("처음");
        check(Find("행 값 : 0", UiAccessibilityRole.Edit).Node.Value == "디모이 \"가상 행\"",
            "recycled virtual rows restore their edited value when returning from the end");
        check(page.Samples[0].Metrics.SyntaxParses == parses,
            "changing virtual offset and realization diagnostics does not reparse its unchanged example source");
        Invoke("행 높이 : 가변");
        check(page.Preview.Options.ExtentIndex is null && page.Samples[0].Source.Contains("ExtentIndex = null", StringComparison.Ordinal),
            "actual fixed-height selection updates the corresponding source option");
        Invoke("행 높이 : 고정"); Invoke("키 순서 바꾸기");
        check(page.Preview.Options.ExtentIndex is not null && page.Samples[0].Source.Contains("Swapped = true", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("ExtentIndex = new ObserverRowExtents(_source)", StringComparison.Ordinal),
            "variable extent and keyed reorder settings are represented together in the current snippet");
        Invoke("방향 : 세로");
        check(page.Preview.Options.Axis == UiScrollAxis.Horizontal
            && page.Samples[0].Source.Contains("Axis = UiScrollAxis.Horizontal, ExtentIndex = new ObserverRowExtents(_source, horizontal : true)", StringComparison.Ordinal),
            "actual horizontal selection updates both the live list and the sample source");
        Invoke("가로 순서 : 왼쪽부터");
        check(page.Preview.Options.FlowDirection == UiFlowDirection.RightToLeft
            && page.Samples[0].Source.Contains("FlowDirection = UiFlowDirection.RightToLeft", StringComparison.Ordinal),
            "changing the horizontal flow updates both the preview and source");
        Invoke("끝");
        check(page.Offset > 100_000_000_000d && page.RealizedCount <= 64
            && Find("행 값 : 0", UiAccessibilityRole.Edit).Node.Value == "디모이 \"가상 행\"",
            "horizontal tail navigation restores the reordered item's keyed value");
    }
    private static void RunWidgetLayoutSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new WidgetLayoutPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 14000), new(1, 1)), idle, new(0, 0, 1280, 14000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.Button) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Action(string name, UiAccessibilityAction action = UiAccessibilityAction.Invoke, string? value = null, UiAccessibilityRole? role = null)
        {
            var element = Find(name, role ?? (action == UiAccessibilityAction.SetValue ? UiAccessibilityRole.Edit
                : action == UiAccessibilityAction.Toggle ? UiAccessibilityRole.Checkbox : UiAccessibilityRole.Button));
            check(tree.RequestAction(element.Node.Id, element.RuntimeId, action, value, wait : false) == UiAccessibilityResult.Success,
                "transformed Gallery example accepts : " + name);
            Frame(); Frame();
        }
        void Range(string name, double value)
        {
            var element = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name
                && element.Node.Role == UiAccessibilityRole.Slider);
            check(tree.RequestRangeValue(element.Node.Id, element.RuntimeId, value, wait : false) == UiAccessibilityResult.Success,
                "widget layout actual configuration accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        var button = Find("변환 버튼 실행").Node;
        check(page.Samples.Count == 8 && sources.Length == 8,
            "button, checkbox, slider, input, popover, menu, tooltip and legacy portal each own their transform source");
        check(sources[0].Bounds.Y >= button.Bounds.Y + button.Bounds.Height
            && sources[0].Bounds.Y - button.Bounds.Y - button.Bounds.Height <= 32,
            "the first widget source follows its actual transformed layout and margins");
        check(page.Samples[^1].Source.Contains("internal sealed class LegacyPortalPreview", StringComparison.Ordinal)
            && page.Samples[^1].Source.Contains("ui.BeginPopup(", StringComparison.Ordinal),
            "legacy transformed portal source includes its actual compiled compatibility implementation");
        Range("공통 여백 - 왼쪽", 12); Range("공통 회전", 60); Range("공통 확대", 1.5);
        check(page.AppliedMargin.Left == 12 && page.AppliedTransform.RotationDegrees == 60 && page.AppliedTransform.Scale == 1.5f
            && page.Samples.All(sample => sample.Source.Contains("new UiEdgeInsets(12f, 8f, 8f, 8f)", StringComparison.Ordinal)
                && sample.Source.Contains("Scale : 1.5f, RotationDegrees : 60f", StringComparison.Ordinal)),
            "current margin, rotation and scale are reflected in every corresponding widget snippet");
        Action("시각 변환 (배치 크기 유지)", UiAccessibilityAction.Toggle);
        Action("회전 중심 전환 (시각 변환)");
        check(page.AppliedTransform.Mode == UiWidgetTransformMode.Visual && page.AppliedTransform.Origin == new UiVector2(1, 1)
            && page.Samples.All(sample => sample.Source.Contains("Origin : new UiVector2(1f, 1f), Mode : UiWidgetTransformMode.Visual", StringComparison.Ordinal)),
            "the selected visual transform mode and pivot appear in all independent examples");
        Action("공통 변환 초기화");
        var buttonParses = page.Samples[0].Metrics.SyntaxParses;
        Action("여백 체크박스", UiAccessibilityAction.Toggle);
        check(page.Samples[1].Source.Contains("new UiState<bool?>(true)", StringComparison.Ordinal)
            && page.Samples[0].Metrics.SyntaxParses == buttonParses,
            "checkbox state updates its own source without reparsing unrelated button code");
        Action("변환 입력", UiAccessibilityAction.SetValue, "디모이 \"변환\"");
        check(page.Samples[3].Source.Contains("디모이 \\\"변환\\\"", StringComparison.Ordinal),
            "edited transformed input is escaped in its corresponding model initializer");
        Action("변환 버튼 실행");
        check(page.Activations == 1 && page.Samples[0].Source.Contains("new UiState<int>(1)", StringComparison.Ordinal),
            "actual transformed button execution updates its shared action model in source");
        Range("공통 회전", 90);
        Action("변환 팝업"); Action("팝업 안에서 실행"); Action("변환 팝업");
        check(page.Activations == 2 && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "팝업 안에서 실행"),
            "the actual quarter-turned Popover opens, runs its upright portal callback and closes again");
        Action("변환 메뉴"); Action("메뉴에서 실행", role : UiAccessibilityRole.MenuItem);
        check(page.Activations == 3, "the actual quarter-turned menu executes its owned popup command");
        Action("기존 팝업 API"); Action("기존 팝업에서 실행"); Action("기존 팝업 닫기");
        check(page.Activations == 4 && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "기존 팝업에서 실행"),
            "the actual transformed compatibility popup preserves open, command and close behavior beside its source");
        Action("변환 도구 설명", UiAccessibilityAction.Focus);
        context.State.AdvanceTime(.3f); Frame(); Frame();
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Tooltip
            && element.Node.Name == "회전한 대상 옆에 화면 기준으로 표시합니다." && !element.Node.Offscreen
            && element.Node.Bounds.Width > element.Node.Bounds.Height),
            "the transformed Tooltip target opens its real upright hover/focus surface");
        Action("변환 버튼 실행", UiAccessibilityAction.Focus);
        context.State.AdvanceTime(.3f); Frame(); Frame();
        var parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "stable widget transform examples reuse parsed code across diagnostic renders");
    }
    private static void RunIntegrationSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var examples = new LayoutIntegrationPage();
        using var page = new WidgetPage("배치 통합", "탭·넘침·메뉴를 각각의 소스와 비교합니다.", examples);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 14000), new(1, 1)), idle, new(0, 0, 1280, 14000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        void Action(string name, UiAccessibilityRole role = UiAccessibilityRole.Button, UiAccessibilityAction action = UiAccessibilityAction.Invoke)
        {
            var element = Find(name, role);
            check(tree.RequestAction(element.Node.Id, element.RuntimeId, action, wait : false) == UiAccessibilityResult.Success,
                "integration actual control accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스").Select(element => element.Node).ToArray();
        check(examples.Samples.Count == 8 && sources.Length == 8,
            "tab overflow, priority overflow and six menu examples each own an adjacent source");
        var selected = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "선택 값 : home").Node;
        check(sources[0].Bounds.Y >= selected.Bounds.Y + selected.Bounds.Height
            && sources[0].Bounds.Y - selected.Bounds.Y - selected.Bounds.Height <= 24,
            "tab overflow source follows its actual header, owned panel and selection status");
        check(examples.Samples[0].Source.Contains("internal sealed class TabOverflowBodies", StringComparison.Ordinal)
            && examples.Samples[0].Source.Contains("_tabs.RenderPanel(ui, entry.Key, entry.Body)", StringComparison.Ordinal),
            "tab example source includes the actual compiled owned-panel implementation");
        Action("넘침 본문 홈 : 0");
        var more = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Tab
            && element.Node.Name.Contains("개 탭 더 보기", StringComparison.Ordinal));
        check(tree.RequestAction(more.Node.Id, more.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "the actual hidden-tab menu opens");
        Frame(); Frame(); Action("도움말", UiAccessibilityRole.MenuItem);
        check(examples.Samples[0].Source.Contains("new UiState<string?>(\"help\")", StringComparison.Ordinal)
            && tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "넘침 본문 도움말 : 0"),
            "hidden-tab selection changes the actual panel and current code selection together");
        Action("탭 폭 : 160"); Action("홈", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "넘침 본문 홈 : 1")
            && examples.Samples[0].Source.Contains("widthValue = 520f", StringComparison.Ordinal)
            && examples.Samples[0].Source.Contains("counts = [new(1), new(0)", StringComparison.Ordinal),
            "tab expansion restores the original panel counter and includes its current initializer");
        Action("탭 방향 : LTR"); Action("탭 배치 : 가로");
        check(examples.Samples[0].Source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && examples.Samples[0].Source.Contains("Vertical : true", StringComparison.Ordinal)
            && examples.Samples[0].Source.Contains("heightValue = 110f", StringComparison.Ordinal),
            "current tab direction, orientation and frame dimensions update only its own source");
        Action("넘침 항목 1 : 0"); Action("우선 항목 : 1"); Action("넘침 폭 : 200"); Action("넘침 방향 : LTR");
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "넘침 항목 1 : 1")
            && examples.Samples[1].Source.Contains("widthValue = 560f", StringComparison.Ordinal)
            && examples.Samples[1].Source.Contains("prioritizeLast = true", StringComparison.Ordinal)
            && examples.Samples[1].Source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && examples.Samples[1].Source.Contains("counts = [new(1), new(0)", StringComparison.Ordinal),
            "priority overflow preserves hidden-button state and exposes actual width, direction and priority configuration");
        Action("공통 메뉴 열기"); Action("첫 번째 동작", UiAccessibilityRole.MenuItem);
        check(examples.Samples[2].Source.Contains("Dux.Menu(", StringComparison.Ordinal)
            && examples.Samples[2].Source.Contains("new UiState<string>(\"선택 : first\")", StringComparison.Ordinal),
            "infrastructure menu code captures its real command and shared selection state");
        Action("Fluent 메뉴 열기"); Action("Fluent 마지막", UiAccessibilityRole.MenuItem);
        check(examples.Samples[3].Source.Contains("Fluent.Menu(", StringComparison.Ordinal)
            && examples.Samples[3].Source.Contains("Fluent 선택 : last", StringComparison.Ordinal),
            "Fluent menu command updates the corresponding independent source");
        Action("긴 메뉴·문자 탐색"); Action("Apple", UiAccessibilityRole.MenuItem);
        check(examples.Samples[4].Source.Contains("MaximumHeight = 210f", StringComparison.Ordinal)
            && examples.Samples[4].Source.Contains("긴 메뉴 선택 : apple", StringComparison.Ordinal),
            "long menu source includes its real height limit and executed command");
        Action("체크 메뉴 열기"); Action("상세 정보 표시", UiAccessibilityRole.MenuItem);
        check(examples.Samples[5].Source.Contains("new UiState<bool>(false)", StringComparison.Ordinal)
            && examples.Samples[7].Source.Contains("var showDetails = new UiState<bool>(false)", StringComparison.Ordinal),
            "borrowed menu checkbox changes update both examples sharing its actual state");
        Action("설정 마침", UiAccessibilityRole.MenuItem);
        Action("라디오 메뉴 열기"); Action("날짜순 정렬", UiAccessibilityRole.MenuItem);
        Action("라디오 메뉴 열기"); Action("넓게 보기", UiAccessibilityRole.MenuItem);
        check(examples.Samples[6].Source.Contains("new UiState<string?>(\"date\")", StringComparison.Ordinal)
            && examples.Samples[6].Source.Contains("new UiState<string?>(\"wide\")", StringComparison.Ordinal),
            "both independent menu radio groups retain their selected values in current code");
        Action("메뉴 방향 : LTR"); Action("아이콘 메뉴 열기"); Action("새 항목 선택", UiAccessibilityRole.MenuItem);
        check(examples.Samples[7].Source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && examples.Samples[7].Source.Contains("UiFluentAddFilledIcon", StringComparison.Ordinal)
            && examples.Samples[7].Source.Contains("아이콘 메뉴 선택 : new", StringComparison.Ordinal),
            "icon menu code includes its actual icon factory, content and current direction/command");
        var parses = examples.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(examples.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "stable integration examples reuse parsed source instead of following transient layout diagnostics");
        for (var pass = 0; pass < 1000; pass++)
            for (var index = 0; index < examples.Samples.Count; index++)
                examples.Samples[index].SynchronizeSource();
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 1000; pass++)
            for (var index = 0; index < examples.Samples.Count; index++)
                examples.Samples[index].SynchronizeSource();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        Console.WriteLine($"INTEGRATION_SOURCE_SYNC_ALLOCATION passes=1000 samples={examples.Samples.Count} bytes={allocated}");
        check(allocated == 0, "1000 unchanged integration source synchronization passes allocate no managed bytes on the calling thread");
    }
    private static void RunViewHostSemantics(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var named = Dux.SizedBox(new UiViewHost(new UiView(ui => ui.Button("Hosted action")))
            { AccessibleName = "Named sequential region" }, width : 200, height : 60);
        using var unnamed = Dux.SizedBox(new UiViewHost(new UiView(ui => ui.Button("Unwrapped action"))), width : 200, height : 60);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.NewFrame(new(.016f, new(400, 200), new(1, 1)), idle, new(0, 0, 400, 200), UiTextSettings.Default);
        context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); named.Render(ui); unnamed.Render(ui); }));
        using var drawing = context.GetDrawData();
        var nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
        var pane = nodes.Single(node => node.Name == "Named sequential region");
        var child = nodes.Single(node => node.Name == "Hosted action");
        check(pane.Role == UiAccessibilityRole.Pane && pane.ReadOnly && !pane.Focusable && !pane.SupportsInvoke
            && pane.Bounds.Width == 200 && pane.Bounds.Height == 60,
            "named sequential host publishes its actual finite allocation as a passive pane");
        check(child.Role == UiAccessibilityRole.Button && child.ParentId == pane.Id && child.SupportsInvoke,
            "host name groups sequential content without replacing child names or actions");
        check(nodes.Single(node => node.Name == "Unwrapped action").ParentId == pane.ParentId,
            "unnamed sequential host keeps the existing unwrapped accessibility hierarchy");
    }
    private static void RunRenderingSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new RenderingPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(float offset = 0)
        {
            context.NewFrame(new(.016f, new(1280, 18000), new(1, 1)), idle, new(0, 0, 1280, 18000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); ui.SetCursorScreenPos(new(0, offset)); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        void Invoke(string name)
        {
            var node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "rendering actual command accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
        var sources = nodes.Where(node => node.Name == "C# 예제 소스" && node.Role == UiAccessibilityRole.Edit).ToArray();
        string[] names = ["정적 도형 예제", "3D 투영 예제", "참조 합성 예제", "그룹 합성 예제", "투영 텍스트 예제",
            "양끝 투명 마스크와 캐시 카메라"];
        check(page.Samples.Count == names.Length && sources.Length == names.Length,
            "every rendering technique owns its compiled preview and source pair");
        for (var index = 0; index < names.Length; index++)
        {
            var source = sources[index];
            check(source.Value == page.Samples[index].Source && nodes.Any(preview =>
                preview.Name == names[index] && preview.Role == UiAccessibilityRole.Pane && preview.Bounds.Height > 0
                && source.Bounds.Y - preview.Bounds.Y - preview.Bounds.Height is >= 0 and <= 24),
                "rendering source immediately follows its finite drawing surface : " + names[index]);
        }
        check(page.Samples[2].Source.Contains("using var _backdropPreview = new UiBackdropImage", StringComparison.Ordinal)
            && page.Samples[3].Source.Contains("paint.Frame", StringComparison.Ordinal)
            && page.Samples[4].Source.Contains("UiPaintGroupTextMode.Projected", StringComparison.Ordinal),
            "rendering snippets include actual reference resources and paint callbacks");
        var builds = page.StaticBuilds;
        var parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(page.StaticBuilds == builds && parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "unchanged rendering frames reuse retained static geometry and all parsed sources");
        Invoke("정적 내용 변경");
        check(page.StaticBuilds == builds + 1 && page.Samples[0].Source.Contains("long _revision = 2;", StringComparison.Ordinal)
            && page.Samples[1].Source.Contains("long _revision = 2;", StringComparison.Ordinal),
            "explicit content revision rebuilds the actual static callback once and updates both versioned snippets");
        page.ToggleRoundedClip(); Frame(); Frame();
        check(new[] { 0, 1, 3 }.All(index => page.Samples[index].Source.Contains("var _roundedClip = true;", StringComparison.Ordinal))
            && page.Samples[2].Metrics.SyntaxParses == parses[2] && page.Samples[4].Metrics.SyntaxParses == parses[4],
            "rounded clip changes only affected examples while independent capture snippets stay parsed");
        parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(10); Frame(10);
        check(parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "drawing allocation movement does not mutate content revision or reparse source");
        Frame(); Invoke("애니메이션 : 꺼짐");
        parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(context.State.NextScheduledFrameTimeSeconds is not null
            && page.Samples[1].Source.Contains("var _animate = true;", StringComparison.Ordinal)
            && parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "enabled 3D animation schedules frames without rebuilding its unchanged source");
        Invoke("애니메이션 : 켜짐");
        for (var settle = 0; settle < 40; settle++) Frame();
        check(context.State.GetScheduledFrameWaitMilliseconds(context.State.TimeSeconds) == Timeout.Infinite,
            "disabled rendering animation returns to indefinite event wait after control transitions settle");
    }
    private static void RunPinnedGalleryHeaders(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "렌더링" };
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 900), new(1, 1)), idle, new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(gallery);
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        Frame(); Frame();
        var destination = Find(gallery.SelectedNavigationLabel, UiAccessibilityRole.Link);
        check(tree.RequestAction(destination.Node.Id, destination.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
            "pinned-header regression reveals the selected navigation destination first");
        Frame(); Frame(); Frame();
        string[] labels = ["Duxel 갤러리", "갤러리 상태와 옵션", "밝은 테마"];
        UiAccessibilityRole[] roles = [UiAccessibilityRole.Label, UiAccessibilityRole.Pane, UiAccessibilityRole.Switch];
        var before = labels.Select((name, index) => Find(name, roles[index]).Node.Bounds).ToArray();
        var target = Find("그룹 합성 예제", UiAccessibilityRole.Pane);
        check(target.Node.Offscreen && tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
            "lower rendering preview starts outside viewport and accepts actual reveal");
        for (var settle = 0; settle < 4; settle++) Frame();
        for (var index = 0; index < labels.Length; index++)
        {
            var after = Find(labels[index], roles[index]).Node.Bounds;
            Console.WriteLine($"Pinned header {labels[index]} : {before[index]} -> {after}");
            check(after == before[index], "body reveal preserves pinned Gallery header geometry : " + labels[index]);
        }
        check(!Find("그룹 합성 예제", UiAccessibilityRole.Pane).Node.Offscreen,
            "fixed headers preserve successful lower-body reveal");
        foreach (var element in tree.CaptureSnapshot().Elements.Where(element => element.Node.Scroll is not null))
            Console.WriteLine($"Reveal scroll : {element.Node.Name} / {element.Node.Bounds} / {element.Node.Scroll}");
    }
    private static void RunPopupInputSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new PopupInputPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 900), new(1, 1)), idle, new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button);
        void Invoke(string name)
        {
            var element = Find(name);
            check(tree.RequestAction(element.Node.Id, element.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "popup input actual control accepts : " + name);
            Frame(); Frame(); Frame();
        }
        Frame(); Frame();
        var source = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Node;
        var tail = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name.StartsWith("팝업 안을 클릭한 뒤", StringComparison.Ordinal)).Node;
        check(page.Samples.Count == 1 && source.Value == page.Samples[0].Source && source.Bounds.Y < 600
            && source.Bounds.Y >= tail.Bounds.Y + tail.Bounds.Height && source.Bounds.Y - tail.Bounds.Y - tail.Bounds.Height <= 24,
            "popup input owns one actual example with source immediately after its natural-height content");
        check(source.Value!.Contains("ui.BeginPopup(\"observer-input-popup\", Mode)", StringComparison.Ordinal)
            && source.Value.Contains("finally { ui.EndPopup(); }", StringComparison.Ordinal)
            && source.Value.Contains("using var _popup = Dux.Column", StringComparison.Ordinal),
            "popup input source includes the real owned controls and balanced overlay callback");
        var parses = page.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(page.Samples[0].Metrics.SyntaxParses == parses, "unchanged popup mode and counts retain parsed source");
        Invoke("입력 팝업 열기");
        check(page.Visible && Find("팝업 바깥 명령").Node.Enabled
            && page.Samples[0].Source.Contains("var _requestOpen = true;", StringComparison.Ordinal),
            "nonmodal popup stays visible outside the body clip and reflects its open state in source");
        Invoke("팝업 안 명령"); Invoke("팝업 바깥 명령");
        check(page.InsideCount == 1 && page.OutsideCount == 1
            && page.Samples[0].Source.Contains("var InsideCount = 1;", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("var OutsideCount = 1;", StringComparison.Ordinal),
            "actual popup and background commands update independent counters and their current source initializers");
        Invoke("입력 팝업 닫기");
        check(!page.Visible && page.Samples[0].Source.Contains("var _requestOpen = false;", StringComparison.Ordinal),
            "actual close retires the overlay and updates its source state");
        Invoke("팝업 입력 모드 전환"); Invoke("입력 팝업 열기");
        check(page.Visible && !Find("팝업 바깥 명령").Node.Enabled && Find("팝업 안 명령").Node.Enabled
            && page.Samples[0].Source.Contains("var Mode = UiPopupInputMode.Exclusive;", StringComparison.Ordinal),
            "exclusive popup blocks its actual background while preserving live popup controls and matching source mode");
        Invoke("팝업 안 명령"); Invoke("입력 팝업 닫기");
        check(!page.Visible && Find("팝업 바깥 명령").Node.Enabled && page.InsideCount == 2,
            "exclusive popup close restores the real background and retains prior model state");
    }
    private static void RunTooltipSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var content = new TooltipPage();
        using var page = new WidgetPage("Tooltip", "설명과 이름, 직접 제어를 확인합니다.", content);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, float delta = .016f)
        {
            context.NewFrame(new(delta, new(1280, 8000), new(1, 1)), input ?? idle, new(0, 0, 1280, 8000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button);
        Frame(); Frame();
        var nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
        var sources = nodes.Where(node => node.Name == "C# 예제 소스" && node.Role == UiAccessibilityRole.Edit).ToArray();
        string[] targets = ["설명 대상", "화살표 대상", "세부 정보", "Tooltip 표시 전환"];
        check(content.Samples.Count == 4 && sources.Length == 4, "Tooltip automatic,arrow,label and caller-controlled examples own four distinct sources");
        for (var index = 0; index < targets.Length; index++)
        {
            var target = Find(targets[index]).Node;
            check(sources[index].Value == content.Samples[index].Source && sources[index].Bounds.Y >= target.Bounds.Y + target.Bounds.Height
                && sources[index].Bounds.Y - target.Bounds.Y - target.Bounds.Height <= 24,
                "Tooltip source immediately follows its actual preview : " + targets[index]);
        }
        check(Find("설명 대상").Node.GetHelpText() == "대상에 포인터를 올리거나 키보드로 포커스를 옮겨 보세요."
            && Find("세부 정보").Node.Tooltip?.Relationship == UiTooltipRelationship.Label,
            "separate Tooltip examples preserve actual description and accessible-name relationships");
        check(content.Samples[1].Source.Contains("ArrowHeight = 6", StringComparison.Ordinal)
            && content.Samples[2].Source.Contains("Relationship = UiTooltipRelationship.Label", StringComparison.Ordinal),
            "Tooltip sources contain the actual arrow and name options rather than the complete combined page");
        var parses = content.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        var normal = Find("설명 대상").Node;
        var hover = idle with { MousePosition = new(normal.Bounds.X + normal.Bounds.Width / 2, normal.Bounds.Y + normal.Bounds.Height / 2) };
        Frame(hover, .01f);
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Tooltip),
            "Gallery automatic Tooltip retains its actual show delay");
        Frame(hover, .26f);
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Tooltip && element.Node.Name == normal.GetHelpText())
            && parses.SequenceEqual(content.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "automatic Tooltip opens on the scheduled hover frame without reparsing any source");
        Frame(hover with { KeyEvents = [new(UiKey.Escape, true, default)] }); Frame();
        var toggle = Find("Tooltip 표시 전환");
        check(tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Gallery caller-owned Tooltip uses the real toggle action");
        Frame(); Frame(); Frame();
        check(content.Controlled.Visible == true && content.Controlled.IsOpen
            && content.Samples[3].Source.Contains("controlled.Visible = true;", StringComparison.Ordinal)
            && Enumerable.Range(0, 3).All(index => content.Samples[index].Metrics.SyntaxParses == parses[index]),
            "caller-owned Tooltip opens and synchronizes only its own source initializer");
        Frame(idle with { KeyEvents = [new(UiKey.Escape, true, default)] }); Frame(); Frame();
        check(content.Controlled.Visible == false && !content.Controlled.IsOpen
            && content.Samples[3].Source.Contains("controlled.Visible = false;", StringComparison.Ordinal),
            "Escape executes the actual visibility callback and updates the displayed caller-owned state");
    }
    private static void RunEditingSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var content = new EditingPage();
        using var page = new WidgetPage("입력·IME", "입력·검증·메모 편집을 확인합니다.", content);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 14000), new(1, 1)), input ?? idle, new(0, 0, 1280, 14000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        void Invoke(string name)
        {
            var target = Find(name, UiAccessibilityRole.Button);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "editing actual control accepts : " + name);
            Frame(); Frame();
        }
        void Set(string name, string value)
        {
            var target = Find(name, UiAccessibilityRole.Edit);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.SetValue, value, wait : false) == UiAccessibilityResult.Success,
                "editing actual input accepts value : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Select(element => element.Node).ToArray();
        check(content.Samples.Count == 2 && sources.Length == 2,
            "name validation and memo editing each own their actual source directly below the example");
        var nameTail = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "이름 : ").Node;
        var memoTail = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name.StartsWith("Tab / Shift+Tab으로", StringComparison.Ordinal)).Node;
        check(sources[0].Bounds.Y >= nameTail.Bounds.Y + nameTail.Bounds.Height && sources[0].Bounds.Y - nameTail.Bounds.Y - nameTail.Bounds.Height <= 24
            && sources[1].Bounds.Y >= memoTail.Bounds.Y + memoTail.Bounds.Height && sources[1].Bounds.Y - memoTail.Bounds.Y - memoTail.Bounds.Height <= 24,
            "both editing sources follow their actual final content within normal component spacing");
        check(Find("이름", UiAccessibilityRole.Edit).Node.Invalid
            && content.Samples[0].Source.Contains("context.Watch(_name)", StringComparison.Ordinal)
            && content.Samples[0].Source.Contains("Fluent.Field(Fluent.Input", StringComparison.Ordinal),
            "name source includes the actual compiled reactive validation widget and native input factory");
        Set("이름", "디모이");
        check(!Find("이름", UiAccessibilityRole.Edit).Node.Invalid && content.Samples[0].Source.Contains("new UiState<string>(\"디모이\")", StringComparison.Ordinal),
            "real name input updates validation and the corresponding current state initializer");
        var nameParses = content.Samples[0].Metrics.SyntaxParses;
        Set("메모", "따옴표 \" · 역슬래시 \\ · 한글\n다음 줄");
        check(content.Samples[1].Source.Contains(GallerySample.String("따옴표 \" · 역슬래시 \\ · 한글\n다음 줄"), StringComparison.Ordinal)
            && content.Samples[0].Metrics.SyntaxParses == nameParses,
            "memo source escapes the current value without reparsing independent name validation source");
        Invoke("줄바꿈 전환"); Invoke("읽기 전용 전환");
        check(Find("메모", UiAccessibilityRole.Edit).Node.ReadOnly
            && content.Samples[1].Source.Contains("Flags = UiInputTextFlags.ReadOnly, Wrap = false", StringComparison.Ordinal),
            "memo wrap and read-only actions change both real editing options and displayed source");
        Invoke("라벨 필수 전환"); Invoke("라벨 크기 전환"); Invoke("라벨 굵기 전환"); Invoke("라벨 비활성 전환");
        check(content.Samples[1].Source.Contains("Required : true, Disabled : true", StringComparison.Ordinal)
            && content.Samples[1].Source.Contains("Size : UiLabelSize.Small, Semibold : true", StringComparison.Ordinal),
            "all independent label option controls update their actual source record");
        Invoke("긴 라벨 전환");
        check(content.Samples[1].Source.Contains("memoLabel.Text = \"메모에 남길 내용을 입력하세요.", StringComparison.Ordinal),
            "long linked label text appears in the matching initializer");
        Invoke("메모 지우기");
        check(content.Samples[1].Source.Contains("new UiState<string>(\"\")", StringComparison.Ordinal),
            "programmatic memo clear synchronizes its current source even in read-only mode");
        var parses = content.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(content.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "unchanged input values and edit/label settings retain both parsed sources");
    }
    private static void RunMeasurementSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var content = new MeasurementPage();
        using var page = new WidgetPage("측정 재사용", "실제 계측과 예제 소스를 비교합니다.", content);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 14000), new(1, 1)), input ?? idle, new(0, 0, 1280, 14000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        void Action(string name, UiAccessibilityRole role = UiAccessibilityRole.Button, UiAccessibilityAction action = UiAccessibilityAction.Invoke, string? value = null)
        {
            var target = Find(name, role);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, action, value, wait : false) == UiAccessibilityResult.Success,
                "measurement actual control accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Select(element => element.Node).ToArray();
        check(content.Samples.Count == 2 && sources.Length == 2,
            "measurement primitive and composite-form examples each own an adjacent actual source");
        var primitiveEnd = Find("재사용 대기", UiAccessibilityRole.ProgressBar).Node;
        var formEnd = Find("재사용 도구 설명 대상", UiAccessibilityRole.Button).Node;
        check(sources[0].Bounds.Y >= primitiveEnd.Bounds.Y + primitiveEnd.Bounds.Height
            && sources[0].Bounds.Y - primitiveEnd.Bounds.Y - primitiveEnd.Bounds.Height <= 100
            && sources[1].Bounds.Y >= formEnd.Bounds.Y + formEnd.Bounds.Height
            && sources[1].Bounds.Y - formEnd.Bounds.Y - formEnd.Bounds.Height <= 100,
            "both measurement sources follow their original finite preview allocations");
        check(content.Samples[0].Source.Contains("class MeasurementPrimitives", StringComparison.Ordinal)
            && content.Samples[0].Source.Contains("Measurements++;", StringComparison.Ordinal)
            && !content.Samples[0].Source.Contains("class MeasurementForm", StringComparison.Ordinal)
            && content.Samples[1].Source.Contains("class MeasurementForm", StringComparison.Ordinal)
            && !content.Samples[1].Source.Contains("class MeasurementPrimitives", StringComparison.Ordinal),
            "measurement sources reuse only their corresponding actual compiled probe declarations");
        var measures = (content.Measurements, content.FormMeasurements, content.TabMeasurements, content.DelegatedMeasurements, content.OwnedSizingMeasurements);
        var parses = content.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        var reads = content.ProgressReads;
        Frame(); Frame();
        check(measures == (content.Measurements, content.FormMeasurements, content.TabMeasurements, content.DelegatedMeasurements, content.OwnedSizingMeasurements)
            && content.ProgressReads > reads && parses.SequenceEqual(content.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "live measurement/progress diagnostics reuse both intrinsic measurements and parsed sources");
        Action("재사용 체크", UiAccessibilityRole.Checkbox, UiAccessibilityAction.Toggle);
        check(content.Checked == true && content.Measurements == measures.Measurements
            && content.Samples[0].Source.Contains("new UiState<bool?>(true)", StringComparison.Ordinal),
            "checkbox state updates its source while preserving actual measured control sizes");
        Action("재사용 슬라이더", UiAccessibilityRole.Slider, UiAccessibilityAction.Focus);
        Frame(idle with { KeyEvents = [new(UiKey.End, true, default)] }); Frame();
        check(content.LastProgressValue == 1 && content.Samples[0].Source.Contains("new UiState<float>(1f)", StringComparison.Ordinal),
            "actual slider keyboard input updates live progress and the primitive source value");
        Action("측정 본문 변경"); Action("측정 라벨 변경"); Action("측정 너비 변경");
        check(content.Measurements > measures.Measurements
            && content.Samples[0].Source.Contains("primitives.Width = 300f;", StringComparison.Ordinal)
            && content.Samples[0].Source.Contains("primitives.BadgeCount = 2;", StringComparison.Ordinal)
            && content.Samples[0].Source.Contains("재사용 체크 : 변경된 라벨", StringComparison.Ordinal),
            "geometry-affecting primitive actions invalidate actual measurements and matching code settings");
        Action("재사용 폼 입력", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "현재 폼 값");
        Action("재사용 여러 줄", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "메모\n현재 줄");
        Action("측정 탭 Beta", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
        check(content.FormValue == "현재 폼 값" && content.MemoValue == "메모\n현재 줄" && content.TabValue == "b"
            && content.Samples[1].Source.Contains("new UiState<string>(\"현재 폼 값\")", StringComparison.Ordinal)
            && content.Samples[1].Source.Contains("new UiState<string?>(\"b\")", StringComparison.Ordinal),
            "actual form,multiline and tab values update only their corresponding source state");
        Action("탭 크기 변경");
        check(content.Samples[1].Source.Contains("form.TabSize = UiTabListSize.Small;", StringComparison.Ordinal),
            "actual tab size option is reflected in the form source");
        parses = content.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(content.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "measurement counters and progress reads do not repeatedly format source snapshots");
        for (var warmup = 0; warmup < 1000; warmup++)
            for (var index = 0; index < content.Samples.Count; index++) content.Samples[index].SynchronizeSource();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 1000; pass++)
            for (var index = 0; index < content.Samples.Count; index++) content.Samples[index].SynchronizeSource();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"Measurement source unchanged-sync allocation : {allocated} B / 2000 calls");
        check(allocated == 0, "unchanged measurement source synchronization allocates no managed bytes on its calling thread");
    }
    private static void RunRepaintSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new RepaintPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 10000), new(1, 1)), input ?? idle, new(0, 0, 1280, 10000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        void Action(string name, UiAccessibilityRole role = UiAccessibilityRole.Button, UiAccessibilityAction action = UiAccessibilityAction.Invoke, string? value = null)
        {
            var target = Find(name, role);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, action, value, wait : false) == UiAccessibilityResult.Success,
                "repaint experiment actual control accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var source = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Node;
        var preview = Find("갱신 관찰 실험", UiAccessibilityRole.Pane).Node;
        check(page.Samples.Count == 1 && source.Value == page.Samples[0].Source
            && source.Bounds.Y >= preview.Bounds.Y + preview.Bounds.Height && source.Bounds.Y - preview.Bounds.Y - preview.Bounds.Height <= 24,
            "repaint experiment source immediately follows the natural-height controls and both retained panels");
        check(source.Value!.Contains("class RepaintExperiment", StringComparison.Ordinal)
            && source.Value.Contains("ContentVersion = version", StringComparison.Ordinal)
            && source.Value.Contains("ui.RequestFrameAt(_nextUpdate)", StringComparison.Ordinal),
            "repaint source includes its actual compiled cache and frame-scheduling behavior");
        var fixedBuilds = page.StaticBuilds;
        var textBuilds = page.TextBuilds;
        var parses = page.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(page.StaticBuilds == fixedBuilds && page.TextBuilds == textBuilds && page.Samples[0].Metrics.SyntaxParses == parses,
            "unchanged repaint frames retain both panel construction and parsed source");
        Action("한 번 변경");
        check(page.TextBuilds > textBuilds && page.StaticBuilds == fixedBuilds && page.Samples[0].Metrics.SyntaxParses == parses,
            "synthetic content revision rebuilds only the changing panel and does not reparse source");
        Action("갱신 관찰 입력", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "진단 입력을 바꿉니다");
        check(Find("갱신 관찰 입력", UiAccessibilityRole.Edit).Node.Value == "진단 입력을 바꿉니다"
            && page.StaticBuilds == fixedBuilds && page.Samples[0].Metrics.SyntaxParses == parses,
            "runtime inspector text changes stay outside source snapshots to avoid contaminating damage observation");
        Action("텍스트 자동 변경 (60Hz)", UiAccessibilityRole.Switch, UiAccessibilityAction.Toggle);
        parses = page.Samples[0].Metrics.SyntaxParses;
        textBuilds = page.TextBuilds;
        for (var tick = 0; tick < 20; tick++) Frame();
        check(page.Running && page.TextBuilds > textBuilds && context.State.NextScheduledFrameTimeSeconds is not null
            && page.Samples[0].Metrics.SyntaxParses == parses
            && page.Samples[0].Source.Contains("experiment.AutoChange = true;", StringComparison.Ordinal),
            "automatic repaint schedules real text changes while keeping its configured source stable");
        Action("CPU 구간 측정", UiAccessibilityRole.Switch, UiAccessibilityAction.Toggle);
        parses = page.Samples[0].Metrics.SyntaxParses;
        Action("측정값 확인");
        check(page.CpuSnapshot.StartsWith("직전 Render : 입력", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("experiment.Profile = true;", StringComparison.Ordinal)
            && page.Samples[0].Metrics.SyntaxParses == parses,
            "actual profiler snapshot changes diagnostics without rewriting source and exposes its profiling setting");
        Action("텍스트 자동 변경 (60Hz)", UiAccessibilityRole.Switch, UiAccessibilityAction.Toggle);
        Action("CPU 구간 측정", UiAccessibilityRole.Switch, UiAccessibilityAction.Toggle);
        for (var settle = 0; settle < 40; settle++) Frame();
        check(!page.Running && context.State.GetScheduledFrameWaitMilliseconds(context.State.TimeSeconds) == Timeout.Infinite,
            "stopped repaint experiment returns to indefinite event wait instead of a source-driven render loop");
    }
    private static void RunMarkdownSources(Action<bool, string> check, string? output)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new MarkdownPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 12000), new(1, 1)), input ?? idle, new(0, 0, 1280, 12000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        void Action(string name, UiAccessibilityRole role = UiAccessibilityRole.Button, UiAccessibilityAction action = UiAccessibilityAction.Invoke, string? value = null)
        {
            var target = Find(name, role);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, action, value, wait : false) == UiAccessibilityResult.Success,
                "Markdown actual control accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var code = Find("C# 예제 소스", UiAccessibilityRole.Edit).Node;
        var editor = Find("마크다운 스마트 편집", UiAccessibilityRole.Edit).Node;
        ExportSources(output, "Markdown-editor", code);
        check(page.Samples.Count == 1 && code.Value == page.Samples[0].Source
            && code.Bounds.Y - editor.Bounds.Y - editor.Bounds.Height is >= 0 and <= 24,
            "selected Markdown editor owns exactly one immediately following source without a page-height spacer");
        check(code.Value!.Contains("Dux.MarkdownEditor(", StringComparison.Ordinal)
            && !code.Value.Contains("Dux.MarkdownView(", StringComparison.Ordinal)
            && code.Value.Contains("class MarkdownImageResources", StringComparison.Ordinal),
            "Markdown editor source captures only its actual factory and complete compiled image resolver");
        var parses = page.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(page.Samples[0].Metrics.SyntaxParses == parses, "idle Markdown preserves its parsed source");
        check(GallerySample.DocumentString("line\n\"\"\"quoted\"\"\"\n").StartsWith("\"\"\"\"\n", StringComparison.Ordinal)
            && GallerySample.DocumentString("line\r\nnext") == GallerySample.String("line\r\nnext"),
            "Markdown raw source literals expand their quote delimiter and preserve exact CR/control characters through escaped literals");
        const string edited = "# 현재 문서\n\n\"따옴표\"와 \\ 경로 · 한글";
        Action("마크다운 스마트 편집", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, edited);
        check(page.Editor.Value.Value == edited && page.Samples[0].Source.Contains(GallerySample.DocumentString(edited), StringComparison.Ordinal),
            "real Markdown editing synchronizes escaped current document text into its source");
        Action("마크다운 스마트 편집", UiAccessibilityRole.Edit, UiAccessibilityAction.Focus);
        parses = page.Samples[0].Metrics.SyntaxParses;
        Frame(idle with { KeyEvents = [new(UiKey.LeftArrow, true, default)] }); Frame();
        check(page.Samples[0].Metrics.SyntaxParses == parses,
            "moving the Markdown caret preserves source syntax instead of treating selection as document state");
        Action("줄바꿈 전환");
        check(!page.Editor.Options.Wrap && !page.View.Options.Wrap
            && page.Samples[0].Source.Contains("Wrap : false", StringComparison.Ordinal),
            "Markdown wrap control updates both live widgets and the selected source options");
        Action("미리보기", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
        check(page.View.Value.Value == edited && page.Samples[0].Source.Contains("Dux.MarkdownView(", StringComparison.Ordinal)
            && !page.Samples[0].Source.Contains("Dux.MarkdownEditor(", StringComparison.Ordinal),
            "switching to preview retains the document and replaces source with the real viewer factory");
        var previewCode = Find("C# 예제 소스", UiAccessibilityRole.Edit).Node;
        ExportSources(output, "Markdown-viewer", previewCode);
        var preview = Find("마크다운 미리보기", UiAccessibilityRole.Edit).Node;
        check(previewCode.Bounds.Y - preview.Bounds.Y - preview.Bounds.Height is >= 0 and <= 24,
            "Markdown viewer source immediately follows the viewer allocation");
        Action("중첩 미리보기", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
        var nestedCode = Find("C# 예제 소스", UiAccessibilityRole.Edit).Node;
        ExportSources(output, "Markdown-nested", nestedCode);
        check(previewCode.Bounds.Y - nestedCode.Bounds.Y == 420
            && page.Samples[0].Source.Contains("Dux.ScrollView(viewer, new(Width : 480, Height : 280))", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("Dux.ScrollView(inner, new(Width : 520, Height : 160))", StringComparison.Ordinal),
            "nested preview source captures both actual scroll containers and follows their smaller natural allocation");
        Action("이미지 예제");
        check(page.ImageExampleReady && page.View.Document.Images.Count == 3
            && page.Samples[0].Source.Contains("markdown-image.svg", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("memory:palette", StringComparison.Ordinal),
            "image example keeps real SVG, memory and unavailable-image behavior with its matching resolver source");
        ExportSources(output, "Markdown-images", Find("C# 예제 소스", UiAccessibilityRole.Edit).Node);
        Action("예제 복원");
        check(page.View.Document.Images.Count == 0 && page.Samples[0].Source.Contains("# 한글 Markdown", StringComparison.Ordinal),
            "restoring Markdown retires image references and restores the matching source document");
    }
    private static void RunControlsSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var content = new ControlsPage();
        using var page = new WidgetPage("Fluent", "상태를 공유하는 실제 컨트롤과 소스를 확인합니다.", content);
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 20000), new(1, 1)), idle, new(0, 0, 1280, 20000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        void Action(string name, UiAccessibilityRole role = UiAccessibilityRole.Button, UiAccessibilityAction action = UiAccessibilityAction.Invoke)
        {
            var target = Find(name, role);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, action, wait : false) == UiAccessibilityResult.Success,
                "Fluent sample actual control accepts : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).ToArray();
        check(content.Samples.Count == 7 + Enum.GetValues<UiTabListAppearance>().Length && sources.Length == content.Samples.Count,
            "Fluent counter,toggle,checkbox,radio,progress,link,Spinner and every tab appearance own distinct source blocks");
        var counter = Find("카운터 증가", UiAccessibilityRole.Button).Node;
        check(sources[0].Node.Bounds.Y - counter.Bounds.Y - counter.Bounds.Height is >= 0 and <= 24,
            "Fluent counter source immediately follows its live preview");
        var parses = content.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(content.Samples.Select(sample => sample.Metrics.SyntaxParses)) && !context.State.HasActiveAnimations,
            "idle Fluent previews retain all parsed sources and start without Spinner animation");
        Action("카운터 증가");
        check(content.ClickCount == 1 && content.Samples[0].Source.Contains("new UiState<int>(1)", StringComparison.Ordinal)
            && parses.Skip(1).SequenceEqual(content.Samples.Skip(1).Select(sample => sample.Metrics.SyntaxParses)),
            "counter action changes its own source and badge without reparsing unrelated examples");
        Action("토글 버튼과 값 공유", UiAccessibilityRole.Switch, UiAccessibilityAction.Toggle);
        check(Find("선택 유지", UiAccessibilityRole.Button).Node.ToggleState == UiAccessibilityToggleState.On
            && content.Samples[1].Source.Contains("new UiState<bool>(true)", StringComparison.Ordinal),
            "shared Switch and ToggleButton binding updates both preview states and their common source initializer");
        Action("혼합 상태에서 시작", UiAccessibilityRole.Checkbox, UiAccessibilityAction.Toggle);
        check(content.Samples[2].Source.Contains("new UiState<bool?>(true)", StringComparison.Ordinal),
            "mixed checkbox source reflects the actual nullable selection transition");
        Action("품질 우선", UiAccessibilityRole.RadioButton, UiAccessibilityAction.Select);
        check(content.Samples[3].Source.Contains("new UiState<string?>(\"quality\")", StringComparison.Ordinal)
            && content.Samples[3].Source.Contains("기본 선택입니다.", StringComparison.Ordinal),
            "radio source includes the live choice plus actual option descriptions and disabled setting");
        content.Value = .72f; Frame(); Frame();
        check(content.Samples[4].Source.Contains("new UiState<float>(0.72f)", StringComparison.Ordinal)
            && content.Samples[4].Source.Contains("progress.Size = new(460, 4)", StringComparison.Ordinal),
            "shared slider and fixed-size progress source follow the actual current value");
        Action("창 안에서 목적지 선택", UiAccessibilityRole.Link);
        check(content.Samples[5].Source.Contains("new UiState<string>(\"/observer\")", StringComparison.Ordinal),
            "internal link callback changes destination state and its corresponding source without external navigation");
        Action("Spinner 재생 전환");
        check(context.State.HasActiveAnimations && content.Samples[6].Source.Contains("Paused = false", StringComparison.Ordinal)
            && content.Samples[6].Source.Contains("spinner.Label = \"재생 중\"", StringComparison.Ordinal),
            "explicit Spinner playback starts animation and synchronizes its real paused state and label");
        parses = content.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        for (var tick = 0; tick < 10; tick++) Frame();
        check(parses.SequenceEqual(content.Samples.Select(sample => sample.Metrics.SyntaxParses)),
            "Spinner animation ticks preserve all parsed source blocks");
        Action("Spinner 재생 전환");
        for (var settle = 0; settle < 50; settle++) Frame();
        check(!context.State.HasActiveAnimations && context.State.GetScheduledFrameWaitMilliseconds(context.State.TimeSeconds) == Timeout.Infinite,
            "paused Fluent Spinner returns all inline examples to indefinite event wait");
        var group = Find("표현 비교 : SubtleCircular", UiAccessibilityRole.TabList).Node;
        var document = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "문서" && element.Node.ParentId == group.Id);
        check(tree.RequestAction(document.Node.Id, document.RuntimeId, UiAccessibilityAction.Select, wait : false) == UiAccessibilityResult.Success,
            "icon-only tab example accepts actual shared selection");
        Frame(); Frame();
        check(content.Samples.Skip(7).All(sample => sample.Source.Contains("new UiState<string?>(\"document\")", StringComparison.Ordinal)
            && sample.Source.Contains("UiTabIcon.Create<HomeTabIcon, HomeTabFilledIcon>()", StringComparison.Ordinal)
            && sample.Source.Contains("partial struct HomeTabIcon", StringComparison.Ordinal)),
            "all appearance snippets reflect shared tab selection and include their real regular/filled generated icon declarations");
    }
    private static void RunPrimarySvgSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new SvgPage(ObserverServices.CreateWindows());
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 900), new(1, 1)), input ?? idle, new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); page.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        void Range(string name, double value)
        {
            var target = Find(name, UiAccessibilityRole.Slider);
            check(tree.RequestRangeValue(target.Node.Id, target.RuntimeId, value, wait : false) == UiAccessibilityResult.Success,
                "SVG primary sample accepts its actual numeric range : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        var samples = page.PrimarySamples;
        var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).ToArray();
        check(samples.Count == 3 && sources.Length == 5 && sources.All(source => page.Samples.Any(sample => sample.Source == source.Node.Value)),
            "SVG primary image/vector,text and three icon adapter sources replace the whole-page footer");
        check(samples[0].Source.Contains("Fluent.Image(new UiImageSource(_decoder.Decode(_paths[0])))", StringComparison.Ordinal)
            && samples[0].Source.Contains("new UiSvg(_store.Load(_paths[0]))", StringComparison.Ordinal)
            && samples[0].Source.Contains("return (_store, example)", StringComparison.Ordinal),
            "SVG comparison source contains actual bitmap and vector factories and returns their resource owner with the preview");
        check(samples[1].Source.Count(character => character == '{') > 0
            && samples[1].Source.IndexOf("using Duxel.Svg;", StringComparison.Ordinal) == samples[1].Source.LastIndexOf("using Duxel.Svg;", StringComparison.Ordinal),
            "SVG source factory keeps its namespace import outside the function body");
        var before = page.Statistics!.Value;
        var parses = samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
        Frame(); Frame();
        check(parses.SequenceEqual(samples.Select(sample => sample.Metrics.SyntaxParses)), "idle primary SVG examples preserve parsed sources");
        Range("SVG 확대", 1.75); Range("SVG 회전", 45); Range("SVG 여백", 7); Range("이미지·SVG 모서리", 8);
        check(context.State.NextScheduledFrameTimeSeconds > context.State.TimeSeconds
            && parses.SequenceEqual(samples.Select(sample => sample.Metrics.SyntaxParses)),
            "rapid primary SVG transforms schedule one refresh without parsing intermediate sources");
        context.State.AdvanceTime(.2f);
        Frame();
        check(page.Preview is { Zoom : 1.75f, RotationDegrees : 45, CornerRadius : 8 }
            && samples[0].Source.Contains("vector.Zoom = 1.75f", StringComparison.Ordinal)
            && samples[0].Source.Contains("vector.RotationDegrees = 45f", StringComparison.Ordinal)
            && samples[0].Source.Contains("vector.Margin = new(7f, 0f, 0f, 0f)", StringComparison.Ordinal)
            && samples[0].Source.Contains("bitmap.CornerRadius = 8f", StringComparison.Ordinal),
            "primary SVG sources follow actual zoom,rotation,margin and shared corner options");
        var fit = Find("이미지 채우기", UiAccessibilityRole.Button);
        check(tree.RequestAction(fit.Node.Id, fit.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "SVG image-fit button accepts its real action");
        Frame(); Frame();
        check(page.Bitmap!.Fit == UiImageFit.Cover && samples[0].Source.Contains("bitmap.Fit = UiImageFit.Cover", StringComparison.Ordinal),
            "bitmap fit changes appear only in the matching image/vector source");
        var after = page.Statistics!.Value;
        check(before.XmlParses == after.XmlParses && before.SceneCompiles == after.SceneCompiles && before.Rasterizations == after.Rasterizations,
            "source synchronization and transforms reuse original parsed SVG scenes and explicit bitmap output");
        var feature = Find("문서 기능", UiAccessibilityRole.Tab);
        check(tree.RequestAction(feature.Node.Id, feature.RuntimeId, UiAccessibilityAction.Select, wait : false) == UiAccessibilityResult.Success,
            "primary SVG feature sample accepts actual tab selection");
        Frame(); Frame();
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "C# 예제 소스" && element.Node.Value == samples[2].Source)
            && samples[2].Source.Contains("_store.Load(_paths[4])", StringComparison.Ordinal),
            "feature tab displays its own compiled factory source immediately with the feature preview");
        var firstTab = Find("이미지·텍스트", UiAccessibilityRole.Tab);
        tree.RequestAction(firstTab.Node.Id, firstTab.RuntimeId, UiAccessibilityAction.Select, wait : false);
        Frame(); Frame();
        var reset = Find("SVG 변환 초기화", UiAccessibilityRole.Button);
        tree.RequestAction(reset.Node.Id, reset.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame(); Frame();
        var menu = Find("SVG 아이콘 메뉴", UiAccessibilityRole.Button);
        Console.WriteLine($"SVG menu before reveal : {menu.Node.Bounds}; offscreen : {menu.Node.Offscreen}; scroll : {menu.Node.CanScrollIntoView}");
        check(tree.RequestAction(menu.Node.Id, menu.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
            "SVG source content exposes its following menu through the actual enclosing scroll owner");
        for (var reveal = 0; reveal < 3; reveal++) Frame();
        menu = Find("SVG 아이콘 메뉴", UiAccessibilityRole.Button);
        Console.WriteLine($"SVG menu after reveal : {menu.Node.Bounds}; offscreen : {menu.Node.Offscreen}; enabled : {menu.Node.Enabled}");
        var point = new UiVector2(menu.Node.Bounds.X + menu.Node.Bounds.Width / 2, menu.Node.Bounds.Y + menu.Node.Bounds.Height / 2);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = point, LeftMouseReleased = true }); Frame();
        var menus = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name.Contains("SVG 아이콘", StringComparison.Ordinal)).Select(element => $"{element.Node.Name}:{element.Node.Role}:{element.Node.Bounds}:{element.Node.Offscreen}");
        Console.WriteLine("SVG menu after pointer : " + string.Join(" | ", menus));
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "SVG 아이콘 항목" && element.Node.Role == UiAccessibilityRole.MenuItem),
            "real pointer opens the SVG icon menu after scrolling past all primary sources");
    }
    private static void RunSvgBrowserSources(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new SvgPage(ObserverServices.CreateWindows());
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 900), new(1, 1)), idle, new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var drawing = context.GetDrawData();
        }
        void Select(string name)
        {
            var tab = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Tab);
            check(tree.RequestAction(tab.Node.Id, tab.RuntimeId, UiAccessibilityAction.Select, wait : false) == UiAccessibilityResult.Success,
                "SVG source gallery accepts actual tab selection : " + name);
            Frame(); Frame();
        }
        void Range(string name, double value)
        {
            var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Slider);
            if (target.Node.Offscreen)
            {
                tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false);
                Frame(); Frame();
                target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Id == target.Node.Id);
            }
            check(tree.RequestRangeValue(target.Node.Id, target.RuntimeId, value, wait : false) == UiAccessibilityResult.Success,
                "SVG browser source accepts real numeric setting : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        check(page.Samples.Count == 5 && !frame.Source!.Contains("class SvgPage", StringComparison.Ordinal)
            && page.Samples.Any(sample => sample.Source.Contains("CreateSvgIconTabs", StringComparison.Ordinal))
            && page.Samples.Any(sample => sample.Source.Contains("CreateSvgIconMenu", StringComparison.Ordinal))
            && page.Samples.Any(sample => sample.Source.Contains("CreateSvgIcon(", StringComparison.Ordinal)),
            "SVG default tab has five actual samples and three explicit icon sources without its obsolete whole-page path");
        var total = 0;
        foreach (var group in new[] { ("브라우저 비교", 2), ("클립·경계", 9), ("레이어·테두리", 5), ("테두리 범위", 5), ("호·곡선 비교", 6) })
        {
            Select(group.Item1);
            var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).ToArray();
            var browser = page.Samples.Where(sample => sample.Source.Contains("CreateBrowserComparison", StringComparison.Ordinal)).ToArray();
            total += browser.Length;
            check(browser.Length == group.Item2 && page.Samples.Count == group.Item2 + 3 && sources.Length == page.Samples.Count
                && sources.All(source => page.Samples.Any(sample => sample.Source == source.Node.Value)),
                "each browser pair owns one matching source in active group : " + group.Item1);
            check(browser.All(sample => sample.Source.Contains("class SvgBrowserPair", StringComparison.Ordinal)
                && sample.Source.Contains("class SvgBrowserReference", StringComparison.Ordinal)
                && sample.Source.Contains("SvgBrowserReference.Validate(sourcePath", StringComparison.Ordinal)
                && sample.Source.Contains("var directory = Path.Combine(AppContext.BaseDirectory", StringComparison.Ordinal)
                && !sample.Source.Contains(GallerySample.String(AppContext.BaseDirectory)[..^1], StringComparison.Ordinal)),
                "browser sources include actual compiled pair/validation code and portable asset paths : " + group.Item1);
            var statistics = page.Statistics;
            var parses = page.Samples.Select(sample => sample.Metrics.SyntaxParses).ToArray();
            Frame(); Frame();
            check(statistics == page.Statistics && parses.SequenceEqual(page.Samples.Select(sample => sample.Metrics.SyntaxParses)),
                "idle browser sources retain parsed syntax and prepared assets : " + group.Item1);
        }
        check(total == 27, "all27 browser comparison sources are exercised across their actual five tabs");
        Range("SVG 확대", 1.5); Range("SVG 회전", 45); Range("SVG 여백", 9); Range("이미지·SVG 모서리", 6);
        DrainSvgScheduledFrames(context, Frame);
        var arcSources = page.Samples.Where(sample => sample.Source.Contains("CreateBrowserComparison", StringComparison.Ordinal)).ToArray();
        check(arcSources.All(sample => sample.Source.Contains("vector.Zoom = 1.5f", StringComparison.Ordinal)
            && sample.Source.Contains("vector.RotationDegrees = 45f", StringComparison.Ordinal)
            && sample.Source.Contains("vector.Margin = new(9f, 0f, 0f, 0f)", StringComparison.Ordinal)
            && sample.Source.Contains("vector.CornerRadius = 6f", StringComparison.Ordinal)),
            "all active arc sources follow current transforms without unresolved page variables");
        var toggle = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "흰색 배경을 SVG에 포함" && element.Node.Role == UiAccessibilityRole.Switch);
        tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false);
        Frame(); Frame();
        toggle = tree.CaptureSnapshot().Elements.Single(element => element.Node.Id == toggle.Node.Id);
        check(tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Toggle, wait : false) == UiAccessibilityResult.Success,
            "browser background option accepts its actual visible action");
        Frame(); Frame();
        DrainSvgScheduledFrames(context, Frame);
        check(arcSources.All(sample => sample.Source.Contains("bool _includeBackground = true", StringComparison.Ordinal)),
            "browser source background binding tracks the actual shared toggle");
    }

    internal static void DrainSvgScheduledFrames(UiContext context, Action renderFrame)
    {
        // A settled preview is presented before the source examples are rebuilt in batches.
        // Follow actual requests with a finite limit so a scheduling loop still fails the test.
        for (var frame = 0; frame < 8 && context.State.NextScheduledFrameTimeSeconds is { } deadline; frame++)
        {
            context.State.AdvanceTime((float)Math.Max(0, deadline - context.State.TimeSeconds));
            renderFrame();
        }
        if (context.State.NextScheduledFrameTimeSeconds is not null)
            throw new InvalidOperationException("The SVG gallery continued to schedule frames after settling.");
    }
    internal static void RunComponentSources(Action<bool, string> check, string? output = null, string? requestedPages = null)
    {
        var snapshots = new List<(string Page, string Phase, string Source)>();
        var unchangedBytes = 0L;
        var requested = requestedPages?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var completedPages = new List<string>();
        foreach (var example in GalleryComponentExamples.Create())
        {
            using var page = (ComponentExamplePage)example;
            if (requested is not null && !requested.Contains(page.Title)) continue;
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            using var frame = new GalleryPageFrame(page);
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Frame()
            {
                context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)), idle, new(0, 0, 1280, 6000), UiTextSettings.Default);
                context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
                using var data = context.GetDrawData();
            }
            UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
                .Single(element => element.Node.Name == name && element.Node.Role == role);
            void Send(string name, UiAccessibilityRole role, UiAccessibilityAction action, string? value = null)
            {
                var target = Find(name, role);
                check(tree.RequestAction(target.Node.Id, target.RuntimeId, action, value, wait : false) == UiAccessibilityResult.Success,
                    "component example accepts actual action : " + page.Title + "/" + name);
                Frame(); Frame(); Frame();
            }
            void Snapshot(string phase)
            {
                var sources = tree.CaptureSnapshot().Elements.Select(element => element.Node)
                    .Where(node => node is { Name: "C# 예제 소스", Role: UiAccessibilityRole.Edit })
                    .OrderBy(node => node.Bounds.Y).ToArray();
                check(sources.Length == page.Samples.Count, "one code view per example : " + page.Title);
                for (var index = 0; index < Math.Min(sources.Length, page.Samples.Count); index++)
                {
                    var code = sources[index];
                    var source = page.Samples[index].Source;
                    check(code.ReadOnly && code.Value == source
                        && source.Contains("Fluent." + page.Title + "(", StringComparison.Ordinal)
                        && !source.Contains("using Duxel.Core;", StringComparison.Ordinal),
                        "component inline code shows its actual factory and current source : " + page.Title + "/" + phase);
                    snapshots.Add((page.Title, index == 0 ? phase : phase + "-" + (index + 1), source));
                }
            }
            Frame(); Frame();
            check(page.Samples.Count == (page.Title == "RadioGroup" ? 2 : 1) && ((IGallerySourcePage)page).HasInlineSamples,
                "component owns its inline samples : " + page.Title);
            Snapshot("initial");
            const string text = "디모이 \"Q\" \\path";
            string? expected = null;
            switch (page.Title)
            {
                case "TagPicker":
                    Send("받는 사람", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
                    Send("김민수", UiAccessibilityRole.Option, UiAccessibilityAction.AddToSelection);
                    check(page.Source.Contains("new UiState<string[]>([\"dimoi\", \"kim\"])", StringComparison.Ordinal),
                        "TagPicker source follows actual multi-selection");
                    Snapshot("selected");
                    Send("받는 사람", UiAccessibilityRole.ComboBox, UiAccessibilityAction.SetValue, "박");
                    check(page.Source.Contains("new UiState<string>(\"박\")", StringComparison.Ordinal),
                        "TagPicker query and selected array both remain current");
                    Send("받는 사람", UiAccessibilityRole.ComboBox, UiAccessibilityAction.SetValue, string.Empty);
                    Send("받는 사람", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
                    Send("제거 김민수", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke);
                    expected = "new UiState<string[]>([\"dimoi\"])";
                    break;
                case "Button":
                    Send("카운터 증가", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke);
                    expected = "new UiState<int>(1)";
                    break;
                case "Checkbox":
                    Send("혼합 상태에서 시작", UiAccessibilityRole.Checkbox, UiAccessibilityAction.Toggle);
                    expected = "new UiState<bool?>(" + GallerySample.Boolean(Find("혼합 상태에서 시작", UiAccessibilityRole.Checkbox).Node.Checked!.Value) + ")";
                    break;
                case "Switch":
                    Send("기능 사용", UiAccessibilityRole.Switch, UiAccessibilityAction.Toggle);
                    expected = "new UiState<bool>(true)";
                    break;
                case "RadioGroup":
                    Send("품질 우선", UiAccessibilityRole.RadioButton, UiAccessibilityAction.Select);
                    expected = "new UiState<string?>(\"quality\")";
                    const string optionLabel = "시간이 더 걸려도 결과의 품질을 높입니다.";
                    var narrow = Find(optionLabel, UiAccessibilityRole.RadioButton).Node.Bounds;
                    Send(optionLabel, UiAccessibilityRole.RadioButton, UiAccessibilityAction.Select);
                    Send("부모 폭 전환", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke);
                    var wide = Find(optionLabel, UiAccessibilityRole.RadioButton).Node.Bounds;
                    check(wide.Width > narrow.Width && wide.Height < narrow.Height,
                        "the allocated RadioGroup option uses one line after widening its parent");
                    var allocationSource = page.Samples[1].Source;
                    check(allocationSource.Contains("new UiState<float>(360f)", StringComparison.Ordinal)
                        && allocationSource.Contains("new UiState<string?>(\"quality\")", StringComparison.Ordinal),
                        "allocation sample source tracks the width and preserves the selected option");
                    break;
                case "Slider":
                    var slider = Find("값 조절", UiAccessibilityRole.Slider);
                    check(tree.RequestRangeValue(slider.Node.Id, slider.RuntimeId, .75, wait : false) == UiAccessibilityResult.Success,
                        "component Slider accepts its actual numeric change");
                    Frame(); Frame(); Frame();
                    expected = "new UiState<float>(0.75f)";
                    break;
                case "Link":
                    Send("목적지 선택", UiAccessibilityRole.Link, UiAccessibilityAction.Invoke);
                    expected = "new UiState<string>(\"/gallery\")";
                    break;
                case "Spinner":
                    Send("재생 전환", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke);
                    expected = "new UiState<bool>(false)";
                    break;
                case "Input":
                case "Textarea":
                case "Field":
                case "Label":
                    var inputName = page.Title switch { "Input" => "한 줄 입력", "Textarea" => "여러 줄 입력", "Field" => "이름", _ => "눌러서 입력창으로 이동" };
                    var inputText = page.Title == "Textarea" ? text + "\n둘째 줄" : text;
                    Send(inputName, UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, inputText);
                    expected = "new UiState<string>(" + GallerySample.String(inputText) + ")";
                    break;
                case "Dropdown":
                    Send("작업 위치", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
                    Send("日本", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
                    expected = "new UiState<string?>(\"jp\")";
                    break;
                case "Combobox":
                    Send("도시 검색", UiAccessibilityRole.ComboBox, UiAccessibilityAction.SetValue, "부");
                    Snapshot("query");
                    check(page.Source.Contains("new UiState<string>(\"부\")", StringComparison.Ordinal),
                        "Combobox source follows free-form query before selection");
                    Send("도시 검색", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
                    Send("부산", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
                    check(page.Source.Contains("new UiState<string>(\"부산\")", StringComparison.Ordinal),
                        "Combobox completion updates its query initializer");
                    expected = "new UiState<string?>(\"busan\")";
                    break;
                case "TabList":
                    Send("세 번째", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
                    expected = "new UiState<string?>(\"third\")";
                    break;
                case "Menu":
                    Send("메뉴 열기", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke);
                    Send("새 문서", UiAccessibilityRole.MenuItem, UiAccessibilityAction.Invoke);
                    expected = "new UiState<string>(\"new\")";
                    break;
            }
            if (expected is not null)
            {
                check(page.Source.Contains(expected, StringComparison.Ordinal),
                    "component source reproduces its actual current value : " + page.Title);
                Snapshot("changed");
            }
            var stable = page.Source;
            var parses = page.Samples[0].Metrics.SyntaxParses;
            Frame(); Frame();
            check(ReferenceEquals(stable, page.Source) && parses == page.Samples[0].Metrics.SyntaxParses,
                "unchanged component source reuses its string and syntax : " + page.Title);
            var sample = page.Samples[0];
            for (var i = 0; i < 4; i++) sample.SynchronizeSource();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 128; i++) sample.SynchronizeSource();
            unchangedBytes += GC.GetAllocatedBytesForCurrentThread() - before;
            completedPages.Add(page.Title);
            if (output is not null)
            {
                var progressFolder = Path.Combine(output, "sources");
                Directory.CreateDirectory(progressFolder);
                foreach (var item in snapshots.Where(item => item.Page == page.Title))
                    File.WriteAllText(Path.Combine(progressFolder, item.Page + "-" + item.Phase + ".cs.txt"), item.Source);
                File.WriteAllLines(Path.Combine(output, "completed-pages.txt"), completedPages);
            }
        }
        if (requested is not null && !requested.SetEquals(completedPages))
            throw new ArgumentException("Unknown Gallery component selection : " + requestedPages, nameof(requestedPages));
        check(snapshots.Select(item => item.Page).Distinct().Count() == completedPages.Count
            && (requested is not null || completedPages.Count == 22),
            "component source audit covers every requested standalone factory");
        check(unchangedBytes == 0, "verified unchanged component source bindings allocate0B across128 repetitions");
        var allBindingsBytes = 0L;
        foreach (var example in GalleryComponentExamples.Create())
        {
            using var page = (ComponentExamplePage)example;
            var sample = page.Samples[0];
            for (var i = 0; i < 4; i++) sample.SynchronizeSource();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 128; i++) sample.SynchronizeSource();
            allBindingsBytes += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        check(allBindingsBytes == 0, "all22 unchanged source bindings allocate0B without repeating UI actions");
        if (output is not null)
        {
            var folder = Path.Combine(output, "sources");
            Directory.CreateDirectory(folder);
            foreach (var item in snapshots) File.WriteAllText(Path.Combine(folder, item.Page + "-" + item.Phase + ".cs.txt"), item.Source);
        }
    }

    internal static void RunLegacySources(Action<bool, string> check)
    {
        CheckLegacyMenuIndicator(check);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        using var wake = new ManualResetEventSlim();
        context.SetAccessibilityTree(tree);
        tree.SetRequestFrameCallback(wake.Set);
        using var page = new LegacyPage();
        using var frame = new GalleryPageFrame(page);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var trace = false;
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 900), new(1, 1)), idle, new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var draw = context.GetDrawData();
            if (trace)
            {
                var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "Scale");
                for (var current = target; ;)
                {
                    Console.WriteLine($"Legacy reveal trace : {current.Node.Name}/{current.Node.Role}; bounds : {current.Node.Bounds}; scroll : {current.Node.Scroll}");
                    if (!tree.TryGet(current.Node.ParentId, out current)) break;
                }
            }
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
            .Single(element => element.Node.Name == name && element.Node.Role == role);
        UiAccessibilityResult Execute(Func<UiAccessibilityResult> command)
        {
            wake.Reset();
            var task = Task.Run(command);
            if (WaitHandle.WaitAny([wake.WaitHandle, ((IAsyncResult)task).AsyncWaitHandle], 1000) == WaitHandle.WaitTimeout)
                throw new TimeoutException("Legacy source request was neither queued nor rejected.");
            for (var i = 0; i < 8 && !task.IsCompleted; i++) Frame();
            if (!task.Wait(1000)) throw new TimeoutException("Legacy source request did not complete after publication.");
            return task.Result;
        }
        void Reveal(string name, UiAccessibilityRole role)
        {
            var target = Find(name, role);
            trace = name == "Scale";
            check(Execute(() => tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView)) == UiAccessibilityResult.Success,
                "legacy target reveals through its actual child scroll owner : " + name);
            Frame(); Frame(); Frame();
            trace = false;
            check(!Find(name, role).Node.Offscreen, "legacy target is visible after its scroll publication : " + name);
        }
        void Action(string name, UiAccessibilityRole role, UiAccessibilityAction action)
        {
            Reveal(name, role);
            var target = Find(name, role);
            var result = Execute(() => tree.RequestAction(target.Node.Id, target.RuntimeId, action));
            Console.WriteLine($"Legacy action {name} : {result}; offscreen : {target.Node.Offscreen}; bounds : {target.Node.Bounds}");
            check(result == UiAccessibilityResult.Success,
                "legacy sample accepts its visible action : " + name);
            Frame(); Frame();
        }
        Frame(); Frame();
        check(page.Samples.Count == 1 && page.Samples[0].Source.Contains("ui.SliderFloat(\"float\"", StringComparison.Ordinal)
            && !page.Samples[0].Source.Contains("SampleScreen", StringComparison.Ordinal),
            "Hello owns its actual captured callback rather than a whole-page wrapper");
        var code = Find("C# 예제 소스", UiAccessibilityRole.Edit).Node;
        var disabled = Find("Disabled Button", UiAccessibilityRole.Button).Node;
        check(code.ReadOnly && code.Bounds.Y - disabled.Bounds.Y - disabled.Bounds.Height is >= 0 and <= 24,
            "legacy Hello source follows its actual controls at ordinary spacing in a finite viewport");
        Action("Button", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke);
        check(page.Samples[0].Source.Contains("int _counter = 1;", StringComparison.Ordinal),
            "the Hello source records the actual incremented counter initializer");
        var helloSource = page.Samples[0].Source;
        var parses = page.Samples[0].Metrics.SyntaxParses;
        Frame(); Frame();
        check(ReferenceEquals(helloSource, page.Samples[0].Source) && page.Samples[0].Metrics.SyntaxParses == parses,
            "unchanged legacy state reuses its source string and parsed code");
        Action("Immediate", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
        check(page.Samples.Count == 8 && page.Samples.Select(sample => sample.Source).Count(source => source.Contains("void Render(", StringComparison.Ordinal)) == 8,
            "Immediate owns separate Menu,Basics,Sliders,Inputs,Drag,Color,Lists and Selection sources");
        check(page.CreatedSources.Single(block => block.Title == "Menu").Source.Contains("bool UseEmbeddedMenuBar = true;", StringComparison.Ordinal),
            "menu source includes its actual embedded-menu setting");
        Reveal("Scale", UiAccessibilityRole.Slider);
        var scale = Find("Scale", UiAccessibilityRole.Slider);
        var panel = Find("Immediate", UiAccessibilityRole.TabPanel).Node;
        check(!panel.Offscreen && panel.Bounds.Width > 0 && panel.Bounds.Height > 0
            && panel.Bounds.X <= scale.Node.Bounds.X && panel.Bounds.Y <= scale.Node.Bounds.Y
            && panel.Bounds.X + panel.Bounds.Width >= scale.Node.Bounds.X + scale.Node.Bounds.Width
            && panel.Bounds.Y + panel.Bounds.Height >= scale.Node.Bounds.Y + scale.Node.Bounds.Height,
            "deeply scrolled legacy panel contains its visible Scale descendant on all four sides");
        var scaleResult = Execute(() => tree.RequestRangeValue(scale.Node.Id, scale.RuntimeId, .75));
        Console.WriteLine($"Legacy Scale : {scaleResult}; offscreen : {scale.Node.Offscreen}; bounds : {scale.Node.Bounds}");
        check(scaleResult == UiAccessibilityResult.Success,
            "legacy Scale accepts its actual numeric update after reveal");
        Frame(); Frame();
        var sliderSource = page.CreatedSources.Single(block => block.Title == "Sliders").Source;
        var actualScale = (float)Find("Scale", UiAccessibilityRole.Slider).Node.Range!.Value.Value;
        Console.WriteLine($"Legacy current Scale : {actualScale:R}");
        check(MathF.Abs(actualScale - .75f) < .000001f
            && sliderSource.Contains("float _scale = " + ImmediateSampleSection.Number(actualScale) + ";", StringComparison.Ordinal)
            && sliderSource.Contains("ui.SliderScalarN(", StringComparison.Ordinal)
            && sliderSource.Contains("float[] _scalarFloats = [", StringComparison.Ordinal),
            "slider source follows current scalar state and includes its array initializer");
        Action("Widget Gallery", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
        check(page.Samples.Count == 3 && page.CreatedSources.Count == 12,
            "all twelve actual legacy sections have independent reusable code widgets");
        check(page.Samples.Any(sample => sample.Source.Contains("ui.BeginTable(", StringComparison.Ordinal)
                && sample.Source.Contains("bool[] _tableEnabled = [", StringComparison.Ordinal))
            && page.Samples.Any(sample => sample.Source.Contains("ui.BeginPopupModal(", StringComparison.Ordinal)),
            "table and popup source blocks retain the real editable array and popup implementations");
        var firstRow = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Checkbox
            && element.Node.Id.Contains("##row0", StringComparison.Ordinal));
        var popupSource = page.CreatedSources.Single(block => block.Title == "Popups/Tooltips").Source;
        check(Execute(() => tree.RequestAction(firstRow.Node.Id, firstRow.RuntimeId, UiAccessibilityAction.ScrollIntoView)) == UiAccessibilityResult.Success,
            "legacy editable table row accepts reveal through the child viewport");
        Frame(); Frame(); Frame();
        firstRow = tree.CaptureSnapshot().Elements.Single(element => element.Node.Id == firstRow.Node.Id);
        check(!firstRow.Node.Offscreen && Execute(() => tree.RequestAction(firstRow.Node.Id, firstRow.RuntimeId, UiAccessibilityAction.Toggle)) == UiAccessibilityResult.Success,
            "legacy editable table row accepts its actual visible toggle");
        Frame(); Frame();
        Console.WriteLine("Legacy table source state : " + page.CreatedSources.Single(block => block.Title == "Table").Source.Split('\n')[1]);
        Console.WriteLine("Legacy actual table state : " + tree.CaptureSnapshot().Elements.Single(element => element.Node.Id == firstRow.Node.Id).Node.Checked);
        check(page.CreatedSources.Single(block => block.Title == "Table").Source.Contains("bool[] _tableEnabled = [true, false", StringComparison.Ordinal)
            && ReferenceEquals(popupSource, page.CreatedSources.Single(block => block.Title == "Popups/Tooltips").Source),
            "legacy array changes update the matching source without rebuilding unrelated popup source");
        Action("Hello", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
        check(page.Samples.Count == 1 && page.Samples[0].Source == helloSource,
            "switching legacy tabs restores the original source and caller state");
        check(page.CreatedSources.All(block => !block.Source.Contains("using Duxel.Core;", StringComparison.Ordinal)
            && !block.Code.Options.ShowToolbar && block.Code.Options.MaximumVisibleLines is null),
            "every legacy code widget keeps full source,hidden toolbar and omitted common import");
        var sourceBlocks = page.CreatedSources.ToArray();
        for (var i = 0; i < 2; i++)
            foreach (var block in sourceBlocks) block.SynchronizeSource();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 128; i++)
            foreach (var block in sourceBlocks) block.SynchronizeSource();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"Legacy twelve-section unchanged source synchronization : {allocated}B / 128 repetitions");
        check(allocated == 0, "all twelve legacy source bindings allocate no managed memory when unchanged");
    }
    private static void CheckLegacyMenuIndicator(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetDirectTextPrimaryFontPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf"));
        context.SetDirectTextFallbackEnabled(false);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        (UiVector2 Size, int Indices) Draw(bool selected, int width)
        {
            UiVector2 size = default;
            context.NewFrame(new(.016f, new(width, 240), new(1, 1)), idle, new(0, 0, width, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.MenuItem("Open", selected);
                size = ui.GetItemRectSize();
            }));
            using var frame = context.GetDrawData();
            return (size, frame.DrawLists.Sum(list => list.Indices.Count));
        }
        var baseline = Draw(false, 320);
        var maximum = Draw(true, 640);
        var restored = Draw(false, 320);
        check(baseline.Size == maximum.Size && maximum.Indices > baseline.Indices,
            "legacy menu keeps the same indicator slot and paints selected geometry with DirectWrite");
        check(baseline == restored, "legacy menu renders again after restoring the viewport and clearing selection");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
