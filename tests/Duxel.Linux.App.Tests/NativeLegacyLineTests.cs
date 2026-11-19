using Duxel.Core;
using Duxel.Platform.Linux;

internal static class NativeLegacyLineTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        tree.FontNameLanguage = "en";
        using var backend = new LinuxPlatformTextBackend();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetPlatformTextBackend(backend);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath(primary);
        context.SetDirectTextSecondaryFontPath(secondary);
        context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary)
        {
            Monospace = primary
        });
        context.SetAccessibilityTree(tree);
        var value = "aa\nbbb\nc";
        var width = 250f;
        var legacy = false;
        var settings = UiTextSettings.Default;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui =>
        {
            if (legacy)
                ui.InputText("Lines", ref value, 1000);
            else
                ui.InputTextMultiline<UiFluentMultilineInputBehavior,
                UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>("Lines", ref value, 1000, width, 180);
        });
        UiAccessibilityTextLayout Layout()
        {
            context.NewFrame(new(.1f, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), settings);
            context.Render(screen);
            context.GetDrawData().ReleasePooled();
            return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Lines").Node.TextLayout
                ?? throw new InvalidOperationException("Actual Linux multiline layout is missing.");
        }
        var layout = Layout();
        UiAccessibilityTextAppearance Appearance() => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Lines").Node.TextAppearance!;
        var appearance = Appearance();
        check(appearance.FontRunCount == 3 && appearance.GetFontRangeAt(4).Font?.FamilyName == "DejaVu Sans"
            && appearance.GetFontRangeAt(2) == (2, 3, null),
            "Actual Linux multiline paint publishes selected font runs and preserves unpainted newline gaps");
        Layout();
        check(ReferenceEquals(appearance, Appearance()), "Unchanged actual font painting reuses its immutable snapshot and selection map");
        tree.FontNameLanguage = "zz";
        Layout();
        check(Appearance().GetFontRangeAt(0).Font is { FamilyName: null, Weight: 400 }
            && appearance.GetFontRangeAt(0).Font?.FamilyName == "DejaVu Sans",
            "Actual font name language changes rebuild metadata without mutating prior snapshots or inventing an English name");
        tree.FontNameLanguage = "en";
        check(layout.LineCount == 3 && layout.GetLine(0) == new UiAccessibilityTextLine(0, 2, 3)
            && layout.GetLine(1) == new UiAccessibilityTextLine(3, 6, 7) && layout.GetLine(2) == new UiAccessibilityTextLine(7, 8, 8),
            "Actual Linux multiline paint layout distinguishes source ends and separator-inclusive next starts");
        var starts = new LinuxTextBoundarySequence(layout, false);
        var ends = new LinuxTextBoundarySequence(layout, true);
        check(starts.GetRange(4, -1) == (0, 3) && starts.GetRange(4, 0) == (3, 7) && starts.GetRange(4, 1) == (7, 8),
            "Actual painted line-start intervals include newline suffixes in all three directions");
        check(ends.GetRange(4, -1) == (0, 2) && ends.GetRange(4, 0) == (2, 6) && ends.GetRange(4, 1) == (6, 8),
            "Actual painted line-end intervals include newline prefixes in all three directions");
        check(starts.GetRange(2, 0) == (0, 3) && ends.GetRange(2, 0) == (2, 6)
            && starts.GetRange(8, 0) == (7, 8) && ends.GetRange(8, 0) == (8, 8),
            "Exact newline and document-end offsets distinguish start and end boundaries");
        value = "a\n\n";
        var blank = Layout();
        check(blank.LineCount == 3 && new LinuxTextBoundarySequence(blank, false).GetRange(3, 0) == (3, 3)
            && new LinuxTextBoundarySequence(blank, true).GetRange(1, 0) == (1, 2),
            "Blank and trailing painted lines preserve ordered source boundaries without duplicate intervals");
        value = "one two three four five six seven eight";
        width = 100;
        var wrap = Layout();
        var first = wrap.GetLine(0);
        check(wrap.LineCount > 1 && first.End == first.NextStart && first.End > 0 && first.End < value.Length
            && new LinuxTextBoundarySequence(wrap, false).GetRange(first.End, 0).Start == first.End,
            "Real Linux soft wrapping selects the next half-open line without inventing a separator");
        check(starts.GetRange(4, 0) == (3, 7) && ends.GetRange(4, 0) == (2, 6),
            "Retained native line views remain immutable after actual editor replacement and reflow");
        value = "정A";
        width = 250;
        foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f, 2.5f })
        {
            context.SetContentScale(scale);
            context.SetDirectTextBaseFontSize(18 * scale);
            legacy = false;
            Layout();
            check(Appearance().Size is { } small && small.PhysicalPixels == 12 * scale
                && small.LogicalPixels == 12 && small.Points == 9,
                $"Actual compiled typography retains12 logical pixels and9 points at configured content scale{scale}");
            legacy = true;
            Layout();
            check(Appearance().Size is { } plain && plain.PhysicalPixels == 18 * scale && plain.Points == 13.5f,
                $"Actual legacy direct editor publishes its scaled physical18-pixel nominal font at content scale{scale}");
        }
        settings = UiTextSettings.Default with
        {
            Scale = 1.25f
        };
        Layout();
        check(Appearance().Size is { LogicalPixels: 22.5f, Points: 16.875f },
            "Actual text zoom contributes to nominal point size once, independently of content scale");
        settings = settings with
        {
            LineHeightScale = 2
        };
        Layout();
        check(Appearance().Size?.Points == 16.875f,
            "Changing actual line spacing cannot be mistaken for changing the nominal font em size");
        check(appearance.Size is { LogicalPixels: 12, Points: 9 },
            "Retained nominal size snapshots remain unchanged after subsequent scale and zoom updates");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
