namespace Duxel.Core;

internal readonly record struct UiTreeEntry(string Value, string? ParentValue, string Label);

/// <summary>Validated immutable flat input, indexed once without recursive traversal.</summary>
internal sealed class UiTreeHierarchy
{
    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
    internal readonly UiTreeEntry[] Entries;
    internal readonly int[] Parent, Depth, PositionInSet, SizeOfSet, Preorder, PreorderIndex, SubtreeEnd, LeafCount;
    internal readonly int[][] Children;
    internal readonly int[] Roots;
    internal int Count => Entries.Length;
    internal UiTreeHierarchy(UiTreeEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries.ToArray();
        var count = entries.Length;
        Parent = new int[count]; Depth = new int[count]; PositionInSet = new int[count]; SizeOfSet = new int[count];
        Preorder = new int[count]; PreorderIndex = new int[count]; SubtreeEnd = new int[count]; LeafCount = new int[count]; Children = new int[count][];
        for (var i = 0; i < count; i++)
        {
            var entry = Entries[i];
            if (string.IsNullOrWhiteSpace(entry.Value) || string.IsNullOrWhiteSpace(entry.Label)
                || UiAccessibilityTree.ContainsControl(entry.Value, false) || UiAccessibilityTree.ContainsControl(entry.Label, false)
                || !_indices.TryAdd(entry.Value, i))
                throw new ArgumentException("Tree entries require unique nonempty keys and single-line labels.", nameof(entries));
        }
        var children = new List<int>[count];
        for (var i = 0; i < count; i++) children[i] = [];
        var roots = new List<int>();
        for (var i = 0; i < count; i++)
        {
            if (Entries[i].ParentValue is not { } parent) { Parent[i] = -1; roots.Add(i); }
            else
            {
                if (!_indices.TryGetValue(parent, out var parentIndex) || parentIndex == i)
                    throw new ArgumentException("A tree parent must identify another existing entry.", nameof(entries));
                Parent[i] = parentIndex;
                children[parentIndex].Add(i);
            }
        }
        Roots = roots.ToArray();
        for (var i = 0; i < count; i++) Children[i] = children[i].ToArray();
        var stack = new Stack<(int Index, int Depth)>();
        void Push(int[] siblings, int depth)
        {
            for (var i = siblings.Length - 1; i >= 0; i--)
            {
                PositionInSet[siblings[i]] = i + 1;
                SizeOfSet[siblings[i]] = siblings.Length;
                stack.Push((siblings[i], depth));
            }
        }
        Push(Roots, 1);
        var cursor = 0;
        while (stack.TryPop(out var item))
        {
            Preorder[cursor] = item.Index;
            PreorderIndex[item.Index] = cursor++;
            Depth[item.Index] = item.Depth;
            Push(Children[item.Index], item.Depth + 1);
        }
        if (cursor != count) throw new ArgumentException("Tree parent links must not contain cycles.", nameof(entries));
        var ancestors = new Stack<int>();
        for (var order = 0; order < count; order++)
        {
            var index = Preorder[order];
            while (ancestors.TryPeek(out var previous) && Depth[previous] >= Depth[index])
                SubtreeEnd[ancestors.Pop()] = order;
            ancestors.Push(index);
        }
        while (ancestors.TryPop(out var index)) SubtreeEnd[index] = count;
        for (var order = count - 1; order >= 0; order--)
        {
            var index = Preorder[order];
            if (!IsBranch(index)) LeafCount[index] = 1;
            if (Parent[index] is var parent && parent >= 0) LeafCount[parent] += LeafCount[index];
        }
    }
    internal int Index(string value) => _indices.TryGetValue(value, out var index) ? index
        : throw new ArgumentException("The tree does not contain this item value.", nameof(value));
    internal bool IsBranch(int index) => Children[index].Length != 0;
}
