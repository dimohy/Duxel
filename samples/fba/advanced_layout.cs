// FBA: 고급 레이아웃 API 시연 — PushID, PushItemWidth, Cursor 조작, AlignTextToFramePadding,
//       Window 속성(Pos/Size/Constraints/BgAlpha/Scroll), PushStyleVar, PushTextWrapPos, ClipRect
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
        Title = "Duxel Advanced Layout Demo",
        Width = 1200,
        Height = 800
    },
    Screen = new AdvancedLayoutScreen()
});

public sealed class AdvancedLayoutScreen : UiScreen
{
    private int _frameCounter;
    private float _bgAlpha = 1.0f;
    private float _scrollTarget = 0f;
    private float _itemWidth = 200f;
    private float _wrapWidth = 300f;

    public override void Render(UiImmediateContext ui)
    {
        _frameCounter++;

        if(ui.BeginTabBar("sample-pages"))
        {
            if(ui.BeginTabItem("PushId",UiTabItemFlags.None))
            {
                RenderPushIdWindow(ui);
                ui.EndTabItem();
            }
            if(ui.BeginTabItem("ItemWidth",UiTabItemFlags.None))
            {
                RenderItemWidthWindow(ui);
                ui.EndTabItem();
            }
            if(ui.BeginTabItem("Cursor",UiTabItemFlags.None))
            {
                RenderCursorWindow(ui);
                ui.EndTabItem();
            }
            if(ui.BeginTabItem("StyleVar",UiTabItemFlags.None))
            {
                RenderStyleVarWindow(ui);
                ui.EndTabItem();
            }
            if(ui.BeginTabItem("WindowProps",UiTabItemFlags.None))
            {
                RenderWindowPropsWindow(ui);
                ui.EndTabItem();
            }
            if(ui.BeginTabItem("TextWrap",UiTabItemFlags.None))
            {
                RenderTextWrapWindow(ui);
                ui.EndTabItem();
            }
            if(ui.BeginTabItem("FontScale",UiTabItemFlags.None))
            {
                RenderFontScaleWindow(ui);
                ui.EndTabItem();
            }
            ui.EndTabBar();
        }
    }

    private void RenderPushIdWindow(UiImmediateContext ui)
    {
        ui.PushID("PushID / PopID"); ui.BeginGroup();

        ui.SeparatorText("ID Scoping");
        ui.Text("Same label, different IDs via PushID:");

        for (var i = 0; i < 3; i++)
        {
            ui.PushID(i);
            if (ui.Button("Click Me"))
                ui.Text($"Button {i} clicked!");
            ui.SameLine();
            ui.Text($"(ID scope: {i})");
            ui.PopID();
        }

        ui.SeparatorText("String ID Scoping");
        ui.PushID("group_a");
        ui.Button("Action");
        ui.SameLine();
        ui.Text("group_a/Action");
        ui.PopID();

        ui.PushID("group_b");
        ui.Button("Action");
        ui.SameLine();
        ui.Text("group_b/Action");
        ui.PopID();

        var idResult = ui.GetID("test_id");
        ui.Text($"GetID(\"test_id\"): {idResult}");

        ui.EndGroup(); ui.PopID();
    }

    private void RenderItemWidthWindow(UiImmediateContext ui)
    {
        ui.PushID("PushItemWidth"); ui.BeginGroup();

        ui.SeparatorText("Item Width Control");
        ui.SliderFloat("Target Width", ref _itemWidth, 80f, 400f, 0f, "0");

        ui.PushItemWidth(_itemWidth);
        var f1 = 0.5f;
        ui.SliderFloat("Width-pushed Slider", ref f1, 0f, 1f, 0f, "0.00");
        var i1 = 5;
        ui.DragInt("Width-pushed Drag", ref i1, 0.2f, 0, 10);
        ui.PopItemWidth();

        ui.SeparatorText("SetNextItemWidth");
        ui.SetNextItemWidth(120f);
        var f2 = 0.3f;
        ui.SliderFloat("120px Slider", ref f2, 0f, 1f, 0f, "0.00");

        ui.EndGroup(); ui.PopID();
    }

