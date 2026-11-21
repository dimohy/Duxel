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

    internal static GalleryLanguage FromSystemCulture(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "ko" => GalleryLanguage.Korean,
        "ja" => GalleryLanguage.Japanese,
        "zh" => GalleryLanguage.SimplifiedChinese,
        _ => GalleryLanguage.English
    };

    internal static string Get(string text) => Catalog.GetText(text, Cultures[(int)_language]);

    internal static string Format(string text, params object[] args) =>
        Catalog.Format(text, Cultures[(int)_language], args);

    internal static DuxelFontOptions ConfigureFonts(DuxelFontOptions fonts)
    {
        var cjk = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSansCJK", "NotoSansCJKsc-Regular.otf");
        return fonts with
        {
            SecondaryFontPath = cjk,
            Typography = fonts.Typography with { SecondaryRegular = cjk, SecondarySemibold = cjk, SecondaryBold = cjk }
        };
    }

    internal static UiCodeViewLabels CodeViewLabels() => new(Get("C# 소스"), Get("{0}줄"), Get("줄바꿈"),
        Get("줄 번호"), Get("전체 표시"), Get("소스 복사"), Get("복사됨"), Get("C# 예제 소스"));

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
            if (row.Length != Cultures.Length || row.Any(string.IsNullOrWhiteSpace))
                throw new FormatException("Invalid Gallery translation: " + line);
            for (var index = 1; index < Cultures.Length; index++)
                yield return (Cultures[index].Name, row[0], row[index]);
        }
    }
}

internal sealed class GalleryLocalizedScreen : UiScreen, IDisposable
{
    private readonly ObserverServices _services;
    private readonly DuxelAppSession _session;
    private readonly string _settingsPath;
    private GalleryLanguage _active;
    private GalleryLanguage _requested;
    private ObserverScreen _observer;

    internal GalleryLocalizedScreen(ObserverServices services, DuxelAppSession session, string? settingsPath = null)
    {
        _services = services;
        _session = session;
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
        return new(_services, _session, _active, RequestLanguage);
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
