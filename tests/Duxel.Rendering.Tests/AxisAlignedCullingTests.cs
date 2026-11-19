using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class AxisAlignedCullingTests
{
    internal static void Run(Action<bool, string> check)
    {
        var old = Environment.GetEnvironmentVariable("DUXEL_VK_AXIS_ALIGNED_CULLING");
        try
        {
            Environment.SetEnvironmentVariable("DUXEL_VK_AXIS_ALIGNED_CULLING", null);
            check(VulkanRendererBackend.ValidationParseAxisAlignedCulling(), "axis-aligned culling defaults enabled");
            Environment.SetEnvironmentVariable("DUXEL_VK_AXIS_ALIGNED_CULLING", "0");
            check(!VulkanRendererBackend.ValidationParseAxisAlignedCulling(), "general culling remains selectable for same-image comparison");
            Environment.SetEnvironmentVariable("DUXEL_VK_AXIS_ALIGNED_CULLING", "invalid");
            var rejected = false;
            try { VulkanRendererBackend.ValidationParseAxisAlignedCulling(); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "invalid culling mode fails before device initialization");
        }
        finally { Environment.SetEnvironmentVariable("DUXEL_VK_AXIS_ALIGNED_CULLING", old); }

        var random = new Random(90313);
        for (var i = 0; i < 100000; i++)
        {
            var viewport = new UiRect(0, 0, random.Next(16, 4000), random.Next(16, 2400));
            var bounds = new UiRect(Next(-10000, 10000), Next(-10000, 10000), Next(0, 1000), Next(0, 1000));
            var translation = new UiVector2(Next(-10000, 10000), Next(-10000, 10000));
            var matrix = Matrix4x4.CreateScale(Next(-16, 16), Next(-16, 16), 1)
                * Matrix4x4.CreateTranslation(Next(-4, 4), Next(-4, 4), Next(-.1f, 1.1f));
            Equal(bounds, translation, matrix, viewport, i % 2 == 0, Next(-.2f, .2f));
        }
        check(true, "100000 signed-scale,translation,depth and viewport cases match general culling");

        foreach (var edge in new[] { -1.01f, -1f, 0f, 1f, 1.01f, float.NaN, float.PositiveInfinity })
        foreach (var inside in new[] { false, true })
        {
            var viewport = new UiRect(0, 0, 1024, 768);
            var bounds = new UiRect(0, 0, 1024, 768);
            Equal(bounds, default, Matrix4x4.CreateTranslation(edge, 0, 0), viewport, inside, 0);
            Equal(bounds, default, Matrix4x4.CreateTranslation(0, 0, edge), viewport, inside, 0);
            Equal(bounds, default, Matrix4x4.CreateRotationY(.4f) * Matrix4x4.CreateTranslation(edge, 0, .4f), viewport, inside, 0);
            Equal(bounds, default, Matrix4x4.CreatePerspectiveFieldOfView(1f, 1f, .1f, 100), viewport, inside, edge);
        }
        check(true, "clip edges,nonfinite inputs,rotation and perspective retain general classifications");

        foreach (var width in new[] { 16, 1024, 3840 })
        foreach (var scale in new[] { -16f, -.001f, 0f, .001f, 16f })
        foreach (var inside in new[] { false, true })
        {
            var viewport = new UiRect(0, 0, width, 768);
            var guard = 4f / width + .00001f;
            var boundary = 1f + guard;
            foreach (var edge in new[] { boundary, MathF.BitIncrement(boundary), MathF.BitDecrement(boundary),
                -boundary, MathF.BitIncrement(-boundary), MathF.BitDecrement(-boundary) })
            {
                var matrix = Matrix4x4.CreateScale(scale, scale, 1) * Matrix4x4.CreateTranslation(edge, edge, .5f);
                Equal(new(width / 2f, 384, 0, 0), default, matrix, viewport, inside, 0);
                Equal(new(width / 2f, 384, width / 4f, 96), default, matrix, viewport, inside, .50001f);
            }
        }
        check(true, "adjacent floating-point values at guarded clip planes and degenerate bounds preserve decisions");

        float Next(float minimum, float maximum) => minimum + random.NextSingle() * (maximum - minimum);
        static void Equal(UiRect bounds, UiVector2 translation, Matrix4x4 matrix, UiRect viewport, bool inside, float depth)
        {
            var general = VulkanRendererBackend.ValidationClassifyLayerBounds(bounds, translation, matrix, viewport, inside, depth, false);
            var specialized = VulkanRendererBackend.ValidationClassifyLayerBounds(bounds, translation, matrix, viewport, inside, depth, true);
            if (general != specialized)
                throw new InvalidOperationException($"Culling mismatch: {general}/{specialized},bounds={bounds},transform={matrix},depth={depth}");
        }
    }
}
