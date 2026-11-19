using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ToastLiveSample : IDisposable
{
    private readonly UiState<int> _actions = new(0);
    private readonly List<string> _history = [];
    private string _historyText = "최근 알림이 없습니다.";
    private UiToastOptions _last = new("저장했습니다", "문서가 준비되었습니다", UiToastIntent.Success);
    internal UiToastController Controller { get; } = new();
    internal UiToasterWidget Surface { get; }
    internal IGallerySample Sample { get; }
    internal UiWidget Root { get; }
    internal ToastLiveSample()
    {
        Surface = Fluent.Toaster(Controller);
        var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button("저장 알림 표시", () => Show(false)), Fluent.Button("동작 있는 알림 표시", () => Show(true)),
                Fluent.Button("진행 알림 표시", ShowProgress),
                Fluent.Button("혼합 언어 알림", () => Publish(new("אבג English 한글 123", "English مرحبا אבג 한글")
                    { Timeout = null, Progress = new(.25f, "התקדמות 25%"), CanDismiss = true, CloseLabel = "알림 닫기" })),
                Fluent.Button("알림 방향 전환", () => Surface.Options = Surface.Options with { RightToLeft = !Surface.Options.RightToLeft }),
                Fluent.Button("알림 너비 전환", () => Surface.Options = Surface.Options with
                    { Width = Surface.Options.Width == 292 ? 160 : Surface.Options.Width == 160 ? 80 : 292 }),
                Fluent.Button("긴 알림 표시", () => Publish(new("긴 알림", string.Join(" ", Enumerable.Repeat("긴 본문을 스크롤하고 아래 동작을 확인하세요.", 30)))
                    { Timeout = null, CanDismiss = true, CloseLabel = "알림 닫기", ActionLabel = "확인", Action = () => _actions.Value++ })),
                Fluent.Button("실제 알림 모두 닫기", () => Controller.DismissAll()),
                Fluent.Button("알림 위치 전환", () => Surface.Options = Surface.Options with
                    { Position = Surface.Options.Position == UiToastPosition.BottomEnd ? UiToastPosition.TopEnd : UiToastPosition.BottomEnd }),
                Fluent.Button("알림 모션 줄이기 전환", () => Surface.Options = Surface.Options with { ReducedMotion = !Surface.Options.ReducedMotion })),
            Fluent.Body1("알림은 화면 모서리에 나타납니다. 마우스를 올리거나 알림에 초점을 두면 자동 닫힘이 멈춥니다. F6으로 들어가거나 돌아오고 Delete·닫기 버튼으로 닫습니다."),
            Fluent.Body1(() => "알림에서 실행 : " + _actions.Value), Fluent.Body1(() => _historyText)));
        sample.BindSource(() => (Surface.Options, Toast : _last), value =>
            "var controller = new UiToastController();\nusing var toaster = Fluent.Toaster(controller, new(\n    Position : UiToastPosition."
            + value.Options.Position + ", Width : " + GallerySample.Number(value.Options.Width)
            + ", RightToLeft : " + GallerySample.Boolean(value.Options.RightToLeft)
            + ", ReducedMotion : " + GallerySample.Boolean(value.Options.ReducedMotion) + "));\n"
            + "var actions = new UiState<int>(0);\nvar history = new List<string>();\nhistory.Insert(0, " + GallerySample.String(value.Toast.Title) + ");\n"
            + "controller.Dispatch(new(" + GallerySample.String(value.Toast.Title) + ", " + GallerySample.String(value.Toast.Body)
            + ", UiToastIntent." + value.Toast.Intent + ")\n{\n    Timeout = " + (value.Toast.Timeout is null ? "null" : "TimeSpan.FromSeconds(7)")
            + (value.Toast.Progress is { } progress ? ",\n    Progress = new(" + (progress.Value is { } number ? GallerySample.Number(number) : "null")
                + ", " + GallerySample.String(progress.StatusText) + ")" : "")
            + ",\n    CanDismiss = " + GallerySample.Boolean(value.Toast.CanDismiss)
            + (value.Toast.CanDismiss ? ",\n    CloseLabel = \"알림 닫기\"" : "")
            + (value.Toast.Action is null ? "" : ",\n    ActionLabel = \"확인\",\n    Action = () => actions.Value++")
            + "\n});\n// Render once each frame after the page content; this portal reserves no layout space.\ntoaster.Render(ui);");
        Sample = sample;
        Root = Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2("실제 알림 표면"), sample);
    }
    private void ShowProgress() => Publish(new("파일 전송 중", "알림에서 진행 상태를 확인합니다")
        { Timeout = null, Progress = new(.35f, "35% 완료"), CanDismiss = true, CloseLabel = "알림 닫기" });
    private void Show(bool actionable)
    {
        Publish(actionable ? new("확인할 알림이 있습니다", "아래 확인 버튼을 눌러 보세요", UiToastIntent.Info)
        { Timeout = null, CanDismiss = true, CloseLabel = "알림 닫기", ActionLabel = "확인", Action = () => _actions.Value++ }
            : new("저장했습니다", "문서가 준비되었습니다", UiToastIntent.Success));
    }
    private void Publish(UiToastOptions options)
    {
        _last = options;
        _history.Insert(0, _last.Title);
        if (_history.Count > 4) _history.RemoveAt(4);
        _historyText = "최근 알림 : " + string.Join(" · ", _history);
        Controller.Dispatch(_last);
    }
    public void Dispose() { Surface.Dispose(); Root.Dispose(); }
}
