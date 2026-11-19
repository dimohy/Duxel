using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class AvatarGroupPage(ObserverServices services) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(services)).Samples;
    public string Title => "아바타 그룹";
    public string Description => "아바타를 나란히 놓거나 겹쳐 보고, 하나의 원으로 묶어 보세요. 숨겨진 구성원 목록도 열어 볼 수 있습니다.";
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
            var selection = "선택한 구성원이 없습니다.";
            var options = new UiAvatarGroupOptions(OverflowLabel : "숨긴 구성원");
            var sample = GallerySample.Create(() =>
            {
                UiAvatarGroupItem[] members =
                [
                    new("ada", "Ada Lovelace") { Source = image },
                    new("jeong", "디모이"),
                    new("kim", "김민수") { ShowActivityRing = true },
                    new("grace", "Grace Hopper") { Presence = new() },
                    new("alan", "Alan Turing"),
                    new("team", "디자인 팀") { Initials = "", Icon = icon.MenuIcon }
                ];
                var names = members.ToDictionary(item => item.Value, item => item.Name, StringComparer.Ordinal);
                return Fluent.AvatarGroup("문서 공동 작업자", members, options, value => selection = names[value]);
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
                    Fluent.Button("그룹 배치 변경", () => Group.Options = Group.Options with
                    {
                        Layout = (UiAvatarGroupLayout)(((int)Group.Options.Layout + 1) % 3)
                    }),
                    Fluent.Button("그룹 크기 변경", () => Group.Options = Group.Options with
                    {
                        Size = Group.Options.Size == UiAvatarSize.Size32 ? UiAvatarSize.Size64 : UiAvatarSize.Size32
                    }),
                    Fluent.Button("그룹 표시 수 변경", () => Group.Options = Group.Options with
                    {
                        MaximumInlineItems = Group.Options.MaximumInlineItems == 5 ? 2 : 5
                    }),
                    Fluent.Button("그룹 방향 전환", () => Group.Options = Group.Options with { RightToLeft = !Group.Options.RightToLeft })),
                Fluent.Body1(() => $"선택 : {selection}"),
                Fluent.Body1(() => $"배치 : {(Group.Options.Layout == UiAvatarGroupLayout.Spread ? "펼침" : Group.Options.Layout == UiAvatarGroupLayout.Stack ? "겹침" : "원 분할")}"),
                Fluent.Text("숫자가 표시된 아바타를 누르면 숨겨진 구성원 목록이 열립니다. 원을 나눈 모양에서는 원 안의 어느 곳을 눌러도 전체 구성원 목록이 열립니다."),
                Fluent.Text("나란히 놓으면 활동 링과 상태 배지가 모두 보이고, 겹쳐 놓으면 활동 링만 보입니다. 원을 나눈 모양에서는 둘 다 표시하지 않습니다."),
                Fluent.Text("Tab·Enter·Escape로 목록을 조작해 보세요. 다른 페이지로 이동했다 돌아와도 설정은 유지됩니다."),
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
