using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ControlsPage : UiWidget, IGallerySourcePage
{
    private readonly UiState<int> _clicks = new(0);
    private readonly UiState<bool> _toggle = new(false);
    private readonly UiState<bool?> _checked = new(null);
    private readonly UiState<float> _value = new(0.35f);
    private readonly UiState<string?> _choice = new("balanced");
    private readonly UiState<string> _destination = new("아직 선택하지 않았습니다.");
    private readonly UiWidget _root;
    internal int ClickCount => _clicks.Value;
    internal float Value { get => _value.Value; set => _value.Value = value; }
    public IReadOnlyList<IGallerySample> Samples { get; }

    internal ControlsPage()
    {
        var counter = GallerySample.Create(() => Dux.Wrap(new(Spacing : 16, RunSpacing : 12, CrossAlignment : UiWrapCrossAlignment.Center),
            Fluent.Button("카운터 증가", () => _clicks.Value++),
            Fluent.Badge("Ready"), Fluent.CounterBadge(() => new(_clicks.Value))));
        var counterFactory = counter.Source;
        counter.BindSource(() => _clicks.Value, value => $"var _clicks = new UiState<int>({value});\n\n" + counterFactory);

        var toggle = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.ToggleButton("선택 유지", _toggle.Binding),
            Fluent.Switch("토글 버튼과 값 공유", _toggle.Binding)));
        var toggleFactory = toggle.Source;
        toggle.BindSource(() => _toggle.Value, value => $"var _toggle = new UiState<bool>({GallerySample.Boolean(value)});\n\n" + toggleFactory);

        var checkbox = GallerySample.Create(() => Fluent.Checkbox("혼합 상태에서 시작", _checked.Binding));
        var checkboxFactory = checkbox.Source;
        checkbox.BindSource(() => _checked.Value, value => $"var _checked = new UiState<bool?>({(value is { } selected ? GallerySample.Boolean(selected) : "null")});\n\n" + checkboxFactory);

        var choicesDefinition = GallerySample.Define("choices", () => new UiRadioOption[]
        {
            new("efficient", "속도 우선"),
            new("balanced", "균형", Description : "기본 선택입니다."),
            new("quality", "품질 우선"),
            new("disabled", "사용 불가", false)
        });
        var choices = choicesDefinition.Value;
        var radio = GallerySample.Create(() => Fluent.RadioGroup("처리 방식", _choice.Binding, choices));
        var radioFactory = radio.Source;
        radio.BindSource(() => _choice.Value, value => $"var _choice = new UiState<string?>({GallerySample.String(value)});\n\n"
            + choicesDefinition.Source + "\n\n" + radioFactory);

        var progressDefinition = GallerySample.Define("progress", () => Fluent.ProgressBar(() => new(_value.Value, 1)));
        var progress = progressDefinition.Value;
        progress.Size = new(460, 4);
        var progressSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Slider("진행률", _value.Binding, 0, 1, .01f), progress,
            Fluent.Body1(() => $"진행률 : {_value.Value:P0}")));
        var progressFactory = progressSample.Source;
        progressSample.BindSource(() => _value.Value, value => $"var _value = new UiState<float>({GallerySample.Number(value)});\n\n"
            + progressDefinition.Source + "\nprogress.Size = new(460, 4);\n\n" + progressFactory);

        var link = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Link("창 안에서 목적지 선택", new Uri("/observer", UriKind.Relative), uri => _destination.Value = uri.ToString()),
            Fluent.Caption1(() => $"선택한 목적지 : {_destination.Value}")));
        var linkFactory = link.Source;
        link.BindSource(() => _destination.Value, value => $"var _destination = new UiState<string>({GallerySample.String(value)});\n\n" + linkFactory);

        var spinnerDefinition = GallerySample.Define("spinner", () => Fluent.Spinner("일시 정지", new(ReducedMotion : true) { Paused = true, Width = 180 }));
        var spinner = spinnerDefinition.Value;
        var spinnerSample = GallerySample.Create(() => Dux.Wrap(new(Spacing : 16, RunSpacing : 12),
            Fluent.Button("Spinner 재생 전환", () =>
            {
                spinner.Options = spinner.Options with { Paused = !spinner.Options.Paused };
                spinner.Label = spinner.Options.Paused ? "일시 정지" : "재생 중";
            }), spinner));
        var spinnerFactory = spinnerSample.Source;
        spinnerSample.BindSource(() => (spinner.Options, spinner.Label), state => spinnerDefinition.Source
            + $"\nspinner.Options = spinner.Options with {{ Paused = {GallerySample.Boolean(state.Options.Paused)} }};"
            + $"\nspinner.Label = {GallerySample.String(state.Label)};\n\n" + spinnerFactory);

        var tabs = new TabExamples();
        Samples = [counter, toggle, checkbox, radio, progressSample, link, spinnerSample, .. tabs.Samples];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(() => $"클릭 : {_clicks.Value} / 토글 : {_toggle.Value} / 체크 : {_checked.Value?.ToString() ?? "혼합"} / 선택 : {_choice.Value}"),
            Fluent.Subtitle2("카운터와 배지"), counter,
            Fluent.Subtitle2("선택 상태 공유"), toggle,
            Fluent.Subtitle2("체크박스의 혼합 상태"), checkbox,
            Fluent.Subtitle2("처리 방식 선택"), radio,
            Fluent.Divider("진행 상태"), progressSample,
            Fluent.Subtitle2("창 안에서 링크 처리"), link,
            Fluent.Subtitle2("Spinner 재생과 일시 정지"), spinnerSample, tabs);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
