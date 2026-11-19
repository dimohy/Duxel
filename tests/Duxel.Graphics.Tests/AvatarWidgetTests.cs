using System.Text.Json;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class AvatarWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var (name, first, rtl, expected) in new[]
        {
            ("Ada Lovelace", false, false, "AL"), ("Ada Augusta Lovelace", false, false, "AL"),
            ("Ada Augusta Byron Lovelace", false, false, "A"), ("Ada Lovelace", true, false, "A"),
            ("Ada Lovelace", false, true, "LA"), (" Ada   Lovelace (Team) ", false, false, "AL"),
            ("123 456 789", false, false, ""), ("김민수", false, false, "김"),
            ("정세일", false, false, "정"), ("정세일", false, false, "정"),
            ("山田 太郎", false, false, ""), ("", false, false, ""),
            ("\U00020000 Smith", false, false, "\U00020000S"), ("!!!", false, false, "")
        }) check(UiAvatarInitials.FromName(name, first, rtl) == expected, "Avatar pinned Unicode initials : " + name);
        using (var palette = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AvatarPalette.json"))))
        {
            var index = 0;
            static UiColor Color(string hex)
            {
                var bytes = Convert.FromHexString(hex.AsSpan(1));
                return new(bytes[0], bytes[1], bytes[2]);
            }
            foreach (var reference in palette.RootElement.EnumerateArray())
            {
                var color = (UiAvatarColor)(3 + index++);
                var tokens = reference.GetProperty("light");
                var light = UiAvatarPalette.Resolve(UiFluentColorScheme.Light, color);
                var dark = UiAvatarPalette.Resolve(UiFluentColorScheme.Dark, color);
                check(light == new UiAvatarPaint(Color(tokens.GetProperty("shade30").GetString()!),
                    Color(tokens.GetProperty("tint40").GetString()!), Color(tokens.GetProperty("primary").GetString()!)),
                    "Avatar light named palette matches pinned token source : " + color);
                check(dark == new UiAvatarPaint(Color(tokens.GetProperty("tint40").GetString()!),
                    Color(tokens.GetProperty("shade30").GetString()!), Color(tokens.GetProperty("tint30").GetString()!)),
                    "Avatar dark named palette matches pinned token source : " + color);
            }
            check(index == 30, "Avatar validates all thirty named colors in both themes");
        }
        foreach (var size in Enum.GetValues<UiAvatarSize>())
        {
            var options = new UiAvatarOptions(size);
            options.Validate();
            check(options.PixelSize is 16 or 20 or 24 or 28 or 32 or 36 or 40 or 48 or 56 or 64 or 72 or 96 or 120 or 128,
                "Avatar supported size : " + size);
        }
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var native = new WindowsAccessibility(0);
        context.SetAccessibilityTree(native.Tree);
        using var avatar = Fluent.Avatar("Ada Lovelace");
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiDrawData Frame(Action<UiImmediateContext>? action = null, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (action is null) avatar.Render(ui); else action(ui);
            }));
            return context.GetDrawData();
        }
        UiAccessibilityElement Element()
        {
            for (var id = native.Tree.Root.First; id is not null;)
            {
                if (!native.Tree.TryGet(id, out var element)) break;
                if (element.Node.Role == UiAccessibilityRole.Image) return element;
                id = element.Next;
            }
            throw new InvalidOperationException("Avatar image accessibility element is missing.");
        }
        using (var measured = Frame(ui => check(avatar.Measure(ui, new(300, 200)) == new UiVector2(32, 32),
            "Avatar default natural measurement is32px without allocating an image texture")))
            check(measured.TextureUpdates.Count == 0, "Avatar pure measurement does not upload pixels");
        using (var first = Frame())
        {
            var node = Element().Node;
            check(node.Name == "Ada Lovelace" && !node.Focusable && !node.ValuePattern,
                "Avatar exposes full person name as one passive image without focus or Value pattern");
        }
        var stable = requests;
        using (var repeated = Frame()) { }
        check(requests == stable && context.State.NextScheduledFrameTimeSeconds is null,
            "unchanged Avatar does not schedule animation or periodic frames");
        var focused = context.State.FocusedId;
        using (var pointer = Frame(input : idle with { MousePosition = new(10, 10), LeftMouseDown = true, LeftMousePressed = true })) { }
        check(context.State.FocusedId == focused, "passive Avatar pointer press does not take keyboard focus");
        avatar.Name = "Grace Hopper";
        using (var changed = Frame()) check(Element().Node.Name == "Grace Hopper" && avatar.DisplayInitials == "GH",
            "Avatar name mutation updates both accessible name and cached initials");
        avatar.Options = avatar.Options with { Size = UiAvatarSize.Size16 };
        check(avatar.DisplayInitials == "G", "Avatar16 uses a single automatically derived initial");
        avatar.Initials = "김";
        check(avatar.DisplayInitials == "김", "explicit Avatar initials support Korean text");
        avatar.Initials = null;
        avatar.Options = default;
        var source = new UiImageSource(new(2, 1, [255, 0, 0, 255, 0, 255, 0, 255]));
        avatar.Source = source;
        UiRectFilledPrimitive[] snapshot;
        using (var image = Frame())
        {
            snapshot = image.DrawLists.SelectMany(list => list.RectFilledPrimitives?.AsSpan().ToArray() ?? []).ToArray();
            var commands = image.DrawLists.SelectMany(list => list.Commands).Where(command => command.IsImagePrimitive).ToArray();
            check(commands.Length == 1 && image.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.Create) == 1,
                "Avatar image uses the common analytic image mask and one native-resolution texture");
            check(snapshot.Any(record => record.Rect == new UiRect(.25f, 0, .5f, 0)),
                "Avatar image uses centered Cover UVs rather than stretching a wide source");
            avatar.Options = avatar.Options with { Shape = UiAvatarShape.Square };
            using var square = Frame();
            check(square.TextureUpdates.Count == 0, "Avatar shape changes retain the source texture without another upload");
            check(image.DrawLists.SelectMany(list => list.RectFilledPrimitives?.AsSpan().ToArray() ?? []).SequenceEqual(snapshot),
                "Avatar later shape changes cannot mutate queued primitive records");
        }
        stable = requests;
        avatar.Name = avatar.Name;
        avatar.Options = avatar.Options;
        avatar.Source = avatar.Source;
        avatar.Initials = avatar.Initials;
        avatar.Icon = avatar.Icon;
        check(requests == stable, "identical Avatar properties do not request frames");
        using (var shared = Fluent.Image(source))
        using (var frame = Frame(ui => { avatar.Render(ui); shared.Render(ui); }))
            check(frame.TextureUpdates.Count == 0, "Avatar and ordinary Image share the same source texture cache");
        using (var allocated = Dux.SizedBox(Fluent.Avatar(""), 64, 48))
        using (var frame = Frame(allocated.Render))
            check(Element().Node.Bounds.Width == 64 && Element().Node.Bounds.Height == 48,
                "Avatar respects its parent allocation and keeps one bounded accessibility image");
        static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
        }
        check(Throws<ArgumentOutOfRangeException>(() => avatar.Options = new((UiAvatarSize)17)), "invalid Avatar size fails explicitly");
        check(Throws<ArgumentOutOfRangeException>(() => avatar.Options = new(Color : (UiAvatarColor)99)), "invalid Avatar color fails explicitly");
        foreach (var ringSize in Enum.GetValues<UiAvatarSize>())
        {
            avatar.Options = new(ringSize, ShowActivityRing : true);
            var edge = avatar.Options.PixelSize;
            var stroke = edge <= 48 ? 2 : edge <= 64 ? 3 : 4;
            using var measuredRing = Frame(ui => check(avatar.Measure(ui, new(300, 200))
                == new UiVector2(edge + stroke * 4, edge + stroke * 4),
                "Avatar activity ring measures its complete visual extent : " + ringSize));
        }
        avatar.Options = new(ShowActivityRing : true);
        avatar.Name = "Ada Lovelace";
        avatar.ActivityText = "활동 중";
        using (var activeRing = Frame())
        {
            var node = Element().Node;
            check(node.Name == "Ada Lovelace 활동 중" && !node.Focusable && !node.ValuePattern,
                "Avatar activity ring adds localized meaning to its existing passive image");
            var rings = activeRing.DrawLists.SelectMany(list => list.Commands
                .Where(command => command.Kind == UiDrawCommandKind.RectFilledPrimitives
                    && command.VertexOffset == (uint)UiRectGeometryVariant.Border)
                .SelectMany(command => Enumerable.Range((int)command.IndexOffset, (int)command.ElementCount)
                    .Select(index => list.RectFilledPrimitives![index]))).ToArray();
            check(rings.Length == 1 && rings[0].Rect.Width == 40 && rings[0].BorderThickness == 2,
                "Avatar activity ring uses the common analytic border-only primitive");
            check(activeRing.TextureUpdates.Count == 0,
                "Avatar activity ring reuses its existing image lease without uploading its pixels again");
        }
        stable = requests;
        avatar.ActivityText = avatar.ActivityText;
        avatar.Options = avatar.Options;
        using (var repeatedRing = Frame()) { }
        check(requests == stable && context.State.NextScheduledFrameTimeSeconds is null,
            "unchanged activity ring neither requests a frame nor schedules animation");
        avatar.Options = avatar.Options with { ShowActivityRing = false };
        using (var inactiveRing = Frame()) check(Element().Node.Name == "Ada Lovelace",
            "hiding the activity ring removes only the activity wording");
        check(Throws<ArgumentException>(() => avatar.ActivityText = " "),
            "empty localized activity wording fails explicitly");
        avatar.Source = source;
        avatar.PresenceText = "사용 가능";
        long sourceUploads = 0;
        using (var frame = Frame(ui => { avatar.Render(ui); sourceUploads = ui.ImageTextureStatistics.Uploads; })) { }
        foreach (var bodySize in Enum.GetValues<UiAvatarSize>())
        {
            avatar.Options = new(bodySize, ShowActivityRing : true, Presence : new());
            var pixels = avatar.Options.PixelSize;
            var expectedBadge = pixels >= 96 ? 28 : pixels >= 64 ? 20 : pixels >= 56 ? 16
                : pixels >= 40 ? 12 : pixels >= 28 ? 10 : 6;
            long currentUploads = 0;
            using var statusFrame = Frame(ui => { avatar.Render(ui); currentUploads = ui.ImageTextureStatistics.Uploads; });
            var masks = statusFrame.DrawLists.SelectMany(list => list.Commands
                .Where(command => command.CircleCutoutIndexPlusOne != 0)
                .Select(command => list.RectFilledPrimitives![(int)command.CircleCutoutIndexPlusOne - 1])).ToArray();
            check(masks.Length >= 2 && masks.All(mask => mask.Rect.Width == expectedBadge * .5f
                + (expectedBadge >= 20 ? 2 : 1)),
                "Avatar image and activity ring share the pinned automatic badge cutout : " + bodySize);
            check(statusFrame.DrawLists.SelectMany(list => list.Commands).Any(command => command.CircleCutoutIndexPlusOne == 0),
                "Avatar badge itself paints outside the subtraction scope : " + bodySize);
            check(currentUploads == sourceUploads, "Avatar badge mutation retains the existing image lease : " + bodySize);
        }
        using (var frame = Frame()) check(Element().Node.Name == "Ada Lovelace 사용 가능 활동 중",
            "Avatar combines localized status and activity into its single passive image name");
        avatar.AccessibleName = "담당자";
        using (var frame = Frame()) check(Element().Node.Name == "담당자", "Avatar explicit accessible name overrides combined semantics");
        avatar.AccessibleName = null;
        avatar.Options = new(UiAvatarSize.Size16, Presence : new(Size : UiBadgeSize.ExtraLarge));
        using (var measured = Frame(ui => check(avatar.Measure(ui, new(300, 200)) == new UiVector2(28, 28),
            "Avatar reserves a badge larger than its body in natural measurement"))) { }
        using (var frame = Frame()) check(Element().Node.Bounds.Width == 28 && Element().Node.Bounds.Height == 28,
            "Avatar accessibility bounds include an explicitly enlarged badge");
        avatar.Options = avatar.Options with { RightToLeft = true };
        using (var measured = Frame(ui => check(avatar.Measure(ui, new(300, 200)) == new UiVector2(28, 28),
            "Avatar oversized RTL badge preserves the complete visual union"))) { }
        using (var tinyOwner = Dux.SizedBox(Fluent.Avatar("Allocated", new(UiAvatarSize.Size16,
            ShowActivityRing : true, Presence : new(Size : UiBadgeSize.ExtraLarge))), 11, 9))
        using (var tinyFrame = Frame(tinyOwner.Render))
        {
            check(Element().Node.Bounds.Width == 11 && Element().Node.Bounds.Height == 9,
                "Avatar presence and activity ring retain the actual tight rectangular parent allocation");
            check(tinyFrame.DrawLists.SelectMany(list => list.Commands).All(command =>
                command.ClipRect.X >= 0 && command.ClipRect.Y >= 0
                && command.ClipRect.X + command.ClipRect.Width <= 11
                && command.ClipRect.Y + command.ClipRect.Height <= 9),
                "Avatar enlarged badge and body stay clipped inside the actual allocated rectangle");
        }
        stable = requests;
        avatar.PresenceText = avatar.PresenceText;
        avatar.Options = avatar.Options;
        using (var repeated = Frame()) { }
        check(requests == stable && context.State.NextScheduledFrameTimeSeconds is null,
            "unchanged Avatar presence neither requests frames nor starts periodic rendering");
        avatar.PresenceText = null;
        avatar.Options = avatar.Options with { Presence = new(UiPresenceStatus.Busy, OutOfOffice : true) };
        using (var frame = Frame()) check(Element().Node.Name == "Ada Lovelace busy out of office",
            "Avatar default presence name tracks status and out-of-office mutations");
        avatar.Options = avatar.Options with { Presence = null };
        using (var frame = Frame()) check(Element().Node.Name == "Ada Lovelace", "removing presence also removes its accessible status");
        check(Throws<ArgumentException>(() => avatar.PresenceText = " "), "Avatar rejects empty localized presence wording");
        check(Throws<ArgumentOutOfRangeException>(() => avatar.Options = new(Presence : new((UiPresenceStatus)99))),
            "Avatar rejects invalid presence status before mutation");
        check(Throws<ArgumentOutOfRangeException>(() => avatar.Options = new(Presence : new(Size : (UiBadgeSize)99))),
            "Avatar rejects invalid explicit badge size before mutation");
        foreach (var sizeWithShadow in Enum.GetValues<UiAvatarSize>())
        {
            avatar.Options = new(sizeWithShadow, ShowActivityShadow : true);
            var pixelSize = avatar.Options.PixelSize;
            var expectedBlur = pixelSize <= 28 ? 4 : pixelSize <= 48 ? 8 : pixelSize <= 64 ? 16 : 28;
            using var measured = Frame(ui => check(avatar.Measure(ui, new(300, 300))
                == new UiVector2(pixelSize + expectedBlur * 4 + 2, pixelSize + expectedBlur * 4 + 2),
                "Avatar reserves the complete pinned shadow support : " + sizeWithShadow));
        }
        avatar.Options = new(UiAvatarSize.Size64, ShowActivityShadow : true, Presence : new());
        avatar.PresenceText = "사용 가능";
        avatar.ActivityText = "활동 중";
        UiRasterTextureStatistics shadowStats = default;
        using (var frame = Frame(ui => { avatar.Render(ui); shadowStats = ui.ShadowTextureStatistics; }))
            check(Element().Node.Name == "Ada Lovelace 사용 가능 활동 중" && shadowStats.Uploads > 0,
                "Avatar activity shadow uses the shared resource and single localized image name");
        var shadowUploads = shadowStats.Uploads;
        stable = requests;
        using (var frame = Frame(ui => { avatar.Render(ui); shadowStats = ui.ShadowTextureStatistics; }))
            check(shadowStats.Uploads == shadowUploads && requests == stable && context.State.NextScheduledFrameTimeSeconds is null,
                "Avatar shadow reuses its context texture and starts no periodic animation");
        avatar.Options = new(UiAvatarSize.Size32, ShowActivityRing: true, ShowActivityShadow: true, IsInactive: true);
        using (var measured = Frame(ui => check(avatar.Measure(ui, new(300, 300)) == new UiVector2(32, 32),
            "inactive Avatar does not reserve its hidden activity decorations"))) { }
        using (var inactive = Frame())
        {
            var captured = inactive.TextureUpdates.Single(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup).PaintGroup!.Content;
            check(captured.PixelWidth == 28 && captured.PixelHeight == 28 && captured.TextMode == UiPaintGroupTextMode.Projected,
                "inactive Avatar captures at its final28px resolution with projected glyph coverage");
            check(inactive.DrawLists.SelectMany(list => list.Commands).Count(command => command.IsBackdropComposite) == 1,
                "inactive Avatar composites the complete visual once instead of changing every command opacity");
            check(Element().Node.Name == "Ada Lovelace" && Element().Node.Bounds.Width == 32,
                "inactive Avatar preserves its layout accessibility allocation and removes hidden activity wording");
        }
        stable = requests;
        using (var repeated = Frame()) check(!repeated.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup)
            && requests == stable && context.State.NextScheduledFrameTimeSeconds is null,
            "unchanged inactive image reuses its capture without scheduling frames");
        avatar.Source = new(new(1, 1, [0, 0, 255, 255]));
        using (var replaced = Frame()) check(replaced.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup) == 1,
            "inactive Avatar source identity replacement invalidates its captured content");
        avatar.Source = null;
        avatar.Initials = "";
        avatar.Icon = UiMenuIcon.Create<MutableIcon, MutableIcon>();
        using (var opaque = Frame()) { }
        var opaquePaints = MutableIcon.Paints;
        using (var opaque = Frame()) check(MutableIcon.Paints > opaquePaints
            && opaque.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup),
            "opaque external icon painters run on requested frames until a content version is supplied");
        avatar.Icon = avatar.Icon.WithContentVersion(1);
        using (var versioned = Frame()) { }
        opaquePaints = MutableIcon.Paints;
        using (var versioned = Frame()) check(MutableIcon.Paints == opaquePaints
            && !versioned.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup),
            "an explicitly versioned icon retains its pure paint capture");
        avatar.Icon = avatar.Icon.WithContentVersion(2);
        using (var versioned = Frame()) check(MutableIcon.Paints > opaquePaints,
            "changing the explicit icon version evaluates the painter again");
        var document = new MutableSvg();
        using (var svgIcon = new UiSvgIcon(document))
        {
            avatar.Icon = svgIcon.MenuIcon;
            using (var firstSvg = Frame()) { }
            using (var retainedSvg = Frame()) check(!retainedSvg.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup),
                "SVG icon document revision participates in inactive Avatar capture retention");
            stable = requests;
            document.Change();
            using (var changedSvg = Frame()) check(requests > stable
                && changedSvg.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup),
                "SVG document change wakes the application and invalidates the inactive Avatar capture");
        }
        avatar.Icon = default;
        avatar.Options = avatar.Options with { IsInactive = false };
        using (var restored = Frame()) check(!restored.DrawLists.SelectMany(list => list.Commands).Any(command => command.IsBackdropComposite)
            && Element().Node.Name == "Ada Lovelace 활동 중",
            "restoring Avatar activity releases the group and restores its original semantics");
        avatar.Dispose();
        check(Throws<ObjectDisposedException>(() => avatar.Name = "Disposed"), "disposed Avatar rejects mutation");
        check(Fluent.ComponentNames.Contains("Avatar"), "Avatar is exposed through the generated Fluent catalog");
        using var custom = new UiAvatar<DynamicPresentation>("Custom");
        using (var frame = Frame(ui => check(custom.Measure(ui, new(300, 200)) == new UiVector2(24, 24)
            && DynamicPresentation.Paints == 0, "custom Avatar measurement is pure and independent of paint"))) { }
        DynamicPresentation.NaturalSize = new(48, 36);
        using (var frame = Frame(ui => check(custom.Measure(ui, new(300, 200)) == new UiVector2(48, 36),
            "custom Avatar presentation without a retention promise observes changed measurement state"))) { }
        using (var owner = Dux.SizedBox(custom, 80, 40))
        using (var frame = Frame(owner.Render))
            check(DynamicPresentation.Bounds.Width == 80 && DynamicPresentation.Bounds.Height == 40,
                "custom Avatar presentation receives the actual parent allocation");
        using var invalid = new UiAvatar<DynamicPresentation>("Invalid");
        DynamicPresentation.NaturalSize = new(float.NaN, 32);
        using (var frame = Frame(ui => check(Throws<InvalidOperationException>(() => invalid.Measure(ui, new(300, 200))),
            "invalid custom Avatar measurement fails before layout and painting"))) { }
        DynamicPresentation.NaturalSize = new(24, 24);
    }
    private readonly struct MutableIcon : IUiButtonIcon
    {
        internal static int Paints;
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
        {
            Paints++;
            context.Frame(bounds, color, default, 0, 0);
        }
    }
    private sealed class MutableSvg : IUiGpuSvgDocument
    {
        public UiGpuVectorScene GpuScene { get; } = GpuVectorTestScenes.Solid(new(16, 16));
        public UiVector2 IntrinsicSize => new(16, 16);
        public long Revision { get; private set; }
        public event Action? Changed;
        internal void Change() { Revision++; Changed?.Invoke(); }
        public UiSvgRaster Rasterize(int width, int height)
        {
            var pixels = new byte[checked(width * height * 4)];
            for (var index = 0; index < pixels.Length; index += 4) { pixels[index] = 255; pixels[index + 3] = 255; }
            return new(width, height, pixels);
        }
    }
    private readonly struct DynamicPresentation : IUiAvatarPresentation
    {
        internal static UiVector2 NaturalSize = new(24, 24);
        internal static UiRect Bounds;
        internal static int Paints;
        public static UiVector2 Measure(UiControlContext context, UiAvatarOptions options) => NaturalSize;
        public static void Paint(UiControlContext context, UiRect bounds, UiAvatarContent content, UiAvatarOptions options)
        {
            Bounds = bounds;
            Paints++;
            context.Frame(bounds, new(255, 0, 0), default, 0, 0);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
