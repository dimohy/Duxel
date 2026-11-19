using Duxel.Vulkan;

internal static class VulkanEarlyInitTests
{
    internal static void Run(string output, int frameSlots)
    {
        using var platform = PresentationTestPlatform.Create();
        var options = new VulkanRendererOptions(2, EnableVSync: false, MaxFramesInFlight: frameSlots);
        string[] stages =
        [
            "VulkanInit[Surface]", "VulkanInit[DeviceEntryPoints]", "VulkanInit[Swapchain]",
            "VulkanInit[GraphicsPipeline]", "VulkanInit[Ready]"
        ];
        foreach (var stage in stages)
        {
            var expected = new InvalidOperationException(stage);
            try
            {
                using var unexpected = new VulkanRendererBackend(platform, options with
                {
                    DiagnosticsLog = state =>
                    {
                        if (state == stage)
                            throw expected;
                    }
                });
                throw new InvalidOperationException($"The test did not reach {stage}.");
            }
            catch (InvalidOperationException error) when (ReferenceEquals(error, expected))
            {
                Console.WriteLine($"Preserved the original error at {stage}.");
            }
        }

        using (var renderer = new VulkanRendererBackend(platform, options))
            renderer.CreateDeviceObjects();

        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "result.txt"), stages);
    }
}
