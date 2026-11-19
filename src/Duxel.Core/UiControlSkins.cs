namespace Duxel.Core;

public enum UiButtonActivation
{
    Release, Press
}
public enum UiWidgetPreset
{
    Windows11, Material3Expressive, Fluent2
}
public enum UiTextFieldAppearance
{
    Frame, Underline
}
public enum UiToggleAppearance
{
    Checkbox, Switch
}
public enum UiScrollbarTrackClick
{
    Page, Jump
}

/// <summary>Build-time widget geometry and interaction policy, independent of the color palette.</summary>
public readonly record struct UiButtonSkin
{
    public float? CornerRadius
    {
        get; init;
    }
    public float? PressedCornerRadius
    {
        get; init;
    }
    public float? BorderWidth
    {
        get; init;
    }
    public float? PressedOffsetY
    {
        get; init;
    }
    public float? PaddingX
    {
        get; init;
    }
    public float? PaddingY
    {
        get; init;
    }
    public float MinimumWidth
    {
        get; init;
    }
    public float MinimumHeight
    {
        get; init;
    }
    public UiButtonActivation Activation
    {
        get; init;
    }
}

public readonly record struct UiControlSkins
{
    public UiButtonSkin Button
    {
        get; init;
    }
    public UiTextFieldSkin TextField
    {
        get; init;
    }
    public UiToggleSkin Toggle
    {
        get; init;
    }
    public UiSliderSkin Slider
    {
        get; init;
    }
    public UiSegmentedSkin Segmented
    {
        get; init;
    }
    public UiScrollbarSkin Scrollbar
    {
        get; init;
    }
    public static UiControlSkins Windows11 => default;
    // Material small-button geometry; Duxel retains its own palette and input engine.
    public static UiControlSkins Material3Expressive => new()
    {
        Button = new()
        {
            CornerRadius = 20,
            PressedCornerRadius = 8,
            BorderWidth = 0,
            PressedOffsetY = 0,
            PaddingX = 16,
            PaddingY = 8,
            MinimumWidth = 58,
            MinimumHeight = 40,
        },
        TextField = new()
        {
            Appearance = UiTextFieldAppearance.Underline,
            CornerRadius = 4,
            BorderWidth = 1,
            FocusedBorderWidth = 2,
            MinimumHeight = 40
        },
        Toggle = new()
        {
            Appearance = UiToggleAppearance.Switch,
            SwitchWidth = 52,
            SwitchHeight = 32,
            OnThumbSize = 24,
            OffThumbSize = 16,
            PressedThumbSize = 28,
            BorderWidth = 2,
            CheckboxSize = 18,
            CornerRadius = 2
        },
        Slider = new()
        {
            TrackHeight = 16,
            TrackCornerRadius = 8,
            HandleWidth = 4,
            PressedHandleWidth = 2,
            HandleHeight = 44,
            HandleCornerRadius = 2,
            MinimumHeight = 44
        },
        Segmented = new()
        {
            CornerRadius = 20,
            ItemCornerRadius = 8,
            MinimumHeight = 40,
            Spacing = 2
        },
        // Scrollbars are a desktop policy; Material does not define a web scrollbar component.
        Scrollbar = new()
        {
            ThumbCornerRadius = 4
        },
    };
    public static UiControlSkins Fluent2 => new()
    {
        Button = new()
        {
            CornerRadius = 4,
            BorderWidth = 1,
            PressedOffsetY = 0,
            PaddingX = 12,
            PaddingY = 5,
            MinimumWidth = 96,
            MinimumHeight = 32
        },
    };
}

public readonly record struct UiTextFieldSkin
{
    public UiTextFieldAppearance Appearance
    {
        get; init;
    }
    public float? CornerRadius
    {
        get; init;
    }
    public float? BorderWidth
    {
        get; init;
    }
    public float? FocusedBorderWidth
    {
        get; init;
    }
    public float MinimumHeight
    {
        get; init;
    }
    public bool SelectAllOnFocus
    {
        get; init;
    }
}

public readonly record struct UiToggleSkin
{
    public UiToggleAppearance Appearance
    {
        get; init;
    }
    public UiButtonActivation Activation
    {
        get; init;
    }
    public float? CheckboxSize
    {
        get; init;
    }
    public float? CornerRadius
    {
        get; init;
    }
    public float? BorderWidth
    {
        get; init;
    }
    public float? SwitchWidth
    {
        get; init;
    }
    public float? SwitchHeight
    {
        get; init;
    }
    public float? OnThumbSize
    {
        get; init;
    }
    public float? OffThumbSize
    {
        get; init;
    }
    public float? PressedThumbSize
    {
        get; init;
    }
}

