namespace Duxel.Core;

/// <summary>A value-type clip scope shared by custom control drawing implementations.</summary>
public readonly struct UiControlClipScope:IDisposable
{
    private readonly UiImmediateContext? _ui;
    internal UiControlClipScope(UiImmediateContext ui)=>_ui=ui;
    public void Dispose()=>_ui?.PopClipRect();
}

public sealed partial class UiImmediateContext
{
    internal UiControlClipScope BeginControlClip(UiRect bounds)
    {
        PushClipRect(IntersectRect(ResolveItemClipRect(),bounds),true);
        return new(this);
    }
}
