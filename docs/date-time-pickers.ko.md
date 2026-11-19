# 날짜·시각 입력 선택기

`./run-gallery.ps1` → **Duxel 위젯 → 날짜와 시간**에서 DatePicker·TimePicker·DateTimePicker를 각각 실행합니다. 예제 바로 아래 코드가 지역·12/24시간제·초 표시·현재 값에 맞춰 바뀝니다.

갤러리 화면 표시 검사는 날짜 팝업을 열어 월 휠에서 다음 달을 고른 뒤 날짜를 선택합니다. 시각 팝업에서는 분을 고르고 **확인**으로 닫은 뒤 DateTimePicker에 날짜와 시각이 함께 저장됐는지 확인합니다. 갤러리의 접근성 트리로 실제 컨트롤에 명령을 전달하고 Vulkan 화면 표시를 기다립니다.

Windows x64 .NET 11.0.0 관리형과 NativeAOT에서 `calendar-rendering-only`와 `temporal-gallery-only`를 실행했습니다. 달력 검사는 각각 68개를 통과했고 Vulkan PNG 36쌍이 일치했습니다. 날짜·시각 선택기 검사는 각각 22개를 통과했고 팝업, 태국어, 공휴일 연결 끊김 예제를 포함한 23쌍이 일치했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/calendar-runtime-pixels-1937/comparison.json`입니다.

Windows x64 .NET 11 NativeAOT 갤러리를 실행한 상태에서 빌드한 테스트를 `Duxel.Graphics.Tests <새-결과-폴더> uia-gallery-time-picker <갤러리-PID>`로 실행합니다. 별도 Windows UI Automation 클라이언트가 TimePicker를 열고 분을 05로 골라 `15:05:05`를 읽었습니다. **확인**을 눌러 팝업을 닫은 뒤에도 값이 유지됐습니다. 세 검사를 통과했고 두 프로세스가 정상 종료했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/uia-gallery-time-1930/native-probe/result.json`입니다.

같은 명령에서 `uia-gallery-time-picker`를 `win32-gallery-time-picker-wheel`로 바꾸면 Windows 휠 메시지 처리도 확인합니다. 검증 도구가 갤러리 창에 아래·위 휠 메시지를 한 번씩 보내자 저장 값이 `15:05:05 → 15:06:05 → 15:05:05`로 바뀌었습니다. 다섯 검사를 통과했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/uia-gallery-time-1931/native-wheel/result.json`입니다.

`uia-gallery-date-pickers`로 실행하면 별도 Windows 클라이언트가 DatePicker와 DateTimePicker를 조작합니다. 한국 지역 설정에서 두 선택기의 월 휠로 4월을 고르고 16일을 선택했습니다. DateTimePicker는 날짜 변경 중 시각을 유지했고 이후 분을 바꾸자 `2024-04-16T15:05:05`가 저장됐습니다. 일곱 검사 결과는 `W:/Cache/Duxel/gallery-source-20261117/uia-gallery-date-1932/native-date/result.json`에 있습니다.

Windows 관리형과 Ubuntu/WSLg x64 NativeAOT에서 세 선택기 예제를 포함한 화면 표시 시나리오 104개가 각각 통과했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/temporal-gallery-1927-retry/verification.json`입니다.

별도 .NET 11 AT-SPI 클라이언트가 D-Bus로 Linux NativeAOT 갤러리의 TimePicker를 조작했습니다. 메뉴 이동, 분 휠 선택, 팝업 닫기를 통과했고 저장된 시각 `15:05:05`를 읽어 확인했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/atspi-gallery-time-1928-retry/atspi-result.json`입니다.

같은 D-Bus 세션에서 `Duxel.Linux.App.Tests atspi-gallery-date-pickers <갤러리-PID> <새-결과.json>`를 실행하면 외부 클라이언트가 두 날짜 선택기를 조작합니다. `C.UTF-8` 환경의 x64 NativeAOT 갤러리에서 일곱 검사가 모두 통과했습니다. DatePicker는 4월 16일을 저장했고, DateTimePicker는 마지막 팝업을 닫은 뒤에도 `2024-04-16T15:05:05`를 유지했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/atspi-gallery-date-1933/date-fresh/result.json`입니다.

