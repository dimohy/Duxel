using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckToggleControls(nint automation, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        var mixed = found["Mixed checkbox"];
        nint TogglePattern(string name)
        {
            var pattern = Pattern(found[name], 10015);
            owned.Add(pattern);
            return pattern;
        }
        var mixedPattern = TogglePattern("Mixed checkbox");
        var switchPattern = TogglePattern("Shared switch");
        var buttonPattern = TogglePattern("Shared toggle");
        var focusOnly = found["Focus-only switch"];
        var focusOnlyPattern = TogglePattern("Focus-only switch");
        var legacyPattern = TogglePattern("Legacy check");
        var disabledPattern = TogglePattern("Disabled option");
        int State(nint pattern)
        {
            int state = -1;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(pattern, 4))(pattern, &state));
            return state;
        }
        int Toggle(nint pattern) => ((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern);
        void Invoke(string name)
        {
            var pattern = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }
        int ControlType(nint element)
        {
            UiaVariant role = default;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, 30003, &role));
            try
            {
                if (role.Type != 3) throw new InvalidOperationException("Native control type must be an integer.");
                return role.Data.Integer;
            }
            finally { Ok(UiaNative.VariantClear(ref role)); }
        }
        check(ControlType(mixed) == 50002 && State(mixedPattern) == 2 && Boolean(mixed, 30041) && !Boolean(mixed, 30031),
            "external mixed Checkbox exposes native ToggleState2 and Toggle rather than Invoke");
        check(ControlType(found["Shared switch"]) == 50000 && ControlType(found["Shared toggle"]) == 50000 &&
            Text(found["Shared switch"], 30012) == "Duxel.Switch" && Text(found["Shared toggle"], 30012) == "Duxel.ToggleButton" &&
            !Boolean(found["Shared toggle"], 30031),
            "external Switch and ToggleButton use Button/Toggle mapping with distinct classes and no duplicate Invoke");
        var source = Text(mixed, 30011);
        ReadPropertyChanges(automation, mixed, 1, owned, [30041, 30086], properties =>
        {
            Ok(Toggle(mixedPattern));
            check(State(mixedPattern) == 1 && properties.Wait() == new TooltipNativeProperty(30086, source, 1),
                "external mixed-to-on Toggle commits state1 without a spurious capability-change event");
            Ok(Toggle(mixedPattern));
            check(State(mixedPattern) == 0 && properties.Wait() == new TooltipNativeProperty(30086, source, 0),
                "external on-to-off Toggle commits state0 through the existing value policy");
            Invoke("Restore mixed");
            check(properties.Wait() == new TooltipNativeProperty(30086, source, 2) && State(mixedPattern) == 2,
                "external caller-restored mixed state emits integer2 while retaining the same Toggle capability");
        });
        Ok(Toggle(switchPattern));
        check(State(switchPattern) == 1 && State(buttonPattern) == 1, "external Switch activation updates its actual shared ToggleButton binding");
        Ok(Toggle(buttonPattern));
        WaitForSlider(() => State(switchPattern) == 0 && State(buttonPattern) == 0);
        check(State(switchPattern) == 0 && State(buttonPattern) == 0, "external ToggleButton activation updates its actual shared Switch binding");
        Ok(Toggle(legacyPattern));
        check(State(legacyPattern) == 1, "external Toggle reaches the legacy Checkbox reference value through the common path");
        check(State(disabledPattern) == 0 && Toggle(disabledPattern) == UiaNative.Disabled,
            "external disabled Checkbox remains readable and rejects Toggle with the exact disabled HRESULT");
        check(!Boolean(focusOnly, 30010) && Boolean(focusOnly, 30009) && State(focusOnlyPattern) == 1,
            "external UIA reads the focus-only Switch as disabled, keyboard focusable and On");
        check(Boolean(focusOnly, 30025) && !Boolean(focusOnly, 30103)
            && Text(focusOnly, 30013) == "Review this setting A value is required",
            "external UIA reads required, invalid and HelpText from the Switch's Field");
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(focusOnly, 3))(focusOnly));
        WaitForSlider(() => Boolean(focusOnly, 30008));
        check(Boolean(focusOnly, 30008) && Toggle(focusOnlyPattern) == UiaNative.Disabled && State(focusOnlyPattern) == 1,
            "external SetFocus reaches the focus-only Switch while Toggle returns element-not-enabled");
        var focusOnlyIdentity = Text(focusOnly, 30011);
        Invoke("Unlock switch");
        WaitForSlider(() => Boolean(focusOnly, 30010));
        Ok(Toggle(focusOnlyPattern));
        WaitForSlider(() => State(focusOnlyPattern) == 0);
        check(Text(focusOnly, 30011) == focusOnlyIdentity && State(focusOnlyPattern) == 0,
            "external Toggle works on the same Switch element after unlocking");
        Invoke("Disable mixed");
        WaitForSlider(() => !Boolean(mixed, 30010));
        check(State(mixedPattern) == 2 && Toggle(mixedPattern) == UiaNative.Disabled,
            "external mixed state is independent of live enabled state and does not activate when disabled");
        Invoke("Remove mixed");
        var removedResult = 0;
        WaitForSlider(() =>
        {
            int state = -1;
            removedResult = ((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(mixedPattern, 4))(mixedPattern, &state);
            return removedResult == UiaNative.Unavailable;
        });
        check(removedResult == UiaNative.Unavailable && Toggle(mixedPattern) == UiaNative.Unavailable,
            "external retained Toggle getter and action reject the removed control with0x80040201");
        Invoke("Finish");
    }
}
