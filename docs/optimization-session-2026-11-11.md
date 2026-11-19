# 2026-11-11 SVG 아이콘·관찰 화면과 이미지 계약 검증

- 변경: UiSvgIcon이 기존 메뉴/탭의 regular/filled 상태를 공유 SVG 텍스처 경로에 연결한다. 실제 Observer SVG 탭은 일반 이미지·전용 위젯·아이콘과 확대/회전/이동·초기화·재로드·분리된 처리 횟수를 노출한다. 유지 레이어가 참조하는 텍스처도 제거 대상에서 제외한다.
- 발견/수정: 기존 Image 계열의 단일 UV, 이미지 효과의 알파255 강제, 높은 SVG ID의 폰트 마스크 분류를 고쳤다. 원본 알파와 이전 프레임 픽셀을 보존하고 IsColorImage로 색 샘플링을 명시한다. 기본 효과는 불변 원본을 직접 큐에 전달하며 RGB 효과 변경만 새 스냅샷을 만든다.
- Before/After: 배경 단독 RGB20/50/80은 정상이었으나 기존 일반 이미지 합성 뒤에는0/0/0이었다. 알파 보존 후 첫 합성도20/50/80이다. 원래 이미지와 전용 SVG 위젯의 GPU 영역이 동일하고, 회전/이동/기존 해상도 복귀에는 업로드가 늘지 않는다. 처리 횟수 계약이며 성능 향상율은 null이다.
- 검증: .NET11.0.100/11.0.0 최종3빌드 경고·오류0. SVG/이미지54, 관찰113(아이콘11 포함), GPU32(MSAA1/4), 실제 관찰 본문 재사용3, 메뉴GPU216/CPU376·탭197·스케줄링35. 관찰 PNG도 직접 확인했다. 명령/결과/실패 기록은 artifacts/svg-support-2026-11-11/observer-verification.json에 보존한다.
- 한계: CPU 시간·할당·Private/working-set·FPS·p95/p99·1% low·개선율은 미측정이다. 글꼴·전체 SVG·기기 수명/배포·NativeAOT와 원래 전체 목표도 완료하지 않았다. 평가용 관측 코드는 원인 확인 후 소스에서 제거하고 실패 증거는 유지했다.

# 2026-11-11 SVG 위젯의 공유 텍스처와 변환 재사용

- 가설: CPU 장면만 캐시해도 위젯이 매 프레임 래스터/업로드를 요청하면 불필요한 작업이 남는다. 문서 객체·버전·픽셀 크기와 변환을 분리한다.
- 변경: UiSvg의 논리 뷰포트/확대/회전/이동/틴트와 순수 측정. 컨텍스트가 임대 기반 텍스처를 공유하며 기존 직접 텍스트와 같은 소유 ID 할당기를 사용한다. 살아 있는 의존성은 제거하지 않고128개/64MiB 한도를 적용한다.
- Before: SVG 전용 위젯이 없었으므로 동일 조건의 시간·할당량·업로드 수치는 미확인이다. 임의의 개선율을 계산하지 않는다.
- After: 동일 위젯100프레임, 회전·이동·틴트 변경과 같은 크기 복귀에서 추가 업로드 요청0개. 두 위젯이 한 ID를 공유한다. 확대 해상도 변경은 장면을 재사용하고 XML은 다시 처리하지 않는다. 정적 SVG에 애니메이션/주기 스케줄 없음.
- 검증: tests/Duxel.Svg.Tests48개, Graphics 메뉴376/탭197/스케줄링35개. .NET11.0.100/11.0.0 빌드 경고·오류0. 원본·로그·명령·결과는 artifacts/svg-support-2026-11-11/widget-verification.json 참조.
- 한계: 업로드 요청을 검증했으며 GPU 실행 완료·픽셀·실제 처리량·시간·할당·메모리·FPS·p95/p99·1% low·개선율은 미측정이다. 동일 조건 교대3회 성능 비교를 수행하지 않았으므로 성능 향상율을 주장하지 않는다. 아이콘/관찰 창·글꼴·전체 SVG·NativeAOT도 남아 있다.

