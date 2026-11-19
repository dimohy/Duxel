namespace Duxel.Core;

public readonly record struct UiMarkdownOptions(float Width = 0, float Height = 420, int MaxLength = 1_048_576,
    bool Wrap = true, bool ShowBorder = true, bool ReadOnly = false, Uri? BaseUri = null)
{
    public UiMarkdownOptions() : this(0, 420, 1_048_576, true, true, false) { }
    internal void Validate()
    {
        if (!float.IsFinite(Width) || Width < 0 || !float.IsFinite(Height) || Height <= 0 || MaxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "Markdown requires a finite nonnegative width, positive height and positive source limit.");
        if (BaseUri is { IsAbsoluteUri: false })
            throw new ArgumentException("Markdown BaseUri must be absolute.", nameof(BaseUri));
    }
}

/// <summary>Owns one source buffer and the shared styled layout, selection, undo and IME lifetime.</summary>
public abstract class UiMarkdownWidget : UiControlWidget, IUiLabelTarget
{
    private UiBinding<string>? _value;
    private readonly UiMultilineInputOwner _owner = new();
    private readonly UiMarkdownTextSource _source;
    private UiMarkdownOptions _options;
    private readonly bool _viewer;
    private Action<string>? _changed;
    private Action<Uri>? _navigate = UiLinkNavigation.OpenExternal;
    private readonly UiMarkdownLinkOwner _links = new();
    private Func<Uri, UiImageSource?>? _resolveImage;
    /// <summary>Resolves each distinct image URI once. Null means unavailable and displays its alternative text.</summary>
    public Func<Uri, UiImageSource?>? ResolveImage
    {
        get => _resolveImage;
        set
        {
            ThrowIfDisposed();
            ThrowIfBuilding();
            if (_resolveImage == value)
                return;
            _resolveImage = value;
            _source.ConfigureImages(value, _options.BaseUri);
            RequestRebuild();
        }
    }
    /// <summary>Retries image resolution after an application's image resources change.</summary>
    public void InvalidateImages()
    {
        ThrowIfDisposed();
        ThrowIfBuilding();
        _source.InvalidateImages();
        RequestRebuild();
    }
    /// <summary>Handles viewer link activation. Defaults to the system web/mail handler.</summary>
    public Action<Uri> Navigate
    {
        get => _navigate ?? throw new ObjectDisposedException(GetType().Name);
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            _navigate = value;
        }
    }
    public string Label
    {
        get;
    }
    public UiMarkdownDocument Document => _source.Document;
    public UiBinding<string> Value => _value ?? throw new ObjectDisposedException(GetType().Name);
    public Action<string>? Changed
    {
        get => _changed;
        set
        {
            ThrowIfDisposed();
            _changed = value;
        }
    }
    public UiMarkdownOptions Options
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
    private protected UiMarkdownWidget(string label, UiBinding<string> value, UiMarkdownOptions? options,
        bool viewer, UiWidgetIdentity identity) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(value);
        _options = options ?? new();
        _options.Validate();
        Label = label;
        _value = value;
        _viewer = viewer;
        var text = ValidateSource(value.Value, _options.MaxLength);
        _source = new(new(text));
    }
    internal static string ValidateSource(string value, int maximum)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), "Markdown source exceeds its configured limit.");
        return value;
    }
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) =>
        context.LayoutConstraints.Constrain(new(_options.Width > 0 ? _options.Width : context.AvailableWidth, _options.Height));
    protected override void RenderControl(UiImmediateContext ui)
    {
        var previous = Value.Value;
        _source.ConfigureImages(_resolveImage, _options.BaseUri);
        ui.RenderMarkdown(PrimaryInputId(Label), Label, Value, _owner, _source, _options, _viewer, _links, Navigate);
        var current = Value.Value;
        if (current != previous)
            Changed?.Invoke(current);
    }
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            _owner.Dispose();
        }
        finally
        {
            _source.Release();
            _value = null;
            _changed = null;
            _navigate = null;
            _resolveImage = null;
            _links.Clear();
            base.ReleaseOwnedResources();
        }
    }
}

/// <summary>Read-only styled Markdown output, with all source delimiters collapsed.</summary>
public sealed class UiMarkdownView(string label, UiBinding<string> value, UiMarkdownOptions? options = null,
    UiWidgetIdentity identity = UiWidgetIdentity.Instance) : UiMarkdownWidget(label, value, options, true, identity);

/// <summary>Live Markdown output with source delimiters revealed only in the active block.</summary>
public sealed class UiMarkdownEditor(string label, UiBinding<string> value, UiMarkdownOptions? options = null,
    UiWidgetIdentity identity = UiWidgetIdentity.Instance) : UiMarkdownWidget(label, value, options, false, identity);

public static partial class Dux
{
    public static UiMarkdownView MarkdownView(string label, UiBinding<string> value, UiMarkdownOptions? options = null) => new(label, value, options);
    public static UiMarkdownView MarkdownView(string markdown, UiMarkdownOptions? options = null) =>
        new("Markdown document", new(() => markdown, _ => throw new InvalidOperationException("A static Markdown view has no writable source binding.")), options);
    public static UiMarkdownEditor MarkdownEditor(string label, UiBinding<string> value, UiMarkdownOptions? options = null) => new(label, value, options);
}
