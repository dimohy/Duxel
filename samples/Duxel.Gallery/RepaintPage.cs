using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class RepaintPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly RepaintExperiment _experiment;
    private readonly UiWidget _root;
    internal bool Running => _experiment.Running;
    internal long StaticBuilds => _experiment.StaticBuilds;
    internal long TextBuilds => _experiment.TextBuilds;
    internal string CpuSnapshot => _experiment.CpuSnapshot;
    public string Title => "갱신 관찰";
    public string Description => "Green borders show the regions actually repainted by Vulkan.";
    public IReadOnlyList<IGallerySample> Samples { get; }
    UiWidget IObserverNaturalPage.MeasureRoot => _root;

    internal RepaintPage()
    {
        var definition = GallerySample.Define("experiment", () => new RepaintExperiment());
        var experiment = _experiment = definition.Value;
        var sample = GallerySample.Create(() => experiment);
        var factory = sample.Source;
        sample.BindSource(() => (experiment.AutoChange, experiment.Profile), state => definition.Source
            + $"\nexperiment.AutoChange = {GallerySample.Boolean(state.AutoChange)};\nexperiment.Profile = {GallerySample.Boolean(state.Profile)};\n\n"
            + factory + "\n\n" + GalleryPageSource.Declaration(nameof(RepaintExperiment)));
        Samples = [sample];
        _root = Dux.ScrollView(sample);
    }
    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();
}
