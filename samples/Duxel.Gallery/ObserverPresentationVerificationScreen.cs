using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.App;
using Duxel.Core;
using Duxel.Gallery;

internal sealed class ObserverPresentationVerificationScreen : UiScreen
{
    private readonly ObserverScreen _observer;
    private readonly DuxelAppSession _session;
    private readonly string _output;
    private readonly bool _highResolutionCamera;
    private enum MarkdownExample
    {
        None, Paragraph, Images, Structure, Table
    }
    private sealed record Scenario(string Name, string Page, float? SvgZoom = null, string? MessageAction = null,
        bool? MessageOptions = null, bool? ParagraphDirection = null, MarkdownExample Markdown = MarkdownExample.None,
        bool SvgFeatures = false, bool? LightTheme = null, bool? SvgTargetBackground = null, bool SvgArcs = false,
        bool NavAction = false, string? DrawerAction = null, bool HolidayOutage = false, bool TemporalPicker = false, bool LiveHoliday = false,
        float SvgRotation = 0);
    private readonly Scenario[] _scenarios;
    private readonly List<ObserverPresentationPresentedPage> _presented = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _index, _warmupFrames;
    private ulong _pendingFrame;
    private Duxel.Svg.SvgStoreStatistics? _cameraSource;
    private Duxel.Svg.SvgStoreStatistics? _backgroundSource;
    private long? _cameraRasterizations;
    private long? _arcRasterizations;
    private double? _scenarioStarted;
    private bool _paragraphRevealQueued;
    private int _paragraphToggleFrame = -1;
    private int _messageOptionsToggleFrame = -1;
    private bool _messageOptionsReady;
    private bool _messageRevealQueued;
    private string? _holidayCellId;
    private DateOnly? _holidayDate;
    private bool _holidayDateQueued;
    private ObserverTemporalPickerControlProbe? _temporalProbe;

    internal ObserverPresentationVerificationScreen(ObserverScreen observer, DuxelAppSession session, string output,
        bool gpuCamera = false, bool liveHoliday = false, bool highResolutionCamera = true)
    {
        _observer = observer;
        _session = session;
        _highResolutionCamera = highResolutionCamera;
        _output = Path.GetFullPath(output);
        if (Directory.Exists(_output))
            throw new IOException("Use a fresh Observer verification directory.");
        Directory.CreateDirectory(_output);
        if (liveHoliday)
        {
            _scenarios = [new("Calendar live Korean holiday", "공휴일 제공자", LiveHoliday : true)];
            Write(false, null);
            return;
        }
        var scenarios = observer.PageTitles.Select(title => new Scenario(title, title));
        scenarios = scenarios.Concat(new[] { true, false }.Select(light =>
            new Scenario("Gallery " + (light ? "light" : "dark") + " theme", "개요", LightTheme: light)));
        scenarios = scenarios.Append(new Scenario("Nav secondary action", "탐색 메뉴", NavAction : true));
        scenarios = scenarios.Append(new Scenario("Calendar holiday outage and selection", "공휴일 제공자", HolidayOutage: true));
        scenarios = scenarios.Concat(new[] { "DatePicker", "TimePicker", "DateTimePicker" }
            .Select(page => new Scenario(page + " wheel selection", page, TemporalPicker: true)));
        scenarios = scenarios.Concat(new[] { "open", "close", "reopen", "save" }.Select(action =>
            new Scenario("Drawer " + action, "보조 패널", DrawerAction : action)));
        scenarios = scenarios.Append(new Scenario("SVG document features", "SVG", SvgFeatures: true));
        scenarios = scenarios.Append(new Scenario("SVG arc geometry", "SVG", SvgArcs: true));
        scenarios = scenarios.Concat(new[] { true, false }.Select(included =>
            new Scenario("SVG target background " + (included ? "white" : "transparent"), "SVG", SvgTargetBackground: included)));
        scenarios = scenarios.Concat(new[] { true, false }.Select(rtl =>
            new Scenario("Paragraph " + (rtl ? "RTL" : "LTR"), "타이포그래피", ParagraphDirection: rtl)));
        scenarios = scenarios.Append(new Scenario("Markdown native paragraph", "Markdown", Markdown: MarkdownExample.Paragraph));
        scenarios = scenarios.Append(new Scenario("Markdown native images", "Markdown", Markdown: MarkdownExample.Images));
        scenarios = scenarios.Append(new Scenario("Markdown native structure", "Markdown", Markdown: MarkdownExample.Structure));
        scenarios = scenarios.Append(new Scenario("Markdown native table cells", "Markdown", Markdown: MarkdownExample.Table));
        scenarios = scenarios.Append(new Scenario("MessageBar add", "메시지 표시", MessageAction: "add"));
        scenarios = scenarios.Concat(new[] { true, false }.Select(enabled =>
            new Scenario("MessageBar options " + (enabled ? "on" : "off"), "메시지 표시", MessageOptions: enabled)));
        scenarios = scenarios.Concat(new[] { "reverse", "remove", "clear" }
            .Select(action => new Scenario("MessageBar " + action, "메시지 표시", MessageAction: action)));
        if (gpuCamera)
        {
            scenarios = scenarios.Concat(new[] { 1f, 2f, 4f }.Select(zoom => new Scenario("SVG camera " + zoom, "SVG", zoom)));
            scenarios = scenarios.Append(new Scenario("SVG camera 2 rotated 37 degrees", "SVG", SvgZoom : 2, SvgRotation : 37));
        }
        _scenarios = scenarios.ToArray();
        Write(false, null);
    }

