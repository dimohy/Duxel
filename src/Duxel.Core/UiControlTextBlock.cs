namespace Duxel.Core;

/// <summary>Retained line strings and measurements; measure and paint under the same typography scope.</summary>
public readonly struct UiControlTextBlock
{
    private readonly string? _single;
    private readonly UiControlTextLine[]? _lines;
    public UiVector2 Size {get;}
    public int RetainedCharacters {get;}
    public int LineCount=>_lines?.Length??(_single is null?0:1);
    internal int RetainedLineCount=>_lines?.Length??0;
    internal UiControlTextBlock(string text,UiVector2 size,int retainedCharacters)
    {_single=text;_lines=null;Size=size;RetainedCharacters=retainedCharacters;}
    internal UiControlTextBlock(UiControlTextLine[] lines,UiVector2 size,int retainedCharacters)
    {_single=null;_lines=lines;Size=size;RetainedCharacters=retainedCharacters;}
    public UiControlTextLine GetLine(int index)
    {
        if((uint)index>=(uint)LineCount)throw new ArgumentOutOfRangeException(nameof(index));
        return _lines is null?new(_single!,Size):_lines[index];
    }
}
public readonly record struct UiControlTextLine(string Text,UiVector2 Size);

public sealed partial class UiImmediateContext
{
    internal UiControlTextBlock MeasureControlTextBlock(string text)
    {
        if(_controlTypographyHeight<=0)throw new InvalidOperationException("Text blocks require an explicit typography scope.");
        var normalized=text.Replace("\r",string.Empty,StringComparison.Ordinal);
        var count=1;foreach(var c in normalized)if(c=='\n')count++;
        if(count==1)return new(normalized,MeasureControlText(normalized) with{Y=_controlTypographyHeight},ReferenceEquals(text,normalized)?0:normalized.Length);
        var lines=new UiControlTextLine[count];var start=0;var line=0;var width=0f;var retained=0;
        for(var i=0;i<=normalized.Length;i++)
        {
            if(i<normalized.Length&&normalized[i]!='\n')continue;
            var value=normalized.Substring(start,i-start);var measured=MeasureControlText(value) with{Y=_controlTypographyHeight};
            lines[line++]=new(value,measured);retained+=value.Length;width=MathF.Max(width,measured.X);start=i+1;
        }
        return new(lines,new(width,count*_controlTypographyHeight),retained);
    }
    internal void PaintControlTextBlock(in UiControlTextBlock block,UiRect bounds,UiColor color,UiItemHorizontalAlign align)
    {
        if(block.LineCount==0)return;
        // Bounds place lines; the caller's clip controls glyph ink, which may
        // extend beyond its measured advance. Never clip a run to advance width.
        var clip=ResolveItemClipRect();
        if(clip.Width<=0||clip.Height<=0)return;
        var height=_controlTypographyHeight;
        if(height<=0)throw new InvalidOperationException("Text blocks require an explicit typography scope.");
        var overhang=MathF.Max(height,(_controlTypographyMetrics.Ascent+_controlTypographyMetrics.Descent)/_contentScale);
        var first=(int)Math.Clamp(MathF.Floor((clip.Y-bounds.Y-overhang)/height),0,block.LineCount);
        var end=(int)Math.Clamp(MathF.Ceiling((clip.Y+clip.Height-bounds.Y+overhang)/height),first,block.LineCount);
        var leading=(height-(_controlTypographyMetrics.Ascent+_controlTypographyMetrics.Descent)/_contentScale)/2;
        for(var i=first;i<end;i++)
        {
            var line=block.GetLine(i);if(line.Text.Length==0)continue;
            var x=align switch
            {
                UiItemHorizontalAlign.Left=>bounds.X,
                UiItemHorizontalAlign.Center=>bounds.X+(bounds.Width-line.Size.X)/2,
                UiItemHorizontalAlign.Right=>bounds.X+bounds.Width-line.Size.X,
                _=>throw new ArgumentOutOfRangeException(nameof(align))
            };
            PaintControlTypographyRun(line.Text,new(x,bounds.Y+i*height+leading),color,clip);
        }
    }
}
