namespace Duxel.Core;

public abstract partial class UiWidget
{
    internal static UiVector2 MeasureOwnedChild(UiWidget owner, UiWidget child, UiControlContext context,
        UiBoxConstraints constraints, ref UiRetainedLayout<UiVector2>? retained, bool render)
    {
        var consent = owner.CanRetainOwnedMeasurement(child);
        if (!consent || !context.CanRetainLayoutMeasurement)
        {
            retained = null;
            return context.MeasureConstrained(child, constraints);
        }
        var cache = retained ??= new();
        if (cache.TryGet(context, constraints, render, out var previous,
            allowMeasurementReuse: true, allowPreparedArrangement: false))
            return previous;
        var attempt = cache.Begin(context, constraints);
        var size = context.MeasureConstrained(child, constraints);
        cache.Commit(context, attempt, size, size, owner.CanRetainOwnedMeasurement(child) && context.CanRetainLayoutMeasurement);
        return size;
    }
    private bool _measuring;
    /// <summary>Measures desired size without rendering or processing input. Available size is a constraint, not a forced size.</summary>
    public UiVector2 Measure(UiImmediateContext ui, UiVector2 available)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ThrowIfDisposed();
        if (!float.IsFinite(available.X) || !float.IsFinite(available.Y) || available.X < 0 || available.Y < 0)
            throw new ArgumentOutOfRangeException(nameof(available));
        return MeasureScoped(ui, available, null);
    }
    /// <summary>Measures under explicit minimum/maximum box constraints; unbounded maxima are allowed.</summary>
    public UiVector2 MeasureConstrained(UiImmediateContext ui, UiBoxConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ThrowIfDisposed();
        constraints.Validate();
        return constraints.Constrain(MeasureScoped(ui, new(constraints.MaxWidth, constraints.MaxHeight), constraints));
    }
    private UiVector2 MeasureScoped(UiImmediateContext ui, UiVector2 available, UiBoxConstraints? constraints)
    {
        if (_measuring || _rendering)
            throw new InvalidOperationException("A widget cannot recursively measure itself or measure during its render.");
        _measuring = true;
        try
        {
            var scope = IdentityScope;
            var previous = scope is not null ? ui.PushWidgetIdentity(this, scope) : null;
            try
            {
                return ui.MeasureWidget(this, available, constraints);
            }
            finally { if (scope is not null) ui.PopWidgetIdentity(previous); }
        }
        finally { _measuring = false; }
    }
    protected virtual UiVector2 MeasureContent(UiControlContext context)
    {
        var tree = PrepareBuiltContent();
        if (tree is not UiWidget widget)
            throw new NotSupportedException("Constrained composition measurement requires a UiWidget root; legacy IUiView delegates must provide explicit measurement.");
        return context.MeasureConstrained(widget, context.HasBoxConstraints ? context.LayoutConstraints : UiBoxConstraints.Loose(context.AvailableSize));
    }
    internal UiVector2 MeasureCore(UiControlContext context)
    {
        var presentation = (_identity as IdentityCache)?.Presentation;
        return presentation is null || presentation.Margin == default && presentation.Transform.IsIdentity
            ? MeasureContent(context) : MeasureWithMargin(context);
    }
}

public readonly partial struct UiControlContext
{
    public UiVector2 AvailableSize => _ui.GetContentRegionAvail();
    public UiVector2 Measure(UiWidget widget, UiVector2 available) => widget.Measure(_ui, available);
    public UiVector2 MeasureConstrained(UiWidget widget, UiBoxConstraints constraints) => widget.MeasureConstrained(_ui, constraints);
    public UiBoxConstraints LayoutConstraints => _ui.CurrentLayoutConstraints;
    internal bool HasBoxConstraints => _ui.HasBoxConstraints;
    internal UiVector2 MeasureButton<TPresentation>(string label, UiVector2 requested) where TPresentation : IUiButtonPresentation => _ui.MeasureButton<TPresentation>(label, requested);
    public UiVector2 MeasureToggle<TPresentation>(string label) where TPresentation : IUiTogglePresentation => _ui.MeasureToggle<TPresentation>(label);
    public UiVector2 MeasureSlider<TPresentation>(UiSliderOptions options, UiVector2 size) where TPresentation : IUiSliderPresentation => _ui.MeasureSlider<TPresentation>(options, size);
    internal UiVector2 MeasureInput<TPresentation>(string label, float width, UiInputOptions options, UiInputChildren? children) where TPresentation : IUiInputPresentation => _ui.MeasureInput<TPresentation>(label, width, options, children);
    public UiVector2 MeasureInput<TPresentation>(string label, float width = 0, UiInputOptions options = default) where TPresentation : IUiInputPresentation => _ui.MeasureInput<TPresentation>(label, width, options, null);
    public UiVector2 MeasureRadioGroup<TPresentation>(string label, float width, UiRadioGroupContent<TPresentation> content) where TPresentation : IUiRadioGroupPresentation => _ui.MeasureRadioGroup(label, width, content);
}

