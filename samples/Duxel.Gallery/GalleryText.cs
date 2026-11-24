using System.Globalization;
using Duxel.App;
using Duxel.Core;

namespace Duxel.Gallery;

internal enum GalleryLanguage
{
    Korean,
    English,
    Japanese,
    SimplifiedChinese
}

internal static class GalleryText
{
    [ThreadStatic]
    private static GalleryLanguage _language;

    private static readonly CultureInfo[] Cultures = Enum.GetValues<GalleryLanguage>()
        .Select(language => CultureInfo.GetCultureInfo(Tag(language))).ToArray();

    private static readonly GalleryLanguage[] TranslationLanguages =
        [GalleryLanguage.Korean, GalleryLanguage.Japanese, GalleryLanguage.SimplifiedChinese];
    private static readonly UiLocalization Catalog = new(ReadTranslations());

    internal static readonly UiSelectOption[] LanguageOptions =
    [
        new("en", "English"),
        new("ko", "한국어"),
        new("ja", "日本語"),
        new("zh-Hans", "简体中文")
    ];

    internal static string Tag(GalleryLanguage language) => language switch
    {
        GalleryLanguage.Korean => "ko",
        GalleryLanguage.English => "en",
        GalleryLanguage.Japanese => "ja",
        GalleryLanguage.SimplifiedChinese => "zh-Hans",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    internal static GalleryLanguage FromTag(string tag) => tag switch
    {
        "ko" => GalleryLanguage.Korean,
        "en" => GalleryLanguage.English,
        "ja" => GalleryLanguage.Japanese,
        "zh-Hans" => GalleryLanguage.SimplifiedChinese,
        _ => throw new ArgumentException("Unknown Gallery language: " + tag, nameof(tag))
    };

    internal static GalleryLanguage FromSystemCulture(CultureInfo culture)
    {
        if (culture.TwoLetterISOLanguageName == "zh")
        {
            for (var current = culture; current.Name.Length != 0; current = current.Parent)
                if (current.Name == "zh-Hant")
                    return GalleryLanguage.English;
            return GalleryLanguage.SimplifiedChinese;
        }

        return culture.TwoLetterISOLanguageName switch
        {
            "ko" => GalleryLanguage.Korean,
            "ja" => GalleryLanguage.Japanese,
            _ => GalleryLanguage.English
        };
    }

    internal static string RegionalCultureName => _language switch
    {
        GalleryLanguage.English => "en-US",
        GalleryLanguage.Korean => "ko-KR",
        GalleryLanguage.Japanese => "ja-JP",
        GalleryLanguage.SimplifiedChinese => "zh-CN",
        _ => throw new ArgumentOutOfRangeException(nameof(_language))
    };

    internal static string Get(string english) => Catalog.GetText(english, Cultures[(int)_language]);

    // Page identifiers predate localization and remain stable for navigation and source lookup.
    internal static string PageTitle(string page) => Get(page switch
    {
        "가상 그리드" => "Virtual grid",
        "가상 목록" => "Virtual list",
        "개요" => "Overview",
        "갱신 관찰" => "Repaint observer",
        "검색 입력" => "Search boxes",
        "경로" => "Breadcrumbs",
        "공휴일 제공자" => "Holiday provider",
        "관찰 범위" => "Implementation scope",
        "기존 API" => "Legacy API",
        "대화 상자" => "Dialogs",
        "도구 모음" => "Toolbars",
        "렌더링" => "Rendering",
        "메시지 표시" => "Message bars",
        "목록" => "Lists",
        "배치 통합" => "Layout integration",
        "보조 패널" => "Side panels",
        "선택 목록" => "Selection lists",
        "숫자 입력" => "Numeric input",
        "스켈레톤" => "Skeleton",
        "아바타" => "Avatars",
        "아바타 그룹" => "Avatar groups",
        "왼쪽 탐색 메뉴·Fluent 표현 비교·컨테이너 넘침" => "Sidebar navigation, Fluent styles, and container overflow",
        "위젯 배치" => "Widget layout",
        "인물 정보" => "Persona",
        "입력·IME" => "Input and IME",
        "접기·펼치기" => "Accordion",
        "추가 정보" => "Information labels",
        "측정 재사용" => "Measurement reuse",
        "카드" => "Cards",
        "컨테이너" => "Containers",
        "컨테이너·Menu 공통 동작" => "Shared container and menu behavior",
        "타이포그래피" => "Typography",
        "탐색 메뉴" => "Navigation menus",
        "파일 대화 상자" => "File dialogs",
        "팝업 입력" => "Pop-up input",
        "평가" => "Rating",
        _ => page
    });

    internal static string Format(string english, params object[] args) =>
        Catalog.Format(english, Cultures[(int)_language], args);

    internal static DuxelFontOptions ConfigureFonts(DuxelFontOptions fonts)
    {
        var cjk = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSansCJK", "NotoSansCJKsc-Regular.otf");
        return fonts with
        {
            DirectTextSecondaryFontPath = cjk,
            Typography = fonts.Typography with { SecondaryRegular = cjk, SecondarySemibold = cjk, SecondaryBold = cjk }
        };
    }

    internal static UiCodeViewLabels CodeViewLabels() => new(Get("C# source"), Get("{0} lines"), Get("Wrap lines"),
        Get("Line numbers"), Get("Show all"), Get("Copy source"), Get("Copied"), Get("C# example source"))
    {
        ContextMenuName = Get("Text actions"),
        CopySelection = Get("Copy"),
        SelectAll = Get("Select All")
    };

    internal static Scope Use(GalleryLanguage language) => new(language);

    internal readonly struct Scope : IDisposable
    {
        private readonly GalleryLanguage _previous;

        internal Scope(GalleryLanguage language)
        {
            _previous = _language;
            _language = language;
        }

        public void Dispose() => _language = _previous;
    }

    private static IEnumerable<(string CultureName, string Source, string Text)> ReadTranslations()
    {
        using var stream = typeof(GalleryText).Assembly.GetManifestResourceStream("GalleryLocalization.tsv")
            ?? throw new InvalidOperationException("Gallery localization resource is missing.");
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            var row = line.Split('\t');
            if (row.Length != TranslationLanguages.Length + 1 || row.Any(string.IsNullOrWhiteSpace))
                throw new FormatException("Invalid Gallery translation: " + line);
            for (var index = 0; index < TranslationLanguages.Length; index++)
                yield return (Cultures[(int)TranslationLanguages[index]].Name, row[0], row[index + 1]);
        }
    }
}

