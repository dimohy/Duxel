namespace Duxel.Core;

public enum UiButtonVisualState { Rest, Hover, Pressed, Disabled }
public readonly record struct UiButtonPaint(UiColor Background,UiColor Border,UiColor Foreground,UiColor Focus);
public static partial class UiFluentButtonPalette
{
    // Fluent UI8830c10b secondary Button tokens; color data is independent of implementation selection.
    public static UiButtonPaint Resolve(UiFluentColorScheme scheme,UiButtonVisualState state)=>(scheme,state) switch
    {
        (UiFluentColorScheme.Light,UiButtonVisualState.Rest)=>new(new(255,255,255),new(209,209,209),new(36,36,36),new(0,0,0)),
        (UiFluentColorScheme.Light,UiButtonVisualState.Hover)=>new(new(245,245,245),new(199,199,199),new(36,36,36),new(0,0,0)),
        (UiFluentColorScheme.Light,UiButtonVisualState.Pressed)=>new(new(224,224,224),new(179,179,179),new(36,36,36),new(0,0,0)),
        (UiFluentColorScheme.Light,UiButtonVisualState.Disabled)=>new(new(240,240,240),new(224,224,224),new(189,189,189),new(0,0,0)),
        (UiFluentColorScheme.Dark,UiButtonVisualState.Rest)=>new(new(41,41,41),new(102,102,102),new(255,255,255),new(255,255,255)),
        (UiFluentColorScheme.Dark,UiButtonVisualState.Hover)=>new(new(61,61,61),new(117,117,117),new(255,255,255),new(255,255,255)),
        (UiFluentColorScheme.Dark,UiButtonVisualState.Pressed)=>new(new(31,31,31),new(107,107,107),new(255,255,255),new(255,255,255)),
        (UiFluentColorScheme.Dark,UiButtonVisualState.Disabled)=>new(new(20,20,20),new(66,66,66),new(92,92,92),new(255,255,255)),
        _=>throw new ArgumentOutOfRangeException(nameof(state))
    };
}
