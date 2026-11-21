using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Duxel.Core;

public enum UiAccordionSize { Medium, Small, Large, ExtraLarge }
public enum UiAccordionChevronPosition { Start, End }
public readonly record struct UiAccordionOptions(bool Multiple = false, bool Collapsible = false,
    UiAccordionSize Size = UiAccordionSize.Medium, UiAccordionChevronPosition ChevronPosition = UiAccordionChevronPosition.Start)
{
    public UiAccordionOptions Validate()
    {
        if ((uint)Size > 3 || (uint)ChevronPosition > 1) throw new ArgumentOutOfRangeException(nameof(UiAccordionOptions));
        return this;
    }
}

/// <summary>The accordion exclusively owns Content. Icon painters are borrowed.</summary>
public readonly record struct UiAccordionItem(string Value, string Label, UiWidget Content, bool Enabled = true,
    UiMenuIcon Icon = default);
public readonly record struct UiAccordionHeaderLayout(UiControlTextBlock Text, UiVector2 Size);

public interface IUiAccordionPresentation
{
    static abstract UiAccordionHeaderLayout MeasureHeader(UiControlContext context, UiAccordionItem item,
        float width, UiAccordionOptions options);
    static abstract void PaintHeader(UiControlContext context, UiAccordionItem item, UiAccordionHeaderLayout layout,
        UiRect bounds, bool expanded, UiButtonInteraction interaction, UiAccordionOptions options);
    static abstract float PanelMargin { get; }
}

