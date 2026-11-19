using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TabOverflowExamples : UiWidget, IGallerySourcePage
{
    private readonly UiState<string?> _value = new("home");
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal TabOverflowExamples()
    {
        var counts = Enumerable.Range(0, 5).Select(_ => new UiState<int>(0)).ToArray();
        var tabOptions = new UiTabListOptions { Overflow = new(Label : "개 탭 더 보기") };
        var tabsDefinition = GallerySample.Define("tabs", () => Fluent.TabList("넘침 비교", _value.Binding,
            new UiTabOption[] { new("home", "홈"), new("document", "문서"), new("analysis", "분석"), new("settings", "설정", false), new("help", "도움말") }, tabOptions));
        var tabs = tabsDefinition.Value;
        var widthValue = 160f;
        var heightValue = 40f;
        var boxDefinition = GallerySample.Define("box", () => Dux.SizedBox(tabs, width : widthValue, height : heightValue));
        var box = boxDefinition.Value;
        var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            box, new TabOverflowBodies(tabs, counts), Fluent.Caption1(() => $"선택 값 : {_value.Value}")));
        var factory = sample.Source;
        var declaration = GalleryPageSource.Declaration(nameof(TabOverflowBodies));
        sample.BindSource(() => (_value.Value, tabs.Options, box.Options.Width, box.Options.Height,
            Counts : (counts[0].Value, counts[1].Value, counts[2].Value, counts[3].Value, counts[4].Value)), state =>
        {
            var options = state.Options;
            var overflow = options.Overflow is { } settings
                ? $"new UiTabListOverflowOptions({settings.MinimumVisible}, {GallerySample.String(settings.Label)})" : "null";
            return $"var _value = new UiState<string?>({GallerySample.String(state.Value)});\n"
                + $"UiState<int>[] counts = [new({state.Counts.Item1}), new({state.Counts.Item2}), new({state.Counts.Item3}), new({state.Counts.Item4}), new({state.Counts.Item5})];\n"
                + $"var tabOptions = new UiTabListOptions(Size : UiTabListSize.{options.Size}, Vertical : {GallerySample.Boolean(options.Vertical)},\n"
                + $"    SelectOnFocus : {GallerySample.Boolean(options.SelectOnFocus)}, RightToLeft : {GallerySample.Boolean(options.RightToLeft)},\n"
                + $"    ReserveSelectedSpace : {GallerySample.Boolean(options.ReserveSelectedSpace)}, Appearance : UiTabListAppearance.{options.Appearance}, Overflow : {overflow});\n"
                + $"float? widthValue = {GallerySample.Number(state.Width)};\nfloat? heightValue = {GallerySample.Number(state.Height)};\n\n"
                + tabsDefinition.Source + "\n\n" + boxDefinition.Source + "\n\n" + factory + "\n\n" + declaration;
        });
        Samples = [sample];
        var width = Fluent.Button("탭 폭 : 160");
        width.Activated = () =>
        {
            var next = box.Options.Width == 160 ? 520 : 160;
            box.Options = box.Options with { Width = next };
            width.Label = $"탭 폭 : {next}";
        };
        var direction = Fluent.Button("탭 방향 : LTR");
        direction.Activated = () =>
        {
            tabs.Options = tabs.Options with { RightToLeft = !tabs.Options.RightToLeft };
            direction.Label = tabs.Options.RightToLeft ? "탭 방향 : RTL" : "탭 방향 : LTR";
        };
        var orientation = Fluent.Button("탭 배치 : 가로");
        orientation.Activated = () =>
        {
            var vertical = !tabs.Options.Vertical;
            tabs.Options = tabs.Options with { Vertical = vertical };
            box.Options = box.Options with { Height = vertical ? 110 : 40 };
            orientation.Label = vertical ? "탭 배치 : 세로" : "탭 배치 : 가로";
        };
        _root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Divider("탭 넘침과 본문"),
            Fluent.Body1("더 보기에서 숨긴 탭을 선택하면 헤더와 본문이 함께 바뀝니다. 본문 카운터는 유지됩니다."),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8), width, direction, orientation), sample);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}

