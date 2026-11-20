namespace Duxel.Core;

internal interface IUiBuildState
{
    ulong Revision
    {
        get;
    }
    void Subscribe(IUiBuildObserver observer);
    void Unsubscribe(IUiBuildObserver observer);
}
internal interface IUiBuildObserver
{
    void Invalidate();
}

/// <summary>Instance identity retains object state; parent identity preserves the caller's label/key scope.</summary>
public enum UiWidgetIdentity
{
    Instance, Parent
}

/// <summary>Builds a retained declarative tree when watched state or explicit invalidation changes.</summary>
public abstract partial class UiWidget : IUiView, IDisposable
{
    public string? AccessibleName
    {
        get => (_identity as IdentityCache)?.AccessibleName;
        set
        {
            ThrowIfDisposed();
            if (AccessibleName == value)
                return;
            ThrowIfBuilding();
            var cache = _identity as IdentityCache ?? new(_identity as string);
            cache.AccessibleName = value;
            _identity = cache;
            RequestRebuild();
        }
    }
    private static long _nextInstance;
    [ThreadStatic]
    private static UiImmediateContext? _renderingContext;
    internal static UiImmediateContext? RenderingContext => _renderingContext;
    // A direct control needs only its frame callback. A composed/owning widget
    // stores that callback in its existing Build context instead of carrying
    // two references on every widget instance.
    private object? _buildOrFrameRequest;
    private IUiView? _tree;
    private bool _invalidated = true;
    private bool _disposed;
    private bool _rendering;
    private UiBuildContext? _ownerContext;
    private UiBuildContext? BuildContext => _buildOrFrameRequest as UiBuildContext;
    private UiBuildContext EnsureBuildContext()
    {
        if (_buildOrFrameRequest is UiBuildContext context)
            return context;
        context = new(this, _buildOrFrameRequest as Action);
        _buildOrFrameRequest = context;
        return context;
    }
    internal void RememberFrameRequest(Action? callback)
    {
        if (_buildOrFrameRequest is UiBuildContext context)
            context.FrameRequest = callback;
        else
            _buildOrFrameRequest = callback;
    }
    internal void RequestFrame()
    {
        if (_buildOrFrameRequest is UiBuildContext context)
            context.FrameRequest?.Invoke();
        else
            (_buildOrFrameRequest as Action)?.Invoke();
    }

    protected UiWidget(UiWidgetIdentity identity = UiWidgetIdentity.Instance)
    {
        if (identity == UiWidgetIdentity.Instance)
            _identity = "\u001fwidget:" + Interlocked.Increment(ref _nextInstance)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        else if (identity != UiWidgetIdentity.Parent)
            throw new ArgumentOutOfRangeException(nameof(identity));
    }

    protected abstract IUiView Build(UiBuildContext context);
    protected bool HasScopedIdentity => IdentityScope is not null;
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    protected void ThrowIfBuilding()
    {
        if (BuildContext?.IsBuilding == true)
            throw new InvalidOperationException("Build must not invalidate itself.");
    }

