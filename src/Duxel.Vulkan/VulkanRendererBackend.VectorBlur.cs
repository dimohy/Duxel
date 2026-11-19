using System.Runtime.InteropServices;
using System.Numerics;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    [StructLayout(LayoutKind.Sequential)]
    private struct VectorBlurPush
    {
        public ulong Source, Destination;
        public uint Width, Height, Mode, LinearRgb;
        public float X, Y;
        public uint Radius;
        public int Left, Top, Right, Bottom;
    }
    private readonly record struct VectorBlurSupport(int Left, int Top, int Right, int Bottom);
    private Pipeline _vectorBlurPipeline;
    private PipelineLayout _vectorBlurLayout;

    private void EnsureVectorBlurPipeline()
    {
        if (_vectorBlurPipeline.Handle != 0)
            return;
        if (Marshal.OffsetOf<VectorBlurPush>(nameof(VectorBlurPush.Left)).ToInt32() != 44 || sizeof(VectorBlurPush) != 64)
            throw new InvalidOperationException("GPU_VECTOR_BLUR_ABI_MISMATCH");
        var range = new PushConstantRange { StageFlags = ShaderStageFlags.ComputeBit, Size = (uint)sizeof(VectorBlurPush) };
        var info = new PipelineLayoutCreateInfo { SType = StructureType.PipelineLayoutCreateInfo, PushConstantRangeCount = 1, PPushConstantRanges = &range };
        PipelineLayout layout;
        Check(_vk.CreatePipelineLayout(_device, &info, null, &layout));
        _vectorBlurLayout = layout;
        var shader = CreateShaderModule(LoadEmbeddedShader("Duxel.Vulkan.Shaders.vector_blur.comp.spv"));
        var entry = VulkanMarshaling.StringToPtr("main");
        try
        {
            var pipelineInfo = new ComputePipelineCreateInfo
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Layout = layout,
                BasePipelineIndex = -1,
                Stage = new() { SType = StructureType.PipelineShaderStageCreateInfo, Stage = ShaderStageFlags.ComputeBit, Module = shader, PName = entry }
            };
            Pipeline pipeline;
            Check(_vk.CreateComputePipelines(_device, _pipelineCache, 1, &pipelineInfo, null, &pipeline));
            _vectorBlurPipeline = pipeline;
        }
        finally { VulkanMarshaling.Free((nint)entry); _vk.DestroyShaderModule(_device, shader, null); }
    }
    private void RecordVectorBlur(CommandBuffer command, VectorSceneOutput output)
    {
        var filter = output.Filter!;
        var push = new VectorBlurPush { Width = (uint)output.Width, Height = (uint)output.Height, LinearRgb = filter.LinearRgb ? 1u : 0u };
        var scale = VectorSceneScale(output.Scene, output.Width, output.Height, output.Camera);
        var horizontal = new Vector2(filter.Horizontal.X, filter.Horizontal.Y) * scale;
        var vertical = new Vector2(filter.Vertical.X, filter.Vertical.Y) * scale;
        var horizontalFirst = horizontal.X != 0 && horizontal.Y == 0 && vertical.X == 0 && vertical.Y != 0;
        var verticalFirst = horizontal.X == 0 && horizontal.Y != 0 && vertical.X != 0 && vertical.Y == 0;
        var bounded = (horizontalFirst || verticalFirst) && (output.Background.Rgba >> 24) == 0;
        var support = bounded ? FindVectorBlurSupport(output, scale) : new(0, 0, output.Width, output.Height);
        _vk.CmdBindPipeline(command, PipelineBindPoint.Compute, _vectorBlurPipeline);
        void Dispatch(uint mode, VectorBuffer source, VectorBuffer destination, float x = 0, float y = 0)
        {
            VectorSceneBarrier(command, PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit);
            push.Mode = mode;
            push.Source = source.Address;
            push.Destination = destination.Address;
            push.X = x;
            push.Y = y;
            push.Radius = (uint)MathF.Ceiling(3 * MathF.Sqrt(x * x + y * y));
            push.Left = support.Left;
            push.Top = support.Top;
            push.Right = support.Right;
            push.Bottom = support.Bottom;
            var parameters = push;
            _vk.CmdPushConstants(command, _vectorBlurLayout, ShaderStageFlags.ComputeBit, 0, (uint)sizeof(VectorBlurPush), &parameters);
            _vk.CmdDispatch(command, ((uint)output.Width + 7) / 8, ((uint)output.Height + 7) / 8, 1);
        }
        Dispatch(0, output.Pixels, output.BlurA);
        Dispatch(1, output.BlurA, output.BlurB, horizontal.X, horizontal.Y);
        if (bounded)
        {
            var radius = (int)push.Radius;
            support = horizontalFirst
                ? support with { Left = Math.Max(0, support.Left - radius), Right = Math.Min(output.Width, support.Right + radius) }
                : support with { Top = Math.Max(0, support.Top - radius), Bottom = Math.Min(output.Height, support.Bottom + radius) };
        }
        Dispatch(1, output.BlurB, output.BlurA, vertical.X, vertical.Y);
        Dispatch(2, output.BlurA, output.Pixels);
    }

    private VectorBlurSupport FindVectorBlurSupport(VectorSceneOutput output, Vector2 scale)
    {
        var scene = output.Scene;
        var resource = _vectorScenes[scene];
        if (resource.BlurPaintBounds is null)
        {
            var data = scene.Data.Span;
            var minimum = new Vector2(float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity);
            for (var command = 0; command < scene.CommandCount * 6; command += 6)
            {
                if (data[command + 5].W != 0)
                    continue;
                // Only drawing commands introduce color. Structural commands operate on
                // that already painted content, including draws of child filter outputs.
                var bounds = data[command];
                var a = new Vector2(bounds.X, bounds.Y);
                var b = new Vector2(bounds.Z, bounds.W);
                minimum = Vector2.Min(minimum, Vector2.Min(a, b));
                maximum = Vector2.Max(maximum, Vector2.Max(a, b));
            }
            resource.BlurPaintBounds = new Vector4(minimum.X, minimum.Y, maximum.X, maximum.Y);
        }
        var painted = resource.BlurPaintBounds.Value;
        if (float.IsPositiveInfinity(painted.X))
            return default;

        var full = output.Camera?.FullPixelSize ?? new UiVector2(output.Width, output.Height);
        var offset = output.Camera?.PixelOrigin ?? default;
        var (left, right) = CoordinateRange(painted.X, painted.Z, scale.X, scene.Size.X, full.X, offset.X, output.Width);
        var (top, bottom) = CoordinateRange(painted.Y, painted.W, scale.Y, scene.Size.Y, full.Y, offset.Y, output.Height);
        return new(left, top, right, bottom);

        static (int Start, int End) CoordinateRange(float min, float max, float scale, float size, float full, float offset, int length)
        {
            var letterbox = (full - (double)size * scale) * .5;
            var low = (double)min * scale + letterbox - offset;
            var high = (double)max * scale + letterbox - offset;
            // The vector shader allows one pixel outside the draw bounds. Include
            // that fringe, pixel centers, and error in its float camera arithmetic.
            var guard = 3 + 2e-6 * (Math.Max(Math.Abs(low), Math.Abs(high)) + Math.Abs(offset) + Math.Abs(letterbox) + full + length);
            return ((int)Math.Clamp(Math.Floor(low - guard), 0, length),
                (int)Math.Clamp(Math.Ceiling(high + guard), 0, length));
        }
    }
    private void DestroyVectorBlurPipeline()
    {
        if (_vectorBlurPipeline.Handle != 0)
            _vk.DestroyPipeline(_device, _vectorBlurPipeline, null);
        if (_vectorBlurLayout.Handle != 0)
            _vk.DestroyPipelineLayout(_device, _vectorBlurLayout, null);
        _vectorBlurPipeline = default;
        _vectorBlurLayout = default;
    }
}
