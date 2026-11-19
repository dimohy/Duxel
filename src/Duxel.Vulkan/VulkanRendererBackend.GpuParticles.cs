using System.Runtime.InteropServices;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ParticlePush
    {
        public ulong Source, Destination, Heads, Next, Vertices;
        public float X, Y, Width, Height;
        public uint Count, MaximumSides;
        public float Delta, Radius, Speed, MaximumAngular;
        public int Sides;
        public uint GridWidth, GridHeight, Mode;
        public float CellSize;
    }
    private readonly record struct ParticleBuffer(VkBuffer Buffer, DeviceMemory Memory, ulong Address);
    private sealed class ParticleResources
    {
        public ParticleBuffer A, B, Heads, Next, Vertices, Indices, Initialization;
        public bool Initialized;
        public nuint InitializationBytes, InitializationStateBytes, InitializationIndexBytes, InitializationOffset;
        public ParticleBuffer PreservedSource;
        public nuint PreservedBytes;
        public UiGpuParticleSnapshot Snapshot = null!;
        public int Capacity;
        public bool CurrentIsA = true;
        public bool Emitted;
        public UiRect LastBounds;
        public float LastRadius;
        public int LastSides;
        public uint GridCapacity;
        public int Count, MaximumSides;
        public ParticleBuffer Current => CurrentIsA ? A : B;
    }
    private readonly Dictionary<UiGpuParticleSystem, ParticleResources> _particleResources = new();
    private readonly Dictionary<UiGpuParticleSystem, UiGpuParticleFrame> _particleFrames = new();
    private readonly List<UiGpuParticleSystem> _retiredParticleSystems = [];
    private Pipeline _particlePipeline;
    private PipelineLayout _particleLayout;
    private int _particleDispatches, _particleCount;
    private ulong _particleStateUploadBytes;

    private void PrepareGpuParticleResources(UiDrawData data)
    {
        _particleFrames.Clear();
        _particleDispatches = _particleCount = 0;
        _particleStateUploadBytes = 0;
        var hasParticles = (data.GetCommandFeatures() & UiDrawCommandFeatures.GpuParticles) != 0;
        if (!hasParticles && _particleResources.Count == 0) return;
        foreach (var list in data.DrawLists)
        {
        if ((list.GetCommandFeatures() & UiDrawCommandFeatures.GpuParticles) == 0) continue;
        foreach (var command in list.Commands.AsSpan())
        {
            if (command.UserData is not UiGpuParticleFrame frame) continue;
            frame.Validate(allowDisposedSystem: true);
            if (_particleFrames.TryGetValue(frame.System, out var existing))
            {
                if (existing != frame) throw new InvalidOperationException("GPU_PARTICLE_CONFLICTING_FRAME: all views must submit the same simulation step.");
                continue;
            }
            _particleFrames.Add(frame.System, frame);
            EnsureParticlePipeline();
            if (!_particleResources.TryGetValue(frame.System, out var resources))
            {
                resources = CreateParticleResources(frame.Snapshot);
                _particleResources.Add(frame.System, resources);
            }
            if (!ReferenceEquals(resources.Snapshot, frame.Snapshot))
            {
                var preserved = frame.Snapshot.GetPreservedPrefixCount(resources.Snapshot);
                if (frame.Snapshot.Count > resources.Capacity)
                {
                    var replacement = CreateParticleResources(frame.Snapshot, preserved);
                    replacement.PreservedSource = resources.Current;
                    replacement.PreservedBytes = checked((nuint)preserved * 48);
                    RetireParticleResources(resources);
                    resources = replacement;
                    _particleResources[frame.System] = resources;
                }
                else
                {
                    PrepareParticleUpload(resources, frame.Snapshot, preserved, includeIndices: false);
                    resources.Snapshot = frame.Snapshot;
                    resources.Count = frame.Snapshot.Count;
                    resources.Emitted = false;
                }
            }
            _particleCount += resources.Count;
            var cellSize = frame.Radius * frame.Snapshot.MaximumSizeScale * 2;
            var width = checked((uint)MathF.Max(1, MathF.Ceiling(frame.Bounds.Width / cellSize)));
            var height = checked((uint)MathF.Max(1, MathF.Ceiling(frame.Bounds.Height / cellSize)));
            var cells = checked(width * height);
            if (cells > 1_048_576) throw new ArgumentOutOfRangeException(nameof(frame), "GPU particle grid cell budget exceeded.");
            if (resources.GridCapacity < cells)
            {
                RetireParticleBuffer(resources.Heads);
                resources.Heads = AllocateParticleBuffer(checked((nuint)cells * 4));
                resources.GridCapacity = cells;
            }
        }
        }
        _retiredParticleSystems.Clear();
        foreach (var pair in _particleResources)
            if (pair.Key.IsDisposed && !_particleFrames.ContainsKey(pair.Key)) _retiredParticleSystems.Add(pair.Key);
        foreach (var system in _retiredParticleSystems)
        {
            var resources = _particleResources[system];
            RetireParticleResources(resources);
            _particleResources.Remove(system);
        }
    }

    private void EnsureParticlePipeline()
    {
        if (_particlePipeline.Handle != 0) return;
        if (Marshal.SizeOf<UiGpuParticle>() != 48 || Marshal.OffsetOf<ParticlePush>(nameof(ParticlePush.CellSize)).ToInt32() != 96)
            throw new InvalidOperationException("GPU_PARTICLE_LAYOUT_MISMATCH");
        uint queueCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueCount, null);
        var queues = new QueueFamilyProperties[queueCount];
        fixed (QueueFamilyProperties* queuePointer = queues)
            _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueCount, queuePointer);
        if ((queues[_graphicsQueueFamily].QueueFlags & QueueFlags.ComputeBit) == 0)
            throw new NotSupportedException("GPU particles require compute support on the graphics queue.");
        var range = new PushConstantRange { StageFlags = ShaderStageFlags.ComputeBit, Offset = 0, Size = (uint)sizeof(ParticlePush) };
        var layoutInfo = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, PushConstantRangeCount = 1, PPushConstantRanges = &range };
        PipelineLayout layout;
        Check(_vk.CreatePipelineLayout(_device, &layoutInfo, null, &layout));
        _particleLayout = layout;
        var shader = CreateShaderModule(LoadParticleShader());
        var entry = VulkanMarshaling.StringToPtr("main");
        try
        {
            var info = new ComputePipelineCreateInfo
            {
                SType = StructureType.ComputePipelineCreateInfo, Layout = layout, BasePipelineIndex = -1,
                Stage = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = shader, PName = entry },
            };
            Pipeline pipeline;
            Check(_vk.CreateComputePipelines(_device, _pipelineCache, 1, &info, null, &pipeline));
            _particlePipeline = pipeline;
        }
        finally { VulkanMarshaling.Free((nint)entry); _vk.DestroyShaderModule(_device, shader, null); }
    }

    private ParticleBuffer AllocateParticleBuffer(nuint bytes, BufferUsageFlags extra = 0)
    {
        CreateBuffer(bytes, BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddressBit
            | BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit | extra,
            MemoryPropertyFlags.DeviceLocalBit, out var buffer, out var memory);
        return new(buffer, memory, GetBufferDeviceAddress(buffer));
    }
    private ParticleResources CreateParticleResources(UiGpuParticleSnapshot snapshot, int preserved = 0)
    {
        var capacity = 1;
        while (capacity < snapshot.Count) capacity = checked(capacity * 2);
        var stateBytes = checked((nuint)capacity * 48);
        var vertexBytes = checked(capacity * snapshot.MaximumSides * 20);
        var indexBytes = checked(capacity * (snapshot.MaximumSides - 2) * 3 * 4);
        var resources = new ParticleResources
        {
            A = AllocateParticleBuffer(stateBytes), B = AllocateParticleBuffer(stateBytes),
            Next = AllocateParticleBuffer(checked((nuint)capacity * 4)),
            Vertices = AllocateParticleBuffer((nuint)vertexBytes),
            Indices = AllocateParticleBuffer((nuint)indexBytes, BufferUsageFlags.IndexBufferBit),
            Count = snapshot.Count, MaximumSides = snapshot.MaximumSides,
            Capacity = capacity, Snapshot = snapshot,
        };
        PrepareParticleUpload(resources, snapshot, preserved, includeIndices: true);
        return resources;
    }

    private void PrepareParticleUpload(ParticleResources resources, UiGpuParticleSnapshot snapshot,
        int preserved, bool includeIndices)
    {
        resources.InitializationOffset = checked((nuint)preserved * 48);
        resources.InitializationStateBytes = checked((nuint)(snapshot.Count - preserved) * 48);
        var indices = includeIndices ? new uint[checked(resources.Capacity * (resources.MaximumSides - 2) * 3)] : [];
        for (var body = 0; includeIndices && body < resources.Capacity; body++)
        for (var triangle = 0; triangle < resources.MaximumSides - 2; triangle++)
        {
            var index = (body * (resources.MaximumSides - 2) + triangle) * 3;
            var vertex = (uint)(body * resources.MaximumSides);
            indices[index] = vertex; indices[index + 1] = vertex + (uint)triangle + 1; indices[index + 2] = vertex + (uint)triangle + 2;
        }
        resources.InitializationIndexBytes = checked((nuint)indices.Length * 4);
        resources.InitializationBytes = checked(resources.InitializationStateBytes + resources.InitializationIndexBytes);
        if (resources.InitializationBytes == 0) return;
        CreateBuffer(resources.InitializationBytes, BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, out var buffer, out var memory);
        resources.Initialization = new(buffer, memory, 0);
        resources.Initialized = false;
        void* mapped;
        Check(_vk.MapMemory(_device, memory, 0, resources.InitializationBytes, 0, &mapped));
        try
        {
            MemoryMarshal.AsBytes(snapshot.Initial.Span[preserved..]).CopyTo(new Span<byte>(mapped, checked((int)resources.InitializationStateBytes)));
            MemoryMarshal.AsBytes(indices.AsSpan()).CopyTo(new Span<byte>((byte*)mapped + resources.InitializationStateBytes, indices.Length * 4));
        }
        finally { _vk.UnmapMemory(_device, memory); }
    }

    private void RetireParticleResources(ParticleResources resources)
    {
        RetireParticleBuffer(resources.A); RetireParticleBuffer(resources.B);
        RetireParticleBuffer(resources.Heads); RetireParticleBuffer(resources.Next);
        RetireParticleBuffer(resources.Vertices); RetireParticleBuffer(resources.Indices);
        RetireParticleBuffer(resources.Initialization);
    }

    private void RecordGpuParticleSteps(CommandBuffer command)
    {
        if (_particleFrames.Count == 0) return;
        // A previous frame may still be reading generated vertices or simulation state.
        ParticleBarrier(command, PipelineStageFlags.VertexShaderBit | PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.TransferBit,
            AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit | AccessFlags.TransferReadBit | AccessFlags.TransferWriteBit,
            PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.TransferBit,
            AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit | AccessFlags.TransferReadBit | AccessFlags.TransferWriteBit);
        _vk.CmdBindPipeline(command, PipelineBindPoint.Compute, _particlePipeline);
        foreach (var pair in _particleFrames)
        {
            var frame = pair.Value; var resources = _particleResources[pair.Key];
            if (!resources.Initialized)
            {
                if (resources.PreservedBytes != 0)
                {
                    var preserve = new BufferCopy { Size = resources.PreservedBytes };
                    _vk.CmdCopyBuffer(command, resources.PreservedSource.Buffer, resources.Current.Buffer, 1, &preserve);
                }
                if (resources.InitializationStateBytes != 0)
                {
                    var stateCopy = new BufferCopy { DstOffset = resources.InitializationOffset, Size = resources.InitializationStateBytes };
                    _vk.CmdCopyBuffer(command, resources.Initialization.Buffer, resources.Current.Buffer, 1, &stateCopy);
                }
                if (resources.InitializationIndexBytes != 0)
                {
                    var indexCopy = new BufferCopy { SrcOffset = resources.InitializationStateBytes, Size = resources.InitializationIndexBytes };
                    _vk.CmdCopyBuffer(command, resources.Initialization.Buffer, resources.Indices.Buffer, 1, &indexCopy);
                }
                ParticleBarrier(command, PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit,
                    PipelineStageFlags.ComputeShaderBit | PipelineStageFlags.VertexInputBit,
                    AccessFlags.ShaderReadBit | AccessFlags.IndexReadBit);
                _particleStateUploadBytes += (ulong)resources.InitializationBytes;
                RetireParticleBuffer(resources.Initialization);
                resources.Initialization = default;
                resources.Initialized = true;
                resources.PreservedSource = default; resources.PreservedBytes = 0;
            }
            var push = new ParticlePush
            {
                Source = resources.Current.Address, Destination = (resources.CurrentIsA ? resources.B : resources.A).Address,
                Heads = resources.Heads.Address, Next = resources.Next.Address, Vertices = resources.Vertices.Address,
                X = frame.Bounds.X, Y = frame.Bounds.Y, Width = frame.Bounds.Width, Height = frame.Bounds.Height,
                Count = (uint)resources.Count, MaximumSides = (uint)resources.MaximumSides,
                Delta = frame.DeltaSeconds, Radius = frame.Radius, Speed = frame.Speed, MaximumAngular = frame.MaximumAngularSpeed,
                Sides = frame.Sides, CellSize = frame.Radius * frame.Snapshot.MaximumSizeScale * 2,
            };
            push.GridWidth = (uint)MathF.Max(1, MathF.Ceiling(frame.Bounds.Width / push.CellSize));
            push.GridHeight = (uint)MathF.Max(1, MathF.Ceiling(frame.Bounds.Height / push.CellSize));
            if (frame.DeltaSeconds > 0)
            {
                DispatchParticles(command, ref push, 0, push.Count);
                resources.CurrentIsA = !resources.CurrentIsA;
                for (var iteration = 0; iteration < frame.CollisionIterations; iteration++)
                {
                    (push.Source, push.Destination) = (push.Destination, push.Source);
                    DispatchParticles(command, ref push, 1, push.GridWidth * push.GridHeight);
                    DispatchParticles(command, ref push, 2, push.Count);
                    DispatchParticles(command, ref push, 3, push.Count);
                    resources.CurrentIsA = !resources.CurrentIsA;
                }
                resources.Emitted = false;
            }
            if (!resources.Emitted || resources.LastBounds != frame.Bounds || resources.LastRadius != frame.Radius || resources.LastSides != frame.Sides)
            {
                push.Source = resources.Current.Address;
                DispatchParticles(command, ref push, 4, push.Count);
                resources.Emitted = true; resources.LastBounds = frame.Bounds; resources.LastRadius = frame.Radius; resources.LastSides = frame.Sides;
            }
        }
        ParticleBarrier(command, PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.VertexShaderBit, AccessFlags.ShaderReadBit);
    }
    private static byte[] LoadParticleShader()
    {
        var shader=LoadEmbeddedShader("Duxel.Vulkan.Shaders.particles.comp.spv");
        OverrideValidationParticleShader(ref shader);
        return shader;
    }
    static partial void OverrideValidationParticleShader(ref byte[] shader);

    private void DispatchParticles(CommandBuffer command, ref ParticlePush push, uint mode, uint elements)
    {
        push.Mode = mode;
        fixed (ParticlePush* pointer = &push)
            _vk.CmdPushConstants(command, _particleLayout, ShaderStageFlags.ComputeBit, 0, (uint)sizeof(ParticlePush), pointer);
        _vk.CmdDispatch(command, (elements + 127) / 128, 1, 1);
        _particleDispatches++;
        ParticleBarrier(command, PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);
    }
    private void ParticleBarrier(CommandBuffer command, PipelineStageFlags sourceStage, AccessFlags sourceAccess,
        PipelineStageFlags destinationStage, AccessFlags destinationAccess)
    {
        var barrier = new MemoryBarrier { SType = StructureType.MemoryBarrier, SrcAccessMask = sourceAccess, DstAccessMask = destinationAccess };
        _vk.CmdPipelineBarrier(command, sourceStage, destinationStage, 0, 1, &barrier, 0, null, 0, null);
    }
    private void RetireParticleBuffer(ParticleBuffer buffer)
    {
        if (buffer.Buffer.Handle != 0) QueueBufferDestroy(buffer.Buffer, buffer.Memory);
    }
    private void DestroyGpuParticleResources()
    {
        foreach (var resources in _particleResources.Values)
        foreach (var buffer in new[] { resources.A, resources.B, resources.Heads, resources.Next, resources.Vertices, resources.Indices, resources.Initialization })
            if (buffer.Buffer.Handle != 0) DestroyBufferResource(new(buffer.Buffer, buffer.Memory));
        _particleResources.Clear(); _particleFrames.Clear();
        if (_particlePipeline.Handle != 0) _vk.DestroyPipeline(_device, _particlePipeline, null);
        if (_particleLayout.Handle != 0) _vk.DestroyPipelineLayout(_device, _particleLayout, null);
        _particlePipeline = default; _particleLayout = default;
    }
}
