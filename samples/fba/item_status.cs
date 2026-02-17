// FBA: Item 상태 쿼리 API 시연 — IsItemActive/Focused/Clicked/Edited/Activated/Deactivated,
//       GetItemRect, SetItemDefaultFocus, MultiSelect, SetNextItemSelectionUserData
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Item Status Demo",
        Width = 1000,
        Height = 700
    },
    Screen = new ItemStatusScreen()
});

public sealed class ItemStatusScreen : UiScreen
{
    private int _frameCounter;
    private float _sliderVal = 0.5f;
    private string _inputText = "Edit me";
    private int _activatedCount;
    private int _deactivatedCount;
    private int _editedCount;
    private string _lastStatus = "";

    public override void Render(UiImmediateContext ui)
    {
        _frameCounter++;

        RenderStatusDemoWindow(ui);
        RenderRectQueryWindow(ui);
        RenderFocusWindow(ui);
    }

    private void RenderStatusDemoWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(500f, 500f));
        ui.BeginWindow("Item Status Queries");

        ui.SeparatorText("Button Status");
        ui.Button("Test Button");
        var btnHovered = ui.IsItemHovered();
        var btnActive = ui.IsItemActive();
        var btnFocused = ui.IsItemFocused();
        var btnClicked = ui.IsItemClicked();
        var btnVisible = ui.IsItemVisible();
        ui.Text($"  Hovered: {btnHovered}  Active: {btnActive}  Focused: {btnFocused}");
        ui.Text($"  Clicked: {btnClicked}  Visible: {btnVisible}");
        if (btnClicked)
            _lastStatus = "Button clicked!";

        ui.SeparatorText("Slider Status (tracks edit lifecycle)");
        ui.SliderFloat("Track Slider", ref _sliderVal, 0f, 1f, 0f, "0.00");
        if (ui.IsItemActivated())
        {
            _activatedCount++;
            _lastStatus = "Slider ACTIVATED";
        }
        if (ui.IsItemEdited())
        {
            _editedCount++;
        }
        if (ui.IsItemDeactivated())
        {
            _deactivatedCount++;
            _lastStatus = "Slider DEACTIVATED";
        }
        if (ui.IsItemDeactivatedAfterEdit())
            _lastStatus = "Slider DEACTIVATED AFTER EDIT";

        ui.Text($"  Activated: {_activatedCount}x  Edited: {_editedCount}x  Deactivated: {_deactivatedCount}x");

        ui.SeparatorText("Input Text Status");
        ui.InputText("Track Input", ref _inputText, 64);
        ui.Text($"  Hovered: {ui.IsItemHovered()}  Active: {ui.IsItemActive()}  Edited: {ui.IsItemEdited()}");
        ui.Text($"  ID: {ui.GetItemID() ?? "(null)"}");

        ui.SeparatorText("Global Item Status");
        ui.Text($"AnyItemHovered: {ui.IsAnyItemHovered()}");
        ui.Text($"AnyItemActive: {ui.IsAnyItemActive()}");
        ui.Text($"AnyItemFocused: {ui.IsAnyItemFocused()}");

        ui.SeparatorText("Event Log");
        ui.TextColored(new UiColor(0xFF44FFAA), _lastStatus);

        ui.EndWindow();
    }

    private void RenderRectQueryWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(400f, 280f));
        ui.BeginWindow("Item Rect Queries");

        ui.SeparatorText("GetItemRect*");
        ui.Button("Measure This Button", new UiVector2(200f, 30f));
        var min = ui.GetItemRectMin();
        var max = ui.GetItemRectMax();
        var size = ui.GetItemRectSize();
        ui.Text($"  RectMin: ({min.X:0}, {min.Y:0})");
        ui.Text($"  RectMax: ({max.X:0}, {max.Y:0})");
        ui.Text($"  RectSize: ({size.X:0}, {size.Y:0})");

        // Visualize with a draw list rect around the button
        var drawList = ui.GetWindowDrawList();
        drawList.PushTexture(ui.WhiteTextureId);
        drawList.AddRect(
            new UiRect(min.X - 2f, min.Y - 2f, size.X + 4f, size.Y + 4f),
            new UiColor(0xFFFF4444), 0f, 1f);
        drawList.PopTexture();

        ui.SeparatorText("IsRectVisible");
        var visible = ui.IsRectVisible(new UiVector2(100f, 20f));
        ui.Text($"IsRectVisible(100,20): {visible}");

        var p1 = ui.GetCursorScreenPos();
        var p2 = new UiVector2(p1.X + 200f, p1.Y + 30f);
        var rectVisible = ui.IsRectVisible(p1, p2);
        ui.Text($"IsRectVisible(screen): {rectVisible}");

        ui.EndWindow();
    }

    private void RenderFocusWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(380f, 200f));
        ui.BeginWindow("Focus & Overlap");

        ui.SeparatorText("SetItemDefaultFocus");
        ui.Text("The second button gets default focus:");
        ui.Button("Button 1##focus");
        ui.Button("Button 2 (default focus)##focus");
        ui.SetItemDefaultFocus();
        ui.Button("Button 3##focus");

        ui.SeparatorText("SetNextItemAllowOverlap");
        ui.SetNextItemAllowOverlap();
        ui.Button("Overlappable Button", new UiVector2(200f, 30f));
        ui.Text("(This button allows overlap with neighboring items)");

        ui.EndWindow();
    }
}
