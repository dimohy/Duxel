using Duxel.Core;

internal sealed class NativeAnnouncementScene : UiScreen, IDisposable
{
    internal const string PoliteMessage = "한글 공지: 연결되었습니다 🙂";
    internal const string AssertiveMessage = "중요 공지: 연결이 끊겼습니다";
    internal const string RepeatMessage = "같은 공지를 다시 전달합니다";
    private readonly UiWidget _content;
    private readonly Queue<UiAccessibilityAnnouncement> _pending = new();
    private int _quietFrames = -1;
    internal bool Finished
    {
        get; private set;
    }
    internal int Queued
    {
        get; private set;
    }

    internal NativeAnnouncementScene()
    {
        _content = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Button("Send polite announcement", () => _pending.Enqueue(new(PoliteMessage, "owned-polite"))),
            Fluent.Button("Send assertive announcement", () => _pending.Enqueue(new(AssertiveMessage, "owned-assertive", UiAnnouncementPriority.Assertive))),
            Fluent.Button("Repeat announcement", () =>
            {
                _pending.Enqueue(new(RepeatMessage, "owned-repeat"));
                _pending.Enqueue(new(RepeatMessage, "owned-repeat"));
            }),
            Fluent.Button("No announcement frame", () => _quietFrames = 0),
            Fluent.Button("Finish announcements", () => Finished = true),
            Fluent.Body1(() => _quietFrames >= 8 ? "Quiet frames : ready" : "Quiet frames : waiting"));
    }

    public override void Render(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding: 8);
        _content.Render(ui);
        while (_pending.TryDequeue(out var item))
        {
            if (!ui.Announce(item.Message, item.ActivityId, item.Priority))
                throw new InvalidOperationException("The owned announcement fixture requires an accessibility tree.");
            Queued++;
        }
        if (_quietFrames >= 0)
            _quietFrames++;
    }

    public void Dispose()
    {
        _content.Dispose();
        _pending.Clear();
    }
}
