using System.Text.Json;

internal static unsafe partial class AtSpiProbe
{
    private static void VerifySelectionPolicy(nint application, string probeOutput)
    {
        var output = Path.Combine(Path.GetDirectoryName(probeOutput)!, "selection-policy.json");
        if (File.Exists(output))
            throw new IOException("The selection-policy result already exists.");
        List<string> checks = [];
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            checks.Add(message);
            Console.WriteLine("PASS " + message);
        }
        using var mandatory = Find(application, value => Name(value) == "mandatory selection fixture")
            ?? throw new InvalidDataException("The mandatory selection fixture is missing.");
        using var mandatorySelection = new NativeObject(atspi_accessible_get_selection_iface(mandatory.Value));
        using var states = new NativeObject(atspi_accessible_get_state_set(mandatory.Value));
        Check(atspi_state_set_contains(states.Value, 33) == 0, "A mandatory selection without a form requirement omits the AT-SPI required state");
        using (var selected = SelectedChild(mandatorySelection.Value, 0))
            Check(SelectionCount(mandatorySelection.Value) == 1 && selected is not null && Name(selected.Value) == "mandatory choice",
                "A required native selection exposes its actual selected child");
        Check(!SelectionAction(mandatorySelection.Value, 0, SelectionOperation.Clear) && !SelectionAction(mandatorySelection.Value, 0, SelectionOperation.DeselectSelected)
            && !SelectionAction(mandatorySelection.Value, 0, SelectionOperation.SelectAll) && SelectionCount(mandatorySelection.Value) == 1,
            "Mandatory single selection refuses clearing, removing the selected child and selecting all children");
        using var disabled = Find(application, value => Name(value) == "disabled selection fixture")
            ?? throw new InvalidDataException("The disabled selection fixture is missing.");
        using var disabledSelection = new NativeObject(atspi_accessible_get_selection_iface(disabled.Value));
        Check(!SelectionAction(disabledSelection.Value, 0, SelectionOperation.Clear) && !SelectionAction(disabledSelection.Value, 0, SelectionOperation.SelectAll)
            && !SelectionAction(disabledSelection.Value, 0, SelectionOperation.Select) && SelectionCount(disabledSelection.Value) == 1,
            "Disabled native selection containers reject changes without removing their existing selection");
        using var bulk = Find(application, value => Name(value) == "bulk preflight fixture")
            ?? throw new InvalidDataException("The bulk selection fixture is missing.");
        using var bulkSelection = new NativeObject(atspi_accessible_get_selection_iface(bulk.Value));
        Check(!SelectionAction(bulkSelection.Value, 0, SelectionOperation.SelectAll) && SelectionCount(bulkSelection.Value) == 0,
            "SelectAll rejects a known disabled target before selecting an earlier eligible child");
        using var plain = Find(application, value => Name(value) == "nonnumeric fixture")
            ?? throw new InvalidDataException("The plain fixture is missing.");
        var unsupported = atspi_accessible_get_selection_iface(plain.Value);
        try
        {
            Check(unsupported == 0, "Plain native elements do not advertise an unsupported Selection interface");
        }
        finally { if (unsupported != 0) g_object_unref(unsupported); }
        using var mandatoryMultiple = Find(application, value => Name(value) == "mandatory multiple fixture")
            ?? throw new InvalidDataException("The mandatory multiple fixture is missing.");
        using var mandatoryMultiSelection = new NativeObject(atspi_accessible_get_selection_iface(mandatoryMultiple.Value));
        Check(!SelectionAction(mandatoryMultiSelection.Value, 0, SelectionOperation.Clear)
            && SelectionCount(mandatoryMultiSelection.Value) == 2,
            "Clearing mandatory multiple selection refuses the complete operation before removing an earlier child");
        using var watch = new EventWatch(requiredState : true) { RequiredTarget = mandatory.Value };
        File.WriteAllText(probeOutput + ".required-on", "");
        Wait(() => watch.RequiredStates.Count >= 1, "the required state being set");
        using (var requiredStates = new NativeObject(atspi_accessible_get_state_set(mandatory.Value)))
            Check(atspi_state_set_contains(requiredStates.Value, 33) != 0,
                "The container exposes AT-SPI required state after the form requirement is set");
        File.WriteAllText(probeOutput + ".required-off", "");
        Wait(() => watch.RequiredStates.Count >= 2, "the required state being cleared");
        watch.Check();
        using var finalStates = new NativeObject(atspi_accessible_get_state_set(mandatory.Value));
        Check(watch.RequiredStates.SequenceEqual([1, 0]) && atspi_state_set_contains(finalStates.Value, 33) == 0,
            "Toggling the form requirement emits set and clear events in order while the selection rule stays active");
        File.WriteAllText(output, JsonSerializer.Serialize(new LinuxPolicyResult(checks.Count, checks), LinuxProbeResultsJson.Default.LinuxPolicyResult));
    }
}
