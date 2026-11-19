using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Validation;

internal static class DialogAccessibilityLinuxHost
{
    internal static void Run(string output, string primary, string secondary)
    {
        using var screen = new DialogAccessibilityScreen();
        var frames = NativeAccessibilityLinuxHost.Run(output, primary, secondary, screen, "Dialog", "atspi-dialog", () =>
        {
            if (!screen.Finished || screen.Dialog.IsOpen || screen.Name.Value != "정세일 AT-SPI")
                throw new InvalidOperationException("The independent client did not complete the actual Dialog model.");
        }, () => screen.Frame++);
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new NativeDialogHostResult(
            RuntimeInformation.FrameworkDescription, 3, frames, "Actual X11/Vulkan host, independent libatspi client and retired registry"),
            NativeDialogJson.Default.NativeDialogHostResult));
    }
}

internal sealed record NativeDialogHostResult(string framework, int passed, int frames, string scope);
internal sealed record NativeDialogClientResult(string framework, int passed, string[] checks);
[JsonSerializable(typeof(NativeDialogHostResult))]
[JsonSerializable(typeof(NativeDialogClientResult))]
internal partial class NativeDialogJson : JsonSerializerContext;
