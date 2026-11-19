namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void DiscardUnpublishedDrawing()
    {
        while (_layerFrames.TryPop(out var frame))
        {
            frame.LayerBuilder?._ResetForNewFrame();
            frame.Entry.BuilderInUse = false;
            frame.Entry.Dirty = true;
            frame.Entry.PendingTextKeys.Clear();
        }
        _cameraLayerDepth = 0;
        _baseBuilder._ResetForNewFrame();
        _presentationLayers.DiscardUnpublishedDrawing();
        _builder = _baseBuilder;
        _builderStack.Clear();
        _clipStack.Clear();
        _clipStack.Push(_clipRect);
        ReleasePendingFrameSceneLeases();
    }
}
