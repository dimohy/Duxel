using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class AnnouncementAccessibilityLinuxHost
{
    internal static void RunMessageBars(string output, string primary, string secondary)
    {
        using var scene = new NativeMessageBarScene();
        var frames = NativeAccessibilityLinuxHost.Run(output, primary, secondary, scene,
            "MessageBar", "atspi-messagebar", () =>
            {
                if (!scene.Finished || scene.Announcements != 4)
                    throw new InvalidOperationException("The native client must complete the actual four automatic MessageBar announcements.");
            });
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new NativeAnnouncementHostResult(
            RuntimeInformation.FrameworkDescription, 3, frames, "Actual MessageBar X11/Vulkan host, independent listener and retired registry"),
            NativeAnnouncementJson.Default.NativeAnnouncementHostResult));
    }
    internal static void Run(string output, string primary, string secondary)
    {
        using var scene = new NativeAnnouncementScene();
        var frames = NativeAccessibilityLinuxHost.Run(output, primary, secondary, scene,
            "Announcement", "atspi-announcement", () =>
            {
                if (!scene.Finished || scene.Queued != 4)
                    throw new InvalidOperationException("The native client must complete the actual four-announcement scene.");
            });
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new NativeAnnouncementHostResult(
            RuntimeInformation.FrameworkDescription, 3, frames, "Actual X11/Vulkan host, independent announcement listener and retired registry"),
            NativeAnnouncementJson.Default.NativeAnnouncementHostResult));
    }
}

internal sealed record NativeAnnouncementHostResult(string framework, int passed, int frames, string scope);
internal sealed record NativeAnnouncementEvent(int Priority, int Unused, string Message);
internal sealed record NativeAnnouncementClientResult(string framework, int passed, string[] checks, NativeAnnouncementEvent[] events);
[JsonSerializable(typeof(NativeAnnouncementHostResult))]
[JsonSerializable(typeof(NativeAnnouncementClientResult))]
internal partial class NativeAnnouncementJson : JsonSerializerContext;
