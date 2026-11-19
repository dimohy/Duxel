using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ToastCompositionSamples
{
    internal List<IGallerySample> Samples { get; } = [];
    internal UiWidget Root { get; }
    internal ToastCompositionSamples() => Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
        Example("독립 알림 구성", new("알림 샘플", "본문과 닫기 버튼을 조작해 보세요")
            { CanDismiss = true, CloseLabel = "알림 닫기", DisableAnnouncement = true }, false),
        Example("진행 상태 구성", Progress(.35f), true));
    private static UiToastOptions Progress(float? value) => new("파일 전송 중", "완료 전까지 알림을 유지합니다")
        { Timeout = null, Progress = new(value), CanDismiss = true, CloseLabel = "알림 닫기", DisableAnnouncement = true };
    private UiWidget Example(string title, UiToastOptions options, bool progress)
    {
        var sample = GallerySample.Create(() => Fluent.Toast(options));
        var preview = sample.Preview;
        var buttons = new List<UiWidget>
        {
            Fluent.Button(title + " 다시 표시", preview.Show), Fluent.Button(title + " 숨기기", preview.Hide),
            Fluent.Button(title + " 방향 전환", () => preview.RightToLeft = !preview.RightToLeft),
            Fluent.Button(title + " 혼합 언어 전환", () => preview.Options = preview.Options with
            {
                Title = preview.Options.Title == options.Title ? "אבג English 한글 123" : options.Title,
                Body = preview.Options.Body == options.Body ? "English مرحبا אבג 한글" : options.Body
            }),
            Fluent.Button(title + " 너비 전환", () => preview.Width = preview.Width == 292 ? 420 : preview.Width == 420 ? 80 : 292)
        };
        if (progress)
        {
            buttons.Add(Fluent.Button("진행률 +10%", () => preview.Options = Progress(MathF.Min(1, (preview.Options.Progress?.Value ?? 0) + .1f))));
            buttons.Add(Fluent.Button("진행 방식 전환", () => preview.Options = Progress(preview.Options.Progress is { Value: null } ? .35f : null)));
            buttons.Add(Fluent.Button("진행 모션 일시 정지 전환", () => preview.AnimationPaused = !preview.AnimationPaused));
            buttons.Add(Fluent.Button("전송 완료 표시", () => preview.Options = new("전송했습니다", "파일을 확인할 수 있습니다", UiToastIntent.Success)
                { CanDismiss = true, CloseLabel = "알림 닫기", DisableAnnouncement = true }));
        }
        sample.BindSource(() => (preview.Options, preview.Width, preview.ReducedMotion, preview.AnimationPaused, preview.RightToLeft, preview.IsOpen), value =>
            "using var toast = Fluent.Toast(new(" + GallerySample.String(value.Options.Title) + ", " + GallerySample.String(value.Options.Body)
            + ", UiToastIntent." + value.Options.Intent + ")\n{\n    CanDismiss = " + GallerySample.Boolean(value.Options.CanDismiss)
            + ",\n    CloseLabel = " + GallerySample.String(value.Options.CloseLabel) + ",\n    DisableAnnouncement = " + GallerySample.Boolean(value.Options.DisableAnnouncement)
            + ",\n    Timeout = " + (value.Options.Timeout is null ? "null" : "TimeSpan.FromSeconds(7)")
            + (value.Options.Progress is { } state ? ",\n    Progress = new(" + (state.Value is { } number ? GallerySample.Number(number) : "null")
                + ", " + GallerySample.String(state.StatusText) + ")" : "")
            + "\n});\ntoast.Width = " + GallerySample.Number(value.Width) + ";\ntoast.ReducedMotion = " + GallerySample.Boolean(value.ReducedMotion)
            + ";\ntoast.AnimationPaused = " + GallerySample.Boolean(value.AnimationPaused) + ";\ntoast.RightToLeft = " + GallerySample.Boolean(value.RightToLeft)
            + ";" + (value.IsOpen ? "" : "\ntoast.Hide();"));
        Samples.Add(sample);
        return Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(title),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8), buttons.ToArray()), sample);
    }
}
