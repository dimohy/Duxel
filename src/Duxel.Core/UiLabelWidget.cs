using System.Runtime.CompilerServices;

namespace Duxel.Core;

public enum UiLabelSize { Medium, Small, Large }

/// <summary>A direct control that accepts external labeling and focus on its first enabled focusable part.</summary>
public interface IUiLabelTarget
{
    /// <summary>A compound input can delegate its standalone label to one owned focusable control.</summary>
    UiWidget? LabelControl => null;
}

public readonly record struct UiLabelOptions(bool Required = false, bool Disabled = false,
    UiLabelSize Size = UiLabelSize.Medium, bool Semibold = false)
{
    public UiLabelOptions Validate()
    {
        if ((uint)Size > (uint)UiLabelSize.Large) throw new ArgumentOutOfRangeException(nameof(Size));
        return this;
    }
}

internal readonly record struct UiLabelLayout(UiControlTextBlock Text, UiRect Required, UiVector2 Size);

/// <summary>A wrapped label borrowing one independently rendered, instance-identified control.</summary>
public class UiLabel : UiControlWidget
{
    private static readonly ConditionalWeakTable<UiWidget, WeakReference<UiLabel>> Associations = new();
    private UiWidget? _target;
    private string _text;
    private UiLabelOptions _options;
    private UiRetainedLayout<UiLabelLayout>? _retained;

    public UiLabel(string text, UiWidget? target = null, UiLabelOptions options = default,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _options = options.Validate();
        SetTarget(target);
    }

    public string Text
    {
        get => _text;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_text == value) return;
            ThrowIfBuilding();
            _text = value;
            RequestRebuild();
            _target?.RefreshAssociatedLabel();
        }
    }

    public UiLabelOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value) return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
            _target?.RefreshAssociatedLabel();
        }
    }

    public UiWidget? Target
    {
        get => _target;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_target, value)) return;
            ThrowIfBuilding();
            SetTarget(value);
            RequestRebuild();
        }
    }

    private void SetTarget(UiWidget? target)
    {
        if (target is IUiLabelTarget { LabelControl: not null })
        {
            var seen = new HashSet<UiWidget>(ReferenceEqualityComparer.Instance);
            while (target is IUiLabelTarget { LabelControl: { } control })
            {
                if (!seen.Add(target)) throw new InvalidOperationException("Compound label targets must not form a cycle.");
                target = control;
            }
        }
        target?.ValidateLabelTarget();
        if (ReferenceEquals(target, this)) throw new ArgumentException("A label cannot target itself.", nameof(target));
        if (target is not null && Find(target) is { } existing && !ReferenceEquals(existing, this))
            throw new InvalidOperationException("A control can have only one standalone label.");
        if (_target is not null)
        {
            Associations.Remove(_target);
            _target.RefreshAssociatedLabel();
        }
        _target = target;
        if (target is not null)
        {
            Associations.Remove(target);
            Associations.Add(target, new(this));
            target.RefreshAssociatedLabel();
        }
    }

    internal static UiLabel? Find(UiWidget? target) => target is not null && Associations.TryGetValue(target, out var weak)
        && weak.TryGetTarget(out var label) ? label : null;

    protected override bool CanRetainMeasurement => true;
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();
    protected override UiVector2 MeasureContent(UiControlContext context) => Layout(context, false).Size;
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderLabel(this);

    internal UiLabelLayout Layout(UiControlContext context, bool render)
    {
        var retain = context.CanRetainLayoutMeasurement && _text.Length <= 1024;
        var cache = _retained ??= new();
        // The result contains glyph text, so invalidated prepared geometry cannot stand in for current content.
        if (cache.TryGet(context, context.LayoutConstraints, render, out var previous,
            allowMeasurementReuse : retain, allowPreparedArrangement : false)) return previous;
        var attempt = cache.Begin(context);
        var layout = UiImmediateContext.MeasureLabel(context, _text, _options);
        if (retain && layout.Text.LineCount <= 256 && layout.Text.RetainedCharacters <= 4096)
            cache.Commit(context, attempt, layout, layout.Size);
        else cache.Clear();
        return layout;
    }

    private protected override void ReleaseOwnedResources()
    {
        if (_target is not null)
        {
            Associations.Remove(_target);
            _target.RefreshAssociatedLabel();
        }
        _target = null;
        _retained = null;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiLabel))]
[UiComponent("Label", UiComponentExposure.Fluent)]
public partial class UiFluentLabel;

public abstract partial class UiWidget
{
    internal void RefreshAssociatedLabel()
    {
        if (!_disposed) RequestRebuild();
    }

    internal void ValidateLabelTarget()
    {
        ThrowIfDisposed();
        if (this is not IUiLabelTarget)
            throw new ArgumentException("A label target must implement IUiLabelTarget.");
        if (!HasScopedIdentity) throw new InvalidOperationException("Standalone labels require an instance-identified target.");
    }
}
