using Duxel.Core;

internal static class TagMediaRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, Action<UiInputState> setInput, int width)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var avatar = Fluent.Avatar("정세일", new(Color: UiAvatarColor.Brand, Presence: new()));
        var media = UiTagMedia.Avatar(avatar);
        using var tag = Fluent.InteractionTag(new UiTagContent("정세일", Media: media), () => { });
        UiWidget current = tag;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        setInput(idle);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            current.Render(ui);
        }
        UiAccessibilityNode Find(string name)
        {
            UiAccessibilityNode? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name && entry.Node.Role is UiAccessibilityRole.Button or UiAccessibilityRole.Label)
                        found = entry.Node;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException(name);
        }
        foreach (var size in Enum.GetValues<UiTagSize>())
        {
            foreach (var shape in Enum.GetValues<UiTagShape>())
            {
                tag.Options = new(size, Shape: shape, RightToLeft: shape == UiTagShape.Circular);
                var name = "media-" + size + "-" + shape;
                var pixels = capture(name, Draw);
                check(pixels.AsSpan().SequenceEqual(capture(name + "-replay", Draw)), "GPU media tag replays its actual Korean avatar/presence pixels : " + name);
                var bounds = Find("정세일").Bounds;
                check(bounds.Height == (size == UiTagSize.Medium ? 32 : size == UiTagSize.Small ? 24 : 20)
                    && avatar.Options.Size == UiAvatarSize.Size32,
                    "GPU media adapts tag size without changing its borrowed avatar : " + name);
            }
        }
        tag.Options = new();
        var initials = capture("media-korean", Draw);
        avatar.Initials = "";
        var noInitials = capture("media-no-initials", Draw);
        check(!initials.AsSpan().SequenceEqual(noInitials), "GPU media paints actual Korean initial glyph pixels");
        avatar.Initials = null;
        using var image = new UiImage(new(new(1, 1, [230, 40, 60, 255])));
        using var imageTag = Fluent.Tag(new UiTagContent("Raster media", Media: UiTagMedia.Image(image)));
        current = imageTag;
        var raster = capture("media-raster", Draw);
        var imageBounds = Find("Raster media").Bounds;
        var pixel = ((int)(imageBounds.Y + 16) * width + (int)(imageBounds.X + 15)) * 4;
        check(raster[pixel] == 230 && raster[pixel + 1] == 40 && raster[pixel + 2] == 60,
            "GPU media uses the existing borrowed raster image pipeline with actual source colors");
        using var large = Fluent.Tag(new UiTagContent("Large media", Media: media));
        using var small = Fluent.Tag(new UiTagContent("Small media", Media: media), options: new(Size: UiTagSize.Small));
        using var extraSmall = Fluent.Tag(new UiTagContent("Extra-small media", Media: media), options: new(Size: UiTagSize.ExtraSmall));
        using var mixed = Dux.Column(new(Spacing: 8), large, small, extraSmall);
        current = mixed;
        avatar.Options = avatar.Options with
        {
            IsInactive = true
        };
        var inactive = capture("media-inactive-shared", Draw);
        check(inactive.AsSpan().SequenceEqual(capture("media-inactive-shared-replay", Draw)),
            "GPU shared inactive avatar owns independent composite resources for three tag sizes");
        avatar.Name = "김민수";
        var changed = capture("media-inactive-updated", Draw);
        check(!changed.AsSpan().SequenceEqual(inactive), "GPU retained media invalidates when its borrowed avatar content changes");
        avatar.Options = avatar.Options with
        {
            IsInactive = false,
            ShowActivityRing = true,
            ShowActivityShadow = true
        };
        var shadow = capture("media-active-shadow-shared", Draw);
        check(shadow.AsSpan().SequenceEqual(capture("media-active-shadow-shared-replay", Draw)),
            "GPU shared media isolates shadow and inactive resource lifetimes across tag sizes");
        current = imageTag;
        mixed.Dispose();
        check(raster.AsSpan().SequenceEqual(capture("media-resources-released", Draw)),
            "GPU releasing tag media composites leaves the independent borrowed image intact");
    }
}
