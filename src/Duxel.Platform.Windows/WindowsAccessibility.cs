using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsAccessibility : IDisposable
{
    internal readonly UiAccessibilityTree Tree = new();
    internal readonly nint Window;
    internal readonly UiaRootProvider Root;
    private readonly object _gate = new();
    private readonly Dictionary<string, WeakReference<UiaNodeProvider>> _providers = new(StringComparer.Ordinal);
    private readonly List<string> _expired = [];
    private readonly Queue<UiaNodeProvider> _retired = [];
    private readonly List<(UiaEventProvider Source, int EventId)> _stagedTooltipEvents = [];
    private readonly Queue<(UiaEventProvider Source, int EventId)> _publishedTooltipEvents = [];
    private readonly Action? _requestCleanup;
    private int _disposed, _registered;
    internal int DisconnectResult
    {
        get; private set;
    }
    internal WindowsAccessibility(nint window, Action? requestCleanup = null)
    {
        Window = window;
        Root = new(this);
        _requestCleanup = requestCleanup;
        _reportedWindowFocus = HasFocus;
        Tree.ElementsRemoved += PruneRemovedProviders;
        Tree.AutomationEvent += RaiseAutomationEvent;
        Tree.Published += PublishTooltipEvents;
        Tree.Announcement += RaiseAnnouncement;
        Tree.TextPropertyChanged += RaiseTextPropertyChanged;
        Tree.ScrollChanged += RaiseScrollChanged;
        Tree.ScrollGeometryChanged += RaiseScrollGeometryChanged;
        Tree.PaneChildrenChanged += RaiseChildrenChanged;
        Tree.TreeChildrenChanged += RaiseChildrenChanged;
        Tree.SelectionChanged += RaiseSelectionChanged;
        Tree.RelationshipChanged += RaiseRelationshipChanged;
        Tree.FocusChanged += RaiseFocusChanged;
        Tree.ExpansionChanged += RaiseExpansionChanged;
        Tree.ToggleChanged += RaiseToggleChanged;
        Tree.RangeChanged += RaiseRangeChanged;
        Tree.GridChanged += RaiseGridChanged;
    }
    private void RaiseAutomationEvent(UiAccessibilityEvent notification)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0)
        {
            return;
        }
        var eventId = notification.Kind switch
        {
            UiAccessibilityEventKind.TooltipOpened => 20000,
            UiAccessibilityEventKind.TooltipClosed => 20001,
            UiAccessibilityEventKind.MenuOpened => 20003,
            UiAccessibilityEventKind.MenuClosed => 20007,
            UiAccessibilityEventKind.Invoked => 20009,
            _ => throw new ArgumentOutOfRangeException(nameof(notification))
        };
        if (_requestCleanup is not null && !notification.DuringDisposal
            && notification.Kind is UiAccessibilityEventKind.TooltipOpened or UiAccessibilityEventKind.TooltipClosed)
        {
            // A native disconnect can invalidate a queued event with the same runtime ID.
            // Capture before removal but send in publication order after native retirement.
            var source = CaptureEventSource(notification.Element, closed : notification.Kind == UiAccessibilityEventKind.TooltipClosed);
            _stagedTooltipEvents.Add((source, eventId));
            return;
        }
        UiaElementProvider provider = notification.Kind is UiAccessibilityEventKind.TooltipClosed or UiAccessibilityEventKind.MenuClosed
            ? CaptureEventSource(notification.Element, closed : true)
            : Find(notification.Element.Node.Id)
                ?? throw new InvalidOperationException("The automation event source must still be published.");
        RaiseNativeAutomationEvent(provider, eventId);
    }

    private void PublishTooltipEvents()
    {
        if (_stagedTooltipEvents.Count == 0)
            return;
        lock (_gate)
            foreach (var notification in _stagedTooltipEvents)
                _publishedTooltipEvents.Enqueue(notification);
        _stagedTooltipEvents.Clear();
        _requestCleanup?.Invoke();
    }

    private static void RaiseNativeAutomationEvent(UiaElementProvider provider, int eventId)
    {
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationEvent(pointer, eventId));
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }
    internal UiaEventProvider CaptureEventSource(UiAccessibilityElement element, bool closed)
    {
        var root = new UiaEventRootProvider(this);
        return CaptureEventSource(element, closed, root);
    }
    private UiaEventProvider CaptureEventSource(UiAccessibilityElement element, bool closed, UiaEventRootProvider root)
    {
        UiaElementProvider parent = root;
        if (element.Node.ParentId.Length != 0)
        {
            if (!Tree.TryGet(element.Node.ParentId, out var owner))
            {
                throw new InvalidOperationException("The event source ancestry must be captured before removal.");
            }
            parent = CaptureEventSource(owner, closed: false, root);
        }
        // UIA builds an event cache asynchronously and retains this provider until handlers finish.
        var snapshot = closed ? element with
        {
            Node = element.Node with
            {
                Offscreen = true
            }
        } : element;
        return new UiaEventProvider(this, snapshot, parent, root);
    }

    private void RaiseTextPropertyChanged(UiAccessibilityTextChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0)
        {
            return;
        }
        var propertyId = change.Property switch
        {
            UiAccessibilityTextProperty.Name => 30005,
            UiAccessibilityTextProperty.HelpText => 30013,
            UiAccessibilityTextProperty.ItemStatus => 30026,
            UiAccessibilityTextProperty.Value => 30045,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var provider = Find(change.Element.Node.Id)
            ?? throw new InvalidOperationException("The property change source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        UiaVariant oldValue = default;
        UiaVariant newValue = default;
        try
        {
            oldValue = UiaVariant.String(change.OldValue);
            newValue = UiaVariant.String(change.NewValue);
            Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, propertyId, oldValue, newValue));
        }
        finally
        {
            var newResult = UiaNative.VariantClear(ref newValue);
            var oldResult = UiaNative.VariantClear(ref oldValue);
            Marshal.Release(pointer);
            Marshal.ThrowExceptionForHR(newResult);
            Marshal.ThrowExceptionForHR(oldResult);
        }
    }

    private void RaiseScrollChanged(UiAccessibilityScrollChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0)
            return;

        var provider = Find(change.Element.Node.Id)
            ?? throw new InvalidOperationException("The scroll change source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        var previous = change.Previous.GetValueOrDefault();
        var current = change.Element.Node.Scroll.GetValueOrDefault();
        try
        {
            RaiseBoolean(30034, change.Previous.HasValue, change.Element.Node.Scroll.HasValue);
            RaiseDouble(30053, previous.HorizontalScrollPercent, current.HorizontalScrollPercent);
            RaiseDouble(30054, previous.HorizontalViewSize, current.HorizontalViewSize);
            RaiseDouble(30055, previous.VerticalScrollPercent, current.VerticalScrollPercent);
            RaiseDouble(30056, previous.VerticalViewSize, current.VerticalViewSize);
            RaiseBoolean(30057, previous.HorizontallyScrollable, current.HorizontallyScrollable);
            RaiseBoolean(30058, previous.VerticallyScrollable, current.VerticallyScrollable);
        }
        finally
        {
            Marshal.Release(pointer);
        }

        void RaiseBoolean(int property, bool oldValue, bool newValue)
        {
            if (oldValue != newValue)
                Raise(property, UiaVariant.Boolean(oldValue), UiaVariant.Boolean(newValue));
        }

        void RaiseDouble(int property, double oldValue, double newValue)
        {
            if (oldValue != newValue)
                Raise(property, UiaVariant.Double(oldValue), UiaVariant.Double(newValue));
        }

        void Raise(int property, UiaVariant oldValue, UiaVariant newValue) =>
            Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, property, oldValue, newValue));
    }

    private void RaiseScrollGeometryChanged(UiAccessibilityGeometryChange change)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0 || !UiaNative.UiaClientsAreListening())
            return;

        var node = change.Element.Node;
        var provider = Find(node.Id)
            ?? throw new InvalidOperationException("The geometry change source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        UiaVariant oldBounds = default;
        UiaVariant newBounds = default;
        try
        {
            if (change.PreviousBounds != node.Bounds)
            {
                oldBounds = UiaNative.Rectangle(Bounds(change.PreviousBounds));
                newBounds = UiaNative.Rectangle(Bounds(node.Bounds));
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30001, oldBounds, newBounds));
            }
            if (change.WasOffscreen != node.Offscreen)
            {
                Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseAutomationPropertyChangedEvent(pointer, 30022,
                    UiaVariant.Boolean(change.WasOffscreen), UiaVariant.Boolean(node.Offscreen)));
            }
        }
        finally
        {
            var newResult = UiaNative.VariantClear(ref newBounds);
            var oldResult = UiaNative.VariantClear(ref oldBounds);
            Marshal.Release(pointer);
            Marshal.ThrowExceptionForHR(newResult);
            Marshal.ThrowExceptionForHR(oldResult);
        }
    }

    private void RaiseChildrenChanged(UiAccessibilityElement pane)
    {
        if (!Serving || Volatile.Read(ref _registered) == 0 || !UiaNative.UiaClientsAreListening())
            return;

        const int childrenInvalidated = 2;
        var provider = Find(pane.Node.Id)
            ?? throw new InvalidOperationException("The structure change source must already be published.");
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            Marshal.ThrowExceptionForHR(UiaNative.UiaRaiseStructureChangedEvent(pointer, childrenInvalidated, 0, 0));
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }
    internal (int Cached, int Retired, int Disconnected, int AlreadyUnavailable, int Capacity) Storage
    {
        get
        {
            lock (_gate)
                return (_providers.Count, _retired.Count, _disconnected, _alreadyUnavailable, _providers.EnsureCapacity(0));
        }
    }
    private int _disconnected, _alreadyUnavailable;
    internal bool Live => !Tree.IsDisposed;
    internal bool Serving => Volatile.Read(ref _disposed) != 2 && Live;
    internal bool HasFocus => Live && UiaNative.WindowHasKeyboardFocus(Window);
    internal UiaNodeProvider? Find(string id)
    {
        lock (_gate)
        {
            if (!Serving || !Tree.TryGet(id, out var entry))
                return null;
            if (_providers.TryGetValue(id, out var weak) && weak.TryGetTarget(out var existing) && existing.RuntimeId == entry.RuntimeId)
                return existing;
            var result = new UiaNodeProvider(this, id, entry.RuntimeId);
            _providers[id] = new(result);
            return result;
        }
    }
    private void PruneRemovedProviders()
    {
        var notify = false;
        lock (_gate)
        {
            if (!Serving)
                return;
            var wasEmpty = _retired.Count == 0;
            foreach (var pair in _providers)
            {
                var alive = pair.Value.TryGetTarget(out var provider);
                if (alive && Tree.TryGet(pair.Key, out var entry) && entry.RuntimeId == provider!.RuntimeId)
                    continue;
                _expired.Add(pair.Key);
                if (alive && Volatile.Read(ref _registered) != 0)
                    _retired.Enqueue(provider!);
            }
            foreach (var id in _expired)
                _providers.Remove(id);
            _expired.Clear();
            var limit = Math.Max(256, _providers.Count * 4);
            if (_providers.EnsureCapacity(0) > limit)
                _providers.TrimExcess();
            if (_expired.Capacity > limit)
                _expired.TrimExcess();
            notify = wasEmpty && _retired.Count != 0;
        }
        if (notify)
            _requestCleanup?.Invoke();
    }
    internal void DrainRetiredProviders(int budget = 32)
    {
        // Called on a queued window message, never while holding a publication/cache lock.
        for (var i = 0; i < budget; i++)
        {
            UiaNodeProvider provider;
            lock (_gate)
            {
                if (!_retired.TryDequeue(out provider!))
                    break;
            }
            Disconnect(provider);
        }
        for (var i = 0; i < budget; i++)
        {
            (UiaEventProvider Source, int EventId) notification;
            lock (_gate)
            {
                if (_retired.Count != 0 || !_publishedTooltipEvents.TryDequeue(out notification))
                    break;
            }
            var live = notification.EventId == 20000 ? Find(notification.Source.SourceId) : null;
            UiaElementProvider sender = live is not null && live.RuntimeId == notification.Source.RuntimeId
                ? live : notification.Source;
            RaiseNativeAutomationEvent(sender, notification.EventId);
        }
        bool again;
        lock (_gate)
        {
            again = _retired.Count != 0 || _publishedTooltipEvents.Count != 0;
            if (_retired.Count == 0 && _retired.EnsureCapacity(0) > 256)
                _retired.TrimExcess();
            if (_publishedTooltipEvents.Count == 0 && _publishedTooltipEvents.EnsureCapacity(0) > 256)
                _publishedTooltipEvents.TrimExcess();
        }
        if (again)
            _requestCleanup?.Invoke();
    }
    private void Disconnect(IUiaSimple provider)
    {
        var pointer = UiaNative.Pointer<IUiaSimple>(provider);
        try
        {
            var hr = UiaNative.UiaDisconnectProvider(pointer);
            if (hr == UiaNative.Unavailable)
                Interlocked.Increment(ref _alreadyUnavailable);
            else
            {
                Marshal.ThrowExceptionForHR(hr);
                Interlocked.Increment(ref _disconnected);
            }
        }
        finally { Marshal.Release(pointer); }
    }
    internal UiaBounds Bounds(UiRect? logical = null)
    {
        if (!Live || !UiaNative.GetClientRect(Window, out var rect))
            throw new COMException("UIA window is unavailable.", UiaNative.Unavailable);
        var point = new UiaNative.NativePoint();
        if (!UiaNative.ClientToScreen(Window, ref point))
            throw new COMException("UIA client origin is unavailable.", UiaNative.Unavailable);
        if (logical is not { } bounds)
            return new()
            {
                X = point.X,
                Y = point.Y,
                Width = rect.Right - rect.Left,
                Height = rect.Bottom - rect.Top
            };
        var dpi = UiaNative.GetDpiForWindow(Window);
        if (dpi == 0)
            throw new COMException("UIA scale is unavailable.", UiaNative.Unavailable);
        var scale = dpi / 96d;
        return new()
        {
            X = point.X + bounds.X * scale,
            Y = point.Y + bounds.Y * scale,
            Width = bounds.Width * scale,
            Height = bounds.Height * scale
        };
    }
    internal nint Return(nuint wParam, nint lParam)
    {
        var pointer = UiaNative.Pointer<IUiaSimple>(Root);
        try
        {
            Volatile.Write(ref _registered, 1);
            return UiaNative.UiaReturnRawElementProvider(Window, wParam, lParam, pointer);
        }
        finally { Marshal.Release(pointer); }
    }
    internal void ReleaseWindowAssociation()
    {
        if (Interlocked.Exchange(ref _registered, 0) != 0)
        {
            _ = UiaNative.UiaReturnRawElementProvider(Window, 0, 0, 0);
        }
    }
    internal static int Result(UiAccessibilityResult result) => result switch
    {
        UiAccessibilityResult.Success => 0,
        UiAccessibilityResult.Unavailable => UiaNative.Unavailable,
        UiAccessibilityResult.Disabled => UiaNative.Disabled,
        UiAccessibilityResult.ReadOnly or UiAccessibilityResult.InvalidOperation => UiaNative.InvalidOperation,
        UiAccessibilityResult.InvalidArgument => UiaNative.InvalidArgument,
        UiAccessibilityResult.Busy => unchecked((int)0x800700AA),
        UiAccessibilityResult.Timeout => unchecked((int)0x80131505),
        _ => unchecked((int)0x80004005)
    };
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }
        try
        {
            try
            {
                DrainRetiredProviders(int.MaxValue);
            }
            finally
            {
                Tree.Dispose();
            }
        }
        finally
        {
            Volatile.Write(ref _disposed, 2);
            DisconnectPublishedProviders();
        }
    }

    private void DisconnectPublishedProviders()
    {
        Tree.ElementsRemoved -= PruneRemovedProviders;
        Tree.AutomationEvent -= RaiseAutomationEvent;
        Tree.Published -= PublishTooltipEvents;
        Tree.Announcement -= RaiseAnnouncement;
        Tree.TextPropertyChanged -= RaiseTextPropertyChanged;
        Tree.ScrollChanged -= RaiseScrollChanged;
        Tree.ScrollGeometryChanged -= RaiseScrollGeometryChanged;
        Tree.PaneChildrenChanged -= RaiseChildrenChanged;
        Tree.TreeChildrenChanged -= RaiseChildrenChanged;
        Tree.SelectionChanged -= RaiseSelectionChanged;
        Tree.RelationshipChanged -= RaiseRelationshipChanged;
        Tree.FocusChanged -= RaiseFocusChanged;
        Tree.ExpansionChanged -= RaiseExpansionChanged;
        Tree.ToggleChanged -= RaiseToggleChanged;
        Tree.RangeChanged -= RaiseRangeChanged;
        Tree.GridChanged -= RaiseGridChanged;
        _reportedFocus = null;
        _reportedWindowFocus = false;
        var root = UiaNative.Pointer<IUiaSimple>(Root);
        try
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _registered) != 0)
                {
                    foreach (var weak in _providers.Values)
                    {
                        if (weak.TryGetTarget(out var provider))
                            _retired.Enqueue(provider);
                    }
                }
                _providers.Clear();
            }
            DrainRetiredProviders(int.MaxValue);
            DisconnectResult = Volatile.Read(ref _registered) != 0 ? UiaNative.UiaDisconnectProvider(root) : 0;
            // UIA has no connection to release after the HWND/provider was already removed.
            // Preserve that terminal HRESULT for diagnostics; every other failure is surfaced.
            if (DisconnectResult != UiaNative.Unavailable)
                Marshal.ThrowExceptionForHR(DisconnectResult);
        }
        finally
        {
            Marshal.Release(root);
            lock (_gate)
            {
                _providers.Clear();
                _expired.Clear();
                _retired.Clear();
                _stagedTooltipEvents.Clear();
                _publishedTooltipEvents.Clear();
            }
        }
    }
}

