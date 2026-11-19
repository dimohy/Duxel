namespace Duxel.Core;

public sealed partial class UiDrawListBuilder
{
    internal UiLayer3D? LayerScene3D => _scene3D;
    internal void AppendDeferredFrom(UiDrawListBuilder child)
    {
        if (ReferenceEquals(child, this))
            throw new InvalidOperationException("A builder cannot append itself.");
        if (!IsPresentationPass)
        {
            child.DiscardDeferred();
            return;
        }
        Flush();
        var first = _drawLists.Count;
        child.AppendBuiltTo(_drawLists);
        if (_circleCutoutRadius == 0)
            return;
        for (var index = first; index < _drawLists.Count; index++)
        {
            var source = _drawLists[index];
            var masked = ApplyCircleCutoutToReference(source);
            if (source.OwnsBuffers)
                source.ReleasePooled();
            _drawLists[index] = masked;
        }
    }
    internal void DiscardDeferred()
    {
        _ResetForNewFrame();
    }
}
