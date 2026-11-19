// FBA: a keyed billion-row virtual list with double offsets and editable visible rows.
#:property TargetFramework=net11.0-windows7.0
#:property OutputType=WinExe
#:property OptimizationPreference=Speed
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.Core;
using Duxel.App;
using Duxel.Windows.App;

var source = new DemoRows();
var list = Dux.VirtualList(source, 1_000_000_000, new(40, 760, 320, Overscan : 2, MaxRealizedItems : 64));
list.AccessibleName = "Billion row list";
using var frame = Dux.ScrollView(new UiColumn([list, Dux.SizedBox(height : 400)], new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min)),
    new(Width : 780, Height : 330));
frame.AccessibleName = "Virtual list frame";
var exercise = Environment.GetEnvironmentVariable("DUXEL_SAMPLE_EXERCISE") == "1";
var previousStep = -1;
var options = DuxelApp.Options<VirtualListDesign>(Dux.App(Dux.View(Render)),
    title : "Duxel / Billion Row Virtual List", width : 900, height : 620);
DuxelWindowsApp.Run(options with
{
    Window = options.Window with { MinWidth = 860, MinHeight = 600 },
    Debug = new() { LogStartupTimings = true }
});

void Render(UiImmediateContext ui)
{
    ui.EnableRootViewportContentLayout(contentPadding : 24);
    var origin = ui.GetMainViewport().WorkPos;
    ui.SetCursorScreenPos(new(origin.X + 24, origin.Y + 24));
    if (ui.Button("Top"))
        list.Offset = 0;
    ui.SameLine();
    if (ui.Button("Middle"))
        list.Offset = (double)list.ItemCount * list.Options.ItemExtent / 2;
    ui.SameLine();
    if (ui.Button("End"))
        list.Offset = double.MaxValue;
    ui.SameLine();
    if (ui.Button($"Count : {list.ItemCount}"))
        list.ItemCount = list.ItemCount == 1000 ? 1_000_000_000 : 1000;
    ui.SameLine();
    if (ui.Button(source.Swapped ? "Restore keys###reorder" : "Reorder keys###reorder"))
    {
        source.Swapped = !source.Swapped;
        list.Refresh();
    }
    ui.SameLine();
    if (ui.Button(list.Options.ExtentIndex is null ? "Variable heights###extent-mode" : "Fixed heights###extent-mode"))
    {
        list.Options = list.Options with { ExtentIndex = list.Options.ExtentIndex is null ? new AlternateRowExtents(source) : null };
    }
    ui.Text($"Realized : {list.RealizedCount} / Live : {source.Created - source.Disposed} / Created : {source.Created}");
    ui.Text($"Offset : {list.Offset:0.00}");
    ui.Text("Edit visible rows. Model text and marks persist; evicted widget state is released.");
    if (exercise)
    {
        var step = (int)(ui.GetTime() * 2) % 5;
        if (step != previousStep)
        {
            previousStep = step;
            switch (step)
            {
                case 0:
                    list.Offset = 0;
                    break;
                case 1:
                    list.Offset = (double)list.ItemCount * 20;
                    break;
                case 2:
                    list.Offset = double.MaxValue;
                    break;
                case 3:
                    list.ItemCount = 1000;
                    break;
                case 4:
                    list.ItemCount = 1_000_000_000;
                    list.Offset = (double)list.ItemCount * 40 - 320.25;
                    break;
            }
            Console.WriteLine($"VirtualExercise : {step}");
        }
        ui.RequestFrameAt(ui.GetTime() + 0.1);
    }
    ui.SetCursorScreenPos(new(origin.X + 24, origin.Y + 130));
    frame.Render(ui);
}

sealed class AlternateRowExtents(DemoRows source) : IUiVirtualListExtentIndex
{
    public double GetBoundary(int index) => (double)(index / 2) * 100 + (index % 2 == 0 ? 0 : 36);

    public int FindIndexByKey(string key, int itemCount) =>
        source.FindAccessibleItem(UiVirtualItemSearchProperty.AutomationId, key, 0, itemCount);
}

sealed class DemoRows : IUiVirtualListAutomationSource
{
    private const int FirstSwapped = 987654321;
    private const int SecondSwapped = 999999999;
    private readonly Dictionary<int, string> _values = [];
    private readonly HashSet<int> _marked = [];
    internal int Created, Disposed;
    internal bool Swapped;
    private int Swap(int value) => Swapped ? value switch
    {
        FirstSwapped => SecondSwapped,
        SecondSwapped => FirstSwapped,
        _ => value
    } : value;
    public string GetKey(int index) => Swap(index).ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string GetAccessibleItemName(int index) => $"Item : {GetKey(index)}";

    public int FindAccessibleItem(UiVirtualItemSearchProperty property, string value, int startIndex, int itemCount)
    {
        const string prefix = "Item : ";
        if (property == UiVirtualItemSearchProperty.Name && !value.StartsWith(prefix, StringComparison.Ordinal))
            return -1;
        var key = property == UiVirtualItemSearchProperty.Name ? value.AsSpan(prefix.Length) : value.AsSpan();
        if (!int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var modelKey))
            return -1;
        var index = Swap(modelKey);
        return index >= startIndex && index < itemCount && key.SequenceEqual(GetKey(index)) ? index : -1;
    }
    public UiWidget Create(int index)
    {
        Created++;
        var key = Swap(index);
        var initial = $"Item : {key}";
        var input = Fluent.Input("Value", new UiBinding<string>(
            () => _values.GetValueOrDefault(key, initial),
            value => _values[key] = value), 256);
        input.AccessibleName = $"Row value : {key}";
        var mark = Fluent.Button(_marked.Contains(key) ? "Mark : On" : "Mark : Off");
        mark.Activated = () =>
        {
            if (!_marked.Add(key))
                _marked.Remove(key);
            mark.Label = _marked.Contains(key) ? "Mark : On" : "Mark : Off";
        };
        return new RowLifetime(this, Dux.Row(
            new(Spacing : 12, CrossAxisAlignment : UiCrossAxisAlignment.Center),
            Fluent.Caption1($"Key : {key}"), Dux.Expanded(input), mark));
    }
    public void Bind(UiWidget widget, int index) { }

    private sealed class RowLifetime : UiControlWidget
    {
        private readonly DemoRows _source;
        private readonly UiWidget _content;
        internal RowLifetime(DemoRows source, UiWidget content)
        {
            _source = source;
            _content = content;
            OwnChild("row", content);
        }
        protected override UiVector2 MeasureContent(UiControlContext context) => throw new InvalidOperationException("Fixed virtual rows do not require measurement.");
        protected override void RenderControl(UiImmediateContext ui) => _content.Render(ui);
        protected override void OnDispose() => _source.Disposed++;
    }
}

public readonly struct VirtualListDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11Dark;
}