internal sealed class GalleryLocalizedScreen : UiScreen, IDisposable
{
    private readonly ObserverServices _services;
    private readonly DuxelAppSession _session;
    private readonly string _settingsPath;
    private readonly bool _controlsInTitleBar;
    private GalleryLanguage _active;
    private GalleryLanguage _requested;
    private ObserverScreen _observer;

    internal GalleryLocalizedScreen(ObserverServices services, DuxelAppSession session, string? settingsPath = null,
        bool controlsInTitleBar = false)
    {
        _services = services;
        _session = session;
        _controlsInTitleBar = controlsInTitleBar;
        var directory = Environment.GetEnvironmentVariable("DUXEL_GALLERY_SETTINGS_DIRECTORY")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Duxel", "Gallery");
        _settingsPath = settingsPath ?? Path.Combine(directory, "language.txt");
        _requested = _active = File.Exists(_settingsPath)
            ? GalleryText.FromTag(File.ReadAllText(_settingsPath).Trim())
            : GalleryText.FromSystemCulture(CultureInfo.CurrentUICulture);
        _observer = CreateObserver();
    }

    internal bool UpdateOnlyOnChange => _observer.UpdateOnlyOnChange;

    private ObserverScreen CreateObserver()
    {
        using var locale = GalleryText.Use(_active);
        return new(_services, _session, _active, RequestLanguage, _controlsInTitleBar);
    }

    private void RequestLanguage(GalleryLanguage language)
    {
        if (_requested == language)
            return;

        var path = Path.GetFullPath(_settingsPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, GalleryText.Tag(language));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        _requested = language;
        _session.RequestFrame();
    }

    public override void Render(UiImmediateContext ui)
    {
        if (_requested != _active)
        {
            var previous = _observer;
            _active = _requested;
            _observer = CreateObserver();
            _observer.SelectedPage = previous.SelectedPage;
            _observer.CopyPreferencesFrom(previous);
            previous.Dispose();
        }

        using var locale = GalleryText.Use(_active);
        _observer.Render(ui);
    }

    public void Dispose() => _observer.Dispose();
}
