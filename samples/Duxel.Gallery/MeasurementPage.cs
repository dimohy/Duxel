using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MeasurementPage : UiWidget, IGallerySourcePage
{
    private readonly UiState<bool?> _checked = new(null);
    private readonly UiState<float> _value = new(.25f);
    private readonly UiState<string?> _choice = new("a");
    private readonly UiState<string> _formValue = new(GalleryText.Get("Edit this note"));
    private readonly UiState<bool> _formChecked = new(false);
    private readonly UiState<string?> _tabValue = new("a");
    private readonly UiState<string> _memo = new(GalleryText.Get("First line") + "\n" + GalleryText.Get("Second line"));
    private readonly MeasurementPrimitives _primitives;
    private readonly MeasurementForm _form;
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal int Measurements => _primitives.Measurements;
    internal bool? Checked => _checked.Value;
    internal int ProgressReads => _primitives.ProgressReads;
    internal float LastProgressValue => _primitives.LastProgressValue;
    internal int Navigations => _primitives.Navigations;
    internal UiRect LinkBounds => _primitives.LinkBounds;
    internal string FormValue => _formValue.Value;
    internal int FormMeasurements => _form.Measurements;
    internal string? TabValue => _tabValue.Value;
    internal int TabMeasurements => _form.TabMeasurements;
    internal string MemoValue => _memo.Value;
    internal bool MeasurementTooltipOpen => _form.TooltipOpen;
    internal int DelegatedMeasurements => _form.DelegatedMeasurements;
    internal int OwnedSizingMeasurements => _form.OwnedSizingMeasurements;

    internal MeasurementPage()
    {
        var primitivesDefinition = GallerySample.Define("primitives", () => new MeasurementPrimitives(_checked, _value, _choice));
        var primitives = _primitives = primitivesDefinition.Value;
        var primitiveSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                Fluent.Button(GalleryText.Get("Probe a frame"), static () => { }),
                Fluent.Button(GalleryText.Get("Change measurement label"), primitives.ToggleLabel),
                Fluent.Button(GalleryText.Get("Change measurement width"), primitives.ToggleWidth),
                Fluent.Button(GalleryText.Get("Change measured body"), primitives.ChangeBody)), primitives));
        var primitiveFactory = primitiveSample.Source;
        var primitiveDeclaration = GallerySample.LocalizeSource(GalleryPageSource.Declaration(nameof(MeasurementPrimitives)));
        primitiveSample.BindSource(() => (_checked.Value, Slider : _value.Value, Choice : _choice.Value,
            primitives.CheckboxLabel, primitives.BodyText, primitives.BadgeCount, primitives.SpinnerLabel, primitives.Width, primitives.Navigations), state =>
        {
            var check = state.Value is { } value ? GallerySample.Boolean(value) : "null";
            return $"var _checked = new UiState<bool?>({check});\nvar _value = new UiState<float>({GallerySample.Number(state.Slider)});\n"
                + $"var _choice = new UiState<string?>({GallerySample.String(state.Choice)});\n\n" + primitivesDefinition.Source
                + $"\nprimitives.CheckboxLabel = {GallerySample.String(state.CheckboxLabel)};\nprimitives.BodyText = {GallerySample.String(state.BodyText)};\n"
                + $"primitives.BadgeCount = {state.BadgeCount};\nprimitives.SpinnerLabel = {GallerySample.String(state.SpinnerLabel)};\n"
                + $"primitives.Width = {GallerySample.Number(state.Width)};\nprimitives.Navigations = {state.Navigations};\n\n" + primitiveFactory
                + "\n\n" + primitiveDeclaration;
        });
        var formDefinition = GallerySample.Define("form", () => new MeasurementForm(_formValue, _formChecked, _tabValue, _memo));
        var form = _form = formDefinition.Value;
        var formSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Button(GalleryText.Get("Change tab size"), form.ToggleTabSize), form));
        var formFactory = formSample.Source;
        var formDeclaration = GallerySample.LocalizeSource(GalleryPageSource.Declaration(nameof(MeasurementForm)));
        formSample.BindSource(() => (_formValue.Value, Checked : _formChecked.Value, Tab : _tabValue.Value, Memo : _memo.Value, form.TabSize), state =>
            $"var _formValue = new UiState<string>({GallerySample.String(state.Value)});\nvar _formChecked = new UiState<bool>({GallerySample.Boolean(state.Checked)});\n"
            + $"var _tabValue = new UiState<string?>({GallerySample.String(state.Tab)});\nvar _memo = new UiState<string>({GallerySample.String(state.Memo)});\n\n"
            + formDefinition.Source + $"\nform.TabSize = UiTabListSize.{state.TabSize};\n\n" + formFactory
            + "\n\n" + formDeclaration);
        Samples = [primitiveSample, formSample];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Try the checkbox, slider, selection, and link. The display and progress can change even when child measurement counts stay the same.")),
            Fluent.Body1(GalleryText.Get("Changing the label or parent width recalculates layout. These counters do not measure FPS or GPU work.")),
            Fluent.Body1(() => GalleryText.Format("Child measurements: {0} / Value: {1:F2} / Selection: {2} / Link actions: {3}", Measurements, _value.Value,
                GalleryText.Get(_choice.Value switch { "a" => "Reuse Alpha", "b" => "Reuse Beta", _ => "None selected" }), Navigations)),
            Fluent.Body1(() => GalleryText.Format("Form measurements: {0} / Input: {1} / Menu checkbox: {2}", FormMeasurements, _formValue.Value, GalleryText.Get(_formChecked.Value ? "On" : "Off"))),
            Fluent.Body1(() => GalleryText.Format("Natural tab measurements (process): {0} / Selection: {1}", TabMeasurements, GalleryText.Get(_tabValue.Value switch
                { "a" => "Measurement tab Alpha", "b" => "Measurement tab Beta", "c" => "Measurement tab Gamma", _ => "None selected" }))),
            Fluent.Body1(() => GalleryText.Format("Delegated parent measurements: {0} / Note length: {1}", DelegatedMeasurements, _memo.Value.Length)),
            Fluent.Body1(() => GalleryText.Format("Owned child measurements: {0}", OwnedSizingMeasurements)),
            Dux.Wrap(new(Spacing : 16, RunSpacing : 16),
                Dux.ConstrainedBox(primitiveSample, new(0, 420, 0, float.PositiveInfinity)),
                Dux.ConstrainedBox(formSample, new(0, 420, 0, float.PositiveInfinity))));
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