    public override void Render(UiImmediateContext ui)
    {
        if (_clock.Elapsed.TotalSeconds > 90)
            throw new TimeoutException("Observer page presentation exceeded the 90-second verification budget.");
        var metrics = ui.GetRendererFrameMetrics();
        if (_pendingFrame != 0 && metrics.DrawDataFrameId >= _pendingFrame && metrics.PresentationSequence != 0)
        {
            var scenario = _scenarios[_index];
            if (!MarkdownReady(scenario))
                throw new InvalidOperationException("The presented Markdown example did not reach its requested source and viewer mode.");
            if (scenario.NavAction && !NavActionReady())
                throw new InvalidOperationException("The presented navigation secondary action must run once without changing its destination.");
            if (scenario.HolidayOutage && !HolidayOutageReady(ui.AccessibilityTree))
                throw new InvalidOperationException("The presented calendar did not retain date selection after the holiday outage.");
            if (scenario.LiveHoliday && !LiveHolidayReady(ui.AccessibilityTree))
                throw new InvalidOperationException("The presented calendar lost the retrieved holiday.");
            if (scenario.TemporalPicker && _temporalProbe?.Ready != true)
                throw new InvalidOperationException("The presented picker did not retain the selected date and time.");
            if (scenario.DrawerAction is { } drawerAction && !DrawerReady(ui.AccessibilityTree, drawerAction))
                throw new InvalidOperationException("The presented Drawer did not reach its requested state : " + drawerAction);
            var source = _observer.SvgPage.Statistics;
            if (scenario.SvgArcs && (_observer.SvgPage.ArcPreviews.Count != 6
                || source?.Rasterizations != _arcRasterizations))
                throw new InvalidOperationException("The presented arc gallery requires all six GPU previews without CPU rasterization.");
            if (scenario.ParagraphDirection is { } direction)
            {
                _observer.TypographyPage.VerifyDirection(direction);
                if (ParagraphSwitch(ui.AccessibilityTree).Node.Checked != direction)
                    throw new InvalidOperationException("The presented paragraph switch does not publish its actual direction.");
            }
            if (scenario.MessageAction is { } completedAction)
                _observer.MessageBarPage.VerifyDynamicState(completedAction);
            var optionSnapshot = scenario.MessageOptions is { } optionsEnabled
                ? ObserverMessageBarControlProbe.Verify(_observer.MessageBarPage, ui.AccessibilityTree
                    ?? throw new InvalidOperationException("The actual presented MessageBar probe requires its accessibility tree."), optionsEnabled) : null;
            var messageProbe = scenario.MessageAction is not null || scenario.MessageOptions is not null;
            if (scenario.LightTheme is { } lightTheme
                && (ui.GetStyle().FluentColorScheme == UiFluentColorScheme.Light) != lightTheme)
                throw new InvalidOperationException("The presented gallery frame did not use the requested theme.");
            if (scenario.SvgTargetBackground is { } background)
            {
                if (!SvgBackgroundReady(background) || source is not { } current)
                    throw new InvalidOperationException("The presented SVG previews did not use the requested target background.");
                if (_backgroundSource is { } previous && (current.XmlParses != previous.XmlParses
                    || current.SceneCompiles != previous.SceneCompiles || current.Rasterizations != previous.Rasterizations))
                    throw new InvalidOperationException("Changing the presented SVG background repeated source or CPU raster work.");
                _backgroundSource = current;
            }
            if (scenario.SvgZoom is not null)
            {
                if (!SvgCameraReady(scenario))
                    throw new InvalidOperationException("The presented SVG camera frame did not reach its final transform and resolution.");
                if (source is not { } current || _observer.SvgPage.TextureStatistics.GpuRenderRequests == 0)
                    throw new InvalidOperationException("Linux SVG camera produced no retained GPU scene output.");
                if (current.Rasterizations != _cameraRasterizations)
                    throw new InvalidOperationException("Linux SVG camera created CPU raster pixels, including its first output.");
                if (_cameraSource is { } previous && (current.Rasterizations != previous.Rasterizations ||
                    current.XmlParses != previous.XmlParses || current.SceneCompiles != previous.SceneCompiles))
                    throw new InvalidOperationException("Linux SVG camera zoom repeated CPU raster/XML/scene work.");
                _cameraSource = current;
            }
            _presented.Add(new(scenario.Name, metrics.DrawDataFrameId, metrics.PresentationSequence,
                metrics.DrawCalls, metrics.FramebufferSize, scenario.SvgZoom is null && !scenario.SvgArcs ? null : source?.Rasterizations,
                scenario.SvgZoom is null && !scenario.SvgArcs ? null : _observer.SvgPage.TextureStatistics.GpuRenderRequests,
                messageProbe ? _observer.MessageBarPage.RequestedDynamicCount : null,
                messageProbe ? _observer.MessageBarPage.PhysicalDynamicCount : null,
                messageProbe ? _observer.MessageBarPage.AnnouncementCount : null, optionSnapshot, scenario.ParagraphDirection,
                scenario.LightTheme is null ? null : ui.GetStyle().FluentColorScheme,
                scenario.SvgTargetBackground is { } included ? included ? new UiColor(255, 255, 255) : default(UiColor) : null,
                scenario.SvgArcs ? _arcRasterizations : null, scenario.SvgArcs ? _observer.SvgPage.ArcPreviews.Count : null,
                scenario.NavAction ? _observer.NavigationPage.PinActions : null,
                scenario.DrawerAction is null ? null : _observer.DrawerPage.Overlay.IsOpen,
                scenario.DrawerAction is null ? null : _observer.DrawerPage.Saves));
            Console.WriteLine("PASS Observer presentation : " + scenario.Name);
            _index++;
            _pendingFrame = 0;
            _warmupFrames = 0;
            _scenarioStarted = null;
            _paragraphRevealQueued = false;
            _paragraphToggleFrame = -1;
            _messageOptionsToggleFrame = -1;
            _messageOptionsReady = false;
            _messageRevealQueued = false;
            _holidayCellId = null;
            _holidayDate = null;
            _holidayDateQueued = false;
            _temporalProbe = null;
            Write(false, null);
        }
        if (_index == _scenarios.Length)
        {
            _session.Exit();
            return;
        }
        _observer.SelectedPage = _scenarios[_index].Page;
        if (_scenarios[_index].SvgFeatures)
            _observer.SvgPage.ConfigureFeaturePreview();
        if (_scenarios[_index].SvgArcs)
        {
            _arcRasterizations ??= _observer.SvgPage.Statistics?.Rasterizations
                ?? throw new InvalidOperationException("The arc probe requires its initial SVG source statistics.");
            _observer.SvgPage.ConfigureArcPreview();
        }
        if (_scenarios[_index].SvgTargetBackground is not null)
            _observer.SvgPage.ConfigureBrowserPreview(1, highResolution: _highResolutionCamera);
        if (_scenarioStarted is null)
        {
            _scenarioStarted = ui.GetTime();
            if (_scenarios[_index].MessageAction is { } action)
                _observer.MessageBarPage.ApplyVerificationAction(action);
            if (_scenarios[_index].TemporalPicker)
                _temporalProbe = new(_observer.TemporalPickerForPage(_scenarios[_index].Page));
            if (_scenarios[_index].LiveHoliday)
                _observer.HolidayProviderPage.Preview.ShowDate(new DateOnly(2026, 5, 1));
        }
        if (_scenarios[_index].SvgZoom is { } zoom)
        {
            _cameraRasterizations ??= _observer.SvgPage.Statistics?.Rasterizations
                ?? throw new InvalidOperationException("Linux camera verification requires the initial SVG page presentation.");
            _observer.SvgPage.ConfigureBrowserPreview(zoom, _scenarios[_index].SvgRotation, highResolution: _highResolutionCamera);
        }
        if (_warmupFrames == 1 && _scenarios[_index].Markdown != MarkdownExample.None)
        {
            var tree = ui.AccessibilityTree ?? throw new InvalidOperationException("The Markdown probe requires its actual accessibility tree.");
            var name = _scenarios[_index].Markdown switch
            {
                MarkdownExample.Paragraph => "문단 조판 예제",
                MarkdownExample.Images => "이미지 예제",
                MarkdownExample.Structure => "구조적 문단 예제",
                MarkdownExample.Table => "표 셀 예제",
                _ => throw new InvalidOperationException("A Markdown scenario requires an explicit example.")
            };
            var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name);
            if (target.Node.Offscreen || tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The actual Markdown paragraph example button could not be invoked.");
        }
        if (_warmupFrames == 1 && _scenarios[_index].NavAction)
        {
            var tree = ui.AccessibilityTree ?? throw new InvalidOperationException("The navigation action probe requires its actual accessibility tree.");
            var action = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == "개요 고정");
            if (action.Node.Offscreen || tree.RequestAction(action.Node.Id, action.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The actual navigation secondary action could not be invoked.");
        }
        if (_warmupFrames >= 1 && _scenarios[_index].HolidayOutage)
            QueueHolidayOutageProbe(ui.AccessibilityTree);
        if (_warmupFrames == 1 && _scenarios[_index].LiveHoliday)
            QueueLiveHolidayProbe(ui.AccessibilityTree);
        if (_warmupFrames >= 1 && _temporalProbe is { Ready: false } probe)
            probe.Advance(ui.AccessibilityTree ?? throw new InvalidOperationException("Picker verification requires an accessibility tree."));
        if (_warmupFrames == 1 && _scenarios[_index].DrawerAction is { } drawerCommand)
        {
            var tree = ui.AccessibilityTree ?? throw new InvalidOperationException("The Drawer probe requires its actual accessibility tree.");
            var name = drawerCommand switch
            {
                "open" or "reopen" => "보조 패널 열기",
                "close" => "보조 패널 닫기",
                "save" => "패널 저장",
                _ => throw new InvalidOperationException("Unknown Drawer command.")
            };
            var button = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == name);
            if (button.Node.Offscreen || tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The actual Drawer control rejected its command : " + drawerCommand);
        }
        if (_warmupFrames >= 1 && _scenarios[_index].MessageOptions is { } enableOptions)
        {
            var tree = ui.AccessibilityTree
                ?? throw new InvalidOperationException("The Observer control probe requires its actual accessibility tree.");
            if (_messageOptionsToggleFrame < 0)
            {
                if (ObserverMessageBarControlProbe.TryQueue(tree, enableOptions))
                    _messageOptionsToggleFrame = _warmupFrames;
            }
            else if (_warmupFrames >= _messageOptionsToggleFrame + 2 && !_messageOptionsReady)
            {
                _messageOptionsReady = ObserverMessageBarControlProbe.RevealMessages(_observer.MessageBarPage, tree, !_messageRevealQueued);
                _messageRevealQueued = true;
            }
        }
        if (_warmupFrames >= 1 && _paragraphToggleFrame < 0 && _scenarios[_index].ParagraphDirection is { } enableRtl)
        {
            var target = ParagraphSwitch(ui.AccessibilityTree);
            if (target.Node.Offscreen)
            {
                if (!_paragraphRevealQueued)
                {
                    var reveal = ui.AccessibilityTree!.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false);
                    if (reveal != UiAccessibilityResult.Success)
                        throw new InvalidOperationException("The actual paragraph switch could not be revealed : " + reveal);
                    _paragraphRevealQueued = true;
                }
            }
            else
            {
                if (target.Node.Checked != !enableRtl)
                    throw new InvalidOperationException("The paragraph switch had an unexpected initial state : " + target.Node.Checked);
                var toggle = ui.AccessibilityTree!.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
                if (toggle != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("The actual visible paragraph switch rejected its input : " + toggle);
                _paragraphToggleFrame = _warmupFrames;
            }
        }
        if (_warmupFrames == 1 && _scenarios[_index].LightTheme is { } light)
        {
            var tree = ui.AccessibilityTree ?? throw new InvalidOperationException("The gallery theme probe requires its accessibility tree.");
            var toggle = tree.CaptureSnapshot().Elements.Single(static element => element.Node.Role == UiAccessibilityRole.Switch
                && element.Node.Name == "밝은 테마");
            if (toggle.Node.Checked != light && tree.RequestAction(toggle.Node.Id, toggle.RuntimeId,
                UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The gallery theme switch rejected its input.");
        }
        if (_warmupFrames == 1 && _scenarios[_index].SvgTargetBackground is { } includeBackground)
        {
            var tree = ui.AccessibilityTree ?? throw new InvalidOperationException("The SVG background probe requires its accessibility tree.");
            var toggle = tree.CaptureSnapshot().Elements.Single(static element => element.Node.Role == UiAccessibilityRole.Switch
                && element.Node.Name == "흰색 배경을 SVG에 포함");
            if (toggle.Node.Checked != includeBackground && tree.RequestAction(toggle.Node.Id, toggle.RuntimeId,
                UiAccessibilityAction.Toggle, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The SVG background switch rejected its input.");
        }
        _observer.Render(ui);
        if (_warmupFrames > 8 && !MarkdownReady(_scenarios[_index]))
            throw new InvalidOperationException("The Markdown example command did not settle within eight frames.");
        if (_scenarios[_index].ParagraphDirection is { } requestedDirection && _paragraphToggleFrame >= 0
            && _warmupFrames - _paragraphToggleFrame > 6)
            _observer.TypographyPage.VerifyDirection(requestedDirection);
        if (_warmupFrames > 8 && _scenarios[_index].NavAction && !NavActionReady())
            throw new InvalidOperationException($"The navigation action did not settle within eight frames : count {_observer.NavigationPage.PinActions}, destination {_observer.NavigationPage.Preview.Value.Value}.");
        if (_warmupFrames > 12 && _scenarios[_index].HolidayOutage && !HolidayOutageReady(ui.AccessibilityTree))
            throw new InvalidOperationException("The holiday outage and date selection did not settle within twelve frames.");
        if (_scenarios[_index].LiveHoliday && ui.GetTime() - _scenarioStarted.Value > 12 && !LiveHolidayReady(ui.AccessibilityTree))
            throw new TimeoutException("The published May 2026 holiday did not appear in the Gallery calendar within twelve seconds.");
        if (_warmupFrames > 20 && _temporalProbe is { Ready: false } stalledPicker)
            throw new InvalidOperationException($"The temporal picker stalled at {stalledPicker.StageName} within twenty frames.");
        if (_warmupFrames > 20 && _scenarios[_index].DrawerAction is { } expectedDrawer
            && !DrawerReady(ui.AccessibilityTree, expectedDrawer))
            throw new InvalidOperationException($"The Drawer command did not settle : {expectedDrawer}, open {_observer.DrawerPage.Overlay.IsOpen}, saves {_observer.DrawerPage.Saves}.");
        if (++_warmupFrames >= (_scenarios[_index].MessageOptions is null && _scenarios[_index].ParagraphDirection is null ? 2 : 3) && _pendingFrame == 0
            && (_scenarios[_index].MessageOptions is null || _messageOptionsReady)
            && (_scenarios[_index].ParagraphDirection is null || _paragraphToggleFrame >= 0 && _warmupFrames >= _paragraphToggleFrame + 2)
            && (_scenarios[_index].ParagraphDirection is not { } directionToPresent || _observer.TypographyPage.DirectionReady(directionToPresent))
            && MarkdownReady(_scenarios[_index])
            && (!_scenarios[_index].NavAction || NavActionReady())
            && (!_scenarios[_index].HolidayOutage || HolidayOutageReady(ui.AccessibilityTree))
            && (!_scenarios[_index].LiveHoliday || LiveHolidayReady(ui.AccessibilityTree))
            && (_temporalProbe is null || _temporalProbe.Ready)
            && (_scenarios[_index].DrawerAction is not { } drawerToPresent
                || ui.GetTime() - _scenarioStarted.Value >= .4 && DrawerReady(ui.AccessibilityTree, drawerToPresent))
            && (_scenarios[_index].SvgTargetBackground is not { } targetBackground || SvgBackgroundReady(targetBackground))
            && SvgCameraReady(_scenarios[_index])
            && (_scenarios[_index].LightTheme is not { } lightToPresent
                || (ui.GetStyle().FluentColorScheme == UiFluentColorScheme.Light) == lightToPresent)
            && (_scenarios[_index].MessageAction is null && _scenarios[_index].MessageOptions is null
                || ui.GetTime() - _scenarioStarted.Value >= .7))
            _pendingFrame = ui.GetDrawDataFrameId();
        ui.RequestFrameAt(ui.GetTime() + .05);
    }

    private bool SvgBackgroundReady(bool included) => _observer.SvgPage.BrowserPreviews.Count == 2
        && _observer.SvgPage.BrowserPreviews.All(svg => svg.Background == (included ? new UiColor(255, 255, 255) : default));

    private bool SvgCameraReady(Scenario scenario) => scenario.SvgZoom is not { } zoom
        || _observer.SvgPage.BrowserPreviews.Count == 2 && _observer.SvgPage.BrowserPreviews.All(svg =>
            svg.Zoom == zoom && svg.RotationDegrees == scenario.SvgRotation && svg.UseFixedResolutionPreview != _highResolutionCamera);

    private static void QueueLiveHolidayProbe(UiAccessibilityTree? tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var button = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
            && element.Node.Name == "현재 달 공휴일 조회");
        if (button.Node.Offscreen || tree.RequestAction(button.Node.Id, button.RuntimeId,
            UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The live holiday retrieval button rejected activation.");
    }

    private bool LiveHolidayReady(UiAccessibilityTree? tree) => tree is not null
        && tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.GridCell && !element.Node.Offscreen
            && element.Node.Name.EndsWith(" · 대체공휴일(부처님 오신 날)", StringComparison.Ordinal))
        && _observer.HolidayProviderPage.Samples[0].Source.Contains("new KoreanHolidayProvider(http)", StringComparison.Ordinal);

    private void QueueHolidayOutageProbe(UiAccessibilityTree? tree)
    {
        if (_holidayDateQueued)
            return;
        if (tree is null)
            throw new InvalidOperationException("The holiday probe requires the actual gallery accessibility tree.");
        var elements = tree.CaptureSnapshot().Elements;
        if (_holidayCellId is null)
        {
            var holiday = elements.Single(element => element.Node.Role == UiAccessibilityRole.GridCell
                && element.Node.Name.EndsWith(" · 신정", StringComparison.Ordinal));
            var outage = elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
                && element.Node.Name == "연결 끊김 예제");
            if (outage.Node.Offscreen || tree.RequestAction(outage.Node.Id, outage.RuntimeId,
                UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The holiday outage control could not be invoked.");
            _holidayCellId = holiday.Node.Id;
            _holidayDate = _observer.HolidayProviderPage.Preview.FocusedDate;
            return;
        }

        var cell = elements.Single(element => element.Node.Id == _holidayCellId);
        if (cell.Node.Name.EndsWith(" · 신정", StringComparison.Ordinal))
            return;
        if (cell.Node.Offscreen || tree.RequestAction(cell.Node.Id, cell.RuntimeId,
            UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The calendar date could not be selected after the holiday outage.");
        _holidayDateQueued = true;
    }

    private bool HolidayOutageReady(UiAccessibilityTree? tree)
    {
        if (!_holidayDateQueued || _observer.HolidayProviderPage.Preview.Value.Value != _holidayDate || tree is null)
            return false;
        var elements = tree.CaptureSnapshot().Elements;
        return elements.Any(element => element.Node.Id == _holidayCellId && element.Node.Selected == true)
            && !elements.Any(element => element.Node.Role == UiAccessibilityRole.GridCell
                && element.Node.Name.EndsWith(" · 신정", StringComparison.Ordinal)
                || element.Node.Invalid || element.Node.Role == UiAccessibilityRole.AlertDialog);
    }

    private static UiAccessibilityElement ParagraphSwitch(UiAccessibilityTree? tree)
        => (tree ?? throw new InvalidOperationException("The paragraph control probe requires the actual accessibility tree."))
            .CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Switch
                && element.Node.Name == "오른쪽에서 왼쪽 문단");

    private bool DrawerReady(UiAccessibilityTree? tree, string action)
    {
        var expectedOpen = action is "open" or "reopen";
        var elements = (tree ?? throw new InvalidOperationException("Drawer readiness requires the actual accessibility tree.")).CaptureSnapshot().Elements;
        var surfaces = elements.Where(element => element.Node.Role == UiAccessibilityRole.Dialog && element.Node.Name == "보조 작업").ToArray();
        bool HasVisibleFocusInside()
        {
            if (surfaces.Length != 1) return false;
            foreach (var focused in elements.Where(element => element.Node.Focused && !element.Node.Offscreen))
            {
                var parent = focused.Node.ParentId;
                while (tree.TryGet(parent, out var ancestor))
                {
                    if (parent == surfaces[0].Node.Id) return true;
                    parent = ancestor.Node.ParentId;
                }
            }
            return false;
        }
        return _observer.DrawerPage.Overlay.IsOpen == expectedOpen
            && (expectedOpen ? surfaces.Length == 1 && !surfaces[0].Node.Offscreen && HasVisibleFocusInside()
                : surfaces.Length == 0 && elements.Any(element => element.Node.Name == "보조 패널 열기" && element.Node.Focused))
            && _observer.DrawerPage.Saves == (action == "save" ? 1 : 0);
    }

    private bool NavActionReady() => _observer.NavigationPage.PinActions == 1 && _observer.NavigationPage.Preview.Value.Value == "overview";

    private bool MarkdownReady(Scenario scenario) => scenario.Markdown switch
    {
        MarkdownExample.None => true,
        MarkdownExample.Paragraph => _observer.MarkdownPage.ParagraphExampleReady,
        MarkdownExample.Images => _observer.MarkdownPage.ImageExampleReady,
        MarkdownExample.Structure => _observer.MarkdownPage.StructuralExampleReady,
        MarkdownExample.Table => _observer.MarkdownPage.TableExampleReady,
        _ => throw new InvalidOperationException("Unknown Markdown example.")
    };

    internal void Complete()
    {
        if (_presented.Count != _scenarios.Length)
            throw new InvalidOperationException("Observer closed before every page was presented.");
        Write(true, null);
    }

    internal void Fail(Exception error) => Write(false, error.ToString());

    private void Write(bool completed, string? failure) => File.WriteAllText(Path.Combine(_output, "result.json"),
        JsonSerializer.Serialize(new ObserverPresentationVerification(RuntimeInformation.FrameworkDescription,
            "actual shared Observer UI and Vulkan QueuePresent acknowledgements; not a pixel comparison", completed,
            failure, _scenarios.Length, _presented.ToArray()), ObserverPresentationVerificationJson.Default.ObserverPresentationVerification));
}

internal sealed record ObserverPresentationPresentedPage(string Title, ulong FrameId, ulong PresentationSequence, int DrawCalls, PlatformSize Framebuffer,
    long? SvgCpuRasterizations = null, long? SvgGpuRenderRequests = null, int? MessageBarRequestedCount = null, int? MessageBarPhysicalCount = null,
    int? MessageBarAnnouncements = null, ObserverMessageBarOptionSnapshot? MessageBarOptions = null, bool? ParagraphRightToLeft = null,
    UiFluentColorScheme? GalleryColorScheme = null, UiColor? SvgTargetBackground = null,
    long? SvgArcRasterizationsBefore = null, int? SvgArcPreviews = null, int? NavSecondaryActions = null, bool? DrawerOpen = null, int? DrawerSaves = null);
internal sealed record ObserverPresentationVerification(string Framework, string Scope, bool Completed, string? Failure, int ExpectedPages, ObserverPresentationPresentedPage[] Pages);
[JsonSerializable(typeof(ObserverPresentationVerification))]
internal partial class ObserverPresentationVerificationJson : JsonSerializerContext;
