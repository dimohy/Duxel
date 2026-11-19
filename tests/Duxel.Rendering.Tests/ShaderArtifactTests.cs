using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Duxel.Vulkan;

internal static class ShaderArtifactTests
{
    internal static void Run(Action<bool, string> check, [CallerFilePath] string sourceFile = "")
    {
        var path = Environment.GetEnvironmentVariable("DUXEL_VALIDATION_FRAGMENT_SHADER")
            ?? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!,
                "../../src/Duxel.Vulkan/Shaders/imgui.frag.spv"));
        var expected = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        check(VulkanRendererBackend.ValidationFragmentShaderSha256 == expected,
            "actual fragment shader matches the declared artifact before GPU initialization");
        var particlePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!,
            "../../src/Duxel.Vulkan/Shaders/particles.comp.spv"));
        check(VulkanRendererBackend.ValidationParticleShaderSha256 == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(particlePath))),
            "actual particle compute shader matches the declared artifact before GPU initialization");
    }
}
