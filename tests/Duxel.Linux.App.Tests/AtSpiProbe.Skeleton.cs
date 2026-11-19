using System.Runtime.InteropServices;
using System.Text.Json;

internal static unsafe partial class AtSpiProbe
{
    internal static void RunSkeleton(uint parentPid, string output)
    {
        if (File.Exists(output))
            throw new IOException("Use a fresh Skeleton native client result.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Skeleton AT-SPI initialization failed.");
        atspi_set_timeout(2000, 2000);
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
        }
        try
        {
            using var app = OwnedApplication(parentPid);
            atspi_accessible_set_cache_mask(app.Value, 0);
            using var window = Child(app.Value, 0);
            NativeObject? FindName(string name)
            {
                atspi_accessible_clear_cache(window.Value);
                return Find(window.Value, value => Name(value) == name);
            }
            NativeObject Required(string name)
            {
                NativeObject? value = null;
                Wait(() => (value = FindName(name)) is not null, "Skeleton native object: " + name);
                return value!;
            }
            bool State(nint value, int state)
            {
                atspi_accessible_clear_cache(value);
                using var set = new NativeObject(atspi_accessible_get_state_set(value));
                return atspi_state_set_contains(set.Value, state) != 0;
            }
            void Invoke(nint value)
            {
                using var action = new NativeObject(atspi_accessible_get_action_iface(value));
                nint error = 0;
                var accepted = atspi_action_do_action(action.Value, 0, &error);
                Error(ref error);
                if (accepted == 0)
                    throw new InvalidOperationException("Skeleton native action rejected.");
            }
            bool Absent(string name)
            {
                using var value = FindName(name);
                return value is null;
            }
            using var complete = Required("Complete Skeleton");
            using var reload = Required("Reload Skeleton");
            for (var round = 0; round < 2; round++)
            {
                using var staticText = Required("Static native text 정세일");
                Check(Role(staticText.Value) == 29 && !State(staticText.Value, 11) && !State(staticText.Value, 12),
                    "Native standalone Korean text has Label role and no keyboard focus: " + round);
                using var skeleton = Required("Native Skeleton");
                Check(Role(skeleton.Value) == 42 && State(skeleton.Value, 3),
                    "Native Skeleton exposes progress-bar role and busy state: " + round);
                Check(!State(skeleton.Value, 11) && !State(skeleton.Value, 12),
                    "Native Skeleton does not become focusable or steal focus: " + round);
                var numeric = atspi_accessible_get_value_iface(skeleton.Value);
                if (numeric != 0)
                {
                    using var unexpected = new NativeObject(numeric);
                    Check(false, "Native Skeleton unexpectedly exposes a numeric value interface.");
                }
                Check(numeric == 0,
                    "Native Skeleton exposes no fictitious numeric percentage: " + round);
                Invoke(complete.Value);
                Wait(() => Absent("Native Skeleton"), "Completed Skeleton leaves the native tree");
                using var updatedText = Required("Updated standalone text 부산");
                Check(Role(updatedText.Value) == 29 && Absent("Static native text 정세일"),
                    "Native standalone text updates from the actual displayed source: " + round);
                using var ready = Required("Skeleton content ready");
                using var readyText = new NativeObject(atspi_accessible_get_text_iface(ready.Value));
                Check(!State(ready.Value, 3) && ReadText(readyText.Value, 0, -1) == "정세일",
                    "Loaded data replaces the busy placeholder with readable content: " + round);
                if (round == 0)
                    Invoke(reload.Value);
            }
        }
        finally { atspi_exit(); }
        File.WriteAllText(output, JsonSerializer.Serialize(new NativeSkeletonClientResult(
            RuntimeInformation.FrameworkDescription, checks.Count, checks.ToArray()), NativeSkeletonJson.Default.NativeSkeletonClientResult));
    }
}
