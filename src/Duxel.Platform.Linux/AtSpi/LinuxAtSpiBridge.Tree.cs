using Duxel.Core;
using static Duxel.Platform.Linux.GioNative;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private sealed record Node(string Path, UiAccessibilityElement? Element, string[] Children, string[] Interfaces, uint Role,
        string ParentPath = "", int IndexInParent = -1)
    {
        internal UiAccessibilitySelectionSnapshot? Selection { get; init; }
        internal AtSpiTable? Table { get; init; }
        internal string? TablePath { get; init; }
        internal LinuxTextBoundaries? TextBoundaries
        {
            get; set;
        }
    }
    private Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);
    private AtSpiWindowGeometry _window = new(0, 0, 0, 0, false, false);
    private static string NodePath(long runtimeId) => RootPath + "/n" + runtimeId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private GVariant ObjectReference(string path) => GVariant.Tuple(GVariant.String(BusName), GVariant.Path(path));
    private static GVariant ExternalReference(Reference reference) => GVariant.Tuple(GVariant.String(reference.Bus), GVariant.Path(reference.Path));

    private void Refresh()
    {
        var snapshot = _tree.CaptureSnapshot();
        var elements = snapshot.ElementsById;
        var tables = AtSpiTable.Capture(snapshot);
        string[] Children(string? first)
        {
            List<string> result = [];
            for (var id = first; id is not null;)
            {
                if (!elements.TryGetValue(id, out var child) || result.Count >= elements.Count)
                    throw new InvalidOperationException("The published accessibility sibling chain is inconsistent.");
                result.Add(NodePath(child.RuntimeId));
                id = child.Next;
            }
            return result.ToArray();
        }
        Dictionary<string, Node> next = new(StringComparer.Ordinal)
        {
            [RootPath] = new(RootPath, null, [WindowPath], [Accessible, Application], 75),
            [WindowPath] = new(WindowPath, null, Children(snapshot.First), [Accessible, Component], 23)
        };
        foreach (var element in snapshot.Elements)
        {
            var path = NodePath(element.RuntimeId);
            List<string> interfaces = [Accessible, Component];
            tables.TryGetValue(element.Node.Id, out var table);
            string? tablePath = null;
            if (table is not null)
                interfaces.Add(TableInterface);
            if (element.Node.GridItem is { } area && element.Node.Role != UiAccessibilityRole.Row)
            {
                tablePath = NodePath(elements[area.GridId].RuntimeId);
                interfaces.Add(TableCellInterface);
            }
            if (element.Node.ImageInfo.HasValue)
                interfaces.Add(ImageInterface);
            if (Actions(element.Node).Length != 0)
                interfaces.Add(ActionInterface);
            if (element.Node.Range.HasValue)
                interfaces.Add(ValueInterface);
            if (HasSelection(element.Node))
                interfaces.Add(SelectionInterface);
            if (element.Node.TextInfo is not null)
                interfaces.Add(TextInterface);
            if (element.Node.SupportsValue && element.Node.TextInfo?.CanEditText == true)
                interfaces.Add(EditableTextInterface);
            UiAccessibilitySelectionSnapshot? selection = null;
            if (HasSelection(element.Node)
                && snapshot.CaptureSelection(element.Node.Id, element.RuntimeId, out selection) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("The published selection container is inconsistent.");
            var node = new Node(path, element, Children(element.FirstChild), interfaces.ToArray(), Role(element.Node.Role))
            { Selection = selection, Table = table, TablePath = tablePath };
            if (_nodes.TryGetValue(path, out var old) && ReferenceEquals(old.TextBoundaries?.Content, element.Node.TextInfo?.Content))
                node.TextBoundaries = old.TextBoundaries;
            next.Add(path, node);
        }
        foreach (var parent in next.Values.ToArray())
            for (var index = 0; index < parent.Children.Length; index++)
            {
                var child = next[parent.Children[index]];
                if (child.ParentPath.Length != 0)
                    throw new InvalidOperationException("An accessibility child cannot belong to multiple parents.");
                next[child.Path] = child with
                {
                    ParentPath = parent.Path,
                    IndexInParent = index
                };
            }
        var previous = _nodes;
        var previousWindow = _window;
        _nodes = next;
        _window = _geometry();
        foreach (var node in next.Values)
        {
            if (_exports.TryGetValue(node.Path, out var registrations))
            {
                if (previous[node.Path].Interfaces.AsSpan().SequenceEqual(node.Interfaces))
                    continue;
                foreach (var registration in registrations)
                    g_dbus_connection_unregister_object(_connection, registration);
                _exports.Remove(node.Path);
            }
            Register(node.Path, node.Interfaces);
        }
        if (!_exports.ContainsKey(CachePath))
            Register(CachePath, [Cache]);
        foreach (var node in previous.Values)
        {
            if (next.ContainsKey(node.Path))
                continue;
            using var signal = GVariant.Tuple(ObjectReference(node.Path));
            Emit(CachePath, Cache, "RemoveAccessible", signal);
            foreach (var registration in _exports[node.Path])
                g_dbus_connection_unregister_object(_connection, registration);
            _exports.Remove(node.Path);
        }
        foreach (var node in next.Values)
        {
            if (!previous.TryGetValue(node.Path, out var old))
            {
                using var signal = GVariant.Tuple(CacheItem(node));
                Emit(CachePath, Cache, "AddAccessible", signal);
                continue;
            }
            if (!old.Interfaces.AsSpan().SequenceEqual(node.Interfaces))
            {
                using var signal = GVariant.Tuple(CacheItem(node));
                Emit(CachePath, Cache, "AddAccessible", signal);
            }
            if (Name(old) != Name(node))
                Event(node.Path, "PropertyChange", "accessible-name", 0, GVariant.String(Name(node)));
            if (Description(old) != Description(node))
                Event(node.Path, "PropertyChange", "accessible-description", 0, GVariant.String(Description(node)));
            PublishRangeChanges(old, node);
            PublishTextChanges(old, node);
            PublishTextAttributeChanges(old, node);
            var changedStates = State(old, previousWindow) ^ State(node, _window);
            for (var bit = 0; bit < StateNames.Length; bit++)
                if ((changedStates & (1UL << bit)) != 0)
                    Event(node.Path, "StateChanged", StateNames[bit], (State(node, _window) & (1UL << bit)) != 0 ? 1 : 0, GVariant.Int(0));
            for (var i = old.Children.Length - 1; i >= 0; i--)
                if (i >= node.Children.Length || old.Children[i] != node.Children[i])
                    Event(node.Path, "ChildrenChanged", "remove", i, ObjectReference(old.Children[i]));
            for (var i = 0; i < node.Children.Length; i++)
                if (i >= old.Children.Length || old.Children[i] != node.Children[i])
                    Event(node.Path, "ChildrenChanged", "add", i, ObjectReference(node.Children[i]));
            if (Bounds(old, previousWindow) != Bounds(node, _window))
                Event(node.Path, "BoundsChanged", "", 0, Rectangle(Bounds(node, _window)));
        }
        PublishSelectionChanges(previous, next);
        PublishAnnouncements();
    }

    private void Register(string path, string[] interfaces)
    {
        List<uint> registrations = [];
        _exports.Add(path, []);
        foreach (var name in interfaces)
        {
            var info = g_dbus_node_info_lookup_interface(_info, name);
            if (info == 0)
                throw new InvalidOperationException("Missing AT-SPI interface: " + name);
            var table = CallbackTable();
            nint error = 0;
            var id = g_dbus_connection_register_object(_connection, path, info, &table, System.Runtime.InteropServices.GCHandle.ToIntPtr(_self), 0, &error);
            CheckError(ref error, "register " + name);
            if (id == 0)
                throw new IOException("GDBus returned an empty registration.");
            registrations.Add(id);
            _exports[path] = registrations.ToArray();
        }
    }

    private void Emit(string path, string @interface, string name, GVariant parameters)
    {
        nint error = 0;
        var success = g_dbus_connection_emit_signal(_connection, 0, path, @interface, name, parameters.Handle, &error);
        CheckError(ref error, "emit " + name);
        if (success == 0)
            throw new IOException("GDBus did not emit " + name);
    }
    private void Event(string path, string name, string detail, int number, GVariant value, int length = 0)
    {
        using var parameters = GVariant.Tuple(GVariant.String(detail), GVariant.Int(number), GVariant.Int(length), GVariant.Box(value), GVariant.Array("{sv}"));
        Emit(path, "org.a11y.atspi.Event.Object", name, parameters);
    }
    private static string Name(Node node) => node.Element?.Node.Name ?? "";
    private string DisplayName(Node node) => node.Element is null ? _title : Name(node);
    private static string Description(Node node) => node.Element?.Node.GetHelpText() ?? "";
    private GVariant Parent(Node node) => node.Path == RootPath ? ExternalReference(Volatile.Read(ref _parent)) : ObjectReference(node.ParentPath);
    private static int Index(Node node) => node.IndexInParent;
    private GVariant CacheItem(Node node) => GVariant.Tuple(ObjectReference(node.Path), ObjectReference(RootPath), Parent(node),
        GVariant.Int(Index(node)), GVariant.Int(node.Children.Length), Strings(node.Interfaces), GVariant.String(DisplayName(node)),
        GVariant.UInt(node.Role), GVariant.String(Description(node)), States(node));
    private static GVariant Strings(IEnumerable<string> values) => GVariant.Array("s", values.Select(GVariant.String).ToArray());
    private GVariant States(Node node)
    {
        var bits = State(node, _window);
        return GVariant.Array("u", GVariant.UInt((uint)bits), GVariant.UInt((uint)(bits >> 32)));
    }
    private static readonly string[] StateNames = ["invalid", "active", "armed", "busy", "checked", "collapsed", "defunct", "editable", "enabled",
        "expandable", "expanded", "focusable", "focused", "has-tooltip", "horizontal", "iconified", "modal", "multi-line", "multiselectable", "opaque",
        "pressed", "resizable", "selectable", "selected", "sensitive", "showing", "single-line", "stale", "transient", "vertical", "visible",
        "manages-descendants", "indeterminate", "required", "truncated", "animated", "invalid-entry", "supports-autocompletion", "selectable-text",
        "is-default", "visited", "checkable", "has-popup", "read-only"];
    private static ulong State(Node node, AtSpiWindowGeometry window)
    {
        ulong bits = 0;
        void Set(int bit, bool value)
        {
            if (value)
                bits |= 1UL << bit;
        }
        var value = node.Element?.Node;
        Set(8, value?.Enabled ?? true);
        Set(24, value?.Enabled ?? true);
        Set(25, window.Visible && value?.Offscreen != true);
        Set(30, window.Visible && value?.Offscreen != true);
        Set(1, node.Path == WindowPath && window.Focused);
        if (value is not { } n)
            return bits;
        Set(3, n.Busy);
        Set(4, n.ToggleState == UiAccessibilityToggleState.On);
        Set(7, (n.SupportsValue || n.Range.HasValue) && !n.ReadOnly);
        Set(43, (n.SupportsValue || n.Range.HasValue) && n.ReadOnly);
        Set(11, n.CanReceiveFocus);
        Set(12, n.Focused && window.Focused);
        Set(13, n.Tooltip.HasValue);
        Set(14, n.Orientation == UiAccessibilityOrientation.Horizontal);
        Set(29, n.Orientation == UiAccessibilityOrientation.Vertical);
        Set(16, n.Modal);
        Set(17, n.SupportsValue && n.Multiline);
        Set(26, n.SupportsValue && !n.Multiline);
        Set(18, n.CanSelectMultiple);
        Set(22, n.SupportsSelectionItem);
        Set(23, n.Selected == true);
        Set(32, n.ToggleState == UiAccessibilityToggleState.Indeterminate);
        Set(33, n.Required || n.SelectionRequired);
        Set(36, n.Invalid);
        Set(38, n.TextInfo?.CanSetSelection == true);
        Set(41, n.ToggleState.HasValue);
        Set(9, n.Expansion is UiAccessibilityExpansionState.Collapsed or UiAccessibilityExpansionState.Expanded);
        Set(10, n.Expansion == UiAccessibilityExpansionState.Expanded);
        Set(5, n.Expansion == UiAccessibilityExpansionState.Collapsed);
        return bits;
    }

    private static uint Role(UiAccessibilityRole role) => role switch
    {
        UiAccessibilityRole.Button => 43,
        UiAccessibilityRole.Edit or UiAccessibilityRole.SearchBox => 79,
        UiAccessibilityRole.ProgressBar => 42,
        UiAccessibilityRole.Tooltip => 64,
        UiAccessibilityRole.Pane or UiAccessibilityRole.TabPanel or UiAccessibilityRole.AccordionPanel or UiAccessibilityRole.Card => 39,
        UiAccessibilityRole.ListItem or UiAccessibilityRole.Option => 32,
        UiAccessibilityRole.TabList => 38,
        UiAccessibilityRole.Tab => 37,
        UiAccessibilityRole.Menu => 33,
        UiAccessibilityRole.MenuItem => 35,
        UiAccessibilityRole.Slider => 51,
        UiAccessibilityRole.Checkbox => 7,
        UiAccessibilityRole.Switch or UiAccessibilityRole.Accordion => 62,
        UiAccessibilityRole.RadioButton => 44,
        UiAccessibilityRole.Label => 29,
        UiAccessibilityRole.Image => 27,
        UiAccessibilityRole.Navigation => 110,
        UiAccessibilityRole.OrderedList or UiAccessibilityRole.List => 31,
        UiAccessibilityRole.ComboBox => 11,
        UiAccessibilityRole.ListBox => 98,
        UiAccessibilityRole.Tree => 65,
        UiAccessibilityRole.TreeGrid => 66,
        UiAccessibilityRole.TreeGridRow => 90,
        UiAccessibilityRole.TreeItem => 91,
        UiAccessibilityRole.Toolbar => 63,
        UiAccessibilityRole.Dialog => 16,
        UiAccessibilityRole.AlertDialog => 2,
        UiAccessibilityRole.Note => 97,
        UiAccessibilityRole.SpinButton => 52,
        UiAccessibilityRole.Grid => 55,
        UiAccessibilityRole.Row => 90,
        UiAccessibilityRole.GridCell => 56,
        UiAccessibilityRole.Link => 88,
        UiAccessibilityRole.MenuGroup or UiAccessibilityRole.RadioGroup or UiAccessibilityRole.AvatarGroup or UiAccessibilityRole.Group => 99,
        _ => throw new NotSupportedException("Unmapped AT-SPI role: " + role)
    };
    private static (string Name, UiAccessibilityAction Action)[] Actions(UiAccessibilityNode node)
    {
        List<(string, UiAccessibilityAction)> actions = [];
        if (node.SupportsInvoke)
            actions.Add(("click", UiAccessibilityAction.Invoke));
        if (node.ToggleState.HasValue)
            actions.Add(("toggle", UiAccessibilityAction.Toggle));
        if (node.SupportsSelectionItem)
            actions.Add(("select", UiAccessibilityAction.Select));
        if (node.Expansion is UiAccessibilityExpansionState.Collapsed or UiAccessibilityExpansionState.Expanded)
        {
            actions.Add(("expand", UiAccessibilityAction.Expand));
            actions.Add(("collapse", UiAccessibilityAction.Collapse));
        }
        return actions.ToArray();
    }
}
