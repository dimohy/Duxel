using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class PersonaPage(ObserverServices services) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(services)).Samples;
    public string Title => "인물 정보";
    public string Description => "이름과 최대 네 줄의 정보, 아바타를 함께 표시합니다. 아바타 대신 상태만 표시하는 모습도 비교해 보세요.";
    internal UiFluentPersona? Preview => _content?.Preview;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new(services)).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new(services)).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        private readonly ObserverServices _services;
        private readonly SvgStore _store = new();
        private readonly UiImageSource _image;
        private string _name = "디모이";
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
            var content = new UiPersonaContent("디모이", SecondaryText : "사용자 인터페이스 개발",
                TertiaryText : "Duxel 프로젝트", QuaternaryText : "한글·SVG·접근성");
            var options = new UiPersonaOptions();
            var avatarOptions = new UiAvatarOptions(Color : UiAvatarColor.Brand, Presence : new());
            var sample = GallerySample.Create(() =>
            {
                var persona = Fluent.Persona(content, options);
                persona.Avatar.Options = avatarOptions;
                persona.Avatar.PresenceText = "사용 가능";
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
            var name = Fluent.Input("인물 이름", new UiBinding<string>(() => _name, value => { _name = value; UpdateContent(); }), 100, 280);
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                name,
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("인물 크기", () => Preview.Options = Preview.Options with { Size = (UiPersonaSize)(((int)Preview.Options.Size + 1) % 6) }),
                    Fluent.Button("인물 텍스트 위치", () => Preview.Options = Preview.Options with { TextPosition = (UiPersonaTextPosition)(((int)Preview.Options.TextPosition + 1) % 3) }),
                    Fluent.Button("인물 정렬", () => Preview.Options = Preview.Options with { TextAlignment = Preview.Options.TextAlignment == UiPersonaTextAlignment.Start ? UiPersonaTextAlignment.Center : UiPersonaTextAlignment.Start }),
                    Fluent.Button("인물 방향", () => Preview.Options = Preview.Options with { RightToLeft = !Preview.Options.RightToLeft }),
                    Fluent.Button("인물 정보 줄 수", () => { _lines = _lines % 4 + 1; UpdateContent(); })),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("인물 상태 전용", () => Preview.Options = Preview.Options with { PresenceOnly = !Preview.Options.PresenceOnly }),
                    Fluent.Button("인물 미디어 표시", () => Preview.Options = Preview.Options with { ShowMedia = !Preview.Options.ShowMedia }),
                    Fluent.Button("인물 SVG 이미지", () => Preview.Avatar.Source = _image),
                    Fluent.Button("인물 이니셜", () => Preview.Avatar.Source = null),
                    Fluent.Button("인물 비활성 표현", () => Preview.Avatar.Options = Preview.Avatar.Options with { IsInactive = !Preview.Avatar.Options.IsInactive })),
                Fluent.Body1(() => $"크기 : {Preview.Options.Size} / 위치 : {Preview.Options.TextPosition} / 상태 전용 : {Preview.Options.PresenceOnly} / 정보 : {_lines}줄"),
                Fluent.Text("아바타와 상태 배지는 각각 Avatar와 PresenceBadge를 그릴 때와 같은 방식을 사용합니다. 배치가 바뀌지 않으면 이전 측정 결과를 재사용합니다."),
                Fluent.Text("이름을 바꾸거나 다른 페이지로 이동했다 돌아와도 입력한 내용과 표시 설정은 유지됩니다."),
                sample));
        }
        private void UpdateContent() => Preview.Content = new(_name,
            SecondaryText : _lines >= 2 ? "사용자 인터페이스 개발" : null,
            TertiaryText : _lines >= 3 ? "Duxel 프로젝트" : null,
            QuaternaryText : _lines >= 4 ? "한글·SVG·접근성" : null);
        public void Dispose()
        {
            Root.Dispose();
            _store.Dispose();
        }
    }
}
