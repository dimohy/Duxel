using System.Globalization;
using Duxel.Core;

internal static class TreeLabelTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark);
        ControlFontFixture.Configure(context);
        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        const string text = "e\u0301 디모이 프로젝트의 긴 이름과 English file names, שלום العربية";
        UiTreeItem[] items = [new("root", null, "Project"), new("long", "root", text), new("short", "root", "Short")];
        using var tree = new UiTree<UiFluentTreePresentation>("Labels", new UiState<string[]>(["root"]).Binding,
            new UiState<string?>("short").Binding, items, new(Width: 220));
        UiWidget current = tree;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, float delta = .016f)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); current.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Row(string name) => accessibility.CaptureSnapshot().Elements.Single(
            item => item.Node.Role == UiAccessibilityRole.TreeItem && item.Node.Name == name);
        var label = (UiFluentTreeLabel)items[1].PlainLabel!.Label;
        Frame();
        check(label.Lines.Count > 1 && !label.IsTruncated && items[1].LabelTooltip is null,
            "plain labels wrap by default without a redundant full-name tooltip");
        check(Row(text).Node.Bounds.Height > 32, "wrapped text expands its real row allocation");
        var identity = Row(text).RuntimeId;
        var mediumHeight = label.Lines.LineHeight(0);
        tree.Options = tree.Options with { Size = UiTreeSize.Small };
        Frame();
        check(label.Lines.LineHeight(0) < mediumHeight && Row("Short").Node.Bounds.Height == 24,
            "Small uses caption typography and 24px short rows");
        tree.Options = tree.Options with { TextOverflow = UiTreeTextOverflow.Ellipsis, Width = 180 };
        Frame();
        check(label.IsTruncated && label.DisplayText.EndsWith('…') && label.Lines.Count == 1,
            "ellipsis fits a single rendered line");
        check(Row(text).RuntimeId == identity, "changing size and overflow preserves row provider identity");
        foreach (var width in new[] { 110, 130, 160, 180 })
        {
            tree.Options = tree.Options with { Width = width };
            Frame();
            var prefix = label.DisplayText[..^1];
            check(text.StartsWith(prefix, StringComparison.Ordinal)
                && StringInfo.ParseCombiningCharacters(text).Contains(prefix.Length),
                $"ellipsis at width {width} preserves complete grapheme boundaries");
        }
        var bounds = Row(text).Node.Bounds;
        var hover = idle with { MousePosition = new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2) };
        Frame(hover);
        Frame(hover, .6f);
        check(accessibility.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Tooltip && item.Node.Name == text),
            "truncation exposes the full original name through the shared tooltip");
        Frame(hover with { KeyEvents = [new(UiKey.Escape, true, default)] });
        check(!accessibility.CaptureSnapshot().Elements.Any(item => item.Node.Role == UiAccessibilityRole.Tooltip),
            "Escape closes the full-name tooltip");
        tree.Options = tree.Options with { TextOverflow = UiTreeTextOverflow.Wrap, Width = 320, RightToLeft = true };
        Frame();
        check(!label.IsTruncated && label.Source.RightToLeft && Row(text).Node.Name == text,
            "RTL wrapping retains the unmodified accessible name");
        var builds = label.Lines.Builds;
        Frame();
        check(label.Lines.Builds == builds, "unchanged rows retain their shaped visual lines");
        tree.Options = tree.Options with { Width = 1, TextOverflow = UiTreeTextOverflow.Ellipsis };
        Frame();
        check(label.DisplayText.Length == 0 && float.IsFinite(Row(text).Node.Bounds.Height),
            "a zero-content-width row has finite allocation and no overflowing ellipsis");
        var (size, appearance, rtl, requestedWidth, navigation) = tree.Options;
        check(size == UiTreeSize.Small && rtl && requestedWidth == 1 && !navigation && appearance == UiTreeAppearance.Subtle,
            "overflow preserves the existing five-value options deconstruction");
        ControlFontFixture.ConfigureEmoji(context, korean: true);
        const string emojiText = "👩‍💻 디모이 프로젝트의 긴 이름";
        var emojiItem = new UiTreeItem("emoji", null, emojiText);
        using var emojiTree = new UiTree<UiFluentTreePresentation>("Emoji labels", new UiState<string[]>([]).Binding,
            [emojiItem], new(Width: 90) { TextOverflow = UiTreeTextOverflow.Ellipsis });
        current = emojiTree;
        Frame();
        var emojiLabel = (UiFluentTreeLabel)emojiItem.PlainLabel!.Label;
        var emojiPrefix = emojiLabel.DisplayText[..^1];
        check(emojiPrefix.StartsWith("👩‍💻", StringComparison.Ordinal)
            && StringInfo.ParseCombiningCharacters(emojiText).Contains(emojiPrefix.Length),
            "an explicitly configured emoji font preserves the entire ZWJ grapheme when truncating");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
