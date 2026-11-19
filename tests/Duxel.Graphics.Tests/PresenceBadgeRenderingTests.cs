using Duxel.Core;

internal static class PresenceBadgeRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int stride,
        Func<UiRendererFrameMetrics> statistics, int acquiredImageCount)
    {
        using var badge = Fluent.PresenceBadge();
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, context.Theme.WindowBg, default, 0, 0);
            ui.SetCursorScreenPos(new(24.25f, 24.25f));
            badge.Render(ui);
        }
        int At(int x, int y) => (y * stride + x) * 4;
        foreach (var size in Enum.GetValues<UiBadgeSize>())
        {
            var pixels = new Dictionary<(UiPresenceStatus Status, bool Out), byte[]>();
            foreach (var status in Enum.GetValues<UiPresenceStatus>())
            {
                foreach (var oof in new[] { false, true })
                {
                    badge.Options = new(status, size, oof);
                    var image = capture($"{size}-{status}-{oof}", Draw);
                    pixels.Add((status, oof), image);
                    var edge = UiFluentPresenceBadgePresentation.Edge(size);
                    var visible = false;
                    for (var y = 24; y < 25 + edge; y++)
                        for (var x = 24; x < 25 + edge; x++)
                            visible |= !image.AsSpan(At(x, y), 3).SequenceEqual(image.AsSpan(0, 3));
                    check(visible && image.AsSpan(At(22, 22), 3).SequenceEqual(image.AsSpan(0, 3)),
                        $"GPU PresenceBadge visible within its actual viewport : {size}/{status}/{oof}");
                }
            }
            bool Equal(UiPresenceStatus a, bool firstOut, UiPresenceStatus b, bool secondOut) =>
                pixels[(a, firstOut)].AsSpan().SequenceEqual(pixels[(b, secondOut)]);
            check(!Equal(UiPresenceStatus.Busy, false, UiPresenceStatus.DoNotDisturb, false)
                && !Equal(UiPresenceStatus.DoNotDisturb, false, UiPresenceStatus.Blocked, false),
                "GPU same-color busy do-not-disturb and blocked badges have distinct actual glyphs : " + size);
            check(!Equal(UiPresenceStatus.Offline, false, UiPresenceStatus.Unknown, false),
                "GPU same-color offline and unknown badges preserve distinct glyphs : " + size);
            check(!Equal(UiPresenceStatus.Available, false, UiPresenceStatus.Available, true),
                "GPU out-of-office available selects its outlined glyph rather than changing only a label : " + size);
            check(Equal(UiPresenceStatus.Away, true, UiPresenceStatus.OutOfOffice, false)
                && Equal(UiPresenceStatus.Offline, true, UiPresenceStatus.OutOfOffice, false),
                "GPU out-of-office away and offline use the pinned out-of-office glyph and color : " + size);
            check(Equal(UiPresenceStatus.Blocked, false, UiPresenceStatus.Blocked, true)
                && Equal(UiPresenceStatus.Unknown, false, UiPresenceStatus.Unknown, true),
                "GPU unchanged blocked and unknown appearances remain exact when the description gains out-of-office : " + size);
            badge.Options = new(UiPresenceStatus.Unknown, size, true);
            var exact = true;
            // The first repeat changes back from Blocked to Unknown. Warm every actual acquired image,
            // then reacquire one already repaired image before requiring zero work.
            for (var repeat = 0; repeat <= acquiredImageCount; repeat++)
                exact &= pixels[(UiPresenceStatus.Unknown, true)].AsSpan().SequenceEqual(capture($"replay-{size}-{repeat}", Draw));
            check(exact,
                "GPU unchanged PresenceBadge requested frames reproduce exact glyph pixels : " + size);
            var metrics = statistics();
            check(metrics.DrawCalls == 0 && metrics.DynamicUploadBytes == 0,
                $"GPU unchanged PresenceBadge requires no draw or streamed upload after acquired-image warmup : {size}; draws={metrics.DrawCalls}, upload={metrics.DynamicUploadBytes}, staging={metrics.StagingUploadBytes}");
        }
    }
}
