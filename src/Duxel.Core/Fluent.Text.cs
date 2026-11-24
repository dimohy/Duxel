namespace Duxel.Core;

/// <summary>Fluent typography styles available through the Text factory.</summary>
public enum Text
{
    Body1,
    Display,
    LargeTitle,
    Title1,
    Title2,
    Title3,
    Subtitle1,
    Subtitle2,
    Subtitle2Stronger,
    Body2,
    Body1Strong,
    Body1Stronger,
    Caption1,
    Caption1Strong,
    Caption1Stronger,
    Caption2,
    Caption2Strong
}

public static partial class Fluent
{
    /// <summary>Creates constant text using the selected Fluent typography style.</summary>
    public static UiWidget Text(global::Duxel.Core.Text style, string text) => style switch
    {
        global::Duxel.Core.Text.Display => Display(text),
        global::Duxel.Core.Text.LargeTitle => LargeTitle(text),
        global::Duxel.Core.Text.Title1 => Title1(text),
        global::Duxel.Core.Text.Title2 => Title2(text),
        global::Duxel.Core.Text.Title3 => Title3(text),
        global::Duxel.Core.Text.Subtitle1 => Subtitle1(text),
        global::Duxel.Core.Text.Subtitle2 => Subtitle2(text),
        global::Duxel.Core.Text.Subtitle2Stronger => Subtitle2Stronger(text),
        global::Duxel.Core.Text.Body2 => Body2(text),
        global::Duxel.Core.Text.Body1 => Body1(text),
        global::Duxel.Core.Text.Body1Strong => Body1Strong(text),
        global::Duxel.Core.Text.Body1Stronger => Body1Stronger(text),
        global::Duxel.Core.Text.Caption1 => Caption1(text),
        global::Duxel.Core.Text.Caption1Strong => Caption1Strong(text),
        global::Duxel.Core.Text.Caption1Stronger => Caption1Stronger(text),
        global::Duxel.Core.Text.Caption2 => Caption2(text),
        global::Duxel.Core.Text.Caption2Strong => Caption2Strong(text),
        _ => throw new ArgumentOutOfRangeException(nameof(style))
    };

    /// <summary>Creates text that reads its value from a callback using the selected Fluent typography style.</summary>
    public static UiWidget Text(global::Duxel.Core.Text style, Func<string> text) => style switch
    {
        global::Duxel.Core.Text.Display => Display(text),
        global::Duxel.Core.Text.LargeTitle => LargeTitle(text),
        global::Duxel.Core.Text.Title1 => Title1(text),
        global::Duxel.Core.Text.Title2 => Title2(text),
        global::Duxel.Core.Text.Title3 => Title3(text),
        global::Duxel.Core.Text.Subtitle1 => Subtitle1(text),
        global::Duxel.Core.Text.Subtitle2 => Subtitle2(text),
        global::Duxel.Core.Text.Subtitle2Stronger => Subtitle2Stronger(text),
        global::Duxel.Core.Text.Body2 => Body2(text),
        global::Duxel.Core.Text.Body1 => Body1(text),
        global::Duxel.Core.Text.Body1Strong => Body1Strong(text),
        global::Duxel.Core.Text.Body1Stronger => Body1Stronger(text),
        global::Duxel.Core.Text.Caption1 => Caption1(text),
        global::Duxel.Core.Text.Caption1Strong => Caption1Strong(text),
        global::Duxel.Core.Text.Caption1Stronger => Caption1Stronger(text),
        global::Duxel.Core.Text.Caption2 => Caption2(text),
        global::Duxel.Core.Text.Caption2Strong => Caption2Strong(text),
        _ => throw new ArgumentOutOfRangeException(nameof(style))
    };
}
