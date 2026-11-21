using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ObserverScreen : UiScreen, IDisposable
{
#if DUXEL_GALLERY_PROFILING
    internal bool ProfileRenderSections { get; set; }
    internal (long Navigation, long Options, long Page) RenderSectionTicks { get; private set; }
#endif
    private readonly IObserverPage[] _pages;
    private readonly GalleryPageFrame?[] _pageFrames;
    private readonly UiState<string?> _selection = new("개요");
    private readonly GalleryNavigation _navigation;
    private readonly UiFluentDrawer _navigationDrawer;
    private readonly UiWidget _compactHeader;
    private bool _compactNavigation;
    internal bool CompactNavigationOpen => _navigationDrawer.IsOpen;
    internal IEnumerable<string> PageTitles => _pages.Select(static page => page.Title);
    internal string? SelectedPage
    {
        get => _navigation.ResolvePage(_selection.Value);
        set => _selection.Value = value is null ? null : _navigation.KeyForPage(value);
    }
    internal string SelectedNavigationLabel => _navigation.SelectedLabel;
    internal (int Measure, int Paint, int Parse) CodeDiagnostics
    {
        get
        {
            for (var index = 0; index < _pages.Length; index++)
                if (_pages[index].Title == SelectedPage) return _pageFrames[index]?.CodeDiagnostics ?? default;
            return default;
        }
    }
    internal GalleryDestinationKind NavigationKindForPage(string page) => _navigation.KindForPage(page);
    internal string SourceKindForPage(string title)
    {
        var page = _pages.Single(item => item.Title == title);
        if (!GalleryPageFrame.HasSource(title)) return "none";
        return page is IGallerySourcePage { HasInlineSamples: true } ? "inline"
            : page is ComponentExamplePage ? "factory" : "full-page";
    }
    internal int SourceCountForPage(string title)
    {
        var page = _pages.Single(item => item.Title == title);
        return page is IGallerySourcePage { HasInlineSamples : true } source ? source.Samples.Count
            : GalleryPageFrame.HasSource(title) ? 1 : 0;
    }
    internal string DescriptionForPage(string page) => _pages.Single(item => item.Title == page).Description;
    internal TemporalPickerPage TemporalPickerForPage(string title) =>
        _pages.OfType<TemporalPickerPage>().Single(page => page.Title == title);
    internal string NavigationLabelForPage(string page) => _navigation.LabelForPage(page);
    private readonly ControlsPage _controls = new();
    internal VirtualPage VirtualPage { get; } = new();
    internal RenderingPage RenderingPage { get; } = new();
    internal SvgPage SvgPage
    {
        get;
    }
    internal WidgetLayoutPage WidgetLayoutPage { get; } = new();
    internal AvatarPage AvatarPage
    {
        get;
    }
    internal PopoverPage PopoverPage { get; } = new();
    internal DialogPage DialogPage { get; } = new();
    internal InfoLabelPage InfoLabelPage { get; } = new();
    internal SpinButtonPage SpinButtonPage { get; } = new();
    internal ListPage ListPage { get; } = new();
    internal MarkdownPage MarkdownPage { get; } = new();
    internal PopupInputPage PopupInputPage { get; } = new();
    internal ToolbarPage ToolbarPage { get; } = new();
    internal SearchBoxPage SearchBoxPage { get; } = new();
    internal PersonaPage PersonaPage
    {
        get;
    }
    internal DropdownPage DropdownPage
    {
        get;
    }
    internal AvatarGroupPage AvatarGroupPage
    {
        get;
    }
    internal CardPage CardPage { get; } = new();
    internal BreadcrumbPage BreadcrumbPage { get; } = new();
    internal NavigationPage NavigationPage { get; } = new();
    internal DrawerPage DrawerPage { get; } = new();
    internal MeasurementPage MeasurementPage { get; } = new();
    internal MessageBarPage MessageBarPage { get; } = new();
    internal HolidayProviderPage HolidayProviderPage { get; } = new();
    internal TypographyPage TypographyPage { get; } = new();
    internal RepaintPage RepaintPage
    {
        get;
    }
    private readonly UiState<bool> _repaintHighlights = new(false);
    private readonly UiWidget _options;
    private readonly UiWidget _themeSwitch;
    private readonly UiSizedBox _toolbar;
    private readonly UiScrollView _toolbarScroll;
    private readonly UiWidget _headerActions;
    private readonly UiFluentDialog? _settings;
    private UiImmediateContext? _currentUi;
    private string _rendererStatus = "";
    internal int ClickCount => _controls.ClickCount;
    internal float ControlsValue
    {
        get => _controls.Value; set => _controls.Value = value;
    }
    internal bool FreezeDiagnosticText
    {
        get; set;
    }
    private UiFpsCounter? _fps;
    private string _performance = "FPS 측정 중";
    private bool _updateOnlyOnChange = true;
    internal bool UpdateOnlyOnChange => _updateOnlyOnChange;
    internal string PerformanceText => _performance;

    internal ObserverScreen(ObserverServices services, Duxel.App.DuxelAppSession? fileDialogSession = null,
        GalleryLanguage language = GalleryLanguage.Korean, Action<GalleryLanguage>? requestLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        SvgPage = new(services);
        AvatarPage = new(services);
        PersonaPage = new(services);
        DropdownPage = new(services);
        AvatarGroupPage = new(services);
        RepaintPage = new();
        CatalogPage.Validate();
        _themeSwitch = Fluent.Switch(GalleryText.Get("밝은 테마"), new UiBinding<bool>(
            () => _currentUi!.GetStyle(includeRequested : true).FluentColorScheme == UiFluentColorScheme.Light, light =>
            {
                var theme = (light ? UiCompiledDesign.Windows11 : UiCompiledDesign.Windows11Dark).Theme;
                if (_currentUi!.GetStyle(includeRequested : true).ForcedColors is { } system) theme = system.Apply(theme);
                _currentUi.RequestTheme(theme);
            }));
        _toolbar = Dux.SizedBox(Dux.Row(new(Spacing: 16, CrossAxisAlignment: UiCrossAxisAlignment.Center),
            Dux.Expanded(Fluent.Tooltip(Fluent.Caption1(() => _rendererStatus), GalleryText.Get("P : 표시 횟수 / D : 그리기 호출 / U : 전송량 / C : 캐시 재사용"))),
            Fluent.Switch("V-Sync", new UiBinding<bool>(() => _currentUi!.GetVSync(), value => _currentUi!.SetVSync(value))),
            Fluent.Switch(GalleryText.Get("이벤트 대기 모드"), new UiBinding<bool>(() => _updateOnlyOnChange, value => _updateOnlyOnChange = value)),
            Fluent.Switch(GalleryText.Get("갱신 영역 강조"), _repaintHighlights.Binding)));
        _toolbarScroll = Dux.ScrollView(_toolbar, new(UiScrollAxis.Horizontal));
        _toolbarScroll.AccessibleName = GalleryText.Get("갤러리 상태와 옵션");
        _performance = GalleryText.Get("FPS 측정 중");
        _headerActions = _themeSwitch;
        if (requestLanguage is not null)
        {
            var current = GalleryText.Tag(language);
            var error = new UiState<string>(string.Empty);
            var choices = GalleryText.LanguageOptions.Select(option => (UiWidget)Fluent.Button(option.Text, () =>
            {
                try
                {
                    requestLanguage(GalleryText.FromTag(option.Value));
                    _settings!.Close();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    error.Value = GalleryText.Get("언어 설정을 저장할 수 없습니다.");
                }
            })).ToArray();
            _settings = Fluent.Dialog(GalleryText.Get("설정"),
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle2(GalleryText.Get("갤러리 언어")),
                    Fluent.Body1(GalleryText.Format("현재 언어: {0}", GalleryText.LanguageOptions.Single(option => option.Value == current).Text)),
                    Dux.Wrap(new(Spacing : 8, RunSpacing : 8), choices),
                    Fluent.Caption1(() => error.Value)),
                [Fluent.Button(GalleryText.Get("닫기"), () => _settings!.Close())],
                new(Width : 410, CloseLabel : GalleryText.Get("닫기")));
            var settingsLabel = GalleryText.Get("설정");
            var settingsButton = new UiButton<UiReleaseButtonBehavior,
                UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize, UiFluentSubtleButtonAppearance,
                    SettingsTabIcon, UiButtonIconBefore>>(string.Empty, () =>
                {
                    error.Value = string.Empty;
                    _settings.Show();
                })
            {
                AccessibleName = settingsLabel
            };
            _headerActions = Dux.Row(new(Spacing : 12, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                _themeSwitch, Fluent.Tooltip(settingsButton, settingsLabel));
        }
        _options = Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            Dux.Row(new(Spacing : 16, CrossAxisAlignment : UiCrossAxisAlignment.Start), Dux.Expanded(_toolbarScroll), _headerActions), Fluent.Divider());
        var catalog = new CatalogPage();
        _pages =
        [
            new WidgetPage("개요", string.Empty, CreateOverview()),
            new LegacyPage(),
            new FluentProviderPage(services.TypographyFonts),
            new ShimmerPage(),
            new ProgressIndicatorPage(),
            new ActivityIndicatorPage(),
            new HeadsUpDisplayPage(),
            new SegmentedControlPage(),
            new ToastPage(),
            new SelectPage(),
            new TreePage(),
            new TextFieldPage(),
            new CardNudgePage(),
            new CarouselPage(),
            new CalendarPage(false, services.TypographyFonts),
            new CalendarPage(true, services.TypographyFonts),
            new TemporalPickerPage(TemporalPickerKind.Date, services.TypographyFonts),
            new TemporalPickerPage(TemporalPickerKind.Time, services.TypographyFonts),
            new TemporalPickerPage(TemporalPickerKind.DateTime, services.TypographyFonts),
            HolidayProviderPage,
            new WidgetPage("Fluent", "실제 컨트롤을 조작하고 탭 이동 후 값이 유지되는지 확인하세요.", _controls),
            new WidgetPage("평가", "별점을 반 개씩 조절하거나 오른쪽부터 표시해 보세요. 부모 너비가 좁아질 때 줄바꿈되는 모습도 확인할 수 있습니다.", new RatingPage()),
            new WidgetPage("타이포그래피", "Fluent의 글자 크기와 굵기, 문단 방향을 비교해 보세요.", TypographyPage),
            new WidgetPage("입력·IME", "한글 입력, 선택, 검증, 줄바꿈과 되돌리기를 확인합니다.", new EditingPage()),
            new WidgetPage("Tooltip", "자동으로 나타나는 설명과 코드에서 표시 여부를 정하는 설명을 비교해 보세요.", new TooltipPage()),
            new WidgetPage("컨테이너", "컨테이너마다 위젯을 어떻게 배치하는지 비교해 보세요. 배치 옵션을 바꾸거나 안쪽 위젯을 직접 조작할 수 있습니다.", new LayoutPage()),
            new WidgetPage("배치 통합", "탭·메뉴·넘침 배치를 조합하고 화면 이동 후 상태가 유지되는지 확인합니다.", new LayoutIntegrationPage()),
            new WidgetPage("접기·펼치기", "Accordion을 하나씩 또는 여러 개 열어 보세요. 본문에 입력한 값이 유지되는지 확인하고 키보드와 접근성 기능으로 조작할 수 있습니다.", new AccordionPage()),
            VirtualPage,
            new VirtualGridPage(),
            new DataGridPage(),
            new FileDialogsPage(fileDialogSession),
            RenderingPage,
            SvgPage,
            WidgetLayoutPage,
            AvatarPage,
            AvatarGroupPage,
            PersonaPage,
            SearchBoxPage,
            ToolbarPage,
            PopupInputPage,
            PopoverPage,
            DialogPage,
            new WidgetPage("스켈레톤", "로딩 중에 표시할 윤곽을 구성해 보세요. 물결과 맥박 애니메이션, 일시 정지와 모션 줄이기를 비교할 수 있습니다.", new SkeletonPage()),
            MessageBarPage,
            InfoLabelPage,
            SpinButtonPage,
            ListPage,
            DropdownPage,
            CardPage,
            BreadcrumbPage,
            NavigationPage,
            new NavigationBarPage(),
            DrawerPage,
            new WidgetPage("관찰 범위", "사용할 수 있는 팩터리와 예제가 있는 메뉴, 아직 검증할 부분을 살펴보세요.", catalog),
            new WidgetPage("측정 재사용", "값·입력은 계속 처리하면서 변하지 않은 배치 측정을 재사용합니다.", MeasurementPage),
            RepaintPage,
            MarkdownPage,
            new LocalizationPage(),
            .. GalleryComponentExamples.Create(),
            .. Fluent.MissingComponentNames.ToArray().Where(static component => component != "Toast").Select(static component => (IObserverPage)new WidgetPage(component,
                "Fluent 구성요소 구현 예정",
                Dux.Column(new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min),
                    Fluent.Subtitle1(GalleryText.Get("아직 미구현됨")),
                    Fluent.Body1(GalleryText.Get("이 구성요소의 API와 동작 예제는 아직 제공되지 않습니다.")),
                    Fluent.Body1(GalleryText.Get("구현 예정 목록은 공식 구성요소 카탈로그를 기준으로 만듭니다. 구현한 예제도 디자인과 접근성이 올바른지는 계속 검증하고 있습니다.")))))
        ];
        _navigation = new(PageTitles, Fluent.MissingComponentNames.ToArray(), new UiBinding<string?>(() => _selection.Value, SelectRoute));
        catalog.Connect(_navigation.Destinations, SelectRoute);
        _navigationDrawer = Fluent.Drawer(GalleryText.Get("갤러리 탐색"), _navigation.Content,
            options : new(Extent : 320, CloseLabel : GalleryText.Get("탐색 메뉴 닫기"), ScrollBody : false));
        _compactHeader = Dux.Row(new(Spacing : 12, CrossAxisAlignment : UiCrossAxisAlignment.Center),
            Fluent.Button(GalleryText.Get("탐색 메뉴 열기"), OpenNavigation),
            Dux.Expanded(Fluent.Subtitle2(() => SelectedPage is null ? GalleryText.Get("페이지를 선택하세요") : _navigation.SelectedLabel)));
        _pageFrames = new GalleryPageFrame?[_pages.Length];
    }

    private UiWidget CreateOverview()
    {
        UiWidget Category(string title, string description, string route, string action)
        {
            var name = GalleryText.Get(title);
            return Fluent.Card(name,
                Dux.ConstrainedBox(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle2(name),
                    new UiInlineText([new(GalleryText.Get(description))]),
                    Fluent.Button(GalleryText.Get(action), () => SelectRoute(route))),
                    new(MinHeight : 118, MaxWidth : float.PositiveInfinity, MaxHeight : float.PositiveInfinity, MinWidth : 0)),
                new(Appearance : UiCardAppearance.Outline, Width : 280));
        }

        var intro = GalleryText.Get("Duxel로 UI 만들기");
        var browse = new UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance>>(
            GalleryText.Get("기능 찾아보기"), () => SelectRoute("관찰 범위"));
        var hero = Fluent.Card(intro,
            Dux.Padding(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
                Fluent.Title1(intro),
                new UiInlineText([new(GalleryText.Get("Duxel의 컨트롤과 그래픽, 문서 기능을 직접 조작하고 C# 예제 코드를 살펴보세요."))]),
                Dux.Align(browse, UiAlignment.TopLeft)), new(12)),
            new(Appearance : UiCardAppearance.FilledAlternative));

        return Dux.Padding(Dux.Column(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min,
            CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
            hero,
            Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Title3(GalleryText.Get("어디서 시작할까요?")),
                Dux.Wrap(new(Spacing : 16, RunSpacing : 16),
                    Category("동작과 탐색", "버튼과 링크, 메뉴로 화면의 흐름을 만드세요.", "Button", "Button 살펴보기"),
                    Category("입력과 선택", "텍스트 입력과 선택, 값 조절을 직접 확인하세요.", "Input", "Input 살펴보기"),
                    Category("콘텐츠와 목록", "카드와 목록으로 내용을 정리하세요.", "Card", "Card 살펴보기"),
                    Category("알림과 도움말", "대화 상자와 보조 패널의 동작을 확인하세요.", "Dialog", "Dialog 살펴보기"),
                    Category("벡터 그래픽", "SVG 확대·회전과 다양한 그래픽 표현을 살펴보세요.", "SVG", "SVG 살펴보기"),
                    Category("문서와 편집", "Markdown 문서를 읽고 실시간으로 편집해 보세요.", "Markdown", "Markdown 살펴보기"))),
            Fluent.Caption1(GalleryText.Get("왼쪽 메뉴에서 구성요소를 선택하세요. 예제 바로 아래에서 C# 소스를 읽고 복사할 수 있습니다."))),
            new(0, 12, 8, 16));
    }

    internal void CopyPreferencesFrom(ObserverScreen previous)
    {
        _updateOnlyOnChange = previous._updateOnlyOnChange;
        _repaintHighlights.Value = previous._repaintHighlights.Value;
    }

    private void OpenNavigation() => _navigationDrawer.Show();
    private void SelectRoute(string? value)
    {
        var changed = _selection.Value != value;
        _selection.Value = value;
        if (changed && _compactNavigation && _navigationDrawer.IsOpen) _navigationDrawer.Close();
    }

    public override void Render(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding: 8);
        _currentUi = ui;
        var fps = default(UiFpsSample);
        if (ui.IsPresentationPass)
        {
            if (_fps is null)
                _fps = new();
            else
                fps = _fps.TickExcludingIdle(ui.HostIdleWaitSeconds);
        }
        if (fps.Updated)
            _performance = $"{fps.Fps:F1} FPS · {fps.ElapsedSeconds * 1000d / fps.Frames:F2}ms";
        var metrics = ui.GetRendererFrameMetrics();
        if (_rendererStatus.Length == 0 || (fps.Updated && !_repaintHighlights.Value && !FreezeDiagnosticText))
            _rendererStatus = $"{_performance} | P {metrics.PresentationSequence} · D {metrics.DrawCalls} · U {(metrics.DynamicUploadBytes + metrics.StagingUploadBytes) / 1024d:F1}KB · C {metrics.ResidentHits}";
        var origin = ui.GetCursorScreenPos();
        var available = ui.GetContentRegionAvail();
        _compactNavigation = available.X + 16 <= 640;
