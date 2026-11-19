using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class DropdownPage(ObserverServices services) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(services)).Samples;
    public string Title => "선택 목록";
    public string Description => "항목을 하나 또는 여러 개 선택해 보세요. 키보드로 선택하거나 목록을 닫은 상태에서 운영체제의 접근성 기능으로 선택할 수 있습니다.";
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
        private readonly UiFluentAvatar _memberAvatar = Fluent.Avatar("디모이", new(Color : UiAvatarColor.Brand, Presence : new()));
        private readonly UiFluentAvatar _imageAvatar = Fluent.Avatar("SVG 이미지", new(Presence : new(UiPresenceStatus.Busy)));
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
            var singleOptions = new UiDropdownOptions(Placeholder : "작업 위치를 선택하세요");
            var singleSample = GallerySample.Create(() => Fluent.Dropdown("작업 위치", country,
                [new("kr", "한국"), new("us", "United States"), new("jp", "日本"), new("old", "사용 중지된 위치", false)],
                singleOptions), layout : widget =>
            {
                var field = Fluent.Field(widget);
                field.Width = singleOptions.Width;
                field.Options = new() { Label = "작업 위치", Hint = "하나를 선택하면 목록이 닫힙니다." };
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
                new("image", "이미지"), new("svg", "SVG 아이콘") { Icon = _icon.MenuIcon },
                new("input", "한글 입력과 편집"), new("layout", "컨테이너와 레이아웃"),
                new("menu", "메뉴와 키보드"), new("a11y", "접근성")
            });
            var featureItems = featureDefinition.Value;
            var multipleOptions = new UiDropdownOptions(Placeholder : "여러 기능을 선택하세요", SelectedCountFormat : "{0}개 선택");
            var multipleSample = GallerySample.Create(() => Fluent.Dropdown("확인할 기능", features, featureItems, multipleOptions),
                layout : widget =>
                {
                    var field = Fluent.Field(widget);
                    field.Width = multipleOptions.Width;
                    field.Options = new() { Label = "확인할 기능", Hint = "여러 항목을 선택한 뒤 Escape로 목록을 닫으세요." };
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

            var editableOptions = new UiComboboxOptions(Field : new(Placeholder : "도시 이름을 입력하세요", ExpandLabel : "도시 목록 열기"),
                Freeform : true, EmptyText : "검색 결과가 없습니다");
            var editableSample = GallerySample.Create(() =>
            {
                var editor = Fluent.Combobox("도시 검색", new UiBinding<string>(() => _query, value => _query = value),
                    new UiBinding<string?>(() => _city, value => _city = value),
                    [new("seoul", "서울"), new("busan", "부산"), new("new-york", "New York"), new("london", "London")], editableOptions);
                editor.Open = new(() => _cityOpen, value => _cityOpen = value);
                editor.OpenChanged = _ => OpenChanges++;
                return editor;
            }, layout : widget =>
            {
                var field = Fluent.Field(widget);
                field.Width = editableOptions.Field!.Value.Width;
                field.Options = new()
                {
                    Label = "도시 검색",
                    Hint = "글자를 입력해 검색하거나 방향키와 Enter로 선택하세요. 목록에 없는 내용을 입력해도 지워지지 않습니다."
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

            var tagStatus = Fluent.Label("태그 확인 : 없음");
            var tagOptions = new UiTagOptions(DismissLabel : "해제");
            var tagsSample = GallerySample.Create(() => new UiFluentTagGroup("선택한 기능 태그", features, featureItems, tagOptions,
                activated : value => { ActivatedTag = value; tagStatus.Text = "태그 확인 : " + value; },
                emphasized : new(() => _emphasizedFeatures, value => _emphasizedFeatures = value)));
            _tags = tagsSample.Preview;
            var tagsFactory = tagsSample.Source;
            tagsSample.BindSource(() => (_features, _emphasizedFeatures, ActivatedTag, _tags.TagOptions), state => FeatureSetup(state._features)
                + $"string[] _emphasizedFeatures = {DropdownGallerySource.Array(state._emphasizedFeatures)};\n"
                + $"string? ActivatedTag = {GallerySample.String(state.ActivatedTag)};\n"
                + "var tagStatus = Fluent.Label(\"태그 확인 : \" + (ActivatedTag ?? \"없음\"));\n"
                + $"var tagOptions = {DropdownGallerySource.Options(state.TagOptions)};\n\n" + tagsFactory);
            _imageAvatar.Source = new(new SvgImageDecoder(_store, _services.ImageDecoder).Decode(Path.Combine(directory, "artwork.svg")));
            var memberTagOptions = new UiTagOptions(Shape : UiTagShape.Circular, DismissLabel : "담당자 해제");
            var memberSample = GallerySample.Create(() => new UiFluentTagGroup("담당자 태그", new(() => _members, value => _members = value),
                [new("jung", "디모이"), new("image", "SVG 이미지")], memberTagOptions,
                emphasized : new(() => _emphasizedMembers, value => _emphasizedMembers = value),
                media : item => UiTagMedia.Avatar(item.Value == "jung" ? _memberAvatar : _imageAvatar)));
            var memberTags = memberSample.Preview;
            var memberFactory = memberSample.Source;
            memberSample.BindSource(() => (_members, _emphasizedMembers, memberTags.TagOptions, _memberAvatar.Options.IsInactive), state =>
                DropdownGallerySource.AvatarSetup(state.IsInactive) + "\n\n"
                + $"string[] _members = {DropdownGallerySource.Array(state._members)};\n"
                + $"string[] _emphasizedMembers = {DropdownGallerySource.Array(state._emphasizedMembers)};\n"
                + $"var memberTagOptions = {DropdownGallerySource.Options(state.TagOptions)};\n\n" + memberFactory);
            var emphasisStatus = Fluent.Label("강조 선택 : 꺼짐 / 변경 허용");
            void UpdateEmphasisStatus() => emphasisStatus.Text = $"강조 선택 : {(_tagSelected ? "켜짐" : "꺼짐")} / {(_allowTagSelection ? "변경 허용" : "변경 거부")}";
            var emphasisSample = GallerySample.Create(() =>
            {
                var emphasis = Fluent.InteractionTag("강조 선택", UpdateEmphasisStatus);
                emphasis.Selected = new(() => _tagSelected, value => { if (_allowTagSelection) _tagSelected = value; });
                return emphasis;
            });
            var emphasisFactory = emphasisSample.Source;
            emphasisSample.BindSource(() => (_tagSelected, _allowTagSelection), state =>
                $"var _tagSelected = {GallerySample.Boolean(state._tagSelected)};\nvar _allowTagSelection = {GallerySample.Boolean(state._allowTagSelection)};\n"
                + """
                    var emphasisStatus = Fluent.Label("강조 선택 : " + (_tagSelected ? "켜짐" : "꺼짐") + " / " + (_allowTagSelection ? "변경 허용" : "변경 거부"));
                    void UpdateEmphasisStatus() => emphasisStatus.Text = $"강조 선택 : {(_tagSelected ? "켜짐" : "꺼짐")} / {(_allowTagSelection ? "변경 허용" : "변경 거부")}";
                    """ + "\n\n" + emphasisFactory);
            var basicTagOptions = new UiTagOptions(DismissLabel : "기본 해제");
            var basicSample = GallerySample.Create(() => new UiFluentTagGroup("기본 선택 태그", features, featureItems, basicTagOptions));
            _basicTags = basicSample.Preview;
            var basicFactory = basicSample.Source;
            basicSample.BindSource(() => (_features, _basicTags.TagOptions), state => FeatureSetup(state._features)
                + $"var basicTagOptions = {DropdownGallerySource.Options(state.TagOptions)};\n\n" + basicFactory);
            Samples = [singleSample, multipleSample, editableSample, tagsSample, basicSample, emphasisSample, memberSample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2("단일 선택과 다중 선택"),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("선택 표시 크기", () => Update(Single.Options with { Size = (UiDropdownSize)(((int)Single.Options.Size + 1) % 3) })),
                    Fluent.Button("선택 외관 변경", () => Update(Single.Options with { Appearance = (UiInputAppearance)(((int)Single.Options.Appearance + 1) % 4) })),
                    Fluent.Button("선택 방향 전환", () => Update(Single.Options with { RightToLeft = !Single.Options.RightToLeft })),
                    Fluent.Button("선택 폭 변경", () => Update(Single.Options with { Width = Single.Options.Width == 250 ? 140 : 250 })),
                    Fluent.Button("검증 상태 전환", () => Update(Single.Options with { Invalid = !Single.Options.Invalid })),
                    Fluent.Button("선택 지우기", () => { country.Value = null; features.Value = []; })),
                singleSample, multipleSample,
                Fluent.Subtitle2("검색과 직접 입력"),
                Fluent.Button("도시 목록 열림 전환", () => _cityOpen = !_cityOpen),
                Fluent.Body1(() => $"도시 값 : {_city ?? "목록 외 입력"} / 입력 : {_query}"),
                Fluent.Body1(() => $"도시 목록 : {(_cityOpen ? "열림" : "닫힘")} / 상태 전환 : {OpenChanges}"),
                editableSample,
                Fluent.Subtitle2("선택한 값을 태그로 표시"),
                Fluent.Button("태그 크기 전환", () =>
                {
                    _tags.TagOptions = _tags.TagOptions with { Size = (UiTagSize)(((int)_tags.TagOptions.Size + 1) % 3) };
                    _basicTags.TagOptions = _tags.TagOptions with { DismissLabel = "기본 해제" };
                }),
                Fluent.Text("확인형 태그는 본문을 눌러 강조하고 ×를 눌러 목록에서 제거할 수 있습니다. 다시 추가해도 강조 여부는 유지됩니다."),
                tagStatus,
                Fluent.Body1(() => "강조 값 : " + (_emphasizedFeatures.Length == 0 ? "없음" : string.Join(", ", _emphasizedFeatures))),
                tagsSample,
                Fluent.Text("기본 태그는 어느 곳을 눌러도 선택이 해제됩니다."),
                Fluent.Body1(() => $"선택 값 : {_country ?? "없음"} / 기능 : {_features.Length}개"),
                basicSample,
                Fluent.Button("태그 변경 허용 전환", () => { _allowTagSelection = !_allowTagSelection; UpdateEmphasisStatus(); }),
                emphasisStatus,
                emphasisSample,
                Fluent.Subtitle2("아바타가 있는 태그"),
                Fluent.Text("담당자 아바타에 한글 이름의 첫 글자나 SVG 이미지, 상태 배지를 표시합니다."),
                Dux.Wrap(new(Spacing : 8),
                    Fluent.Button("담당자 태그 크기", () => memberTags.TagOptions = memberTags.TagOptions with
                    {
                        Size = (UiTagSize)(((int)memberTags.TagOptions.Size + 1) % 3)
                    }),
                    Fluent.Button("담당자 태그 방향·모양", () => memberTags.TagOptions = memberTags.TagOptions with
                    {
                        RightToLeft = !memberTags.TagOptions.RightToLeft,
                        Shape = memberTags.TagOptions.Shape == UiTagShape.Circular ? UiTagShape.Rounded : UiTagShape.Circular
                    }),
                    Fluent.Button("담당자 비활성 표현", () =>
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
                    Fluent.Button("담당자 태그 복원", () => _members = ["jung", "image"])),
                Fluent.Text("Tab·위아래 방향키·Home·End·Enter·Space로 조작하세요. 비활성 항목은 건너뜁니다."),
                Fluent.Text("목록을 연 뒤 이름의 첫 글자부터 입력해 찾을 수 있습니다. 다른 페이지로 이동했다 돌아와도 선택은 유지됩니다."),
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
                    Placeholder = "도시 이름을 입력하세요",
                    ExpandLabel = "도시 목록 열기"
                }
            };
            Multiple.Options = options with
            {
                Placeholder = "여러 기능을 선택하세요",
                SelectedCountFormat = "{0}개 선택"
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
