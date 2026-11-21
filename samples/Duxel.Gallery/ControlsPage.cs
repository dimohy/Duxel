using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ControlsPage : UiWidget, IGallerySourcePage
{
    private readonly UiState<int> _clicks = new(0);
    private readonly UiState<bool> _toggle = new(false);
    private readonly UiState<bool?> _checked = new(null);
    private readonly UiState<float> _value = new(0.35f);
    private readonly UiState<string?> _choice = new("balanced");
    private readonly UiState<string> _destination = new(GalleryText.Get("No destination selected yet."));
    private readonly UiWidget _root;
    internal int ClickCount => _clicks.Value;
    internal float Value { get => _value.Value; set => _value.Value = value; }
    public IReadOnlyList<IGallerySample> Samples { get; }

    internal ControlsPage()
    {
        var counter = GallerySample.Create(() => Dux.Wrap(new(Spacing : 16, RunSpacing : 12, CrossAlignment : UiWrapCrossAlignment.Center),
            Fluent.Button(GalleryText.Get("Increase count"), () => _clicks.Value++),
            Fluent.Badge(GalleryText.Get("Ready")), Fluent.CounterBadge(() => new(_clicks.Value))));
        var counterFactory = counter.Source;
        counter.BindSource(() => _clicks.Value, value => $"var _clicks = new UiState<int>({value});\n\n" + counterFactory);

        var toggle = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.ToggleButton(GalleryText.Get("Keep selected"), _toggle.Binding),
            Fluent.Switch(GalleryText.Get("Share the toggle button state"), _toggle.Binding)));
        var toggleFactory = toggle.Source;
        toggle.BindSource(() => _toggle.Value, value => $"var _toggle = new UiState<bool>({GallerySample.Boolean(value)});\n\n" + toggleFactory);

        var checkbox = GallerySample.Create(() => Fluent.Checkbox(GalleryText.Get("Start in mixed state"), _checked.Binding));
        var checkboxFactory = checkbox.Source;
        checkbox.BindSource(() => _checked.Value, value => $"var _checked = new UiState<bool?>({(value is { } selected ? GallerySample.Boolean(selected) : "null")});\n\n" + checkboxFactory);

        var choicesDefinition = GallerySample.Define("choices", () => new UiRadioOption[]
        {
            new("efficient", GalleryText.Get("Prioritize speed")),
            new("balanced", GalleryText.Get("Balanced"), Description : GalleryText.Get("This is the default choice.")),
            new("quality", GalleryText.Get("Prioritize quality")),
            new("disabled", GalleryText.Get("Unavailable processing option"), false)
        });
        var choices = choicesDefinition.Value;
        var radio = GallerySample.Create(() => Fluent.RadioGroup(GalleryText.Get("Processing mode"), _choice.Binding, choices));
        var radioFactory = radio.Source;
        radio.BindSource(() => _choice.Value, value => $"var _choice = new UiState<string?>({GallerySample.String(value)});\n\n"
            + choicesDefinition.Source + "\n\n" + radioFactory);

        var progressDefinition = GallerySample.Define("progress", () => Fluent.ProgressBar(() => new(_value.Value, 1)));
        var progress = progressDefinition.Value;
        progress.Size = new(460, 4);
        var progressSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Slider(GalleryText.Get("Progress value"), _value.Binding, 0, 1, .01f), progress,
            Fluent.Body1(() => string.Format(GalleryText.Get("Progress: {0:P0}"), _value.Value))));
        var progressFactory = progressSample.Source;
        progressSample.BindSource(() => _value.Value, value => $"var _value = new UiState<float>({GallerySample.Number(value)});\n\n"
            + progressDefinition.Source + "\nprogress.Size = new(460, 4);\n\n" + progressFactory);

        var link = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Link(GalleryText.Get("Choose an in-app destination"), new Uri("/observer", UriKind.Relative), uri => _destination.Value = uri.ToString()),
            Fluent.Caption1(() => string.Format(GalleryText.Get("Selected destination: {0}"), _destination.Value))));
        var linkFactory = link.Source;
        link.BindSource(() => _destination.Value, value => $"var _destination = new UiState<string>({GallerySample.String(value)});\n\n" + linkFactory);

        var spinnerDefinition = GallerySample.Define("spinner", () => Fluent.Spinner(GalleryText.Get("Paused"), new(ReducedMotion : true) { Paused = true, Width = 180 }));
        var spinner = spinnerDefinition.Value;
        var spinnerSample = GallerySample.Create(() => Dux.Wrap(new(Spacing : 16, RunSpacing : 12),
            Fluent.Button(GalleryText.Get("Toggle Spinner playback"), () =>
            {
                spinner.Options = spinner.Options with { Paused = !spinner.Options.Paused };
                spinner.Label = spinner.Options.Paused ? GalleryText.Get("Paused") : GalleryText.Get("Playing");
            }), spinner));
        var spinnerFactory = spinnerSample.Source;
        spinnerSample.BindSource(() => (spinner.Options, spinner.Label), state => spinnerDefinition.Source
            + $"\nspinner.Options = spinner.Options with {{ Paused = {GallerySample.Boolean(state.Options.Paused)} }};"
            + $"\nspinner.Label = {GallerySample.String(state.Label)};\n\n" + spinnerFactory);

        var tabs = new TabExamples();
        Samples = [counter, toggle, checkbox, radio, progressSample, link, spinnerSample, .. tabs.Samples];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(() => GalleryText.Format("Clicks: {0} / Toggle: {1} / Checkbox: {2} / Choice: {3}", _clicks.Value,
                GalleryText.Get(_toggle.Value ? "On" : "Off"),
                GalleryText.Get(_checked.Value switch { true => "Checked", false => "Unchecked", null => "Mixed" }),
                choices.FirstOrDefault(choice => choice.Value == _choice.Value).Label ?? GalleryText.Get("None selected"))),
            Fluent.Subtitle2(GalleryText.Get("Counter and badges")), counter,
            Fluent.Subtitle2(GalleryText.Get("Shared selection state")), toggle,
            Fluent.Subtitle2(GalleryText.Get("Mixed checkbox state")), checkbox,
            Fluent.Subtitle2(GalleryText.Get("Choose a processing mode")), radio,
            Fluent.Divider(GalleryText.Get("Progress state")), progressSample,
            Fluent.Subtitle2(GalleryText.Get("Handle links within the window")), link,
            Fluent.Subtitle2(GalleryText.Get("Play and pause a Spinner")), spinnerSample, tabs);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
