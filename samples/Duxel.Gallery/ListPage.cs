using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ListPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private UiWidget? _root;
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples
    {
        get
        {
            _root ??= Create();
            return _samples;
        }
    }
    public string Title => "목록";
    public string Description => "Try a plain list, single and multiple selection, and inputs and buttons inside rows.";
    UiWidget IObserverNaturalPage.MeasureRoot => _root ??= Create();
    public void Render(UiImmediateContext ui) => (_root ??= Create()).Render(ui);
    public void Dispose() => _root?.Dispose();
    private UiWidget Create()
    {
        var selected = new UiState<string?>("seoul");
        var members = new UiState<string[]>(["seil"]);
        var note = new UiState<string>("회의 준비");
        var keep = new UiState<bool>(false);
        var commands = new UiState<int>(0);
        var plain = GallerySample.Create(() => Fluent.List("일반 안내",
        [
            new("first", "첫 번째 안내", Fluent.Body1("내용을 자유롭게 배치하는 일반 목록입니다.")),
            new("second", "두 번째 안내", Fluent.Button("목록 안 독립 버튼", () => commands.Value++))
        ], new(Width : 520, Spacing : 8)));
        var plainFactory = plain.Source;
        plain.BindSource(() => commands.Value, value => $"var commands = new UiState<int>({value});\n\n" + plainFactory);
        var single = GallerySample.Create(() => Fluent.List("방문 도시", selected.Binding,
        [
            new("seoul", "서울", Fluent.Body1("서울"), selectionLabel : "서울 선택"),
            new("busan", "부산", Fluent.Body1("부산"), selectionLabel : "부산 선택"),
            new("jeju", "제주", Fluent.Body1("제주"), selectionLabel : "제주 선택")
        ], new(Width : 520)));
        var singleFactory = single.Source;
        single.BindSource(() => selected.Value, value => $"var selected = new UiState<string?>({GallerySample.String(value)});\n\n" + singleFactory);
        var composite = GallerySample.Create(() =>
        {
            void Act(UiListItemAction action)
            {
                commands.Value++;
                action.PreventSelection = keep.Value;
            }
            return Fluent.List("함께 작업하는 팀", members.Binding,
            [
                new("seil", "디모이", Dux.Row(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                    Fluent.Body1("디모이"), Fluent.Input("디모이 메모", note.Binding, 100, 180),
                    Fluent.Button("디모이에게 실행", () => commands.Value++)), Act, selectionLabel : "디모이 선택"),
                new("minsu", "김민수", Dux.Row(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                    Fluent.Body1("김민수"), Fluent.Button("김민수에게 실행", () => commands.Value++)), Act, selectionLabel : "김민수 선택"),
                new("guest", "선택할 수 없는 손님", Fluent.Body1("손님 : 행 실행만 가능"), Act,
                    disabledSelection : true, selectionLabel : "손님 선택")
            ], new(Navigation : UiListNavigation.Composite, Width : 520, Spacing : 4));
        });
        var compositeFactory = composite.Source;
        composite.BindSource(() => (Members : members.Value, Memo : note.Value, Keep : keep.Value, Commands : commands.Value), state =>
            $"var members = new UiState<string[]>([{string.Join(", ", state.Members.Select(GallerySample.String))}]);\n"
            + $"var note = new UiState<string>({GallerySample.String(state.Memo)});\n"
            + $"var keep = new UiState<bool>({GallerySample.Boolean(state.Keep)});\n"
            + $"var commands = new UiState<int>({state.Commands});\n\n" + compositeFactory);
        _samples.AddRange([plain, single, composite]);
        return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            plain, Fluent.Subtitle1("단일 선택"), single,
            Fluent.Body1(() => "방문 도시 : " + (selected.Value ?? "선택 없음")),
            Fluent.Subtitle1("복합 목록·다중 선택"), composite,
            Fluent.Checkbox("행 실행 시 선택 유지", keep.Binding),
            Fluent.Body1(() => $"선택 : {string.Join(", ", members.Value)} / 실행 : {commands.Value} / 메모 : {note.Value}"),
            Fluent.Caption1("↑·↓로 행을 이동합니다. 복합 목록에서는 →로 행 안의 위젯에 들어가고 ←나 Esc로 행으로 돌아올 수 있습니다."),
            Fluent.Caption1("Space로 선택하거나 선택을 해제하세요. Enter를 누르거나 행을 클릭하면 행의 동작을 실행한 뒤 선택합니다. 행 안의 체크박스나 버튼을 눌러도 행 자체의 동작은 실행되지 않습니다."));
    }
}
