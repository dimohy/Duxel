using System;
using System.Collections.Generic;
using System.Linq;

namespace Dux.Core;

public sealed class UiSelectionBasicStorage
{
    private readonly SortedSet<string> _selected = new(StringComparer.Ordinal);
    private List<string>? _sortedCache;
    private bool _dirty = true;

    public bool Contains(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _selected.Contains(id);
    }

    public void Clear()
    {
        _selected.Clear();
        _sortedCache = null;
        _dirty = true;
    }

    public void Swap(UiSelectionBasicStorage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
        {
            return;
        }

        var temp = _selected.ToArray();
        _selected.Clear();
        _selected.UnionWith(other._selected);
        other._selected.Clear();
        other._selected.UnionWith(temp);

        (_sortedCache, other._sortedCache) = (other._sortedCache, _sortedCache);
        (_dirty, other._dirty) = (other._dirty, _dirty);
    }

    public void SetItemSelected(string id, bool selected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var changed = selected ? _selected.Add(id) : _selected.Remove(id);
        if (changed)
        {
            _dirty = true;
        }
    }

    public bool GetNextSelectedItem(ref UiSelectionIterator iterator, out string id)
    {
        EnsureCache();
        if (_sortedCache is null || iterator.Index >= _sortedCache.Count)
        {
            id = string.Empty;
            return false;
        }

        id = _sortedCache[iterator.Index++];
        return true;
    }

    public void ClearFreeMemory()
    {
        _selected.Clear();
        _sortedCache?.Clear();
        _sortedCache?.TrimExcess();
        _dirty = true;
    }

    private void EnsureCache()
    {
        if (!_dirty && _sortedCache is not null)
        {
            return;
        }

        _sortedCache = new List<string>(_selected);
        _dirty = false;
    }

    public struct UiSelectionIterator
    {
        internal int Index;
    }
}
