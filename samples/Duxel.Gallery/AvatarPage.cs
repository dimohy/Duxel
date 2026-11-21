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
        private const string KoreanInitialsExample = "김민수";
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
            var previewSample = GallerySample.Create(() =>
            {
                var avatar = Fluent.Avatar("Ada Lovelace", new(UiAvatarSize.Size64, Color : UiAvatarColor.Colorful));
                avatar.ActivityText = GalleryText.Get("Active");
                return avatar;
            });
            Preview = previewSample.Preview;
            var presenceSample = GallerySample.Create(() =>
            {
                var badge = Fluent.PresenceBadge(new(Size : UiBadgeSize.ExtraLarge));
                badge.AccessibleName = GalleryText.Get("Available");
                return badge;
            });
            Presence = presenceSample.Preview;
            presenceSample.BindSource(() => Presence.Options, options =>
                $"var badge = Fluent.PresenceBadge(new(UiPresenceStatus.{options.Status}, UiBadgeSize.{options.Size}, "
                + $"OutOfOffice : {GallerySample.Boolean(options.OutOfOffice)}));\n"
                + $"badge.AccessibleName = {GallerySample.String(PresenceLabel(options.Status, options.OutOfOffice))};");
            var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
            _image = new(new SvgImageDecoder(_store, _services.ImageDecoder).Decode(Path.Combine(directory, "artwork.svg")));
            _icon = new(_store.Load(Path.Combine(directory, "regular.svg")), _store.Load(Path.Combine(directory, "filled.svg")),
                UiSvgIconColorMode.MultiplyForeground);
            var name = Fluent.Input(GalleryText.Get("Avatar name"), new UiBinding<string>(() => _name, value => { _name = value; Preview.Name = value; }), 100, 280);
            var gallery = Sizes.Select(size => GallerySample.Create(() => Fluent.Avatar("Ada Lovelace",
                new(size, Color: UiAvatarColor.Colorful)), setup: $"var size = UiAvatarSize.{size};")).ToArray();
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                name,
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Toggle round or square avatar"), () => Preview.Options = Preview.Options with
                    {
                        Shape = Preview.Options.Shape == UiAvatarShape.Circular ? UiAvatarShape.Square : UiAvatarShape.Circular
                    }),
                    Fluent.Button(GalleryText.Get("Change avatar size"), () =>
                    {
                        Preview.Options = Preview.Options with
                        {
                            Size = Sizes[_size]
                        };
                        _size = (_size + 1) % Sizes.Length;
                    }),
                    Fluent.Button(GalleryText.Get("Change avatar color"), () => Preview.Options = Preview.Options with
                    {
                        Color = (UiAvatarColor)(((int)Preview.Options.Color + 1) % 33)
                    })),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Show avatar initials"), () => { Preview.Source = null; Preview.Initials = null; Preview.Icon = default; }),
                    Fluent.Button(GalleryText.Get("Show avatar image"), () => Preview.Source = _image),
                    Fluent.Button(GalleryText.Get("Show avatar SVG icon"), () => { Preview.Source = null; Preview.Initials = ""; Preview.Icon = _icon.MenuIcon; }),
                    Fluent.Button(GalleryText.Get("Try Korean initials"), () => { Preview.Source = null; Preview.Name = _name = KoreanInitialsExample; Preview.Initials = null; })),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Toggle activity ring"), () => Preview.Options = Preview.Options with
                    {
                        ShowActivityRing = !Preview.Options.ShowActivityRing
                    }),
                    Fluent.Button(GalleryText.Get("Toggle activity shadow"), () => Preview.Options = Preview.Options with
                    {
                        ShowActivityShadow = !Preview.Options.ShowActivityShadow
                    }),
                    Fluent.Button(GalleryText.Get("Toggle inactive avatar"), () => Preview.Options = Preview.Options with
                    {
                        IsInactive = !Preview.Options.IsInactive
                    })),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Toggle avatar presence badge"), () =>
                    {
                        Preview.Options = Preview.Options with
                        {
                            Presence = Preview.Options.Presence is null ? new() : null
                        };
                        UpdatePresenceText();
                    }),
                    Fluent.Button(GalleryText.Get("Change avatar status"), () =>
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
                    Fluent.Button(GalleryText.Get("Toggle avatar out-of-office status"), () =>
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
                    Fluent.Button(GalleryText.Get("Change avatar badge size"), () =>
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
                    Fluent.Button(GalleryText.Get("Reverse avatar direction"), () => Preview.Options = Preview.Options with
                    {
                        RightToLeft = !Preview.Options.RightToLeft
                    })),
                Record(previewSample),
                Fluent.Body1(() => GalleryText.Format("Size: {0}px / Shape: {1} / Color: {2}",
                    Preview.Options.PixelSize, Preview.Options.Shape, Preview.Options.Color)),
                Fluent.Text(GalleryText.Get("Korean names show the first Hangul character of the name. The default person icon uses the Fluent icon font, and the SVG icon reuses its parsed asset.")),
                Fluent.Text(GalleryText.Get("An inactive avatar is scaled to 0.875 of its size and drawn at 0.8 opacity. It hides the activity ring and shadow while keeping the presence badge visible. A static avatar does not request periodic frames.")),
                Fluent.Subtitle1(GalleryText.Get("Presence badge")),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change presence badge status"), () => SetBadgeOptions(Presence.Options with
                    {
                        Status = (UiPresenceStatus)(((int)Presence.Options.Status + 1) % 8)
                    })),
                    Fluent.Button(GalleryText.Get("Toggle out-of-office badge"), () => SetBadgeOptions(Presence.Options with
                    {
                        OutOfOffice = !Presence.Options.OutOfOffice
                    })),
                    Fluent.Button(GalleryText.Get("Change presence badge size"), () => Presence.Options = Presence.Options with
                    {
                        Size = (UiBadgeSize)(((int)Presence.Options.Size + 1) % 6)
                    })),
                Record(presenceSample),
                Fluent.Body1(() => GalleryText.Format(Presence.Options.OutOfOffice
                    ? "Status: {0} / Out-of-office marker" : "Status: {0}", StatusLabel(Presence.Options.Status))),
                Fluent.Subtitle1(GalleryText.Get("Supported sizes")),
                Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                    gallery.Select(sample => (UiWidget)Record(sample)).ToArray())));
        }
        private void UpdatePresenceText() => Preview.PresenceText = Preview.Options.Presence is { } presence
            ? PresenceLabel(presence.Status, presence.OutOfOffice) : null;

        private void SetBadgeOptions(UiPresenceBadgeOptions options)
        {
            Presence.Options = options;
            Presence.AccessibleName = PresenceLabel(options.Status, options.OutOfOffice);
        }

        private static string PresenceLabel(UiPresenceStatus status, bool outOfOffice) => outOfOffice && status != UiPresenceStatus.OutOfOffice
            ? GalleryText.Format("{0} (out of office)", StatusLabel(status))
            : StatusLabel(status);

        private static string StatusLabel(UiPresenceStatus status) => status switch
        {
            UiPresenceStatus.Available => GalleryText.Get("Available"),
            UiPresenceStatus.Away => GalleryText.Get("Away"),
            UiPresenceStatus.Busy => GalleryText.Get("Busy"),
            UiPresenceStatus.DoNotDisturb => GalleryText.Get("Do not disturb"),
            UiPresenceStatus.Offline => GalleryText.Get("Offline"),
            UiPresenceStatus.OutOfOffice => GalleryText.Get("Out of office"),
            UiPresenceStatus.Unknown => GalleryText.Get("Unknown status"),
            UiPresenceStatus.Blocked => GalleryText.Get("Blocked"),
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