검증용 창에 X11 아래·위 휠 이벤트를 각각 한 번 보냈을 때 저장 값은 `15:05:05 → 15:06:05 → 15:05:05`로 바뀌었고 초는 유지됐습니다. 총 다섯 검사가 통과했습니다. 검증 도구가 X11 이벤트를 주입한 결과입니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/x11-gallery-wheel-1929/client-result.json`입니다.

```csharp
var date = new UiState<DateOnly?>(null);
var datePicker = Dux.DatePicker("날짜", date.Binding);
var time = new UiState<TimeOnly?>(null);
var timePicker = Dux.TimePicker("시각", time.Binding);
var appointment = new UiState<DateTime?>(null);
var dateTimePicker = Dux.DateTimePicker("예약", appointment.Binding);
```

DatePicker는 지역화한 날짜 입력과 Calendar 팝업을 하나의 바인딩으로 연결합니다. `UiDatePickerOptions.System`에 읽기 전용 `UiCultureCalendar`를 지정합니다. 기본 형식은 해당 문화권의 짧은 날짜이며, Format을 지정하면 그 형식으로 정확히 파싱합니다. Availability·Holidays·HolidayProvider는 공통 달력으로 전달합니다.

TimePicker는 직접 입력과 시·분 휠을 제공합니다. `UiTimeHourCycle.Culture`는 지역의12/24시간제를 따릅니다. Hours12·Hours24로 고정할 수도 있고 ShowSeconds로 초 휠을 추가합니다.12시간제는 오전·오후 휠이 있으며 RTL 문화권에서는 열 순서를 반대로 배치합니다. 휠은 스크롤·드래그·방향키·행 선택을 지원합니다. 확인 버튼으로 닫습니다. 표시하지 않는 초·초 미만 값은 편집 시 보존합니다. 빈 값의 휠 시작점은 InitialTime, 생략 시00:00입니다.

```csharp
timePicker.Options = timePicker.Options with
{
    HourCycle = UiTimeHourCycle.Hours24,
    ShowSeconds = true
};
```

DateTimePicker는 DatePicker·TimePicker를 재사용합니다. 한쪽만 입력하면 임시 값을 보관하고 둘 다 갖춰지면 DateTime 하나를 확정합니다. 기존 Kind를 유지하며 시간대를 변환하지 않습니다. 처음 입력한 값은 Unspecified입니다. `Clear()`는 미완성 임시 값까지 함께 지웁니다. DateTimeOffset·시간대 선택은 이번 API에 포함되지 않습니다.

잘못된 문자열은 입력창에 남기며 마지막 정상 값은 덮어쓰지 않습니다. 필드에 검증 메시지를 표시합니다. 빈 문자열은 null입니다. 제어 바인딩이 새 값을 거절하면 적용됐다고 처리하지 않습니다. 외부 값이 바뀌면 텍스트를 동기화하며 지역 변경 시 현재 값을 다시 표기합니다. 네트워크 공휴일 실패는 입력 오류와 별개이며 오류 문구를 표시하지 않습니다.

`UiPickerLabels`로 필드 조작·검증 문구를 지정합니다. 한국어 갤러리는 한국어 문구를 사용하며 날짜 문화권만 별도로 바꿀 수 있습니다. 글꼴은 부모 구성을 따릅니다. 태국어 예제는 동봉한 Noto Sans Thai를 명시적으로 지정합니다.

## 공통 휠

달력과 시각 선택은 `UiWheelPicker<T>`를 공유합니다. 이웃 값·표기·안정 ID는 `IUiWheelSequence<T>`로 분리합니다. 선택 상태별 행은 한 번 준비하고 그대로인 프레임은 재사용합니다. `UiIntegerWheelSequence`는 유한/순환 정수 구간과 지역 숫자를 제공합니다.

```csharp
var minute = new UiState<int>(30);
var wheel = Dux.WheelPicker(minute.Binding,
    new UiIntegerWheelSequence(0, 59, wrap : true),
    new UiWheelOptions("분", Width : 72));
```

Windows 검증과 재사용한 검사 범위는 `artifacts/calendar-widgets-2026-11-17/result.json`에 기록합니다. 실제 Linux와 고정 RC3 NativeAOT 실행은 별도 수용 항목입니다.

DatePicker의 달력은 하나의 팝업 표면에 바로 배치합니다. TimePicker의 확인 버튼은 시간 휠과 같은 폭을 사용하고 팝업 양쪽에 같은 여백을 둡니다.
