using System.Globalization;
using Duxel.App;
using Duxel.Core;
using Duxel.Gallery;

internal static class GalleryLocalizationTests
{
    internal static void Run(Action<bool, string> check)
    {
        var catalog = new UiLocalization([("fr", "Amount: {0:N2}", "Montant : {0:N2}"), ("ja", "Settings", "設定")]);
        var region = CultureInfo.GetCultureInfo("fr-CA");
        check(catalog.TryGetText("Amount: {0:N2}", region, out var template) && template == "Montant : {0:N2}",
            "Duxel localization resolves the parent culture");
        check(catalog.Format("Amount: {0:N2}", region, 1234.5) == string.Format(region, template!, 1234.5),
            "Duxel localization formats arguments in the requested culture");
        check(catalog.GetText("Settings", region) == "Settings", "Duxel localization uses the source when no translation exists");

        foreach (var (language, dialog, messageBar, avatarGroup) in new[]
        {
            (GalleryLanguage.English, "Dialogs", "Message bars", "Avatar groups"),
            (GalleryLanguage.Japanese, "ダイアログ", "メッセージバー", "アバターグループ"),
            (GalleryLanguage.SimplifiedChinese, "对话框", "消息栏", "头像组")
        })
        {
            using var locale = GalleryText.Use(language);
            var titles = CatalogPage.Groups.Select(group => GalleryText.Get(group.Page)).ToArray();
            check(titles.All(title => !title.Any(character => character is >= '\uAC00' and <= '\uD7A3'))
                && titles.Contains(dialog) && titles.Contains(messageBar) && titles.Contains(avatarGroup),
                "Feature index localizes every factory group heading in " + language);
        }

        foreach (var (language, field, multiline, country, container) in new[]
        {
            (GalleryLanguage.English, "Enter at least two characters.", "First line\nSecond line", "United States", "Maximum container width: {0:F0} px"),
            (GalleryLanguage.Korean, "두 글자 이상 입력하세요.", "첫 줄\n둘째 줄", "미국", "부모 최대 폭 {0:F0}px"),
            (GalleryLanguage.Japanese, "2 文字以上入力してください。", "1 行目\n2 行目", "米国", "親コンテナーの最大幅：{0:F0} px"),
            (GalleryLanguage.SimplifiedChinese, "请输入至少两个字符。", "第一行\n第二行", "美国", "父容器最大宽度：{0:F0} px")
        })
        {
            using var locale = GalleryText.Use(language);
            foreach (var example in GalleryComponentExamples.Create())
            {
                using var page = (ComponentExamplePage)example;
                var description = GalleryText.Get(page.Description);
                var samples = page.Samples.Select(sample => sample.Source).ToArray();
                check(!string.IsNullOrWhiteSpace(description) && (language == GalleryLanguage.Korean
                    || (description != page.Description && !ContainsHangul(description))),
                    $"{page.Title} has a localized description in {language}");
                check(samples.All(source => !source.Contains("GalleryText.Get", StringComparison.Ordinal)
                    && (language == GalleryLanguage.Korean || !ContainsHangul(source))),
                    $"{page.Title} exposes self-contained localized C# in {language}");

                var expectedSourceText = page.Title switch
                {
                    "Field" => field,
                    "Textarea" => multiline,
                    "Dropdown" => country,
                    "RadioGroup" => container,
                    _ => null
                };
                if (expectedSourceText is not null)
                    check(samples.Any(source => source.Contains(GallerySample.String(expectedSourceText), StringComparison.Ordinal)),
                        $"{page.Title} includes the expected localized label or value in {language}");
            }
        }

        var systemLanguage = CultureInfo.CurrentUICulture;
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            var fonts = GalleryText.ConfigureFonts(new());
            context.SetDirectTextSecondaryFontPath(fonts.SecondaryFontPath);
            context.SetTypographyFonts(fonts.Typography);
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            var services = ObserverServices.CreateWindows();
            var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Render(UiScreen active)
            {
                context.NewFrame(new(1f / 60, new(1280, 900), new(1, 1)), input, new(0, 0, 1280, 900), UiTextSettings.Default);
                context.Render(active);
                using var draw = context.GetDrawData();
            }
            UiAccessibilityNode? Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements
                .Where(element => element.Node.Name == name && element.Node.Role == role)
                .Select(element => (UiAccessibilityNode?)element.Node).FirstOrDefault();

            foreach (var (culture, heading) in new[]
            {
                ("ko-KR", "개요"),
                ("en-GB", "Overview"),
                ("ja-JP", "概要"),
                ("zh-CN", "概览"),
                ("zh-TW", "Overview"),
                ("fr-FR", "Overview"),
                ("", "Overview")
            })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                using var firstLaunch = new GalleryLocalizedScreen(services, new DuxelAppSession(), path);
                Render(firstLaunch);
                check(Find(heading, UiAccessibilityRole.Pane) is not null && !File.Exists(path),
                    "Gallery first launch uses the expected language for " + (culture.Length == 0 ? "the invariant culture" : culture));
            }

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
            File.WriteAllText(path, "en");
            using var screen = new GalleryLocalizedScreen(services, new DuxelAppSession(), path);
            void Frame()
            {
                Render(screen);
                context.State.AdvanceTime(.25f);
                Render(screen);
            }
            void Invoke(string name, UiAccessibilityRole role = UiAccessibilityRole.Button,
                UiAccessibilityAction action = UiAccessibilityAction.Invoke)
            {
                var node = Find(name, role) ?? throw new InvalidOperationException("Gallery command is missing: " + name);
                if (!tree.TryGet(node.Id, out var entry)
                    || tree.RequestAction(node.Id, entry.RuntimeId, action, wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Gallery command was not accepted: " + name);
                Frame();
            }

            Frame();
            check(Find("Overview", UiAccessibilityRole.Pane) is not null, "Gallery prefers saved English to the Japanese system language");
            var settingsButton = Find("Settings", UiAccessibilityRole.Button)
                ?? throw new InvalidOperationException("The settings gear was not published.");
            check(settingsButton.Bounds.Width <= settingsButton.Bounds.Height + 1f,
                "Gallery settings uses a square icon button with a localized accessible name");
            Invoke("Feature index", UiAccessibilityRole.Link);
            Invoke("DataGrid");
            check(Find("DataGrid", UiAccessibilityRole.Pane) is not null, "Feature index opens the live DataGrid route");

            Invoke("Settings");
            Invoke("日本語", UiAccessibilityRole.RadioButton, UiAccessibilityAction.Select);
            Invoke("Cancel");
            check(File.ReadAllText(path) == "en", "Cancel leaves the saved Gallery language unchanged");
            Invoke("Button", UiAccessibilityRole.Link);
            var currentLanguage = "English";
            foreach (var (settings, choice, next, language, description, increment, count, codeView) in new[]
            {
                ("Settings", "日本語", "設定", "ja", "ボタンを押してクリック数を増やせます。", "カウントを増やす", "クリック数：", "C# サンプルソース"),
                ("設定", "简体中文", "设置", "zh-Hans", "按下按钮增加点击次数。", "增加计数", "点击次数：", "C# 示例代码"),
                ("设置", "한국어", "설정", "ko", "버튼을 눌러 클릭 횟수를 늘려 보세요.", "카운터 증가", "클릭 횟수 : ", "C# 예제 소스"),
                ("설정", "English", "Settings", "en", "Press the button to increase the click count.", "Increase count", "Click count: ", "C# example source")
            })
            {
                Invoke(settings);
                var radios = tree.CaptureSnapshot().Elements.Select(element => element.Node)
                    .Where(node => node.Role == UiAccessibilityRole.RadioButton && !node.Offscreen).ToArray();
                check(radios.Length == 4 && radios.Count(node => node.Selected == true) == 1
                    && radios.Any(node => node.Name == currentLanguage && node.Selected == true && node.Focused),
                    "Settings announces and focuses the applied language before switching to " + language);
                Invoke(choice, UiAccessibilityRole.RadioButton, UiAccessibilityAction.Select);
                check(Find(choice, UiAccessibilityRole.RadioButton)?.Selected == true && File.ReadAllText(path) != language,
                    "Language selection is a draft until Apply for " + language);
                Invoke(settings switch
                {
                    "設定" => "適用",
                    "设置" => "应用",
                    "설정" => "적용",
                    _ => "Apply"
                });
                currentLanguage = choice;
                check(Find(next, UiAccessibilityRole.Button) is not null && File.ReadAllText(path) == language,
                    "Gallery settings apply and save " + language);
                check(Find("Button", UiAccessibilityRole.Pane) is not null,
                    "Gallery retains the selected page after switching to " + language);
                check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == description),
                    "Gallery localizes the Button description in " + language);
                Invoke(increment);
                var source = Find(codeView, UiAccessibilityRole.Edit)?.Value;
                check(source is not null
                    && source.Contains("var clicks = new UiState<int>(1);", StringComparison.Ordinal)
                    && source.Contains("Fluent.Button(" + GallerySample.String(increment), StringComparison.Ordinal)
                    && source.Contains(GallerySample.String(count), StringComparison.Ordinal)
                    && !source.Contains("GalleryText.Get", StringComparison.Ordinal),
                    "Gallery source and working example use the same localized strings in " + language);
            }

