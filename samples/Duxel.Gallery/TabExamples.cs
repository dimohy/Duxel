using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TabExamples : UiWidget, IGallerySourcePage
{
    private readonly UiState<string?> _selection = new("home");
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }

    internal TabExamples()
    {
        var homeDefinition = GallerySample.Define("home", () => UiTabIcon.Create<HomeTabIcon, HomeTabFilledIcon>());
        var documentDefinition = GallerySample.Define("document", () => UiTabIcon.Create<DocumentTabIcon, DocumentTabFilledIcon>());
        var settingsDefinition = GallerySample.Define("settings", () => UiTabIcon.Create<SettingsTabIcon, SettingsTabFilledIcon>());
        var home = homeDefinition.Value;
        var document = documentDefinition.Value;
        var settings = settingsDefinition.Value;
        var iconsSource = homeDefinition.Source + "\n" + documentDefinition.Source + "\n" + settingsDefinition.Source;
        var declarations = GalleryPageSource.Declaration("TabExampleIcons");
        var examples = new List<UiWidget>
        {
            Fluent.Divider("탭 표현과 아이콘"),
            Fluent.Body1("각 탭 목록은 선택한 값을 공유합니다. 방향키로 포커스를 옮겼을 때와 탭을 선택했을 때를 비교하고, 아이콘이 윤곽선과 채움 모양으로 바뀌는지도 살펴보세요."),
            Fluent.Body1(() => $"공유 선택 : {_selection.Value ?? "선택 없음"}")
        };
        var samples = new List<IGallerySample>();
        foreach (var appearance in Enum.GetValues<UiTabListAppearance>())
        {
            var iconOnly = appearance == UiTabListAppearance.SubtleCircular;
            var size = appearance == UiTabListAppearance.Transparent ? UiTabListSize.Small
                : appearance == UiTabListAppearance.FilledCircular ? UiTabListSize.Large : UiTabListSize.Medium;
            var sample = GallerySample.Create(() => Fluent.TabList("표현 비교 : " + appearance, _selection.Binding, new UiTabOption[]
            {
                new("home", "홈", Icon : home, IconOnly : iconOnly),
                new("document", "문서", Icon : document, IconOnly : iconOnly),
                new("settings", "설정", false, settings, iconOnly)
            }, new(size, Appearance : appearance)));
            var factory = sample.Source;
            sample.BindSource(() => _selection.Value, value => $"var _selection = new UiState<string?>({GallerySample.String(value)});\n"
                + $"var appearance = UiTabListAppearance.{appearance};\nvar size = UiTabListSize.{size};\nbool iconOnly = {GallerySample.Boolean(iconOnly)};\n\n"
                + iconsSource + "\n\n" + factory + "\n\n" + declarations);
            samples.Add(sample);
            examples.Add(Fluent.Subtitle2($"{appearance} / {size}"));
            examples.Add(sample);
        }
        examples.Add(Fluent.Caption1("아이콘 전용 목록도 접근성 이름과 선택 값을 유지합니다. 설정 탭은 비활성 상태입니다."));
        Samples = samples;
        _root = new UiColumn(examples.ToArray(), new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min));
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
