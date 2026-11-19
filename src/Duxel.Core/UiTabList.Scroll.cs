namespace Duxel.Core;

/// <summary>Horizontal drag scrolling with edge actions and a transient overlay thumb.</summary>
public readonly record struct UiTabListScrollOptions(string PreviousLabel = "Previous tabs", string NextLabel = "Next tabs")
{
    public UiScrollbarMotionOptions? Motion
    {
        get; init;
    }
    public UiTabListScrollOptions() : this("Previous tabs", "Next tabs") { }
    public UiTabListScrollOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PreviousLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(NextLabel);
        Motion?.Validate();
        return this;
    }
}

public partial class UiTabList<TBehavior, TPresentation> where TBehavior : IUiTabListBehavior where TPresentation : IUiTabListPresentation
{
    private readonly UiHorizontalScrollState _scroll = new();
    private readonly UiMenuInputState _scrollPreviousMenu = new(), _scrollNextMenu = new();
    private UiMenuOption[] _scrollPreviousChoices = [], _scrollNextChoices = [];
    private int _scrollMenuFocus;
    private bool _scrollRightToLeft;

    public float ScrollOffset => _scroll.Offset;
    public float MaximumScrollOffset => _scroll.Maximum;
    public bool IsScrollBarVisible => Options.Scroll is not null && _scroll.Visible;
    public float ScrollBarOpacity => _scroll.OpacityValue;

