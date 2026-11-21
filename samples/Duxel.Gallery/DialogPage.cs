using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class DialogPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    internal UiFluentDialog? Preview => _content?.Dialog;
    internal float MotionValue => _content?.Value ?? 0;
    public string Title => "대화 상자";
    public string Description => "Compare modal, nonmodal, and alert dialogs. Enter some text and close the dialog to return focus to its opening button.";
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    void IObserverNaturalPage.PrepareContent(UiImmediateContext ui) => (_content ??= new()).Prepare(ui);
    void IObserverNaturalPage.RenderOverlays(UiImmediateContext ui) => _content!.RenderOverlays(ui);
    public void Render(UiImmediateContext ui)
    {
        var content = _content ??= new();
        content.Prepare(ui);
        content.Root.Render(ui);
        content.RenderOverlays(ui);
    }
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        private UiFloatTransition _transition = new(0);
        private UiCubicBezier _curve = new(0, 0, 0, 1);
        private float _target, _value;
        private readonly Action<UiImmediateContext> _prepare, _renderOverlays;
        internal float Value => _value;
        internal UiFluentDialog Dialog { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal void Prepare(UiImmediateContext ui) => _prepare(ui);
        internal void RenderOverlays(UiImmediateContext ui) => _renderOverlays(ui);
        internal UiWidget Root
        {
            get;
        }
        internal Content()
        {
            var name = new UiState<string>("");
            var message = new UiState<string>(GalleryText.Get("Enter your name, then save."));
            var open = new UiState<bool>(false);
            var dialogOptions = new UiDialogOptions(CloseLabel : GalleryText.Get("Close"), Description : GalleryText.Get("Enter your name, then save or cancel."));
            var dialogDefinition = GallerySample.Define("_dialog", () =>
            {
                UiFluentDialog? dialog = null;
                var input = Fluent.Input(GalleryText.Get("Name"), name.Binding, 100, 300);
                var save = Fluent.Button(GalleryText.Get("Save"), () =>
                {
                    if (string.IsNullOrWhiteSpace(name.Value))
                    {
                        message.Value = GalleryText.Get("Enter a name before saving.");
                        return;
                    }
                    message.Value = string.Format(GalleryText.Get("Saved: {0}"), name.Value);
                    dialog!.Close();
                });
                var cancel = Fluent.Button(GalleryText.Get("Cancel"), () => dialog!.Close());
                dialog = Fluent.Dialog(GalleryText.Get("User details"), Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Body1(GalleryText.Get("Use Tab to move between controls and try typing Korean. Your input is kept when you reopen the dialog.")),
                    Fluent.Label(GalleryText.Get("Name"), input), input, Fluent.Body1(() => message.Value)), [save, cancel], dialogOptions);
                dialog.Open = open.Binding;
                return dialog;
            });
            var _dialog = Dialog = dialogDefinition.Value;
            var showDefinition = GallerySample.Define<Action<UiDialogType>>("show", () => (UiDialogType type) =>
            {
                _dialog.Options = _dialog.Options with
                {
                    Type = type,
                    SuppressEscapeDismissal = type == UiDialogType.Alert
                };
                _dialog.Show();
            });
            var show = showDefinition.Value;
            var renderDefinition = GallerySample.Define<Action<UiImmediateContext>>("renderOverlays", () => (UiImmediateContext ui) => _dialog.Render(ui));
            _renderOverlays = renderDefinition.Value;
            var dialogSample = GallerySample.Create(() => Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button(GalleryText.Get("Open modal"), () => show(UiDialogType.Modal)),
                Fluent.Button(GalleryText.Get("Open nonmodal"), () => show(UiDialogType.NonModal)),
                Fluent.Button(GalleryText.Get("Open alert"), () => show(UiDialogType.Alert))));
            var dialogFactory = dialogSample.Source;
            dialogSample.BindSource(() => (Name : name.Value, Message : message.Value, Open : open.Value, _dialog.Options), state =>
                $"var name = new UiState<string>({GallerySample.String(state.Name)});\nvar message = new UiState<string>({GallerySample.String(state.Message)});\n"
                + $"var open = new UiState<bool>({GallerySample.Boolean(state.Open)});\nvar dialogOptions = {SourceOptions(state.Options)};\n\n"
                + dialogDefinition.Source + "\n\n" + showDefinition.Source + "\n\n" + dialogFactory + "\n\n" + renderDefinition.Source
                + "\n// Invoke renderOverlays(ui) after rendering example in each frame.");

            var prepareDefinition = GallerySample.Define<Action<UiImmediateContext>>("prepareMotion", () => (UiImmediateContext ui) =>
            {
                _value = ui.AnimateFloat(ref _transition, _target, new(.25) { Curve = _curve });
            });
            _prepare = prepareDefinition.Value;
            UiWidget? motionPreview = null;
            var curveDefinition = GallerySample.Define<Action<UiCubicBezier>>("setCurve", () => (UiCubicBezier curve) =>
            {
                _curve = curve;
                _target = _target == 0 ? 1 : 0;
                motionPreview!.RequestRebuild();
            });
            var setCurve = curveDefinition.Value;
            var motionSample = GallerySample.Create(() =>
            {
                var progress = Fluent.ProgressBar(() => new(_value, 1, ReducedMotion : true));
                progress.Size = new(460, 4);
                motionPreview = progress;
                return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                        Fluent.Button(GalleryText.Get("Entrance curve"), () => setCurve(new(0, 0, 0, 1))),
                        Fluent.Button(GalleryText.Get("Exit curve"), () => setCurve(new(.8, 0, .78, 1))),
                        Fluent.Button(GalleryText.Get("Backdrop curve"), () => setCurve(new(.33, 0, .67, 1)))), progress);
            });
            var motionFactory = motionSample.Source;
            motionSample.BindSource(() => (_curve, _target), state =>
                $"var _transition = new UiFloatTransition(0);\nvar _target = {Number(state._target)}f;\nvar _value = 0f;\n"
                + $"var _curve = new UiCubicBezier({Number(state._curve.X1)}d, {Number(state._curve.Y1)}d, {Number(state._curve.X2)}d, {Number(state._curve.Y2)}d);\n"
                + "UiWidget? motionPreview = null;\n\n" + prepareDefinition.Source + "\n\n" + curveDefinition.Source + "\n\n" + motionFactory
                + "\n// Invoke prepareMotion(ui) before rendering example in each frame.");
            Samples = [dialogSample, motionSample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("The modal blocks background input. The nonmodal dialog leaves the background interactive. Clicking the background does not close the alert.")),
                Fluent.Body1(() => GalleryText.Format("Open: {0} / Name: {1}", GalleryText.Get(open.Value ? "On" : "Off"), name.Value)),
                Fluent.Caption1(GalleryText.Get("Use Escape, Tab, and Shift+Tab, and try saving to see input validation. Closing the dialog returns focus to the button that opened it.")),
                dialogSample,
                Fluent.Subtitle2(GalleryText.Get("Motion curve preview")),
                Fluent.Body1(GalleryText.Get("Press a button to start a 250 ms transition. Reversing continues from the current position. When the transition ends, it stops requesting frames.")),
                Fluent.Caption1(GalleryText.Get("Several widgets share these animation curves. The dialog changes size and opacity as it opens and animates a snapshot as it closes. Reopening midway continues from its current state.")),
                motionSample));
        }
        private static string Number(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static string SourceOptions(UiDialogOptions value) => $"new UiDialogOptions(Type : UiDialogType.{value.Type}, Width : {Number(value.Width)}f,\n"
            + $"    MaximumHeight : {(value.MaximumHeight is { } height ? Number(height) + "f" : "null")}, LightDismiss : {GallerySample.Boolean(value.LightDismiss)},\n"
            + $"    ShowCloseButton : {(value.ShowCloseButton is { } close ? GallerySample.Boolean(close) : "null")}, CloseLabel : {GallerySample.String(value.CloseLabel)},\n"
            + $"    Description : {GallerySample.String(value.Description)}, MotionEnabled : {GallerySample.Boolean(value.MotionEnabled)})"
            + $" {{ SuppressEscapeDismissal = {GallerySample.Boolean(value.SuppressEscapeDismissal)} }}";
        public void Dispose() { Dialog.Dispose(); Root.Dispose(); }
    }
}
