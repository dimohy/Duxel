using System.Collections.Concurrent;

namespace Duxel.Core;

public enum UiAccessibilityRole
{
    Button,
    Edit,
    ProgressBar,
    Tooltip,
    Pane,
    ListItem,
    TabList,
    Tab,
    TabPanel,
    Menu,
    MenuItem,
    MenuGroup,
    Slider,
    Checkbox,
    Switch,
    RadioGroup,
    RadioButton,
    Label,
    Accordion,
    AccordionPanel,
    Image,
    Card,
    Navigation,
    OrderedList,
    AvatarGroup,
    ComboBox,
    ListBox,
    Option,
    Toolbar,
    Group,
    SearchBox,
    Dialog,
    Note,
    SpinButton,
    List,
    Grid,
    Row,
    GridCell,
    Link,
    AlertDialog,
    Tree,
    TreeItem,
    TreeGrid,
    TreeGridRow
}
public enum UiAccessibilityOrientation
{
    Unspecified, Horizontal, Vertical
}

public enum UiAccessibilityEventKind
{
    TooltipOpened,
    TooltipClosed,
    MenuOpened,
    MenuClosed,
    Invoked
}
public readonly record struct UiAccessibilityEvent(UiAccessibilityEventKind Kind, UiAccessibilityElement Element)
{
    /// <summary>Indicates that the tree is notifying observers before disposal.</summary>
    public bool DuringDisposal { get; init; }
}

public enum UiAccessibilityTextProperty
{
    Name,
    HelpText,
    ItemStatus,
    Value
}

public readonly record struct UiAccessibilityTextChange(UiAccessibilityTextProperty Property, UiAccessibilityElement Element,
    string OldValue, string NewValue);

public enum UiAccessibilitySelectionEventKind
{
    Selected, Removed, Invalidated, Added
}
public readonly record struct UiAccessibilitySelectionChange(UiAccessibilityElement Element, bool? Previous,
    UiAccessibilitySelectionEventKind? Event)
{
    public bool? PreviousRequired
    {
        get; init;
    }
    public bool? PreviousMultiple
    {
        get; init;
    }
}

/// <summary>Scroll metrics before and after publication for the same live element.</summary>
public readonly record struct UiAccessibilityScrollChange(UiAccessibilityElement Element, UiAccessibilityScrollInfo? Previous);

/// <summary>Bounds and visibility before and after publication for the same live element.</summary>
public readonly record struct UiAccessibilityGeometryChange(UiAccessibilityElement Element, UiRect PreviousBounds, bool WasOffscreen);
public enum UiAccessibilityAction
{
    Focus, Invoke, SetValue, Scroll, ScrollIntoView, FindVirtualItem, RealizeVirtualItem, Select, Deselect, Expand, Collapse, Toggle, SetRangeValue, AddToSelection, SetTextSelection, EditText, ScrollText
}
public enum UiAccessibilityResult
{
    Success, Unavailable, Disabled, ReadOnly, InvalidArgument, Busy, Timeout, Failed, InvalidOperation
}

public enum UiAccessibilityScrollAmount
{
    LargeDecrement = 0,
    SmallDecrement = 1,
    NoAmount = 2,
    LargeIncrement = 3,
    SmallIncrement = 4
}

public readonly record struct UiAccessibilityScrollCommand
{
    private readonly bool _initialized;

    private UiAccessibilityScrollCommand(bool isPercent, double horizontal, double vertical)
    {
        _initialized = true;
        IsPercent = isPercent;
        Horizontal = horizontal;
        Vertical = vertical;
    }

    internal bool IsPercent
    {
        get;
    }
    internal double Horizontal
    {
        get;
    }
    internal double Vertical
    {
        get;
    }

    public static UiAccessibilityScrollCommand ByAmount(UiAccessibilityScrollAmount horizontal, UiAccessibilityScrollAmount vertical) =>
        new(false, (int)horizontal, (int)vertical);

    public static UiAccessibilityScrollCommand ByPercent(double horizontal, double vertical) => new(true, horizontal, vertical);

    internal bool IsValid => _initialized && (IsPercent
        ? ValidPercent(Horizontal) && ValidPercent(Vertical)
        : Horizontal is >= 0 and <= 4 && Vertical is >= 0 and <= 4);

    internal bool CanApply(in UiAccessibilityScrollInfo scroll)
    {
        var unchanged = IsPercent ? -1 : (int)UiAccessibilityScrollAmount.NoAmount;
        return (Horizontal == unchanged || scroll.HorizontallyScrollable)
            && (Vertical == unchanged || scroll.VerticallyScrollable);
    }

    internal UiVector2 Apply(in UiAccessibilityScrollInfo scroll, float step) => new(
        (float)ApplyHorizontal(scroll, step),
        (float)ApplyVertical(scroll, step));

    internal double ApplyHorizontal(in UiAccessibilityScrollInfo scroll, float step) =>
        ApplyAxis(Horizontal, scroll.HorizontalOffset, scroll.Viewport.X, scroll.HorizontalMaximum, step, scroll.RightToLeft);

    internal double ApplyVirtualHorizontal(in UiAccessibilityScrollInfo scroll, float step) =>
        ApplyAxis(Horizontal, scroll.HorizontalRevealOffset, scroll.Viewport.X, scroll.HorizontalMaximum, step, reverse : false);

    internal double ApplyVertical(in UiAccessibilityScrollInfo scroll, float step) =>
        ApplyAxis(Vertical, scroll.VerticalOffset, scroll.Viewport.Y, scroll.VerticalMaximum, step, reverse: false);

    private static bool ValidPercent(double value) => value == -1 || value is >= 0 and <= 100;

    private double ApplyAxis(double amount, double offset, float viewport, double maximum, float step, bool reverse)
    {
        if (IsPercent)
        {
            return amount == -1 ? offset : (reverse ? 100 - amount : amount) / 100 * maximum;
        }
        var delta = (UiAccessibilityScrollAmount)amount switch
        {
            UiAccessibilityScrollAmount.LargeDecrement => -viewport,
            UiAccessibilityScrollAmount.SmallDecrement => -step,
            UiAccessibilityScrollAmount.LargeIncrement => viewport,
            UiAccessibilityScrollAmount.SmallIncrement => step,
            _ => 0
        };
        return Math.Clamp(offset + (reverse ? -delta : delta), 0, maximum);
    }
}