public readonly record struct UiSliderSkin
{
    public float? TrackHeight
    {
        get; init;
    }
    public float? TrackCornerRadius
    {
        get; init;
    }
    public float? HandleWidth
    {
        get; init;
    }
    public float? PressedHandleWidth
    {
        get; init;
    }
    public float? HandleHeight
    {
        get; init;
    }
    public float? HandleCornerRadius
    {
        get; init;
    }
    public float MinimumHeight
    {
        get; init;
    }
}

public readonly record struct UiSegmentedSkin
{
    public float? CornerRadius
    {
        get; init;
    }
    public float? ItemCornerRadius
    {
        get; init;
    }
    public float? Spacing
    {
        get; init;
    }
    public float MinimumHeight
    {
        get; init;
    }
    public UiButtonActivation Activation
    {
        get; init;
    }
}

public readonly record struct UiScrollbarSkin
{
    public UiScrollbarMotionOptions? Motion
    {
        get; init;
    }
    public float? Width
    {
        get; init;
    }
    public float? ThumbCornerRadius
    {
        get; init;
    }
    public float? RecentInteractionSeconds
    {
        get; init;
    }
    public bool AlwaysShowThumb
    {
        get; init;
    }
    public UiScrollbarTrackClick TrackClick
    {
        get; init;
    }
}

/// <summary>Emits an IUiDesign implementation with a statically selected widget policy.</summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class UiWidgetThemeAttribute(UiWidgetPreset preset) : Attribute
{
    public UiWidgetPreset Preset { get; } = preset;
    public float ButtonCornerRadius
    {
        get; set;
    }
    public float ButtonPressedCornerRadius
    {
        get; set;
    }
    public float ButtonBorderWidth
    {
        get; set;
    }
    public float ButtonPressedOffsetY
    {
        get; set;
    }
    public float ButtonPaddingX
    {
        get; set;
    }
    public float ButtonPaddingY
    {
        get; set;
    }
    public float ButtonMinimumWidth
    {
        get; set;
    }
    public float ButtonMinimumHeight
    {
        get; set;
    }
    public UiButtonActivation ButtonActivation
    {
        get; set;
    }
    public bool DarkColors
    {
        get; set;
    }
    public UiTextFieldAppearance TextFieldAppearance
    {
        get; set;
    }
    public float TextFieldCornerRadius
    {
        get; set;
    }
    public float TextFieldBorderWidth
    {
        get; set;
    }
    public float TextFieldFocusedBorderWidth
    {
        get; set;
    }
    public float TextFieldMinimumHeight
    {
        get; set;
    }
    public bool TextFieldSelectAllOnFocus
    {
        get; set;
    }
    public UiToggleAppearance ToggleAppearance
    {
        get; set;
    }
    public UiButtonActivation ToggleActivation
    {
        get; set;
    }
    public float ToggleCheckboxSize
    {
        get; set;
    }
    public float ToggleCornerRadius
    {
        get; set;
    }
    public float ToggleBorderWidth
    {
        get; set;
    }
    public float ToggleSwitchWidth
    {
        get; set;
    }
    public float ToggleSwitchHeight
    {
        get; set;
    }
    public float ToggleOnThumbSize
    {
        get; set;
    }
    public float ToggleOffThumbSize
    {
        get; set;
    }
    public float TogglePressedThumbSize
    {
        get; set;
    }
    public float SliderTrackHeight
    {
        get; set;
    }
    public float SliderTrackCornerRadius
    {
        get; set;
    }
    public float SliderHandleWidth
    {
        get; set;
    }
    public float SliderPressedHandleWidth
    {
        get; set;
    }
    public float SliderHandleHeight
    {
        get; set;
    }
    public float SliderHandleCornerRadius
    {
        get; set;
    }
    public float SliderMinimumHeight
    {
        get; set;
    }
    public float SegmentedCornerRadius
    {
        get; set;
    }
    public float SegmentedItemCornerRadius
    {
        get; set;
    }
    public float SegmentedSpacing
    {
        get; set;
    }
    public float SegmentedMinimumHeight
    {
        get; set;
    }
    public UiButtonActivation SegmentedActivation
    {
        get; set;
    }
    public float ScrollbarWidth
    {
        get; set;
    }
    public float ScrollbarThumbCornerRadius
    {
        get; set;
    }
    public float ScrollbarRecentInteractionSeconds
    {
        get; set;
    }
    public bool ScrollbarAlwaysShowThumb
    {
        get; set;
    }
    public UiScrollbarTrackClick ScrollbarTrackClick
    {
        get; set;
    }
}
