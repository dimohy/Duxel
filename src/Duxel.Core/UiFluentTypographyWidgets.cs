namespace Duxel.Core;

// Pinned Fluent typography source: artifacts/fluent-typography-2026-11-05/typographyStyles.ts.

public readonly struct UiFluentType100:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>10;
    public static float LineHeight=>14;
}

public readonly struct UiFluentType200:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>12;
    public static float LineHeight=>16;
}

public readonly struct UiFluentType300:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>14;
    public static float LineHeight=>20;
}

public readonly struct UiFluentType400:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>16;
    public static float LineHeight=>22;
}

public readonly struct UiFluentType500:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>20;
    public static float LineHeight=>28;
}

public readonly struct UiFluentType600:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>24;
    public static float LineHeight=>32;
}

public readonly struct UiFluentType700:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>28;
    public static float LineHeight=>36;
}

public readonly struct UiFluentType800:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>32;
    public static float LineHeight=>40;
}

public readonly struct UiFluentType900:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>40;
    public static float LineHeight=>52;
}

public readonly struct UiFluentType1000:IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize=>68;
    public static float LineHeight=>92;
}

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType300>))]
[UiComponent("Body1",UiComponentExposure.Fluent)]
public partial class UiFluentBody1;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType300>))]
[UiComponent("Body1",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicBody1;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType300>))]
[UiComponent("Body1Strong",UiComponentExposure.Fluent)]
public partial class UiFluentBody1Strong;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType300>))]
[UiComponent("Body1Strong",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicBody1Strong;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiBoldTypeface,UiFluentType300>))]
[UiComponent("Body1Stronger",UiComponentExposure.Fluent)]
public partial class UiFluentBody1Stronger;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiBoldTypeface,UiFluentType300>))]
[UiComponent("Body1Stronger",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicBody1Stronger;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType400>))]
[UiComponent("Body2",UiComponentExposure.Fluent)]
public partial class UiFluentBody2;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType400>))]
[UiComponent("Body2",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicBody2;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType200>))]
[UiComponent("Caption1",UiComponentExposure.Fluent)]
public partial class UiFluentCaption1;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType200>))]
[UiComponent("Caption1",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicCaption1;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType200>))]
[UiComponent("Caption1Strong",UiComponentExposure.Fluent)]
public partial class UiFluentCaption1Strong;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType200>))]
[UiComponent("Caption1Strong",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicCaption1Strong;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiBoldTypeface,UiFluentType200>))]
[UiComponent("Caption1Stronger",UiComponentExposure.Fluent)]
public partial class UiFluentCaption1Stronger;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiBoldTypeface,UiFluentType200>))]
[UiComponent("Caption1Stronger",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicCaption1Stronger;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType100>))]
[UiComponent("Caption2",UiComponentExposure.Fluent)]
public partial class UiFluentCaption2;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiRegularTypeface,UiFluentType100>))]
[UiComponent("Caption2",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicCaption2;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType100>))]
[UiComponent("Caption2Strong",UiComponentExposure.Fluent)]
public partial class UiFluentCaption2Strong;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType100>))]
[UiComponent("Caption2Strong",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicCaption2Strong;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType500>))]
[UiComponent("Subtitle1",UiComponentExposure.Fluent)]
public partial class UiFluentSubtitle1;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType500>))]
[UiComponent("Subtitle1",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicSubtitle1;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType400>))]
[UiComponent("Subtitle2",UiComponentExposure.Fluent)]
public partial class UiFluentSubtitle2;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType400>))]
[UiComponent("Subtitle2",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicSubtitle2;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiBoldTypeface,UiFluentType400>))]
[UiComponent("Subtitle2Stronger",UiComponentExposure.Fluent)]
public partial class UiFluentSubtitle2Stronger;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiBoldTypeface,UiFluentType400>))]
[UiComponent("Subtitle2Stronger",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicSubtitle2Stronger;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType800>))]
[UiComponent("Title1",UiComponentExposure.Fluent)]
public partial class UiFluentTitle1;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType800>))]
[UiComponent("Title1",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicTitle1;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType700>))]
[UiComponent("Title2",UiComponentExposure.Fluent)]
public partial class UiFluentTitle2;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType700>))]
[UiComponent("Title2",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicTitle2;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType600>))]
[UiComponent("Title3",UiComponentExposure.Fluent)]
public partial class UiFluentTitle3;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType600>))]
[UiComponent("Title3",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicTitle3;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType900>))]
[UiComponent("LargeTitle",UiComponentExposure.Fluent)]
public partial class UiFluentLargeTitle;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType900>))]
[UiComponent("LargeTitle",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicLargeTitle;

[UiWidgetStyle(typeof(UiText<,,>),typeof(string),typeof(UiStaticTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType1000>))]
[UiComponent("Display",UiComponentExposure.Fluent)]
public partial class UiFluentDisplay;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTypographyPresentation<UiSemiboldTypeface,UiFluentType1000>))]
[UiComponent("Display",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicDisplay;