    private void RenderCursorWindow(UiImmediateContext ui)
    {
        ui.PushID("Cursor & Region Queries"); ui.BeginGroup();

        ui.SeparatorText("Cursor Position");
        var cursorPos = ui.GetCursorPos();
        var screenPos = ui.GetCursorScreenPos();
        var startPos = ui.GetCursorStartPos();
        ui.Text($"GetCursorPos: ({cursorPos.X:0}, {cursorPos.Y:0})");
        ui.Text($"GetCursorScreenPos: ({screenPos.X:0}, {screenPos.Y:0})");
        ui.Text($"GetCursorStartPos: ({startPos.X:0}, {startPos.Y:0})");
        ui.Text($"GetCursorPosX: {ui.GetCursorPosX():0}  Y: {ui.GetCursorPosY():0}");

        ui.SeparatorText("Content Region");
        var avail = ui.GetContentRegionAvail();
        var max = ui.GetContentRegionMax();
        var wMin = ui.GetWindowContentRegionMin();
        var wMax = ui.GetWindowContentRegionMax();
        ui.Text($"ContentRegionAvail: ({avail.X:0}, {avail.Y:0})");
        ui.Text($"ContentRegionMax: ({max.X:0}, {max.Y:0})");
        ui.Text($"WindowContentRegion: ({wMin.X:0},{wMin.Y:0}) to ({wMax.X:0},{wMax.Y:0})");

        ui.SeparatorText("Window Info");
        var wPos = ui.GetWindowPos();
        var wSize = ui.GetWindowSize();
        ui.Text($"WindowPos: ({wPos.X:0}, {wPos.Y:0})");
        ui.Text($"WindowSize: ({wSize.X:0}, {wSize.Y:0})  W:{ui.GetWindowWidth():0} H:{ui.GetWindowHeight():0}");
        ui.Text($"Appearing: {ui.IsWindowAppearing()}  Collapsed: {ui.IsWindowCollapsed()}");
        ui.Text($"Focused: {ui.IsWindowFocused()}  Hovered: {ui.IsWindowHovered()}");

        ui.SeparatorText("AlignTextToFramePadding");
        ui.AlignTextToFramePadding();
        ui.Text("Aligned text");
        ui.SameLine();
        ui.Button("Next to it");

        ui.EndGroup(); ui.PopID();
    }

    private void RenderStyleVarWindow(UiImmediateContext ui)
    {
        ui.PushID("PushStyleVar"); ui.BeginGroup();

        ui.SeparatorText("Style Var Modifications");

        ui.Text("Normal spacing:");
        ui.Button("A");
        ui.SameLine();
        ui.Button("B");
        ui.SameLine();
        ui.Button("C");

        ui.PushStyleVar(UiStyleVar.ItemSpacing, new UiVector2(20f, 10f));
        ui.Text("Wide spacing (20, 10):");
        ui.Button("A##wide");
        ui.SameLine();
        ui.Button("B##wide");
        ui.SameLine();
        ui.Button("C##wide");
        ui.PopStyleVar();

        ui.PushStyleVar(UiStyleVar.FramePadding, new UiVector2(12f, 8f));
        ui.Text("Large frame padding (12, 8):");
        ui.Button("Padded Button");
        var f = 0.5f;
        ui.SliderFloat("Padded Slider", ref f, 0f, 1f, 0f, "0.00");
        ui.PopStyleVar();

        ui.PushStyleVar(UiStyleVar.WindowPadding, new UiVector2(24f, 24f));
        if (ui.BeginChild("styled_child", new UiVector2(350f, 60f), true))
        {
            ui.Text("Child with 24px window padding");
        }
        ui.EndChild();
        ui.PopStyleVar();

        ui.EndGroup(); ui.PopID();
    }

    private void RenderWindowPropsWindow(UiImmediateContext ui)
    {
        ui.PushID("Panel Properties"); ui.BeginGroup();

        ui.SeparatorText("SetNextPanelBgAlpha");
        ui.SliderFloat("BG Alpha", ref _bgAlpha, 0.1f, 1.0f, 0f, "0.00");

        ui.SetNextPanelBgAlpha(_bgAlpha);
        ui.SetNextPanelSize(new UiVector2(250f, 100f));
        ui.BeginPanel("Alpha Panel");
        ui.Text($"Alpha: {_bgAlpha:0.00}");
        ui.Text("Translucent panel!");
        ui.EndPanel();

        ui.SeparatorText("Scroll Control");
        ui.SliderFloat("ScrollY Target", ref _scrollTarget, 0f, 500f, 0f, "0");
        if (ui.Button("Go to Scroll"))
            ui.SetNextWindowScroll(0f, _scrollTarget);

        if (ui.BeginChild("scroll_child", new UiVector2(350f, 80f), true))
        {
            for (var i = 0; i < 50; i++)
                ui.Text($"Scroll line {i}");
            ui.Text($"ScrollX: {ui.GetScrollX():0}  ScrollY: {ui.GetScrollY():0}");
        }
        ui.EndChild();

        ui.EndGroup(); ui.PopID();
    }

    private void RenderTextWrapWindow(UiImmediateContext ui)
    {
        ui.PushID("PushTextWrapPos"); ui.BeginGroup();

        ui.SliderFloat("Wrap Width", ref _wrapWidth, 100f, 500f, 0f, "0");

        ui.PushTextWrapPos(_wrapWidth);
        ui.Text("This is a long text that demonstrates PushTextWrapPos. " +
                "The text will wrap at the specified position instead of the default window edge. " +
                "Useful for controlling text layout precisely.");
        ui.PopTextWrapPos();

        ui.Separator();
        ui.Text("(Text above wraps at specified width)");

        ui.EndGroup(); ui.PopID();
    }

    private void RenderFontScaleWindow(UiImmediateContext ui)
    {
        ui.PushID("Font Scale"); ui.BeginGroup();

        ui.SeparatorText("Font Metrics");
        ui.Text($"Current font size: {ui.GetFontSize():0.0}");
        ui.Text($"Line height: {ui.GetTextLineHeight():0.0}");
        ui.Text($"Line+spacing: {ui.GetTextLineHeightWithSpacing():0.0}");
        ui.Text($"Frame height: {ui.GetFrameHeight():0.0}");
        ui.Button("Scaled Button");

        ui.EndGroup(); ui.PopID();
    }
}
