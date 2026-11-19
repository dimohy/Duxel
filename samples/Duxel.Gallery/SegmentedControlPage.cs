using Duxel.Core;

namespace Duxel.Gallery;

[UiFluentIcon("calendar", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct SegmentCalendarIcon;
[UiFluentIcon("mail", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct SegmentMailIcon;

internal sealed class SegmentedControlPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "SegmentedControl";
    public string Description => "여러 항목 중 하나를 선택합니다. 네 가지 스타일을 비교할 수 있으며, 드래그와 휠 스크롤을 확인할 수 있습니다. 숨은 항목이 있는 쪽에만 페이드가 표시됩니다.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    internal UiSegmentedControlWidget Preview(int index) => (_content ??= new()).Previews[index];
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal List<UiSegmentedControlWidget> Previews { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing : 20, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("기본 선택·키보드·RTL·아이콘·읽지 않음 표시와 네 가지 스타일을 확인하세요. 가로 스크롤에서는 캐시한 콘텐츠를 재사용하며 숨은 쪽만 옅어집니다."),
            Example("중립 탐색 모음", [new("day", "일"), new("week", "주", false), new("month", "월", Unread : true)], false,
                UiSegmentedControlStyle.NeutralOverNavBarPill),
            Example("중립 본문", [new("all", "전체"), new("assigned", "나에게 할당된 항목"), new("saved", "저장한 항목")], false,
                UiSegmentedControlStyle.NeutralOverCanvasPill),
            Example("브랜드 탐색 모음", [new("calendar", "달력", Icon : UiMenuIcon.Create<SegmentCalendarIcon, SegmentCalendarIcon>()),
                new("mail", "메일", Icon : UiMenuIcon.Create<SegmentMailIcon, SegmentMailIcon>(), Unread : true)], true,
                UiSegmentedControlStyle.BrandOverNavBarPill),
            Example("브랜드 본문", [new("all", "전체"), new("active", "진행 중"), new("done", "완료", Unread : true)], false,
                UiSegmentedControlStyle.BrandOverCanvasPill),
            Example("가로 스크롤", Enumerable.Range(1, 10).Select(i => new UiSegmentOption(i.ToString(), "선택 항목 " + i)).ToArray(), false,
                UiSegmentedControlStyle.NeutralOverCanvasPill, scrolling : true));
        private UiWidget Example(string label, UiSegmentOption[] items, bool icons, UiSegmentedControlStyle style, bool scrolling = false)
        {
            var state = new UiState<string?>(items[0].Value);
            var sample = GallerySample.Create(() => Fluent.SegmentedControl(label, state.Binding, items,
                new(Style : style, UnreadLabel : "읽지 않음", IsFixedWidth : !scrolling)));
            var preview = sample.Preview;
            Previews.Add(preview);
            sample.BindSource(() => (preview.Options, state.Value, preview.ContentVersion), value =>
            {
                var rows = string.Join(",\n    ", preview.Items.ToArray().Select(item => "new(" + GallerySample.String(item.Value) + ", "
                    + GallerySample.String(item.Label) + ", Enabled : " + GallerySample.Boolean(item.Enabled)
                    + (item.Icon.IsEmpty ? "" : ", Icon : UiMenuIcon.Create<Segment" + (item.Value == "calendar" ? "Calendar" : "Mail")
                        + "Icon, Segment" + (item.Value == "calendar" ? "Calendar" : "Mail") + "Icon>()")
                    + ", Unread : " + GallerySample.Boolean(item.Unread) + ")"));
                return (icons ? "// Icons are generated from [UiFluentIcon] declarations in this sample.\n" : "")
                    + "var value = new UiState<string?>(" + GallerySample.String(value.Value) + ");\n"
                    + "UiSegmentOption[] items = [\n    " + rows + "\n];\n"
                    + "var control = Fluent.SegmentedControl(" + GallerySample.String(label) + ", value.Binding, items,\n"
                    + "    new(Width : " + GallerySample.Number(value.Options.Width) + ", EqualWidth : " + GallerySample.Boolean(value.Options.EqualWidth)
                    + ", RightToLeft : " + GallerySample.Boolean(value.Options.RightToLeft) + ", ReducedMotion : " + GallerySample.Boolean(value.Options.ReducedMotion)
                    + ",\n        Style : UiSegmentedControlStyle." + value.Options.Style + ", HorizontalInset : " + GallerySample.Number(value.Options.HorizontalInset)
                    + ", UnreadLabel : " + GallerySample.String(value.Options.UnreadLabel)
                    + ", IsFixedWidth : " + GallerySample.Boolean(value.Options.IsFixedWidth) + "));"
                    + (icons ? "\n\n[UiFluentIcon(\"calendar\", UiFluentIconStyle.Regular, 20)]\ninternal readonly partial struct SegmentCalendarIcon;\n"
                        + "[UiFluentIcon(\"mail\", UiFluentIconStyle.Regular, 20)]\ninternal readonly partial struct SegmentMailIcon;" : "");
            });
            Samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(label),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(label + " 너비 전환", () => preview.Options = preview.Options with { Width = preview.Options.Width == 320 ? 480 : 320 }),
                    Fluent.Button(label + " 균등 너비 전환", () => preview.Options = preview.Options with { EqualWidth = !preview.Options.EqualWidth }),
                    Fluent.Button(label + " 방향 전환", () => preview.Options = preview.Options with { RightToLeft = !preview.Options.RightToLeft }),
                    Fluent.Button(label + " 스타일 전환", () => preview.Options = preview.Options with
                    { Style = (UiSegmentedControlStyle)(((int)preview.Options.Style + 1) % 4) }),
                    Fluent.Button(label + " 모션 줄이기 전환", () => preview.Options = preview.Options with { ReducedMotion = !preview.Options.ReducedMotion })),
                Fluent.Body1(() => "선택 : " + state.Value), sample);
        }
    }
}
