namespace Duxel.Core;

/// <summary>Shared title, close action and surface chrome for dialogs and drawers.</summary>
public interface IUiSurfacePresentation
{
    static abstract UiDialogMetrics Metrics
    {
        get;
    }
    static abstract UiControlTypographyScope BeginBodyText(UiControlContext context);
    static abstract UiVector2 MeasureTitle(UiControlContext context, string title);
    static abstract void PaintTitle(UiControlContext context, string title, UiColor? color);
    static abstract UiBoxShadowOptions Shadow(UiControlContext context);
    static abstract UiColor Background(UiControlContext context);
    static abstract UiColor Foreground(UiControlContext context);
    static abstract UiColor Backdrop(UiControlContext context);
    static abstract void PaintClose(UiControlContext context, UiRect bounds, UiButtonInteraction interaction);
}