internal abstract partial class UiaElementProvider(WindowsAccessibility host, string? id, long runtimeId) : IUiaSimple, IUiaFragment
{
    protected readonly WindowsAccessibility Host = host;
    protected readonly string? Id = id;
    internal readonly long RuntimeId = runtimeId;
    protected virtual bool Read(out UiAccessibilityElement element)
    {
        element = default;
        return Host.Live && (Id is null || Host.Tree.TryGet(Id, out element) && element.RuntimeId == RuntimeId);
    }
    public int GetProviderOptions(out int value)
    {
        value = 34;
        return Id is not null || Read(out _) ? 0 : UiaNative.Unavailable;
    }
    public virtual int GetPatternProvider(int pattern, out nint value)
    {
        value = 0;
        return Read(out _) ? 0 : UiaNative.Unavailable;
    }
    private static string AriaProperties(UiAccessibilityNode node)
    {
        var result = string.Empty;
        void Append(string value)
        {
            if (result.Length > 0) result += ";";
            result += value;
        }
        if (node.Modal) Append("modal=true");
        else if (node.Role is UiAccessibilityRole.Dialog or UiAccessibilityRole.AlertDialog) Append("modal=false");
        if (node.IsCurrent) Append("current=page");
        if (node.Busy) Append("busy=true");
        return result;
    }

