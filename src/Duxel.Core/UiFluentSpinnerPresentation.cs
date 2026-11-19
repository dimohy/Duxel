namespace Duxel.Core;

/// <summary>Fluent's radial mask and two clipped135-degree conic tails.</summary>
public readonly struct UiFluentSpinnerPresentation : IUiSpinnerPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginLabel(UiControlContext context, UiSpinnerOptions options, bool hasLabel)
    {
        if (!hasLabel)
            return default;
        return options.Size switch
        {
            UiSpinnerSize.ExtraTiny or UiSpinnerSize.Tiny or UiSpinnerSize.ExtraSmall or UiSpinnerSize.Small => context.BeginTypography<UiRegularTypeface, UiFluentType300>(),
            UiSpinnerSize.Huge => context.BeginTypography<UiSemiboldTypeface, UiFluentType500>(),
            _ => context.BeginTypography<UiSemiboldTypeface, UiFluentType400>()
        };
    }
    public static float Diameter(UiSpinnerSize size) => size switch
    {
        UiSpinnerSize.ExtraTiny => 16,
        UiSpinnerSize.Tiny => 20,
        UiSpinnerSize.ExtraSmall => 24,
        UiSpinnerSize.Small => 28,
        UiSpinnerSize.Medium => 32,
        UiSpinnerSize.Large => 36,
        UiSpinnerSize.ExtraLarge => 40,
        UiSpinnerSize.Huge => 44,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
    public static UiSpinnerLayout Measure(UiControlContext context, string? label, UiSpinnerOptions options)
    {
        options.Validate();
        var diameter = Diameter(options.Size);
        var block = string.IsNullOrEmpty(label) ? default : context.MeasureTextBlock(label);
        var gap = block.LineCount == 0 ? 0 : 8;
        var vertical = options.LabelPosition is UiSpinnerLabelPosition.Above or UiSpinnerLabelPosition.Below;
        var width = vertical ? MathF.Max(diameter, block.Size.X) : diameter + gap + block.Size.X;
        var height = vertical ? diameter + gap + block.Size.Y : MathF.Max(diameter, block.Size.Y);
        // A native block-level flex root fills its available width and centers its children.
        var rootWidth = MathF.Max(width, options.Width > 0 ? options.Width : context.AvailableWidth);
        var offset = (rootWidth - width) * .5f;
        var first = options.LabelPosition is UiSpinnerLabelPosition.Above or UiSpinnerLabelPosition.Before;
        if (!vertical && options.RightToLeft)
            first = !first;
        UiRect ring, labelBounds;
        if (vertical)
        {
            ring = new(offset + (width - diameter) * .5f, first ? block.Size.Y + gap : 0, diameter, diameter);
            labelBounds = new(offset + (width - block.Size.X) * .5f, first ? 0 : diameter + gap, block.Size.X, block.Size.Y);
        }
        else
        {
            ring = new(offset + (first ? block.Size.X + gap : 0), (height - diameter) * .5f, diameter, diameter);
            labelBounds = new(offset + (first ? 0 : diameter + gap), (height - block.Size.Y) * .5f, block.Size.X, block.Size.Y);
        }
        return new(new(rootWidth, height), ring, labelBounds, block);
    }
    public static UiSpinnerLayout Arrange(UiControlContext context, UiSpinnerOptions options, in UiSpinnerLayout natural, UiVector2 allocatedSize)
    {
        var x = (allocatedSize.X - natural.Size.X) * .5f;
        var y = (allocatedSize.Y - natural.Size.Y) * .5f;
        return natural with
        {
            Size = allocatedSize,
            Ring = natural.Ring with
            {
                X = natural.Ring.X + x,
                Y = natural.Ring.Y + y
            },
            Label = natural.Label with
            {
                X = natural.Label.X + x,
                Y = natural.Label.Y + y
            }
        };
    }
    public static void Paint(UiControlContext context, UiSpinnerOptions options, in UiSpinnerFrame frame, in UiSpinnerLayout layout, UiVector2 origin)
    {
        var dark = context.FluentColorScheme switch
        {
            UiFluentColorScheme.Light => false,
            UiFluentColorScheme.Dark => true,
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
        var inverted = options.Appearance == UiSpinnerAppearance.Inverted;
        var background = inverted ? new UiColor(255, 255, 255, 51) : dark ? new UiColor(14, 71, 117) : new UiColor(180, 214, 250);
        var foreground = inverted ? new UiColor(255, 255, 255) : dark ? new UiColor(71, 158, 245) : new UiColor(15, 108, 189);
        var labelColor = inverted ? new UiColor(255, 255, 255) : dark ? new UiColor(255, 255, 255) : new UiColor(36, 36, 36);
        var radius = layout.Ring.Width * .5f;
        var center = new UiVector2(origin.X + layout.Ring.X + radius, origin.Y + layout.Ring.Y + radius);
        var stroke = options.Size == UiSpinnerSize.Huge ? 4 : radius >= 16 ? 3 : 2;
        Span<float> cuts = stackalloc float[8];
        var count = 0;
        cuts[count++] = 0;
        cuts[count++] = 105;
        cuts[count++] = 360;
        if (frame.ReducedMotion)
            cuts[count++] = 120;
        else
        {
            cuts[count++] = frame.Before;
            cuts[count++] = frame.Before + 135;
            cuts[count++] = frame.After;
            cuts[count++] = frame.After + 135;
        }
        cuts = cuts[..count];
        cuts.Sort();
        var rotation = frame.Rotation + (frame.ReducedMotion ? 0 : frame.TailRotation);
        var mirror = frame.RightToLeft ? -1f : 1;
        for (var i = 0; i < cuts.Length - 1; i++)
        {
            var a = cuts[i];
            var b = cuts[i + 1];
            if (b - a < .00001f)
                continue;
            var middle = (a + b) * .5f;
            UiColor first, last;
            if (frame.ReducedMotion)
            {
                first = Composite(background, foreground, a >= 105 ? Math.Clamp((a - 120) / 240, 0, 1) : 0);
                last = Composite(background, foreground, b > 105 ? Math.Clamp((b - 120) / 240, 0, 1) : 0);
            }
            else
            {
                var visible = middle >= 105 && (middle >= frame.Before && middle <= frame.Before + 135 || middle >= frame.After && middle <= frame.After + 135);
                first = last = visible ? foreground : background;
            }
            context.AnnularSector(center, radius, stroke, (a + rotation) * MathF.PI / 180 * mirror, (b - a) * MathF.PI / 180 * mirror, first, last);
        }
        if (layout.Text.LineCount > 0)
            context.TypographyTextBlock(layout.Text, layout.Label with
            {
                X = origin.X + layout.Label.X,
                Y = origin.Y + layout.Label.Y
            }, labelColor, UiItemHorizontalAlign.Center);
    }
    private static UiColor Composite(UiColor background, UiColor foreground, float coverage)
    {
        var oldAlpha = (background.Rgba >> 24) / 255f;
        var alpha = coverage + oldAlpha * (1 - coverage);
        uint color = (uint)MathF.Round(alpha * 255) << 24;
        for (var c = 0; c < 24; c += 8)
        {
            var a = (background.Rgba >> c) & 255;
            var b = (foreground.Rgba >> c) & 255;
            var value = alpha == 0 ? 0 : (b * coverage + a * oldAlpha * (1 - coverage)) / alpha;
            color |= (uint)MathF.Round(value) << c;
        }
        return new(color);
    }
}
