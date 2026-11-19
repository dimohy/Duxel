internal static unsafe partial class AtSpiProbe
{
    private static bool HasNumericInterface(nint accessible)
    {
        var value = atspi_accessible_get_value_iface(accessible);
        if (value == 0)
            return false;
        g_object_unref(value);
        return true;
    }
    private static void VerifyProgress(nint window, Action<bool, string> check)
    {
        using var progress = Find(window, value => Role(value) == 42 && Name(value) == "진행 상태")
            ?? throw new InvalidOperationException("The actual sample progress widget was not exported.");
        using var numeric = new NativeObject(atspi_accessible_get_value_iface(progress.Value));
        using var states = new NativeObject(atspi_accessible_get_state_set(progress.Value));
        nint error = 0;
        var current = atspi_value_get_current_value(numeric.Value, &error);
        Error(ref error);
        var minimum = atspi_value_get_minimum_value(numeric.Value, &error);
        Error(ref error);
        var maximum = atspi_value_get_maximum_value(numeric.Value, &error);
        Error(ref error);
        var increment = atspi_value_get_minimum_increment(numeric.Value, &error);
        Error(ref error);
        check(current == 100 && minimum == 0 && maximum == 100 && increment == 0,
            "The actual progress widget exposes the committed logical target and readonly numeric bounds");
        check(atspi_state_set_contains(states.Value, 43) != 0 && atspi_state_set_contains(states.Value, 7) == 0
            && atspi_state_set_contains(states.Value, 11) == 0,
            "Actual progress semantics are readonly, noneditable and nonfocusable");
        var rejected = RejectNumericWrite(progress.Value, 40, "ReadOnly");
        current = atspi_value_get_current_value(numeric.Value, &error);
        Error(ref error);
        check(rejected && current == 100, "The actual progress widget rejects native numeric writes without mutation");
        using var toggle = Find(window, value => Role(value) == 43 && Name(value) == "진행률 모드")
            ?? throw new InvalidOperationException("The actual progress mode button was not exported.");
        using var action = new NativeObject(atspi_accessible_get_action_iface(toggle.Value));
        void Toggle()
        {
            nint toggleError = 0;
            var accepted = atspi_action_do_action(action.Value, 0, &toggleError);
            Error(ref toggleError);
            if (accepted == 0)
                throw new InvalidOperationException("The actual progress mode action was rejected.");
        }
        Toggle();
        Wait(() => !HasNumericInterface(progress.Value), "the cached progress removes its Value interface in indeterminate mode");
        using (var same = Find(window, value => Role(value) == 42 && Name(value) == "진행 상태"))
            check(same?.Value == progress.Value, "Indeterminate progress retains native identity while removing the cached numeric interface");
        Toggle();
        Wait(() => HasNumericInterface(progress.Value), "the cached progress restores its Value interface in determinate mode");
        using var restored = new NativeObject(atspi_accessible_get_value_iface(progress.Value));
        current = atspi_value_get_current_value(restored.Value, &error);
        Error(ref error);
        check(current == 100, "Determinate progress restores its native numeric contract without clearing the client cache");
    }
}
