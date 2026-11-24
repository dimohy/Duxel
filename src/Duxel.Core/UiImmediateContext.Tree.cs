namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal readonly record struct TreeScope(string Parent, UiWidget? Widget, int TabStart, int Tier, string? FocusAtStart, int FocusRow);
    internal IReadOnlyList<UiKeyEvent> TreeInputKeys
    {
        get => _keyEvents;
        set => _keyEvents = value;
    }

    internal void UpdateTreeInput(UiTreeViewState tree)
    {
        tree.Bind(this, ResolveId(UiControlWidget.PrimaryControlId)); tree.Synchronize();
        if (_accessibility is { } accessibility)
            tree.Automation.Prepare(tree, accessibility, IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier);
        var index = tree.FindFocusRow(_state.FocusedId);
        if (index < 0) { tree.Search.Reset(); return; }
        if (!tree.Model.IsVisible(index))
        {
            tree.Search.Reset();
            if (tree.Model.Focused >= 0 && tree.RowIds[tree.Model.Focused] is { } ancestor)
                FocusGroupControl(ancestor, keyboard : true);
            return;
        }
        if (tree.Model.Focused != index) tree.Search.Reset();
        tree.Model.Focus(index);
        if (!IsControlEnabled || _popupTierDepth < _blockingKeyboardTier) return;
        if (RouteTreeActionKeys(tree, index))
        {
            tree.Search.Reset();
            return;
        }
        var revision = tree.Model.VisibilityRevision;
        var rightToLeft = tree.Options.RightToLeft || RightToLeft;
        var handled = false;
        foreach (var key in _keyEvents)
        {
            if (!key.IsDown || key.Modifiers != 0) continue;
            if (key.Key == UiKey.DownArrow) tree.Model.Move(1);
            else if (key.Key == UiKey.UpArrow) tree.Model.Move(-1);
            else if (key.Key == UiKey.Home) tree.Model.Boundary(false);
            else if (key.Key == UiKey.End) tree.Model.Boundary(true);
            else if (key.Key == (rightToLeft ? UiKey.LeftArrow : UiKey.RightArrow)) tree.Model.Inward();
            else if (key.Key == (rightToLeft ? UiKey.RightArrow : UiKey.LeftArrow)) tree.Model.Outward();
            else continue;
            handled = true;
        }
        if (handled) tree.Search.Reset();
        if ((_state.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) == 0)
            foreach (var character in _charEvents)
            {
                if (!System.Text.Rune.IsValid(character.CodePoint)) continue;
                var rune = new System.Text.Rune((int)character.CodePoint);
                if (System.Text.Rune.IsControl(rune) || System.Text.Rune.IsWhiteSpace(rune) && !tree.Search.Active(GetTime())) continue;
                var query = tree.Search.Append(rune.ToString(), GetTime(), out var cycle);
                handled |= tree.Model.Search(query, cycle);
            }
        if (handled && tree.Model.Focused >= 0 && tree.RowIds[tree.Model.Focused] is { } focused)
            FocusGroupControl(focused, keyboard : true);
        if (tree.Model.VisibilityRevision != revision) tree.Invalidate?.Invoke();
    }
    private bool RouteTreeActionKeys(UiTreeViewState tree, int row)
    {
        if (!tree.ActionMembers.TryGetValue(row, out var members) || members.Count == 0)
            return false;

        var child = members.FindIndex(member => member.Id == _state.FocusedId);
        var startedInChild = child >= 0;
        if (!tree.Options.RowActions)
        {
            if (startedInChild)
                FocusGroupControl(tree.RowIds[row]!, keyboard: true);
            tree.ActionMembers.Clear();
            return startedInChild;
        }

        var consumed = false;
        var keys = tree.ActionKeys;
        keys.Clear();
        foreach (var key in _keyEvents)
        {
            if (key.IsDown && key.Modifiers == 0)
            {
                string? target = null;
                var horizontal = UiCompositeMember.Horizontal(key.Key, tree.Options.RightToLeft || RightToLeft);
                if (child < 0 && key.Key == UiKey.F2 && members.Count != 0)
                    target = members[child = 0].Id;
                else if (child >= 0 && key.Key == UiKey.Escape && !_focusGroupEscapeConsumed && !_tooltipEscapeConsumed
                    && string.IsNullOrEmpty(_imeHandler?.GetCompositionText()))
                {
                    child = -1;
                    target = tree.RowIds[row];
                }
                else if (child >= 0 && !members[child].ConsumesArrows && key.Key is UiKey.UpArrow or UiKey.DownArrow)
                {
                    tree.Model.Move(key.Key == UiKey.DownArrow ? 1 : -1);
                    var nextRow = tree.Model.Focused;
                    if (nextRow < 0 || nextRow == row || tree.RowIds[nextRow] is null)
                    {
                        consumed = true;
                        continue;
                    }
                    row = nextRow;
                    if (tree.ActionMembers.TryGetValue(row, out var nextMembers) && child < nextMembers.Count)
                    {
                        members = nextMembers;
                        target = members[child].Id;
                    }
                    else
                    {
                        members = nextMembers ?? [];
                        child = -1;
                        target = tree.RowIds[row];
                    }
                }
                else if (child >= 0 && !members[child].ConsumesArrows && key.Key is UiKey.Home or UiKey.End)
                {
                    child = key.Key == UiKey.Home ? 0 : members.Count - 1;
                    target = members[child].Id;
                }
                else if (child >= 0 && horizontal != 0 && !members[child].ConsumesArrows)
                {
                    var next = child + horizontal;
                    if (next < members.Count)
                    {
                        child = next;
                        target = child < 0 ? tree.RowIds[row] : members[child].Id;
                    }
                    else
                    {
                        consumed = true;
                        continue;
                    }
                }
                if (target is not null)
                {
                    FocusGroupControl(target, keyboard: true);
                    consumed = true;
                    continue;
                }
            }
            keys.Add(key);
        }
        if (consumed)
            _keyEvents = keys;
        return consumed || startedInChild;
    }

    internal TreeScope BeginTree(string label, UiRect bounds, UiTreeViewState tree)
    {
        var root = ResolveId(UiControlWidget.PrimaryControlId);
        var focus = _state.FocusedId;
        var scope = new TreeScope(_accessibilityParent, _accessibilityWidget, _currentTabStops.Count, _popupTierDepth,
            focus, tree.FindFocusRow(focus));
        tree.Root = root;
        tree.PublishedGridRows = 0;
        ExposeAccessibility(root, scope.Parent, tree.Options.RowActions ? UiAccessibilityRole.TreeGrid : UiAccessibilityRole.Tree, label, bounds, focusable : false, valuePattern : false, virtualItemContainer : true);
        _accessibility?.SetTreeRoot(root, tree.Single is not null, tree.Multiple is not null);
        _accessibilityParent = root; _accessibilityWidget = null;
        return scope;
    }
    internal void EndTree(TreeScope scope, UiTreeViewState tree)
    {
        try
        {
            tree.Synchronize();
            var focusedRow = _state.FocusedId == scope.FocusAtStart ? scope.FocusRow : tree.FindFocusRow(_state.FocusedId);
            if (focusedRow >= 0 && tree.RowFrames[focusedRow] == GetFrameCount()
                && tree.RowIds[focusedRow] != _state.FocusedId)
            {
                var childPresent = false;
                for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
                    childPresent |= _currentTabStops[i].PopupTier == scope.Tier && _currentTabStops[i].Id == _state.FocusedId;
                if (!childPresent && tree.RowIds[focusedRow] is { } rowId)
                    FocusGroupControl(rowId, keyboard: true);
            }
            var keep = -1;
            for (var i = scope.TabStart; i < _currentTabStops.Count; i++)
            {
                var stop = _currentTabStops[i];
                if (stop.PopupTier != scope.Tier)
                    continue;
                if (stop.Id == _state.FocusedId)
                {
                    keep = i;
                    break;
                }
                var row = tree.Find(stop.Id);
                if (row >= 0 && (keep < 0 || row == tree.Model.Focused))
                    keep = i;
            }
            for (var i = _currentTabStops.Count - 1; i >= scope.TabStart; i--)
                if (_currentTabStops[i].PopupTier == scope.Tier && i != keep) _currentTabStops.RemoveAt(i);
            for (var index = 0; index < tree.Items.Length; index++)
            {
                if (tree.RowFrames[index] != GetFrameCount() || tree.RowIds[index] is not { } id) continue;
                _accessibility?.SetTreeItem(id, tree.Root!, tree, index);
                if (tree.Selectable) PaintTreeIndicator(tree, index);
                if (_state.FocusedId == id && _state.NavCursorVisible)
                {
                    var context = new UiControlContext(this);
                    using var clip = context.Clip(tree.Indicators[index].Clip);
                    tree.PaintFocus(context, tree.RowBounds[index]);
                }
            }
            foreach (var (row, members) in tree.ActionMembers)
                if (tree.RowFrames[row] != GetFrameCount())
                    members.Clear();
            if (!tree.Options.RowActions)
                tree.ActionMembers.Clear();
            if (tree.Options.RowActions)
                _accessibility?.SetGrid(tree.Root!, new(tree.PublishedGridRows, 1));
            PublishHiddenTreeItems(tree);
        }
        finally { _accessibilityParent = scope.Parent; _accessibilityWidget = scope.Widget; }
    }
    internal void RenderTreeItem(UiTreeViewState tree, UiTreeItem item)
    {
        var context = new UiControlContext(this);
        var size = context.AllocatedLayoutSize;
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var id = ResolveId(UiControlWidget.PrimaryControlId);
        var priorParent = _accessibilityParent; var priorWidget = _accessibilityWidget;
        var index = item.Index; var graph = tree.Model.Hierarchy;
        var parent = graph.Parent[index] is var parentIndex && parentIndex >= 0
            ? tree.RowIds[parentIndex] ?? throw new InvalidOperationException("Render tree parents before children.") : tree.Root!;
        var indent = MathF.Min(tree.Indent(index), bounds.Width);
        var rightToLeft = tree.Options.RightToLeft || RightToLeft;
        UiRect Slot(float start, float width) => new(rightToLeft ? bounds.X + bounds.Width - start - width : bounds.X + start,
            bounds.Y, width, bounds.Height);
        var chevron = Slot(indent, MathF.Min(tree.Metrics.ChevronWidth, bounds.Width - indent));
        var check = Slot(MathF.Min(indent + tree.Metrics.ChevronWidth, bounds.Width), MathF.Min(tree.Selectable ? tree.Metrics.SelectorWidth : 0, MathF.Max(0, bounds.Width - indent - tree.Metrics.ChevronWidth)));
        if (tree.Options.AlignSelectorToTop)
            check = check with { Height = MathF.Min(check.Height, tree.Metrics.MinimumRowHeight) };
        var available = MathF.Max(0, bounds.Width - tree.Prefix(index) - tree.Metrics.ContentPadding * 2);
        var contentBounds = Slot(MathF.Min(tree.Prefix(index) + tree.Metrics.ContentPadding, bounds.Width), available);
        try
        {
            using var clip = context.Clip(bounds);
            var capture = tree.Options.RowActions && item.PlainLabel is null ? BeginInputSlotCapture() : null;
            var completed = false;
            try
            {
                var claims = _focusGroupPointerClaims;
                if (capture is not null)
                {
                    _focusGroupPointerDepth++;
                    try
                    {
                        RenderTreeContent(tree, item, context, id, parent, contentBounds, bounds, default, capture);
                    }
                    finally
                    {
                        _focusGroupPointerDepth--;
                    }
                }
                var activation = UiButtonActivation.Release;
                if (tree.HasPressActivation && !(tree.Selectable && IsHovering(check)))
                    activation = graph.IsBranch(index) && IsHovering(chevron)
                        ? tree.DisclosurePointerActivation : tree.RowPointerActivation;
                // Keep the original edge until release, even when the pointer crosses into another part.
                var pointerActivation = tree.HasPressActivation && !_leftMousePressed && _state.ActiveId == id
                    ? tree.HeldPointerActivation : activation;
                var interaction = claims != _focusGroupPointerClaims
                    ? ProcessControlKeyboard<RangeControlKeys>(id, bounds, default)
                    : ProcessButton<RangeControlKeys>(UiControlWidget.PrimaryControlId, bounds, pointerActivation);
                if (tree.HasPressActivation && interaction.Hovered && _leftMousePressed)
                    tree.HeldPointerActivation = pointerActivation;
            var availability = RadioAccessibilityAvailability(true, IntersectRect(CurrentClipRect, bounds));
            var body = interaction.Activated && activation == pointerActivation;
            foreach (var action in (ReadOnlySpan<UiAccessibilityAction>)[UiAccessibilityAction.Expand, UiAccessibilityAction.Collapse])
                if (_accessibility?.Take(id, action) is { } request)
                {
                    request.Result = !graph.IsBranch(index) ? UiAccessibilityResult.InvalidOperation : availability;
                    if (request.Result == UiAccessibilityResult.Success && !tree.Expand(index, action == UiAccessibilityAction.Expand))
                        request.Result = UiAccessibilityResult.InvalidOperation;
                }
            if (_accessibility?.Take(id, UiAccessibilityAction.Invoke) is { } invoke)
            {
                invoke.Result = item.Invoked is null ? UiAccessibilityResult.InvalidOperation : availability;
                if (invoke.Result == UiAccessibilityResult.Success) item.Invoked!(item.Value);
            }
            foreach (var action in (ReadOnlySpan<UiAccessibilityAction>)[UiAccessibilityAction.Select, UiAccessibilityAction.AddToSelection, UiAccessibilityAction.Deselect, UiAccessibilityAction.Toggle])
                if (_accessibility?.Take(id, action) is { } request)
                {
                    request.Result = !tree.Selectable ? UiAccessibilityResult.InvalidOperation : availability;
                    if (request.Result == UiAccessibilityResult.Success)
                    {
                        var value = action == UiAccessibilityAction.Toggle ? tree.Checked(index) != true : action != UiAccessibilityAction.Deselect;
                        if (action == UiAccessibilityAction.AddToSelection && tree.Single is { Count: > 0 } single && !single.Selected[index])
                            request.Result = UiAccessibilityResult.InvalidOperation;
                        else if (!tree.Check(index, value)) request.Result = UiAccessibilityResult.InvalidOperation;
                    }
                }
            if (_state.FocusedId == id && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
                foreach (var key in _keyEvents)
                    if (key.IsDown && key.Modifiers == 0)
                    {
                        if (key.Key == UiKey.Space && tree.Selectable && !tree.Search.Active(GetTime())) tree.Check(index, tree.Checked(index) != true);
                        else if (key.Key == UiKey.Enter) body = true;
                    }
            if (body && IsControlEnabled)
            {
                if (tree.Selectable && interaction.Hovered && IsHovering(check)) tree.Check(index, tree.Checked(index) != true);
                else if (graph.IsBranch(index) && (!tree.Options.Navigation || IsHovering(chevron))) tree.Expand(index, !tree.Model.IsExpanded(index));
                else
                {
                    if (tree.Options.Navigation && tree.Single is not null) tree.Check(index, true);
                    item.Invoked?.Invoke(item.Value);
                }
                _requestFrame?.Invoke();
            }
            tree.PaintRow(this, item, context, bounds, chevron, interaction, graph.IsBranch(index), tree.Model.IsExpanded(index), tree.Checked(index) == true);
            if (capture is null)
                RenderTreeContent(tree, item, context, id, parent, contentBounds, bounds, interaction, _builder);
            else
                _builder.AppendDeferredFrom(capture);
            completed = true;
            tree.RowIds[index] = id; tree.RowBounds[index] = bounds; tree.RowFrames[index] = GetFrameCount();
            tree.Indicators[index] = new(check, CurrentClipRect, interaction, IsControlEnabled);
            if (_state.FocusedId == id || tree.ActionMembers.TryGetValue(index, out var actions)
                && actions.Exists(member => member.Id == _state.FocusedId))
                tree.Model.Focus(index);
            _accessibility?.SetTreeItem(id, tree.Root!, tree, index);
            if (interaction.Hovered && IsControlEnabled && (graph.IsBranch(index) || tree.Selectable || item.Invoked is not null)) SetMouseCursor(UiMouseCursor.Hand);
            }
            finally
            {
                if (capture is not null)
                    EndInputSlotCapture(capture, completed);
            }
        }
        finally { _accessibilityParent = priorParent; _accessibilityWidget = priorWidget; FinishLayoutBox(origin, size); }
    }
    private void RenderTreeContent(UiTreeViewState tree, UiTreeItem item, UiControlContext context, string id,
        string parent, UiRect contentBounds, UiRect bounds, UiButtonInteraction interaction, UiDrawListBuilder builder)
    {
        var contentSize = context.MeasureConstrained(item.Content, new(0, contentBounds.Width, 0, contentBounds.Height));
        item.PlainLabel?.Label.SetInteraction(interaction);
        var tooltip = BeginTreeLabelTooltip(item, id);
        try
        {
            ExposeAccessibility(id, parent, tree.RowRole, item.Label, bounds, valuePattern: false);
            _accessibilityParent = id;
            _accessibilityWidget = null;
            if (tree.Options.RowActions && _accessibility is { } accessibility)
            {
                var cell = ResolveCachedIdentifier(id, "##tree-cell", false);
                ExposeAccessibility(cell, id, UiAccessibilityRole.GridCell, item.Label, contentBounds,
                    focusable: false, valuePattern: false);
                accessibility.SetGridItem(cell, new(tree.Root!, tree.PublishedGridRows++, 0));
                _accessibilityParent = cell;
            }
            var start = _currentTabStops.Count;
            var childBounds = contentBounds with
            {
                X = tree.Options.RightToLeft || RightToLeft ? contentBounds.X + contentBounds.Width - contentSize.X : contentBounds.X,
                Y = bounds.Y + (bounds.Height - contentSize.Y) / 2,
                Width = contentSize.X,
                Height = contentSize.Y
            };
            RenderAllocatedChild(builder, item.Content, childBounds, bounds, constrainLayout: true);
            tree.ActionMembers.TryGetValue(item.Index, out var members);
            members?.Clear();
            for (var i = start; i < _currentTabStops.Count; i++)
            {
                var stop = _currentTabStops[i];
                if (stop.PopupTier != _popupTierDepth)
                    continue;
                if (!tree.Options.RowActions)
                    throw new NotSupportedException("Interactive Tree row content requires RowActions.");
                if (members is null)
                    tree.ActionMembers.Add(item.Index, members = []);
                members.Add(new(stop.Id, stop.ConsumesArrows));
            }
        }
        finally
        {
            EndTreeLabelTooltip(tooltip, id, bounds);
        }
    }

    private void PaintTreeIndicator(UiTreeViewState tree, int index)
    {
        var paint = tree.Indicators[index]; var context = new UiControlContext(this);
        using var clip = context.Clip(paint.Clip);
        BeginDisabled(!paint.Enabled);
        try
        {
            tree.PaintSelector(context, paint.Bounds, paint.Interaction, tree.Checked(index));
        }
        finally { EndDisabled(); }
    }
}
