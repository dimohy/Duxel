using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ProgressIndicatorPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "ProgressIndicator";
    public string Description => "Show a task name and description next to the progress bar. Use an indeterminate indicator when the progress is unknown.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content() => Root = Dux.Column(new(Spacing : 20, MainAxisSize : UiMainAxisSize.Min),
            Example(GalleryText.Get("File upload"), .25f, GalleryText.Get("Sending the selected file to the server.")),
            Example(GalleryText.Get("Server connection"), null, GalleryText.Get("Waiting for the server to respond. The task name and description stay visible when the bar is hidden.")),
            CircularExample(),
            WebExample());

        private UiWidget CircularExample()
        {
            var sample = GallerySample.Create(() => Fluent.CircularProgressIndicator(.25f));
            var preview = sample.Preview;
            preview.AccessibleName = GalleryText.Get("File sync circular progress");
            preview.Size = new(40, 40);
            sample.BindSource(() => (preview.Value, preview.ReducedMotion), state =>
                "var progress = Fluent.CircularProgressIndicator(" + GallerySample.Number(state.Value) + ");\n"
                + "progress.AccessibleName = " + GallerySample.String(preview.AccessibleName) + ";\n"
                + "progress.Size = new(40, 40);\n"
                + "progress.ReducedMotion = " + GallerySample.Boolean(state.ReducedMotion) + ";");
            Samples.Add(sample);

            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("Circular progress")),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Increase circular progress"), () => preview.Value = ((preview.Value ?? 0) + .25f) % 1.25f),
                    Fluent.Button(GalleryText.Get("Toggle circular waiting state"), () => preview.Value = preview.Value is null ? .5f : null),
                    Fluent.Button(GalleryText.Get("Toggle circular reduced motion"), () => preview.ReducedMotion = !preview.ReducedMotion)),
                sample);
        }

        private UiWidget WebExample()
        {
            var initial = new UiProgressIndicatorOptions(GalleryText.Get("Document upload"), GalleryText.Get("Uploading the document."), .25f,
                Width : 360, ValueText : GalleryText.Get("Uploading"))
            {
                FinishIndeterminateCycle = true
            };
            var sample = CreateLinearSample(initial);
            var preview = sample.Preview;
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("Web progress transitions and errors")),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Start waiting"), () => preview.Options = initial with
                    { PercentComplete = null, Description = GalleryText.Get("Waiting for a server response."), ValueText = GalleryText.Get("Waiting for server") }),
                    Fluent.Button(GalleryText.Get("Complete upload"), () => preview.Options = initial with
                    { PercentComplete = 1, Description = GalleryText.Get("The document finished uploading."), ValueText = GalleryText.Get("Upload complete") }),
                    Fluent.Button(GalleryText.Get("Show upload error"), () => preview.Options = preview.Options with
                    {
                        Color = UiProgressColor.Error,
                        ProgressHidden = preview.Options.PercentComplete is null,
                        Description = GalleryText.Get("Upload failed. Check the connection and try again."),
                        ValueText = GalleryText.Get("Upload failed")
                    }),
                    Fluent.Button(GalleryText.Get("Retry upload"), () => preview.Options = initial)), sample);
        }

        private GallerySample<UiFluentProgressIndicator> CreateLinearSample(UiProgressIndicatorOptions options)
        {
            var sample = GallerySample.Create(() => Fluent.ProgressIndicator(options));
            var preview = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => preview.Options, state =>
                "var options = new UiProgressIndicatorOptions(Label : " + GallerySample.String(state.Label)
                + ",\n    Description : " + GallerySample.String(state.Description)
                + ",\n    PercentComplete : " + GallerySample.Number(state.PercentComplete)
                + ", ProgressHidden : " + GallerySample.Boolean(state.ProgressHidden)
                + ",\n    BarHeight : " + GallerySample.Number(state.BarHeight) + ", Width : " + GallerySample.Number(state.Width)
                + ", ReducedMotion : " + GallerySample.Boolean(state.ReducedMotion)
                + ",\n    ValueText : " + GallerySample.String(state.ValueText) + ")\n"
                + "{\n    Color = UiProgressColor." + state.Color
                + ",\n    FinishIndeterminateCycle = " + GallerySample.Boolean(state.FinishIndeterminateCycle) + "\n};\n\n" + factory);
            Samples.Add(sample);
            return sample;
        }

        private UiWidget Example(string label, float? value, string description)
        {
            var sample = CreateLinearSample(new(label, description, value, Width : 360, ValueText : GalleryText.Get("Processing")));
            var preview = sample.Preview;
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Format("Increase progress for {0}", label), () => preview.Options = preview.Options with
                    { PercentComplete = ((preview.Options.PercentComplete ?? 0) + .25f) % 1.25f }),
                    Fluent.Button(GalleryText.Format("Toggle waiting for {0}", label), () => preview.Options = preview.Options with
                    { PercentComplete = preview.Options.PercentComplete is null ? .5f : null }),
                    Fluent.Button(GalleryText.Format("Toggle progress bar for {0}", label), () => preview.Options = preview.Options with
                    { ProgressHidden = !preview.Options.ProgressHidden }),
                    Fluent.Button(GalleryText.Format("Toggle reduced motion for {0}", label), () => preview.Options = preview.Options with
                    { ReducedMotion = !preview.Options.ReducedMotion })), sample);
        }
    }
}
