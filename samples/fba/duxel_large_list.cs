#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:property PublishAot=false
#:property SelfContained=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:include Host/$(DuxelHost).cs
#:package Duxel.$(DuxelHost).App@0.4.0

using System.Runtime.CompilerServices;
using Duxel.App;
using Duxel.Core;
using Duxel.Samples;

const int ItemCount = 100_000;
const float RowHeight = 36;
var selected = new UiState<int>(0);
var id = new UiState<string>("");
var status = new UiState<string>("Enter an item number to jump to its row.");
var rows = new Rows(selected);
var list = Dux.VirtualList(rows, ItemCount, new UiVirtualListOptions(ItemExtent: RowHeight, Height: 320, Overscan: 3));
list.AccessibleName = "Inventory";
var root = Dux.Column(new UiFlexOptions(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    Dux.Row(new(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Center),
        Fluent.Text(Text.Title2, "Inventory"), Fluent.Badge("100,000 items")),
    Dux.Row(new(Spacing: 8, CrossAxisAlignment: UiCrossAxisAlignment.Center),
        Fluent.Input("Item number", id.Binding, 8, 170), Fluent.Button("Find item", Find),
        Fluent.Button("First", () => Jump(0)), Fluent.Button("Last", () => Jump(ItemCount - 1))),
    Fluent.Text(Text.Caption1, () => status.Value),
    Fluent.Divider(),
    list,
    Fluent.Card("Selected item", Fluent.Text(Text.Body1, () => Rows.Label(selected.Value))),
    Fluent.Text(Text.Caption1, () => $"{list.RealizedCount:N0} row widgets for {ItemCount:N0} items"));
Run(Dux.ScrollView(Dux.SizedBox(root, width: 606), new(UiScrollAxis.Both)), "Duxel Inventory", 684, 616);

void Find()
{
    if (int.TryParse(id.Value, out var number) && number is >= 1 and <= ItemCount)
        Jump(number - 1);
    else
        status.Value = "Enter an item number from 1 to 100,000.";
}

void Jump(int index)
{
    selected.Value = index;
    list.Offset = index * (double)RowHeight;
    status.Value = $"Showing item {index + 1:N0}.";
}

static void Run(UiWidget root, string title, int width, int height, Func<bool>? animationActive = null)
{
    var options = DuxelApp.Options(Dux.App(root), title: title, width: width, height: height);
    options = options with
    {
        Debug = new() { LogStartupTimings = true },
        Frame = options.Frame with
        {
            UseEventDrivenPlatformLoop = true,
            EnableIdleFrameSkip = true,
            IsAnimationActiveProvider = animationActive
        }
    };
    DesktopSampleHost.Run(options);
}


sealed class Rows(UiState<int> selected) : IUiVirtualListSource
{
    private readonly ConditionalWeakTable<UiWidget, UiState<int>> _rowIndex = new();

    public string GetKey(int index) => $"inventory-{index}";

    public static string Label(int index) => $"INV-{index + 1:D6}    ·    Aisle {(index % 48) + 1:D2}    ·    Stock {(index * 37) % 220}";

    public UiWidget Create(int index)
    {
        var position = new UiState<int>(index);
        var row = Dux.Row(new(Spacing: 8, CrossAxisAlignment: UiCrossAxisAlignment.Center),
            Dux.SizedBox(Fluent.Text(Text.Body1, () => Label(position.Value)), width: 394),
            Fluent.Button("Select", () => selected.Value = position.Value));
        _rowIndex.Add(row, position);
        return row;
    }

    public void Bind(UiWidget widget, int index)
    {
        if (!_rowIndex.TryGetValue(widget, out var position))
            throw new InvalidOperationException("The row was not created by this source.");
        position.Value = index;
    }
}
