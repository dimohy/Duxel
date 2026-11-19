using System.Runtime.InteropServices;
using Duxel.Platform.Windows;

// The test is a separate client process. Slots are pinned to UIAutomationClient.h in source-manifest.json.
internal static unsafe partial class AccessibilityClientTests
{
    internal static nint WindowForProcess(int processId, string? title = null)
    {
        var search = new WindowSearch(processId, title);
        var handle = GCHandle.Alloc(search);
        try
        {
            EnumWindows(&VisitWindow, GCHandle.ToIntPtr(handle));
            return search.Window;
        }
        finally { handle.Free(); }
    }
    private sealed class WindowSearch(int processId, string? title)
    {
        internal readonly int ProcessId = processId;
        internal readonly string? Title = title;
        internal nint Window;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static int VisitWindow(nint window, nint parameter)
    {
        var search = (WindowSearch)GCHandle.FromIntPtr(parameter).Target!;
        GetWindowThreadProcessId(window, out var processId);
        if (processId == search.ProcessId)
        {
            char* name = stackalloc char[256];
            var length = GetClassNameW(window, name, 256);
            if (new ReadOnlySpan<char>(name, length).StartsWith("Duxel.WindowClass.", StringComparison.Ordinal))
            {
                if (search.Title is { } expected)
                {
                    var titleLength = GetWindowTextW(window, name, 256);
                    if (!new ReadOnlySpan<char>(name, titleLength).SequenceEqual(expected))
                        return 1;
                }
                search.Window = window;
                return 0;
            }
        }
        return 1;
    }
    private static nint Slot(nint instance, int index) => (*(nint**)instance)[index];
    private static void Ok(int hr) => Marshal.ThrowExceptionForHR(hr);
    internal static void Run(nint window, Action<bool, string> check, bool field = false, bool lifetime = false,
        bool spinner = false, bool tooltip = false, bool tooltipEvents = false, string? tooltipHostVisibility = null,
        bool tooltipProperties = false, bool tooltipShutdown = false, bool requireShutdownEvent = true,
        bool tooltipScroll = false, bool tabSelection = false, bool relationshipEvents = false, string? focusEvents = null,
        bool menu = false, bool tabOverflow = false, bool menuScroll = false, bool menuExpansion = false, bool menuCheckbox = false, bool menuRadio = false, bool menuContent = false,
        bool sliderRange = false, string? rangeProbeDirectory = null, bool galleryTimePicker = false,
        bool galleryTimePickerWheel = false, bool galleryDatePickers = false, bool leaveGalleryTimePopupOpen = false, bool galleryHolidays = false)
    {
        Ok(CoInitializeEx(0, 0));
        nint automation = 0, root = 0, condition = 0, elements = 0;
        var owned = new List<nint>();
        try
        {
            var clsid = new Guid("ff48dba4-60ef-4201-aa87-54103eef594e");
            var iid = new Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
            Ok(CoCreateInstance(in clsid, 0, 1, in iid, out automation));
            Ok(((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Slot(automation, 6))(automation, window, &root));
            Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(automation, 21))(automation, &condition));
            if (galleryTimePicker || galleryDatePickers || galleryHolidays)
            {
                if (galleryHolidays)
                {
                    CheckGalleryPicker(window, root, condition, check, "공휴일 제공자", false);
                }
                else if (galleryDatePickers)
                {
                    CheckGalleryPicker(window, root, condition, check, "DatePicker", false);
                    CheckGalleryPicker(window, root, condition, check, "DateTimePicker", false);
                }
                else
                {
                    CheckGalleryPicker(window, root, condition, check, "TimePicker", galleryTimePickerWheel, leaveGalleryTimePopupOpen);
                }
                return;
            }
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(root, 6))(root, 4, condition, &elements));
            int length = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &length));
            var found = new Dictionary<string, nint>(StringComparer.Ordinal);
            var labels = new Dictionary<string, nint>(StringComparer.Ordinal);
            var fields = new List<nint>();
            for (var i = 0; i < length; i++)
            {
                nint element = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, i, &element));
                owned.Add(element);
                var name = Text(element, 30005);
                Console.WriteLine("UIA element: " + name);
                if (name.Length > 0)
                {
                    if (Text(element, 30012) == "Duxel.Label")
                        labels.TryAdd(name, element);
                    else
                        found.TryAdd(name, element);
                }
                if (name == "Work email")
                    fields.Add(element);
            }
            if (field)
            {
                CheckFields(window, found, fields, owned, check);
                return;
            }
            if (found.ContainsKey("Send polite announcement") || found.ContainsKey("Mount message"))
            {
                CheckAnnouncements(window, found, owned, check);
                return;
            }
            if (found.ContainsKey("Complete Skeleton"))
            {
                CheckSkeleton(automation, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("Change presence"))
            {
                CheckPresence(automation, found, owned, check);
                return;
            }
            if (found.ContainsKey("Open modal"))
            {
                CheckDialog(automation, window, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("프로필 편집 열기"))
            {
                CheckPopover(automation, window, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("입력 팝업 열기"))
            {
                CheckPopupInput(automation, window, root, found, labels, owned, check);
                return;
            }
            if (found.ContainsKey("Native toolbar"))
            {
                CheckToolbar(window, found, labels, check);
                return;
            }
            if (found.ContainsKey("Native search"))
            {
                CheckSearchBox(automation, window, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native list"))
            {
                CheckList(found, owned, check, retirementOnly: found.ContainsKey("Retirement probe"));
                return;
            }
            if (found.ContainsKey("Native Markdown viewer"))
            {
                CheckMarkdown(automation, window, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native combobox"))
            {
                CheckCombobox(automation, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native dropdown"))
            {
                CheckDropdown(automation, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native avatar group"))
            {
                CheckAvatarGroup(automation, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native breadcrumb"))
            {
                CheckBreadcrumb(automation, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("Rename persona"))
            {
                CheckPersona(automation, found, labels, owned, check);
                return;
            }
            if (found.ContainsKey("Native Avatar"))
            {
                CheckAvatar(automation, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native Card"))
            {
                CheckCard(automation, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native Accordion"))
            {
                CheckAccordion(automation, root, condition, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native label target"))
            {
                CheckLabels(window, automation, root, found, owned, check);
                return;
            }
            if (found.ContainsKey("Mixed checkbox") && found.ContainsKey("Shared switch"))
            {
                CheckToggleControls(automation, found, owned, check);
                return;
            }
            if (found.ContainsKey("Native radio choice"))
            {
                CheckRadioControls(automation, root, found, owned, check);
                return;
            }
            if (sliderRange)
            {
                CheckSliderRange(automation, root, found, owned, check);
                return;
            }
            if (rangeProbeDirectory is not null)
            {
                ProbeSliderRetirement(found, owned, check, rangeProbeDirectory);
                return;
            }
            if (menu || tabOverflow || menuScroll || menuExpansion || menuCheckbox || menuRadio || menuContent)
            {
                CheckMenu(automation, root, condition, found, owned, check, tabOverflow, menuScroll, menuExpansion, menuCheckbox, menuRadio, menuContent);
                return;
            }
            if (lifetime)
            {
                CheckLifetime(root, condition, found, owned, check);
                return;
            }
            if (spinner)
            {
                CheckSpinners(found, check);
                return;
            }
            if (tabSelection)
            {
                CheckTabSelection(automation, root, found, owned, check);
                return;
            }
            if (relationshipEvents)
            {
                CheckRelationshipEvents(automation, root, found, owned, check);
                return;
            }
            if (focusEvents is not null)
            {
                CheckFocusEvents(automation, root, found, owned, check, focusEvents);
                return;
            }
            if (tooltip)
            {
                CheckTooltip(found, check);
                return;
            }
            if (tooltipEvents)
            {
                CheckTooltipEvents(automation, root, condition, found, owned, check);
                return;
            }
            if (tooltipHostVisibility is not null)
            {
                CheckTooltipHostVisibility(window, automation, root, condition, found, owned, tooltipHostVisibility, check,
                    tooltipShutdown, requireShutdownEvent);
                return;
            }
            if (tooltipProperties)
            {
                CheckTooltipProperties(automation, root, condition, found, owned, check);
                return;
            }
            if (tooltipScroll)
            {
                CheckTooltipScroll(automation, root, condition, found, owned, check);
                return;
            }
            check(found.ContainsKey("Small Outline") && found.ContainsKey("Readonly") && found.ContainsKey("Disabled") && found.ContainsKey("Clear"), "external Windows UIA client discovers real input gallery controls");
            var input = found["Small Outline"];
            var value = Pattern(input, 10002);
            owned.Add(value);
            var text = Marshal.StringToBSTR("UIA-123");
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(value, 3))(value, text));
            }
            finally { Marshal.FreeBSTR(text); }
            check(Text(input, 30045) == "UIA-123" && Text(found["Readonly"], 30045) == "UIA-123", "external UIA SetValue reaches shared live widget binding");
            var readOnly = Pattern(found["Readonly"], 10002);
            owned.Add(readOnly);
            text = Marshal.StringToBSTR("rejected");
            try
            {
                check(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(readOnly, 3))(readOnly, text) < 0, "external UIA read-only SetValue is rejected");
            }
            finally { Marshal.FreeBSTR(text); }
            var invoke = Pattern(found["Clear"], 10000);
            owned.Add(invoke);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(invoke, 3))(invoke));
            var deadline = Environment.TickCount64 + 2000;
            while (Text(input, 30045).Length != 0 && Environment.TickCount64 < deadline)
                Thread.Sleep(10);
            check(Text(input, 30045) == "", "external UIA Invoke executes the actual Clear button callback");
        }
        finally
        {
            foreach (var pointer in owned)
                Marshal.Release(pointer);
            if (elements != 0)
                Marshal.Release(elements);
            if (condition != 0)
                Marshal.Release(condition);
            if (root != 0)
                Marshal.Release(root);
            if (automation != 0)
                Marshal.Release(automation);
            CoUninitialize();
        }
    }
    private static void CheckTooltip(Dictionary<string, nint> found, Action<bool, string> check)
    {
        var footer = found.FirstOrDefault(pair => pair.Key.StartsWith("Footer : ", StringComparison.Ordinal));
        var inset = found.FirstOrDefault(pair => pair.Key.StartsWith("Inset : ", StringComparison.Ordinal));
        check(footer.Value != 0 && inset.Value != 0, "external UIA discovers both actual sample buttons");
        const string description = "Reserve space below the editor. Hover or focus to inspect the custom tooltip.";
        check(Text(footer.Value, 30013) == description, "external UIA reads tooltip text from the actual target HelpText");
        check(Text(footer.Value, 30005).StartsWith("Footer : ", StringComparison.Ordinal),
            "external UIA description preserves the target button name");
        check(Text(inset.Value, 30013) == string.Empty, "external UIA tooltip scope does not leak to the neighboring button");
        check(Boolean(footer.Value, 30009), "external UIA target remains keyboard focusable");
        var validSource = true;
        if (found.TryGetValue(description, out var surface))
        {
            UiaVariant type = default;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(surface, 10))(surface, 30003, &type));
            try
            {
                validSource = type.Type == 3 && type.Data.Integer == 50022 && !Boolean(surface, 30009) && !Boolean(surface, 30017);
            }
            finally
            {
                UiaNative.VariantClear(ref type);
            }
        }
        check(validSource, "external UIA description belongs only to a real plain tooltip when its surface is shown");
    }

    private static void CheckSpinners(Dictionary<string, nint> found, Action<bool, string> check)
    {
        var names = new[] { "Size 16", "Size 20", "Size 24", "Size 28", "Size 32", "Size 36", "Size 40", "Size 44", "After", "Before", "Above", "Below", "Reduced motion", "Right to left", "Shown after one second", "Downloading content …" };
        check(names.All(found.ContainsKey), "external UIA discovers all sixteen actual Spinner variants and label placements");
        foreach (var name in names)
        {
            var element = found[name];
            UiaVariant type = default;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, 30003, &type));
            try
            {
                check(type.Type == 3 && type.Data.Integer == 50012 && !Boolean(element, 30009), "external Spinner has ProgressBar role and is not focusable: " + name);
            }
            finally { UiaNative.VariantClear(ref type); }
            nint pattern = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(element, 16))(element, 10003, &pattern));
            try
            {
                check(pattern == 0, "external indeterminate Spinner exposes no fabricated RangeValue pattern: " + name);
            }
            finally { if (pattern != 0) Marshal.Release(pattern); }
        }
    }
    private static void CheckLifetime(nint root, nint condition, Dictionary<string, nint> found, List<nint> owned, Action<bool, string> check)
    {
        check(found.Keys.Contains("Transient") && found.Keys.Contains("Remove") && found.Keys.Contains("Restore") && found.Keys.Contains("Finish"), "external client discovers the real lifetime fixture controls");
        var stale = found["Transient"];
        var oldIdentity = RuntimeIdentity(stale);
        void Invoke(nint element)
        {
            var pattern = Pattern(element, 10000);
            try
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern));
            }
            finally { Marshal.Release(pattern); }
        }
        Invoke(found["Remove"]);
        var deadline = Environment.TickCount64 + 3000;
        int staleResult = 0;
        while (Environment.TickCount64 < deadline)
        {
            UiaVariant value = default;
            staleResult = ((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(stale, 10))(stale, 30005, &value);
            UiaNative.VariantClear(ref value);
            if (staleResult < 0)
                break;
            Thread.Sleep(5);
        }
        check(staleResult == UiaNative.Unavailable, "retained external client element becomes unavailable after actual widget removal");
        Invoke(found["Restore"]);
        nint replacement = 0;
        deadline = Environment.TickCount64 + 3000;
        while (replacement == 0 && Environment.TickCount64 < deadline)
        {
            nint elements = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(root, 6))(root, 4, condition, &elements));
            try
            {
                int count = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
                for (var i = 0; i < count; i++)
                {
                    nint element = 0;
                    Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, i, &element));
                    if (Text(element, 30005) == "Transient")
                    {
                        replacement = element;
                        owned.Add(element);
                        break;
                    }
                    Marshal.Release(element);
                }
            }
            finally { Marshal.Release(elements); }
            if (replacement == 0)
                Thread.Sleep(5);
        }
        check(replacement != 0 && RuntimeIdentity(replacement) != oldIdentity, "recreated real widget receives a distinct external UIA runtime identity");
        UiaVariant oldValue = default;
        var oldHr = ((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(stale, 10))(stale, 30005, &oldValue);
        UiaNative.VariantClear(ref oldValue);
        check(oldHr == UiaNative.Unavailable && Text(replacement, 30005) == "Transient", "old client does not bind to a new widget with the same logical ID");
        Invoke(found["Finish"]);
    }
    private static void CheckMenu(nint automation, nint root, nint condition, Dictionary<string, nint> found, List<nint> owned,
        Action<bool, string> check, bool tabOverflow = false, bool menuScroll = false, bool menuExpansion = false, bool menuCheckbox = false, bool menuRadio = false, bool menuContent = false)
    {
        var trigger = found[tabOverflow ? "2 more tabs" : "Actions"];
        void Invoke(nint element)
        {
            var pattern = Pattern(element, 10000);
            owned.Add(pattern);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern));
        }
        bool NameMatches(nint element, string name)
        {
            UiaVariant value = default;
            var hr = ((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, 30005, &value);
            if (hr == UiaNative.Unavailable)
                return false;
            Ok(hr);
            try
            {
                return value.Type == 8 && (value.Data.Pointer == 0 ? name.Length == 0 : Marshal.PtrToStringBSTR(value.Data.Pointer) == name);
            }
            finally { UiaNative.VariantClear(ref value); }
        }
        nint Find(string name, int type)
        {
            var deadline = Environment.TickCount64 + 3000;
            while (Environment.TickCount64 < deadline)
            {
                nint elements = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(root, 6))(root, 4, condition, &elements));
                try
                {
                    int count = 0;
                    Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(elements, 3))(elements, &count));
                    for (var i = 0; i < count; i++)
                    {
                        nint element = 0;
                        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(elements, 4))(elements, i, &element));
                        if (NameMatches(element, name) && ControlType(element) == type)
                        {
                            owned.Add(element);
                            return element;
                        }
                        Marshal.Release(element);
                    }
                }
                finally { Marshal.Release(elements); }
                Thread.Sleep(5);
            }
            throw new InvalidOperationException("External menu control did not appear : " + name);
        }
        if (menuContent)
        {
            Invoke(trigger);
            var contentItem = Find("One", 50011);
            check(Text(contentItem, 30005) == "One" && Text(contentItem, 30013) == "ZIP",
                "external menu content keeps its action Name and exposes secondary information as HelpText");
            var contentDisabled = Find("Disabled", 50011);
            check(!Boolean(contentDisabled, 30010) && Text(contentDisabled, 30013) == "Locked",
                "external disabled content row retains its secondary HelpText");
            Invoke(contentItem);
            var contentDeadline = Environment.TickCount64 + 3000;
            while (!TooltipElementUnavailable(contentItem) && Environment.TickCount64 < contentDeadline)
                Thread.Sleep(5);
            check(TooltipElementUnavailable(contentItem), "external rich menu action dismisses and retires its source");
            var contentExpand = Pattern(trigger, 10005);
            owned.Add(contentExpand);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(contentExpand, 3))(contentExpand));
            var contentFinish = Find("Finish", 50011);
            check(Text(contentFinish, 30013) == "Done", "external reopened menu exposes trailing information on a row without an icon");
            Invoke(contentFinish);
            return;
        }
        if (menuRadio)
        {
            CheckRadioMenu(automation, root, trigger, owned, check, Find, Invoke);
            return;
        }
        if (menuCheckbox)
        {
            Invoke(trigger);
            var option = Find("One", 50011);
            var toggle = Pattern(option, 10015);
            owned.Add(toggle);
            int State()
            {
                var state = -1;
                Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(toggle, 4))(toggle, &state));
                return state;
            }
            check(Boolean(option, 30041) && State() == 0, "external checkbox menu exposes native Toggle and initial off state");
            var checkboxIdentity = RuntimeIdentity(option);
            var optionId = Text(option, 30011);
            ReadPropertyChanges(automation, option, 1, owned, [30086], properties =>
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(toggle, 3))(toggle));
                check(State() == 1 && properties.Wait() == new TooltipNativeProperty(30086, optionId, 1)
                    && RuntimeIdentity(Find("One", 50011)) == checkboxIdentity,
                    "external Toggle commits on, reports the original property source and retains the open menu identity");
                Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(toggle, 3))(toggle));
                check(State() == 0 && properties.Wait() == new TooltipNativeProperty(30086, optionId, 0),
                    "external second Toggle commits off on the same retained provider");
                var disabled = Pattern(Find("Disabled", 50011), 10015);
                owned.Add(disabled);
                check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(disabled, 3))(disabled) == UiaNative.Disabled,
                    "external disabled checkbox Toggle returns UIA_E_ELEMENTNOTENABLED");
            });
            var expander = Pattern(trigger, 10005);
            owned.Add(expander);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 4))(expander));
            check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(toggle, 3))(toggle) == UiaNative.Unavailable,
                "external retained Toggle mutation rejects the closed menu");
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 3))(expander));
            check(RuntimeIdentity(Find("One", 50011)) != checkboxIdentity, "external checkbox menu reopen publishes a fresh runtime identity");
            Invoke(Find("Finish", 50011));
            return;
        }
        if (menuExpansion)
        {
            var expander = Pattern(trigger, 10005);
            owned.Add(expander);
            int State()
            {
                var state = -1;
                Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(expander, 5))(expander, &state));
                return state;
            }
            check(Boolean(trigger, 30028) && State() == 0, "external menu button exposes a real initially collapsed ExpandCollapse pattern");
            var triggerId = Text(trigger, 30011);
            ReadPopupEvents(automation, root, owned, "Actions", 20003, 20007, sink =>
            {
                ReadPropertyChanges(automation, trigger, 1, owned, [30070], properties =>
                {
                    Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 3))(expander));
                    var openedMenu = sink.Wait(20003);
                    check(State() == 1 && openedMenu.ControlType == 50009 && openedMenu.Name == "Actions" && !openedMenu.Offscreen,
                        "external Expand commits state and delivers a cached real MenuOpened source");
                    check(properties.Wait() == new TooltipNativeProperty(30070, triggerId, 1),
                        "external UIA receives the expanded-state property on the original trigger");
                    var openedSurface = Find("Actions", 50009);
                    Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 3))(expander));
                    Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 4))(expander));
                    var closedMenu = sink.Wait(20007);
                    check(State() == 0 && closedMenu.Id == openedMenu.Id && closedMenu.ControlType == 50009 && closedMenu.Offscreen
                        && TooltipElementUnavailable(openedSurface), "external Collapse retires the actual surface and preserves its cached MenuClosed identity");
                    check(properties.Wait() == new TooltipNativeProperty(30070, triggerId, 0) && sink.TargetEvents == 2,
                        "idempotent expansion emits only one open and one close with the matching final property states");
                    Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 4))(expander));
                    Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 3))(expander));
                    sink.Wait(20003);
                    Invoke(Find("One", 50011));
                    sink.Wait(20007);
                    check(State() == 0, "actual menu item invocation collapses its original expansion provider");
                    Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(expander, 3))(expander));
                    sink.Wait(20003);
                    Invoke(Find("Finish", 50011));
                    var shutdownClose = sink.Wait(20007);
                    check(shutdownClose.ControlType == 50009 && shutdownClose.Offscreen && sink.TargetEvents == 6,
                        "menu shutdown preserves the final closed event without duplicate replay events");
                });
            });
            return;
        }
        if (menuScroll)
        {
            Invoke(trigger);
            var longSurface = Find("Actions", 50009);
            var first = Find("One", 50011);
            var last = Find("Finish", 50011);
            var scroll = Pattern(longSurface, 10004);
            owned.Add(scroll);
            var firstReveal = Pattern(first, 10017);
            var lastReveal = Pattern(last, 10017);
            owned.Add(firstReveal);
            owned.Add(lastReveal);
            double Percent()
            {
                double value = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, double*, int>)Slot(scroll, 6))(scroll, &value));
                return value;
            }
            check(Boolean(longSurface, 30034) && !Boolean(longSurface, 30057) && Boolean(longSurface, 30058) && Percent() == 0 && Boolean(last, 30022),
                "external long menu exposes vertical Scroll and offscreen MenuItem ScrollItem patterns");
            var sourceId = Text(longSurface, 30011);
            ReadPropertyChanges(automation, longSurface, 1, owned, [30055], sink =>
            {
                Ok(((delegate* unmanaged[Stdcall]<nint, double, double, int>)Slot(scroll, 4))(scroll, -1, 100));
                check(Percent() == 100 && !Boolean(last, 30022), "external menu SetScrollPercent reveals its real final row before returning");
                check(sink.Wait() == new TooltipNativeProperty(30055, sourceId, 100d),
                    "external menu scroll notification reports its actual source and final percentage");
            });
            var longIdentity = RuntimeIdentity(first);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(firstReveal, 3))(firstReveal));
            check(!Boolean(first, 30022) && RuntimeIdentity(first) == longIdentity && Percent() == 0,
                "external ScrollItem reveals the original first menu provider without replacing identity");
            Invoke(first);
            var deadline = Environment.TickCount64 + 3000;
            while (!TooltipElementUnavailable(longSurface) && Environment.TickCount64 < deadline)
                Thread.Sleep(5);
            double stale = 0;
            var staleResult = ((delegate* unmanaged[Stdcall]<nint, double*, int>)Slot(scroll, 6))(scroll, &stale);
            Console.WriteLine($"Retired menu percentage read : HRESULT = 0x{staleResult:X8}, value = {stale}");
            var staleAction = ((delegate* unmanaged[Stdcall]<nint, double, double, int>)Slot(scroll, 4))(scroll, -1, 0);
            check(TooltipElementUnavailable(longSurface) && staleAction == UiaNative.Unavailable,
                "closed long menu rejects mutations through its retained native scroll pattern");
            Invoke(trigger);
            var longReplacement = Find("Finish", 50011);
            var reveal = Pattern(longReplacement, 10017);
            owned.Add(reveal);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(reveal, 3))(reveal));
            check(!Boolean(longReplacement, 30022), "external ScrollItem reveals the last item through a reopened popup");
            Invoke(longReplacement);
            return;
        }
        if (tabOverflow)
        {
            check(ControlType(trigger) == 50019 && Boolean(trigger, 30031) && !Boolean(trigger, 30079),
                "external tab overflow action is an unselected TabItem with an actual Invoke pattern");
            var overflowExpand = Pattern(trigger, 10005);
            owned.Add(overflowExpand);
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(overflowExpand, 3))(overflowExpand));
            var overflowState = -1;
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(overflowExpand, 5))(overflowExpand, &overflowState));
            check(Boolean(trigger, 30028) && overflowState == 1, "external overflow TabItem exposes the same real ExpandCollapse pattern");
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(overflowExpand, 4))(overflowExpand));
            Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(overflowExpand, 5))(overflowExpand, &overflowState));
            check(overflowState == 0, "external overflow Collapse returns to the retained selected tab without invocation");
            Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(overflowExpand, 3))(overflowExpand));
            var hidden = Find("Four", 50011);
            check(Boolean(hidden, 30031), "external hidden tab is exposed through the shared actual MenuItem");
            Invoke(hidden);
            var selected = Find("Four", 50019);
            check(Boolean(selected, 30079), "external overflow invocation selects and promotes the actual original tab");
            CheckControlledPanel(selected, owned, check);
            var deadline = Environment.TickCount64 + 3000;
            while (!TooltipElementUnavailable(hidden) && Environment.TickCount64 < deadline)
                Thread.Sleep(5);
            check(TooltipElementUnavailable(hidden), "external overflow menu provider retires after the promoted selection commits");
            Invoke(Find("Finish overflow", 50000));
            return;
        }
        Invoke(trigger);
        var surface = Find("Actions", 50009);
        var item = Find("One", 50011);
        var disabled = Find("Disabled", 50011);
        check(ControlType(surface) == 50009 && ControlType(item) == 50011 && Boolean(item, 30031),
            "external UIA sees the real Menu and MenuItem control types with Invoke support");
        check(!Boolean(disabled, 30010) && !Boolean(disabled, 30009), "external disabled menu item is disabled and not keyboard focusable");
        var identity = RuntimeIdentity(item);
        Invoke(item);
        var closeDeadline = Environment.TickCount64 + 3000;
        while (!TooltipElementUnavailable(item) && Environment.TickCount64 < closeDeadline)
            Thread.Sleep(5);
        check(TooltipElementUnavailable(item), "external invoked menu item becomes unavailable after popup dismissal");
        Invoke(trigger);
        var replacement = Find("One", 50011);
        check(RuntimeIdentity(replacement) != identity && TooltipElementUnavailable(item),
            "external reopening preserves logical menu entries with fresh runtime identity and retired old clients");
        Invoke(Find("Finish", 50011));
    }
    private static string RuntimeIdentity(nint element)
    {
        nint array = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(element, 4))(element, &array));
        try
        {
            Ok(SafeArrayGetLBound(array, 1, out var first));
            Ok(SafeArrayGetUBound(array, 1, out var last));
            Ok(SafeArrayAccessData(array, out var pointer));
            try
            {
                return string.Join(",", new ReadOnlySpan<int>((void*)pointer, last - first + 1).ToArray());
            }
            finally { Ok(SafeArrayUnaccessData(array)); }
        }
        finally { Ok(SafeArrayDestroy(array)); }
    }
    private static void CheckFields(nint window, Dictionary<string, nint> found, List<nint> fields, List<nint> owned, Action<bool, string> check)
    {
        check(fields.Count == 4 && found.ContainsKey("Responsive full width") && found.ContainsKey("Disabled control"), "external UIA discovers all six actual Field inputs with label names");
        foreach (var element in fields)
        {
            check(Boolean(element, 30025) && !Boolean(element, 30103), "external Field exposes required and invalid state");
            check(Text(element, 30013) == "Use your work address Enter at least three characters", "external Field exposes hint and error message through HelpText");
        }
        var disabled = found["Disabled control"];
        check(!Boolean(disabled, 30010) && !Boolean(disabled, 30009), "external disabled Field input is disabled and not keyboard focusable");
        var input = fields[0];
        var identity = Text(input, 30011);
        var pattern = Pattern(input, 10002);
        owned.Add(pattern);
        var text = Marshal.StringToBSTR("valid@example.com");
        try
        {
            Ok(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(pattern, 3))(pattern, text));
        }
        finally { Marshal.FreeBSTR(text); }
        var deadline = Environment.TickCount64 + 2000;
        while (!Boolean(input, 30103) && Environment.TickCount64 < deadline)
            Thread.Sleep(10);
        check(fields.All(element => Boolean(element, 30103) && Text(element, 30045) == "valid@example.com"), "UIA editing updates every shared Field value and validation state");
        check(fields.All(element => Text(element, 30013) == "Use your work address Ready") && Text(input, 30011) == identity, "Field rebuild preserves automation identity and refreshes validation HelpText");
        UiaVariant rectangle = default;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(input, 10))(input, 30001, &rectangle));
        try
        {
            check(rectangle.Type == (0x2000 | 5), "external UIA bounding rectangle uses a native double SAFEARRAY");
            Ok(SafeArrayAccessData(rectangle.Data.Pointer, out var data));
            try
            {
                var bounds = (double*)data;
                var origin = new NativePoint();
                if (ClientToScreen(window, ref origin) == 0 || GetClientRect(window, out var client) == 0)
                    throw new InvalidOperationException("Owned test HWND is unavailable.");
                check(double.IsFinite(bounds[0]) && double.IsFinite(bounds[1]) && bounds[2] > 0 && bounds[3] > 0 && bounds[0] >= origin.X && bounds[1] >= origin.Y && bounds[0] + bounds[2] <= origin.X + client.Right && bounds[1] + bounds[3] <= origin.Y + client.Bottom, "external Field bounds are physical screen coordinates within the real client area");
            }
            finally { Ok(SafeArrayUnaccessData(rectangle.Data.Pointer)); }
        }
        finally { UiaNative.VariantClear(ref rectangle); }
    }
    private static (int X, int Y) ClientCenter(nint window, nint element)
    {
        UiaVariant rectangle = default;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, 30001, &rectangle));
        try
        {
            if (rectangle.Type != (0x2000 | 5))
                throw new InvalidOperationException("Pointer target bounds must be a double SAFEARRAY.");
            Ok(SafeArrayAccessData(rectangle.Data.Pointer, out var data));
            try
            {
                var origin = new NativePoint();
                if (ClientToScreen(window, ref origin) == 0)
                    throw new InvalidOperationException("Owned pointer client origin is unavailable.");
                var bounds = (double*)data;
                return ((int)Math.Round(bounds[0] + bounds[2] / 2 - origin.X), (int)Math.Round(bounds[1] + bounds[3] / 2 - origin.Y));
            }
            finally { Ok(SafeArrayUnaccessData(rectangle.Data.Pointer)); }
        }
        finally { UiaNative.VariantClear(ref rectangle); }
    }

    private static int ControlType(nint element)
    {
        UiaVariant value = default;
        var hr = ((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, 30003, &value);
        if (hr == UiaNative.Unavailable)
            return -1;
        Ok(hr);
        try
        {
            return value.Data.Integer;
        }
        finally { UiaNative.VariantClear(ref value); }
    }
    private static bool Boolean(nint element, int property)
    {
        UiaVariant value = default;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, property, &value));
        try
        {
            if (value.Type != 11)
                throw new InvalidOperationException("UIA property is not a Boolean: " + property);
            return value.Data.Boolean != 0;
        }
        finally { UiaNative.VariantClear(ref value); }
    }
    private static string Text(nint element, int property)
    {
        UiaVariant value = default;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, UiaVariant*, int>)Slot(element, 10))(element, property, &value));
        try
        {
            return value.Type == 8 && value.Data.Pointer != 0 ? Marshal.PtrToStringBSTR(value.Data.Pointer) : string.Empty;
        }
        finally { UiaNative.VariantClear(ref value); }
    }
    private static nint Pattern(nint element, int id)
    {
        nint value = 0;
        Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(element, 16))(element, id, &value));
        if (value == 0)
            throw new InvalidOperationException("Required UIA pattern is unavailable: " + id);
        try
        {
            var iid = new Guid(id switch
            {
                10000 => "fb377fbe-8ea6-46d5-9c73-6499642d3059",
                10002 => "a94cd8b1-0844-4cd6-9d2d-640537ab39e9",
                10003 => "59213f4f-7346-49e5-b120-80555987a148",
                10004 => "88f4d42a-e881-459d-a77c-73bbbb7e02dc",
                10010 => "a8efa66a-0fda-421a-9194-38021f3578ea",
                10017 => "b488300f-d015-4f19-9c29-bb595e3645ef",
                10005 => "619be086-1f4e-4ee4-bafa-210128738730",
                10006 => "414c3cdc-856b-4f5b-8538-3131c6302550",
                10007 => "78f8ef57-66c3-4e09-bd7c-e79b2004894d",
                10015 => "94cf8058-9b8d-4ab9-8bfd-4cd0a33c8c70",
                10001 => "5ed5202e-b2ac-47a6-b638-4b0bf140d78e",
                _ => throw new ArgumentOutOfRangeException(nameof(id))
            });
            Ok(Marshal.QueryInterface(value, in iid, out var typed));
            return typed;
        }
        finally { Marshal.Release(value); }
    }
    [LibraryImport("ole32.dll")] private static partial int CoInitializeEx(nint reserved, uint mode);
    [LibraryImport("ole32.dll")] private static partial void CoUninitialize();
    [LibraryImport("ole32.dll")] private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint value);
    [LibraryImport("user32.dll")] private static partial int EnumWindows(delegate* unmanaged[Stdcall]<nint, nint, int> callback, nint parameter);
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(nint window, out uint processId);
    [LibraryImport("user32.dll")] private static partial int GetClassNameW(nint window, char* value, int length);
    [LibraryImport("oleaut32.dll")] private static partial int SafeArrayAccessData(nint array, out nint data);
    [LibraryImport("oleaut32.dll")] private static partial int SafeArrayUnaccessData(nint array);
    [LibraryImport("oleaut32.dll")] private static partial int SafeArrayDestroy(nint array);
    [LibraryImport("oleaut32.dll")] private static partial int SafeArrayGetLBound(nint array, uint dimension, out int value);
    [LibraryImport("oleaut32.dll")] private static partial int SafeArrayGetUBound(nint array, uint dimension, out int value);
    [LibraryImport("user32.dll")] private static partial int ClientToScreen(nint window, ref NativePoint point);
    [LibraryImport("user32.dll")] private static partial int GetClientRect(nint window, out NativeRect rect);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X, Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left, Top, Right, Bottom;
    }
}
