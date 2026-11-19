using Duxel.Core;

internal static class MarkdownImageRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        var pixels = new byte[120 * 60 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 230;
            pixels[i + 3] = 255;
        }
        var source = new UiImageSource(new(120, 60, pixels));
        var text = new UiState<string>("![red](memory:red)\n\nAfter image");
        using var view = Dux.MarkdownView("Image preview", text.Binding, new(Width: 220, Height: 180));
        using var editor = Dux.MarkdownEditor("Image editor", text.Binding, new(Width: 220, Height: 180));
        var resolutions = 0;
        long uploadedImages = 0;
        UiImageSource? Resolve(Uri uri)
        {
            resolutions++;
            return uri.OriginalString == "memory:red" ? source : null;
        }
        view.ResolveImage = editor.ResolveImage = Resolve;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var background = scheme == UiFluentColorScheme.Light ? new UiColor(255, 255, 255) : new UiColor(32, 32, 32);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, 760, 480), background, default, 0, 0);
            paint.RestartInputCaretBlink();
            ui.SetCursorScreenPos(new(10, 20));
            view.Render(ui);
            ui.SetCursorScreenPos(new(300, 20));
            editor.Render(ui);
            uploadedImages = ui.ImageTextureStatistics.Uploads;
        }
        static int RedPixels(byte[] image)
        {
            var count = 0;
            for (var i = 0; i < image.Length; i += 4)
                if (image[i] > 200 && image[i + 1] < 30 && image[i + 2] < 30 && image[i + 3] > 240)
                    count++;
            return count;
        }
        setInput(idle);
        var rendered = capture("images", Draw);
        check(RedPixels(rendered) == 120 * 60 * 2, "GPU Markdown images paint their exact rectangular area without drawing syntax");
        var uploads = uploadedImages;
        var replay = capture("image-replay", Draw);
        check(rendered.AsSpan().SequenceEqual(replay) && resolutions == 2 && uploadedImages == uploads,
            "GPU Markdown replay retains pixels and image leases without resolution or upload");
        view.Options = view.Options with
        {
            Width = 84
        };
        var narrow = capture("image-narrow", Draw);
        check(RedPixels(narrow) == 60 * 30 + 120 * 60, "GPU Markdown image width follows its content box and preserves aspect ratio");
        view.Options = view.Options with
        {
            Width = 220
        };
        setInput(idle with
        {
            MousePosition = new(350, 50),
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        capture("image-editor-press", Draw);
        setInput(idle with
        {
            MousePosition = new(350, 50),
            LeftMouseDown = true
        });
        capture("image-editor-hold", Draw);
        setInput(idle with
        {
            MousePosition = new(350, 50),
            LeftMouseReleased = true
        });
        var raw = capture("image-editor-raw", Draw);
        check(RedPixels(raw) == 120 * 60 && text.Value.StartsWith("![red]", StringComparison.Ordinal),
            "GPU Markdown image editing reveals source while the preview keeps its rendered image");
        setInput(idle with
        {
            MousePosition = new(700, 430),
            LeftMousePressed = true,
            LeftMouseReleased = true
        });
        capture("image-blur", Draw);
        setInput(idle);
        check(RedPixels(capture("image-collapsed", Draw)) == 120 * 60 * 2,
            "GPU Markdown image returns after leaving its edited block");
        text.Value = "| Picture | Value |\n| - | - |\n| ![red](memory:red) | 1 |";
        var table = capture("image-table", Draw);
        check(RedPixels(table) == 120 * 60 * 2 && resolutions == 2, "GPU Markdown table cells retain image pixels and resources");
        text.Value = "![missing](memory:missing)";
        var missing = capture("image-unavailable", Draw);
        check(RedPixels(missing) == 0 && resolutions == 4, "GPU Markdown unavailable images display explicit alternatives without stale pixels");
        check(missing.AsSpan().SequenceEqual(capture("image-unavailable-replay", Draw)) && resolutions == 4,
            "GPU missing-image alternatives remain stable without repeated resolver calls");
        setInput(idle);
    }
}
