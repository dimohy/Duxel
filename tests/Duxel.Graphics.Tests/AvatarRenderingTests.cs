using System.Numerics;
using Duxel.Core;

internal static class AvatarRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int stride,
        Action<bool> configureKorean, Func<UiRendererFrameMetrics> metrics)
    {
        var source = new UiImageSource(new(1, 1, [255, 0, 0, 255]));
        using var avatar = Fluent.Avatar("Ada Lovelace");
        avatar.Source = source;
        long uploads = 0;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, context.Theme.WindowBg, default, 0, 0);
            ui.SetCursorScreenPos(new(24.25f, 24.25f));
            avatar.Render(ui);
            uploads = ui.ImageTextureStatistics.Uploads;
        }
        int At(int x, int y) => (y * stride + x) * 4;
        var firstUploads = 0L;
        foreach (var size in Enum.GetValues<UiAvatarSize>())
        {
            avatar.Options = new(size);
            var pixels = capture("size-" + size, Draw);
            if (firstUploads == 0) firstUploads = uploads;
            var edge = avatar.Options.PixelSize;
            var center = At(24 + edge / 2, 24 + edge / 2);
            check(pixels[center] == 255 && pixels[center + 1] == 0 && pixels[center + 2] == 0
                && pixels.AsSpan(At(24, 24), 3).SequenceEqual(pixels.AsSpan(At(0, 0), 3)),
                "GPU Avatar circle preserves its image center and transparent corner : " + size);
        }
        check(uploads == firstUploads, "GPU Avatar fourteen viewport sizes retain one native-resolution image upload");
        avatar.Options = new(UiAvatarSize.Size64);
        var circle = capture("circle", Draw);
        check(circle.AsSpan().SequenceEqual(capture("circle-replay", Draw)), "GPU unchanged Avatar image reproduces exact retained pixels");
        avatar.Options = avatar.Options with { Shape = UiAvatarShape.Square };
        var square = capture("square", Draw);
        check(!square.AsSpan().SequenceEqual(circle) && square[At(27, 27)] == 255,
            "GPU square Avatar uses its small Fluent corner rather than the circular image mask");
        avatar.Source = null;
        avatar.Options = new(UiAvatarSize.Size64, Color : UiAvatarColor.Brand);
        var initials = capture("initials", Draw);
        check(initials.AsSpan().SequenceEqual(capture("initials-replay", Draw)), "GPU Avatar text remains stable on requested frames");
        avatar.Name = "Grace Hopper";
        check(!initials.AsSpan().SequenceEqual(capture("renamed", Draw)), "GPU Avatar name mutation replaces actual initial glyphs");
        avatar.Name = "정세일";
        avatar.Initials = null;
        var automaticKorean = capture("automatic-korean-jeong", Draw);
        avatar.Initials = "정";
        check(avatar.DisplayInitials == "정" && automaticKorean.AsSpan().SequenceEqual(capture("explicit-korean-jeong-reference", Draw)),
            "GPU automatic Korean name uses the same complete Hangul glyph as explicit first-syllable initials");
        avatar.Initials = "";
        check(!automaticKorean.AsSpan().SequenceEqual(capture("korean-person-reference", Draw)),
            "GPU automatic Korean initials render actual text instead of the person icon");
        avatar.Name = "김민수";
        avatar.Initials = "김";
        var korean = capture("explicit-korean", Draw);
        configureKorean(false);
        var missingKorean = capture("missing-korean-font-reference", Draw);
        configureKorean(true);
        check(!korean.AsSpan().SequenceEqual(missingKorean),
            "GPU explicit Korean initial differs from the known missing-font box using registered application typography fonts");
        check(korean.AsSpan().SequenceEqual(capture("korean-font-restored", Draw)),
            "GPU restoring registered Korean typography reproduces the exact original Hangul pixels");
        avatar.Initials = "";
        var person = capture("person-icon", Draw);
        check(!korean.AsSpan().SequenceEqual(person), "GPU Avatar explicit Korean initials differ from the default person icon");
        check(person.AsSpan().SequenceEqual(capture("person-replay", Draw)), "GPU Avatar compiled person font icon remains stable");
        avatar.Options = avatar.Options with { Color = UiAvatarColor.Colorful };
        var colorful = capture("colorful", Draw);
        avatar.IdForColor = "a different stable identifier";
        check(!colorful.AsSpan().SequenceEqual(capture("color-identifier", Draw)),
            "GPU Avatar colorful identifier changes the actual background without changing its name");
        avatar.Source = source;
        foreach (var ringSize in Enum.GetValues<UiAvatarSize>())
        foreach (var ringShape in Enum.GetValues<UiAvatarShape>())
        {
            avatar.Options = new(ringSize, ringShape, UiAvatarColor.Brand, ShowActivityRing : true);
            var ringPixels = capture("activity-ring-" + ringSize + "-" + ringShape, Draw);
            var bodyEdge = avatar.Options.PixelSize;
            var stroke = bodyEdge <= 48 ? 2 : bodyEdge <= 64 ? 3 : 4;
            var extent = bodyEdge + stroke * 4;
            var centerX = 24 + extent / 2;
            var centerY = 24 + extent / 2;
            var ring = At(centerX, 25);
            var gap = At(centerX, 24 + stroke + 1);
            var center = At(centerX, centerY);
            check(!ringPixels.AsSpan(ring, 3).SequenceEqual(ringPixels.AsSpan(0, 3))
                && ringPixels.AsSpan(gap, 3).SequenceEqual(ringPixels.AsSpan(0, 3))
                && ringPixels[center] == 255 && ringPixels[center + 1] == 0,
                "GPU activity ring has a visible analytic border, transparent gap and intact image : " + ringSize + " " + ringShape);
        }
        var lastRing = capture("activity-ring-stable", Draw);
        check(lastRing.AsSpan().SequenceEqual(capture("activity-ring-replay", Draw)),
            "GPU unchanged activity ring reproduces exact retained pixels");
        check(uploads == firstUploads, "GPU activity ring size and shape changes do not reupload the source image");
        var showAvatar = true;
        void Pattern(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var paint = new UiControlContext(ui);
            for (var y = 0; y < 192; y += 8)
            for (var x = 0; x < 192; x += 8)
                paint.Frame(new(x, y, 8, 8), ((x + y) / 8 & 1) == 0 ? new(21, 87, 103) : new(79, 32, 91), default, 0, 0);
            ui.SetCursorScreenPos(new(24.25f, 24.25f));
            if (showAvatar) avatar.Render(ui);
        }
        showAvatar = false;
        var backdrop = capture("presence-pattern-reference", Pattern);
        showAvatar = true;
        foreach (var rtl in new[] { false, true })
        foreach (var shape in Enum.GetValues<UiAvatarShape>())
        foreach (var body in new[] { "image", "initials", "icon" })
        {
            avatar.Options = new(UiAvatarSize.Size96, shape, UiAvatarColor.Brand, rtl,
                Presence : new(UiPresenceStatus.Busy));
            avatar.Source = body == "image" ? source : null;
            avatar.Initials = body == "icon" ? "" : "AL";
            var masked = capture($"presence-{rtl}-{shape}-{body}", Pattern);
            var centerX = rtl ? 38 : 106;
            var gapX = rtl ? centerX + 15 : centerX - 15;
            check(masked.AsSpan(At(gapX, 106), 3).SequenceEqual(backdrop.AsSpan(At(gapX, 106), 3)),
                "GPU Avatar badge gap reveals the patterned background : " + rtl + " " + shape + " " + body);
            check(!masked.AsSpan(At(centerX, 106), 3).SequenceEqual(backdrop.AsSpan(At(centerX, 106), 3)),
                "GPU Avatar paints the presence glyph after leaving the cutout scope : " + rtl + " " + shape + " " + body);
            check(masked[At(centerX + 14, 106)] < masked[At(centerX, 106)],
                "GPU Avatar status circle right edge retains partial curved coverage instead of a fully colored clipped cap : " + rtl + " " + shape + " " + body);
            check(masked.AsSpan().SequenceEqual(capture($"presence-replay-{rtl}-{shape}-{body}", Pattern)),
                "GPU Avatar unchanged presence pixels replay exactly : " + rtl + " " + shape + " " + body);
        }
        avatar.Source = source;
        avatar.Options = new(UiAvatarSize.Size96, ShowActivityRing : true);
        var fullRing = capture("presence-ring-reference", Pattern);
        avatar.Options = avatar.Options with { Presence = new(UiPresenceStatus.Busy) };
        var cutRing = capture("presence-ring-cutout", Pattern);
        showAvatar = false;
        var badgeOnPattern = capture("presence-ring-badge-reference", ui =>
        {
            Pattern(ui);
            UiFluentPresenceBadgePresentation.Paint(new(ui), new(100.25f, 100.25f, 28, 28),
                new("busy", default), new(UiPresenceStatus.Busy, UiBadgeSize.ExtraLarge));
        });
        showAvatar = true;
        var transparentSamples = 0;
        var ringGapSamples = 0;
        var ringGapExact = true;
        for (var y = 24; y < 136; y++)
        for (var x = 24; x < 136; x++)
        {
            var distance = Vector2.Distance(new(x + .5f, y + .5f), new(114.25f, 114.25f));
            var radial = Vector2.Distance(new(x + .5f, y + .5f), new(80.25f, 80.25f));
            if (distance < 14.7f || distance > 15.2f || radial < 53 || radial > 55) continue;
            if (fullRing.AsSpan(At(x, y), 3).SequenceEqual(backdrop.AsSpan(At(x, y), 3))) continue;
            ringGapSamples++;
            ringGapExact &= cutRing.AsSpan(At(x, y), 3).SequenceEqual(badgeOnPattern.AsSpan(At(x, y), 3));
            if (badgeOnPattern.AsSpan(At(x, y), 3).SequenceEqual(backdrop.AsSpan(At(x, y), 3))) transparentSamples++;
        }
        check(ringGapSamples >= 2 && transparentSamples > 0 && ringGapExact,
            "GPU Avatar subtracts actual activity ring pixels in the badge annulus without painting a fake background");
        avatar.Options = new(UiAvatarSize.Size96, Presence : new(UiPresenceStatus.Busy));
        void Depth(UiImmediateContext ui, bool reverse)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            void Back()
            {
                if (ui.BeginLayer("avatar-presence-depth-back", new(false, 1, default)
                    { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .5f)) }))
                    new UiControlContext(ui).Frame(new(0, 0, 192, 192), new(0, 0, 255), default, 0, 0);
                ui.EndLayer();
            }
            void Front()
            {
                if (ui.BeginLayer("avatar-presence-depth-front", new(false, 1, default)
                    { Scene3D = new(Matrix4x4.Identity, true) }))
                {
                    ui.SetCursorScreenPos(new(24.25f, 24.25f));
                    avatar.Render(ui);
                }
                ui.EndLayer();
            }
            if (reverse) { Front(); Back(); } else { Back(); Front(); }
        }
        var depth = capture("presence-depth", ui => Depth(ui, false));
        check(depth[At(91, 106)] == 0 && depth[At(91, 106) + 2] == 255 && depth[At(70, 70)] == 255,
            "GPU Avatar presence gap exposes the rear depth plane while its body remains in front");
        check(depth.AsSpan().SequenceEqual(capture("presence-depth-reversed", ui => Depth(ui, true))),
            "GPU Avatar presence retains exact overlapping depth pixels under layer submission reversal");
        avatar.Options = new(UiAvatarSize.Size64, ShowActivityShadow : true);
        avatar.Source = source;
        var shadowPixels = capture("activity-shadow", Pattern);
        check(shadowPixels[At(89, 115)] < backdrop[At(89, 115)] && shadowPixels[At(89, 81)] == 255,
            "GPU Avatar shadow follows its reserved body and reveals the patterned surface through soft coverage");
        check(shadowPixels.AsSpan().SequenceEqual(capture("activity-shadow-replay", Pattern)),
            "GPU Avatar shadow and image replay exactly without a periodic update");
        avatar.Options = avatar.Options with { ShowActivityRing = true, Presence = new() };
        var decorated = capture("activity-ring-shadow-presence", Pattern);
        check(!decorated.AsSpan().SequenceEqual(shadowPixels)
            && decorated.AsSpan().SequenceEqual(capture("activity-ring-shadow-presence-replay", Pattern)),
            "GPU Avatar composes activity shadow ring image and presence through their shared retained paths");
        avatar.Options = new(UiAvatarSize.Size16, ShowActivityRing : true,
            Presence : new(UiPresenceStatus.Available, UiBadgeSize.ExtraLarge));
        avatar.Source = source;
        var oversized = capture("presence-oversized-ring", Pattern);
        check(!oversized.AsSpan().SequenceEqual(backdrop), "GPU oversized presence and activity ring remain visible inside their reserved union");
        var coldCapturePasses = 0;
        var replayCapturePasses = 0;
        foreach (var inactiveSize in Enum.GetValues<UiAvatarSize>())
        foreach (var inactiveShape in Enum.GetValues<UiAvatarShape>())
        {
            avatar.Source = source;
            avatar.Options = new(inactiveSize, inactiveShape, ShowActivityRing: true, ShowActivityShadow: true,
                Presence: new(UiPresenceStatus.Busy), IsInactive: true);
            var inactive = capture($"inactive-{inactiveSize}-{inactiveShape}", Pattern);
            coldCapturePasses += metrics().PaintGroupPasses;
            var extent = avatar.Options.PixelSize;
            var center = At(24 + extent / 2, 24 + extent / 2);
            var expectedRed = 255 * .8f + backdrop[center] * .2f;
            check(MathF.Abs(inactive[center] - expectedRed) <= 2
                && MathF.Abs(inactive[center + 1] - backdrop[center + 1] * .2f) <= 2
                && MathF.Abs(inactive[center + 2] - backdrop[center + 2] * .2f) <= 2
                && inactive.AsSpan(At(24, 24 + extent / 2), 3).SequenceEqual(backdrop.AsSpan(At(24, 24 + extent / 2), 3)),
                "GPU inactive Avatar scales inward and applies one opacity while hiding activity decorations : " + inactiveSize + " " + inactiveShape);
            var replayed = capture($"inactive-replay-{inactiveSize}-{inactiveShape}", Pattern);
            replayCapturePasses += metrics().PaintGroupPasses;
            check(inactive.AsSpan().SequenceEqual(replayed),
                "GPU unchanged inactive Avatar replays exact pixels : " + inactiveSize + " " + inactiveShape);
        }
        check(coldCapturePasses == 56 && replayCapturePasses == 0,
            "GPU28 inactive Avatar definitions need56 initial capture passes and zero offscreen passes on unchanged replay");
        avatar.Source = null;
        avatar.Initials = "김";
        avatar.Options = new(UiAvatarSize.Size64, Color: UiAvatarColor.Brand, IsInactive: true);
        var inactiveKorean = capture("inactive-korean", Pattern);
        configureKorean(false);
        var inactiveMissing = capture("inactive-korean-missing-reference", Pattern);
        configureKorean(true);
        check(!inactiveKorean.AsSpan().SequenceEqual(inactiveMissing)
            && inactiveKorean.AsSpan().SequenceEqual(capture("inactive-korean-restored", Pattern)),
            "GPU inactive Korean typography tracks font resource changes without caching a missing-font box");
        avatar.Initials = "AL";
        var inactiveLatin = capture("inactive-latin", Pattern);
        avatar.Name = "Grace Hopper";
        avatar.Initials = "GH";
        check(!inactiveLatin.AsSpan().SequenceEqual(capture("inactive-latin-revised", Pattern)),
            "GPU inactive Avatar content mutation replaces actual cached initials");
        using (var overlapping = new UiAvatar<OverlapPresentation>("Overlap", new(IsInactive: true)))
        {
            var overlap = capture("inactive-overlap-opacity", ui =>
            {
                showAvatar = false;
                Pattern(ui);
                showAvatar = true;
                ui.SetCursorScreenPos(new(24, 24));
                overlapping.Render(ui);
            });
            var sample = At(48, 48);
            check(MathF.Abs(overlap[sample] - backdrop[sample] * .2f) <= 2
                && MathF.Abs(overlap[sample + 1] - (255 * .8f + backdrop[sample + 1] * .2f)) <= 2,
                "GPU inactive Avatar overlaps are composited once rather than accumulating per-command transparency");
        }
        avatar.Source = source;
        avatar.Options = new(UiAvatarSize.Size64, Color : UiAvatarColor.Colorful);
        using var allocated = Dux.SizedBox(avatar, 96, 48);
        var parent = capture("parent-allocation", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, context.Theme.WindowBg, default, 0, 0);
            ui.SetCursorScreenPos(new(24.25f, 24.25f));
            allocated.Render(ui);
        });
        check(parent[At(72, 48)] == 255 && parent[At(72, 48) + 1] == 0
            && parent.AsSpan(At(28, 48), 3).SequenceEqual(parent.AsSpan(0, 3)),
            "GPU Avatar centers its square image within a rectangular parent allocation");
    }
    private readonly struct OverlapPresentation : IUiAvatarPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiAvatarOptions options) => new(64, 64);
        public static void Paint(UiControlContext context, UiRect bounds, UiAvatarContent content, UiAvatarOptions options)
        {
            context.Frame(bounds, new(255, 0, 0), default, 0, 0);
            context.Frame(new(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, bounds.Height - 16), new(0, 255, 0), default, 0, 0);
        }
    }
}