public readonly record struct UiAccessibilityScrollInfo(UiVector2 Viewport, UiVector2 Content, UiVector2 Offset)
{
    /// <summary>Measures horizontal percentages from the right while Offset remains in physical coordinates.</summary>
    public bool RightToLeft
    {
        get; init;
    }
    internal (double Content, double Offset)? VirtualVertical
    {
        get; init;
    }
    internal (double Content, double Offset)? VirtualHorizontal { get; init; }
    public double HorizontalContentWidth => VirtualHorizontal?.Content ?? Content.X;
    public double HorizontalOffset => ReversesHorizontalReveal ? HorizontalMaximum - HorizontalRevealOffset : HorizontalRevealOffset;
    internal double HorizontalRevealOffset => VirtualHorizontal?.Offset ?? Offset.X;
    internal bool ReversesHorizontalReveal => RightToLeft && VirtualHorizontal is not null;
    public double VerticalContentHeight => VirtualVertical?.Content ?? Content.Y;
    public double VerticalOffset => VirtualVertical?.Offset ?? Offset.Y;
    internal double HorizontalMaximum => VirtualHorizontal is { } range
        ? Math.Max(0, range.Content - Viewport.X) : MathF.Max(0, Content.X - Viewport.X);
    internal double VerticalMaximum => VirtualVertical is { } range
        ? Math.Max(0, range.Content - Viewport.Y) : MathF.Max(0, Content.Y - Viewport.Y);
    public bool HorizontallyScrollable => HorizontalContentWidth > Viewport.X;
    public bool VerticallyScrollable => VerticalContentHeight > Viewport.Y;
    public double HorizontalScrollPercent => HorizontallyScrollable
        ? (RightToLeft && VirtualHorizontal is null ? HorizontalMaximum - HorizontalRevealOffset : HorizontalRevealOffset)
            / HorizontalMaximum * 100 : -1;
    public double VerticalScrollPercent => VerticallyScrollable ? VerticalOffset / VerticalMaximum * 100 : -1;
    public double HorizontalViewSize => HorizontallyScrollable ? Viewport.X / HorizontalContentWidth * 100 : 100;
    public double VerticalViewSize => VerticallyScrollable ? Viewport.Y / VerticalContentHeight * 100 : 100;

    internal static UiAccessibilityScrollInfo VirtualList(UiVector2 viewport, double extent, double offset, bool horizontal, bool rightToLeft)
    {
        var summarizedExtent = (float)Math.Min(extent, float.MaxValue);
        var physicalOffset = rightToLeft ? Math.Max(0, extent - viewport.X) - offset : offset;
        var summarizedOffset = (float)Math.Min(physicalOffset, float.MaxValue);
        return horizontal
            ? new(viewport, new(summarizedExtent, viewport.Y), new(summarizedOffset, 0))
            { RightToLeft = rightToLeft, VirtualHorizontal = (extent, offset) }
            : new(viewport, new(viewport.X, summarizedExtent), new(0, summarizedOffset)) { VirtualVertical = (extent, offset) };
    }

    internal static UiAccessibilityScrollInfo VirtualGrid(UiVector2 viewport, double width, double height,
        double x, double y, bool rightToLeft)
    {
        var physicalX = rightToLeft ? Math.Max(0, width - viewport.X) - x : x;
        return new(viewport, new((float)Math.Min(width, float.MaxValue), (float)Math.Min(height, float.MaxValue)),
            new((float)Math.Min(physicalX, float.MaxValue), (float)Math.Min(y, float.MaxValue)))
        {
            RightToLeft = rightToLeft,
            VirtualHorizontal = (width, x),
            VirtualVertical = (height, y)
        };
    }
}
/// <summary>Text associated with a target independently of the tooltip's visual visibility.</summary>
public readonly record struct UiAccessibilityTooltip(string SourceId, string Text, UiTooltipRelationship Relationship);
public readonly record struct UiAccessibilityNode(string Id, string ParentId, UiAccessibilityRole Role, string Name, UiRect Bounds,
    bool Enabled, bool Offscreen, bool Focused, string? Value = null, bool ReadOnly = false, int MaximumLength = 0,
    string? Hint = null, string? ValidationMessage = null, bool Required = false, bool Invalid = false, bool Focusable = true, bool ValuePattern = true)
{
    internal IUiAccessibilityPointGeometry? PointGeometry
    {
        get; init;
    }
    public IUiAccessibilityPointGeometry? GetPointGeometry() => PointGeometry;
    public UiAccessibilityImageInfo? ImageInfo
    {
        get; init;
    }
    public bool Modal
    {
        get; init;
    }
    /// <summary>Content is being updated; distinct from a disabled or determinate control.</summary>
    public bool Busy
    {
        get; init;
    }
    /// <summary>A point within the rendered action surface, in logical UI coordinates.</summary>
    public UiVector2? ClickablePoint
    {
        get; init;
    }
    public UiAccessibilityOrientation Orientation
    {
        get; init;
    }
    public bool DisabledFocusable
    {
        get; init;
    }
    public bool CanReceiveFocus => Focusable && !Offscreen && (Enabled || DisabledFocusable);
    public int PositionInSet
    {
        get; init;
    }
    public int SizeOfSet
    {
        get; init;
    }
    public bool IsCurrent
    {
        get; init;
    }
    public string? ItemStatus
    {
        get; init;
    }
    public bool? Selected
    {
        get; init;
    }
    public string? SelectionContainerId { get; init; }
    public bool CanSelectMultiple
    {
        get; init;
    }
    public bool SelectionRequired
    {
        get; init;
    }
    public bool SupportsSelectionItem => Role is UiAccessibilityRole.Tab or UiAccessibilityRole.RadioButton or UiAccessibilityRole.Option
        || Role is UiAccessibilityRole.MenuItem or UiAccessibilityRole.Row or UiAccessibilityRole.GridCell or UiAccessibilityRole.TreeItem or UiAccessibilityRole.TreeGridRow && Selected.HasValue;
    public bool SupportsSelection => Role is UiAccessibilityRole.TabList or UiAccessibilityRole.MenuGroup or UiAccessibilityRole.RadioGroup or UiAccessibilityRole.ListBox or UiAccessibilityRole.ComboBox
        || Role is UiAccessibilityRole.Grid or UiAccessibilityRole.Tree or UiAccessibilityRole.TreeGrid && ListSelection;
    internal bool ListSelection
    {
        get; init;
    }
    /// <summary>Null means the Toggle pattern is unsupported; Indeterminate is a supported third state.</summary>
    public UiAccessibilityToggleState? ToggleState
    {
        get; init;
    }
    /// <summary>Two-state compatibility projection of ToggleState. Use ToggleState to distinguish mixed from unsupported.</summary>
    public bool? Checked
    {
        get => UiAccessibilityToggleStates.ToChecked(ToggleState);
        init => ToggleState = UiAccessibilityToggleStates.FromChecked(value);
    }
    public UiAccessibilityRangeInfo? Range
    {
        get; init;
    }
    public UiAccessibilityTextInfo? TextInfo
    {
        get; init;
    }
    public UiAccessibilityTextLayout? TextLayout
    {
        get; init;
    }
    public UiAccessibilityTextAppearance? TextAppearance
    {
        get; init;
    }
    public UiAccessibilityGridInfo? Grid
    {
        get; init;
    }
    public UiAccessibilityGridItemInfo? GridItem
    {
        get; init;
    }
    public UiAccessibilityExpansionState? Expansion
    {
        get; init;
    }
    /// <summary>Allows a non-button action control, such as a tab overflow trigger, to expose Invoke.</summary>
    public bool InvokePattern
    {
        get; init;
    }
    public bool SupportsInvoke => InvokePattern || Role is UiAccessibilityRole.MenuItem or UiAccessibilityRole.Link
        || Role == UiAccessibilityRole.Button && !ToggleState.HasValue;
    public bool SupportsValue => ValuePattern && Role is UiAccessibilityRole.Edit or UiAccessibilityRole.ComboBox
        or UiAccessibilityRole.SearchBox or UiAccessibilityRole.SpinButton or UiAccessibilityRole.Link;
    public string? LabeledBy
    {
        get; init;
    }
    /// <summary>The single rendered body controlled by this element.</summary>
    public string? ControlledElementId
    {
        get; init;
    }
    /// <summary>Allows line breaks in Value actions without relaxing single-line edit validation.</summary>
    public bool Multiline
    {
        get; init;
    }
    public UiAccessibilityTooltip? Tooltip
    {
        get; init;
    }
    public UiAccessibilityScrollInfo? Scroll
    {
        get; init;
    }
    public bool CanScrollIntoView
    {
        get; internal init;
    }
    public bool VirtualItemContainer
    {
        get; internal init;
    }
    public UiAccessibilityVirtualItem? VirtualItem
    {
        get; internal init;
    }

    public string GetHelpText()
    {
        var text = Hint is null ? ValidationMessage ?? string.Empty
            : ValidationMessage is null ? Hint : Hint + " " + ValidationMessage;
        if (Tooltip is not { Relationship: UiTooltipRelationship.Description } tooltip)
        {
            return text;
        }
        return text.Length == 0 ? tooltip.Text : text + " " + tooltip.Text;
    }
}
public readonly record struct UiAccessibilityElement(UiAccessibilityNode Node, long RuntimeId, string? Previous, string? Next, string? FirstChild, string? LastChild);

