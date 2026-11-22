using System.Text.Json;
using Duxel.Core;

internal static class TextImageMemoryTests
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var slots in new[] { 1, 3 })
            Verify(slots, Path.Combine(output, $"slots-{slots}.json"));
    }

    private static void Verify(int slots, string output)
    {
        var fixture = new PresentationReuseTests.Fixture(slots, false);
        var renderer = fixture.Renderer;
        const int count = 48;
        const int cycles = 96;
        var first = new UiTextureId(1_100_400_000);
        ulong peakBytes = 0;
        var peakPending = 0;
        var peakIdle = 0;
        long reuses = 0;
        long evictions = 0;
        try
        {
            Submit();
            var startup = renderer.ValidationTextImageStorage();

            // A view creation failure must return the range before anything can reference it.
            renderer.ValidationBeforeTextureImageView = () => throw new TestImageViewException();
            fixture.Context.QueueTextureUpdate(Create(first, 0, 1));
            try
            {
                Submit();
                throw new InvalidOperationException("Expected the injected image view failure.");
            }
            catch (TestImageViewException)
            {
                if (renderer.ValidationTextImageStorage() != startup)
                    throw new InvalidOperationException("Failed image view creation retained text image storage.");
            }
            finally { renderer.ValidationBeforeTextureImageView = null; }

            for (var cycle = 0; cycle < cycles; cycle++)
            {
                for (var i = 0; i < count; i++)
                {
                    var texture = new UiTextureId(first.Value + (nuint)i);
                    if (cycle > 0)
                        fixture.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture, UiTextureFormat.Rgba8Unorm, 0, 0, default));
                    fixture.Context.QueueTextureUpdate(Create(texture, i, cycle));
                }
                Submit(paint: true);
                var storage = renderer.ValidationTextImageStorage();
                peakBytes = Math.Max(storage.Bytes, peakBytes);
                peakPending = Math.Max(storage.Pending, peakPending);
                peakIdle = Math.Max(storage.Idle, peakIdle);
                if (storage.Ranges < count || storage.Pages >= count || storage.Bytes > 48UL * 1024 * 1024)
                    throw new InvalidOperationException($"Unexpected text image storage at cycle {cycle}: {storage}.");
                reuses = renderer.ValidationReusedTextImages;
            }
            for (var i = 0; i < count; i++)
                fixture.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, new(first.Value + (nuint)i),
                    UiTextureFormat.Rgba8Unorm, 0, 0, default));
            for (var i = 0; i < slots * 2 + 1; i++)
                Submit();
            var retired = renderer.ValidationTextImageStorage();
            evictions = startup.Ranges + cycles * count - reuses - retired.Ranges;
            if (retired.Pending != 0 || peakPending < count || peakIdle == 0 || reuses == 0 || evictions <= 0)
                throw new InvalidOperationException("Replacement must exercise fence retirement, reuse and idle image eviction.");

        }
        finally { fixture.Dispose(); }
        if (renderer.ValidationTextImageStorage() != default)
            throw new InvalidOperationException("Renderer teardown left text image pages or pending ranges.");
        using var file = File.Create(output);
        using var json = new Utf8JsonWriter(file);
        json.WriteStartObject();
        json.WriteNumber("slots", slots);
        json.WriteNumber("cycles", cycles);
        json.WriteNumber("texturesPerCycle", count);
        json.WriteNumber("peakBytes", peakBytes);
        json.WriteNumber("peakPending", peakPending);
        json.WriteNumber("peakIdle", peakIdle);
        json.WriteNumber("reuses", reuses);
        json.WriteNumber("evictions", evictions);
        json.WriteNumber("teardownBytes", 0);
        json.WriteBoolean("injectedViewFailure", true);
        json.WriteEndObject();

        void Submit(bool paint = false)
        {
            using var data = fixture.Build(ui =>
            {
                if (paint)
                {
                    ui.GetWindowDrawList().AddImage(first, new(10, 10), new(87, 58), default, new(1, 1),
                        new(255, 255, 255));
                    ui.GetWindowDrawList().AddImage(new(first.Value + 1), new(90, 10), new(167, 58), default,
                        new(1, 1), new(255, 255, 255));
                }
            });
            if (!fixture.RenderSubmit(data))
                throw new InvalidOperationException("Text image memory fixture failed to present.");
        }
    }

    private static UiTextureUpdate Create(UiTextureId id, int index, int cycle)
    {
        var width = 64 + (index + cycle % 12) % 48;
        // Reuse the warm set first, then introduce new extents to overflow the idle pool.
        var height = cycle < 24 ? 96 : cycle + 1;
        var singleChannel = (index & 1) != 0;
        var bytes = new byte[width * height * (singleChannel ? 1 : 4)];
        Array.Fill(bytes, (byte)(127 + cycle % 64));
        return new(UiTextureUpdateKind.Create, id,
            singleChannel ? UiTextureFormat.R8CoverageUnorm : UiTextureFormat.Rgba8Unorm, width, height, bytes);
    }

    private sealed class TestImageViewException : Exception;
}