# 2026-11-11 SVG 원본 해석과 래스터 재사용

- 가설: 반복 표시마다 SVG XML/장면을 재구성하면 이벤트 대기 및 유지 렌더링의 이점을 잃는다. 원본 버전과 출력 크기를 분리해 재사용해야 한다.
- 변경: SvgStore가 최초 파일/XML과 장면을 보관한다. Rasterize는 버전/픽셀 크기의 LRU를 사용하며 회전/위치는 예정된 위젯 기하에서 처리한다. Reload는 명시적이며 동일 바이트면 해석하지 않는다. 결과 픽셀은 변경하지 않고 프레임/비트맵 소비자의 소유권을 구분한다.
- 환경/검증: SDK11.0.100/runtime11.0.0, net11.0-windows, 경고를 오류로 하는 관리 빌드 및 Duxel.Svg.Tests의28개 계약 검사. 원장: artifacts/svg-support-2026-11-11/product-verification.json. 워밍업/교대 성능 실행은 없으며 기능·처리 횟수 검사다. NativeAOT/RC3·앱 프레임 비교는 미실행이다.
- Before: 이전에는 SVG 제품 입력이 없어서 처리량·할당·FPS의 비교 기준이 없다. 미측정 값을0이나 가상의 매 프레임 파싱 횟수로 채우지 않는다.
- After: 동일 크기100회 재사용에서 파일/XML/장면/래스터 각1회, 캐시 적중100회. 소비자8개×100회 동시 요청에서도 각1회·적중799회. 보관한 래스터12288B는 CPU 캐시만 의미한다. 크기 변경은 장면을 재사용하며 LRU 축출 후 다시 만든 픽셀은 동일하다.
- 개선율: CPU 시간·할당·Private·작업 집합·평균 FPS·median/p95/p99·1% low를 측정하지 않아 계산하지 않는다. 최고 속도나 전체 GPU 비용 감소를 주장하지 않는다.
- 리스크/후속: SVG 텍스트/글꼴은 미구현으로 명시적 실패한다. 전용 위젯·아이콘·관찰 창과 실제 GPU 업로드·네이티브 배포/NativeAOT 및 전체 SVG 기능 검증을 이어간다. 기존 전체 목표·리사이즈 보류·도구체인 경로 탐색 금지를 유지한다.

# 2026-11-11 컨테이너 배치 무효화 교정