    /// <summary>Opts into measurement depending only on box constraints, configured typography and properties invalidated with RequestRebuild.</summary>
    protected virtual bool CanRetainMeasurement => false;
    /// <summary>Supports explicit measurement delegation to a live child owned by this widget. Additional measurement inputs must invalidate with RequestRebuild.</summary>
    protected bool CanRetainOwnedMeasurement(UiWidget child)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        child.ThrowIfDisposed();
        if (!ReferenceEquals(child._ownerContext?.Owner, this))
            throw new InvalidOperationException("Measurement delegation requires a child owned by this widget.");
        return child.RetainsMeasurement;
    }
    internal bool RetainsMeasurement => CanRetainMeasurement;
    internal virtual void InvalidateRetainedMeasurement()
    {
    }
    internal void InvalidateMeasurementChain()
    {
        for (UiWidget? current = this; current is not null && !current._disposed; current = current._ownerContext?.Owner)
            current.InvalidateRetainedMeasurement();
    }

    public void RequestRebuild()
    {
        ThrowIfDisposed();
        ThrowIfBuilding();
        _invalidated = true;
        InvalidateMeasurementChain();
        RequestFrame();
    }

    internal void InvalidateState()
    {
        if (_disposed)
            return;
        _invalidated = true;
        InvalidateMeasurementChain();
        RequestFrame();
    }

    public void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ui.IsMeasuring)
            throw new InvalidOperationException("Measurement cannot render a widget.");
        if (_rendering)
            throw new InvalidOperationException("A widget tree cannot recursively render itself.");
        RememberFrameRequest(ui.FrameRequestCallback);
        var previousRenderingContext = _renderingContext;
        _renderingContext = ui;
        _rendering = true;
        try
        {
            // Input and view bindings still execute every frame, independently of Build reuse.
            var scope = IdentityScope;
            var previousWidget = scope is not null ? ui.PushWidgetIdentity(this, scope) : null;
            var previousAccessibility = ui.SwapAccessibilityWidget(this);
            try
            {
                // Batched inline text keeps real child identities and its prepared-frame geometry.
                if (!ui.TryRenderInlineChild(this))
                {
                    var presentation = (_identity as IdentityCache)?.Presentation;
                    if (presentation is null)
                        RenderContent(ui);
                    else if (!presentation.Transform.IsIdentity)
                        ui.RenderTransformedWidget(this, presentation.Margin, presentation.Transform);
                    else if (presentation.Margin == default)
                        RenderContent(ui);
                    else
                        ui.RenderWidgetMargin(this, presentation.Margin);
                }
            }
            finally { ui.SwapAccessibilityWidget(previousAccessibility); if (scope is not null) ui.PopWidgetIdentity(previousWidget); }
        }
        finally { _rendering = false; _renderingContext = previousRenderingContext; }
    }

    protected virtual void RenderContent(UiImmediateContext ui) => RenderBuiltContent(ui);

    protected void RenderBuiltContent(UiImmediateContext ui)
        => PrepareBuiltContent().Render(ui);

    private IUiView PrepareBuiltContent()
    {
        var context = EnsureBuildContext();
        if (_invalidated || _tree is null || context.HasChanged)
        {
            _invalidated = false;
            context.BeginBuild();
            try
            {
                var tree = Build(context) ?? throw new InvalidOperationException("Build must return a view.");
                context.EndBuild();
                _tree = tree;
            }
            catch (Exception failure)
            {
                _invalidated = true;
                try
                {
                    context.AbortBuild();
                }
                catch (Exception cleanup) { throw new AggregateException("Build and cleanup failed.", failure, cleanup); }
                throw;
            }
        }
        return _tree;
    }

    internal void ValidateOwner(UiBuildContext owner, string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (UiWidget? ancestor = owner.Owner; ancestor is not null; ancestor = ancestor._ownerContext?.Owner)
            if (ReferenceEquals(ancestor, this))
                throw new InvalidOperationException("A widget cannot own an ancestor.");
        if (_ownerContext is not null && (!ReferenceEquals(_ownerContext, owner) || !owner.Owns(key, this)))
            throw new InvalidOperationException("A stateful widget instance cannot belong to multiple child keys or parents.");
    }

    internal void Attach(UiBuildContext owner, string key)
    {
        ValidateOwner(owner, key);
        _ownerContext = owner;
        // Instance scopes stay unique; Parent scopes reuse the existing key string.
        // The child map remains the authority for ownership validation.
        if (_identity is IdentityCache cache)
            cache.Scope ??= key;
        else
            _identity ??= key;
    }

    protected void ValidateOwnedChild(string key, UiWidget child) => EnsureBuildContext().ValidateAdoption(key, child);
    protected void OwnChild(string key, UiWidget child) => EnsureBuildContext().Adopt(key, child);
    protected void ReleaseOwnedChild(string key, UiWidget child) => EnsureBuildContext().ReleaseOwned(key, child);

    protected virtual void OnDispose()
    {
    }

    internal void ValidateRetirement()
    {
        if (_rendering)
            throw new InvalidOperationException("A rendering child cannot be retired.");
    }
    private protected virtual void ReleaseOwnedResources()
    {
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_rendering)
            throw new InvalidOperationException("A widget cannot be disposed while rendering.");
        InvalidateMeasurementChain();
        _disposed = true;
        _tree = null;
        List<Exception>? errors = null;
        try
        {
            BuildContext?.Release();
        }
        catch (Exception error) { (errors ??= []).Add(error); }
        try
        {
            OnDispose();
        }
        catch (Exception error) { (errors ??= []).Add(error); }
        try
        {
            ReleaseOwnedResources();
        }
        catch (Exception error) { (errors ??= []).Add(error); }
        _ownerContext = null;
        _buildOrFrameRequest = null;
        _identity = null;
        GC.SuppressFinalize(this);
        if (errors is not null)
            throw new AggregateException("Widget disposal failed.", errors);
    }
}

