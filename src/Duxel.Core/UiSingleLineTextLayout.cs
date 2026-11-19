namespace Duxel.Core;

internal sealed class UiSingleLineTextLayout
{
    private string? _text;
    private UiLayoutMeasurementKey _key;
    private bool _valid;
    private long _frame = -1;
    private int _affinityIndex = -1;
    private float _affinityX;
    private UiSingleLineComposition? _composition;
    internal UiSingleLineComposition PrepareComposition(UiImmediateContext ui, string text, int caret, string preedit)
    {
        var composition = _composition ??= new();
        composition.Prepare(ui, text, caret, preedit);
        return composition;
    }
    internal void ClearComposition() => _composition = null;
    internal PlatformTextLineGeometry? Geometry
    {
        get; private set;
    }
    internal float Scale { get; private set; } = 1;
    internal int Version
    {
        get; private set;
    }
    internal float Width => Geometry!.Advance * Scale;

    internal void Prepare(UiImmediateContext ui, string text)
    {
        var key = ui.LayoutMeasurementKey;
        var retain = ui.CanRetainControlTextLayout;
        var frame = ui.GetFrameCount();
        var same = _text == text && _key == key;
        if (same && (_valid && retain || _frame == frame && _valid == retain))
            return;
        if (!same)
            ClearAffinity();
        Geometry = null;
        Scale = 1;
        if (text.AsSpan().IndexOfAny('\r', '\n') < 0 && ui.TryPrepareLineGeometry(text, out var geometry, out var scale))
        {
            Geometry = geometry;
            Scale = scale;
        }
        _text = text;
        _key = key;
        _valid = retain;
        _frame = frame;
        Version++;
    }

    internal void ClearAffinity()
    {
        if (_affinityIndex < 0)
            return;
        _affinityIndex = -1;
        Version++;
    }
    internal float X(int index) => _affinityIndex == index ? _affinityX : Geometry!.CaretX(index) * Scale;
    internal int Hit(float x) => Accept(Geometry!.HitTestCaret(x / Scale));
    internal void RememberTrailingCaret(int index)
    {
        if (Geometry is { } geometry)
            Accept(new(index, geometry.TrailingCaretX(index), true));
    }
    internal int Move(int index, bool right) => Geometry!.MoveVisualCaret(X(index) / Scale, right) is { } target
        ? Accept(target) : index;
    internal int Collapse(UiTextSelection selection, bool right)
    {
        var first = X(selection.Start);
        var last = X(selection.End);
        var useFirst = right ? first > last : first < last;
        return Accept(new(useFirst ? selection.Start : selection.End, (useFirst ? first : last) / Scale, false));
    }

    private int Accept(PlatformTextHitTestResult hit)
    {
        if (_affinityIndex != hit.Position || _affinityX != hit.X * Scale)
            Version++;
        _affinityIndex = hit.Position;
        _affinityX = hit.X * Scale;
        return hit.Position;
    }
}
