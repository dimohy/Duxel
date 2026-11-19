using System.Numerics;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgRasterSurface
{
    private Dictionary<int, SvgSamplePixel>? _samplePixels;

    private SvgSamplePixel Samples(int index) => _samplePixels is not null && _samplePixels.TryGetValue(index, out var samples)
        ? samples : SvgSamplePixel.FromUniform(_pixels[index]);

    private void StoreSamples(int index, SvgSamplePixel samples)
    {
        _pixels[index] = samples.Resolve();
        if (samples.Uniform)
            _samplePixels?.Remove(index);
        else
        {
            _samplePixels ??= [];
            _samplePixels[index] = samples;
        }
    }

    private void Blend(int index, Vector4 foreground, byte mask)
    {
        if (mask == 15 && (_samplePixels is null || !_samplePixels.ContainsKey(index)))
        {
            _pixels[index] = SvgCompositing.Over(foreground, _pixels[index]);
            return;
        }
        StoreSamples(index, Samples(index).Over(foreground, mask));
    }

    private void CompositePixel(SvgRasterSurface source, int sourceIndex, int index, float opacity, float sourceCoverage = 1)
    {
        var coverage = _clip is { } clip ? SvgViewport.Coverage(clip, index % Width, index / Width) : 1;
        if (opacity == 0 || coverage == 0)
            return;
        if ((_samplePixels is null || !_samplePixels.ContainsKey(index)) &&
            (source._samplePixels is null || !source._samplePixels.ContainsKey(sourceIndex)))
            _pixels[index] = SvgCompositing.Over(SvgCompositing.LayerSource(source._pixels[sourceIndex], opacity, sourceCoverage) * coverage, _pixels[index]);
        else
            StoreSamples(index, Samples(index).Composite(source.Samples(sourceIndex), opacity, coverage, sourceCoverage));
    }
}