- 가설: 유지된 배치를 무효화해도 할당 크기가 같으면 이전 배치를 계속 재사용한다. 부모의 현재 프레임 배치를 보존하는 규칙과 다음 프레임 갱신을 구분해야 한다.
- 변경: `UiRetainedLayout`에 준비 프레임을 기록한다. 캐시에서 명시적 측정 결과를 읽을 때 이를 갱신한다. 위젯을 렌더링하는 동안의 무효화는 같은 자원 문맥의 해당 프레임을 보존한다. 현재 렌더 문맥은 스레드별 일시적 참조로 저장하고 `finally`에서 복원한다. 위젯 인스턴스마다 필드를 추가하지 않는다.
- Before: 같은100x100 할당에서 Grid를2열에서1열로 바꿔도 두 번째 자식이(50,0),50x20으로 남았다. 실패 로그를 보존했다.
- After: 두 번째 자식이(0,20),100x20으로 갱신된다. 같은 프레임의 명시적 측정, 다음 프레임의 반영, 변경 없는 측정 재사용, Stack/Container 정렬까지 새7검사가 통과했다.
- 초기 수정 회귀: 재사용된 부모 배치에서 먼저 실행된 콜백이 뒤쪽 ScrollView를 키울 때 기존 검사1개가 실패했다. 렌더링 도중 무효화 시 현재 프레임을 보존하고 명시적 캐시 측정의 프레임도 갱신해 ScrollView75개를 다시 통과했다. 기대값은 바꾸지 않았다.
- 검증: 고정SDK11.0.100-rc.3.26522.148, Graphics Release 경고0/오류0. CPU 컨테이너170, ScrollView75, 가상 목록75, 수명101, 접근성49. GPU252검사, 기존 컨테이너169개 readback SHA-256 동일. MSAA1/4에서 새8개 픽셀 비교를 수행했다.
- 실행: `node W:/MyWorks/AgenticShaping/scripts/run-workflow.mjs artifacts/assigned-arrangement-invalidation-2026-11-11/workflow-plan.json artifacts/assigned-arrangement-invalidation-2026-11-11/workflow-state.json --resume`로 기록과 재검증 경로를 확인한다. 기존 입력·결과를 가진 실행은 완료 단계를 다시 실행하지 않는다. GPU 원본은 검증 기록의 C: 경로에 있다.
- 샘플: 본문이 같은 flow-container FBA에 고정 검증 targets 지시문만 추가해 `./run-fba.ps1`의 로컬 프로젝트/Managed/NoCache 경로로 실행했다. 첫 표시531.49ms,5초 자동 정상 종료다. 수동 버튼 입력이나 공식 외형 승인의 근거는 아니다.
- 개선율: 시간·FPS·Private 전후 측정을 수행하지 않아 계산하지 않는다. 이번 결과는 배치 정확성 교정이다. 기존 유지 기록마다 프레임 정수 하나가 추가되며 전체 메모리 감소를 주장하지 않는다.
- 남은 범위: NativeAOT·런타임 이미지 비교와 전체 성능/컨테이너 게이트. 도구체인 경로를 반복 탐색하거나 설치하지 않았고 리사이즈는 계속 보류한다.
# SVG 명시적 글꼴과 장면 재사용

## Slider 수치 접근성 경로 — 외부 수명 검증 미완료

- Before: 생성형 Slider는 수치 접근성 노드를 게시하지 않아 관찰 SVG 검사도 포인터 좌표를 사용했다. 범위 모델·요청·이벤트를 기존 트리와 UI 실행 경로에 연결하고 Windows RangeValue를 구현했다.
- 변경: 논리 값·범위·작은/페이지 변경량·방향을 불변 값으로 게시한다. 수치 요청은 기존 큐·깨우기·현재 상태 재확인·스냅·바인딩 콜백을 공유한다. 같은 값·변화 없는 프레임은 추가 쓰기/알림을 만들지 않는다. 내부 컨트롤 ID가 표시 이름으로 보이던 첫 외부 검사 결함도 Label 전용 경로로 수정했다.
- 통과 근거: 관리/직접 공급자23개, 관찰125개, 메뉴376개, 예약35개, 접근성49개, Slider GPU156개와 관찰 GPU7개. 기존 Slider79개 PPM은 해시가 같고 관찰 본문도 이전과 같다. 실제 SVG 접근성1px 이동은 XML/래스터를 추가하지 않는다.
- 실패/미완료: 외부 활성 값·수치 속성·범위/방향·비활성 거부 검사는 통과했지만 제거 후 보관한 RangeValue getter는5초간 S_OK/0으로 남아 엄격한 수명 검사에 실패했다. 직접 공급자 조회와 외부 변경 요청의 제거 거부와 구분한다. 전체 외부 실행은 실패이며 실패한 소유 호스트는 기록 후 취소했다. 기대값을 완화하지 않고 검사를 유지한다. 한 번의 추가 관측 예산을 소진했으므로 새 근거 없이 같은 전체 실행을 반복하지 않는다.
- 비용/성능: UIA 노드에 범위 값 필드를 추가했다. 시간·FPS·관리/네이티브 메모리 전후 측정이 없어 개선율·메모리 감소를 주장하지 않는다. 진단·원본 로그·프로세스 종료 상태는 artifacts/slider-range-2026-11-11/verification.json에 분리한다. 기존 포인터·키보드·화질과 스냅 규칙은 유지한다.

## SVG 곡선 텍스트 참조 컴파일

