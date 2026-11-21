using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class AvatarGroupPage(ObserverServices services) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(services)).Samples;
    public string Title => "아바타 그룹";
    public string Description => "Place avatars side by side, overlap them, or group them in a single circle. Open the list to see hidden members.";
    internal UiFluentAvatarGroup? Preview => _content?.Group;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new(services)).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new(services)).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable

    {
        private readonly ObserverServices _services;
        private readonly SvgStore _store = new();
        private readonly UiSvgIcon _icon;
        internal UiFluentAvatarGroup Group
        {
            get;
        }
        internal UiWidget Root
        {
            get;
        }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal Content(ObserverServices services)
        {
            _services = services;
            var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
            var image = new UiImageSource(new SvgImageDecoder(_store, _services.ImageDecoder).Decode(Path.Combine(directory, "artwork.svg")));
            _icon = new(_store.Load(Path.Combine(directory, "regular.svg")), _store.Load(Path.Combine(directory, "filled.svg")));
            var icon = _icon;
            var selection = GalleryText.Get("No member selected.");
            var options = new UiAvatarGroupOptions(OverflowLabel : GalleryText.Get("Hidden members"));
            var sample = GallerySample.Create(() =>
            {
                UiAvatarGroupItem[] members =
                [
                    new("ada", "Ada Lovelace") { Source = image },
                    new("jeong", GalleryText.Get("Dimoi")),
                    new("kim", GalleryText.Get("Minsoo Kim")) { ShowActivityRing = true },
                    new("grace", "Grace Hopper") { Presence = new() },
                    new("alan", "Alan Turing"),
                    new("team", GalleryText.Get("Design team")) { Initials = "", Icon = icon.MenuIcon }
                ];
                var names = members.ToDictionary(item => item.Value, item => item.Name, StringComparer.Ordinal);
                return Fluent.AvatarGroup(GalleryText.Get("Document collaborators"), members, options, value => selection = names[value]);
            });
            Group = sample.Preview;
            var factory = sample.Source;
            const string resources = """
                using Duxel.Svg;

                using var store = new SvgStore();
                var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
                var artwork = store.Load(Path.Combine(directory, "artwork.svg"));
                var size = artwork.IntrinsicSize;
                var raster = artwork.Rasterize(checked((int)MathF.Ceiling(size.X)), checked((int)MathF.Ceiling(size.Y)));
                var image = new UiImageSource(raster.CopyImageData());
                using var icon = new UiSvgIcon(store.Load(Path.Combine(directory, "regular.svg")),
                    store.Load(Path.Combine(directory, "filled.svg")));
                """;
            sample.BindSource(() => (Group.Options, Selection : selection), state => resources + "\n\n"
                + $"var selection = {GallerySample.String(state.Selection)};\n"
                + $"var options = new UiAvatarGroupOptions(Size : UiAvatarSize.{state.Options.Size},\n"
                + $"    Layout : UiAvatarGroupLayout.{state.Options.Layout}, MaximumInlineItems : {state.Options.MaximumInlineItems},\n"
                + $"    RightToLeft : {GallerySample.Boolean(state.Options.RightToLeft)}, OverflowLabel : {GallerySample.String(state.Options.OverflowLabel)});\n\n"
                + factory);
            Samples = [sample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Change group layout"), () => Group.Options = Group.Options with
                    {
                        Layout = (UiAvatarGroupLayout)(((int)Group.Options.Layout + 1) % 3)
                    }),
                    Fluent.Button(GalleryText.Get("Change group size"), () => Group.Options = Group.Options with
                    {
                        Size = Group.Options.Size == UiAvatarSize.Size32 ? UiAvatarSize.Size64 : UiAvatarSize.Size32
                    }),
                    Fluent.Button(GalleryText.Get("Change the visible member count"), () => Group.Options = Group.Options with
                    {
                        MaximumInlineItems = Group.Options.MaximumInlineItems == 5 ? 2 : 5
                    }),
                    Fluent.Button(GalleryText.Get("Reverse group direction"), () => Group.Options = Group.Options with { RightToLeft = !Group.Options.RightToLeft })),
                Fluent.Body1(() => GalleryText.Get("Selected: ") + selection),
                Fluent.Body1(() => GalleryText.Format("Layout: {0}", Group.Options.Layout switch
                {
                    UiAvatarGroupLayout.Spread => GalleryText.Get("Side by side"),
                    UiAvatarGroupLayout.Stack => GalleryText.Get("Overlapping"),
                    _ => GalleryText.Get("Pie")
                })),
                Fluent.Text(GalleryText.Get("Select the avatar with a number to open the hidden members. In the pie layout, select anywhere in the circle to open the full list.")),
                Fluent.Text(GalleryText.Get("Side-by-side avatars show both activity rings and status badges. Overlapping avatars show rings, and the pie layout hides both indicators.")),
                Fluent.Text(GalleryText.Get("Use Tab, Enter, and Escape to operate the member list. Settings stay in place when you leave this page and return.")),
                sample));
        }
        public void Dispose()
        {
            Root.Dispose();
            _icon.Dispose();
            _store.Dispose();
        }
    }
}
