using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TextFieldPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "TextField";
    public string Description => "짧은 텍스트를 입력하고, 지우기 버튼과 오류 표시를 확인합니다.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min),
            Example(GalleryText.Get("이름"), GalleryText.Get("디모이"), GalleryText.Get("앱에 표시할 이름을 입력하세요."), false),
            Example(GalleryText.Get("이메일"), "example", GalleryText.Get("예: name@example.com"), true));

        private UiWidget Example(string label, string initial, string hint, bool validate)
        {
            var value = new UiState<string>(initial);
            var sample = GallerySample.Create(() => Fluent.TextField(label, value.Binding,
                inputOptions: new(Placeholder: label, ClearLabel: GalleryText.Format("{0} 지우기", label)),
                field: new() { Hint = hint, Required = validate }));
            var input = sample.Preview;
            sample.BindSource(() => (value.Value, input.InputOptions, input.Options), state =>
                $"var text = new UiState<string>({GallerySample.String(state.Value)});\n"
                + $"using var field = Fluent.TextField({GallerySample.String(label)}, text.Binding,\n"
                + $"    inputOptions: new(Placeholder: {GallerySample.String(state.InputOptions.Placeholder)}, ReadOnly: {GallerySample.Boolean(state.InputOptions.ReadOnly)},\n"
                + $"        RightToLeft: {GallerySample.Boolean(state.InputOptions.RightToLeft)}, ClearLabel: {GallerySample.String(state.InputOptions.ClearLabel)}),\n"
                + $"    field: new() {{ Hint = {GallerySample.String(hint)}, Required = {GallerySample.Boolean(validate)},\n"
                + $"        ValidationMessage = {GallerySample.String(state.Options.ValidationMessage)} }});\nfield.Render(ui);");
            Samples.Add(sample);
            return Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Subtitle2(validate ? GalleryText.Get("입력 후 검증") : GalleryText.Get("짧은 텍스트")),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Format("{0} 읽기 전용 전환", label), () => input.InputOptions = input.InputOptions with { ReadOnly = !input.InputOptions.ReadOnly }),
                    Fluent.Button(GalleryText.Format("{0} 방향 전환", label), () => input.InputOptions = input.InputOptions with { RightToLeft = !input.InputOptions.RightToLeft }),
                    validate ? Fluent.Button(GalleryText.Get("이메일 확인"), () =>
                    {
                        var at = value.Value.IndexOf('@');
                        input.Options = input.Options with
                        {
                            ValidationMessage = at > 0 && at < value.Value.Length - 1 ? null : GalleryText.Get("@ 앞뒤에 주소를 입력하세요.")
                        };
                    }) : Fluent.Button(GalleryText.Get("이름 복원"), () => value.Value = initial)),
                sample);
        }
    }
}
