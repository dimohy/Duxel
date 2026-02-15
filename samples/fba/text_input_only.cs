// FBA: 텍스트 입력 전용 샘플 — 단일/멀티라인 입력, 힌트, 길이 제한, 상태 표시
#:property TargetFramework=net10.0
#:package Duxel.Windows.App@*-*

using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Text Input Only",
        Width = 980,
        Height = 700
    },
    Screen = new TextInputOnlyScreen()
});

public sealed class TextInputOnlyScreen : UiScreen
{
    private string _single = "안녕하세요";
    private string _withHint = string.Empty;
    private string _multi = "여러 줄 입력 테스트\n한글 IME 조합 테스트";
    private string _limited = string.Empty;

    public override void Render(UiImmediateContext ui)
    {
        ui.SetNextWindowSize(new UiVector2(900f, 620f));
        ui.BeginWindow("Text Input Only");

        ui.Text("한글/영문 입력 전용 샘플");
        ui.SeparatorText("Single Line");
        ui.InputText("기본 입력", ref _single, 256);

        ui.SeparatorText("Hint");
        ui.InputTextWithHint("힌트 입력", "여기에 입력하세요", ref _withHint, 256);

        ui.SeparatorText("Length Limit (16)");
        ui.InputText("길이 제한", ref _limited, 16);
        ui.Text($"현재 길이: {_limited.Length}/16");

        ui.SeparatorText("Multiline");
        ui.InputTextMultiline("멀티라인", ref _multi, 4096, 280f);

        ui.Separator();
        ui.Text("현재 값 미리보기");
        ui.Text($"Single: {_single}");
        ui.Text($"Hint: {_withHint}");

        ui.EndWindow();
    }
}
