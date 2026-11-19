using System.Runtime.InteropServices;

internal static unsafe partial class AtSpiProbe
{
    private enum SelectionOperation
    {
        Select, Deselect, DeselectSelected, SelectAll, Clear, IsSelected
    }
    private static int SelectionCount(nint selection)
    {
        nint error = 0;
        var result = atspi_selection_get_n_selected_children(selection, &error);
        Error(ref error);
        return result;
    }
    private static NativeObject? SelectedChild(nint selection, int index)
    {
        nint error = 0;
        var child = atspi_selection_get_selected_child(selection, index, &error);
        Error(ref error);
        return child == 0 ? null : new(child);
    }
    private static bool SelectionAction(nint selection, int index, SelectionOperation operation)
    {
        nint error = 0;
        var result = operation switch
        {
            SelectionOperation.Select => atspi_selection_select_child(selection, index, &error),
            SelectionOperation.Deselect => atspi_selection_deselect_child(selection, index, &error),
            SelectionOperation.DeselectSelected => atspi_selection_deselect_selected_child(selection, index, &error),
            SelectionOperation.SelectAll => atspi_selection_select_all(selection, &error),
            SelectionOperation.Clear => atspi_selection_clear_selection(selection, &error),
            SelectionOperation.IsSelected => atspi_selection_is_child_selected(selection, index, &error),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        Error(ref error);
        return result != 0;
    }
    private static void VerifySelection(nint window, EventWatch events, Action<bool, string> check)
    {
        using var single = Find(window, value => Role(value) == 98 && Name(value) == "도시 하나")
            ?? throw new InvalidDataException("The actual single-selection list was not exported.");
        using var singleSelection = new NativeObject(atspi_accessible_get_selection_iface(single.Value));
        events.Selection = single.Value;
        using (var selected = SelectedChild(singleSelection.Value, 0))
            check(SelectionCount(singleSelection.Value) == 1 && selected is not null && Name(selected.Value) == "서울",
                "The native selection interface exposes the actual single list's initial selected child");
        using var busan = Child(single.Value, 1);
        using (var states = new NativeObject(atspi_accessible_get_state_set(busan.Value)))
            check(atspi_state_set_contains(states.Value, 23) == 0, "The client caches an initially unselected row state");
        var eventCount = events.Selections;
        check(SelectionAction(singleSelection.Value, 1, SelectionOperation.Select), "Native SelectChild replaces a single selection through the shared list binding");
        Wait(() => { events.Check(); return events.Selections > eventCount; }, "the actual native SelectionChanged event");
        using (var selected = SelectedChild(singleSelection.Value, 0))
        using (var states = new NativeObject(atspi_accessible_get_state_set(busan.Value)))
            check(selected?.Value == busan.Value && SelectionCount(singleSelection.Value) == 1 && atspi_state_set_contains(states.Value, 23) != 0,
                "Selection events update cached row state without clearing the native cache");
        check(SelectionAction(singleSelection.Value, 0, SelectionOperation.DeselectSelected) && SelectionCount(singleSelection.Value) == 0,
            "DeselectSelectedChild uses the selected subset index rather than the original child index");
        check(!SelectionAction(singleSelection.Value, 2, SelectionOperation.Select) && SelectionCount(singleSelection.Value) == 0,
            "A row with disabled selection rejects native selection without changing the binding");
        check(!SelectionAction(singleSelection.Value, -1, SelectionOperation.Select) && !SelectionAction(singleSelection.Value, int.MaxValue, SelectionOperation.IsSelected),
            "Out-of-range native selection child indices return false");
        using (var missing = SelectedChild(singleSelection.Value, 0))
            check(missing is null, "GetSelectedChild returns the native null reference for a missing selected index");
        check(SelectionAction(singleSelection.Value, 1, SelectionOperation.Select) && !SelectionAction(singleSelection.Value, 0, SelectionOperation.SelectAll)
            && SelectionCount(singleSelection.Value) == 1 && SelectionAction(singleSelection.Value, 1, SelectionOperation.IsSelected),
            "Single-selection SelectAll refuses multiple children without replacing the current choice");
        check(SelectionAction(singleSelection.Value, 0, SelectionOperation.Clear) && SelectionCount(singleSelection.Value) == 0,
            "ClearSelection removes the optional single list selection");
        using var multiple = Find(window, value => Role(value) == 98 && Name(value) == "도시 여러 개")
            ?? throw new InvalidDataException("The actual multiple-selection list was not exported.");
        using var multiSelection = new NativeObject(atspi_accessible_get_selection_iface(multiple.Value));
        check(SelectionCount(multiSelection.Value) == 1 && SelectionAction(multiSelection.Value, 1, SelectionOperation.Select)
            && SelectionCount(multiSelection.Value) == 2 && SelectionAction(multiSelection.Value, 0, SelectionOperation.IsSelected) && SelectionAction(multiSelection.Value, 1, SelectionOperation.IsSelected),
            "Native multi-selection SelectChild adds a child while preserving earlier selected children");
        check(SelectionAction(multiSelection.Value, 0, SelectionOperation.Deselect) && SelectionCount(multiSelection.Value) == 1
            && SelectionAction(multiSelection.Value, 1, SelectionOperation.IsSelected), "DeselectChild uses the original child index in a multiple selection");
        check(SelectionAction(multiSelection.Value, 0, SelectionOperation.DeselectSelected) && SelectionCount(multiSelection.Value) == 0,
            "Selected-subset deselection removes the remaining nonzero-index child");
        check(SelectionAction(multiSelection.Value, 0, SelectionOperation.SelectAll) && SelectionCount(multiSelection.Value) == 3,
            "Native SelectAll selects every enabled child through the actual multiple list");
        using (var third = SelectedChild(multiSelection.Value, 2))
            check(third is not null && Name(third.Value) == "제주", "Selected children retain their actual native child order");
        check(SelectionAction(multiSelection.Value, 0, SelectionOperation.Clear) && SelectionCount(multiSelection.Value) == 0,
            "Native ClearSelection removes every multiple-list selection");
    }

    [LibraryImport(Library)] private static partial nint atspi_accessible_get_selection_iface(nint value);
    [LibraryImport(Library)] private static partial int atspi_selection_get_n_selected_children(nint selection, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_selection_get_selected_child(nint selection, int index, nint* error);
    [LibraryImport(Library)] private static partial int atspi_selection_select_child(nint selection, int index, nint* error);
    [LibraryImport(Library)] private static partial int atspi_selection_deselect_child(nint selection, int index, nint* error);
    [LibraryImport(Library)] private static partial int atspi_selection_deselect_selected_child(nint selection, int index, nint* error);
    [LibraryImport(Library)] private static partial int atspi_selection_is_child_selected(nint selection, int index, nint* error);
    [LibraryImport(Library)] private static partial int atspi_selection_select_all(nint selection, nint* error);
    [LibraryImport(Library)] private static partial int atspi_selection_clear_selection(nint selection, nint* error);
}
