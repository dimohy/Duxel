namespace Duxel.Core;

public readonly record struct UiCarouselSlide(string Key, string Label, UiWidget Content, UiImageSource? Preview = null);

public sealed record UiCarouselOptions
{
    public bool Wrap { get; init; }
    public bool RightToLeft { get; init; }
    public bool ReducedMotion { get; init; }
    public TimeSpan? AutoPlayInterval { get; init; }
    public string PreviousLabel { get; init; } = "Previous slide";
    public string NextLabel { get; init; } = "Next slide";
    public string AutoPlayLabel { get; init; } = "Auto-play";
}

/// <summary>Owns each slide. A complete set of previews changes the step navigation to image previews.</summary>
public class UiCarouselWidget<TButtonBehavior, TButtonPresentation> : UiControlWidget
    where TButtonBehavior : IUiButtonBehavior
    where TButtonPresentation : IUiButtonPresentation
{
    private SlideBody _body;
    private UiWidget _layout;
    private readonly UiCarouselOptions _options;
    private bool _playing;
    private int _lastFrame;
    private string? _timedSlide;
    private string? _requestedSlide;
    private double? _deadline;

    public UiCarouselWidget(string label, UiBinding<string> selected, ReadOnlyMemory<UiCarouselSlide> slides,
        UiCarouselOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(selected);
        _options = options ?? new();
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.PreviousLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.NextLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.AutoPlayLabel);
        if (_options.AutoPlayInterval is { } interval && interval < TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(options), "Auto-play requires an interval of at least one second.");
        var items = slides.ToArray();
        _body = new(items, selected, out var tabs);
        Value = selected;
        Label = label;
        var navigation = Fluent.TabList(label, new(() => Value.Value, SelectStep), tabs,
            new(RightToLeft: _options.RightToLeft, Appearance: UiTabListAppearance.SubtleCircular)
            {
                ReducedMotion = _options.ReducedMotion, RequireSelection = true, Scroll = new UiTabListScrollOptions()
            });
        var previous = new DirectionButton(true, _options.RightToLeft, _options.PreviousLabel,
            () => QueueMove(-1), () => CanMove(-1));
        var next = new DirectionButton(false, _options.RightToLeft, _options.NextLabel,
            () => QueueMove(1), () => CanMove(1));
        var wrap = new UiWrapOptions(Spacing : 6, RunSpacing : 6) { RightToLeft = _options.RightToLeft };
        UiWidget controls = _options.AutoPlayInterval is null
            ? Dux.Wrap(wrap, previous, next)
            : Dux.Wrap(wrap,
                Fluent.Switch(_options.AutoPlayLabel, new UiBinding<bool>(() => IsPlaying, value => IsPlaying = value)),
                previous, next);
        _layout = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min), controls, _body, navigation);
        OwnChild("layout", _layout);
    }

    public string Label { get; }
    public UiBinding<string> Value { get; }
    public bool IsPlaying
    {
        get => _playing;
        set
        {
            ThrowIfDisposed();
            if (value && _options.AutoPlayInterval is null)
                throw new InvalidOperationException("Set an autoplay interval before starting playback.");
            if (_playing == value)
                return;
            _playing = value;
            _deadline = null;
            RequestRebuild();
        }
    }

    private bool CanMove(int direction) => _body.Count > 1
        && (_options.Wrap || (uint)(_body.Index + direction) < (uint)_body.Count);

    private void QueueMove(int direction)
    {
        if (CanMove(direction))
            _requestedSlide = _body.KeyAt((_body.Index + direction + _body.Count) % _body.Count);
    }

    private void Select(string key)
    {
        if (Value.Value == key)
            return;
        Value.Value = key;
        if (Value.Value != key)
            return;
        _deadline = null;
        _body.RequestRebuild();
    }

    private void SelectStep(string? key)
    {
        if (key is null)
            return;
        Select(key);
        // TabList paints the requested step before committing the controlled value.
        if (Value.Value != key && RenderingContext is { } ui)
            ui.RequestFrameAt(ui.GetTime());
    }

    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var constraints = context.LayoutConstraints;
        var width = Math.Clamp(context.AvailableWidth, constraints.MinWidth, constraints.MaxWidth);
        if (width is > 0 and <= 600)
            constraints = constraints with { MinWidth = width, MaxWidth = width };
        return context.MeasureConstrained(_layout, constraints);
    }

    protected override void RenderControl(UiImmediateContext ui)
    {
        var frame = ui.GetFrameCount();
        if (_lastFrame != frame && _lastFrame != frame - 1)
            _deadline = null;
        _lastFrame = frame;
        var size = MeasureContent(new(ui));
        var (visible, hovered, focused) = ui.RenderCarouselLayout(PrimaryInputId(Label), Label, _layout, size);
        if (_requestedSlide is { } requested)
        {
            _requestedSlide = null;
            Select(requested);
        }
        if (_options.AutoPlayInterval is not { } interval || !IsPlaying || !visible
            || hovered || focused || !CanMove(1) || !ui.IsControlEnabled)
        {
            _deadline = null;
            return;
        }
        var now = ui.GetTime();
        if (_deadline is null || _timedSlide != Value.Value)
        {
            _timedSlide = Value.Value;
            _deadline = now + interval.TotalSeconds;
        }
        if (now >= _deadline.Value)
        {
            var old = Value.Value;
            Select(_body.KeyAt((_body.Index + 1) % _body.Count));
            if (old != Value.Value)
                ui.RequestFrameAt(now);
            _timedSlide = Value.Value;
            _deadline = now + interval.TotalSeconds;
        }
        if (CanMove(1))
            ui.RequestFrameAt(_deadline.Value);
    }

    private protected override void ReleaseOwnedResources()
    {
        _layout = null!;
        _body = null!;
        base.ReleaseOwnedResources();
    }

    private sealed class DirectionButton : UiButton<TButtonBehavior, TButtonPresentation>
    {
        private readonly Func<bool> _enabled;
        private readonly bool _previous, _rightToLeft;

        internal DirectionButton(bool previous, bool rightToLeft, string label, Action activate, Func<bool> enabled)
            : base(label, activate)
        {
            AccessibleName = label;
            _enabled = enabled;
            _previous = previous;
            _rightToLeft = rightToLeft;
        }

        private string Glyph(bool inherited) => _previous != (_rightToLeft || inherited) ? "‹" : "›";
        protected override UiVector2 MeasureContent(UiControlContext context) =>
            context.MeasureButton<TButtonPresentation>(Glyph(context.RightToLeft), Size);

        private protected override bool RenderButton(UiImmediateContext ui, string id)
        {
            ui.BeginDisabled(!_enabled());
            try
            {
                return ui.Button<TButtonBehavior, TButtonPresentation>(id, Glyph(ui.RightToLeft), Size,
                    constrainLayout : true);
            }
            finally { ui.EndDisabled(); }
        }
    }

    private sealed class SlideBody : UiControlWidget
    {
        private UiCarouselSlide[] _items;
        private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
        private readonly UiBinding<string> _selected;

        internal SlideBody(UiCarouselSlide[] items, UiBinding<string> selected, out UiTabOption[] tabs)
        {
            if (items.Length == 0)
                throw new ArgumentException("A carousel requires at least one slide.", nameof(items));
            _items = items;
            _selected = selected;
            var contents = new HashSet<UiWidget>(ReferenceEqualityComparer.Instance);
            var hasPreviews = items[0].Preview is not null;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Key);
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Label);
                ArgumentNullException.ThrowIfNull(item.Content);
                if (!_indices.TryAdd(item.Key, i) || !contents.Add(item.Content))
                    throw new ArgumentException("Slides require unique keys and content widgets.", nameof(items));
                if (hasPreviews != (item.Preview is not null))
                    throw new ArgumentException("Supply previews for all slides or none.", nameof(items));
                ValidateOwnedChild("slide:" + i, item.Content);
            }
            _ = Index;
            tabs = new UiTabOption[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                OwnChild("slide:" + i, item.Content);
                UiTabIcon icon = default;
                if (item.Preview is { } source)
                {
                    var image = new UiImage(source);
                    OwnChild("preview:" + i, image);
                    icon = new((context, bounds, _, _, _) => context.PaintCarouselPreview(image, bounds));
                }
                tabs[i] = new(item.Key, item.Label, Icon: icon, IconOnly: hasPreviews);
            }
        }

        internal int Index => _indices.TryGetValue(_selected.Value, out var index) ? index
            : throw new InvalidOperationException("Carousel selection must identify one of its slides.");
        internal int Count => _items.Length;
        internal string KeyAt(int index) => _items[index].Key;
        protected override UiVector2 MeasureContent(UiControlContext context) =>
            context.MeasureConstrained(_items[Index].Content, context.LayoutConstraints);

        protected override void RenderControl(UiImmediateContext ui)
        {
            var context = new UiControlContext(ui);
            var slide = _items[Index];
            var size = context.MeasureConstrained(slide.Content, context.LayoutConstraints);
            var bounds = new UiRect(0, 0, size.X, size.Y);
            ui.RenderContainer<UiPlainContainerPresentation>(slide.Content, new(size, bounds, bounds));
        }

        private protected override void ReleaseOwnedResources()
        {
            _items = [];
            _indices.Clear();
            base.ReleaseOwnedResources();
        }
    }
}