- Before: 일반 tref·xlink·중첩 문자·확대/재사용은 정상이나 textPath 안의 tref는 완전히 투명한 픽셀을 출력했다. 고정 백엔드 SvgSceneTextCompiler의15321행은 해당 조합에서 false를 반환한다. tref-path 실패 및 literal/reference RGBA 비교 자료를 보존한다.
- 근거/변경: SVG1.1은 tref를 대상의 문자 데이터를 가진 등가 tspan으로 설명한다. 이미 해석한 XML DOM에서 대상의 문자 데이터를 읽고 백엔드 DOM/스타일 해석 뒤 타입 지정 복제 API로 참조를 등가 span으로 컴파일한다. 원래 요소 이름·ID·클래스·스타일·위치를 유지하고 별도 렌더러·프레임별 참조 처리를 추가하지 않는다. 참조 없는 문서는 ID/문자 데이터 테이블을 만들지 않는다.
- After: 일반/곡선 참조가 직접 작성한 문자와 픽셀이 같고, 경쟁하는 tref/tspan 선택자·ID/클래스·위치·엔티티·주변 형제 문자·소스 갱신을 확인했다. 곡선 문자를 일반 비트맵으로 읽고100회 재사용·새 해상도를 요청해도 XML/장면/글꼴 결정은 반복하지 않는다.
- 검증: CPU112·관찰123·SVG GPU52·관찰 GPU7 통과, 세 빌드 경고·오류0개. 실제 샘플의 문장을 직접 작성 방식에서 참조 방식으로 바꾼 뒤 이전 본문 픽셀과 완전히 일치했다. 전체 프레임은 상단 FPS 문구(y12–29)만 달랐다. 근거: artifacts/svg-support-2026-11-11/tref-verification.json.
- 개선율/한계: 컴파일 정확성 교정이다. 시간·FPS·관리/네이티브 메모리 전후 측정이 없어 개선율 미산정. 백엔드 복제 API의 패키징/NativeAOT와 전체 SVG/문자 체계/기기 수명 준수는 미검증이다. 기본 텍스트 참조는 이미 활성화되어 있었으며 이번 수정은 경로 위 참조 조합의 누락을 해결한다.

## SVG 문서 기능과 CSS 자원 토큰 검사

- Before: 실제 자원을 요청하지 않는 CSS 주석에 @import 예제 문구가 있으면 정상 SVG를 미지원 입력으로 거부했다. 수정 전 features-before 실행은 use/symbol·xlink·패턴·마스크·방사형 그라디언트·마커·내장 PNG까지 통과한 뒤 이 입력에서 실패했다.
- 변경: 부분 문자열·URL 정규식 검사를 자원 토큰 판독으로 교체해 주석·따옴표 속성값과 실제 URL/규칙을 구분한다. ExCSS의 공개 API에는 토큰 판독기가 없어 자원 경계만 검사하며 CSS 렌더링은 기존 백엔드를 유지한다. 이스케이프·CRLF·잘못된 자원 검사를 포함한다. XML 최초1회 계약과 기존 외부 자원 정책을 바꾸지 않는다.
- After: 정상 주석·문자열 문서가 실제 색 픽셀을 그리고 이스케이프한 실제 외부 URL/규칙과 잘못된 URL·문자열·주석·EOF 이스케이프는 게시 전에 실패한다. EOF 검사의 첫 fixture는 뒤에 붙인 CSS 닫는 중괄호가 이스케이프 대상이 되는 오류가 있었으며 실제 EOF 입력으로 바로잡았다. 실패 자료는 보존한다.
- 검증: CPU102·관찰123·MSAA1/4 SVG GPU52·관찰 GPU7 통과, 세 빌드 경고·오류0개. 추가 픽셀 기준은 색·투명도·반복 주기·마스크의 알려진 위치이며 자체 결과를 기대값으로 복사하지 않는다. 실제 포인터로 문서 기능 탭을 선택하고3회 획득 이미지 재사용도 확인했다. 근거: artifacts/svg-support-2026-11-11/features-verification.json.
- 샘플: SVG 페이지의 실제 이미지·텍스트/문서 기능 탭, 원본5개, 공유1px·1°·1% 조절기와 캐시 지표를 제공한다. 기능 탭은 패턴·radial gradient·재사용 심볼·마스크·마커를 표시한다.
- 개선율/한계: 이 변경은 입력 정확성 교정이다. 시간·할당·FPS 전후 측정이 없어 개선율은 미산정이며 동기 GPU 읽기 검사 화면의 FPS를 앱 성능으로 해석하지 않는다. 전체 SVG/CSS·문자 체계·네이티브 메모리/배포/AOT 준수는 미완료다.

