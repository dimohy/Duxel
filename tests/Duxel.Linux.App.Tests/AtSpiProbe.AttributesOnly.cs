using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static unsafe partial class AtSpiProbe
{
    internal static void RunAttributes(uint parentPid, string output)
    {
        if (File.Exists(output))
            throw new IOException("Use a fresh attribute result path.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent AT-SPI initialization failed.");
        atspi_set_timeout(2000, 2000);
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }
        try
        {
            using var app = OwnedApplication(parentPid);
            using var window = Child(app.Value, 0);
            using var input = Find(window.Value, value => Role(value) == 79 && Name(value) == "입력")
                ?? throw new InvalidOperationException("The owned input was not exported.");
            using var text = new NativeObject(atspi_accessible_get_text_iface(input.Value));
            using var edit = new NativeObject(atspi_accessible_get_editable_text_iface(input.Value));
            using var events = new EventWatch();
            events.Input = input.Value;
            using var component = new NativeObject(atspi_accessible_get_component_iface(input.Value));
            nint error = 0;
            var focused = atspi_component_grab_focus(component.Value, &error);
            Error(ref error);
            Check(focused != 0, "The targeted native focus request succeeds before attribute mutations");
            Wait(() =>
            {
                events.Check();
                using var state = new NativeObject(atspi_accessible_get_state_set(input.Value));
                return events.Focus > 0 && atspi_state_set_contains(state.Value, 12) != 0;
            }, "targeted cached native focus state");
            Check(true, "Native focus publication reaches the client cache without clearing it");
            Check(SetText(edit.Value, "붙여넣기 😀") && ReadText(text.Value, 0, -1) == "붙여넣기 😀",
                "Targeted attribute fixture publishes the exact six-scalar source");
            VerifyTextBoundaries(edit.Value, text.Value, Check);
            VerifyLegacyText(edit.Value, text.Value, Check);
            VerifyTextAttributes(edit.Value, text.Value, Check);
        }
        finally { atspi_exit(); }
        File.WriteAllText(output, JsonSerializer.Serialize(new NativeAttributeResult(
            RuntimeInformation.FrameworkDescription, true, checks.Count, checks.ToArray()),
            NativeAttributeJson.Default.NativeAttributeResult));
    }
}

internal sealed record NativeAttributeResult(string framework, bool nativeCleanupCompleted, int passed, string[] checks);
[JsonSerializable(typeof(NativeAttributeResult))]
internal partial class NativeAttributeJson : JsonSerializerContext;