public sealed partial class UiImmediateContext
{
    private int _measurementDepth;
    private UiBoxConstraints? _measureConstraints;
    internal bool HasBoxConstraints => _measureConstraints.HasValue;
    internal bool IsMeasuring => _measurementDepth != 0;
    private void EnsureNotMeasuring()
    {
        if (IsMeasuring)
            throw new InvalidOperationException("Measurement cannot render or process input.");
    }
    internal UiVector2 MeasureWidget(UiWidget widget, UiVector2 available, UiBoxConstraints? constraints = null)
        => MeasureWidgetCore(widget, available, constraints, false, out _);
    internal UiVector2 MeasureWidgetContent(UiWidget widget, UiVector2 available, UiBoxConstraints? constraints, bool transformed = false)
    {
        if (transformed)
            _widgetVectorMeasurementDepth++;
        try
        {
            return MeasureWidgetCore(widget, available, constraints, false, out _, contentOnly: true);
        }
        finally { if (transformed) _widgetVectorMeasurementDepth--; }
    }
    internal UiWidget? ResolveComposedLayoutRoot(UiWidget widget)
    {
        var constraints = CurrentLayoutConstraints;
        MeasureWidgetCore(widget, new(constraints.MaxWidth, constraints.MaxHeight), constraints, true, out var data);
        return data;
    }
    private UiVector2 MeasureWidgetCore(UiWidget widget, UiVector2 available, UiBoxConstraints? constraints, bool parentDataOnly, out UiWidget? parentData, bool contentOnly = false)
    {
        parentData = null;
        var previousConstraints = _measureConstraints;
        _measureConstraints = constraints;
        var previousBounds = _inputSlotBounds;
        var previousTier = _inputSlotBoundsTier;
        var depth = _layouts.Count;
        var next = (_nextItemWidth, _nextItemVerticalAlign, _hasNextItemVerticalAlign);
        _inputSlotBounds = new(0, 0, available.X, available.Y);
        _inputSlotBoundsTier = _popupTierDepth;
        _layouts.Push(new(default, false, 0, 0));
        _measurementDepth++;
        var completed = false;
        try
        {
            if (parentDataOnly)
            {
                parentData = widget.ResolveBuiltLayoutRoot(this);
                completed = true;
                return default;
            }
            var size = contentOnly ? widget.MeasureContentCore(new(this)) : widget.MeasureCore(new(this));
            if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
                throw new InvalidOperationException("Widget measurement returned invalid extents.");
            completed = true;
            return size;
        }
        finally
        {
            var balanced = _layouts.Count == depth + 1;
            while (_layouts.Count > depth)
                _layouts.Pop();
            _measurementDepth--;
            _inputSlotBounds = previousBounds;
            _inputSlotBoundsTier = previousTier;
            _measureConstraints = previousConstraints;
            (_nextItemWidth, _nextItemVerticalAlign, _hasNextItemVerticalAlign) = next;
            if (completed && !balanced)
                throw new InvalidOperationException("Widget measurement left an unbalanced layout scope.");
        }
    }
    private (UiVector2 Text, UiVector2 Size) MeasureButtonLocal<TPresentation>(UiControlContext context, string text, UiVector2 requested, float lineHeight)
        where TPresentation : IUiButtonPresentation
    {
        var textSize = MeasureTextInternal(text, _textSettings, _lineHeight);
        if (lineHeight > 0)
            textSize = textSize with
            {
                Y = lineHeight
            };
        return (textSize, TPresentation.Measure(context, textSize, requested));
    }
    internal UiVector2 MeasureButton<TPresentation>(string label, UiVector2 requested) where TPresentation : IUiButtonPresentation
    {
        var text = GetDisplayLabel(label);
        var context = new UiControlContext(this, text.Length > 0);
        using var typography = TPresentation.BeginText(context, text.Length > 0);
        return MeasureButtonLocal<TPresentation>(context, text, requested, typography.LineHeight).Size;
    }
    private (UiVector2 Text, UiToggleLayout Layout) MeasureToggleLocal<TPresentation>(UiControlContext context, string text, float lineHeight, bool constrainLayout = false) where TPresentation : IUiTogglePresentation
    {
        var textSize = MeasureTextInternal(text, _textSettings, _lineHeight);
        if (lineHeight > 0)
            textSize = textSize with
            {
                Y = lineHeight
            };
        var layout = TPresentation.Measure(context, textSize, text.Length > 0);
        if (constrainLayout)
        {
            var size = ConstrainToLayout(layout.Size);
            if (size != layout.Size)
                layout = TPresentation.Arrange(context, layout, size);
            if (layout.Size != size)
                throw new InvalidOperationException("Toggle presentation did not honor the parent allocation.");
        }
        return (textSize, layout);
    }
    internal UiVector2 MeasureToggle<TPresentation>(string label) where TPresentation : IUiTogglePresentation
    {
        var text = GetDisplayLabel(label);
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context, text.Length > 0);
        return MeasureToggleLocal<TPresentation>(context, text, typography.LineHeight, true).Layout.Size;
    }
    private UiSliderLayout MeasureSliderLocal<TPresentation>(UiControlContext context, UiSliderOptions options, UiVector2 requested, bool constrainLayout) where TPresentation : IUiSliderPresentation
    {
        var layout = TPresentation.Measure(context, options, requested);
        if (constrainLayout)
        {
            var size = ConstrainToLayout(layout.Size);
            if (size != layout.Size)
                layout = TPresentation.Arrange(context, layout, options, size);
            if (layout.Size != size)
                throw new InvalidOperationException("Slider presentation did not honor the parent allocation.");
        }
        if (!float.IsFinite(layout.Track.Width) || !float.IsFinite(layout.Track.Height) || layout.Track.Width < 0 || layout.Track.Height < 0)
            throw new InvalidOperationException("Slider presentation returned invalid track extents.");
        return layout;
    }
    internal UiVector2 MeasureSlider<TPresentation>(UiSliderOptions options, UiVector2 size) where TPresentation : IUiSliderPresentation
        => MeasureSliderLocal<TPresentation>(new(this), options, size, true).Size;
    private UiInputLayout MeasureInputLocal<TPresentation>(UiControlContext context, string label, float width, UiInputOptions options, UiInputChildren? children, bool constrainLayout = false)
        where TPresentation : IUiInputPresentation
    {
        var layout = TPresentation.Measure(context, label, width);
        if (children?.Before is { } before)
            layout = layout with
            {
                BeforeChild = true,
                Before = new(0, 0, before.Size.X, before.Size.Y)
            };
        if (children?.After is { } after)
            layout = layout with
            {
                AfterChild = true,
                After = new(0, 0, after.Size.X, after.Size.Y)
            };
        layout = TPresentation.MeasureContent(context, layout, options);
        if (constrainLayout)
        {
            var size = ConstrainToLayout(layout.Size);
            if (size != layout.Size)
                layout = TPresentation.Arrange(context, layout, options, size);
            if (layout.Size != size)
                throw new InvalidOperationException("Input presentation did not honor the parent allocation.");
        }
        layout.Validate();
        return layout;
    }
    internal UiVector2 MeasureInput<TPresentation>(string label, float width, UiInputOptions options, UiInputChildren? children) where TPresentation : IUiInputPresentation
    {
        options.Validate();
        if (_currentField is { Invalid: true })
            options = options with
            {
                Invalid = true
            };
        if ((children?.Before.HasValue == true || children?.After.HasValue == true) && !TPresentation.SupportsChildren)
            throw new NotSupportedException("This input presentation does not support child slots.");
        if (children?.Before.HasValue == true && options.ContentBefore is not null || children?.After.HasValue == true && options.ContentAfter is not null)
            throw new InvalidOperationException("A slot cannot contain both text data and an owned child.");
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context);
        return MeasureInputLocal<TPresentation>(context, GetDisplayLabel(label), width, options, children, true).Size;
    }
    internal UiVector2 MeasureRadioGroup<TPresentation>(string label, float width, UiRadioGroupContent<TPresentation> content) where TPresentation : IUiRadioGroupPresentation
    {
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context);
        content.Measure(this, context, GetDisplayLabel(label), ConstrainLayoutWidth(width > 0 ? width : MathF.Max(1, GetContentRegionAvail().X)), CurrentControlTextLayoutKey, CanRetainControlTextLayout);
        try
        {
            return content.ResolveLayout(context, true).Size;
        }
        finally { content.EndFrame(); }
    }
}
