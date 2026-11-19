namespace Duxel.Core;

/// <summary>One consumer's paint resources, independent of its borrowed avatar's configured size and lifetime.</summary>
internal sealed class UiAvatarPaintBinding<TPresentation>(UiAvatar<TPresentation> avatar) : IDisposable where TPresentation : IUiAvatarPresentation
{
    private UiAvatar<TPresentation>? _avatar = avatar;
    private UiAvatarPaintGroup<TPresentation>? _inactive;
    private UiBoxShadow? _shadow;
    internal UiAvatar<TPresentation> Owner => _avatar ?? throw new ObjectDisposedException(nameof(UiAvatarPaintBinding<TPresentation>));
    internal void Paint(UiControlContext context, UiRect bounds, UiAvatarOptions options)
    {
        var owner = Owner;
        if (options.IsInactive)
            _inactive ??= new();
        else
        {
            _inactive?.Dispose();
            _inactive = null;
        }
        if (options.VisibleOptions.ShowActivityShadow)
            _shadow ??= new(default);
        owner.PaintMedia(context, bounds, options, _inactive, _shadow);
    }
    public void Dispose()
    {
        _inactive?.Dispose();
        _shadow?.Dispose();
        _inactive = null;
        _shadow = null;
        _avatar = null;
    }
}
