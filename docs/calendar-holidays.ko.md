# 공휴일 제공자

갤러리는 인증키나 계정 없이 받을 수 있는 [holidays-kr의 연도별 공개 JSON](https://github.com/hyunbinseo/holidays-kr/tree/main/public)을 사용합니다. 다른 국가나 사내 휴일도 같은 인터페이스로 연결할 수 있습니다.

```csharp
public interface IUiCalendarHolidayProvider
{
    ValueTask<UiCalendarHolidayResult> GetHolidaysAsync(
        int year, int month, CancellationToken cancellationToken = default);
}
```

연·월은 DateOnly의 양력 기준입니다. `UiCalendarHolidaySet`은 날짜와 이름을 준비한 불변 스냅샷입니다. 같은 날의 서로 다른 이름을 함께 보관하고 중복은 제거합니다. 휴일이 없는 정상 응답과 네트워크 실패는 상태로 구분합니다.

```csharp
using Duxel.App;

using var http = new HttpClient();
IUiCalendarHolidayProvider provider = new KoreanHolidayProvider(http);
var date = new UiState<DateOnly?>(null);
var calendar = Dux.Calendar(date.Binding, new(HolidayProvider : provider));
```

갤러리에서 **날짜와 시간 → 공휴일 제공자 → 현재 달 공휴일 조회**를 누르면 바로 불러옵니다. 처음 표시되는 신정은 오프라인 예제 데이터입니다. 조회할 수 있는 연도는 공개 파일의 범위에 따르며, 새로 지정된 공휴일은 원본 데이터에 반영된 뒤 표시됩니다.

## 실행과 실패 처리

- Calendar는 보이는 범위나 제공자가 바뀌거나 새로고침을 요청할 때 작업을 예약합니다. 6주 그리드가 걸치는 양력 월을 작업 스레드에서 조회하고 UI 스레드에서 결과를 적용합니다.
- 완료 시 화면을 한 번 깨우며 대기 중 애니메이션이나 주기적 화면 폴링을 만들지 않습니다. 월 이동·제공자 변경·해제 시 지난 작업을 취소하고 오래된 결과를 적용하지 않습니다. 색상만 바꾸면 요청을 재시작하지 않습니다.
- 네트워크 오류·연결 중단·시간 초과에는 **오류 메시지 없이 공휴일 표시만 생략**합니다. 사용자 정의 제공자에서 통신 예외가 발생해도 같은 방식으로 처리합니다. 선택·달 이동·입력은 계속 동작합니다. ServiceRejected/InvalidResponse도 표시 데이터가 없으며 상태·고정 진단 코드로 구분할 수 있습니다. 원본 서버 메시지·요청 URL·인증키는 진단 결과에 담지 않습니다.
- 공개 제공자는 연도별 파일 한 개를 받아 12개월 결과를 미리 준비합니다. 최대 8개 연도를 보관하며 정상 데이터는 6시간, 실패는 30초 뒤 다시 요청할 수 있습니다. 연도 파일이 없거나 응답이 잘못되면 공휴일 표시를 생략합니다. JSON은 최대 64KiB까지 읽고 날짜·연도·이름을 확인합니다.
- 기존 `KoreanPublicHolidayProvider`는 [한국천문연구원 특일 정보 API](https://www.data.go.kr/data/15012690/openapi.do)에 인증키로 연결하는 앱에서 계속 쓸 수 있습니다. `Calendar.RefreshHolidays`는 연결된 제공자의 캐시 정책을 따릅니다.

미리 취득한 데이터만 사용하려면 `new UiCalendarOptions(Holidays : snapshot)`을 지정합니다. Holidays와 HolidayProvider는 동시에 지정하지 않습니다. 휴일은 빨간 날짜·점·이름 Tooltip·접근성 이름으로 표현하며, 휴일이라는 이유만으로 선택을 막지 않습니다. `UiCalendarAvailability`로 선택 제한을 따로 지정합니다. 시스템 대비 색상이 사용자 휴일 색상보다 우선합니다. DatePicker도 Holidays/HolidayProvider를 같은 방식으로 받습니다.

`holiday-provider-only`와 `calendar-holiday-loading-only` 검사에서 파싱, 캐시, 실패 처리를 확인합니다. 그래픽 테스트 실행 파일의 `holiday-provider-live`로 갤러리와 같은 제공자를 통해 실제 공개 데이터를 조회할 수 있습니다.

호출자가 소유한 HttpClient와 제공자는 달력을 사용하는 동안 유지하고, 달력을 먼저 해제한 뒤 정리합니다.
## 갤러리 검증

Windows에서 실제 데이터를 조회해 2025년 임시공휴일과 2026년 부처님 오신 날 대체공휴일, 전국동시지방선거일을 확인했습니다. 갤러리의 조회 버튼도 작동시켜 달력에 대체공휴일이 표시되는 것을 확인했습니다. Ubuntu/WSLg Linux x64 NativeAOT 갤러리에서는 연결 끊김 뒤 날짜 선택을 포함해 화면 표시 시나리오 104개가 통과했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/keyless-holidays-1944/holiday-gallery-live-retry/result.json`과 `W:/Cache/Duxel/gallery-source-20261117/keyless-holidays-1944/linux-presentation/result.json`입니다.

저장소 루트에서 아래 명령으로 갤러리의 실제 조회를 검증할 수 있습니다. 아직 없는 출력 폴더를 지정하세요. 갤러리가 2026년 5월 대체공휴일을 달력에 표시하고 예제 코드도 갱신한 뒤 `result.json`을 남깁니다. 일반 `--verify`에서는 외부 접속 없이 연결 끊김 동작을 확인합니다.

```powershell
.\run-gallery.ps1 -VerifyLiveHolidays -VerificationOutput .\artifacts\windows-live-holidays
.\run-gallery-linux.ps1 -SampleArguments @('--verify-live-holidays', '/tmp/duxel-linux-live-holidays')
```

실제 조회 검사는 .NET 11.0.0 Windows 관리형과 Ubuntu/WSLg Linux x64 NativeAOT 갤러리에서 통과했습니다. 같은 Linux 실행 파일로 일반 화면 표시 검사 104개도 통과했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/linux-keyless-holiday-1950/verification.json`에 있습니다.

WSLg의 별도 `libatspi` 클라이언트에서 달력 셀의 오프라인 공휴일 이름 `신정`을 읽었습니다. 연결 끊김 예제로 바꾸고 날짜를 선택한 뒤 실제 조회 버튼을 눌렀습니다. 공개 데이터의 이름 `1월 1일`이 붙고 선택 상태도 유지됐습니다. 보조 프로그램이 AT-SPI로 읽는 이름과 선택 상태, 실행할 수 있는 동작을 검증한 결과입니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/atspi-holiday-1951/holiday-retry/result.json`에 있습니다.

Windows에서도 별도 UI Automation 클라이언트가 관리형과 x64 NativeAOT 갤러리에서 같은 동작을 확인했습니다. 실제 조회 뒤 날짜 이름과 `SelectionItem.IsSelected`를 읽었고, 두 갤러리 모두 정상 종료했습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/uia-holiday-1952/managed/result.json`과 같은 실행 폴더의 `native/result.json`입니다. 스크린리더 음성 안내는 별도로 확인합니다.

