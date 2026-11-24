using System.Buffers.Binary;
using System.Security.Cryptography;
using Duxel.Vulkan;

internal static class VectorSceneShaderContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        byte[] Shader(uint version = VectorSceneShaderContract.Version, uint specId = 1)
        {
            uint[] words = [0x07230203, 0x00010500, 0, 20, 0,
                4u << 16 | 71, 10, 1, specId, 4u << 16 | 21, 1, 32, 0,
                4u << 16 | 50, 1, 10, version];
            var bytes = new byte[words.Length * 4];
            for (var i = 0; i < words.Length; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
            return bytes;
        }
        bool Accepts(byte[] bytes)
        {
            try
            {
                VectorSceneShaderContract.Validate(bytes);
                return true;
            }
            catch (InvalidDataException) { return false; }
        }
        check(Accepts(Shader()), "Vector shader protocol / current unsigned32 marker is accepted");
        check(Enumerable.Range(1, (int)VectorSceneShaderContract.Version - 1).All(version => !Accepts(Shader((uint)version)))
            && !Accepts(Shader(VectorSceneShaderContract.Version + 1)),
            "Vector shader protocol / old and unknown versions fail before module creation");
        check(!Accepts(Shader(specId: 0)), "Vector shader protocol / stack-capacity specialization cannot masquerade as the protocol marker");
        var truncated = Shader()[..^1];
        check(!Accepts(truncated), "Vector shader protocol / truncated words reject without reading outside the artifact");
        var invalid = Shader();
        BinaryPrimitives.WriteUInt32LittleEndian(invalid.AsSpan(20), 0);
        check(!Accepts(invalid), "Vector shader protocol / zero-sized instructions reject before scanning");
        var legacy = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "VectorSceneShader", "legacy.spv"));
        if (Convert.ToHexString(SHA256.HashData(legacy)) != "B4D2E2A4F703C15203952469CC79516562F77DD092BC05924B26E17D23AB0B3D")
            throw new InvalidDataException("The previous shader regression fixture has changed.");
        check(!Accepts(legacy), "Vector shader protocol / the actual previous shader that produced the reported triangle is rejected");
        var protocol2 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "VectorSceneShader", "protocol2.spv"));
        if (Convert.ToHexString(SHA256.HashData(protocol2)) != "BD2658CC95035A9237007656309053FC085CD064DDA90AB2007C5AAA720477E3")
            throw new InvalidDataException("The previous push-constant ABI fixture changed.");
        check(!Accepts(protocol2), "Vector shader protocol / the actual previous push-constant ABI is rejected before module creation");
        var protocol3 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "VectorSceneShader", "protocol3.spv"));
        if (Convert.ToHexString(SHA256.HashData(protocol3)) != "8634590FDC87391C847F3FA3D5755C3C6AEDA16BB274270D21B87E8FE9A71312")
            throw new InvalidDataException("The previous curve-payload shader fixture changed.");
        check(!Accepts(protocol3), "Vector shader protocol / the actual previous curve-payload shader is rejected before module creation");
        var oldStroke = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "VectorSceneShader", "stroke-legacy.spv"));
        if (Convert.ToHexString(SHA256.HashData(oldStroke)) != "50BADA304E8DD095FE4A04382BC58E8F45077FC51FA1FE3A4E2AC5961361EDC5")
            throw new InvalidDataException("The previous stroke shader fixture changed.");
        check(!Accepts(oldStroke), "Vector shader protocol / the actual previous stroke shader rejects the rational curve payload before module creation");
    }
}
