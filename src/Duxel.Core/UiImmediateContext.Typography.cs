namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal UiVector2 MeasureControlTypography<TTypeface,TMetrics>(string text,bool wrap)
        where TTypeface:IUiTypographyTypeface where TMetrics:IUiTypographyMetrics
    {
        using var typography=BeginControlTypography<TTypeface,TMetrics>(text.Length>0);
        if(text.Length==0)return new(0,typography.LineHeight);
        var normalized=text.Replace("\r",string.Empty,StringComparison.Ordinal);
        if(wrap)normalized=WrapControlText(normalized);
        return MeasureControlTextBlock(normalized).Size;
    }
    private UiTypographyFonts? _typographyFonts;
    internal void SetTypographyFonts(UiTypographyFonts? fonts)
    {
        if(_typographyFonts!=fonts)
        {
            InvalidateLayoutMeasurementResources();
            _controlMetricSlots=default;_controlMetricLast=_controlMetricNext=0;
            if(_layerCacheStore is not null)MarkAllLayersDirty();
        }
        _typographyFonts=fonts;
    }

    internal void RenderTypography<TTypeface,TMetrics>(string text,UiColor? color,bool wrap=false)
        where TTypeface:IUiTypographyTypeface where TMetrics:IUiTypographyMetrics
    {
        if(!_directTextEnabled||_platformTextBackend is not IPlatformFontMetricsBackend metricsBackend)
            throw new NotSupportedException("Fluent typography requires DirectText and a font-metrics backend.");
        var fonts=_typographyFonts??throw new InvalidOperationException("Typography font faces have not been configured.");
        var paths=TTypeface.Select(fonts);
        var fontSize=TMetrics.FontSize*_textSettings.Scale;
        var lineHeight=TMetrics.LineHeight*_textSettings.Scale;
        var physicalFontSize=fontSize*_contentScale;
        if(!float.IsFinite(physicalFontSize)||physicalFontSize<=0||!float.IsFinite(lineHeight)||lineHeight<=0)
            throw new ArgumentOutOfRangeException(nameof(TMetrics),"Typography sizes must be finite and positive.");
        var vector=IsVectorTextLayer();
        if(!vector&&physicalFontSize>512)
            throw new NotSupportedException("The raster text backend supports typography up to512 physical pixels.");
        var metricsSize=vector?physicalFontSize:MathF.Max(1,MathF.Round(physicalFontSize));
        if(!metricsBackend.TryGetFontMetrics(paths.Primary,metricsSize,out var metrics))
            throw new InvalidOperationException("Typography font metrics are unavailable: "+paths.Primary);
        var previousPrimary=_directTextPrimaryFontPath;
        var previousSecondary=_directTextSecondaryFontPath;
        var previousSize=_pushedFontSize;
        _directTextPrimaryFontPath=paths.Primary;
        _directTextSecondaryFontPath=paths.Secondary;
        _pushedFontSize=physicalFontSize;
        try
        {
            var normalized=text.Replace("\r",string.Empty,StringComparison.Ordinal);
            if(wrap)normalized=WrapControlText(normalized);
            var width=0f;var lines=1;var start=0;
            for(var i=0;i<=normalized.Length;i++)
            {
                if(i<normalized.Length&&normalized[i]!='\n')continue;
                if(i>start)
                {
                    var line=start==0&&i==normalized.Length?normalized:normalized.Substring(start,i-start);
                    if(!TryMeasureDirectText(line,_textSettings,out var measured))
                        throw new InvalidOperationException("Typography text measurement failed.");
                    width=MathF.Max(width,measured.X);
                }
                if(i<normalized.Length)lines++;
                start=i+1;
            }
            var layout=PlaceControlText(new(width,lines*lineHeight));
            if(!layout.Visible)return;
            var leading=(lineHeight-(metrics.Ascent+metrics.Descent)/_contentScale)*.5f;
            start=0;var index=0;
            for(var i=0;i<=normalized.Length;i++)
            {
                if(i<normalized.Length&&normalized[i]!='\n')continue;
                if(i>start)
                {
                    var line=start==0&&i==normalized.Length?normalized:normalized.Substring(start,i-start);
                    var sizeQuarter=(int)MathF.Round(physicalFontSize*4);
                    float runAscent;
                    if(vector)runAscent=GetVectorTextMesh(line,sizeQuarter,physicalFontSize).Baseline;
                    else
                    {
                        if(!TryGetOrCreateDirectText(line,physicalFontSize,out var entry))
                            throw new InvalidOperationException("Typography glyph data is unavailable.");
                        runAscent=entry.FontAscent;
                    }
                    var position=new UiVector2(layout.Position.X,layout.Position.Y+index*lineHeight+leading+(metrics.Ascent-runAscent)/_contentScale);
                    if(!TryRenderDirectText(_builder,line,position,color??_theme.Text,layout.ClipRect,_textSettings,out _))
                        throw new InvalidOperationException("Typography text rendering failed.");
                }
                start=i+1;index++;
            }
        }
        finally
        {
            _directTextPrimaryFontPath=previousPrimary;
            _directTextSecondaryFontPath=previousSecondary;
            _pushedFontSize=previousSize;
        }
    }
}