    private void RenderScrollingTabs(UiImmediateContext ui, UiTabListScrollOptions options)
    {
        var context = new UiControlContext(ui);
        var tabOptions = EffectiveOptions(context);
        var selected = Value.Value;
        var natural = MeasureTabs(context, selected);
        var size = context.LayoutConstraints.Constrain(context.ConstrainToLayout(natural));
        var origin = ui.AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var id = PrimaryInputId(Label);
        ui.CancelMenuPopup(_overflowMenu, id);
        OverflowResult = null;
        _input.Visibility = null;
        _input.OverflowFocused = false;
        _input.DeferPublication = true;
        _scroll.ContentWidth = natural.X;
        _scroll.Maximum = MathF.Max(0, natural.X - size.X);
        _scroll.Offset = Math.Clamp(_scroll.Offset, 0, _scroll.Maximum);
        if (_scrollRightToLeft != tabOptions.RightToLeft)
        {
            _scroll.SetOffset(_scroll.Maximum - _scroll.Offset);
            _scrollRightToLeft = tabOptions.RightToLeft;
            _scroll.Selected = null;
        }
        var edge = MathF.Min(TPresentation.MeasureOverflow(context, tabOptions).X, size.X / 2);
        if (_scroll.Selected != selected)
            RevealScrollTab(selected, size.X, edge, tabOptions.RightToLeft);
        _scroll.Selected = selected;
        ui.ProcessHorizontalScroll(bounds, edge, _scroll, UiImmediateContext.TabScrollThumb(bounds, _scroll), _scroll.Visible);
        _input.SuppressPointerActivation = _scroll.SuppressClick;
        ArrangeScrollingHeader(size, edge, origin);
        UiTabListInteraction interaction;
        using (context.Clip(bounds))
            interaction = TBehavior.Process(context, id, bounds, _items, _layouts, selected, tabOptions, _input);
        var changedFocus = interaction.Control.Focused && ui.TabScrollNavigationRequested;
        if (ui.TabScrollNavigationRequested)
            _scrollMenuFocus = 0;
        var clickedTab = interaction.Control.Activated && interaction.HoveredIndex >= 0 && !_input.SuppressPointerActivation;
        if (changedFocus || _input.Value != selected || clickedTab)
        {
            // Recreate intrinsic geometry before applying the updated scroll translation.
            MeasureTabs(context, _input.Value);
            RevealScrollTab(changedFocus ? _input.FocusedValue : _input.Value, size.X, edge, tabOptions.RightToLeft);
            _scroll.Selected = _input.Value;
            ArrangeScrollingHeader(size, edge, origin);
        }
        var previousBounds = new UiRect(bounds.X, bounds.Y, edge, size.Y);
        var nextBounds = new UiRect(bounds.X + size.X - edge, bounds.Y, edge, size.Y);
        var previous = _scroll.Offset > .01f;
        var next = _scroll.Offset < _scroll.Maximum - .01f;
        UiButtonInteraction previousInput = default, nextInput = default;
        UpdateScrollChoices(size.X, edge);
        string? invoked = null;
        if (previous)
        {
            using (context.Clip(bounds))
                previousInput = ui.ProcessMenuTrigger(id + "##scroll-previous", options.PreviousLabel, _scrollPreviousChoices,
                    _scrollPreviousMenu, previousBounds, publish: false, focusOwner: ui.GetID(id), ownerFocused: _scrollMenuFocus == -1);
            invoked = ui.RenderMenuPopup<UiFluentMenuPresentation>(id + "##scroll-previous", options.PreviousLabel,
                _scrollPreviousChoices, UiFluentMenuPresentation.DefaultOptions with
                {
                    RightToLeft = tabOptions.RightToLeft
                },
                _scrollPreviousMenu, previousBounds, previousInput);
            if (previousInput.Focused || _scrollPreviousMenu.Open)
                _scrollMenuFocus = -1;
        }
        else
            ui.CancelMenuPopup(_scrollPreviousMenu, id);
        if (next)
        {
            using (context.Clip(bounds))
                nextInput = ui.ProcessMenuTrigger(id + "##scroll-next", options.NextLabel, _scrollNextChoices,
                    _scrollNextMenu, nextBounds, publish: false, focusOwner: ui.GetID(id), ownerFocused: _scrollMenuFocus == 1);
            invoked = ui.RenderMenuPopup<UiFluentMenuPresentation>(id + "##scroll-next", options.NextLabel,
                _scrollNextChoices, UiFluentMenuPresentation.DefaultOptions with
                {
                    RightToLeft = tabOptions.RightToLeft
                },
                _scrollNextMenu, nextBounds, nextInput) ?? invoked;
            if (nextInput.Focused || _scrollNextMenu.Open)
                _scrollMenuFocus = 1;
        }
        else
            ui.CancelMenuPopup(_scrollNextMenu, id);
        if (invoked is not null)
        {
            _input.Select(invoked);
            _scrollMenuFocus = 0;
            MeasureTabs(context, _input.Value);
            RevealScrollTab(invoked, size.X, edge, tabOptions.RightToLeft);
            _scroll.Selected = invoked;
            ArrangeScrollingHeader(size, edge, origin);
        }
        ui.CommitTabList(id, bounds, _items, _layouts, _input, interaction);
        using (context.Clip(_input.HeaderClip!.Value))
        {
            for (var i = 0; i < _items.Length; i++)
            {
                var rect = _layouts[i] with
                {
                    X = bounds.X + _layouts[i].X,
                    Y = bounds.Y + _layouts[i].Y
                };
                if (rect.X + rect.Width <= _input.HeaderClip.Value.X || rect.X >= _input.HeaderClip.Value.X + _input.HeaderClip.Value.Width)
                    continue;
                var visual = new UiButtonInteraction(false, i == interaction.HoveredIndex && !_scroll.Dragging,
                    i == interaction.HeldIndex && !_scroll.Dragging)
                {
                    Focused = interaction.Control.Focused && i == interaction.FocusedIndex,
                    FocusVisible = interaction.Control.FocusVisible && i == interaction.FocusedIndex
                };
                ui.BeginDisabled(!_items[i].Enabled);
                try
                {
                    PaintTab(context, _items[i], rect, visual);
                }
                finally { ui.EndDisabled(); }
            }
            PaintSelectionIndicator(ui, context, origin, interaction, _scroll.Offset);
        }
        using (context.Clip(bounds))
        {
            if (_scroll.Offset > .01f)
            {
                ui.PublishTabScrollAction(id + "##scroll-previous", options.PreviousLabel, previousBounds, ui.GetID(id), previousInput, _scrollPreviousMenu);
                TPresentation.PaintOverflow(context, previousBounds, previousInput, tabOptions);
            }
            if (_scroll.Offset < _scroll.Maximum - .01f)
            {
                ui.PublishTabScrollAction(id + "##scroll-next", options.NextLabel, nextBounds, ui.GetID(id), nextInput, _scrollNextMenu);
                TPresentation.PaintOverflow(context, nextBounds, nextInput, tabOptions);
            }
            ui.PaintTabScrollOverlay(bounds, _scroll, options);
        }
        ui.FinishLayoutBox(origin, size);
        CompleteTabFrame();
    }

    private void UpdateScrollChoices(float width, float edge)
    {
        var left = _scroll.Offset > .01f ? edge : 0;
        var right = width - (_scroll.Offset < _scroll.Maximum - .01f ? edge : 0);
        UpdateChoices(ref _scrollPreviousChoices, true);
        UpdateChoices(ref _scrollNextChoices, false);
        void UpdateChoices(ref UiMenuOption[] choices, bool leading)
        {
            var count = 0;
            var matches = true;
            for (var i = 0; i < _items.Length; i++)
            {
                if (!(leading ? _layouts[i].X < left : _layouts[i].X + _layouts[i].Width > right))
                    continue;
                var item = new UiMenuOption(_items[i].Value, _items[i].Label, _items[i].Enabled);
                if (count >= choices.Length || choices[count] != item)
                    matches = false;
                count++;
            }
            if (matches && choices.Length == count)
                return;
            choices = new UiMenuOption[count];
            count = 0;
            for (var i = 0; i < _items.Length; i++)
                if (leading ? _layouts[i].X < left : _layouts[i].X + _layouts[i].Width > right)
                    choices[count++] = new(_items[i].Value, _items[i].Label, _items[i].Enabled);
        }
    }
    private void ReleaseScrollingMenus()
    {
        foreach (var menu in new[] { _scrollPreviousMenu, _scrollNextMenu })
        {
            menu.Open = false;
            menu.ScrollOwner.Release();
            menu.ItemIds = [];
            menu.IdentityItems = null;
        }
        _scrollPreviousChoices = _scrollNextChoices = [];
    }

