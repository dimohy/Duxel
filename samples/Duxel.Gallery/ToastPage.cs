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
        private UiToastOptions _last = new(GalleryText.Format("Toast {0}", 1));
        private string _status = GalleryText.Get("No toasts are visible.");
        internal UiToastController Controller { get; }
        internal UiWidget Root { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal Content()
        {
            Controller = new(clock : _clock);
            var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Add timed toast"), () => Add(false, 0)),
                    Fluent.Button(GalleryText.Get("Add persistent toast"), () => Add(true, 0)),
                    Fluent.Button(GalleryText.Get("Add priority toast"), () => Add(false, 10))),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Finish entering"), () => Run(() => { foreach (var row in Controller.Visible) if (row.Status == UiToastStatus.Entering) Controller.MarkVisible(row.Id); })),
                    Fluent.Button(GalleryText.Get("Advance one second"), () => Run(() => { _clock.Step(); Controller.Advance(); })),
                    Fluent.Button(GalleryText.Get("Toggle pause"), () => Run(() => { _paused = !_paused; foreach (var row in Controller.Visible) Controller.SetPaused(row.Id, UiToastPauseReason.Explicit, _paused); })),
                    Fluent.Button(GalleryText.Get("Dismiss all"), () => Run(() => Controller.DismissAll())),
                    Fluent.Button(GalleryText.Get("Finish exiting"), () => Run(() => { foreach (var row in Controller.Visible) if (row.Status == UiToastStatus.Exiting) Controller.Remove(row.Id); }))),
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
                Live.Root, Composition.Root, Fluent.Subtitle2(GalleryText.Get("Lifetime and queue diagnostics")), Fluent.Body1(GalleryText.Get("Advance time manually in this preview. The buttons below complete the entry and exit transitions.")), sample);
        }
        private void Add(bool persistent, int priority) => Run(() =>
        {
            _last = new(GalleryText.Format("Toast {0}", ++_count)) { Timeout = persistent ? null : TimeSpan.FromSeconds(7), Priority = priority };
            var id = Controller.Dispatch(_last);
            if (_paused) Controller.SetPaused(id, UiToastPauseReason.Explicit, true);
        });
        private void Run(Action action)
        {
            action();
            string Status(UiToastSnapshot row) => row.Options.Title + " : " + (row.Status switch
            {
                UiToastStatus.Entering => GalleryText.Get("Entering"), UiToastStatus.Visible => GalleryText.Get("Visible"), UiToastStatus.Exiting => GalleryText.Get("Exiting"), _ => GalleryText.Get("Queued")
            }) + (row.PauseReasons != UiToastPauseReason.None ? " · " + GalleryText.Get("Paused") : "");
            _status = GalleryText.Format("Visible: {0} / Queued: {1} / Next timeout: {2}",
                Controller.Visible.Count, Controller.Queued.Count,
                Controller.NextTimeout is { } delay ? GalleryText.Format("In {0:0} s", delay.TotalSeconds) : GalleryText.Get("None"))
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
