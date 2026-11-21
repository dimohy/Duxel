using Duxel.Core;

internal static class ProgressContrastTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        using var tree = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(tree);
        using var ring = Fluent.CircularProgressIndicator(.5f);
        ring.Size = new(40, 40);
        ring.AccessibleName = "Synchronization";
        using var indicator = Fluent.ProgressIndicator(new(Label : "Upload", Description : "Check your connection and retry.",
            PercentComplete : .25f, Width : 160, BarHeight : 6, ValueText : "Upload failed")
        {
            Color = UiProgressColor.Error
        });

        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var theme = new UiControlContext(ui).Theme;
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                theme.WindowBg, ui.WhiteTextureId);
            ui.SetCursorScreenPos(new(16, 16));
            ui.ProgressBar<UiFluentProgressPresentation>("Known", new(.25f, Shape : UiProgressShape.Square, Color : UiProgressColor.Error), new(160, 8));
            ui.SetCursorScreenPos(new(16, 34));
            ui.ProgressBar<UiFluentProgressPresentation>("Waiting", new(null, Shape : UiProgressShape.Square), new(160, 8));
            ui.SetCursorScreenPos(new(16, 52));
            ui.ProgressBar<UiFluentProgressPresentation>("Pulsing", new(null, Shape : UiProgressShape.Square, ReducedMotion : true), new(160, 8));
            ui.SetCursorScreenPos(new(16, 76));
            ring.Render(ui);
            ui.SetCursorScreenPos(new(90, 76));
            indicator.Render(ui);
        }

        var darkPalette = new UiForcedColorPalette(new(0, 0, 34), new(255, 255, 255), new(160, 160, 160),
            new(255, 255, 0), new(0, 0, 0), new(0, 255, 255), new(0, 0, 34), new(255, 255, 255));
        var lightPalette = new UiForcedColorPalette(new(255, 255, 255), new(0, 0, 0), new(90, 90, 90),
            new(0, 0, 255), new(255, 255, 255), new(0, 128, 128), new(255, 255, 255), new(0, 0, 0));
        foreach (var (name, palette, design) in new[]
        {
            ("dark", darkPalette, UiCompiledDesign.Windows11Dark),
            ("light", lightPalette, UiCompiledDesign.Windows11)
        })
        {
            fixture.Context.SetDesign(design with { Theme = palette.Apply(design.Theme) });
            var phase = fixture.Context.State.TimeSeconds % 3;
            fixture.Context.State.AdvanceTime((float)((3 + 1.5 - phase - fixture.FrameInfo.DeltaTime) % 3));
            var pixels = fixture.Capture(Draw);
            fixture.WriteCapturePng(Path.Combine(output, name + ".png"), pixels);
            var width = fixture.LastCaptureSize.Width;
            uint Pixel(int x, int y) => BitConverter.ToUInt32(pixels, (y * width + x) * 4);
            check(Pixel(30, 20) == palette.Highlight.Rgba && Pixel(80, 20) == palette.CanvasText.Rgba,
                $"Progress contrast / {name} / known error fill and track use Highlight and CanvasText");
            check(Pixel(17, 38) == palette.CanvasText.Rgba && Pixel(94, 38) != palette.CanvasText.Rgba
                && Pixel(94, 38) != design.Theme.ProgressBarFill.Rgba,
                $"Progress contrast / {name} / waiting gradient uses the forced progress colors");
            check(Pixel(30, 56) == Pixel(150, 56) && Pixel(30, 56) == palette.Highlight.Rgba,
                $"Progress contrast / {name} / reduced motion pulses across the system-color track");
            check(Pixel(53, 96) == palette.CanvasText.Rgba && Pixel(36, 96) == palette.Canvas.Rgba,
                $"Progress contrast / {name} / circular progress keeps system foreground and clear center");

            var snapshots = tree.CaptureSnapshot().Elements.Select(item => item.Node).ToArray();
            var upload = snapshots.Single(node => node.Name == "Upload" && node.Role == UiAccessibilityRole.ProgressBar);
            var waiting = snapshots.Single(node => node.Name == "Waiting" && node.Role == UiAccessibilityRole.ProgressBar);
            check(upload.ReadOnly && !upload.Focusable && upload.Range?.Value == 25 && upload.ItemStatus == "Upload failed"
                && upload.GetHelpText() == "Check your connection and retry." && waiting.Busy && waiting.Range is null,
                $"Progress contrast / {name} / range and error or waiting status remain accessible");
        }
        CheckThemeColors(fixture, darkPalette, output, check);
    }

    private static void CheckThemeColors(PresentationReuseTests.Fixture fixture, UiForcedColorPalette forcedPalette,
        string output, Action<bool, string> check)
    {
        using var progress = Fluent.ProgressBar(.5f);
        progress.Shape = UiProgressShape.Square;
        progress.Size = new(160, 4);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(16, 16));
            progress.Render(ui);
        }

        void Verify(string name, UiCompiledDesign design, UiColor expectedTrack, UiColor expectedFill)
        {
            fixture.Context.SetDesign(design);
            var pixels = fixture.Capture(Draw);
            fixture.WriteCapturePng(Path.Combine(output, name + ".png"), pixels);
            var width = fixture.LastCaptureSize.Width;
            uint Pixel(int x) => BitConverter.ToUInt32(pixels, (17 * width + x) * 4);
            check(Pixel(30) == expectedFill.Rgba && Pixel(150) == expectedTrack.Rgba, "Progress palette / " + name);
        }

        var light = UiCompiledDesign.Windows11;
        var dark = UiCompiledDesign.Windows11Dark;
        Verify("default-light", light, new(230, 230, 230), new(15, 108, 189));
        Verify("default-dark", dark, new(51, 51, 51), new(71, 158, 245));

        var customTrack = new UiColor(43, 19, 57);
        var customFill = new UiColor(228, 91, 193);
        var theme = dark.Theme;
        theme.ProgressBarBg = customTrack;
        theme.ProgressBarFill = customFill;
        Verify("custom", dark with { Theme = theme }, customTrack, customFill);
        theme = forcedPalette.Apply(theme);
        theme.ProgressBarBg = customTrack;
        theme.ProgressBarFill = customFill;
        Verify("forced-over-custom", dark with { Theme = theme }, forcedPalette.CanvasText, forcedPalette.Highlight);
    }
}
