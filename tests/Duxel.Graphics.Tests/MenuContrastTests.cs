using Duxel.Core;

internal static class MenuContrastTests
{
    internal static void Run(UiForcedColorPalette palette, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        var surface = new UiRect(10, 10, 280, 50);
        var row = new UiRect(15, 15, 270, 40);
        var content = new UiMenuItemContent("Menu A", true, true)
        {
            HasIcons = true,
            Icon = UiMenuIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>(),
            SecondaryContent = "ZIP"
        };
        var options = UiFluentMenuPresentation.DefaultOptions;
        var state = "Rest";
        var reference = false;
        var interaction = default(UiButtonInteraction);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false,
            0, 0, [], [], default, default);
        var width = (int)fixture.FrameInfo.DisplaySize.X;
        uint Pixel(byte[] pixels, int x, int y) => BitConverter.ToUInt32(pixels, (y * width + x) * 4);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), new(11, 22, 33), ui.WhiteTextureId);
            ui.BeginDisabled(state == "Disabled");
            var context = new UiControlContext(ui);
            if (reference)
            {
                var colors = context.Theme.ForcedColors!.Value;
                var active = state is "Hover" or "SecondaryHover" or "Pressed" or "HoverFocus" or "PressedFocus";
                var foreground = state == "Disabled" ? colors.GrayText : active ? colors.HighlightText : colors.CanvasText;
                context.Frame(surface, colors.Canvas, colors.CanvasText, 4, 1);
                context.Frame(row, active ? colors.Highlight : colors.Canvas, default, 4, 0);
                UiMenuContentLayout.Paint(context, content, row, interaction, options,
                    foreground, foreground, foreground, foreground, 8, 6, wrapLabel: true);
                if (state is "Focus" or "HoverFocus" or "PressedFocus")
                    context.Frame(new(row.X - 2, row.Y - 2, row.Width + 4, row.Height + 4), default, colors.Highlight, 4, 2);
            }
            else
            {
                UiFluentMenuPresentation.PaintSurface(context, surface);
                UiFluentMenuPresentation.PaintItem(context, content, row, interaction, options);
                UiFluentMenuPresentation.PaintFocus(context, row, interaction, options);
            }
            ui.EndDisabled();
        }
        byte[] Frame(UiCompiledDesign design, UiForcedColorPalette colors, UiInputState input)
        {
            fixture.Context.SetTheme(colors.Apply(design.Theme));
            using var drawing = fixture.Build(Draw, input);
            return fixture.Render(drawing);
        }
        foreach (var rtl in new[] { false, true })
            foreach (var selected in new[] { false, true })
                foreach (var current in new[] { "Rest", "Hover", "SecondaryHover", "Pressed", "Disabled", "Focus", "HoverFocus", "PressedFocus" })
                {
                    options = options with { RightToLeft = rtl };
                    content = content with { Checked = selected };
                    state = current;
                    var active = state is "Hover" or "SecondaryHover" or "Pressed" or "HoverFocus" or "PressedFocus";
                    interaction = new(false, active || state == "Disabled", state is "Pressed" or "PressedFocus")
                    {
                        Focused = state is "Focus" or "HoverFocus" or "PressedFocus",
                        FocusVisible = state is "Focus" or "HoverFocus" or "PressedFocus"
                    };
                    var input = state == "SecondaryHover" ? idle with
                    {
                        MousePosition = new(rtl ? row.X + 12 : row.X + row.Width - 12, row.Y + row.Height / 2)
                    } : idle;
                    reference = false;
                    var dark = Frame(UiCompiledDesign.Windows11Dark, palette, input);
                    var light = Frame(UiCompiledDesign.Windows11, palette, input);
                    reference = true;
                    var expected = Frame(UiCompiledDesign.Windows11, palette, input);
                    reference = false;
                    var changedPalette = state == "Disabled" ? palette with { GrayText = new(170, 20, 210) }
                        : active ? palette with { HighlightText = new(170, 20, 210) }
                        : palette with { CanvasText = new(170, 20, 210) };
                    var changed = Frame(UiCompiledDesign.Windows11, changedPalette, input);
                    var recolored = false;
                    for (var y = (int)row.Y + 6; y < row.Y + row.Height - 6; y++)
                        for (var x = (int)row.X + 6; x < row.X + row.Width - 6; x++)
                            recolored |= Pixel(light, x, y) != Pixel(changed, x, y);
                    var label = $"Menu contrast / RTL={rtl}, checked={selected}, {state}";
                    check(Pixel(dark, 287, 35) == palette.Canvas.Rgba
                        && (Pixel(dark, 10, 35) == palette.CanvasText.Rgba || Pixel(dark, 11, 35) == palette.CanvasText.Rgba)
                        && Pixel(dark, 150, 19) == (active ? palette.Highlight : palette.Canvas).Rgba,
                        label + " / surface, border and row background use system roles");
                    check(dark.AsSpan().SequenceEqual(expected), label + " / complete GPU output equals independently specified literal roles");
                    check(dark.AsSpan().SequenceEqual(light), label + " / authored light/dark modes do not change system-colored output");
                    check(recolored, label + " / content pixels follow their enabled, disabled or highlighted foreground role");
                }
        RunPopup(fixture, palette, idle, check);
    }

    private static void RunPopup(PresentationReuseTests.Fixture fixture, UiForcedColorPalette palette, UiInputState idle,
        Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(tree);
        var selected = new UiState<bool>(true);
        using var menu = Fluent.Menu("Contrast popup", [UiMenuOption.Checkbox("a", "A", selected.Binding), new("b", "B", false)], _ => { });
        byte[] Frame(UiInputState? input = null)
        {
            using var drawing = fixture.Build(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 10));
                menu.Render(ui);
            }, input ?? idle);
            return fixture.Render(drawing);
        }
        fixture.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
        Frame();
        var trigger = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Contrast popup" && e.Node.Role == UiAccessibilityRole.Button);
        var opened = tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Expand, wait: false);
        Frame();
        check(opened == UiAccessibilityResult.Success && menu.IsOpen,
            "Menu contrast / actual bound popup opens through its published accessibility action");
        var entry = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "A");
        var bounds = entry.Node.Bounds;
        var pointerOverEntry = idle with { MousePosition = new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2) };
        var hovered = Frame(pointerOverEntry);
        var offset = ((int)(bounds.Y + 4) * (int)fixture.FrameInfo.DisplaySize.X + (int)(bounds.X + bounds.Width / 2)) * 4;
        check(BitConverter.ToUInt32(hovered, offset) == palette.Highlight.Rgba,
            "Menu contrast / actual popup pointer hover reaches the system Highlight background");
        var focused = tree.RequestAction(entry.Node.Id, entry.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        var focusPixels = Frame(pointerOverEntry);
        var ring = ((int)(bounds.Y + bounds.Height / 2) * (int)fixture.FrameInfo.DisplaySize.X + (int)bounds.X - 2) * 4;
        check(focused == UiAccessibilityResult.Success && BitConverter.ToUInt32(focusPixels, ring) == palette.Highlight.Rgba,
            "Menu contrast / actual focused hovered row paints its outer Highlight ring beyond the item viewport clip");
        var authoredTheme = UiCompiledDesign.Windows11Dark.Theme;
        fixture.Context.SetTheme(authoredTheme);
        var ordinaryPixels = Frame(pointerOverEntry);
        var switchedPalette = palette with { Highlight = new(131, 51, 107) };
        fixture.Context.SetTheme(switchedPalette.Apply(authoredTheme));
        var switchedPixels = Frame(pointerOverEntry);
        check(BitConverter.ToUInt32(switchedPixels, offset) == switchedPalette.Highlight.Rgba
            && BitConverter.ToUInt32(switchedPixels, ring) == switchedPalette.Highlight.Rgba,
            "Menu contrast / enabling a new palette repaints the open row and its focus ring");
        var switchedEntry = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "A");
        check(menu.IsOpen && selected.Value && switchedEntry.Node.Focused && switchedEntry.RuntimeId == entry.RuntimeId
            && switchedEntry.Node.Bounds == bounds,
            "Menu contrast / applying a palette preserves the open checkbox, focus identity and layout");
        fixture.Context.SetTheme(authoredTheme);
        check(Frame(pointerOverEntry).AsSpan().SequenceEqual(ordinaryPixels),
            "Menu contrast / disabling the palette restores the open menu's ordinary Vulkan pixels");
        fixture.Context.SetTheme(palette.Apply(authoredTheme));
        check(Frame(pointerOverEntry).AsSpan().SequenceEqual(focusPixels),
            "Menu contrast / reapplying the original palette restores the focused popup's exact pixels");
        entry = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "A");
        var toggled = tree.RequestAction(entry.Node.Id, entry.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        Frame();
        check(toggled == UiAccessibilityResult.Success && !selected.Value && menu.IsOpen,
            "Menu contrast / actual checkbox toggles its binding and keeps the contrasted popup open");
        fixture.Context.SetAccessibilityTree(null);
    }
}
