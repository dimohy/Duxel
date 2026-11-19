namespace Duxel.Core;

/// <summary>Releases popup ownership and scroll references when a composite changes context or is disposed.</summary>
internal sealed class UiOwnedMenuState : IDisposable
{
    internal readonly UiMenuInputState Menu = new();
    private WeakReference<UiImmediateContext>? _context;
    internal bool Attach(UiImmediateContext ui)
    {
        if (_context is not null && _context.TryGetTarget(out var previous))
        {
            if (ReferenceEquals(previous, ui))
                return false;
            previous.CancelMenuPopup(Menu);
        }
        // Publication stamps belong to one context even when its frame number and element IDs match another context.
        Menu.PublishedFrame = int.MinValue;
        Menu.PublishedRoot = null;
        _context ??= new(ui);
        _context.SetTarget(ui);
        return true;
    }
    public void Dispose()
    {
        if (_context is not null && _context.TryGetTarget(out var ui))
            ui.CancelMenuPopup(Menu);
        Menu.Open = false;
        Menu.PublishedFrame = int.MinValue;
        Menu.PublishedRoot = null;
        Menu.ScrollOwner.Release();
        Menu.ItemIds = [];
        Menu.IdentityItems = null;
        Menu.Checked = [];
        Menu.Groups = [];
        Menu.GroupIds = [];
        Menu.GroupIndex = Menu.GroupStarts = Menu.GroupEnds = [];
        Menu.Selection = [];
        _context = null;
    }
}
