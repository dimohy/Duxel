using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Duxel.Core;
using static Duxel.Platform.Linux.GioNative;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private sealed class ProtocolError(string name, string message) : Exception(message)
    {
        internal string Name { get; } = name;
    }
    private static ProtocolError Invalid(string message) => new("org.freedesktop.DBus.Error.InvalidArgs", message);
    private static LinuxAtSpiBridge Owner(nint data) => (LinuxAtSpiBridge)GCHandle.FromIntPtr(data).Target!;
    private static VTable CallbackTable() => new()
    {
        Method = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, nint, nint, nint, void>)&Method,
        GetProperty = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, nint*, nint, nint>)&GetProperty,
        SetProperty = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, nint, nint*, nint, int>)&SetProperty
    };

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Method(nint connection, nint sender, nint path, nint @interface, nint method, nint parameters, nint invocation, nint data)
    {
        var owner = Owner(data);
        try
        {
            using var input = GVariant.Referenced(parameters);
            using var reply = owner.HandleMethod(Text(path), Text(@interface), Text(method), input);
            g_dbus_method_invocation_return_value(invocation, reply.Handle);
        }
        catch (ProtocolError error) { g_dbus_method_invocation_return_dbus_error(invocation, error.Name, error.Message); }
        catch (Exception error)
        {
            g_dbus_method_invocation_return_dbus_error(invocation, "org.freedesktop.DBus.Error.Failed", error.Message);
            owner.Fail(error);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint GetProperty(nint connection, nint sender, nint path, nint @interface, nint property, nint* error, nint data)
    {
        var owner = Owner(data);
        try
        {
            return owner.Property(Text(path), Text(@interface), Text(property)).Release();
        }
        catch (ProtocolError failure) { SetError(error, failure.Message); return 0; }
        catch (Exception failure) { SetError(error, failure.Message); owner.Fail(failure); return 0; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetProperty(nint connection, nint sender, nint path, nint @interface, nint property, nint value, nint* error, nint data)
    {
        var owner = Owner(data);
        try
        {
            if (Text(@interface) == ValueInterface && Text(property) == "CurrentValue")
            {
                using var numeric = GVariant.Referenced(value);
                owner.SetRangeValue(owner.Find(Text(path)), numeric.AsDouble());
                return 1;
            }
            if (Text(path) != RootPath || Text(@interface) != Application || Text(property) != "Id")
                throw Invalid("The requested AT-SPI property is not writable.");
            using var input = GVariant.Referenced(value);
            owner._applicationId = input.AsInt();
            return 1;
        }
        catch (ProtocolError failure) { SetError(error, failure.Message); return 0; }
        catch (Exception failure) { SetError(error, failure.Message); owner.Fail(failure); return 0; }
    }

    private static void SetError(nint* error, string message) =>
        g_set_error_literal(error, g_quark_from_string("Duxel.AT-SPI"), 1, message);
    private Node Find(string path) => _nodes.TryGetValue(path, out var node) ? node
        : throw new ProtocolError("org.freedesktop.DBus.Error.UnknownObject", "The accessible object is no longer published.");
    private static string Locale
    {
        get
        {
            var name = CultureInfo.CurrentUICulture.Name;
            return name.Length == 0 ? "C" : name.Replace('-', '_');
        }
    }
    private GVariant Property(string path, string @interface, string property)
    {
        if (path == CachePath && @interface == Cache && property == "version")
            return GVariant.UInt(1);
        var node = Find(path);
        if (property is "version" or "InterfaceVersion")
            return GVariant.UInt(1);
        if (@interface == Accessible)
            return property switch
            {
                "Name" => GVariant.String(DisplayName(node)),
                "Description" or "HelpText" => GVariant.String(Description(node)),
                "Parent" => Parent(node),
                "ChildCount" => GVariant.Int(node.Children.Length),
                "Locale" => GVariant.String(Locale),
                "AccessibleId" => GVariant.String(node.Element?.Node.Id ?? node.Path),
                _ => throw Invalid("Unknown accessible property: " + property)
            };
        if (@interface == Application && path == RootPath)
            return property switch
            {
                "ToolkitName" => GVariant.String("Duxel"),
                "AtspiVersion" => GVariant.String("2.1"),
                "Id" => GVariant.Int(_applicationId),
                "Version" or "ToolkitVersion" => GVariant.String(typeof(LinuxAtSpiBridge).Assembly.GetName().Version!.ToString()),
                _ => throw Invalid("Unknown application property: " + property)
            };
        if (@interface == ActionInterface && property == "NActions")
            return GVariant.Int(Actions(node.Element!.Value.Node).Length);
        if (@interface == ValueInterface)
            return RangeProperty(node, property);
        if (@interface == TextInterface)
            return TextProperty(node, property);
        if (@interface is TableInterface or TableCellInterface)
            return TableProperty(node, @interface, property);
        if (@interface == ImageInterface)
            return ImageProperty(node, property);
        if (@interface == SelectionInterface && property == "NSelectedChildren")
            return GVariant.Int(SelectedChildren(node, _nodes).Length);
        throw Invalid("Unknown AT-SPI property.");
    }

    private GVariant HandleMethod(string path, string @interface, string method, GVariant input)
    {
        if (path == CachePath && @interface == Cache && method == "GetItems")
            return GVariant.Tuple(GVariant.Array("((so)(so)(so)iiassusau)", _nodes.Values.Select(CacheItem).ToArray()));
        var node = Find(path);
        if (@interface == Accessible)
            return method switch
            {
                "GetChildAtIndex" => GVariant.Tuple(Child(node, Int(input, 0))),
                "GetChildren" => GVariant.Tuple(GVariant.Array("(so)", node.Children.Select(ObjectReference).ToArray())),
                "GetIndexInParent" => GVariant.Tuple(GVariant.Int(Index(node))),
                "GetRole" => GVariant.Tuple(GVariant.UInt(node.Role)),
                "GetRoleName" or "GetLocalizedRoleName" => GVariant.Tuple(GVariant.String(RoleName(node.Role))),
                "GetState" => GVariant.Tuple(States(node)),
                "GetInterfaces" => GVariant.Tuple(Strings(node.Interfaces)),
                "GetApplication" => GVariant.Tuple(ObjectReference(RootPath)),
                "GetRelationSet" => GVariant.Tuple(Relations(node)),
                "GetAttributes" => GVariant.Tuple(GVariant.Array("{ss}", GVariant.DictEntry(GVariant.String("toolkit"), GVariant.String("Duxel")),
                    GVariant.DictEntry(GVariant.String("process-id"), GVariant.String(Environment.ProcessId.ToString(CultureInfo.InvariantCulture))))),
                _ => throw Invalid("Unknown accessible method: " + method)
            };
        if (@interface == Application && path == RootPath)
            return method switch
            {
                "GetLocale" => GVariant.Tuple(GVariant.String(Locale)),
                "GetApplicationBusAddress" => GVariant.Tuple(GVariant.String("")),
                _ => throw Invalid("Unknown application method: " + method)
            };
        if (@interface == Component)
            return ComponentMethod(node, method, input);
        if (@interface == SelectionInterface)
            return SelectionMethod(node, method, input);
        if (@interface == TextInterface)
            return TextMethod(node, method, input);
        if (@interface == ImageInterface)
            return ImageMethod(node, method, input);
        if (@interface is TableInterface or TableCellInterface)
            return TableMethod(node, @interface, method, input);
        if (@interface == EditableTextInterface)
            return EditableTextMethod(node, method, input);
        if (@interface == ActionInterface && node.Element is { } element)
        {
            var actions = Actions(element.Node);
            if (method == "GetActions")
                return GVariant.Tuple(GVariant.Array("(sss)", actions.Select(a => GVariant.Tuple(GVariant.String(a.Name), GVariant.String(a.Name), GVariant.String(""))).ToArray()));
            var index = Int(input, 0);
            if (index < 0 || index >= actions.Length)
                throw Invalid("The action index is out of range.");
            var action = actions[index];
            return method switch
            {
                "GetName" or "GetLocalizedName" or "GetDescription" => GVariant.Tuple(GVariant.String(action.Name)),
                "GetKeyBinding" => GVariant.Tuple(GVariant.String("")),
                "DoAction" => GVariant.Tuple(GVariant.Bool(Invoke(node, action.Action))),
                _ => throw Invalid("Unknown action method: " + method)
            };
        }
        throw Invalid("Unsupported AT-SPI interface: " + @interface);
    }
    private static int Int(GVariant input, int index)
    {
        using var value = input.Child(index);
        return value.AsInt();
    }
    private static string RoleName(uint role) => role switch
    {
        75 => "application",
        23 => "frame",
        43 => "push button",
        79 => "entry",
        42 => "progress bar",
        64 => "tool tip",
        39 => "panel",
        32 => "list item",
        38 => "page tab list",
        37 => "page tab",
        33 => "menu",
        35 => "menu item",
        51 => "slider",
        7 => "check box",
        62 => "toggle button",
        44 => "radio button",
        29 => "label",
        27 => "image",
        110 => "landmark",
        31 => "list",
        11 => "combo box",
        98 => "list box",
        63 => "tool bar",
        16 => "dialog",
        97 => "comment",
        52 => "spin button",
        55 => "table",
        65 => "tree",
        66 => "tree table",
        91 => "tree item",
        90 => "table row",
        56 => "table cell",
        88 => "link",
        99 => "grouping",
        _ => throw new NotSupportedException("Unmapped AT-SPI role name: " + role)
    };
    private static uint UInt(GVariant input, int index)
    {
        using var value = input.Child(index);
        return value.AsUInt();
    }
    private GVariant Child(Node node, int index) => index >= 0 && index < node.Children.Length ? ObjectReference(node.Children[index]) : throw Invalid("The child index is out of range.");
    private bool Invoke(Node node, UiAccessibilityAction action)
    {
        if (node.Element is not { } element || _tree.RequestAction(element.Node.Id, element.RuntimeId, action) != UiAccessibilityResult.Success)
            return false;
        Refresh();
        return true;
    }
    private bool GrabFocus(Node node) => _window.Visible && node.Element?.Node.CanReceiveFocus == true
        && _focusWindow() && Invoke(node, UiAccessibilityAction.Focus);
    private GVariant Relations(Node node)
    {
        List<GVariant> relations = [];
        if (node.Element?.Node.LabeledBy is { } label)
        {
            var target = _nodes.Values.FirstOrDefault(n => n.Element?.Node.Id == label);
            if (target is not null)
                relations.Add(GVariant.Tuple(GVariant.UInt(2), GVariant.Array("(so)", ObjectReference(target.Path))));
        }
        return GVariant.Array("(ua(so))", relations.ToArray());
    }

    private static UiRect Bounds(Node node, AtSpiWindowGeometry window) => node.Element?.Node.Bounds is { } bounds
        ? ToScreen(bounds, window)
        : new(window.X, window.Y, window.Width, window.Height);
    private static UiRect ToScreen(UiRect bounds, AtSpiWindowGeometry window)
        => new(window.X + bounds.X * window.ContentScale, window.Y + bounds.Y * window.ContentScale,
            bounds.Width * window.ContentScale, bounds.Height * window.ContentScale);
    private readonly record struct PixelRect(int X, int Y, int Width, int Height);
    private static PixelRect PixelBounds(UiRect value)
    {
        var x = checked((int)MathF.Floor(value.X));
        var y = checked((int)MathF.Floor(value.Y));
        return new(x, y, checked((int)MathF.Ceiling(value.X + value.Width) - x), checked((int)MathF.Ceiling(value.Y + value.Height) - y));
    }
    private PixelRect Bounds(Node node, uint coordinates)
    {
        return CoordinateBounds(node, PixelBounds(Bounds(node, _window)), coordinates);
    }
    private UiVector2 ClientPoint(Node node, int x, int y, uint coordinates)
    {
        var screen = PixelBounds(Bounds(node, _window));
        var requested = Bounds(node, coordinates);
        return new(((long)x + screen.X - requested.X - _window.X) / _window.ContentScale,
            ((long)y + screen.Y - requested.Y - _window.Y) / _window.ContentScale);
    }

    private PixelRect CoordinateBounds(Node node, PixelRect value, uint coordinates)
    {
        if (coordinates == 0)
            return value;
        if (coordinates == 1)
            return value with
            {
                X = value.X - _window.X,
                Y = value.Y - _window.Y
            };
        if (coordinates == 2 && _nodes.TryGetValue(node.ParentPath, out var parent))
        {
            var origin = PixelBounds(Bounds(parent, _window));
            return value with
            {
                X = value.X - origin.X,
                Y = value.Y - origin.Y
            };
        }
        throw Invalid("Unsupported coordinate space.");
    }
    private static GVariant Rectangle(UiRect value) => Rectangle(PixelBounds(value));
    private static GVariant Rectangle(PixelRect value) => GVariant.Tuple(GVariant.Int(value.X), GVariant.Int(value.Y), GVariant.Int(value.Width), GVariant.Int(value.Height));
    private static bool Contains(PixelRect bounds, long x, long y) => x >= bounds.X && y >= bounds.Y && x < (long)bounds.X + bounds.Width && y < (long)bounds.Y + bounds.Height;
    private bool ContainsNode(Node node, long screenX, long screenY)
    {
        if (!Contains(PixelBounds(Bounds(node, _window)), screenX, screenY))
            return false;
        return node.Element?.Node.GetPointGeometry() is not { } geometry
            || geometry.ContainsPoint(screenX - _window.X, screenY - _window.Y);
    }
    private GVariant ComponentMethod(Node node, string method, GVariant input)
    {
        if (method == "GetExtents")
            return GVariant.Tuple(Rectangle(Bounds(node, UInt(input, 0))));
        if (method == "GetPosition")
        {
            var bounds = Bounds(node, UInt(input, 0));
            return GVariant.Tuple(GVariant.Int(bounds.X), GVariant.Int(bounds.Y));
        }
        if (method == "GetSize")
        {
            var bounds = PixelBounds(Bounds(node, _window));
            return GVariant.Tuple(GVariant.Int(bounds.Width), GVariant.Int(bounds.Height));
        }
        if (method is "Contains" or "GetAccessibleAtPoint")
        {
            var x = Int(input, 0);
            var y = Int(input, 1);
            var coordinates = UInt(input, 2);
            var requested = Bounds(node, coordinates);
            var screen = PixelBounds(Bounds(node, _window));
            var screenX = (long)x + screen.X - requested.X;
            var screenY = (long)y + screen.Y - requested.Y;
            if (method == "Contains")
                return GVariant.Tuple(GVariant.Bool(ContainsNode(node, screenX, screenY)));
            Stack<(Node Node, bool ChildrenVisited)> pending = new();
            foreach (var child in node.Children)
                pending.Push((_nodes[child], false));
            Node? hit = null;
            while (pending.TryPop(out var entry))
            {
                if (!entry.ChildrenVisited)
                {
                    pending.Push((entry.Node, true));
                    foreach (var child in entry.Node.Children)
                        pending.Push((_nodes[child], false));
                }
                else if (entry.Node.Element?.Node.Offscreen != true && ContainsNode(entry.Node, screenX, screenY))
                {
                    hit = entry.Node;
                    break;
                }
            }
            return GVariant.Tuple(hit is null ? ExternalReference(new("", NullPath)) : ObjectReference(hit.Path));
        }
        return method switch
        {
            "GetLayer" => GVariant.Tuple(GVariant.UInt(3)),
            "GetMDIZOrder" => GVariant.Tuple(GVariant.Short(0)),
            "GetAlpha" => GVariant.Tuple(GVariant.Double(1)),
            "GrabFocus" => GVariant.Tuple(GVariant.Bool(GrabFocus(node))),
            "ScrollTo" or "ScrollToPoint" => GVariant.Tuple(GVariant.Bool(ScrollComponent(node, method, input))),
            "SetExtents" or "SetPosition" or "SetSize" => GVariant.Tuple(GVariant.Bool(false)),
            _ => throw Invalid("Unknown component method: " + method)
        };
    }

    private bool ScrollComponent(Node node, string method, GVariant input)
    {
        if (node.Element is not { } target)
            return false;
        UiAccessibilityScrollAlignment alignment;
        if (method == "ScrollTo")
        {
            var placement = UInt(input, 0);
            if (placement > (uint)UiAccessibilityScrollPlacement.Nearest)
                return false;
            if (target.Node.GridItem is { } gridItem && target.Node.VirtualItem is { IsPlaceholder : true }
                && _tree.TryGet(gridItem.GridId, out var grid) && grid.Node.Grid is { IsVirtualized : true })
            {
                if (placement != (uint)UiAccessibilityScrollPlacement.Nearest)
                    return false;
                var realized = _tree.RealizeVirtualItem(grid.Node.Id, grid.RuntimeId, new(target.Node.Id, target.RuntimeId))
                    == UiAccessibilityResult.Success;
                if (realized)
                    Refresh();
                return realized;
            }
            alignment = new((UiAccessibilityScrollPlacement)placement);
        }
        else
            alignment = new(ClientPoint(node, Int(input, 1), Int(input, 2), UInt(input, 0)));
        var accepted = _tree.RequestScrollIntoView(target.Node.Id, target.RuntimeId, alignment) == UiAccessibilityResult.Success;
        if (accepted)
            Refresh();
        return accepted;
    }
}
