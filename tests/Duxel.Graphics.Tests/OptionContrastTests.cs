using Duxel.Core;

internal static class OptionContrastTests
{
    internal static void Run(UiForcedColorPalette palette, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        var surface = new UiRect(10, 10, 280, 50);
        var row = new UiRect(15, 15, 270, 40);
        var content = new UiMenuItemContent("Option A", true, true)
        {
            HasIcons = true,
            Icon = UiMenuIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>(),
            SecondaryContent = "ZIP"
        };
        var options = UiFluentMenuPresentation.DefaultOptions;
        var interaction = default(UiButtonInteraction);
        var disabled = false;
        var reference = false;
        var combobox = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false,
            0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.BeginDisabled(disabled);
            var context = new UiControlContext(ui);
            UiFluentDropdownPresentation.PaintSurface(context, surface);
            if (reference && !context.Theme.ForcedColors.HasValue)
                PaintAuthored(context);
            else if (reference)
            {
                var colors = context.Theme.ForcedColors!.Value;
                var foreground = disabled ? colors.GrayText : colors.CanvasText;
                context.Frame(row, !disabled && (interaction.Hovered || interaction.Held) ? colors.Canvas : default, default, 4, 0);
                UiMenuContentLayout.Paint(context, content with { Checked = null }, row, interaction, options,
                    foreground, foreground, foreground, foreground, 8, 6);
                var slot = UiMenuContentLayout.Arrange(context, content, row, options.RightToLeft, 8, 6).Checkmark;
                if (content.MultipleSelection)
                    context.Frame(slot, content.Checked == true ? colors.Canvas : default, foreground, 2, 1);
                var glyph = content.MultipleSelection ? new UiRect(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4) : slot;
                if (content.Checked == true)
                    UiFluentCheckboxCheck16.Paint(context, glyph, foreground, interaction);
                if (!disabled && interaction.FocusVisible)
                    context.Frame(new(row.X - 2, row.Y - 2, row.Width + 4, row.Height + 4), default, colors.CanvasText, 4, 2);
            }
            else if (combobox)
            {
                PaintOption<UiFluentComboboxPresentation>(context);
            }
            else
            {
                PaintOption<UiFluentDropdownPresentation>(context);
            }
            ui.EndDisabled();
        }
        void PaintOption<TPresentation>(UiControlContext context) where TPresentation : IUiMenuPresentation
        {
            TPresentation.PaintItem(context, content, row, interaction, options);
            TPresentation.PaintFocus(context, row, interaction, options);
        }
        void PaintAuthored(UiControlContext context)
        {
            var state = disabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
                : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
            var original = UiFluentButtonPalette.Resolve(context.FluentColorScheme, state);
            context.Frame(row, interaction.Held || interaction.Hovered ? original.Background : default, default, 4, 0);
            UiMenuContentLayout.Paint(context, content with { Checked = null }, row, interaction, options,
                original.Foreground, original.Foreground,
                UiFluentMenuPalette.Secondary(context.FluentColorScheme, false, !disabled),
                UiFluentMenuPalette.Secondary(context.FluentColorScheme, true, !disabled), 8, 6);
            if (!disabled && interaction.FocusVisible)
                context.FocusOutline(row, 4);
            var slot = UiMenuContentLayout.Arrange(context, content, row, options.RightToLeft, 8, 6).Checkmark;
            var mark = UiFluentCheckboxPalette.Resolve(context.FluentColorScheme, state, content.Checked == true);
            if (content.MultipleSelection)
                context.Frame(slot, content.Checked == true ? mark.Background : default, mark.Border, 2, 1);
            var glyph = content.MultipleSelection ? new UiRect(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4) : slot;
            if (content.Checked == true)
                UiFluentCheckboxCheck16.Paint(context, glyph, content.MultipleSelection ? mark.Indicator : original.Foreground, interaction);
        }
        byte[] Frame(UiCompiledDesign design, UiForcedColorPalette? colors)
        {
            fixture.Context.SetTheme(colors?.Apply(design.Theme) ?? design.Theme);
            using var drawing = fixture.Build(Draw, idle);
            return fixture.Render(drawing);
        }
        foreach (var rtl in new[] { false, true })
            foreach (var multiple in new[] { false, true })
                foreach (var selected in new[] { false, true })
                    foreach (var state in new[] { "Rest", "Hover", "Pressed", "Disabled", "Focus", "HoverFocus" })
                    {
                        options = options with { RightToLeft = rtl };
                        content = content with { MultipleSelection = multiple, Checked = selected };
                        disabled = state == "Disabled";
                        interaction = new(false, state is "Hover" or "Pressed" or "Disabled" or "HoverFocus", state == "Pressed")
                        {
                            FocusVisible = state is "Focus" or "HoverFocus",
                            Focused = state is "Focus" or "HoverFocus"
                        };
                        reference = false;
                        combobox = false;
                        var dark = Frame(UiCompiledDesign.Windows11Dark, palette);
                        var light = Frame(UiCompiledDesign.Windows11, palette);
                        combobox = true;
                        var combo = Frame(UiCompiledDesign.Windows11, palette);
                        reference = true;
                        var expected = Frame(UiCompiledDesign.Windows11, palette);
                        reference = false;
                        var changed = Frame(UiCompiledDesign.Windows11, disabled
                            ? palette with { GrayText = new(170, 20, 210) }
                            : palette with { CanvasText = new(170, 20, 210) });
                        var recolored = false;
                        var width = (int)fixture.FrameInfo.DisplaySize.X;
                        for (var y = (int)row.Y + 6; y < row.Y + row.Height - 6; y++)
                            for (var x = (int)row.X + 6; x < row.X + row.Width - 6; x++)
                            {
                                var offset = (y * width + x) * 4;
                                recolored |= BitConverter.ToUInt32(light, offset) != BitConverter.ToUInt32(changed, offset);
                            }
                        var label = $"Option contrast / RTL={rtl}, multiple={multiple}, selected={selected}, {state}";
                        check(dark.AsSpan().SequenceEqual(expected), label + " / entire GPU image equals independent literal system roles");
                        check(dark.AsSpan().SequenceEqual(light), label + " / authored light and dark do not alter system roles");
                        check(light.AsSpan().SequenceEqual(combo), label + " / Dropdown and Combobox share the option contract");
                        check(recolored, label + " / option content follows its own system role independently of the surface border");
                    }
        foreach (var rtl in new[] { false, true })
            foreach (var multiple in new[] { false, true })
                foreach (var selected in new[] { false, true })
                    foreach (var state in new[] { "Rest", "Hover", "Pressed", "Disabled", "Focus", "HoverFocus" })
                        foreach (var design in new[] { UiCompiledDesign.Windows11, UiCompiledDesign.Windows11Dark })
                        {
                            options = options with { RightToLeft = rtl };
                            content = content with { MultipleSelection = multiple, Checked = selected };
                            disabled = state == "Disabled";
                            interaction = new(false, state is "Hover" or "Pressed" or "Disabled" or "HoverFocus", state == "Pressed")
                            {
                                FocusVisible = state is "Focus" or "HoverFocus",
                                Focused = state is "Focus" or "HoverFocus"
                            };
                            combobox = false;
                            reference = false;
                            var actual = Frame(design, null);
                            combobox = true;
                            var combo = Frame(design, null);
                            reference = true;
                            var original = Frame(design, null);
                            var label = $"Ordinary option / {design.Theme.FluentColorScheme}, RTL={rtl}, multiple={multiple}, selected={selected}, {state}";
                            check(actual.AsSpan().SequenceEqual(original), label + " / whole GPU image preserves the original authored painter");
                            check(actual.AsSpan().SequenceEqual(combo), label + " / Dropdown and Combobox retain the same ordinary output");
                        }
        RunPopups(fixture, palette, idle, check);
    }

    private static void RunPopups(PresentationReuseTests.Fixture fixture, UiForcedColorPalette palette, UiInputState idle,
        Action<bool, string> check)
    {
        foreach (var combo in new[] { false, true })
            foreach (var multiple in new[] { false, true })
            {
                using var tree = new UiAccessibilityTree();
                fixture.Context.SetAccessibilityTree(tree);
                var single = new UiState<string?>("a");
                var many = new UiState<string[]>(["a"]);
                var text = new UiState<string>("");
                UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta"), new("d", "Disabled", false)];
                using UiWidget widget = combo
                    ? multiple ? Fluent.Combobox("Choice", text.Binding, many.Binding, items) : Fluent.Combobox("Choice", text.Binding, single.Binding, items)
                    : multiple ? Fluent.Dropdown("Choice", many.Binding, items) : Fluent.Dropdown("Choice", single.Binding, items);
                byte[] Frame(UiInputState? input = null)
                {
                    using var drawing = fixture.Build(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        ui.SetCursorScreenPos(new(10, 10));
                        widget.Render(ui);
                    }, input ?? idle);
                    return fixture.Render(drawing);
                }
                UiAccessibilityElement Find(string name, UiAccessibilityRole role)
                    => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role && !e.Node.Offscreen);
                uint Pixel(byte[] pixels, float x, float y)
                    => BitConverter.ToUInt32(pixels, ((int)y * (int)fixture.FrameInfo.DisplaySize.X + (int)x) * 4);
                fixture.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
                Frame();
                var trigger = Find("Choice", UiAccessibilityRole.ComboBox);
                var opened = tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Expand, wait: false);
                Frame();
                var entry = Find("Beta", UiAccessibilityRole.Option);
                var bounds = entry.Node.Bounds;
                var label = $"Option contrast / actual {(combo ? "Combobox" : "Dropdown")}, multiple={multiple}";
                check(opened == UiAccessibilityResult.Success, label + " / bound popup opens through accessibility");
                var hovered = Frame(idle with { MousePosition = new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2) });
                check(Pixel(hovered, bounds.X + bounds.Width / 2, bounds.Y + 4) == palette.Canvas.Rgba,
                    label + " / hovered option stays Canvas rather than Menu Highlight");
                var focused = tree.RequestAction(entry.Node.Id, entry.RuntimeId, UiAccessibilityAction.Focus, wait: false);
                var focus = Frame();
                check(focused == UiAccessibilityResult.Success && Pixel(focus, bounds.X + bounds.Width / 2, bounds.Y - 1) == palette.CanvasText.Rgba,
                    label + " / outer CanvasText focus survives the popup row clip");
                var chosen = tree.RequestAction(entry.Node.Id, entry.RuntimeId, UiAccessibilityAction.Select, wait: false);
                Frame();
                check(chosen == UiAccessibilityResult.Success && (multiple ? many.Value.Contains("b") : single.Value == "b"),
                    label + " / actual option action changes the consumer binding");
                fixture.Context.SetAccessibilityTree(null);
            }
    }
}
