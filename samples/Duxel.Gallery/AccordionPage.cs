using System.Collections.Immutable;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class AccordionPage : UiWidget, IGallerySourcePage
{
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;
    private readonly UiState<ImmutableArray<string>> _open = new(ImmutableArray<string>.Empty);
    private readonly UiState<string> _memo = new("접었다 펼쳐도 이 입력은 유지됩니다.");
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        _samples.Clear();
        var status = Fluent.Body1("열린 항목 : 없음");
        var clicks = 0;
        var action = Fluent.Button("본문 클릭 : 0");
        action.Activated = () => action.Label = $"본문 클릭 : {++clicks}";
        var options = new UiAccordionOptions(Collapsible : true);
        var sample = GallerySample.Create(() => Fluent.Accordion("접기·펼치기 예제",
            new(() => _open.Value, next =>
            {
                _open.Value = next;
                status.Source = next.IsEmpty ? "열린 항목 : 없음" : "열린 항목 : " + string.Join(", ", next);
            }), new UiAccordionItem[]
            {
                new("입력", "입력 내용 보존", Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Body1("본문을 접었다 펴거나 다른 페이지로 이동했다 돌아와도 입력한 값이 유지되는지 확인하세요."),
                    Fluent.Input("접기 예제 메모", _memo.Binding, 256, 460))),
                new("버튼", "본문 버튼과 키보드 이동", Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Body1("Tab으로 헤더와 본문, 다음 헤더로 차례로 이동하세요. Enter나 Space로 열고 닫을 수 있습니다."), action)),
                new("비활성", "비활성 헤더", Fluent.Body1("비활성 헤더는 포인터·키보드·접근성 요청으로 열리지 않습니다."), false)
            }, 640, options));
        var accordion = sample.Preview;
        var factory = sample.Source;
        sample.BindSource(() => (accordion.Options, Open : _open.Value, Memo : _memo.Value, Clicks : clicks), state =>
        {
            var opened = state.Open.IsEmpty ? "ImmutableArray<string>.Empty"
                : "ImmutableArray.Create<string>(" + string.Join(", ", state.Open.Select(GallerySample.String)) + ")";
            var openStatus = state.Open.IsEmpty ? "열린 항목 : 없음" : "열린 항목 : " + string.Join(", ", state.Open);
            return "using System.Collections.Immutable;\n\n"
                + $"var _open = new UiState<ImmutableArray<string>>({opened});\n"
                + $"var _memo = new UiState<string>({GallerySample.String(state.Memo)});\n"
                + $"var status = Fluent.Body1({GallerySample.String(openStatus)});\n"
                + $"var clicks = {state.Clicks};\n"
                + "var action = Fluent.Button($\"본문 클릭 : {clicks}\");\n"
                + "action.Activated = () => action.Label = $\"본문 클릭 : {++clicks}\";\n"
                + $"var options = new UiAccordionOptions(Multiple : {GallerySample.Boolean(state.Options.Multiple)},\n"
                + $"    Collapsible : {GallerySample.Boolean(state.Options.Collapsible)}, Size : UiAccordionSize.{state.Options.Size},\n"
                + $"    ChevronPosition : UiAccordionChevronPosition.{state.Options.ChevronPosition});\n\n"
                + factory;
        });
        _samples.Add(sample);
        var multiple = Fluent.Button("여러 항목 열기 : 꺼짐");
        multiple.Activated = () =>
        {
            var enabled = !accordion.Options.Multiple;
            if (!enabled && _open.Value.Length > 1)
            {
                _open.Value = ImmutableArray.Create(_open.Value[0]);
                status.Source = "열린 항목 : " + _open.Value[0];
            }
            accordion.Options = accordion.Options with { Multiple = enabled };
            multiple.Label = "여러 항목 열기 : " + (enabled ? "켜짐" : "꺼짐");
        };
        var collapsible = Fluent.Button("모두 접기 : 허용");
        collapsible.Activated = () =>
        {
            var enabled = !accordion.Options.Collapsible;
            accordion.Options = accordion.Options with { Collapsible = enabled };
            collapsible.Label = "모두 접기 : " + (enabled ? "허용" : "금지");
        };
        var chevron = Fluent.Button("화살표 : 앞");
        chevron.Activated = () =>
        {
            var end = accordion.Options.ChevronPosition == UiAccordionChevronPosition.Start;
            accordion.Options = accordion.Options with { ChevronPosition = end ? UiAccordionChevronPosition.End : UiAccordionChevronPosition.Start };
            chevron.Label = "화살표 : " + (end ? "뒤" : "앞");
        };
        var size = Fluent.Button("헤더 크기 : Medium");
        size.Activated = () =>
        {
            var next = (UiAccordionSize)(((int)accordion.Options.Size + 1) % 4);
            accordion.Options = accordion.Options with { Size = next };
            size.Label = "헤더 크기 : " + next;
        };
        return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("처음에는 닫혀 있습니다. 헤더를 눌러 본문을 열고 입력해 보세요. 헤더와 본문을 연결한 접근성 정보도 확인할 수 있습니다."),
            Dux.Row(new(Spacing : 8), multiple, collapsible, chevron, size), status, sample);
    });
}