## SVG 해제 뒤 백엔드 참조 정리

- 가설/재현: 저장소가 그림 핸들을 닫아도 호출자가 보관한 SvgAsset → SvgScene → SKSvg 경로가 렌더 캐시를 계속 보관할 수 있다. 수정 전 검사는 그림 핸들 해제는 통과했지만 백엔드 소유자의 도달 가능 여부에서 실패했다.
- 변경: 해제되는 SvgScene이 그림·백엔드 소유자 참조를 비우고 반복 해제를 허용한다. 저장소 잠금과 기존 그림/글꼴 해제 순서를 유지한다. 공개 API나 입력·화질을 바꾸지 않는다.
- After: 자원 참조를 계속 보관해도 백엔드 소유자는 수집 가능하다. 텍스트32회 갱신에서 이전 그림 핸들을 닫고 실패한 갱신에서는 현재 그림/글꼴 핸들을 유지한다. 저장소 해제는 등록 SKFont/SKTypeface와 최종 그림 핸들을 닫고 래스터 보관량을 비운다. 강제 GC는 도달 가능 여부 검사에만 사용하며 성능 부하에 삽입하지 않는다.
- 검증: CPU81·관찰120·SVG GPU44·관찰 GPU3 통과, 최종 빌드 경고·오류0개. SVG GPU43개 캡처는 이전 글꼴 단계와 바이트가 같다. 관찰6개 캡처의 본문도 모두 같으며 전체 프레임3개는 시간에 따라 달라지는 상단 FPS 문구(y12–29)만 다르다. 이 차이를 숨기거나 전체 프레임 일치로 보고하지 않는다. 근거: artifacts/svg-support-2026-11-11/lifetime-verification.json.
- 개선율/한계: 실행 시간·FPS·Private/working-set·전체 네이티브 할당 전후 측정은 수행하지 않아 개선율 미산정. 이번 결과는 오래 남는 소유 참조와 명시적으로 소유한 핸들의 수명 교정이다. 백엔드 내부 캐시의 모든 네이티브 할당을 결정적으로 해제하거나 NativeAOT/기기 종료를 인증한 것은 아니다.

