using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class SelectPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "Select";
    public string Description => "텍스트 항목 하나를 선택합니다. 닫힌 상태에서도 방향키와 이름 입력으로 값을 바꿀 수 있습니다.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Example(GalleryText.Get("기본 선택"), GalleryText.Get("지역"), null, new(), false),
            Example(GalleryText.Get("크기와 모양"), GalleryText.Get("표시 방식"), "jp", new(Size : UiDropdownSize.Large, Appearance : UiInputAppearance.FilledLighter), false),
            Example(GalleryText.Get("입력 검증과 방향"), GalleryText.Get("검증할 지역"), "us", new(RightToLeft : true, Invalid : true), true));
        private UiWidget Example(string title, string label, string? initial, UiSelectOptions options, bool validation)
        {
            var value = new UiState<string?>(initial);
            UiSelectOption[] items = [new("kr", GalleryText.Get("대한민국")), new("us", GalleryText.Get("미국")), new("jp", GalleryText.Get("일본")), new("de", GalleryText.Get("독일"), false)];
            var sourceOptions = string.Join(", ", items.Select(item =>
                $"new({GallerySample.String(item.Value)}, {GallerySample.String(item.Text)}, {GallerySample.Boolean(item.Enabled)})"));
            options = options with { ExpandLabel = GalleryText.Get("목록 열기") };
            var sample = GallerySample.Create(() => Fluent.Select(label, value.Binding, items, options));
            var preview = sample.Preview;
            sample.BindSource(() => (value.Value, preview.Options), current =>
                "var value = new UiState<string?>(" + GallerySample.String(current.Value) + ");\n"
                + "using var select = Fluent.Select(" + GallerySample.String(label) + ", value.Binding,\n"
                + "    [" + sourceOptions + "],\n"
                + "    new(Width : " + GallerySample.Number(current.Options.Width) + ", Size : UiDropdownSize." + current.Options.Size
                + ",\n        Appearance : UiInputAppearance." + current.Options.Appearance + ", RightToLeft : " + GallerySample.Boolean(current.Options.RightToLeft)
                + ", Invalid : " + GallerySample.Boolean(current.Options.Invalid)
                + ", ExpandLabel : " + GallerySample.String(current.Options.ExpandLabel) + "));\nselect.Render(ui);");
            Samples.Add(sample);
            return Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(title),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Format("{0} 크기 전환", title), () => preview.Options = preview.Options with { Size = (UiDropdownSize)(((int)preview.Options.Size + 1) % 3) }),
                    Fluent.Button(GalleryText.Format("{0} 모양 전환", title), () => preview.Options = preview.Options with { Appearance = (UiInputAppearance)(((int)preview.Options.Appearance + 1) % 4) }),
                    Fluent.Button(GalleryText.Format("{0} 방향 전환", title), () => preview.Options = preview.Options with { RightToLeft = !preview.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Format("{0} 검증 전환", title), () => preview.Options = preview.Options with { Invalid = !preview.Options.Invalid })),
                Fluent.Body1(() => GalleryText.Format("선택한 값 : {0}", value.Value ?? GalleryText.Get("없음"))),
                Fluent.Caption1(validation ? GalleryText.Get("오류 표시는 선택을 막지 않습니다. 필요하면 Field로 설명과 검증 문구를 연결하세요.")
                    : GalleryText.Get("아이콘·설명·다중 선택은 Dropdown, 검색과 직접 입력은 Combobox 예제에서 확인하세요.")),
                    sample);
        }
    }
}
