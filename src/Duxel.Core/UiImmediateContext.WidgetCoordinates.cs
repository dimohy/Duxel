using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private WidgetAffineSpaceScope BeginWidgetAffineSpace(Matrix3x2 matrix, float geometryScale, UiRect parentClip, float? rasterScale = null)
    {
        if (!UiWidgetAffineLayout.TryInvert(matrix, out var inverse))
            throw new InvalidOperationException("Widget transform must have a finite inverse.");
        var localClip = UiWidgetAffineLayout.MapBounds(parentClip, inverse);
        var previous = (_mousePosition, _widgetPointerBlockedTier, _widgetToScreen, _widgetScreenClip, _imeHandler,
            _widgetRasterScale, _widgetFromScreen, _widgetClipGeometry);
        var treeCoordinates = _accessibility?.WidgetCoordinates;
        var treeClip = _accessibility?.WidgetClip;
        var treeClips = _accessibility?.WidgetClipGeometry;
        _mousePosition = UiWidgetAffineLayout.MapPoint(_mousePosition, inverse);
        if (previous._mousePosition.X < parentClip.X || previous._mousePosition.Y < parentClip.Y
            || previous._mousePosition.X >= parentClip.X + parentClip.Width || previous._mousePosition.Y >= parentClip.Y + parentClip.Height)
            _widgetPointerBlockedTier = _popupTierDepth;
        _widgetToScreen = matrix * _widgetToScreen;
        _widgetClipGeometry = new(parentClip, _widgetFromScreen, _widgetClipGeometry);
        _widgetFromScreen *= inverse;
        _widgetRasterScale *= rasterScale ?? geometryScale;
        var mappedParentClip = UiWidgetAffineLayout.MapBounds(parentClip, previous._widgetToScreen);
        _widgetScreenClip = previous._widgetScreenClip is { } inherited ? IntersectRect(inherited, mappedParentClip) : mappedParentClip;
        if (_accessibility is { } accessibilityTree)
        {
            accessibilityTree.WidgetCoordinates = _widgetToScreen;
            accessibilityTree.WidgetClip = _widgetScreenClip;
            accessibilityTree.WidgetClipGeometry = _widgetClipGeometry;
        }
        if (_imeHandler is { } handler)
        {
            if (_widgetTransformDepth == _widgetImeHandlers.Count)
                _widgetImeHandlers.Add(new());
            var mapped = _widgetImeHandlers[_widgetTransformDepth];
            mapped.Configure(handler, matrix, geometryScale);
            _imeHandler = mapped;
        }
        _widgetTransformDepth++;
        _clipStack.Push(localClip);
        return new(this, localClip, previous, treeCoordinates, treeClip, treeClips);
    }

    // Paint capture remains with the caller; input, accessibility and IME share one coordinate lifetime.
    private readonly struct WidgetAffineSpaceScope(UiImmediateContext ui, UiRect localClip,
        (UiVector2 Mouse, int BlockedTier, Matrix3x2 ToScreen, UiRect? ScreenClip, IUiImeHandler? Ime,
            float RasterScale, Matrix3x2 FromScreen, UiWidgetClipGeometry? Clips) saved,
        Matrix3x2? treeCoordinates, UiRect? treeClip, UiWidgetClipGeometry? treeClips) : IDisposable
    {
        internal UiRect LocalClip => localClip;
        public void Dispose()
        {
            ui._clipStack.Pop();
            ui._widgetTransformDepth--;
            (ui._mousePosition, ui._widgetPointerBlockedTier, ui._widgetToScreen, ui._widgetScreenClip, ui._imeHandler,
                ui._widgetRasterScale, ui._widgetFromScreen, ui._widgetClipGeometry) = saved;
            if (ui._accessibility is { } restoredTree)
            {
                restoredTree.WidgetCoordinates = treeCoordinates ?? Matrix3x2.Identity;
                restoredTree.WidgetClip = treeClip;
                restoredTree.WidgetClipGeometry = treeClips;
            }
        }
    }
}
