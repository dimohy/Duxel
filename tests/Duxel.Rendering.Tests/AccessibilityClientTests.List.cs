using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckList(Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check, bool retirementOnly = false)
    {
        // Client ABI: microsoft/win32metadata UIAutomationClient.h, Grid and GridItem patterns.
        static int ReadInt(nint pattern, int slot)
        {
            int value = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(pattern, slot))(pattern, &value));
            return value;
        }
        static void Wait(Func<bool> predicate, string description)
        {
            var limit = Environment.TickCount64 + 5000;
            while (!predicate())
            {
                if (Environment.TickCount64 >= limit)
                    throw new TimeoutException("List native state did not commit: " + description);
                Thread.Sleep(5);
            }
        }
        static void Invoke(nint element)
        {
            var pattern = Pattern(element, 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern));
            }
            finally { Marshal.Release(pattern); }
        }
        var list = found["Native list"];
        var grid = Pattern(list, 10006);
        owned.Add(grid);
        check(ReadInt(grid, 4) == 2 && ReadInt(grid, 5) == 2, "external List Grid reports two loaded rows and two logical columns");
        var identity = Text(list, 30011);
        void Retire()
        {
            var editor = found["Native list editor"];
            Invoke(found["Hide list"]);
            Wait(() => TooltipElementUnavailable(list), "hidden grid retirement");
            int rows = -1;
            var result = ((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(grid, 4))(grid, &rows);
            var editorUnavailable = TooltipElementUnavailable(editor);
            nint staleCell = 0;
            var itemResult = ((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Slot(grid, 3))(grid, 0, 0, &staleCell);
            if (staleCell != 0)
                owned.Add(staleCell);
            Console.WriteLine($"LIST_RETIREMENT rowCountHr=0x{result:X8} rows={rows} editorUnavailable={editorUnavailable} itemHr=0x{itemResult:X8} cell={staleCell}");
            // Client scalar getters may supply defaults. Availability is an element/operation contract.
            check(TooltipElementUnavailable(list) && editorUnavailable && itemResult == UiaNative.Unavailable && staleCell == 0,
                "external hidden List retires grid identity, GetItem operation and owned editor providers");
        }
        if (retirementOnly)
        {
            Retire();
            Invoke(found["Finish"]);
            return;
        }
        nint Cell(int row, int column)
        {
            nint cell = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Slot(grid, 3))(grid, row, column, &cell));
            owned.Add(cell);
            return cell;
        }
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 2; column++)
            {
                var cell = Cell(row, column);
                var item = Pattern(cell, 10007);
                owned.Add(item);
                check(ReadInt(item, 4) == row && ReadInt(item, 5) == column && ReadInt(item, 6) == 1 && ReadInt(item, 7) == 1,
                    $"external List cell {row},{column} publishes correct GridItem coordinates and spans");
                nint container = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(item, 3))(item, &container));
                owned.Add(container);
                check(Text(container, 30011) == identity, $"external List cell {row},{column} returns its real containing grid");
            }
        nint invalid = 0;
        var invalidResult = ((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Slot(grid, 3))(grid, 2, 0, &invalid);
        if (invalid != 0)
            owned.Add(invalid);
        check(invalidResult == unchecked((int)0x80070057) && invalid == 0, "external List Grid rejects an out-of-range item with E_INVALIDARG");
        var rowB = found["Native row B"];
        var selection = Pattern(rowB, 10010);
        owned.Add(selection);
        Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(selection, 3))(selection));
        Wait(() => ReadInt(selection, 6) != 0, "row B selection");
        check(ReadInt(selection, 6) == 1, "external List SelectionItem selects its real row");
        var edit = found["Native list editor"];
        var value = Pattern(edit, 10002);
        owned.Add(value);
        var text = Marshal.StringToBSTR("native list");
        try
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(value, 3))(value, text));
        }
        finally { Marshal.FreeBSTR(text); }
        Wait(() => Text(edit, 30045) == "native list", "editor Value");
        check(Text(edit, 30045) == "native list", "external List content retains its editable Value provider");
        Invoke(found["Disable list"]);
        Wait(() => !Boolean(rowB, 30010), "disabled row");
        var disabledResult = ((delegate* unmanaged[Stdcall]<nint, int>)Slot(selection, 3))(selection);
        check(disabledResult == unchecked((int)0x80040200), "external disabled List rejects SelectionItem changes with element-not-enabled");
        Retire();
        Invoke(found["Finish"]);
    }
}