#if DUXEL_GALLERY_PROFILING
        var sample = ProfileRenderSections;
        var navigationStart = sample ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
#endif
        if (_compactNavigation)
        {
            _compactHeader.Render(ui);
            origin = ui.GetCursorScreenPos();
            _navigation.PrepareOverlay();
            _navigationDrawer.Render(ui);
            ui.SetCursorScreenPos(origin);
        }
        else
        {
            if (_navigationDrawer.IsOpen) _navigationDrawer.Close();
            var navigationWidth = MathF.Min(280, MathF.Max(112, available.X * .26f));
            _navigation.Render(ui, new(navigationWidth, MathF.Max(0, available.Y)));
            var contentX = origin.X + navigationWidth + 28;
            ui.GetWindowDrawList().AddRectFilled(new(contentX - 14, origin.Y, 1, MathF.Max(0, available.Y)),
                ui.GetColorU32(UiStyleColor.Separator), ui.WhiteTextureId);
            ui.SetCursorScreenPos(new(contentX, origin.Y));
        }
#if DUXEL_GALLERY_PROFILING
        var optionsStart = sample ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
#endif
        var themeSize = _headerActions.MeasureConstrained(ui, new(0, float.PositiveInfinity, 0, float.PositiveInfinity));
        var statusWidth = MathF.Max(1, ui.GetContentRegionAvail().X - themeSize.X - 16);
        var toolbarWidth = MathF.Max(720, statusWidth);
        _toolbar.Options = new(Width: toolbarWidth);
        var toolbarSize = _toolbar.MeasureConstrained(ui, new(0, float.PositiveInfinity, 0, float.PositiveInfinity));
        _toolbarScroll.Options = new(UiScrollAxis.Horizontal, Width: statusWidth,
            Height: toolbarSize.Y + (statusWidth < toolbarWidth ? ui.GetFrameHeightWithSpacing() : 0));
        _options.Render(ui);
        ui.SetRepaintHighlights(_repaintHighlights.Value);
#if DUXEL_GALLERY_PROFILING
        var pageStart = sample ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
#endif
        for (var index = 0; index < _pages.Length; index++)
        {
            if (SelectedPage == _pages[index].Title)
            {
                var page = _pages[index];
                var frame = _pageFrames[index] ??= new GalleryPageFrame(page, _navigation.LabelForPage(page.Title));
                _navigation.RenderPanel(ui, page.Title, frame.Render, navigationVisible : !_compactNavigation);
                break;
            }
        }
        _settings?.Render(ui);
#if DUXEL_GALLERY_PROFILING
        if (sample)
            RenderSectionTicks = (optionsStart - navigationStart, pageStart - optionsStart,
                System.Diagnostics.Stopwatch.GetTimestamp() - pageStart);
#endif
    }

    public void Dispose()
    {
        _navigationDrawer.Dispose();
        _compactHeader.Dispose();
        _navigation.Dispose();
        _options.Dispose();
        _settings?.Dispose();
        foreach (var frame in _pageFrames) frame?.Dispose();
        foreach (var page in _pages)
            page.Dispose();
    }
}
