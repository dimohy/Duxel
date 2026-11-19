namespace Duxel.Core;

/// <summary>Borrowed decorative media. Each tag binds its own paint resources while borrowing the avatar or image.</summary>
public readonly record struct UiTagMedia
{
    private readonly Func<MediaBinding>? _create;
    private readonly MediaBinding? _binding;
    private UiTagMedia(Func<MediaBinding> create) => _create = create;
    private UiTagMedia(MediaBinding binding) => _binding = binding;
    public bool IsEmpty => _create is null && _binding is null;
    internal UiTagMedia Bind() => IsEmpty ? default : new((_create ?? throw new InvalidOperationException("Tag media is already bound."))());
    internal void Release() => _binding?.Dispose();
    internal static int PixelSize(UiTagOptions options) => options.Size switch
    {
        UiTagSize.Medium => 28,
        UiTagSize.Small => 20,
        UiTagSize.ExtraSmall => 16,
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };
    public static UiTagMedia Avatar<TPresentation>(UiAvatar<TPresentation> avatar) where TPresentation : IUiAvatarPresentation
    {
        ArgumentNullException.ThrowIfNull(avatar);
        return new(() => new AvatarBinding<TPresentation>(avatar));
    }
    public static UiTagMedia Image(UiImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new(() => new ImageBinding(image));
    }
    /// <summary>Called by a tag presentation with the bound media supplied in its render content.</summary>
    public void Paint(UiControlContext context, UiRect bounds, UiTagOptions options)
    {
        var binding = _binding ?? throw new InvalidOperationException("Tag media must be bound by its owning tag before painting.");
        using var clip = context.Clip(bounds);
        binding.Paint(context, bounds, options);
    }
    private abstract class MediaBinding : IDisposable
    {
        internal abstract void Paint(UiControlContext context, UiRect bounds, UiTagOptions options);
        public abstract void Dispose();
    }
    private sealed class ImageBinding(UiImage image) : MediaBinding
    {
        private UiImage? _image = image;
        internal override void Paint(UiControlContext context, UiRect bounds, UiTagOptions options)
            => context.Image(_image ?? throw new ObjectDisposedException(nameof(UiTagMedia)), bounds, new(255, 255, 255),
                options.Shape == UiTagShape.Circular ? bounds.Height / 2 : 4, UiImageFit.Cover);
        public override void Dispose() => _image = null;
    }
    private sealed class AvatarBinding<TPresentation>(UiAvatar<TPresentation> avatar) : MediaBinding where TPresentation : IUiAvatarPresentation
    {
        private readonly UiAvatarPaintBinding<TPresentation> _paint = new(avatar);
        internal override void Paint(UiControlContext context, UiRect bounds, UiTagOptions options)
        {
            var resolved = _paint.Owner.Options with
            {
                Size = (UiAvatarSize)PixelSize(options),
                Shape = options.Shape == UiTagShape.Circular ? UiAvatarShape.Circular : UiAvatarShape.Square,
                RightToLeft = options.RightToLeft
            };
            _paint.Paint(context, bounds, resolved);
        }
        public override void Dispose() => _paint.Dispose();
    }
}
