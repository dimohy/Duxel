using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class PersonaPage(ObserverServices services) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(services)).Samples;
    public string Title => "인물 정보";
    public string Description => "Display a name, up to four lines of information, and an avatar. Compare a status-only version in place of the avatar.";
    internal UiFluentPersona? Preview => _content?.Preview;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new(services)).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new(services)).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        private readonly ObserverServices _services;
        private readonly SvgStore _store = new();
        private readonly UiImageSource _image;
        private string _name = GalleryText.Get("Dimoi");
        private int _lines = 4;
        internal UiFluentPersona Preview
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
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "artwork.svg");
            _image = new(new SvgImageDecoder(_store, _services.ImageDecoder).Decode(path));
            var content = ContentForLines();
            var options = new UiPersonaOptions();
            var avatarOptions = new UiAvatarOptions(Color : UiAvatarColor.Brand, Presence : new());
            var sample = GallerySample.Create(() =>
            {
                var persona = Fluent.Persona(content, options);
                persona.Avatar.Options = avatarOptions;
                persona.Avatar.PresenceText = GalleryText.Get("Available");
                return persona;
            });
            Preview = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => (Preview.Content, Preview.Options, Preview.Avatar.Options.IsInactive,
                HasImage : Preview.Avatar.Source is not null), state =>
            {
                var text = state.Content;
                var appearance = state.Options;
                var source = $"var content = new UiPersonaContent({GallerySample.String(text.Name)},\n"
                    + $"    PrimaryText : {GallerySample.String(text.PrimaryText)}, SecondaryText : {GallerySample.String(text.SecondaryText)},\n"
                    + $"    TertiaryText : {GallerySample.String(text.TertiaryText)}, QuaternaryText : {GallerySample.String(text.QuaternaryText)});\n"
                    + $"var options = new UiPersonaOptions(Size : UiPersonaSize.{appearance.Size},\n"
                    + $"    TextPosition : UiPersonaTextPosition.{appearance.TextPosition}, TextAlignment : UiPersonaTextAlignment.{appearance.TextAlignment},\n"
                    + $"    PresenceOnly : {GallerySample.Boolean(appearance.PresenceOnly)}, RightToLeft : {GallerySample.Boolean(appearance.RightToLeft)}, ShowMedia : {GallerySample.Boolean(appearance.ShowMedia)});\n"
                    + $"var avatarOptions = new UiAvatarOptions(Color : UiAvatarColor.Brand, Presence : new(), IsInactive : {GallerySample.Boolean(state.IsInactive)});\n\n"
                    + factory;
                if (state.HasImage)
                    source = "using Duxel.Svg;\n\n" + source + "\n\n" + """
                        using var store = new SvgStore();
                        var artwork = store.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "artwork.svg"));
                        var size = artwork.IntrinsicSize;
                        var raster = artwork.Rasterize(checked((int)MathF.Ceiling(size.X)), checked((int)MathF.Ceiling(size.Y)));
                        ((UiFluentPersona)example).Avatar.Source = new UiImageSource(raster.CopyImageData());
                        """;
                return source;
            });
            Samples = [sample];
            var name = Fluent.Input(GalleryText.Get("Persona name"), new UiBinding<string>(() => _name, value => { _name = value; UpdateContent(); }), 100, 280);
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                name,
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Change persona size"), () => Preview.Options = Preview.Options with { Size = (UiPersonaSize)(((int)Preview.Options.Size + 1) % 6) }),
                    Fluent.Button(GalleryText.Get("Change persona text position"), () => Preview.Options = Preview.Options with { TextPosition = (UiPersonaTextPosition)(((int)Preview.Options.TextPosition + 1) % 3) }),
                    Fluent.Button(GalleryText.Get("Change persona alignment"), () => Preview.Options = Preview.Options with { TextAlignment = Preview.Options.TextAlignment == UiPersonaTextAlignment.Start ? UiPersonaTextAlignment.Center : UiPersonaTextAlignment.Start }),
                    Fluent.Button(GalleryText.Get("Reverse persona direction"), () => Preview.Options = Preview.Options with { RightToLeft = !Preview.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Get("Change the number of information lines"), () => { _lines = _lines % 4 + 1; UpdateContent(); })),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Toggle status-only persona"), () => Preview.Options = Preview.Options with { PresenceOnly = !Preview.Options.PresenceOnly }),
                    Fluent.Button(GalleryText.Get("Toggle persona media"), () => Preview.Options = Preview.Options with { ShowMedia = !Preview.Options.ShowMedia }),
                    Fluent.Button(GalleryText.Get("Show persona SVG image"), () => Preview.Avatar.Source = _image),
                    Fluent.Button(GalleryText.Get("Show persona initials"), () => Preview.Avatar.Source = null),
                    Fluent.Button(GalleryText.Get("Toggle inactive persona"), () => Preview.Avatar.Options = Preview.Avatar.Options with { IsInactive = !Preview.Avatar.Options.IsInactive })),
                Fluent.Body1(() => GalleryText.Format("Size: {0} / Position: {1} / Status only: {2} / Information lines: {3}",
                    Preview.Options.Size, Preview.Options.TextPosition, Preview.Options.PresenceOnly, _lines)),
                Fluent.Text(GalleryText.Get("The avatar and status badge use the same rendering as Avatar and PresenceBadge. The example reuses its previous measurements when the layout stays the same.")),
                Fluent.Text(GalleryText.Get("Your edits and display options stay in place when you leave this page and return.")),
                sample));
        }
        private void UpdateContent() => Preview.Content = ContentForLines();

        private UiPersonaContent ContentForLines() => new(_name,
            SecondaryText : _lines >= 2 ? GalleryText.Get("User interface development") : null,
            TertiaryText : _lines >= 3 ? GalleryText.Get("Duxel project") : null,
            QuaternaryText : _lines >= 4 ? GalleryText.Get("Hangul, SVG, and accessibility") : null);
        public void Dispose()
        {
            Root.Dispose();
            _store.Dispose();
        }
    }
}
