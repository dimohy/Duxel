# 달력 세계화 조사와 구현 계약

2026-11-17 사용자 요청: 단일 날짜와 기간 선택을 모두 지원합니다. 날짜·문화권·월 그리드를 화면 코드와 분리하고 단일·기간 선택, 입력·접근성·갤러리를 연결했습니다.

## 조사에서 확인한 점

- 날짜 표기는 언어만 바꾸는 문제가 아닙니다. 주 시작 요일·주 번호 규칙·달력 체계·숫자와 글의 방향을 별도로 다뤄야 합니다. CLDR는 주 시작 요일과 주말을 구분하고, 달력 머리글의 월 이름과 문장 속 월 이름도 구분합니다. [CLDR 날짜 규칙](https://unicode.org/reports/tr35/tr35-dates.html).
- .NET은 문화권마다 기본·선택 가능한 달력이 다릅니다. 지원하지 않는 달력을 DateTimeFormat에 지정하면 예외가 나므로 양력이나 영어로 조용히 바꾸지 않습니다. [Microsoft 달력 안내](https://learn.microsoft.com/en-us/dotnet/standard/datetime/working-with-calendars).
- .NET의 NativeDigits만 설정해도 숫자가 자동 변환되는 것은 아닙니다. 숫자 표시는 준비 단계에서 명시적으로 처리합니다. [NativeDigits](https://learn.microsoft.com/en-us/dotnet/api/system.globalization.numberformatinfo.nativedigits).
- 날짜 그리드는 한 칸만 Tab 순서에 넣고 방향키·Home/End·PageUp/PageDown을 지원하는 접근성 패턴을 기준으로 삼습니다. [W3C 날짜 선택 예제](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/examples/datepicker-dialog/).
- 공식 Fluent 명세와 RangeCalendar 사례를 함께 확인했습니다. 입력 필드와 달력은 책임을 나누고, 기간 선택과 다른 달력 체계를 처음부터 설계에 포함합니다. [Fluent 명세](https://github.com/microsoft/fluentui/blob/master/specs/Datepicker.md), [React Aria 기간 달력](https://react-aria.adobe.com/RangeCalendar).

## 구현 계약

| 영역 | 결정 |
| --- | --- |
| 저장 값 | DateOnly의 절대 날짜를 사용합니다. 선택 때 시간대·DST 변환으로 날짜를 옮기지 않습니다. |
| 선택 | 단일 날짜와 시작·종료 기간을 지원합니다. 기간의 임시 시작점과 확정 값을 구분하고 역방향 선택도 처리합니다. |
| 달력 체계 | IUiCalendarSystem으로 날짜 계산·표기를 분리합니다. 기본 어댑터는 읽기 전용으로 복사한 CultureInfo와 System.Globalization.Calendar를 사용합니다. |
| 기본 설정 | OS에서 설정한 CurrentCulture를 캡처합니다. 문화권·주 시작 요일·글 방향은 명시적으로 바꿀 수 있습니다. |
| 표시 | 지역화한 월·연도·요일, 지역 숫자 또는 라틴 숫자, RTL 배치를 지원합니다. 요일의 짧은 이름과 접근성용 전체 이름을 따로 보관합니다. |
| 경계 | 윤일·윤달·13개월·일본 연호 변경·달력별 최소/최대 날짜를 다룹니다. 월 시작일이 지원 범위 밖이어도 잘못된 날짜를 만들지 않습니다. |
| 주말·주 번호 | 일요일을 무조건 빨갛게 표시하지 않습니다. 주말 정책은 주 시작 요일과 별개입니다. 문화권 주 번호와 ISO 주 번호를 혼동하지 않습니다. |
| 제약 | 최소/최대 날짜와 선택 불가 날짜·기간을 표현하고, 범위 안의 선택 불가 날짜 처리도 명시합니다. |
| 입력·접근성 | 포커스와 선택을 분리하고 날짜 전체 이름·오늘·선택·비활성 상태를 제공합니다. RTL에서도 물리적 좌우 이동이 일관되어야 합니다. |
| 실행 비용 | 문화권 정보는 한 번 캡처하고 월 그리드는 월·문화권 설정이 바뀔 때 준비합니다. 날짜마다 XML을 읽거나 매 프레임 월 계산·지역화를 반복하지 않습니다. |
| 예제 | 한국·미국·영국·아랍어·페르시아어·히브리어·태국어·일본어를 비교하고 단일/기간 선택 각각 바로 아래에 실제 설정에 맞는 코드를 둡니다. |

언어와 달력의 임의 조합이 .NET의 선택 가능한 달력에 포함되지 않으면 명시적으로 거부합니다. IUiCalendarSystem으로 별도 날짜 계산·표기 어댑터를 제공할 수 있도록 경계를 엽니다. 모든 역사적 달력과 언어 조합, 운영체제·스크린리더를 검증했다고 주장하지 않습니다. 실제 실행에 성공한 조합과 지원 날짜 범위를 따로 기록합니다.

## 사용법과 현재 구현

`./run-gallery.ps1`을 실행하고 **Duxel 위젯 → 날짜와 시간 → Calendar / RangeCalendar**를 선택하세요. 각 예제 바로 아래에서 현재 설정의 코드를 확인할 수 있습니다. 문화권·주 시작 요일·글 방향·날짜 숫자·주말 색상·선택 제한을 바꿀 수 있습니다.

```csharp
var date = new UiState<DateOnly?>(null);
var calendar = Dux.Calendar(date.Binding);
var range = new UiState<UiCalendarDateRange?>(null);
var rangeCalendar = Dux.RangeCalendar(range.Binding);
```

- 클릭·Enter·Space는 날짜를 선택합니다. 방향키는 선택 값과 별개로 포커스만 이동합니다. Home/End는 현재 문화권의 한 주 안에서 이동합니다. PageUp/PageDown은 월, Shift+PageUp/PageDown은 연도를 바꿉니다.
- 위의 연도·월을 각각 누르면 세로 휠이 열립니다. 마우스 휠·드래그·방향키로 이동하고 행 클릭·Enter로 닫습니다. Escape는 휠을 닫습니다. 휠 이동은 표시 중인 달을 바꾸며 확정된 날짜 값은 바꾸지 않습니다. 공통 Popover가 포커스·팝업 계층·닫기를 처리합니다.
- 기간은 두 번 선택해 확정합니다. 역방향도 정규화합니다. Escape는 임시 시작점만 취소하며 기존 확정 값은 유지합니다. 선택 불가 날짜가 사이에 있으면 그 기간을 확정하지 않습니다.
- `UiCalendarAvailability`는 최소·최대 날짜와 병합한 선택 불가 구간을 사용합니다. 긴 기간도 날짜별 반복 대신 준비한 구간을 검색합니다.
- 기본 주말은 [CLDR48 weekData](https://github.com/unicode-org/cldr/blob/release-48/common/supplemental/supplementalData.xml)를 따릅니다. 한국은 토요일 파랑·일요일 빨강입니다. 사우디아라비아는 금·토요일, 이란은 금요일입니다. 중립·불변 문화권은 CLDR의001 규칙을 사용합니다. `Weekend`와 `DayColors`로 명시적으로 바꿀 수 있습니다. 공휴일은 별도 데이터이며 시스템 대비 색상이 우선합니다.
- 월·연도·요일·숫자와 접근성용 전체 날짜를 지역화합니다. 히브리 윤달·13개월, 일본 연호 경계, 달력 최소·최대 범위를 처리합니다. `UiCalendarDayNumbers.Latin`은 날짜 칸만 바꾸고 머리글·접근성 날짜는 문화권을 유지합니다.
- 태국어 갤러리는 OFL 라이선스를 동봉한 Noto Sans Thai를 명시적으로 사용합니다. 달력·선택 날짜 요약·제한 설명 모두 같은 글꼴 범위에 들어가므로 날짜를 선택한 뒤 태국어로 바꿔도 글리프 누락이 발생하지 않습니다. `Fluent.FluentProvider(...).Fonts`로 부모 색상과 기하 토큰을 유지하며 글꼴만 지정할 수 있습니다.
- 오늘 날짜는 생성 시 캡처합니다. 자정 갱신이 필요하면 호스트가 `Options.Today`를 바꿉니다. 정지한 달력은 계속 프레임을 요청하지 않습니다.

`UiCultureCalendar`는 문화권과 .NET Calendar를 읽기 전용으로 복사합니다. 지원하지 않는 조합은 명시적으로 거부합니다. `IUiCalendarSystem`은 계산·표기 확장 경계이고, `IUiCalendarPeriodSystem`을 함께 구현하면 사용자 정의 달력에도 월·연도 휠을 제공할 수 있습니다. 주 번호·공휴일 산출·시간대 변환을 달력 체계에 혼합하지 않습니다. 주 번호 표시는 이번 구현에 포함되지 않습니다.

달력은 기존 Fluent2 핵심55개 집계와 구분하는 Duxel 확장입니다. Fluent 표현과 공식 날짜 선택 지침을 참고했으며 모든 Fluent 구현과 동일한 픽셀이라는 뜻은 아닙니다. 현재 Windows의 모델·입력·Vulkan·갤러리 검증 기록은 `artifacts/calendar-widgets-2026-11-17/result.json`입니다. 실제 Linux 화면·외부 보조공학 전체 수용·고정 RC3 NativeAOT는 미검증입니다. [입력 선택기](date-time-pickers.ko.md), [공휴일 제공자](calendar-holidays.ko.md).

Linux x64에서 갤러리를 띄운 뒤 외부 검증기 `Duxel.Linux.App.Tests`의 `atspi-gallery-range-calendar <갤러리 PID> <새 결과 JSON 경로>`를 실행할 수 있습니다. 첫 날짜를 고른 상태, 종료일을 먼저 골라 기간을 확정하는 동작, 태국 불기로 전환한 뒤 두 날짜가 유지되는지, 선택을 지웠을 때 상태가 해제되는지를 확인합니다. 검증 범위는 접근성 이름과 동작, 선택 상태입니다.

X11 키보드 입력도 확인하려면 `x11-gallery-range-calendar <갤러리 PID> <새 결과 JSON 경로> <10진수 X11 창 ID>`를 실행하세요. Enter로 포커스가 있는 날짜에서 새 기간 선택을 시작한 뒤 Esc를 누릅니다. 검증기는 임시 선택이 취소되고 접근성 트리에 이전 기간이 다시 선택된 것으로 표시되는지 확인합니다.

1280×900 갤러리 창에서 `x11-gallery-range-calendar-pixels <갤러리 PID> <새 결과 JSON 경로> <10진수 X11 창 ID>`를 실행하면 키보드로 기간을 확정하고 결과 파일 옆에 화면을 저장합니다. Enter → 오른쪽 방향키 → Enter로 새 기간을 확정한 뒤, 태국 불기 달력으로 바꿔 같은 날짜가 선택되어 있는지 AT-SPI로 확인합니다. 기존 기간 복원, 새 기간 확정, 달력 변경 화면을 각각 두 번 캡처합니다.

WSLg x64의 .NET 11.0.0 관리형과 NativeAOT에서 각각 외부 검사 10개를 통과하고 정상 종료했습니다. 두 런타임의 화면 여섯 쌍은 상단 진단 표시 아래에서 픽셀까지 일치했습니다. 같은 런타임에서 상태별로 두 번 캡처한 화면은 창 전체가 일치했습니다. 합성 X11 입력과 실제 AT-SPI 상태를 확인했습니다. 스크린리더 음성과 ARM64 실기기 검증은 남아 있습니다.