/// <summary>A controlled, vertically laid out accordion owning persistent panel widgets.</summary>
public class UiAccordion<TBehavior, TPresentation> : UiMultiChildWidget
    where TBehavior : IUiButtonBehavior
    where TPresentation : IUiAccordionPresentation
{
    private UiAccordionItem[] _items;
    private UiBinding<ImmutableArray<string>>? _value;
    private string _label;
    private float _width;
    private UiAccordionOptions _options;
    internal readonly UiAccordionHeaderLayout[] Headers;
    internal readonly UiRect[] HeaderBounds, PanelBounds;
    internal readonly UiButtonInteraction[] Interactions;
    internal readonly string[] Suffixes;
    internal readonly string[] HeaderIds, PanelIds;
    internal int FocusedPanel = -1;
    internal string? FocusedPanelControl;

    public UiAccordion(string label, UiBinding<ImmutableArray<string>> value, ReadOnlyMemory<UiAccordionItem> items,
        float width = 320, UiAccordionOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : this(Prepare(label, value, items, width, options), label, value, width, options, identity) { }

    private UiAccordion(UiAccordionItem[] items, string label, UiBinding<ImmutableArray<string>> value, float width,
        UiAccordionOptions options, UiWidgetIdentity identity) : base(Contents(items), identity)
    {
        _items = items; _label = label; _value = value; _width = width; _options = options;
        Headers = new UiAccordionHeaderLayout[items.Length];
        HeaderBounds = new UiRect[items.Length]; PanelBounds = new UiRect[items.Length];
        Interactions = new UiButtonInteraction[items.Length];
        Suffixes = new string[items.Length]; HeaderIds = new string[items.Length]; PanelIds = new string[items.Length];
        for (var i = 0; i < items.Length; i++)
            Suffixes[i] = "##accordion-" + Convert.ToHexString(MemoryMarshal.AsBytes(items[i].Value.AsSpan()));
    }

    public string Label
    {
        get => _label;
        set { ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value); if (_label == value) return; ThrowIfBuilding(); _label = value; RequestRebuild(); }
    }
    public float Width
    {
        get => _width;
        set { ThrowIfDisposed(); ValidateWidth(value); if (_width == value) return; ThrowIfBuilding(); _width = value; RequestRebuild(); }
    }
    public UiAccordionOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed(); value.Validate();
            ValidateSelection(Value.Value, _items, value);
            if (_options == value) return;
            ThrowIfBuilding(); _options = value; RequestRebuild();
        }
    }
    public UiBinding<ImmutableArray<string>> Value => _value ?? throw new ObjectDisposedException(nameof(UiAccordion<TPresentation>));
    public ReadOnlySpan<UiAccordionItem> Items => _items;

    public void SetHeader(string value, string label, bool enabled = true, UiMenuIcon icon = default)
    {
        ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(value); ArgumentNullException.ThrowIfNull(label);
        ThrowIfBuilding();
        var index = Array.FindIndex(_items, item => item.Value == value);
        if (index < 0) throw new ArgumentException("Accordion header value is not declared : " + value, nameof(value));
        var next = _items[index] with { Label = label, Enabled = enabled, Icon = icon };
        if (_items[index] == next) return;
        _items[index] = next;
        RequestRebuild();
    }

    private static UiAccordionItem[] Prepare(string label, UiBinding<ImmutableArray<string>> value,
        ReadOnlyMemory<UiAccordionItem> items, float width, UiAccordionOptions options)
    {
        ArgumentNullException.ThrowIfNull(label); ArgumentNullException.ThrowIfNull(value);
        ValidateWidth(width); options.Validate();
        if (!float.IsFinite(TPresentation.PanelMargin) || TPresentation.PanelMargin < 0)
            throw new InvalidOperationException("Accordion panel margin must be finite and nonnegative.");
        var copy = items.ToArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in copy)
        {
            ArgumentException.ThrowIfNullOrEmpty(item.Value); ArgumentNullException.ThrowIfNull(item.Label);
            ArgumentNullException.ThrowIfNull(item.Content);
            if (!keys.Add(item.Value)) throw new ArgumentException("Accordion values must be unique.", nameof(items));
        }
        ValidateSelection(value.Value, copy, options);
        return copy;
    }
    private static UiWidget[] Contents(UiAccordionItem[] items)
    {
        var result = new UiWidget[items.Length];
        for (var i = 0; i < items.Length; i++) result[i] = items[i].Content;
        return result;
    }
    private static void ValidateWidth(float width)
    {
        if (!float.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
    }
    private static void ValidateSelection(ImmutableArray<string> values, ReadOnlySpan<UiAccordionItem> items, UiAccordionOptions options)
    {
        if (values.IsDefaultOrEmpty) return;
        if (!options.Multiple && values.Length > 1) throw new ArgumentException("Single-open accordion cannot contain multiple values.");
        for (var i = 0; i < values.Length; i++)
        {
            var known = false;
            foreach (var item in items) known |= item.Value == values[i];
            if (!known) throw new ArgumentException("Accordion value is not declared : " + values[i]);
            for (var j = 0; j < i; j++)
                if (values[i] == values[j]) throw new ArgumentException("Accordion selection contains duplicate values.");
        }
    }
    internal ImmutableArray<string> ReadSelection()
    {
        var value = Value.Value;
        ValidateSelection(value, _items, _options);
        return value.IsDefault ? ImmutableArray<string>.Empty : value;
    }
    internal bool SetExpanded(int index, bool expanded)
    {
        var values = ReadSelection();
        var key = _items[index].Value;
        var found = values.Contains(key, StringComparer.Ordinal);
        if (found == expanded) return true;
        if (!expanded && values.Length == 1 && !_options.Collapsible) return false;
        var next = expanded ? _options.Multiple ? values.Add(key).Sort(StringComparer.Ordinal) : ImmutableArray.Create(key)
            : values.Remove(key, StringComparer.Ordinal);
        Value.Value = next;
        RequestRebuild();
        return ReadSelection().Contains(key, StringComparer.Ordinal) == expanded;
    }
    internal UiVector2 PrepareLayout(UiControlContext context, ImmutableArray<string> open)
    {
        var constraints = context.LayoutConstraints;
        var width = Math.Clamp(_width, constraints.MinWidth, constraints.MaxWidth);
        double y = 0;
        for (var i = 0; i < _items.Length; i++)
        {
            var header = TPresentation.MeasureHeader(context, _items[i], width, _options);
            if (!float.IsFinite(header.Size.X) || !float.IsFinite(header.Size.Y) || header.Size.X < 0 || header.Size.Y < 0)
                throw new InvalidOperationException("Accordion presentation returned invalid header extents.");
            Headers[i] = header;
            HeaderBounds[i] = new(0, UiLayoutDistribution.Finite(y), width, header.Size.Y);
            y += header.Size.Y;
            PanelBounds[i] = default;
            if (open.Contains(_items[i].Value, StringComparer.Ordinal))
            {
                var margin = MathF.Min(width * .5f, TPresentation.PanelMargin);
                var body = context.MeasureConstrained(_items[i].Content, new(width - 2 * margin, width - 2 * margin, 0, float.PositiveInfinity));
                PanelBounds[i] = new(margin, UiLayoutDistribution.Finite(y), width - 2 * margin, body.Y);
                y += body.Y;
            }
        }
        return constraints.Constrain(new(width, UiLayoutDistribution.Finite(y)));
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => PrepareLayout(context, ReadSelection());
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderAccordion(this);
    private protected override void ReleaseOwnedResources()
    {
        _items = []; _value = null; _label = string.Empty;
        FocusedPanel = -1; FocusedPanelControl = null;
        Array.Clear(Headers); Array.Clear(Interactions); Array.Clear(HeaderIds); Array.Clear(PanelIds); Array.Clear(Suffixes);
        base.ReleaseOwnedResources();
    }
}

public class UiAccordion<TPresentation>(string label, UiBinding<ImmutableArray<string>> value, ReadOnlyMemory<UiAccordionItem> items,
    float width = 320, UiAccordionOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
    : UiAccordion<UiReleaseButtonBehavior, TPresentation>(label, value, items, width, options, identity)
    where TPresentation : IUiAccordionPresentation;

[UiWidgetStyle(typeof(UiAccordion<>), typeof(UiFluentAccordionPresentation))]
[UiComponent("Accordion", UiComponentExposure.Fluent)]
public partial class UiFluentAccordion;