- 가설: XML을 유지하는 장면에 명시적으로 결정한 글꼴·텍스트 명령도 보관하면 확대와 프레임 재사용에서 파일·글꼴 결정 작업을 반복할 필요가 없다.
- Before: SVG 텍스트는 미지원 오류였다. 이전의 텍스트 처리 시간이나 FPS는 측정하지 않았다.
- 변경: SvgFontOptions가 파일·컬렉션 면을 저장소 생성 시 보관한다. CSS 글꼴 목록과 실제 등록 스타일·글리프 포함 여부를 컴파일 시 결정하며, 공급자와 전용 로더가 설치 글꼴 자동 대체를 차단한다. 텍스트도 공유 장면·래스터·GPU 임대를 사용한다.
- After: Latin ABC의 최초 컴파일은 등록 면3개·공급자 요청1회·글리프 확인3회이며 같은 크기100회 재사용과2배 해상도에서 이 수치와 XML1회·장면1회가 유지된다. 해상도 변경으로 래스터만1회에서2회가 된다. 이 수치는 네이티브 글리프 래스터 작업 전체가 아니다.
- 검증: 자원/이미지/글꼴74개, 현재 관찰120개, MSAA1/4 SVG GPU44개와 실제 관찰 본문 재사용3개 통과. 일반 이미지 파일 API의 SVG 텍스트와 전용 문서 픽셀 경로도 확인했다. 빌드는 경고·오류0개이며 SDK11.0.100/runtime11.0.0의 관리 실행이다. 정확한 RC3 NativeAOT 검증으로 승격하지 않는다.
- 실패 기록: 직접 텍스트·윤곽선 참조는 SVG 백엔드의 위치 지정·힌팅 경로와 달라 GPU 비교에 실패했다. 실패 자료를 보존하고 같은 명시적 글꼴을 쓰는 표준 SVG 엔진과 뷰포트 전체를 RGB 오차1 이내로 비교했다. 독립적인 SVG 문자 체계 준수나 성능 비교는 아니다.
- 개선율: 시간·FPS·Private/working-set 전후 측정이 없어 미산정. 입력 기능이 달라진 미지원 단계와 성능 비율을 비교하지 않는다.
- 리스크/잔여: 전체 문자 체계·양방향·가변 글꼴·내장 글꼴·네이티브 수명/메모리·배포/AOT. 폰트 스냅샷 교체는 새 저장소가 필요하며 파일당 기본32MiB 상한이다. 근거는 artifacts/svg-support-2026-11-11/font-verification.json이다.


## Slider retired child provider identity boundary — 2026-11-11

Fixed the strict external RangeValue removal gate, preserving its expected0x80040201. Before: direct server errors propagated but UIA disconnection reported AlreadyUnavailable; external held getter returnedS_OK/0 and setter InvalidOperation. After retaining child FragmentRoot/null-host/configuration/runtime identity: actual disconnection succeeds and held getter/setter return0x80040201. Mutable node data/actions stay guarded. Intermediate global root metadata extension suppressed asynchronous TooltipClosed; the final implementation preserves HWND-root host lifetime and ordinary no-ACK shutdown passes. Failures remain in native-default-probe, native-identity-fix-probe and association-tooltip-shutdown.

Final managed28/accessibility49/Observer125/Menu376/scheduling35; external RangeValue client13/host3; Tooltip shutdown client6/host4; builds0warnings/errors. Evidence: artifacts/slider-range-2026-11-11/lifetime-verification.json. Existing native fixture and runner reused with new source inputs; no new general runner. This is a correctness change, with no timing/memory/performance improvement percentage claimed. Historical GPU evidence remains valid for unchanged paint/geometry. NativeAOT, exactRC3 and whole-goal gates remain open. Legacy SliderFloat/VSlider numeric migration is next.
## Legacy Slider numeric publication and shared request validation — 2026-11-11

Legacy SliderFloat/VSlider and integer/scalar/vector/array/angle wrappers now publish Slider range semantics and consume typed numeric requests through the same enabled/clip/popup/current-bounds queue validation as generated controls. Generated snapping remains separate; legacy floats stay continuous, keyboard steps stay increments, integer conversion retains ties-to-even and angle publishes degrees/commits radians. Existing float precision and focus behavior are retained and documented. Paint/pointer code is unchanged.

Focused44=28existing+16new,Observer127,accessibility49,Menu376,scheduling35 pass. External legacy6/host3 and generated13/host3 pass strict removal tests. ObserverGPU47 covers actual pointer-selected Immediate metadata plus existing acquired-image replay; screenshot viewed. Final graphics/sample builds0warnings/errors. Test-only compile and legacy-tab-assumption failures preserved; ordinary legacy headers have no semantic Tab relationship, which remains next scope. Existing GPU selection runner reused. No new general runner, fallback, timer or ACK. No speed/memory/percentage improvement claimed: correctness change only. See artifacts/slider-range-2026-11-11/legacy-verification.json; full NativeAOT/goal remains open.
## Legacy streaming tab semantics and publication completion — 2026-11-11

