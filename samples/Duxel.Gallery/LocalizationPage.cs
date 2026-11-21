using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class LocalizationPage : IObserverPage, IObserverNaturalPage
{
    private readonly UiWidget _root;
    public string Title => "Localization";
    public string Description => "언어를 바꿔 같은 메시지에 번역과 숫자 서식이 적용되는지 확인하세요.";
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal LocalizationPage()
    {
        const string message = "Files selected: {0:N0}";
        var localized = new UiLocalization(
        [
            ("ko", message, "선택한 파일: {0:N0}개"),
            ("ja", message, "選択したファイル: {0:N0} 件"),
            ("zh-Hans", message, "已选文件：{0:N0} 个")
        ]);
        var language = new UiState<string?>("en");
        _root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Dropdown(GalleryText.Get("예제 언어"), language.Binding, GalleryText.LanguageOptions,
                new(Width : 240, ExpandLabel : GalleryText.Get("언어 옵션 열기"))),
            Fluent.Body1(() => localized.Format(message, CultureInfo.GetCultureInfo(language.Value!), 1234)));
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();
}
