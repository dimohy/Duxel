// FBA: Image 위젯 + 고급 Popup/Tooltip API 시연 — Image, ImageWithBg, ImageButton,
//       OpenPopupOnItemClick, BeginPopupContextVoid, IsPopupOpen, TextLink,
//       BeginItemTooltip, SetTooltipV, TreeNodeV, ListBoxHeader/Footer
#:property TargetFramework=net10.0
#:package Duxel.Windows.App@*-*

using System;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Image & Popup Demo",
        Width = 1000,
        Height = 700
    },
    Screen = new ImageAndPopupScreen()
});

public sealed class ImageAndPopupScreen : UiScreen
{
    private int _frameCounter;
    private int _imageButtonClicks;
    private int _linkClicks;

    public override void Render(UiImmediateContext ui)
    {
        _frameCounter++;

        RenderImageWindow(ui);
        RenderAdvancedPopupWindow(ui);
        RenderTextLinkWindow(ui);
        RenderAdvancedTreeWindow(ui);
        RenderListBoxCustomWindow(ui);
    }

    private void RenderImageWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(420f, 300f));
        ui.BeginWindow("Image Widgets");

        ui.SeparatorText("Image (using WhiteTexture)");
        // Image uses a texture ID — we use the built-in white texture as a placeholder
        ui.Image(ui.WhiteTextureId, new UiVector2(64f, 64f), new UiColor(0xFF4488FF));
        ui.SameLine();
        ui.Text("Image: 64x64 tinted blue");

        ui.SeparatorText("ImageWithBg");
        ui.ImageWithBg(ui.WhiteTextureId, new UiVector2(64f, 64f), new UiColor(0xFF333333), new UiColor(0xFFFF8844));
        ui.SameLine();
        ui.Text("ImageWithBg: dark bg + orange tint");

        ui.SeparatorText("ImageButton");
        if (ui.ImageButton("img_btn", ui.WhiteTextureId, new UiVector2(48f, 48f), new UiColor(0xFF44FF88)))
            _imageButtonClicks++;
        ui.SameLine();
        ui.Text($"ImageButton clicked: {_imageButtonClicks}x");

        ui.EndWindow();
    }

    private void RenderAdvancedPopupWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(420f, 300f));
        ui.BeginWindow("Advanced Popups");

        ui.SeparatorText("OpenPopupOnItemClick");
        ui.Button("Right-click for popup##opoc");
        ui.OpenPopupOnItemClick("auto_context");
        if (ui.BeginPopup("auto_context"))
        {
            ui.Text("Auto-opened popup!");
            ui.MenuItem("Action A##opoc");
            ui.MenuItem("Action B##opoc");
            if (ui.Button("Close##opoc"))
                ui.CloseCurrentPopup();
            ui.EndPopup();
        }

        ui.SeparatorText("BeginPopupContextVoid");
        ui.Text("Right-click empty area (void context):");
        if (ui.BeginPopupContextVoid("void_ctx"))
        {
            ui.MenuItem("Void Action 1");
            ui.MenuItem("Void Action 2");
            ui.EndPopup();
        }

        ui.SeparatorText("IsPopupOpen");
        ui.Text($"'auto_context' open: {ui.IsPopupOpen("auto_context")}");
        ui.Text($"'void_ctx' open: {ui.IsPopupOpen("void_ctx")}");

        ui.SeparatorText("Advanced Tooltip");
        ui.Button("Hover for SetTooltipV");
        if (ui.IsItemHovered())
            ui.SetTooltipV("Frame: {0}  Time: {1:0.00}s", _frameCounter, ui.GetTime());

        ui.Button("Hover for BeginItemTooltip");
        if (ui.BeginItemTooltip())
        {
            ui.Text("Rich item tooltip:");
            ui.BulletText("Auto-detected hover");
            ui.BulletText("No explicit IsItemHovered check");
            ui.EndTooltip();
        }

        ui.EndWindow();
    }

    private void RenderTextLinkWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(380f, 180f));
        ui.BeginWindow("TextLink / TextLinkOpenURL");

        ui.SeparatorText("TextLink (clickable text)");
        if (ui.TextLink("Click this link"))
            _linkClicks++;
        ui.SameLine();
        ui.Text($"({_linkClicks} clicks)");

        ui.SeparatorText("TextLinkOpenURL");
        ui.TextLinkOpenURL("Open Duxel GitHub", "https://github.com/dimohy/Duxel");
        ui.Text("(Opens URL in system browser)");

        ui.EndWindow();
    }

    private void RenderAdvancedTreeWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(380f, 220f));
        ui.BeginWindow("Advanced Tree");

        ui.SeparatorText("TreeNodeV (format string)");
        if (ui.TreeNodeV("Node {0}", "Alpha"))
        {
            ui.Text("Child of Alpha");
            if (ui.TreeNodeExV("Sub {0}", UiTreeNodeFlags.DefaultOpen, "Beta"))
            {
                ui.BulletText("Leaf item");
                ui.TreePop();
            }
            ui.TreePop();
        }

        ui.SeparatorText("SetNextItemOpen");
        ui.SetNextItemOpen(true);
        if (ui.TreeNode("Force-opened node"))
        {
            ui.Text("This node starts opened via SetNextItemOpen");
            ui.Text($"TreeNodeToLabelSpacing: {ui.GetTreeNodeToLabelSpacing():0}px");
            ui.TreePop();
        }

        ui.EndWindow();
    }

    private void RenderListBoxCustomWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(380f, 200f));
        ui.BeginWindow("ListBox Custom");

        ui.SeparatorText("ListBoxHeader / ListBoxFooter");
        if (ui.ListBoxHeader("Custom List", new UiVector2(0f, 0f), 5))
        {
            for (var i = 0; i < 10; i++)
            {
                ui.Selectable($"Custom Item {i}", i == 3);
            }
            ui.ListBoxFooter();
        }

        ui.EndWindow();
    }
}
