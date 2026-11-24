namespace Duxel.Core;

/// <summary>A standalone label borrowing its target and owning optional supplemental popover content.</summary>
public class UiInfoLabel : UiControlWidget
{
    internal UiLabel LabelControl
    {
        get;
    }
    internal UiInfoButton? InfoControl
    {
        get;
    }
    private string _informationLabel;

    public UiInfoLabel(string text, UiWidget? info = null, UiWidget? target = null,
        UiLabelOptions options = default, string informationLabel = "More information",
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateInformationLabel(informationLabel);
        _ = InformationName(text, informationLabel);
        options.Validate();
        _informationLabel = informationLabel;
        LabelControl = new(text, target, options);
        OwnChild("label", LabelControl);
        if (info is not null)
        {
            InfoControl = new(InformationName(text, informationLabel), info, options.Size);
            OwnChild("information", InfoControl);
        }
    }

    public string Text
    {
        get => LabelControl.Text;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (Text == value)
                return;
            ThrowIfBuilding();
            var name = InformationName(value, _informationLabel);
            LabelControl.Text = value;
            if (InfoControl is { } button)
                button.Label = name;
            RequestRebuild();
        }
    }
    public string InformationLabel
    {
        get => _informationLabel;
        set
        {
            ThrowIfDisposed();
            ValidateInformationLabel(value);
            if (_informationLabel == value)
                return;
            ThrowIfBuilding();
            _informationLabel = value;
            if (InfoControl is { } button)
                button.Label = InformationName(Text, value);
            RequestRebuild();
        }
    }
    public UiLabelOptions Options
    {
        get => LabelControl.Options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (Options == value)
                return;
            ThrowIfBuilding();
            LabelControl.Options = value;
            if (InfoControl is { } button)
                button.Size = value.Size;
            RequestRebuild();
        }
    }
    public UiWidget? Target
    {
        get => LabelControl.Target;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(Target, value))
                return;
            ThrowIfBuilding();
            LabelControl.Target = value;
            RequestRebuild();
        }
    }
    public UiWidget? Info => InfoControl?.Content;
    public bool IsOpen => InfoControl?.IsOpen == true;
    public UiBinding<bool>? Open
    {
        get => InfoControl?.Open;
        set
        {
            ThrowIfDisposed();
            RequireInfo().Open = value;
        }
    }
    public Action<bool>? OpenChanged
    {
        get => InfoControl?.OpenChanged;
        set
        {
            ThrowIfDisposed();
            RequireInfo().OpenChanged = value;
        }
    }
    public UiPopoverOptions PopoverOptions
    {
        get => RequireInfo().Options;
        set
        {
            ThrowIfDisposed();
            RequireInfo().Options = UiInfoButton.Configure(value, Options.Size);
        }
    }
    public void Show()
    {
        ThrowIfDisposed();
        RequireInfo().Show();
    }
    public bool Close()
    {
        ThrowIfDisposed();
        return InfoControl?.Close() ?? true;
    }
    private UiInfoButton RequireInfo() => InfoControl ?? throw new InvalidOperationException("This InfoLabel has no information content.");
    private static string InformationName(string text, string suffix)
    {
        var label = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (UiAccessibilityTree.ContainsControl(label, false))
            throw new ArgumentException("The visible label contains unsupported control characters.", nameof(text));
        return label.Length > 0 ? label + " " + suffix : suffix;
    }
    private static void ValidateInformationLabel(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (UiAccessibilityTree.ContainsControl(value, false))
            throw new ArgumentException("The information button label must be single-line.", nameof(value));
    }
    protected override bool CanRetainMeasurement => true;
    internal (UiVector2 Label, UiVector2 LabelOrigin, UiVector2 Button, UiVector2 ButtonOrigin, UiVector2 Size) Layout(UiControlContext context)
    {
        var constraints = context.LayoutConstraints;
        var button = InfoControl is { } info ? context.MeasureParentDataChild(info, null, new()) : default;
        var label = UiImmediateContext.MeasureLabel(context, Text, Options, new(0, constraints.MaxWidth, 0, constraints.MaxHeight));
        if (InfoControl is null)
            return (label.Size, default, default, default, constraints.Constrain(label.Size));
        var lineHeight = label.Text.Size.Y / label.Text.LineCount;
        var margin = Options.Size == UiLabelSize.Large ? 1 : 2;
        var last = Math.Max(0, label.Text.LineCount - 1);
        var x = label.Text.LineCount > 0 ? label.Text.GetLine(last).Size.X : 0;
        var y = last * lineHeight;
        if (Options.Required)
        {
            x = label.Required.X + label.Required.Width;
            y = label.Required.Y;
        }
        if (x + button.X > constraints.MaxWidth)
        {
            x = 0;
            y += lineHeight;
        }
        // Native parent allocations clip their children. Keep the full button target inside
        // the box while preserving Fluent's text-to-icon alignment as an internal text inset.
        return (label.Size, new(0, margin), button, new(x, y),
            constraints.Constrain(new(MathF.Max(label.Size.X, x + button.X), MathF.Max(label.Size.Y + margin * 2, y + button.Y))));
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => Layout(context).Size;
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderInfoLabel(this);
}

