using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class EditingPage : UiWidget, IGallerySourcePage
{
    private readonly UiState<string> _name = new("");
    private readonly UiState<string> _notes = new("한글 입력과 English, 선택·복사·붙여넣기·되돌리기를 확인하세요.\n탭을 바꿔도 입력 내용은 유지됩니다.");
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }

    internal EditingPage()
    {
        var nameSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            new ValidatedNameExample(_name),
            Fluent.Button("한 줄 커서 예제", () => _name.Value = "abc אבגד 123 · AV e\u0301 · 한글"),
            Fluent.Body1(() => $"이름 : {_name.Value}")));
        var nameFactory = nameSample.Source;
        nameSample.BindSource(() => _name.Value, value => $"var _name = new UiState<string>({GallerySample.String(value)});\n\n"
            + nameFactory + "\n\n" + GalleryPageSource.Declaration(nameof(ValidatedNameExample)));
        var editorDefinition = GallerySample.Define("editor", () => Fluent.Textarea("메모", _notes.Binding, 16384, 620, 220));
        var editor = editorDefinition.Value;
        var labelDefinition = GallerySample.Define("memoLabel", () => Fluent.Label("메모", editor));
        var memoLabel = labelDefinition.Value;
        var memoSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Dux.SizedBox(memoLabel, width : 620), editor,
            Dux.Wrap(new(Spacing : 16, RunSpacing : 12, CrossAlignment : UiWrapCrossAlignment.Center),
                Fluent.Button("줄바꿈 전환", () => editor.Options = editor.Options with { Wrap = editor.Options.Wrap == false }),
                Fluent.Button("읽기 전용 전환", () => editor.Options = editor.Options with
                {
                    Flags = (editor.Options.Flags & UiInputTextFlags.ReadOnly) == 0 ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None
                }),
                Fluent.Button("메모 지우기", () => _notes.Value = string.Empty),
                Fluent.Button("커닝·혼합 문장 예제", () => _notes.Value = "AV office\nabc אב 123\n한글과 e\u0301\nA \u202Bאב 정 גד\u202C Z"),
                Fluent.Button("라벨 필수 전환", () => memoLabel.Options = memoLabel.Options with { Required = !memoLabel.Options.Required }),
                Fluent.Button("라벨 크기 전환", () => memoLabel.Options = memoLabel.Options with { Size = (UiLabelSize)(((int)memoLabel.Options.Size + 1) % 3) }),
                Fluent.Button("라벨 굵기 전환", () => memoLabel.Options = memoLabel.Options with { Semibold = !memoLabel.Options.Semibold }),
                Fluent.Button("라벨 비활성 전환", () => memoLabel.Options = memoLabel.Options with { Disabled = !memoLabel.Options.Disabled }),
                Fluent.Button("긴 라벨 전환", () => memoLabel.Text = memoLabel.Text == "메모"
                    ? "메모에 남길 내용을 입력하세요. 긴 라벨은 지정한 너비에서 다음 줄로 이어지며, 라벨을 누르면 아래 메모 입력창으로 포커스를 옮깁니다."
                    : "메모")),
            Fluent.Body1(() => $"메모 길이 : {_notes.Value.Length}"),
            Fluent.Caption1("Tab / Shift+Tab으로 이동하고 Ctrl+Z로 되돌리세요. 줄바꿈과 읽기 전용 버튼을 누르면 메모 편집 방식이 바뀝니다.")));
        var memoFactory = memoSample.Source;
        memoSample.BindSource(() => (_notes.Value, editor.Options, Label : memoLabel.Text, LabelOptions : memoLabel.Options), state =>
        {
            var wrap = state.Options.Wrap is { } value ? GallerySample.Boolean(value) : "null";
            var flags = string.Join(" | ", state.Options.Flags.ToString().Split(", ").Select(flag => "UiInputTextFlags." + flag));
            return $"var _notes = new UiState<string>({GallerySample.String(state.Value)});\n\n" + editorDefinition.Source
                + $"\neditor.Options = new UiMultilineInputOptions {{ Flags = {flags}, Wrap = {wrap},\n"
                + $"    Placeholder = {GallerySample.String(state.Options.Placeholder)}, Invalid = {GallerySample.Boolean(state.Options.Invalid)} }};\n\n"
                + labelDefinition.Source + $"\nmemoLabel.Text = {GallerySample.String(state.Label)};\n"
                + $"memoLabel.Options = new UiLabelOptions(Required : {GallerySample.Boolean(state.LabelOptions.Required)}, Disabled : {GallerySample.Boolean(state.LabelOptions.Disabled)},\n"
                + $"    Size : UiLabelSize.{state.LabelOptions.Size}, Semibold : {GallerySample.Boolean(state.LabelOptions.Semibold)});\n\n" + memoFactory;
        });
        Samples = [nameSample, memoSample];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Subtitle2("이름 입력과 검증"), nameSample,
            Fluent.Subtitle2("메모 편집과 연결 라벨"), memoSample);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
