using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static unsafe partial class AtSpiProbe
{
    internal enum GalleryProbe { TimePicker, DatePickers, Holidays, ProviderDirection, Tree, ScrollView, VirtualList, VirtualHorizontal, VirtualRtl, VirtualGrid, SearchInput, Rating, NavigationBar, RangeCalendar, RadioGroup, SvgPixels, SvgThumb, SvgLayerMix, Tooltip, Carousel, NavigationRepaint, Fps, Drawer, Dialog, CardNudge, MenuSubText, TagPicker, Select, Combobox, Dropdown }

    internal static void RunGalleryControls(uint parentPid, string output, GalleryProbe probe = GalleryProbe.TimePicker,
        nuint? nativeWindow = null, bool profileScroll = false, bool capturePixels = false)
    {
        if (File.Exists(output))
            throw new IOException("Use a fresh Gallery AT-SPI result path.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("The independent Gallery AT-SPI client could not initialize.");
        var transportTimeout = probe is GalleryProbe.SvgPixels or GalleryProbe.SvgThumb or GalleryProbe.SvgLayerMix ? 6000 : 2000;
        atspi_set_timeout(transportTimeout, transportTimeout);
        var checks = new List<string>();
        try
        {
            using var app = OwnedApplication(parentPid);
            atspi_accessible_set_cache_mask(app.Value, 0);
            using var window = Child(app.Value, 0);
            NativeObject? FindName(nint root, string name, int? role = null)
            {
                atspi_accessible_clear_cache(root);
                return Find(root, value => Name(value) == name && (role is null || Role(value) == role));
            }

            NativeObject Required(string name, nint root, int? role = null)
            {
                NativeObject? result = null;
                Wait(() => (result = FindName(root, name, role)) is not null, "Gallery object : " + name);
                return result!;
            }

            void Navigate(string page, string section = "날짜와 시간")
            {
                using var existingRoute = FindName(window.Value, page, 88);
                if (existingRoute is null)
                {
                    using var group = Required(section, window.Value, 43);
                    Reveal(group);
                    InvokeNamed(group, "expand");
                }
                using var route = Required(page, window.Value, 88);
                Reveal(route);
                InvokeNamed(route, "click");
            }

            void WaitForRemoval(string name, int role)
            {
                Wait(() =>
                {
                    using var element = FindName(window.Value, name, role);
                    return element is null;
                }, name + " to close");
            }

            void CheckDate(string page, string initial, string expected)
            {
                Navigate(page);
                using var original = Required(initial, window.Value);
                using var opener = Required("날짜 선택", window.Value, 43);
                InvokeNamed(opener, "click");
                var calendar = new UiCultureCalendar();
                var date = new DateOnly(2024, 4, 16);
                using var month = Required(calendar.FormatMonth(new(2024, 3, 15)), window.Value, 43);
                InvokeNamed(month, "click");
                using var months = Required("월 선택", window.Value, 98);
                using var april = Required(calendar.FormatMonth(date), months.Value, 32);
                InvokeNamed(april, "click");
                WaitForRemoval("월 선택", 98);
                checks.Add(page + " opens its month wheel and selects April through AT-SPI.");

                var dateName = calendar.FormatFullDate(date);
                using var day = Required(dateName, window.Value, 56);
                InvokeNamed(day, "click");
                WaitForRemoval(dateName, 56);
                using var value = Required(expected, window.Value);
                checks.Add(page + " saves April 16 and closes the calendar.");
            }

            void CheckTime(string page, string expected, nuint? wheelWindow)
            {
                using var opener = Required("시각 선택", window.Value, 43);
                InvokeNamed(opener, "click");
                using var minutes = Required("분", window.Value, 98);
                checks.Add("External AT-SPI opens the Gallery " + page + " time popup.");
                var minute = new UiIntegerWheelSequence(0, 59, System.Globalization.CultureInfo.CurrentCulture).Format(5);
                using var option = Required(minute, minutes.Value, 32);
                InvokeNamed(option, "click");
                using var value = Required(expected, window.Value);
                checks.Add("The external client selects minute 05 and reads the saved value with seconds preserved.");

                if (wheelWindow is { } windowId)
                {
                    var bounds = ComponentBox(minutes.Value);
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                        throw new InvalidOperationException("The minute wheel has no visible native bounds.");
                    var x = bounds.X + bounds.Width / 2;
                    var y = bounds.Y + bounds.Height / 2;
                    using var peer = new NativePeer(windowId);
                    peer.Pointer(6, x, y);
                    peer.Pointer(4, x, y, 5);
                    using var afterDown = Required("저장 값 : 15:06:05.0000000", window.Value);
                    checks.Add("One queued X11 wheel-down event advances the actual minute wheel once.");
                    peer.Pointer(4, x, y, 4);
                    using var afterUp = Required(expected, window.Value);
                    checks.Add("One queued X11 wheel-up event restores the minute and preserves seconds.");
                }

                using var done = Required("확인", window.Value, 43);
                InvokeNamed(done, "click");
                WaitForRemoval("분", 98);
                using var retained = Required(expected, window.Value);
                checks.Add("The external client closes the popup with Done and the saved value remains present.");
            }

            if (probe == GalleryProbe.Fps)
            {
                using var eventWait = Required("이벤트 대기 모드", window.Value);
                VerifyGalleryFps(window.Value, eventWait,
                    nativeWindow ?? throw new InvalidOperationException("The FPS probe requires an owned X11 window."), checks);
            }
            else if (probe == GalleryProbe.Drawer)
            {
                Navigate("Drawer", "알림과 진행 상태");
                VerifyGalleryDrawer(window.Value, Required, checks, output,
                    nativeWindow ?? throw new InvalidOperationException("The Drawer probe requires an owned X11 window."));
            }
            else if (probe == GalleryProbe.Dialog)
            {
                Navigate("Dialog", "알림과 진행 상태");
                VerifyGalleryDialog(window.Value, Required, checks,
                    nativeWindow ?? throw new InvalidOperationException("The Dialog probe requires an owned X11 window."));
            }
            else if (probe == GalleryProbe.TagPicker)
            {
                Navigate("TagPicker", "입력과 선택");
                VerifyGalleryTagPicker(window.Value, Required, WaitForRemoval, checks,
                    nativeWindow ?? throw new InvalidOperationException("The TagPicker probe requires an owned X11 window."));
            }
            else if (probe == GalleryProbe.Select)
            {
                Navigate("Select", "Input and selection");
                VerifyGallerySelect(window.Value, Required, checks,
                    nativeWindow ?? throw new InvalidOperationException("The Select probe requires an owned X11 window."));
            }
            else if (probe == GalleryProbe.Combobox)
            {
                Navigate("Combobox", "Input and selection");
                VerifyGalleryCombobox(window.Value, Required, checks,
                    nativeWindow ?? throw new InvalidOperationException("The Combobox probe requires an owned X11 window."));
            }
            else if (probe == GalleryProbe.Dropdown)
            {
                Navigate("Selection lists · combined", "Combined examples");
                VerifyGalleryDropdown(window.Value, Required, checks,
                    nativeWindow ?? throw new InvalidOperationException("The Dropdown probe requires an owned X11 window."));
            }
            else if (probe == GalleryProbe.SvgPixels)
            {
                Navigate("SVG", "문서와 그래픽");
                VerifyGallerySvgPixels(window.Value, Required, checks, output,
                    nativeWindow ?? throw new InvalidOperationException("The SVG capture requires an owned X11 window."));
            }
            else if (probe is GalleryProbe.SvgThumb or GalleryProbe.SvgLayerMix)
            {
                Navigate("SVG", "문서와 그래픽");
                VerifyGallerySvgThumb(window.Value, Required, checks, output,
                    nativeWindow ?? throw new InvalidOperationException("SVG thumb replay requires an owned X11 window."),
                    layerMix : probe == GalleryProbe.SvgLayerMix);
            }
            else if (probe == GalleryProbe.RadioGroup)
            {
                Navigate("RadioGroup", "입력과 선택");
                VerifyGalleryRadioGroup(window.Value, Required, checks,
                    nativeWindow ?? throw new InvalidOperationException("The RadioGroup probe requires an owned X11 window."),
                    capturePixels ? output : null);
            }
            else if (probe == GalleryProbe.MenuSubText)
            {
                Navigate("배치 통합 · 종합", "통합 예제");
                VerifyGalleryMenuSubText(window.Value, Required, WaitForRemoval, checks,
                    nativeWindow ?? throw new InvalidOperationException("The Menu probe requires an owned X11 window."));
            }
            else if (probe == GalleryProbe.RangeCalendar)
            {
                Navigate("RangeCalendar");
                VerifyGalleryRangeCalendar(window.Value, Required, checks, nativeWindow, capturePixels ? output : null);
            }
            else if (probe == GalleryProbe.Tooltip)
            {
                Navigate("Tooltip", "알림과 진행 상태");
                VerifyGalleryTooltip(window.Value, Required, checks, nativeWindow);
            }
            else if (probe == GalleryProbe.Carousel)
            {
                Navigate("Carousel", "콘텐츠와 목록");
                VerifyGalleryCarousel(window.Value, Required, checks);
            }
            else if (probe == GalleryProbe.CardNudge)
            {
                Navigate("CardNudge", "콘텐츠와 목록");
                VerifyGalleryCardNudge(window.Value, Required, WaitForRemoval, checks,
                    nativeWindow ?? throw new InvalidOperationException("The CardNudge probe requires an owned X11 window."));
            }
            else if (probe is GalleryProbe.NavigationBar or GalleryProbe.NavigationRepaint)
            {
                var trace = probe == GalleryProbe.NavigationRepaint
                    ? new FileInfo(Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_OUT")
                        ?? throw new InvalidOperationException("Set DUXEL_VK_PROFILE_OUT for the Gallery repaint probe."))
                    : null;
                void Phase(string name)
                {
                    System.Threading.Thread.Sleep(500);
                    trace!.Refresh();
                    Console.WriteLine($"{name}: {trace.Length}");
                }
                Action<string>? mark = trace is null ? null : Phase;
                if (mark is not null)
                {
                    using var highlights = Required("갱신 영역 강조", window.Value);
                    InvokeNamed(highlights, "toggle");
                    using var eventWait = Required("이벤트 대기 모드", window.Value);
                    InvokeNamed(eventWait, "toggle");
                    mark("continuous");
                }
                Navigate("NavigationBar", "동작과 탐색");
                mark?.Invoke("entry");
                VerifyGalleryNavigationBar(window.Value, Required, checks, mark);
            }
            else if (probe == GalleryProbe.Rating)
            {
                const int groupRole = 99;
                const int imageRole = 27;
                const int focusableState = 11;
                const int halfPointIndex = 6;
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                var score = 3.5f.ToString("0.#", culture);
                var scoredItem = $"5점 만점에 {score}점";
                var displayName = $"요약 평가 {score} {1234.ToString("N0", culture)}";

                Navigate("Rating", "입력과 선택");
                using (var group = Required("반 점 단위 평가", window.Value, groupRole))
                {
                    Reveal(group);
                    using var selection = new NativeObject(atspi_accessible_get_selection_iface(group.Value));
                    if (!SelectionAction(selection.Value, halfPointIndex, SelectionOperation.Select))
                        throw new InvalidOperationException("The native Rating group refused a valid half-point selection.");
                    Wait(() =>
                    {
                        using var selected = SelectedChild(selection.Value, 0);
                        return SelectionCount(selection.Value) == 1 && selected is not null && Name(selected.Value) == scoredItem;
                    }, "the selected half-point Rating item");
                }
                using (Required($"평점 : {score} / 5", window.Value))
                    checks.Add("AT-SPI selects a half point and updates the shared Gallery rating.");

                using (var display = Required(displayName, window.Value, imageRole))
                {
                    using var states = new NativeObject(atspi_accessible_get_state_set(display.Value));
                    if (atspi_state_set_contains(states.Value, focusableState) != 0 || Count(display.Value) != 0)
                        throw new InvalidOperationException("RatingDisplay must be a single nonfocusable image.");
                    checks.Add("The compact RatingDisplay reports the score and review count as one nonfocusable image.");
                }

                Navigate("개요");
                WaitForRemoval("반 점 단위 평가", groupRole);
                Navigate("Rating", "입력과 선택");
                using var restored = Required("반 점 단위 평가", window.Value, groupRole);
                using var restoredSelection = new NativeObject(atspi_accessible_get_selection_iface(restored.Value));
                using var retained = SelectedChild(restoredSelection.Value, 0);
                if (retained is null || Name(retained.Value) != scoredItem || SelectionCount(restoredSelection.Value) != 1)
                    throw new InvalidOperationException("The Rating selection did not survive navigation.");
                checks.Add("The selected half point survives navigating away and back.");
            }
            else if (probe == GalleryProbe.VirtualGrid)
            {
                Navigate("가상 그리드", "레이아웃과 입력");
                using var grid = Required("가상 그리드 예제", window.Value, 55);
                using var table = new NativeObject(atspi_accessible_get_table_iface(grid.Value));
                nint error = 0;
                var rowCount = atspi_table_get_n_rows(table.Value, &error);
                Error(ref error);
                var columnCount = atspi_table_get_n_columns(table.Value, &error);
                Error(ref error);
                if (rowCount != 1_000_000_000 || columnCount != 1_000_000_000)
                    throw new InvalidOperationException("The sparse table must expose its full logical dimensions.");
                checks.Add("AT-SPI Table exposes the billion-by-billion logical dimensions.");
                using var first = Required("1행 1열", grid.Value);
                using var editor = new NativeObject(atspi_accessible_get_editable_text_iface(first.Value));
                const string saved = "그리드 편집값";
                if (!SetText(editor.Value, saved))
                    throw new InvalidOperationException("The first grid cell rejected the native edit.");
                checks.Add("The external client edits the first virtual grid cell.");
                const string lastName = "1,000,000,000행 1,000,000,000열";
                using var lastCell = new NativeObject(atspi_table_get_accessible_at(table.Value, rowCount - 1, columnCount - 1, &error));
                Error(ref error);
                if (lastCell.Value == 0)
                    throw new InvalidOperationException("The sparse table did not return the distant cell.");
                using var absent = FindName(lastCell.Value, lastName);
                if (IsShowing(lastCell) || absent is not null || !IsShowing(first))
                    throw new InvalidOperationException("Sparse coordinate lookup must publish only an offscreen cell reference.");
                checks.Add("AT-SPI coordinates retrieve a placeholder while leaving the first editor visible.");
                Reveal(lastCell);
                using var retained = new NativeObject(atspi_table_get_accessible_at(table.Value, rowCount - 1, columnCount - 1, &error));
                Error(ref error);
                if (retained.Value != lastCell.Value)
                    throw new InvalidOperationException("The distant cell lost its accessible identity during realization.");
                using var last = Required("1,000,000,000행 1,000,000,000열", grid.Value);
                using var evicted = FindName(grid.Value, "1행 1열");
                if (!IsShowing(last) || evicted is not null || Count(grid.Value) is 0 or > 80)
                    throw new InvalidOperationException("The grid should reach the far corner with a bounded exposed range.");
                checks.Add("The far corner is visible and the first cell leaves the bounded accessibility subtree.");
                using var firstAction = Required("첫 행·첫 열", window.Value, 43);
                InvokeNamed(firstAction, "click");
                using var restored = Required("1행 1열", grid.Value);
                using var text = new NativeObject(atspi_accessible_get_text_iface(restored.Value));
                if (ReadText(text.Value, 0, -1) != saved)
                    throw new InvalidOperationException("Returning to the first grid cell lost the model edit.");
                checks.Add("The recreated first cell preserves the keyed model value.");
            }
            else if (probe is GalleryProbe.VirtualList or GalleryProbe.VirtualHorizontal or GalleryProbe.VirtualRtl)
            {
                Navigate("가상 목록", "레이아웃과 입력");
                void Click(string name)
                {
                    using var button = Required(name, window.Value, 43);
                    Reveal(button);
                    InvokeNamed(button, "click");
                }

                var horizontal = probe != GalleryProbe.VirtualList;
                if (horizontal)
                {
                    Click("방향 : 세로");
                    using var direction = Required("방향 : 가로", window.Value, 43);
                    checks.Add("The external client switches the Gallery virtual list to horizontal.");
                }
                using var list = Required("관찰 가상 목록", window.Value, 39);
                if (probe == GalleryProbe.VirtualRtl)
                {
                    Click("가로 순서 : 왼쪽부터");
                    using var order = Required("가로 순서 : 오른쪽부터", window.Value, 43);
                    using var right = Required("행 값 : 0", list.Value);
                    using var left = Required("행 값 : 1", list.Value);
                    static int NativeX(NativeObject element)
                    {
                        using var component = new NativeObject(atspi_accessible_get_component_iface(element.Value));
                        nint error = 0;
                        var pointer = atspi_component_get_extents(component.Value, 1, &error);
                        try
                        {
                            Error(ref error);
                            if (pointer == 0)
                                throw new InvalidOperationException("The RTL editor returned no native bounds.");
                            return ((Rect*)pointer)->X;
                        }
                        finally { if (pointer != 0) g_free(pointer); }
                    }
                    if (NativeX(right) <= NativeX(left) || !IsShowing(right) || !IsShowing(left))
                        throw new InvalidOperationException("The external client must see item zero to the right of item one.");
                    checks.Add("The visible RTL editors expose coordinates ordered from right to left.");
                }
                using var first = Required("행 값 : 0", list.Value);
                Reveal(first);
                using var edit = new NativeObject(atspi_accessible_get_editable_text_iface(first.Value));
                using var text = new NativeObject(atspi_accessible_get_text_iface(first.Value));
                const string saved = "가상 목록 편집값";
                if (!SetText(edit.Value, saved) || ReadText(text.Value, 0, -1) != saved)
                    throw new InvalidOperationException("The first realized editor did not save its native text replacement.");
                checks.Add("External AT-SPI edits row key 0 through the actual Gallery editor.");

                if (horizontal)
                {
                    using var overscan = Required("행 값 : 3", list.Value);
                    if (IsShowing(overscan))
                        throw new InvalidOperationException("The horizontal overscan editor must start outside the viewport.");
                    Reveal(overscan);
                    using var shown = Required("행 값 : 3", list.Value);
                    if (shown.Value != overscan.Value || !IsShowing(shown))
                        throw new InvalidOperationException("Horizontal ScrollTo must reveal the original overscan editor.");
                    checks.Add("AT-SPI ScrollTo reveals the same offscreen horizontal overscan editor.");
                    Click("처음");
                }

                Click("끝");
                using var tail = Required("행 값 : 999999999", list.Value);
                using var old = FindName(list.Value, "행 값 : 0");
                if (old is not null || Count(list.Value) is 0 or > 64)
                    throw new InvalidOperationException("The virtual list did not evict the first editor or bound its exposed range.");
                checks.Add("The billionth row is reachable and the old first editor leaves the bounded AT-SPI subtree.");

                Click("키 순서 바꾸기");
                using var moved = Required("행 값 : 0", list.Value);
                using var movedText = new NativeObject(atspi_accessible_get_text_iface(moved.Value));
                if (ReadText(movedText.Value, 0, -1) != saved)
                    throw new InvalidOperationException("The recreated editor lost the text associated with its stable key.");
                checks.Add("Reordering moves key 0 to the tail and its recreated editor exposes the original edit.");

                Click("처음");
                using var movedTail = Required("행 값 : 999999999", list.Value);
                using var evicted = FindName(list.Value, "행 값 : 0");
                if (evicted is not null)
                    throw new InvalidOperationException("Returning to the start left the tail editor exposed.");
                checks.Add("Returning to the start exposes the swapped key and retires the tail editor again.");
            }
            else if (probe == GalleryProbe.SearchInput)
            {
                const int entryRole = 79;
                Navigate("SearchBox", "입력과 선택");
                using var input = Required("검색어", window.Value, entryRole);
                using var component = new NativeObject(atspi_accessible_get_component_iface(input.Value));
                using var edit = new NativeObject(atspi_accessible_get_editable_text_iface(input.Value));
                using var text = new NativeObject(atspi_accessible_get_text_iface(input.Value));
                using var peer = new NativePeer(nativeWindow ?? throw new InvalidOperationException("SearchBox probe requires an owned X11 window."));
                nint error = 0;
                var focused = atspi_component_grab_focus(component.Value, &error);
                Error(ref error);
                if (focused == 0 || !SetText(edit.Value, "ab"))
                    throw new InvalidOperationException("Could not focus and initialize the Gallery SearchBox.");
                peer.KeyBurst([0xff57]);
                Wait(() => ReadText(text.Value, 0, -1) == "ab" && TextCaret(text.Value) == 2, "SearchBox seed and end caret");
                checks.Add("External AT-SPI seeds the visible SearchBox and X11 End positions its caret.");
                peer.KeyBurst(['x', 0xff51, 'y']);
                string observed = "";
                Wait(() => (observed = ReadText(text.Value, 0, -1)).Length == 4, "two characters from the X11 burst");
                if (observed != "abyx" || TextCaret(text.Value) != 3)
                    throw new InvalidOperationException($"Expected SearchBox text abyx and caret 3. Actual: {observed}, caret {TextCaret(text.Value)}.");
                checks.Add("X11 x, Left, y updates text to abyx and leaves the caret at offset 3.");
                Navigate("개요", "시작");
                WaitForRemoval("검색어", entryRole);
                Navigate("SearchBox", "입력과 선택");
                using var restored = Required("검색어", window.Value, entryRole);
                using var restoredText = new NativeObject(atspi_accessible_get_text_iface(restored.Value));
                if (ReadText(restoredText.Value, 0, -1) != "abyx")
                    throw new InvalidOperationException("The native SearchBox edit was lost across navigation.");
                checks.Add("The native SearchBox edit survives navigation away and back.");
                if (capturePixels)
                {
                    CaptureGalleryPair(peer, output, "SearchBox", checks);
                    Navigate("개요", "시작");
                    WaitForRemoval("검색어", entryRole);
                    CaptureGalleryPair(peer, output, "Overview", checks);
                    Navigate("RadioGroup", "입력과 선택");
                    using var radio = Required("처리 방식", window.Value);
                    CaptureGalleryPair(peer, output, "RadioGroup", checks);
                }
            }
            else if (probe == GalleryProbe.ScrollView)
            {
                Navigate("레이아웃 예제", "레이아웃과 입력");
                using var vertical = Required("스크롤 방향 : 세로", window.Value, 43);
                Reveal(vertical);

                string? ReadScrollSource()
                {
                    string? source = null;
                    atspi_accessible_clear_cache(window.Value);
                    using var match = Find(window.Value, value =>
                    {
                        if (Name(value) != "C# 예제 소스")
                            return false;
                        using var text = new NativeObject(atspi_accessible_get_text_iface(value));
                        var content = ReadText(text.Value, 0, -1);
                        if (!content.Contains("Dux.ScrollView", StringComparison.Ordinal))
                            return false;
                        source = content;
                        return true;
                    });
                    return source;
                }

                void WaitForSource(string axis)
                {
                    Wait(() => ReadScrollSource()?.Contains("UiScrollAxis." + axis, StringComparison.Ordinal) == true,
                        "Gallery source for " + axis);
                }

                WaitForSource("Vertical");
                InvokeNamed(vertical, "click");
                using var both = Required("스크롤 방향 : 가로·세로", window.Value, 43);
                WaitForSource("Both");
                checks.Add("External AT-SPI toggles the real ScrollView and reads its changed C# source.");

                using var item = Required("스크롤 항목 8", window.Value, 29);
                Reveal(item);
                var before = ComponentBox(item.Value);
                var x = before.X + before.Width / 2;
                var y = before.Y + before.Height / 2;
                using var peer = new NativePeer(nativeWindow ?? throw new InvalidOperationException("The ScrollView probe requires an owned X11 window."));
                peer.Pointer(6, x, y);
                peer.Pointer(4, x, y, 7);
                Wait(() => ComponentBox(item.Value).X < before.X, "horizontal wheel movement in the ScrollView");
                peer.Pointer(4, x, y, 5);
                Wait(() => ComponentBox(item.Value).Y < before.Y, "vertical wheel movement in the ScrollView");
                checks.Add("External X11 wheel events move the same ScrollView child along both axes.");

                if (profileScroll)
                    ProfileScrollWheel(peer, item, x, y);

                WaitForSource("Both");
                Reveal(both);
                InvokeNamed(both, "click");
                using var restored = Required("스크롤 방향 : 세로", window.Value, 43);
                WaitForSource("Vertical");
                checks.Add("External AT-SPI switches back to vertical and reads the restored C# source.");
            }
            else if (probe == GalleryProbe.Tree)
            {
                const int treeRole = 65;
                const int treeItemRole = 91;
                const int checkedState = 4;
                const int expandedState = 10;
                const int mixedState = 32;
                const string notesName = "디모이의 프로젝트를 시작하기 전에 읽어 볼 문서";

                Navigate("Tree", "콘텐츠와 목록");
                using var tree = Required("폴더와 파일 선택", window.Value, treeRole);
                using var folder = Required("프로젝트", tree.Value, treeItemRole);
                using var notes = Required(notesName, folder.Value, treeItemRole);
                bool HasState(NativeObject target, int state)
                {
                    atspi_accessible_clear_cache(target.Value);
                    using var states = new NativeObject(atspi_accessible_get_state_set(target.Value));
                    return atspi_state_set_contains(states.Value, state) != 0;
                }

                if (!HasState(folder, expandedState) || !HasState(folder, mixedState) || !HasState(notes, checkedState))
                    throw new InvalidOperationException("The AT-SPI Tree does not publish the initial hierarchy and mixed selection.");
                checks.Add("External AT-SPI reads an expanded mixed folder and its checked child.");

                InvokeNamed(folder, "collapse");
                Wait(() =>
                {
                    using var hiddenReport = FindName(folder.Value, "보고서", treeItemRole);
                    return !HasState(folder, expandedState) && hiddenReport is null;
                }, "collapsed Tree folder and hidden unselected child");
                checks.Add("External AT-SPI collapses the folder and its unselected child leaves the exposed hierarchy.");

                InvokeNamed(folder, "expand");
                using var restoredNotes = Required(notesName, folder.Value, treeItemRole);
                using var report = Required("보고서", folder.Value, treeItemRole);
                if (!HasState(folder, expandedState) || !HasState(restoredNotes, checkedState) || HasState(report, checkedState))
                    throw new InvalidOperationException("Re-expanding the folder lost the original selection.");
                checks.Add("External AT-SPI reopens the folder and observes the original checked child.");

                InvokeNamed(folder, "toggle");
                Wait(() => HasState(folder, checkedState) && !HasState(folder, mixedState)
                    && HasState(restoredNotes, checkedState) && HasState(report, checkedState), "checked Tree subtree");
                checks.Add("External AT-SPI toggles the mixed folder and sees both descendants checked.");

                InvokeNamed(report, "toggle");
                Wait(() => HasState(folder, mixedState) && HasState(restoredNotes, checkedState)
                    && !HasState(report, checkedState), "mixed Tree subtree after unchecking a single child");
                checks.Add("External AT-SPI toggles one child and sees the folder become mixed again.");

                using var grid = Required("행 안의 버튼", window.Value, 66);
                using var table = new NativeObject(atspi_accessible_get_table_iface(grid.Value));
                nint error = 0;
                var rows = atspi_table_get_n_rows(table.Value, &error);
                Error(ref error);
                var columns = atspi_table_get_n_columns(table.Value, &error);
                Error(ref error);
                if (rows != 3 || columns != 1)
                    throw new InvalidOperationException($"The TreeGrid published {rows} rows and {columns} columns.");
                using var reportCell = new NativeObject(atspi_table_get_accessible_at(table.Value, 2, 0, &error));
                Error(ref error);
                using var open = Required("열기", reportCell.Value, 43);
                checks.Add("External AT-SPI Table resolves the report's only cell and its Open button.");

                Reveal(open);
                InvokeNamed(open, "click");
                using var opened = Required("보고서를 연 횟수 : 1", window.Value);
                checks.Add("External AT-SPI invokes the real TreeGrid cell button and reads the new count.");
                if (nativeWindow is { } treeWindow)
                    VerifyGalleryTreeActions(treeWindow, window.Value, grid.Value, open, Required, checks, output);
            }
            else if (probe == GalleryProbe.ProviderDirection)
            {
                const int Checked = 4;
                Navigate("FluentProvider", "테마와 설정");
                using var checkbox = Required("미리 알림", window.Value, 7);
                Reveal(checkbox);
                using var sound = Required("소리", window.Value, 62);
                using var direction = Required("배치 방향 바꾸기", window.Value, 43);
                using var daily = Required("매일", window.Value, 44);
                using var weekly = Required("매주", window.Value, 44);
                Wait(() => ComponentBox(daily.Value).X > ComponentBox(weekly.Value).X, "right-to-left option order");
                using var peer = nativeWindow is { } xid ? new NativePeer(xid) : null;
                using var frequencies = peer is not null ? Required("알림 빈도", window.Value, 99) : null;
                using var radioSelection = frequencies is not null
                    ? new NativeObject(atspi_accessible_get_selection_iface(frequencies.Value)) : null;
                using var monthly = frequencies is not null ? Required("매월", frequencies.Value, 44) : null;
                if (peer is not null)
                {
                    Wait(() => ComponentBox(weekly.Value).X > ComponentBox(monthly!.Value).X,
                        "three distinct right-to-left radio positions");
                    FocusRadio(daily);
                    peer.KeyBurst([0xff51]);
                    Wait(() => RadioSelected(weekly, "매주"), "RTL Left selects and focuses the option on its left");
                    checks.Add("The X11 Left arrow selects the visual neighbor in the right-to-left RadioGroup.");
                    CaptureGalleryPair(peer, output, "ProviderRadioRightToLeft", checks);
                }
                bool IsChecked(NativeObject accessible)
                {
                    atspi_accessible_clear_cache(accessible.Value);
                    using var states = new NativeObject(atspi_accessible_get_state_set(accessible.Value));
                    return atspi_state_set_contains(states.Value, Checked) != 0;
                }
                if (!IsChecked(checkbox) || IsChecked(sound))
                    throw new InvalidOperationException("The Gallery toggle example did not start with its documented values.");
                InvokeNamed(checkbox, "toggle");
                Reveal(sound);
                InvokeNamed(sound, "toggle");
                Wait(() => !IsChecked(checkbox) && IsChecked(sound), "the two right-to-left toggle values");
                checks.Add("External AT-SPI toggles the RTL Checkbox and Switch and reads both new values.");

                using var slider = Required("알림 음량", window.Value, 51);
                Reveal(slider);
                using var numeric = new NativeObject(atspi_accessible_get_value_iface(slider.Value));
                double Volume()
                {
                    nint error = 0;
                    var current = atspi_value_get_current_value(numeric.Value, &error);
                    Error(ref error);
                    return current;
                }
                nint sliderError = 0;
                var accepted = atspi_value_set_current_value(numeric.Value, 65, &sliderError);
                Error(ref sliderError);
                if (accepted == 0)
                    throw new InvalidOperationException("The RTL Gallery Slider rejected a valid volume.");
                Wait(() => Volume() == 65, "the RTL Slider value");
                checks.Add("External AT-SPI writes and reads the live RTL Slider volume.");

                Reveal(direction);
                InvokeNamed(direction, "click");
                Wait(() => ComponentBox(daily.Value).X < ComponentBox(weekly.Value).X, "left-to-right option order");
                checks.Add("The actual direction button mirrors the RadioGroup's accessible bounds.");
                if (IsChecked(checkbox) || !IsChecked(sound))
                    throw new InvalidOperationException("Changing direction lost a toggle value.");
                checks.Add("The previously toggled Checkbox and Switch retain their states in LTR.");
                if (Volume() != 65)
                    throw new InvalidOperationException("Changing direction lost the Slider value.");
                checks.Add("The Gallery Slider preserves its numeric value when switching to LTR.");
                if (peer is not null)
                {
                    Wait(() => ComponentBox(weekly.Value).X < ComponentBox(monthly!.Value).X
                        && HasRadioSelection("매주"), "LTR option order and retained weekly selection");
                    checks.Add("Switching the provider direction preserves the selected RadioGroup value.");
                    FocusRadio(weekly);
                    peer.KeyBurst([0xff53]);
                    Wait(() => RadioSelected(monthly!, "매월"), "LTR Right selects and focuses the option on its right");
                    checks.Add("The X11 Right arrow selects the visual neighbor in the left-to-right RadioGroup.");
                    CaptureGalleryPair(peer, output, "ProviderRadioLeftToRight", checks);
                }

                bool HasRadioSelection(string label)
                {
                    using var selected = SelectedChild(radioSelection!.Value, 0);
                    return SelectionCount(radioSelection.Value) == 1 && selected is not null && Name(selected.Value) == label;
                }

                bool RadioSelected(NativeObject option, string label) => HasRadioSelection(label) && HasState(option.Value, 12);

                void FocusRadio(NativeObject option)
                {
                    Reveal(option);
                    using var component = new NativeObject(atspi_accessible_get_component_iface(option.Value));
                    nint error = 0;
                    var accepted = atspi_component_grab_focus(component.Value, &error);
                    Error(ref error);
                    if (accepted == 0)
                        throw new InvalidOperationException("Could not focus the Gallery RadioGroup option.");
                    Wait(() => HasState(option.Value, 12), "focused RadioGroup option");
                }
            }
            else if (probe == GalleryProbe.Holidays)
            {
                Navigate("공휴일 제공자");
                var calendar = new UiCultureCalendar(System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));
                var date = calendar.FormatFullDate(new DateOnly(DateTime.Today.Year, 1, 1));
                var offlineHoliday = date + " · 신정";
                using var initial = Required(offlineHoliday, window.Value, 56);
                checks.Add("External AT-SPI reads the date and offline holiday name from the same calendar cell.");

                using var outage = Required("연결 끊김 예제", window.Value, 43);
                InvokeNamed(outage, "click");
                using var bareDate = Required(date, window.Value, 56);
                WaitForRemoval(offlineHoliday, 56);
                InvokeNamed(bareDate, "click");
                void WaitForSelection(NativeObject cell)
                {
                    Wait(() =>
                    {
                        atspi_accessible_clear_cache(cell.Value);
                        using var states = new NativeObject(atspi_accessible_get_state_set(cell.Value));
                        return atspi_state_set_contains(states.Value, 23) != 0;
                    }, "native calendar cell selected state");
                }
                WaitForSelection(bareDate);
                checks.Add("External AT-SPI selects the date after the holiday annotation is removed.");

                using var load = Required("현재 달 공휴일 조회", window.Value, 43);
                InvokeNamed(load, "click");
                using var loaded = Required(date + " · 1월 1일", window.Value, 56);
                WaitForSelection(loaded);
                checks.Add("The live public lookup restores the holiday name and preserves the selected date in AT-SPI.");
            }
            else if (probe == GalleryProbe.DatePickers)
            {
                CheckDate("DatePicker", "저장 값 : 2024-03-15", "저장 값 : 2024-04-16");
                CheckDate("DateTimePicker", "저장 값 : 2024-03-15T15:04:05.0000000", "저장 값 : 2024-04-16T15:04:05.0000000");
                CheckTime("DateTimePicker", "저장 값 : 2024-04-16T15:05:05.0000000", null);
            }
            else
            {
                Navigate("TimePicker");
                CheckTime("TimePicker", "저장 값 : 15:05:05.0000000", nativeWindow);
            }
        }
        finally
        {
            atspi_exit();
        }

        File.WriteAllText(output, JsonSerializer.Serialize(new NativeGalleryCalendarAndPickerResult(
            RuntimeInformation.FrameworkDescription, checks.Count, checks.ToArray()),
            NativeGalleryCalendarAndPickerJson.Default.NativeGalleryCalendarAndPickerResult));
    }

    private static bool IsShowing(NativeObject target)
    {
        atspi_accessible_clear_cache(target.Value);
        using var states = new NativeObject(atspi_accessible_get_state_set(target.Value));
        return atspi_state_set_contains(states.Value, 25) != 0;
    }

    private static void Reveal(NativeObject target)
    {
        if (IsShowing(target)) return;
        using var component = new NativeObject(atspi_accessible_get_component_iface(target.Value));
        if (!ScrollComponentTo(component.Value, 6))
            throw new InvalidOperationException("The external client could not reveal '" + Name(target.Value) + "'.");
        Wait(() => IsShowing(target), "revealing a Gallery destination");
    }

    private static void InvokeNamed(NativeObject target, string name)
    {
        var actionInterface = atspi_accessible_get_action_iface(target.Value);
        if (actionInterface == 0)
            throw new InvalidOperationException($"'{Name(target.Value)}' ({Role(target.Value)}) has no AT-SPI Action interface.");
        using var action = new NativeObject(actionInterface);
        nint error = 0;
        var count = atspi_action_get_n_actions(action.Value, &error);
        Error(ref error);
        for (var i = 0; i < count; i++)
        {
            var nativeName = atspi_action_get_action_name(action.Value, i, &error);
            string? candidate;
            try
            {
                Error(ref error);
                candidate = Marshal.PtrToStringUTF8(nativeName);
            }
            finally { if (nativeName != 0) g_free(nativeName); }
            if (candidate != name) continue;
            var accepted = atspi_action_do_action(action.Value, i, &error);
            Error(ref error);
            if (accepted != 0) return;
            throw new InvalidOperationException($"AT-SPI action '{name}' was rejected for '{Name(target.Value)}'.");
        }
        throw new InvalidOperationException($"AT-SPI action '{name}' is missing for '{Name(target.Value)}'.");
    }

    [LibraryImport(Library)] private static partial nint atspi_accessible_get_table_iface(nint value);
    [LibraryImport(Library)] private static partial int atspi_table_get_n_rows(nint value, nint* error);
    [LibraryImport(Library)] private static partial int atspi_table_get_n_columns(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_table_get_accessible_at(nint value, int row, int column, nint* error);

    private static void ProfileScrollWheel(NativePeer peer, NativeObject item, int x, int y)
    {
        var path = Environment.GetEnvironmentVariable("DUXEL_APP_PROFILE_OUT")
            ?? throw new InvalidOperationException("The scroll profile requires DUXEL_APP_PROFILE_OUT.");
        var vulkanPath = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_OUT");
        void Mark(string phase)
        {
            Console.WriteLine("PROFILE_" + phase + " " + File.ReadLines(path).Count());
            if (!string.IsNullOrWhiteSpace(vulkanPath))
                Console.WriteLine("VK_PROFILE_" + phase + " " + File.ReadLines(vulkanPath).Count());
        }

        void Move(bool right)
        {
            var previous = ComponentBox(item.Value).X;
            peer.Pointer(4, x, y, right ? 7u : 6u);
            Wait(() => right ? ComponentBox(item.Value).X < previous : ComponentBox(item.Value).X > previous,
                "profiled horizontal movement in the ScrollView");
        }

        const int warmup = 8;
        const int samples = 24;
        for (var i = 0; i < warmup; i++)
            Move((i & 1) != 0);
        Thread.Sleep(250);
        Mark("BEGIN");
        for (var i = 0; i < samples; i++)
            Move((i & 1) != 0);
        Thread.Sleep(250);
        Mark("END");
    }

}

internal sealed record NativeGalleryCalendarAndPickerResult(string framework, int passed, string[] checks);
[JsonSerializable(typeof(NativeGalleryCalendarAndPickerResult))]
internal partial class NativeGalleryCalendarAndPickerJson : JsonSerializerContext;
