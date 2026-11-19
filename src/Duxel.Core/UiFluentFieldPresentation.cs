namespace Duxel.Core;

[UiFluentIcon("diamond_dismiss", UiFluentIconStyle.Filled, 12)] public readonly partial struct UiFluentFieldError12;
[UiFluentIcon("warning", UiFluentIconStyle.Filled, 12)] public readonly partial struct UiFluentFieldWarning12;
[UiFluentIcon("checkmark_circle", UiFluentIconStyle.Filled, 12)] public readonly partial struct UiFluentFieldSuccess12;

public interface IUiFieldSize
{
    /// <summary>Promises stable label typography and spacing for the current text environment.</summary>
    static virtual bool CanRetainMeasurement => false;
    static abstract UiControlTypographyScope BeginLabel(UiControlContext context);
    static abstract float VerticalPadding
    {
        get;
    }
    static abstract float VerticalMargin
    {
        get;
    }
    static abstract float HorizontalPadding
    {
        get;
    }
}
public readonly struct UiFluentSmallFieldSize : IUiFieldSize
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginLabel(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType200>();
    public static float VerticalPadding => 2; public static float VerticalMargin => 2; public static float HorizontalPadding => 4;
}
public readonly struct UiFluentMediumFieldSize : IUiFieldSize
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginLabel(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static float VerticalPadding => 2; public static float VerticalMargin => 2; public static float HorizontalPadding => 6;
}
public readonly struct UiFluentLargeFieldSize : IUiFieldSize
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginLabel(UiControlContext context) => context.BeginTypography<UiSemiboldTypeface, UiFluentType400>();
    public static float VerticalPadding => 1; public static float VerticalMargin => 4; public static float HorizontalPadding => 9;
}
public interface IUiFieldOrientation
{
    /// <summary>Promises stable orientation without externally mutable measurement state.</summary>
    static virtual bool CanRetainMeasurement => false;
    static abstract bool Horizontal
    {
        get;
    }
}
public readonly struct UiVerticalFieldLayout : IUiFieldOrientation
{
    public static bool CanRetainMeasurement => true;
    public static bool Horizontal => false;
}
public readonly struct UiHorizontalFieldLayout : IUiFieldOrientation
{
    public static bool CanRetainMeasurement => true;
    public static bool Horizontal => true;
}

