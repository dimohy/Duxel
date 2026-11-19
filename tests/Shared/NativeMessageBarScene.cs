using Duxel.Core;

internal sealed class NativeMessageBarScene : UiScreen, IDisposable
{
    internal const string Title = "한글 상태";
    internal const string OriginalBody = "연결되었습니다 🙂";
    internal const string UpdatedBody = "내용이 변경되었습니다";
    internal const string OriginalMessage = Title + " " + OriginalBody + ",Retry message Dismiss message";
    internal const string UpdatedMessage = Title + " " + UpdatedBody + ",Retry message Dismiss message";
    private readonly UiFluentMessageBar _message;
    private readonly UiFluentMessageBarBody _body;
    private readonly UiFluentMessageBarGroup _group;
    private readonly UiWidget _content;
    private readonly UiState<string> _stage = new("hidden");
    private int _quietFrames = -1;
    private int _quietSerial;
    private UiAccessibilityTree? _tree;
    internal bool Finished
    {
        get; private set;
    }
    internal int Announcements
    {
        get; private set;
    }

    internal NativeMessageBarScene()
    {
        _body = Fluent.MessageBarBody(OriginalBody, Fluent.MessageBarTitle(Title));
        _message = Fluent.MessageBar("Native message bar", _body,
            Fluent.MessageBarActions([Fluent.Button("Retry message")]), Fluent.Button("Dismiss message"),
            new(Width: 500));
        _group = Fluent.MessageBarGroup([_message], new(ReducedMotion: true));
        _group.SetVisible(0, false);
        _content = new UiScrollView(Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Button("Mount message", () => { _group.SetVisible(0, true); _stage.Value = "mounted"; }),
            Fluent.Button("Make message urgent", () =>
            {
                _message.Options = _message.Options with
                {
                    Intent = UiMessageBarIntent.Error
                };
                _stage.Value = "urgent";
            }),
            Fluent.Button("Edit mounted message", () => { _body.Text = UpdatedBody; BeginQuiet(); }),
            Fluent.Button("Disable message announcement", () =>
            {
                _message.Options = _message.Options with
                {
                    DisableAnnouncement = true
                };
                _stage.Value = "disabled";
            }),
            Fluent.Button("Enable message announcement", () =>
            {
                _message.Options = _message.Options with
                {
                    DisableAnnouncement = false
                };
                _stage.Value = "enabled";
            }),
            Fluent.Button("Hide message", () => { _group.SetVisible(0, false); _stage.Value = "hidden"; }),
            Fluent.Button("Quiet message frames", BeginQuiet),
            Fluent.Button("Finish message bars", () => Finished = true),
            Fluent.Body1(() => "Message stage : " + _stage.Value),
            Fluent.Body1(() => _quietFrames >= 8 ? "Message quiet : ready : " + _quietSerial : "Message quiet : waiting"), _group));
    }

    public override void Render(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding: 8);
        if (!ReferenceEquals(_tree, ui.AccessibilityTree))
        {
            if (_tree is not null)
                _tree.Announcement -= Count;
            _tree = ui.AccessibilityTree;
            if (_tree is not null)
                _tree.Announcement += Count;
        }
        _content.Render(ui);
        if (_quietFrames >= 0)
            _quietFrames++;
    }
    private void BeginQuiet()
    {
        _quietFrames = 0;
        _quietSerial++;
    }
    private void Count(UiAccessibilityAnnouncement _) => Announcements++;
    public void Dispose()
    {
        if (_tree is not null)
            _tree.Announcement -= Count;
        _tree = null;
        _content.Dispose();
    }
}