Connected existing streaming headers/bodies to common TabList/SelectionItem/TabPanel contracts, reciprocal links and nested parent restoration. Existing pointer/drawing logic remains frame-deferred with one body per frame; action buttons remain Invoke. Public tree wait=true waits for the selected publication; target close/removal rejects false completion. Windows SelectionItem keeps existing asynchronous acceptance. Required Deselect is rejected before enqueue. Committed transitions stop waking/scheduling.

Final legacy25(new15),Observer131,TabList197,accessibility49,range44,Menu376,scheduling35; native legacy15/host3 and generated25/host3 pass. ObserverGPU47 and all73 comparable body captures unchanged belowY128. Builds0warnings/errors; no speed/memory improvement claimed. Test discovery/focus/asynchronous-assumption failures and canceled owned-host records are preserved. Generated focus expectation stilltrue; legacy explicitlyfalse. No fallback or wait timer added. Evidence: artifacts/legacy-tabs-2026-11-11/verification.json. Full keyboard/flags, narration, exactRC3/AOT and original major goal gates remain open; next work should rejoin original remaining-unit ledger.
## Common Toggle semantics and immutable event getters — 2026-11-11

Before: generic Checkbox/Switch/ToggleButton published no canonical Toggle capability; nullable Checked could not distinguish mixed from unsupported. After: nullable ToggleState separates unsupported from Off/On/Indeterminate and Checked projects the same state. Static roles and existing typed request/callback paths preserve input and paint; value-policy Checked is evaluated once per frame. Frozen event providers now implement read-only native Toggle from their captured snapshot even after source-tree disposal, rejecting actions.

Verification: managed21, Observer134, generators115, menu376/accessibility49/range44/legacy25/TabList197/scheduling35. Actual external Toggle11/host4, legacy menu6/host3, ordinary no-ACK TooltipClosed6/host4 all terminal0. ObserverGPU47 and73 previous body images unchanged belowY128. Graphics and actual Observer builds0warnings/errors, SDK11.0.100/runtime11.0.0; not exactRC3 or NativeAOT. Shared peers painted earlier update next requested frame; no transactional repaint claim. Baseline missing semantics and test-assumption failures preserved; stale DLL invocation excluded. Existing runners reused, no fallback/ACK/timer. No speed, memory, FPS or improvement percentage claimed; this is correctness work. Whole-goal ledgers unchanged. ExactRC3/NativeAOT, complete lifetime/narration/portable scope and strict icon declaration-removal cache gate remain open. See artifacts/toggle-accessibility-2026-11-11/verification.json and scope-audit.json.

## RadioGroup exclusive selection accessibility — 2026-11-11

Before: existing RadioGroup layout/paint/input had no semantic owner/options or native SelectionItem. After: common generated/generic/direct paths publish Group50026/Selection10001 and RadioButton50013/SelectionItem10010, never Toggle/Invoke. Select/Focus reuse real caller binding, current disabled/clip/popup checks and original roving keyboard behavior. Null/absent caller values stay untouched; selected radio cannot be removed from selection. IDs follow immutable values across reorder and clear with owned content. Focus moves without selecting. Parent allocation and paint geometry remain unchanged.

Verification: focused18, pre-existing input/allocation91, actual Observer137, TabList197/Menu376/Toggle21/accessibility49/scheduling35 pass. Actual external client9/host3 both terminal0; real cached selected event and both original-source property changes, exact0x80040201 after group removal. ObserverGPU47 and73 prior bodies identical belowY128. Final app/Graphics builds0warnings/errors, SDK11.0.100/runtime11.0.0, not exactRC3/NativeAOT. Native-first failed because shared test sink read IsSelected only for tabs/menu items, corrected to also read RadioButton50013. Native-final failed because the client queried before next-frame publication after a later-painted removal button; existing bounded committed-state wait reused without relaxing errors. Owned hosts64088/63272 canceled and both failed clients terminal; logs preserved. Final native-qualified54340/3648 terminal0, no cancellations. No product timer, ACK, fallback, layout/pixel change or extra preview. No timing/memory/performance improvement percentage claimed. Whole-goal original gates remain active; see artifacts/radio-accessibility-2026-11-11/verification.json.
