using Duxel.Core;
using System.Globalization;

namespace Duxel.Gallery;

internal sealed class ToastPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "Toast";
    public string Description => "Try independent toasts and progress updates, automatic dismissal, pausing, and keyboard navigation.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    internal UiToastController Controller => (_content ??= new()).Controller;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    void IObserverNaturalPage.RenderOverlays(UiImmediateContext ui) => (_content ??= new()).Live.Surface.Render(ui);
    public void Render(UiImmediateContext ui)
    {
        var content = _content ??= new();
        content.Root.Render(ui);
        content.Live.Surface.Render(ui);
    }
    public void Dispose() { _content?.Live.Dispose(); _content?.Root.Dispose(); }
    private sealed class Content
    {
        internal readonly ToastLiveSample Live = new();
        internal readonly ToastCompositionSamples Composition = new();
        private readonly PreviewClock _clock = new();
        private int _count;
        private bool _paused;
        private UiToastOptions _last = new("알림 1");
        private string _status = "표시 중인 알림이 없습니다.";
        internal UiToastController Controller { get; }
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal Content()
        {
            Controller = new(clock : _clock);
            var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("시간제 알림 추가", () => Add(false, 0)),
                    Fluent.Button("계속 표시할 알림 추가", () => Add(true, 0)),
                    Fluent.Button("우선 알림 추가", () => Add(false, 10))),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("등장 완료", () => Run(() => { foreach (var row in Controller.Visible) if (row.Status == UiToastStatus.Entering) Controller.MarkVisible(row.Id); })),
                    Fluent.Button("1초 진행", () => Run(() => { _clock.Step(); Controller.Advance(); })),
                    Fluent.Button("일시 정지 전환", () => Run(() => { _paused = !_paused; foreach (var row in Controller.Visible) Controller.SetPaused(row.Id, UiToastPauseReason.Explicit, _paused); })),
                    Fluent.Button("모두 닫기", () => Run(() => Controller.DismissAll())),
                    Fluent.Button("퇴장 완료", () => Run(() => { foreach (var row in Controller.Visible) if (row.Status == UiToastStatus.Exiting) Controller.Remove(row.Id); }))),
                Fluent.Body1(() => _status)));
            sample.BindSource(() => _last, options =>
                "var clock = new PreviewClock();\nvar controller = new UiToastController(limit : 4, clock : clock);\n"
                + "var id = controller.Dispatch(new(" + GallerySample.String(options.Title) + ")\n{\n    Timeout = "
                + (options.Timeout is null ? "null" : "TimeSpan.FromSeconds(7)") + ",\n    Priority = " + options.Priority.ToString(CultureInfo.InvariantCulture)
                + "\n});\n// The host calls these after real entry/exit animations finish.\ncontroller.MarkVisible(id);\nclock.Step();\ncontroller.Advance();\n"
                + "controller.SetPaused(id, UiToastPauseReason.Explicit, true);\ncontroller.SetPaused(id, UiToastPauseReason.Explicit, false);\n"
                + "controller.Dismiss(id);\ncontroller.Remove(id);\n\nsealed class PreviewClock : TimeProvider\n{\n    private long _ticks;\n"
                + "    public override long TimestampFrequency => TimeSpan.TicksPerSecond;\n    public override long GetTimestamp() => _ticks;\n"
                + "    public void Step() => _ticks += TimeSpan.TicksPerSecond;\n}");
            Samples = [Live.Sample, .. Composition.Samples, sample];
            Root = Dux.Column(new(Spacing : 14, MainAxisSize : UiMainAxisSize.Min),
                Live.Root, Composition.Root, Fluent.Subtitle2("수명·대기열 진단"), Fluent.Body1("동작 검증용 미리보기입니다. 시간을 직접 진행하므로 대기 중에 계속 화면을 갱신하지 않습니다. 아래 버튼은 실제 등장·퇴장 완료 신호를 대신합니다."), sample);
        }
        private void Add(bool persistent, int priority) => Run(() =>
        {
            _last = new("알림 " + (++_count).ToString(CultureInfo.InvariantCulture)) { Timeout = persistent ? null : TimeSpan.FromSeconds(7), Priority = priority };
            var id = Controller.Dispatch(_last);
            if (_paused) Controller.SetPaused(id, UiToastPauseReason.Explicit, true);
        });
        private void Run(Action action)
        {
            action();
            string Status(UiToastSnapshot row) => row.Options.Title + " : " + (row.Status switch
            {
                UiToastStatus.Entering => "나타나는 중", UiToastStatus.Visible => "표시 중", UiToastStatus.Exiting => "닫히는 중", _ => "대기 중"
            }) + (row.PauseReasons != UiToastPauseReason.None ? " · 일시 정지" : "");
            _status = "표시 " + Controller.Visible.Count + " / 대기 " + Controller.Queued.Count + " / 다음 자동 닫힘 : "
                + (Controller.NextTimeout is { } delay ? delay.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + "초 후" : "없음")
                + "\n" + string.Join("\n", Controller.Visible.Concat(Controller.Queued).Select(Status));
        }
    }
    private sealed class PreviewClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Step() => _ticks += TimeSpan.TicksPerSecond;
    }
}
