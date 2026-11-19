namespace Duxel.Core;

public partial class UiTabList<TBehavior, TPresentation>
    where TBehavior : IUiTabListBehavior where TPresentation : IUiTabListPresentation
{
    private TabMeasurements? _measurements;

    internal override void InvalidateRetainedMeasurement() => _measurements?.Invalidate();

    private TabMeasurements? PrepareMeasurementCache(UiControlContext context, string? selected, out bool reuse)
    {
        if (!TPresentation.CanRetainMeasurement || !context.CanRetainLayoutMeasurement || _items.Length > 256
            || !Options.ReserveSelectedSpace && (selected?.Length ?? 0) > 1024
            || (Options.Overflow?.Label.Length ?? 0) > 1024)
        {
            _measurements = null;
            reuse = false;
            return null;
        }
        if (_measurements is null || _measurements.Sizes.Length != _items.Length)
            _measurements = new(_items.Length);
        var selection = Options.ReserveSelectedSpace ? null : selected;
        reuse = _measurements.Matches(context.LayoutMeasurementKey, EffectiveOptions(context), selection);
        if (!reuse) _measurements.Clear();
        return _measurements;
    }

    /// <summary>One bounded intrinsic-size buffer. Allocated header rectangles are always rebuilt separately.</summary>
    private sealed class TabMeasurements(int count)
    {
        internal readonly UiVector2[] Sizes = new UiVector2[count];
        internal ulong Revision;
        private bool _valid;
        private UiLayoutMeasurementKey _key;
        private UiTabListOptions _options;
        private string? _selected;

        internal bool Matches(UiLayoutMeasurementKey key, UiTabListOptions options, string? selected) =>
            _valid && _key == key && _options == options && _selected == selected;

        internal void Clear()
        {
            _valid = false;
            _key = default;
            _options = default;
            _selected = null;
        }
        internal void Invalidate()
        {
            unchecked { Revision++; }
            Clear();
        }
        internal void Commit(UiLayoutMeasurementKey key, UiTabListOptions options, string? selected)
        {
            _key = key;
            _options = options;
            _selected = selected;
            _valid = true;
        }
    }
}
