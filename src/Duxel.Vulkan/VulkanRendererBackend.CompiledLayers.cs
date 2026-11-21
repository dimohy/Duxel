using System.Numerics;
using System.Runtime.InteropServices;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    // One real indexed draw can contain different transforms, clips and primitive
    // kinds. The index high byte selects a descriptor, retaining vertex reuse.
    private const int CompiledDescriptorLimit = 256;
    private readonly int _compiledIndexLimit = ParseCompiledIndexLimit();
    private const ulong CompiledCacheByteLimit = 512UL * 1024 * 1024;
    private readonly bool _compiledLayersEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_COMPILED_LAYERS") != "0";
    private readonly bool _sharedLayerBasisEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_SHARED_LAYER_BASIS") != "0";
    private readonly bool _sharedLayerCameraEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_SHARED_LAYER_CAMERA") != "0";
    private readonly bool _compiledDescriptorReuseEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_DESCRIPTOR_REUSE") != "0";
    private readonly bool _compiledCommandSpansEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_COMMAND_SPANS") != "0";

    private static int ParseCompiledIndexLimit()
    {
        var text = Environment.GetEnvironmentVariable("DUXEL_VK_COMPILED_INDEX_LIMIT");
        if (string.IsNullOrWhiteSpace(text))
            return 2 * 1024 * 1024;
        if (int.TryParse(text, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            && value is 1048576 or 2097152 or 4194304 or 8388608)
            return value;
        throw new ArgumentException("DUXEL_VK_COMPILED_INDEX_LIMIT must be 1048576, 2097152, 4194304 or 8388608.");
    }
    private Pipeline _compiledDepthReadPipeline, _compiledDepthWritePipeline;
    private Pipeline _sharedBasisDepthReadPipeline, _sharedBasisDepthWritePipeline;
    private Pipeline _sharedCameraDepthReadPipeline, _sharedCameraDepthWritePipeline;
    private readonly Dictionary<ulong, CompiledLayerGroup> _compiledLayerGroups = [];
    private readonly Dictionary<int, CompiledFrameSpan> _compiledFrameGroups = [];
    private readonly List<CompiledSourceSlice> _compiledSlices = [];
    private readonly HashSet<string> _compiledSourceAnchors = new(StringComparer.Ordinal);
    private CompiledPlanEntry[] _compiledPlan = [];
    private ulong _compiledCacheBytes;
    private ulong _frameCompiledIndexUploadBytes, _frameLayerStateUploadBytes;
    private int _frameCompiledDraws, _frameCompiledCommands, _frameSharedBasisDraws;
    private int _frameSharedCameraDraws;
    private int _frameCompiledDescriptorBuilds;
    private int _frameCompiledCommandVisits;

    private readonly record struct CompiledCommandSpan(int Count, UiDrawCommandKind FirstKind,
        UiDrawCommandKind LastKind, int TriangleToPrimitive, int PrimitiveToTriangle, int RectCircle);

    private readonly record struct CompiledPlanEntry(ulong Commands, string? Tag,
        ulong IndexHash, int VertexCount, UiRect? Viewport, int Scope, bool WriteDepth, bool Is3D);

    private static CompiledPlanEntry GetCompiledPlanEntry(UiDrawList list, int index, StaticGeometryFrameBindings bindings)
    {
        var scene = list.Scene3D;
        if (!scene.HasValue || !bindings.TryGetValue(index, out var binding))
            return default;
        return new(binding.CommandTopologyHash, binding.Tag, binding.IndexTopologyHash, list.Vertices.Count,
            scene?.Viewport, scene?.DepthScopeId ?? 0, scene?.WriteDepth ?? false, scene.HasValue);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompiledLayerDescriptor
    {
        public ulong VertexAddress, PrimitiveAddress;
        public uint PrimitiveOffset, PrimitiveVertexCount, DrawMode, MatrixIndex;
        public float Opacity, TranslateX, TranslateY;
        public int CircleCutoutOffset;
        public UiRect Clip;
    }

    private sealed class CompiledDescriptorFrame
    {
        public VkBuffer Buffer;
        public StaticMemorySlice MemorySlice;
        public ulong Address;
        public ulong MatrixAddress;
        public void* Mapped;
        public CompiledLayerDescriptor[] Values = [];
        public Matrix4x4[] Matrices = [];
        public byte[] MatrixModes = [];
        public Vector4[] PlaneModels = [];
        public StaticGeometryBuffer?[] BindingSources = [];
        public ulong[] CommandStates = [];
        public Vector4 DescriptorContext;
        public bool HasDescriptorContext;
    }

    private sealed class CompiledLayerGroup
    {
        public required string[] Tags;
        public required ulong[] Stamps;
        public required int[] CommandCounts;
        public required int[] CommandStarts;
        public required CompiledCommandSpan[] CommandSpans;
        public required uint[] IndexStarts;
        public required CompiledDescriptorFrame?[] Frames;
        public VkBuffer IndexBuffer;
        public StaticMemorySlice IndexMemorySlice;
        public ulong Bytes;
        public int LastUsedFrame;
        public bool HasCircleCutout;
        public bool HasBackdropComposite;
        public int MemberCount => Tags.Length;
    }

    private readonly record struct CompiledSourceSlice(int ListIndex, int CommandStart, int CommandCount);
    private readonly record struct CompiledPlacement(CompiledLayerGroup Group, int FirstList);
    private sealed class CompiledFrameSpan(int listCount)
    {
        public int ListCount { get; } = listCount;
        public readonly List<CompiledPlacement> Groups = [];
    }

    private void PrepareCompiledLayerGroups(UiDrawData data, StaticGeometryFrameBindings bindings)
    {
        _frameCompiledIndexUploadBytes = _frameLayerStateUploadBytes = 0;
        _frameCompiledDraws = _frameCompiledCommands = _frameSharedBasisDraws = 0;
        _frameSharedCameraDraws = 0;
        _frameCompiledDescriptorBuilds = _frameCompiledCommandVisits = 0;
        var repairsColor = !_damageEmpty || _repaintDiagnosticRedraw;
        PrepareLayerVisibilityFrame(repairsColor && _frameUsesDepthLayers ? data.DrawLists.Count : 0);
        if (!repairsColor)
            return;
        if (!_compiledLayersEnabled || !_frameUsesDepthLayers)
        {
            _compiledFrameGroups.Clear();
            _compiledPlan = [];
            if (_frameIndex % 60 == 0)
                TrimCompiledLayerCache(0);
            return;
        }
        var samePlan = _compiledPlan.Length == data.DrawLists.Count;
        for (var i = 0; samePlan && i < _compiledPlan.Length; i++)
            samePlan = _compiledPlan[i] == GetCompiledPlanEntry(data.DrawLists[i], i, bindings);
        if (samePlan)
        {
            foreach (var span in _compiledFrameGroups.Values)
                foreach (var placement in span.Groups)
                    placement.Group.LastUsedFrame = _frameIndex;
            if (_frameIndex % 60 == 0)
                TrimCompiledLayerCache(0);
            return;
        }
        // Preserve previous group starts when an unrelated preceding list changes.
        // Otherwise one changed connection can shift every subsequent descriptor
        // and force hundreds of megabytes of unchanged mesh indices to be rebuilt.
        _compiledSourceAnchors.Clear();
        foreach (var span in _compiledFrameGroups.Values)
            foreach (var placement in span.Groups)
                if (placement.Group.CommandStarts[0] == 0)
                    _compiledSourceAnchors.Add(placement.Group.Tags[0]);
        _compiledFrameGroups.Clear();
        if (_compiledDepthReadPipeline.Handle == 0)
            CreateGraphicsPipeline(createDepthVariants: true, compiledLayers: true);
        if (_sharedLayerBasisEnabled && _sharedBasisDepthReadPipeline.Handle == 0)
            CreateGraphicsPipeline(createDepthVariants: true, compiledLayers: true, sharedBasis: true);
        if (_sharedLayerCameraEnabled && _sharedCameraDepthReadPipeline.Handle == 0)
            CreateGraphicsPipeline(createDepthVariants: true, compiledLayers: true, sharedCamera: true);

        for (var start = 0; start < data.DrawLists.Count;)
        {
            if (!TryGetCompiledListShape(data.DrawLists[start], start, bindings, out var material, out _))
            {
                start++;
                continue;
            }
            var scene = data.DrawLists[start].Scene3D!.Value;
            var hasMesh = data.DrawLists[start].Vertices.Count != 0;
            var end = start + 1;
            for (; end < data.DrawLists.Count; end++)
            {
                var list = data.DrawLists[end];
                if (!TryGetCompiledListShape(list, end, bindings, out var nextMaterial, out _)
                    || nextMaterial != material || list.Scene3D!.Value.Viewport != scene.Viewport
                    || (list.Vertices.Count != 0) != hasMesh
                    || list.Scene3D.Value.DepthScopeId != scene.DepthScopeId || list.Scene3D.Value.WriteDepth != scene.WriteDepth)
                    break;
            }
            var span = new CompiledFrameSpan(end - start);
            var sourceList = start;
            var sourceCommand = 0;
            var fitsBudget = true;
            while (sourceList < end)
            {
                var descriptorCount = 0;
                var indexCount = 0;
                var hash = 14695981039346656037UL;
                _compiledSlices.Clear();
                while (sourceList < end && descriptorCount < CompiledDescriptorLimit)
                {
                    var list = data.DrawLists[sourceList];
                    var firstCommand = sourceCommand;
                    if (sourceCommand == 0 && descriptorCount > 0)
                    {
                        if (_compiledSourceAnchors.Contains(bindings[sourceList].Tag))
                            break;
                        // Keep a bounded source list whole so its successor's
                        // identity is a stable cache boundary, not a remainder.
                        if (list.Commands.Count <= CompiledDescriptorLimit)
                        {
                            var sourceIndices = 0;
                            foreach (ref readonly var command in list.Commands.AsSpan())
                                sourceIndices += GetCompiledCommandIndexCount(in command);
                            if (sourceIndices <= _compiledIndexLimit && (descriptorCount + list.Commands.Count > CompiledDescriptorLimit
                                || indexCount + sourceIndices > _compiledIndexLimit))
                                break;
                        }
                        else
                            break;
                    }
                    while (sourceCommand < list.Commands.Count && descriptorCount < CompiledDescriptorLimit)
                    {
                        var command = list.Commands[sourceCommand];
                        var count = GetCompiledCommandIndexCount(in command);
                        if (indexCount + count > _compiledIndexLimit)
                            break;
                        hash = (hash ^ command.IndexOffset) * 1099511628211UL;
                        hash = (hash ^ command.ElementCount) * 1099511628211UL;
                        hash = (hash ^ command.VertexOffset) * 1099511628211UL;
                        hash = (hash ^ (uint)command.Kind) * 1099511628211UL;
                        hash = (hash ^ (command.CircleCutoutIndexPlusOne != 0 ? 1u : 0u)) * 1099511628211UL;
                        hash = (hash ^ (command.IsBackdropComposite ? 1u : 0u)) * 1099511628211UL;
                        sourceCommand++;
                        descriptorCount++;
                        indexCount += count;
                    }
                    if (sourceCommand > firstCommand)
                    {
                        var binding = bindings[sourceList];
                        hash = (hash ^ (uint)StringComparer.Ordinal.GetHashCode(binding.Tag)) * 1099511628211UL;
                        hash = (hash ^ binding.IndexTopologyHash) * 1099511628211UL;
                        hash = (hash ^ (uint)list.Vertices.Count) * 1099511628211UL;
                        hash = (hash ^ (uint)firstCommand) * 1099511628211UL;
                        hash = (hash ^ (uint)(sourceCommand - firstCommand)) * 1099511628211UL;
                        _compiledSlices.Add(new(sourceList, firstCommand, sourceCommand - firstCommand));
                    }
                    if (sourceCommand < list.Commands.Count)
                        break;
                    sourceList++;
                    sourceCommand = 0;
                }
                if (indexCount == 0)
                    continue;
                if (_compiledLayerGroups.TryGetValue(hash, out var group))
                {
                    if (group.MemberCount != _compiledSlices.Count)
                        throw new InvalidOperationException("Compiled layer cache key collision.");
                    for (var i = 0; i < _compiledSlices.Count; i++)
                    {
                        var slice = _compiledSlices[i];
                        var binding = bindings[slice.ListIndex];
                        if (group.Tags[i] != binding.Tag || group.Stamps[i] != binding.IndexTopologyHash
                            || group.CommandStarts[i] != slice.CommandStart || group.CommandCounts[i] != slice.CommandCount)
                            throw new InvalidOperationException("Compiled layer cache key collision.");
                    }
                }
                else
                {
                    TrimCompiledLayerCache((ulong)indexCount * sizeof(uint));
                    if (_compiledLayerGroups.Count >= 256 || _compiledCacheBytes + (ulong)indexCount * sizeof(uint) > CompiledCacheByteLimit)
                    {
                        fitsBudget = false;
                        break;
                    }
                    group = CreateCompiledLayerGroup(data, _compiledSlices, descriptorCount, indexCount, bindings);
                    _compiledLayerGroups.Add(hash, group);
                    _compiledCacheBytes += group.Bytes;
                }
                group.LastUsedFrame = _frameIndex;
                span.Groups.Add(new(group, _compiledSlices[0].ListIndex));
            }
            // Batching owns a bounded resident working set. Lists outside that
            // working set keep their established direct submission contract.
            if (fitsBudget)
                _compiledFrameGroups.Add(start, span);
            start = end;
        }
        if (_frameIndex % 60 == 0)
            TrimCompiledLayerCache(0);
        if (_compiledPlan.Length != data.DrawLists.Count)
            _compiledPlan = new CompiledPlanEntry[data.DrawLists.Count];
        for (var i = 0; i < _compiledPlan.Length; i++)
            _compiledPlan[i] = GetCompiledPlanEntry(data.DrawLists[i], i, bindings);
    }

    private bool TryGetCompiledListShape(UiDrawList list, int listIndex,
        StaticGeometryFrameBindings bindings, out (UiTextureId Texture, bool Font, UiTextureId? WhiteBackdrop) material, out int indexCount)
    {
        material = default;
        indexCount = 0;
        // Mutable frame geometry and expanded legacy primitives have separate
        // submission contracts. This compiler owns resident analytic geometry.
        if (list.Scene3D is null || list.Commands.Count == 0 || list.Vertices.Count > 65536
            || IsScreenAlignedScene(list.Scene3D.Value)
            || !bindings.TryGetValue(listIndex, out var binding) || binding.HasExpandedPrimitiveGeometry)
            return false;
        var hasMaterial = false;
        foreach (ref readonly var command in list.Commands.AsSpan())
        {
            if (command.Callback is not null || command.UserData is UiGpuParticleFrame)
                return false;
            if (command.ElementCount == 0)
                continue;
            if (command.Kind == UiDrawCommandKind.Triangles && command.ElementCount % 3 != 0)
                return false;
            if (!hasMaterial)
            {
                material = (command.TextureId, command.Kind == UiDrawCommandKind.Triangles && IsFontTextureId(command.TextureId), command.WhiteBackdropTextureId);
                hasMaterial = true;
            }
            if (command.WhiteBackdropTextureId != material.WhiteBackdrop || command.TextureId != material.Texture
                || (command.Kind == UiDrawCommandKind.Triangles && IsFontTextureId(command.TextureId)) != material.Font)
                return false;
            var count = command.Kind == UiDrawCommandKind.Triangles ? (long)command.ElementCount
                : (long)command.PrimitiveInstanceCount * command.PrimitiveVertexCount;
            if (command.Kind != UiDrawCommandKind.Triangles && count > 65536)
                return false;
            if (count > _compiledIndexLimit || count > int.MaxValue - indexCount)
                return false;
            indexCount += (int)count;
        }
        return indexCount > 0;
    }

    private static int GetCompiledCommandIndexCount(in UiDrawCommand command)
        => checked((int)(command.Kind == UiDrawCommandKind.Triangles ? command.ElementCount
            : command.PrimitiveInstanceCount * command.PrimitiveVertexCount));

    private CompiledLayerGroup CreateCompiledLayerGroup(UiDrawData data, List<CompiledSourceSlice> slices,
        int descriptorCount, int indexCount, StaticGeometryFrameBindings bindings)
    {
        var group = new CompiledLayerGroup
        {
            Tags = new string[slices.Count],
            Stamps = new ulong[slices.Count],
            CommandCounts = new int[slices.Count],
            CommandStarts = new int[slices.Count],
            CommandSpans = new CompiledCommandSpan[slices.Count],
            IndexStarts = new uint[descriptorCount + 1],
            Frames = new CompiledDescriptorFrame?[_frames.Length],
            Bytes = (ulong)indexCount * sizeof(uint),
        };
        CreateCompiledBuffer(group.Bytes, BufferUsageFlags.IndexBufferBit,
            out group.IndexBuffer, out group.IndexMemorySlice, out var mapped);
        var destination = (uint*)mapped;
        var descriptor = 0;
        var written = 0;
        try
        {
            for (var i = 0; i < slices.Count; i++)
            {
                var slice = slices[i];
                var list = data.DrawLists[slice.ListIndex];
                var binding = bindings[slice.ListIndex];
                group.Tags[i] = binding.Tag;
                group.Stamps[i] = binding.IndexTopologyHash;
                group.CommandCounts[i] = slice.CommandCount;
                group.CommandStarts[i] = slice.CommandStart;
                var spanProfile = new CommandRecordProfileState();
                var firstKind = default(UiDrawCommandKind);
                foreach (ref readonly var command in list.Commands.AsSpan().Slice(slice.CommandStart, slice.CommandCount))
                {
                    group.HasCircleCutout |= command.CircleCutoutIndexPlusOne != 0;
                    group.HasBackdropComposite |= command.IsBackdropComposite;
                    if (command.ElementCount > 0)
                    {
                        if (spanProfile.CommandCount == 0)
                            firstKind = command.Kind;
                        RecordCommandProfile(in command, true, ref spanProfile);
                    }
                    group.IndexStarts[descriptor] = (uint)written;
                    var descriptorBits = (uint)descriptor++ << 16;
                    if (command.Kind == UiDrawCommandKind.Triangles)
                    {
                        for (var k = 0u; k < command.ElementCount; k++)
                        {
                            var index = checked(list.Indices[checked((int)(command.IndexOffset + k))] + command.VertexOffset);
                            if (index >= list.Vertices.Count || index > 65535)
                                throw new InvalidOperationException("Compiled layer triangle index is outside its vertex stream.");
                            destination[written++] = descriptorBits | index;
                        }
                    }
                    else
                    {
                        var vertices = command.PrimitiveVertexCount;
                        for (var k = 0u; k < command.PrimitiveInstanceCount * vertices; k++)
                        {
                            var corner = k % 6;
                            var index = corner == 3 ? k - 3 : corner == 4 ? k - 2 : k;
                            destination[written++] = descriptorBits | index;
                        }
                    }
                }
                group.CommandSpans[i] = new(spanProfile.CommandCount, firstKind, spanProfile.LastRecordedKind,
                    spanProfile.TriangleToPrimitiveTransitionCount, spanProfile.PrimitiveToTriangleTransitionCount,
                    spanProfile.RectCircleTransitionCount);
            }
            group.IndexStarts[descriptor] = (uint)written;
        }
        catch
        {
            DestroyBufferResource(new(group.IndexBuffer, default), group.IndexMemorySlice);
            throw;
        }
        _frameCompiledIndexUploadBytes += group.Bytes;
        return group;
    }

    private CompiledDescriptorFrame GetCompiledDescriptorFrame(CompiledLayerGroup group)
    {
        var slot = _frameIndex % _frames.Length;
        if (group.Frames[slot] is { } existing)
            return existing;
        var frame = new CompiledDescriptorFrame();
        var descriptorBytes = checked((ulong)(group.IndexStarts.Length - 1) * (ulong)sizeof(CompiledLayerDescriptor));
        var bytes = descriptorBytes + (ulong)group.MemberCount * (ulong)sizeof(Matrix4x4);
        frame.Values = new CompiledLayerDescriptor[group.IndexStarts.Length - 1];
        frame.Matrices = new Matrix4x4[group.MemberCount];
        frame.MatrixModes = new byte[group.MemberCount];
        frame.PlaneModels = new Vector4[group.MemberCount];
        frame.BindingSources = new StaticGeometryBuffer?[group.MemberCount];
        frame.CommandStates = new ulong[group.MemberCount];
        CreateCompiledBuffer(bytes, BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddressBit,
            out frame.Buffer, out frame.MemorySlice, out var mapped);
        frame.Address = GetBufferDeviceAddress(frame.Buffer);
        frame.MatrixAddress = frame.Address + descriptorBytes;
        frame.Mapped = mapped;
        new Span<byte>(mapped, checked((int)bytes)).Clear();
        group.Frames[slot] = frame;
        return frame;
    }

    private void RecordCompiledLayerGroup(CompiledLayerGroup group, UiDrawData data, int start,
        StaticGeometryFrameBindings bindings, uint imageIndex, in CommandFrameContext context, ref CommandRecordingState state)
    {
        var sharedCamera = _sharedLayerCameraEnabled;
        var camera = default(Matrix4x4);
        var hasCamera = false;
        if (sharedCamera)
        {
            for (var member = 0; member < group.MemberCount; member++)
            {
                ref var entry = ref GetCachedLayerVisibility(start + member, data.DrawLists[start + member], in context);
                if (entry.Visibility == LayerVisibility.Outside)
                    continue;
                if (!entry.View.PlaneModel.HasValue || (hasCamera && camera != entry.View.Transform))
                {
                    sharedCamera = false;
                    break;
                }
                camera = entry.View.Transform;
                hasCamera = true;
            }
            if (sharedCamera && !hasCamera)
                return;
        }
        var sharedBasis = _sharedLayerBasisEnabled;
        var basis = default(Matrix4x4);
        var hasBasis = false;
        sharedBasis &= !sharedCamera;
        if (sharedBasis)
        {
            for (var member = 0; member < group.MemberCount; member++)
            {
                ref var entry = ref GetCachedLayerVisibility(start + member, data.DrawLists[start + member], in context);
                if (entry.Visibility == LayerVisibility.Outside)
                    continue;
                var candidate = GetEffectiveLayerTransform(in entry.View, in context);
                candidate.M41 = candidate.M42 = candidate.M43 = candidate.M44 = 0;
                if (hasBasis && candidate != basis)
                {
                    sharedBasis = false;
                    break;
                }
                basis = candidate;
                hasBasis = true;
            }
            if (!hasBasis)
                return;
        }
        var opaqueGroup = CanUseDepthMask4 && !group.HasCircleCutout && !group.HasBackdropComposite
            && data.DrawLists[start].Scene3D is { WriteDepth: true };
        if (opaqueGroup && !_viewportClipPlanesEnabled)
        {
            ref var firstView = ref GetCachedLayerVisibility(start, data.DrawLists[start], in context);
            opaqueGroup = CanUseDepthMask4ForViewport(firstView.View.PixelViewport);
        }
        for (var member = 0; opaqueGroup && member < group.MemberCount; member++)
        {
            var list = data.DrawLists[start + member];
            foreach (var command in list.Commands)
            {
                if (command.ElementCount != 0 && !HasOpaqueAnalyticMaterial(list, in command))
                {
                    opaqueGroup = false;
                    break;
                }
            }
        }
        var descriptor = 0;
        uint rangeStart = 0, rangeEnd = 0;
        var bound = false;
        CompiledDescriptorFrame? descriptorFrame = null;
        for (var member = 0; member < group.MemberCount; member++)
        {
            var list = data.DrawLists[start + member];
            ref var cachedVisibility = ref GetCachedLayerVisibility(start + member, list, in context);
            var layerView = cachedVisibility.View;
            var visibility = cachedVisibility.Visibility;
            if (visibility == LayerVisibility.Outside)
            {
                descriptor += group.CommandCounts[member];
                continue;
            }
            var binding = bindings[start + member];
            if (!bound)
            {
                var firstCommand = 0;
                while (list.Commands[firstCommand].ElementCount == 0)
                    firstCommand++;
                var command = list.Commands[firstCommand];
                var classification = ClassifyCommand(in command, true, binding);
                if (!TryResolveCommandTexture(in command, in classification, imageIndex, start + member, 0,
                    context.ProfileEnabled, ref state.FontDiagnostic, ref state.Texture, out var texture))
                    return;
                var gpuView = layerView with
                {
                    Transform = sharedCamera ? camera : sharedBasis ? basis : GetEffectiveLayerTransform(in layerView, in context),
                    PlaneModel = null,
                };
                ApplyDepthLayerState(list.Scene3D, in context, in gpuView, ref state.Pipeline,
                    compiled: true, sharedBasis: sharedBasis, sharedCamera: sharedCamera);
                ApplyPaintPipeline(context.CommandBuffer, group.HasCircleCutout, group.HasBackdropComposite, ref state.Pipeline, opaqueGroup);
                var screenCommand = command with
                {
                    ClipRect = layerView.ScreenClip,
                    Translation = default
                };
                if (!TryApplyScissorForCommand(in screenCommand, in context, ref state.Scissor))
                    return;
                ApplyPushConstantsIfNeeded(in context, 0, 0, 1, ref state.PushConstant);
                PushTextureIndexIfNeeded(context.CommandBuffer, CommandTextureIndex(command, texture, classification.IsFontCommand), context.ProfileEnabled, ref state.Descriptor);
                descriptorFrame = GetCompiledDescriptorFrame(group);
                var descriptorContext = new Vector4(context.ScaleX, context.ScaleY, context.TranslateX, context.TranslateY);
                if (!descriptorFrame.HasDescriptorContext || descriptorFrame.DescriptorContext != descriptorContext)
                {
                    // Hidden members must also be invalidated before the new
                    // context becomes current for this frame slot.
                    Array.Clear(descriptorFrame.BindingSources);
                    descriptorFrame.DescriptorContext = descriptorContext;
                    descriptorFrame.HasDescriptorContext = true;
                }
                BindTriangleGeometryIfNeeded(context.CommandBuffer, descriptorFrame.Address, group.IndexBuffer, context.ProfileEnabled, ref state.BufferBinding);
                PushPrimitiveAddressIfNeeded(context.CommandBuffer, descriptorFrame.MatrixAddress, context.ProfileEnabled, ref state.BufferBinding);
                bound = true;
            }
            var previousMatrix = descriptorFrame!.Matrices[member];
            var currentMatrix = sharedCamera ? layerView.Transform : GetEffectiveLayerTransform(in layerView, in context);
            var matrixDestination = (Matrix4x4*)((byte*)descriptorFrame.Mapped
                + (group.IndexStarts.Length - 1) * sizeof(CompiledLayerDescriptor));
            var mode = (byte)(sharedCamera ? 2 : sharedBasis ? 1 : 0);
            var previousMode = descriptorFrame.MatrixModes[member];
            if (sharedCamera)
            {
                var model = layerView.PlaneModel!.Value;
                var value = new Vector4(model.Translation.X, model.Translation.Y, model.DepthOffset, 0f);
                if (previousMode != mode || descriptorFrame.PlaneModels[member] != value)
                {
                    *(Vector4*)(matrixDestination + member) = value;
                    descriptorFrame.PlaneModels[member] = value;
                    _frameLayerStateUploadBytes += (ulong)sizeof(Vector4);
                }
            }
            else if (sharedBasis)
            {
                var translation = new Vector4(currentMatrix.M41, currentMatrix.M42, currentMatrix.M43, currentMatrix.M44);
                if (previousMode != mode
                    || translation != new Vector4(previousMatrix.M41, previousMatrix.M42, previousMatrix.M43, previousMatrix.M44))
                {
                    ((Vector4*)(matrixDestination + member))[3] = translation;
                    _frameLayerStateUploadBytes += (ulong)sizeof(Vector4);
                }
            }
            else if (previousMode != mode || previousMatrix != currentMatrix)
            {
                matrixDestination[member] = currentMatrix;
                _frameLayerStateUploadBytes += (ulong)sizeof(Matrix4x4);
            }
            descriptorFrame.Matrices[member] = currentMatrix;
            descriptorFrame.MatrixModes[member] = mode;
            RecordDrawListProfile(context.ProfileEnabled, true, ref state.Profile);
            state.Pipeline.LayerTransform = layerView.Transform;
            state.Pipeline.LayerModelTranslation = layerView.PlaneModel?.Translation ?? default;
            state.Pipeline.LayerModelDepth = layerView.PlaneModel?.DepthOffset ?? 0;
            state.Pipeline.ViewportPixels = layerView.PixelViewport;
            var cacheable = _compiledDescriptorReuseEnabled && list.CommandStateStamp != 0;
            var descriptorsCurrent = cacheable && ReferenceEquals(descriptorFrame.BindingSources[member], binding)
                && descriptorFrame.CommandStates[member] == list.CommandStateStamp;
            if (_compiledCommandSpansEnabled && descriptorsCurrent && visibility == LayerVisibility.Inside)
            {
                var span = group.CommandSpans[member];
                var nextDescriptor = descriptor + group.CommandCounts[member];
                if (span.Count > 0)
                {
                    var firstIndex = group.IndexStarts[descriptor];
                    if (rangeEnd > rangeStart && firstIndex != rangeEnd)
                    {
                        DrawCompiledLayerRange(rangeStart, rangeEnd, in context, ref state, sharedBasis, sharedCamera);
                        rangeStart = firstIndex;
                    }
                    else if (rangeEnd == rangeStart)
                        rangeStart = firstIndex;
                    rangeEnd = group.IndexStarts[nextDescriptor];
                    _frameCompiledCommands += span.Count;
                    if (context.ProfileEnabled)
                        RecordCompiledSpanProfile(in span, ref state.Profile);
                }
                descriptor = nextDescriptor;
                continue;
            }
            foreach (ref readonly var command in list.Commands.AsSpan().Slice(group.CommandStarts[member], group.CommandCounts[member]))
            {
                _frameCompiledCommandVisits++;
                var commandDescriptor = descriptor++;
                if (command.ElementCount == 0)
                    continue;
                var culled = visibility != LayerVisibility.Inside
                    && IsOutsideLayerFrustum(in command, in context, in state.Pipeline);
                if (!descriptorsCurrent && (!culled || cacheable))
                {
                    _frameCompiledDescriptorBuilds++;
                    var translateX = context.TranslateX;
                    var translateY = context.TranslateY;
                    if (command.Translation.X != 0)
                        translateX += context.ScaleX * command.Translation.X;
                    if (command.Translation.Y != 0)
                        translateY += context.ScaleY * command.Translation.Y;
                    var value = new CompiledLayerDescriptor
                    {
                        VertexAddress = binding.VertexAddress,
                        PrimitiveAddress = binding.PrimitiveAddress,
                        PrimitiveOffset = command.IndexOffset + (command.Kind == UiDrawCommandKind.CircleFilledPrimitives ? (uint)binding.RectPrimitiveCount : 0u),
                        PrimitiveVertexCount = command.PrimitiveVertexCount,
                        DrawMode = command.PrimitiveDrawMode,
                        MatrixIndex = (uint)member,
                        Opacity = command.Opacity,
                        TranslateX = translateX,
                        TranslateY = translateY,
                        Clip = command.ClipRect,
                        CircleCutoutOffset = CircleCutoutOffset(command, 0, command.UsesPairedPrimitiveRecords),
                    };
                    if (command.UsesPairedPrimitiveRecords)
                    {
                        value.PrimitiveAddress += checked((ulong)value.PrimitiveOffset * (ulong)sizeof(PrimitiveInstance));
                        value.PrimitiveOffset = 0;
                    }
                    var target = (CompiledLayerDescriptor*)descriptorFrame!.Mapped + commandDescriptor;
                    ref var previous = ref descriptorFrame.Values[commandDescriptor];
                    if (!MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in value, 1))
                        .SequenceEqual(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in previous, 1))))
                    {
                        previous = value;
                        *target = value;
                        _frameLayerStateUploadBytes += (ulong)sizeof(CompiledLayerDescriptor);
                    }
                }
                if (culled)
                    continue;
                RecordCommandProfile(in command, context.ProfileEnabled, ref state.Profile);
                var firstIndex = group.IndexStarts[commandDescriptor];
                var endIndex = group.IndexStarts[commandDescriptor + 1];
                if (rangeEnd > rangeStart && firstIndex != rangeEnd)
                {
                    DrawCompiledLayerRange(rangeStart, rangeEnd, in context, ref state, sharedBasis, sharedCamera);
                    rangeStart = firstIndex;
                }
                else if (rangeEnd == rangeStart)
                    rangeStart = firstIndex;
                rangeEnd = endIndex;
                _frameCompiledCommands++;
            }
            if (cacheable)
            {
                descriptorFrame.BindingSources[member] = binding;
                descriptorFrame.CommandStates[member] = list.CommandStateStamp;
            }
        }
        if (rangeEnd > rangeStart)
            DrawCompiledLayerRange(rangeStart, rangeEnd, in context, ref state, sharedBasis, sharedCamera);
        // CPU culling tracks each member independently from the pushed matrix.
        // Keep the common GPU basis valid across adjacent compiled groups.
    }

    private static void RecordCompiledSpanProfile(in CompiledCommandSpan span, ref CommandRecordProfileState state)
    {
        if (span.Count == 0)
            return;
        RecordCommandKindTransition(span.FirstKind, ref state);
        state.CommandCount += span.Count;
        state.TriangleToPrimitiveTransitionCount += span.TriangleToPrimitive;
        state.PrimitiveToTriangleTransitionCount += span.PrimitiveToTriangle;
        state.RectCircleTransitionCount += span.RectCircle;
        state.LastRecordedKind = span.LastKind;
        state.HasLastRecordedKind = true;
    }

    private void DrawCompiledLayerRange(uint first, uint end, in CommandFrameContext context, ref CommandRecordingState state, bool sharedBasis, bool sharedCamera)
    {
        var timestamp = BeginFrameProfileTiming(context.ProfileEnabled);
        _vk.CmdDrawIndexed(context.CommandBuffer, end - first, 1, first, 0, 0);
        RecordDrawCallProfile(context.ProfileEnabled, timestamp, ref state.DrawDispatch);
        if (context.ProfileEnabled)
            state.Profile.StaticDrawCallCount++;
        _frameCompiledDraws++;
        if (sharedBasis)
            _frameSharedBasisDraws++;
        if (sharedCamera)
            _frameSharedCameraDraws++;
    }

    private void TrimCompiledLayerCache(ulong requestedBytes)
    {
        while (true)
        {
            ulong oldestKey = 0;
            CompiledLayerGroup? oldest = null;
            foreach (var pair in _compiledLayerGroups)
            {
                if (pair.Value.LastUsedFrame == _frameIndex)
                    continue;
                if (oldest is null || pair.Value.LastUsedFrame < oldest.LastUsedFrame)
                {
                    oldest = pair.Value;
                    oldestKey = pair.Key;
                }
            }
            if (oldest is null)
                break;
            if (_compiledCacheBytes + requestedBytes <= CompiledCacheByteLimit && _compiledLayerGroups.Count < 256
                && _frameIndex - oldest.LastUsedFrame <= 120)
                break;
            _compiledLayerGroups.Remove(oldestKey);
            _compiledCacheBytes -= oldest.Bytes;
            DestroyCompiledLayerGroup(oldest, deferred: true);
        }
    }

    private void DestroyCompiledLayerGroup(CompiledLayerGroup group, bool deferred)
    {
        foreach (var frame in group.Frames)
        {
            if (frame is null)
                continue;
            if (deferred)
                QueueBufferDestroy(frame.Buffer, default, frame.MemorySlice);
            else
                DestroyBufferResource(new(frame.Buffer, default), frame.MemorySlice);
        }
        if (deferred)
            QueueBufferDestroy(group.IndexBuffer, default, group.IndexMemorySlice);
        else
            DestroyBufferResource(new(group.IndexBuffer, default), group.IndexMemorySlice);
    }

    private void DestroyCompiledLayerGroups()
    {
        foreach (var group in _compiledLayerGroups.Values)
            DestroyCompiledLayerGroup(group, deferred: false);
        _compiledLayerGroups.Clear();
        DestroyCompiledMemoryPages();
        _compiledFrameGroups.Clear();
        _compiledSourceAnchors.Clear();
        _compiledPlan = [];
        _layerVisibility = [];
        _compiledCacheBytes = 0;
    }
}
