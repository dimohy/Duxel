using System.Numerics;
using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const nuint ProjectedGlyphPageBytes = 4 * 1024 * 1024;
    private const nuint ProjectedGlyphCacheBytes = 48 * 1024 * 1024;
    private readonly record struct ProjectedGlyphBuffer(Buffer Buffer, DeviceMemory Memory, ulong Address, nuint Bytes);
    private sealed class ProjectedGlyphPage(ProjectedGlyphBuffer buffer, void* mapped, nuint capacity)
    {
        public readonly ProjectedGlyphBuffer Buffer = buffer;
        public readonly byte* Mapped = (byte*)mapped;
        public readonly nuint Capacity = capacity;
        public readonly List<UiGpuVectorScene> Scenes = [];
        public nuint Written;
    }
    private readonly record struct CachedProjectedGlyph(ulong Address, LinkedListNode<ProjectedGlyphPage> Page);
    private readonly Dictionary<UiGpuVectorScene, CachedProjectedGlyph> _projectedGlyphBuffers = [];
    private readonly LinkedList<ProjectedGlyphPage> _projectedGlyphPages = [];
    private LinkedListNode<ProjectedGlyphPage>? _projectedGlyphWritePage;
    private nuint _projectedGlyphBytes;
    private Pipeline _projectedGlyphPipeline;
    private long _projectedGlyphDraws, _projectedGlyphUploads, _projectedGlyphEvictions;
    private long _projectedGlyphBatches, _projectedGlyphBatchedRuns;
    private long _projectedGlyphAcrossPlanes, _projectedGlyphAcrossPlaneRuns;
    private readonly bool _projectedGlyphsRequested = ParseBooleanEnvironmentFlag("DUXEL_VK_PROJECTED_GLYPHS");
    private readonly bool _projectedGlyphBatchingEnabled = !TryParseBooleanEnvironmentFlag("DUXEL_VK_PROJECTED_GLYPH_BATCHING", out var batching) || batching;
    private readonly bool _projectedGlyphPlaneBatchingEnabled = ParseBooleanEnvironmentFlag("DUXEL_VK_PROJECTED_GLYPH_PLANE_BATCHING");
    private bool SupportsProjectedGlyphSamples => SupportsProjectedGlyphSampleCount(_msaaSampleCount);
    private bool SupportsProjectedGlyphSampleCount(SampleCountFlags samples) => samples == SampleCountFlags.Count1Bit
        || samples == SampleCountFlags.Count4Bit && _standardSampleLocations;
    internal bool ProjectedGlyphTextEnabled => _projectedGlyphsRequested && SupportsProjectedGlyphSamples;
    internal bool CanUseProjectedGlyphText(int requestedSamples) => _projectedGlyphsRequested
        && SupportsProjectedGlyphSampleCount(ResolveMsaaSampleCount(NormalizeRequestedMsaaSamples(requestedSamples)));
    internal (long Draws, long Uploads, long Evictions, int Buffers, nuint Bytes) ProjectedGlyphDiagnostics
        => (_projectedGlyphDraws, _projectedGlyphUploads, _projectedGlyphEvictions, _projectedGlyphBuffers.Count, _projectedGlyphBytes);
    internal (bool Enabled, long Draws, long Runs) ProjectedGlyphBatchDiagnostics
        => (_projectedGlyphBatchingEnabled, _projectedGlyphBatches, _projectedGlyphBatchedRuns);
    internal (bool Enabled, long Draws, long Runs) ProjectedGlyphPlaneBatchDiagnostics
        => (_projectedGlyphPlaneBatchingEnabled, _projectedGlyphAcrossPlanes, _projectedGlyphAcrossPlaneRuns);

    private void CreateProjectedGlyphPipeline(ReadOnlySpan<byte> vertexCode, ReadOnlySpan<byte> fragmentCode, Pipeline* pipeline)
    {
        ShaderModule vertex = default, fragment = default;
        try
        {
            vertex = CreateShaderModule(vertexCode);
            fragment = CreateShaderModule(fragmentCode);
            CreateGraphicsPipeline(glyphStages: new(vertex, fragment), glyphPipeline: pipeline);
        }
        finally
        {
            if (fragment.Handle != 0)
                _vk.DestroyShaderModule(_device, fragment, null);
            if (vertex.Handle != 0)
                _vk.DestroyShaderModule(_device, vertex, null);
        }
    }

    private ProjectedGlyphPage AllocateProjectedGlyphPage(nuint bytes)
    {
        Buffer buffer = default;
        DeviceMemory memory = default;
        void* mapped = null;
        try
        {
            CreateBuffer(bytes, BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddressBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out buffer, out memory);
            Check(_vk.MapMemory(_device, memory, 0, bytes, 0, &mapped));
            var address = GetBufferDeviceAddress(buffer);
            _vk.GetBufferMemoryRequirements(_device, buffer, out var requirements);
            return new(new(buffer, memory, address, checked((nuint)requirements.Size)), mapped, bytes);
        }
        catch
        {
            if (mapped != null)
                _vk.UnmapMemory(_device, memory);
            DestroyBufferResource(new(buffer, memory));
            throw;
        }
    }

    private ProjectedGlyphBuffer UploadProjectedGlyphs(UiGpuVectorScene scene)
    {
        var page = AllocateProjectedGlyphPage(checked((nuint)scene.Data.Length * (nuint)sizeof(Vector4)));
        scene.Data.Span.CopyTo(new Span<Vector4>(page.Mapped, scene.Data.Length));
        _vk.UnmapMemory(_device, page.Buffer.Memory);
        return page.Buffer;
    }

    private ulong GetProjectedGlyphAddress(UiGpuVectorScene scene)
    {
        if (_projectedGlyphBuffers.TryGetValue(scene, out var cached))
        {
            if (_projectedGlyphPages.Last != cached.Page)
            {
                _projectedGlyphPages.Remove(cached.Page);
                _projectedGlyphPages.AddLast(cached.Page);
            }
            return cached.Address;
        }

        var bytes = checked((nuint)scene.Data.Length * (nuint)sizeof(Vector4));
        if (_projectedGlyphWritePage is not { } write || bytes > write.Value.Capacity - write.Value.Written)
        {
            var page = AllocateProjectedGlyphPage(Math.Max(bytes, ProjectedGlyphPageBytes));
            while (_projectedGlyphPages.First is { } first && _projectedGlyphBytes + page.Buffer.Bytes > ProjectedGlyphCacheBytes)
            {
                var removed = first.Value;
                foreach (var expired in removed.Scenes)
                    _projectedGlyphBuffers.Remove(expired);
                _projectedGlyphPages.RemoveFirst();
                _projectedGlyphBytes -= removed.Buffer.Bytes;
                _projectedGlyphEvictions += removed.Scenes.Count;
                _vk.UnmapMemory(_device, removed.Buffer.Memory);
                QueueBufferDestroy(removed.Buffer.Buffer, removed.Buffer.Memory);
            }
            _projectedGlyphWritePage = write = _projectedGlyphPages.AddLast(page);
            _projectedGlyphBytes += page.Buffer.Bytes;
        }
        var target = write.Value;
        scene.Data.Span.CopyTo(new Span<Vector4>(target.Mapped + target.Written, scene.Data.Length));
        var address = target.Buffer.Address + target.Written;
        target.Written += bytes;
        target.Scenes.Add(scene);
        _projectedGlyphBuffers.Add(scene, new(address, write));
        _projectedGlyphUploads++;
        return address;
    }

    private void DestroyProjectedGlyphBuffers()
    {
        foreach (var page in _projectedGlyphPages)
        {
            _vk.UnmapMemory(_device, page.Buffer.Memory);
            DestroyBufferResource(new(page.Buffer.Buffer, page.Buffer.Memory));
        }
        _projectedGlyphBuffers.Clear();
        _projectedGlyphPages.Clear();
        _projectedGlyphWritePage = null;
        _projectedGlyphBytes = 0;
    }

    private void RecordProjectedGlyph(in UiDrawCommand command, UiProjectedGlyphDraw glyph, in CommandFrameContext context,
        ref CommandRecordingState state)
    {
        if (command.ClipRect.Width <= 0f || command.ClipRect.Height <= 0f)
            return;
        if (!ProjectedGlyphTextEnabled || state.Pipeline.DepthMode != 2 || command.Opacity != 1f)
            throw new InvalidOperationException("Projected glyphs require an enabled opaque depth-writing plane.");
        EnsureProjectedGlyphPipeline();
        var address = GetProjectedGlyphAddress(glyph.Scene);
        RecordProjectedGlyphDraw(in command, glyph, _projectedGlyphPipeline, address, in context, ref state);
    }

    private void EnsureProjectedGlyphPipeline()
    {
        if (_projectedGlyphPipeline.Handle == 0)
            fixed (Pipeline* pipeline = &_projectedGlyphPipeline)
                CreateProjectedGlyphPipeline(
                    LoadEmbeddedShader("Duxel.Vulkan.Shaders.projected_glyph.vert.spv"),
                    LoadEmbeddedShader(ResolveBlendFragmentResource("Duxel.Vulkan.Shaders.projected_glyph.frag.spv")), pipeline);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ProjectedGlyphPlacement(float X, float Y, float Scale, uint Color);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ProjectedGlyphRun(ProjectedGlyphPlacement Placement, ulong Address, uint First, uint Reserved);

    private const int ProjectedGlyphPlaneRunLimit = 128;

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ProjectedGlyphPlaneRun(ProjectedGlyphRun Glyph, Vector4 Translate, UiRect Clip, Matrix4x4 Transform);

    private ProjectedGlyphPlaneRun[]? _projectedGlyphPlaneRuns;
    private UiRect[]? _projectedGlyphPlaneFootprints;

    private bool TryPrepareProjectedGlyphPlane(UiDrawList list, in LayerViewportTransform view,
        in CommandFrameContext context, ref CommandRecordingState state)
    {
        if (!ProjectedGlyphTextEnabled || list.Scene3D is not { WriteDepth: true } scene || _projectedPaintCapture)
            return false;
        var commands = list.Commands.AsSpan();
        if (commands.IsEmpty || commands[^1].ProjectedGlyphs is null)
            return false;
        var count = 0;
        foreach (ref readonly var command in commands)
        {
            if (command.ProjectedGlyphs is not null)
            {
                if (command.Opacity != 1f || ++count > ProjectedGlyphPlaneRunLimit)
                    return false;
            }
            else if (count != 0 || command.Callback is not null || command.UserData is UiGpuParticleFrame
                || command.IsBackdropComposite)
                return false;
        }
        if (!TryGetLayerBounds(list, out var bounds))
            return false;
        var transform = GetEffectiveLayerTransform(in view, in context);
        if (transform.M12 != 0 || transform.M21 != 0 || transform.M14 != 0 || transform.M24 != 0 || transform.M44 != 1)
            return false;

        var viewport = view.PixelViewport;
        var left = ToPixel(bounds.X, context.ScaleX, context.TranslateX, transform.M11, transform.M41, viewport.X, viewport.Width);
        var right = ToPixel(bounds.X + bounds.Width, context.ScaleX, context.TranslateX, transform.M11, transform.M41, viewport.X, viewport.Width);
        var top = ToPixel(bounds.Y, context.ScaleY, context.TranslateY, transform.M22, transform.M42, viewport.Y, viewport.Height);
        var bottom = ToPixel(bounds.Y + bounds.Height, context.ScaleY, context.TranslateY, transform.M22, transform.M42, viewport.Y, viewport.Height);
        if (!float.IsFinite(left) || !float.IsFinite(top) || !float.IsFinite(right) || !float.IsFinite(bottom))
            return false;
        // Include the rasterization fringe along the axis-aligned screen footprint.
        var footprint = new UiRect(MathF.Min(left, right) - 2, MathF.Min(top, bottom) - 2,
            MathF.Abs(right - left) + 4, MathF.Abs(bottom - top) + 4);
        var footprints = _projectedGlyphPlaneFootprints ??= new UiRect[ProjectedGlyphPlaneRunLimit];
        if (state.ProjectedGlyphPlaneFootprints != 0 &&
            (state.ProjectedGlyphPlaneRuns + count > ProjectedGlyphPlaneRunLimit
             || state.ProjectedGlyphPlaneFootprints == footprints.Length || state.ProjectedGlyphPlaneViewport != viewport
             || state.ProjectedGlyphPlaneClip != view.ScreenClip || state.ProjectedGlyphPlaneDepthScope != scene.DepthScopeId
             || state.ProjectedGlyphPlaneFractionalViewport != view.FractionalPixelViewport
             || OverlapsProjectedGlyphFootprints(footprint, footprints.AsSpan(0, state.ProjectedGlyphPlaneFootprints))))
            FlushProjectedGlyphPlanes(in context, ref state);
        state.ProjectedGlyphPlaneViewport = viewport;
        state.ProjectedGlyphPlaneClip = view.ScreenClip;
        state.ProjectedGlyphPlaneFractionalViewport = view.FractionalPixelViewport;
        state.ProjectedGlyphPlaneDepthScope = scene.DepthScopeId;
        footprints[state.ProjectedGlyphPlaneFootprints++] = footprint;
        return true;
    }

    private static float ToPixel(float point, float scale, float translate, float axis, float offset, float origin, float extent)
        => origin + (((point * scale + translate) * axis + offset) + 1) * (extent * .5f);

    private static bool OverlapsProjectedGlyphFootprints(UiRect bounds, ReadOnlySpan<UiRect> footprints)
    {
        foreach (ref readonly var previous in footprints)
            if (MathF.Min(bounds.X + bounds.Width, previous.X + previous.Width) > MathF.Max(bounds.X, previous.X)
                && MathF.Min(bounds.Y + bounds.Height, previous.Y + previous.Height) > MathF.Max(bounds.Y, previous.Y))
                return true;
        return false;
    }

    private void QueueProjectedGlyphPlane(in UiDrawCommand command, UiProjectedGlyphDraw glyph,
        in CommandFrameContext context, ref CommandRecordingState state)
    {
        var runs = _projectedGlyphPlaneRuns ??= new ProjectedGlyphPlaneRun[ProjectedGlyphPlaneRunLimit];
        var translate = new Vector4(context.TranslateX + command.Translation.X * context.ScaleX,
            context.TranslateY + command.Translation.Y * context.ScaleY, 0, 0);
        runs[state.ProjectedGlyphPlaneRuns++] = new(
            new(GetProjectedGlyphPlacement(glyph), GetProjectedGlyphAddress(glyph.Scene), state.ProjectedGlyphPlaneInstances, 0),
            translate, command.ClipRect, state.Pipeline.PushedLayerTransform);
        state.ProjectedGlyphPlaneInstances = checked(state.ProjectedGlyphPlaneInstances + (uint)glyph.Scene.CommandCount);
    }

    private void FlushProjectedGlyphPlanes(in CommandFrameContext context, ref CommandRecordingState state)
    {
        var count = state.ProjectedGlyphPlaneRuns;
        state.ProjectedGlyphPlaneFootprints = 0;
        if (count == 0)
            return;
        EnsureProjectedGlyphPipeline();
        var address = WriteProjectedGlyphBatch<ProjectedGlyphPlaneRun>(_projectedGlyphPlaneRuns.AsSpan(0, count), ref state);
        var placement = new ProjectedGlyphPlacement(BitConverter.UInt32BitsToSingle((uint)count), BitConverter.UInt32BitsToSingle(1), 0, 0);
        var command = new UiDrawCommand(state.ProjectedGlyphPlaneClip, default, 0, 0, 0);
        RecordProjectedGlyphDraw(in command, placement, state.ProjectedGlyphPlaneInstances, _projectedGlyphPipeline, address, in context, ref state);
        _projectedGlyphAcrossPlanes++;
        _projectedGlyphAcrossPlaneRuns += count;
        state.ProjectedGlyphPlaneRuns = 0;
        state.ProjectedGlyphPlaneInstances = 0;
    }

    private int RecordConsecutiveProjectedGlyphs(UiDrawList list, int start, in CommandFrameContext context,
        in CommandDrawListContext listContext, ref CommandRecordingState state)
    {
        const int MaxRunsPerDraw = 128;
        var commands = list.Commands.AsSpan();
        ref readonly var first = ref commands[start];
        var end = start + 1;
        for (; end < commands.Length && end - start < MaxRunsPerDraw; end++)
        {
            ref readonly var next = ref commands[end];
            if (next.ProjectedGlyphs is null || next.ClipRect != first.ClipRect
                || next.Translation != first.Translation || next.Opacity != first.Opacity)
                break;
        }
        if (end == start + 1 || first.ClipRect.Width <= 0f || first.ClipRect.Height <= 0f)
            return end;
        if (!ProjectedGlyphTextEnabled || state.Pipeline.DepthMode != 2 || first.Opacity != 1f)
            throw new InvalidOperationException("Projected glyphs require an enabled opaque depth-writing plane.");

        Span<ProjectedGlyphRun> runs = stackalloc ProjectedGlyphRun[end - start];
        uint instances = 0;
        var count = 0;
        foreach (ref readonly var command in commands[start..end])
        {
            if (IsOutsideFrameDamage(in command, in context, list.Scene3D)
                || !listContext.SkipLayerFrustumChecks && IsOutsideLayerFrustum(in command, in context, in state.Pipeline))
                continue;
            var glyph = command.ProjectedGlyphs!;
            runs[count++] = new(GetProjectedGlyphPlacement(glyph), GetProjectedGlyphAddress(glyph.Scene), instances, 0);
            instances = checked(instances + (uint)glyph.Scene.CommandCount);
        }
        if (count == 0)
            return end;
        EnsureProjectedGlyphPipeline();
        var address = WriteProjectedGlyphBatch<ProjectedGlyphRun>(runs[..count], ref state);
        var placement = new ProjectedGlyphPlacement(BitConverter.UInt32BitsToSingle((uint)count), 0, 0, 0);
        RecordProjectedGlyphDraw(in first, placement, instances, _projectedGlyphPipeline, address, in context, ref state);
        _projectedGlyphBatches++;
        _projectedGlyphBatchedRuns += count;
        return end;
    }

    private ulong WriteProjectedGlyphBatch<T>(ReadOnlySpan<T> runs, ref CommandRecordingState state) where T : unmanaged
    {
        const nuint PageBytes = 64 * 1024;
        var frame = _frames[_frameIndex % _frames.Length];
        var pages = frame.ProjectedGlyphBatchPages ??= [];
        var bytes = (nuint)(runs.Length * sizeof(T));
        if (state.ProjectedGlyphBatchOffset + bytes > PageBytes)
        {
            state.ProjectedGlyphBatchPage++;
            state.ProjectedGlyphBatchOffset = 0;
        }
        if (state.ProjectedGlyphBatchPage == pages.Count)
            pages.Add(AllocateProjectedGlyphPage(PageBytes));
        var page = pages[state.ProjectedGlyphBatchPage];
        var offset = state.ProjectedGlyphBatchOffset;
        runs.CopyTo(new Span<T>(page.Mapped + offset, runs.Length));
        state.ProjectedGlyphBatchOffset += bytes;
        return page.Buffer.Address + offset;
    }

    private void DestroyProjectedGlyphBatchPages(FrameResources frame)
    {
        if (frame.ProjectedGlyphBatchPages is not { } pages)
            return;
        foreach (var page in pages)
        {
            _vk.UnmapMemory(_device, page.Buffer.Memory);
            DestroyBufferResource(new(page.Buffer.Buffer, page.Buffer.Memory));
        }
        frame.ProjectedGlyphBatchPages = null;
    }

    private static ProjectedGlyphPlacement GetProjectedGlyphPlacement(UiProjectedGlyphDraw glyph)
        => new(glyph.Origin.X, glyph.Origin.Y, glyph.Scale, glyph.Color.Rgba);

    private void RecordProjectedGlyphDraw(in UiDrawCommand command, UiProjectedGlyphDraw glyph, Pipeline pipeline, ulong address,
        in CommandFrameContext context, ref CommandRecordingState state)
        => RecordProjectedGlyphDraw(in command, GetProjectedGlyphPlacement(glyph), (uint)glyph.Scene.CommandCount,
            pipeline, address, in context, ref state);

    private void RecordProjectedGlyphDraw(in UiDrawCommand command, ProjectedGlyphPlacement placement, uint instanceCount,
        Pipeline pipeline, ulong address, in CommandFrameContext context, ref CommandRecordingState state)
    {
        var screenCommand = command with { ClipRect = state.Pipeline.LayerScreenClip, Translation = default };
        if (!TryApplyScissorForCommand(in screenCommand, in context, ref state.Scissor))
            return;

        var cb = context.CommandBuffer;
        ApplyPaintPipeline(cb, false, false, ref state.Pipeline);
        _vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, pipeline);
        var scaleAndTranslate = new Vector4(context.ScaleX, context.ScaleY,
            context.TranslateX + command.Translation.X * context.ScaleX, context.TranslateY + command.Translation.Y * context.ScaleY);
        _vk.CmdPushConstants(cb, _pipelineLayout, ShaderStageFlags.VertexBit, 0, 16, &scaleAndTranslate);
        _vk.CmdPushConstants(cb, _pipelineLayout, ShaderStageFlags.VertexBit, 16, 16, &placement);
        _vk.CmdPushConstants(cb, _pipelineLayout, ShaderStageFlags.VertexBit, PrimitiveAddressPushOffset, 8, &address);
        var viewport = state.Pipeline.ViewportPixels;
        var transform = state.Pipeline.PushedLayerTransform;
        if (!_viewportClipPlanesEnabled || transform.M33 <= 0)
        {
            transform.M31 = viewport.X;
            transform.M32 = viewport.Y;
            transform.M33 = -viewport.Width;
            transform.M34 = viewport.Height;
        }
        var stages = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit;
        _vk.CmdPushConstants(cb, _pipelineLayout, stages, 48, 64, &transform);
        var clip = command.ClipRect;
        _vk.CmdPushConstants(cb, _pipelineLayout, ShaderStageFlags.FragmentBit, 112, 16, &clip);
        var drawStart = BeginFrameProfileTiming(context.ProfileEnabled);
        _vk.CmdDraw(cb, 6, instanceCount, 0, 0);
        RecordDrawCallProfile(context.ProfileEnabled, drawStart, ref state.DrawDispatch);
        _projectedGlyphDraws++;

        _vk.CmdBindPipeline(cb, PipelineBindPoint.Graphics, GetUnmaskedPipeline(state.Pipeline.DepthMode));
        transform = state.Pipeline.PushedLayerTransform;
        _vk.CmdPushConstants(cb, _pipelineLayout, stages, 48, 64, &transform);
        clip = state.Pipeline.LayerLocalClip;
        _vk.CmdPushConstants(cb, _pipelineLayout, ShaderStageFlags.FragmentBit, 112, 16, &clip);
        state.Pipeline.HasDrawMode = false;
        state.PushConstant.HasPushMode = false;
        state.BufferBinding.HasVertexAddress = false;
        state.BufferBinding.HasPrimitiveAddress = false;
    }
}
