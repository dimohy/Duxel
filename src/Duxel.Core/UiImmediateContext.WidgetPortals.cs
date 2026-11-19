using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private Stack<(int Tier, WidgetScreenSpaceScope Scope)>? _widgetPopupScopes;
    private int _widgetScreenTransformDepth;
    private int _screenPortalLayerDepth;
    private bool HasWidgetCoordinates => _widgetToScreen != Matrix3x2.Identity || _widgetClipGeometry is not null || _widgetRasterScale != 1;
    private UiVector2 WidgetScreenPoint(UiVector2 point) => UiWidgetAffineLayout.MapPoint(point, _widgetToScreen);
    private UiRect WidgetScreenBounds(UiRect bounds) => UiWidgetAffineLayout.MapBounds(bounds, _widgetToScreen);
    private bool IsScreenHovering(UiRect rect)
    {
        var point = WidgetScreenPoint(_mousePosition);
        return point.X >= rect.X && point.X <= rect.X + rect.Width && point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
    }

    // Portals escape the owner's affine coordinates, clipping and IME mapping together.
    // The popup builder still owns paint order and its screen-space presentation camera.
    private WidgetScreenSpaceScope BeginWidgetScreenSpace()
    {
        if (!HasWidgetCoordinates && !HasLayerCameraAboveScreenPortal())
            return default;
        var saved = new WidgetScreenState(_mousePosition, _widgetToScreen, _widgetFromScreen, _widgetClipGeometry,
            _widgetScreenClip, _widgetRasterScale, _imeHandler, _layoutPointerBlockedTier, _widgetPointerBlockedTier, _widgetScreenTransformDepth, _screenPortalLayerDepth,
            _accessibility?.WidgetCoordinates, _accessibility?.WidgetClip, _accessibility?.WidgetClipGeometry);
        _mousePosition = WidgetScreenPoint(_mousePosition);
        _widgetToScreen = _widgetFromScreen = Matrix3x2.Identity;
        _widgetClipGeometry = null;
        _widgetScreenClip = null;
        _widgetRasterScale = 1;
        _layoutPointerBlockedTier = -1;
        _widgetPointerBlockedTier = -1;
        _widgetScreenTransformDepth = _widgetTransformDepth;
        _screenPortalLayerDepth = _layerFrames.Count;
        while (_imeHandler is WidgetImeHandler mapped)
            _imeHandler = mapped.Source;
        if (_accessibility is { } tree)
        {
            tree.WidgetCoordinates = Matrix3x2.Identity;
            tree.WidgetClip = null;
            tree.WidgetClipGeometry = null;
        }
        _clipStack.Push(_clipRect);
        return new(this, saved);
    }

    private readonly record struct WidgetScreenState(UiVector2 Mouse, Matrix3x2 ToScreen, Matrix3x2 FromScreen,
        UiWidgetClipGeometry? Clips, UiRect? ScreenClip, float RasterScale, IUiImeHandler? Ime, int BlockedTier, int WidgetBlockedTier, int ScreenTransformDepth, int ScreenLayerDepth,
        Matrix3x2? TreeCoordinates, UiRect? TreeClip, UiWidgetClipGeometry? TreeClips);

    private readonly struct WidgetScreenSpaceScope(UiImmediateContext ui, WidgetScreenState saved) : IDisposable
    {
        internal bool IsActive => ui is not null;
        public void Dispose()
        {
            if (ui is null)
                return;
            ui._clipStack.Pop();
            (ui._mousePosition, ui._widgetToScreen, ui._widgetFromScreen, ui._widgetClipGeometry,
                ui._widgetScreenClip, ui._widgetRasterScale, ui._imeHandler, ui._layoutPointerBlockedTier, ui._widgetPointerBlockedTier, ui._widgetScreenTransformDepth, ui._screenPortalLayerDepth) =
                (saved.Mouse, saved.ToScreen, saved.FromScreen, saved.Clips, saved.ScreenClip, saved.RasterScale, saved.Ime, saved.BlockedTier, saved.WidgetBlockedTier, saved.ScreenTransformDepth, saved.ScreenLayerDepth);
            if (ui._accessibility is { } tree)
            {
                tree.WidgetCoordinates = saved.TreeCoordinates ?? Matrix3x2.Identity;
                tree.WidgetClip = saved.TreeClip;
                tree.WidgetClipGeometry = saved.TreeClips;
            }
        }
    }
}
