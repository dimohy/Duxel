using System.Numerics;
using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

internal static class ProjectedGlyphMsaaSwitchTests
{
    internal static void Run(string output, bool queued)
    {
        Directory.CreateDirectory(output);
        var profile = Path.GetFullPath(Path.Combine(output, "vulkan.log"));
        File.WriteAllText(profile, string.Empty);
        Environment.SetEnvironmentVariable("DUXEL_TEST_DISABLE_STANDARD_SAMPLE_LOCATIONS", "1");
        Environment.SetEnvironmentVariable("DUXEL_VK_PROJECTED_GLYPHS", "1");
        Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE", "1");
        Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", "1");
        Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_OUT", profile);
        var session = new DuxelAppSession();
        DuxelWindowsApp.Run(new DuxelAppOptions
        {
            Window = new() { Width = 440, Height = 320, Resizable = false, VSync = false },
            Renderer = new() { MsaaSamples = 1, MaxFramesInFlight = 3 },
            Frame = new() { EnableRenderPipeline = queued },
            Font = new() { FastStartup = false },
            Screen = new SampleSwitchScreen(session),
        }, session);

        var draws = 0;
        var observed = new List<(int Samples, int Draws)>();
        foreach (var line in File.ReadLines(profile))
        {
            const string drawPrefix = "projectedGlyphs(draws=";
            const string framePrefix = "accepted=True msaa=";
            var drawStart = line.IndexOf(drawPrefix, StringComparison.Ordinal);
            var frameStart = line.IndexOf(framePrefix, StringComparison.Ordinal);
            if (drawStart < 0 || frameStart < 0)
                continue;
            var total = ReadInt(line.AsSpan(drawStart + drawPrefix.Length));
            observed.Add((ReadInt(line.AsSpan(frameStart + framePrefix.Length)), total - draws));
            draws = total;
        }
        if (!observed.SequenceEqual(new[] { (1, 1), (1, 1), (4, 0), (4, 0), (1, 1), (1, 1) }))
            throw new InvalidOperationException($"Unexpected MSAA and projected glyph sequence: {string.Join(", ", observed)}.");
        Console.WriteLine("PASS Retained glyphs return to the projected path after switching through unsupported MSAA.");

        static int ReadInt(ReadOnlySpan<char> value) => int.Parse(value[..value.IndexOfAny(' ', ')')],
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class SampleSwitchScreen(DuxelAppSession session) : UiScreen
    {
        private readonly Matrix4x4 _camera = Matrix4x4.CreateLookAt(new(0, 0, 2.2f), Vector3.Zero, Vector3.UnitY)
            * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 440f / 320f, .1f, 10);
        private int _frame;

        public override void Render(UiImmediateContext ui)
        {
            var options = new UiLayerOptions(true, 1, default)
            {
                ContentVersion = 1,
                FontAtlasIndependent = true,
                Scene3D = new(_camera, true),
            };
            if (!ui.TryReplayLayer("label", options))
            {
                if (ui.BeginLayer("label", options))
                {
                    ui.SetCursorScreenPos(new(68, 112));
                    ui.TextColored(new(255, 0, 0), "한글 A09");
                }
                ui.EndLayer();
            }
            if (!ui.IsPresentationPass)
                return;

            if (++_frame == 2)
                ui.SetMsaaSamples(4);
            else if (_frame == 4)
                ui.SetMsaaSamples(1);

            if (_frame == 7)
                session.Exit();
            else
                session.RequestFrame();
        }
    }
}
