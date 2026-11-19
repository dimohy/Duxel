namespace Duxel.Core;

public enum UiToolbarSize
{
    Medium, Small, Large
}
public readonly record struct UiToolbarOptions(UiToolbarSize Size = UiToolbarSize.Medium, bool Vertical = false, bool RightToLeft = false)
{
    internal UiToolbarOptions WithInheritedDirection(bool rightToLeft) =>
        rightToLeft && !RightToLeft ? this with { RightToLeft = true } : this;

    internal void Validate()
    {
        if ((uint)Size > 2)
            throw new ArgumentOutOfRangeException(nameof(Size));
    }
}
public interface IUiToolbarPresentation
{
    static abstract UiEdgeInsets Padding(UiToolbarOptions options);
}
public readonly struct UiFluentToolbarPresentation : IUiToolbarPresentation
{
    public static UiEdgeInsets Padding(UiToolbarOptions options) => options.Vertical ? new(8, 4, 8, 4) : options.Size switch
    {
        UiToolbarSize.Small => new(4, 0, 4, 0),
        UiToolbarSize.Large => new(20, 4, 20, 4),
        _ => new(8, 4, 8, 4)
    };
}
internal sealed class UiToolbarInputState
{
    internal readonly record struct Member(string Id, bool ConsumesArrows);
    internal readonly List<Member> Members = [];
    internal readonly List<UiKeyEvent> FilteredKeys = [];
    internal string? Remembered;
    internal string? Root;
    private WeakReference<UiImmediateContext>? _context;
    internal void Bind(UiImmediateContext context, string root)
    {
        if (_context is null || !_context.TryGetTarget(out var previous) || !ReferenceEquals(previous, context) || Root != root)
        {
            Members.Clear();
            Remembered = null;
            Root = root;
            _context = new(context);
        }
    }
    internal int IndexOf(string? id)
    {
        for (var i = 0; i < Members.Count; i++)
        {
            if (Members[i].Id == id)
                return i;
        }
        return -1;
    }
    internal void Clear()
    {
        Members.Clear();
        FilteredKeys.Clear();
        Remembered = null;
        Root = null;
        _context = null;
    }
}

/// <summary>Owns ordinary controls, one roving tab entry and circular arrow navigation.</summary>
public class UiToolbar<TPresentation> : UiArrangedChildrenWidget where TPresentation : IUiToolbarPresentation
{
    private UiToolbarOptions _options;
    private readonly UiToolbarInputState _input = new();
    public string Label
    {
        get;
    }
    public UiToolbarOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiToolbar(string label, UiWidget[] children, UiToolbarOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(Validate(children, label, options), identity)
    {
        Label = label;
        _options = options;
    }
    private static UiWidget[] Validate(UiWidget[] children, string label, UiToolbarOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        options.Validate();
        return children;
    }
    protected override bool CanRetainArrangement => true;
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        var options = _options.WithInheritedDirection(context.RightToLeft);
        var padding = TPresentation.Padding(options);
        if (!float.IsFinite(padding.Left) || !float.IsFinite(padding.Right) || !float.IsFinite(padding.Top) || !float.IsFinite(padding.Bottom)
            || padding.Left < 0 || padding.Right < 0 || padding.Top < 0 || padding.Bottom < 0)
            throw new InvalidOperationException("Toolbar presentation must provide finite nonnegative padding.");
        var constraints = context.LayoutConstraints.Validate();
        var width = MathF.Max(0, constraints.MaxWidth - padding.Left - padding.Right);
        var height = MathF.Max(0, constraints.MaxHeight - padding.Top - padding.Bottom);
        var main = 0f;
        var cross = 0f;
        for (var i = 0; i < Children.Length; i++)
        {
            var child = context.MeasureParentDataChild(Children[i], null, new(0, width, 0, height));
            bounds[i] = new(0, 0, child.X, child.Y);
            main = UiLayoutDistribution.Finite((double)main + (_options.Vertical ? child.Y : child.X));
            cross = MathF.Max(cross, _options.Vertical ? child.X : child.Y);
        }
        var size = constraints.Constrain(new((_options.Vertical ? cross : main) + padding.Left + padding.Right,
            (_options.Vertical ? main : cross) + padding.Top + padding.Bottom));
        var contentWidth = MathF.Max(0, size.X - padding.Left - padding.Right);
        var contentHeight = MathF.Max(0, size.Y - padding.Top - padding.Bottom);
        var position = 0f;
        for (var i = 0; i < bounds.Length; i++)
        {
            var child = bounds[i];
            var x = _options.Vertical ? MathF.Max(0, (contentWidth - child.Width) / 2) : position;
            if (options.RightToLeft && !options.Vertical)
                x = contentWidth - position - child.Width;
            bounds[i] = child with
            {
                X = padding.Left + x,
                Y = padding.Top + (_options.Vertical ? position : MathF.Max(0, (contentHeight - child.Height) / 2))
            };
            position += _options.Vertical ? child.Height : child.Width;
        }
        return size;
    }
    protected override void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
    {
        var scope = ui.BeginToolbar(Label, root, _input, _options.WithInheritedDirection(ui.RightToLeft));
        try
        {
            base.RenderArrangedChildren(ui, layout, root);
        }
        finally { ui.EndToolbar(scope, _input); }
    }
    private protected override void ReleaseOwnedResources()
    {
        _input.Clear();
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiToolbar<>), typeof(UiFluentToolbarPresentation))]
[UiComponent("Toolbar", UiComponentExposure.Fluent)]
public partial class UiFluentToolbar;
