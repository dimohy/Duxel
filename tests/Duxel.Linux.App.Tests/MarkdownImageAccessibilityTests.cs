using Duxel.Core;
using Duxel.Platform.Linux;

internal static class MarkdownImageAccessibilityTests
{
    internal const string Source = "# Title\n\n[plain](https://example.test/plain)\n\nbefore ![한글 **그림**](asset.png) after\n\n![second](asset.png)\n\n![missing](missing.png)\n\n![](asset.png)\n\n[![linked](asset.png)](https://example.test/)\n\n[after link](https://example.test/after)\n\nitem | image\n--- | ---\n![leading](asset.png) | a\na | ![table](asset.png)\n\n![tail](asset.png)\n\nend";
    internal static UiImageSource Picture(int height = 60)
    {
        var pixels = new byte[120 * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 53;
            pixels[index + 1] = 168;
            pixels[index + 2] = 124;
            pixels[index + 3] = 255;
        }
        return new(new(120, height, pixels));
    }
    internal static void Run(string primary, string secondary, Action<bool, string> check)
    {
        const string original = Source;
        foreach (var editor in new[] { false, true })
        {
            using var tree = new UiAccessibilityTree();
            using var wake = new ManualResetEventSlim();
            using var backend = new LinuxPlatformTextBackend();
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            context.SetPlatformTextBackend(backend);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextPrimaryFontPath(primary);
            context.SetDirectTextSecondaryFontPath(secondary);
            context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary) { Monospace = primary });
            context.SetAccessibilityTree(tree);
            tree.SetRequestFrameCallback(wake.Set);
            var value = original;
            var calls = 0;
            var image = Picture();
            using UiMarkdownWidget widget = editor ? new UiMarkdownEditor("Images", new(() => value, text => value = text), new(Width: 400, Height: 260))
                : new UiMarkdownView("Images", new(() => value, text => value = text), new(Width: 400, Height: 260));
            widget.ResolveImage = uri => { calls++; return uri.OriginalString == "asset.png" ? image : null; };
            var disabled = false;
            var visible = true;
            var textureUpdates = 0;
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            var screen = new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                if (!visible)
                    return;
                ui.BeginDisabled(disabled);
                widget.Render(ui);
                ui.EndDisabled();
            });
            void Frame()
            {
                context.NewFrame(new(.1f, new(500, 400), new(1, 1)), idle, new(0, 0, 500, 400), UiTextSettings.Default);
                context.Render(screen);
                using var draw = context.GetDrawData();
                textureUpdates = draw.TextureUpdates.Count;
            }
            UiAccessibilityElement Root() => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Images");
            UiAccessibilityElement[] Pictures() => tree.CaptureSnapshot().Elements.Where(e => e.Node.Role == UiAccessibilityRole.Image).ToArray();
            UiAccessibilityResult Reveal(UiAccessibilityElement picture)
            {
                wake.Reset();
                var task = Task.Run(() => tree.RequestAction(picture.Node.Id, picture.RuntimeId, UiAccessibilityAction.ScrollIntoView));
                if (!wake.Wait(1000))
                    throw new InvalidOperationException(task.IsCompleted ? "Image reveal admission: " + task.Result : "Image reveal did not wake its frame.");
                for (var frame = 0; frame < 4 && !task.IsCompleted; frame++)
                {
                    Frame();
                    task.Wait(10);
                }
                if (!task.Wait(1000))
                    throw new TimeoutException("Accessible image reveal did not publish within its frame budget.");
                return task.Result;
            }
            Frame();
            var pictures = Pictures();
            check(pictures.Length == 7 && pictures.Select(p => p.Node.Name).ToHashSet().SetEquals(
                new[] { "한글 그림", "second", "Image unavailable: missing", "linked", "leading", "table", "tail" }),
                "Markdown exposes named image children, plain Korean alternatives and unavailable text while omitting decorative images, editor=" + editor);
            var first = pictures.Single(p => p.Node.Name == "한글 그림");
            check(first.Node.ImageInfo is { Description: "한글 그림", Locale: null, Bounds.Width: 120, Bounds.Height: 60 },
                "Resolved images publish the complete rendered rectangle and alternative text without inventing a locale, editor=" + editor);
            check(pictures.Single(p => p.Node.Name == "tail").Node.ImageInfo is { Bounds.Width: 120, Bounds.Height: 60 },
                "Image metadata retains the complete dimensions even when Component bounds are clipped offscreen, editor=" + editor);
            check(pictures.Single(p => p.Node.Name == "Image unavailable: missing").Node.ImageInfo is null,
                "An unavailable-image caption does not claim a rendered native image, editor=" + editor);
            check(first.Node.ParentId == Root().Node.Id && first.Node.Bounds.Width == 120 && first.Node.Bounds.Height == 60
                && !first.Node.Focusable && first.Node.ReadOnly && !first.Node.Offscreen,
                "The first image uses its actual native aspect ratio and noninteractive document child role, editor=" + editor);
            check(pictures.Single(p => p.Node.Name == "tail").Node.Offscreen
                && pictures.Single(p => p.Node.Name == "table").Node.CanScrollIntoView
                && pictures.Single(p => p.Node.Name == "table").Node.ParentId == Root().Node.Id,
                "Offscreen images remain discoverable with the existing clipped-bounds and reveal contract, editor=" + editor);
            if (!editor)
            {
                var link = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Link && e.Node.Name == "linked");
                check(pictures.Single(p => p.Node.Name == "linked").Node.ParentId == link.Node.Id,
                    "A viewer image inside a Markdown link belongs to that real accessible link rather than an unrelated sibling");
            }
            Frame();
            check(Pictures().Single(p => p.Node.Name == "한글 그림").RuntimeId == first.RuntimeId && calls == 2
                && widget.Document.ParseCount == 1 && textureUpdates == 0,
                "Unchanged accessible images reuse identity, parsed source, resolved assets and existing texture uploads, editor=" + editor);
            check(Reveal(Pictures().Single(p => p.Node.Name == "table")) == UiAccessibilityResult.Success,
                "An offscreen image completes the shared item reveal request, editor=" + editor);
            check(Pictures().Single(p => p.Node.Name == "table").Node.Bounds is { Width: 120, Height: 60 },
                "A fully visible table image publishes its actual intrinsic image rectangle, editor=" + editor);
            check(Reveal(Pictures().Single(p => p.Node.Name == "leading")) == UiAccessibilityResult.Success,
                "A leading table-cell image can be revealed without an outer pipe delimiter, editor=" + editor);
            check(Pictures().Single(p => p.Node.Name == "leading").Node.Bounds is { Width: 120, Height: 60 } leading
                && leading.X > Root().Node.Bounds.X + 12,
                "The leading table-cell image keeps actual cell padding rather than the source-line caret origin, editor=" + editor);
            var reset = Root();
            tree.RequestScroll(reset.Node.Id, reset.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 0), wait: false);
            Frame();
            disabled = true;
            Frame();
            check(Pictures().All(p => !p.Node.Enabled), "Image children inherit the disabled document state, editor=" + editor);
            disabled = false;
            widget.Options = widget.Options with { Width = 80 };
            Frame();
            var narrow = Pictures().Single(p => p.Node.Name == "한글 그림").Node.Bounds;
            check(narrow.Width > 0 && narrow.Width <= 56 && MathF.Abs(narrow.Width / narrow.Height - 2) < .001f
                && Pictures().Single(p => p.Node.Name == "table").Node.CanScrollIntoView && calls == 2,
                "Accessible image rectangles follow ordinary scaling while clipped table images remain revealable without resolving assets again, editor=" + editor);
            widget.Options = widget.Options with { Width = 400 };
            Frame();
            var root = Root();
            var originalText = root.Node.TextInfo!.Content;
            if (editor)
            {
                tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Focus, wait: false);
                Frame();
                tree.RequestTextSelection(root.Node.Id, root.RuntimeId, new(originalText, original.IndexOf("![", StringComparison.Ordinal), original.IndexOf("![", StringComparison.Ordinal)), wait: false);
                Frame();
                check(!Pictures().Any(p => p.Node.Name == "한글 그림") && Pictures().Any(p => p.Node.Name == "second"),
                    "The active source block exposes raw Markdown instead of a phantom image while other blocks keep real images");
                root = Root();
                tree.RequestTextSelection(root.Node.Id, root.RuntimeId, new(root.Node.TextInfo!.Content, value.Length, value.Length), wait: false);
                Frame();
                check(Pictures().Single(p => p.Node.Name == "한글 그림").RuntimeId != first.RuntimeId && value == original && calls == 2,
                    "Leaving the edited block restores its accessible image without modifying source or resolving assets again");
            }
            root = Root();
            tree.RequestScroll(root.Node.Id, root.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 100), wait: false);
            Frame();
            check(!Pictures().Single(p => p.Node.Name == "tail").Node.Offscreen && value == original,
                "Document scrolling brings the real tail image into the published viewport without changing source, editor=" + editor);
            var retained = first.Node;
            value = "![replacement](missing.png)\n\nend";
            Frame();
            check(Pictures() is [{ Node.Name: "Image unavailable: replacement", Node.Bounds.Height: 32 }] && calls == 2,
                "Changing image syntax publishes current placeholder metadata and reuses the cached unavailable resource, editor=" + editor);
            value = "No images";
            Frame();
            check(Pictures().Length == 0 && retained.Name == "한글 그림" && retained.Bounds.Width == 120,
                "Removing images retires their nodes while retained immutable metadata remains readable, editor=" + editor);
            visible = false;
            Frame();
            check(!tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "Images"),
                "Removing the document retires its accessible subtree, editor=" + editor);
        }
    }

    internal sealed class NativeScreen : UiScreen, IDisposable
    {
        private readonly UiMarkdownView _view;
        private string _value = Source;
        internal ManualResetEventSlim FirstFrame { get; } = new();
        internal NativeScreen()
        {
            var picture = Picture();
            _view = new("Images", new(() => _value, value => _value = value), new(Width: 400, Height: 260))
            {
                ResolveImage = uri => uri.OriginalString == "asset.png" ? picture : null
            };
        }
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            _view.Render(ui);
            FirstFrame.Set();
        }
        public void Dispose()
        {
            _view.Dispose();
            FirstFrame.Dispose();
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
