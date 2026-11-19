using Duxel.Core;
using Duxel.Platform.Linux;

internal static class MarkdownImageComponentScrollTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check)
    {
        foreach (var editor in new[] { false, true })
            foreach (var (nested, rtl) in new[] { (false, false), (true, false), (true, true) })
            {
                using var tree = new UiAccessibilityTree();
                using var wake = new ManualResetEventSlim();
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
                tree.SetRequestFrameCallback(wake.Set);
                using var scene = new Scene(editor, nested, rtl);
                var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                void Frame()
                {
                    context.NewFrame(new(.1f, new(700, 500), new(1, 1)), idle, new(0, 0, 700, 500), UiTextSettings.Default);
                    context.Render(scene);
                    context.GetDrawData().ReleasePooled();
                }
                UiAccessibilityElement Root() => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == Scene.DocumentName);
                UiAccessibilityElement Picture(string name) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Image && e.Node.Name == name);
                UiAccessibilityResult Reveal(UiAccessibilityElement picture, UiAccessibilityScrollAlignment alignment)
                {
                    wake.Reset();
                    var task = Task.Run(() => tree.RequestScrollIntoView(picture.Node.Id, picture.RuntimeId, alignment));
                    if (!wake.Wait(1000))
                        throw new InvalidOperationException(task.IsCompleted ? "Image Component admission: " + task.Result : "Image Component did not wake its frame.");
                    for (var frame = 0; frame < 4 && !task.IsCompleted; frame++)
                    {
                        Frame();
                        task.Wait(10);
                    }
                    if (!task.Wait(1000))
                        throw new TimeoutException("Image Component reveal exceeded its publication budget.");
                    return task.Result;
                }
                void Reset()
                {
                    if (scene.Outer is not null)
                        check(Reveal(Root(), new(UiAccessibilityScrollPlacement.Nearest)) == UiAccessibilityResult.Success,
                            "The nested document is exposed before requesting its own scroll reset");
                    var root = Root();
                    check(tree.RequestScroll(root.Node.Id, root.RuntimeId, UiAccessibilityScrollCommand.ByPercent(0, 0), wait: false) == UiAccessibilityResult.Success,
                        "The real image document accepts a bounded two-axis reset");
                    Frame();
                }
                UiRect Viewport()
                {
                    if (!nested)
                    {
                        var root = Root().Node;
                        var viewport = root.Scroll!.Value.Viewport;
                        return new(root.Bounds.X + 12, root.Bounds.Y + 10, viewport.X, viewport.Y);
                    }
                    var outer = tree.CaptureSnapshot().Elements.First(e => e.Node.Scroll is not null).Node;
                    var size = outer.Scroll!.Value.Viewport;
                    return new(outer.Bounds.X + (rtl ? outer.Bounds.Width - size.X : 0), outer.Bounds.Y, size.X, size.Y);
                }
                Frame();
                if (scene.Outer is not null)
                {
                    scene.Outer.Offset = default;
                    Frame();
                }
                var original = Root().Node.TextInfo!;
                var originalId = Root().RuntimeId;
                var scope = $"editor={editor},nested={nested},RTL={rtl}";
                foreach (var name in new[] { "target", "linked", "table" })
                {
                    foreach (var placement in Enum.GetValues<UiAccessibilityScrollPlacement>())
                    {
                        Reset();
                        var image = Picture(name);
                        var identity = image.RuntimeId;
                        check(image.Node.CanScrollIntoView, "Images inherit the existing shared viewport reveal contract, " + name + "/" + scope);
                        check(Reveal(image, new(placement)) == UiAccessibilityResult.Success, "Image Component placement completes, " + placement + "/" + name + "/" + scope);
                        image = Picture(name);
                        var box = image.Node.ImageInfo!.Value.Bounds;
                        var visible = Viewport();
                        check(!image.Node.Offscreen && image.Node.Bounds.Width == 120 && image.Node.Bounds.Height == 60
                            && box.X >= visible.X - .05f && box.Y >= visible.Y - .05f
                            && box.X + box.Width <= visible.X + visible.Width + .05f && box.Y + box.Height <= visible.Y + visible.Height + .05f,
                            $"The complete rendered image is visible, {placement}/{name}/{scope}; image={box}; viewport={visible}");
                        var left = placement is UiAccessibilityScrollPlacement.TopLeft or UiAccessibilityScrollPlacement.LeftEdge;
                        var top = placement is UiAccessibilityScrollPlacement.TopLeft or UiAccessibilityScrollPlacement.TopEdge;
                        var right = placement is UiAccessibilityScrollPlacement.BottomRight or UiAccessibilityScrollPlacement.RightEdge;
                        var bottom = placement is UiAccessibilityScrollPlacement.BottomRight or UiAccessibilityScrollPlacement.BottomEdge;
                        check((!left || MathF.Abs(box.X - visible.X) < .05f) && (!top || MathF.Abs(box.Y - visible.Y) < .05f)
                            && (!right || MathF.Abs(box.X + box.Width - visible.X - visible.Width) < .05f)
                            && (!bottom || MathF.Abs(box.Y + box.Height - visible.Y - visible.Height) < .05f),
                            $"Image edges reach the requested actual viewport, {placement}/{name}/{scope}; image={box}; viewport={visible}");
                        check(image.RuntimeId == identity && Root().RuntimeId == originalId && Root().Node.TextInfo!.Content.Text == Scene.Source
                            && Root().Node.TextInfo!.CaretUtf16 == original.CaretUtf16 && Root().Node.TextInfo!.AnchorUtf16 == original.AnchorUtf16
                            && !Root().Node.Focused && !image.Node.Focused && scene.Navigations == 0,
                            "Component image reveal preserves source, caret, selection, identity and focus without navigating a linked image, " + name + "/" + scope);
                    }
                    Reset();
                    check(Reveal(Picture(name), new(Scene.Point)) == UiAccessibilityResult.Success, "Image Component-to-point completes, " + name + "/" + scope);
                    var aligned = Picture(name).Node.ImageInfo!.Value.Bounds;
                    check(MathF.Abs(aligned.X - Scene.Point.X) < .05f && MathF.Abs(aligned.Y - Scene.Point.Y) < .05f,
                        $"The actual image reaches its client point, {name}/{scope}; actual={aligned}; point={Scene.Point}");
                    var before = aligned;
                    var invalid = Reveal(Picture(name), new(new UiVector2(600, 450)));
                    var afterInvalid = Picture(name).Node.ImageInfo!.Value.Bounds;
                    check(invalid == UiAccessibilityResult.InvalidArgument && afterInvalid == before,
                        $"An image target outside the outer viewport fails without moving its geometry, {name}/{scope}; result={invalid}; before={before}; after={afterInvalid}");
                }
                check(scene.Value == Scene.Source && scene.Widget.Document.ParseCount == 1 && scene.Resolutions == 1 && scene.Navigations == 0,
                    "Image scrolling reuses parsed Markdown and resolved image resources, " + scope);
            }
    }

    internal sealed class Scene : UiScreen, IDisposable
    {
        internal const string DocumentName = "Component images";
        internal static readonly UiVector2 Point = new(80, 70);
        private static readonly string Line = new('W', 80);
        private static readonly string Prefix = new('W', 60);
        internal static readonly string Source = "# 정세일 😀\n\n" + string.Join("\n\n", Enumerable.Repeat(Line, 8))
            + "\n\n" + Prefix + " ![target](asset.png) " + Prefix
            + "\n\n" + Prefix + " [![linked](asset.png)](https://example.test/image) " + Prefix
            + "\n\n" + Line + " | image\n--- | ---\n" + Line + " | ![table](asset.png)"
            + "\n\n" + string.Join("\n\n", Enumerable.Repeat(Line, 8));
        internal readonly UiMarkdownWidget Widget;
        internal readonly UiScrollView? Outer;
        internal string Value = Source;
        internal int Navigations, Resolutions;
        internal ManualResetEventSlim FirstFrame { get; } = new();

        internal Scene(bool editor = false, bool nested = false, bool rtl = false)
        {
            var picture = MarkdownImageAccessibilityTests.Picture();
            var binding = new UiBinding<string>(() => Value, value => Value = value);
            var options = new UiMarkdownOptions(Width: 400, Height: 260) { Wrap = false };
            Widget = editor ? new UiMarkdownEditor(DocumentName, binding, options) : new UiMarkdownView(DocumentName, binding, options);
            Widget.ResolveImage = uri => { Resolutions++; return uri.OriginalString == "asset.png" ? picture : null; };
            if (Widget is UiMarkdownView view)
                view.Navigate = _ => Navigations++;
            if (nested)
                Outer = new(Dux.Padding(Widget, new(300, 300, 400, 400)), new(UiScrollAxis.Both, 260, 200)
                {
                    RightToLeft = rtl
                });
        }

        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(10, 10));
            if (Outer is not null)
                Outer.Render(ui);
            else
                Widget.Render(ui);
            FirstFrame.Set();
        }

        public void Dispose()
        {
            if (Outer is not null)
                Outer.Dispose();
            else
                Widget.Dispose();
            FirstFrame.Dispose();
        }
    }
}
