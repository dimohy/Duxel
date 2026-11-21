using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class DropdownPage(ObserverServices services) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(services)).Samples;
    public string Title => "선택 목록";
    public string Description => "Try selecting one or more items by keyboard or through the operating system's accessibility features while the list is closed.";
    internal UiFluentDropdown? Single => _content?.Single;
    internal UiFluentDropdown? Multiple => _content?.Multiple;
    internal UiFluentCombobox? Editable => _content?.Editable;
    internal int OpenChanges => _content?.OpenChanges ?? 0;
    internal string? ActivatedTag => _content?.ActivatedTag;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new(services)).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new(services)).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        private string? _country;
        private string[] _features = [];
        private string[] _emphasizedFeatures = [];
        private string _query = string.Empty;
        private string? _city;
        private bool _cityOpen;
        private bool _tagSelected;
        private bool _allowTagSelection = true;
        internal int OpenChanges
        {
            get; private set;
        }
        private readonly ObserverServices _services;
        private readonly SvgStore _store = new();
        private readonly UiFluentAvatar _memberAvatar = Fluent.Avatar(GalleryText.Get("Dimohy"), new(Color : UiAvatarColor.Brand, Presence : new()));
        private readonly UiFluentAvatar _imageAvatar = Fluent.Avatar(GalleryText.Get("SVG image"), new(Presence : new(UiPresenceStatus.Busy)));
        private string[] _members = ["jung", "image"];
        private string[] _emphasizedMembers = [];
        private readonly UiSvgIcon _icon;
        private readonly UiFluentField _singleField;
        private readonly UiFluentField _multipleField;
        private readonly UiFluentField _editableField;
        private readonly UiFluentTagGroup _tags;
        private readonly UiFluentTagGroup _basicTags;
        internal string? ActivatedTag
        {
            get; private set;
        }
        internal UiFluentDropdown Single
        {
            get;
        }
        internal UiFluentDropdown Multiple
        {
            get;
        }
        internal UiFluentCombobox Editable
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
            _icon = new(_store.Load(Path.Combine(directory, "regular.svg")), _store.Load(Path.Combine(directory, "filled.svg")), UiSvgIconColorMode.MultiplyForeground);
            var country = new UiBinding<string?>(() => _country, value => _country = value);
            var features = new UiBinding<string[]>(() => _features, value => _features = value);
            var singleOptions = new UiDropdownOptions(Placeholder : GalleryText.Get("Choose a work location"), ExpandLabel : GalleryText.Get("Open location choices"));
            var singleSample = GallerySample.Create(() => Fluent.Dropdown(GalleryText.Get("Work location"), country,
                [new("kr", GalleryText.Get("South Korea")), new("us", GalleryText.Get("United States")), new("jp", GalleryText.Get("Japan")), new("old", GalleryText.Get("Disabled location"), false)],
                singleOptions), layout : widget =>
            {
                var field = Fluent.Field(widget);
                field.Width = singleOptions.Width;
                field.Options = new() { Label = GalleryText.Get("Work location"), Hint = GalleryText.Get("Select one item to close the list.") };
                return field;
            });
            Single = singleSample.Preview;
            _singleField = (UiFluentField)singleSample.PreviewLayout;
            var singleFactory = singleSample.Source;
            singleSample.BindSource(() => (_country, Single.Options), state =>
                $"string? _country = {GallerySample.String(state._country)};\n"
                + "var country = new UiBinding<string?>(() => _country, value => _country = value);\n"
                + $"var singleOptions = {DropdownGallerySource.Options(state.Options)};\n\n" + singleFactory);

            var featureDefinition = GallerySample.Define("featureItems", () => new UiSelectOption[]
            {
                new("image", GalleryText.Get("Image")), new("svg", GalleryText.Get("SVG icon")) { Icon = _icon.MenuIcon },
                new("input", GalleryText.Get("Text input and editing")), new("layout", GalleryText.Get("Containers and layouts")),
                new("menu", GalleryText.Get("Menus and keyboard")), new("a11y", GalleryText.Get("Accessibility"))
            });
            var featureItems = featureDefinition.Value;
            var multipleOptions = new UiDropdownOptions(Placeholder : GalleryText.Get("Choose several features"), SelectedCountFormat : GalleryText.Get("{0} selected"),
                ExpandLabel : GalleryText.Get("Open feature choices"));
            var multipleSample = GallerySample.Create(() => Fluent.Dropdown(GalleryText.Get("Features to try"), features, featureItems, multipleOptions),
                layout : widget =>
                {
                    var field = Fluent.Field(widget);
                    field.Width = multipleOptions.Width;
                    field.Options = new() { Label = GalleryText.Get("Features to try"), Hint = GalleryText.Get("Select multiple items, then press Escape to close the list.") };
                    return field;
                });
            Multiple = multipleSample.Preview;
            _multipleField = (UiFluentField)multipleSample.PreviewLayout;
            var multipleFactory = multipleSample.Source;
            string FeatureSetup(string[] selected) => DropdownGallerySource.IconSetup + "\n\n" + featureDefinition.Source + "\n\n"
                + $"string[] _features = {DropdownGallerySource.Array(selected)};\n"
                + "var features = new UiBinding<string[]>(() => _features, value => _features = value);\n";
            multipleSample.BindSource(() => (_features, Multiple.Options), state => FeatureSetup(state._features)
                + $"var multipleOptions = {DropdownGallerySource.Options(state.Options)};\n\n" + multipleFactory);

            var editableOptions = new UiComboboxOptions(Field : new(Placeholder : GalleryText.Get("Enter a city name"), ExpandLabel : GalleryText.Get("Open city list")),
                Freeform : true, EmptyText : GalleryText.Get("No search results"));
            var editableSample = GallerySample.Create(() =>
            {
                var editor = Fluent.Combobox(GalleryText.Get("Search for a city"), new UiBinding<string>(() => _query, value => _query = value),
                    new UiBinding<string?>(() => _city, value => _city = value),
                    [new("seoul", GalleryText.Get("Seoul")), new("busan", GalleryText.Get("Busan")), new("new-york", GalleryText.Get("New York")), new("london", GalleryText.Get("London"))], editableOptions);
                editor.Open = new(() => _cityOpen, value => _cityOpen = value);
                editor.OpenChanged = _ => OpenChanges++;
                return editor;
            }, layout : widget =>
            {
                var field = Fluent.Field(widget);
                field.Width = editableOptions.Field!.Value.Width;
                field.Options = new()
                {
                    Label = GalleryText.Get("Search for a city"),
                    Hint = GalleryText.Get("Type to search, or choose with the arrow keys and Enter. Text outside the list is kept.")
                };
                return field;
            });
            Editable = editableSample.Preview;
            _editableField = (UiFluentField)editableSample.PreviewLayout;
            var editableFactory = editableSample.Source;
            editableSample.BindSource(() => (_query, _city, _cityOpen, OpenChanges, Editable.Options), state =>
                $"var _query = {GallerySample.String(state._query)};\nstring? _city = {GallerySample.String(state._city)};\n"
                + $"var _cityOpen = {GallerySample.Boolean(state._cityOpen)};\nvar OpenChanges = {state.OpenChanges};\n"
                + $"var editableOptions = {DropdownGallerySource.Options(state.Options)};\n\n" + editableFactory);

            var tagStatus = Fluent.Label(GalleryText.Format("Tag activation: {0}", GalleryText.Get("None")));
            var tagOptions = new UiTagOptions(DismissLabel : GalleryText.Get("Remove tag"));
            var tagsSample = GallerySample.Create(() => new UiFluentTagGroup(GalleryText.Get("Selected feature tags"), features, featureItems, tagOptions,
                activated : value => { ActivatedTag = value; tagStatus.Text = string.Format(GalleryText.Get("Tag activation: {0}"), value); },
                emphasized : new(() => _emphasizedFeatures, value => _emphasizedFeatures = value)));
            _tags = tagsSample.Preview;
            var tagsFactory = tagsSample.Source;
            tagsSample.BindSource(() => (_features, _emphasizedFeatures, ActivatedTag, _tags.TagOptions), state => FeatureSetup(state._features)
                + $"string[] _emphasizedFeatures = {DropdownGallerySource.Array(state._emphasizedFeatures)};\n"
                + $"string? ActivatedTag = {GallerySample.String(state.ActivatedTag)};\n"
                + $"var tagStatus = Fluent.Label(string.Format({GallerySample.String(GalleryText.Get("Tag activation: {0}"))}, "
                + $"ActivatedTag ?? {GallerySample.String(GalleryText.Get("None"))}));\n"
                + $"var tagOptions = {DropdownGallerySource.Options(state.TagOptions)};\n\n" + tagsFactory);
            _imageAvatar.Source = new(new SvgImageDecoder(_store, _services.ImageDecoder).Decode(Path.Combine(directory, "artwork.svg")));
            var memberTagOptions = new UiTagOptions(Shape : UiTagShape.Circular, DismissLabel : GalleryText.Get("Remove assignee"));
            var memberSample = GallerySample.Create(() => new UiFluentTagGroup(GalleryText.Get("Assignee tags"), new(() => _members, value => _members = value),
                [new("jung", GalleryText.Get("Dimohy")), new("image", GalleryText.Get("SVG image"))], memberTagOptions,
                emphasized : new(() => _emphasizedMembers, value => _emphasizedMembers = value),
                media : item => UiTagMedia.Avatar(item.Value == "jung" ? _memberAvatar : _imageAvatar)));
            var memberTags = memberSample.Preview;
            var memberFactory = memberSample.Source;
            memberSample.BindSource(() => (_members, _emphasizedMembers, memberTags.TagOptions, _memberAvatar.Options.IsInactive), state =>
                DropdownGallerySource.AvatarSetup(state.IsInactive) + "\n\n"
                + $"string[] _members = {DropdownGallerySource.Array(state._members)};\n"
                + $"string[] _emphasizedMembers = {DropdownGallerySource.Array(state._emphasizedMembers)};\n"
                + $"var memberTagOptions = {DropdownGallerySource.Options(state.TagOptions)};\n\n" + memberFactory);
            var emphasisText = GallerySample.Define<Func<string>>("readEmphasis", () => () => string.Format(
                GalleryText.Get("Emphasis: {0} / {1}"), _tagSelected ? GalleryText.Get("On") : GalleryText.Get("Off"),
                _allowTagSelection ? GalleryText.Get("Changes allowed") : GalleryText.Get("Changes rejected")));
            var readEmphasis = emphasisText.Value;
            var emphasisStatus = Fluent.Label(readEmphasis());
            void UpdateEmphasisStatus() => emphasisStatus.Text = readEmphasis();
            var emphasisSample = GallerySample.Create(() =>
            {
                var emphasis = Fluent.InteractionTag(GalleryText.Get("Emphasis selection"), UpdateEmphasisStatus);
                emphasis.Selected = new(() => _tagSelected, value => { if (_allowTagSelection) _tagSelected = value; });
                return emphasis;
            });
            var emphasisFactory = emphasisSample.Source;
            emphasisSample.BindSource(() => (_tagSelected, _allowTagSelection), state =>
                $"var _tagSelected = {GallerySample.Boolean(state._tagSelected)};\nvar _allowTagSelection = {GallerySample.Boolean(state._allowTagSelection)};\n"
                + emphasisText.Source + "\nvar emphasisStatus = Fluent.Label(readEmphasis());\n"
                + "void UpdateEmphasisStatus() => emphasisStatus.Text = readEmphasis();\n\n" + emphasisFactory);
            var basicTagOptions = new UiTagOptions(DismissLabel : GalleryText.Get("Remove basic tag"));
            var basicSample = GallerySample.Create(() => new UiFluentTagGroup(GalleryText.Get("Basic selection tags"), features, featureItems, basicTagOptions));
            _basicTags = basicSample.Preview;
            var basicFactory = basicSample.Source;
            basicSample.BindSource(() => (_features, _basicTags.TagOptions), state => FeatureSetup(state._features)
                + $"var basicTagOptions = {DropdownGallerySource.Options(state.TagOptions)};\n\n" + basicFactory);
            Samples = [singleSample, multipleSample, editableSample, tagsSample, basicSample, emphasisSample, memberSample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2(GalleryText.Get("Single and multiple selection")),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Change selection display size"), () => Update(Single.Options with { Size = (UiDropdownSize)(((int)Single.Options.Size + 1) % 3) })),
                    Fluent.Button(GalleryText.Get("Change selection appearance"), () => Update(Single.Options with { Appearance = (UiInputAppearance)(((int)Single.Options.Appearance + 1) % 4) })),
                    Fluent.Button(GalleryText.Get("Reverse selection direction"), () => Update(Single.Options with { RightToLeft = !Single.Options.RightToLeft })),
                    Fluent.Button(GalleryText.Get("Change selection width"), () => Update(Single.Options with { Width = Single.Options.Width == 250 ? 140 : 250 })),
                    Fluent.Button(GalleryText.Get("Toggle validation state"), () => Update(Single.Options with { Invalid = !Single.Options.Invalid })),
                    Fluent.Button(GalleryText.Get("Clear selections"), () => { country.Value = null; features.Value = []; })),
                singleSample, multipleSample,
                Fluent.Subtitle2(GalleryText.Get("Search and type a value")),
                Fluent.Button(GalleryText.Get("Toggle city list"), () => _cityOpen = !_cityOpen),
                Fluent.Body1(() => GalleryText.Format("City: {0} / Input: {1}",
                    GalleryText.Get(_city switch { "seoul" => "Seoul", "busan" => "Busan", "new-york" => "New York", "london" => "London", _ => "Text outside the list" }), _query)),
                Fluent.Body1(() => GalleryText.Format("City list: {0} / State changes: {1}", GalleryText.Get(_cityOpen ? "List open" : "List closed"), OpenChanges)),
                editableSample,
                Fluent.Subtitle2(GalleryText.Get("Show selections as tags")),
                Fluent.Button(GalleryText.Get("Change tag size"), () =>
                {
                    _tags.TagOptions = _tags.TagOptions with { Size = (UiTagSize)(((int)_tags.TagOptions.Size + 1) % 3) };
                    _basicTags.TagOptions = _tags.TagOptions with { DismissLabel = GalleryText.Get("Remove basic tag") };
                }),
                Fluent.Text(GalleryText.Get("Select an interactive tag to emphasize it, or select × to remove it. Its emphasis is kept when you add it again.")),
                tagStatus,
                Fluent.Body1(() => GalleryText.Format("Emphasized values: {0}", _emphasizedFeatures.Length == 0 ? GalleryText.Get("None") : string.Join(", ", _emphasizedFeatures))),
                tagsSample,
                Fluent.Text(GalleryText.Get("Select anywhere on a basic tag to remove it.")),
                Fluent.Body1(() => GalleryText.Format("Location: {0} / Features: {1}",
                    GalleryText.Get(_country switch { "kr" => "South Korea", "us" => "United States", "jp" => "Japan", _ => "None" }), _features.Length)),
                basicSample,
                Fluent.Button(GalleryText.Get("Toggle whether tag changes are allowed"), () => { _allowTagSelection = !_allowTagSelection; UpdateEmphasisStatus(); }),
                emphasisStatus,
                emphasisSample,
                Fluent.Subtitle2(GalleryText.Get("Tags with avatars")),
                Fluent.Text(GalleryText.Get("Assignee avatars show initials or an SVG image, plus a status badge.")),
                Dux.Wrap(new(Spacing : 8),
                    Fluent.Button(GalleryText.Get("Change assignee tag size"), () => memberTags.TagOptions = memberTags.TagOptions with
                    {
                        Size = (UiTagSize)(((int)memberTags.TagOptions.Size + 1) % 3)
                    }),
                    Fluent.Button(GalleryText.Get("Change assignee tag direction and shape"), () => memberTags.TagOptions = memberTags.TagOptions with
                    {
                        RightToLeft = !memberTags.TagOptions.RightToLeft,
                        Shape = memberTags.TagOptions.Shape == UiTagShape.Circular ? UiTagShape.Rounded : UiTagShape.Circular
                    }),
                    Fluent.Button(GalleryText.Get("Toggle inactive assignee style"), () =>
                    {
                        _memberAvatar.Options = _memberAvatar.Options with
                        {
                            IsInactive = !_memberAvatar.Options.IsInactive
                        };
                        _imageAvatar.Options = _imageAvatar.Options with
                        {
                            IsInactive = _memberAvatar.Options.IsInactive
                        };
                    }),
                    Fluent.Button(GalleryText.Get("Restore assignee tags"), () => _members = ["jung", "image"])),
                Fluent.Text(GalleryText.Get("Use Tab, Up, Down, Home, End, Enter, and Space. Navigation skips disabled items.")),
                Fluent.Text(GalleryText.Get("Open the list and type the first letters of a name to find it. Selection is kept when you return to this page.")),
                memberSample));
        }
        private void Update(UiDropdownOptions options)
        {
            Single.Options = options;
            _singleField.Width = _multipleField.Width = _editableField.Width = options.Width;
            Editable.Options = Editable.Options with
            {
                Field = options with
                {
                    Placeholder = GalleryText.Get("Enter a city name"),
                    ExpandLabel = GalleryText.Get("Open city list")
                }
            };
            Multiple.Options = options with
            {
                Placeholder = GalleryText.Get("Choose several features"),
                SelectedCountFormat = GalleryText.Get("{0} selected"),
                ExpandLabel = GalleryText.Get("Open feature choices")
            };
            _tags.TagOptions = _tags.TagOptions with
            {
                RightToLeft = options.RightToLeft
            };
            _basicTags.TagOptions = _basicTags.TagOptions with
            {
                RightToLeft = options.RightToLeft
            };
        }
        public void Dispose()
        {
            Root.Dispose();
            _memberAvatar.Dispose();
            _imageAvatar.Dispose();
            _icon.Dispose();
            _store.Dispose();
        }
    }
}
