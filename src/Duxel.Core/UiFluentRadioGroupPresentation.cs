namespace Duxel.Core;

public interface IUiRadioGroupLayout
{
    /// <summary>Layout depends only on supplied options, heading dimensions and available width.</summary>
    static virtual bool CanRetainMeasurement => false;
    static abstract bool LabelBelow {get;}
    static abstract float OptionWidth(float availableWidth,int count);
    static abstract UiVector2 Place(Span<UiRadioOptionLayout> options,float labelHeight,float labelWidth);
}
public readonly struct UiRadioVerticalLayout:IUiRadioGroupLayout
{
    public static bool CanRetainMeasurement => true;
    public static bool LabelBelow=>false;
    public static float OptionWidth(float availableWidth,int count)=>availableWidth;
    public static UiVector2 Place(Span<UiRadioOptionLayout> options,float labelHeight,float labelWidth)
    {
        var y=labelHeight;var width=labelWidth;
        for(var i=0;i<options.Length;i++){options[i]=options[i] with{Bounds=options[i].Bounds with{Y=y}};y+=options[i].Bounds.Height;width=MathF.Max(width,options[i].Bounds.Width);}
        return new(width,y);
    }
}
public readonly struct UiRadioHorizontalLayout:IUiRadioGroupLayout
{
    public static bool CanRetainMeasurement => true;
    public static bool LabelBelow=>false;
    public static float OptionWidth(float availableWidth,int count)=>availableWidth/Math.Max(1,count);
    public static UiVector2 Place(Span<UiRadioOptionLayout> options,float labelHeight,float labelWidth)=>UiRadioHorizontalStackedLayout.Place(options,labelHeight,labelWidth);
}
public readonly struct UiRadioHorizontalStackedLayout:IUiRadioGroupLayout
{
    public static bool CanRetainMeasurement => true;
    public static bool LabelBelow=>true;
    public static float OptionWidth(float availableWidth,int count)=>availableWidth/Math.Max(1,count);
    public static UiVector2 Place(Span<UiRadioOptionLayout> options,float labelHeight,float labelWidth)
    {
        var x=0f;var height=0f;
        for(var i=0;i<options.Length;i++){options[i]=options[i] with{Bounds=options[i].Bounds with{X=x,Y=labelHeight}};x+=options[i].Bounds.Width;height=MathF.Max(height,options[i].Bounds.Height);}
        return new(MathF.Max(x,labelWidth),labelHeight+height);
    }
}

