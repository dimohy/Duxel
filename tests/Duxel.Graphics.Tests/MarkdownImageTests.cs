using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Svg;

internal static class MarkdownImageTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        const string original = "Before\n\n![first **red**](asset.png)\n\n![again](asset.png)\n\n![missing](missing.png)\n\nAfter";
        var pixels = new byte[120 * 60 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 230;
            pixels[i + 3] = 255;
        }
        var image = new UiImageSource(new(120, 60, pixels));
        var requests = new List<Uri>();
        UiImageSource? Resolve(Uri uri)
        {
            requests.Add(uri);
            return uri.AbsolutePath.EndsWith("asset.png", StringComparison.Ordinal) ? image : null;
        }
        var source = new UiMarkdownTextSource(new(original));
        source.ConfigureImages(Resolve, new("https://example.test/docs/"));
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var frameRequests = 0;
        context.SetRequestFrameCallback(() => frameRequests++);
        var visual = new UiVisualTextLines();
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Layout(string text, float width = 200, int active = -1)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => visual.Prepare(ui, text, width, 400, 20, 0, styled: source, activeSourceIndex: active)));
            using var draw = context.GetDrawData();
        }
        Layout(original);
        check(source.Document.Images.Count == 3 && source.Document.Images[0].AlternativeText == "first red",
            "Markdown image compilation retains source references and semantic alternative text");
        check(requests.Count == 2 && requests.All(uri => uri.AbsoluteUri.StartsWith("https://example.test/docs/", StringComparison.Ordinal)),
            "Markdown resolves duplicate image destinations once using the explicit base URI");
        check(source.Projection.Text.Count(character => character == '\uFFFC') == 3 && !source.Projection.Text.Contains("asset.png", StringComparison.Ordinal),
            "Markdown image projection replaces complete syntax with one atomic object");
        check(source.Projection.SourceToDisplay.Zip(source.Projection.SourceToDisplay.Skip(1)).All(pair => pair.First <= pair.Second),
            "Image source-to-display mapping remains total and monotonic");
        var first = source.Document.Images[0].Source;
        var row = visual.FindLine(first.Start);
        check(visual.NativeParagraph is { Objects.Length: 3 },
            "Markdown images use native paragraph object allocations including explicit missing-image alternatives");
        var bounds = visual.NativeObjectBounds(first.Start);
        check(visual.Hit(bounds.X + bounds.Width * .25f, bounds.Y + bounds.Height / 2, 20) == first.Start
            && visual.Hit(bounds.X + bounds.Width * .75f, bounds.Y + bounds.Height / 2, 20) == first.End,
            "Native image pointer hits map to complete original syntax boundaries instead of hidden interior characters");
        visual.Hit(bounds.X + bounds.Width * .25f, bounds.Y + bounds.Height / 2, 20);
        check(visual.MoveHorizontal(first.Start, right: true) == first.End
            && visual.MoveHorizontal(first.End, right: false) == first.Start,
            "Physical arrows cross one native image as its complete original source atom");
        var nativeSnapshot = visual.NativeParagraph;
        Layout(original);
        check(ReferenceEquals(nativeSnapshot, visual.NativeParagraph),
            "Unchanged Markdown image layout retains its complete native paragraph snapshot");
        check(visual.LineHeight(row) == 60 && visual.X(first.End, row) - visual.X(first.Start, row) == 120,
            "Markdown image reserves its native aspect ratio in shared caret and line geometry");
        Layout(original, 60);
        row = visual.FindLine(first.Start);
        check(visual.LineHeight(row) == 30 && visual.X(first.End, row) - visual.X(first.Start, row) == 60,
            "Narrow Markdown images scale down proportionally without overflowing text layout");
        Layout(original, active: first.Start);
        check(visual.NativeParagraph is { Objects.Length: 2 },
            "Revealing image source removes only that native object while retaining other projected images");
        check(source.Projection.Text.Contains("![first **red**](asset.png)", StringComparison.Ordinal),
            "Editing an image block reveals its exact source syntax");
        Layout(original);
        check(requests.Count == 2 && source.Document.ParseCount == 1,
            "Image source reveal, collapse and width changes neither resolve nor parse again");
        var retained = source.Images[0].Image;
        Layout(original.Replace("Before", "Changed", StringComparison.Ordinal));
        check(requests.Count == 2 && ReferenceEquals(retained, source.Images[0].Image),
            "Unrelated document edits retain existing image resources");
        Layout(original + "\n\n![alias](./asset.png)");
        check(requests.Count == 2 && ReferenceEquals(source.Images[0].Image, source.Images[^1].Image),
            "Relative image aliases share one canonical resolved URI and image lease owner");
        source.InvalidateImages();
        Layout(original);
        check(requests.Count == 4 && !ReferenceEquals(retained, source.Images[0].Image),
            "Explicit image invalidation retries cached missing images and releases old leases");
        Layout("No images");
        var released = false;
        try
        {
            _ = retained!.Source;
        }
        catch (ObjectDisposedException) { released = true; }
        check(source.Images.Count == 0 && released, "Removing image references releases document-owned image objects");
        source.Release();

        var mixedSource = new UiMarkdownTextSource(new());
        mixedSource.ConfigureImages(Resolve, new("https://example.test/docs/"));
        var mixed = new UiVisualTextLines();
        void Mixed(string text, float width)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => mixed.Prepare(ui, text, width, 400, 20, 0, styled: mixedSource)));
            using var draw = context.GetDrawData();
        }
        Mixed("אב ![x](asset.png) גד", 200);
        var reference = mixedSource.Document.Images[0].Source;
        var imageBounds = mixed.NativeObjectBounds(reference.Start);
        check(mixed.NativeParagraph is { Objects.Length: 1 } paragraph && paragraph.Objects.Span[0].RightToLeft,
            "An image participates in native RTL context between Hebrew text runs");
        check(mixed.Hit(imageBounds.X + imageBounds.Width * .25f, imageBounds.Y + imageBounds.Height / 2, 20) == reference.End
            && mixed.Hit(imageBounds.X + imageBounds.Width * .75f, imageBounds.Y + imageBounds.Height / 2, 20) == reference.Start,
            "RTL image pointer edges preserve logical source direction without shifting the image rectangle");
        mixed.Hit(imageBounds.X + imageBounds.Width * .25f, imageBounds.Y + imageBounds.Height / 2, 20);
        check(mixed.MoveHorizontal(reference.End, right: true) == reference.Start
            && mixed.MoveHorizontal(reference.Start, right: false) == reference.End,
            "Physical RTL image arrows retain native direction and skip every hidden syntax character");
        Mixed("a![x](asset.png)b", 60);
        reference = mixedSource.Document.Images[0].Source;
        imageBounds = mixed.NativeObjectBounds(reference.Start);
        row = mixed.ImageLine(reference.Start);
        var imageEnd = mixed.Hit(imageBounds.X + imageBounds.Width * .75f, imageBounds.Y + imageBounds.Height / 2, 20);
        check(mixed[row].End == reference.End && imageEnd == reference.End && mixed.FindLine(imageEnd) == row,
            "Soft-wrapped image rows retain complete source syntax and clicked trailing caret affinity");
        var wrappedStart = mixed.MoveHorizontal(imageEnd, right: true);
        check(wrappedStart == reference.End && mixed.FindLine(wrappedStart) == row + 1
            && mixed.MoveHorizontal(wrappedStart, right: true) == "a![x](asset.png)b".Length,
            "Arrows cross a shared soft-wrap source boundary while preserving the destination visual line");
        Mixed("| H |\n| --- |\n| אב ![x](asset.png) גד |", 200);
        reference = mixedSource.Document.Images[0].Source;
        var tableLine = mixed.ImageLine(reference.Start);
        var hasTableBounds = mixed.TryTableObjectBounds(reference.Start, tableLine, out var tableBounds);
        check(hasTableBounds && MathF.Abs(tableBounds.Width - 120) < .002f && MathF.Abs(tableBounds.Height - 60) < .002f,
            "Table images expose native cell object bounds for accessibility and selection");
        check(mixed.Hit(tableBounds.X + tableBounds.Width / 4, tableBounds.Y + tableBounds.Height / 2, 20) == reference.End
            && mixed.Hit(tableBounds.X + tableBounds.Width * .75f, tableBounds.Y + tableBounds.Height / 2, 20) == reference.Start,
            "Table image pointer edges follow native RTL cell geometry instead of logical prefix widths");
        var imageFragments = mixed.SelectionFragments(reference.Start, reference.End, tableLine);
        check(imageFragments.Length == 1 && MathF.Abs(imageFragments[0].Left - tableBounds.X) < .002f
            && MathF.Abs(imageFragments[0].Width - tableBounds.Width) < .002f,
            "Table image selection and accessibility object rectangles share the exact painted horizontal allocation");
        Mixed("![x](asset.png)\n\n| H |\n| --- |\n| value |\n\n![y](asset.png)", 200);
        for (var imageIndex = 0; imageIndex < mixedSource.Document.Images.Count; imageIndex++)
        {
            var blockImage = mixedSource.Document.Images[imageIndex];
            var imageLine = mixed.ImageLine(blockImage.Source.Start);
            var found = mixed.TryComposedObjectBounds(blockImage.Source.Start, imageLine, out var blockBounds);
            check(found && blockBounds.Width == 120 && blockBounds.Height == 60
                && blockBounds.Y == (imageIndex == 0 ? 0 : 164),
                "Composed native image bounds translate exactly before and after a table grid : " + imageIndex);
            var selected = mixed.SelectionFragments(blockImage.Source.Start, blockImage.Source.End, imageLine);
            check(selected.Length == 1 && selected[0].Left == blockBounds.X && selected[0].Width == blockBounds.Width
                && mixed.Hit(blockBounds.X + 90, blockBounds.Y + 30, 20) == blockImage.Source.End,
                "Mixed-document image paint, selection and pointer source boundaries share one allocation : " + imageIndex);
        }
        mixed.Clear();
        mixedSource.Release();

        var pngPath = Path.Combine(output, "markdown-image.png");
        var svgPath = Path.Combine(output, "markdown-image.svg");
        File.WriteAllBytes(pngPath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0WQAAAAASUVORK5CYII="));
        File.WriteAllText(svgPath, "<svg xmlns='http://www.w3.org/2000/svg' width='120' height='60'><rect width='120' height='60' fill='#e60000'/></svg>");
        using var store = new SvgStore();
        var decoder = new SvgImageDecoder(store, WindowsUiImageDecoder.Shared);
        using var view = Dux.MarkdownView("Decoded images", new UiState<string>("![png](markdown-image.png)\n\n![svg](markdown-image.svg)").Binding,
            new(Width: 240, Height: 240, BaseUri: new Uri(output.TrimEnd('/', '\\') + Path.DirectorySeparatorChar)));
        var decodes = 0;
        view.ResolveImage = uri => { decodes++; return new(decoder.Decode(uri.LocalPath)); };
        void Draw()
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); view.Render(ui); }));
        }
        Draw();
        using (var draw = context.GetDrawData())
            check(decodes == 2 && store.Statistics.XmlParses == 1 && draw.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.Create) == 2,
                "Markdown PNG and SVG inputs decode once and upload native image textures");
        Draw();
        using (var draw = context.GetDrawData())
            check(decodes == 2 && store.Statistics.XmlParses == 1 && draw.TextureUpdates.Count == 0,
                "Unchanged Markdown image frames reuse decoded PNG, compiled SVG and GPU uploads");
        var beforeRetry = frameRequests;
        view.InvalidateImages();
        check(frameRequests > beforeRetry, "Markdown image invalidation wakes event-wait rendering");
        Draw();
        using (var draw = context.GetDrawData())
            check(decodes == 4 && view.Document.ParseCount == 1 && store.Statistics.XmlParses == 1,
                "Retrying image resolution preserves parsed Markdown and compiled SVG source");
        view.Dispose();
        check(view.ResolveImage is null, "Disposed Markdown releases its image resolver delegate");
        var rejected = false;
        try
        {
            view.InvalidateImages();
        }
        catch (ObjectDisposedException) { rejected = true; }
        check(rejected, "Disposed Markdown rejects image invalidation");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
