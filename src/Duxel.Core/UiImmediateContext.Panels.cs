using System.ComponentModel;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    [UiComponent("Window",UiComponentExposure.Legacy),EditorBrowsable(EditorBrowsableState.Never)]
    public void BeginWindow(string title)=>BeginPanel(title??"Window");
    [UiComponent("Window",UiComponentExposure.Legacy),EditorBrowsable(EditorBrowsableState.Never)]
    public void EndWindow()=>EndPanel();
    public bool IsPanelAppearing()=>IsWindowAppearing();
    public bool IsPanelFocused()=>IsWindowFocused();
    public bool IsPanelHovered()=>IsWindowHovered();
    public UiDrawListBuilder GetPanelDrawList()=>GetWindowDrawList();
    public void SetNextPanelSizeConstraints(UiVector2 min,UiVector2 max)=>SetNextWindowSizeConstraints(min,max);
    public void SetNextPanelContentSize(UiVector2 size)=>SetNextWindowContentSize(size);
    public void SetNextPanelScroll(float x,float y)=>SetNextWindowScroll(x,y);
    public void SetPanelCollapsed(bool collapsed)=>SetWindowCollapsed(collapsed);
    public bool HasPanelPlacement(string title)=>HasWindowPlacement(title);
    public void SetNextPanelPos(UiVector2 position)=>SetNextWindowPos(position);
    public void SetNextPanelBgAlpha(float alpha)=>SetNextWindowBgAlpha(alpha);
    public void SetNextPanelSize(UiVector2 size)=>SetNextWindowSize(size);
    public void SetNextPanelOpen(bool open)=>SetNextWindowOpen(open);
    public void SetNextPanelFocus()=>SetNextWindowFocus();
    public void SetNextPanelTopMost(bool topMost=true)=>SetNextWindowTopMost(topMost);
    public void SetNextPanelCollapsed(bool collapsed)=>SetNextWindowCollapsed(collapsed);
    public void SetNextPanelAutoHeight(bool enabled=true)=>SetNextWindowAutoHeight(enabled);
    public UiVector2 GetPanelPos()=>GetWindowPos();
    public UiVector2 GetPanelSize()=>GetWindowSize();
    public float GetPanelWidth()=>GetWindowWidth();
    public float GetPanelHeight()=>GetWindowHeight();
    public void SetPanelPos(UiVector2 position)=>SetWindowPos(position);
    public void SetPanelPos(string title,UiVector2 position)=>SetWindowPos(title,position);
    public void SetPanelSize(UiVector2 size)=>SetWindowSize(size);
    public void SetPanelSize(string title,UiVector2 size)=>SetWindowSize(title,size);
    public void SetPanelCollapsed(string title,bool collapsed)=>SetWindowCollapsed(title,collapsed);
    public bool IsPanelCollapsed()=>IsWindowCollapsed();
    public void SetPanelOpen(string title,bool open)=>SetWindowOpen(title,open);
    public bool GetPanelOpen(string title,bool defaultValue=true)=>GetWindowOpen(title,defaultValue);
    public void SetPanelFocus()=>SetWindowFocus();
    public void SetPanelFocus(string title)=>SetWindowFocus(title);
}