            Invoke("Settings");
            Invoke("日本語", UiAccessibilityRole.RadioButton, UiAccessibilityAction.Select);
            Invoke("Apply");
            using var restarted = new GalleryLocalizedScreen(services, new DuxelAppSession(), path);
            Render(restarted);
            check(Find("設定", UiAccessibilityRole.Button) is not null && Find("概要", UiAccessibilityRole.Pane) is not null,
                "Gallery restores Japanese in a new session");
        }
        finally
        {
            CultureInfo.CurrentUICulture = systemLanguage;
            File.Delete(path);
        }
    }

    private static bool ContainsHangul(string text) => text.Any(character => character is >= '\uAC00' and <= '\uD7A3');

    internal static void CaptureOverview(string output, Action<bool, string> check)
    {
        using var locale = GalleryText.Use(GalleryLanguage.English);
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1280, 900));
        using var tree = new UiAccessibilityTree();
        using var screen = new ObserverScreen(ObserverServices.CreateWindows(), language : GalleryLanguage.English,
            requestLanguage : _ => { }) { FreezeDiagnosticText = true };
        var font = GalleryText.ConfigureFonts(new());
        fixture.Context.SetDirectTextSecondaryFontPath(font.SecondaryFontPath);
        fixture.Context.SetTypographyFonts(font.Typography);
        fixture.Context.SetAccessibilityTree(tree);
        foreach (var (name, theme) in new[]
        {
            ("dark", UiCompiledDesign.Windows11Dark.Theme), ("light", UiCompiledDesign.Windows11.Theme)
        })
        {
            fixture.Context.SetTheme(theme);
            var pixels = fixture.Capture(ui =>
            {
                ui.GetWindowDrawList().AddRectFilled(new(0, 0, 1280, 900), theme.WindowBg, ui.WhiteTextureId);
                screen.Render(ui);
            });
            BrowserPng.Write(Path.Combine(output, "gallery-overview-" + name + ".png"), 1280, 900, pixels);
            var nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
            check(nodes.Any(node => node.Name == "Feature index" && node.Role == UiAccessibilityRole.Button && !node.Offscreen)
                && nodes.Any(node => node.Name == "Explore Button" && !node.Offscreen)
                && nodes.Any(node => node.Name == "Explore Markdown" && !node.Offscreen),
                "Gallery overview presents the feature index and example cards in " + name);
        }

        using var japanese = GalleryText.Use(GalleryLanguage.Japanese);
        using var japaneseScreen = new ObserverScreen(ObserverServices.CreateWindows(), language : GalleryLanguage.Japanese,
            requestLanguage : _ => { }) { FreezeDiagnosticText = true };
        var dark = UiCompiledDesign.Windows11Dark.Theme;
        fixture.Context.SetTheme(dark);
        byte[] CaptureJapanese() => fixture.Capture(ui =>
        {
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, 1280, 900), dark.WindowBg, ui.WhiteTextureId);
            japaneseScreen.Render(ui);
        });
        CaptureJapanese();
        var settings = tree.CaptureSnapshot().Elements.Single(element =>
            element.Node.Name == "設定" && element.Node.Role == UiAccessibilityRole.Button);
        if (tree.RequestAction(settings.Node.Id, settings.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The Japanese settings action was not accepted.");
        CaptureJapanese();
        BrowserPng.Write(Path.Combine(output, "settings-japanese.png"), 1280, 900, CaptureJapanese());
    }
}
