using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ObserverScreen : UiScreen, IDisposable
{
#if DUXEL_GALLERY_PROFILING
    internal bool ProfileRenderSections { get; set; }
    internal (long Navigation, long Options, long Page) RenderSectionTicks { get; private set; }
#endif
    private static readonly UiTooltipOptions ToolbarTooltipOptions = new()
    {
        ShowDelaySeconds = .75,
        UseShowDelayOnHandover = true
    };

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
    internal string FullImplementationForPage(string page) => GalleryPageSource.FullImplementation(_pages.Single(item => item.Title == page));
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
    internal void FreezeDiagnosticsForMeasurement()
    {
        FreezeDiagnosticText = true;
        _rendererStatus = "250.0 FPS · 4.00ms | P 100 · D 1 · U 4.0KB · C 0";
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
        _themeSwitch = Fluent.Switch(GalleryText.Get("Light theme"), new UiBinding<bool>(
            () => _currentUi!.GetStyle(includeRequested : true).FluentColorScheme == UiFluentColorScheme.Light, light =>
            {
                var theme = (light ? UiCompiledDesign.Windows11 : UiCompiledDesign.Windows11Dark).Theme;
                if (_currentUi!.GetStyle(includeRequested : true).ForcedColors is { } system) theme = system.Apply(theme);
                _currentUi.RequestTheme(theme);
            }));
        _toolbar = Dux.SizedBox(Dux.Row(new(Spacing: 16, CrossAxisAlignment: UiCrossAxisAlignment.Center),
            Dux.Expanded(Fluent.Tooltip(Fluent.Caption1(() => _rendererStatus), GalleryText.Get("P : presentations / D : draw calls / U : uploaded bytes / C : cache hits"), ToolbarTooltipOptions)),
            Fluent.Switch("V-Sync", new UiBinding<bool>(() => _currentUi!.GetVSync(), value => _currentUi!.SetVSync(value))),
            Fluent.Switch(GalleryText.Get("Event-driven mode"), new UiBinding<bool>(() => _updateOnlyOnChange, value => _updateOnlyOnChange = value)),
            Fluent.Switch(GalleryText.Get("Highlight repaints"), _repaintHighlights.Binding)));
        _toolbarScroll = Dux.ScrollView(_toolbar, new(UiScrollAxis.Horizontal));
        _toolbarScroll.AccessibleName = GalleryText.Get("Gallery status and options");
        _performance = GalleryText.Get("Measuring FPS");
        _headerActions = _themeSwitch;
        if (requestLanguage is not null)
        {
            var current = GalleryText.Tag(language);
            var error = new UiState<string>(string.Empty);
            var selected = new UiState<string?>(current);
            var languageLabel = GalleryText.Get("Gallery language");
            var choices = GalleryText.LanguageOptions
                .Select(option => new UiRadioOption(option.Value, option.Text)).ToArray();
            var apply = Fluent.Button(GalleryText.Get("Apply"), () =>
            {
                try
                {
                    if (selected.Value is { } tag)
                        requestLanguage(GalleryText.FromTag(tag));
                    _settings!.Close();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    error.Value = GalleryText.Get("Could not save the language setting.");
                }
            });
            _settings = Fluent.Dialog(GalleryText.Get("Settings"),
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.RadioGroup(languageLabel, selected.Binding, choices),
                    Fluent.Caption1(() => error.Value)),
                [apply, Fluent.Button(GalleryText.Get("Cancel"), () => _settings!.Close())],
                new(Width : 410, CloseLabel : GalleryText.Get("Close")));
            var settingsLabel = GalleryText.Get("Settings");
            var settingsButton = new UiButton<UiReleaseButtonBehavior,
                UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize, UiFluentSubtleButtonAppearance,
                    SettingsTabIcon, UiButtonIconBefore>>(string.Empty, () =>
                {
                    selected.Value = current;
                    error.Value = string.Empty;
                    _settings.Show();
                })
            {
                AccessibleName = settingsLabel
            };
            _headerActions = Dux.Row(new(Spacing : 12, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                _themeSwitch, Fluent.Tooltip(settingsButton, settingsLabel, ToolbarTooltipOptions));
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
            new WidgetPage("Fluent", "Try the live controls and check that values survive switching tabs.", _controls),
            new WidgetPage("평가", "Adjust ratings in half-star steps or display them from the right. Narrow the parent to see how the stars wrap.", new RatingPage()),
            new WidgetPage("타이포그래피", "Compare Fluent text sizes, weights, and paragraph directions.", TypographyPage),
            new WidgetPage("입력·IME", "Try Korean input, selection, validation, line wrapping, and undo.", new EditingPage()),
            new WidgetPage("Tooltip", "Compare tooltips that open automatically with tooltips controlled by code.", new TooltipPage()),
            new WidgetPage("컨테이너", "Compare widget placement across containers. Change layouts or interact with the widgets inside them.", new LayoutPage()),
            new WidgetPage("배치 통합", "Combine tabs, menus, and overflow layouts, and check that state survives navigation.", new LayoutIntegrationPage()),
            new WidgetPage("접기·펼치기", "Open one or several accordion sections. Check that input values persist and try operating the sections with a keyboard or accessibility features.", new AccordionPage()),
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
            new WidgetPage("스켈레톤", "Build loading placeholders. Compare wave and pulse animations, pause them, and try reduced motion.", new SkeletonPage()),
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
            new WidgetPage("관찰 범위", "Review available factories, example destinations, and areas awaiting verification.", catalog),
            new WidgetPage("측정 재사용", "Reuse layout measurements that did not change while continuing to handle values and input.", MeasurementPage),
            RepaintPage,
            MarkdownPage,
            new LocalizationPage(),
            .. GalleryComponentExamples.Create(page => SelectedPage = page),
            .. Fluent.MissingComponentNames.ToArray().Where(static component => component != "Toast").Select(static component => (IObserverPage)new WidgetPage(component,
                "Fluent 구성요소 구현 예정",
                Dux.Column(new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min),
                    Fluent.Subtitle1(GalleryText.Get("Planned component")),
                    Fluent.Body1(GalleryText.Get("The API and interactive examples for this component are planned.")),
                    Fluent.Body1(GalleryText.Get("The official component catalog guides this roadmap. Design and accessibility checks continue for the available examples.")))))
        ];
        _navigation = new(PageTitles, Fluent.MissingComponentNames.ToArray(), new UiBinding<string?>(() => _selection.Value, SelectRoute));
        catalog.Connect(_navigation.Destinations, SelectRoute);
        _navigationDrawer = Fluent.Drawer(GalleryText.Get("Gallery navigation"), _navigation.Content,
            options : new(Extent : 320, CloseLabel : GalleryText.Get("Close navigation"), ScrollBody : false));
        _compactHeader = Dux.Row(new(Spacing : 12, CrossAxisAlignment : UiCrossAxisAlignment.Center),
            Fluent.Button(GalleryText.Get("Open navigation"), OpenNavigation),
            Dux.Expanded(Fluent.Subtitle2(() => SelectedPage is null ? GalleryText.Get("Select a page") : _navigation.SelectedLabel)));
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

        var intro = GalleryText.Get("Build UI with Duxel");
        var browse = new UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance>>(
            GalleryText.Get("Feature index"), () => SelectRoute("관찰 범위"));
        var hero = Fluent.Card(intro,
            Dux.Padding(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
                Fluent.Title1(intro),
                new UiInlineText([new(GalleryText.Get("Try Duxel controls, graphics, and document features, and inspect the C# examples."))]),
                Dux.Align(browse, UiAlignment.TopLeft)), new(12)),
            new(Appearance : UiCardAppearance.FilledAlternative));

        return Dux.Padding(Dux.Column(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min,
            CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
            hero,
            Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Title3(GalleryText.Get("Where to start")),
                Dux.Wrap(new(Spacing : 16, RunSpacing : 16),
                    Category("Actions and navigation", "Build navigation with buttons, links, and menus.", "Button", "Explore Button"),
                    Category("Input and selection", "Try entering text, making selections, and adjusting values.", "Input", "Explore Input"),
                    Category("Content and lists", "Organize content with cards and lists.", "Card", "Explore Card"),
                    Category("Alerts and help", "Try dialogs and auxiliary panels.", "Dialog", "Explore Dialog"),
                    Category("Vector graphics", "Zoom and rotate SVGs and explore graphics examples.", "SVG", "Explore SVG"),
                    Category("Documents and editing", "Read and edit Markdown documents in real time.", "Markdown", "Explore Markdown"))),
            Fluent.Caption1(GalleryText.Get("Choose a component from the navigation. Read or copy the C# source just below its example."))),
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