[UiWidgetStyle(typeof(UiInfoLabel))]
[UiComponent("InfoLabel", UiComponentExposure.Fluent)]
public partial class UiFluentInfoLabel;

internal sealed class UiInfoButton : UiPopover<UiInfoButtonPresentation<UiInfoMedium>>
{
    private UiLabelSize _size;
    internal UiInfoButton(string label, UiWidget info, UiLabelSize size)
        : base(label, info, Configure(new(Width: 264, WithArrow: true,
            Positioning: new()
            {
                Position = UiPopupPosition.AboveStart
            }), size)) => _size = size;
    internal UiLabelSize Size
    {
        get => _size;
        set
        {
            ThrowIfDisposed();
            if (_size == value)
                return;
            ThrowIfBuilding();
            Options = Configure(Options, value);
            _size = value;
            RequestRebuild();
        }
    }
    internal static UiPopoverOptions Configure(UiPopoverOptions options, UiLabelSize size)
    {
        if ((uint)size > 2 || options.TriggerMode != UiPopoverTriggerMode.Click)
            throw new ArgumentOutOfRangeException(nameof(options), "InfoLabel supports a click or keyboard information trigger.");
        return options with
        {
            Size = size == UiLabelSize.Large ? UiPopoverSize.Medium : UiPopoverSize.Small,
            CloseOnFocusLeave = true,
            SurfaceRole = UiAccessibilityRole.Note
        };
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => _size switch
    {
        UiLabelSize.Small => MeasurePopover<UiInfoButtonPresentation<UiInfoSmall>>(context),
        UiLabelSize.Large => MeasurePopover<UiInfoButtonPresentation<UiInfoLarge>>(context),
        _ => MeasurePopover<UiInfoButtonPresentation<UiInfoMedium>>(context)
    };
    protected override void RenderControl(UiImmediateContext ui)
    {
        switch (_size)
        {
            case UiLabelSize.Small:
                RenderPopover<UiInfoButtonPresentation<UiInfoSmall>>(ui);
                break;
            case UiLabelSize.Large:
                RenderPopover<UiInfoButtonPresentation<UiInfoLarge>>(ui);
                break;
            default:
                RenderPopover<UiInfoButtonPresentation<UiInfoMedium>>(ui);
                break;
        }
    }
}

public sealed partial class UiImmediateContext
{
    internal void RenderInfoLabel(UiInfoLabel label)
    {
        EnsureNotMeasuring();
        var layout = label.Layout(new(this));
        var origin = AllocateLayoutBox(layout.Size);
        var root = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        UiRect ChildBounds(UiVector2 size, UiVector2 offset) =>
            new(root.X + (RightToLeft ? root.Width - offset.X - size.X : offset.X),
                root.Y + offset.Y, size.X, size.Y);
        RenderParentDataChild(label.LabelControl, null, ChildBounds(layout.Label, layout.LabelOrigin), root);
        if (label.InfoControl is { } info)
        {
            RenderParentDataChild(info, null, ChildBounds(layout.Button, layout.ButtonOrigin), root);
        }
        FinishLayoutBox(origin, layout.Size);
    }
}
