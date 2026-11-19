namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private bool _nextWindowAutoHeight,_windowAutoHeight;
    internal bool HasWindowPlacement(string title)=>_state.TryGetWindowRect(title,out _);
    [UiComponent("Window",UiComponentExposure.Legacy),System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void SetNextWindowAutoHeight(bool enabled=true)=>_nextWindowAutoHeight=enabled;
    private void FitAutomaticWindowHeight()
    {
        if(!_windowAutoHeight||!_hasWindowRect||_windowCollapsed||_currentWindowId is null)return;
        var viewport=GetMainViewport();
        var content=MathF.Max(WindowMinHeight,_windowContentMax.Y+_windowScrollY-_windowRect.Y+WindowPadding);
        var available=MathF.Max(WindowMinHeight,viewport.WorkPos.Y+viewport.WorkSize.Y-_windowRect.Y-WindowPadding);
        var height=MathF.Min(content,available);
        var saved=_state.GetWindowRect(_currentWindowId,_windowRect);
        if(MathF.Abs(saved.Height-height)<.01f)return;
        _state.SetWindowRect(_currentWindowId,saved with {Height=height});
        _state.SetWindowExpandedSize(_currentWindowId,new(saved.Width,height));
        _requestFrame?.Invoke();
    }
}
