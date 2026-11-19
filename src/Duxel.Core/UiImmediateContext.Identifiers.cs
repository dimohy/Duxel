using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const int MaxResolvedIdentifiers = 4096;
    private const int MaxResolvedIdentifierCharacters = 1_048_576;
    private struct ResolvedIdentifier(string value, int frame, int characters)
    {
        internal readonly string Value = value;
        internal int LastFrame = frame;
        internal readonly int Characters = characters;
    }
    private readonly Dictionary<(string Prefix, string Id, bool Separator), ResolvedIdentifier> _resolvedIdentifiers = [];
    private readonly List<(string Prefix, string Id, bool Separator)> _expiredIdentifiers = [];
    private int _resolvedIdentifierCharacters;
    private UiWidget? _identityWidget;

    internal UiWidget? PushWidgetIdentity(UiWidget widget,string scope)
    {
        var previous=_identityWidget;
        _idStack.Push(_idPrefix);
        _idPrefix=widget.ResolveIdentityScope(_idPrefix,scope);
        _identityWidget=widget;
        return previous;
    }

    internal void PopWidgetIdentity(UiWidget? previous)
    {
        _identityWidget=previous;
        PopID();
    }

    private string ResolveCachedIdentifier(string prefix, string id, bool separator)
    {
        var key = (prefix, id, separator);
        ref var cached = ref CollectionsMarshal.GetValueRefOrNullRef(_resolvedIdentifiers, key);
        if (!Unsafe.IsNullRef(ref cached))
        {
            cached.LastFrame = _state.FrameCount;
            return cached.Value;
        }
        var value = separator ? string.Concat(prefix, "/", id) : string.Concat(prefix, id);
        // Charge input references as well as the result; admission is bounded
        // even for applications that generate long or continuously changing IDs.
        var characters = 2L * (prefix.Length + (long)id.Length) + (separator ? 1 : 0);
        if (_resolvedIdentifiers.Count < MaxResolvedIdentifiers
            && characters <= MaxResolvedIdentifierCharacters - _resolvedIdentifierCharacters)
        {
            _resolvedIdentifiers.Add(key, new(value, _state.FrameCount, (int)characters));
            _resolvedIdentifierCharacters += (int)characters;
        }
        return value;
    }

    private string PopupStateKey(string resolvedId) => ResolveCachedIdentifier(resolvedId, "##popup", false);

    private void PruneResolvedIdentifiers()
    {
        if (_state.FrameCount % 60 != 0 || _resolvedIdentifiers.Count == 0) return;
        foreach (var pair in _resolvedIdentifiers)
            if (unchecked((uint)(_state.FrameCount - pair.Value.LastFrame)) > 180)
                _expiredIdentifiers.Add(pair.Key);
        foreach (var key in _expiredIdentifiers)
        {
            _resolvedIdentifierCharacters -= _resolvedIdentifiers[key].Characters;
            _resolvedIdentifiers.Remove(key);
        }
        _expiredIdentifiers.Clear();
    }

    internal (int Entries, int Characters) ResolvedIdentifierCacheState => (_resolvedIdentifiers.Count, _resolvedIdentifierCharacters);

    internal void ReleaseIdentifierResources()
    {
        _resolvedIdentifiers.Clear();
        _resolvedIdentifiers.TrimExcess();
        _expiredIdentifiers.Clear();
        _expiredIdentifiers.TrimExcess();
        _resolvedIdentifierCharacters = 0;
    }
}