/// <summary>Carousel with Fluent previous and next buttons that activate on release.</summary>
public class UiCarouselWidget : UiCarouselWidget<UiReleaseButtonBehavior, UiFluentButtonPresentation>
{
    public UiCarouselWidget(string label, UiBinding<string> selected, ReadOnlyMemory<UiCarouselSlide> slides,
        UiCarouselOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(label, selected, slides, options, identity) { }
}

[UiWidgetStyle(typeof(UiCarouselWidget))]
[UiComponent("Carousel", UiComponentExposure.Fluent)]
public partial class UiFluentCarousel;

public readonly partial struct UiControlContext
{
    internal void PaintCarouselPreview(UiImage image, UiRect bounds) =>
        _ui.PaintImage(image, bounds, new(255, 255, 255), 0, UiImageFit.Contain);
}

public sealed partial class UiImmediateContext
{
    internal (bool Visible, bool Hovered, bool Focused) RenderCarouselLayout(string id, string label, UiWidget layout, UiVector2 size)
    {
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var visible = IntersectRect(ResolveItemClipRect(), bounds);
        var root = ResolveId(id);
        var parent = _accessibilityParent;
        ExposeAccessibility(root, parent, UiAccessibilityRole.Group, label, bounds, focusable: false, valuePattern: false, literalLabel: true);
        _accessibilityParent = root;
        bool focused;
        try { focused = RenderAllocatedChild(_builder, layout, bounds, bounds, constrainLayout: true); }
        finally { _accessibilityParent = parent; }
        FinishLayoutBox(origin, size);
        return (visible.Width > 0 && visible.Height > 0,
            _mousePosition.X >= visible.X && _mousePosition.X < visible.X + visible.Width
            && _mousePosition.Y >= visible.Y && _mousePosition.Y < visible.Y + visible.Height, focused);
    }
}
