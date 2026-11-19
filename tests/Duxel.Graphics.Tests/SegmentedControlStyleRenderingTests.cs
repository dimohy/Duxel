using Duxel.Core;

internal static class SegmentedControlStyleRenderingTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(420, 160));
        var value = new UiState<string?>("b");
        using var widget = new UiSegmentedControlWidget("Colors", value.Binding,
            new UiSegmentOption[] { new("a", "First"), new("b", "Selected"), new("c", "Last", Unread : true) }, new(ReducedMotion : true));
        var disabled = false;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
            ui.BeginDisabled(disabled);
            try { widget.Render(ui); }
            finally { ui.EndDisabled(); }
        }
        // Literal sRGB source colors are checked against native readback bytes, not packed-color helpers.
        (uint Surface, uint Selected)[] reference =
        [
            (0xF0F0F0, 0x0F6CBD), (0xF0F0F0, 0x0F6CBD), (0x3D3D3D, 0x479EF5), (0x3D3D3D, 0x479EF5),
            (0xFFFFFF, 0x0F6CBD), (0xFFFFFF, 0x0F6CBD), (0x292929, 0x479EF5), (0x292929, 0x479EF5),
            (0x115EA3, 0xFFFFFF), (0x115EA3, 0xFFFFFF), (0x3D3D3D, 0x616161), (0x3D3D3D, 0x616161),
            (0xFFFFFF, 0x0F6CBD), (0xFFFFFF, 0x0F548C), (0x292929, 0x4D4D4D), (0x292929, 0x4D4D4D)
        ];
        var row = 0;
        foreach (var style in Enum.GetValues<UiSegmentedControlStyle>())
            foreach (var dark in new[] { false, true })
                foreach (var enabled in new[] { true, false })
                {
                    fixture.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                    disabled = !enabled;
                    widget.Options = widget.Options with { Style = style };
                    var pixels = fixture.Capture(Draw);
                    var source = reference[row++];
                    bool Pixel(int item, uint expected)
                    {
                        var bounds = widget.Layouts[item].Bounds;
                        var offset = (4 * fixture.LastCaptureSize.Width + (int)(bounds.X + bounds.Width / 2)) * 4;
                        return pixels[offset] == (byte)(expected >> 16) && pixels[offset + 1] == (byte)(expected >> 8)
                            && pixels[offset + 2] == (byte)expected && pixels[offset + 3] == 255;
                    }
                    check(Pixel(0, source.Surface) && Pixel(1, source.Selected),
                        style + " dark=" + dark + " enabled=" + enabled + " preserves official RGB channels in actual GPU pixels");
                    fixture.WriteCapturePng(Path.Combine(output, style + "-" + (dark ? "dark" : "light")
                        + (enabled ? "" : "-disabled") + ".png"), pixels);
                }
        var system = new UiForcedColorPalette(new(8, 8, 8), new(250, 250, 250), new(180, 180, 180),
            new(230, 240, 10), new(0, 0, 0), new(0, 220, 250), new(8, 8, 8), new(250, 250, 250));
        var design = UiCompiledDesign.Windows11Dark;
        fixture.Context.SetDesign(design with { Theme = system.Apply(design.Theme) });
        disabled = false;
        byte[]? first = null;
        foreach (var style in Enum.GetValues<UiSegmentedControlStyle>())
        {
            widget.Options = widget.Options with { Style = style };
            var pixels = fixture.Capture(Draw);
            first ??= pixels;
            check(first.AsSpan().SequenceEqual(pixels), style + " yields identical authoritative forced-color pixels");
        }
    }
}
