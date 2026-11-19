internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryNavigationBar(nint window,
        Func<string, nint, int?, NativeObject> required, List<string> checks, Action<string>? mark = null)
    {
        const int navigationRole = 110;
        const int buttonRole = 43;
        const int groupRole = 99;

        using var document = required("문서 작업", window, navigationRole);
        using var originalTitle = required("회의록", document.Value, null);
        using var back = required("돌아가기", document.Value, buttonRole);
        using var save = required("저장", document.Value, buttonRole);
        checks.Add("AT-SPI exposes the NavigationBar landmark, title and both actions.");

        Reveal(back);
        mark?.Invoke("before-back");
        InvokeNamed(back, "click");
        using var listTitle = required("문서 목록", document.Value, null);
        mark?.Invoke("back");
        checks.Add("Invoking the leading action changes the title in the same landmark.");

        InvokeNamed(save, "click");
        using var firstSaveTitle = required("저장 1회", document.Value, null);
        mark?.Invoke("save-one");
        InvokeNamed(save, "click");
        using var secondSaveTitle = required("저장 2회", document.Value, null);
        mark?.Invoke("save-two");
        checks.Add("The trailing action stays usable and preserves its counter as the title changes.");

        using var period = required("기간 탐색", window, navigationRole);
        using var segments = required("기간", period.Value, groupRole);
        Reveal(segments);
        mark?.Invoke("period-reveal");
        using var selection = new NativeObject(atspi_accessible_get_selection_iface(segments.Value));
        if (!SelectionAction(selection.Value, 1, SelectionOperation.Select))
            throw new InvalidOperationException("The NavigationBar secondary selector refused the weekly period.");
        WaitForSelection("주간");
        mark?.Invoke("week");

        using var today = required("오늘", period.Value, buttonRole);
        Reveal(today);
        mark?.Invoke("today-reveal");
        InvokeNamed(today, "click");
        WaitForSelection("일간");
        mark?.Invoke("day");
        checks.Add("AT-SPI selects the weekly period in the secondary row and the leading action restores the daily period.");

        void WaitForSelection(string label)
        {
            Wait(() =>
            {
                using var selected = SelectedChild(selection.Value, 0);
                return SelectionCount(selection.Value) == 1 && selected is not null && Name(selected.Value) == label;
            }, label + " NavigationBar period");
        }
    }
}