    public virtual int GetPropertyValue(int property, out UiaVariant value)
    {
        value = default;
        if (!Read(out var element))
            return UiaNative.Unavailable;
        if (Id is null)
        {
            value = property switch
            {
                30003 => UiaVariant.Integer(50032),
                30008 => UiaVariant.Boolean(Host.HasFocus && Host.Tree.Root.Focus is null),
                30024 => UiaVariant.String("Duxel"),
                _ => default
            };
            return 0;
        }
        var node = element.Node;
        value = property switch
        {
            30003 => UiaVariant.Integer(node.Role switch
            {
                UiAccessibilityRole.Button => 50000,
                UiAccessibilityRole.Link => 50005,
                UiAccessibilityRole.Edit or UiAccessibilityRole.SearchBox => 50004,
                UiAccessibilityRole.ProgressBar => 50012,
                UiAccessibilityRole.Tooltip => 50022,
                UiAccessibilityRole.Pane or UiAccessibilityRole.Dialog or UiAccessibilityRole.AlertDialog => 50033,
                UiAccessibilityRole.Toolbar => 50021,
                UiAccessibilityRole.Group or UiAccessibilityRole.Note => 50026,
                UiAccessibilityRole.SpinButton => 50016,
                UiAccessibilityRole.List => 50008,
                UiAccessibilityRole.Grid => 50028,
                UiAccessibilityRole.Row => 50029,
                UiAccessibilityRole.GridCell => 50025,
                UiAccessibilityRole.ListItem => 50007,
                UiAccessibilityRole.TabList => 50018,
                UiAccessibilityRole.Tab => 50019,
                UiAccessibilityRole.TabPanel => 50033,
                UiAccessibilityRole.Menu => 50009,
                UiAccessibilityRole.MenuItem => 50011,
                UiAccessibilityRole.MenuGroup => 50026,
                UiAccessibilityRole.Slider => 50015,
                UiAccessibilityRole.Checkbox => 50002,
                UiAccessibilityRole.Switch => 50000,
                UiAccessibilityRole.RadioGroup => 50026,
                UiAccessibilityRole.RadioButton => 50013,
                UiAccessibilityRole.Label => 50020,
                UiAccessibilityRole.Accordion => 50026,
                UiAccessibilityRole.AccordionPanel => 50033,
                UiAccessibilityRole.Image => 50006,
                UiAccessibilityRole.Card => 50026,
                UiAccessibilityRole.AvatarGroup => 50026,
                UiAccessibilityRole.ComboBox => 50003,
                UiAccessibilityRole.ListBox => 50008,
                UiAccessibilityRole.Tree or UiAccessibilityRole.TreeGrid => 50023,
                UiAccessibilityRole.TreeGridRow => 50029,
                UiAccessibilityRole.TreeItem => 50024,
                UiAccessibilityRole.Option => 50007,
                UiAccessibilityRole.Navigation => 50026,
                UiAccessibilityRole.OrderedList => 50008,
                _ => throw new InvalidOperationException("Unknown accessibility role.")
            }),
            30005 => UiaVariant.String(node.Name),
            30014 when node.ClickablePoint is { } point => UiaNative.ClickablePoint(Host.Bounds(new(point.X, point.Y, 0, 0))),
            30008 => UiaVariant.Boolean(Host.IsFocused(element)),
            30009 => UiaVariant.Boolean(node.CanReceiveFocus),
            30010 => UiaVariant.Boolean(node.Enabled),
            30011 => UiaVariant.String(node.VirtualItem?.Key ?? node.Id),
            30012 => UiaVariant.String(node.Role switch
            {
                UiAccessibilityRole.Button => node.ToggleState.HasValue ? "Duxel.ToggleButton" : "Duxel.Button",
                UiAccessibilityRole.Link => "Duxel.Link",
                UiAccessibilityRole.Edit => "Duxel.Input",
                UiAccessibilityRole.SearchBox => "Duxel.SearchBox",
                UiAccessibilityRole.Tooltip => "Duxel.Tooltip",
                UiAccessibilityRole.ProgressBar => "Duxel.ProgressBar",
                UiAccessibilityRole.Pane => "Duxel.ScrollView",
                UiAccessibilityRole.Toolbar => "Duxel.Toolbar",
                UiAccessibilityRole.Group => "Duxel.Group",
                UiAccessibilityRole.Note => "Duxel.Note",
                UiAccessibilityRole.SpinButton => "Duxel.SpinButton",
                UiAccessibilityRole.List => "Duxel.List",
                UiAccessibilityRole.Grid => "Duxel.Grid",
                UiAccessibilityRole.Row => "Duxel.Row",
                UiAccessibilityRole.GridCell => "Duxel.GridCell",
                UiAccessibilityRole.Dialog => "Duxel.Dialog",
                UiAccessibilityRole.AlertDialog => "Duxel.AlertDialog",
                UiAccessibilityRole.ListItem => node.VirtualItem.HasValue ? "Duxel.VirtualListItem" : "Duxel.ListItem",
                UiAccessibilityRole.TabList => "Duxel.TabList",
                UiAccessibilityRole.Tab => "Duxel.Tab",
                UiAccessibilityRole.TabPanel => "Duxel.TabPanel",
                UiAccessibilityRole.Menu => "Duxel.Menu",
                UiAccessibilityRole.MenuItem => "Duxel.MenuItem",
                UiAccessibilityRole.MenuGroup => "Duxel.MenuRadioGroup",
                UiAccessibilityRole.Slider => "Duxel.Slider",
                UiAccessibilityRole.Checkbox => "Duxel.Checkbox",
                UiAccessibilityRole.Switch => "Duxel.Switch",
                UiAccessibilityRole.RadioGroup => "Duxel.RadioGroup",
                UiAccessibilityRole.RadioButton => "Duxel.RadioButton",
                UiAccessibilityRole.Label => "Duxel.Label",
                UiAccessibilityRole.Accordion => "Duxel.Accordion",
                UiAccessibilityRole.AccordionPanel => "Duxel.AccordionPanel",
                UiAccessibilityRole.Image => "Duxel.Image",
                UiAccessibilityRole.Card => "Duxel.Card",
                UiAccessibilityRole.AvatarGroup => "Duxel.AvatarGroup",
                UiAccessibilityRole.ComboBox => "Duxel.ComboBox",
                UiAccessibilityRole.ListBox => "Duxel.ListBox",
                UiAccessibilityRole.Tree => "Duxel.Tree",
                UiAccessibilityRole.TreeGrid => "Duxel.TreeGrid",
                UiAccessibilityRole.TreeGridRow => "Duxel.TreeGridRow",
                UiAccessibilityRole.TreeItem => "Duxel.TreeItem",
                UiAccessibilityRole.Option => "Duxel.Option",
                UiAccessibilityRole.Navigation => "Duxel.Navigation",
                UiAccessibilityRole.OrderedList => "Duxel.OrderedList",
                _ => throw new InvalidOperationException("Unknown accessibility role.")
            }),
            30013 => UiaVariant.String(node.GetHelpText()),
            30026 => UiaVariant.String(node.ItemStatus ?? string.Empty),
            30152 => UiaVariant.Integer(node.PositionInSet),
            30153 => UiaVariant.Integer(node.SizeOfSet),
            30157 when node.Role == UiAccessibilityRole.Navigation => UiaVariant.Integer(80003),
            30101 when node.Role == UiAccessibilityRole.Navigation => UiaVariant.String("navigation"),
            30101 when node.Role == UiAccessibilityRole.OrderedList => UiaVariant.String("list"),
            30101 when node.Role == UiAccessibilityRole.Toolbar => UiaVariant.String("toolbar"),
            30101 when node.Role == UiAccessibilityRole.Dialog => UiaVariant.String("dialog"),
            30101 when node.Role == UiAccessibilityRole.AlertDialog => UiaVariant.String("alertdialog"),
            30102 when node.Modal || node.Busy || node.IsCurrent || node.Role is UiAccessibilityRole.Dialog or UiAccessibilityRole.AlertDialog
                => UiaVariant.String(AriaProperties(node)),
            30101 when node.Role == UiAccessibilityRole.Group => UiaVariant.String("group"),
            30101 when node.Role == UiAccessibilityRole.Note => UiaVariant.String("note"),
            30101 when node.Role == UiAccessibilityRole.SpinButton => UiaVariant.String("spinbutton"),
            30101 when node.Role == UiAccessibilityRole.List => UiaVariant.String("list"),
            30101 when node.Role == UiAccessibilityRole.TreeGrid => UiaVariant.String("treegrid"),
            30101 when node.Role == UiAccessibilityRole.TreeGridRow => UiaVariant.String("row"),
            30101 when node.Role == UiAccessibilityRole.Grid => UiaVariant.String("grid"),
            30101 when node.Role == UiAccessibilityRole.Row => UiaVariant.String("row"),
            30101 when node.Role == UiAccessibilityRole.GridCell => UiaVariant.String("gridcell"),
            30101 when node.Role == UiAccessibilityRole.SearchBox => UiaVariant.String("searchbox"),
            30101 when node.Role == UiAccessibilityRole.ComboBox => UiaVariant.String("combobox"),
            30101 when node.Role == UiAccessibilityRole.ListBox => UiaVariant.String("listbox"),
            30101 when node.Role == UiAccessibilityRole.Option => UiaVariant.String("option"),
            30018 => RelatedElement(node.LabeledBy),
            30104 => ControlledElement(node.ControlledElementId),
            30101 when node.Role == UiAccessibilityRole.TabPanel => UiaVariant.String("tabpanel"),
            30016 => UiaVariant.Boolean(node.Role is not (UiAccessibilityRole.TreeItem or UiAccessibilityRole.TreeGridRow) || node.VirtualItem is not { IsPlaceholder : true }),
            30017 => UiaVariant.Boolean((node.Role is not (UiAccessibilityRole.TreeItem or UiAccessibilityRole.TreeGridRow) || node.VirtualItem is not { IsPlaceholder : true }) && (node.IsCurrent || node.Role != UiAccessibilityRole.Label) && (node.Role != UiAccessibilityRole.Tooltip || node.Focusable)),
            30019 => UiaVariant.Boolean(false),
            30022 => UiaVariant.Boolean(node.Offscreen),
            30024 => UiaVariant.String("Duxel"),
            30025 => UiaVariant.Boolean(node.Required),
            30103 => UiaVariant.Boolean(!node.Invalid),
            30031 => UiaVariant.Boolean(node.SupportsInvoke),
            30043 => UiaVariant.Boolean(node.SupportsValue),
            30034 => UiaVariant.Boolean(node.Scroll.HasValue),
            30028 => UiaVariant.Boolean(node.Expansion.HasValue),
            30070 => UiaVariant.Integer((int)(node.Expansion ?? UiAccessibilityExpansionState.LeafNode)),
            30041 => UiaVariant.Boolean(node.ToggleState.HasValue),
            30033 => UiaVariant.Boolean(node.Range.HasValue),
            30030 => UiaVariant.Boolean(node.Grid.HasValue),
            30029 => UiaVariant.Boolean(node.GridItem.HasValue),
            30062 when node.Grid is { } grid => UiaVariant.Integer(grid.RowCount),
            30063 when node.Grid is { } grid => UiaVariant.Integer(grid.ColumnCount),
            30064 when node.GridItem is { } item => UiaVariant.Integer(item.Row),
            30065 when node.GridItem is { } item => UiaVariant.Integer(item.Column),
            30066 when node.GridItem is { } item => UiaVariant.Integer(item.RowSpan),
            30067 when node.GridItem is { } item => UiaVariant.Integer(item.ColumnSpan),
            30068 when node.GridItem is { } item => RelatedElement(item.GridId),
            30047 when node.Range is { } range => UiaVariant.Double(range.Value),
            30048 when node.Range.HasValue => UiaVariant.Boolean(node.ReadOnly),
            30049 when node.Range is { } range => UiaVariant.Double(range.Minimum),
            30050 when node.Range is { } range => UiaVariant.Double(range.Maximum),
            30051 when node.Range is { } range => UiaVariant.Double(range.LargeChange),
            30052 when node.Range is { } range => UiaVariant.Double(range.SmallChange),
            30023 when node.Orientation != UiAccessibilityOrientation.Unspecified => UiaVariant.Integer((int)node.Orientation),
            30023 when node.Range is { } range => UiaVariant.Integer(range.Vertical ? 2 : 1),
            30086 when node.ToggleState is { } toggleState => UiaVariant.Integer((int)toggleState),
            30036 => UiaVariant.Boolean(node.SupportsSelectionItem),
            30037 => UiaVariant.Boolean(node.SupportsSelection),
            30079 when node.SupportsSelectionItem => UiaVariant.Boolean(node.Selected == true),
            30060 when node.SupportsSelection => UiaVariant.Boolean(node.CanSelectMultiple),
            30061 when node.SupportsSelection => UiaVariant.Boolean(node.SelectionRequired),
            30035 => UiaVariant.Boolean(node.CanScrollIntoView),
            30108 => UiaVariant.Boolean(node.VirtualItemContainer),
            30109 => UiaVariant.Boolean(node.VirtualItem is { IsPlaceholder: true }),
            30045 when node.SupportsValue => UiaVariant.String(node.Value ?? string.Empty),
            30053 when node.Scroll is not null || node.Role is UiAccessibilityRole.Pane or UiAccessibilityRole.Tooltip or UiAccessibilityRole.Menu => UiaVariant.Double(node.Scroll.GetValueOrDefault().HorizontalScrollPercent),
            30054 when node.Scroll is not null || node.Role is UiAccessibilityRole.Pane or UiAccessibilityRole.Tooltip or UiAccessibilityRole.Menu => UiaVariant.Double(node.Scroll.GetValueOrDefault().HorizontalViewSize),
            30055 when node.Scroll is not null || node.Role is UiAccessibilityRole.Pane or UiAccessibilityRole.Tooltip or UiAccessibilityRole.Menu => UiaVariant.Double(node.Scroll.GetValueOrDefault().VerticalScrollPercent),
            30056 when node.Scroll is not null || node.Role is UiAccessibilityRole.Pane or UiAccessibilityRole.Tooltip or UiAccessibilityRole.Menu => UiaVariant.Double(node.Scroll.GetValueOrDefault().VerticalViewSize),
            30057 when node.Scroll is not null || node.Role is UiAccessibilityRole.Pane or UiAccessibilityRole.Tooltip or UiAccessibilityRole.Menu => UiaVariant.Boolean(node.Scroll.GetValueOrDefault().HorizontallyScrollable),
            30058 when node.Scroll is not null || node.Role is UiAccessibilityRole.Pane or UiAccessibilityRole.Tooltip or UiAccessibilityRole.Menu => UiaVariant.Boolean(node.Scroll.GetValueOrDefault().VerticallyScrollable),
            30046 when node.SupportsValue => UiaVariant.Boolean(node.ReadOnly),
            _ => default
        };
        return 0;
    }
    public virtual int GetHostProvider(out nint value)
    {
        value = 0;
        // Child identity has no HWND host; the root association expires with its host.
        if (Id is not null)
            return 0;
        return Read(out _) ? UiaNative.UiaHostProviderFromHwnd(Host.Window, out value) : UiaNative.Unavailable;
    }
    public virtual int Navigate(int direction, out nint value)
    {
        value = 0;
        if (!Read(out var entry))
            return UiaNative.Unavailable;
        string? target;
        if (Id is null)
            target = direction switch
            {
                3 => Host.Tree.Root.First,
                4 => Host.Tree.Root.Last,
                _ => null
            };
        else
        {
            if (direction == 0 && entry.Node.ParentId.Length == 0)
            {
                value = UiaNative.Pointer<IUiaFragment>(Host.Root);
                return 0;
            }
            target = direction switch
            {
                0 => entry.Node.ParentId,
                1 => entry.Next,
                2 => entry.Previous,
                3 => entry.FirstChild,
                4 => entry.LastChild,
                _ => null
            };
        }
        if (direction is < 0 or > 4)
            return UiaNative.InvalidArgument;
        if (target is not null && Host.Find(target) is { } node)
            value = UiaNative.Pointer<IUiaFragment>(node);
        return 0;
    }
    public int GetRuntimeId(out nint value)
    {
        // Identity remains available while UI Automation disconnects a retired provider.
        value = Id is null ? 0 : UiaNative.RuntimeId(RuntimeId);
        return Id is not null || Read(out _) ? 0 : UiaNative.Unavailable;
    }
    public virtual int GetBounds(out UiaBounds value)
    {
        value = default;
        if (!Read(out var entry))
            return UiaNative.Unavailable;
        value = Host.Bounds(Id is null ? null : entry.Node.Bounds);
        return 0;
    }
    public int GetEmbeddedRoots(out nint value)
    {
        value = 0;
        return Read(out _) ? 0 : UiaNative.Unavailable;
    }
    public virtual int SetFocus() => !Read(out _) ? UiaNative.Unavailable : Id is null ? 0
        : WindowsAccessibility.Result(Host.Tree.RequestAction(Id, RuntimeId, UiAccessibilityAction.Focus));
    public virtual int GetFragmentRoot(out nint value)
    {
        // UIA needs the original fragment owner to resolve a retired runtime identifier.
        value = 0;
        if (Id is null && !Read(out _))
            return UiaNative.Unavailable;
        value = UiaNative.Pointer<IUiaFragmentRoot>(Host.Root);
        return 0;
    }
}
internal abstract partial class UiaControlProvider(WindowsAccessibility host, string id, long runtimeId)
    : UiaElementProvider(host, id, runtimeId), IUiaInvoke, IUiaValue, IUiaScroll, IUiaScrollItem, IUiaItemContainer, IUiaVirtualizedItem, IUiaSelection, IUiaSelectionItem, IUiaExpandCollapse, IUiaToggle, IUiaRangeValue, IUiaGrid, IUiaGridItem
{
    public override int GetPatternProvider(int pattern, out nint value)
    {
        value = 0;
        if (!Read(out var entry))
            return UiaNative.Unavailable;
        if (pattern == 10000 && entry.Node.SupportsInvoke)
            value = UiaNative.Pointer<IUiaInvoke>(this);
        else if (pattern == 10002 && entry.Node.SupportsValue)
            value = UiaNative.Pointer<IUiaValue>(this);
        else if (pattern == 10004 && entry.Node.Scroll is not null && !entry.Node.Offscreen)
            value = UiaNative.Pointer<IUiaScroll>(this);
        else if (pattern == 10017 && entry.Node.CanScrollIntoView)
            value = UiaNative.Pointer<IUiaScrollItem>(this);
        else if (pattern == 10019 && entry.Node.VirtualItemContainer)
            value = UiaNative.Pointer<IUiaItemContainer>(this);
        else if (pattern == 10020 && entry.Node.VirtualItem is { IsPlaceholder: true })
            value = UiaNative.Pointer<IUiaVirtualizedItem>(this);
        else if (pattern == 10001 && entry.Node.SupportsSelection)
            value = UiaNative.Pointer<IUiaSelection>(this);
        else if (pattern == 10010 && entry.Node.SupportsSelectionItem)
            value = UiaNative.Pointer<IUiaSelectionItem>(this);
        else if (pattern == 10005 && entry.Node.Expansion is not null)
            value = UiaNative.Pointer<IUiaExpandCollapse>(this);
        else if (pattern == 10015 && entry.Node.ToggleState is not null)
            value = UiaNative.Pointer<IUiaToggle>(this);
        else if (pattern == 10003 && entry.Node.Range is not null)
            value = UiaNative.Pointer<IUiaRangeValue>(this);
        else if (pattern == 10006 && entry.Node.Grid is not null)
            value = UiaNative.Pointer<IUiaGrid>(this);
        else if (pattern == 10007 && entry.Node.GridItem is not null)
            value = UiaNative.Pointer<IUiaGridItem>(this);
        return 0;
    }
    public int Invoke() => WindowsAccessibility.Result(Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.Invoke, wait: false));
    public int SetValue(nint value) => value == 0 ? UiaNative.InvalidArgument : WindowsAccessibility.Result(Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.SetValue, Marshal.PtrToStringUni(value)));
    public int GetValue(out nint value)
    {
        value = 0;
        if (!Read(out var entry))
            return UiaNative.Unavailable;
        if (!entry.Node.SupportsValue)
            return UiaNative.InvalidOperation;
        value = UiaNative.Bstr(entry.Node.Value ?? string.Empty);
        return 0;
    }
    public int GetReadOnly(out int value)
    {
        value = 0;
        if (!Read(out var entry))
            return UiaNative.Unavailable;
        value = entry.Node.ReadOnly ? 1 : 0;
        return 0;
    }

    public int Scroll(int horizontalAmount, int verticalAmount) => WindowsAccessibility.Result(Host.Tree.RequestScroll(Id!, RuntimeId,
        UiAccessibilityScrollCommand.ByAmount((UiAccessibilityScrollAmount)horizontalAmount, (UiAccessibilityScrollAmount)verticalAmount)));

    public int SetScrollPercent(double horizontalPercent, double verticalPercent) => WindowsAccessibility.Result(
        Host.Tree.RequestScroll(Id!, RuntimeId, UiAccessibilityScrollCommand.ByPercent(horizontalPercent, verticalPercent)));

    public int ScrollIntoView() => WindowsAccessibility.Result(
        Host.Tree.RequestAction(Id!, RuntimeId, UiAccessibilityAction.ScrollIntoView));

    public unsafe int FindItemByProperty(nint startAfter, int propertyId, UiaVariant value, out nint found)
    {
        found = 0;
        if (!Read(out var pane))
            return UiaNative.Unavailable;
        if (!pane.Node.VirtualItemContainer)
            return UiaNative.InvalidOperation;

        UiVirtualItemSearchProperty property;
        string? text = null;
        switch (propertyId)
        {
            case 0:
                property = UiVirtualItemSearchProperty.Next;
                break;
            case 30005 or 30011 when value.Type == 8:
                property = propertyId == 30005 ? UiVirtualItemSearchProperty.Name : UiVirtualItemSearchProperty.AutomationId;
                text = value.Data.Pointer == 0 ? string.Empty : Marshal.PtrToStringBSTR(value.Data.Pointer);
                break;
            default:
                return UiaNative.InvalidArgument;
        }

        UiAccessibilityItemReference? reference = null;
        if (startAfter != 0)
        {
            if (ComInterfaceMarshaller<IUiaSimple>.ConvertToManaged((void*)startAfter) is not UiaControlProvider start
                || start.Id is not { } startId || !ReferenceEquals(start.Host, Host))
                return UiaNative.InvalidArgument;
            reference = new(startId, start.RuntimeId);
        }

        var result = Host.Tree.FindVirtualItem(Id!, RuntimeId, property, text, reference, out var foundId);
        if (result != UiAccessibilityResult.Success || foundId is null)
            return WindowsAccessibility.Result(result);
        if (Host.Find(foundId) is not { } item)
            return UiaNative.Unavailable;
        found = UiaNative.Pointer<IUiaSimple>(item);
        return 0;
    }

    public int Realize()
    {
        if (!Read(out var entry))
            return UiaNative.Unavailable;
        if (entry.Node.VirtualItem is not { } item)
            return UiaNative.InvalidOperation;
        if (!item.IsPlaceholder && !entry.Node.Offscreen)
            return 0;
        if (!Host.Tree.TryGet(item.ContainerId ?? entry.Node.ParentId, out var pane))
            return UiaNative.Unavailable;
        return WindowsAccessibility.Result(Host.Tree.RealizeVirtualItem(pane.Node.Id, pane.RuntimeId, new(Id!, RuntimeId)));
    }

    private int ReadScroll(out UiAccessibilityScrollInfo value)
    {
        value = default;
        if (!Read(out var entry) || entry.Node.Offscreen)
            return UiaNative.Unavailable;
        if (entry.Node.Scroll is not { } scroll)
            return UiaNative.InvalidOperation;
        value = scroll;
        return 0;
    }

    public int GetHorizontalScrollPercent(out double value)
    {
        var result = ReadScroll(out var scroll);
        value = scroll.HorizontalScrollPercent;
        return result;
    }
    public int GetVerticalScrollPercent(out double value)
    {
        var result = ReadScroll(out var scroll);
        value = scroll.VerticalScrollPercent;
        return result;
    }
    public int GetHorizontalViewSize(out double value)
    {
        var result = ReadScroll(out var scroll);
        value = scroll.HorizontalViewSize;
        return result;
    }
    public int GetVerticalViewSize(out double value)
    {
        var result = ReadScroll(out var scroll);
        value = scroll.VerticalViewSize;
        return result;
    }
    public int GetHorizontallyScrollable(out int value)
    {
        var result = ReadScroll(out var scroll);
        value = scroll.HorizontallyScrollable ? 1 : 0;
        return result;
    }
    public int GetVerticallyScrollable(out int value)
    {
        var result = ReadScroll(out var scroll);
        value = scroll.VerticallyScrollable ? 1 : 0;
        return result;
    }
}
[GeneratedComClass]
internal sealed partial class UiaNodeProvider(WindowsAccessibility host, string id, long runtimeId)
    : UiaControlProvider(host, id, runtimeId);

