#if DUXEL_LINUX_SHADER_ARCHIVE
using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private static partial class CompactShaderArchive
    {
        private const string ResourceName = "Duxel.Vulkan.Shaders.br";
        private const int BrotliSuccess = 1;

        internal static readonly Dictionary<string, byte[]> Shaders = Read();

        private static Dictionary<string, byte[]> Read()
        {
            using var resource = typeof(CompactShaderArchive).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded shader archive not found: {ResourceName}.");
            using var input = new BinaryReader(resource);
            var size = input.ReadInt32();
            if (size <= 0)
                throw new InvalidDataException("Invalid shader archive size.");

            var compressed = input.ReadBytes(checked((int)(resource.Length - resource.Position)));
            var contents = new byte[size];
            var decodedLength = (nuint)size;
            fixed (byte* source = compressed, destination = contents)
            {
                if (Decode((nuint)compressed.Length, source, ref decodedLength, destination) != BrotliSuccess
                    || decodedLength != (nuint)size)
                    throw new InvalidDataException("Could not decompress the embedded shader archive.");
            }

            using var data = new MemoryStream(contents, writable: false);
            using var entries = new BinaryReader(data);
            var count = entries.ReadInt32();
            if (count <= 0 || count > data.Length / 6)
                throw new InvalidDataException("Invalid shader archive entry count.");

            var shaders = new Dictionary<string, byte[]>(count, StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                var name = entries.ReadString();
                var length = entries.ReadInt32();
                if (length <= 0 || length > data.Length - data.Position)
                    throw new InvalidDataException($"Invalid shader size: {name}.");
                if (!shaders.TryAdd(name, entries.ReadBytes(length)))
                    throw new InvalidDataException($"Duplicate embedded shader: {name}.");
            }

            if (data.Position != data.Length)
                throw new InvalidDataException("Unexpected data in the shader archive.");
            return shaders;
        }

        [LibraryImport("libbrotlidec.so.1", EntryPoint = "BrotliDecoderDecompress")]
        private static partial int Decode(nuint encodedSize, byte* encoded, ref nuint decodedSize, byte* decoded);
    }
}
#endif
