using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static unsafe partial class AtSpiProbe
{
    internal static void RunDialog(uint parentPid, string output, nuint nativeWindow)
    {
        if (File.Exists(output))
            throw new IOException("Use a fresh native Dialog client result.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent Dialog AT-SPI initialization failed.");
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
            atspi_accessible_set_cache_mask(app.Value, 0);
            using var window = Child(app.Value, 0);
            using var peer = new NativePeer(nativeWindow);
            NativeObject? FindName(string name, int? role = null)
            {
                atspi_accessible_clear_cache(window.Value);
                return Find(window.Value, value => Name(value) == name && (!role.HasValue || Role(value) == role));
            }
            NativeObject Required(string name, int? role = null)
            {
                NativeObject? result = null;
                Wait(() => (result = FindName(name, role)) is not null, "Dialog object : " + name);
                return result!;
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
                var result = atspi_action_do_action(action.Value, 0, &error);
                Error(ref error);
                if (result == 0)
                    throw new InvalidOperationException("The actual Dialog action was rejected.");
            }
            void Focus(nint value)
            {
                using var component = new NativeObject(atspi_accessible_get_component_iface(value));
                nint error = 0;
                var result = atspi_component_grab_focus(component.Value, &error);
                Error(ref error);
                if (result == 0)
                    throw new InvalidOperationException("The actual Dialog focus action was rejected.");
                Wait(() => State(value, 12), "Dialog focus committed");
            }
            using var marker = Required("Native frame");
            using var markerText = new NativeObject(atspi_accessible_get_text_iface(marker.Value));
            int Serial() => int.Parse(ReadText(markerText.Value, 0, -1), CultureInfo.InvariantCulture);
            void Key(nuint symbol)
            {
                var serial = Serial();
                peer.Key(true, symbol);
                peer.Key(false, symbol);
                Wait(() => Serial() >= serial + 2, "Dialog native key committed to a published frame");
            }
            void Backdrop()
            {
                var serial = Serial();
                peer.Pointer(6, 4, 4);
                peer.Pointer(4, 4, 4, 1);
                peer.Pointer(5, 4, 4, 1, 256);
                Wait(() => Serial() >= serial + 2, "Dialog native backdrop committed");
            }
            bool Absent(string name)
            {
                using var value = FindName(name);
                return value is null;
            }
            void Retired(string child, nint opener, string phase)
            {
                var absent = false;
                var focused = false;
                try
                {
                    Wait(() =>
                    {
                        absent = Absent(child);
                        focused = State(opener, 12);
                        return absent && focused;
                    }, phase);
                }
                catch (TimeoutException error)
                {
                    throw new TimeoutException($"{phase}: childAbsent={absent}, openerFocused={focused}, "
                        + $"openerEnabled={State(opener, 8)}, surfaceAbsent={Absent("Dialog profile")}, "
                        + $"nativeWindowActive={State(window.Value, 1)}", error);
                }
            }
            using var modal = Required("Open modal", 43);
            using var nonmodal = Required("Open non-modal", 43);
            using var alert = Required("Open alert", 43);
            Invoke(modal.Value);
            using (var surface = Required("Dialog profile", 16))
            using (var editor = Required("Dialog name"))
            {
                Wait(() => State(editor.Value, 12), "modal editor autofocus");
                Check(Role(surface.Value) == 16 && State(surface.Value, 16), "native AT-SPI modal Dialog exposes dialog role and modal state");
                nint error = 0;
                var description = atspi_accessible_get_description(surface.Value, &error);
                try
                {
                    Error(ref error);
                    Check((Marshal.PtrToStringUTF8(description) ?? throw new InvalidDataException("The native Dialog description is missing.")).Contains("Profile description", StringComparison.Ordinal),
                    "native AT-SPI Dialog exposes its actual optional description");
                }
                finally { if (description != 0) g_free(description); }
                Check(!State(nonmodal.Value, 8) && State(editor.Value, 12), "native modal Dialog disables background and focuses its editor");
                using var edit = new NativeObject(atspi_accessible_get_editable_text_iface(editor.Value));
                using var text = new NativeObject(atspi_accessible_get_text_iface(editor.Value));
                Check(SetText(edit.Value, "정세일 AT-SPI"), "native editable Dialog text accepts the actual Korean model write");
                Wait(() => ReadText(text.Value, 0, -1) == "정세일 AT-SPI", "Korean Dialog value published");
                Check(ReadText(text.Value, 0, -1) == "정세일 AT-SPI", "native Dialog text retains the actual bound Korean value");
                Key(0xff09);
                using var save = Required("Save profile", 43);
                Wait(() => State(save.Value, 12), "Tab enters actions");
                Key(0xff09);
                using var cancel = Required("Cancel profile", 43);
                Wait(() => State(cancel.Value, 12), "Tab follows actions");
                Key(0xff09);
                Wait(() => State(editor.Value, 12), "Tab wraps to editor");
                Check(State(editor.Value, 12), "native X11 Tab stays inside modal Dialog");
                Key(0xff1b);
                Retired("Dialog name", modal.Value, "Escape retires modal and restores opener");
                Check(Absent("Dialog profile") && State(modal.Value, 12), "native modal Escape retires its tree and restores the opener");
            }
            Invoke(nonmodal.Value);
            using (var surface = Required("Dialog profile", 16))
            using (var close = Required("Close dialog", 43))
            {
                Wait(() => State(close.Value, 12), "non-modal close autofocus");
                Check(!State(surface.Value, 16) && State(modal.Value, 8), "native AT-SPI non-modal Dialog permits background interaction");
                Focus(modal.Value);
                Key(0xff1b);
                Check(!Absent("Dialog profile"), "native Escape outside non-modal Dialog retains its surface");
                Invoke(close.Value);
                Retired("Dialog profile", nonmodal.Value, "non-modal close restores opener");
                Check(State(nonmodal.Value, 12), "native close action restores the non-modal opener");
            }
            Invoke(alert.Value);
            using (var surface = Required("Dialog profile", 2))
            using (var editor = Required("Dialog name"))
            {
                Wait(() => State(editor.Value, 12), "alert editor autofocus");
                Check(Role(surface.Value) == 2 && State(surface.Value, 16), "native AT-SPI alert Dialog exposes distinct alert role and modal state");
                Backdrop();
                Check(!Absent("Dialog profile") && State(editor.Value, 12), "native alert backdrop retains the actual surface and focus");
                Key(0xff1b);
                Retired("Dialog profile", alert.Value, "alert Escape restores opener");
                Check(State(alert.Value, 12), "native focused Escape retires the alert tree and restores its opener");
            }
            Invoke(modal.Value);
            using (var surface = Required("Dialog profile", 16))
            {
                Backdrop();
                Retired("Dialog profile", modal.Value, "modal backdrop dismisses");
                Check(State(modal.Value, 12), "native X11 modal backdrop click dismisses the actual Dialog");
            }
            using var finish = Required("Finish Dialog", 43);
            Invoke(finish.Value);
        }
        finally { atspi_exit(); }
        File.WriteAllText(output, JsonSerializer.Serialize(new NativeDialogClientResult(RuntimeInformation.FrameworkDescription,
            checks.Count, checks.ToArray()), NativeDialogJson.Default.NativeDialogClientResult));
    }
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_description(nint value, nint* error);
}
