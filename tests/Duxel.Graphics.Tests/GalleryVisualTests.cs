using Duxel.Core;
using Duxel.Gallery;

internal static class GalleryVisualTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        NavSectionHeaderTests.RunGpu(Path.Combine(output, "nav-sections"), check);
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1280, 900));
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true };
        using var tree = new UiAccessibilityTree();
        fixture.Context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
        fixture.Context.State.VSync = false;
        fixture.Context.SetAccessibilityTree(tree);
        var clipboard = new CodeClipboard();
        fixture.Context.SetClipboard(clipboard);
        var adornmentPreservesCursor = false;
        using (var adornment = fixture.Build(ui =>
        {
            ui.SetCursorScreenPos(new(10, 10));
            ui.Button("Badge anchor");
            var cursor = ui.GetCursorScreenPos();
            new UiControlContext(ui).PaintFluentBadge(new(180, 10, 50, 16), new("미구현"),
                new(UiBadgeSize.Small, Appearance: UiBadgeAppearance.Tint, Color: UiBadgeColor.Informative));
            adornmentPreservesCursor = ui.GetCursorScreenPos() == cursor;
        }))
            fixture.Render(adornment);
        check(adornmentPreservesCursor, "common badge adornment paints in supplied bounds without advancing its parent cursor");
        void Draw(UiImmediateContext ui)
        {
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
            gallery.Render(ui);
        }
        void RevealBodyTarget(string name, UiAccessibilityRole role)
        {
            var inspectHeader = name == "그룹 합성 예제" && role == UiAccessibilityRole.Pane;
            var headerBefore = inspectHeader ? fixture.Capture(Draw) : null;
            var headerBounds = inspectHeader ? tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "갤러리 상태와 옵션" && element.Node.Role == UiAccessibilityRole.Pane).Node.Bounds : default;
            var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
            if (tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Gallery capture could not reveal its body target : " + name);
            for (var revealFrame = 0; revealFrame < 3; revealFrame++) fixture.Capture(Draw);
            check(!tree.CaptureSnapshot().Elements.Single(element => element.Node.Id == target.Node.Id).Node.Offscreen,
                "gallery GPU capture reveals its actual body target : " + name);
            if (inspectHeader)
            {
                var headerAfter = fixture.Capture(Draw);
                var afterBounds = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "갤러리 상태와 옵션" && element.Node.Role == UiAccessibilityRole.Pane).Node.Bounds;
                Console.WriteLine($"Gallery sequence header : {headerBounds} -> {afterBounds}");
                var width = (int)fixture.FrameInfo.DisplaySize.X;
                var height = (int)fixture.FrameInfo.DisplaySize.Y;
                BrowserPng.Write(Path.Combine(output, "gallery-header-sequence-before.png"), width, height, headerBefore!);
                BrowserPng.Write(Path.Combine(output, "gallery-header-sequence-after.png"), width, height, headerAfter);
                var equal = true;
                for (var y = 0; y < 54; y++)
                    equal &= headerBefore!.AsSpan((y * width + 252) * 4, (width - 252) * 4).SequenceEqual(headerAfter.AsSpan((y * width + 252) * 4, (width - 252) * 4));
                check(headerBounds == afterBounds, "Gallery sequence preserves pinned header bounds during lower-preview reveal");
                check(equal, "Gallery sequence preserves pinned header pixels during lower-preview reveal");
            }
        }
        void Capture(string page, string name, string? revealName = null, UiAccessibilityRole revealRole = UiAccessibilityRole.Button)
        {
            gallery.SelectedPage = page;
            var label = gallery.SelectedNavigationLabel;
            fixture.Capture(Draw);
            var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Link && element.Node.Name == label);
            if (target.Node.Offscreen)
            {
                if (tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Gallery capture could not reveal the selected navigation item.");
                fixture.Capture(Draw);
            }
            if (revealName is not null) RevealBodyTarget(revealName, revealRole);
            var pixels = fixture.Capture(Draw);
            BrowserPng.Write(Path.Combine(output, name + ".png"), (int)fixture.FrameInfo.DisplaySize.X,
                (int)fixture.FrameInfo.DisplaySize.Y, pixels);
            var snapshot = tree.CaptureSnapshot();
            var tab = snapshot.Elements.Single(element => element.Node.Role == UiAccessibilityRole.Link && element.Node.Name == label).Node;
            var panel = snapshot.Elements.Single(element => element.Node.Role == UiAccessibilityRole.Pane && element.Node.Name == label).Node;
            check(tab.IsCurrent == true && panel.Bounds.X > 200 && panel.LabeledBy == tab.Id,
                "gallery actual GPU capture has left navigation and its associated content : " + page);
        }
        foreach (var example in GalleryComponentExamples.Create())
        {
            using (example)
            {
                var source = GalleryPageSource.For(example);
                check(source.Contains("Fluent." + example.Title + "(", StringComparison.Ordinal)
                    && source.Contains("UiWidget example = create();", StringComparison.Ordinal),
                    "standalone Gallery source is captured from its compiled factory : " + example.Title);
            }
        }
        Capture("개요", "gallery-overview");
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Edit && element.Node.Name == "C# 예제 소스"),
            "Overview presents gallery categories without an unrelated source listing");
        Capture("탐색 메뉴", "gallery-page-header-nav");
        var navHeader = tree.CaptureSnapshot().Elements;
        check(navHeader.Any(element => element.Node.Role == UiAccessibilityRole.Label && element.Node.Name == "Nav")
            && !navHeader.Any(element => element.Node.Role == UiAccessibilityRole.Label && element.Node.Name == "탐색 메뉴"),
            "the visible Nav page uses its sidebar label as the only page heading");
        Capture("RadioGroup", "gallery-page-header-radio");
        var radioHeader = tree.CaptureSnapshot().Elements;
        var pageDescription = radioHeader.Single(element => element.Node.Role == UiAccessibilityRole.Label
            && element.Node.Name == gallery.DescriptionForPage("RadioGroup")).Node.Bounds;
        var pageTitle = radioHeader.Single(element => element.Node.Role == UiAccessibilityRole.Label
            && element.Node.Name == "RadioGroup").Node.Bounds;
        var radioExample = radioHeader.Single(element => element.Node.Role == UiAccessibilityRole.RadioGroup
            && element.Node.Name == "처리 방식").Node.Bounds;
        check(pageDescription.Y - pageTitle.Y - pageTitle.Height < radioExample.Y - pageDescription.Y - pageDescription.Height,
            "the page header stays compact and the example starts after a larger gap");
        Capture("보조 패널", "gallery-drawer-source-order");
        var drawerNodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
        var overlayCode = drawerNodes.Single(node => node.Role == UiAccessibilityRole.Edit && node.Name == "C# 예제 소스"
            && node.Value!.Contains("UiDrawerType.Overlay", StringComparison.Ordinal));
        var drawerSettings = drawerNodes.Where(node => !node.Offscreen && node.Name is "패널 위치 변경" or "패널 크기 변경"
            or "패널 모달" or "패널 오른쪽에서 왼쪽" or "보조 패널 열기").ToArray();
        var overlayOrdered = drawerSettings.Length == 5 && !overlayCode.Offscreen
            && drawerSettings.All(node => node.Bounds.Y + node.Bounds.Height <= overlayCode.Bounds.Y);
        Capture("보조 패널", "gallery-drawer-inline-source", "인라인 패널 표시", UiAccessibilityRole.Switch);
        var inlineNodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
        var inlineToggle = inlineNodes.Single(node => node.Role == UiAccessibilityRole.Switch && node.Name == "인라인 패널 표시");
        var inlineCode = inlineNodes.Single(node => node.Role == UiAccessibilityRole.Edit && node.Name == "C# 예제 소스"
            && node.Value!.Contains("UiDrawerType.Inline", StringComparison.Ordinal));
        check(overlayOrdered && !inlineToggle.Offscreen
            && (inlineCode.Offscreen ? inlineCode.CanScrollIntoView : inlineCode.Bounds.Y >= inlineToggle.Bounds.Y + inlineToggle.Bounds.Height),
            "Drawer shows overlay controls before its source and follows with a separate inline example and source");
        Capture("개요", "gallery-overview-header");
        var headerNodes = tree.CaptureSnapshot().Elements;
        var navigationRoot = headerNodes.Single(element => element.Node.Role == UiAccessibilityRole.Navigation && element.Node.Name == "갤러리 탐색").Node.Id;
        foreach (var label in new[] { "Fluent 컨트롤", "Duxel 위젯", "통합·개발 도구" })
        {
            var section = headerNodes.Single(element => element.Node.ParentId == navigationRoot && element.Node.Name == label).Node;
            check(section.Role == UiAccessibilityRole.Label && !section.Focusable && !section.SupportsInvoke && section.Expansion is null,
                "Gallery uses a passive common section header rather than a category button : " + label);
        }
        var brand = headerNodes.Single(element => element.Node.Name == "Duxel 갤러리" && element.Node.Role == UiAccessibilityRole.Label).Node;
        var theme = headerNodes.Single(element => element.Node.Name == "밝은 테마" && element.Node.Role == UiAccessibilityRole.Switch).Node;
        check(headerNodes.Any(element => element.Node.Name == "Duxel 로고" && !element.Node.Offscreen),
            "Navigation header renders the existing Duxel vector logo without a raster substitute");
        var logoBounds = headerNodes.Single(element => element.Node.Name == "Duxel 로고").Node.Bounds;
        var logoPixels = fixture.Capture(Draw);
        byte[] LogoPixel(float x, float y)
        {
            var column = (int)(logoBounds.X + (x - 50) * logoBounds.Width / 180);
            var row = (int)(logoBounds.Y + (y - 40) * logoBounds.Height / 180);
            var offset = (row * (int)fixture.FrameInfo.DisplaySize.X + column) * 4;
            return logoPixels.AsSpan(offset, 4).ToArray();
        }
        var backPlate = LogoPixel(90, 57);
        var logoCounter = LogoPixel(150, 145);
        var stem = LogoPixel(120, 135);
        check(backPlate[0] is > 80 and < 190 && backPlate[1] is > 80 and < 190,
            "Gallery logo back plate composites at half opacity on the actual dark Vulkan surface");
        check(stem[0] > 235 && logoCounter[0] < stem[0] - 25 && logoCounter[1] < stem[1] - 25,
            "Gallery D monogram has a transparent counter revealing its underlying violet layer");
        var status = headerNodes.Single(element => element.Node.Name == "갤러리 상태와 옵션" && element.Node.Role == UiAccessibilityRole.Pane).Node;
        var contentPanel = headerNodes.Single(element => element.Node.Name == "개요" && element.Node.Role == UiAccessibilityRole.Pane).Node;
        check(brand.Bounds.X < 120 && brand.Bounds.Y < 40 && theme.Bounds.Y < 40 && theme.Bounds.X > 900
            && status.Bounds.X == contentPanel.Bounds.X && status.Bounds.X > brand.Bounds.X + brand.Bounds.Width,
            "Sidebar branding and content options occupy independent top-aligned headers");
        Capture("NavigationBar", "gallery-latest-new");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(9));
        var newMenus = tree.CaptureSnapshot().Elements.Where(element => element.Node.Role == UiAccessibilityRole.Link
                && element.Node.ItemStatus == "NEW").Select(element => element.Node.Name).ToHashSet(StringComparer.Ordinal);
        check(newMenus.SetEquals(new[] { "NavigationBar", "DataGrid" }.Where(menu => GalleryNavigation.IsNewMenu(menu, today))),
            "implemented Gallery destinations display NEW during their registration week");
        check(!GalleryNavigation.IsNewMenu("DataGrid", new(2026, 11, 20))
            && GalleryNavigation.IsNewMenu("DataGrid", new(2026, 11, 21))
            && GalleryNavigation.IsNewMenu("DataGrid", new(2026, 11, 27))
            && !GalleryNavigation.IsNewMenu("DataGrid", new(2026, 11, 28)),
            "DataGrid NEW starts on registration day and expires seven days later");
        Capture("ActivityIndicator", "gallery-activity-sources");
        check(tree.CaptureSnapshot().Elements.Count(element => element.Node.Name == "C# 예제 소스"
            && element.Node.Role == UiAccessibilityRole.Edit) == 5,
            "ActivityIndicator owns adjacent source for each size example");
        Capture("TagPicker", "gallery-tag-picker-before-selection");
        void TagPickerClick(UiRect bounds)
        {
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            fixture.Capture(Draw, new(point, true, false, true, false, false, false, 0, 0, [], [], default, default));
            fixture.Capture(Draw, new(point, false, false, false, true, false, false, 0, 0, [], [], default, default));
            fixture.Capture(Draw);
        }
        var tagPickerArrow = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Id.EndsWith("##expand", StringComparison.Ordinal));
        TagPickerClick(tagPickerArrow.Node.Bounds);
        var kimOption = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Option && element.Node.Name == "김민수");
        check(!kimOption.Node.Offscreen, "actual Gallery TagPicker pointer opens the Kim suggestion");
        TagPickerClick(kimOption.Node.Bounds);
        Capture("TagPicker", "gallery-tag-picker-selected");
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "제거 김민수"), "actual GPU TagPicker selection creates its dismissible Kim tag without crashing");
        check(tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Edit
            && element.Node.Name == "C# 예제 소스").Node.Value!.Contains("new UiState<string[]>([\"dimoi\", \"kim\"])", StringComparison.Ordinal),
            "actual GPU TagPicker selection updates the source displayed directly below it");
        Capture("경로", "gallery-breadcrumb-source");
        var breadcrumbNodes = tree.CaptureSnapshot().Elements;
        var breadcrumbCode = breadcrumbNodes.Single(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Node;
        check(breadcrumbCode.Bounds.Y < 750,
            "Short Breadcrumb content places its source immediately below the example instead of after a viewport-sized gap");
        Capture("기존 API", "gallery-legacy-hello-source");
        var helloCode = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "C# 예제 소스").Node;
        var helloEnd = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "Disabled Button").Node;
        check(helloCode.Value!.Contains("ui.SliderFloat(\"float\"", StringComparison.Ordinal)
            && helloCode.Bounds.Y - helloEnd.Bounds.Y - helloEnd.Bounds.Height is >= 0 and <= 24,
            "actual GPU Hello source immediately follows its real controls");
        void LegacyTab(string name)
        {
            RevealBodyTarget(name, UiAccessibilityRole.Tab);
            var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Tab);
            if (tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Select, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Legacy GPU tab selection failed : " + name);
            for (var frame = 0; frame < 3; frame++) fixture.Capture(Draw);
        }
        LegacyTab("Immediate");
        Capture("기존 API", "gallery-legacy-immediate-source", "Scale", UiAccessibilityRole.Slider);
        check(tree.CaptureSnapshot().Elements.Count(element => element.Node.Name == "C# 예제 소스") == 8,
            "actual GPU Immediate page owns eight inline source blocks");
        LegacyTab("Widget Gallery");
        Capture("기존 API", "gallery-legacy-widget-source");
        check(tree.CaptureSnapshot().Elements.Count(element => element.Node.Name == "C# 예제 소스") == 3,
            "actual GPU Widget Gallery owns distinct plot,table and popup source blocks");
        Capture("Markdown", "gallery-markdown-smart-source");
        UiAccessibilityNode SourceNode() => tree.CaptureSnapshot().Elements.Single(element =>
            element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Node;
        var smartNodes = tree.CaptureSnapshot().Elements;
        var smartEditor = smartNodes.Single(element => element.Node.Name == "마크다운 스마트 편집" && element.Node.Role == UiAccessibilityRole.Edit).Node;
        var smartCode = SourceNode();
        check(smartCode.Value!.Contains("Dux.MarkdownEditor(", StringComparison.Ordinal)
            && !smartCode.Value.Contains("Dux.MarkdownView(", StringComparison.Ordinal),
            "GPU editor sample exposes only its matching Markdown factory");
        check(smartCode.Bounds.Y >= smartEditor.Bounds.Y + smartEditor.Bounds.Height
            && smartCode.Bounds.Y - smartEditor.Bounds.Y - smartEditor.Bounds.Height <= 24,
            "Markdown source follows the editor within normal component spacing");
        void MarkdownMode(string name)
        {
            var tab = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Tab);
            if (tree.RequestAction(tab.Node.Id, tab.RuntimeId, UiAccessibilityAction.Select, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Markdown mode rejected selection : " + name);
            for (var frame = 0; frame < 3; frame++) fixture.Capture(Draw);
        }
        MarkdownMode("미리보기");
        var preview = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "마크다운 미리보기" && element.Node.Role == UiAccessibilityRole.Edit).Node;
        var previewCode = SourceNode();
        check(previewCode.Value!.Contains("Dux.MarkdownView(", StringComparison.Ordinal)
            && !previewCode.Value.Contains("Dux.MarkdownEditor(", StringComparison.Ordinal),
            "GPU viewer sample switches to its matching Markdown factory");
        check(previewCode.Bounds.Y >= preview.Bounds.Y + preview.Bounds.Height
            && previewCode.Bounds.Y - preview.Bounds.Y - preview.Bounds.Height <= 24,
            "Markdown preview has no reserved viewport gap before its source");
        MarkdownMode("중첩 미리보기");
        var nestedCode = SourceNode();
        check(smartCode.Bounds.Y - nestedCode.Bounds.Y == 420,
            "Changing Markdown from its 580px editor to its 160px nested preview moves source upward by exactly 420px");
        Capture("Markdown", "gallery-markdown-nested-source");
        Capture("Button", "gallery-button-source");
        var buttonTree = tree.CaptureSnapshot().Elements;
        var buttonSource = buttonTree.Single(element => element.Node.Role == UiAccessibilityRole.Edit && element.Node.Name == "C# 예제 소스").Node;
        check(buttonSource.Value!.Contains("Fluent.Button", StringComparison.Ordinal)
            && !buttonSource.Value.Contains("Fluent.Checkbox", StringComparison.Ordinal)
            && !buttonTree.Any(element => element.Node.Role == UiAccessibilityRole.Checkbox)
            && buttonSource.Bounds.Y > buttonTree.Single(element => element.Node.Name == "카운터 증가" && element.Node.Role == UiAccessibilityRole.Button).Node.Bounds.Y,
            "Button destination has an isolated live example with equivalent source below it");
        check(!buttonTree.Any(element => element.Node.Role == UiAccessibilityRole.Switch
            && element.Node.Name is "줄바꿈" or "줄 번호" or "전체 표시")
            && !buttonTree.Any(element => element.Node.Name == "C# 소스"),
            "Gallery source is immediately visible without a toolbar, count heading or option switches");
        var copy = buttonTree.Single(element => element.Node.Name == "소스 복사" && element.Node.Role == UiAccessibilityRole.Button);
        check(copy.Node.Bounds.Width == 32 && copy.Node.Bounds.Height == 32
            && copy.Node.Bounds.X > buttonSource.Bounds.X + buttonSource.Bounds.Width - 48
            && copy.Node.Bounds.Y >= buttonSource.Bounds.Y && copy.Node.Bounds.Y < buttonSource.Bounds.Y + 40,
            "Copy is an icon-sized overlay in the top-right corner of the code surface");
        check(!buttonTree.Any(element => element.Node.Name == "준비"), "Source toolbar omits the permanent ready status");
        if (tree.RequestAction(copy.Node.Id, copy.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Source copy icon rejected Invoke.");
        fixture.Capture(Draw);
        var confirmation = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-copy-confirmed.png"), (int)fixture.FrameInfo.DisplaySize.X,
            (int)fixture.FrameInfo.DisplaySize.Y, confirmation);
        check(clipboard.Text == buttonSource.Value, "Copy icon writes the exact original C# buffer to the clipboard");
        var copied = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Tooltip && element.Node.Name == "복사됨").Node;
        check(copied.Bounds.Y + copied.Bounds.Height <= copy.Node.Bounds.Y,
            "Copy confirmation appears above its icon instead of occupying the source toolbar");
        fixture.Context.State.AdvanceTime(1.4f);
        fixture.Capture(Draw);
        fixture.Context.State.AdvanceTime(.4f);
        fixture.Capture(Draw);
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Tooltip && element.Node.Name == "복사됨"),
            "Copy confirmation disappears after its finite display interval");
        check(fixture.Context.State.NextScheduledFrameTimeSeconds is null,
            "Settled copy confirmation stops scheduling renders in event-wait mode");
        Capture("Checkbox", "gallery-checkbox-source");
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Checkbox)
            && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "카운터 증가"),
            "Checkbox destination does not reuse the combined Button example");
        Capture("선택 목록", "gallery-dropdown-source");
        var multi = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "확인할 기능" && element.Node.Role == UiAccessibilityRole.ComboBox);
        if (tree.RequestAction(multi.Node.Id, multi.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The combined Dropdown trigger could not be revealed above the source pane.");
        for (var revealFrame = 0; revealFrame < 3; revealFrame++) fixture.Capture(Draw);
        multi = tree.CaptureSnapshot().Elements.Single(element => element.Node.Id == multi.Node.Id);
        File.WriteAllLines(Path.Combine(output, "gallery-dropdown-reveal.txt"), tree.CaptureSnapshot().Elements
            .Where(element => element.Node.Name == "확인할 기능" || element.Node.Scroll is not null)
            .Select(element => element.Node.Name + " | " + element.Node.Bounds + " | offscreen : " + element.Node.Offscreen + " | " + element.Node.Scroll));
        if (tree.RequestAction(multi.Node.Id, multi.RuntimeId, UiAccessibilityAction.Expand, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The combined Dropdown trigger rejected expansion.");
        fixture.Capture(Draw);
        var popupPixels = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-dropdown-over-source.png"), (int)fixture.FrameInfo.DisplaySize.X,
            (int)fixture.FrameInfo.DisplaySize.Y, popupPixels);
        var popupOptions = tree.CaptureSnapshot().Elements.Where(element => element.Node.Role == UiAccessibilityRole.Option).ToArray();
        File.WriteAllLines(Path.Combine(output, "gallery-dropdown-geometry.txt"), popupOptions.Select(element =>
            element.Node.Name + " | " + element.Node.Bounds + " | offscreen : " + element.Node.Offscreen));
        check(popupOptions.Any(element => element.Node.Name == "이미지" && !element.Node.Offscreen)
            && popupOptions.Any(element => element.Node.Name == "SVG 아이콘" && !element.Node.Offscreen),
            "hosted view popup exposes visible options independently of its clipped body and source pane");
        var option = popupOptions.Single(element => element.Node.Name == "이미지").Node.Bounds;
        var point = new UiVector2(option.X + option.Width / 2, option.Y + option.Height / 2);
        fixture.Capture(Draw, new(point, true, false, true, false, false, false, 0, 0, [], [], default, default));
        fixture.Capture(Draw, new(point, false, false, false, true, false, false, 0, 0, [], [], default, default));
        var selectedPixels = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-dropdown-selected-over-source.png"), (int)fixture.FrameInfo.DisplaySize.X,
            (int)fixture.FrameInfo.DisplaySize.Y, selectedPixels);
        var selectedPopup = tree.CaptureSnapshot().Elements;
        check(gallery.DropdownPage.Multiple!.IsOpen
            && selectedPopup.Any(element => element.Node.Role == UiAccessibilityRole.Option && element.Node.Name == "이미지" && element.Node.Selected == true)
            && selectedPopup.Any(element => element.Node.Role == UiAccessibilityRole.Option && element.Node.Name == "SVG 아이콘" && !element.Node.Offscreen)
            && selectedPopup.Single(element => element.Node.Id == multi.Node.Id).Node.Bounds == multi.Node.Bounds,
            "selecting a tag preserves the visible multi-select anchor and the remaining popup options");
        var escaped = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0,
            [new(UiKey.Escape, true, default)], [], default, default);
        fixture.Capture(Draw, escaped);
        Capture("개요", "gallery-overview-source");
        var switches = tree.CaptureSnapshot().Elements.Where(static element => element.Node.Role == UiAccessibilityRole.Switch
            && element.Node.Name is "V-Sync" or "이벤트 대기 모드" or "갱신 영역 강조" or "밝은 테마").Select(static element => element.Node).ToArray();
        check(switches.Length == 4 && switches.All(node => node.Bounds.Y == switches[0].Bounds.Y),
            "gallery status options occupy one horizontal toolbar row");
        Capture("SVG", "gallery-svg-primary-source");
        var svgSources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).ToArray();
        check(svgSources.Length == 5 && gallery.SvgPage.PrimarySamples.Take(2).All(sample => svgSources.Any(source => source.Node.Value == sample.Source)),
            "GPU SVG primary comparison and text expose matching individual sources with three icon adapter sources");
        gallery.SvgPage.ConfigureArcPreview();
        Capture("SVG", "gallery-svg-arcs");
        check(gallery.SvgPage.ArcPreviews.Count == 6,
            "gallery arc comparison presents all six independently captured examples through the actual SVG page");
        Capture("평가", "gallery-rating");
        var brandRating = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.RadioButton
            && element.Node.Name == "브랜드");
        if (tree.RequestAction(brandRating.Node.Id, brandRating.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The Rating color control could not be revealed.");
        fixture.Capture(Draw);
        fixture.Capture(Draw);
        brandRating = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.RadioButton
            && element.Node.Name == "브랜드");
        var brandPoint = new UiVector2(brandRating.Node.Bounds.X + brandRating.Node.Bounds.Width / 2,
            brandRating.Node.Bounds.Y + brandRating.Node.Bounds.Height / 2);
        Console.WriteLine($"Rating brand pointer : {brandPoint}; offscreen : {brandRating.Node.Offscreen}");
        var ratingPointer = new UiInputState(brandPoint, false, false, false, false, false, false, 0, 0, [], [], default, default);
        fixture.Capture(Draw, ratingPointer with { LeftMouseDown = true, LeftMousePressed = true });
        fixture.Capture(Draw, ratingPointer with { LeftMouseReleased = true });
        fixture.Capture(Draw);
        var ratingCode = tree.CaptureSnapshot().Elements.First(element => element.Node.Role == UiAccessibilityRole.Edit
            && element.Node.Name == "C# 예제 소스");
        check(ratingCode.Node.Value!.Contains("Color = UiRatingColor.Brand", StringComparison.Ordinal),
            "actual Gallery GPU presentation updates Rating source after a brand-color pointer selection");
        if (tree.RequestAction(ratingCode.Node.Id, ratingCode.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The Rating source could not be revealed for GPU capture.");
        fixture.Capture(Draw);
        var ratingPixels = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-rating-brand-source.png"), (int)fixture.FrameInfo.DisplaySize.X,
            (int)fixture.FrameInfo.DisplaySize.Y, ratingPixels);
        Capture("Fluent", "gallery-controls");
        var counter = tree.CaptureSnapshot().Elements.Single(static element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == "카운터 증가");
        if (tree.RequestAction(counter.Node.Id, counter.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Gallery model-state counter rejected its input.");
        fixture.Capture(Draw);
        check(gallery.ClickCount == 1, "gallery theme-state fixture changes an actual page model before switching themes");
        Capture("보조 패널", "gallery-drawer-inline", "인라인 닫기");
        RevealBodyTarget("보조 패널 열기", UiAccessibilityRole.Button);
        var drawerOpen = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "보조 패널 열기");
        if (tree.RequestAction(drawerOpen.Node.Id, drawerOpen.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The Drawer example rejected its opener.");
        fixture.Capture(Draw);
        fixture.Context.State.AdvanceTime(.35f);
        var drawerPixels = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-drawer-overlay.png"), (int)fixture.FrameInfo.DisplaySize.X,
            (int)fixture.FrameInfo.DisplaySize.Y, drawerPixels);
        check(gallery.DrawerPage.Overlay.IsOpen && tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog
            && element.Node.Name == "보조 작업" && !element.Node.Offscreen), "gallery actual GPU capture contains its opened Drawer example");
        var drawerSave = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "패널 저장");
        if (tree.RequestAction(drawerSave.Node.Id, drawerSave.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The Drawer example rejected its save action.");
        fixture.Capture(Draw);
        check(gallery.DrawerPage.Saves == 1 && !gallery.DrawerPage.Overlay.IsOpen,
            "gallery actual Drawer footer callback saves once and closes the logical surface");
        fixture.Context.State.AdvanceTime(.35f);
        Capture("렌더링", "gallery-rendering-source", "정적 도형 예제", UiAccessibilityRole.Pane);
        Capture("렌더링", "gallery-rendering-composition-source", "그룹 합성 예제", UiAccessibilityRole.Pane);
        Capture("팝업 입력", "gallery-popup-input-source");
        void PopupCommand(string name)
        {
            var command = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button);
            check(tree.RequestAction(command.Node.Id, command.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "Gallery GPU popup command accepts : " + name);
            for (var settle = 0; settle < 3; settle++) fixture.Capture(Draw);
        }
        PopupCommand("입력 팝업 열기");
        Capture("팝업 입력", "gallery-popup-input-nonmodal");
        check(gallery.PopupInputPage.Visible && tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "팝업 바깥 명령").Node.Enabled
            && tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "팝업 안 명령").Node.Bounds.Y >= 230,
            "Gallery GPU nonmodal overlay remains visible at its real position outside the example clip");
        PopupCommand("팝업 안 명령"); PopupCommand("팝업 바깥 명령"); PopupCommand("입력 팝업 닫기");
        PopupCommand("팝업 입력 모드 전환"); PopupCommand("입력 팝업 열기");
        Capture("팝업 입력", "gallery-popup-input-exclusive");
        check(gallery.PopupInputPage.Visible && gallery.PopupInputPage.InsideCount == 1 && gallery.PopupInputPage.OutsideCount == 1
            && !tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "팝업 바깥 명령").Node.Enabled
            && tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Node.Value!.Contains("var Mode = UiPopupInputMode.Exclusive;", StringComparison.Ordinal),
            "Gallery GPU exclusive overlay preserves counters, blocks the background and shows its current mode in actual source");
        PopupCommand("입력 팝업 닫기");
        check(!gallery.PopupInputPage.Visible && tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "팝업 바깥 명령").Node.Enabled,
            "Gallery GPU popup close restores actual background controls");
        Capture("Tooltip", "gallery-tooltip-sources");
        void TooltipHover(string name, string file)
        {
            RevealBodyTarget(name, UiAccessibilityRole.Button);
            var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button).Node;
            var hover = new UiInputState(new(target.Bounds.X + target.Bounds.Width / 2, target.Bounds.Y + target.Bounds.Height / 2),
                false, false, false, false, false, false, 0, 0, [], [], default, default);
            fixture.Capture(Draw, hover);
            fixture.Context.State.AdvanceTime(.26f);
            var pixels = fixture.Capture(Draw, hover);
            BrowserPng.Write(Path.Combine(output, file + ".png"), (int)fixture.FrameInfo.DisplaySize.X, (int)fixture.FrameInfo.DisplaySize.Y, pixels);
            check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Tooltip && !element.Node.Offscreen),
                "Gallery actual GPU hover opens the scheduled Tooltip surface : " + name);
            fixture.Capture(Draw, hover with { KeyEvents = [new(UiKey.Escape, true, default)] });
            fixture.Capture(Draw);
        }
        TooltipHover("설명 대상", "gallery-tooltip-automatic");
        TooltipHover("화살표 대상", "gallery-tooltip-arrow");
        Capture("Tooltip", "gallery-tooltip-controlled-source", "Tooltip 표시 전환");
        var tooltipToggle = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "Tooltip 표시 전환" && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(tooltipToggle.Node.Id, tooltipToggle.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Gallery GPU caller-controlled Tooltip accepts its actual toggle");
        for (var settle = 0; settle < 3; settle++) fixture.Capture(Draw);
        var tooltipPixels = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-tooltip-controlled-open.png"), (int)fixture.FrameInfo.DisplaySize.X,
            (int)fixture.FrameInfo.DisplaySize.Y, tooltipPixels);
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Tooltip
                && element.Node.Name == "코드에서 표시 여부를 직접 정하는 Tooltip입니다." && !element.Node.Offscreen)
            && tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit)
                .Any(element => element.Node.Value!.Contains("controlled.Visible = true;", StringComparison.Ordinal)),
            "Gallery GPU controlled Tooltip and its live source reflect the same open state");
        fixture.Capture(Draw, new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0,
            [new(UiKey.Escape, true, default)], [], default, default));
        fixture.Capture(Draw); fixture.Capture(Draw);
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Tooltip)
            && tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit)
                .Any(element => element.Node.Value!.Contains("controlled.Visible = false;", StringComparison.Ordinal)),
            "Gallery GPU Escape invokes the real visibility callback and synchronizes the closed source state");
        Capture("입력·IME", "gallery-editing-name-source");
        void EditValue(string name, string value)
        {
            RevealBodyTarget(name, UiAccessibilityRole.Edit);
            var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Edit);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.SetValue, value, wait : false) == UiAccessibilityResult.Success,
                "Gallery GPU revealed input accepts value : " + name);
            for (var settle = 0; settle < 3; settle++) fixture.Capture(Draw);
        }
        void EditOption(string name)
        {
            RevealBodyTarget(name, UiAccessibilityRole.Button);
            var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "Gallery GPU edit option accepts action : " + name);
            for (var settle = 0; settle < 3; settle++) fixture.Capture(Draw);
        }
        EditValue("이름", "디모이");
        check(!tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "이름" && element.Node.Role == UiAccessibilityRole.Edit).Node.Invalid,
            "Gallery GPU actual name input clears reactive validation after a valid value");
        EditValue("메모", "한글과 English\n둘째 줄");
        EditOption("줄바꿈 전환"); EditOption("읽기 전용 전환"); EditOption("라벨 필수 전환");
        Capture("입력·IME", "gallery-editing-memo-source", "메모", UiAccessibilityRole.Edit);
        var actualMemo = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "메모" && element.Node.Role == UiAccessibilityRole.Edit).Node;
        var actualMemoLabel = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "메모" && element.Node.Role == UiAccessibilityRole.Label).Node;
        check(actualMemo.Value == "한글과 English\n둘째 줄" && actualMemo.ReadOnly && actualMemo.LabeledBy == actualMemoLabel.Id
            && tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit)
                .Any(element => element.Node.Value!.Contains("Flags = UiInputTextFlags.ReadOnly, Wrap = false", StringComparison.Ordinal)
                    && element.Node.Value.Contains("Required : true", StringComparison.Ordinal)),
            "Gallery GPU memo options,label association,current text and corresponding source remain consistent");
        Capture("측정 재사용", "gallery-measurement-sources");
        var measurementBaseline = gallery.MeasurementPage.Measurements;
        var measuredToggle = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "재사용 체크" && element.Node.Role == UiAccessibilityRole.Checkbox);
        check(tree.RequestAction(measuredToggle.Node.Id, measuredToggle.RuntimeId, UiAccessibilityAction.Toggle, wait : false) == UiAccessibilityResult.Success,
            "Gallery GPU measurement checkbox accepts its real toggle");
        for (var settle = 0; settle < 3; settle++) fixture.Capture(Draw);
        check(gallery.MeasurementPage.Checked == true && gallery.MeasurementPage.Measurements == measurementBaseline
            && tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit)
                .Any(element => element.Node.Value!.Contains("new UiState<bool?>(true)", StringComparison.Ordinal)),
            "Gallery GPU retained checkbox changes painting and source without remeasuring its controls");
        EditOption("측정 본문 변경"); EditOption("측정 너비 변경");
        EditValue("재사용 폼 입력", "디모이"); EditValue("재사용 여러 줄", "실제 메모\n두 번째 줄");
        EditOption("탭 크기 변경");
        Capture("측정 재사용", "gallery-measurement-current-source");
        check(gallery.MeasurementPage.Measurements > measurementBaseline && gallery.MeasurementPage.FormValue == "디모이"
            && gallery.MeasurementPage.MemoValue == "실제 메모\n두 번째 줄"
            && tree.CaptureSnapshot().Elements.Where(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit)
                .Any(element => element.Node.Value!.Contains("form.TabSize = UiTabListSize.Small;", StringComparison.Ordinal)
                    && element.Node.Value.Contains("new UiState<string>(\"디모이\")", StringComparison.Ordinal)),
            "Gallery GPU measurement mutations update actual geometry,form state and corresponding source");
        Capture("갱신 관찰", "gallery-repaint-source");
        string RepaintSource() => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "C# 예제 소스" && element.Node.Role == UiAccessibilityRole.Edit).Node.Value!;
        var repaintSource = RepaintSource();
        var staticRepaintBuilds = gallery.RepaintPage.StaticBuilds;
        var dynamicRepaintBuilds = gallery.RepaintPage.TextBuilds;
        EditOption("한 번 변경");
        EditValue("갱신 관찰 입력", "실제 갱신 관찰 입력");
        check(gallery.RepaintPage.TextBuilds > dynamicRepaintBuilds && gallery.RepaintPage.StaticBuilds == staticRepaintBuilds
            && RepaintSource() == repaintSource,
            "Gallery GPU runtime inspector content updates retain the fixed panel and do not mutate source pixels");
        void RepaintToggle()
        {
            var toggle = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "텍스트 자동 변경 (60Hz)" && element.Node.Role == UiAccessibilityRole.Switch);
            check(tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Toggle, wait : false) == UiAccessibilityResult.Success,
                "Gallery GPU inspector automatic switch accepts its actual toggle");
            for (var settle = 0; settle < 3; settle++) fixture.Capture(Draw);
        }
        RepaintToggle();
        repaintSource = RepaintSource();
        dynamicRepaintBuilds = gallery.RepaintPage.TextBuilds;
        for (var tick = 0; tick < 5; tick++) fixture.Capture(Draw);
        check(gallery.RepaintPage.Running && gallery.RepaintPage.TextBuilds > dynamicRepaintBuilds
            && RepaintSource() == repaintSource && repaintSource.Contains("experiment.AutoChange = true;", StringComparison.Ordinal),
            "Gallery GPU automatic revisions keep the configured source stable instead of creating diagnostic work");
        Capture("갱신 관찰", "gallery-repaint-automatic-source");
        RepaintToggle();
        Capture("배치 통합", "gallery-integration-source");
        Capture("위젯 배치", "gallery-widget-layout-source");
        Capture("가상 목록", "gallery-virtual-source");
        Capture("메시지 표시", "gallery-messagebar-source");
        Capture("메시지 표시", "gallery-messagebar-dynamic-source", "메시지 추가");
        var addMessage = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "메시지 추가");
        if (tree.RequestAction(addMessage.Node.Id, addMessage.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The MessageBar example rejected its actual insertion control.");
        fixture.Capture(Draw);
        fixture.Capture(Draw);
        fixture.Context.State.AdvanceTime(.35f);
        Capture("메시지 표시", "gallery-messagebar-inserted", "message-1", UiAccessibilityRole.Group);
        check(gallery.MessageBarPage.RequestedDynamicCount == 1 && gallery.MessageBarPage.PhysicalDynamicCount == 1,
            "gallery GPU keyed MessageBar insertion settles with its adjacent source");
        Capture("대화 상자", "gallery-dialog-source");
        var dialogOpen = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "모달 열기");
        if (tree.RequestAction(dialogOpen.Node.Id, dialogOpen.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The Dialog example rejected its opener.");
        fixture.Capture(Draw);
        fixture.Context.State.AdvanceTime(.35f);
        var dialogPixels = fixture.Capture(Draw);
        BrowserPng.Write(Path.Combine(output, "gallery-dialog-open.png"), (int)fixture.FrameInfo.DisplaySize.X,
            (int)fixture.FrameInfo.DisplaySize.Y, dialogPixels);
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog
            && element.Node.Name == "사용자 정보" && !element.Node.Offscreen),
            "gallery GPU capture contains its actual Dialog outside the scrolled source body");
        var dialogCancel = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "취소");
        if (tree.RequestAction(dialogCancel.Node.Id, dialogCancel.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The Dialog example rejected its cancel action.");
        fixture.Capture(Draw);
        fixture.Context.State.AdvanceTime(.35f);
        fixture.Capture(Draw);
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog
            && element.Node.Name == "사용자 정보" && !element.Node.Offscreen),
            "gallery actual Dialog cancellation completes before returning to navigation");
        Capture("탐색 메뉴", "gallery-navigation-idle");
        void CaptureNavHover(string file)
        {
            var action = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == "개요 고정");
            if (action.Node.Offscreen) throw new InvalidOperationException("The actual navigation secondary action must be visible in its example viewport.");
            var bounds = action.Node.Bounds;
            var input = new UiInputState(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
                false, false, false, false, false, false, 0, 0, [], [], default, default);
            var pixels = fixture.Capture(Draw, input);
            BrowserPng.Write(Path.Combine(output, file + ".png"), (int)fixture.FrameInfo.DisplaySize.X, (int)fixture.FrameInfo.DisplaySize.Y, pixels);
            check(gallery.SelectedPage == "탐색 메뉴" && gallery.NavigationPage.Preview.Value.Value == "overview",
                "gallery actual secondary action hover preserves its selected example and destination : " + file);
        }
        CaptureNavHover("gallery-nav-dark-hover");
        var navAction = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == "개요 고정");
        if (tree.RequestAction(navAction.Node.Id, navAction.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The actual navigation secondary action rejected accessible Invoke.");
        fixture.Capture(Draw);
        check(gallery.NavigationPage.PinActions == 1 && gallery.NavigationPage.Preview.Value.Value == "overview",
            "gallery actual borrowed secondary callback runs once without changing navigation");
        var planned = Fluent.MissingComponentNames.ToArray();
        if (planned.Length != 0)
            Capture(planned[0], "gallery-planned");
        void ToggleTheme()
        {
            var toggle = tree.CaptureSnapshot().Elements.Single(static element => element.Node.Name == "밝은 테마" && element.Node.Role == UiAccessibilityRole.Switch);
            if (tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Gallery theme toggle was rejected.");
            fixture.Capture(Draw);
            fixture.Capture(Draw);
        }
        var selected = gallery.SelectedPage;
        ToggleTheme();
        check(fixture.Context.GetStyle().FluentColorScheme == UiFluentColorScheme.Light && gallery.SelectedPage == selected && gallery.ClickCount == 1,
            "gallery actual theme switch applies the light palette while preserving its selected page");
        Capture(selected!, "gallery-light");
        var lightPixels = fixture.Capture(Draw);
        var tagline = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "컨트롤 · 그래픽 · 문서").Node;
        var countBadge = new UiRect(logoBounds.X, MathF.Max(logoBounds.Y + logoBounds.Height, tagline.Bounds.Y + tagline.Bounds.Height) + 10, 100, 16);
        var visibleCountPixels = 0;
        for (var y = (int)countBadge.Y; y < (int)(countBadge.Y + countBadge.Height); y++)
            for (var x = (int)countBadge.X; x < (int)(countBadge.X + countBadge.Width); x++)
            {
                var pixel = (y * (int)fixture.FrameInfo.DisplaySize.X + x) * 4;
                if (lightPixels[pixel] < 180 && lightPixels[pixel + 1] < 180 && lightPixels[pixel + 2] < 180) visibleCountPixels++;
            }
        check(visibleCountPixels > 5, "Light Gallery count badge paints visible dark glyphs instead of inverse white text");
        var lightPage = 0;
        foreach (var page in gallery.PageTitles)
            Capture(page, "gallery-light-page-" + lightPage++);
        Capture(selected!, "gallery-light-restored");
        Capture("탐색 메뉴", "gallery-nav-light-idle");
        CaptureNavHover("gallery-nav-light-hover");
        check(gallery.NavigationPage.PinActions == 1, "gallery secondary action model survives its theme and page changes");
        gallery.SelectedPage = selected;
        fixture.Capture(Draw);
        ToggleTheme();
        check(fixture.Context.GetStyle().FluentColorScheme == UiFluentColorScheme.Dark && gallery.SelectedPage == selected && gallery.ClickCount == 1,
            "gallery actual theme switch restores the dark palette while preserving its selected page");
        var forced = new UiForcedColorPalette(new(3, 6, 9), new(240, 230, 220), new(110, 120, 130),
            new(10, 90, 180), new(250, 240, 230), new(30, 200, 220), new(20, 30, 40), new(210, 220, 230));
        fixture.Context.SetTheme(forced.Apply(UiCompiledDesign.Windows11Dark.Theme));
        fixture.Capture(Draw);
        ToggleTheme();
        check(fixture.Context.GetStyle().ForcedColors == forced && fixture.Context.GetStyle().WindowBg == forced.Canvas,
            "gallery manual theme choice preserves the active system forced-color palette");
        RunCompact(output, check);
    }
    private static void RunCompact(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(320, 900));
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true };
        using var tree = new UiAccessibilityTree();
        fixture.Context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
        fixture.Context.SetAccessibilityTree(tree);
        void Draw(UiImmediateContext ui)
        {
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
            gallery.Render(ui);
        }
        byte[] Capture(string name)
        {
            var pixels = fixture.Capture(Draw);
            BrowserPng.Write(Path.Combine(output, name + ".png"), (int)fixture.FrameInfo.DisplaySize.X,
                (int)fixture.FrameInfo.DisplaySize.Y, pixels);
            return pixels;
        }
        void Invoke(string name, UiAccessibilityRole role, UiAccessibilityAction action = UiAccessibilityAction.Invoke)
        {
            var entry = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
            if (entry.Node.Offscreen || tree.RequestAction(entry.Node.Id, entry.RuntimeId, action, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Compact Gallery control rejected its actual action : " + name);
            fixture.Capture(Draw);
        }
        var closed = Capture("gallery-compact-closed");
        check(!gallery.CompactNavigationOpen && tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "탐색 메뉴 열기" && !element.Node.Offscreen), "compact Gallery GPU capture starts with its discoverable navigation trigger");
        Invoke("탐색 메뉴 열기", UiAccessibilityRole.Button);
        fixture.Context.State.AdvanceTime(.35f);
        var dark = Capture("gallery-compact-dark");
        check(gallery.CompactNavigationOpen && !dark.AsSpan().SequenceEqual(closed)
            && tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog && element.Node.Name == "갤러리 탐색"
                && element.Node.Bounds.Width == 320 && !element.Node.Offscreen), "compact Gallery GPU capture displays the real edge Drawer and grouped navigation");
        Invoke("탐색 메뉴 닫기", UiAccessibilityRole.Button);
        fixture.Context.State.AdvanceTime(.35f);
        fixture.Capture(Draw);
        Invoke("밝은 테마", UiAccessibilityRole.Switch, UiAccessibilityAction.Toggle);
        Invoke("탐색 메뉴 열기", UiAccessibilityRole.Button);
        fixture.Context.State.AdvanceTime(.35f);
        var light = Capture("gallery-compact-light");
        check(gallery.CompactNavigationOpen && fixture.Context.GetStyle().FluentColorScheme == UiFluentColorScheme.Light
            && !light.AsSpan().SequenceEqual(dark), "compact navigation reopens in the theme selected through the global header");
        Invoke("Button", UiAccessibilityRole.Link);
        fixture.Context.State.AdvanceTime(.35f);
        Capture("gallery-compact-selected");
        check(!gallery.CompactNavigationOpen && gallery.SelectedPage == "Button"
            && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog),
            "compact Gallery destination invocation replaces its full-width page and retires the menu surface");
        Invoke("카운터 증가", UiAccessibilityRole.Button);
        Invoke("탐색 메뉴 열기", UiAccessibilityRole.Button);
        fixture.Context.State.AdvanceTime(.35f);
        fixture.Capture(Draw);
        Invoke("탐색 메뉴 닫기", UiAccessibilityRole.Button);
        fixture.Context.State.AdvanceTime(.35f);
        Capture("gallery-compact-dismissed");
        check(!gallery.CompactNavigationOpen && gallery.SelectedPage == "Button"
            && tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "클릭 횟수 : 1")
            && tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Button
                && element.Node.Name == "탐색 메뉴 열기" && element.Node.Focused),
            "compact Gallery explicit close returns focus and preserves the edited page model");
    }

    private sealed class CodeClipboard : IUiClipboard
    {
        internal string Text = "";
        public string GetText() => Text;
        public void SetText(string text) => Text = text;
    }

}
