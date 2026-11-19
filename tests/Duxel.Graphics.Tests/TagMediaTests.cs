using Duxel.Core;

internal static class TagMediaTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var avatar = new UiAvatar<TraceAvatar>("정세일", new(Color: UiAvatarColor.Brand));
        var media = UiTagMedia.Avatar(avatar);
        using var tag = Fluent.Tag(new UiTagContent("정세일", "담당자", Media: media));
        UiWidget current = tag;
        void Frame()
        {
            context.NewFrame(new(0, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); current.Render(ui); }));
            using var data = context.GetDrawData();
        }
        foreach (var size in Enum.GetValues<UiTagSize>())
        {
            foreach (var shape in Enum.GetValues<UiTagShape>())
            {
                tag.Options = new(size, Shape: shape, RightToLeft: shape == UiTagShape.Circular);
                Frame();
                var expected = size == UiTagSize.Medium ? 28 : size == UiTagSize.Small ? 20 : 16;
                check(TraceAvatar.LastOptions.PixelSize == expected && TraceAvatar.LastBounds.Width == expected
                    && TraceAvatar.LastOptions.Shape == (shape == UiTagShape.Circular ? UiAvatarShape.Circular : UiAvatarShape.Square)
                    && TraceAvatar.LastOptions.RightToLeft == tag.Options.RightToLeft,
                    "Tag media uses pinned avatar size/shape/direction defaults : " + size + " / " + shape);
                check(TraceAvatar.LastContent.Initials == "정" && avatar.Options.PixelSize == 32 && avatar.Options.Shape == UiAvatarShape.Circular,
                    "Tag media shares Korean initials without mutating the borrowed avatar options");
            }
        }
        avatar.Name = "Ada Lovelace";
        tag.Options = new(Size: UiTagSize.ExtraSmall);
        Frame();
        check(TraceAvatar.LastContent.Initials == "A", "Extra-small media recomputes the existing single-initial rule at16px");
        var cachedInitials = TraceAvatar.LastContent.Initials;
        Frame();
        check(ReferenceEquals(cachedInitials, TraceAvatar.LastContent.Initials), "Unchanged media initials reuse their derived string instead of reparsing names");
        tag.Options = new();
        Frame();
        check(TraceAvatar.LastContent.Initials == "AL", "Medium media preserves the existing multiword initials rule");
        var rejected = false;
        try
        {
            using var invalid = Fluent.Tag(new UiTagContent("Conflict", Icon: UiMenuIcon.Create<TraceIcon, TraceIcon>(), Media: media));
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Tag rejects overlapping icon/media content before layout");
        var selected = new[] { "a", "b" };
        var mediaCalls = 0;
        using var group = new UiFluentTagGroup("Media group", new(() => selected, next => selected = next),
            [new("a", "Alpha"), new("b", "Beta")], media: item => { mediaCalls++; return media; });
        current = group;
        Frame();
        Frame();
        check(mediaCalls == 2, "TagGroup resolves media once per option rather than per requested frame");
        using var large = Fluent.Tag(new UiTagContent("Large media", Media: media));
        using var small = Fluent.Tag(new UiTagContent("Small media", Media: media), options: new(Size: UiTagSize.Small));
        using var mixed = Dux.Column(new(Spacing: 8), large, small);
        avatar.Options = avatar.Options with
        {
            IsInactive = true
        };
        current = mixed;
        Frame();
        Frame();
        check(TraceAvatar.LastOptions.PixelSize == 20, "Shared inactive avatar media binds separate paint resources for different tag sizes");
        avatar.Options = avatar.Options with
        {
            IsInactive = false
        };
        using var image = new UiImage(new(new(1, 1, [220, 30, 50, 255])));
        using var imageTag = Fluent.Tag(new UiTagContent("Image media", Media: UiTagMedia.Image(image)));
        current = imageTag;
        Frame();
        imageTag.Dispose();
        current = image;
        Frame();
        check(true, "Disposing media tags preserves their borrowed image owner");
        tag.Dispose();
        current = avatar;
        Frame();
        check(TraceAvatar.LastOptions.PixelSize == 32, "Disposing a tag does not dispose its borrowed avatar");
        avatar.Dispose();
        current = group;
        var disposed = false;
        try
        {
            Frame();
        }
        catch (ObjectDisposedException) { disposed = true; }
        check(disposed, "Painting media after its avatar owner is disposed fails explicitly");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private readonly struct TraceIcon : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
        {
        }
    }
    private readonly struct TraceAvatar : IUiAvatarPresentation
    {
        internal static UiAvatarOptions LastOptions;
        internal static UiAvatarContent LastContent;
        internal static UiRect LastBounds;
        public static UiVector2 Measure(UiControlContext context, UiAvatarOptions options) => new(options.PixelSize, options.PixelSize);
        public static void Paint(UiControlContext context, UiRect bounds, UiAvatarContent content, UiAvatarOptions options)
        {
            LastOptions = options;
            LastContent = content;
            LastBounds = bounds;
            context.Frame(bounds, new(120, 70, 60), default, 0, 0);
        }
    }
}
