using System.Buffers.Binary;

namespace Duxel.Vulkan;

/// <summary>Checks the retained-scene protocol before a compiled shader reaches Vulkan.</summary>
internal static class VectorSceneShaderContract
{
    internal const uint Version = 6;
    private static uint Word(ReadOnlySpan<byte> bytes, int index) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[(index * 4)..]);

    internal static void Validate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 20 || bytes.Length % 4 != 0 || Word(bytes, 0) != 0x07230203)
            throw new InvalidDataException("VECTOR_SCENE_SHADER_INVALID_SPIRV");
        var length = bytes.Length / 4;
        uint marker = 0;
        for (var at = 5; at < length;)
        {
            var instruction = Word(bytes, at);
            var count = (int)(instruction >> 16);
            if (count == 0 || count > length - at)
                throw new InvalidDataException("VECTOR_SCENE_SHADER_INVALID_INSTRUCTION");
            // OpDecorate target SpecId 1 identifies the protocol specialization constant.
            if ((instruction & 0xffff) == 71 && count == 4 && Word(bytes, at + 2) == 1 && Word(bytes, at + 3) == 1)
            {
                if (marker != 0 || Word(bytes, at + 1) == 0)
                    throw new InvalidDataException("VECTOR_SCENE_SHADER_AMBIGUOUS_PROTOCOL");
                marker = Word(bytes, at + 1);
            }
            at += count;
        }
        uint type = 0;
        if (marker != 0)
            for (var at = 5; at < length; at += (int)(Word(bytes, at) >> 16))
                if ((Word(bytes, at) & 0xffff) == 50 && Word(bytes, at) >> 16 == 4 && Word(bytes, at + 2) == marker)
                {
                    if (type != 0 || Word(bytes, at + 3) != Version)
                        throw new InvalidDataException("VECTOR_SCENE_SHADER_PROTOCOL_MISMATCH");
                    type = Word(bytes, at + 1);
                }
        if (type != 0)
            for (var at = 5; at < length; at += (int)(Word(bytes, at) >> 16))
                if ((Word(bytes, at) & 0xffff) == 21 && Word(bytes, at) >> 16 == 4 &&
                    Word(bytes, at + 1) == type && Word(bytes, at + 2) == 32 && Word(bytes, at + 3) == 0)
                    return;
        throw new InvalidDataException("VECTOR_SCENE_SHADER_PROTOCOL_MISSING: rebuild the matching vector_scene.comp.spv artifact.");
    }
}