/// <summary>Official Field grid and typography, statically selected by size and orientation.</summary>
public readonly struct UiFluentFieldPresentation<TSize, TOrientation> : IUiFieldPresentation
    where TSize : IUiFieldSize where TOrientation : IUiFieldOrientation
{
    public static bool ReuseLayout => TSize.CanRetainMeasurement && TOrientation.CanRetainMeasurement;
    public static float ControlWidth(float fieldWidth) => TOrientation.Horizontal ? fieldWidth * .67f : fieldWidth;
    public static float FieldWidth(float controlWidth) => TOrientation.Horizontal ? controlWidth + controlWidth * 33 / 67 : controlWidth;
    public static UiFieldLayout Arrange(UiControlContext context, UiFieldOptions options, in UiFieldLayout natural, UiVector2 allocatedSize)
    {
        var controlBottom = natural.Control.Y + natural.Control.Height;
        var contentBottom = options.Hint is not null ? natural.Hint.Y + natural.Hint.Height
            : options.ValidationMessage is not null ? natural.Validation.Y + MathF.Max(natural.Validation.Height,
                options.EffectiveValidationState == UiFieldValidationState.None ? 0 : 16)
            : controlBottom;
        var trailing = MathF.Max(0, contentBottom - controlBottom);
        var height = MathF.Max(0, allocatedSize.Y - natural.Control.Y - trailing);
        var delta = height - natural.Control.Height;
        return natural with
        {
            Size = allocatedSize,
            Control = natural.Control with
            {
                Height = height
            },
            Validation = options.ValidationMessage is null ? natural.Validation : natural.Validation with
            {
                Y = natural.Validation.Y + delta
            },
            ValidationIcon = options.ValidationMessage is null ? natural.ValidationIcon : natural.ValidationIcon with
            {
                Y = natural.ValidationIcon.Y + delta
            },
            Hint = options.Hint is null ? natural.Hint : natural.Hint with
            {
                Y = natural.Hint.Y + delta
            }
        };
    }
    public static UiFieldLayout Measure(UiControlContext context, UiFieldOptions options, UiVector2 controlSize)
    {
        var column = TOrientation.Horizontal ? controlSize.X * 33 / 67 : 0;
        var labelWidth = TOrientation.Horizontal ? MathF.Max(1, column - 12) : controlSize.X;
        UiControlTextBlock label = default;
        UiRect labelRect = default, required = default, target = default;
        var y = 0f;
        var labelBottom = 0f;
        if (options.Label is { } labelValue)
        {
            using var scope = TSize.BeginLabel(context);
            var star = options.Required ? context.MeasureText("*").X : 0;
            label = labelValue.Length == 0 ? default : context.MeasureTextBlock(context.WrapText(labelValue, labelWidth));
            var padding = TOrientation.Horizontal ? TSize.HorizontalPadding : TSize.VerticalPadding;
            labelRect = new(0, padding, MathF.Min(labelWidth, label.Size.X), label.Size.Y);
            var textHeight = label.Size.Y;
            if (options.Required)
            {
                var x = (label.LineCount > 0 ? label.GetLine(label.LineCount - 1).Size.X : 0) + 4;
                var starY = padding + Math.Max(0, label.LineCount - 1) * context.TypographyLineHeight;
                if (label.LineCount > 0 && x + star > labelWidth)
                {
                    x = 4;
                    starY += context.TypographyLineHeight;
                }
                textHeight = MathF.Max(textHeight, starY - padding + context.TypographyLineHeight);
                required = new(x, starY, star, context.TypographyLineHeight);
            }
            labelBottom = padding * 2 + textHeight;
            target = new(0, 0, MathF.Min(labelWidth, MathF.Max(labelRect.Width, options.Required ? required.X + required.Width : 0)), labelBottom);
            if (!TOrientation.Horizontal)
                y = labelBottom + TSize.VerticalMargin;
        }
        var control = new UiRect(column, y, controlSize.X, controlSize.Y);
        y += controlSize.Y;
        UiControlTextBlock validation = default, hint = default;
        UiRect validationRect = default, icon = default, hintRect = default;
        using (var scope = context.BeginTypography<UiRegularTypeface, UiFluentType200>())
        {
            if (options.ValidationMessage is { } message)
            {
                y += 2;
                var gutter = options.EffectiveValidationState == UiFieldValidationState.None ? 0 : 16;
                validation = message.Length == 0 ? default : context.MeasureTextBlock(context.WrapText(message, MathF.Max(1, controlSize.X - gutter)));
                validationRect = new(column + gutter, y, MathF.Max(0, controlSize.X - gutter), validation.Size.Y);
                if (gutter > 0)
                    icon = new(column, y + 3, 12, 12);
                y += MathF.Max(validation.Size.Y, gutter > 0 ? 16 : 0);
            }
            if (options.Hint is { } hintValue)
            {
                y += 2;
                hint = hintValue.Length == 0 ? default : context.MeasureTextBlock(context.WrapText(hintValue, controlSize.X));
                hintRect = new(column, y, controlSize.X, hint.Size.Y);
                y += hint.Size.Y;
            }
        }
        return new(new(column + controlSize.X, MathF.Max(y, labelBottom)), control, labelRect, required, validationRect, icon, hintRect, label, validation, hint)
        {
            LabelTarget = target,
            LabelClip = new(0, 0, labelWidth, labelBottom)
        };
    }
    public static void Paint(UiControlContext context, UiFieldOptions options, in UiFieldLayout layout, UiVector2 origin)
    {
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var secondary = dark ? new UiColor(173, 173, 173) : new UiColor(97, 97, 97);
        var error = dark ? new UiColor(227, 125, 128) : new UiColor(188, 47, 50);
        using var clip = context.Clip(new(origin.X, origin.Y, layout.Size.X, layout.Size.Y));
        if (options.Label is not null)
        {
            using var labelClip = context.Clip(At(layout.LabelClip ?? new(0, 0, layout.Size.X, layout.Size.Y), origin));
            using var scope = TSize.BeginLabel(context);
            context.TypographyTextBlock(layout.LabelText, At(layout.Label, origin), dark ? new(255, 255, 255) : new(36, 36, 36));
            if (options.Required)
            {
                var star = context.MeasureTextBlock("*");
                context.TypographyTextBlock(star, At(layout.RequiredIndicator, origin), dark ? new(227, 125, 128) : new(209, 52, 56));
            }
        }
        using var text = context.BeginTypography<UiRegularTypeface, UiFluentType200>();
        if (options.ValidationMessage is not null)
        {
            var state = options.EffectiveValidationState;
            context.TypographyTextBlock(layout.ValidationText, At(layout.Validation, origin), state == UiFieldValidationState.Error ? error : secondary);
            var icon = At(layout.ValidationIcon, origin);
            switch (state)
            {
                case UiFieldValidationState.Error:
                    UiFluentFieldError12.Paint(context, icon, error, default);
                    break;
                case UiFieldValidationState.Warning:
                    UiFluentFieldWarning12.Paint(context, icon, dark ? new(233, 131, 94) : new(196, 53, 1), default);
                    break;
                case UiFieldValidationState.Success:
                    UiFluentFieldSuccess12.Paint(context, icon, dark ? new(84, 176, 84) : new(14, 112, 14), default);
                    break;
            }
        }
        if (options.Hint is not null)
            context.TypographyTextBlock(layout.HintText, At(layout.Hint, origin), secondary);
    }
    private static UiRect At(UiRect rect, UiVector2 origin) => UiImmediateContext.OffsetFieldRect(rect, origin);
}
