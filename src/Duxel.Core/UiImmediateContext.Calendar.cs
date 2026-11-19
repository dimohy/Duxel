namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderCalendar(UiCalendarView widget, string id, UiCalendarLayout layout)
    {
        var context = new UiControlContext(this);
        using var font = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        UiColor DayColor(DayOfWeek day, bool enabled, bool current = true, bool holiday = false)
        {
            if (!enabled || !IsControlEnabled) return context.Theme.TextDisabled;
            var colors = widget.Options.DayColors;
            var color = context.Theme.Text;
            if (colors?.Enabled != false && context.Theme.ForcedColors is null && widget.Weekend.Contains(day))
            {
                var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
                color = day == widget.Weekend.End ? colors?.WeekendEnd ?? (dark ? new UiColor(255, 153, 153) : new UiColor(196, 43, 28))
                    : colors?.WeekendStart ?? (dark ? new UiColor(117, 182, 245) : new UiColor(0, 103, 192));
            }
            if (holiday && context.Theme.ForcedColors is null)
                color = widget.Options.HolidayColor ?? (context.FluentColorScheme == UiFluentColorScheme.Dark ? new UiColor(255, 153, 153) : new UiColor(196, 43, 28));
            return current ? color : context.Theme.ForcedColors is not null ? context.Theme.TextDisabled : ApplyAlpha(color, .5f);
        }
        var month = widget.Month;
        var cellKeys = widget.CellKeys;
        var cellNames = widget.CellNames;
        var position = AdvanceCursor(layout.Size);
        var allocation = new UiRect(position.X, position.Y, layout.Size.X, layout.Size.Y);
        using var clipping = context.Clip(allocation);
        var root = ResolveId(id);
        var gridId = ResolveId(id + "##grid");
        var gridTop = position.Y + 8 + layout.HeaderHeight + 8;
        var grid = new UiRect(position.X + 8, gridTop + layout.WeekdayHeight, layout.Edge * 7, month.WeekCount * layout.Edge);
        var owner = _accessibilityWidget;
        if (!widget.EmbeddedInPopup)
            context.Frame(allocation, context.Theme.WindowBg, context.Theme.Border, 6, 1);
        var header = new UiRect(position.X + 48, position.Y + 8, layout.Edge * 7 - 80, layout.HeaderHeight);
        header = header with { Y = header.Y + (header.Height - layout.Title.Size.Y) / 2, Height = layout.Title.Size.Y };
        if (widget.System is not IUiCalendarPeriodSystem)
            context.TypographyTextBlock(layout.Title, header, context.Theme.Text, UiItemHorizontalAlign.Center);
        int navigate = 0;
        int navigateYear = 0;
        bool today = false;
        DateOnly? activate = null;
        var tabStart = _currentTabStops.Count;
        var control = ProcessButton<SpaceControlKeys>(id + "##grid", grid, UiButtonActivation.Release);
        if (control.Focused && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
        {
            foreach (var key in _keyEvents)
            {
                if (key.IsDown && key.Modifiers == KeyModifiers.Shift && key.Key is UiKey.PageUp or UiKey.PageDown)
                {
                    navigateYear = key.Key == UiKey.PageUp ? -1 : 1;
                    continue;
                }
                if (!key.IsDown || key.Modifiers != 0) continue;
                var step = key.Key switch
                {
                    UiKey.LeftArrow => month.RightToLeft ? 1 : -1,
                    UiKey.RightArrow => month.RightToLeft ? -1 : 1,
                    UiKey.UpArrow => -7,
                    UiKey.DownArrow => 7,
                    UiKey.Home => -(((int)widget.FocusedDate.DayOfWeek - (int)month.FirstDayOfWeek + 7) % 7),
                    UiKey.End => 6 - (((int)widget.FocusedDate.DayOfWeek - (int)month.FirstDayOfWeek + 7) % 7),
                    _ => 0
                };
                if (step != 0)
                {
                    var day = (long)widget.FocusedDate.DayNumber + step;
                    if (day >= widget.Minimum.DayNumber && day <= widget.Maximum.DayNumber)
                        widget.Focus(DateOnly.FromDayNumber((int)day));
                    _requestFrame?.Invoke();
                }
                if (key.Key == UiKey.PageUp) navigate = -1;
                if (key.Key == UiKey.PageDown) navigate = 1;
                if (key.Key == UiKey.Enter) activate = widget.FocusedDate;
                if (key.Key == UiKey.Escape) { widget.CancelPending(); _requestFrame?.Invoke(); }
            }
        }
        DateOnly? hit = null;
        if (control.Hovered)
        {
            var column = (int)((_mousePosition.X - grid.X) / layout.Edge);
            var row = (int)((_mousePosition.Y - grid.Y) / layout.Edge);
            var logical = month.RightToLeft ? 6 - column : column;
            var index = row * 7 + logical;
            if (column is >= 0 and < 7 && row >= 0 && index >= 0 && index < month.Cells.Length)
                hit = month.Cells.Span[index].IsSupported ? month.Cells.Span[index].Date : null;
        }
        if (_leftMousePressed) widget.PointerPressedDate = hit;
        if (control.Activated)
        {
            if (_leftMouseReleased && hit is { } pointer && widget.PointerPressedDate == hit) activate = pointer;
            else if (!_leftMouseReleased) activate = widget.FocusedDate;
        }
        if (_leftMouseReleased) widget.PointerPressedDate = null;
        var selectionBefore = widget.Selection;
        var pendingBefore = widget.PendingStart;
        if (activate is { } pointerDate && widget.WithinBounds(pointerDate))
        {
            widget.Focus(pointerDate);
            widget.Commit(pointerDate);
            _requestFrame?.Invoke();
        }
        UiRect CellBounds(int index) => new(grid.X + month.PhysicalColumn(index % 7) * layout.Edge,
            grid.Y + index / 7 * layout.Edge, layout.Edge, layout.Edge);
        if (_accessibility is { } actions)
            for (var i = 0; i < month.Cells.Length; i++)
            {
                var cell = month.Cells.Span[i];
                var cellId = ResolveCachedIdentifier(root, cellKeys.Span[i], false);
                var clip = IntersectRect(CurrentClipRect, CellBounds(i));
                var supported = cell.IsSupported && cell.Date is { } date && widget.WithinBounds(date);
                foreach (var action in (ReadOnlySpan<UiAccessibilityAction>)[UiAccessibilityAction.Focus, UiAccessibilityAction.Select,
                    UiAccessibilityAction.Invoke, UiAccessibilityAction.AddToSelection, UiAccessibilityAction.Deselect])
                    if (actions.Take(cellId, action) is { } request)
                    {
                        request.Result = RadioAccessibilityAvailability(supported
                            && (action == UiAccessibilityAction.Focus || widget.Available(cell.Date!.Value)), clip);
                        if (request.Result != UiAccessibilityResult.Success) continue;
                        widget.Focus(cell.Date!.Value);
                        if (action == UiAccessibilityAction.Focus)
                        {
                            ApplyAccessibilityFocus(gridId, clip, request);
                            control = control with { Focused = true, FocusVisible = true };
                            _focusedControlSeen = true;
                            _focusedControlTier = _popupTierDepth;
                        }
                        else if (!(action == UiAccessibilityAction.Invoke ? widget.Commit(cell.Date!.Value)
                            : widget.AccessibleSelect(cell.Date!.Value, action))) request.Result = UiAccessibilityResult.InvalidOperation;
                        _requestFrame?.Invoke();
                    }
            }
        var selection = widget.Selection;
        var visibleSelection = widget.PendingStart is { } pending ? new UiCalendarDateRange(pending, pending) : selection;
        var preview = widget.PendingStart is { } rangeStart && hit is { } rangeEnd
            ? UiCalendarDateRange.FromEndpoints(rangeStart, rangeEnd) : visibleSelection;
        if (preview is { } proposed && !widget.Available(proposed)) preview = visibleSelection;
        ExposeAccessibility(gridId, _accessibilityParent, UiAccessibilityRole.Grid,
            widget.Options.AccessibleLabel ?? month.Title, new(grid.X, gridTop, grid.Width, grid.Height + layout.WeekdayHeight),
            value : widget.PendingStart is { } start ? widget.Labels.ChooseRangeEnd + ": " + widget.System.FormatFullDate(start) : null,
            focusable : false, valuePattern : false, literalLabel : true);
        _accessibility?.SetListContainer(gridId, widget.Multiple);
        _accessibility?.SetGrid(gridId, new(month.WeekCount + 1, 7));
        _accessibility?.SetListItem(gridId, null, false, false);
        _accessibilityWidget = null;
        try
        {
            bool HeaderButton(string suffix, string label, string glyph, UiRect bounds, bool enabled)
            {
                BeginDisabled(!enabled);
                try
                {
                    var interaction = ProcessButton(id + suffix, bounds, UiButtonActivation.Release);
                    var buttonId = ResolveId(id + suffix);
                    interaction = ApplyAccessibilityInvoke(buttonId, bounds, interaction);
                    ExposeAccessibility(buttonId, _accessibilityParent, UiAccessibilityRole.Button, label, bounds,
                        valuePattern : false, literalLabel : true);
                    if (interaction.Hovered) context.Frame(bounds, context.Theme.ButtonHovered, default, 4, 0);
                    var foreground = enabled ? context.Theme.Text : context.Theme.TextDisabled;
                    if (glyph is "‹" or "›")
                        DrawChevronIcon(new(bounds.X + (bounds.Width - 16) / 2, bounds.Y + (bounds.Height - 16) / 2, 16, 16),
                            glyph == "‹" ? 90 : -90, .8f, 1.2f, foreground);
                    else context.TypographyButtonText(glyph, bounds, context.MeasureText(glyph), foreground);
                    if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
                    return interaction.Activated;
                }
                finally { EndDisabled(); }
            }
            var left = new UiRect(position.X + 8, position.Y + 8, 32, layout.HeaderHeight);
            var right = left with { X = position.X + layout.Edge * 7 - 24 };
            if (HeaderButton("##prev", widget.Labels.PreviousMonth, month.RightToLeft ? "›" : "‹", month.RightToLeft ? right : left,
                widget.NavigationDate(-1, out _))) navigate = -1;
            if (widget.System is IUiCalendarPeriodSystem periods)
            {
                var first = periods.MonthBeforeYear ? widget.MonthPicker : widget.YearPicker;
                var second = periods.MonthBeforeYear ? widget.YearPicker : widget.MonthPicker;
                var firstText = context.MeasureText(first.Label).X;
                var secondText = context.MeasureText(second.Label).X;
                var available = layout.Edge * 7 - 80;
                var total = firstText + secondText + 32;
                var firstWidth = total <= available ? firstText + 16 : available * (firstText + 16) / total;
                var secondWidth = total <= available ? secondText + 16 : available - firstWidth;
                var x = header.X + (available - firstWidth - secondWidth) / 2;
                var top = position.Y + 8;
                RenderLayoutChild(first, new(x, top, firstWidth, layout.HeaderHeight), allocation);
                RenderLayoutChild(second, new(x + firstWidth, top, secondWidth, layout.HeaderHeight), allocation);
            }
            if (HeaderButton("##next", widget.Labels.NextMonth, month.RightToLeft ? "‹" : "›", month.RightToLeft ? left : right,
                widget.NavigationDate(1, out _))) navigate = 1;
            var gridStop = -1;
            for (var stopIndex = tabStart; stopIndex < _currentTabStops.Count; stopIndex++)
                if (_currentTabStops[stopIndex].Id == gridId) { gridStop = stopIndex; break; }
            if (gridStop >= 0)
            {
                var stop = _currentTabStops[gridStop];
                _currentTabStops.RemoveAt(gridStop);
                _currentTabStops.Add(stop);
            }
            for (var column = 0; column < 7; column++)
            {
                var weekday = (DayOfWeek)(((int)month.FirstDayOfWeek + column) % 7);
                var bounds = new UiRect(grid.X + month.PhysicalColumn(column) * layout.Edge, gridTop, layout.Edge, layout.WeekdayHeight);
                context.TypographyButtonText(widget.System.GetWeekdayName(weekday, true), bounds,
                    context.MeasureText(widget.System.GetWeekdayName(weekday, true)), DayColor(weekday, true));
                var node = ResolveCachedIdentifier(root, "##weekday-" + column, false);
                ExposeAccessibility(node, gridId, UiAccessibilityRole.GridCell, widget.System.GetWeekdayName(weekday, false), bounds,
                    readOnly : true, focusable : false, valuePattern : false, literalLabel : true);
                _accessibility?.SetGridItem(node, new(gridId, 0, month.PhysicalColumn(column)));
            }
            for (var i = 0; i < month.Cells.Length; i++)
            {
                var cell = month.Cells.Span[i];
                var bounds = CellBounds(i);
                var clip = IntersectRect(CurrentClipRect, bounds);
                var cellId = ResolveCachedIdentifier(root, cellKeys.Span[i], false);
                var supported = cell.IsSupported && cell.Date is { } date && widget.WithinBounds(date);
                var enabled = supported && widget.Available(cell.Date!.Value);
                var focused = control.Focused && cell.Date == widget.FocusedDate;
                if (_accessibility is { } tree)
                {
                    tree.Add(new(cellId, gridId, UiAccessibilityRole.GridCell, cellNames.Span[i], clip,
                        enabled && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, clip.Width <= 0 || clip.Height <= 0,
                        focused, ReadOnly : true, Focusable : supported, ValuePattern : false)
                    {
                        Selected = cell.Date is { } selectedDate && visibleSelection?.Contains(selectedDate) == true,
                        DisabledFocusable = supported && IsControlEnabled,
                        InvokePattern = enabled,
                        GridItem = new(gridId, i / 7 + 1, month.PhysicalColumn(i % 7))
                    }, bounds);
                    if (focused) (_accessibilityFocusOwner, _accessibilityFocusTarget) = (gridId, cellId);
                }
                var selected = cell.Date is { } candidate && preview?.Contains(candidate) == true;
                var inset = new UiRect(bounds.X + 2, bounds.Y + 2, MathF.Max(0, bounds.Width - 4), MathF.Max(0, bounds.Height - 4));
                if (selected && widget.Multiple && preview is { } band && band.Start != band.End)
                {
                    var strip = inset with { X = bounds.X, Width = bounds.Width };
                    var fill = widget.PendingStart.HasValue ? context.Theme.FrameBgHovered : context.Theme.FrameBgActive;
                    context.Frame(strip, fill, default, 4, 0);
                    var startCap = cell.Date == band.Start || i % 7 == 0;
                    var endCap = cell.Date == band.End || i % 7 == 6;
                    if (!startCap) context.Frame(strip with { X = strip.X + (month.RightToLeft ? strip.Width / 2 : 0), Width = strip.Width / 2 }, fill, default, 0, 0);
                    if (!endCap) context.Frame(strip with { X = strip.X + (month.RightToLeft ? 0 : strip.Width / 2), Width = strip.Width / 2 }, fill, default, 0, 0);
                }
                else if (selected || enabled && cell.Date == hit)
                    context.Frame(inset, selected ? context.Theme.FrameBgActive : context.Theme.ButtonHovered,
                        selected ? context.Theme.Border : default, 4, selected ? 1 : 0);
                if (cell.Date == widget.Today) context.Frame(inset, default, context.Theme.Text, 4, 1);
                var holidayName = string.Empty;
                var holiday = cell.Date is { } holidayDate && widget.EffectiveHolidays?.TryGetName(holidayDate, out holidayName) == true;
                var dayColor = cell.Date is { } dayDate ? DayColor(dayDate.DayOfWeek, enabled, cell.IsCurrentMonth, holiday) : context.Theme.TextDisabled;
                context.TypographyButtonText(cell.Text, bounds, context.MeasureText(cell.Text), dayColor);
                if (holiday && cell.Date is { } dateWithHoliday)
                {
                    context.Circle(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height - 5), 1.5f, dayColor);
                    var tooltip = widget.HolidayTip(i);
                    tooltip.Attach(this); tooltip.Text = holidayName;
                    var available = clip.Width > 0 && clip.Height > 0 && _layoutPointerBlockedTier != _popupTierDepth && _popupTierDepth >= _blockingKeyboardTier;
                    ObserveTooltipTarget<UiHoverFocusTooltipBehavior>(tooltip, holidayName, tooltip.Options, null, bounds,
                        available, available && !_layoutPointerBlocked && cell.Date == hit, focused, _tooltipTargetDepth + 1);
                }
                if (focused && control.FocusVisible) context.FocusOutline(bounds, 4);
            }
            if (HeaderButton("##today", widget.Labels.Today, widget.Labels.Today,
                new(grid.X, grid.Y + grid.Height + 8, grid.Width, 32), widget.WithinBounds(widget.Today))) today = true;
        }
        finally { _accessibilityWidget = owner; }
        if (navigate != 0) widget.NavigateMonth(navigate);
        if (navigateYear != 0) widget.NavigateYear(navigateYear);
        if (today) { widget.Focus(widget.Today); _requestFrame?.Invoke(); }
        _lastItemPos = position;
        _lastItemSize = layout.Size;
        _lastItemId = root;
        _lastItemEdited = selectionBefore != widget.Selection || pendingBefore != widget.PendingStart;
    }
}
