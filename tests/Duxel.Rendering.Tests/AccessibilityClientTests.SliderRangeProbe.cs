using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void ProbeSliderRetirement(Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check, string directory)
    {
        var element = found["Native range"];
        var pattern = Pattern(element, 10003);
        owned.Add(pattern);
        var readings = new List<SliderPropertyReading>();
        SliderRetirementState? drained = null;
        int? getterResult = null, setterResult = null;
        double? getterValue = null;
        void Property(string phase, nint target, bool ignoreDefault)
        {
            UiaVariant value = default;
            var hr = ((delegate* unmanaged[Stdcall]<nint, int, int, UiaVariant*, int>)Slot(target, 11))(
                target, 30047, ignoreDefault ? 1 : 0, &value);
            try
            {
                var reading = new SliderPropertyReading(phase, ignoreDefault, hr, value.Type, value.Type == 5 ? value.Data.Real : null);
                readings.Add(reading);
                Console.WriteLine("SliderPropertyReading : " + reading);
            }
            finally { Ok(UiaNative.VariantClear(ref value)); }
        }
        void InvokeProbeButton(string name)
        {
            var invoke = Pattern(found[name], 10000);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(invoke, 3))(invoke)); }
            finally { Marshal.Release(invoke); }
        }
        try
        {
            Property("live-range", element, false);
            Property("live-range", element, true);
            Property("unsupported-button", found["Remove range"], false);
            Property("unsupported-button", found["Remove range"], true);
            check(readings[0].Hresult == 0 && readings[0].NumericValue == 35 && readings[1].NumericValue == 35,
                "focused retirement probe starts with the actual numeric property in both default modes");
            InvokeProbeButton("Remove range");
            var path = Path.Combine(directory, "drained.json");
            WaitForSlider(() => File.Exists(path));
            drained = JsonSerializer.Deserialize(File.ReadAllText(path), SliderRetirementJson.Default.SliderRetirementState)
                ?? throw new InvalidDataException("Native retirement acknowledgement is empty.");
            check(drained.Retired == 0 && drained.Disconnected + drained.AlreadyUnavailable > 0,
                "focused client observes completed native disconnection before reading the removed provider");
            Property("retired-range", element, false);
            Property("retired-range", element, true);
            double numeric = -1;
            getterResult = ((delegate* unmanaged[Stdcall]<nint, double*, int>)Slot(pattern, 4))(pattern, &numeric);
            getterValue = numeric;
            setterResult = ((delegate* unmanaged[Stdcall]<nint, double, int>)Slot(pattern, 3))(pattern, 20);
            Console.WriteLine($"RetiredPattern : getter = 0x{getterResult:X8}, value = {getterValue}, setter = 0x{setterResult:X8}");
            InvokeProbeButton("Finish");
            check(setterResult == UiaNative.Unavailable, "focused removed-pattern mutation is rejected after native disconnection");
            check(getterResult == UiaNative.Unavailable, "strict removed-pattern getter criterion remains unchanged after confirmed disconnection");
        }
        finally
        {
            var result = new SliderRetirementProbeResult(drained, readings.ToArray(), getterResult, getterValue, setterResult,
                getterResult == UiaNative.Unavailable);
            File.WriteAllText(Path.Combine(directory, "observations.json"), JsonSerializer.Serialize(result, SliderRetirementJson.Default.SliderRetirementProbeResult));
        }
    }
}

internal sealed record SliderRetirementState(int Cached, int Retired, int Disconnected, int AlreadyUnavailable);
internal sealed record SliderPropertyReading(string Phase, bool IgnoreDefaultValue, int Hresult, int VariantType, double? NumericValue);
internal sealed record SliderRetirementProbeResult(SliderRetirementState? Drained, SliderPropertyReading[] Readings,
    int? GetterHresult, double? GetterValue, int? SetterHresult, bool StrictGetterPassed);
[JsonSerializable(typeof(SliderRetirementState))]
[JsonSerializable(typeof(SliderRetirementProbeResult))]
internal partial class SliderRetirementJson : JsonSerializerContext;
