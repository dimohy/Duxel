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
            var titles = CatalogPage.Groups.Select(group => GalleryText.PageTitle(group.Page)).ToArray();
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
                check(!string.IsNullOrWhiteSpace(description) && !ContainsHangul(page.Description)
                    && (language == GalleryLanguage.Korean || !ContainsHangul(description))
                    && (language != GalleryLanguage.English || description == page.Description),
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

        using (GalleryText.Use(GalleryLanguage.English))
            check(GalleryText.Get("Example language") == "Example language", "English is the source and fallback language");
        using (GalleryText.Use(GalleryLanguage.Korean))
            check(GalleryText.Get("Example language") == "예제 언어", "Gallery translates an English source phrase to Korean");

        VerifyNavigationAndSelection(check);

        VerifyInputExampleSources(check);

        VerifyOverlayExampleSources(check);

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

            VerifyDedicatedExamples(Render, tree, services, check);
            VerifyLoadingExamples(Render, tree, services, check);
            VerifyNoticeExamples(Render, tree, services, check);

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
                var dialog = tree.CaptureSnapshot().Elements.Single(element => element.Node is { Role: UiAccessibilityRole.Dialog });
                var pendingNodes = new Queue<string>();
                void AddChild(string? child)
                {
                    if (child is not null)
                        pendingNodes.Enqueue(child);
                }
                AddChild(dialog.FirstChild);
                var scrollable = false;
                while (pendingNodes.TryDequeue(out var id) && tree.TryGet(id, out var element))
                {
                    scrollable |= element.Node.Scroll is { VerticallyScrollable: true } or { HorizontallyScrollable: true };
                    AddChild(element.FirstChild);
                    AddChild(element.Next);
                }
                check(!scrollable, "Settings fits its controls without a scroll range before switching to " + language);
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

    private static void VerifyDedicatedExamples(Action<UiScreen> render, UiAccessibilityTree tree, ObserverServices services,
        Action<bool,string> check)
    {
        foreach (var (language, widthButton, breadcrumb, archive, grid, column, file) in new[]
        {
            (GalleryLanguage.English, "Change path width", "Project path", "Document archive", "Design review files", "File name", "Design review 0001.pdf"),
            (GalleryLanguage.Korean, "경로 폭 변경", "프로젝트 경로", "문서 보관함", "디자인 검토 파일", "파일 이름", "디자인 검토 0001.pdf"),
            (GalleryLanguage.Japanese, "パスの幅を変更", "プロジェクトのパス", "ドキュメント保管庫", "デザインレビューのファイル", "ファイル名", "デザインレビュー 0001.pdf"),
            (GalleryLanguage.SimplifiedChinese, "更改路径宽度", "项目路径", "文档归档", "设计评审文件", "文件名", "设计评审 0001.pdf")
        })
        {
            using var locale = GalleryText.Use(language);
            using var screen = new ObserverScreen(services, language : language);
            screen.SelectedPage = "경로";
            render(screen);
            var nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
            check(nodes.Any(node => node.Name == widthButton && node.Role == UiAccessibilityRole.Button)
                && nodes.Any(node => node.Name == breadcrumb && node.Role == UiAccessibilityRole.Navigation),
                "Breadcrumb localizes its controls and navigation landmark in " + language);
            VerifySource(nodes, language, archive, "Breadcrumb");
            screen.SelectedPage = "DataGrid";
            render(screen);
            nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
            check(nodes.Any(node => node.Name == grid && node.Role == UiAccessibilityRole.Grid)
                && nodes.Any(node => node.Name == column && node.Role == UiAccessibilityRole.Button)
                && nodes.Any(node => node.Value == file || node.Name == file),
                "DataGrid localizes its name, header and sample file in " + language);
            VerifySource(nodes, language, column, "DataGrid");
        }

        void VerifySource(UiAccessibilityNode[] nodes, GalleryLanguage language, string expected, string page)
        {
            var editorName = GalleryText.Get("C# example source");
            check(nodes.Any(node => node.Name == editorName && node.Role == UiAccessibilityRole.Edit && node.Value is { } code
                && code.Contains(GallerySample.String(expected), StringComparison.Ordinal)
                && !code.Contains("GalleryText.Get", StringComparison.Ordinal)
                && (language == GalleryLanguage.Korean || !ContainsHangul(code))),
                page + " localizes its copied C# in " + language);
        }
    }

    private static void VerifyNavigationAndSelection(Action<bool, string> check)
    {
        foreach (var (language, title, week, unread, region, germany) in new[]
        {
            (GalleryLanguage.English, "Meeting notes", "Weekly", "Unread", "Region", "Germany"),
            (GalleryLanguage.Korean, "회의록", "주간", "읽지 않음", "지역", "독일"),
            (GalleryLanguage.Japanese, "議事録", "週別", "未読", "地域", "ドイツ"),
            (GalleryLanguage.SimplifiedChinese, "会议纪要", "按周", "未读", "地区", "德国")
        })
        {
            using var locale = GalleryText.Use(language);
            using var navigation = new NavigationBarPage();
            var doc = (GallerySample<UiFluentNavigationBar>)navigation.Samples[0];
            check(doc.Preview.Title == title && navigation.PeriodSelector.Items[1].Label == week,
                "NavigationBar localizes its title and hosted selection in " + language);
            CheckSources(navigation.Samples, title, week, language, "NavigationBar");

            using var segments = new SegmentedControlPage();
            check(segments.Preview(0).Options.UnreadLabel == unread && segments.Preview(4).Items.Length == 10,
                "SegmentedControl localizes its unread label and retains its horizontal samples in " + language);
            CheckSources(segments.Samples, unread, unread, language, "SegmentedControl");

            using var select = new SelectPage();
            var preview = ((GallerySample<UiFluentSelect>)select.Samples[0]).Preview;
            check(preview.Label == region && preview.Items[3] is { Enabled: false } disabled && disabled.Text == germany,
                "Select localizes its accessible label and retains the disabled country in " + language);
            CheckSources(select.Samples, region, germany, language, "Select");
        }

        void CheckSources(IEnumerable<IGallerySample> samples, string first, string second, GalleryLanguage language, string page)
        {
            var source = string.Join("\n", samples.Select(sample => sample.Source));
            check(source.Contains(GallerySample.String(first), StringComparison.Ordinal)
                && source.Contains(GallerySample.String(second), StringComparison.Ordinal)
                && !source.Contains("GalleryText.", StringComparison.Ordinal)
                && (language == GalleryLanguage.Korean || !ContainsHangul(source)),
                page + " exposes the localized example data in its C# source in " + language);
        }
    }

    private static void VerifyInputExampleSources(Action<bool, string> check)
    {
        foreach (var (language, query, clear, quantity, weight, email, hint) in new[]
        {
            (GalleryLanguage.English, "Search query", "Clear search query", "Increase quantity", "Weight (kg)", "Email", "Example: name@example.com"),
            (GalleryLanguage.Korean, "검색어", "검색어 지우기", "수량 늘리기", "무게 (kg)", "이메일", "예: name@example.com"),
            (GalleryLanguage.Japanese, "検索語", "検索語を消去", "数量を増やす", "重量 (kg)", "メールアドレス", "例：name@example.com"),
            (GalleryLanguage.SimplifiedChinese, "搜索词", "清除搜索词", "增加数量", "重量（kg）", "电子邮箱", "例如：name@example.com")
        })
        {
            using var locale = GalleryText.Use(language);
            using var search = new SearchBoxPage();
            using var number = new SpinButtonPage();
            using var field = new TextFieldPage();
            Check(search.Samples, query, clear, "SearchBox");
            Check(number.Samples, quantity, weight, "SpinButton");
            Check(field.Samples, email, hint, "TextField");

            void Check(IEnumerable<IGallerySample> samples, string name, string label, string page)
            {
                var source = string.Join("\n", samples.Select(sample => sample.Source));
                check(source.Contains(GallerySample.String(name), StringComparison.Ordinal)
                    && source.Contains(GallerySample.String(label), StringComparison.Ordinal)
                    && !source.Contains("GalleryText.", StringComparison.Ordinal)
                    && (language == GalleryLanguage.Korean || !ContainsHangul(source)),
                    page + " copies localized input and accessibility labels in " + language);
            }
        }
    }

    private static void VerifyOverlayExampleSources(Action<bool, string> check)
    {
        foreach (var (language, profile, close, details, tooltip, user, info, popup, command) in new[]
        {
            (GalleryLanguage.English, "Open profile editor", "Close decorative popover", "Details", "Tooltip target",
                "User name", "Show more information", "Open input pop-up", "Run command inside pop-up"),
            (GalleryLanguage.Korean, "프로필 편집 열기", "장식 팝업 닫기", "세부 정보", "설명 대상",
                "사용자 이름", "추가 정보 보기", "입력 팝업 열기", "팝업 안 명령"),
            (GalleryLanguage.Japanese, "プロフィール編集を開く", "装飾付きポップオーバーを閉じる", "詳細", "説明の対象",
                "ユーザー名", "詳細情報を表示", "入力ポップアップを開く", "ポップアップ内のコマンドを実行"),
            (GalleryLanguage.SimplifiedChinese, "打开个人资料编辑", "关闭装饰弹出层", "详细信息", "提示目标",
                "用户名", "显示更多信息", "打开输入弹出窗口", "执行弹出窗口内的命令")
        })
        {
            using var locale = GalleryText.Use(language);
            using var popover = new PopoverPage();
            using var tooltips = new TooltipPage();
            using var infoLabel = new InfoLabelPage();
            using var popupInput = new PopupInputPage();
            Check(popover.Samples, profile, close, "Popover");
            Check(tooltips.Samples, details, tooltip, "Tooltip");
            Check(infoLabel.Samples, user, info, "InfoLabel");
            Check(popupInput.Samples, popup, command, "Pop-up input");

            var declaration = GalleryPageSource.Declaration(nameof(GalleryPopoverDecoration));
            check(!declaration.Contains('\r') && popover.Samples[1].Source.EndsWith(declaration, StringComparison.Ordinal),
                "Popover copies the embedded presentation declaration with consistent line endings in " + language);

            void Check(IEnumerable<IGallerySample> samples, string first, string second, string page)
            {
                var source = string.Join("\n", samples.Select(sample => sample.Source));
                check(source.Contains(GallerySample.String(first), StringComparison.Ordinal)
                    && source.Contains(GallerySample.String(second), StringComparison.Ordinal)
                    && !source.Contains("GalleryText.", StringComparison.Ordinal)
                    && (language == GalleryLanguage.Korean || !ContainsHangul(source)),
                    page + " copies localized overlay strings in " + language);
            }
        }
    }

    private static void VerifyLoadingExamples(Action<UiScreen> render, UiAccessibilityTree tree, ObserverServices services,
        Action<bool, string> check)
    {
        foreach (var (language, shimmerToggle, profile, upload, increase, startStop, document, skeletonToggle) in new[]
        {
            (GalleryLanguage.English, "Toggle loading state", "Open profile", "File upload", "Increase progress for File upload",
                "Start or stop activity", "Loading document", "Loading"),
            (GalleryLanguage.Korean, "불러오기 상태 전환", "프로필 열기", "파일 업로드", "파일 업로드 진행률 증가",
                "활동 시작·중지", "문서를 불러오는 중", "로딩 중"),
            (GalleryLanguage.Japanese, "読み込み状態を切り替える", "プロフィールを開く", "ファイルのアップロード", "ファイルのアップロードの進捗を増やす",
                "アクティビティを開始または停止", "ドキュメントを読み込み中", "読み込み中"),
            (GalleryLanguage.SimplifiedChinese, "切换加载状态", "打开个人资料", "文件上传", "增加文件上传的进度",
                "启动或停止活动指示器", "正在加载文档", "加载中")
        })
        {
            using var locale = GalleryText.Use(language);
            using var screen = new ObserverScreen(services, language : language);
            RenderPage("Shimmer");
            Invoke(GalleryText.Get("Toggle reduced motion"), UiAccessibilityAction.Invoke);
            Invoke(shimmerToggle, UiAccessibilityAction.Invoke);
            check(Nodes().Any(node => node.Name == profile && node.Role == UiAccessibilityRole.Button && node.Enabled)
                && HasSource(GallerySample.String(profile), "example.IsDataLoaded = true"),
                "Shimmer reveals localized content and updates copied loading state in " + language);

            RenderPage("ProgressIndicator");
            Invoke(increase, UiAccessibilityAction.Invoke);
            check(HasSource(GallerySample.String(upload), "PercentComplete : 0.5f"),
                "ProgressIndicator retains the localized task name when progress changes in " + language);

            RenderPage("ActivityIndicator");
            Invoke(startStop, UiAccessibilityAction.Invoke);
            check(HasSource(GallerySample.String(document), "IsAnimating : false"),
                "ActivityIndicator retains the localized accessible name when stopped in " + language);

            RenderPage("스켈레톤");
            Invoke(skeletonToggle, UiAccessibilityAction.Toggle);
            check(HasSource(GallerySample.String(GalleryText.Get("Loading profile")), "var loading = false;"),
                "Skeleton keeps localized strings when its loading state changes in " + language);

            UiAccessibilityNode[] Nodes() => tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
            void RenderPage(string page)
            {
                screen.SelectedPage = page;
                render(screen);
                render(screen);
            }

            void Invoke(string name, UiAccessibilityAction action)
            {
                var target = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && !element.Node.Offscreen
                    && (element.Node.Role is UiAccessibilityRole.Button or UiAccessibilityRole.Switch));
                if (tree.RequestAction(target.Node.Id, target.RuntimeId, action, wait : false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Loading example action was rejected: " + name);
                render(screen);
                render(screen);
            }

            bool HasSource(string expectedLabel, string expectedState) => Nodes().Any(node => node.Role == UiAccessibilityRole.Edit
                && node.Name == GalleryText.Get("C# example source") && node.Value is { } code
                && code.Contains(expectedLabel, StringComparison.Ordinal) && code.Contains(expectedState, StringComparison.Ordinal)
                && !code.Contains("GalleryText.", StringComparison.Ordinal)
                && (language == GalleryLanguage.Korean || !ContainsHangul(code)));
        }
    }

    private static void VerifyNoticeExamples(Action<UiScreen> render, UiAccessibilityTree tree, ObserverServices services,
        Action<bool, string> check)
    {
        foreach (var (language, showToast, toastTitle, dismiss, showHud, hudTitle, addMessage, error) in new[]
        {
            (GalleryLanguage.English, "Show toast with an action", "Confirmation required", "Dismiss toast",
                "Show success", "Save complete", "Add message", "Error"),
            (GalleryLanguage.Korean, "동작 있는 알림 표시", "확인할 알림이 있습니다", "알림 닫기",
                "성공 표시", "저장 완료", "메시지 추가", "오류"),
            (GalleryLanguage.Japanese, "操作付きの通知を表示", "確認が必要です", "通知を閉じる",
                "成功を表示", "保存完了", "メッセージを追加", "エラー"),
            (GalleryLanguage.SimplifiedChinese, "显示带操作的通知", "需要确认", "关闭通知",
                "显示成功", "保存完成", "添加消息", "错误")
        })
        {
            using var locale = GalleryText.Use(language);
            using var screen = new ObserverScreen(services, language : language);
            RenderPage("Toast");
            Invoke(GalleryText.Get("Toggle reduced motion for toasts"));
            Invoke(showToast);
            var notice = Nodes().Single(node => node.Role == UiAccessibilityRole.Group && node.Name == toastTitle);
            check(HasSource(GallerySample.String(toastTitle), "Fluent.Toaster")
                && HasSource(GallerySample.String(dismiss), "CloseLabel"),
                "Toast copies the localized live title and dismiss button in " + language);
            Invoke(dismiss, notice.Id);
            check(!Nodes().Any(node => node.Role == UiAccessibilityRole.Group && node.Name == toastTitle),
                "The localized toast dismiss button closes the actual toast in " + language);

            RenderPage("HeadsUpDisplay");
            Invoke(showHud);
            check(Nodes().Any(node => node.Name == hudTitle) && HasSource(GallerySample.String(hudTitle), "UiHeadsUpDisplayKind.Success"),
                "HeadsUpDisplay shows the localized success status and copies the same label in " + language);

            RenderPage("메시지 표시");
            Invoke(addMessage);
            check(HasSource(GallerySample.String(error), "UiMessageBarIntent.Error")
                && HasSource(GallerySample.String("message-1"), GallerySample.String(addMessage)),
                "MessageBar copies localized intent titles and the inserted stable key in " + language);

            UiAccessibilityNode[] Nodes() => tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
            void RenderPage(string page)
            {
                screen.SelectedPage = page;
                render(screen);
                render(screen);
            }

            void Invoke(string name, string? parent = null)
            {
                UiAccessibilityElement Find() => tree.CaptureSnapshot().Elements.Single(element =>
                    element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button
                    && (parent is null || HasAncestor(element.Node, parent)));
                bool HasAncestor(UiAccessibilityNode node, string ancestor)
                {
                    while (node.ParentId.Length != 0 && tree.TryGet(node.ParentId, out var owner))
                    {
                        if (owner.Node.Id == ancestor) return true;
                        node = owner.Node;
                    }
                    return false;
                }

                var target = Find();
                if (target.Node.Offscreen)
                {
                    if (tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) != UiAccessibilityResult.Success)
                        throw new InvalidOperationException("Notice example cannot be revealed: " + name);
                    render(screen);
                    render(screen);
                    target = Find();
                }
                if (tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Notice example action was rejected: " + name);
                render(screen);
                render(screen);
            }

            bool HasSource(string expectedLabel, string expectedState) => Nodes().Any(node => node.Role == UiAccessibilityRole.Edit
                && node.Name == GalleryText.Get("C# example source") && node.Value is { } code
                && code.Contains(expectedLabel, StringComparison.Ordinal) && code.Contains(expectedState, StringComparison.Ordinal)
                && !code.Contains("GalleryText.", StringComparison.Ordinal)
                && (language == GalleryLanguage.Korean || !ContainsHangul(code)));
        }
    }

    private static bool ContainsHangul(string text) => text.Any(character => character is >= '\uAC00' and <= '\uD7A3');

    internal static void AuditPages(string output)
    {
        Directory.CreateDirectory(output);
        using var report = File.CreateText(Path.Combine(output, "pages.jsonl"));
        var json = new GraphicsJson(new System.Text.Json.JsonSerializerOptions());
        var services = ObserverServices.CreateWindows();
        var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var language in Enum.GetValues<GalleryLanguage>())
        {
            using var locale = GalleryText.Use(language);
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            var fonts = GalleryText.ConfigureFonts(new());
            context.SetDirectTextSecondaryFontPath(fonts.SecondaryFontPath);
            context.SetTypographyFonts(fonts.Typography);
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            using var screen = new ObserverScreen(services, language : language) { FreezeDiagnosticText = true };
            foreach (var page in screen.PageTitles)
            {
                screen.SelectedPage = page;
                context.NewFrame(new(1f / 60, new(1280, 900), new(1, 1)), input, new(0, 0, 1280, 900), UiTextSettings.Default);
                context.Render(screen);
                using var draw = context.GetDrawData();
                var snapshot = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
                var pane = snapshot.SingleOrDefault(node => node.Role == UiAccessibilityRole.Pane
                    && node.Name == screen.SelectedNavigationLabel && !node.Offscreen);
                var nodesById = snapshot.ToDictionary(node => node.Id, StringComparer.Ordinal);
                bool IsInPage(UiAccessibilityNode node)
                {
                    if (string.IsNullOrEmpty(pane.Id)) return false;
                    while (!string.IsNullOrEmpty(node.ParentId) && nodesById.TryGetValue(node.ParentId, out node))
                        if (node.Id == pane.Id) return true;
                    return false;
                }

                var nodes = snapshot.Where(IsInPage).ToArray();
                var visibleStrings = nodes.Where(node => !node.Offscreen)
                    .SelectMany(node => new[] { node.Name, node.GetHelpText(), node.Role == UiAccessibilityRole.Edit ? null : node.Value })
                    .Where(text => !string.IsNullOrEmpty(text) && ContainsHangul(text))
                    .Distinct(StringComparer.Ordinal).ToArray();
                var snippets = nodes.Where(node => node.Role == UiAccessibilityRole.Edit && node.Name == GalleryText.Get("C# example source"))
                    .Select(node => node.Value).Where(text => !string.IsNullOrEmpty(text)).ToArray();
                var sourceCandidates = snippets.SelectMany(text => text!.Split('\n'))
                    .Where(ContainsHangul).Select(line => line.Trim()).Distinct(StringComparer.Ordinal).ToArray();
                var result = new GalleryLocalizationAuditPage(GalleryText.Tag(language), page, screen.SelectedNavigationLabel,
                    !string.IsNullOrEmpty(pane.Id), screen.SourceCountForPage(page), snippets.Length, visibleStrings, sourceCandidates);
                report.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, json.GalleryLocalizationAuditPage));
            }
            report.Flush();
        }
    }

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

internal sealed record GalleryLocalizationAuditPage(string Language, string Page, string Label, bool PaneFound,
    int ExpectedSources, int ObservedSources, string?[] VisibleHangul, string[] SourceHangul);
