namespace Duxel.Core;

/// <summary>Input traverses layout and behavior without producing visible drawing output.</summary>
public enum UiRenderPhase
{
    Presentation, Input
}

public sealed partial class UiDrawListSharedData
{
    public UiRenderPhase RenderPhase
    {
        get; internal set;
    }
}

public sealed partial class UiImmediateContext
{
    public UiRenderPhase RenderPhase => _drawListSharedData.RenderPhase;
    public bool IsPresentationPass => RenderPhase == UiRenderPhase.Presentation;
    internal void SetRenderPhase(UiRenderPhase phase)
    {
        _drawListSharedData.RenderPhase = phase;
        if (phase == UiRenderPhase.Input)
        {
            // Initial texture/clip setup may have prepared an empty command before phase selection.
            _baseBuilder._PopUnusedDrawCmd();
            _presentationLayers.RemoveEmptyCommands();
        }
    }
}

public readonly partial struct UiControlContext
{
    public UiRenderPhase RenderPhase => _ui.RenderPhase;
    public bool IsPresentationPass => _ui.IsPresentationPass;
}

public sealed partial class UiDrawListBuilder
{
    // A standalone builder is a presentation producer; attached builders share their context's phase.
    public UiRenderPhase RenderPhase => _sharedData is { } shared ? shared.RenderPhase : UiRenderPhase.Presentation;
    public bool IsPresentationPass => RenderPhase == UiRenderPhase.Presentation;
}
