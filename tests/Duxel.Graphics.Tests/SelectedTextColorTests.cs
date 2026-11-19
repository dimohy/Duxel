using Duxel.Core;

internal static class SelectedTextColorTests
{
    internal static void Run(UiForcedColorPalette palette, Action<bool, string> check)
    {
        foreach (var mode in new[] { "Input", "Textarea", "Hard lines", "Wrapped Textarea" })
        {
            using var fixture = new PresentationReuseTests.Fixture(1, true);
            var text = new UiState<string>("ABC DEF GHI\nJKL MNO");
            using var editor = mode == "Input"
                ? (UiWidget)Fluent.Input(string.Empty, new UiState<string>("ABC DEF GHI").Binding, 256, 200)
                : Fluent.Textarea(string.Empty, text.Binding, 256, mode == "Wrapped Textarea" ? 80 : 200, 100);
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false,
                0, 0, [], [], default, default);
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 10));
                if (mode == "Hard lines")
                {
                    ui.SetNextItemWidth(200);
                    var value = text.Value;
                    ui.InputTextMultiline("##hard-lines", ref value, 256, 100);
                }
                else
                    editor.Render(ui);
            }
            byte[] Frame(UiInputState? input = null)
            {
                using var data = fixture.Build(Draw, input ?? idle);
                return fixture.Render(data);
            }
            byte[] Key(UiKey key, KeyModifiers modifiers = default)
                => Frame(idle with
                {
                    KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)],
                    Modifiers = modifiers
                });
            fixture.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
            Frame();
            Frame(idle with { MousePosition = new(30, 24), LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = new(30, 24), LeftMouseReleased = true });
            var selected = Key(UiKey.A, KeyModifiers.Ctrl);
            var width = (int)fixture.FrameInfo.DisplaySize.X;
            var foreground = 0;
            var background = 0;
            for (var y = 16; y < 34; y++)
                for (var x = 24; x < 60; x++)
                {
                    var color = BitConverter.ToUInt32(selected, (y * width + x) * 4);
                    if (color == palette.HighlightText.Rgba) foreground++;
                    if (color == palette.Highlight.Rgba) background++;
                }
            check(foreground > 0 && background > 0,
                $"Selection foreground / {mode} Ctrl+A paints system HighlightText over Highlight in actual GPU output");
            if (mode == "Wrapped Textarea")
            {
                var thirdRow = false;
                for (var y = 60; y < 78; y++)
                    for (var x = 24; x < 60; x++)
                        thirdRow |= BitConverter.ToUInt32(selected, (y * width + x) * 4) == palette.HighlightText.Rgba;
                check(thirdRow,
                    "Selection foreground / narrow Textarea visibly wraps the two source lines into at least three selected visual rows");
            }
            fixture.Context.SetTheme((palette with { CanvasText = new(190, 30, 80) }).Apply(UiCompiledDesign.Windows11Dark.Theme));
            var changed = Frame();
            var identical = true;
            for (var y = 16; y < 34; y++)
                for (var x = 24; x < 60; x++)
                    identical &= BitConverter.ToUInt32(selected, (y * width + x) * 4)
                        == BitConverter.ToUInt32(changed, (y * width + x) * 4);
            check(identical,
                $"Selection foreground / {mode} selected glyph edges do not retain the ordinary foreground under their antialiasing");
            Key(UiKey.Home, KeyModifiers.Ctrl);
            var partial = Key(UiKey.RightArrow, KeyModifiers.Shift);
            var selectedCount = 0;
            var ordinaryCount = 0;
            for (var y = 16; y < 34; y++)
                for (var x = 16; x < 78; x++)
                {
                    var color = BitConverter.ToUInt32(partial, (y * width + x) * 4);
                    if (color == palette.HighlightText.Rgba) selectedCount++;
                    if (color == new UiColor(190, 30, 80).Rgba) ordinaryCount++;
                }
            check(selectedCount > 0 && ordinaryCount > 0,
                $"Selection foreground / {mode} Shift+Right preserves distinct selected and unselected glyph colors"
                + $" / selected pixels={selectedCount}, ordinary pixels={ordinaryCount}, selection="
                + (fixture.Context.State.ActiveId is { } active ? fixture.Context.State.GetSelection(active).ToString() : "inactive"));
        }
    }
}
