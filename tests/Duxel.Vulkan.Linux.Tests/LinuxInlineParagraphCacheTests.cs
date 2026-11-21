using System.Security.Cryptography;
using Duxel.Core;

internal static class LinuxInlineParagraphCacheTests
{
    internal static void Run(string output, int slots)
    {
        Directory.CreateDirectory(output);
        UiInlineTextRun[] original = [
            new("abc אב "), new("גד office 한글 ", UiInlineTextWeight.Bold), new("123 wrap end")
        ];
        UiInlineTextRun[] edited = [
            new("바뀐 글자 אב "), new("גד office ", UiInlineTextWeight.Bold), new("456 end")
        ];
        var white = new UiColor(255, 255, 255);
        var tinted = new UiColor(68, 202, 177);
        using var retained = new PresentationReuseTests.Fixture(slots, false, useR8ParagraphCoverage: true);
        using var reference = new PresentationReuseTests.Fixture(slots, false);
        foreach (var samples in new[] { 1, 4 })
        {
            retained.Renderer.SetMsaaSamples(samples);
            reference.Renderer.SetMsaaSamples(samples);
            using var widget = new UiInlineText(original, color: white);
            using var host = Dux.SizedBox(widget, 225, 90);
            var atStart = new UiVector2(14, 16);
            var moved = new UiVector2(33, 19);
            var baseline = Compare("initial", atStart);
            Require(samples == 1 ? baseline.Uploads == 1 : baseline.Uploads == 0,
                "initial draw uploads one whole mask and the later MSAA pass reuses it");
            var same = Compare("steady", atStart);
            Require(ReferenceEquals(baseline.Layout, same.Layout) && baseline.Builds == same.Builds && same.Uploads == 0,
                "steady paragraph retains layout and textures");

            widget.Color = tinted;
            var color = Compare("color-only", atStart);
            Require(ReferenceEquals(same.Layout, color.Layout) && same.Builds == color.Builds && color.Uploads == 0
                && same.Pixels != color.Pixels, "color changes repaint through retained coverage");

            var position = Compare("moved", moved);
            Require(ReferenceEquals(color.Layout, position.Layout) && color.Builds == position.Builds && position.Uploads == 0
                && color.Pixels != position.Pixels, "integer relocation reuses the layout and coverage");

            host.Options = new(92, 90);
            var narrow = Compare("narrow-wrap", moved);
            Require(!ReferenceEquals(position.Layout, narrow.Layout) && narrow.Layout.Lines.Length > position.Layout.Lines.Length,
                "narrower width creates a wrapping layout");
            host.Options = new(225, 90);
            var returned = Compare("restore-width", moved);
            Require(ReferenceEquals(position.Layout, returned.Layout) && returned.Uploads == 0
                && returned.Pixels == position.Pixels, "restoring the recent width reuses its prior layout and pixels");

            widget.Runs = edited;
            var changed = Compare("edited-runs", moved);
            Require(!ReferenceEquals(returned.Layout, changed.Layout) && changed.Pixels != returned.Pixels,
                "changing literal runs replaces the layout and visible glyphs");
            widget.RightToLeft = true;
            var directed = Compare("rtl", moved);
            Require(!ReferenceEquals(changed.Layout, directed.Layout) && changed.Pixels != directed.Pixels,
                "direction changes replace the layout and visible ordering");

            host.Options = new(225, 12);
            var clipped = Compare("clipped", moved);
            Require(ReferenceEquals(directed.Layout, clipped.Layout) && directed.Builds == clipped.Builds
                && clipped.Pixels != directed.Pixels && clipped.Uploads == 0,
                "short host clips visible ink without rebuilding paragraph coverage");
            host.Options = new(225, 90);
            var revealed = Compare("reveal", moved);
            Require(ReferenceEquals(directed.Layout, revealed.Layout) && directed.Builds == revealed.Builds
                && revealed.Pixels == directed.Pixels && revealed.Uploads == 0,
                "revealing a clipped paragraph restores its prior pixels");

            widget.Runs = [new("Fresh אב "), new("גד tint", UiInlineTextWeight.Bold)];
            widget.Color = new(67, 194, 151, 150);
            var translucent = Compare("translucent", moved);
            Require(samples == 1 ? translucent.Uploads > 1 : translucent.Uploads == 0,
                "translucent runs retain separate coverage and reuse it on the later MSAA pass");

            Frame Compare(string stage, UiVector2 at)
            {
                using var fresh = new UiInlineText(widget.Runs, wrap: widget.Wrap, color: widget.Color)
                {
                    RightToLeft = widget.RightToLeft
                };
                using var freshHost = Dux.SizedBox(fresh, host.Options.Width, host.Options.Height);
                var expected = reference.Capture(ui => Draw(ui, freshHost, at));
                var uploaded = 0;
                var actual = retained.Capture(ui => Draw(ui, host, at), null, updates =>
                {
                    foreach (var update in updates)
                        if (update.TextureId.Value >= 1_100_000_000 && !update.IsColorImage
                            && update.Kind is UiTextureUpdateKind.Create or UiTextureUpdateKind.Update)
                        {
                            var compact = update.Format == UiTextureFormat.R8CoverageUnorm;
                            var bytesPerPixel = compact ? 1 : 4;
                            if ((stage == "initial" && !compact) || (stage == "translucent" && compact)
                                || update.RgbaPixels.Length != update.EffectiveRegionWidth * update.EffectiveRegionHeight * bytesPerPixel)
                                throw new InvalidOperationException($"Unexpected glyph payload for {stage}: {update.Format}.");
                            uploaded++;
                        }
                });
                var label = $"msaa{samples}-{stage}";
                if (!actual.AsSpan().SequenceEqual(expected))
                {
                    File.WriteAllBytes(Path.Combine(output, label + ".cold.rgba"), expected);
                    File.WriteAllBytes(Path.Combine(output, label + ".retained.rgba"), actual);
                    throw new InvalidOperationException("Retained paragraph differs from freshly laid out widget : " + label);
                }
                var layout = widget.VisualLines.NativeParagraph
                    ?? throw new InvalidOperationException("The widget did not use the Linux native paragraph backend.");
                var builds = widget.LayoutBuilds;
                var hash = Convert.ToHexString(SHA256.HashData(actual));
                Console.WriteLine($"PARAGRAPH {label} {layout.Lines.Length} {builds} {uploaded} {hash}");
                return new(layout, builds, uploaded, hash);
            }
        }

        VerifyCoverageTextureLifecycle(retained, reference);
        VerifyCodeSyntaxColors(retained);
    }