/// <summary>Single render writer and concurrent provider readers. Publication updates only changed entries.</summary>
public sealed partial class UiAccessibilityTree : IDisposable
{
    private sealed class Entry(long runtimeId)
    {
        internal readonly long RuntimeId = runtimeId;
        internal UiAccessibilityNode Staged;
        internal UiAccessibilityElement Published;
        internal UiAccessibilityElement Candidate;
        internal bool HasPublished;
        internal ulong Seen;
        internal UiAccessibilityTextAppearance? TextAppearance;
        internal UiAccessibilityTextFontKey? TextFontKey;
        internal UiAccessibilityStyledPaintKey? StyledPaintKey;
        internal UiVisualTextLines? UnwrappedTextLines;
        internal InputTextScrollRetention? InputTextScroll;
        internal UiRect? TextViewport;
        internal ulong TextPaintGeneration;
        internal string? Previous, Next, First, Last;
        internal UiRect? RevealBounds;
        internal ScrollViewport? ScrollViewport;
        internal ulong PaneChildrenChanged;
    }
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiAccessibilityElement> _published = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];
    private readonly List<string> _removed = [];
    private readonly List<Entry> _updates = [];
    private readonly Dictionary<string, string> _lastByParent = new(StringComparer.Ordinal);
    private readonly ReaderWriterLockSlim _gate = new();
    private readonly ConcurrentQueue<Request> _incoming = new();
    private readonly List<Request> _pending = [];
    private Action? _wake;
    private ulong _generation;
    private long _nextRuntimeId;
    private int _queued, _writerThread;
    private bool _building, _disposed;
    private string? _first, _last, _focus;
    public long Revision
    {
        get; private set;
    }
    /// <summary>Raised after removed elements are unpublished, outside the publication lock.</summary>
    public event Action? ElementsRemoved;
    /// <summary>Close is delivered before unpublication; open follows publication, with no publication lock held.</summary>
    public event Action<UiAccessibilityEvent>? AutomationEvent;
    /// <summary>Text changes are delivered after publication, with no publication lock held.</summary>
    public event Action<UiAccessibilityTextChange>? TextPropertyChanged;
    /// <summary>Scroll changes are delivered after publication, with no publication lock held.</summary>
    public event Action<UiAccessibilityScrollChange>? ScrollChanged;
    /// <summary>Published geometry changes for retained owned panes and their rendered descendants.</summary>
    public event Action<UiAccessibilityGeometryChange>? ScrollGeometryChanged;
    /// <summary>Published changes to the direct children of a retained owned pane.</summary>
    public event Action<UiAccessibilityElement>? PaneChildrenChanged;
    /// <summary>Tab selection changes are delivered after publication, outside the publication lock.</summary>
    public event Action<UiAccessibilitySelectionChange>? SelectionChanged;
    private List<UiAccessibilityElement>? _closingTooltips;
    private List<UiAccessibilityElement>? _openingTooltips;
    private List<UiAccessibilityTextChange>? _textChanges;
    private List<UiAccessibilityScrollChange>? _scrollChanges;
    private List<UiAccessibilityGeometryChange>? _scrollGeometryChanges;
    private List<UiAccessibilityElement>? _paneChildrenChanges;
    private List<UiAccessibilitySelectionChange>? _selectionChanges;
    private Dictionary<string, string>? _selectedTabs;
    private Dictionary<string, int>? _selectedCounts;
    private bool _notifying;
    public int Count
    {
        get
        {
            _gate.EnterReadLock();
            try
            {
                return _published.Count;
            }
            finally { _gate.ExitReadLock(); }
        }
    }
    public void SetRequestFrameCallback(Action? callback) => Volatile.Write(ref _wake, callback);
    public bool TryGet(string id, out UiAccessibilityElement element)
    {
        element = default;
        _gate.EnterReadLock();
        try
        {
            return !_disposed && _published.TryGetValue(id, out element);
        }
        finally { _gate.ExitReadLock(); }
    }
    public (string? First, string? Last, string? Focus) Root
    {
        get
        {
            _gate.EnterReadLock();
            try
            {
                return (_first, _last, _focus);
            }
            finally { _gate.ExitReadLock(); }
        }
    }
    public bool IsDisposed => Volatile.Read(ref _disposed);

    internal void BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_notifying)
        {
            throw new InvalidOperationException("Accessibility publication cannot reenter an automation notification.");
        }
        if (_building)
            throw new InvalidOperationException("Accessibility frames cannot reenter.");
        var thread = Environment.CurrentManagedThreadId;
        if (_writerThread != 0 && _writerThread != thread)
            throw new InvalidOperationException("An accessibility tree requires one render writer.");
        _writerThread = thread;
        _building = true;
        _generation++;
        ClearAnnouncements();
        _order.Clear();
        while (_incoming.TryDequeue(out var request))
            _pending.Add(request);
    }
    internal void Add(UiAccessibilityNode node, UiRect? revealBounds = null)
    {
        node = MapWidgetNode(node, ref revealBounds);
        if (!_building)
            throw new InvalidOperationException("Accessibility capture requires an active frame.");
        ArgumentException.ThrowIfNullOrEmpty(node.Id);
        ArgumentNullException.ThrowIfNull(node.Name);
        node.Range?.Validate();
        node.ImageInfo?.Validate();
        if (node.TextLayout is { } layout && !ReferenceEquals(layout.Content, node.TextInfo?.Content))
            throw new InvalidOperationException("Published text layout must use the current text content instance.");
        if (node.TextAppearance is { } appearance && !ReferenceEquals(appearance.Content, node.TextInfo?.Content))
            throw new InvalidOperationException("Published text appearance must use the current text content instance.");
        if (node.TextInfo is { } text && node.SupportsValue && !string.Equals(node.Value, text.Content.Text, StringComparison.Ordinal))
            throw new InvalidOperationException("The published text state must match its value projection.");
        if (node.Tooltip is { } tooltip)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tooltip.SourceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(tooltip.Text);
            if (tooltip.Relationship is not (UiTooltipRelationship.Description or UiTooltipRelationship.Label))
            {
                throw new InvalidOperationException("Only accessible tooltip relationships can be published.");
            }
        }
        if (!float.IsFinite(node.Bounds.X) || !float.IsFinite(node.Bounds.Y) || !float.IsFinite(node.Bounds.Width) || !float.IsFinite(node.Bounds.Height) || node.Bounds.Width < 0 || node.Bounds.Height < 0)
            throw new InvalidOperationException("Accessibility bounds must be finite and nonnegative.");
        if (node.ClickablePoint is { } point && (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || node.Offscreen
            || point.X < node.Bounds.X || point.X >= node.Bounds.X + node.Bounds.Width
            || point.Y < node.Bounds.Y || point.Y >= node.Bounds.Y + node.Bounds.Height))
            throw new InvalidOperationException("An accessibility clickable point must lie inside its visible bounds.");
        if (node.Expansion is { } expansion && (uint)expansion > 3)
            throw new ArgumentOutOfRangeException(nameof(node));
        if (node.ToggleState is { } toggleState && (uint)toggleState > 2)
            throw new ArgumentOutOfRangeException(nameof(node));
        var entry = GetEntry(node.Id);
        if (entry.Seen == _generation)
            throw new InvalidOperationException("Accessibility control IDs must be unique within a frame: " + node.Id);
        entry.Seen = _generation;
        if (entry.TextPaintGeneration == _generation && ReferenceEquals(entry.TextAppearance?.Content, node.TextInfo?.Content))
            node = node with
            {
                TextAppearance = entry.TextAppearance
            };
        entry.Staged = node;
        entry.Previous = entry.Next = entry.First = entry.Last = null;
        _order.Add(node.Id);
        entry.RevealBounds = revealBounds;
        entry.ScrollViewport = null;
    }
    internal int StagedCount => _building ? _order.Count
        : throw new InvalidOperationException("Accessibility order requires an active frame.");

    internal void PromoteTrailingStagedRange(int start, int split)
    {
        if (!_building || start < 0 || split < start || split > _order.Count)
            throw new InvalidOperationException("Accessibility order range is outside the current frame.");
        _order.Reverse(start, split - start);
        _order.Reverse(split, _order.Count - split);
        _order.Reverse(start, _order.Count - start);
    }

    internal bool TryGetStaged(string id, out UiAccessibilityNode node)
    {
        node = default;
        if (_building && _entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
        {
            node = entry.Staged;
            return true;
        }
        return false;
    }
    internal void ConnectNavigationPage(string destinationId, string pageId)
    {
        if (!TryGetStaged(destinationId, out var destination) || destination.Role is not (UiAccessibilityRole.Link or UiAccessibilityRole.Button or UiAccessibilityRole.Label)
            || !TryGetStaged(pageId, out var page) || page.Role != UiAccessibilityRole.Pane)
            throw new InvalidOperationException("Navigation relationships require a destination and its page in the current frame.");
        _entries[destinationId].Staged = destination with { ControlledElementId = pageId };
    }
    internal void ConnectTabPanel(string tabId, string panelId)
    {
        if (!TryGetStaged(tabId, out var tab) || tab.Role != UiAccessibilityRole.Tab
            || !TryGetStaged(panelId, out var panel) || panel.Role != UiAccessibilityRole.TabPanel)
            throw new InvalidOperationException("Tab relationships require both controls in the current frame.");
        _entries[tabId].Staged = tab with
        {
            ControlledElementId = panelId
        };
    }
    internal void EndFrame(string? focusId = null, bool authoritativeFocus = false)
    {
        if (!_building)
            throw new InvalidOperationException("No accessibility frame is active.");
        if (_notifying)
        {
            throw new InvalidOperationException("Accessibility publication cannot reenter an automation notification.");
        }
        string? first = null, last = null, focus = null;
        foreach (var id in _order)
        {
            var entry = _entries[id];
            var parent = entry.Staged.ParentId;
            if (authoritativeFocus)
                entry.Staged = entry.Staged with
                {
                    Focused = id == focusId && entry.Staged.Focusable
                };
            ValidateRelationship(entry.Staged.LabeledBy);
            ValidateRelationship(entry.Staged.ControlledElementId);
            if (parent.Length != 0 && (!_entries.TryGetValue(parent, out var parentEntry) || parentEntry.Seen != _generation))
                throw new InvalidOperationException("Accessibility parent was not rendered.");
            if (_lastByParent.TryGetValue(parent, out var previous))
            {
                entry.Previous = previous;
                _entries[previous].Next = id;
            }
            _lastByParent[parent] = id;
            if (parent.Length == 0)
            {
                first ??= id;
                last = id;
            }
            else
            {
                var owner = _entries[parent];
                owner.First ??= id;
                owner.Last = id;
            }
            if (entry.Staged.Focused && entry.Staged.CanReceiveFocus)
            {
                if (focus is not null)
                    throw new InvalidOperationException($"An accessibility frame cannot focus both '{_entries[focus].Staged.Name}' and '{entry.Staged.Name}'.");
                focus = id;
            }
        }
        foreach (var pair in _entries)
        {
            if (pair.Value.Seen != _generation)
                _removed.Add(pair.Key);
        }
        foreach (var id in _order)
            ExposeScrollItem(_entries[id]);
        ProcessScrollIntoView();
        foreach (var id in _order)
        {
            var e = _entries[id];
            e.Candidate = new(e.Staged, e.RuntimeId, e.Previous, e.Next, e.First, e.Last);
            if (!e.HasPublished || e.Published != e.Candidate)
                _updates.Add(e);
        }
        var changed = _first != first || _last != last || _focus != focus || _removed.Count != 0 || _updates.Count != 0;
        if (changed)
            PrepareGridPublication();
        var events = AutomationEvent;
        var textObserver = TextPropertyChanged;
        var scrollObserver = ScrollChanged;
        var geometryObserver = ScrollGeometryChanged;
        var childrenObserver = PaneChildrenChanged;
        var treeChildrenObserver = TreeChildrenChanged;
        if (treeChildrenObserver is not null) CollectTreeChildrenChanges();
        var selectionObserver = SelectionChanged;
        var expansionObserver = ExpansionChanged;
        var toggleObserver = ToggleChanged;
        var rangeObserver = RangeChanged;
        var gridObserver = GridChanged;
        if (gridObserver is not null)
            foreach (var entry in _updates)
                if (entry.HasPublished && (entry.Published.Node.Grid != entry.Candidate.Node.Grid
                    || entry.Published.Node.GridItem != entry.Candidate.Node.GridItem))
                    (_gridChanges ??= []).Add(new(entry.Candidate, entry.Published.Node.Grid, entry.Published.Node.GridItem));
        if (rangeObserver is not null)
            foreach (var entry in _updates)
                if (entry.HasPublished && (entry.Published.Node.Range != entry.Candidate.Node.Range ||
                    entry.Candidate.Node.Range.HasValue && entry.Published.Node.ReadOnly != entry.Candidate.Node.ReadOnly))
                    (_rangeChanges ??= []).Add(new(entry.Candidate, entry.Published.Node.Range, entry.Published.Node.ReadOnly));
        if (toggleObserver is not null)
            foreach (var entry in _updates)
                if (entry.HasPublished && entry.Published.Node.ToggleState != entry.Candidate.Node.ToggleState)
                    (_toggleChanges ??= []).Add(UiAccessibilityToggleChange.FromStates(entry.Candidate, entry.Published.Node.ToggleState));
        if (expansionObserver is not null)
            foreach (var entry in _updates)
                if (entry.HasPublished && entry.Published.Node.Expansion != entry.Candidate.Node.Expansion)
                    (_expansionChanges ??= []).Add(new(entry.Candidate, entry.Published.Node.Expansion));
        var relationshipObserver = RelationshipChanged;
        var focusObserver = FocusChanged;
        UiAccessibilityFocusChange? focusChange = null;
        if (focusObserver is not null && _focus != focus)
        {
            UiAccessibilityElement? previousFocus = _focus is null ? null : _entries[_focus].Published;
            UiAccessibilityElement? currentFocus = focus is null ? null : _entries[focus].Candidate;
            focusChange = new(previousFocus, currentFocus);
        }
        if (relationshipObserver is not null)
            CollectRelationshipChanges();
        if (selectionObserver is not null)
            CollectSelectionChanges();
        if (textObserver is not null || scrollObserver is not null || geometryObserver is not null || childrenObserver is not null)
        {
            foreach (var entry in _updates)
            {
                if (childrenObserver is not null)
                    CollectPaneChildrenChanges(entry);
                if (!entry.HasPublished)
                    continue;
                if (textObserver is not null)
                    CollectTextChanges(entry.Published.Node, entry.Candidate);
                if (scrollObserver is not null && entry.Published.Node.Scroll != entry.Candidate.Node.Scroll)
                {
                    (_scrollChanges ??= []).Add(new(entry.Candidate, entry.Published.Node.Scroll));
                }
                var previous = entry.Published.Node;
                var current = entry.Candidate.Node;
                if (geometryObserver is not null && (previous.Bounds != current.Bounds || previous.Offscreen != current.Offscreen)
                    && (entry.ScrollViewport is not null || entry.RevealBounds is not null && FindScrollParent(entry) is not null))
                {
                    (_scrollGeometryChanges ??= []).Add(new(entry.Candidate, previous.Bounds, previous.Offscreen));
                }
            }
            if (childrenObserver is not null)
                foreach (var id in _removed)
                    MarkPaneChildrenChanged(_entries[id].Published.Node.ParentId);
        }
        if (events is not null)
        {
            CollectSurfaceChanges(UiAccessibilityRole.Tooltip, ref _closingTooltips, ref _openingTooltips);
            CollectSurfaceChanges(UiAccessibilityRole.Menu, ref _closingMenus, ref _openingMenus);
            if (_closingTooltips is { } closing)
                NotifySurfaces(events, closing, UiAccessibilityEventKind.TooltipClosed);
            if (_closingMenus is { } menus)
                NotifySurfaces(events, menus, UiAccessibilityEventKind.MenuClosed);
        }
        if (changed)
        {
            _gate.EnterWriteLock();
            try
            {
                foreach (var e in _updates)
                {
                    e.Published = e.Candidate;
                    e.HasPublished = true;
                    _published[e.Staged.Id] = e.Published;
                }
                foreach (var id in _removed)
                    _published.Remove(id);
                _first = first;
                _last = last;
                _focus = focus;
                (_publishedGrids, _stagedGrids) = (_stagedGrids, _publishedGrids);
                Revision++;
            }
            finally { _gate.ExitWriteLock(); }
        }
        var reclaimed = _removed.Count != 0;
        foreach (var id in _removed)
            _entries.Remove(id);
        CompleteAutomaticAnnouncements(commit: true);
        _updates.Clear();
        _removed.Clear();
        _lastByParent.Clear();
        _order.Clear();
        _building = false;
        if (events is not null && _openingTooltips is { } opening)
        {
            NotifySurfaces(events, opening, UiAccessibilityEventKind.TooltipOpened);
        }
        if (events is not null && _openingMenus is { } menusOpened)
            NotifySurfaces(events, menusOpened, UiAccessibilityEventKind.MenuOpened);
        FlushInvocations(events);
        NotifyPropertyChanges(textObserver, _textChanges);
        NotifyPropertyChanges(scrollObserver, _scrollChanges);
        NotifyPropertyChanges(geometryObserver, _scrollGeometryChanges);
        NotifyPropertyChanges(childrenObserver, _paneChildrenChanges);
        NotifyPropertyChanges(treeChildrenObserver, _treeChildrenChanges);
        NotifyPropertyChanges(relationshipObserver, _relationshipChanges);
        NotifyPropertyChanges(selectionObserver, _selectionChanges);
        NotifyPropertyChanges(expansionObserver, _expansionChanges);
        NotifyPropertyChanges(toggleObserver, _toggleChanges);
        NotifyPropertyChanges(rangeObserver, _rangeChanges);
        NotifyPropertyChanges(gridObserver, _gridChanges);
        if (focusObserver is not null && focusChange is { } committedFocus)
            NotifyFocusChanged(focusObserver, committedFocus);
        if (reclaimed)
            TrimStorage();
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var request = _pending[i];
            if (request.Result == UiAccessibilityResult.Success && request.ExpectedText is { } expected)
            {
                if (!TryGet(request.Id, out var edited) || edited.RuntimeId != request.RuntimeId)
                    request.Result = UiAccessibilityResult.Unavailable;
                else if (edited.Node.TextInfo?.Content.Text != expected)
                    request.Result = UiAccessibilityResult.Failed;
            }
            if (request.Result == UiAccessibilityResult.Success && request.SelectedPublicationGeneration is { } selectedGeneration)
            {
                if (!TryGet(request.Id, out var selected) || selected.RuntimeId != request.RuntimeId)
                    request.Result = UiAccessibilityResult.Unavailable;
                else if (_generation < selectedGeneration && !request.Canceled)
                    continue;
                else if (selected.Node.Selected != true)
                    request.Result = UiAccessibilityResult.InvalidOperation;
            }
            if (request.TextScrollApplied && request.Result is null && !request.Canceled)
                continue;
            if (!request.Consumed && request.Result is null && !request.Canceled && TryGet(request.Id, out var current) && current.RuntimeId == request.RuntimeId)
                continue;
            if (request.Result is null && (!request.Consumed || request.Canceled))
                request.Result = request.Canceled ? UiAccessibilityResult.Timeout : UiAccessibilityResult.Unavailable;
            request.Completion.TrySetResult(request.Result ?? UiAccessibilityResult.Failed);
            Interlocked.Decrement(ref _queued);
            _pending.RemoveAt(i);
        }
        if (_pending.Count != 0 || !_incoming.IsEmpty)
            Volatile.Read(ref _wake)?.Invoke();
        if (reclaimed)
            ElementsRemoved?.Invoke();
        if (changed)
            NotifyPublication();
        PublishAnnouncements();
    }
    private static bool ShownTooltip(in UiAccessibilityNode node) => node.Role == UiAccessibilityRole.Tooltip && !node.Offscreen;

    private void CollectSelectionChanges()
    {
        try
        {
            foreach (var id in _order)
            {
                var node = _entries[id].Candidate.Node;
                if (node.SupportsSelectionItem && node.Selected == true)
                {
                    var first = (_selectedTabs ??= new(StringComparer.Ordinal)).TryAdd((node.SelectionContainerId ?? node.ParentId), id);
                    var multiple = _entries[(node.SelectionContainerId ?? node.ParentId)].Candidate.Node.CanSelectMultiple;
                    if (multiple)
                    {
                        var counts = _selectedCounts ??= new(StringComparer.Ordinal);
                        counts.TryGetValue((node.SelectionContainerId ?? node.ParentId), out var count);
                        counts[(node.SelectionContainerId ?? node.ParentId)] = count + 1;
                    }
                    if (!first && !multiple)
                        throw new InvalidOperationException("A single-selection container cannot publish multiple selected items.");
                }
            }
            foreach (var entry in _updates)
            {
                var current = entry.Candidate.Node;
                if (entry.HasPublished && current.SupportsSelection && entry.Published.Node.SelectionRequired != current.SelectionRequired)
                    (_selectionChanges ??= []).Add(new(entry.Candidate, null, null)
                    {
                        PreviousRequired = entry.Published.Node.SelectionRequired
                    });
                if (entry.HasPublished && current.SupportsSelection && entry.Published.Node.CanSelectMultiple != current.CanSelectMultiple)
                    (_selectionChanges ??= []).Add(new(entry.Candidate, null, null)
                    {
                        PreviousMultiple = entry.Published.Node.CanSelectMultiple
                    });
                if (!current.SupportsSelectionItem && !(entry.HasPublished && entry.Published.Node.SupportsSelectionItem))
                    continue;
                var previous = entry.HasPublished ? entry.Published.Node.Selected : null;
                if (!entry.HasPublished)
                {
                    if (current.Selected != true || !_entries[(current.SelectionContainerId ?? current.ParentId)].HasPublished)
                        continue;
                }
                else if (previous == current.Selected)
                    continue;
                var multiple = _entries[(current.SelectionContainerId ?? current.ParentId)].Candidate.Node.CanSelectMultiple;
                UiAccessibilitySelectionEventKind? kind = current.Selected == true
                    ? multiple && _selectedCounts![(current.SelectionContainerId ?? current.ParentId)] > 1 ? UiAccessibilitySelectionEventKind.Added : UiAccessibilitySelectionEventKind.Selected
                    : multiple ? UiAccessibilitySelectionEventKind.Removed
                    : _selectedTabs?.ContainsKey((current.SelectionContainerId ?? current.ParentId)) == true ? null : UiAccessibilitySelectionEventKind.Removed;
                (_selectionChanges ??= []).Add(new(entry.Candidate, previous, kind));
            }
            foreach (var id in _removed)
            {
                var previous = _entries[id].Published.Node;
                if (!previous.SupportsSelectionItem || previous.Selected != true
                    || _selectedTabs?.ContainsKey((previous.SelectionContainerId ?? previous.ParentId)) == true
                    || !_entries.TryGetValue((previous.SelectionContainerId ?? previous.ParentId), out var parent) || parent.Seen != _generation)
                    continue;
                (_selectionChanges ??= []).Add(new(parent.Candidate, null, UiAccessibilitySelectionEventKind.Invalidated));
            }
        }
        finally { _selectedTabs?.Clear(); _selectedCounts?.Clear(); }
    }

    private void CollectPaneChildrenChanges(Entry entry)
    {
        var parent = entry.Candidate.Node.ParentId;
        if (!entry.HasPublished)
        {
            MarkPaneChildrenChanged(parent);
            return;
        }

        var previousParent = entry.Published.Node.ParentId;
        if (previousParent != parent)
        {
            MarkPaneChildrenChanged(previousParent);
            MarkPaneChildrenChanged(parent);
        }
        else if (entry.Published.Previous != entry.Candidate.Previous || entry.Published.Next != entry.Candidate.Next
            || entry.Published.Node.VirtualItem?.IsPlaceholder != entry.Candidate.Node.VirtualItem?.IsPlaceholder)
        {
            MarkPaneChildrenChanged(parent);
        }
    }

    private void MarkPaneChildrenChanged(string? parent)
    {
        if (string.IsNullOrEmpty(parent) || !_entries.TryGetValue(parent, out var pane) || pane.Seen != _generation
            || !pane.HasPublished || pane.ScrollViewport is null || pane.PaneChildrenChanged == _generation)
            return;

        pane.PaneChildrenChanged = _generation;
        (_paneChildrenChanges ??= []).Add(pane.Candidate);
    }

    private void NotifyPropertyChanges<T>(Action<T>? observer, List<T>? changes)
    {
        if (observer is null || changes is not { Count: > 0 })
            return;

        _notifying = true;
        try
        {
            foreach (var change in changes)
                observer(change);
        }
        finally
        {
            changes.Clear();
            _notifying = false;
        }
    }

    private void NotifyClosingPublishedSurfaces()
    {
        if (AutomationEvent is not { } events)
        {
            return;
        }
        CollectPublishedSurfaces(UiAccessibilityRole.Tooltip, ref _closingTooltips);
        CollectPublishedSurfaces(UiAccessibilityRole.Menu, ref _closingMenus);
        if (_closingTooltips is { Count: > 0 } closing)
            NotifySurfaces(events, closing, UiAccessibilityEventKind.TooltipClosed, duringDisposal : true);
        if (_closingMenus is { Count: > 0 } menus)
            NotifySurfaces(events, menus, UiAccessibilityEventKind.MenuClosed, duringDisposal : true);
    }
    private void CollectTextChanges(in UiAccessibilityNode previous, in UiAccessibilityElement current)
    {
        var node = current.Node;
        if (node.Role is UiAccessibilityRole.ComboBox or UiAccessibilityRole.SearchBox or UiAccessibilityRole.SpinButton && previous.Value != node.Value)
            (_textChanges ??= []).Add(new(UiAccessibilityTextProperty.Value, current, previous.Value ?? string.Empty, node.Value ?? string.Empty));
        if (previous.ItemStatus != node.ItemStatus)
            (_textChanges ??= []).Add(new(UiAccessibilityTextProperty.ItemStatus, current, previous.ItemStatus ?? string.Empty, node.ItemStatus ?? string.Empty));
        if (previous.Name != node.Name && (node.Role != UiAccessibilityRole.Tooltip
            || ShownTooltip(previous) && ShownTooltip(node)))
        {
            (_textChanges ??= []).Add(new(UiAccessibilityTextProperty.Name, current, previous.Name, node.Name));
        }
        if (previous.Tooltip != node.Tooltip || previous.Hint != node.Hint || previous.ValidationMessage != node.ValidationMessage)
        {
            var oldHelp = previous.GetHelpText();
            var newHelp = node.GetHelpText();
            if (oldHelp != newHelp)
            {
                (_textChanges ??= []).Add(new(UiAccessibilityTextProperty.HelpText, current, oldHelp, newHelp));
            }
        }
    }
    private void NotifySurfaces(Action<UiAccessibilityEvent> observer, List<UiAccessibilityElement> elements,
        UiAccessibilityEventKind kind, bool duringDisposal = false)
    {
        _notifying = true;
        try
        {
            foreach (var element in elements)
            {
                observer(new(kind, element) { DuringDisposal = duringDisposal });
            }
        }
        finally
        {
            elements.Clear();
            _notifying = false;
        }
    }
    internal void AbortFrame()
    {
        CompleteAutomaticAnnouncements(commit: false);
        ClearAnnouncements();
        ClearInvocations();
        _stagedGrids.Clear();
        ResetScrollIntoView();
        _closingTooltips?.Clear();
        _openingTooltips?.Clear();
        _textChanges?.Clear();
        _scrollChanges?.Clear();
        _scrollGeometryChanges?.Clear();
        _paneChildrenChanges?.Clear();
        _treeChildrenChanges?.Clear();
        _selectionChanges?.Clear();
        _expansionChanges?.Clear();
        _toggleChanges?.Clear();
        _rangeChanges?.Clear();
        _gridChanges?.Clear();
        _closingMenus?.Clear();
        _openingMenus?.Clear();
        _selectedTabs?.Clear();
        _relationshipChanges?.Clear();
        if (!_building)
            return;
        // Omitted published entries belong to the committed snapshot, not to
        // the new entries that must be discarded with this aborted frame.
        _removed.Clear();
        foreach (var id in _order)
        {
            var e = _entries[id];
            if (e.HasPublished)
                e.Staged = e.Published.Node;
            else
                _removed.Add(id);
        }
        foreach (var id in _removed)
            _entries.Remove(id);
        _updates.Clear();
        _removed.Clear();
        _lastByParent.Clear();
        _order.Clear();
        _building = false;
        foreach (var r in _pending)
        {
            r.Completion.TrySetResult(UiAccessibilityResult.Failed);
            Interlocked.Decrement(ref _queued);
        }
        _pending.Clear();
    }
    public UiAccessibilityResult RequestAction(string id, long runtimeId, UiAccessibilityAction action, string? value = null, bool wait = true) =>
        QueueAction(id, runtimeId, action, value, null, wait);

    public UiAccessibilityResult RequestScroll(string id, long runtimeId, UiAccessibilityScrollCommand scroll, bool wait = true) =>
        QueueAction(id, runtimeId, UiAccessibilityAction.Scroll, null, scroll, wait);

    private UiAccessibilityResult QueueAction(string id, long runtimeId, UiAccessibilityAction action, string? value,
        UiAccessibilityScrollCommand? scroll, bool wait)
        => QueueAction(id, runtimeId, action, value, scroll, wait, null, out _);

    private UiAccessibilityResult QueueAction(string id, long runtimeId, UiAccessibilityAction action, string? value,
        UiAccessibilityScrollCommand? scroll, bool wait, VirtualItemAction? virtualItem, out string? foundId, double? rangeValue = null,
        UiAccessibilityTextInfo? textSelection = null, UiAccessibilityTextEdit? textEdit = null, UiAccessibilityTextScroll? textScroll = null,
        UiAccessibilityScrollAlignment? scrollAlignment = null)
    {
        foundId = null;
        if (wait && Environment.CurrentManagedThreadId == _writerThread)
            return UiAccessibilityResult.Busy;
        Request request;
        _gate.EnterReadLock();
        try
        {
            if (_disposed || !_published.TryGetValue(id, out var entry) || entry.RuntimeId != runtimeId)
                return UiAccessibilityResult.Unavailable;
            if ((uint)action > (uint)UiAccessibilityAction.ScrollText)
                return UiAccessibilityResult.InvalidArgument;
            var node = entry.Node;
            if (action is UiAccessibilityAction.FindVirtualItem or UiAccessibilityAction.RealizeVirtualItem)
            {
                if (virtualItem is not { } itemAction)
                    return UiAccessibilityResult.InvalidArgument;
                var virtualGrid = node.Grid is { IsVirtualized : true };
                if (itemAction.GridCoordinates is { } cell)
                {
                    if (!virtualGrid || (uint)cell.Row >= (uint)node.Grid!.Value.RowCount
                        || (uint)cell.Column >= (uint)node.Grid.Value.ColumnCount)
                        return UiAccessibilityResult.InvalidArgument;
                }
                else if (!node.VirtualItemContainer && !(virtualGrid && action == UiAccessibilityAction.RealizeVirtualItem))
                    return UiAccessibilityResult.InvalidArgument;
                if (itemAction.Reference is { } reference)
                {
                    if (!_published.TryGetValue(reference.Id, out var item) || item.RuntimeId != reference.RuntimeId)
                        return UiAccessibilityResult.Unavailable;
                    if (!OwnsVirtualItem(id, item))
                        return UiAccessibilityResult.InvalidArgument;
                }
            }
            if (action != UiAccessibilityAction.FindVirtualItem && !node.Enabled
                && !(action == UiAccessibilityAction.Focus && node.DisabledFocusable && node.Focusable))
                return UiAccessibilityResult.Disabled;
            if (action == UiAccessibilityAction.ScrollIntoView)
            {
                if (!node.CanScrollIntoView)
                    return UiAccessibilityResult.InvalidOperation;
            }
            else if (node.Offscreen && action is not (UiAccessibilityAction.FindVirtualItem or UiAccessibilityAction.RealizeVirtualItem)
                && !(action == UiAccessibilityAction.ScrollText && node.CanScrollIntoView)
                && !(node.Role == UiAccessibilityRole.Option && action is UiAccessibilityAction.Select or UiAccessibilityAction.AddToSelection or UiAccessibilityAction.Deselect))
                return UiAccessibilityResult.Unavailable;
            if (action == UiAccessibilityAction.Focus && !node.Focusable)
                return UiAccessibilityResult.InvalidArgument;
            if (action == UiAccessibilityAction.SetValue)
            {
                if (!node.SupportsValue)
                    return UiAccessibilityResult.InvalidArgument;
                if (node.ReadOnly)
                    return UiAccessibilityResult.ReadOnly;
                if (value is null || value.Length > node.MaximumLength || ContainsControl(value, node.Multiline))
                    return UiAccessibilityResult.InvalidArgument;
            }
            if (action == UiAccessibilityAction.SetTextSelection)
            {
                if (node.TextInfo is not { CanSetSelection: true } currentText)
                    return UiAccessibilityResult.InvalidOperation;
                if (textSelection is null || !ReferenceEquals(textSelection.Content, currentText.Content))
                    return UiAccessibilityResult.InvalidArgument;
            }
            if (action == UiAccessibilityAction.EditText)
            {
                if (!node.SupportsValue || node.TextInfo is not { CanEditText: true } editable)
                    return UiAccessibilityResult.InvalidOperation;
                if (textEdit is null || !ReferenceEquals(textEdit.Content, editable.Content))
                    return UiAccessibilityResult.InvalidArgument;
                if (textEdit.Kind != UiAccessibilityTextEditKind.Copy && node.ReadOnly)
                    return UiAccessibilityResult.ReadOnly;
                if (textEdit.Replacement is { } replacement && ContainsControl(replacement, node.Multiline))
                    return UiAccessibilityResult.InvalidArgument;
            }
            if (action == UiAccessibilityAction.ScrollText)
            {
                if (node.TextInfo is not { } current || node.TextLayout is not { } layout)
                    return UiAccessibilityResult.InvalidOperation;
                if (textScroll is null || !ReferenceEquals(textScroll.Content, current.Content))
                    return UiAccessibilityResult.InvalidArgument;
                if (layout.HasComposition)
                    return UiAccessibilityResult.Busy;
            }
            if (action == UiAccessibilityAction.Invoke && !node.SupportsInvoke)
                return UiAccessibilityResult.InvalidArgument;
            if (action == UiAccessibilityAction.Toggle && node.ToggleState is null)
                return UiAccessibilityResult.InvalidOperation;
            if (action == UiAccessibilityAction.SetRangeValue)
            {
                if (node.Range is not { } range)
                    return UiAccessibilityResult.InvalidOperation;
                if (node.ReadOnly)
                    return UiAccessibilityResult.ReadOnly;
                if (rangeValue is not { } numeric || !double.IsFinite(numeric) || numeric < range.Minimum || numeric > range.Maximum)
                    return UiAccessibilityResult.InvalidArgument;
            }
            if (action is UiAccessibilityAction.Expand or UiAccessibilityAction.Collapse
                && node.Expansion is null or UiAccessibilityExpansionState.LeafNode)
                return UiAccessibilityResult.InvalidOperation;
            if (action is UiAccessibilityAction.Select or UiAccessibilityAction.Deselect or UiAccessibilityAction.AddToSelection && !node.SupportsSelectionItem)
                return UiAccessibilityResult.InvalidOperation;
            if (action == UiAccessibilityAction.Deselect && node.Selected == true &&
                (node.Role is UiAccessibilityRole.MenuItem or UiAccessibilityRole.RadioButton ||
                    UiAccessibilitySelectionProjection.Resolve(_published, id, runtimeId, out var selectionOwner) == UiAccessibilityResult.Success && selectionOwner.Node.SelectionRequired
                    && !HasOtherSelectedChild(selectionOwner, id)))
                return UiAccessibilityResult.InvalidOperation;
            if (action == UiAccessibilityAction.Scroll)
            {
                if (scroll is not { IsValid: true } command)
                    return UiAccessibilityResult.InvalidArgument;
                if (node.Scroll is not { } viewport || !command.CanApply(viewport))
                    return UiAccessibilityResult.InvalidOperation;
            }
            if (Interlocked.Increment(ref _queued) > 1024)
            {
                Interlocked.Decrement(ref _queued);
                return UiAccessibilityResult.Busy;
            }
            request = new(id, runtimeId, action, value, scroll)
            {
                VirtualItem = virtualItem,
                RangeValue = rangeValue,
                TextSelection = textSelection,
                TextEdit = textEdit,
                TextScroll = textScroll,
                ScrollAlignment = scrollAlignment
            };
            _incoming.Enqueue(request);
        }
        finally { _gate.ExitReadLock(); }
        Volatile.Read(ref _wake)?.Invoke();
        if (!wait)
            return UiAccessibilityResult.Success;
        if (request.Completion.Task.Wait(TimeSpan.FromSeconds(2)))
        {
            var result = request.Completion.Task.Result;
            if (result == UiAccessibilityResult.Success)
                foundId = request.FoundVirtualItemId;
            return result;
        }
        Volatile.Write(ref request.Canceled, true);
        return UiAccessibilityResult.Timeout;
    }
    internal Request? Take(string id, UiAccessibilityAction action)
    {
        foreach (var r in _pending)
        {
            if (r.Consumed && r.Id == id && r.Action == action)
                return null;
        }
        foreach (var r in _pending)
        {
            if (r.Consumed || Volatile.Read(ref r.Canceled) || r.Id != id)
                continue;
            if (r.Action != action)
                return null;
            r.Consumed = true;
            if (!_entries.TryGetValue(id, out var entry) || entry.RuntimeId != r.RuntimeId)
            {
                r.Result = UiAccessibilityResult.Unavailable;
                return null;
            }
            return r;
        }
        return null;
    }
    internal void AwaitSelectedPublication(Request request)
    {
        request.SelectedPublicationGeneration = _generation + 1;
    }
    internal sealed class Request(string id, long runtimeId, UiAccessibilityAction action, string? value, UiAccessibilityScrollCommand? scroll)
    {
        internal readonly string Id = id; internal readonly long RuntimeId = runtimeId; internal readonly UiAccessibilityAction Action = action;
        internal readonly string? Value = value; internal readonly TaskCompletionSource<UiAccessibilityResult> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly UiAccessibilityScrollCommand? Scroll = scroll;
        internal VirtualItemAction? VirtualItem
        {
            get; init;
        }
        internal double? RangeValue
        {
            get; init;
        }
        internal UiAccessibilityTextInfo? TextSelection
        {
            get; init;
        }
        internal string? FoundVirtualItemId;
        internal UiAccessibilityTextEdit? TextEdit
        {
            get; init;
        }
        internal UiAccessibilityTextScroll? TextScroll
        {
            get; init;
        }
        internal bool TextScrollApplied;
        internal UiAccessibilityScrollAlignment? ScrollAlignment
        {
            get; init;
        }
        internal string? ExpectedText;
        internal UiRect? VirtualRevealBounds;
        internal ulong RevealAfterGeneration;
        internal ulong? SelectedPublicationGeneration;
        internal bool Consumed, Canceled; internal UiAccessibilityResult? Result;
    }
    internal static bool ContainsControl(string value, bool multiline = false)
    {
        foreach (var character in value)
            if (char.IsControl(character) && !(multiline && character is '\r' or '\n'))
                return true;
        return false;
    }
    private void TrimStorage()
    {
        // Reclaim burst capacity after removals, rather than pinning a large former screen forever.
        var limit = Math.Max(256, _entries.Count * 4);
        if (_entries.EnsureCapacity(0) > limit)
            _entries.TrimExcess();
        _gate.EnterWriteLock();
        try
        {
            if (_published.EnsureCapacity(0) > limit)
                _published.TrimExcess();
        }
        finally { _gate.ExitWriteLock(); }
        if (_order.Capacity > limit)
            _order.TrimExcess();
        if (_removed.Capacity > limit)
            _removed.TrimExcess();
        if (_updates.Capacity > limit)
            _updates.TrimExcess();
        if (_lastByParent.EnsureCapacity(0) > limit)
            _lastByParent.TrimExcess();
    }
    public void Dispose()
    {
        if (_notifying)
        {
            throw new InvalidOperationException("Accessibility disposal cannot reenter an automation notification.");
        }
        if (_disposed)
        {
            return;
        }
        // The render producer has stopped. Keep published ancestry available until close observers return.
        try
        {
            NotifyClosingPublishedSurfaces();
        }
        finally
        {
            _gate.EnterWriteLock();
            try
            {
                _disposed = true;
                _published.Clear();
                _publishedGrids.Clear();
                _stagedGrids.Clear();
                _first = _last = _focus = null;
            }
            finally
            {
                _gate.ExitWriteLock();
            }
            // Reader locks remain usable by retained COM providers.
            AbortFrame();
            ReleaseAutomaticAnnouncements();
            _entries.Clear();
            _wake = null;
            foreach (var pending in _pending)
            {
                pending.Completion.TrySetResult(UiAccessibilityResult.Unavailable);
                Interlocked.Decrement(ref _queued);
            }
            _pending.Clear();
            _order.Clear();
            _removed.Clear();
            _updates.Clear();
            _lastByParent.Clear();
            TrimStorage();
            while (_incoming.TryDequeue(out var request))
            {
                request.Completion.TrySetResult(UiAccessibilityResult.Unavailable);
                Interlocked.Decrement(ref _queued);
            }
        }
    }
}

public interface IPlatformAccessibilityBackend
{
    UiAccessibilityTree AccessibilityTree
    {
        get;
    }
}
