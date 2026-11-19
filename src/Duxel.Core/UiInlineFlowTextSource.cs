namespace Duxel.Core;

/// <summary>Literal spans and explicitly measured widget objects, without a document parser or image surrogate.</summary>
internal sealed class UiInlineFlowTextSource : UiStyledTextSource
{
    private string _source = string.Empty;
    private UiStyledSourceRun[] _runs = [];
    private UiStyledTextObject[] _objects = [];
    private long _revision;
    internal override string Source => _source;
    internal override IReadOnlyList<UiStyledSourceRun> Runs => _runs;
    internal override IReadOnlyList<UiStyledTextObject> Objects => _objects;
    internal override long Revision => _revision;
    internal bool RightToLeft
    {
        get; private set;
    }
    internal void SetDirection(bool rightToLeft)
    {
        RightToLeft = rightToLeft;
        _revision++;
        ReleaseProjection();
    }
    internal override UiSourceTextRange? ActiveBlock => null;
    internal override UiStyledTextSource Composition => throw new NotSupportedException("Owned inline flow is not an editor composition source.");
    internal void Replace(string source, UiStyledSourceRun[] runs, UiStyledTextObject[] objects)
    {
        if (_source == source && _runs.AsSpan().SequenceEqual(runs) && _objects.AsSpan().SequenceEqual(objects))
            return;
        _source = source;
        _runs = runs;
        _objects = objects;
        _revision++;
        ReleaseProjection();
    }
    internal override void Prepare(string source, int activeIndex, bool preserveActiveBlock = false)
    {
        if (_source != source || activeIndex >= 0 || preserveActiveBlock)
            throw new InvalidOperationException("Owned inline flow must use its prepared literal source without an editing block.");
    }
    internal void Clear()
    {
        _source = string.Empty;
        _runs = [];
        _objects = [];
        _revision++;
        ReleaseProjection();
    }
}
