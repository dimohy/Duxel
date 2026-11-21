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
                .Select(element => element.Node).FirstOrDefault(node => node.Name == name && node.Role == role);

            foreach (var (culture, heading) in new[]
            {
                ("ko-KR", "개요"),
                ("en-GB", "Overview"),
                ("ja-JP", "概要"),
                ("zh-CN", "概览"),
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
            void Invoke(string name, UiAccessibilityRole role = UiAccessibilityRole.Button)
            {
                var node = Find(name, role) ?? throw new InvalidOperationException("Gallery command is missing: " + name);
                if (!tree.TryGet(node.Id, out var entry)
                    || tree.RequestAction(node.Id, entry.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Gallery command was not accepted: " + name);
                Frame();
            }

            Frame();
            check(Find("Overview", UiAccessibilityRole.Pane) is not null, "Gallery prefers saved English to the Japanese system language");
            Invoke("Feature index", UiAccessibilityRole.Link);
            Invoke("DataGrid");
            check(Find("DataGrid", UiAccessibilityRole.Pane) is not null, "Feature index opens the live DataGrid route");

            foreach (var (settings, choice, next, language) in new[]
            {
                ("Settings", "日本語", "設定", "ja"),
                ("設定", "简体中文", "设置", "zh-Hans"),
                ("设置", "한국어", "설정", "ko"),
                ("설정", "English", "Settings", "en")
            })
            {
                Invoke(settings);
                Invoke(choice);
                check(Find(next, UiAccessibilityRole.Button) is not null && File.ReadAllText(path) == language,
                    "Gallery settings apply and save " + language);
                check(Find("DataGrid", UiAccessibilityRole.Pane) is not null,
                    "Gallery retains the selected page after switching to " + language);
            }

            Invoke("Settings");
            Invoke("日本語");
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
}
