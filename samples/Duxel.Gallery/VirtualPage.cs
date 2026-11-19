using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class VirtualPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly ObserverRows _source = new();
    private readonly UiVirtualList<ObserverRows> _list;
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }
    public string Title => "가상 목록";
    public string Description => "10억 개 항목 가운데 화면에 필요한 항목만 생성합니다. 세로와 가로를 전환해도 편집한 값은 항목의 키에 연결해 보관합니다.";
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
        _list.AccessibleName = "관찰 가상 목록";
        var frameDefinition = GallerySample.Define("frame", () => Dux.ScrollView(
            new UiColumn([_list, Dux.SizedBox(height : 100)], new(MainAxisSize : UiMainAxisSize.Min)),
            new(Width : 1000, Height : 380)));
        var frame = frameDefinition.Value;
        frame.AccessibleName = "가상 목록 바깥 스크롤";
        var actionsDefinition = GallerySample.Define("actions", () =>
        {
            string ExtentLabel() => (_list.Options.Axis == UiScrollAxis.Horizontal ? "항목 너비" : "행 높이")
                + (_list.Options.ExtentIndex is null ? " : 고정" : " : 가변");
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
            string AxisLabel() => _list.Options.Axis == UiScrollAxis.Horizontal ? "방향 : 가로" : "방향 : 세로";
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
                ? "가로 순서 : 오른쪽부터" : "가로 순서 : 왼쪽부터";
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
            var reorder = Fluent.Button("키 순서 바꾸기", () => { _source.Swapped = !_source.Swapped; _list.Refresh(); });
            return Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                Fluent.Button("처음", () => _list.Offset = 0),
                Fluent.Button("중간", () => _list.Offset = (_list.Options.ExtentIndex?.GetBoundary(_list.ItemCount)
                    ?? (double)_list.ItemCount * _list.Options.ItemExtent) / 2),
                Fluent.Button("끝", () => _list.Offset = double.MaxValue), direction, heights, order, reorder);
        });
        var actions = actionsDefinition.Value;
        var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            actions,
            Fluent.Body1(() => $"생성된 행 : {_list.RealizedCount} / 살아 있는 위젯 : {_source.Created - _source.Disposed} / 누적 생성 : {_source.Created} / 누적 해제 : {_source.Disposed}"),
            Fluent.Body1(() => $"행 수 : {_list.ItemCount:N0} / 오프셋 : {_list.Offset:N2}"), frame));
        var factory = sample.Source;
        var declaration = GalleryPageSource.Declaration(nameof(ObserverRows));
        sample.BindSource(() => (_list.Options, _source.Swapped,
            Values : string.Join("\n", _source.Values.OrderBy(pair => pair.Key).Select(pair =>
                $"_source.SetValue({pair.Key}, {GallerySample.String(pair.Value)});"))), state =>
        {
            var options = state.Options;
            static string Number(float? value) => value is { } number ? number.ToString("R", CultureInfo.InvariantCulture) + "f" : "null";
            var extentIndex = options.ExtentIndex is null ? "null"
                : options.Axis == UiScrollAxis.Horizontal ? "new ObserverRowExtents(_source, horizontal : true)" : "new ObserverRowExtents(_source)";
            return $"var _source = new ObserverRows {{ Swapped = {GallerySample.Boolean(state.Swapped)} }};\n"
                + (state.Values.Length == 0 ? string.Empty : state.Values + "\n")
                + $"var listOptions = new UiVirtualListOptions(ItemExtent : {Number(options.ItemExtent)}, Width : {Number(options.Width)},\n"
                + $"    Height : {Number(options.Height)}, Overscan : {options.Overscan}, MaxRealizedItems : {options.MaxRealizedItems})\n"
                + $"{{ Axis = UiScrollAxis.{options.Axis}, ExtentIndex = {extentIndex}, FlowDirection = UiFlowDirection.{options.FlowDirection} }};\n\n"
                + listDefinition.Source + "\n_list.AccessibleName = \"관찰 가상 목록\";\n\n"
                + frameDefinition.Source + "\nframe.AccessibleName = \"가상 목록 바깥 스크롤\";\n\n"
                + actionsDefinition.Source + "\n\n" + factory + "\n\n" + declaration;
        });
        Samples = [sample];
        _root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("처음 행을 편집하고 끝으로 이동했다가 돌아오면 값이 유지됩니다."),
            Fluent.Body1("키 순서를 바꾸면 첫 번째 행과 마지막 행의 위치가 서로 바뀝니다."),
            Fluent.Body1("가로 모드에서는 아래 손잡이 또는 가로 휠로 이동합니다."), sample));
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();
}
