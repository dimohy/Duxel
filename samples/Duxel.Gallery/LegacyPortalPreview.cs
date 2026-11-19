using Duxel.Core;

namespace Duxel.Gallery;

/// <summary>Exercises the compatibility popup API using Fluent content under the shared widget transform.</summary>
internal sealed class LegacyPortalPreview : UiControlWidget
{
    private readonly UiWidget _trigger, _content;
    private bool _openRequested, _closeRequested;
    internal LegacyPortalPreview(Action executed)
    {
        var trigger = Fluent.Button("기존 팝업 API", () => _openRequested = true);
        trigger.Size = new(140, 32);
        _trigger = trigger;
        _content = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Body1("BeginPopup도 화면 좌표로 열립니다."),
            Fluent.Button("기존 팝업에서 실행", executed),
            Fluent.Button("기존 팝업 닫기", () => _closeRequested = true));
        OwnChild("trigger", _trigger);
        OwnChild("content", _content);
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => new(140, 32);
    protected override void RenderControl(UiImmediateContext ui)
    {
        _trigger.Render(ui);
        if (_openRequested)
        {
            _openRequested = false;
            ui.OpenPopup("compatibility-preview");
        }
        if (!ui.BeginPopup("compatibility-preview"))
            return;
        try
        {
            _content.Render(ui);
            if (_closeRequested)
            {
                _closeRequested = false;
                ui.CloseCurrentPopup();
            }
        }
        finally { ui.EndPopup(); }
    }
}
