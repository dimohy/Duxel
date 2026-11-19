using Duxel.Core;
using Duxel.Platform.Linux;

internal static class StyledTextLayoutTests
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
        context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary) { Monospace = primary });
        context.SetAccessibilityTree(tree);
        var source = "# 정😀\n\n본문 **bold** and `code`";
        var normalize = false;
        using var editor = new UiMarkdownEditor("Document", new(() => source, value => source = normalize ? value.ToUpperInvariant() : value), new(Width: 300, Height: 220));
        using var viewer = new UiMarkdownView("Document", new(() => source, value => source = value), new(Width: 300, Height: 220));
        UiMarkdownWidget widget = editor;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui => widget.Render(ui));
        UiAccessibilityTextLayout Frame()
        {
            context.NewFrame(new(.1f, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            context.GetDrawData().ReleasePooled();
            return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document").Node.TextLayout
                ?? throw new InvalidOperationException("Actual styled editor did not publish its source geometry.");
        }
        var layout = Frame();
        UiRect Box(int utf16) => layout.GetCharacterExtents(layout.Content.GetCharacterOffset(utf16));
        check(layout.Content.Text == source && layout.Content.CharacterCount == source.Length - 1,
            "Actual Markdown layout publishes unchanged source offsets including supplementary characters");
        check(Box(0).Width == 0 && Box(1).Width == 0 && Box(2).Width > 0,
            "Hidden Markdown heading delimiters have zero width while visible Korean retains its painted cell");
        check(Box(source.IndexOf("**", StringComparison.Ordinal)).Width == 0
            && Box(source.IndexOf('`')).Width == 0,
            "Hidden emphasis and code delimiters cannot claim a visible text hit area");
        var heading = Box(2);
        var body = Box(source.IndexOf("본문", StringComparison.Ordinal));
        check(heading.Height > body.Height && body.Y > heading.Y + heading.Height,
            "Styled source boxes preserve actual heading and paragraph typography heights and vertical layout");
        check(layout.GetOffsetAtPoint(heading.X + heading.Width / 2, heading.Y + heading.Height / 2) == 2,
            "A painted Korean heading cell hit returns the original scalar position after hidden syntax");
        var emoji = Box(3);
        check(emoji.Width > 0 && layout.GetOffsetAtPoint(emoji.X + emoji.Width / 2, emoji.Y + emoji.Height / 2) == 3,
            "Supplementary heading text keeps one hit-test cell and one native scalar offset");
        check(ReferenceEquals(layout, Frame()), "Unchanged actual styled painting retains its immutable source geometry");
        var node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
        var appearance = node.Node.TextAppearance!;
        check(node.Node.TextInfo is { CanSetSelection: true, CanEditText: true } && appearance is { HasUniformPaint: false },
            "Styled source selection and guarded editing expose source paint ranges without inventing one uniform style");
        check(appearance.GetPaintRangeAt(0).Foreground is null && appearance.GetFontRangeAt(0).Font is null,
            "Hidden source syntax publishes no invented paint or font attributes");
        check(appearance.GetPaintRangeAt(2).Size?.LogicalPixels == UiFluentType700.FontSize
            && appearance.GetFontRangeAt(2).Font?.FamilyName == "Malgun Gothic",
            "Actual styled heading publishes its exact typography size and selected Korean font");
        var bodyScalar = appearance.Content.GetCharacterOffset(source.IndexOf("본문", StringComparison.Ordinal));
        check(appearance.GetPaintRangeAt(bodyScalar).Size?.LogicalPixels == UiFluentType300.FontSize,
            "Actual body paint retains its distinct nominal size in original source coordinates");
        Frame();
        check(ReferenceEquals(appearance, tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document").Node.TextAppearance),
            "Unchanged projected style, palette and font language retain the same immutable paint map");
        var selection = new UiAccessibilityTextInfo(node.Node.TextInfo!.Content, 2, 2);
        check(tree.RequestTextSelection(node.Node.Id, node.RuntimeId, selection, wait: false) == UiAccessibilityResult.Success,
            "Markdown accepts a guarded source-based native selection request");
        Frame();
        node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
        check(node.Node.TextInfo?.CaretUtf16 == 2 && tree.RequestAction(node.Node.Id, node.RuntimeId,
            UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "Source selection commits without stealing focus; explicit focus activates the styled editor");
        var revealed = Frame();
        check(revealed.GetCharacterExtents(0).Width > 0 && layout.GetCharacterExtents(0).Width == 0,
            "Revealing the selected block rebuilds geometry while retained rendered snapshots remain immutable");
        node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
        check(node.Node.TextAppearance!.GetPaintRangeAt(0).Foreground is { } syntax
            && syntax != node.Node.TextAppearance.GetPaintRangeAt(2).Foreground
            && appearance.GetPaintRangeAt(0).Foreground is null,
            "Active source delimiters receive their real syntax color without mutating the retained hidden-syntax paint map");
        var original = node.Node.TextInfo!.Content;
        check(tree.RequestTextEdit(node.Node.Id, node.RuntimeId,
            new(original, UiAccessibilityTextEditKind.Replace, 0, 2, "## "), wait: false) == UiAccessibilityResult.Success,
            "Markdown queues native range replacement against original source rather than projected text");
        var edited = Frame();
        check(source.StartsWith("## 정😀", StringComparison.Ordinal) && edited.Content.Text == source
            && original.Text.StartsWith("# 정😀", StringComparison.Ordinal),
            "Actual source replacement reparses Markdown and publishes coherent text and geometry without mutating snapshots");
        node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
        check(tree.RequestTextEdit(node.Node.Id, node.RuntimeId,
            new(original, UiAccessibilityTextEditKind.Replace, 0, 2, "bad"), wait: false) == UiAccessibilityResult.InvalidArgument,
            "Stale source replacement is rejected after Markdown source and projection change");
        normalize = true;
        check(tree.RequestTextEdit(node.Node.Id, node.RuntimeId,
            new(node.Node.TextInfo!.Content, UiAccessibilityTextEditKind.Replace, 0, 3, "## title "), wait: false) == UiAccessibilityResult.Success,
            "Markdown range replacement reaches an application binding that normalizes accepted source");
        var accepted = Frame();
        node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
        check(source.StartsWith("## TITLE ", StringComparison.Ordinal) && node.Node.TextInfo!.Content.Text == source
            && ReferenceEquals(node.Node.TextInfo.Content, accepted.Content),
            "Binding normalization reconciles final source, caret and immutable layout in the same published frame");
        widget = viewer;
        var view = Frame();
        check(view.GetCharacterExtents(0).Width == 0 && view.Content.Text == source,
            "Read-only Markdown uses the rendered projection without exposing active editor syntax");
        node = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document");
        check(tree.RequestTextEdit(node.Node.Id, node.RuntimeId,
            new(node.Node.TextInfo!.Content, UiAccessibilityTextEditKind.Replace, 0, 3, "bad"), wait: false) == UiAccessibilityResult.ReadOnly,
            "Read-only Markdown rejects a native mutation before changing its source");
        source = "| Left | Right |\n| --- | --- |\n| A | B |";
        layout = Frame();
        var a = Box(source.LastIndexOf('A'));
        var b = Box(source.LastIndexOf('B'));
        check(a.Width > 0 && b.X > a.X + a.Width && a.Y == b.Y,
            "Actual Markdown table cells retain column padding and independent source positions");
        check(Box(0).Width == 0 && Box(source.LastIndexOf('|')).Width == 0,
            "Table delimiters cannot inherit the gap between rendered columns as a hit area");
        check(layout.GetOffsetAtPoint(b.X + b.Width / 2, b.Y + b.Height / 2)
            == layout.Content.GetCharacterOffset(source.LastIndexOf('B')),
            "Actual table text hit testing maps a rendered cell back to the correct source scalar");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
