namespace Duxel.Core;

internal enum UiFluentButtonAppearanceKind { Secondary, Outline, Subtle, Transparent, Primary }

public static partial class UiFluentButtonPalette
{
    internal static UiButtonPaint Resolve(UiFluentColorScheme scheme, UiButtonVisualState state,
        UiFluentButtonAppearanceKind appearance) => appearance switch
    {
        UiFluentButtonAppearanceKind.Secondary => Resolve(scheme, state),
        UiFluentButtonAppearanceKind.Outline => Outline(scheme, state),
        UiFluentButtonAppearanceKind.Subtle => Subtle(scheme, state),
        UiFluentButtonAppearanceKind.Transparent => Transparent(scheme, state),
        UiFluentButtonAppearanceKind.Primary => Primary(scheme, state),
        _ => throw new ArgumentOutOfRangeException(nameof(appearance))
    };

    internal static UiButtonPaint ResolveForced(UiForcedColorPalette colors, UiButtonVisualState state,
        UiFluentButtonAppearanceKind appearance)
    {
        if (state == UiButtonVisualState.Disabled)
            return new(colors.ButtonFace, colors.GrayText, colors.GrayText, colors.GrayText);
        if (state is UiButtonVisualState.Hover or UiButtonVisualState.Pressed)
        {
            var background = appearance switch
            {
                UiFluentButtonAppearanceKind.Transparent => default,
                UiFluentButtonAppearanceKind.Subtle => colors.ButtonFace,
                _ => colors.HighlightText
            };
            return new(background, colors.Highlight, colors.Highlight, colors.Highlight);
        }
        return appearance switch
        {
            UiFluentButtonAppearanceKind.Primary => new(colors.Highlight, colors.HighlightText, colors.HighlightText, colors.HighlightText),
            UiFluentButtonAppearanceKind.Secondary => new(colors.ButtonFace, colors.ButtonText, colors.ButtonText, colors.Highlight),
            _ => new(default, colors.ButtonText, colors.ButtonText, colors.Highlight)
        };
    }

    public static UiButtonPaint Outline(UiFluentColorScheme scheme,UiButtonVisualState state)=>
        Resolve(scheme,state) with {Background=default};

    public static UiButtonPaint Primary(UiFluentColorScheme scheme,UiButtonVisualState state)
    {
        var neutral=Resolve(scheme,state);
        if(state==UiButtonVisualState.Disabled)return neutral with {Border=default};
        var background=state switch
        {
            UiButtonVisualState.Rest=>scheme==UiFluentColorScheme.Light?new UiColor(15,108,189):new(17,94,163),
            UiButtonVisualState.Hover=>scheme==UiFluentColorScheme.Light?new UiColor(17,94,163):new(15,108,189),
            UiButtonVisualState.Pressed=>new UiColor(12,59,94),
            _=>throw new ArgumentOutOfRangeException(nameof(state))
        };
        return new(background,default,new(255,255,255),neutral.Focus);
    }
    public static UiButtonPaint Subtle(UiFluentColorScheme scheme,UiButtonVisualState state)
    {
        var neutral=Resolve(scheme,state);
        if(state==UiButtonVisualState.Disabled)return neutral with {Background=default,Border=default};
        var background=state switch
        {
            UiButtonVisualState.Rest=>default,
            UiButtonVisualState.Hover=>scheme==UiFluentColorScheme.Light?new UiColor(245,245,245):new(56,56,56),
            UiButtonVisualState.Pressed=>scheme==UiFluentColorScheme.Light?new UiColor(224,224,224):new(46,46,46),
            _=>throw new ArgumentOutOfRangeException(nameof(state))
        };
        var foreground=state==UiButtonVisualState.Rest?
            scheme==UiFluentColorScheme.Light?new UiColor(66,66,66):new(214,214,214):neutral.Foreground;
        return new(background,default,foreground,neutral.Focus);
    }
    public static UiButtonPaint Transparent(UiFluentColorScheme scheme,UiButtonVisualState state)
    {
        var subtle=Subtle(scheme,state);
        var foreground=state switch
        {
            UiButtonVisualState.Hover=>scheme==UiFluentColorScheme.Light?new UiColor(15,108,189):new(71,158,245),
            UiButtonVisualState.Pressed=>scheme==UiFluentColorScheme.Light?new UiColor(17,94,163):new(40,134,222),
            _=>subtle.Foreground
        };
        return subtle with {Background=default,Foreground=foreground};
    }
}
