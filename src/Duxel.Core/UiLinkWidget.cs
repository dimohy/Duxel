namespace Duxel.Core;

public readonly record struct UiLinkLayout(UiVector2 Size, UiControlTextBlock Text);
public interface IUiLinkPresentation
{
    static virtual bool ReuseLayout => false;
    /// <summary>Arranges a local copy; retained natural content remains owned by UiLinkContent.</summary>
    static virtual UiLinkLayout Arrange(UiControlContext context, in UiLinkLayout natural, UiVector2 allocatedSize) =>
        throw new NotSupportedException("This Link presentation must implement Arrange to accept a changed parent allocation.");
    static abstract UiControlTypographyScope BeginText(UiControlContext context);
    static abstract UiLinkLayout Measure(UiControlContext context, string text, float width);
    static abstract void Paint(UiControlContext context, in UiLinkLayout layout, UiRect bounds, UiButtonInteraction interaction);
}

/// <summary>Owned layout storage for one logical link, usable by immediate and inherited widgets.</summary>
public sealed class UiLinkContent<TPresentation> : IDisposable where TPresentation : IUiLinkPresentation
{
    private readonly WeakReference<UiImmediateContext> _context = new(null!);
    private string? _text;
    private UiControlTextLayoutKey _key;
    private float _width;
    private int _derivedCharacters;
    private bool _valid, _disposed;
    internal UiLinkLayout Layout;
    internal int LayoutBuildCount
    {
        get;
        private set;
    }
    internal int RetainedCharacters => _valid ? (_text?.Length ?? 0) + _derivedCharacters : 0;
    internal void Measure(UiImmediateContext ui, UiControlContext context, string text, float width, in UiControlTextLayoutKey key, bool cacheable)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (cacheable && TPresentation.ReuseLayout && _valid && _text == text && _width == width && _key == key && _context.TryGetTarget(out var previous) && ReferenceEquals(previous, ui))
            return;
        Clear();
        try
        {
            Layout = TPresentation.Measure(context, text, width);
            LayoutBuildCount++;
            if (!float.IsFinite(Layout.Size.X) || !float.IsFinite(Layout.Size.Y) || Layout.Size.X < 0 || Layout.Size.Y < 0)
                throw new InvalidOperationException("Link presentation returned invalid extents.");
            long derived = 0;
            if (Layout.Text.RetainedLineCount <= 256)
                for (var i = 0; i < Layout.Text.LineCount && derived <= 4096; i++)
                {
                    var line = Layout.Text.GetLine(i);
                    if (!ReferenceEquals(line.Text, text))
                        derived += line.Text.Length;
                }
            _valid = TPresentation.ReuseLayout && cacheable && text.Length <= 1024 && derived <= 4096 && Layout.Text.RetainedLineCount <= 256;
            _derivedCharacters = _valid ? (int)derived : 0;
            if (_valid)
            {
                _text = text;
                _width = width;
                _key = key;
                _context.SetTarget(ui);
            }
        }
        catch
        {
            Clear();
            throw;
        }
    }
    internal void EndFrame()
    {
        if (!_valid)
            Clear();
    }
    private void Clear()
    {
        _valid = false;
        _derivedCharacters = 0;
        _text = null;
        _key = default;
        Layout = default;
        _context.SetTarget(null!);
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
    }
}

/// <summary>Navigation destination and callback are owned independently of input and visual policies.</summary>
public class UiLink<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiButtonBehavior where TPresentation : IUiLinkPresentation
{
    private string _label;
    private Uri? _destination;
    private Action<Uri>? _navigate;
    private float _width;
    private readonly UiLinkContent<TPresentation> _content = new();
    public UiLink(string label, Uri destination, Action<Uri> navigate, float width = 0, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(navigate);
        ValidateWidth(width);
        _label = label;
        _destination = destination;
        _navigate = navigate;
        _width = width;
    }
    private static void ValidateWidth(float width)
    {
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
    }
    public string Label
    {
        get => _label;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_label == value)
                return;
            _label = value;
            RequestRebuild();
        }
    }
    public Uri Destination
    {
        get => _destination ?? throw new ObjectDisposedException(nameof(UiLink<TBehavior, TPresentation>));
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_destination == value)
                return;
            _destination = value;
            RequestRebuild();
        }
    }
    public Action<Uri> Navigate
    {
        get => _navigate ?? throw new ObjectDisposedException(nameof(UiLink<TBehavior, TPresentation>));
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            _navigate = value;
        }
    }
    public float Width
    {
        get => _width;
        set
        {
            ThrowIfDisposed();
            ValidateWidth(value);
            if (_width == value)
                return;
            _width = value;
            RequestRebuild();
        }
    }
    protected virtual void OnNavigate(Uri destination) => Navigate(destination);
    protected override bool CanRetainMeasurement => TPresentation.ReuseLayout;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureLink(Label, _content, Width);
    protected override void RenderControl(UiImmediateContext ui)
    {
        if (ui.Link<TBehavior, TPresentation>(PrimaryInputId(Label), Label, _content, Width, constrainLayout : true))
            OnNavigate(Destination);
    }
    private protected override void ReleaseOwnedResources()
    {
        _content.Dispose();
        _destination = null;
        _navigate = null;
        _label = string.Empty;
        base.ReleaseOwnedResources();
    }
}

public readonly struct UiLinkNavigationBehavior : IUiButtonBehavior
{
    public static UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds) => context.ActivateEnterOnRelease(id, bounds);
}

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior), typeof(UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>))]
[UiComponent("Link", UiComponentExposure.Fluent)]
public partial class UiFluentLink;
