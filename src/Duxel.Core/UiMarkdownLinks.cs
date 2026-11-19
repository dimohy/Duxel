using System.Diagnostics;

namespace Duxel.Core;

internal readonly record struct UiMarkdownLink(UiSourceTextRange Label, string Destination, string Name = "");

/// <summary>Opens web and mail links through the operating system's registered handler.</summary>
public static class UiLinkNavigation
{
    public static void OpenExternal(Uri destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.IsAbsoluteUri || destination.Scheme is not ("http" or "https" or "mailto"))
            throw new ArgumentException("External links require an absolute HTTP, HTTPS or mailto URI. Use a navigation callback for application routes.", nameof(destination));
        using var process = Process.Start(new ProcessStartInfo(destination.AbsoluteUri) { UseShellExecute = true });
    }
}

internal sealed partial class UiMarkdownLinkOwner
{
    private WeakReference<UiImmediateContext>? _context;
    private string? _root;
    private long _revision;
    private int _frame = -1;
    private UiMarkdownLink? _pressed;
    private UiVector2 _origin;
    private UiVector2 _scroll;

    internal bool Update(UiImmediateContext context, string root, long revision, UiMarkdownLink? hovered,
        UiVector2 scroll, bool available)
    {
        var frame = context.GetFrameCount();
        if (_context is null || !_context.TryGetTarget(out var previous) || !ReferenceEquals(previous, context)
            || _root != root || _revision != revision || frame != _frame + 1 || !available)
            _pressed = null;
        if (_context is null)
            _context = new(context);
        else
            _context.SetTarget(context);
        _root = root;
        _revision = revision;
        _frame = frame;
        if (!available)
            return false;
        var point = context.GetMousePos();
        if (context.IsMouseClicked(0))
        {
            _pressed = hovered;
            _origin = point;
            _scroll = scroll;
        }
        if (MathF.Abs(point.X - _origin.X) >= 3 || MathF.Abs(point.Y - _origin.Y) >= 3 || scroll != _scroll)
            _pressed = null;
        var activated = context.IsMouseReleased(0) && _pressed is { } pressed && hovered == pressed;
        if (context.IsMouseReleased(0) || !context.IsMouseDown(0))
            _pressed = null;
        return activated;
    }

    internal void Clear()
    {
        ReleaseNavigation();
        _context = null;
        _root = null;
        _pressed = null;
        _frame = -1;
    }
}

public sealed partial class UiImmediateContext
{
    private void ProcessMarkdownLinks(string root, UiMarkdownDocument document,
        UiMultilineEditResult edit, UiMarkdownLinkOwner owner, Action<Uri> navigate)
    {
        var available = IsControlEnabled && !_layoutPointerBlocked && _popupTierDepth >= _blockingKeyboardTier
            && (_popupTierDepth != 0 || !IsMouseOverAnyBlockingPopup() && !IsMouseOverAnyOpenMenuPopup());
        var content = edit.VisualContent!.Value;
        var clip = IntersectRect(CurrentClipRect, content);
        UiMarkdownLink? hovered = null;
        if (available && IsHovering(clip))
            foreach (var entry in owner.Entries)
            {
                if (entry.Destination is null)
                    continue;
                foreach (var fragment in entry.Fragments)
                    if (IsHovering(MarkdownLinkBounds(fragment, edit)))
                    {
                        hovered = entry.Link;
                        break;
                    }
                if (hovered is not null)
                {
                    break;
                }
            }
        if (hovered.HasValue)
            SetMouseCursor(UiMouseCursor.Hand);
        var pointerActivated = owner.Update(this, root, document.Revision, hovered, new(edit.ScrollX, edit.ScrollY), available);
        var widget = _accessibilityWidget;
        _accessibilityWidget = null;
        using var clipped = new UiControlContext(this).Clip(content);
        try
        {
            foreach (var entry in owner.Entries)
            {
                var bounds = MarkdownLinkBounds(entry.Bounds, edit);
                UiVector2? clickablePoint = null;
                foreach (var fragment in entry.Fragments)
                {
                    var visible = IntersectRect(clip, MarkdownLinkBounds(fragment, edit));
                    if (visible is { Width: > 0, Height: > 0 })
                    {
                        clickablePoint = new(visible.X + visible.Width / 2, visible.Y + visible.Height / 2);
                        break;
                    }
                }
                BeginDisabled(entry.Destination is null);
                try
                {
                    var value = entry.Destination is { IsAbsoluteUri: true } uri ? uri.AbsoluteUri : null;
                    ExposeAccessibility(entry.Id, root, UiAccessibilityRole.Link,
                        entry.Link.Name.Length == 0 ? entry.Link.Destination : entry.Link.Name, bounds,
                        value, readOnly: true, valuePattern: value is not null, literalLabel: true, clickablePoint: clickablePoint);
                    var interaction = ApplyAccessibilityInvoke(entry.Id, bounds, entry.Interaction);
                    if (interaction.FocusVisible)
                        foreach (var fragment in entry.Fragments)
                            new UiControlContext(this).FocusOutline(MarkdownLinkBounds(fragment, edit), 2);
                    if (IsControlEnabled && entry.Destination is { } destination
                        && (interaction.Activated || pointerActivated && hovered == entry.Link))
                    {
                        _accessibility?.MarkInvoked(entry.Id);
                        navigate(destination);
                    }
                }
                finally { EndDisabled(); }
            }
        }
        finally { _accessibilityWidget = widget; }
    }
}
