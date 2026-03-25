using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

public sealed unsafe partial class Vk
{
    public const uint False = 0;
    public const uint SubpassExternal = ~0u;
    public const uint QueueFamilyIgnored = ~0u;
    public static Version32 Version12 => new(1, 2, 0);

    public static Vk GetApi() => new();

    public nint GetInstanceProcAddr(Instance instance, string name)
    {
        var namePtr = VulkanMarshaling.StringToPtr(name);
        try
        {
            return vkGetInstanceProcAddr(instance, namePtr);
        }
        finally
        {
            VulkanMarshaling.Free((nint)namePtr);
        }
    }

    public nint GetDeviceProcAddr(Device device, string name)
    {
        var namePtr = VulkanMarshaling.StringToPtr(name);
        try
        {
            return vkGetDeviceProcAddr(device, namePtr);
        }
        finally
        {
            VulkanMarshaling.Free((nint)namePtr);
        }
    }

    public bool IsInstanceExtensionPresent(string extensionName)
    {
        uint count = 0;
        _ = vkEnumerateInstanceExtensionProperties((byte*)0, &count, null);
        if (count is 0)
        {
            return false;
        }

        var properties = stackalloc ExtensionProperties[(int)count];
        _ = vkEnumerateInstanceExtensionProperties((byte*)0, &count, properties);
        for (var i = 0; i < count; i++)
        {
            if (properties[i].GetExtensionName().Equals(extensionName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsDeviceExtensionPresent(Instance instance, string extensionName)
    {
        uint deviceCount = 0;
        _ = vkEnumeratePhysicalDevices(instance, &deviceCount, null);
        if (deviceCount is 0)
        {
            return false;
        }

        var devices = stackalloc PhysicalDevice[(int)deviceCount];
        _ = vkEnumeratePhysicalDevices(instance, &deviceCount, devices);
        for (var i = 0; i < deviceCount; i++)
        {
            if (IsDeviceExtensionPresent(devices[i], extensionName))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsDeviceExtensionPresent(PhysicalDevice device, string extensionName)
    {
        uint count = 0;
        _ = vkEnumerateDeviceExtensionProperties(device, (byte*)0, &count, null);
        if (count is 0)
        {
            return false;
        }

        var properties = stackalloc ExtensionProperties[(int)count];
        _ = vkEnumerateDeviceExtensionProperties(device, (byte*)0, &count, properties);
        for (var i = 0; i < count; i++)
        {
            if (properties[i].GetExtensionName().Equals(extensionName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public Result CreateInstance(InstanceCreateInfo* createInfo, AllocationCallbacks* allocator, Instance* instance) => vkCreateInstance(createInfo, allocator, instance);
    public void DestroyInstance(Instance instance, AllocationCallbacks* allocator) => vkDestroyInstance(instance, allocator);
    public Result EnumeratePhysicalDevices(Instance instance, uint* physicalDeviceCount, PhysicalDevice* physicalDevices) => vkEnumeratePhysicalDevices(instance, physicalDeviceCount, physicalDevices);
    public void GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice physicalDevice, uint* queueFamilyPropertyCount, QueueFamilyProperties* queueFamilyProperties) => vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, queueFamilyPropertyCount, queueFamilyProperties);
    public void GetPhysicalDeviceProperties(PhysicalDevice physicalDevice, out PhysicalDeviceProperties properties) => vkGetPhysicalDeviceProperties(physicalDevice, out properties);
    public void GetPhysicalDeviceMemoryProperties(PhysicalDevice physicalDevice, out PhysicalDeviceMemoryProperties properties) => vkGetPhysicalDeviceMemoryProperties(physicalDevice, out properties);
    public Result CreateDevice(PhysicalDevice physicalDevice, DeviceCreateInfo* createInfo, AllocationCallbacks* allocator, Device* device) => vkCreateDevice(physicalDevice, createInfo, allocator, device);
    public void DestroyDevice(Device device, AllocationCallbacks* allocator) => vkDestroyDevice(device, allocator);
    public void GetDeviceQueue(Device device, uint queueFamilyIndex, uint queueIndex, out Queue queue) => vkGetDeviceQueue(device, queueFamilyIndex, queueIndex, out queue);
    public Result DeviceWaitIdle(Device device) => vkDeviceWaitIdle(device);
    public Result CreateBuffer(Device device, BufferCreateInfo* createInfo, AllocationCallbacks* allocator, Buffer* buffer) => vkCreateBuffer(device, createInfo, allocator, buffer);
    public void DestroyBuffer(Device device, Buffer buffer, AllocationCallbacks* allocator) => vkDestroyBuffer(device, buffer, allocator);
    public void GetBufferMemoryRequirements(Device device, Buffer buffer, out MemoryRequirements memoryRequirements) => vkGetBufferMemoryRequirements(device, buffer, out memoryRequirements);
    public Result AllocateMemory(Device device, MemoryAllocateInfo* allocateInfo, AllocationCallbacks* allocator, DeviceMemory* memory) => vkAllocateMemory(device, allocateInfo, allocator, memory);
    public void FreeMemory(Device device, DeviceMemory memory, AllocationCallbacks* allocator) => vkFreeMemory(device, memory, allocator);
    public Result BindBufferMemory(Device device, Buffer buffer, DeviceMemory memory, ulong memoryOffset) => vkBindBufferMemory(device, buffer, memory, memoryOffset);
    public Result CreateImage(Device device, ImageCreateInfo* createInfo, AllocationCallbacks* allocator, Image* image) => vkCreateImage(device, createInfo, allocator, image);
    public void DestroyImage(Device device, Image image, AllocationCallbacks* allocator) => vkDestroyImage(device, image, allocator);
    public void GetImageMemoryRequirements(Device device, Image image, out MemoryRequirements memoryRequirements) => vkGetImageMemoryRequirements(device, image, out memoryRequirements);
    public Result BindImageMemory(Device device, Image image, DeviceMemory memory, ulong memoryOffset) => vkBindImageMemory(device, image, memory, memoryOffset);
    public Result CreateImageView(Device device, ImageViewCreateInfo* createInfo, AllocationCallbacks* allocator, ImageView* view) => vkCreateImageView(device, createInfo, allocator, view);
    public void DestroyImageView(Device device, ImageView imageView, AllocationCallbacks* allocator) => vkDestroyImageView(device, imageView, allocator);
    public Result CreateRenderPass(Device device, RenderPassCreateInfo* createInfo, AllocationCallbacks* allocator, RenderPass* renderPass) => vkCreateRenderPass(device, createInfo, allocator, renderPass);
    public void DestroyRenderPass(Device device, RenderPass renderPass, AllocationCallbacks* allocator) => vkDestroyRenderPass(device, renderPass, allocator);
    public Result CreateDescriptorSetLayout(Device device, DescriptorSetLayoutCreateInfo* createInfo, AllocationCallbacks* allocator, DescriptorSetLayout* setLayout) => vkCreateDescriptorSetLayout(device, createInfo, allocator, setLayout);
    public void DestroyDescriptorSetLayout(Device device, DescriptorSetLayout descriptorSetLayout, AllocationCallbacks* allocator) => vkDestroyDescriptorSetLayout(device, descriptorSetLayout, allocator);
    public Result CreatePipelineLayout(Device device, PipelineLayoutCreateInfo* createInfo, AllocationCallbacks* allocator, PipelineLayout* pipelineLayout) => vkCreatePipelineLayout(device, createInfo, allocator, pipelineLayout);
    public void DestroyPipelineLayout(Device device, PipelineLayout pipelineLayout, AllocationCallbacks* allocator) => vkDestroyPipelineLayout(device, pipelineLayout, allocator);
    public Result CreateSampler(Device device, SamplerCreateInfo* createInfo, AllocationCallbacks* allocator, Sampler* sampler) => vkCreateSampler(device, createInfo, allocator, sampler);
    public void DestroySampler(Device device, Sampler sampler, AllocationCallbacks* allocator) => vkDestroySampler(device, sampler, allocator);
    public Result CreateShaderModule(Device device, ShaderModuleCreateInfo* createInfo, AllocationCallbacks* allocator, ShaderModule* shaderModule) => vkCreateShaderModule(device, createInfo, allocator, shaderModule);
    public void DestroyShaderModule(Device device, ShaderModule shaderModule, AllocationCallbacks* allocator) => vkDestroyShaderModule(device, shaderModule, allocator);
    public Result CreatePipelineCache(Device device, PipelineCacheCreateInfo* createInfo, AllocationCallbacks* allocator, PipelineCache* pipelineCache) => vkCreatePipelineCache(device, createInfo, allocator, pipelineCache);
    public void DestroyPipelineCache(Device device, PipelineCache pipelineCache, AllocationCallbacks* allocator) => vkDestroyPipelineCache(device, pipelineCache, allocator);
    public Result GetPipelineCacheData(Device device, PipelineCache pipelineCache, nuint* dataSize, byte* data) => vkGetPipelineCacheData(device, pipelineCache, dataSize, data);
    public Result CreateGraphicsPipelines(Device device, PipelineCache pipelineCache, uint createInfoCount, GraphicsPipelineCreateInfo* createInfos, AllocationCallbacks* allocator, Pipeline* pipelines) => vkCreateGraphicsPipelines(device, pipelineCache, createInfoCount, createInfos, allocator, pipelines);
    public void DestroyPipeline(Device device, Pipeline pipeline, AllocationCallbacks* allocator) => vkDestroyPipeline(device, pipeline, allocator);
    public Result CreateDescriptorPool(Device device, DescriptorPoolCreateInfo* createInfo, AllocationCallbacks* allocator, DescriptorPool* descriptorPool) => vkCreateDescriptorPool(device, createInfo, allocator, descriptorPool);
    public void DestroyDescriptorPool(Device device, DescriptorPool descriptorPool, AllocationCallbacks* allocator) => vkDestroyDescriptorPool(device, descriptorPool, allocator);
    public Result AllocateDescriptorSets(Device device, DescriptorSetAllocateInfo* allocateInfo, DescriptorSet* descriptorSets) => vkAllocateDescriptorSets(device, allocateInfo, descriptorSets);
    public Result FreeDescriptorSets(Device device, DescriptorPool descriptorPool, uint descriptorSetCount, DescriptorSet* descriptorSets) => vkFreeDescriptorSets(device, descriptorPool, descriptorSetCount, descriptorSets);
    public void UpdateDescriptorSets(Device device, uint descriptorWriteCount, WriteDescriptorSet* descriptorWrites, uint descriptorCopyCount, void* descriptorCopies) => vkUpdateDescriptorSets(device, descriptorWriteCount, descriptorWrites, descriptorCopyCount, descriptorCopies);
    public Result CreateFramebuffer(Device device, FramebufferCreateInfo* createInfo, AllocationCallbacks* allocator, Framebuffer* framebuffer) => vkCreateFramebuffer(device, createInfo, allocator, framebuffer);
    public void DestroyFramebuffer(Device device, Framebuffer framebuffer, AllocationCallbacks* allocator) => vkDestroyFramebuffer(device, framebuffer, allocator);
    public Result CreateCommandPool(Device device, CommandPoolCreateInfo* createInfo, AllocationCallbacks* allocator, CommandPool* commandPool) => vkCreateCommandPool(device, createInfo, allocator, commandPool);
    public void DestroyCommandPool(Device device, CommandPool commandPool, AllocationCallbacks* allocator) => vkDestroyCommandPool(device, commandPool, allocator);
    public Result ResetCommandPool(Device device, CommandPool commandPool, uint flags) => vkResetCommandPool(device, commandPool, flags);
    public Result AllocateCommandBuffers(Device device, CommandBufferAllocateInfo* allocateInfo, CommandBuffer* commandBuffers) => vkAllocateCommandBuffers(device, allocateInfo, commandBuffers);
    public void FreeCommandBuffers(Device device, CommandPool commandPool, uint commandBufferCount, CommandBuffer* commandBuffers) => vkFreeCommandBuffers(device, commandPool, commandBufferCount, commandBuffers);
    public Result ResetCommandBuffer(CommandBuffer commandBuffer, uint flags) => vkResetCommandBuffer(commandBuffer, flags);
    public Result BeginCommandBuffer(CommandBuffer commandBuffer, CommandBufferBeginInfo* beginInfo) => vkBeginCommandBuffer(commandBuffer, beginInfo);
    public Result EndCommandBuffer(CommandBuffer commandBuffer) => vkEndCommandBuffer(commandBuffer);
    public Result CreateFence(Device device, FenceCreateInfo* createInfo, AllocationCallbacks* allocator, Fence* fence) => vkCreateFence(device, createInfo, allocator, fence);
    public void DestroyFence(Device device, Fence fence, AllocationCallbacks* allocator) => vkDestroyFence(device, fence, allocator);
    public Result ResetFences(Device device, uint fenceCount, Fence* fences) => vkResetFences(device, fenceCount, fences);
    public Result WaitForFences(Device device, uint fenceCount, Fence* fences, bool waitAll, ulong timeout) => vkWaitForFences(device, fenceCount, fences, waitAll ? TrueValue : False, timeout);
    public Result CreateSemaphore(Device device, SemaphoreCreateInfo* createInfo, AllocationCallbacks* allocator, Semaphore* semaphore) => vkCreateSemaphore(device, createInfo, allocator, semaphore);
    public void DestroySemaphore(Device device, Semaphore semaphore, AllocationCallbacks* allocator) => vkDestroySemaphore(device, semaphore, allocator);
    public Result QueueSubmit(Queue queue, uint submitCount, SubmitInfo* submits, Fence fence) => vkQueueSubmit(queue, submitCount, submits, fence);
    public Result MapMemory(Device device, DeviceMemory memory, ulong offset, nuint size, uint flags, void** data) => vkMapMemory(device, memory, offset, size, flags, data);
    public void UnmapMemory(Device device, DeviceMemory memory) => vkUnmapMemory(device, memory);
    public void CmdBindPipeline(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, Pipeline pipeline) => vkCmdBindPipeline(commandBuffer, pipelineBindPoint, pipeline);
    public void CmdSetViewport(CommandBuffer commandBuffer, uint firstViewport, uint viewportCount, Viewport* viewports) => vkCmdSetViewport(commandBuffer, firstViewport, viewportCount, viewports);
    public void CmdSetScissor(CommandBuffer commandBuffer, uint firstScissor, uint scissorCount, Rect2D* scissors) => vkCmdSetScissor(commandBuffer, firstScissor, scissorCount, scissors);
    public void CmdSetBlendConstants(CommandBuffer commandBuffer, float* blendConstants) => vkCmdSetBlendConstants(commandBuffer, blendConstants);
    public void CmdBindVertexBuffers(CommandBuffer commandBuffer, uint firstBinding, uint bindingCount, Buffer* buffers, ulong* offsets) => vkCmdBindVertexBuffers(commandBuffer, firstBinding, bindingCount, buffers, offsets);
    public void CmdBindIndexBuffer(CommandBuffer commandBuffer, Buffer buffer, ulong offset, IndexType indexType) => vkCmdBindIndexBuffer(commandBuffer, buffer, offset, indexType);
    public void CmdBindDescriptorSets(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, PipelineLayout layout, uint firstSet, uint descriptorSetCount, DescriptorSet* descriptorSets, uint dynamicOffsetCount, uint* dynamicOffsets) => vkCmdBindDescriptorSets(commandBuffer, pipelineBindPoint, layout, firstSet, descriptorSetCount, descriptorSets, dynamicOffsetCount, dynamicOffsets);
    public void CmdPushConstants(CommandBuffer commandBuffer, PipelineLayout layout, ShaderStageFlags stageFlags, uint offset, uint size, void* values) => vkCmdPushConstants(commandBuffer, layout, stageFlags, offset, size, values);
    public void CmdDrawIndexed(CommandBuffer commandBuffer, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance) => vkCmdDrawIndexed(commandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    public void CmdBeginRenderPass(CommandBuffer commandBuffer, RenderPassBeginInfo* renderPassBegin, SubpassContents contents) => vkCmdBeginRenderPass(commandBuffer, renderPassBegin, contents);
    public void CmdEndRenderPass(CommandBuffer commandBuffer) => vkCmdEndRenderPass(commandBuffer);
    public void CmdClearAttachments(CommandBuffer commandBuffer, uint attachmentCount, ClearAttachment* attachments, uint rectCount, ClearRect* rects) => vkCmdClearAttachments(commandBuffer, attachmentCount, attachments, rectCount, rects);
    public void CmdCopyImage(CommandBuffer commandBuffer, Image srcImage, ImageLayout srcImageLayout, Image dstImage, ImageLayout dstImageLayout, uint regionCount, ImageCopy* regions) => vkCmdCopyImage(commandBuffer, srcImage, srcImageLayout, dstImage, dstImageLayout, regionCount, regions);
    public void CmdCopyImageToBuffer(CommandBuffer commandBuffer, Image srcImage, ImageLayout srcImageLayout, Buffer dstBuffer, uint regionCount, BufferImageCopy* regions) => vkCmdCopyImageToBuffer(commandBuffer, srcImage, srcImageLayout, dstBuffer, regionCount, regions);
    public void CmdCopyBufferToImage(CommandBuffer commandBuffer, Buffer srcBuffer, Image dstImage, ImageLayout dstImageLayout, uint regionCount, BufferImageCopy* regions) => vkCmdCopyBufferToImage(commandBuffer, srcBuffer, dstImage, dstImageLayout, regionCount, regions);
    public void CmdCopyBuffer(CommandBuffer commandBuffer, Buffer srcBuffer, Buffer dstBuffer, uint regionCount, BufferCopy* regions) => vkCmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, regionCount, regions);
    public void CmdPipelineBarrier(CommandBuffer commandBuffer, PipelineStageFlags srcStageMask, PipelineStageFlags dstStageMask, DependencyFlags dependencyFlags, uint memoryBarrierCount, void* memoryBarriers, uint bufferMemoryBarrierCount, void* bufferMemoryBarriers, uint imageMemoryBarrierCount, ImageMemoryBarrier* imageMemoryBarriers) => vkCmdPipelineBarrier(commandBuffer, srcStageMask, dstStageMask, dependencyFlags, memoryBarrierCount, memoryBarriers, bufferMemoryBarrierCount, bufferMemoryBarriers, imageMemoryBarrierCount, imageMemoryBarriers);

    private const uint TrueValue = 1;

    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetInstanceProcAddr")]
    private static partial nint vkGetInstanceProcAddr(Instance instance, byte* name);

    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetDeviceProcAddr")]
    private static partial nint vkGetDeviceProcAddr(Device device, byte* name);

    [LibraryImport("vulkan-1.dll", EntryPoint = "vkEnumerateInstanceExtensionProperties")]
    private static partial Result vkEnumerateInstanceExtensionProperties(byte* layerName, uint* propertyCount, ExtensionProperties* properties);

    [LibraryImport("vulkan-1.dll", EntryPoint = "vkEnumerateDeviceExtensionProperties")]
    private static partial Result vkEnumerateDeviceExtensionProperties(PhysicalDevice physicalDevice, byte* layerName, uint* propertyCount, ExtensionProperties* properties);

    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateInstance")]
    private static partial Result vkCreateInstance(InstanceCreateInfo* createInfo, AllocationCallbacks* allocator, Instance* instance);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyInstance")]
    private static partial void vkDestroyInstance(Instance instance, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkEnumeratePhysicalDevices")]
    private static partial Result vkEnumeratePhysicalDevices(Instance instance, uint* physicalDeviceCount, PhysicalDevice* physicalDevices);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetPhysicalDeviceQueueFamilyProperties")]
    private static partial void vkGetPhysicalDeviceQueueFamilyProperties(PhysicalDevice physicalDevice, uint* queueFamilyPropertyCount, QueueFamilyProperties* queueFamilyProperties);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetPhysicalDeviceProperties")]
    private static partial void vkGetPhysicalDeviceProperties(PhysicalDevice physicalDevice, out PhysicalDeviceProperties properties);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetPhysicalDeviceMemoryProperties")]
    private static partial void vkGetPhysicalDeviceMemoryProperties(PhysicalDevice physicalDevice, out PhysicalDeviceMemoryProperties properties);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateDevice")]
    private static partial Result vkCreateDevice(PhysicalDevice physicalDevice, DeviceCreateInfo* createInfo, AllocationCallbacks* allocator, Device* device);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyDevice")]
    private static partial void vkDestroyDevice(Device device, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetDeviceQueue")]
    private static partial void vkGetDeviceQueue(Device device, uint queueFamilyIndex, uint queueIndex, out Queue queue);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDeviceWaitIdle")]
    private static partial Result vkDeviceWaitIdle(Device device);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateBuffer")]
    private static partial Result vkCreateBuffer(Device device, BufferCreateInfo* createInfo, AllocationCallbacks* allocator, Buffer* buffer);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyBuffer")]
    private static partial void vkDestroyBuffer(Device device, Buffer buffer, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetBufferMemoryRequirements")]
    private static partial void vkGetBufferMemoryRequirements(Device device, Buffer buffer, out MemoryRequirements memoryRequirements);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkAllocateMemory")]
    private static partial Result vkAllocateMemory(Device device, MemoryAllocateInfo* allocateInfo, AllocationCallbacks* allocator, DeviceMemory* memory);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkFreeMemory")]
    private static partial void vkFreeMemory(Device device, DeviceMemory memory, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkBindBufferMemory")]
    private static partial Result vkBindBufferMemory(Device device, Buffer buffer, DeviceMemory memory, ulong memoryOffset);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateImage")]
    private static partial Result vkCreateImage(Device device, ImageCreateInfo* createInfo, AllocationCallbacks* allocator, Image* image);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyImage")]
    private static partial void vkDestroyImage(Device device, Image image, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetImageMemoryRequirements")]
    private static partial void vkGetImageMemoryRequirements(Device device, Image image, out MemoryRequirements memoryRequirements);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkBindImageMemory")]
    private static partial Result vkBindImageMemory(Device device, Image image, DeviceMemory memory, ulong memoryOffset);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateImageView")]
    private static partial Result vkCreateImageView(Device device, ImageViewCreateInfo* createInfo, AllocationCallbacks* allocator, ImageView* view);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyImageView")]
    private static partial void vkDestroyImageView(Device device, ImageView imageView, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateRenderPass")]
    private static partial Result vkCreateRenderPass(Device device, RenderPassCreateInfo* createInfo, AllocationCallbacks* allocator, RenderPass* renderPass);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyRenderPass")]
    private static partial void vkDestroyRenderPass(Device device, RenderPass renderPass, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateDescriptorSetLayout")]
    private static partial Result vkCreateDescriptorSetLayout(Device device, DescriptorSetLayoutCreateInfo* createInfo, AllocationCallbacks* allocator, DescriptorSetLayout* setLayout);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyDescriptorSetLayout")]
    private static partial void vkDestroyDescriptorSetLayout(Device device, DescriptorSetLayout descriptorSetLayout, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreatePipelineLayout")]
    private static partial Result vkCreatePipelineLayout(Device device, PipelineLayoutCreateInfo* createInfo, AllocationCallbacks* allocator, PipelineLayout* pipelineLayout);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyPipelineLayout")]
    private static partial void vkDestroyPipelineLayout(Device device, PipelineLayout pipelineLayout, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateSampler")]
    private static partial Result vkCreateSampler(Device device, SamplerCreateInfo* createInfo, AllocationCallbacks* allocator, Sampler* sampler);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroySampler")]
    private static partial void vkDestroySampler(Device device, Sampler sampler, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateShaderModule")]
    private static partial Result vkCreateShaderModule(Device device, ShaderModuleCreateInfo* createInfo, AllocationCallbacks* allocator, ShaderModule* shaderModule);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyShaderModule")]
    private static partial void vkDestroyShaderModule(Device device, ShaderModule shaderModule, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreatePipelineCache")]
    private static partial Result vkCreatePipelineCache(Device device, PipelineCacheCreateInfo* createInfo, AllocationCallbacks* allocator, PipelineCache* pipelineCache);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyPipelineCache")]
    private static partial void vkDestroyPipelineCache(Device device, PipelineCache pipelineCache, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkGetPipelineCacheData")]
    private static partial Result vkGetPipelineCacheData(Device device, PipelineCache pipelineCache, nuint* dataSize, byte* data);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateGraphicsPipelines")]
    private static partial Result vkCreateGraphicsPipelines(Device device, PipelineCache pipelineCache, uint createInfoCount, GraphicsPipelineCreateInfo* createInfos, AllocationCallbacks* allocator, Pipeline* pipelines);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyPipeline")]
    private static partial void vkDestroyPipeline(Device device, Pipeline pipeline, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateDescriptorPool")]
    private static partial Result vkCreateDescriptorPool(Device device, DescriptorPoolCreateInfo* createInfo, AllocationCallbacks* allocator, DescriptorPool* descriptorPool);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyDescriptorPool")]
    private static partial void vkDestroyDescriptorPool(Device device, DescriptorPool descriptorPool, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkAllocateDescriptorSets")]
    private static partial Result vkAllocateDescriptorSets(Device device, DescriptorSetAllocateInfo* allocateInfo, DescriptorSet* descriptorSets);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkFreeDescriptorSets")]
    private static partial Result vkFreeDescriptorSets(Device device, DescriptorPool descriptorPool, uint descriptorSetCount, DescriptorSet* descriptorSets);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkUpdateDescriptorSets")]
    private static partial void vkUpdateDescriptorSets(Device device, uint descriptorWriteCount, WriteDescriptorSet* descriptorWrites, uint descriptorCopyCount, void* descriptorCopies);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateFramebuffer")]
    private static partial Result vkCreateFramebuffer(Device device, FramebufferCreateInfo* createInfo, AllocationCallbacks* allocator, Framebuffer* framebuffer);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyFramebuffer")]
    private static partial void vkDestroyFramebuffer(Device device, Framebuffer framebuffer, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateCommandPool")]
    private static partial Result vkCreateCommandPool(Device device, CommandPoolCreateInfo* createInfo, AllocationCallbacks* allocator, CommandPool* commandPool);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyCommandPool")]
    private static partial void vkDestroyCommandPool(Device device, CommandPool commandPool, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkResetCommandPool")]
    private static partial Result vkResetCommandPool(Device device, CommandPool commandPool, uint flags);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkAllocateCommandBuffers")]
    private static partial Result vkAllocateCommandBuffers(Device device, CommandBufferAllocateInfo* allocateInfo, CommandBuffer* commandBuffers);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkFreeCommandBuffers")]
    private static partial void vkFreeCommandBuffers(Device device, CommandPool commandPool, uint commandBufferCount, CommandBuffer* commandBuffers);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkResetCommandBuffer")]
    private static partial Result vkResetCommandBuffer(CommandBuffer commandBuffer, uint flags);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkBeginCommandBuffer")]
    private static partial Result vkBeginCommandBuffer(CommandBuffer commandBuffer, CommandBufferBeginInfo* beginInfo);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkEndCommandBuffer")]
    private static partial Result vkEndCommandBuffer(CommandBuffer commandBuffer);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateFence")]
    private static partial Result vkCreateFence(Device device, FenceCreateInfo* createInfo, AllocationCallbacks* allocator, Fence* fence);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroyFence")]
    private static partial void vkDestroyFence(Device device, Fence fence, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkResetFences")]
    private static partial Result vkResetFences(Device device, uint fenceCount, Fence* fences);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkWaitForFences")]
    private static partial Result vkWaitForFences(Device device, uint fenceCount, Fence* fences, uint waitAll, ulong timeout);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCreateSemaphore")]
    private static partial Result vkCreateSemaphore(Device device, SemaphoreCreateInfo* createInfo, AllocationCallbacks* allocator, Semaphore* semaphore);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkDestroySemaphore")]
    private static partial void vkDestroySemaphore(Device device, Semaphore semaphore, AllocationCallbacks* allocator);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkQueueSubmit")]
    private static partial Result vkQueueSubmit(Queue queue, uint submitCount, SubmitInfo* submits, Fence fence);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkMapMemory")]
    private static partial Result vkMapMemory(Device device, DeviceMemory memory, ulong offset, nuint size, uint flags, void** data);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkUnmapMemory")]
    private static partial void vkUnmapMemory(Device device, DeviceMemory memory);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdBindPipeline")]
    private static partial void vkCmdBindPipeline(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, Pipeline pipeline);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdSetViewport")]
    private static partial void vkCmdSetViewport(CommandBuffer commandBuffer, uint firstViewport, uint viewportCount, Viewport* viewports);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdSetScissor")]
    private static partial void vkCmdSetScissor(CommandBuffer commandBuffer, uint firstScissor, uint scissorCount, Rect2D* scissors);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdSetBlendConstants")]
    private static partial void vkCmdSetBlendConstants(CommandBuffer commandBuffer, float* blendConstants);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdBindVertexBuffers")]
    private static partial void vkCmdBindVertexBuffers(CommandBuffer commandBuffer, uint firstBinding, uint bindingCount, Buffer* buffers, ulong* offsets);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdBindIndexBuffer")]
    private static partial void vkCmdBindIndexBuffer(CommandBuffer commandBuffer, Buffer buffer, ulong offset, IndexType indexType);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdBindDescriptorSets")]
    private static partial void vkCmdBindDescriptorSets(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, PipelineLayout layout, uint firstSet, uint descriptorSetCount, DescriptorSet* descriptorSets, uint dynamicOffsetCount, uint* dynamicOffsets);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdPushConstants")]
    private static partial void vkCmdPushConstants(CommandBuffer commandBuffer, PipelineLayout layout, ShaderStageFlags stageFlags, uint offset, uint size, void* values);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdDrawIndexed")]
    private static partial void vkCmdDrawIndexed(CommandBuffer commandBuffer, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdBeginRenderPass")]
    private static partial void vkCmdBeginRenderPass(CommandBuffer commandBuffer, RenderPassBeginInfo* renderPassBegin, SubpassContents contents);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdEndRenderPass")]
    private static partial void vkCmdEndRenderPass(CommandBuffer commandBuffer);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdClearAttachments")]
    private static partial void vkCmdClearAttachments(CommandBuffer commandBuffer, uint attachmentCount, ClearAttachment* attachments, uint rectCount, ClearRect* rects);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdCopyImage")]
    private static partial void vkCmdCopyImage(CommandBuffer commandBuffer, Image srcImage, ImageLayout srcImageLayout, Image dstImage, ImageLayout dstImageLayout, uint regionCount, ImageCopy* regions);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdCopyImageToBuffer")]
    private static partial void vkCmdCopyImageToBuffer(CommandBuffer commandBuffer, Image srcImage, ImageLayout srcImageLayout, Buffer dstBuffer, uint regionCount, BufferImageCopy* regions);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdCopyBufferToImage")]
    private static partial void vkCmdCopyBufferToImage(CommandBuffer commandBuffer, Buffer srcBuffer, Image dstImage, ImageLayout dstImageLayout, uint regionCount, BufferImageCopy* regions);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdCopyBuffer")]
    private static partial void vkCmdCopyBuffer(CommandBuffer commandBuffer, Buffer srcBuffer, Buffer dstBuffer, uint regionCount, BufferCopy* regions);
    [LibraryImport("vulkan-1.dll", EntryPoint = "vkCmdPipelineBarrier")]
    private static partial void vkCmdPipelineBarrier(CommandBuffer commandBuffer, PipelineStageFlags srcStageMask, PipelineStageFlags dstStageMask, DependencyFlags dependencyFlags, uint memoryBarrierCount, void* memoryBarriers, uint bufferMemoryBarrierCount, void* bufferMemoryBarriers, uint imageMemoryBarrierCount, ImageMemoryBarrier* imageMemoryBarriers);
}
