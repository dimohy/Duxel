using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class VirtualPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly ObserverRows _source = new(GalleryText.Get("Row"), GalleryText.Get("Row: {0}"),
        GalleryText.Get("Row value: {0}"), GalleryText.Get("Key: {0}"));
    private readonly UiVirtualList<ObserverRows> _list;
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }
    public string Title => "가상 목록";
    public string Description => "The list creates only visible items from a billion entries. Edited values stay tied to their item keys when the orientation changes.";
    internal int RealizedCount => _list.RealizedCount;
    internal double Offset => _list.Offset;
    internal int LiveCount => _source.Created - _source.Disposed;
    internal UiVirtualList<ObserverRows> Preview => _list;
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal VirtualPage()
    {
        var listOptions = new UiVirtualListOptions(40, 980, 320, Overscan : 2, MaxRealizedItems : 64)
        {
            ExtentIndex = new ObserverRowExtents(_source)
        };
        var listDefinition = GallerySample.Define("_list", () => Dux.VirtualList(_source, 1_000_000_000, listOptions));
        _list = listDefinition.Value;
        _list.AccessibleName = GalleryText.Get("Observed virtual list");
        var frameDefinition = GallerySample.Define("frame", () => Dux.ScrollView(
            new UiColumn([_list, Dux.SizedBox(height : 100)], new(MainAxisSize : UiMainAxisSize.Min)),
            new(Width : 1000, Height : 380)));
        var frame = frameDefinition.Value;
        frame.AccessibleName = GalleryText.Get("Outer virtual list scroll view");
        var actionsDefinition = GallerySample.Define("actions", () =>
        {
            string ExtentLabel() => _list.Options.Axis == UiScrollAxis.Horizontal
                ? (_list.Options.ExtentIndex is null ? GalleryText.Get("Item width: fixed") : GalleryText.Get("Item width: variable"))
                : (_list.Options.ExtentIndex is null ? GalleryText.Get("Row height: fixed") : GalleryText.Get("Row height: variable"));
            var heights = Fluent.Button(ExtentLabel());
            heights.Activated = () =>
            {
                _list.Options = _list.Options with
                {
                    ExtentIndex = _list.Options.ExtentIndex is null
                        ? new ObserverRowExtents(_source, _list.Options.Axis == UiScrollAxis.Horizontal)
                        : null
                };
                heights.Label = ExtentLabel();
            };
            string AxisLabel() => _list.Options.Axis == UiScrollAxis.Horizontal ? GalleryText.Get("Orientation: horizontal") : GalleryText.Get("Orientation: vertical");
            var direction = Fluent.Button(AxisLabel());
            direction.Activated = () =>
            {
                var horizontal = _list.Options.Axis == UiScrollAxis.Vertical;
                _list.Options = _list.Options with
                {
                    Axis = horizontal ? UiScrollAxis.Horizontal : UiScrollAxis.Vertical,
                    ItemExtent = horizontal ? 360 : 40,
                    ExtentIndex = _list.Options.ExtentIndex is null ? null : new ObserverRowExtents(_source, horizontal)
                };
                _list.Offset = 0;
                direction.Label = AxisLabel();
                heights.Label = ExtentLabel();
            };
            string OrderLabel() => _list.Options.FlowDirection == UiFlowDirection.RightToLeft
                ? GalleryText.Get("Horizontal order: right to left") : GalleryText.Get("Horizontal order: left to right");
            var order = Fluent.Button(OrderLabel());
            order.Activated = () =>
            {
                _list.Options = _list.Options with
                {
                    FlowDirection = _list.Options.FlowDirection == UiFlowDirection.RightToLeft
                        ? UiFlowDirection.LeftToRight : UiFlowDirection.RightToLeft
                };
                order.Label = OrderLabel();
            };
            var reorder = Fluent.Button(GalleryText.Get("Swap the first and last keys"), () => { _source.Swapped = !_source.Swapped; _list.Refresh(); });
            return Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                Fluent.Button(GalleryText.Get("Go to start"), () => _list.Offset = 0),
                Fluent.Button(GalleryText.Get("Go to middle"), () => _list.Offset = (_list.Options.ExtentIndex?.GetBoundary(_list.ItemCount)
                    ?? (double)_list.ItemCount * _list.Options.ItemExtent) / 2),
                Fluent.Button(GalleryText.Get("Go to end"), () => _list.Offset = double.MaxValue), direction, heights, order, reorder);
        });
        var actions = actionsDefinition.Value;
        var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            actions,
            Fluent.Body1(() => string.Format(GalleryText.Get("Realized rows: {0} / Live widgets: {1} / Created: {2} / Disposed: {3}"),
                _list.RealizedCount, _source.Created - _source.Disposed, _source.Created, _source.Disposed)),
            Fluent.Body1(() => string.Format(GalleryText.Get("Rows: {0:N0} / Offset: {1:N2}"), _list.ItemCount, _list.Offset)), frame));
        var factory = sample.Source;
        var declaration = GalleryPageSource.Declaration(nameof(ObserverRows));
        var rowLabels = string.Join(", ", GallerySample.String(GalleryText.Get("Row")), GallerySample.String(GalleryText.Get("Row: {0}")),
            GallerySample.String(GalleryText.Get("Row value: {0}")), GallerySample.String(GalleryText.Get("Key: {0}")));
        sample.BindSource(() => (_list.Options, _source.Swapped,
            Values : string.Join("\n", _source.Values.OrderBy(pair => pair.Key).Select(pair =>
                $"_source.SetValue({pair.Key}, {GallerySample.String(pair.Value)});"))), state =>
        {
            var options = state.Options;
            static string Number(float? value) => value is { } number ? number.ToString("R", CultureInfo.InvariantCulture) + "f" : "null";
            var extentIndex = options.ExtentIndex is null ? "null"
                : options.Axis == UiScrollAxis.Horizontal ? "new ObserverRowExtents(_source, horizontal : true)" : "new ObserverRowExtents(_source)";
            return $"var _source = new ObserverRows({rowLabels}) {{ Swapped = {GallerySample.Boolean(state.Swapped)} }};\n"
                + (state.Values.Length == 0 ? string.Empty : state.Values + "\n")
                + $"var listOptions = new UiVirtualListOptions(ItemExtent : {Number(options.ItemExtent)}, Width : {Number(options.Width)},\n"
                + $"    Height : {Number(options.Height)}, Overscan : {options.Overscan}, MaxRealizedItems : {options.MaxRealizedItems})\n"
                + $"{{ Axis = UiScrollAxis.{options.Axis}, ExtentIndex = {extentIndex}, FlowDirection = UiFlowDirection.{options.FlowDirection} }};\n\n"
                + listDefinition.Source + "\n_list.AccessibleName = " + GallerySample.String(_list.AccessibleName) + ";\n\n"
                + frameDefinition.Source + "\nframe.AccessibleName = " + GallerySample.String(frame.AccessibleName) + ";\n\n"
                + actionsDefinition.Source + "\n\n" + factory + "\n\n" + declaration;
        });
        Samples = [sample];
        _root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Edit the first row, go to the end, then return to see the value retained.")),
            Fluent.Body1(GalleryText.Get("Swapping the keys exchanges the positions of the first and last rows.")),
            Fluent.Body1(GalleryText.Get("In horizontal mode, use the bottom scrollbar or the horizontal wheel.")), sample));
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();
}
