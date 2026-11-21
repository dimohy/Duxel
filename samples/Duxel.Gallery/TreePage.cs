using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TreePage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "Tree";
    public string Description => "Expand a folder, select its children together, or activate a button within a row.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min),
            Example("폴더와 파일 선택", false), Example("행 안의 버튼", true));

        private UiWidget Example(string title, bool rowActions)
        {
            var expanded = new UiState<string[]>(["folder"]);
            var selected = new UiState<string[]>(["notes"]);
            var opens = new UiState<int>(0);
            UiTreeItem[] items =
            [
                new("folder", null, "프로젝트"),
                new("notes", "folder", "디모이의 프로젝트를 시작하기 전에 읽어 볼 문서"),
                rowActions
                    ? new("report", "folder", "보고서", Dux.Row(new(Spacing: 8),
                        Fluent.Body1("보고서"), Fluent.Button("열기", () => opens.Value++)))
                    : new("report", "folder", "보고서")
            ];
            var sample = GallerySample.Create(() => Fluent.Tree(title, expanded.Binding, selected.Binding, items,
                new UiTreeOptions(Width: 320) { RowActions = rowActions }));
            var tree = sample.Preview;
            sample.BindSource(() => (Expanded: expanded.Value, Selected: selected.Value, Opens: rowActions ? opens.Value : 0, tree.Options), state =>
            {
                static string Strings(string[] values) => "[" + string.Join(", ", values.Select(GallerySample.String)) + "]";
                var options = state.Options;
                return $"var expanded = new UiState<string[]>({Strings(state.Expanded)});\n"
                    + $"var selected = new UiState<string[]>({Strings(state.Selected)});\n"
                    + (rowActions ? $"var opens = new UiState<int>({state.Opens});\n" : "")
                    + $"using var tree = Fluent.Tree({GallerySample.String(title)}, expanded.Binding, selected.Binding,\n"
                    + "    [new(\"folder\", null, \"프로젝트\"),\n"
                    + "     new(\"notes\", \"folder\", \"디모이의 프로젝트를 시작하기 전에 읽어 볼 문서\"),\n"
                    + (rowActions
                        ? "     new(\"report\", \"folder\", \"보고서\", Dux.Row(new(Spacing: 8),\n"
                            + "         Fluent.Body1(\"보고서\"), Fluent.Button(\"열기\", () => opens.Value++)))],\n"
                        : "     new(\"report\", \"folder\", \"보고서\")],\n")
                    + $"    new UiTreeOptions(Width: 320, RightToLeft: {GallerySample.Boolean(options.RightToLeft)}, Size: UiTreeSize.{options.Size})\n"
                    + $"    {{ RowActions = {GallerySample.Boolean(rowActions)}, TextOverflow = UiTreeTextOverflow.{options.TextOverflow},\n"
                    + $"      ReducedMotion = {GallerySample.Boolean(options.ReducedMotion)}, AlignSelectorToTop = {GallerySample.Boolean(options.AlignSelectorToTop)} }});\ntree.Render(ui);";
            });
            Samples.Add(sample);
            return Dux.Column(new(Spacing: 10, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Subtitle2(title),
                rowActions ? Fluent.Body1("보고서 행에서 F2를 누르면 열기 버튼으로 이동합니다. Escape를 누르면 행으로 돌아옵니다.")
                    : Fluent.Body1("프로젝트 옆 체크 표시를 누르면 하위 항목이 함께 선택됩니다."),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("크기 전환", () => tree.Options = tree.Options with
                    { Size = tree.Options.Size == UiTreeSize.Small ? UiTreeSize.Medium : UiTreeSize.Small }),
                    Fluent.Button("줄바꿈 전환", () => tree.Options = tree.Options with
                    { TextOverflow = tree.Options.TextOverflow == UiTreeTextOverflow.Wrap ? UiTreeTextOverflow.Ellipsis : UiTreeTextOverflow.Wrap }),
                    Fluent.Button("방향 전환", () => tree.Options = tree.Options with { RightToLeft = !tree.Options.RightToLeft }),
                    Fluent.Button("모션 전환", () => tree.Options = tree.Options with { ReducedMotion = !tree.Options.ReducedMotion }),
                    Fluent.Button("체크 표시 위쪽 정렬", () => tree.Options = tree.Options with { AlignSelectorToTop = !tree.Options.AlignSelectorToTop })),
                rowActions ? Fluent.Body1(() => $"보고서를 연 횟수 : {opens.Value}") : Fluent.Body1(() => $"선택한 파일 : {selected.Value.Length}개"),
                sample);
        }
    }
}
