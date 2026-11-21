using System.Globalization;
using System.Text;
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
        var grid = Dux.VirtualGrid(new Cells(GalleryText.Get("Row {0:N0}, column {1:N0}"), GalleryText.Get("Cell value")), 1_000_000_000, 1_000_000_000,
            new(RowHeight : 44, ColumnWidth : 200, Width : 960, Height : 320) { OwnsWheelInput = true });
        grid.AccessibleName = GalleryText.Get("Virtual grid example");
        _root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                Fluent.Button(GalleryText.Get("First row and column"), () => grid.Offset = default),
                Fluent.Button(GalleryText.Get("Last row and column"), () => grid.Offset = new(double.MaxValue, double.MaxValue))),
            Fluent.Body1(() => string.Format(GalleryText.Get("Realized cells: {0:N0} · Horizontal: {1:N2} · Vertical: {2:N2}"),
                grid.RealizedCount, grid.Offset.X, grid.Offset.Y)),
            grid);
    }

    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();

    private sealed class Cells(string cellNameFormat, string cellLabel) : IUiVirtualGridSource
    {
        private readonly Dictionary<(int Row, int Column), string> _edits = [];
        private readonly CompositeFormat _cellName = CompositeFormat.Parse(cellNameFormat);

        public string GetKey(int row, int column) => string.Create(CultureInfo.InvariantCulture, $"{row}:{column}");

        public string GetAccessibleCellName(int row, int column) =>
            string.Format(CultureInfo.InvariantCulture, _cellName, row + 1, column + 1);

        public UiWidget Create(int row, int column)
        {
            var cell = (row, column);
            var initial = GetAccessibleCellName(row, column);
            var editor = Fluent.Input(cellLabel, new UiBinding<string>(
                () => _edits.GetValueOrDefault(cell, initial), value => _edits[cell] = value), 196);
            editor.AccessibleName = initial;
            return editor;
        }

        public void Bind(UiWidget widget, int row, int column) { }
    }
}
