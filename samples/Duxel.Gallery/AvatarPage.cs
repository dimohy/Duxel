using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class AvatarPage(ObserverServices services) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(services)).Samples;
    public string Title => "아바타";
    public string Description => "Display an avatar with a photo, initials, or icon. Compare round and square shapes in 14 sizes.";
    internal UiFluentAvatar? Preview => _content?.Preview;
    internal UiFluentPresenceBadge? Presence => _content?.Presence;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new(services)).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new(services)).Root.Render(ui);
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable

    {
        private readonly ObserverServices _services;
        private readonly SvgStore _store = new();
        private readonly UiSvgIcon _icon;
        private readonly UiImageSource _image;
        private string _name = "Ada Lovelace";
        private int _size;
        internal List<IGallerySample> Samples { get; } = [];
        private GallerySample<T> Record<T>(GallerySample<T> sample) where T : UiWidget
        {
            Samples.Add(sample);
            return sample;
        }
        internal UiFluentAvatar Preview { get; }
        internal UiFluentPresenceBadge Presence { get; }
        internal UiWidget Root
        {
            get;
        }
        private static readonly UiAvatarSize[] Sizes =
        [
            UiAvatarSize.Size16, UiAvatarSize.Size20, UiAvatarSize.Size24, UiAvatarSize.Size28,
            UiAvatarSize.Size32, UiAvatarSize.Size36, UiAvatarSize.Size40, UiAvatarSize.Size48,
            UiAvatarSize.Size56, UiAvatarSize.Size64, UiAvatarSize.Size72, UiAvatarSize.Size96,
            UiAvatarSize.Size120, UiAvatarSize.Size128
        ];
        internal Content(ObserverServices services)
        {
            _services = services;
            var previewSample = GallerySample.Create(() => Fluent.Avatar("Ada Lovelace",
                new(UiAvatarSize.Size64, Color: UiAvatarColor.Colorful)));
            Preview = previewSample.Preview;
            var presenceSample = GallerySample.Create(() => Fluent.PresenceBadge(new(Size: UiBadgeSize.ExtraLarge)));
            Presence = presenceSample.Preview;
            Preview.ActivityText = "활동 중";
            var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
            _image = new(new SvgImageDecoder(_store, _services.ImageDecoder).Decode(Path.Combine(directory, "artwork.svg")));
            _icon = new(_store.Load(Path.Combine(directory, "regular.svg")), _store.Load(Path.Combine(directory, "filled.svg")),
                UiSvgIconColorMode.MultiplyForeground);
            var name = Fluent.Input("아바타 이름", new UiBinding<string>(() => _name, value => { _name = value; Preview.Name = value; }), 100, 280);
            var gallery = Sizes.Select(size => GallerySample.Create(() => Fluent.Avatar("Ada Lovelace",
                new(size, Color: UiAvatarColor.Colorful)), setup: $"var size = UiAvatarSize.{size};")).ToArray();
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                name,
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("아바타 원형·사각", () => Preview.Options = Preview.Options with
                    {
                        Shape = Preview.Options.Shape == UiAvatarShape.Circular ? UiAvatarShape.Square : UiAvatarShape.Circular
                    }),
                    Fluent.Button("아바타 크기 변경", () =>
                    {
                        Preview.Options = Preview.Options with
                        {
                            Size = Sizes[_size]
                        };
                        _size = (_size + 1) % Sizes.Length;
                    }),
                    Fluent.Button("아바타 색 변경", () => Preview.Options = Preview.Options with
                    {
                        Color = (UiAvatarColor)(((int)Preview.Options.Color + 1) % 33)
                    })),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("아바타 이니셜", () => { Preview.Source = null; Preview.Initials = null; Preview.Icon = default; }),
                    Fluent.Button("아바타 이미지", () => Preview.Source = _image),
                    Fluent.Button("아바타 SVG 아이콘", () => { Preview.Source = null; Preview.Initials = ""; Preview.Icon = _icon.MenuIcon; }),
                    Fluent.Button("아바타 한글 이니셜", () => { Preview.Source = null; Preview.Name = _name = "김민수"; Preview.Initials = null; })),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("아바타 활동 링", () => Preview.Options = Preview.Options with
                    {
                        ShowActivityRing = !Preview.Options.ShowActivityRing
                    }),
                    Fluent.Button("아바타 활동 그림자", () => Preview.Options = Preview.Options with
                    {
                        ShowActivityShadow = !Preview.Options.ShowActivityShadow
                    }),
                    Fluent.Button("아바타 비활성 전환", () => Preview.Options = Preview.Options with
                    {
                        IsInactive = !Preview.Options.IsInactive
                    })),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("아바타 상태 배지", () =>
                    {
                        Preview.Options = Preview.Options with
                        {
                            Presence = Preview.Options.Presence is null ? new() : null
                        };
                        UpdatePresenceText();
                    }),
                    Fluent.Button("아바타 상태 변경", () =>
                    {
                        var presence = Preview.Options.Presence ?? new();
                        Preview.Options = Preview.Options with
                        {
                            Presence = presence with
                            {
                                Status = (UiPresenceStatus)(((int)presence.Status + 1) % 8)
                            }
                        };
                        UpdatePresenceText();
                    }),
                    Fluent.Button("아바타 외근 전환", () =>
                    {
                        var presence = Preview.Options.Presence ?? new();
                        Preview.Options = Preview.Options with
                        {
                            Presence = presence with
                            {
                                OutOfOffice = !presence.OutOfOffice
                            }
                        };
                        UpdatePresenceText();
                    }),
                    Fluent.Button("아바타 배지 크기", () =>
                    {
                        var presence = Preview.Options.Presence ?? new();
                        Preview.Options = Preview.Options with
                        {
                            Presence = presence with
                            {
                                Size = presence.Size is null ? UiBadgeSize.Tiny
                            : presence.Size == UiBadgeSize.ExtraLarge ? null : (UiBadgeSize)((int)presence.Size.Value + 1)
                            }
                        };
                        UpdatePresenceText();
                    }),
                    Fluent.Button("아바타 좌우 전환", () => Preview.Options = Preview.Options with
                    {
                        RightToLeft = !Preview.Options.RightToLeft
                    })),
                Record(previewSample),
                Fluent.Body1(() => $"크기 : {Preview.Options.PixelSize}px / 모양 : {Preview.Options.Shape} / 색 : {Preview.Options.Color}"),
                Fluent.Text("한글 이름은 첫 글자만 표시합니다. 예를 들어 디모이는 디로 표시됩니다. 기본 사람 아이콘은 Fluent 아이콘 폰트를 사용하고, SVG 아이콘은 한 번 해석한 결과를 재사용합니다."),
                Fluent.Text("비활성 상태에서는 아바타 전체 크기를 0.875배로 줄이고 불투명도 0.8을 한 번 적용합니다. 활동 링과 그림자는 숨기지만 상태 배지는 그대로 표시합니다. 움직이지 않는 아바타는 주기적으로 프레임을 요청하지 않습니다."),
                Fluent.Subtitle1("상태 배지"),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("상태 배지 변경", () => Presence.Options = Presence.Options with
                    {
                        Status = (UiPresenceStatus)(((int)Presence.Options.Status + 1) % 8)
                    }),
                    Fluent.Button("상태 배지 외근 전환", () => Presence.Options = Presence.Options with
                    {
                        OutOfOffice = !Presence.Options.OutOfOffice
                    }),
                    Fluent.Button("상태 배지 크기 변경", () => Presence.Options = Presence.Options with
                    {
                        Size = (UiBadgeSize)(((int)Presence.Options.Size + 1) % 6)
                    })),
                Record(presenceSample),
                Fluent.Body1(() => "상태 : " + StatusLabel(Presence.Options.Status)
                    + (Presence.Options.OutOfOffice ? " / 외근 표시" : "")),
                Fluent.Subtitle1("지원 크기"),
                Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                    gallery.Select(sample => (UiWidget)Record(sample)).ToArray())));
        }
        private void UpdatePresenceText() => Preview.PresenceText = Preview.Options.Presence is { } presence
            ? StatusLabel(presence.Status) + (presence.OutOfOffice ? " 외근" : "") : null;

        private static string StatusLabel(UiPresenceStatus status) => status switch
        {
            UiPresenceStatus.Available => "사용 가능",
            UiPresenceStatus.Away => "자리 비움",
            UiPresenceStatus.Busy => "다른 용무 중",
            UiPresenceStatus.DoNotDisturb => "방해 금지",
            UiPresenceStatus.Offline => "오프라인",
            UiPresenceStatus.OutOfOffice => "외근",
            UiPresenceStatus.Unknown => "상태 미확인",
            UiPresenceStatus.Blocked => "차단됨",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        public void Dispose()
        {
            Root.Dispose();
            _icon.Dispose();
            _store.Dispose();
        }
    }
}
