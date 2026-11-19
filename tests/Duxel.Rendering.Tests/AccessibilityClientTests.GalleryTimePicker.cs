using System.ComponentModel;
using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static unsafe partial class AccessibilityClientTests
{
    private static void CheckGalleryPicker(nint window, nint root, nint condition,
        Action<bool, string> check, string page, bool verifyWheel, bool leaveTimePopupOpen = false)
    {
        const int Button = 50000, Link = 50005, List = 50008, ListItem = 50007, GridCell = 50025;
        const int Invoke = 10000, ExpandCollapse = 10005, ScrollItem = 10017, SelectionItem = 10010;
        const int IsOffscreen = 30022, Name = 30005;
        var combined = page == "DateTimePicker";
        var savedTime = combined ? "저장 값 : 2024-04-16T15:05:05.0000000" : "저장 값 : 15:05:05.0000000";

        nint Find(nint parent, string name, int? type = null)
        {
            nint items = 0;
            Ok(((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)Slot(parent, 6))(parent, 4, condition, &items));
            try
            {
                int count = 0;
                Ok(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(items, 3))(items, &count));
                for (var index = 0; index < count; index++)
                {
                    nint item = 0;
                    Ok(((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(items, 4))(items, index, &item));
                    try
                    {
                        if ((type is null || ControlType(item) == type) && Text(item, Name) == name)
                        {
                            Marshal.AddRef(item);
                            return item;
                        }
                    }
                    catch (COMException error) when (error.HResult == UiaNative.Unavailable)
                    {
                        // A provider can retire between enumeration and property retrieval.
                    }
                    finally { Marshal.Release(item); }
                }
                return 0;
            }
            finally { Marshal.Release(items); }
        }

        void Wait(Func<bool> predicate, string description)
        {
            var deadline = Environment.TickCount64 + 8000;
            while (!predicate())
            {
                if (Environment.TickCount64 >= deadline)
                    throw new TimeoutException("Gallery UIA client timed out waiting for " + description);
                Thread.Sleep(25);
            }
        }

        void WithElement(nint parent, string name, int? type, Action<nint> use)
        {
            nint element = 0;
            Wait(() => (element = Find(parent, name, type)) != 0, name);
            try { use(element); }
            finally { Marshal.Release(element); }
        }

        static void Execute(nint element, int patternId)
        {
            var pattern = Pattern(element, patternId);
            try { Ok(((delegate* unmanaged[Stdcall]<nint, int>)Slot(pattern, 3))(pattern)); }
            finally { Marshal.Release(pattern); }
        }

        static void Reveal(nint element)
        {
            if (Boolean(element, IsOffscreen))
                Execute(element, ScrollItem);
        }

        var existing = Find(root, page, Link);
        if (existing == 0)
        {
            WithElement(root, "날짜와 시간", Button, group =>
            {
                Reveal(group);
                Wait(() => !Boolean(group, IsOffscreen), "visible date and time navigation group");
                Execute(group, ExpandCollapse);
            });
        }
        else
        {
            Marshal.Release(existing);
        }

        WithElement(root, page, Link, route =>
        {
            Reveal(route);
            Wait(() => !Boolean(route, IsOffscreen), "visible " + page + " route");
            Execute(route, Invoke);
        });

        void WaitForRemoval(string name, int type)
        {
            Wait(() =>
            {
                var element = Find(root, name, type);
                if (element == 0)
                    return true;
                Marshal.Release(element);
                return false;
            }, name + " to close");
        }

        if (page == "공휴일 제공자")
        {
            const int IsSelected = 30079;
            var calendar = new UiCultureCalendar(System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));
            var date = calendar.FormatFullDate(new DateOnly(DateTime.Today.Year, 1, 1));
            var offlineHoliday = date + " · 신정";
            WithElement(root, offlineHoliday, GridCell, cell =>
                check(!Boolean(cell, IsOffscreen), "External UIA reads the offline holiday from its visible calendar cell."));
            WithElement(root, "연결 끊김 예제", Button, button => Execute(button, Invoke));
            WaitForRemoval(offlineHoliday, GridCell);
            WithElement(root, date, GridCell, cell =>
            {
                Execute(cell, SelectionItem);
                Wait(() => Boolean(cell, IsSelected), "the date selection after the simulated outage");
            });
            check(true, "External UIA selects the same date after its holiday annotation is removed.");
            WithElement(root, "현재 달 공휴일 조회", Button, button => Execute(button, Invoke));
            WithElement(root, date + " · 1월 1일", GridCell, cell =>
                check(Boolean(cell, IsSelected), "The live holiday name appears in UIA without losing the selected date."));
            return;
        }

        if (page != "TimePicker")
        {
            var system = new UiCultureCalendar();
            var sourceDate = new DateOnly(2024, 3, 15);
            var targetDate = new DateOnly(2024, 4, 16);
            var initial = combined ? "저장 값 : 2024-03-15T15:04:05.0000000" : "저장 값 : 2024-03-15";
            WithElement(root, initial, null, _ => { });
            WithElement(root, "날짜 선택", Button, opener => Execute(opener, Invoke));
            WithElement(root, system.FormatMonth(sourceDate), Button, month => Execute(month, Invoke));
            WithElement(root, "월 선택", List, months =>
                WithElement(months, system.FormatMonth(targetDate), ListItem, april => Execute(april, SelectionItem)));
            WaitForRemoval("월 선택", List);
            check(true, page + " opens its month wheel and selects April through Windows UI Automation.");
            var date = system.FormatFullDate(targetDate);
            WithElement(root, date, GridCell, day => Execute(day, Invoke));
            WaitForRemoval(date, GridCell);
            var savedDate = combined ? "저장 값 : 2024-04-16T15:04:05.0000000" : "저장 값 : 2024-04-16";
            WithElement(root, savedDate, null, _ =>
                check(true, page + " saves April 16 and closes the calendar."));
            if (!combined)
                return;
        }

        WithElement(root, "시각 선택", Button, opener => Execute(opener, Invoke));
        WithElement(root, "분", List, minutes =>
        {
            check(true, "External UIA opens the Gallery " + page + " time popup and finds the minute wheel.");
            var minute = new UiIntegerWheelSequence(0, 59, System.Globalization.CultureInfo.CurrentCulture).Format(5);
            WithElement(minutes, minute, ListItem, option => Execute(option, SelectionItem));
        });
        WithElement(root, savedTime, null, _ =>
            check(true, "External UIA selects minute 05 and reads the saved time with seconds preserved."));

        if (leaveTimePopupOpen)
        {
            WithElement(root, "분", List, minutes => check(!Boolean(minutes, IsOffscreen),
                "The TimePicker popup and minute wheel are still visible before window close."));
            return;
        }

        if (verifyWheel)
        {
            const uint MouseMove = 0x0200, MouseWheel = 0x020A;
            const short WheelDelta = 120;
            static nint PackedPoint(int x, int y) => (nint)(unchecked((ushort)checked((short)x)) | checked((short)y) << 16);
            void Post(uint message, nuint value, nint point)
            {
                if (LabelPostMessage(window, message, value, point) == 0)
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not queue mouse message {message:X}.");
            }
            WithElement(root, "분", List, minutes =>
            {
                if (Boolean(minutes, IsOffscreen))
                    throw new InvalidOperationException("The minute wheel is offscreen.");
                var (x, y) = ClientCenter(window, minutes);
                var screen = new NativePoint { X = x, Y = y };
                if (ClientToScreen(window, ref screen) == 0)
                    throw new InvalidOperationException("Could not resolve the minute wheel screen coordinate.");
                Post(MouseMove, 0, PackedPoint(x, y));
                var screenPoint = PackedPoint(screen.X, screen.Y);
                void Step(short delta, string expected, string description)
                {
                    Post(MouseWheel, (nuint)((uint)unchecked((ushort)delta) << 16), screenPoint);
                    WithElement(root, expected, null, _ => check(true, description));
                }
                Step(-WheelDelta, "저장 값 : 15:06:05.0000000",
                    "One queued Windows wheel-down message advances the minute once.");
                Step(WheelDelta, savedTime,
                    "One queued Windows wheel-up message restores the minute and preserves seconds.");
            });
        }

        WithElement(root, "확인", Button, done => Execute(done, Invoke));
        WaitForRemoval("분", List);
        WithElement(root, savedTime, null, _ =>
            check(true, "External UIA closes the popup and reads the same saved time."));
    }
}
