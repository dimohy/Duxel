using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class VirtualGridPage : IObserverPage, IObserverNaturalPage
{
    private readonly UiWidget _root;
    public string Title => "가상 그리드";
    public string Description => "The grid creates only cells near the viewport in a billion rows and columns. Cell edits survive scrolling away.";
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal VirtualGridPage()
    {
        var grid = Dux.VirtualGrid(new Cells(), 1_000_000_000, 1_000_000_000,
            new(RowHeight : 44, ColumnWidth : 200, Width : 960, Height : 320) { OwnsWheelInput = true });
        grid.AccessibleName = "가상 그리드 예제";
        _root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                Fluent.Button("첫 행·첫 열", () => grid.Offset = default),
                Fluent.Button("마지막 행·열", () => grid.Offset = new(double.MaxValue, double.MaxValue))),
            Fluent.Body1(() => $"생성된 셀 {grid.RealizedCount:N0}개 · 가로 {grid.Offset.X:N2} · 세로 {grid.Offset.Y:N2}"),
            grid);
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();

    private sealed class Cells : IUiVirtualGridSource
    {
        private readonly Dictionary<(int Row, int Column), string> _edits = [];

        public string GetKey(int row, int column) => string.Create(CultureInfo.InvariantCulture, $"{row}:{column}");

        public UiWidget Create(int row, int column)
        {
            var cell = (row, column);
            var initial = string.Create(CultureInfo.InvariantCulture, $"{row + 1:N0}행 {column + 1:N0}열");
            var editor = Fluent.Input("셀 값", new UiBinding<string>(
                () => _edits.GetValueOrDefault(cell, initial), value => _edits[cell] = value), 196);
            editor.AccessibleName = initial;
            return editor;
        }

        public void Bind(UiWidget widget, int row, int column) { }
    }
}
