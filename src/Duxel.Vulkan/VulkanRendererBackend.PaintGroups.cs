using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly Dictionary<UiTextureId, PaintGroupEntry> _paintGroups = [];
    private readonly Dictionary<UiTextureId, PaintGroupEntry> _paintGroupOutputs = [];
    private readonly Dictionary<UiTextureId, ulong> _paintTextureRevisions = [];
    private readonly List<PaintGroupEntry> _paintGroupOrder = [];
    private readonly Dictionary<long, int> _paintGeometryUsers = [];
    private ulong _paintTextureRevision;
    private int _framePaintGroupPasses, _framePaintGroupDrawCalls, _framePaintGroupCacheHits;

    private sealed class PaintGroupEntry(UiTextureId black, UiTextureId white, UiPaintGroupLease lease)
    {
        internal readonly UiTextureId Black = black, White = white;
        internal readonly UiPaintGroupLease Lease = lease;
        internal readonly StaticGeometryFrameBindings Bindings = new();
        internal byte Visit;
        internal bool Dirty, Initialized;
        internal ulong[] Revisions = [];
        internal Image Msaa;
        internal ImageView MsaaView;
        internal DeviceMemory MsaaMemory;
        internal ulong MsaaBytes;
        internal SampleCountFlags Samples;
        internal Format Format;
        internal UiDrawData? View;
    }

    private void RegisterPaintGroup(UiTextureUpdate update)
    {
        var content = update.PaintGroup?.Content ?? throw new ArgumentException("A paint-group update requires a snapshot lease.");
        var white = update.WhitePaintTexture ?? throw new ArgumentException("A paint-group update requires its white texture identity.");
        if (white == update.TextureId || content.TextureDependencies.Contains(white)
            || content.TextureDependencies.Contains(update.TextureId))
            throw new ArgumentException("A paint group cannot sample its own output.");
        if (content.PixelWidth != update.Width || content.PixelHeight != update.Height || !update.RgbaPixels.IsEmpty)
            throw new ArgumentException("Paint-group extent must match the immutable snapshot and carry no pixel upload.");
        if (_paintGroups.TryGetValue(update.TextureId, out var previous)
            && previous.White == white && previous.Lease.Content.Identity == content.Identity)
            return;
        if (_paintGroupOutputs.TryGetValue(white, out var whiteOwner) && !ReferenceEquals(whiteOwner, previous))
            throw new ArgumentException("Paint-group white output is already owned.");
        if (_paintGroupOutputs.TryGetValue(update.TextureId, out var blackOwner) && !ReferenceEquals(blackOwner, previous))
            throw new ArgumentException("Paint-group black output is already owned.");
        if (previous is not null)
            DestroyTexture(previous.Black);
        if (_textures.ContainsKey(update.TextureId) || _textures.ContainsKey(white))
            throw new ArgumentException("Paint-group outputs must use unoccupied texture identities.");
        var entry = new PaintGroupEntry(update.TextureId, white, content.Acquire());
        _paintGeometryUsers.TryGetValue(content.Identity, out var geometryUsers);
        _paintGeometryUsers[content.Identity] = geometryUsers + 1;
        _paintGroups.Add(update.TextureId, entry);
        _paintGroupOutputs.Add(update.TextureId, entry);
        _paintGroupOutputs.Add(white, entry);
        _colorImageTextures.Add(update.TextureId);
        _colorImageTextures.Add(white);
    }

    private void DestroyPaintGroups()
    {
        // Device teardown has already waited idle; do not enqueue new deferred frees after its drain.
        foreach (var entry in _paintGroups.Values)
        {
            entry.View?.Dispose();
            entry.Lease.Dispose();
            if (entry.MsaaView.Handle != 0)
                _vk.DestroyImageView(_device, entry.MsaaView, null);
            if (entry.Msaa.Handle != 0)
                _vk.DestroyImage(_device, entry.Msaa, null);
            if (entry.MsaaMemory.Handle != 0)
                _vk.FreeMemory(_device, entry.MsaaMemory, null);
        }
        _paintGeometryUsers.Clear();
        _paintGroups.Clear();
        _paintGroupOutputs.Clear();
        _paintGroupOrder.Clear();
        _paintTextureRevisions.Clear();
    }

    private void ForgetPaintGroup(UiTextureId id)
    {
        if (!_paintGroupOutputs.Remove(id, out var entry))
            return;
        _paintGroups.Remove(entry.Black);
        if (_paintGroups.Count == 0)
            _paintTextureRevisions.Clear();
        _paintGroupOutputs.Remove(entry.Black);
        _paintGroupOutputs.Remove(entry.White);
        var content = entry.Lease.Content;
        var users = _paintGeometryUsers[content.Identity] - 1;
        if (users == 0)
        {
            _paintGeometryUsers.Remove(content.Identity);
            for (var i = 0; i < content.DrawLists.Count; i++)
            {
                var tag = PaintGeometryTag(content, i);
                if (_staticGeometryBuffers.Remove(tag, out var geometry))
                    QueueStaticGeometryBufferDestroy(geometry);
                _staticGeometryLastSeenFrame.Remove(tag);
                QueueRetiredStaticGeometryBuffersDestroy(tag);
                RemoveStaticPrimitiveTrianglePolicyState(tag);
            }
        }
        else
            _paintGeometryUsers[content.Identity] = users;
        entry.View?.Dispose();
        entry.Lease.Dispose();
        RetirePaintGroupMsaa(entry);
        var other = id == entry.Black ? entry.White : entry.Black;
        DestroyTexture(other);
        _colorImageTextures.Remove(entry.Black);
        _colorImageTextures.Remove(entry.White);
    }

    private void PlanPaintGroupsForFrame(UiDrawData drawData)
    {
        _framePaintGroupPasses = _framePaintGroupDrawCalls = _framePaintGroupCacheHits = 0;
        _paintGroupOrder.Clear();
        if (_paintGroups.Count == 0)
            return;
        foreach (var entry in _paintGroups.Values)
            entry.Visit = 0;
        // Resolve the complete dependency graph before allocating any target or geometry.
        void Visit(PaintGroupEntry entry)
        {
            if (entry.Visit == 2)
                return;
            if (entry.Visit == 1)
                throw new InvalidOperationException("Paint-group texture dependency cycle.");
            entry.Visit = 1;
            foreach (var dependency in entry.Lease.Content.TextureDependencies)
            {
                if (_paintGroupOutputs.TryGetValue(dependency, out var source))
                    Visit(source);
                else if (!_textures.ContainsKey(dependency))
                    throw new InvalidOperationException($"Paint-group source texture is missing : {dependency.Value}.");
            }
            entry.Visit = 2;
            _paintGroupOrder.Add(entry);
        }
        foreach (var list in drawData.DrawLists)
            foreach (var command in list.Commands)
            {
                if (_paintGroupOutputs.TryGetValue(command.TextureId, out var source))
                    Visit(source);
                if (command.WhiteBackdropTextureId is { } white && _paintGroupOutputs.TryGetValue(white, out source))
                    Visit(source);
            }
        long logicalTargetBytes = 0;
        foreach (var entry in _paintGroups.Values)
        {
            var content = entry.Lease.Content;
            logicalTargetBytes = checked(logicalTargetBytes + (long)content.PixelWidth * content.PixelHeight
                * (8 + (_msaaSampleCount == SampleCountFlags.Count1Bit ? 0 : 4 * (int)_msaaSampleCount)));
        }
        ulong neededSlots = 0;
        foreach (var entry in _paintGroupOrder)
        {
            var content = entry.Lease.Content;
            if ((uint)content.PixelWidth > _maxImageDimension2D || (uint)content.PixelHeight > _maxImageDimension2D)
                throw new InvalidOperationException("Paint-group extent exceeds the device image limit.");
            if (entry.Format != _swapchainFormat || entry.Samples != _msaaSampleCount)
                neededSlots += 2;
            else
            {
                if (!_textures.ContainsKey(entry.Black))
                    neededSlots++;
                if (!_textures.ContainsKey(entry.White))
                    neededSlots++;
            }
        }
        var availableSlots = (ulong)BindlessTextureCapacity - _nextBindlessTextureSlot + (ulong)_freeBindlessTextureSlots.Count;
        if (neededSlots > availableSlots)
            throw new InvalidOperationException("Paint-group outputs exceed available bindless texture slots.");
        if (logicalTargetBytes > 256 * 1024 * 1024)
            throw new InvalidOperationException("Live paint-group targets exceed the256MiB logical target budget.");
    }

    private bool IsPaintGroupDirty(PaintGroupEntry entry)
    {
        var dependencies = entry.Lease.Content.TextureDependencies;
        if (!entry.Initialized || entry.Samples != _msaaSampleCount || entry.Format != _swapchainFormat
            || entry.Revisions.Length != dependencies.Count)
            return true;
        for (var i = 0; i < dependencies.Count; i++)
            if (_paintTextureRevisions.GetValueOrDefault(dependencies[i]) != entry.Revisions[i])
                return true;
        return false;
    }

    private bool HasDirtyPaintGroups()
    {
        foreach (var entry in _paintGroupOrder)
            if (IsPaintGroupDirty(entry))
                return true;
        return false;
    }

    private void PreparePaintGroupsForFrame()
    {
        foreach (var entry in _paintGroupOrder)
        {
            var content = entry.Lease.Content;
            var dependencies = content.TextureDependencies;
            var dirty = IsPaintGroupDirty(entry);
            if (entry.Revisions.Length != dependencies.Count)
                entry.Revisions = new ulong[dependencies.Count];
            for (var i = 0; i < dependencies.Count; i++)
            {
                _paintTextureRevisions.TryGetValue(dependencies[i], out var revision);

                entry.Revisions[i] = revision;
            }
            entry.Dirty = dirty;
            if (!dirty)
            {
                _framePaintGroupCacheHits++;
                continue;
            }
            EnsurePaintGroupTargets(entry);
            entry.Bindings.Reset(content.DrawLists.Count);
            for (var i = 0; i < content.DrawLists.Count; i++)
            {
                var tag = PaintGeometryTag(content, i);
                MarkStaticGeometrySeen(tag, _frameIndex);
                entry.Bindings[i] = EnsureStaticGeometryBuffer(tag, content.DrawLists[i], requiresContentHash: true);
            }
            entry.View ??= new UiDrawData(new(content.Bounds.Width, content.Bounds.Height),
                new(content.Bounds.X, content.Bounds.Y), content.FramebufferScale,
                content.DrawLists.Sum(list => list.Vertices.Count), content.DrawLists.Sum(list => list.Indices.Count),
                UiPooledList<UiDrawList>.RentAndCopy(content.DrawLists.Select(list => list with { OwnsBuffers = false }).ToArray().AsSpan()),
                UiPooledList<UiTextureUpdate>.RentAndCopy(ReadOnlySpan<UiTextureUpdate>.Empty));
            var revisionValue = ++_paintTextureRevision;
            // The damage snapshot tracks both matte revisions at each consumer's bounds.
            _paintTextureRevisions[entry.Black] = _paintTextureRevisions[entry.White] = revisionValue;
        }
    }

    private void EnsurePaintGroupTargets(PaintGroupEntry entry)
    {
        var content = entry.Lease.Content;
        if ((uint)content.PixelWidth > _maxImageDimension2D || (uint)content.PixelHeight > _maxImageDimension2D)
            throw new InvalidOperationException("Paint-group extent exceeds the device image limit.");
        if (entry.Format != _swapchainFormat || entry.Samples != _msaaSampleCount)
        {
            RetirePaintGroupTexture(entry.Black);
            RetirePaintGroupTexture(entry.White);
            RetirePaintGroupMsaa(entry);
            entry.Initialized = false;
        }
        foreach (var id in new[] { entry.Black, entry.White })
        {
            if (_textures.ContainsKey(id))
                continue;
            var allocationBytes = CreateImage(content.PixelWidth, content.PixelHeight, _swapchainFormat, ImageTiling.Optimal,
                ImageUsageFlags.SampledBit | ImageUsageFlags.ColorAttachmentBit,
                MemoryPropertyFlags.DeviceLocalBit, out var image, out var memory);
            var view = CreateImageView(image, _swapchainFormat);
            var slot = AllocateBindlessTextureSlot(view, false);
            _textures.Add(id, new(image, memory, view, slot, content.PixelWidth, content.PixelHeight, _swapchainFormat,
                allocationBytes: allocationBytes));
        }
        if (_msaaSampleCount != SampleCountFlags.Count1Bit && entry.Msaa.Handle == 0)
        {
            entry.MsaaBytes = CreateImage(content.PixelWidth, content.PixelHeight, _swapchainFormat, ImageTiling.Optimal,
                ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit,
                MemoryPropertyFlags.DeviceLocalBit, out entry.Msaa, out entry.MsaaMemory, _msaaSampleCount);
            entry.MsaaView = CreateImageView(entry.Msaa, _swapchainFormat);
        }
        entry.Format = _swapchainFormat;
        entry.Samples = _msaaSampleCount;
    }

    private void RetirePaintGroupTexture(UiTextureId id)
    {
        if (!_textures.Remove(id, out var texture))
            return;
        _frames[_frameIndex % _frames.Length].PendingTextureDestroys.Add(new(texture, _frameIndex + _frames.Length));
    }
    private void RetirePaintGroupMsaa(PaintGroupEntry entry)
    {
        if (entry.Msaa.Handle == 0)
            return;
        QueueRetiredRenderTargets(new([], entry.Msaa, entry.MsaaView, entry.MsaaMemory, entry.MsaaBytes, [], [], [], 0));
        entry.Msaa = default;
        entry.MsaaView = default;
        entry.MsaaMemory = default;
        entry.MsaaBytes = 0;
    }

    private void RecordPaintGroups(CommandBuffer commandBuffer, uint imageIndex)
    {
        if (_paintGroupOrder.Count == 0)
            return;
        var damagePartial = _damagePartial;
        var projectedCapture = _projectedPaintCapture;
        _damagePartial = false;
        try
        {
            foreach (var entry in _paintGroupOrder)
            {
                if (!entry.Dirty)
                    continue;
                var content = entry.Lease.Content;
                _projectedPaintCapture = content.TextMode == UiPaintGroupTextMode.Projected;
                for (var whitePass = 0; whitePass < 2; whitePass++)
                {
                    var target = _textures[whitePass == 0 ? entry.Black : entry.White];
                    var barrier = PaintGroupBarrier(target.Image,
                        entry.Initialized ? ImageLayout.ShaderReadOnlyOptimal : ImageLayout.Undefined,
                        ImageLayout.ColorAttachmentOptimal, entry.Initialized ? AccessFlags.ShaderReadBit : 0,
                        AccessFlags.ColorAttachmentWriteBit);
                    _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.FragmentShaderBit,
                        PipelineStageFlags.ColorAttachmentOutputBit, 0, 0, null, 0, null, 1, &barrier);
                    if (entry.Msaa.Handle != 0)
                    {
                        barrier = PaintGroupBarrier(entry.Msaa, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal,
                            AccessFlags.ColorAttachmentWriteBit, AccessFlags.ColorAttachmentWriteBit);
                        _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.ColorAttachmentOutputBit,
                            PipelineStageFlags.ColorAttachmentOutputBit, 0, 0, null, 0, null, 1, &barrier);
                    }
                    var clear = new ClearValue { Color = new((float)whitePass, (float)whitePass, (float)whitePass, (float)whitePass) };
                    var attachment = new RenderingAttachmentInfo
                    {
                        SType = StructureType.RenderingAttachmentInfo,
                        ImageView = entry.Msaa.Handle != 0 ? entry.MsaaView : target.View,
                        ImageLayout = ImageLayout.ColorAttachmentOptimal,
                        ResolveMode = entry.Msaa.Handle != 0 ? ResolveModeFlags.AverageBit : ResolveModeFlags.None,
                        ResolveImageView = entry.Msaa.Handle != 0 ? target.View : default,
                        ResolveImageLayout = entry.Msaa.Handle != 0 ? ImageLayout.ColorAttachmentOptimal : ImageLayout.Undefined,
                        LoadOp = AttachmentLoadOp.Clear,
                        StoreOp = entry.Msaa.Handle != 0 ? AttachmentStoreOp.DontCare : AttachmentStoreOp.Store,
                        ClearValue = clear
                    };
                    var rendering = new RenderingInfo
                    {
                        SType = StructureType.RenderingInfo,
                        RenderArea = new(new(0, 0), new((uint)content.PixelWidth, (uint)content.PixelHeight)),
                        LayerCount = 1,
                        ColorAttachmentCount = 1,
                        PColorAttachments = &attachment
                    };
                    _khrDynamicRendering.CmdBeginRendering(commandBuffer, &rendering);
                    BindGlobalBindlessTextureSet(commandBuffer);
                    _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);
                    var scaleX = 2f / content.Bounds.Width;
                    var scaleY = 2f / content.Bounds.Height;
                    var tx = -1 - content.Bounds.X * scaleX;
                    var ty = -1 - content.Bounds.Y * scaleY;
                    var context = new CommandFrameContext(commandBuffer, 0, default, 0, scaleX, scaleY, tx, ty, tx, ty,
                        content.Bounds.X, content.Bounds.Y, content.PixelWidth / content.Bounds.Width,
                        content.PixelHeight / content.Bounds.Height, new(content.Bounds.Width, content.Bounds.Height),
                        content.PixelWidth, content.PixelHeight, false, _profilingEnabled);
                    var state = CreateCommandRecordingState();
                    RecordCommandDrawLists(entry.View!, imageIndex, entry.Bindings, in context, ref state, allowCompiled: false);
                    _framePaintGroupDrawCalls += state.DrawDispatch.DrawCallCount;
                    _framePaintGroupPasses++;
                    _khrDynamicRendering.CmdEndRendering(commandBuffer);
                    barrier = PaintGroupBarrier(target.Image, ImageLayout.ColorAttachmentOptimal, ImageLayout.ShaderReadOnlyOptimal,
                        AccessFlags.ColorAttachmentWriteBit, AccessFlags.ShaderReadBit);
                    _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.ColorAttachmentOutputBit,
                        PipelineStageFlags.FragmentShaderBit, 0, 0, null, 0, null, 1, &barrier);
                }
                entry.Initialized = true;
                entry.Dirty = false;
            }
        }
        finally { _damagePartial = damagePartial; _projectedPaintCapture = projectedCapture; }
    }

    private static string PaintGeometryTag(UiPaintGroupContent content, int index) => "duxel.paint:" + content.Identity + ":" + index;

    private static ImageMemoryBarrier PaintGroupBarrier(Image image, ImageLayout oldLayout, ImageLayout newLayout,
        AccessFlags source, AccessFlags destination) => new()
        {
            SType = StructureType.ImageMemoryBarrier,
            Image = image,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcAccessMask = source,
            DstAccessMask = destination,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
        };
}
