using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly Dictionary<UiTextureId, TextureResource> _textures = new();
    private readonly Stack<uint> _freeBindlessTextureSlots = new();
    private uint _nextBindlessTextureSlot;
    private readonly nuint _fontTextureIdValue;
    private readonly nuint _whiteTextureIdValue;

    private void ApplyTextureUpdates(ReadOnlySpan<UiTextureUpdate> updates)
    {
        if (updates.Length is 0)
        {
            return;
        }

        BeginUploadBatch();
        try
        {
            var lastId = default(UiTextureId);
            var hasLast = false;
            var lastKind = UiTextureUpdateKind.Create;
            var lastHasExisting = false;
            var lastExisting = default(TextureResource);
            for (var i = 0; i < updates.Length; i++)
            {
                var update = updates[i];
                var sameId = hasLast && update.TextureId.Equals(lastId);
                var hasExisting = lastHasExisting;
                var existing = lastExisting;
                if (!sameId)
                {
                    hasExisting = _textures.TryGetValue(update.TextureId, out existing);
                    lastHasExisting = hasExisting;
                    lastExisting = existing;
                }

                hasLast = true;
                lastId = update.TextureId;
                lastKind = update.Kind;
                var expectedFormat = ToVkFormat(update.Format);

                switch (update.Kind)
                {
                    case UiTextureUpdateKind.Create:
                        if (hasExisting)
                        {
                            if (existing.Width != update.Width
                                || existing.Height != update.Height
                                || existing.Format != expectedFormat)
                            {
                                DestroyTexture(update.TextureId);
                                lastHasExisting = false;
                                CreateOrUpdateTexture(update, true);
                                _textures.TryGetValue(update.TextureId, out lastExisting);
                                lastHasExisting = true;
                            }
                            else
                            {
                                CreateOrUpdateTexture(update, false);
                                lastHasExisting = true;
                            }
                        }
                        else
                        {
                            CreateOrUpdateTexture(update, true);
                            _textures.TryGetValue(update.TextureId, out lastExisting);
                            lastHasExisting = true;
                        }
                        break;
                    case UiTextureUpdateKind.Update:
                        if (hasExisting)
                        {
                            if (existing.Width != update.Width
                                || existing.Height != update.Height
                                || existing.Format != expectedFormat)
                            {
                                DestroyTexture(update.TextureId);
                                lastHasExisting = false;
                                CreateOrUpdateTexture(update, true);
                                _textures.TryGetValue(update.TextureId, out lastExisting);
                                lastHasExisting = true;
                            }
                            else
                            {
                                var batchCount = CountConsecutiveCompatibleTextureRegionUpdates(updates.Slice(i));
                                if (batchCount > 1)
                                {
                                    UploadTextureDataBatch(existing.Image, updates.Slice(i, batchCount), ImageLayout.ShaderReadOnlyOptimal);
                                    i += batchCount - 1;
                                }
                                else
                                {
                                    CreateOrUpdateTexture(update, false);
                                }

                                lastHasExisting = true;
                            }
                        }
                        else
                        {
                            CreateOrUpdateTexture(update, true);
                            _textures.TryGetValue(update.TextureId, out lastExisting);
                            lastHasExisting = true;
                        }
                        break;
                    case UiTextureUpdateKind.Destroy:
                        DestroyTexture(update.TextureId);
                        lastHasExisting = false;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported texture update kind: {update.Kind}.");
                }
            }
        }
        finally
        {
            EndUploadBatch();
        }
    }

    private void CreateOrUpdateTexture(UiTextureUpdate update, bool isCreate)
    {
        ValidateTextureUpdateRegion(update);

        if (isCreate)
        {
            if (_textures.ContainsKey(update.TextureId))
            {
                throw new InvalidOperationException("Texture already exists.");
            }

            var resource = CreateTextureResource(update, ImageLayout.Undefined);
            _textures[update.TextureId] = resource;
            return;
        }

        if (!_textures.TryGetValue(update.TextureId, out var existing))
        {
            throw new InvalidOperationException("Texture update requested for missing texture.");
        }

        if (existing.Width != update.Width || existing.Height != update.Height || existing.Format != ToVkFormat(update.Format))
        {
            throw new InvalidOperationException("Texture update size/format mismatch.");
        }

        UploadTextureData(existing.Image, update, ImageLayout.ShaderReadOnlyOptimal);
    }

    private static void ValidateTextureUpdateRegion(UiTextureUpdate update)
    {
        if (update.Width <= 0 || update.Height <= 0)
        {
            throw new InvalidOperationException("Texture size must be positive.");
        }

        var regionWidth = update.EffectiveRegionWidth;
        var regionHeight = update.EffectiveRegionHeight;
        if (regionWidth <= 0
            || regionHeight <= 0
            || update.X < 0
            || update.Y < 0
            || update.X + regionWidth > update.Width
            || update.Y + regionHeight > update.Height)
        {
            throw new InvalidOperationException("Texture update region is outside texture bounds.");
        }

        var expectedLength = checked(regionWidth * regionHeight * 4);
        if (update.RgbaPixels.Length != expectedLength)
        {
            throw new InvalidOperationException("Texture RGBA payload size does not match update region.");
        }
    }

    private TextureResource CreateTextureResource(UiTextureUpdate update, ImageLayout initialLayout)
    {
        var format = ToVkFormat(update.Format);
        CreateImage(
            update.Width,
            update.Height,
            format,
            ImageTiling.Optimal,
            ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out var image,
            out var memory
        );

        UploadTextureData(image, update, initialLayout);

        var view = CreateImageView(image, format);
        var isFontTexture = IsFontTextureId(update.TextureId);
        var slotIndex = AllocateBindlessTextureSlot(view, isFontTexture);

        return new TextureResource(image, memory, view, slotIndex, update.Width, update.Height, format);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsFontTextureId(UiTextureId textureId)
    {
        var value = textureId.Value;
        return value == _fontTextureIdValue || value >= 1_100_000_000;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsWhiteTextureId(UiTextureId textureId) => textureId.Value == _whiteTextureIdValue;

    private void DestroyTextureResources()
    {
        if (_textures.Count is 0)
        {
            return;
        }

        var keys = new List<UiTextureId>(_textures.Keys);
        foreach (var key in keys)
        {
            DestroyTextureImmediate(key);
        }
    }

    private void DestroyTexture(UiTextureId textureId)
    {
        if (!_textures.TryGetValue(textureId, out var resource))
        {
            return;
        }

        _textures.Remove(textureId);

        if (_frames.Length == 0)
        {
            DestroyTextureResource(resource);
            return;
        }

        var frameCount = _frames.Length;
        var frame = _frameIndex % frameCount;
        var destroyFrame = _frameIndex + frameCount;
        _frames[frame].PendingTextureDestroys.Add(new PendingTextureDestroy(resource, destroyFrame));
    }

    private void DestroyTextureImmediate(UiTextureId textureId)
    {
        if (!_textures.TryGetValue(textureId, out var resource))
        {
            return;
        }

        _textures.Remove(textureId);
        DestroyTextureResource(resource);
    }

    private void DestroyTextureResource(TextureResource resource)
    {
        ForgetPendingTextureShaderReadTransition(resource.Image);

        // The bindless slot is recycled once the deferred destroy point is reached,
        // so no in-flight frame can still reference it when it is rewritten.
        _freeBindlessTextureSlots.Push(resource.SlotIndex);

        if (resource.View.Handle is not 0)
        {
            _vk.DestroyImageView(_device, resource.View, null);
        }

        if (resource.Image.Handle is not 0)
        {
            _vk.DestroyImage(_device, resource.Image, null);
        }

        if (resource.Memory.Handle is not 0)
        {
            _vk.FreeMemory(_device, resource.Memory, null);
        }
    }

    private void FlushPendingTextureDestroys(int frame)
    {
        if (_frames.Length == 0)
        {
            return;
        }

        var list = _frames[frame].PendingTextureDestroys;
        if (list.Count == 0)
        {
            return;
        }

        var hasDueDestroy = false;
        for (var i = 0; i < list.Count; i++)
        {
            if (_frameIndex >= list[i].DestroyFrame)
            {
                hasDueDestroy = true;
                break;
            }
        }

        if (!hasDueDestroy)
        {
            return;
        }

        WaitForAllInFlightFrameFences();

        var write = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (_frameIndex >= entry.DestroyFrame)
            {
                DestroyTextureResource(entry.Resource);
                continue;
            }

            list[write++] = entry;
        }

        if (write < list.Count)
        {
            list.RemoveRange(write, list.Count - write);
        }
    }

    private void FlushPendingTextureDestroysAll()
    {
        if (_frames.Length == 0)
        {
            return;
        }

        WaitForAllInFlightFrameFences();

        for (var i = 0; i < _frames.Length; i++)
        {
            var list = _frames[i].PendingTextureDestroys;
            foreach (var entry in list)
            {
                DestroyTextureResource(entry.Resource);
            }
            list.Clear();
        }
    }

    private static int CountConsecutiveCompatibleTextureRegionUpdates(ReadOnlySpan<UiTextureUpdate> updates)
    {
        if (updates.Length <= 1)
        {
            return updates.Length;
        }

        var first = updates[0];
        if (first.Kind != UiTextureUpdateKind.Update || first.CoversEntireTexture)
        {
            return 1;
        }

        var count = 1;
        for (var i = 1; i < updates.Length; i++)
        {
            var candidate = updates[i];
            if (candidate.Kind != UiTextureUpdateKind.Update
                || candidate.CoversEntireTexture
                || !candidate.TextureId.Equals(first.TextureId)
                || candidate.Format != first.Format
                || candidate.Width != first.Width
                || candidate.Height != first.Height)
            {
                break;
            }

            var overlaps = false;
            for (var existingIndex = 0; existingIndex < count; existingIndex++)
            {
                if (TextureUpdateRegionsOverlap(candidate, updates[existingIndex]))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static bool TextureUpdateRegionsOverlap(UiTextureUpdate left, UiTextureUpdate right)
    {
        var leftMaxX = left.X + left.EffectiveRegionWidth;
        var leftMaxY = left.Y + left.EffectiveRegionHeight;
        var rightMaxX = right.X + right.EffectiveRegionWidth;
        var rightMaxY = right.Y + right.EffectiveRegionHeight;
        return left.X < rightMaxX
            && leftMaxX > right.X
            && left.Y < rightMaxY
            && leftMaxY > right.Y;
    }

    private void UploadTextureDataBatch(Image image, ReadOnlySpan<UiTextureUpdate> updates, ImageLayout initialLayout)
    {
        if (updates.Length <= 0)
        {
            return;
        }

        if (updates.Length is 1)
        {
            UploadTextureData(image, updates[0], initialLayout);
            return;
        }

        var totalSize = 0UL;
        for (var i = 0; i < updates.Length; i++)
        {
            ValidateTextureUpdateRegion(updates[i]);
            totalSize = checked(totalSize + (ulong)GetExpectedTextureDataSize(
                updates[i].Format,
                updates[i].EffectiveRegionWidth,
                updates[i].EffectiveRegionHeight));
        }

        if (totalSize > nuint.MaxValue)
        {
            throw new InvalidOperationException("Texture update batch is too large.");
        }

        var commandBuffer = BeginStagingUpload((nuint)totalSize, out var stagingOffset);
        var regions = new BufferImageCopy[updates.Length];
        var relativeOffset = 0UL;
        for (var i = 0; i < updates.Length; i++)
        {
            var update = updates[i];
            var regionWidth = update.EffectiveRegionWidth;
            var regionHeight = update.EffectiveRegionHeight;
            var expectedBytes = GetExpectedTextureDataSize(update.Format, regionWidth, regionHeight);
            var destination = new Span<byte>((byte*)_stagingMappedPtr + stagingOffset + (nuint)relativeOffset, (int)expectedBytes);
            update.RgbaPixels.Span.CopyTo(destination);

            regions[i] = new BufferImageCopy
            {
                BufferOffset = (ulong)stagingOffset + relativeOffset,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                ImageOffset = new Offset3D(update.X, update.Y, 0),
                ImageExtent = new Extent3D((uint)regionWidth, (uint)regionHeight, 1),
            };
            relativeOffset += (ulong)expectedBytes;
        }

        try
        {
            var uploadInitialLayout = PrepareTextureUploadTransferLayout(image, initialLayout);
            if (uploadInitialLayout != ImageLayout.TransferDstOptimal)
            {
                TransitionImageLayout(commandBuffer, image, uploadInitialLayout, ImageLayout.TransferDstOptimal);
            }

            fixed (BufferImageCopy* regionsPtr = regions)
            {
                RecordUploadTextureCopyProfile(regions.Length);
                _vk.CmdCopyBufferToImage(
                    commandBuffer,
                    _stagingBuffer,
                    image,
                    ImageLayout.TransferDstOptimal,
                    (uint)regions.Length,
                    regionsPtr);
            }

            CompleteTextureUploadTransferLayout(commandBuffer, image);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

    private void UploadTextureData(Image image, UiTextureUpdate update, ImageLayout initialLayout)
    {
        var regionWidth = update.EffectiveRegionWidth;
        var regionHeight = update.EffectiveRegionHeight;
        var expectedBytes = GetExpectedTextureDataSize(update.Format, regionWidth, regionHeight);
        var source = update.RgbaPixels.Span;
        byte[]? normalizedPixels = null;
        ReadOnlySpan<byte> data;

        if ((nuint)source.Length == expectedBytes)
        {
            data = source;
        }
        else
        {
            normalizedPixels = new byte[(int)expectedBytes];
            source[..Math.Min(source.Length, normalizedPixels.Length)].CopyTo(normalizedPixels);
            data = normalizedPixels;
        }

        var bufferSize = expectedBytes;
        var commandBuffer = BeginStagingUpload(bufferSize, out var stagingOffset);

        data.CopyTo(new Span<byte>((byte*)_stagingMappedPtr + stagingOffset, data.Length));

        try
        {
            var uploadInitialLayout = PrepareTextureUploadTransferLayout(image, initialLayout);
            if (uploadInitialLayout != ImageLayout.TransferDstOptimal)
            {
                TransitionImageLayout(commandBuffer, image, uploadInitialLayout, ImageLayout.TransferDstOptimal);
            }

            var region = new BufferImageCopy
            {
                BufferOffset = stagingOffset,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                ImageOffset = new Offset3D(update.X, update.Y, 0),
                ImageExtent = new Extent3D((uint)regionWidth, (uint)regionHeight, 1),
            };

            RecordUploadTextureCopyProfile(1);
            _vk.CmdCopyBufferToImage(commandBuffer, _stagingBuffer, image, ImageLayout.TransferDstOptimal, 1, &region);
            CompleteTextureUploadTransferLayout(commandBuffer, image);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

    private static nuint GetExpectedTextureDataSize(UiTextureFormat format, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var bytesPerPixel = format switch
        {
            UiTextureFormat.Rgba8Unorm => 4,
            UiTextureFormat.Rgba8Srgb => 4,
            _ => throw new InvalidOperationException($"Unsupported texture format: {format}.")
        };

        return (nuint)width * (nuint)height * (nuint)bytesPerPixel;
    }

    private uint AllocateBindlessTextureSlot(ImageView view, bool isFontTexture)
    {
        if (!_freeBindlessTextureSlots.TryPop(out var slot))
        {
            if (_nextBindlessTextureSlot >= BindlessTextureCapacity)
            {
                throw new InvalidOperationException(
                    $"Bindless texture capacity exceeded ({BindlessTextureCapacity} slots).");
            }

            slot = _nextBindlessTextureSlot++;
        }

        WriteBindlessTextureSlot(slot, view, isFontTexture);
        return slot;
    }

    private void WriteBindlessTextureSlot(uint slot, ImageView view, bool isFontTexture)
    {
        var imageInfo = new DescriptorImageInfo
        {
            Sampler = isFontTexture ? _fontSampler : _imageSampler,
            ImageView = view,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _bindlessTextureSet,
            DstBinding = 0,
            DstArrayElement = slot,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo,
        };

        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }
}
