using Duxel.Core;
using Duxel.Gallery;

internal static class ObserverGalleryTests
{
    internal static void Run(Action<bool, string> check)
    {
        GalleryLocalizationTests.Run(check);
        GallerySampleTests.Run(check);
        CodeViewCacheTests.Run(check);
        ObserverBatchPresentationTests.Run(check);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        using var screen = new ObserverScreen(ObserverServices.CreateWindows());
        ControlFontFixture.Configure(context);
        context.State.VSync = false;
        context.SetAccessibilityTree(tree);
        UiInputState Input(UiVector2 pointer, bool down = false, bool pressed = false, bool released = false) =>
            new(pointer, down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        var viewportSize = new UiVector2(1280, 900);
        var renderedHighlights = false;
        Action<int>? missingGlyphObserver = null;
        void RenderFrame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, viewportSize, new(1, 1)), input ?? Input(new(-100, -100)), new(0, 0, viewportSize.X, viewportSize.Y),
                UiTextSettings.Default with
                {
                    MissingGlyphObserver = missingGlyphObserver
                });
            context.Render(screen);
            using var draw = context.GetDrawData();
            renderedHighlights = draw.RepaintHighlights;
        }
        void Frame(string page, UiInputState? input = null)
        {
            var changedPage = screen.SelectedPage != page;
            screen.SelectedPage = page;
            RenderFrame(input);
            if (changedPage)
            {
                // Body idle assertions run after the default header's finite hold and exit.
                context.State.AdvanceTime(.5f);
                RenderFrame();
                context.State.AdvanceTime(.2f);
                RenderFrame();
            }
        }
        UiAccessibilityNode Find(string name, UiAccessibilityRole? role = null)
        {
            UiAccessibilityNode? found = null;
            var matches = 0;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    if (element.Node.Name == name && (role is null || element.Node.Role == role))
                    {
                        found = element.Node;
                        matches++;
                    }
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            if (matches > 1)
                throw new InvalidOperationException("Observer control name is ambiguous; specify its role : " + name);
            return found ?? throw new InvalidOperationException("Observer control missing : " + name);
        }
        foreach (var page in new[] { "개요", "기존 API", "Fluent", "타이포그래피", "입력·IME", "Tooltip", "컨테이너", "접기·펼치기", "가상 목록", "렌더링", "SVG", "카드", "경로", "선택 목록", "인물 정보", "관찰 범위" })
        {
            Frame(page);
            check(screen.SelectedPage == page,
                "observer renders its actual persistent page : " + page);
            var pageHeader = Find(screen.SelectedNavigationLabel, UiAccessibilityRole.Link);
            var panel = Find(screen.SelectedNavigationLabel, UiAccessibilityRole.Pane);
            check(pageHeader.ControlledElementId == panel.Id && panel.LabeledBy == pageHeader.Id,
                "observer actual page publishes reciprocal navigation and page relationships : " + page);
        }
        Frame("개요");
        check(Find("V-Sync", UiAccessibilityRole.Switch).ToggleState == UiAccessibilityToggleState.Off
            && Find("이벤트 대기 모드", UiAccessibilityRole.Switch).ToggleState == UiAccessibilityToggleState.On,
            "observer header uses actual Fluent switches with VSync off and event-wait on");
        var globalHighlight = Find("갱신 영역 강조", UiAccessibilityRole.Switch);
        check(globalHighlight.ToggleState == UiAccessibilityToggleState.Off
            && globalHighlight.Bounds.X + globalHighlight.Bounds.Width < Find("밝은 테마", UiAccessibilityRole.Switch).Bounds.X
            && globalHighlight.Bounds.Y == Find("V-Sync", UiAccessibilityRole.Switch).Bounds.Y,
            "global repaint switch starts off in the single status row before the pinned theme switch");
        if (!tree.TryGet(globalHighlight.Id, out var globalEntry))
            throw new InvalidOperationException("Global repaint switch missing.");
        if (tree.RequestAction(globalHighlight.Id, globalEntry.RuntimeId, UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Global repaint toggle rejected.");
        Frame("Fluent");
        check(renderedHighlights && Find("갱신 영역 강조", UiAccessibilityRole.Switch).ToggleState == UiAccessibilityToggleState.On,
            "global repaint toggle applies to an ordinary Fluent page without entering the diagnostic sample");
        Frame("타이포그래피");
        check(renderedHighlights, "global repaint setting persists across page changes");
        globalHighlight = Find("갱신 영역 강조", UiAccessibilityRole.Switch);
        if (!tree.TryGet(globalHighlight.Id, out globalEntry)
            || tree.RequestAction(globalHighlight.Id, globalEntry.RuntimeId, UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Disabling global repaint diagnostics rejected.");
        Frame("갱신 관찰");
        check(!renderedHighlights, "disabling the header repaint switch clears diagnostics on the sample page too");
        Frame("갱신 관찰");
        var firstStaticBuilds = screen.RepaintPage.StaticBuilds;
        var firstTextBuilds = screen.RepaintPage.TextBuilds;
        Frame("갱신 관찰");
        check(screen.RepaintPage.StaticBuilds == firstStaticBuilds && screen.RepaintPage.TextBuilds == firstTextBuilds,
            "repaint observer retains both unchanged panels without rebuilding their content");
        var advance = Find("한 번 변경", UiAccessibilityRole.Button);
        if (!tree.TryGet(advance.Id, out var advanceEntry))
            throw new InvalidOperationException("Repaint action missing.");
        check(tree.RequestAction(advance.Id, advanceEntry.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "repaint observer accepts real semantic content-change action");
        Frame("갱신 관찰");
        check(screen.RepaintPage.StaticBuilds == firstStaticBuilds && screen.RepaintPage.TextBuilds > firstTextBuilds,
            "repaint observer rebuilds changed text while preserving static panel construction");
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "repaint observer does not schedule animation frames for an idle highlight switch");
        var cpuToggle = Find("CPU 구간 측정", UiAccessibilityRole.Switch);
        check(!context.InputProfilingEnabled, "observer CPU profiling starts off");
        if (!tree.TryGet(cpuToggle.Id, out var cpuEntry))
            throw new InvalidOperationException("CPU profiling switch missing.");
        check(tree.RequestAction(cpuToggle.Id, cpuEntry.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Success,
            "observer CPU profiling uses the real semantic switch");
        Frame("갱신 관찰");
        Frame("갱신 관찰");
        check(context.InputProcessingMetrics is { ProfilingEnabled: true, Completed: true }, "observer switch enables completed Core measurements");
        var snapshotButton = Find("측정값 확인", UiAccessibilityRole.Button);
        if (!tree.TryGet(snapshotButton.Id, out var snapshotEntry))
            throw new InvalidOperationException("CPU snapshot button missing.");
        check(tree.RequestAction(snapshotButton.Id, snapshotEntry.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "observer measurement snapshot uses the real semantic button");
        Frame("갱신 관찰");
        var cpuSnapshot = screen.RepaintPage.CpuSnapshot;
        check(cpuSnapshot.Contains("직전 Render", StringComparison.Ordinal) && cpuSnapshot.Contains("마무리", StringComparison.Ordinal),
            "observer snapshot exposes the completed Render stage values");
        context.State.AdvanceTime(.2f);
        Frame("갱신 관찰");
        check(screen.RepaintPage.CpuSnapshot == cpuSnapshot && context.State.NextScheduledFrameTimeSeconds is null,
            "observer CPU snapshot remains fixed without scheduling diagnostic frames");
        cpuToggle = Find("CPU 구간 측정", UiAccessibilityRole.Switch);
        if (!tree.TryGet(cpuToggle.Id, out cpuEntry)
            || tree.RequestAction(cpuToggle.Id, cpuEntry.RuntimeId, UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("CPU profiling switch could not be disabled.");
        Frame("갱신 관찰");
        Frame("갱신 관찰");
        check(!context.InputProfilingEnabled && !context.InputProcessingMetrics.ProfilingEnabled, "observer can disable timing and allocation sampling again");
        Frame("기존 API");
        var legacyFloat = Find("float", UiAccessibilityRole.Slider);
        if (!tree.TryGet(legacyFloat.Id, out var legacyEntry))
            throw new InvalidOperationException("Legacy observer Slider is missing.");
        var originalLegacyValue = legacyFloat.Range!.Value.Value;
        check(tree.RequestRangeValue(legacyFloat.Id, legacyEntry.RuntimeId, .42, wait: false) == UiAccessibilityResult.Success,
            "observer actual Existing API Hello Slider accepts typed continuous numeric changes");
        Frame("기존 API");
        Frame("Fluent");
        Frame("기존 API");
        check(Find("float", UiAccessibilityRole.Slider).Range?.Value == (double).42f,
            "observer Existing API Slider commits its caller value and retains it across page visits");
        if (!tree.TryGet(legacyFloat.Id, out var restoredLegacyEntry))
            throw new InvalidOperationException("Restored legacy Slider is missing.");
        if (tree.RequestRangeValue(legacyFloat.Id, restoredLegacyEntry.RuntimeId, originalLegacyValue, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Restoring the original legacy Slider value failed.");
        Frame("기존 API");
        var immediateTab = Find("Immediate", UiAccessibilityRole.Tab);
        if (!tree.TryGet(immediateTab.Id, out var immediateEntry))
            throw new InvalidOperationException("Immediate header is missing.");
        check(tree.RequestAction(immediateTab.Id, immediateEntry.RuntimeId, UiAccessibilityAction.Select, wait: false) == UiAccessibilityResult.Success,
            "observer actual legacy Immediate header accepts semantic selection");
        Frame("기존 API");
        check(Find("Hello", UiAccessibilityRole.Tab).Selected == true && Find("Immediate", UiAccessibilityRole.Tab).Selected == false,
            "observer legacy selection keeps the current body consistent during its deferred frame");
        Frame("기존 API");
        var immediatePanel = Find("Immediate", UiAccessibilityRole.TabPanel);
        check(Find("Immediate", UiAccessibilityRole.Tab).ControlledElementId == immediatePanel.Id &&
            Find("Scale", UiAccessibilityRole.Slider).ParentId == immediatePanel.Id,
            "observer actual Immediate panel links its header and owns the legacy numeric controls");
        var hello = Find("Hello", UiAccessibilityRole.Tab);
        if (!tree.TryGet(hello.Id, out var helloEntry))
            throw new InvalidOperationException("Hello header is missing.");
        tree.RequestAction(hello.Id, helloEntry.RuntimeId, UiAccessibilityAction.Select, wait: false);
        Frame("기존 API");
        Frame("기존 API");
        check(Find("float", UiAccessibilityRole.Slider).Range?.Value == originalLegacyValue,
            "observer semantic legacy return restores Hello with its retained caller value");
        Frame("Fluent");
        var mixedCheckbox = Find("혼합 상태에서 시작", UiAccessibilityRole.Checkbox);
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "observer Fluent page starts idle with a paused Spinner and fixed determinate progress");
        check(mixedCheckbox.ToggleState == UiAccessibilityToggleState.Indeterminate &&
            Find("토글 버튼과 값 공유", UiAccessibilityRole.Switch).ToggleState == UiAccessibilityToggleState.Off,
            "observer actual Fluent checkbox distinguishes mixed state from the separate off switch");
        if (!tree.TryGet(mixedCheckbox.Id, out var mixedEntry))
            throw new InvalidOperationException("Mixed checkbox is missing.");
        tree.RequestAction(mixedCheckbox.Id, mixedEntry.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        Frame("Fluent");
        check(Find("혼합 상태에서 시작", UiAccessibilityRole.Checkbox).ToggleState == UiAccessibilityToggleState.On,
            "observer actual mixed checkbox applies typed Toggle through its original caller state");
        var sharedSwitch = Find("토글 버튼과 값 공유", UiAccessibilityRole.Switch);
        if (!tree.TryGet(sharedSwitch.Id, out var switchEntry))
            throw new InvalidOperationException("Shared switch is missing.");
        tree.RequestAction(sharedSwitch.Id, switchEntry.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        Frame("Fluent");
        Frame("Fluent");
        check(Find("선택 유지", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On &&
            Find("토글 버튼과 값 공유", UiAccessibilityRole.Switch).ToggleState == UiAccessibilityToggleState.On,
            "observer actual Switch and ToggleButton publish their shared binding after one typed mutation");
        context.State.AdvanceTime(.2f);
        Frame("Fluent");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "observer finite toggle motion returns Fluent to event waiting");
        var playback = Reveal("Fluent", Find("Spinner 재생 전환"));
        var playbackPoint = new UiVector2(playback.Bounds.X + playback.Bounds.Width / 2, playback.Bounds.Y + playback.Bounds.Height / 2);
        Frame("Fluent", Input(playbackPoint, down: true, pressed: true));
        Frame("Fluent", Input(playbackPoint, released: true));
        check(context.State.HasActiveAnimations, "observer explicit Spinner playback requests animation; bounds : " + playback.Bounds + "; focused : " + context.State.FocusedId);
        Frame("Fluent", Input(playbackPoint, down: true, pressed: true));
        Frame("Fluent", Input(playbackPoint, released: true));
        Frame("Fluent");
        context.State.AdvanceTime(.7f);
        Frame("Fluent");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "observer explicit Spinner pause returns the Fluent page to event waiting");
        var radioGroup = Find("처리 방식", UiAccessibilityRole.RadioGroup);
        var radioOption = Reveal("Fluent", Find("품질 우선", UiAccessibilityRole.RadioButton));
        check(radioGroup.SupportsSelection && radioGroup.SelectionRequired && radioOption.ParentId == radioGroup.Id &&
            !radioOption.SupportsInvoke && radioOption.ToggleState is null,
            "observer actual Fluent RadioGroup exposes exclusive selectable options rather than Toggle or Invoke");
        if (!tree.TryGet(radioOption.Id, out var radioEntry))
            throw new InvalidOperationException("Observer radio option is missing.");
        check(tree.RequestAction(radioOption.Id, radioEntry.RuntimeId, UiAccessibilityAction.Select, wait: false) == UiAccessibilityResult.Success,
            "observer real visible radio option accepts a typed selection request");
        Frame("Fluent");
        check(Find("품질 우선", UiAccessibilityRole.RadioButton).Selected == true &&
            Find("균형", UiAccessibilityRole.RadioButton).Selected == false,
            "observer real radio selection changes its original caller state and clears the previous option");
        var counter = Reveal("Fluent", Find("카운터 증가"));
        var center = new UiVector2(counter.Bounds.X + counter.Bounds.Width / 2, counter.Bounds.Y + counter.Bounds.Height / 2);
        Frame("Fluent", Input(center, down: true, pressed: true));
        Frame("Fluent", Input(center, released: true));
        Frame("Fluent", Input(new(500, 500)) with
        {
            MouseWheel = -50
        });
        Frame("Fluent");
        foreach (var appearance in Enum.GetValues<UiTabListAppearance>())
            check(!Reveal("Fluent", Find("표현 비교 : " + appearance, UiAccessibilityRole.TabList)).Offscreen,
                "observer scroll reveals the actual tab representation : " + appearance);
        var iconGroup = Reveal("Fluent", Find("표현 비교 : SubtleCircular", UiAccessibilityRole.TabList));
        tree.TryGet(iconGroup.Id, out var iconList);
        tree.TryGet(iconList.FirstChild!, out var homeTab);
        tree.TryGet(homeTab.Next!, out var documentTab);
        var documentPointer = new UiVector2(documentTab.Node.Bounds.X + documentTab.Node.Bounds.Width / 2,
            documentTab.Node.Bounds.Y + documentTab.Node.Bounds.Height / 2);
        Frame("Fluent", Input(documentPointer, down: true, pressed: true));
        Frame("Fluent", Input(documentPointer, released: true));
        Frame("Fluent");
        var shared = true;
        foreach (var appearance in Enum.GetValues<UiTabListAppearance>())
        {
            tree.TryGet(Find("표현 비교 : " + appearance, UiAccessibilityRole.TabList).Id, out var list);
            tree.TryGet(list.FirstChild!, out var firstTab);
            tree.TryGet(firstTab.Next!, out var secondTab);
            shared &= firstTab.Node.Selected == false && secondTab.Node.Name == "문서" && secondTab.Node.Selected == true;
        }
        check(shared, "observer actual icon-only pointer selection updates all four lists through their persistent shared value");
        Frame("입력·IME");
        check(Find("이름").Role == UiAccessibilityRole.Edit && Find("메모", UiAccessibilityRole.Edit).Multiline,
            "observer publishes the actual single-line and multiline editing controls");
        var name = Find("이름");
        var notes = Find("메모", UiAccessibilityRole.Edit);
        check(notes.LabeledBy == Find("메모", UiAccessibilityRole.Label).Id,
            "observer memo editor links its independently rendered Label provider");
        name = Reveal("입력·IME", name);
        tree.TryGet(name.Id, out var nameElement);
        check(tree.RequestAction(name.Id, nameElement.RuntimeId, UiAccessibilityAction.SetValue, "관찰자", wait : false) == UiAccessibilityResult.Success,
            "observer revealed name input accepts its actual queued value action");
        Frame("입력·IME"); Frame("입력·IME");
        notes = Reveal("입력·IME", Find("메모", UiAccessibilityRole.Edit));
        tree.TryGet(notes.Id, out var notesElement);
        check(tree.RequestAction(notes.Id, notesElement.RuntimeId, UiAccessibilityAction.SetValue, "보존할 메모\n두 번째 줄", wait : false) == UiAccessibilityResult.Success,
            "observer revealed memo input accepts its actual queued value action");
        Frame("입력·IME"); Frame("입력·IME");
        check(Find("이름").Value == "관찰자" && !Find("이름").Invalid,
            "observer name binding updates its actual field validation");
        Frame("Fluent");
        check(Find("카운터 증가").Enabled && screen.ClickCount == 1,
            "observer real pointer callback fires once and its value survives leaving and returning to a page");
        Frame("입력·IME");
        check(Find("이름").Value == "관찰자" && !Find("이름").Invalid,
            "observer single-line value and valid state survive leaving and returning to the editing page");
        check(Find("메모", UiAccessibilityRole.Edit).Value == "보존할 메모\n두 번째 줄",
            "observer multiline value survives leaving and returning to the editing page");
        Click("렌더링", "원형 잘라내기 : 꺼짐");
        check(screen.RenderingPage.CircleCutoutEnabled, "observer circular cutout button changes both retained primitive demonstrations");
        Frame("개요");
        Frame("렌더링");
        check(screen.RenderingPage.CircleCutoutEnabled && context.State.NextScheduledFrameTimeSeconds is null,
            "observer circular subtraction survives tab switches without an animation timer");
        Click("렌더링", "원형 잘라내기 : 켜짐");
        check(!screen.RenderingPage.CircleCutoutEnabled, "observer circular cutout can be disabled through its actual button");
        Click("렌더링", "둥근 클리핑 : 꺼짐");
        var roundedBuilds = screen.RenderingPage.StaticBuilds;
        Frame("개요");
        Frame("렌더링");
        check(screen.RenderingPage.RoundedClipEnabled && screen.RenderingPage.StaticBuilds == roundedBuilds
            && context.State.NextScheduledFrameTimeSeconds is null,
            "observer rounded clip survives tab switches and reuses static paint without scheduling animation");
        Click("렌더링", "둥근 클리핑 : 켜짐");
        check(!screen.RenderingPage.RoundedClipEnabled, "observer rounded clip can be disabled through its actual button");
        Frame("팝업 입력");
        Click("팝업 입력", "입력 팝업 열기");
        check(screen.PopupInputPage.Visible && screen.PopupInputPage.Mode == UiPopupInputMode.NonModal, "observer popup input sample opens actual nonmodal surface");
        Click("팝업 입력", "팝업 안 명령");
        check(screen.PopupInputPage.InsideCount == 1 && screen.PopupInputPage.OutsideCount == 0, "observer popup actual child pointer command changes its model");
        Click("팝업 입력", "입력 팝업 닫기");
        Frame("팝업 입력");
        Click("팝업 입력", "팝업 입력 모드 전환");
        Frame("개요");
        Frame("팝업 입력");
        check(!screen.PopupInputPage.Visible && screen.PopupInputPage.Mode == UiPopupInputMode.Exclusive && screen.PopupInputPage.InsideCount == 1,
            "observer popup input mode and command state persist across tabs");
        Frame("Popover");
        Click("Popover", "팝업 모션");
        check(!screen.PopoverPage.Preview!.Options.MotionEnabled, "observer static input fixture explicitly disables motion");
        Click("Popover", "팝업 화살표");
        check(screen.PopoverPage.Preview!.Options.WithArrow, "observer Popover arrow uses actual widget option");
        Click("Popover", "프로필 편집 열기");
        check(screen.PopoverPage.Preview!.IsOpen && Find("이름", UiAccessibilityRole.Edit).Value == "디모이", "observer actual Popover owns Korean editor");
        Click("Popover", "팝업 명령 실행");
        Click("Popover", "팝업 내부 메뉴");
        Click("Popover", "메뉴 명령 실행");
        check(screen.PopoverPage.Preview.IsOpen, "observer owned menu pointer action preserves its parent Popover");
        Click("Popover", "프로필 편집 닫기");
        Frame("Popover");
        check(!screen.PopoverPage.Preview.IsOpen, "observer Popover child closes owned surface");
        Click("Popover", "팝업 열기 방식");
        var hoverTrigger = Find("프로필 편집 열기", UiAccessibilityRole.Button).Bounds;
        Frame("Popover", Input(new(hoverTrigger.X + hoverTrigger.Width / 2, hoverTrigger.Y + hoverTrigger.Height / 2)));
        check(screen.PopoverPage.Preview.IsOpen && screen.PopoverPage.Preview.Options.TriggerMode == UiPopoverTriggerMode.Hover,
            "observer actual pointer hover opens the selected trigger mode");
        check(Find("이름", UiAccessibilityRole.Edit).LabeledBy == Find("이름", UiAccessibilityRole.Label).Id,
            "reopening the observer Popover restores its live Label relationship after same-frame close retirement");
        Frame("Popover");
        var leaveFrames = (int)Math.Ceiling(screen.PopoverPage.Preview.Options.MouseLeaveDelaySeconds / (1f / 60)) + 1;
        for (var frame = 0; frame < leaveFrames; frame++)
            Frame("Popover");
        check(!screen.PopoverPage.Preview.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "observer hover delayed close returns to unscheduled event wait");
        Click("Popover", "팝업 열기 방식");
        var contextTrigger = Find("프로필 편집 열기", UiAccessibilityRole.Button);
        if (!tree.TryGet(contextTrigger.Id, out var contextTriggerEntry))
            throw new InvalidOperationException("Observer context trigger provider is missing.");
        tree.RequestAction(contextTrigger.Id, contextTriggerEntry.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame("Popover");
        Frame("Popover", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.ContextMenu, true, default)]
        });
        Frame("Popover", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.ContextMenu, false, default)]
        });
        check(screen.PopoverPage.Preview.IsOpen && screen.PopoverPage.Preview.Options.TriggerMode == UiPopoverTriggerMode.Context,
            "observer Menu key opens actual context trigger mode");
        Click("Popover", "프로필 편집 닫기");
        Frame("Popover");
        Click("Popover", "팝업 열기 방식");
        Click("Popover", "팝업 포커스 제한");
        Frame("개요");
        Frame("Popover");
        check(screen.PopoverPage.Preview.Options.TrapFocus, "observer Popover options persist across page switches");
        Click("Popover", "넓은 장식 팝업 열기");
        check(screen.PopoverPage.DecorationPreview!.IsOpen, "observer custom paint-extent sample opens its actual widget");
        Click("Popover", "장식 팝업 닫기");
        check(!screen.PopoverPage.DecorationPreview.IsOpen, "observer custom paint-extent sample closes during entry");
        Frame("도구 모음");
        check(Find("문서 도구", UiAccessibilityRole.Toolbar).Orientation == UiAccessibilityOrientation.Horizontal, "observer Toolbar exposes actual horizontal root");
        Click("도구 모음", "도구 모음 방향");
        Click("도구 모음", "도구 모음 크기");
        Click("도구 모음", "도구 모음 RTL");
        Click("도구 모음", "새 문서");
        Click("도구 모음", "굵게");
        Frame("개요");
        Frame("도구 모음");
        check(screen.ToolbarPage.Preview!.Options is { Vertical: true, Size: UiToolbarSize.Small, RightToLeft: true }
            && Find("굵게", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On,
            "observer real Toolbar layout and child state persist across tabs");
        Frame("검색 입력");
        check(Find("검색어", UiAccessibilityRole.SearchBox).Value == "디모이", "observer SearchBox starts with bound Korean text");
        Click("검색 입력", "검색 읽기 전용");
        check(Find("검색어", UiAccessibilityRole.SearchBox).ReadOnly, "observer real SearchBox readonly button changes editor behavior");
        Click("검색 입력", "검색 크기");
        Click("검색 입력", "검색 외관");
        Click("검색 입력", "검색 방향");
        Frame("개요");
        Frame("검색 입력");
        check(screen.SearchBoxPage.Preview!.Options is { ReadOnly: true, Size: UiSearchBoxSize.Small, Appearance: UiInputAppearance.Underline, RightToLeft: true },
            "observer SearchBox controls persist across tabs");
        Frame("인물 정보");
        check(screen.PersonaPage.Preview is not null && Find("디모이, 사용자 인터페이스 개발, Duxel 프로젝트, 한글·SVG·접근성, 사용 가능", UiAccessibilityRole.Group).Bounds.Height > 0,
            "observer Persona displays actual owned Korean person information");
        Click("인물 정보", "인물 크기");
        Click("인물 정보", "인물 텍스트 위치");
        Click("인물 정보", "인물 정렬");
        Click("인물 정보", "인물 상태 전용");
        Click("인물 정보", "인물 방향");
        Click("인물 정보", "인물 정보 줄 수");
        Click("인물 정보", "인물 SVG 이미지");
        Click("인물 정보", "인물 비활성 표현");
        Frame("개요");
        Frame("인물 정보");
        check(screen.PersonaPage.Preview!.Options is
        {
            Size: UiPersonaSize.ExtraSmall, TextPosition: UiPersonaTextPosition.Before,
            TextAlignment: UiPersonaTextAlignment.Center, PresenceOnly: true, RightToLeft: true
        }
            && screen.PersonaPage.Preview.Content.SecondaryText is null && screen.PersonaPage.Preview.Avatar.Options.IsInactive,
            "observer actual Persona controls and line count persist across page switches");
        Click("인물 정보", "인물 미디어 표시");
        check(Find("디모이", UiAccessibilityRole.Group).Bounds.Height == 16 && context.State.NextScheduledFrameTimeSeconds is null,
            "observer Persona text-only single-line caption schedules no periodic frames");
        Frame("선택 목록");
        check(screen.DropdownPage.Single is not null && screen.DropdownPage.Multiple is not null
            && Find("작업 위치", UiAccessibilityRole.ComboBox).Value == string.Empty,
            "observer Dropdown page owns actual bound single and multiple controls");
        void DropdownPointer(UiAccessibilityNode node)
        {
            node = Reveal("선택 목록", node);
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame("선택 목록", Input(point, down: true, pressed: true));
            Frame("선택 목록", Input(point, released: true));
            Frame("선택 목록");
        }
        DropdownPointer(Find("작업 위치", UiAccessibilityRole.ComboBox));
        check(screen.DropdownPage.Single!.IsOpen, "observer Dropdown pointer opens the actual single-select list");
        DropdownPointer(Find("한국", UiAccessibilityRole.Option));
        check(!screen.DropdownPage.Single.IsOpen && Find("작업 위치", UiAccessibilityRole.ComboBox).Value == "한국",
            "observer Dropdown pointer commits Korean option text to the real single binding");
        DropdownPointer(Find("확인할 기능", UiAccessibilityRole.ComboBox));
        DropdownPointer(Find("이미지", UiAccessibilityRole.Option));
        DropdownPointer(Find("SVG 아이콘", UiAccessibilityRole.Option));
        check(screen.DropdownPage.Multiple!.IsOpen && Find("이미지", UiAccessibilityRole.Option).Selected == true
            && Find("SVG 아이콘", UiAccessibilityRole.Option).Selected == true,
            "observer Dropdown real pointer selections retain both image and SVG options with the popup open");
        Frame("선택 목록", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        Frame("선택 목록");
        Click("선택 목록", "선택 표시 크기");
        Click("선택 목록", "선택 외관 변경");
        Click("선택 목록", "선택 방향 전환");
        Click("선택 목록", "선택 폭 변경");
        Frame("개요");
        Frame("선택 목록");
        check(screen.DropdownPage.Single.Options is { Size: UiDropdownSize.Small, Appearance: UiInputAppearance.Underline, RightToLeft: true, Width: 140 }
            && screen.DropdownPage.Multiple.Options.RightToLeft
            && Find("도시 검색", UiAccessibilityRole.ComboBox).Bounds.Width == 140
            && Find("작업 위치", UiAccessibilityRole.ComboBox).Bounds.Width == 140
            && Find("확인할 기능", UiAccessibilityRole.ComboBox).Bounds.Width == 140
            && Find("작업 위치", UiAccessibilityRole.ComboBox).Value == "한국"
            && Find("확인할 기능", UiAccessibilityRole.ComboBox).Value == "이미지, SVG 아이콘",
            "observer Dropdown actual appearance buttons and both selections survive page switches");
        Click("선택 목록", "SVG 아이콘", UiAccessibilityRole.Button);
        check(Find("SVG 아이콘", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On
            && screen.DropdownPage.ActivatedTag == "svg" && Find("태그 확인 : svg", UiAccessibilityRole.Label).Name == "태그 확인 : svg"
            && Find("확인할 기능", UiAccessibilityRole.ComboBox).Value == "이미지, SVG 아이콘",
            "observer interactive tag primary pointer action reports its canonical value without dismissing selection");
        Frame("선택 목록", Input(new(500, 500)) with
        {
            MouseWheel = -50
        });
        Frame("선택 목록");
        Click("선택 목록", "강조 선택", UiAccessibilityRole.Button);
        check(Find("강조 선택", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On
            && Find("강조 선택 : 켜짐 / 변경 허용", UiAccessibilityRole.Label).Name.Length > 0,
            "observer controlled emphasis commits real selected state and accessible feedback");
        Click("선택 목록", "태그 변경 허용 전환");
        Click("선택 목록", "강조 선택", UiAccessibilityRole.Button);
        check(Find("강조 선택", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On
            && Find("강조 선택 : 켜짐 / 변경 거부", UiAccessibilityRole.Label).Name.Length > 0,
            "observer emphasis rejection preserves the real selected state and describes rejection");
        Frame("개요");
        Frame("선택 목록");
        check(Find("강조 선택", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On,
            "observer emphasis binding survives page switches");
        Click("선택 목록", "디모이", UiAccessibilityRole.Button);
        check(Find("디모이", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On,
            "observer actual Korean-avatar media tag supports independent primary selection");
        Click("선택 목록", "담당자 해제 디모이");
        Click("선택 목록", "담당자 태그 복원");
        check(Find("디모이", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On,
            "observer media dismissal and restoration retain the independent member highlight");
        Click("선택 목록", "담당자 태그 크기");
        Click("선택 목록", "담당자 태그 방향·모양");
        Click("선택 목록", "담당자 비활성 표현");
        Frame("개요");
        Frame("선택 목록");
        check(Find("디모이", UiAccessibilityRole.Button).Bounds.Height == 24
            && Find("디모이", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On,
            "observer actual avatar media reuses inactive composites and preserves size/state across page switches");
        Frame("선택 목록", Input(new(500, 500)) with
        {
            MouseWheel = 50
        });
        Frame("선택 목록");
        Click("선택 목록", "해제 SVG 아이콘");
        check(Find("확인할 기능", UiAccessibilityRole.ComboBox).Value == "이미지"
            && !Find("SVG 아이콘", UiAccessibilityRole.Option).Selected!.Value,
            "observer real tag dismissal updates the same Dropdown selection and hidden logical option");
        DropdownPointer(Find("확인할 기능", UiAccessibilityRole.ComboBox));
        DropdownPointer(Find("SVG 아이콘", UiAccessibilityRole.Option));
        Frame("선택 목록", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        Frame("선택 목록");
        check(Find("SVG 아이콘", UiAccessibilityRole.Button).ToggleState == UiAccessibilityToggleState.On
            && Find("확인할 기능", UiAccessibilityRole.ComboBox).Value == "이미지, SVG 아이콘",
            "observer feature restoration retains independent tag emphasis through actual Dropdown selection");
        Click("선택 목록", "태그 크기 전환");
        Frame("개요");
        Frame("선택 목록");
        var resizedTag = Reveal("선택 목록", Find("해제 이미지", UiAccessibilityRole.Button));
        check(resizedTag.Bounds.Height == 24,
            "observer tag size control and selection survive page switches; bounds : " + resizedTag.Bounds);
        Click("선택 목록", "선택 지우기");
        check(Find("작업 위치", UiAccessibilityRole.ComboBox).Value == string.Empty
            && Find("확인할 기능", UiAccessibilityRole.ComboBox).Value == string.Empty,
            "observer Dropdown reset immediately clears both authoritative bindings");
        Console.WriteLine($"Dropdown reset schedule : now = {context.State.TimeSeconds}, next = {context.State.NextScheduledFrameTimeSeconds}");
        // Revealing this now-distant button schedules the scrollbar's finite motion.
        var resetMotion = new UiScrollbarMotionOptions();
        context.State.AdvanceTime((float)(resetMotion.Enter.DurationSeconds + resetMotion.HoldSeconds + resetMotion.Exit.DurationSeconds));
        Frame("선택 목록");
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "observer Dropdown reset schedules no periodic frames after the scroll reveal's finite motion finishes");
        var city = Reveal("선택 목록", Find("도시 검색", UiAccessibilityRole.ComboBox));
        if (!tree.TryGet(city.Id, out var cityElement))
            throw new InvalidOperationException("Combobox root is missing.");
        tree.RequestAction(city.Id, cityElement.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame("선택 목록");
        tree.RequestAction(city.Id, cityElement.RuntimeId, UiAccessibilityAction.SetValue, "New ", wait: false);
        Frame("선택 목록");
        check(screen.DropdownPage.Editable!.IsOpen && !Find("New York", UiAccessibilityRole.Option).Offscreen
            && Find("서울", UiAccessibilityRole.Option).Offscreen,
            "observer editable Combobox actually filters the persistent city options");
        DropdownPointer(Find("New York", UiAccessibilityRole.Option));
        Frame("개요");
        Frame("선택 목록");
        check(Find("도시 검색", UiAccessibilityRole.ComboBox).Value == "New York" && !screen.DropdownPage.Editable.IsOpen,
            "observer editable Combobox pointer selection and input value survive page changes");
        var openCount = screen.DropdownPage.OpenChanges;
        Click("선택 목록", "도시 목록 열림 전환");
        check(screen.DropdownPage.Editable.IsOpen && screen.DropdownPage.Editable.Open!.Value
            && screen.DropdownPage.OpenChanges == openCount + 1,
            "observer city popup button opens the actual controlled list and reports one transition");
        Frame("선택 목록");
        Frame("선택 목록");
        check(screen.DropdownPage.OpenChanges == openCount + 1,
            "observer city popup stable frames do not increase its displayed transition counter");
        screen.DropdownPage.Editable.Open!.Value = false;
        Frame("선택 목록");
        check(!screen.DropdownPage.Editable.IsOpen && screen.DropdownPage.OpenChanges == openCount + 2,
            "observer city popup model closes the real list with one matching transition notification");
        Click("선택 목록", "검증 상태 전환");
        Frame("개요");
        Frame("선택 목록");
        check(Find("도시 검색", UiAccessibilityRole.ComboBox).Invalid && Find("도시 검색", UiAccessibilityRole.Edit).Invalid
            && Find("작업 위치", UiAccessibilityRole.ComboBox).Invalid && Find("확인할 기능", UiAccessibilityRole.ComboBox).Invalid,
            "observer validation toggle updates all actual option controls and survives page switches");
        Click("선택 목록", "검증 상태 전환");
        check(!Find("도시 검색", UiAccessibilityRole.ComboBox).Invalid && !Find("도시 검색", UiAccessibilityRole.Edit).Invalid
            && !Find("작업 위치", UiAccessibilityRole.ComboBox).Invalid && !Find("확인할 기능", UiAccessibilityRole.ComboBox).Invalid,
            "observer validation toggle clears all option control native validation states");
        Frame("경로");
        check(screen.BreadcrumbPage.Preview is not null && Find("프로젝트 경로", UiAccessibilityRole.Navigation).Role == UiAccessibilityRole.Navigation,
            "observer exposes the actual Breadcrumb page and navigation landmark");
        Click("경로", "홈");
        check(screen.BreadcrumbPage.LastNavigation == "home" && screen.BreadcrumbPage.Navigations == 1,
            "observer Breadcrumb root invokes its real navigation callback");
        Click("경로", "경로 폭 변경");
        check(Find("숨긴 경로", UiAccessibilityRole.Button).Expansion == UiAccessibilityExpansionState.Collapsed,
            "observer Breadcrumb width control exposes its actual hidden-level menu trigger");
        Click("경로", "숨긴 경로");
        check(screen.BreadcrumbPage.Preview!.IsOverflowOpen, "observer Breadcrumb overflow opens the shared menu through pointer input");
        Frame("경로", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        Frame("경로");
        Click("경로", "경로 키보드 방식");
        Click("경로", "경로 방향 전환");
        Frame("개요");
        Frame("경로");
        check(screen.BreadcrumbPage.Preview!.Options is { FocusMode: UiBreadcrumbFocusMode.Arrow, RightToLeft: true }
            && screen.BreadcrumbPage.Navigations == 1, "observer Breadcrumb settings and navigation survive page switches");
        Frame("아바타");
        var avatar = screen.AvatarPage.Preview!;
        Click("아바타", "아바타 이미지");
        check(avatar.Source is not null, "observer Avatar image button selects decoded SVG pixels");
        Click("아바타", "아바타 원형·사각");
        check(avatar.Options.Shape == UiAvatarShape.Square, "observer Avatar shape button updates actual presentation options");
        Click("아바타", "아바타 SVG 아이콘");
        check(avatar.Source is null && avatar.Initials == "" && !avatar.Icon.IsEmpty,
            "observer Avatar icon button selects the borrowed retained SVG painter");
        Click("아바타", "아바타 한글 이니셜");
        check(avatar.Name == "김민수" && avatar.DisplayInitials == "김" && avatar.Initials is null, "observer Avatar derives Korean initials from its actual name");
        Frame("개요");
        Frame("아바타");
        check(ReferenceEquals(avatar, screen.AvatarPage.Preview) && avatar.DisplayInitials == "김"
            && avatar.Options.Shape == UiAvatarShape.Square, "observer Avatar state survives page changes");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "observer Avatar image glyphs and counters schedule no periodic rendering");
        var avatarName = Find("아바타 이름", UiAccessibilityRole.Edit);
        tree.TryGet(avatarName.Id, out var avatarNameElement);
        check(tree.RequestAction(avatarName.Id, avatarNameElement.RuntimeId, UiAccessibilityAction.SetValue,
            "디모이", wait: false) == UiAccessibilityResult.Success, "observer actual Avatar name input accepts Korean editing");
        Frame("아바타");
        check(avatar.Name == "디모이" && avatar.DisplayInitials == "디" && avatar.Initials is null,
            "observer editing the actual Avatar name derives Di without retaining a hardcoded Korean preset initial");
        Click("아바타", "아바타 활동 링");
        check(avatar.Options.ShowActivityRing, "observer activity button changes the actual retained Avatar ring");
        Frame("개요");
        Frame("아바타");
        check(avatar.Options.ShowActivityRing && context.State.NextScheduledFrameTimeSeconds is null,
            "observer activity ring survives page switches without periodic updates");
        Click("아바타", "아바타 활동 링");
        check(!avatar.Options.ShowActivityRing, "observer activity button hides the ring again");
        Click("아바타", "아바타 활동 그림자");
        Frame("개요");
        Frame("아바타");
        check(avatar.Options.ShowActivityShadow && context.State.NextScheduledFrameTimeSeconds is null,
            "observer Avatar activity shadow persists across page changes without scheduling frames");
        Click("아바타", "아바타 비활성 전환");
        Frame("개요");
        Frame("아바타");
        check(avatar.Options.IsInactive && !context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "observer inactive Avatar toggle changes the real widget and survives page switches without animation");
        Click("아바타", "아바타 비활성 전환");
        check(!avatar.Options.IsInactive && avatar.Options.ShowActivityShadow,
            "observer restoring the Avatar preserves its selected activity decoration");
        Click("아바타", "아바타 상태 배지");
        check(avatar.Options.Presence is { Status: UiPresenceStatus.Available, Size: null }
            && avatar.PresenceText == "사용 가능", "observer Avatar enables an automatic localized presence overlay");
        Click("아바타", "아바타 상태 변경");
        Click("아바타", "아바타 외근 전환");
        Click("아바타", "아바타 배지 크기");
        Click("아바타", "아바타 좌우 전환");
        Frame("개요");
        Frame("아바타");
        check(avatar.Options.Presence is { Status: UiPresenceStatus.Away, Size: UiBadgeSize.Tiny, OutOfOffice: true }
            && avatar.Options.RightToLeft && avatar.PresenceText == "자리 비움 외근"
            && context.State.NextScheduledFrameTimeSeconds is null,
            "observer Avatar overlay controls persist real status size RTL and localized semantics without timers");
        Click("아바타", "상태 배지 변경");
        check(screen.AvatarPage.Presence!.Options.Status == UiPresenceStatus.Away,
            "observer presence control changes the real status badge");
        Click("아바타", "상태 배지 외근 전환");
        check(screen.AvatarPage.Presence!.Options.OutOfOffice && screen.AvatarPage.Presence.StatusText == "away out of office",
            "observer out-of-office control updates the real badge description");
        Click("아바타", "상태 배지 크기 변경");
        check(screen.AvatarPage.Presence!.Options.Size == UiBadgeSize.Medium,
            "observer presence size control changes the real glyph size");
        Frame("개요");
        Frame("아바타");
        check(screen.AvatarPage.Presence!.Options.Status == UiPresenceStatus.Away
            && screen.AvatarPage.Presence.Options.OutOfOffice && context.State.NextScheduledFrameTimeSeconds is null,
            "observer presence state survives page changes without scheduling periodic rendering");
        Frame("접기·펼치기");
        var accordionHeader = Find("입력 내용 보존", UiAccessibilityRole.Button);
        tree.TryGet(accordionHeader.Id, out var accordionElement);
        check(tree.RequestAction(accordionHeader.Id, accordionElement.RuntimeId, UiAccessibilityAction.Expand, wait: false) == UiAccessibilityResult.Success,
            "observer Accordion header accepts expansion through its actual accessibility control");
        RenderFrame();
        var accordionEditor = Find("접기 예제 메모", UiAccessibilityRole.Edit);
        tree.TryGet(accordionEditor.Id, out var accordionEditorElement);
        check(tree.RequestAction(accordionEditor.Id, accordionEditorElement.RuntimeId, UiAccessibilityAction.SetValue,
            "접은 뒤에도 유지", wait: false) == UiAccessibilityResult.Success,
            "observer Accordion actual retained editor accepts a bound text update");
        RenderFrame();
        Frame("개요");
        Frame("접기·펼치기");
        check(Find("접기 예제 메모", UiAccessibilityRole.Edit).Value == "접은 뒤에도 유지",
            "observer Accordion value and open panel survive switching top-level tabs");
        Frame("Tooltip");
        check(Find("설명 대상").GetHelpText().Length > 0 && Find("세부 정보").Role == UiAccessibilityRole.Button,
            "observer Tooltip page publishes actual description and label target relationships");
        check(screen.UpdateOnlyOnChange, "observer defaults to event waiting without a metrics timer");
        UiAccessibilityNode Reveal(string page, UiAccessibilityNode node)
        {
            var element = tree.CaptureSnapshot().Elements.Single(entry => entry.Node.Id == node.Id);
            if (!node.CanScrollIntoView)
            {
                if (node.Offscreen) throw new InvalidOperationException("Observer target has no visible or revealable geometry : " + node.Name);
                return node;
            }
            if (tree.RequestAction(node.Id, element.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Observer target could not be revealed : " + node.Name);
            for (var revealFrame = 0; revealFrame < 3; revealFrame++) Frame(page);
            return Find(node.Name, node.Role);
        }
        void Click(string page, string name, UiAccessibilityRole? role = null)
        {
            Frame(page);
            var node = Reveal(page, Find(name, role));
            var pointer = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(page, Input(pointer, down: true, pressed: true));
            Frame(page, Input(pointer, released: true));
            Frame(page);
        }
        Click("카드", "떠 있는 카드 선택");
        check(screen.CardPage.FloatingSelected && screen.CardPage.FloatingWrites == 1,
            "observer actual floating checkbox writes its shared Card model once");
        Click("카드", "떠 있는 카드 내부 실행");
        check(screen.CardPage.FloatingExecutions == 1 && screen.CardPage.FloatingWrites == 1,
            "observer floating Card keeps its actual child action independent of selection");
        var clippedDemo = Find("둥근 카드 내부 실행");
        tree.TryGet(clippedDemo.Id, out var clippedDemoElement);
        check(tree.RequestAction(clippedDemo.Id, clippedDemoElement.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) == UiAccessibilityResult.Success,
            "observer clipped Card sample is reachable through its actual ScrollItem provider");
        Frame("카드");
        Frame("카드");
        Click("카드", "둥근 카드 내부 실행");
        check(screen.CardPage.ClippedExecutions == 1, "observer actual zero-padding rounded Card executes its owned button");
        Click("카드", "둥근 모서리 : 켜짐");
        Frame("개요");
        Frame("카드");
        check(!screen.CardPage.ClippingEnabled && screen.CardPage.ClippedExecutions == 1,
            "observer custom Card radius and counter survive real page switches");
        Click("카드", "둥근 모서리 : 꺼짐");
        check(screen.CardPage.ClippingEnabled, "observer restores rounded Card clipping through its actual toggle");
        var mediaDemo = Find("미리보기 폭 변경");
        tree.TryGet(mediaDemo.Id, out var mediaElement);
        check(tree.RequestAction(mediaDemo.Id, mediaElement.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) == UiAccessibilityResult.Success,
            "observer media Preview controls are reachable through the actual ScrollItem provider");
        Frame("카드");
        Frame("카드");
        var mediaWidth = Find("미리보기 카드").Bounds.Width;
        Click("카드", "미리보기 폭 변경");
        check(Find("미리보기 카드").Bounds.Width == mediaWidth + 80,
            "observer actual Preview width button relayouts its full-bleed SVG card");
        Click("카드", "미리보기 크기 변경");
        Frame("개요");
        Frame("카드");
        check(Find("미리보기 카드").Bounds.Width == mediaWidth + 80 && context.State.NextScheduledFrameTimeSeconds is null,
            "observer Preview options persist across tabs without periodic rendering");
        var minimumDemo = Find("최소 크기 카드");
        tree.TryGet(minimumDemo.Id, out var minimumElement);
        check(tree.RequestAction(minimumDemo.Id, minimumElement.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) == UiAccessibilityResult.Success,
            "observer minimum-only Card example exposes its actual ScrollItem reveal");
        Frame("카드");
        Frame("카드");
        var minimumWidth = Find("최소 크기 카드").Bounds.Width;
        Click("카드", "부모 최소 폭 변경");
        check(Find("최소 크기 카드").Bounds.Width == minimumWidth + 60,
            "observer actual parent minimum button expands its owned Card without a fixed width");
        Click("카드", "최소 크기 카드 방향");
        Frame("개요");
        Frame("카드");
        check(Find("최소 크기 카드").Bounds.Width == minimumWidth + 60 && context.State.NextScheduledFrameTimeSeconds is null,
            "observer minimum-only Card keeps its width and orientation model across tabs without a timer");
        var cardScroll = Find("갤러리 페이지", UiAccessibilityRole.Pane);
        tree.TryGet(cardScroll.Id, out var cardScrollElement);
        tree.RequestScroll(cardScroll.Id, cardScrollElement.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 0), wait: false);
        Frame("카드");
        Frame("카드");
        Click("카드", "카드 내부 실행");
        check(screen.CardPage.Executions == 1, "observer Card executes its actual owned button");
        Click("카드", "카드 정보");
        check(screen.CardPage.HeaderExecutions == 1 && screen.CardPage.RootExecutions == 0 && screen.CardPage.SelectionWrites == 0,
            "observer Card Header action updates its real description independently of root action and selection");
        Click("카드", "카드 메모");
        Frame("카드", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.End, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        Frame("카드", Input(new(-100, -100)) with
        {
            CharEvents = [new('x')]
        });
        check(screen.CardPage.Memo == "편집하세요x", "observer Card keeps real text editing in its owned input");
        Click("카드", "카드 외관 변경");
        Click("카드", "카드 크기 변경");
        Click("카드", "카드 폭 변경");
        var retainedCard = screen.CardPage.Preview ?? throw new InvalidOperationException("Observer Card preview was not initialized.");
        Frame("개요");
        Frame("카드");
        check(ReferenceEquals(retainedCard, screen.CardPage.Preview) && retainedCard.Options is
        { Size: UiCardSize.Small, Appearance: UiCardAppearance.FilledAlternative, Width: 240 }
            && screen.CardPage.Memo == "편집하세요x" && screen.CardPage.Executions == 1,
            "observer Card content and options survive page changes on the same owned instances");
        check(!Find("작업 카드", UiAccessibilityRole.Card).Focusable && context.State.NextScheduledFrameTimeSeconds is null,
            "observer passive Card adds no root focus stop or periodic frame");
        Click("카드", "카드 포커스 방식");
        var focusCard = Find("작업 카드", UiAccessibilityRole.Card);
        tree.TryGet(focusCard.Id, out var focusCardElement);
        tree.RequestAction(focusCard.Id, focusCardElement.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame("카드");
        Frame("카드", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Enter, true, default)]
        });
        Frame("카드", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Enter, false, default)]
        });
        check(context.State.FocusedId == Find("카드 정보", UiAccessibilityRole.Button).Id
            && screen.CardPage.Executions == 1 && screen.CardPage.HeaderExecutions == 1,
            "observer actual Card Enter enters its first Header action without activating any child");
        Frame("카드", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Tab, true, default)]
        });
        Frame("카드", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Tab, false, default)]
        });
        check(context.State.FocusedId == Find("카드 메모", UiAccessibilityRole.Edit).Id,
            "observer Card Tab proceeds from the real Header action into the retained input");
        Frame("카드", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        Frame("카드", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Escape, false, default)]
        });
        check(context.State.FocusedId == focusCard.Id, "observer actual Card Escape returns to its named group");
        Click("카드", "카드 루트 사용 전환");
        Click("카드", "카드 내부 실행");
        check(!Find("작업 카드", UiAccessibilityRole.Card).Enabled && screen.CardPage.Executions == 2,
            "observer disables only the Card root while its actual child action stays live");
        Click("카드", "카드 루트 사용 전환");
        for (var mode = 0; mode < 3; mode++)
            Click("카드", "카드 포커스 방식");
        check(retainedCard.Options is { FocusMode: UiFocusGroupMode.Off, Disabled: false },
            "observer Card mode controls cycle through all four choices and restore the default");
        Click("카드", "카드 루트 동작 전환");
        var actionCard = Find("작업 카드", UiAccessibilityRole.Card);
        var background = new UiVector2(actionCard.Bounds.X + actionCard.Bounds.Width - 4, actionCard.Bounds.Y + actionCard.Bounds.Height - 4);
        Frame("카드", Input(background, down: true, pressed: true));
        Frame("카드", Input(background, released: true));
        Click("카드", "카드 내부 실행");
        check(screen.CardPage.RootExecutions == 1 && screen.CardPage.Executions == 3,
            "observer actual Card root toggle separates background activation from its owned child button");
        Click("카드", "카드 루트 동작 전환");
        Click("카드", "카드 선택 모드 전환");
        var selectableCard = Find("작업 카드", UiAccessibilityRole.Card);
        var selectionCheckbox = Find("작업 카드", UiAccessibilityRole.Checkbox);
        check(!selectableCard.Focusable && selectionCheckbox.Focusable,
            "observer actual Card selection toggle exposes its checkbox instead of a group trap");
        var selectionPoint = new UiVector2(selectableCard.Bounds.X + selectableCard.Bounds.Width - 4,
            selectableCard.Bounds.Y + selectableCard.Bounds.Height - 4);
        Frame("카드", Input(selectionPoint, down: true, pressed: true));
        Frame("카드", Input(selectionPoint, released: true));
        Click("카드", "카드 내부 실행");
        check(screen.CardPage.Selected && screen.CardPage.SelectionWrites == 1 && screen.CardPage.Executions == 4
            && screen.CardPage.RootExecutions == 1, "observer background selects once while its actual child button remains independent");
        tree.TryGet(selectionCheckbox.Id, out var selectionElement);
        tree.RequestAction(selectionCheckbox.Id, selectionElement.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        Frame("카드");
        Frame("개요");
        Frame("카드");
        check(!screen.CardPage.Selected && screen.CardPage.SelectionWrites == 2 && ReferenceEquals(retainedCard, screen.CardPage.Preview),
            "observer real Card Toggle binding and model persist across page changes");
        Click("카드", "카드 선택 모드 전환");
        Click("컨테이너", "열 수 : 3");
        check(Find("열 수 : 2").Enabled && Find("셀 6").Bounds.Y > Find("셀 1").Bounds.Y,
            "observer grid changes its real child geometry through the actual column-count button");
        Frame("배치 통합");
        var overflowTabGroup = Find("넘침 비교", UiAccessibilityRole.TabList);
        tree.TryGet(overflowTabGroup.Id, out var overflowTabElement);
        check(tree.RequestAction(overflowTabGroup.Id, overflowTabElement.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) == UiAccessibilityResult.Success,
            "observer actual overflow tab group participates in its real enclosing ScrollView navigation");
        Frame("배치 통합");
        Frame("배치 통합");
        var overflowBody = Find("넘침 본문 홈 : 0");
        tree.TryGet(overflowBody.Id, out var overflowBodyElement);
        tree.RequestAction(overflowBody.Id, overflowBodyElement.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false);
        Frame("배치 통합");
        Frame("배치 통합");
        check(!Find("넘침 비교", UiAccessibilityRole.TabList).Offscreen && !Find("넘침 본문 홈 : 0").Offscreen,
            "observer brings the actual overflow header and selected panel into the viewport");
        Click("배치 통합", "넘침 본문 홈 : 0");
        tree.TryGet(overflowTabGroup.Id, out overflowTabElement);
        var overflowActionId = overflowTabElement.FirstChild;
        UiAccessibilityElement? overflowAction = null;
        while (overflowActionId is not null && tree.TryGet(overflowActionId, out var childTab))
        {
            if (childTab.Node.SupportsInvoke)
                overflowAction = childTab;
            overflowActionId = childTab.Next;
        }
        Click("배치 통합", overflowAction?.Node.Name ?? throw new InvalidOperationException("Observer tab overflow action missing"));
        check(!Find("설정", UiAccessibilityRole.MenuItem).Enabled, "observer actual overflow menu retains the disabled settings tab");
        Click("배치 통합", "도움말");
        check(Find("넘침 본문 도움말 : 0").Enabled && Find("도움말", UiAccessibilityRole.Tab).Selected == true,
            "observer real overflow pointer selection updates the promoted header and actual body");
        Click("배치 통합", "홈");
        check(Find("넘침 본문 홈 : 1").Enabled, "observer real overflow page switch preserves its body counter instance");
        Click("배치 통합", "탭 배치 : 가로");
        check(Find("탭 배치 : 세로").Enabled && Find("문서", UiAccessibilityRole.Tab).Bounds.Y > Find("홈", UiAccessibilityRole.Tab).Bounds.Y,
            "observer actual orientation control switches the overflowing tab header to vertical allocation");
        Click("배치 통합", "탭 배치 : 세로");
        Frame("배치 통합", Input(new(500, 500)) with
        {
            MouseWheel = -50
        });
        Frame("배치 통합");
        check(!Reveal("배치 통합", Find("넘침 폭 : 200")).Offscreen && !Reveal("배치 통합", Find("넘침 항목 1 : 0")).Offscreen,
            "observer container scrolling reveals the actual overflow controls and prioritized first child");
        Click("배치 통합", "넘침 항목 1 : 0");
        Click("배치 통합", "넘침 폭 : 200");
        check(!Find("넘침 항목 4 : 0").Offscreen && Find("넘침 항목 1 : 1").Enabled,
            "observer width expansion exposes every real overflow counter and preserves the first value");
        Click("배치 통합", "넘침 항목 4 : 0");
        Click("배치 통합", "넘침 폭 : 560");
        Click("배치 통합", "우선 항목 : 1");
        check(!Find("넘침 항목 4 : 1").Offscreen && Find("우선 항목 : 4").Enabled,
            "observer priority changes restore the same fourth counter through actual sample callbacks");
        var leftPosition = Find("넘침 항목 4 : 1").Bounds.X;
        Click("배치 통합", "넘침 방향 : LTR");
        check(Find("넘침 방향 : RTL").Enabled && Find("넘침 항목 4 : 1").Bounds.X > leftPosition,
            "observer RTL control changes the actual overflow child placement");
        Click("배치 통합", "공통 메뉴 열기");
        check(Find("마지막 동작").Role == UiAccessibilityRole.MenuItem && !Find("사용할 수 없는 동작").Enabled,
            "observer opens the actual shared menu and preserves its disabled item semantics");
        var ambiguousRejected = false;
        try
        {
            Find("공통 메뉴 열기");
        }
        catch (InvalidOperationException error) { ambiguousRejected = error.Message.Contains("ambiguous", StringComparison.Ordinal); }
        check(ambiguousRejected, "observer lookup rejects ambiguous trigger/surface names before issuing actions");
        var expandedTrigger = Find("공통 메뉴 열기", UiAccessibilityRole.Button);
        tree.TryGet(expandedTrigger.Id, out var expandedElement);
        check(expandedTrigger.Expansion == UiAccessibilityExpansionState.Expanded,
            "observer actual menu trigger publishes its expanded state");
        check(tree.RequestAction(expandedTrigger.Id, expandedElement.RuntimeId, UiAccessibilityAction.Collapse, wait: false) == UiAccessibilityResult.Success,
            "observer queues Collapse on the actual button rather than its namesake menu surface");
        Frame("배치 통합");
        check(tree.RequestAction(expandedTrigger.Id, expandedElement.RuntimeId, UiAccessibilityAction.Expand, wait: false) == UiAccessibilityResult.Success,
            "observer queues Expand on the actual retained button");
        Frame("배치 통합");
        check(Find("공통 메뉴 열기", UiAccessibilityRole.Button).Expansion == UiAccessibilityExpansionState.Expanded && !Find("마지막 동작").Offscreen,
            "observer actual menu accepts Collapse/Expand through the same accessibility contract");
        var menuItem = Find("마지막 동작");
        tree.TryGet(menuItem.Id, out var menuElement);
        Click("배치 통합", "마지막 동작");
        check(!tree.TryGet(menuItem.Id, out _) && tree.RequestAction(menuItem.Id, menuElement.RuntimeId,
            UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Unavailable,
            "observer actual menu pointer invocation dismisses the popup and retires its old semantic identity");
        Click("배치 통합", "Fluent 메뉴 열기");
        check(Find("Fluent 첫 번째").Role == UiAccessibilityRole.MenuItem && !Find("Fluent 사용 불가").Enabled,
            "observer exposes the generated Fluent Menu factory through its catalog-mapped actual demo");
        Click("배치 통합", "Fluent 마지막");
        check(Find("Fluent 메뉴 열기").Enabled,
            "observer generated Fluent menu accepts a real pointer action and returns to its trigger");
        Click("배치 통합", "긴 메뉴·문자 탐색");
        var hiddenMenuItem = Find("Zulu 마지막 동작");
        tree.TryGet(hiddenMenuItem.Id, out var hiddenMenuElement);
        check(hiddenMenuItem.CanScrollIntoView && tree.RequestAction(hiddenMenuItem.Id, hiddenMenuElement.RuntimeId,
            UiAccessibilityAction.ScrollIntoView, wait: false) == UiAccessibilityResult.Success,
            "observer actual long menu exposes ScrollItem to reveal its hidden last action");
        Frame("배치 통합");
        Frame("배치 통합");
        Frame("배치 통합");
        check(!Find("Zulu 마지막 동작").Offscreen,
            "observer actual menu reveal succeeds independently of its scrolling page owner");
        Frame("배치 통합", Input(new(-100, -100)) with
        {
            CharEvents = new UiCharEvent[] { new('z') }
        });
        check(Find("Zulu 마지막 동작").Focused && !Find("Zulu 마지막 동작").Offscreen,
            "observer actual long-menu demo reveals its last action through printable text");
        var longItem = Find("Zulu 마지막 동작");
        Frame("배치 통합", Input(new(-100, -100)) with
        {
            KeyEvents = new UiKeyEvent[] { new(UiKey.Enter, true, 0) }
        });
        Frame("배치 통합");
        check(!tree.TryGet(longItem.Id, out _) && Find("긴 메뉴·문자 탐색").Focused,
            "observer actual long-menu keyboard invocation dismisses its rows and restores the trigger");
        Click("배치 통합", "체크 메뉴 열기");
        check(Find("상세 정보 표시").Checked == true && Find("격자 표시").Checked == false,
            "observer actual checkbox menu reads its two caller-owned settings");
        Click("배치 통합", "격자 표시");
        check(Find("격자 표시").Checked == true && Find("변경할 수 없는 옵션").Checked == true,
            "observer checkbox pointer activation stays open and updates its disabled shared-state peer");
        Click("배치 통합", "설정 마침");
        Click("배치 통합", "라디오 메뉴 열기");
        check(Find("이름순 정렬").Selected == true && Find("간결하게 보기").Selected == true
            && Find("정렬 기준", UiAccessibilityRole.MenuGroup).SupportsSelection,
            "observer actual radio menu exposes independent named selection groups");
        Click("배치 통합", "날짜순 정렬");
        Click("배치 통합", "아이콘 메뉴 열기");
        check(Find("새 항목 선택").GetHelpText() == "템플릿" && Find("형식 선택").GetHelpText() == "PNG"
            && !Find("사용할 수 없는 항목").Enabled,
            "observer actual menu exposes icon/secondary content and disabled information through the shared item semantics");
        Click("배치 통합", "형식 선택");
        Click("배치 통합", "메뉴 방향 : LTR");
        Click("배치 통합", "아이콘 메뉴 열기");
        check(Find("새 항목 선택").GetHelpText() == "템플릿", "observer RTL content menu preserves its action name and secondary information");
        Click("배치 통합", "새 항목 선택");
        Click("배치 통합", "라디오 메뉴 열기");
        check(Find("날짜순 정렬").Selected == true && Find("이름순 정렬").Selected == false && Find("간결하게 보기").Selected == true,
            "observer radio pointer action changes only one group and preserves it after reopen");
        Click("배치 통합", "넓게 보기");
        Frame("개요");
        Frame("배치 통합");
        Click("배치 통합", "체크 메뉴 열기");
        check(Find("격자 표시").Checked == true, "observer checked settings survive actual page navigation and reopen");
        Click("배치 통합", "설정 마침");
        Click("배치 통합", "라디오 메뉴 열기");
        check(Find("날짜순 정렬").Selected == true && Find("넓게 보기").Selected == true,
            "observer radio settings survive actual page navigation");
        Click("배치 통합", "날짜순 정렬");
        check(Find("넘침 항목 4 : 1").Enabled && Find("우선 항목 : 4").Enabled,
            "observer overflow state survives leaving and returning to its actual page");
        Frame("가상 목록");
        var row = Find("행 값 : 0");
        tree.TryGet(row.Id, out var rowElement);
        check(tree.RequestAction(row.Id, rowElement.RuntimeId, UiAccessibilityAction.SetValue, "키로 유지하는 값", wait: false) == UiAccessibilityResult.Success,
            "observer virtual row accepts an actual edit action");
        Frame("가상 목록");
        Click("가상 목록", "끝");
        check(screen.VirtualPage.Offset > 40_000_000_000d && screen.VirtualPage.RealizedCount < 64 && Find("행 값 : 999999999").Enabled,
            "observer reaches the billionth variable-height row with bounded realization");
        Click("가상 목록", "처음");
        check(Find("행 값 : 0").Value == "키로 유지하는 값", "observer preserves edited model text after virtual widget eviction and recreation");
        Click("가상 목록", "키 순서 바꾸기");
        Click("가상 목록", "끝");
        check(Find("행 값 : 0").Value == "키로 유지하는 값", "observer preserves the edited key while its index moves from first to last");
        Click("가상 목록", "행 높이 : 가변");
        check(Find("행 높이 : 고정").Enabled && screen.VirtualPage.RealizedCount < 64,
            "observer switches the actual virtual extent mode without unbounded realization");
        Frame("렌더링");
        var builds = screen.RenderingPage.StaticBuilds;
        Frame("렌더링");
        check(screen.RenderingPage.StaticBuilds == builds, "observer static layer skips its actual drawing callback on unchanged frames");
        Click("렌더링", "정적 내용 변경");
        check(screen.RenderingPage.StaticBuilds == builds + 1, "observer content revision rebuilds its actual static layer once");
        Click("렌더링", "애니메이션 : 꺼짐");
        check(context.State.NextScheduledFrameTimeSeconds > context.State.TimeSeconds,
            "observer schedules a future frame when its explicit animation is enabled");
        Click("렌더링", "애니메이션 : 켜짐");
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "observer stops scheduling animation deadlines after its animation is disabled");
        Frame("SVG");
        var svgBefore = screen.SvgPage.Statistics!.Value;
        var svgFonts = screen.SvgPage.FontStatistics;
        var svgUploads = screen.SvgPage.TextureStatistics.GpuRenderRequests;
        for (var repeat = 0; repeat < 10; repeat++)
            Frame("SVG");
        check(screen.SvgPage.Statistics == svgBefore && screen.SvgPage.TextureStatistics.GpuRenderRequests == svgUploads,
            "observer actual SVG page reuses XML, scene, raster and texture across ten unchanged frames");
        check(svgFonts is { LoadedFaces: 3, GlyphChecks: > 0 } && screen.SvgPage.FontStatistics == svgFonts,
            "observer actual SVG text uses registered fonts and repeats without font or glyph processing");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "observer SVG page counters and icons do not schedule animation or periodic frames");
        void SetSvgRange(int index, float fraction)
        {
            string[] names = ["SVG 확대", "SVG 회전", "SVG 여백", "이미지·SVG 모서리"];
            var slider = Reveal("SVG", Find(names[index], UiAccessibilityRole.Slider));
            // The default20px thumb insets the actual track by10px at each end.
            var point = new UiVector2(slider.Bounds.X + 10 + fraction * (slider.Bounds.Width - 20),
                slider.Bounds.Y + slider.Bounds.Height / 2);
            Frame("SVG", Input(point, down: true, pressed: true));
            Frame("SVG", Input(point, released: true));
            Frame("SVG");
        }
        SetSvgRange(0, (2f - .25f) / (4f - .25f));
        check(screen.SvgPage.Preview!.Zoom == 2 && screen.SvgPage.Statistics!.Value.XmlParses == svgBefore.XmlParses &&
            screen.SvgPage.Statistics!.Value.Rasterizations == svgBefore.Rasterizations && screen.SvgPage.FontStatistics == svgFonts,
            "observer actual SVG zoom control changes image/text resolution without another XML parse or font lookup");
        var retainedSvgScene = ((IUiGpuSvgDocument)screen.SvgPage.Preview.Document).GpuScene;
        var afterZoom = screen.SvgPage.Statistics;
        SetSvgRange(1, .25f);
        SetSvgRange(2, .2f);
        check(screen.SvgPage.Preview.RotationDegrees == 90 && screen.SvgPage.Preview.Offset.X == 20 &&
            screen.SvgPage.Statistics == afterZoom && screen.SvgPage.TextureStatistics.UploadedBytes == 0 &&
            ReferenceEquals(retainedSvgScene, ((IUiGpuSvgDocument)screen.SvgPage.Preview.Document).GpuScene),
            "observer SVG transforms retain the GPU scene while a visible camera window may change");
        Frame("개요");
        Frame("SVG");
        check(screen.SvgPage.Preview.Zoom == 2 && screen.SvgPage.Statistics == afterZoom,
            "observer SVG page retains its source cache and transform model after leaving and returning");
        Click("SVG", "SVG 파일 다시 읽기");
        check(screen.SvgPage.Statistics!.Value.SourceReads == afterZoom!.Value.SourceReads + 5 &&
            screen.SvgPage.Statistics!.Value.XmlParses == afterZoom.Value.XmlParses &&
            screen.SvgPage.TextureStatistics.UploadedBytes == 0 &&
            ReferenceEquals(retainedSvgScene, ((IUiGpuSvgDocument)screen.SvgPage.Preview.Document).GpuScene),
            "observer same-byte reload reads five assets and preserves the GPU program without CPU pixels");
        Click("SVG", "SVG 변환 초기화");
        check(screen.SvgPage.Preview.Zoom == 1 && screen.SvgPage.Preview.RotationDegrees == 0 &&
            screen.SvgPage.Preview.Offset == default && screen.SvgPage.TextureStatistics.UploadedBytes == 0 &&
            ReferenceEquals(retainedSvgScene, ((IUiGpuSvgDocument)screen.SvgPage.Preview.Document).GpuScene),
            "observer transform reset restores the model and retained GPU scene without CPU image uploads");
        Click("SVG", "SVG 아이콘 메뉴");
        check(Find("SVG 아이콘 항목", UiAccessibilityRole.MenuItem).Enabled,
            "observer SVG icon menu exposes a real actionable item");
        Click("SVG", "SVG 아이콘 항목");
        check(screen.SvgPage.Statistics!.Value.XmlParses == afterZoom.Value.XmlParses,
            "observer actual SVG icon menu activation does not reparse source XML");
        SetSvgRange(2, .01f);
        check(screen.SvgPage.Preview.Offset.X == 1,
            "observer SVG movement can select one pixel rather than snapping to five pixels");
        void ArrowRight()
        {
            Frame("SVG", new(new(-100, -100), false, false, false, false, false, false, 0, 0,
                [new(UiKey.RightArrow, true, default)], [], default, default));
            Frame("SVG", new(new(-100, -100), false, false, false, false, false, false, 0, 0,
                [new(UiKey.RightArrow, false, default)], [], default, default));
        }
        ArrowRight();
        check(screen.SvgPage.Preview.Offset.X == 2,
            "observer focused SVG movement advances by one pixel on the right arrow");
        SetSvgRange(1, 1f / 360);
        check(screen.SvgPage.Preview.RotationDegrees == 1,
            "observer SVG rotation can select one degree rather than snapping to fifteen degrees");
        ArrowRight();
        check(screen.SvgPage.Preview.RotationDegrees == 2,
            "observer focused SVG rotation advances by one degree on the right arrow");
        SetSvgRange(0, (1.01f - .25f) / (4f - .25f));
        check(MathF.Abs(screen.SvgPage.Preview.Zoom - 1.01f) < .0001f,
            "observer SVG zoom can select one percent rather than snapping to twenty-five percent");
        ArrowRight();
        check(MathF.Abs(screen.SvgPage.Preview.Zoom - 1.02f) < .0001f,
            "observer focused SVG zoom advances by one percent on the right arrow");
        Click("SVG", "SVG 변환 초기화");
        void SelectSvgTab(string name)
        {
            var node = Find(name, UiAccessibilityRole.Tab);
            var pointer = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame("SVG", Input(pointer, down: true, pressed: true));
            Frame("SVG", Input(pointer, released: true));
            Frame("SVG");
        }
        var beforeFeatures = screen.SvgPage.Statistics!.Value;
        SelectSvgTab("문서 기능");
        var featurePanel = Find("문서 기능", UiAccessibilityRole.TabPanel);
        check(featurePanel.LabeledBy == Find("문서 기능", UiAccessibilityRole.Tab).Id &&
            screen.SvgPage.Statistics!.Value.XmlParses == beforeFeatures.XmlParses &&
            screen.SvgPage.Statistics!.Value.Rasterizations == beforeFeatures.Rasterizations,
            "observer actual SVG feature tab renders its linked retained document without reparsing XML");
        var featureStatistics = screen.SvgPage.Statistics;
        Frame("SVG");
        check(screen.SvgPage.Statistics == featureStatistics,
            "observer actual SVG feature panel reuses its pattern/mask/symbol scene and raster unchanged");
        SelectSvgTab("이미지·텍스트");
        check(Find("이미지·텍스트", UiAccessibilityRole.TabPanel).LabeledBy == Find("이미지·텍스트", UiAccessibilityRole.Tab).Id &&
            screen.SvgPage.Statistics == featureStatistics,
            "observer returns to its actual SVG comparison panel without losing cached scenes or transforms");
        var beforeShape = screen.SvgPage.Statistics;
        SetSvgRange(3, .01f);
        check(screen.SvgPage.Preview.CornerRadius == 1 && screen.SvgPage.Bitmap!.CornerRadius == 1,
            "observer shared bitmap and SVG corner radius selects one pixel");
        ArrowRight();
        check(screen.SvgPage.Preview.CornerRadius == 2 && screen.SvgPage.Bitmap!.CornerRadius == 2,
            "observer focused corner radius advances by one pixel");
        Click("SVG", "이미지 전체 맞춤");
        check(screen.SvgPage.Bitmap!.Fit == UiImageFit.Contain, "observer actual image fit control selects Contain");
        Click("SVG", "이미지 채우기");
        check(screen.SvgPage.Bitmap.Fit == UiImageFit.Cover, "observer actual image fit control selects Cover");
        Click("SVG", "SVG 변환 초기화");
        check(screen.SvgPage.Bitmap.Fit == UiImageFit.Stretch && screen.SvgPage.Preview.CornerRadius == 0
            && screen.SvgPage.Statistics == beforeShape,
            "observer shape and fit controls reset without reparsing or rerasterizing SVG");
        var movementRange = Find("SVG 여백", UiAccessibilityRole.Slider);
        var movementEntry = tree.TryGet(movementRange.Id, out var publishedMovement) ? publishedMovement
            : throw new InvalidOperationException("Observer SVG movement range was not published.");
        var beforeNumericMove = screen.SvgPage.Statistics;
        check(movementRange.Range is { Minimum: 0, Maximum: 100, SmallChange: 1 } &&
            tree.RequestRangeValue(movementRange.Id, movementEntry.RuntimeId, 7, wait: false) == UiAccessibilityResult.Success,
            "observer actual SVG movement publishes its one-pixel numeric accessibility contract");
        Frame("SVG");
        check(screen.SvgPage.Preview.Offset.X == 7 && screen.SvgPage.Statistics == beforeNumericMove,
            "observer numeric accessibility movement updates the actual SVG without XML or raster work");
        // Keep both transformed previews inside the viewport when asserting texture reuse.
        viewportSize = new(1600, 2200);
        Frame("SVG");
        var beforeBrowser = screen.SvgPage.Statistics!.Value;
        SelectSvgTab("브라우저 비교");
        check(screen.SvgPage.Statistics!.Value.XmlParses == beforeBrowser.XmlParses + 2 &&
            Find("브라우저 비교", UiAccessibilityRole.TabPanel).LabeledBy == Find("브라우저 비교", UiAccessibilityRole.Tab).Id,
            "observer browser-comparison tab lazily compiles both unmodified complex originals");
        var browserStatistics = screen.SvgPage.Statistics;
        var browserUploads = screen.SvgPage.TextureStatistics.GpuRenderRequests;
        GallerySampleTests.DrainSvgScheduledFrames(context, () => Frame("SVG"));
        Frame("SVG");
        check(screen.SvgPage.Statistics == browserStatistics && screen.SvgPage.TextureStatistics.GpuRenderRequests == browserUploads &&
            context.State.NextScheduledFrameTimeSeconds is null,
            "observer complex browser comparison reuses its retained scenes and rasters without scheduled frames");
        check(screen.SvgPage.BrowserPreviews.Count == 2 && screen.SvgPage.BrowserPreviews.All(svg => svg.Offset.X == 7),
            "observer both browser-comparison SVGs inherit the existing shared movement value on first display");
        SetSvgRange(1, .25f);
        SetSvgRange(2, .2f);
        SetSvgRange(3, .03f);
        check(screen.SvgPage.BrowserPreviews.All(svg => svg.RotationDegrees == 90 && svg.Offset.X == 20 && svg.CornerRadius == 3) &&
            screen.SvgPage.Statistics == browserStatistics && screen.SvgPage.TextureStatistics.GpuRenderRequests == browserUploads,
            "observer browser-comparison rotation, movement and corners change both vector previews without XML, raster or upload work");
        SetSvgRange(0, (2f - .25f) / (4f - .25f));
        viewportSize = new(1280, 2200);
        Frame("SVG");
        check(screen.SvgPage.BrowserPreviews.All(svg => svg.Zoom == 2 && svg.UseFixedResolutionPreview) &&
            screen.SvgPage.Statistics!.Value.XmlParses == browserStatistics!.Value.XmlParses &&
            screen.SvgPage.Statistics.Value.Rasterizations == browserStatistics.Value.Rasterizations,
            "observer browser-comparison zoom moves both GPU previews without reparsing XML");
        var interactionUploads = screen.SvgPage.TextureStatistics.GpuRenderRequests;
        SetSvgRange(0, (3f - .25f) / (4f - .25f));
        SetSvgRange(1, .5f);
        check(screen.SvgPage.BrowserPreviews.All(svg => svg.Zoom == 3 && svg.RotationDegrees == 180
                && svg.UseFixedResolutionPreview)
            && screen.SvgPage.TextureStatistics.GpuRenderRequests == interactionUploads,
            "observer rapid zoom and rotation reuse the same GPU preview textures");
        context.State.AdvanceTime(.2f);
        Frame("SVG");
        check(screen.SvgPage.BrowserPreviews.All(svg => svg.UseFixedResolutionPreview)
            && screen.SvgPage.TextureStatistics.GpuRenderRequests == interactionUploads
            && screen.SvgPage.Samples.Count(sample => sample.Source.Contains("vector.UseFixedResolutionPreview = true;", StringComparison.Ordinal)) == 2,
            "observer browser comparison defaults to camera reuse after idle and shows the matching source");
        Click("SVG", SvgPage.BrowserResolutionLabel);
        Frame("SVG");
        check(screen.SvgPage.BrowserPreviews.All(svg => !svg.UseFixedResolutionPreview)
            && screen.SvgPage.TextureStatistics.GpuRenderRequests > interactionUploads,
            "observer can select high-resolution browser comparison from the Gallery");
        interactionUploads = screen.SvgPage.TextureStatistics.GpuRenderRequests;
        var zoomControl = Reveal("SVG", Find("SVG 확대", UiAccessibilityRole.Slider));
        var held = new UiVector2(zoomControl.Bounds.X + zoomControl.Bounds.Width / 2,
            zoomControl.Bounds.Y + zoomControl.Bounds.Height / 2);
        Frame("SVG", Input(held, down: true, pressed: true));
        context.State.AdvanceTime(.3f);
        Frame("SVG", Input(held, down: true));
        check(screen.SvgPage.BrowserPreviews.All(svg => svg.UseFixedResolutionPreview),
            "observer still holds the SVG preview when a captured slider pauses beyond the idle deadline");
        var heldUploads = screen.SvgPage.TextureStatistics.GpuRenderRequests;
        Frame("SVG", Input(held, released: true));
        check(screen.SvgPage.BrowserPreviews.All(svg => svg.UseFixedResolutionPreview)
            && screen.SvgPage.TextureStatistics.GpuRenderRequests == heldUploads
            && context.State.NextScheduledFrameTimeSeconds is not null,
            "observer schedules exact SVG output after release even when the last pointer movement was earlier");
        context.State.AdvanceTime(.2f);
        Frame("SVG");
        check(screen.SvgPage.BrowserPreviews.All(svg => !svg.UseFixedResolutionPreview)
            && screen.SvgPage.TextureStatistics.GpuRenderRequests > interactionUploads,
            "observer transform idle deadline restores GPU output at the final resolution");
        Click("SVG", SvgPage.BrowserResolutionLabel);
        Frame("SVG");
        check(screen.SvgPage.BrowserPreviews.All(svg => svg.UseFixedResolutionPreview),
            "observer can return to camera reuse after high-resolution comparison");
        viewportSize = new(1280, 900);
        Frame("SVG");
        Click("SVG", "SVG 변환 초기화");
        check(screen.SvgPage.BrowserPreviews.All(svg => svg.Zoom == 1 && svg.RotationDegrees == 0 && svg.Offset == default && svg.CornerRadius == 0),
            "observer shared transform reset also restores both browser-comparison SVGs");
        Frame("위젯 배치");
        var marginButton = Find("변환 버튼 실행", UiAccessibilityRole.Button);
        var originalMarginBounds = marginButton.Bounds;
        var marginSlider = Find("공통 여백 - 왼쪽", UiAccessibilityRole.Slider);
        if (!tree.TryGet(marginSlider.Id, out var marginEntry))
            throw new InvalidOperationException("Common widget margin slider is missing.");
        check(tree.RequestRangeValue(marginSlider.Id, marginEntry.RuntimeId, 17, wait: false) == UiAccessibilityResult.Success,
            "observer common widget margin exposes one-pixel numeric adjustment");
        Frame("위젯 배치");
        // Natural layout is prepared before queued slider input; inspect its following layout frame.
        Frame("위젯 배치");
        var adjustedMarginBounds = Find("변환 버튼 실행", UiAccessibilityRole.Button).Bounds;
        check(screen.WidgetLayoutPage.AppliedMargin == new UiEdgeInsets(17, 8, 8, 8) &&
            adjustedMarginBounds.X == originalMarginBounds.X + 9 && adjustedMarginBounds.Width == originalMarginBounds.Width,
            "observer inherited widget margin changes button placement without enlarging its hit area");
        Click("위젯 배치", "변환 버튼 실행");
        check(screen.WidgetLayoutPage.Activations == 1, "observer actual button remains clickable inside common margin");
        Frame("개요");
        Frame("위젯 배치");
        check(screen.WidgetLayoutPage.Activations == 1 && screen.WidgetLayoutPage.AppliedMargin.Left == 17,
            "observer common margin controls and button state survive page changes");
        Click("위젯 배치", "공통 여백 초기화");
        check(screen.WidgetLayoutPage.AppliedMargin == default, "observer common widget margin reset restores zero edges");
        var commonRotation = Find("공통 회전", UiAccessibilityRole.Slider);
        if (!tree.TryGet(commonRotation.Id, out var rotationEntry))
            throw new InvalidOperationException("Common widget rotation control is missing.");
        check(tree.RequestRangeValue(commonRotation.Id, rotationEntry.RuntimeId, 90, wait: false) == UiAccessibilityResult.Success,
            "observer common rotation exposes one-degree numeric adjustment");
        Frame("위젯 배치");
        Frame("위젯 배치");
        var turnedButton = Find("변환 버튼 실행", UiAccessibilityRole.Button);
        check(screen.WidgetLayoutPage.AppliedTransform.RotationDegrees == 90 && turnedButton.Bounds.Width == 32 && turnedButton.Bounds.Height == 140,
            "observer actual button changes its measured bounds through inherited common rotation");
        Click("위젯 배치", "변환 버튼 실행");
        check(screen.WidgetLayoutPage.Activations == 2, "observer actual quarter-turned button remains pointer-operable");
        Frame("개요");
        Frame("위젯 배치");
        check(screen.WidgetLayoutPage.AppliedTransform.RotationDegrees == 90, "observer common transform state survives tab changes");
        Click("위젯 배치", "공통 변환 초기화");
        check(screen.WidgetLayoutPage.AppliedTransform.IsIdentity && Find("변환 버튼 실행", UiAccessibilityRole.Button).Bounds.Width == 140,
            "observer transform reset restores the actual button's original layout");
        Frame("SVG");
        var editingNavigation = screen.NavigationLabelForPage("입력·IME");
        var header = Find(editingNavigation, UiAccessibilityRole.Link);
        if (header.Offscreen)
        {
            if (!tree.TryGet(header.Id, out var entry)
                || tree.RequestAction(header.Id, entry.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Editing example navigation cannot scroll into view.");
            RenderFrame();
            RenderFrame();
            header = Find(editingNavigation, UiAccessibilityRole.Link);
        }
        var headerPointer = new UiVector2(header.Bounds.X + header.Bounds.Width / 2, header.Bounds.Y + header.Bounds.Height / 2);
        RenderFrame(Input(headerPointer, down: true, pressed: true));
        RenderFrame(Input(headerPointer, released: true));
        check(screen.SelectedPage == "입력·IME" && Find("이름").Role == UiAccessibilityRole.Edit,
            "observer actual Fluent tab pointer input switches its body without injected page state");
        for (var i = 0; i < 4; i++)
            Frame("측정 재사용");
        var measured = screen.MeasurementPage.Measurements;
        Frame("측정 재사용");
        check(screen.MeasurementPage.Measurements == measured && measured > 0,
            "observer measurement page retains real mixed-control layout across requested frames");
        var measurementToggle = Find("재사용 체크", UiAccessibilityRole.Checkbox);
        if (!tree.TryGet(measurementToggle.Id, out var measurementEntry))
            throw new InvalidOperationException("Measurement checkbox is missing.");
        tree.RequestAction(measurementToggle.Id, measurementEntry.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        Frame("측정 재사용");
        check(screen.MeasurementPage.Checked == true && screen.MeasurementPage.Measurements == measured,
            "observer retained measurement page still processes real checkbox selection without remeasurement");
        var progressReads = screen.MeasurementPage.ProgressReads;
        var measuredSlider = Find("재사용 슬라이더", UiAccessibilityRole.Slider);
        if (!tree.TryGet(measuredSlider.Id, out var measuredSliderEntry))
            throw new InvalidOperationException("Measurement slider is missing.");
        tree.RequestRangeValue(measuredSlider.Id, measuredSliderEntry.RuntimeId, .75, wait: false);
        Frame("측정 재사용");
        check(screen.MeasurementPage.LastProgressValue == .75f && screen.MeasurementPage.ProgressReads > progressReads &&
            screen.MeasurementPage.Measurements == measured,
            "observer retained content still reads the real Progress getter after its shared slider value changes");
        var linkBounds = screen.MeasurementPage.LinkBounds;
        var linkPointer = new UiVector2(linkBounds.X + linkBounds.Width / 2, linkBounds.Y + linkBounds.Height / 2);
        Frame("측정 재사용", Input(linkPointer, down: true, pressed: true));
        Frame("측정 재사용", Input(linkPointer, released: true));
        Frame("측정 재사용");
        check(screen.MeasurementPage.Navigations == 1 && screen.MeasurementPage.Measurements == measured,
            "observer retained Link executes its real pointer navigation callback without remeasuring the children");
        Click("측정 재사용", "측정 본문 변경");
        Frame("측정 재사용");
        check(screen.MeasurementPage.Measurements > measured,
            "observer static body, CounterBadge and Spinner label source updates invalidate the retained parent");
        measured = screen.MeasurementPage.Measurements;
        Click("측정 재사용", "측정 라벨 변경");
        Frame("측정 재사용");
        check(screen.MeasurementPage.Measurements > measured && Find("재사용 체크 : 변경된 라벨", UiAccessibilityRole.Checkbox).Checked == true,
            "observer label mutation invalidates the real retained parent while preserving its caller value");
        measured = screen.MeasurementPage.Measurements;
        Click("측정 재사용", "측정 너비 변경");
        Frame("측정 재사용");
        check(screen.MeasurementPage.Measurements > measured,
            "observer width option invalidates the actual mixed-control parent allocation");
        measured = screen.MeasurementPage.Measurements;
        Frame("개요");
        Frame("측정 재사용");
        Frame("측정 재사용");
        check(screen.MeasurementPage.Checked == true && screen.MeasurementPage.Measurements == measured &&
            !context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "observer measurement page preserves values and cache across tabs without requesting ongoing animation frames");
        var formMeasurements = screen.MeasurementPage.FormMeasurements;
        Frame("측정 재사용");
        Frame("측정 재사용");
        check(formMeasurements > 0 && screen.MeasurementPage.FormMeasurements == formMeasurements,
            "observer actual form parent retains Input, Divider, Menu trigger and Field measurements");
        Click("측정 재사용", "재사용 폼 입력");
        Frame("측정 재사용", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.End, true, default)],
            CharEvents = [new('x')]
        });
        check(screen.MeasurementPage.FormValue == "편집하세요x" && screen.MeasurementPage.FormMeasurements == formMeasurements,
            "observer retained form editor accepts actual typing without remeasuring the parent");
        Frame("측정 재사용", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        check(screen.MeasurementPage.FormValue == "편집하세요",
            "observer retained form editor preserves actual undo history");
        Click("측정 재사용", "재사용 폼 메뉴");
        Click("측정 재사용", "재사용 폼 체크");
        check(Find("재사용 폼 체크").Checked == true && screen.MeasurementPage.FormMeasurements == formMeasurements,
            "observer retained menu trigger keeps actual popup checkbox bindings live");
        Frame("측정 재사용", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        var tabMeasurements = screen.MeasurementPage.TabMeasurements;
        Frame("측정 재사용");
        Frame("측정 재사용");
        check(tabMeasurements > 0 && screen.MeasurementPage.TabMeasurements == tabMeasurements,
            "observer actual sample tabs reuse intrinsic text measurements across requested frames");
        Click("측정 재사용", "측정 탭 Beta");
        check(screen.MeasurementPage.TabValue == "b" && screen.MeasurementPage.TabMeasurements == tabMeasurements,
            "observer actual tab pointer selection updates painting and binding without remeasuring reserved text");
        Click("측정 재사용", "탭 크기 변경");
        Frame("측정 재사용");
        check(screen.MeasurementPage.TabMeasurements > tabMeasurements,
            "observer actual tab size option invalidates its intrinsic metrics");
        var delegatedMeasurements = screen.MeasurementPage.DelegatedMeasurements;
        Frame("측정 재사용");
        Frame("측정 재사용");
        check(delegatedMeasurements > 0 && screen.MeasurementPage.DelegatedMeasurements == delegatedMeasurements,
            "observer actual Textarea, Tooltip and explicit composition retain ancestor measurement");
        Click("측정 재사용", "재사용 여러 줄");
        Frame("측정 재사용", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.End, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        Frame("측정 재사용", Input(new(-100, -100)) with
        {
            CharEvents = [new('x')]
        });
        check(screen.MeasurementPage.MemoValue == "첫 줄\n둘째 줄x" && screen.MeasurementPage.DelegatedMeasurements == delegatedMeasurements,
            "observer retained multiline editor preserves real editing without ancestor remeasurement");
        Frame("측정 재사용", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        check(screen.MeasurementPage.MemoValue == "첫 줄\n둘째 줄", "observer retained multiline editor preserves undo");
        Click("측정 재사용", "재사용 도구 설명 대상");
        for (var i = 0; i < 6; i++)
            Frame("측정 재사용");
        check(screen.MeasurementPage.MeasurementTooltipOpen && screen.MeasurementPage.DelegatedMeasurements == delegatedMeasurements,
            "observer retained delegated Tooltip target preserves scheduled popup opening");
        Frame("측정 재사용", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(!screen.MeasurementPage.MeasurementTooltipOpen, "observer retained delegated Tooltip preserves Escape dismissal");
        var ownedSizing = screen.MeasurementPage.OwnedSizingMeasurements;
        Frame("측정 재사용");
        Frame("측정 재사용");
        check(ownedSizing > 0 && screen.MeasurementPage.OwnedSizingMeasurements == ownedSizing,
            "observer natural Field and Tooltip preserve actual owned-child measurement counts across render frames");
        var nativeMissingCalls = 0;
        missingGlyphObserver = _ => nativeMissingCalls++;
        var retainedMeasurements = (screen.MeasurementPage.Measurements, screen.MeasurementPage.FormMeasurements,
            screen.MeasurementPage.TabMeasurements, screen.MeasurementPage.DelegatedMeasurements, screen.MeasurementPage.OwnedSizingMeasurements);
        Frame("측정 재사용");
        Frame("측정 재사용");
        check(retainedMeasurements == (screen.MeasurementPage.Measurements, screen.MeasurementPage.FormMeasurements,
            screen.MeasurementPage.TabMeasurements, screen.MeasurementPage.DelegatedMeasurements, screen.MeasurementPage.OwnedSizingMeasurements),
            "application atlas observer does not disable independent native-text measurement retention");
        check(nativeMissingCalls == 0, "native control measurement retention preserves the unused atlas observer without invoking or removing it");
        missingGlyphObserver = null;
        Frame("추가 정보");
        var infoLabel = screen.InfoLabelPage.Preview;
        check(infoLabel is not null && Find("사용자 이름", UiAccessibilityRole.Edit).Required,
            "observer InfoLabel labels its real editable target and required metadata");
        Click("추가 정보", "사용자 이름 추가 정보");
        check(infoLabel!.IsOpen && Find("정보 안에서 실행", UiAccessibilityRole.Button).Enabled,
            "observer InfoLabel actual information button opens supplemental interactive content");
        Frame("추가 정보", Input(new(-100, -100)) with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(!infoLabel.IsOpen, "observer InfoLabel actual Escape closes supplemental content");
        Click("추가 정보", "정보 라벨 전환");
        Frame("개요");
        Frame("추가 정보");
        check(ReferenceEquals(infoLabel, screen.InfoLabelPage.Preview) && Find("디모이 표시 이름", UiAccessibilityRole.Edit).Name == "디모이 표시 이름",
            "observer InfoLabel label changes and model identity survive page switches");
        viewportSize = new(800, 400);
        Frame("아바타");
        var avatarScroll = Find("갤러리 페이지", UiAccessibilityRole.Pane);
        check(avatarScroll.Scroll is { VerticallyScrollable: true }
            && avatarScroll.Bounds.Y >= 0 && avatarScroll.Bounds.Y + avatarScroll.Bounds.Height <= viewportSize.Y,
            "observer Avatar uses the common finite scrolling viewport in a small window");
        if (!tree.TryGet(avatarScroll.Id, out var avatarScrollEntry))
            throw new InvalidOperationException("Avatar scroll provider is missing.");
        check(tree.RequestScroll(avatarScroll.Id, avatarScrollEntry.RuntimeId,
            UiAccessibilityScrollCommand.ByPercent(-1, 100), wait: false) == UiAccessibilityResult.Success,
            "observer Avatar gallery accepts actual scroll-to-bottom requests");
        Frame("아바타");
        check(Find("갤러리 페이지", UiAccessibilityRole.Pane).Scroll is { VerticalScrollPercent: 100 },
            "observer Avatar exposes the bottom of its growing gallery without layout overflow");
        Frame("개요");
        Frame("아바타");
        check(Find("갤러리 페이지", UiAccessibilityRole.Pane).Scroll is { VerticalScrollPercent: 100 }
            && ReferenceEquals(avatar, screen.AvatarPage.Preview),
            "observer Avatar keeps the scroll offset and model across tab switches");
        Frame("Markdown");
        Click("Markdown", "이미지 예제");
        check(Find("마크다운 미리보기", UiAccessibilityRole.Edit).Value?.Contains("markdown-image.svg", StringComparison.Ordinal) == true
            && screen.MarkdownPage.View.Document.Images.Count == 3,
            "observer image example opens the real SVG, memory image and unavailable alternative in Markdown preview");
        Click("Markdown", "예제 복원");
        check(screen.MarkdownPage.View.Document.Images.Count == 0
            && Find("마크다운 미리보기", UiAccessibilityRole.Edit).Value?.StartsWith("# 한글 Markdown", StringComparison.Ordinal) == true,
            "observer restoring Markdown source retires example image references");
        screen.Dispose();
        check(screen.VirtualPage.LiveCount == 0, "disposing the observer releases every currently realized virtual row");
    }
}