/// <summary>Records build dependencies and owns keyed child instances across rebuilds.</summary>
public sealed class UiBuildContext : IUiBuildObserver
{
    // Scratch lists belong to their existing stores, avoiding extra wrapper objects
    // and unused references on ownership-only or dependency-free contexts.
    private sealed class BuildDependencies : Dictionary<IUiBuildState, (ulong Revision, ulong Generation)>
    {
        internal List<IUiBuildState>? Added, Removed;
        internal BuildDependencies() : base(ReferenceEqualityComparer.Instance) { }
    }
    private sealed class BuildChildren : Dictionary<string, (UiWidget View, ulong Generation, ulong CreatedGeneration)>
    {
        internal List<string>? Removed;
        internal BuildChildren() : base(StringComparer.Ordinal) { }
    }
    private BuildDependencies? _dependencies;
    private BuildChildren? _children;
    private ulong _generation;
    private readonly UiWidget _owner;
    internal bool IsBuilding
    {
        get; private set;
    }
    internal bool HasChanged
    {
        get
        {
            if (_dependencies is { } dependencies)
                foreach (var dependency in dependencies)
                    if (dependency.Key.Revision != dependency.Value.Revision)
                        return true;
            return false;
        }
    }

    internal Action? FrameRequest;
    internal UiBuildContext(UiWidget owner, Action? frameRequest)
    {
        _owner = owner;
        FrameRequest = frameRequest;
    }
    internal UiWidget Owner => _owner;
    void IUiBuildObserver.Invalidate() => _owner.InvalidateState();

    public T Watch<T>(UiState<T> state)
    {
        RequireBuild();
        ArgumentNullException.ThrowIfNull(state);
        var source = (IUiBuildState)state;
        var dependencies = _dependencies ??= new();
        ref var dependency = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(dependencies, source, out var exists);
        if (!exists)
        {
            source.Subscribe(this);
            (dependencies.Added ??= []).Add(source);
        }
        dependency = (source.Revision, _generation);
        return state.Value;
    }

    public TView Child<TView>(string key, Func<TView> create) where TView : UiWidget
    {
        RequireBuild();
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(create);
        var children = _children ??= new();
        if (children.TryGetValue(key, out var entry))
        {
            if (entry.Generation == _generation)
                throw new InvalidOperationException($"Duplicate child key '{key}'.");
            if (entry.View is not TView typed)
                throw new InvalidOperationException($"Child key '{key}' must retain the same view type.");
            children[key] = (typed, _generation, entry.CreatedGeneration);
            return typed;
        }
        var child = create() ?? throw new InvalidOperationException("A child factory must return a view.");
        if (ReferenceEquals(child, _owner))
            throw new InvalidOperationException("A widget cannot own itself.");
        child.Attach(this, key);
        children.Add(key, (child, _generation, _generation));
        _owner.InvalidateMeasurementChain();
        return child;
    }

