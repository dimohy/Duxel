using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly struct CommandClassification
    {
        public readonly bool IsTriangleCommand;
        public readonly bool IsPrimitiveCommand;
        public readonly bool TriangleUsesColorPipeline;
        public readonly bool TriangleUsesTexture;
        public readonly bool PrimitiveUsesTexture;
        public readonly bool StaticPrimitiveUsesTriangleGeometry;
        public readonly bool CommandNeedsTexture;
        public readonly bool IsFontCommand;

        public CommandClassification(
            bool isTriangleCommand,
            bool isPrimitiveCommand,
            bool triangleUsesColorPipeline,
            bool triangleUsesTexture,
            bool primitiveUsesTexture,
            bool staticPrimitiveUsesTriangleGeometry,
            bool commandNeedsTexture,
            bool isFontCommand)
        {
            IsTriangleCommand = isTriangleCommand;
            IsPrimitiveCommand = isPrimitiveCommand;
            TriangleUsesColorPipeline = triangleUsesColorPipeline;
            TriangleUsesTexture = triangleUsesTexture;
            PrimitiveUsesTexture = primitiveUsesTexture;
            StaticPrimitiveUsesTriangleGeometry = staticPrimitiveUsesTriangleGeometry;
            CommandNeedsTexture = commandNeedsTexture;
            IsFontCommand = isFontCommand;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CommandClassification ClassifyCommand(
        in UiDrawCommand command,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding)
    {
        var isTriangleCommand = command.Kind is UiDrawCommandKind.Triangles;
        var isPrimitiveCommand = !isTriangleCommand;
        var triangleUsesColorPipeline = isTriangleCommand
            && _triangleColorPipelineEnabled
            && IsWhiteTextureId(command.TextureId);
        var triangleUsesTexture = isTriangleCommand && !triangleUsesColorPipeline;
        var primitiveUsesTexture = isPrimitiveCommand && !IsWhiteTextureId(command.TextureId);
        var staticPrimitiveUsesTriangleGeometry = hasStaticBinding
            && isPrimitiveCommand
            && staticBinding.HasExpandedPrimitiveGeometry;
        var commandNeedsTexture = triangleUsesTexture || primitiveUsesTexture;
        var isFontCommand = triangleUsesTexture && IsFontTextureId(command.TextureId);

        return new CommandClassification(
            isTriangleCommand,
            isPrimitiveCommand,
            triangleUsesColorPipeline,
            triangleUsesTexture,
            primitiveUsesTexture,
            staticPrimitiveUsesTriangleGeometry,
            commandNeedsTexture,
            isFontCommand);
    }
}