    private void ArrangeScrollingHeader(UiVector2 size, float edge, UiVector2 origin)
    {
        var leading = _scroll.Offset > .01f ? edge : 0;
        var trailing = _scroll.Offset < _scroll.Maximum - .01f ? edge : 0;
        _input.HeaderClip = new(origin.X + leading, origin.Y, MathF.Max(0, size.X - leading - trailing), size.Y);
        for (var i = 0; i < _layouts.Length; i++)
            _layouts[i] = _layouts[i] with
            {
                X = _layouts[i].X - _scroll.Offset,
                Height = size.Y
            };
    }
    private void RevealScrollTab(string? value, float width, float edge, bool rightToLeft)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_items[i].Value != value)
                continue;
            var rect = _layouts[i];
            var offset = _scroll.Offset;
            var left = offset + (offset > 0 ? edge : 0);
            var right = offset + width - (offset < _scroll.Maximum ? edge : 0);
            var previous = rightToLeft ? i + 1 : i - 1;
            var next = rightToLeft ? i - 1 : i + 1;
            var previousHidden = (uint)previous < (uint)_items.Length && _layouts[previous].X < left;
            var nextHidden = (uint)next < (uint)_items.Length && _layouts[next].X + _layouts[next].Width > right;
            var targetLeft = rect.X;
            var targetRight = rect.X + rect.Width;
            if (previousHidden && (rect.X < left || targetRight <= right && rect.X - left <= right - targetRight))
                targetLeft = _layouts[previous].X;
            else if (nextHidden)
                targetRight = _layouts[next].X + _layouts[next].Width;
            if (rect.Width >= width - edge * 2)
                offset = rect.X - (rect.X > 0 ? edge : 0);
            else if (targetLeft < left)
                offset = targetLeft - (targetLeft > 0 ? edge : 0);
            else if (targetRight > right)
                offset = targetRight - width + (targetRight < _scroll.ContentWidth ? edge : 0);
            // Keep the selected tab reachable even when its neighbour is wider than the available strip.
            if (rect.Width < width - edge * 2)
                offset = Math.Clamp(offset, MathF.Max(0, rect.X + rect.Width - width + edge), MathF.Max(0, rect.X - edge));
            _scroll.SetOffset(offset);
            return;
        }
    }
}

public sealed partial class UiImmediateContext
{
    internal bool TabScrollNavigationRequested => _keyEvents.Any(key => key.IsDown
        && key.Key is UiKey.LeftArrow or UiKey.RightArrow or UiKey.Home or UiKey.End);
    internal void PublishTabScrollAction(string id, string label, UiRect bounds, string parent,
        UiButtonInteraction interaction, UiMenuInputState menu)
    {
        var root = ResolveId(id);
        var clip = IntersectRect(CurrentClipRect, bounds);
        _accessibility?.Add(new(root, parent, UiAccessibilityRole.Button, label, clip,
            IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, clip.Width <= 0 || clip.Height <= 0,
            interaction.Focused, ValuePattern: false)
        {
            Expansion = menu.Open ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed
        }, bounds);
        if (interaction.Focused)
            (_accessibilityFocusOwner, _accessibilityFocusTarget) = (parent, root);
    }
    internal static UiRect TabScrollThumb(UiRect bounds, UiHorizontalScrollState state)
    {
        var width = MathF.Min(bounds.Width, MathF.Max(24, bounds.Width * bounds.Width / MathF.Max(1, state.ContentWidth)));
        var x = state.Maximum > 0 ? state.Offset / state.Maximum * (bounds.Width - width) : 0;
        return new(bounds.X + x, bounds.Y + bounds.Height - 3, width, 3);
    }
    internal void PaintTabScrollOverlay(UiRect bounds, UiHorizontalScrollState state, UiTabListScrollOptions options)
    {
        var clip = IntersectRect(bounds, CurrentClipRect);
        if (state.Maximum == 0 || clip.Width <= 0 || clip.Height <= 0)
        {
            state.Motion.Reset();
            state.OpacityValue = 0;
            state.Visible = false;
            return;
        }
        var motion = (options.Motion ?? ScrollbarMotionOptions) with { IdleOpacity = 0 };
        state.OpacityValue = SampleScrollbarMotion(ref state.Motion, state.Offset, state.Dragging, false, motion);
        state.Visible = state.Motion.TargetVisible || state.OpacityValue > 0;
        PaintScrollbarVisual(new(bounds.X, bounds.Y + bounds.Height - 3, bounds.Width, 3), TabScrollThumb(bounds, state),
            clip, state.OpacityValue, state.Dragging, false, 1.5f);
    }
}