public readonly struct UiFluentRadioGroupPresentation<TLayout>:IUiRadioGroupPresentation where TLayout:IUiRadioGroupLayout
{
    public static UiRadioGroupLayout Arrange(UiControlContext context,in UiRadioGroupLayout natural,UiVector2 allocatedSize)=>natural with{Size=allocatedSize};
    public static bool ReuseLayout => TLayout.CanRetainMeasurement;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>context.BeginTypography<UiRegularTypeface,UiFluentType300>();
    public static UiRadioGroupLayout Measure(UiControlContext context,string label,ReadOnlySpan<UiRadioOption> items,Span<UiRadioOptionLayout> layouts,float width)
    {
        var heading=context.WrapText(label,width);var headingBlock=heading.Length==0?default:context.MeasureTextBlock(heading);
        var headingSize=headingBlock.Size;var headingHeight=headingSize.Y;
        var optionWidth=TLayout.OptionWidth(width,items.Length);
        for(var i=0;i<items.Length;i++)
        {
            var maxLabel=MathF.Max(1,optionWidth-(TLayout.LabelBelow?16:44));
            var text=context.WrapText(items[i].Label,maxLabel);var textBlock=context.MeasureTextBlock(text);var measured=textBlock.Size;
            var textHeight=measured.Y;
            string? description=items[i].Description;var descriptionSize=default(UiVector2);var descriptionHeight=0f;
            var descriptionBlock=default(UiControlTextBlock);
            if(!string.IsNullOrEmpty(description))
            {
                using var caption=context.BeginTypography<UiRegularTypeface,UiFluentType200>();
                description=context.WrapText(description,maxLabel);descriptionBlock=context.MeasureTextBlock(description);descriptionSize=descriptionBlock.Size;descriptionHeight=descriptionSize.Y;
            }
            var textWidth=MathF.Max(measured.X,descriptionSize.X);
            var rowWidth=MathF.Max(32,textWidth+(TLayout.LabelBelow?16:44));
            var textTop=TLayout.LabelBelow?36:6;
            var rowHeight=MathF.Max(32,textTop+textHeight+(descriptionHeight>0?descriptionHeight+2:0)+(TLayout.LabelBelow?8:6));
            var textX=TLayout.LabelBelow?8:36;
            layouts[i]=new(new(0,0,rowWidth,rowHeight),new(TLayout.LabelBelow?(rowWidth-16)/2:8,8,16,16),
                new(textX,textTop,rowWidth-textX-8,textHeight),new(textX,textTop+textHeight+2,rowWidth-textX-8,descriptionHeight),text,description)
                {LabelBlock=textBlock,DescriptionBlock=descriptionBlock};
        }
        var size=TLayout.Place(layouts,headingHeight>0?headingHeight+4:0,headingSize.X);
        var headingBounds = new UiRect(0, 0, headingSize.X, headingHeight);
        if (context.RightToLeft)
        {
            headingBounds = Mirror(headingBounds, size.X);
            foreach (ref var row in layouts)
            {
                var rowWidth = row.Bounds.Width;
                row = row with
                {
                    Bounds = Mirror(row.Bounds, size.X),
                    Indicator = Mirror(row.Indicator, rowWidth),
                    LabelBounds = Mirror(row.LabelBounds, rowWidth),
                    DescriptionBounds = Mirror(row.DescriptionBounds, rowWidth)
                };
            }
        }
        return new(size, headingBounds, heading) { LabelBlock = headingBlock };
    }

    private static UiRect Mirror(UiRect bounds, float width) => bounds with { X = width - bounds.X - bounds.Width };

    public static void PaintLabel(UiControlContext context,UiRadioGroupLayout layout,UiRect bounds)
    {
        if(layout.Label.Length==0)return;
        var label = layout.LabelBounds;
        context.TypographyTextBlock(layout.LabelBlock, label with { X = bounds.X + label.X, Y = bounds.Y + label.Y }, context.Theme.Text);
    }
    public static void PaintOption(UiControlContext context,UiRadioOptionLayout layout,bool selected,UiButtonInteraction interaction)
    {
        var bounds=layout.Bounds;
        using var clip=context.Clip(bounds);
        var state=!context.IsEnabled?UiButtonVisualState.Disabled:interaction.Hovered&&interaction.Held?UiButtonVisualState.Pressed:interaction.Hovered?UiButtonVisualState.Hover:UiButtonVisualState.Rest;
        // Checked radios share the outlined brand-indicator tokens with mixed Checkbox.
        var forcedColors = context.Theme.ForcedColors;
        var paint = UiFluentCheckboxPalette.Resolve(context.FluentColorScheme, state, selected ? null : false);
        if (forcedColors is { } colors)
        {
            var foreground = context.IsEnabled ? colors.CanvasText : colors.GrayText;
            var dot = context.IsEnabled ? colors.Highlight : colors.GrayText;
            var border = context.IsEnabled && (!selected || interaction.Hovered) ? colors.ButtonText : dot;
            paint = new(default, border, dot, foreground);
        }
        var indicator=layout.Indicator with{X=bounds.X+layout.Indicator.X,Y=bounds.Y+layout.Indicator.Y};
        context.Frame(indicator,new(0,0,0,0),paint.Border,8,1);
        if(selected)context.Circle(new(indicator.X+8,indicator.Y+8),5,paint.Indicator);
        if (interaction.FocusVisible)
            context.FocusOutline(bounds, 4, forcedColors?.Highlight ?? context.Theme.Text);
        var label=layout.LabelBounds with{X=bounds.X+layout.LabelBounds.X,Y=bounds.Y+layout.LabelBounds.Y};
        var labelY = TLayout.LabelBelow ? label.Y : context.IndicatorBlockY(layout.LabelBlock, indicator.Y + indicator.Height / 2, label.Y);
        var labelShift = labelY - label.Y;
        label = label with { Y = labelY };
        var align = TLayout.LabelBelow ? UiItemHorizontalAlign.Center
            : context.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left;
        context.TypographyTextBlock(layout.LabelBlock, label, paint.Label, align);
        if(!string.IsNullOrEmpty(layout.Description))
        {
            using var caption=context.BeginTypography<UiRegularTypeface,UiFluentType200>();
            var description=layout.DescriptionBounds with{X=bounds.X+layout.DescriptionBounds.X,Y=bounds.Y+layout.DescriptionBounds.Y+labelShift};
            var color = forcedColors.HasValue ? paint.Label
                : UiFluentCheckboxPalette.Resolve(context.FluentColorScheme,
                    context.IsEnabled ? UiButtonVisualState.Rest : UiButtonVisualState.Disabled, false).Label;
            context.TypographyTextBlock(layout.DescriptionBlock, description, color, align);
        }
    }
}
