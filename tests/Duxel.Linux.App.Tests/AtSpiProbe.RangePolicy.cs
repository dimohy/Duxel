using System.Runtime.InteropServices;
using System.Text.Json;

internal static unsafe partial class AtSpiProbe
{
    internal static void VerifyRangePolicy(uint pid, string output)
    {
        if (File.Exists(output))
            throw new IOException("The native numeric-policy result already exists.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Native AT-SPI initialization failed.");
        atspi_set_timeout(2000, 2000);
        List<string> checks = [];
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            checks.Add(message);
            Console.WriteLine("PASS " + message);
        }
        try
        {
            using var desktop = new NativeObject(atspi_get_desktop(0));
            NativeObject? found = null;
            Wait(() =>
            {
                atspi_accessible_clear_cache(desktop.Value);
                for (var i = 0; i < Count(desktop.Value); i++)
                {
                    using var candidate = Child(desktop.Value, i);
                    nint error = 0;
                    var ownerPid = atspi_accessible_get_process_id(candidate.Value, &error);
                    Error(ref error);
                    if (ownerPid == pid)
                    {
                        found = new(g_object_ref(candidate.Value));
                        return true;
                    }
                }
                return false;
            }, "the owned numeric-policy application's native registration");
            using var application = found!;
            using var readOnly = Find(application.Value, value => Name(value) == "readonly numeric fixture")
                ?? throw new InvalidDataException("The readonly numeric fixture is missing.");
            using var readOnlyValue = new NativeObject(atspi_accessible_get_value_iface(readOnly.Value));
            using var readOnlyStates = new NativeObject(atspi_accessible_get_state_set(readOnly.Value));
            Check(atspi_state_set_contains(readOnlyStates.Value, 43) != 0 && atspi_state_set_contains(readOnlyStates.Value, 7) == 0,
                "Readonly numeric elements expose readonly state and omit editable state");
            nint callError = 0;
            var text = atspi_value_get_text(readOnlyValue.Value, &callError);
            try
            {
                Error(ref callError);
                Check(Marshal.PtrToStringUTF8(text) == "절반", "Native numeric Text preserves the explicit Korean description");
            }
            finally { if (text != 0) g_free(text); }
            var rejected = RejectNumericWrite(readOnly.Value, 75, "ReadOnly");
            var current = atspi_value_get_current_value(readOnlyValue.Value, &callError);
            Error(ref callError);
            Check(rejected && current == 50, "Readonly native numeric writes fail and preserve the original value");
            using var disabled = Find(application.Value, value => Name(value) == "disabled numeric fixture")
                ?? throw new InvalidDataException("The disabled numeric fixture is missing.");
            using var disabledValue = new NativeObject(atspi_accessible_get_value_iface(disabled.Value));
            using var disabledStates = new NativeObject(atspi_accessible_get_state_set(disabled.Value));
            rejected = RejectNumericWrite(disabled.Value, 75, "Disabled");
            current = atspi_value_get_current_value(disabledValue.Value, &callError);
            Error(ref callError);
            Check(rejected && current == 25 && atspi_state_set_contains(disabledStates.Value, 8) == 0,
                "Disabled native numeric writes fail and preserve their value and disabled state");
            using var plain = Find(application.Value, value => Name(value) == "nonnumeric fixture")
                ?? throw new InvalidDataException("The nonnumeric fixture is missing.");
            var unsupported = atspi_accessible_get_value_iface(plain.Value);
            try
            {
                Check(unsupported == 0, "Nonnumeric elements do not advertise an unsupported Value interface");
            }
            finally { if (unsupported != 0) g_object_unref(unsupported); }
            VerifySelectionPolicy(application.Value, output);
            File.WriteAllText(output, JsonSerializer.Serialize(new LinuxPolicyResult(checks.Count, checks), LinuxProbeResultsJson.Default.LinuxPolicyResult));
        }
        finally { atspi_exit(); }
    }

    [LibraryImport(Library)] private static partial nint atspi_value_get_text(nint value, nint* error);
}