    private static void VerifyCodeSyntaxColors(PresentationReuseTests.Fixture fixture)
    {
        using var code = new UiCodeView("return \"string\";\nreturn \"again\";",
            new UiCodeViewOptions(Wrap: false, LineNumbers: false, ShowToolbar: false));
        var pixels = fixture.Capture(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 8);
            code.Render(ui);
        });
        bool ContainsColor(UiColor color)
        {
            for (var y = 8; y < 75; y++)
                for (var x = 8; x < 225; x++)
                {
                    var start = (y * 320 + x) * 4;
                    if (pixels[start] == (byte)color.Rgba && pixels[start + 1] == (byte)(color.Rgba >> 8)
                        && pixels[start + 2] == (byte)(color.Rgba >> 16))
                        return true;
                }
            return false;
        }
        var dark = ContainsColor(new(86, 156, 214)) && ContainsColor(new(206, 145, 120));
        var light = ContainsColor(new(0, 0, 180)) && ContainsColor(new(163, 21, 21));
        Require(dark || light, "Vulkan code view keeps separate keyword and string colors");
    }

    private static void VerifyCoverageTextureLifecycle(PresentationReuseTests.Fixture compact, PresentationReuseTests.Fixture rgba)
    {
        const int width = 9;
        const int height = 5;
        var texture = new UiTextureId(2_099_999_999);
        var regionA = new byte[] { 255, 12, 220 };
        var regionB = new byte[] { 130, 255, 78, 190, 70 };
        var initial = Enumerable.Repeat((byte)42, width * height).ToArray();
        var updated = (byte[])initial.Clone();
        regionA.CopyTo(updated, width + 1);
        regionB.CopyTo(updated, width * 3 + 3);

        compact.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, texture, UiTextureFormat.R8CoverageUnorm, width, height, initial));
        SetReference(initial);
        var first = Match("single-channel texture creation matches RGBA");
        compact.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Update, texture, UiTextureFormat.R8CoverageUnorm,
            width, height, regionA, 1, 1, 3, 1));
        compact.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Update, texture, UiTextureFormat.R8CoverageUnorm,
            width, height, regionB, 3, 3, 5, 1));
        SetReference(updated);
        var patched = Match("odd-sized batched regions match a complete RGBA upload");
        Require(!patched.AsSpan().SequenceEqual(first), "batched regions visibly change the coverage texture");

        compact.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture, UiTextureFormat.R8CoverageUnorm, width, height, default));
        rgba.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture, UiTextureFormat.Rgba8Unorm, width, height, default));
        var cleared = compact.Capture(static _ => { });
        Require(cleared.AsSpan().SequenceEqual(rgba.Capture(static _ => { })) && !cleared.AsSpan().SequenceEqual(patched),
            "texture destruction leaves identical clear frames");
        compact.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, texture, UiTextureFormat.R8CoverageUnorm, width, height, updated));
        SetReference(updated);
        Match("recreated coverage texture matches RGBA");

        void SetReference(byte[] coverage)
        {
            var pixels = new PlatformTextParagraphCoverage(width, height, coverage, default).ToRgba().RgbaPixels;
            rgba.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, texture, UiTextureFormat.Rgba8Unorm, width, height, pixels));
        }

        void DrawTexture(UiImmediateContext ui) => ui.GetWindowDrawList().AddTextImage(texture, new(16, 16), new(106, 66),
            default, new(1, 1), new(68, 202, 177), new(0, 0, 320, 240));

        byte[] Match(string message)
        {
            var actual = compact.Capture(DrawTexture);
            Require(actual.AsSpan().SequenceEqual(rgba.Capture(DrawTexture)), message);
            return actual;
        }
    }

    private static void Draw(UiImmediateContext ui, UiSizedBox host, UiVector2 at)
    {
        ui.EnableRootViewportContentLayout(contentPadding: 0);
        ui.SetCursorScreenPos(at);
        host.Render(ui);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
        Console.WriteLine($"PASS {reason}");
    }

    private sealed record Frame(PlatformTextParagraphLayout Layout, int Builds, int Uploads, string Pixels);
}