    internal bool Owns(string key, UiWidget child)
        => _children is { } children && children.TryGetValue(key, out var entry) && ReferenceEquals(entry.View, child);
    internal void ValidateAdoption(string key, UiWidget child)
    {
        if (IsBuilding)
            throw new InvalidOperationException("Owned children cannot change during Build.");
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(child);
        if (ReferenceEquals(child, _owner))
            throw new InvalidOperationException("A widget cannot own itself.");
        child.ValidateOwner(this, key);
        if (_children is { } children && children.TryGetValue(key, out var current) && !ReferenceEquals(current.View, child))
            throw new InvalidOperationException("An owned child key cannot replace its widget instance.");
    }
    internal void Adopt(string key, UiWidget child)
    {
        ValidateAdoption(key, child);
        var children = _children ??= new();
        if (children.ContainsKey(key))
            return;
        child.Attach(this, key);
        children.Add(key, (child, _generation, _generation));
        _owner.InvalidateMeasurementChain();
    }

    internal void ReleaseOwned(string key, UiWidget child)
    {
        if (IsBuilding || !Owns(key, child))
            throw new InvalidOperationException("Only a directly owned child can be retired outside Build.");
        child.ValidateRetirement();
        _children!.Remove(key);
        _owner.InvalidateMeasurementChain();
        child.Dispose();
    }

    internal void BeginBuild()
    {
        if (IsBuilding)
            throw new InvalidOperationException("Recursive Build is not supported.");
        IsBuilding = true;
        unchecked
        {
            _generation++;
        }
        _dependencies?.Added?.Clear();
        _dependencies?.Removed?.Clear();
    }

    internal void EndBuild()
    {
        IsBuilding = false;
        if (_dependencies is { } dependencies)
        {
            foreach (var dependency in dependencies)
                if (dependency.Value.Generation != _generation)
                    (dependencies.Removed ??= []).Add(dependency.Key);
            if (dependencies.Removed is { } removed)
            {
                foreach (var source in removed)
                {
                    source.Unsubscribe(this);
                    dependencies.Remove(source);
                }
                removed.Clear();
            }
        }
        List<Exception>? errors = null;
        if (_children is { } children)
        {
            foreach (var pair in children)
                if (pair.Value.Generation != _generation)
                    (children.Removed ??= []).Add(pair.Key);
            if (children.Removed is { } removed)
            {
                foreach (var key in removed)
                {
                    var child = children[key].View;
                    children.Remove(key);
                    try
                    {
                        child.Dispose();
                    }
                    catch (Exception error) { (errors ??= []).Add(error); }
                }
                removed.Clear();
            }
        }
        if (errors is not null)
            throw new AggregateException("Child removal failed.", errors);
        _dependencies?.Added?.Clear();
    }

    internal void AbortBuild()
    {
        IsBuilding = false;
        if (_dependencies?.Added is { } added)
        {
            foreach (var source in added)
            {
                source.Unsubscribe(this);
                _dependencies!.Remove(source);
            }
            added.Clear();
        }
        _dependencies?.Removed?.Clear();
        _children?.Removed?.Clear();
        List<Exception>? errors = null;
        if (_children is { } children)
        {
            foreach (var child in children)
                if (child.Value.CreatedGeneration == _generation)
                    (children.Removed ??= []).Add(child.Key);
            if (children.Removed is { } removed)
            {
                foreach (var key in removed)
                {
                    var child = children[key].View;
                    children.Remove(key);
                    try
                    {
                        child.Dispose();
                    }
                    catch (Exception error) { (errors ??= []).Add(error); }
                }
                removed.Clear();
            }
        }
        if (errors is not null)
            throw new AggregateException("Failed Build child cleanup failed.", errors);
    }

    internal void Release()
    {
        List<Exception>? errors = null;
        if (_dependencies is { } dependencies)
            foreach (var source in dependencies.Keys)
                source.Unsubscribe(this);
        if (_children is { } children)
        {
            foreach (var child in children.Values)
            {
                try
                {
                    child.View.Dispose();
                }
                catch (Exception error) { (errors ??= []).Add(error); }
            }
        }
        _children = null;
        _dependencies = null;
        FrameRequest = null;
        if (errors is not null)
            throw new AggregateException("Child disposal failed.", errors);
    }

    private void RequireBuild()
    {
        if (!IsBuilding)
            throw new InvalidOperationException("Build context can only be used during Build.");
    }
}
