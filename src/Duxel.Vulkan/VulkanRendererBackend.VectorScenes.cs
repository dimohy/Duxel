using System.Runtime.InteropServices;
using System.Numerics;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    [StructLayout(LayoutKind.Sequential)]
    private struct VectorScenePush
    {
        public ulong Scene, Pixels;
        public uint Width, Height, CommandCount;
        public float SourceWidth, SourceHeight;
        public ulong Dependencies;
        public uint TileOffset, TileColumns, TileRows;
        public uint Reserved;
        public Vector4 Camera;
        public ulong Strokes;
        public uint Background;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct VectorSceneDependency
    {
        public ulong Pixels;
        public uint Width, Height;
        public Vector4 Bounds;
    }
    private readonly record struct VectorBuffer(VkBuffer Buffer, DeviceMemory Memory, ulong Address,
        nuint Bytes, bool Upload, bool Reusable);
    private sealed class VectorSceneResource
    {
        internal VectorBuffer Data, Upload;
        internal nuint Bytes;
        internal int References;
        internal UiGpuVectorScene[] Dependencies = [];
        internal int DependencyReferences;
        internal Vector4? BlurPaintBounds;
    }
    private sealed class VectorSceneOutput(UiGpuVectorScene scene, int width, int height, VectorBuffer pixels)
    {
        internal readonly UiGpuVectorScene Scene = scene;
        internal readonly int Width = width, Height = height;
        internal VectorBuffer Pixels = pixels;
        internal VectorSceneOutput?[] Dependencies = [];
        internal VectorBuffer DependencyData, DependencyUpload, BlurA, BlurB;
        internal VectorBuffer[] StrokeBuffers = [];
        internal VectorStrokeTarget[] StrokeTargets = [];
        internal VectorBuffer StrokeData, StrokeUpload;
        internal bool HasDashedStrokes;
        internal UiGpuVectorFilter? Filter;
        internal UiGpuVectorCamera? Camera;
        internal UiColor Background;
        internal bool Initialized;
    }
    private readonly Dictionary<UiGpuVectorScene, VectorSceneResource> _vectorScenes = [];
    private readonly Dictionary<UiTextureId, VectorSceneOutput> _vectorSceneOutputs = [];
    private readonly HashSet<UiTextureId> _pendingVectorSceneOutputs = [];
    private readonly Dictionary<int, Pipeline> _vectorScenePipelines = [];
    private PipelineLayout _vectorSceneLayout;
    private long _vectorSceneUploads, _vectorSceneDispatches;
    internal (long Uploads, long Dispatches, int Scenes, int Outputs) VectorSceneDiagnostics
        => (_vectorSceneUploads, _vectorSceneDispatches, _vectorScenes.Count, _vectorSceneOutputs.Count);

    private VectorBuffer AllocateVectorBuffer(nuint bytes, bool upload = false, bool reusable = true)
    {
        if (reusable && TryRentVectorWork(bytes, upload, out var cached))
            return cached;
        CreateBuffer(bytes, upload ? BufferUsageFlags.TransferSrcBit : BufferUsageFlags.StorageBuffer
            | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit,
            upload ? MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit : MemoryPropertyFlags.DeviceLocalBit,
            out var buffer, out var memory);
        if (reusable)
            _vectorWorkAllocations++;
        return new(buffer, memory, upload ? 0 : GetBufferDeviceAddress(buffer), bytes, upload, reusable);
    }

    private void RegisterVectorScene(UiTextureUpdate update)
    {
        var scene = update.VectorScene ?? throw new ArgumentException("Vector rendering requires a retained scene.");
        update.VectorCamera?.Validate();
        if (update.Format != UiTextureFormat.Rgba8Unorm || !update.RgbaPixels.IsEmpty || update.X != 0 || update.Y != 0
            || update.RegionWidth != 0 || update.RegionHeight != 0 || update.Width <= 0 || update.Height <= 0
            || (long)update.Width * update.Height > 16 * 1024 * 1024)
            throw new ArgumentException("Vector rendering requires a bounded output extent and no CPU pixels.");
        if (_vectorSceneOutputs.TryGetValue(update.TextureId, out var previous)
            && ReferenceEquals(previous.Scene, scene) && previous.Width == update.Width && previous.Height == update.Height
            && previous.Camera == update.VectorCamera && previous.Background == update.VectorBackground)
            return;
        if (previous is null && _textures.ContainsKey(update.TextureId))
            throw new ArgumentException("Vector outputs require an unoccupied texture identity.");
        var workStart = BeginFrameProfileTiming(_profilingEnabled);
        var output = CreateVectorSceneOutput(scene, update.Width, update.Height, camera: update.VectorCamera, background: update.VectorBackground);
        _profileVectorWorkTicks += EndFrameProfileTiming(_profilingEnabled, workStart);
        Image image = default;
        DeviceMemory memory = default;
        ImageView view = default;
        TextureResource? replacement = null;
        var attached = false;
        try
        {
            var imageStart = BeginFrameProfileTiming(_profilingEnabled);
            var usage = ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit;
            SelectValidationVectorImageUsage(ref usage);
            var allocationBytes = CreateImage(update.Width, update.Height, Format.R8G8B8A8Unorm, ImageTiling.Optimal,
            usage, MemoryPropertyFlags.DeviceLocalBit,
            out image, out memory);
            view = CreateImageView(image, Format.R8G8B8A8Unorm);
            _profileVectorImageTicks += EndFrameProfileTiming(_profilingEnabled, imageStart);
            var registrationStart = BeginFrameProfileTiming(_profilingEnabled);
            var slot = AllocateBindlessTextureSlot(view, false);
            replacement = new(image, memory, view, slot, update.Width, update.Height, Format.R8G8B8A8Unorm,
                allocationBytes: allocationBytes);
            // The replacement owns its complete scene tree and target before the old output retires.
            if (previous is not null)
                DestroyTexture(update.TextureId);
            _textures.Add(update.TextureId, replacement.Value);
            attached = true;
            _vectorSceneOutputs.Add(update.TextureId, output);
            _pendingVectorSceneOutputs.Add(update.TextureId);
            _colorImageTextures.Add(update.TextureId);
            _paintTextureRevisions[update.TextureId] = ++_paintTextureRevision;
            _profileVectorRegistrationTicks += EndFrameProfileTiming(_profilingEnabled, registrationStart);
            if (_profilingEnabled)
                _profileVectorOutputCount++;
        }
        catch
        {
            if (attached)
            {
                _textures.Remove(update.TextureId);
                _vectorSceneOutputs.Remove(update.TextureId);
                _pendingVectorSceneOutputs.Remove(update.TextureId);
                _colorImageTextures.Remove(update.TextureId);
                _paintTextureRevisions.Remove(update.TextureId);
            }
            ReleaseVectorSceneOutput(output, false);
            if (replacement is { } created)
                DestroyTextureResource(created);
            else
            {
                if (view.Handle != 0)
                    _vk.DestroyImageView(_device, view, null);
                if (image.Handle != 0)
                    _vk.DestroyImage(_device, image, null);
                if (memory.Handle != 0)
                    _vk.FreeMemory(_device, memory, null);
            }
            throw;
        }
    }

    partial void SelectValidationVectorImageUsage(ref ImageUsageFlags usage);

    private static Vector2 VectorSceneScale(UiGpuVectorScene scene, int width, int height, UiGpuVectorCamera? camera)
    {
        var full = camera?.FullPixelSize ?? new UiVector2(width, height);
        var scale = new Vector2(full.X / scene.Size.X, full.Y / scene.Size.Y);
        return camera is { Stretch: true } ? scale : new(MathF.Min(scale.X, scale.Y));
    }
    private static UiRect VectorSceneView(UiGpuVectorScene scene, int width, int height, UiGpuVectorCamera? camera)
    {
        var full = camera?.FullPixelSize ?? new UiVector2(width, height);
        var origin = camera?.PixelOrigin ?? default;
        var scale = VectorSceneScale(scene, width, height, camera);
        return new((origin.X - (full.X - scene.Size.X * scale.X) * .5f) / scale.X,
            (origin.Y - (full.Y - scene.Size.Y * scale.Y) * .5f) / scale.Y, width / scale.X, height / scale.Y);
    }
    private VectorSceneOutput CreateVectorSceneOutput(UiGpuVectorScene scene, int width, int height,
        UiGpuVectorFilter? filter = null, UiGpuVectorCamera? camera = null, UiColor background = default)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(width), "GPU vector filter extent exceeds the surface budget.");
        var scale = VectorSceneScale(scene, width, height, camera);
        var viewRect = VectorSceneView(scene, width, height, camera);
        if (filter is not null)
        {
            var maximum = MathF.Max((new Vector2(filter.Horizontal.X, filter.Horizontal.Y) * scale).Length(),
                (new Vector2(filter.Vertical.X, filter.Vertical.Y) * scale).Length());
            if (!float.IsFinite(maximum) || MathF.Ceiling(maximum * 3) > 2048)
                throw new ArgumentOutOfRangeException(nameof(filter), "GPU vector blur exceeds the shader kernel capacity.");
            EnsureVectorBlurPipeline();
        }
        var preparePipelinesConcurrently = scene.Strokes.Count != 0 && !_vectorScenePipelines.ContainsKey(scene.StackCapacity);
        if (!preparePipelinesConcurrently)
            EnsureVectorScenePipeline(scene.StackCapacity);
        AcquireVectorSceneResource(scene);
        var output = new VectorSceneOutput(scene, width, height, default) { Filter = filter, Camera = camera, Background = background };
        try
        {
            output.Pixels = AllocateVectorBuffer(checked((nuint)width * (nuint)height * 4));
            if (preparePipelinesConcurrently)
            {
                // The pipelines own separate layouts and share an internally synchronized Vulkan cache.
                Parallel.Invoke(
                    () => EnsureVectorScenePipeline(scene.StackCapacity),
                    () => PrepareVectorStrokes(output));
            }
            else
                PrepareVectorStrokes(output);
            if (filter is not null)
            {
                output.BlurA = AllocateVectorBuffer(checked((nuint)width * (nuint)height * 16));
                output.BlurB = AllocateVectorBuffer(checked((nuint)width * (nuint)height * 16));
            }
            output.Dependencies = new VectorSceneOutput?[scene.Filters.Count + scene.Patterns.Count];
            var descriptors = new VectorSceneDependency[output.Dependencies.Length];
            for (var i = 0; i < scene.Filters.Count; i++)
            {
                var dependency = scene.Filters[i];
                var left = MathF.Max(viewRect.X, dependency.Bounds.X);
                var top = MathF.Max(viewRect.Y, dependency.Bounds.Y);
                var right = MathF.Min(viewRect.X + viewRect.Width, dependency.Bounds.X + dependency.Bounds.Width);
                var bottom = MathF.Min(viewRect.Y + viewRect.Height, dependency.Bounds.Y + dependency.Bounds.Height);
                if (right <= left || bottom <= top)
                    continue;
                // Filter surfaces share the parent's device grid. A fractional filter region
                // must not rescale/recenter its source before convolution.
                var full = camera?.FullPixelSize ?? new UiVector2(width, height);
                var haloX = 3 * (MathF.Abs(dependency.Horizontal.X) + MathF.Abs(dependency.Vertical.X)) + 4 / scale.X;
                var haloY = 3 * (MathF.Abs(dependency.Horizontal.Y) + MathF.Abs(dependency.Vertical.Y)) + 4 / scale.Y;
                var letterX = (full.X - scene.Size.X * scale.X) * .5f;
                var letterY = (full.Y - scene.Size.Y * scale.Y) * .5f;
                var firstX = checked((int)MathF.Floor(MathF.Max(left - haloX, dependency.Bounds.X) * scale.X + letterX));
                var firstY = checked((int)MathF.Floor(MathF.Max(top - haloY, dependency.Bounds.Y) * scale.Y + letterY));
                var lastX = checked((int)MathF.Ceiling(MathF.Min(right + haloX, dependency.Bounds.X + dependency.Bounds.Width) * scale.X + letterX));
                var lastY = checked((int)MathF.Ceiling(MathF.Min(bottom + haloY, dependency.Bounds.Y + dependency.Bounds.Height) * scale.Y + letterY));
                UiGpuVectorCamera? childCamera = new(full, new(firstX, firstY), camera?.Stretch ?? false);
                var child = CreateVectorSceneOutput(dependency.Scene, lastX - firstX, lastY - firstY, dependency, childCamera);
                output.Dependencies[i] = child;
                var childView = VectorSceneView(child.Scene, child.Width, child.Height, childCamera);
                descriptors[i] = new()
                {
                    Pixels = child.Pixels.Address,
                    Width = (uint)child.Width,
                    Height = (uint)child.Height,
                    Bounds = new(childView.X, childView.Y, childView.Width, childView.Height)
                };
            }
            for (var i = 0; i < scene.Patterns.Count; i++)
            {
                var pattern = scene.Patterns[i];
                var mapping = pattern.Mapping;
                var patternWidth = Math.Max(1, checked((int)MathF.Ceiling(pattern.Scene.Size.X *
                    (new Vector2(mapping.M11, mapping.M12) * scale).Length())));
                var patternHeight = Math.Max(1, checked((int)MathF.Ceiling(pattern.Scene.Size.Y *
                    (new Vector2(mapping.M21, mapping.M22) * scale).Length())));
                var child = CreateVectorSceneOutput(pattern.Scene, patternWidth, patternHeight,
                    camera: new(new(patternWidth, patternHeight), default, Stretch: true));
                var index = scene.Filters.Count + i;
                output.Dependencies[index] = child;
                descriptors[index] = new()
                {
                    Pixels = child.Pixels.Address,
                    Width = (uint)child.Width,
                    Height = (uint)child.Height
                };
            }
            if (descriptors.Length != 0)
            {
                var bytes = checked((nuint)descriptors.Length * (nuint)sizeof(VectorSceneDependency));
                output.DependencyData = AllocateVectorBuffer(bytes);
                output.DependencyUpload = AllocateVectorBuffer(bytes, true);
                void* mapped;
                Check(_vk.MapMemory(_device, output.DependencyUpload.Memory, 0, bytes, 0, &mapped));
                try
                {
                    MemoryMarshal.AsBytes(descriptors.AsSpan()).CopyTo(new Span<byte>(mapped, checked((int)bytes)));
                }
                finally { _vk.UnmapMemory(_device, output.DependencyUpload.Memory); }
            }
            return output;
        }
        catch
        {
            // Release only the unfinished tree; shared immutable scene references remain owned by other outputs.
            ReleaseVectorSceneOutput(output, false);
            throw;
        }
    }

    private VectorSceneResource AcquireVectorSceneResource(UiGpuVectorScene scene)
    {
        if (_vectorScenes.TryGetValue(scene, out var existing))
        {
            existing.References++;
            return existing;
        }
        var bytes = (nuint)Math.Max(16, checked(scene.Data.Length * 16));
        var resource = new VectorSceneResource
        {
            Bytes = bytes,
            References = 1,
            Dependencies = scene.Filters.Select(filter => filter.Scene).Concat(scene.Patterns.Select(pattern => pattern.Scene)).ToArray()
        };
        _vectorScenes.Add(scene, resource);
        try
        {
            resource.Data = AllocateVectorBuffer(bytes, reusable: false);
            resource.Upload = AllocateVectorBuffer(bytes, true, reusable: false);
            void* mapped;
            Check(_vk.MapMemory(_device, resource.Upload.Memory, 0, bytes, 0, &mapped));
            try
            {
                MemoryMarshal.AsBytes(scene.Data.Span).CopyTo(new Span<byte>(mapped, checked((int)bytes)));
            }
            finally { _vk.UnmapMemory(_device, resource.Upload.Memory); }
            foreach (var child in resource.Dependencies)
            {
                AcquireVectorSceneResource(child);
                resource.DependencyReferences++;
            }
            return resource;
        }
        catch
        {
            ReleaseVectorSceneResource(scene, false);
            throw;
        }
    }

    private void EnsureVectorScenePipeline(int stackCapacity)
    {
        if (_vectorScenePipelines.ContainsKey(stackCapacity))
            return;
        if (sizeof(Vector4) != 16 || sizeof(VectorSceneDependency) != 32
            || Marshal.OffsetOf<VectorScenePush>(nameof(VectorScenePush.Camera)).ToInt32() != 64
            || Marshal.OffsetOf<VectorScenePush>(nameof(VectorScenePush.Strokes)).ToInt32() != 80)
            throw new InvalidOperationException("GPU_VECTOR_SCENE_ABI_MISMATCH");
        if (_vectorSceneLayout.Handle == 0)
        {
            uint queueCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueCount, null);
            var queues = new QueueFamilyProperties[queueCount];
            fixed (QueueFamilyProperties* pointer = queues)
                _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueCount, pointer);
            if ((queues[_graphicsQueueFamily].QueueFlags & QueueFlags.ComputeBit) == 0)
                throw new NotSupportedException("GPU vector rendering requires compute support on the graphics queue.");
            var range = new PushConstantRange { StageFlags = ShaderStageFlags.ComputeBit, Size = (uint)sizeof(VectorScenePush) };
            var info = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, PushConstantRangeCount = 1, PPushConstantRanges = &range };
            PipelineLayout layout;
            Check(_vk.CreatePipelineLayout(_device, &info, null, &layout));
            _vectorSceneLayout = layout;
        }
        var shaderBytes = LoadEmbeddedShader("Duxel.Vulkan.Shaders.vector_scene.comp.spv");
        VectorSceneShaderContract.Validate(shaderBytes);
        var shader = CreateShaderModule(shaderBytes);
        var entry = VulkanMarshaling.StringToPtr("main");
        try
        {
            var capacity = (uint)stackCapacity;
            var map = new SpecializationMapEntry { ConstantID = 0, Size = sizeof(uint) };
            var specialization = new SpecializationInfo { MapEntryCount = 1, PMapEntries = &map, DataSize = sizeof(uint), PData = &capacity };
            var pipelineInfo = new ComputePipelineCreateInfo
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Layout = _vectorSceneLayout,
                BasePipelineIndex = -1,
                Stage = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = shader, PName = entry, PSpecializationInfo = &specialization }
            };
            Pipeline pipeline;
            Check(_vk.CreateComputePipelines(_device, _pipelineCache, 1, &pipelineInfo, null, &pipeline));
            _vectorScenePipelines.Add(stackCapacity, pipeline);
        }
        finally { VulkanMarshaling.Free((nint)entry); _vk.DestroyShaderModule(_device, shader, null); }
    }

    private VectorProfileCounts RecordVectorScenes(CommandBuffer command, QueryPool timestampQueryPool)
    {
        VectorProfileCounts counts = default;
        if (timestampQueryPool.Handle != 0)
            _vk.CmdWriteTimestamp(command, PipelineStageFlags.BottomOfPipeBit, timestampQueryPool, GpuProfileVectorStartQuery);
        foreach (var id in _pendingVectorSceneOutputs)
        {
            var output = _vectorSceneOutputs[id];
            RecordVectorSceneOutput(command, output, timestampQueryPool, ref counts);
            VectorSceneBarrier(command, PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
                PipelineStageFlags.TransferBit, AccessFlags.TransferReadBit);
            var texture = _textures[id];
            var toTransfer = PaintGroupBarrier(texture.Image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal,
                0, AccessFlags.TransferWriteBit);
            _vk.CmdPipelineBarrier(command, PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &toTransfer);
            var region = new BufferImageCopy
            {
                ImageSubresource = new() { AspectMask = ImageAspectFlags.ColorBit, LayerCount = 1 },
                ImageExtent = new((uint)output.Width, (uint)output.Height, 1)
            };
            var copyQuery = BeginVectorProfileStage(command, timestampQueryPool, GpuProfileCopyStartQuery,
                GpuProfileMaxVectorStageSamples, ref counts.Copies);
            _vk.CmdCopyBufferToImage(command, output.Pixels.Buffer, texture.Image, ImageLayout.TransferDstOptimal, 1, &region);
            EndVectorProfileStage(command, timestampQueryPool, copyQuery);
            var toRead = PaintGroupBarrier(texture.Image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
                AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit);
            _vk.CmdPipelineBarrier(command, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit,
                0, 0, null, 0, null, 1, &toRead);
            RetireVectorSceneWork(output);
        }
        _pendingVectorSceneOutputs.Clear();
        if (timestampQueryPool.Handle != 0)
            _vk.CmdWriteTimestamp(command, PipelineStageFlags.BottomOfPipeBit, timestampQueryPool, GpuProfileVectorEndQuery);
        return counts;
    }

    private void RecordVectorSceneOutput(CommandBuffer command, VectorSceneOutput output,
        QueryPool timestampQueryPool, ref VectorProfileCounts counts)
    {
        if (output.Initialized)
            return;
        foreach (var dependency in output.Dependencies)
            if (dependency is not null)
                RecordVectorSceneOutput(command, dependency, timestampQueryPool, ref counts);
        var resource = _vectorScenes[output.Scene];
        if (resource.Upload.Buffer.Handle != 0)
        {
            var copy = new BufferCopy { Size = resource.Bytes };
            var copyQuery = BeginVectorProfileStage(command, timestampQueryPool, GpuProfileCopyStartQuery,
                GpuProfileMaxVectorStageSamples, ref counts.Copies);
            _vk.CmdCopyBuffer(command, resource.Upload.Buffer, resource.Data.Buffer, 1, &copy);
            EndVectorProfileStage(command, timestampQueryPool, copyQuery);
            RetireVectorBuffer(resource.Upload);
            resource.Upload = default;
            _vectorSceneUploads++;
        }
        if (output.DependencyUpload.Buffer.Handle != 0)
        {
            var copy = new BufferCopy { Size = checked((nuint)output.Dependencies.Length * (nuint)sizeof(VectorSceneDependency)) };
            var copyQuery = BeginVectorProfileStage(command, timestampQueryPool, GpuProfileCopyStartQuery,
                GpuProfileMaxVectorStageSamples, ref counts.Copies);
            _vk.CmdCopyBuffer(command, output.DependencyUpload.Buffer, output.DependencyData.Buffer, 1, &copy);
            EndVectorProfileStage(command, timestampQueryPool, copyQuery);
            RetireVectorBuffer(output.DependencyUpload);
            output.DependencyUpload = default;
        }
        VectorSceneBarrier(command, PipelineStageFlags.TransferBit | PipelineStageFlags.ComputeShaderBit,
            AccessFlags.TransferWriteBit | AccessFlags.ShaderWriteBit, PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit);
        RecordVectorStrokes(command, output, resource.Data.Address);
        _vk.CmdBindPipeline(command, PipelineBindPoint.Compute, _vectorScenePipelines[output.Scene.StackCapacity]);
        var push = new VectorScenePush
        {
            Scene = resource.Data.Address,
            Pixels = output.Pixels.Address,
            Dependencies = output.DependencyData.Address,
            Strokes = output.StrokeData.Address,
            Background = output.Background.Rgba,
            Width = (uint)output.Width,
            Height = (uint)output.Height,
            CommandCount = (uint)output.Scene.CommandCount,
            SourceWidth = output.Scene.Size.X,
            SourceHeight = output.Scene.Size.Y
            ,
            TileOffset = (uint)output.Scene.TileOffset,
            TileColumns = (uint)output.Scene.TileColumns,
            TileRows = (uint)output.Scene.TileRows,
            Reserved = (output.Filter is null ? 0u : 1u) | (output.Camera is { Stretch: true } ? 2u : 0u),
            Camera = output.Camera is { } camera ? new(camera.FullPixelSize.X, camera.FullPixelSize.Y, camera.PixelOrigin.X, camera.PixelOrigin.Y) : default
        };
        _vk.CmdPushConstants(command, _vectorSceneLayout, ShaderStageFlags.ComputeBit, 0, (uint)sizeof(VectorScenePush), &push);
        var pathQuery = BeginVectorProfileStage(command, timestampQueryPool, GpuProfilePathStartQuery,
            GpuProfileMaxVectorStageSamples, ref counts.Paths);
        _vk.CmdDispatch(command, ((uint)output.Width + 7) / 8, ((uint)output.Height + 7) / 8, 1);
        EndVectorProfileStage(command, timestampQueryPool, pathQuery);
        _vectorSceneDispatches++;
        if (output.Filter is not null)
        {
            var blurStartQuery = BeginVectorProfileStage(command, timestampQueryPool, GpuProfileBlurStartQuery,
                GpuProfileMaxBlurFilters, ref counts.Blurs);
            RecordVectorBlur(command, output);
            EndVectorProfileStage(command, timestampQueryPool, blurStartQuery);
        }
        output.Initialized = true;
    }

    private uint BeginVectorProfileStage(CommandBuffer command, QueryPool pool, uint firstQuery, uint limit, ref uint count)
    {
        if (pool.Handle == 0)
            return 0;
        var sample = count++;
        if (sample >= limit)
            return 0;
        var query = firstQuery + 2 * sample;
        _vk.CmdWriteTimestamp(command, PipelineStageFlags.BottomOfPipeBit, pool, query);
        return query;
    }

    private void EndVectorProfileStage(CommandBuffer command, QueryPool pool, uint query)
    {
        if (query != 0)
            _vk.CmdWriteTimestamp(command, PipelineStageFlags.BottomOfPipeBit, pool, query + 1);
    }

    private void VectorSceneBarrier(CommandBuffer command, PipelineStageFlags from, AccessFlags source,
        PipelineStageFlags to, AccessFlags destination)
    {
        var barrier = new MemoryBarrier { SType = StructureType.MemoryBarrier, SrcAccessMask = source, DstAccessMask = destination };
        _vk.CmdPipelineBarrier(command, from, to, 0, 1, &barrier, 0, null, 0, null);
    }
    private void RetireVectorBuffer(VectorBuffer buffer)
    {
        if (buffer.Buffer.Handle != 0)
        {
            if (buffer.Reusable)
                QueueVectorWorkReuse(buffer);
            else
                QueueBufferDestroy(buffer.Buffer, buffer.Memory);
        }
    }
    private void RetireVectorSceneWork(VectorSceneOutput output)
    {
        foreach (var child in output.Dependencies)
            if (child is not null)
                RetireVectorSceneWork(child);
        RetireVectorBuffer(output.Pixels);
        RetireVectorBuffer(output.DependencyData);
        RetireVectorBuffer(output.DependencyUpload);
        RetireVectorBuffer(output.BlurA);
        RetireVectorBuffer(output.BlurB);
        foreach (var buffer in output.StrokeBuffers)
            RetireVectorBuffer(buffer);
        RetireVectorBuffer(output.StrokeData);
        RetireVectorBuffer(output.StrokeUpload);
        output.StrokeBuffers = [];
        output.StrokeData = output.StrokeUpload = default;
        output.Pixels = output.DependencyData = output.DependencyUpload = output.BlurA = output.BlurB = default;
    }
    private void ForgetVectorScene(UiTextureId id)
    {
        if (!_vectorSceneOutputs.Remove(id, out var output))
            return;
        _pendingVectorSceneOutputs.Remove(id);
        ReleaseVectorSceneOutput(output, false);
    }
    private void ReleaseVectorSceneOutput(VectorSceneOutput output, bool immediate)
    {
        foreach (var dependency in output.Dependencies)
            if (dependency is not null)
                ReleaseVectorSceneOutput(dependency, immediate);
        void Release(VectorBuffer buffer)
        {
            if (buffer.Buffer.Handle == 0)
                return;
            if (immediate)
                DestroyBufferResource(new(buffer.Buffer, buffer.Memory));
            else
                RetireVectorBuffer(buffer);
        }
        Release(output.Pixels);
        Release(output.DependencyData);
        Release(output.DependencyUpload);
        Release(output.BlurA);
        Release(output.BlurB);
        foreach (var buffer in output.StrokeBuffers)
            Release(buffer);
        Release(output.StrokeData);
        Release(output.StrokeUpload);
        ReleaseVectorSceneResource(output.Scene, immediate);
    }
    private void ReleaseVectorSceneResource(UiGpuVectorScene scene, bool immediate)
    {
        var resource = _vectorScenes[scene];
        if (--resource.References != 0)
            return;
        foreach (var buffer in new[] { resource.Data, resource.Upload })
            if (buffer.Buffer.Handle != 0)
            {
                if (immediate)
                    DestroyBufferResource(new(buffer.Buffer, buffer.Memory));
                else
                    RetireVectorBuffer(buffer);
            }
        _vectorScenes.Remove(scene);
        for (var i = 0; i < resource.DependencyReferences; i++)
            ReleaseVectorSceneResource(resource.Dependencies[i], immediate);
    }
    private void DestroyVectorScenes()
    {
        foreach (var output in _vectorSceneOutputs.Values)
            ReleaseVectorSceneOutput(output, true);
        _vectorSceneOutputs.Clear();
        _vectorScenes.Clear();
        _pendingVectorSceneOutputs.Clear();
        DestroyVectorWorkCache();
        foreach (var pipeline in _vectorScenePipelines.Values)
            _vk.DestroyPipeline(_device, pipeline, null);
        _vectorScenePipelines.Clear();
        if (_vectorSceneLayout.Handle != 0)
            _vk.DestroyPipelineLayout(_device, _vectorSceneLayout, null);
        DestroyVectorBlurPipeline();
        DestroyVectorStrokePipeline();
    }
}
