using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MeasurementPrimitives : UiWidget
{
    private readonly UiSizedBox _root;
    private readonly Checkbox _checkbox;
    private readonly Slider _slider;
    private readonly Radio _radio;
    private readonly Body _body;
    private readonly Progress _progress;
    private readonly Badge _badge;
    private readonly Link _link;
    private readonly Spinner _spinner;
    internal int ProgressReads { get; private set; }
    internal float LastProgressValue { get; private set; }
    internal int Navigations { get; set; }
    internal UiRect LinkBounds => _link.Bounds;
    internal int Measurements => _checkbox.Measurements + _slider.Measurements + _radio.Measurements
        + _body.Measurements + _progress.Measurements + _badge.Measurements + _link.Measurements + _spinner.Measurements;
    internal string CheckboxLabel { get => _checkbox.Label; set => _checkbox.Label = value; }
    internal string BodyText { get => _body.Source; set => _body.Source = value; }
    internal int BadgeCount { get => _badge.Count; set => _badge.Count = value; }
    internal string? SpinnerLabel { get => _spinner.Label; set => _spinner.Label = value; }
    internal float? Width { get => _root.Options.Width; set => _root.Options = _root.Options with { Width = value }; }

    internal MeasurementPrimitives(UiState<bool?> check, UiState<float> value, UiState<string?> choice)
    {
        _checkbox = new(check.Binding);
        _slider = new(value.Binding);
        _radio = new(choice.Binding);
        _body = new();
        _progress = new(() =>
        {
            ProgressReads++;
            LastProgressValue = value.Value;
            return new(LastProgressValue);
        }) { Size = new(0, 4) };
        _badge = new();
        _link = new(_ => { Navigations++; RequestRebuild(); });
        _spinner = new();
        _root = Dux.SizedBox(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            _body, _checkbox, _slider, _radio, _link, _progress, _badge, _spinner), 420, 420);
        OwnChild("root", _root);
    }
    internal void ToggleLabel() => CheckboxLabel = CheckboxLabel == GalleryText.Get("Reuse checkbox") ? GalleryText.Get("Reuse checkbox: changed label") : GalleryText.Get("Reuse checkbox");
    internal void ToggleWidth() => Width = Width == 420 ? 300 : 420;
    internal void ChangeBody()
    {
        BodyText = BodyText == GalleryText.Get("Reuse body") ? GalleryText.Get("Reuse body: content changed.") : GalleryText.Get("Reuse body");
        BadgeCount++;
        SpinnerLabel = SpinnerLabel == GalleryText.Get("Reuse spinner") ? GalleryText.Get("Reuse spinner: changed") : GalleryText.Get("Reuse spinner");
    }
    protected override bool CanRetainMeasurement => CanRetainOwnedMeasurement(_root);
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
    private sealed class Checkbox(UiBinding<bool?> value) : UiToggle<bool?, UiNullableToggleValue, UiReleaseButtonBehavior, UiFluentCheckboxPresentation>(GalleryText.Get("Reuse checkbox"), value)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Slider(UiBinding<float> value) : UiSlider<UiFluentSliderBehavior, UiFluentSliderPresentation>(GalleryText.Get("Reuse slider"), value, 0, 1, .01f)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Radio(UiBinding<string?> value) : UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(
        GalleryText.Get("Reuse selection"), value, new UiRadioOption[] { new("a", GalleryText.Get("Reuse Alpha")), new("b", GalleryText.Get("Reuse Beta")) })
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Body() : UiText<UiFluentWrappedTextPresentation>(GalleryText.Get("Reuse body"))
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Progress(Func<UiProgressValue> source) : UiProgress<Func<UiProgressValue>, UiGetterProgressSource, UiFluentProgressPresentation>(source)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Badge() : UiCounterBadge<UiFluentBadgePresentation>(1)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Link(Action<Uri> navigate) : UiLink<UiLinkNavigationBehavior, UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>(
        GalleryText.Get("Reuse link"), new("/measurement", UriKind.Relative), navigate)
    {
        internal int Measurements;
        internal UiRect Bounds;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            var position = ui.GetItemRectMin(); var size = ui.GetItemRectSize();
            Bounds = new(position.X, position.Y, size.X, size.Y);
        }
    }
    private sealed class Spinner() : UiSpinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation>(GalleryText.Get("Reuse spinner"), new() { Paused = true })
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
}
