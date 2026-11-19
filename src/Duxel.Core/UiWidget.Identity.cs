namespace Duxel.Core;

public abstract partial class UiWidget
{
    // A scope token becomes owned cache storage only on the first scoped render.
    // Unowned Parent factories need neither a token nor cache allocation.
    private object? _identity;
    private string? IdentityScope => _identity switch
    {
        IdentityCache cache => cache.Scope,
        string scope => scope,
        _ => null,
    };
    internal int IdentityCacheCharacters => (_identity as IdentityCache)?.Characters ?? 0;

    // Passive content needs a stable identity even when input identity belongs to its parent.
    internal static string NewPassiveAccessibilityIdentity() => "\u001fcontent" +
        Interlocked.Increment(ref _nextInstance).ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal string ResolveIdentityScope(string parentPrefix, string scope)
    {
        if (_identity is not IdentityCache cache)
        {
            cache = new(scope);
            _identity = cache;
        }
        cache.Scope ??= scope;
        return cache.ResolveScope(parentPrefix);
    }

    internal string? ResolvePrimaryInputId(string prefix) =>
        (_identity as IdentityCache)?.ResolvePrimary(prefix);

    internal string ResolvePrimaryControlIdentity(string parentPrefix)
    {
        var scope = IdentityScope ?? throw new InvalidOperationException("A virtual control requires scoped identity.");
        var prefix = ResolveIdentityScope(parentPrefix, scope);
        return ResolvePrimaryInputId(prefix) ?? string.Concat(prefix, "/", UiControlWidget.PrimaryControlId);
    }

    private sealed class IdentityCache(string? scope)
    {
        internal TextLayoutMemo? TextLayout;
        internal string? Scope = scope;
        internal string? AccessibleName;
        internal PresentationState? Presentation;
        private string? _parentPrefix;
        private string? _resolvedScope;
        private string? _primary;
        internal int Characters => (_parentPrefix?.Length ?? 0) + (_resolvedScope?.Length ?? 0) + (_primary?.Length ?? 0);

        internal string ResolveScope(string parentPrefix)
        {
            var scope = Scope ?? throw new InvalidOperationException("Identity scope must be assigned before resolution.");
            if (_resolvedScope is not null && string.Equals(_parentPrefix, parentPrefix, StringComparison.Ordinal))
                return _resolvedScope;
            var resolved = parentPrefix.Length == 0 ? scope : string.Concat(parentPrefix, "/", scope);
            _parentPrefix = null;
            _resolvedScope = null;
            _primary = null;
            // Charge retained inputs as well as both computed strings.
            if (parentPrefix.Length + 2L * resolved.Length + 1 + UiControlWidget.PrimaryControlId.Length <= 4096)
            {
                _parentPrefix = parentPrefix;
                _resolvedScope = resolved;
            }
            return resolved;
        }

        internal string? ResolvePrimary(string prefix) =>
            string.Equals(prefix, _resolvedScope, StringComparison.Ordinal)
                ? _primary ??= string.Concat(prefix, "/", UiControlWidget.PrimaryControlId) : null;
    }
}
