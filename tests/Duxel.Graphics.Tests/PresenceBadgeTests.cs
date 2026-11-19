using Duxel.Core;
using Duxel.Platform.Windows;

internal static class PresenceBadgeTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var native = new WindowsAccessibility(0);
        context.SetAccessibilityTree(native.Tree);
        using var badge = Fluent.PresenceBadge();
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext>? draw = null, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (draw is null) badge.Render(ui); else draw(ui);
            }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityNode Element()
        {
            for (var id = native.Tree.Root.First; id is not null;)
            {
                if (!native.Tree.TryGet(id, out var element)) break;
                if (element.Node.Role == UiAccessibilityRole.Image) return element.Node;
                id = element.Next;
            }
            throw new InvalidOperationException("PresenceBadge image node was not published.");
        }
        check(badge.Options == default && badge.StatusText == "available", "PresenceBadge defaults to available and medium");
        foreach (var size in Enum.GetValues<UiBadgeSize>())
        {
            badge.Options = new(Size : size);
            var edge = size switch { UiBadgeSize.Tiny => 6, UiBadgeSize.ExtraSmall => 10, UiBadgeSize.Small => 12,
                UiBadgeSize.Medium => 16, UiBadgeSize.Large => 20, _ => 28 };
            Frame(ui => check(badge.Measure(ui, new(300, 200)) == new UiVector2(edge, edge),
                "PresenceBadge pure natural measurement : " + size));
        }
        foreach (var status in Enum.GetValues<UiPresenceStatus>())
        {
            foreach (var oof in new[] { false, true })
            {
                badge.Options = new(status, OutOfOffice : oof);
                Frame();
                var node = Element();
                check(node.Name == badge.StatusText && node.Role == UiAccessibilityRole.Image
                    && !node.Focusable && !node.ValuePattern, "PresenceBadge status publishes a passive named image : " + status + "/" + oof);
                check(!oof || status == UiPresenceStatus.OutOfOffice || badge.StatusText.EndsWith(" out of office", StringComparison.Ordinal),
                    "PresenceBadge out-of-office description is included exactly once : " + status + "/" + oof);
            }
        }
        badge.Options = default;
        Frame();
        var stable = requests;
        Frame(); Frame();
        check(requests == stable && context.State.NextScheduledFrameTimeSeconds is null,
            "unchanged PresenceBadge schedules neither animation nor periodic frames");
        var focused = context.State.FocusedId;
        Frame(input : idle with { MousePosition = new(5, 5), LeftMouseDown = true, LeftMousePressed = true });
        check(context.State.FocusedId == focused, "PresenceBadge pointer press does not acquire keyboard focus");
        badge.AccessibleName = "사용 가능";
        Frame();
        check(Element().Name == "사용 가능", "PresenceBadge accepts an explicit localized accessible name");
        badge.Icon = UiMenuIcon.Create<Probe, Probe>();
        Frame();
        check(Probe.Interaction == default && Probe.Bounds.Width == 16,
            "PresenceBadge borrows a custom icon without inventing hover or activation");
        using (var owner = Dux.SizedBox(badge, 64, 32))
        {
            Frame(owner.Render);
            check(Probe.Bounds == new UiRect(16, 0, 32, 32) && Element().Bounds.Width == 64,
                "PresenceBadge centers its glyph in a rectangular parent while preserving the assigned accessibility bounds");
        }
        static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
        }
        using var invalid = Fluent.PresenceBadge();
        check(Throws<ArgumentOutOfRangeException>(() => invalid.Options = new((UiPresenceStatus)99)), "invalid presence status fails explicitly");
        check(Throws<ArgumentOutOfRangeException>(() => invalid.Options = new(Size : (UiBadgeSize)99)), "invalid presence size fails explicitly");
        check(Fluent.ComponentNames.Contains("PresenceBadge"), "PresenceBadge is an exposed Badge variant");
        foreach (var size in new[] { 6, 8, 10, 16 })
        {
            var backend = WindowsPlatformTextBackend.Instance;
            var glyph = char.ConvertFromUtf32(63017);
            check(backend.TryLayout(new(UiFluentFilledIconFont.Path, null, glyph, size), out var layout),
                "small presence glyph pure layout is available : " + size);
            if (!backend.TryRasterize(new(UiFluentFilledIconFont.Path, null, glyph, size), out var pixels))
                throw new InvalidOperationException("Presence glyph rasterization failed.");
            try
            {
                check(layout.Width == pixels.Width && layout.Height == pixels.Height && layout.Advance == pixels.Advance,
                    "small presence glyph pure layout matches its actual raster extents : " + size);
                if (size < 10)
                {
                    var coverage = pixels.RgbaPixels.Span;
                    var matches = true;
                    for (var index = 0; index < coverage.Length; index += 4)
                        matches &= coverage[index] == coverage[index + 3] && coverage[index + 1] == coverage[index + 3]
                            && coverage[index + 2] == coverage[index + 3];
                    check(matches, "small grayscale glyph masks encode actual coverage in RGB rather than opaque white : " + size);
                }
            }
            finally { pixels.PixelOwner?.Dispose(); }
            if (size < 10)
            {
                var secondary = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgunbd.ttf");
                var text = glyph + "김";
                if (!backend.TryLayout(new(UiFluentFilledIconFont.Path, secondary, text, size), out var mixedLayout)
                    || !backend.TryRasterize(new(UiFluentFilledIconFont.Path, secondary, text, size), out var mixed))
                    throw new InvalidOperationException("Mixed presence/Korean glyph rasterization failed.");
                try
                {
                    check(mixedLayout.Width == mixed.Width && mixedLayout.Height == mixed.Height && mixedLayout.Advance == mixed.Advance,
                        "small mixed-font pure layout matches actual raster extents : " + size);
                    var coverage = mixed.RgbaPixels.Span;
                    var matches = true;
                    for (var index = 0; index < coverage.Length; index += 4)
                        matches &= coverage[index] == coverage[index + 3] && coverage[index + 1] == coverage[index + 3]
                            && coverage[index + 2] == coverage[index + 3];
                    check(matches, "small mixed-font grayscale masks preserve RGB coverage in both font runs : " + size);
                }
                finally { mixed.PixelOwner?.Dispose(); }
            }
        }
    }
    private readonly struct Probe : IUiButtonIcon
    {
        internal static UiRect Bounds;
        internal static UiButtonInteraction Interaction;
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
        {
            Bounds = bounds;
            Interaction = interaction;
            context.Frame(bounds, color, default, 0, 0);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
