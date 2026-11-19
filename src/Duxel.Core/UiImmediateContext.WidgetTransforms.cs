using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private Matrix3x2 _widgetToScreen = Matrix3x2.Identity;
    private Matrix3x2 _widgetFromScreen = Matrix3x2.Identity;
    private UiWidgetClipGeometry? _widgetClipGeometry;
    private UiRect? _widgetScreenClip;
    private readonly List<WidgetImeHandler> _widgetImeHandlers = [];
    private int _widgetTransformDepth;
    private int _widgetVectorMeasurementDepth;
    private float _widgetRasterScale = 1;
    private int _widgetPointerBlockedTier = -1;
    internal float WidgetRasterScale => _widgetRasterScale;

    internal void RenderTransformedWidget(UiWidget widget, UiEdgeInsets margin, UiWidgetTransform transform)
    {
        var available = GetWidgetMeasureAvailable();
        var contentAvailable = transform.ContentMaximum(new(MathF.Max(0, available.X - margin.Left - margin.Right),
            MathF.Max(0, available.Y - margin.Top - margin.Bottom)));
        var constraints = _inputSlotBounds is not null && _inputSlotBoundsTier == _popupTierDepth && _layoutChildConstraints
            ? UiBoxConstraints.Loose(contentAvailable) : (UiBoxConstraints?)null;
        var content = MeasureWidgetContent(widget, contentAvailable, constraints, transformed: true);
        var local = transform.Arrange(content, default);
        var desired = UiWidget.MarginExtent(local.Extent, margin);
        var size = ConstrainToLayout(desired);
        var position = AdvanceCursor(size);
        FinishLayoutBox(position, size);
        var placement = new UiVector2(position.X + margin.Left, position.Y + margin.Top);
        var matrix = transform.Arrange(content, placement).Matrix;
        var geometryScale = transform.Scale;
        if (size != desired && transform.Mode == UiWidgetTransformMode.Layout && local.Extent.X > 0 && local.Extent.Y > 0)
        {
            var room = new UiVector2(MathF.Max(0, size.X - margin.Left - margin.Right), MathF.Max(0, size.Y - margin.Top - margin.Bottom));
            var fit = MathF.Min(1, MathF.Min(room.X / local.Extent.X, room.Y / local.Extent.Y));
            if (fit > 0 && fit < 1)
            {
                matrix = local.Matrix * Matrix3x2.CreateScale(fit) * Matrix3x2.CreateTranslation(placement.X, placement.Y);
                geometryScale *= fit;
            }
        }
        var parentClip = CurrentClipRect;
        using var coordinates = BeginWidgetAffineSpace(matrix, geometryScale, parentClip);
        var localClip = coordinates.LocalClip;
        var capture = BeginInputSlotCapture();
        capture.SetLayerScene3D(null);
        capture._SetClipRect(localClip);
        var complete = false;
        try
        {
            RenderAllocatedChild(capture, widget, new(0, 0, content.X, content.Y), localClip,
                constrainLayout: true, clipToAllocation: false, renderContentOnly: true);
            _builder.AppendTransformedWidget(capture, matrix, _displaySize, parentClip);
            complete = true;
        }
        finally
        {
            EndInputSlotCapture(capture, complete);
        }
    }

    private sealed class WidgetImeHandler : IUiImeHandler
    {
        private IUiImeHandler _source = null!;
        internal IUiImeHandler Source => _source;
        private Matrix3x2 _matrix;
        private float _fontScale;
        internal void Configure(IUiImeHandler source, Matrix3x2 matrix, float fontScale) => (_source, _matrix, _fontScale) = (source, matrix, fontScale);
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
            _source.SetCaretRect(UiWidgetAffineLayout.MapBounds(caretRect, _matrix),
                UiWidgetAffineLayout.MapBounds(inputRect, _matrix), fontPixelHeight * _fontScale, fontPixelWidth * _fontScale);
        }
        public string? GetCompositionText() => _source.GetCompositionText();
        public void SetCompositionOwner(string? inputId) => _source.SetCompositionOwner(inputId);
        public void ReleaseInput(string inputId) => _source.ReleaseInput(inputId);
        public string? ConsumeCommittedText(string inputId) => _source.ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => _source.ConsumeRecentCommittedText();
    }
}
