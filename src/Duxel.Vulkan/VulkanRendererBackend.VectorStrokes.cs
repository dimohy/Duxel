using System.Numerics;
using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    [StructLayout(LayoutKind.Sequential)]
    private struct VectorStrokeTarget
    {
        internal ulong Edges;
        internal uint Capacity, Count;
        internal uint Offset, Reserved0, Reserved1, Reserved2;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct VectorStrokePush
    {
        internal ulong Scene, Targets;
        internal Vector2 Scale, Origin;
        internal uint Count, First;
    }
    private Pipeline _vectorStrokePipeline;
    private Pipeline _vectorSolidStrokePipeline;
    private PipelineLayout _vectorStrokeLayout;

    private void PrepareVectorStrokes(VectorSceneOutput output)
    {
        var programs = output.Scene.Strokes;
        if (programs.Count == 0)
            return;
        var scale = VectorSceneScale(output.Scene, output.Width, output.Height, output.Camera);
        var view = VectorSceneView(output.Scene, output.Width, output.Height, output.Camera);
        var maximumScale = MathF.Max(scale.X, scale.Y);
        var targets = new VectorStrokeTarget[programs.Count];
        long totalBytes = 0;
        for (var i = 0; i < programs.Count; i++)
        {
            var program = programs[i];
            var bounds = program.Bounds;
            if (bounds.X > view.X + view.Width + 1 / scale.X || bounds.Y > view.Y + view.Height + 1 / scale.Y
                || bounds.X + bounds.Width < view.X - 1 / scale.X || bounds.Y + bounds.Height < view.Y - 1 / scale.Y)
                continue;
            var positional = Math.Ceiling(Math.Sqrt(3d * program.MaximumDifference * maximumScale));
            var deviceRadius = (double)program.MaximumRadius * maximumScale;
            var angle = deviceRadius == 0 ? Math.PI : 2 * Math.Asin(Math.Sqrt(Math.Min(.25 / deviceRadius, 2) / 2));
            var angular = Math.Ceiling(Math.PI / angle);
            var capacity = checked((uint)(6 * (Math.Max(1, positional) + Math.Max(1, angular) + 3) * program.WorkFactor));
            targets[i] = new()
            {
                Count = (uint)program.PrimitiveCount,
                Capacity = capacity,
                Offset = (uint)program.Offset
            };
            output.HasDashedStrokes |= program.HasDashes;
            totalBytes = checked(totalBytes + (long)program.PrimitiveCount * (capacity + 65) * 16);
            if (totalBytes > 64 * 1024 * 1024)
                throw new InvalidOperationException("GPU stroke geometry exceeds the per-output 64 MiB work budget.");
        }
        if (totalBytes > 0)
        {
            EnsureVectorStrokePipeline(output.HasDashedStrokes);
            var buffer = AllocateVectorBuffer((nuint)totalBytes);
            output.StrokeBuffers = [buffer];
            ulong offset = 0;
            for (var i = 0; i < programs.Count; i++)
            {
                if (targets[i].Count == 0)
                    continue;
                targets[i].Edges = buffer.Address + offset;
                offset += (ulong)targets[i].Count * (targets[i].Capacity + 65) * 16;
            }
        }
        output.StrokeTargets = targets;
        var bytes = checked((nuint)targets.Length * (nuint)sizeof(VectorStrokeTarget));
        output.StrokeData = AllocateVectorBuffer(bytes);
        output.StrokeUpload = AllocateVectorBuffer(bytes, true);
        void* mapped;
        Check(_vk.MapMemory(_device, output.StrokeUpload.Memory, 0, bytes, 0, &mapped));
        try
        {
            MemoryMarshal.AsBytes(targets.AsSpan()).CopyTo(new Span<byte>(mapped, (int)bytes));
        }
        finally { _vk.UnmapMemory(_device, output.StrokeUpload.Memory); }
    }

    private void RecordVectorStrokes(CommandBuffer command, VectorSceneOutput output, ulong source)
    {
        if (output.StrokeTargets.Length == 0)
            return;
        var copy = new BufferCopy { Size = checked((nuint)output.StrokeTargets.Length * (nuint)sizeof(VectorStrokeTarget)) };
        _vk.CmdCopyBuffer(command, output.StrokeUpload.Buffer, output.StrokeData.Buffer, 1, &copy);
        RetireVectorBuffer(output.StrokeUpload);
        output.StrokeUpload = default;
        VectorSceneBarrier(command, PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit,
            PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit);
        var maximum = output.StrokeTargets.Max(target => target.Count);
        var scale = VectorSceneScale(output.Scene, output.Width, output.Height, output.Camera);
        var extent = output.Camera?.FullPixelSize ?? new Core.UiVector2(output.Width, output.Height);
        var origin = new Vector2((extent.X - output.Scene.Size.X * scale.X) * .5f,
            (extent.Y - output.Scene.Size.Y * scale.Y) * .5f);
        if (maximum != 0)
        {
            _vk.CmdBindPipeline(command, PipelineBindPoint.Compute,
                output.HasDashedStrokes ? _vectorStrokePipeline : _vectorSolidStrokePipeline);
            for (uint first = 0; first < output.StrokeTargets.Length; first += 65_535)
            {
                var push = new VectorStrokePush
                {
                    Scene = source,
                    Targets = output.StrokeData.Address,
                    Scale = scale,
                    Origin = origin,
                    Count = (uint)output.StrokeTargets.Length,
                    First = first
                };
                _vk.CmdPushConstants(command, _vectorStrokeLayout, ShaderStageFlags.ComputeBit, 0, (uint)sizeof(VectorStrokePush), &push);
                _vk.CmdDispatch(command, (maximum + 63) / 64, Math.Min(65_535u, push.Count - first), 1);
            }
        }
        VectorSceneBarrier(command, PipelineStageFlags.TransferBit | PipelineStageFlags.ComputeShaderBit,
            AccessFlags.TransferWriteBit | AccessFlags.ShaderWriteBit, PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit);
    }

    private void EnsureVectorStrokePipeline(bool hasDashes)
    {
        ref var selected = ref (hasDashes ? ref _vectorStrokePipeline : ref _vectorSolidStrokePipeline);
        if (selected.Handle != 0)
            return;
        if (sizeof(VectorStrokeTarget) != 32 || sizeof(VectorStrokePush) != 40)
            throw new InvalidOperationException("GPU_VECTOR_STROKE_ABI_MISMATCH");
        var shaderBytes = LoadEmbeddedShader(hasDashes
            ? "Duxel.Vulkan.Shaders.vector_stroke.comp.spv"
            : "Duxel.Vulkan.Shaders.vector_stroke.solid.comp.spv");
        VectorSceneShaderContract.Validate(shaderBytes);
        if (_vectorStrokeLayout.Handle == 0)
        {
            var range = new PushConstantRange { StageFlags = ShaderStageFlags.ComputeBit, Size = (uint)sizeof(VectorStrokePush) };
            var info = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &range
            };
            PipelineLayout layout;
            Check(_vk.CreatePipelineLayout(_device, &info, null, &layout));
            _vectorStrokeLayout = layout;
        }
        var shader = CreateShaderModule(shaderBytes);
        var entry = VulkanMarshaling.StringToPtr("main");
        try
        {
            var pipelineInfo = new ComputePipelineCreateInfo
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Layout = _vectorStrokeLayout,
                BasePipelineIndex = -1,
                Stage = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = shader, PName = entry }
            };
            Pipeline pipeline;
            Check(_vk.CreateComputePipelines(_device, _pipelineCache, 1, &pipelineInfo, null, &pipeline));
            selected = pipeline;
        }
        finally { VulkanMarshaling.Free((nint)entry); _vk.DestroyShaderModule(_device, shader, null); }
    }
    private void DestroyVectorStrokePipeline()
    {
        if (_vectorStrokePipeline.Handle != 0)
            _vk.DestroyPipeline(_device, _vectorStrokePipeline, null);
        if (_vectorSolidStrokePipeline.Handle != 0)
            _vk.DestroyPipeline(_device, _vectorSolidStrokePipeline, null);
        if (_vectorStrokeLayout.Handle != 0)
            _vk.DestroyPipelineLayout(_device, _vectorStrokeLayout, null);
        _vectorStrokePipeline = default;
        _vectorSolidStrokePipeline = default;
        _vectorStrokeLayout = default;
    }
}
