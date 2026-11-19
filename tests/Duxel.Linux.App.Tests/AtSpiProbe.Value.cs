using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    private static void VerifyRange(nint window, EventWatch events, Action<bool, string> check)
    {
        using var slider = Find(window, value => Role(value) == 51 && Name(value) == "수준")
            ?? throw new InvalidOperationException("The owned numeric slider was not exported.");
        using var numeric = new NativeObject(atspi_accessible_get_value_iface(slider.Value));
        events.Slider = slider.Value;
        nint error = 0;
        var minimum = atspi_value_get_minimum_value(numeric.Value, &error);
        Error(ref error);
        var maximum = atspi_value_get_maximum_value(numeric.Value, &error);
        Error(ref error);
        var increment = atspi_value_get_minimum_increment(numeric.Value, &error);
        Error(ref error);
        var current = atspi_value_get_current_value(numeric.Value, &error);
        Error(ref error);
        check(minimum == 0 && maximum == 100 && increment == 1 && current == 25,
            "The native Value interface exposes the actual slider bounds, increment and initial value");
        var accepted = atspi_value_set_current_value(numeric.Value, 73, &error);
        Error(ref error);
        check(accepted != 0, "A native numeric value write reaches the shared slider");
        Wait(() =>
        {
            events.Check();
            nint readError = 0;
            var value = atspi_value_get_current_value(numeric.Value, &readError);
            Error(ref readError);
            return events.Values > 0 && value == 73;
        }, "the slider's actual numeric property-change event");
        check(true, "A native value-change event reports the committed shared slider value");
        foreach (var invalid in new[] { -1d, 101d, double.NaN, double.PositiveInfinity })
        {
            var rejected = RejectNumericWrite(slider.Value, invalid);
            current = atspi_value_get_current_value(numeric.Value, &error);
            Error(ref error);
            check(rejected && current == 73, "Invalid native numeric write is rejected without changing the slider: " + invalid);
        }
        accepted = atspi_value_set_current_value(numeric.Value, 73.6, &error);
        Error(ref error);
        current = atspi_value_get_current_value(numeric.Value, &error);
        Error(ref error);
        check(accepted != 0 && current == 74, "Native numeric writes retain the actual integer widget rounding contract");
        accepted = atspi_value_set_current_value(numeric.Value, 0, &error);
        Error(ref error);
        current = atspi_value_get_current_value(numeric.Value, &error);
        Error(ref error);
        check(accepted != 0 && current == 0, "The native slider accepts its inclusive minimum");
        accepted = atspi_value_set_current_value(numeric.Value, 100, &error);
        Error(ref error);
        current = atspi_value_get_current_value(numeric.Value, &error);
        Error(ref error);
        check(accepted != 0 && current == 100, "The native slider accepts its inclusive maximum after rejected writes");
    }

    [LibraryImport(Library)] private static partial double atspi_value_get_minimum_value(nint value, nint* error);
    [LibraryImport(Library)] private static partial double atspi_value_get_maximum_value(nint value, nint* error);
    [LibraryImport(Library)] private static partial double atspi_value_get_minimum_increment(nint value, nint* error);
    [LibraryImport(Library)] private static partial double atspi_value_get_current_value(nint value, nint* error);
    [LibraryImport(Library)] private static partial int atspi_value_set_current_value(nint value, double current, nint* error);
}
