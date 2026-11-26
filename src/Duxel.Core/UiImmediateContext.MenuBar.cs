namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderMenuBar<TPresentation>(string id, string label, UiMenuBarEntry[] entries, UiMenuInputState[] menus,
        UiRect[] itemBounds, UiButtonInteraction[] interactions, string[] ids, ref int selected, ref UiVector2 lastPointer,
        UiMenuBar<TPresentation> widget) where TPresentation : IUiMenuPresentation
    {
        var context = new UiControlContext(this);
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        var size = context.ConstrainToLayout(widget.MeasureBar(context));
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var root = ResolveId(id);
        var open = Array.FindIndex(menus, static menu => menu.Open);
        var moved = lastPointer != _mousePosition;
        lastPointer = _mousePosition;
        var originalKeys = _keyEvents;
        var parent = _accessibilityParent;
        var activate = -1;

        var focused = _state.FocusedId == root;
        var menuFocused = open >= 0 && OwnsMenuFocus(menus[open]);
        if (IsControlEnabled && (_popupTierDepth >= _blockingKeyboardTier || menuFocused))
        {
            foreach (var key in originalKeys)
            {
                if (!key.IsDown)
                    continue;
                if (key.Modifiers == KeyModifiers.Alt && key.Key != UiKey.None)
                {
                    var target = Array.FindIndex(entries, entry => entry.AccessKey == key.Key);
                    if (target < 0)
                        continue;
                    selected = target;
                    if (target != open)
                        activate = target;
                    _keyEvents = originalKeys.Where(item => item.Key != key.Key || item.Modifiers != key.Modifiers).ToArray();
                    _requestFrame?.Invoke();
                    break;
                }
                if (key.Modifiers != 0 || (!focused && !menuFocused))
                    continue;

                var direction = key.Key == UiKey.LeftArrow ? -1 : key.Key == UiKey.RightArrow ? 1 : 0;
                if (direction == 0)
                    continue;
                if (RightToLeft)
                    direction = -direction;

                if (open >= 0)
                {
                    var menu = menus[open];
                    if (menu.Child is { Open: true } || direction > 0 && (uint)menu.Focused < (uint)entries[open].Items.Length
                        && entries[open].Items[menu.Focused].SubmenuItems is not null)
                        break;
                }

                selected = (selected + direction + entries.Length) % entries.Length;
                if (open >= 0 && selected != open)
                    activate = selected;
                _keyEvents = originalKeys.Where(item => item.Key != key.Key || item.Modifiers != key.Modifiers).ToArray();
                _requestFrame?.Invoke();
                break;
            }
        }

        ExposeAccessibility(root, parent, UiAccessibilityRole.MenuBar, label, bounds, focusable: false, valuePattern: false);
        _accessibility?.SetOrientation(root, UiAccessibilityOrientation.Horizontal);
        _accessibilityParent = root;
        try
        {
            var x = RightToLeft ? bounds.X + bounds.Width - 2 : bounds.X + 2;
            using (context.Clip(bounds))
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    var width = UiMenuBar<TPresentation>.MeasureEntry(context, entries[i].Label).X;
                    var rect = new UiRect(RightToLeft ? x - width : x, bounds.Y, width, bounds.Height);
                    x += RightToLeft ? -width : width;
                    itemBounds[i] = rect;
                    var interaction = ProcessMenuTrigger(ids[i], entries[i].Label, entries[i].Items, menus[i], rect,
                        focusOwner: root, ownerFocused: i == selected, triggerRole: UiAccessibilityRole.MenuItem);
                    interactions[i] = interaction;
                    if (interaction.Activated || ReferenceEquals(_keyEvents, originalKeys) && open >= 0 && open != i && interaction.Hovered && moved)
                    {
                        selected = i;
                        activate = i;
                    }
                }

                for (var i = 0; i < entries.Length; i++)
                    PaintMenuBarEntry(context, entries[i].Label, itemBounds[i], interactions[i],
                        i == activate || activate < 0 && i == open);
            }

            if (selected >= 0 && _state.FocusedId == root && menus[selected].TriggerId is { } focusedTrigger)
                (_accessibilityFocusOwner, _accessibilityFocusTarget) = (root, focusedTrigger);

            for (var i = 0; i < entries.Length; i++)
            {
                if (activate >= 0 && i == open && open != activate)
                    continue;
                var interaction = interactions[i] with { Activated = i == activate };
                var result = RenderMenuPopup<TPresentation>(ids[i], entries[i].Label, entries[i].Items,
                    TPresentation.DefaultOptions, menus[i], itemBounds[i], interaction);
                if (menus[i].Open)
                    selected = i;
                if (result is not null)
                    entries[i].Invoked(result);
            }
        }
        finally
        {
            _keyEvents = originalKeys;
            _accessibilityParent = parent;
            FinishLayoutBox(origin, size);
        }
    }

    private static void PaintMenuBarEntry(UiControlContext context, string label, UiRect bounds,
        UiButtonInteraction button, bool open)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : button.Held || open
            ? UiButtonVisualState.Pressed : button.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentMenuPalette.Resolve(context, state);
        var surface = new UiRect(bounds.X + 1, bounds.Y + 2, bounds.Width - 2, bounds.Height - 4);
        if (state is UiButtonVisualState.Hover or UiButtonVisualState.Pressed)
            context.Frame(surface, paint.Background, default, 4, 0);
        if (button.FocusVisible)
            context.FocusOutline(surface, 4);
        var text = context.MeasureText(label);
        context.Text(label, new(bounds.X + (bounds.Width - text.X) / 2, bounds.Y + (bounds.Height - text.Y) / 2),
            paint.Foreground);
    }
}
