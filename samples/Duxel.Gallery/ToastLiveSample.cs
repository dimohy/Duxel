using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ToastLiveSample : IDisposable
{
    private readonly UiState<int> _actions = new(0);
    private readonly List<string> _history = [];
    private string _historyText = GalleryText.Get("No recent toasts.");
    private UiToastOptions _last = new(GalleryText.Get("Document saved"), GalleryText.Get("The document is ready"), UiToastIntent.Success);
    internal UiToastController Controller { get; } = new();
    internal UiToasterWidget Surface { get; }
    internal IGallerySample Sample { get; }
    internal UiWidget Root { get; }
    internal ToastLiveSample()
    {
        Surface = Fluent.Toaster(Controller);
        var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button(GalleryText.Get("Show save toast"), () => Show(false)), Fluent.Button(GalleryText.Get("Show toast with an action"), () => Show(true)),
                Fluent.Button(GalleryText.Get("Show progress toast"), ShowProgress),
                Fluent.Button(GalleryText.Get("Show mixed-language toast"), () => Publish(new("אבג English 한글 123", "English مرحبا אבג 한글")
                    { Timeout = null, Progress = new(.25f, "התקדמות 25%"), CanDismiss = true, CloseLabel = GalleryText.Get("Dismiss toast") })),
                Fluent.Button(GalleryText.Get("Reverse toast direction"), () => Surface.Options = Surface.Options with { RightToLeft = !Surface.Options.RightToLeft }),
                Fluent.Button(GalleryText.Get("Change toast width"), () => Surface.Options = Surface.Options with
                    { Width = Surface.Options.Width == 292 ? 160 : Surface.Options.Width == 160 ? 80 : 292 }),
                Fluent.Button(GalleryText.Get("Show long toast"), () => Publish(new(GalleryText.Get("Long toast"), string.Join(" ", Enumerable.Repeat(GalleryText.Get("Scroll the long message and try the action below."), 30)))
                    { Timeout = null, CanDismiss = true, CloseLabel = GalleryText.Get("Dismiss toast"), ActionLabel = GalleryText.Get("Confirm"), Action = () => _actions.Value++ })),
                Fluent.Button(GalleryText.Get("Dismiss all live toasts"), () => Controller.DismissAll()),
                Fluent.Button(GalleryText.Get("Change toast position"), () => Surface.Options = Surface.Options with
                    { Position = Surface.Options.Position == UiToastPosition.BottomEnd ? UiToastPosition.TopEnd : UiToastPosition.BottomEnd }),
                Fluent.Button(GalleryText.Get("Toggle reduced motion for toasts"), () => Surface.Options = Surface.Options with { ReducedMotion = !Surface.Options.ReducedMotion })),
            Fluent.Body1(GalleryText.Get("Toasts appear in a corner. Hover or focus a toast to pause its timer. Press F6 to enter or return, and press Delete or use the dismiss button to close it.")),
            Fluent.Body1(() => GalleryText.Format("Toast actions: {0}", _actions.Value)), Fluent.Body1(() => _historyText)));
        sample.BindSource(() => (Surface.Options, Toast : _last), value =>
            "var controller = new UiToastController();\nusing var toaster = Fluent.Toaster(controller, new(\n    Position : UiToastPosition."
            + value.Options.Position + ", Width : " + GallerySample.Number(value.Options.Width)
            + ", RightToLeft : " + GallerySample.Boolean(value.Options.RightToLeft)
            + ", ReducedMotion : " + GallerySample.Boolean(value.Options.ReducedMotion) + "));\n"
            + "var actions = new UiState<int>(0);\nvar history = new List<string>();\nhistory.Insert(0, " + GallerySample.String(value.Toast.Title) + ");\n"
            + "controller.Dispatch(new(" + GallerySample.String(value.Toast.Title) + ", " + GallerySample.String(value.Toast.Body)
            + ", UiToastIntent." + value.Toast.Intent + ")\n{\n    Timeout = " + (value.Toast.Timeout is null ? "null" : "TimeSpan.FromSeconds(7)")
            + (value.Toast.Progress is { } progress ? ",\n    Progress = new(" + (progress.Value is { } number ? GallerySample.Number(number) : "null")
                + ", " + GallerySample.String(progress.StatusText) + ")" : "")
            + ",\n    CanDismiss = " + GallerySample.Boolean(value.Toast.CanDismiss)
            + (value.Toast.CanDismiss ? ",\n    CloseLabel = " + GallerySample.String(value.Toast.CloseLabel) : "")
            + (value.Toast.Action is null ? "" : ",\n    ActionLabel = " + GallerySample.String(value.Toast.ActionLabel) + ",\n    Action = () => actions.Value++")
            + "\n});\n// Render once each frame after the page content; this portal reserves no layout space.\ntoaster.Render(ui);");
        Sample = sample;
        Root = Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(GalleryText.Get("Live toasts")), sample);
    }
    private void ShowProgress() => Publish(new(GalleryText.Get("Transferring file"), GalleryText.Get("Track progress directly in the toast"))
        { Timeout = null, Progress = new(.35f, GalleryText.Get("35% complete")), CanDismiss = true, CloseLabel = GalleryText.Get("Dismiss toast") });
    private void Show(bool actionable)
    {
        Publish(actionable ? new(GalleryText.Get("Confirmation required"), GalleryText.Get("Try the confirmation button below"), UiToastIntent.Info)
        { Timeout = null, CanDismiss = true, CloseLabel = GalleryText.Get("Dismiss toast"), ActionLabel = GalleryText.Get("Confirm"), Action = () => _actions.Value++ }
            : new(GalleryText.Get("Document saved"), GalleryText.Get("The document is ready"), UiToastIntent.Success));
    }
    private void Publish(UiToastOptions options)
    {
        _last = options;
        _history.Insert(0, _last.Title);
        if (_history.Count > 4) _history.RemoveAt(4);
        _historyText = GalleryText.Format("Recent toasts: {0}", string.Join(" · ", _history));
        Controller.Dispatch(_last);
    }
    public void Dispose() { Surface.Dispose(); Root.Dispose(); }
}