[GeneratedComClass]
internal sealed partial class UiaEventProvider(WindowsAccessibility host, UiAccessibilityElement snapshot,
    UiaElementProvider parent, UiaEventRootProvider root) : UiaElementProvider(host, snapshot.Node.Id, snapshot.RuntimeId), IUiaSelectionItem, IUiaToggle
{
    private readonly UiaBounds _bounds = host.Bounds(snapshot.Node.Bounds);
    private readonly bool _focused = snapshot.Node.Focused && host.HasFocus;
    internal string SourceId => snapshot.Node.Id;
    protected override bool Read(out UiAccessibilityElement element)
    {
        element = snapshot;
        return true;
    }
    public override int GetPatternProvider(int pattern, out nint value)
    {
        value = pattern switch
        {
            10010 when snapshot.Node.SupportsSelectionItem => UiaNative.Pointer<IUiaSelectionItem>(this),
            10015 when snapshot.Node.ToggleState.HasValue => UiaNative.Pointer<IUiaToggle>(this),
            _ => 0
        };
        return 0;
    }
    public int Toggle() => UiaNative.Unavailable;
    public int GetToggleState(out int value)
    {
        value = 0;
        if (snapshot.Node.ToggleState is not { } state)
            return UiaNative.InvalidOperation;
        value = (int)state;
        return 0;
    }
    public int Select() => UiaNative.Unavailable;
    public int AddToSelection() => UiaNative.Unavailable;
    public int RemoveFromSelection() => UiaNative.Unavailable;
    public int GetIsSelected(out int value)
    {
        value = snapshot.Node.Selected == true ? 1 : 0;
        return snapshot.Node.SupportsSelectionItem ? 0 : UiaNative.InvalidOperation;
    }
    public int GetSelectionContainer(out nint value)
    {
        value = snapshot.Node.SupportsSelectionItem ? UiaNative.Pointer<IUiaSimple>(parent) : 0;
        return snapshot.Node.SupportsSelectionItem ? 0 : UiaNative.InvalidOperation;
    }
    public override int GetPropertyValue(int property, out UiaVariant value)
    {
        if (property == 30008)
        {
            value = UiaVariant.Boolean(_focused);
            return 0;
        }
        if (property is 30029 or 30030 or 30031 or 30034 or 30035 or 30043 or 30108 or 30109)
        {
            value = UiaVariant.Boolean(false);
            return 0;
        }
        return base.GetPropertyValue(property, out value);
    }
    public override int Navigate(int direction, out nint value)
    {
        value = 0;
        if (direction is < 0 or > 4)
        {
            return UiaNative.InvalidArgument;
        }
        if (direction == 0)
            value = UiaNative.Pointer<IUiaFragment>(parent);
        return 0;
    }
    public override int GetBounds(out UiaBounds value)
    {
        value = _bounds;
        return 0;
    }
    public override int SetFocus() => UiaNative.Unavailable;
    public override int GetFragmentRoot(out nint value)
    {
        value = UiaNative.Pointer<IUiaFragmentRoot>(root);
        return 0;
    }
}
[GeneratedComClass]
internal sealed partial class UiaRootProvider(WindowsAccessibility host) : UiaElementProvider(host, null, 0), IUiaFragmentRoot
{
    public int ElementFromPoint(double x, double y, out nint value)
    {
        value = 0;
        if (!Host.Live)
            return UiaNative.Unavailable;
        var rootBounds = Host.Bounds();
        if (!Contains(rootBounds, x, y))
            return 0;
        var target = FindAt(Host.Tree.Root.Last, x, y);
        value = target is null ? UiaNative.Pointer<IUiaFragment>(this) : UiaNative.Pointer<IUiaFragment>(target);
        return 0;
    }
    private UiaNodeProvider? FindAt(string? id, double x, double y)
    {
        while (id is not null && Host.Tree.TryGet(id, out var entry))
        {
            var bounds = Host.Bounds(entry.Node.Bounds);
            if (!entry.Node.Offscreen && Contains(bounds, x, y)
                && (entry.Node.GetPointGeometry() is not { } geometry || geometry.ContainsPoint(
                    (float)(entry.Node.Bounds.X + (x - bounds.X) * entry.Node.Bounds.Width / bounds.Width),
                    (float)(entry.Node.Bounds.Y + (y - bounds.Y) * entry.Node.Bounds.Height / bounds.Height))))
                return FindAt(entry.LastChild, x, y) ?? Host.Find(id);
            id = entry.Previous;
        }
        return null;
    }
    private static bool Contains(UiaBounds bounds, double x, double y) => x >= bounds.X && y >= bounds.Y && x < bounds.X + bounds.Width && y < bounds.Y + bounds.Height;
    public int GetFocus(out nint value)
    {
        value = 0;
        if (!Host.Live)
            return UiaNative.Unavailable;
        if (Host.HasFocus)
            value = Host.Tree.Root.Focus is { } id && Host.Find(id) is { } node
                ? UiaNative.Pointer<IUiaFragment>(node) : UiaNative.Pointer<IUiaFragment>(this);
        return 0;
    }
}
