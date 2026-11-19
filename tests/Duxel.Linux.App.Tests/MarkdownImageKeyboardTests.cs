using Duxel.Core;
using Duxel.Platform.Linux;

internal static class MarkdownImageKeyboardTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check)
    {
        foreach (var editor in new[] { false, true })
        foreach (var tall in new[] { false, true })
        foreach (var linked in new[] { false, true })
        {
            using var tree = new UiAccessibilityTree();
            using var backend = new LinuxPlatformTextBackend();
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            context.SetPlatformTextBackend(backend);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextPrimaryFontPath(primary);
            context.SetDirectTextSecondaryFontPath(secondary);
            context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary) { Monospace = primary });
            context.SetAccessibilityTree(tree);
            var clause = linked ? "[![한글](asset.png)](https://example.test/destination)" : "![한글](asset.png)";
            var value = "before\n\n" + clause + "\n\nafter";
            var original = value;
            var imageStart = value.IndexOf("![", StringComparison.Ordinal);
            var imageEnd = value.IndexOf(')') + 1;
            var height = tall ? 300 : 60;
            var picture = MarkdownImageAccessibilityTests.Picture(height);
            using UiMarkdownWidget widget = editor ? new UiMarkdownEditor("Keys", new(() => value, text => value = text), new(Width: 180, Height: 180))
                : new UiMarkdownView("Keys", new(() => value, text => value = text), new(Width: 180, Height: 180));
            widget.ResolveImage = _ => picture;
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            var screen = new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); widget.Render(ui); });
            UiAccessibilityElement Frame(UiInputState? input = null)
            {
                context.NewFrame(new(.1f, new(400, 300), new(1, 1)), input ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(screen);
                context.GetDrawData().ReleasePooled();
                return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Keys");
            }
            var node = Frame();
            tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
            Frame();
            UiAccessibilityTextInfo Caret(int position)
            {
                var current = Frame();
                tree.RequestTextSelection(current.Node.Id, current.RuntimeId, new(current.Node.TextInfo!.Content, position, position), wait: false);
                return Frame().Node.TextInfo!;
            }
            UiAccessibilityTextInfo Key(UiKey key, bool shift = false)
            {
                var modifiers = shift ? KeyModifiers.Shift : default;
                Frame(idle with { KeyEvents = [new(key, true, modifiers)], Modifiers = modifiers });
                return Frame(idle with { KeyEvents = [new(key, false, modifiers)], Modifiers = modifiers }).Node.TextInfo!;
            }
            foreach (var shift in new[] { false, true })
            {
                var current = Caret(0);
                var path = new List<int> { current.CaretUtf16 };
                for (var step = 0; step < 12 && current.CaretUtf16 <= imageEnd; step++)
                {
                    current = Key(UiKey.DownArrow, shift);
                    path.Add(current.CaretUtf16);
                }
                check(current.CaretUtf16 > imageEnd && (!shift || current.AnchorUtf16 == 0 && current.HasSelection),
                    $"Down/Shift+Down passes the image and extends source selection, editor={editor}, tall={tall}, shift={shift}, path={string.Join(',', path)}");
                current = Caret(value.Length);
                path = [current.CaretUtf16];
                for (var step = 0; step < 12 && current.CaretUtf16 >= imageStart; step++)
                {
                    current = Key(UiKey.UpArrow, shift);
                    path.Add(current.CaretUtf16);
                }
                check(current.CaretUtf16 < imageStart && (!shift || current.AnchorUtf16 == value.Length && current.HasSelection),
                    $"Up/Shift+Up passes the image and preserves the source selection anchor, editor={editor}, tall={tall}, shift={shift}, path={string.Join(',', path)}");
                current = Caret(0);
                for (var step = 0; step <= value.Length; step++)
                    current = Key(UiKey.RightArrow, shift);
                check(current.CaretUtf16 == value.Length && (!shift || current.AnchorUtf16 == 0 && current.HasSelection),
                    $"Right/Shift+Right can cross all image source positions, editor={editor}, tall={tall}, shift={shift}");
                current = Caret(value.Length);
                for (var step = 0; step <= value.Length; step++)
                    current = Key(UiKey.LeftArrow, shift);
                check(current.CaretUtf16 == 0 && (!shift || current.AnchorUtf16 == value.Length && current.HasSelection),
                    $"Left/Shift+Left can cross all image source positions, editor={editor}, tall={tall}, shift={shift}");
            }
            if (!editor)
            {
                Caret(imageStart);
                var forward = Key(UiKey.RightArrow, true);
                check(forward.CaretUtf16 == imageEnd && forward.AnchorUtf16 == imageStart,
                    $"One Shift+Right selects the whole rendered image instead of entering hidden URI syntax, tall={tall}; caret={forward.CaretUtf16}; expected={imageEnd}");
                var after = Key(UiKey.RightArrow);
                after = Key(UiKey.RightArrow);
                check(after.CaretUtf16 > value.IndexOf("\n\nafter", StringComparison.Ordinal),
                    $"Right leaves the rendered image and any invisible outer link URI, linked={linked}, tall={tall}");
                Caret(imageEnd);
                var backward = Key(UiKey.LeftArrow, true);
                check(backward.CaretUtf16 == imageStart && backward.AnchorUtf16 == imageEnd,
                    $"One Shift+Left crosses the whole rendered image, tall={tall}; caret={backward.CaretUtf16}; expected={imageStart}");
            }
            check(value == original, $"Image keyboard navigation never edits source, editor={editor}, tall={tall}");
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
