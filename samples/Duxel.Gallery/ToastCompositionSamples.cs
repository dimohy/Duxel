using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ToastCompositionSamples
{
    internal List<IGallerySample> Samples { get; } = [];
    internal UiWidget Root { get; }
    internal ToastCompositionSamples() => Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
        Example(GalleryText.Get("Standalone toast composition"), new(GalleryText.Get("Toast sample"), GalleryText.Get("Try the body and dismiss button"))
            { CanDismiss = true, CloseLabel = GalleryText.Get("Dismiss toast"), DisableAnnouncement = true }, false),
        Example(GalleryText.Get("Progress composition"), Progress(.35f), true));
    private static UiToastOptions Progress(float? value) => new(GalleryText.Get("Transferring file"), GalleryText.Get("Keep the toast open until the transfer finishes"))
        { Timeout = null, Progress = new(value), CanDismiss = true, CloseLabel = GalleryText.Get("Dismiss toast"), DisableAnnouncement = true };
    private UiWidget Example(string title, UiToastOptions options, bool progress)
    {
        var sample = GallerySample.Create(() => Fluent.Toast(options));
        var preview = sample.Preview;
        var buttons = new List<UiWidget>
        {
            Fluent.Button(GalleryText.Format("Show {0} again", title), preview.Show), Fluent.Button(GalleryText.Format("Hide {0}", title), preview.Hide),
            Fluent.Button(GalleryText.Format("Reverse direction for {0}", title), () => preview.RightToLeft = !preview.RightToLeft),
            Fluent.Button(GalleryText.Format("Toggle mixed-language text for {0}", title), () => preview.Options = preview.Options with
            {
                Title = preview.Options.Title == options.Title ? "אבג English 한글 123" : options.Title,
                Body = preview.Options.Body == options.Body ? "English مرحبا אבג 한글" : options.Body
            }),
            Fluent.Button(GalleryText.Format("Change width for {0}", title), () => preview.Width = preview.Width == 292 ? 420 : preview.Width == 420 ? 80 : 292)
        };
        if (progress)
        {
            buttons.Add(Fluent.Button(GalleryText.Get("Add 10% progress"), () => preview.Options = Progress(MathF.Min(1, (preview.Options.Progress?.Value ?? 0) + .1f))));
            buttons.Add(Fluent.Button(GalleryText.Get("Change progress mode"), () => preview.Options = Progress(preview.Options.Progress is { Value: null } ? .35f : null)));
            buttons.Add(Fluent.Button(GalleryText.Get("Toggle pause for progress animation"), () => preview.AnimationPaused = !preview.AnimationPaused));
            buttons.Add(Fluent.Button(GalleryText.Get("Show transfer completion"), () => preview.Options = new(GalleryText.Get("Transfer complete"), GalleryText.Get("The file is ready to view"), UiToastIntent.Success)
                { CanDismiss = true, CloseLabel = GalleryText.Get("Dismiss toast"), DisableAnnouncement = true }));
        }
        sample.BindSource(() => (preview.Options, preview.Width, preview.ReducedMotion, preview.AnimationPaused, preview.RightToLeft, preview.IsOpen), value =>
            "using var toast = Fluent.Toast(new(" + GallerySample.String(value.Options.Title) + ", " + GallerySample.String(value.Options.Body)
            + ", UiToastIntent." + value.Options.Intent + ")\n{\n    CanDismiss = " + GallerySample.Boolean(value.Options.CanDismiss)
            + ",\n    CloseLabel = " + GallerySample.String(value.Options.CloseLabel) + ",\n    DisableAnnouncement = " + GallerySample.Boolean(value.Options.DisableAnnouncement)
            + ",\n    Timeout = " + (value.Options.Timeout is null ? "null" : "TimeSpan.FromSeconds(7)")
            + (value.Options.Progress is { } state ? ",\n    Progress = new(" + (state.Value is { } number ? GallerySample.Number(number) : "null")
                + ", " + GallerySample.String(state.StatusText) + ")" : "")
            + "\n});\ntoast.Width = " + GallerySample.Number(value.Width) + ";\ntoast.ReducedMotion = " + GallerySample.Boolean(value.ReducedMotion)
            + ";\ntoast.AnimationPaused = " + GallerySample.Boolean(value.AnimationPaused) + ";\ntoast.RightToLeft = " + GallerySample.Boolean(value.RightToLeft)
            + ";" + (value.IsOpen ? "" : "\ntoast.Hide();"));
        Samples.Add(sample);
        return Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(title),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8), buttons.ToArray()), sample);
    }
}
