namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private float ResolveDirectTextFontSize(UiTextSettings settings) => _pushedFontSize > 0f
        ? MathF.Max(1f, _pushedFontSize) : _directTextBaseFontSize > 0f
            ? MathF.Max(1f, _directTextBaseFontSize * settings.Scale) : MathF.Max(1f, _lineHeight * settings.Scale);

    /// <summary>Selects the explicit name language for accessible font metadata. The initial value is und.</summary>
    public void SetAccessibilityFontNameLanguage(string language)
    {
        EnsureNotMeasuring();
        PlatformFontMetadata.ValidateNameLanguage(language);
        if (_accessibility is { } tree)
            tree.FontNameLanguage = language;
    }
    private void CaptureAccessibilityTextAppearance(string id, UiAccessibilityTextInfo info, in UiInputTextPaint paint)
    {
        if (_accessibility is not { } tree)
            return;
        var direct = (_directTextEnabled || paint.Typography) && HasDirectTextBackend();
        var fonts = direct ? _platformTextBackend as IPlatformTextFontRunBackend : null;
        UiAccessibilityTextSize? size = direct ? new(ResolveDirectTextFontSize(_textSettings), _contentScale) : null;
        size?.Validate();
        PlatformTextMeasureRequest? request = fonts is null ? null
            : new(_directTextPrimaryFontPath!, _directTextSecondaryFontPath, info.Content.Text, ResolveDirectTextFontSize(_textSettings));
        tree.CaptureTextAppearance(id, info.Content, paint.Text, LayoutMeasurementKey, fonts, request, size);
    }
}
