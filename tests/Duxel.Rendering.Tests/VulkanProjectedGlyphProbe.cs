using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    internal sealed class ProjectedGlyphProbe(VulkanRendererBackend renderer) : IDisposable
    {
        internal Buffer Buffer;
        internal DeviceMemory Memory;
        internal Pipeline Pipeline;
        internal ulong Address;
        internal UiProjectedGlyphDraw Glyphs = null!;

        public void Dispose()
        {
            if (Pipeline.Handle != 0 || Buffer.Handle != 0)
                renderer.WaitForAllInFlightFrameFences();
            if (Pipeline.Handle != 0)
                renderer._vk.DestroyPipeline(renderer._device, Pipeline, null);
            if (Buffer.Handle != 0)
                renderer._vk.DestroyBuffer(renderer._device, Buffer, null);
            if (Memory.Handle != 0)
                renderer._vk.FreeMemory(renderer._device, Memory, null);
            Pipeline = default;
            Buffer = default;
            Memory = default;
            Address = 0;
        }
    }

    internal sealed record ProjectedGlyphDraw(ProjectedGlyphProbe Probe, UiRect Clip);

    internal ProjectedGlyphProbe CreateProjectedGlyphProbe(UiGpuVectorScene scene, UiVector2 origin, float scale, UiColor color,
        ReadOnlySpan<byte> vertexCode, ReadOnlySpan<byte> fragmentCode)
    {
        if (!SupportsProjectedGlyphSamples)
            throw new NotSupportedException("The projected glyph probe requires one sample or four standard samples.");
        if (color.Rgba >> 24 != 255)
            throw new ArgumentException("The depth-writing glyph probe requires opaque ink.", nameof(color));
        var probe = new ProjectedGlyphProbe(this) { Glyphs = new(scene, origin, scale, color) };
        try
        {
            var resource = UploadProjectedGlyphs(scene);
            probe.Buffer = resource.Buffer;
            probe.Memory = resource.Memory;
            probe.Address = resource.Address;
            fixed (Pipeline* pipeline = &probe.Pipeline)
                CreateProjectedGlyphPipeline(vertexCode, fragmentCode, pipeline);
            return probe;
        }
        catch
        {
            probe.Dispose();
            throw;
        }
    }

    partial void RecordValidationProjectedGlyph(in UiDrawCommand command, in CommandFrameContext context,
        ref CommandRecordingState state, ref bool handled)
    {
        if (command.UserData is not ProjectedGlyphDraw draw)
            return;
        handled = true;
        if (!state.Pipeline.Layer3DEnabled || state.Pipeline.DepthMode != 2 || draw.Probe.Address == 0)
            throw new InvalidOperationException("A projected glyph probe requires a live depth-writing plane.");
        var clipped = command with { ClipRect = draw.Clip };
        RecordProjectedGlyphDraw(in clipped, draw.Probe.Glyphs, draw.Probe.Pipeline, draw.Probe.Address, in context, ref state);
    }
}
