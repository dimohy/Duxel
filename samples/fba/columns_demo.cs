// FBA: Legacy Columns API 전체 시연 — Columns, NextColumn, GetColumnWidth/Offset 등
#:property TargetFramework=net10.0
#:package Duxel.App@*-*

using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Columns Demo",
        Width = 900,
        Height = 600
    },
    Screen = new ColumnsScreen()
});

public sealed class ColumnsScreen : UiScreen
{
    private int _frameCounter;

    public override void Render(UiImmediateContext ui)
    {
        _frameCounter++;

        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(860f, 560f));
        ui.BeginWindow("Legacy Columns API");

        // ── 2-Column Basic ──
        ui.SeparatorText("2 Columns (no border)");
        ui.Columns(2);
        ui.Text("Left column");
        ui.Text("Some content here");
        ui.NextColumn();
        ui.Text("Right column");
        ui.Text("More content here");
        ui.Columns(1);

        // ── 3-Column with border ──
        ui.SeparatorText("3 Columns (with border)");
        ui.Columns(3, true);
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                ui.Text($"R{row} C{col}");
                if (col < 2) ui.NextColumn();
            }
            ui.NextColumn();
        }
        ui.Columns(1);

        // ── Column queries ──
        ui.SeparatorText("Column Info");
        ui.Columns(3, true);
        for (var i = 0; i < 3; i++)
        {
            var width = ui.GetColumnWidth(i);
            var offset = ui.GetColumnOffset(i);
            ui.Text($"Col {i}");
            ui.Text($"  W: {width:0.0}");
            ui.Text($"  Off: {offset:0.0}");
            if (i < 2) ui.NextColumn();
        }
        ui.Text($"GetColumnsCount: {ui.GetColumnsCount()}");
        ui.Text($"GetColumnIndex: {ui.GetColumnIndex()}");
        ui.Columns(1);

        // ── Mixed content in columns ──
        ui.SeparatorText("Mixed Content in Columns");
        ui.Columns(2, true);
        ui.Text("Buttons:");
        ui.Button("Button A");
        ui.Button("Button B");
        ui.NextColumn();
        ui.Text("Toggles:");
        var check = false;
        ui.Checkbox("Column Checkbox", ref check);
        var radio = 0;
        ui.RadioButton("Option 1", ref radio, 0);
        ui.RadioButton("Option 2", ref radio, 1);
        ui.Columns(1);

        ui.EndWindow();
    }
}
