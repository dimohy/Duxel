# 최적화 세션 기록 — 2026-11-06

## 전체 기존 컨테이너의 유지 결과 통합

구조: UiRetainedLayout<TResult> 한 개로 유지 키·제약·결과·무효화 변경 횟수를 통합하고 Row/Column·Container·Stack에도 연결했다. 배치 인스턴스의 상시 변경 횟수 필드는 없애고 선택적 참조만 두었다. 기존 자식 크기/영역/부모 데이터 배열은 재사용한다. Expanded/Flexible/Spacer/Positioned는 부모가 정한 렌더 크기를 사용해 두 번째 자식 측정을 없앴다. 사용자 박스 배치/축/flex맞춤 정책은 정적 참여 값이 기본false이며 알려진 기본 구현만true다. 시간 등 외부 값에 의존하는 사용자 정책을 캐시하지 않음을 추가 검사했다. Row/Column 위치 순회와 전체 증분 배치·말단/단일자식/스크롤 과제는 남아 있다.

최종 관리/고정RC3 Native 각각175검사·MSAA1/4·3슬롯 GPU121이미지 전부 동치. 기존 순수 GPU 블록을 참여시켰고 독립 참조 기대값은 바꾸지 않았다. 가중치·Stack위치·Container옵션 변경과 여러 획득 이미지의 재사용을 확인했다. UiWidget/측정/버튼 기반과 기존 트리/소유권/수명 검사 소스는 그대로이고 변경한 배치 경로를 쓰지 않아 이전101검사 근거를 재사용했다. 호환성은 artifacts/container-retention-2026-11-06/focused-reuse.json에 기록한다. 실제 최신 샘플 첫 표시+15초 정상 종료0은 수동 입력/시각·리사이즈 승인과 구분한다.

비교는 이전 검증 Native2954F447을 기준으로 재사용해 기준 빌드를 다시 하지 않았다. 같은 측정 소스 LayoutRetentionTests.cs/ContainerWidgetTests.cs의두 모드를 각각3회씩 ABBAAB,총12회 실행했다. 기존 Wrap/Grid 무변경 순수 측정 중앙값은0.3→0.3µs이고 할당/자식 측정/GC는양쪽0이다. 변경후1회중앙값1.7µs,p95/p99중앙값0.3→0.4µs를 버리지 않았다. 시간·Private·작업집합 범위가 겹치며 원인이 확인되지 않아 속도/메모리 개선을 확정하지 않는다. 새 Row/Column·Box·Stack 재측정 생략은 횟수·기능 검증이며 새 계열의 전후 시간 비교로 확대하지 않는다.

Container1만 개 소유권 생성 할당은6,000,000→6,080,000B(+80,000B,+1.33%)로 참조 필드 한 개 비용이 증가했다. 첫Build880,000B·변경 없는Build0B는 유지했다. 인스턴스가 측정한 뒤 선택적 유지 결과를 확보하는 비용은 이 생성 구간에 포함되지 않는다. 기존 전체앱/2048텍스트 Private 문제를 이 자료로 해결됐다고 하지 않는다. 중간 비용 증가를 숨기거나 올바른 공통 경로를 폐기하지 않고 후속 메모리·핫패스 개선 대상으로 남긴다.

근거 artifacts/container-retention-2026-11-06/verification.json,comparison.json,image-comparison.json,원본 L:/Cache/Duxel/container-retention-2026-11-06/. 최종 검사 후 소스 변경0개. NET11만 검증하고 NET9·10최종배포컴파일·리사이즈 별도보류·이전삭제거부22개보존·전체목표active를 유지한다. 전체4694·실제고부하12회 비교는 불필요하게 반복하지 않았다. 이번 최종 기능 검사는 실패 없음이다.

## 공통 무효화 전파와 Wrap/Grid 측정 유지

가설: 같은 제약에서 자식을 매번 측정하고 부모 측정 직후 자식 렌더링에서 강제 크기로 다시 측정하는 중복이 있다. UiWidget의 새 저장 필드 없이 기존 소유권을 따라 RequestRebuild·관찰 상태·채택/제거·해제를 전달했다. UiArrangedChildrenWidget은 명시적으로 순수 배치인 경우 기존 자식 영역을 유지하고,측정 결과와 같은 렌더 할당에는 그 배치를 사용한다. 변경 없는 형제는 측정을 유지하고 그리기·입력·콜백은 계속 실행한다. 기본 사용자 위젯/표현은 비캐시이며 Fluent 버튼은 소스 생성기가 연결한 표현의 정적 정책으로 참여한다. 다른 의존성을 추가한 파생 클래스는 무효화 계약을 지키거나 명시적으로 비캐시를 선택해야 한다.

조건 키는 제약·문맥/자원 정체성·백엔드 세대·폰트/줄/배율 설정을 포함하며 글리프 관찰자가 있으면 재사용하지 않는다. 토큰에 렌더 문맥·백엔드·아틀라스 역참조를 넣지 않았다. 배치당 선택적 캐시 한 개·기존 영역 배열을 사용하며 측정 중 무효화·실패 시 결과를 등록하지 않는다. Row/Column/Container/Stack과 전체 증분 배치는 아직 남아 있다.

관리/고정RC3 Native 각각159검사·MSAA1/4·3슬롯 GPU109이미지 동치. 공통 UiWidget 변경을 이유로 기존 트리72·소유권17·버튼 수명12 집중 검사도.NET11관리로 실행해101개 통과했다. 최신 Native 샘플은 첫 실제 표시+15초 정상 종료0이며 수동 클릭·시각/리사이즈 승인은 아니다. 소스 변경0개를 재확인했다. 근거 artifacts/layout-retention-2026-11-06/verification.json,원본 L:/Cache/Duxel/layout-retention-2026-11-06/.

고정SDK/runtime11.0.100/11.0.0-rc.3.26522.148,win-x64 Release/Speed NativeAOT로 전후3회씩 ABBAAB 비교했다. 무텍스트 Fluent 버튼 Wrap1만+Grid1만·요청1x1·제약100x100·준비96회·1000회의 두 MeasureConstrained 호출만 시간/할당 구간이다. NewFrame·Render호출부·GetDrawData·해제는 구간 밖이고 실제 그리기/GPU/입력을 측정하지 않는다. 기준 자식 측정2천만 회→0회,실행별 중앙값의 중앙값1827.4→0.2µs(구간99.99% 감소),p952581.6→0.2µs,p992907.5→0.2µs다. 개선값은 타이머 해상도 근처이므로 정확한 배수로 해석하지 않는다. 할당은 양쪽0B,GC각세대0,강제GC없음이다. 종료Private중앙값18,620,416→18,608,128B·작업집합24,604,672→24,621,056B의 실행 범위는 겹쳐 메모리 개선을 확정하지 않는다. 전체 앱 FPS/draw/업로드·가시 버튼 부하·Pi 성능으로 확대하지 않는다.

W:공간 부족을 사전 검사로 감지해 첫 Native 빌드는 실행하지 않았다. SDK 공식 UseArtifactsOutput/ArtifactsPath로 중간 파일과 프로젝트별 출력까지 L:에 분리하고 소스 글롭에 기존 obj/bin이 들어가지 않음을 확인한 뒤 양쪽에 같은 설정을 적용했다. 기존 파일 삭제/이동·임시 환경변수 우회는 없다. 집계 명령의 Where-Object 매개변수 공백 오류로 나온 null 결과는 comparison.invalid.json에 보존하고 원본6회 기록에서 실행3회씩을 강제 확인해 재집계했으며 측정을 다시 실행하지 않았다.

컨테이너2/6과 전체 목표 active를 유지한다. 실제 고부하12회·전체4694개는 불필요하게 반복하지 않았고 이전 앱 Private/FPS 개선 미확정·2048텍스트 장기 메모리·리사이즈 보류·Fluent 전체/이식성 미완료를 유지한다. 개발NET11만/NET9·10최종배포컴파일 기준도 같다.

## Wrap·Grid 공통 배치와 묶음 단계2/6

판단: 소유권·측정·그리기/입력 클립을 UiArrangedChildrenWidget으로 재사용하고,Wrap의 줄 형성과 균일 Grid의 셀 배치를 별도 책임으로 구현했다. 생성기는 실제 UiWrapWidget/UiUniformGridWidget의 닫힌 축 타입을 선택한다. 기존 Dux.Grid 오버로드를 보존하고 Dux.UniformGrid를 새 소유 트리 진입점으로 사용한다. 줄별 간격·정렬·교차 정렬,고정 개수·간격·셀 비율/명시 크기,동적 옵션의 자식 보존을 지원한다. 유한하지 않은 영역·무한 교차축·모순된 옵션·사용자 배치의 부모 제약 위반은 명확히 실패한다.

최종 관리/고정RC3 Native 각각136검사,MSAA1/4·3슬롯 GPU93이미지 전부 동치. 자연/강제 제약·양 축·독립 사각형·동적 옵션·여러 획득 이미지·실제 Fluent 버튼 클릭·배타적 소유권·부모 데이터 경계·해제를 확인했다. 준비2회 후 Wrap1만+Grid1만 자식 순수 측정 합계0B이며 전체 프레임·렌더·처리량·Private 비교 결과가 아니다. 배열을 자식 수에 맞춰 유지하지만 매 측정 시 자식 순회는 남아 있으며 증분 배치를 구현했다고 주장하지 않는다. 일반 말단/확장 계약과 스크롤·가상화·RTL·완전한 입력/접근성 결합도 별도 남은 범위다.

새 layout_flow_containers_fba.cs는 Columns2/3 변경과 여섯 셀 카운터를 제공한다. 고정RC3 Native 첫 실제 표시+15초 정상 종료를 확인했으나 데스크톱 캡처·사용자 수동 클릭/시각 승인·리사이즈 검증은 아니다. 최소480x560은 스크롤 통합 전 샘플 제약으로 엔진 성능 개선에 포함하지 않는다. 근거 artifacts/flow-containers-2026-11-06/verification.json,원본 L:/Cache/Duxel/flow-containers-2026-11-06/. 종료한 자체 샘플 PDB6개만 NTFS 무손실 압축해 내용 해시를 보존했고 삭제/이동은 하지 않았다. 보류한 과거 삭제 대상도 그대로다.

Stack/Positioned/Wrap/Grid 묶음 단계를 완료해 컨테이너2/6을 기록했다. 기존4694전체 회귀·실제 앱12회 비교는 관련 없는 반복으로 실행하지 않았다. 전체 목표 active,리사이즈 별도 보류,앱 성능·Private 개선 미확정,2048텍스트 장기 메모리 미해결,.NET11만 개발 검증/.NET9·10최종배포컴파일 기준을 유지한다. 이번 최종검사 실패 없음이며 중간135검사 이후 사용자 배치 제약 검사를 추가해136검사로 최종 측정했다.

## Stack·Positioned 공통 배치

판단: 가장자리 배치마다 Build 탐색·자식 소유권·측정 보호 코드를 복제하지 않고 기존 flex 경로를 공통 구성 루트 조회와 UiMultiChildWidget으로 통합했다. 생성 UiStack/UiPositioned가 실제 기반 구현을 선택하며 제약·옵션은 런타임 데이터다. 일반 자식으로 크기를 정한 뒤 위치 지정 자식을 측정하고 삽입 순서로 그린다. 표준 포인터 입력은 최상단 할당 사각형과 Stack 클립을 따른다. 물리 배치 경계에서 다른 종류의 부모 데이터를 명확히 거부한다.

관리/고정RC3 Native 각각95검사,MSAA1/4·3슬롯 GPU53이미지 전부 동치. 기존67검사 범위를 포함하고 추가 좌표·제약·해제·실패 복원·동적 옵션·클릭·누름 즉시 동작·넘침 입력·1만 자식 측정 계약을 확인했다.1만 작은 위치 지정 자식은 준비 후 순수 측정0B이며 비교 성능·전체 프레임/메모리 결과가 아니다. CPU용 중첩 버튼 검사에서 글꼴 백엔드 없는 fixture에 비어 있지 않은 레이블을 넣어 첫 실행이 실패했다. 포인터 동작을 그대로 두고 레이블 없는 버튼으로 수정했으며 원본 실패 로그를 보존했다. 테스트 기대값을 완화하지 않았다.

근거는 `artifacts/stack-containers-2026-11-06/verification.json`과 원본 `L:/Cache/Duxel/stack-containers-2026-11-06/`이다. 기존 전체 그래픽·고부하12회 비교는 반복하지 않았고 앱 Private/FPS 개선 미확정 상태도 유지한다. Wrap/Grid를 포함한 단계는 아직 완료가 아니므로 컨테이너1/6을 유지한다. 투명 픽셀 판정·포인터 통과·표준 입력을 우회하는 사용자 컨트롤·키보드/접근성 전체 결합·증분 배치·전체 말단 크기 반영은 별도 남은 범위다. 리사이즈 보류와 .NET11만 개발 검증하는 기준은 그대로다.

## 화면 밖 애니메이션·중첩 캐시 합성

가설: 정적 캡처의 넓은 클립 때문에 화면 밖 Spinner/Progress가 계속 앱을 깨운다. .NET11 집중 검사로 실제 실패를 재현했다. 공통 RequestAnimation(bounds)는 실제2D 레이어 이동·클립·완전 투명 조상을 검사하되 애니메이션 의존성은 보존한다. GPU 비교에서 별도로 발견한 자식 이동 덮어쓰기도 수정하여 부모/자식 이동을 합산하고 클립을 명령 로컬 좌표로 변환했다. 화면 복귀·투명도 복원 시 같은 위젯이 다시 애니메이션을 진행한다.

개발 검증은 사용자 변경 기준에 따라 .NET11만 실행했다. 고정 NativeAOT 집중 EXT2/core3 각각51개·MSAA1/4 이미지29개씩 관리/Native 일치, 실제 앱20개·전체 그래픽4690개 통과다. 마지막 집중 검사에서 빈 화면끼리 같은 것으로 잘못 확신하지 않도록 실제 일부 링 픽셀이 보이는 클립 참조를 확인했다. 전체 그래픽·방식 비교 이후 제품 소스는 그대로이고 추가 변경은 집중 실행기의 MSAA1/4 명시 실행과 클립 검증 보강뿐이므로 원본 실행 파일과 기존 전체/비교 결과를 재사용했다. 원본 실패·중간 단계·두 차례 집중 보강 로그를 보존했다. 새 .NET9·10 실행/솔루션 다중 TFM 검사는 하지 않았다.

고정 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 win-x64 Release/Speed NativeAOT, 같은 화면 밖 Spinner·논리640x480/실제800x600·MSAA4·VSync 끔·슬롯2·파이프라인·이벤트 대기·강제 GC 없음이다.300ms 이후 제출 상태를 갱신하고100ms 더 기다린 뒤1초씩 독립3회 교대 측정했다. 같은 실행 파일에서 이전 인수 없는 연속 깨우기만 재현했다. 앱 프레임 중앙값3701→0·할당9,223,472B→0(각100% 감소), 프로세스 CPU593.75ms→타이머 해상도에서0ms다. Private 마지막 중앙값154,398,720B→142,352,384B(-7.8021%), 작업 집합124,248,064B→111,054,848B(-10.6184%)이며 범위는 겹치지 않았다. 마지막 GPU draw·업로드 표본은 양쪽 모두0이다. 프레임을 만들지 않는 대기 실험이므로 FPS·raw 프레임 분포 개선 주장은 하지 않는다. 표본의 CPU·기하 처리 생략과 GPU 제출 상태를 구분한다.

리스크/후속: 다른 작업이 프레임을 만들면 숨긴 애니메이션 레이어도 다시 구성될 수 있다. 투영 장면의 화면 밖 생략, 최초 기하 캡처, 수동 캐시의 입력/UIA 의미 정보 보존은 남아 있다. 표시 FPS·전체 앱·장시간 누수·생성기·Pi로 결과를 확대하지 않는다. 근거 `artifacts/animation-visibility-2026-11-06/verification.json`·`comparison.json`·`image-comparison-qualified-final.json`, 설명은 [공통 예약·애니메이션](../../frame-scheduling.ko.md)에 있다.

English: bounded visual wakes now respect nested2D clips/translations and transparent ancestors; retained replay adds child/parent offsets instead of replacing them. Fixed.NET11 Native focused51 per presentation mode and29 exact images per mode, application20 and broad graphics4690 pass. Three alternating hidden-phase samples reduce median frames3701/allocation9,223,472B to0 while last GPU draw/upload snapshots already remained0 in both cases. Final Private/working-set medians fall7.8021%/10.6184% in this short phase. Visible throughput, whole-app, leak, generator and Pi results are not established. Hidden layers may still rebuild on unrelated frames. See [scheduled frames](../../frame-scheduling.md) and the linked evidence.

## 공통 예약 프레임·캐시 의존성

표시 지연 중 연속 갱신 대신 가장 이른 UI 시각을 기존 이벤트 대기에 연결했다. 타이머 객체 없이 외부 요청·입력·종료와 커서 깜빡임을 유지한다. 캐시 재생에서 예약이 사라지는 집중 실패를 재현하고 레이어별 예약·애니메이션 의존성을 보존하도록 수정했다. 관계없는 정적 레이어는 재사용한다. .NET10 전체3140·집중16·실제 앱14, 고정 RC3 NativeAOT 집중16·실제 앱14 통과, net8/9/10 빌드 경고0/오류0이다.

동일 실행 파일에서 이전 연속 깨우기만 재현한 비교다. 고정 Native 독립3회씩 교대, 표시 대기1초·실제800x600·MSAA4·VSync 끔에서 연속10,622~10,926프레임·할당5,869,600~6,032,808B 대 예약0프레임·관측 할당0B였다. 프로세스 CPU859.375~968.75ms 대 타이머 해상도에서0ms, 마지막 Private147,390,464~149,946,368B 대140,902,400~141,201,408B였다. 강제 GC·부하/품질 감소는 없다. 전체 앱·표시 모션·장시간 누수·생성기·Pi 성능 주장으로 확대하지 않는다. 자세한 범위·작업 집합·원본 결과는 `artifacts/frame-deadlines-2026-11-06/comparison.json`과 [예약 프레임](../../frame-scheduling.ko.md)을 참고한다.

English: common UI deadlines now survive retained-layer replay and share the existing interruptible event wait. NET10 full3140/focused16/application14 and fixed RC3 Native focused16/application14 pass. Three alternating native samples per mechanism show zero pending frames/observed allocation instead of10,622–10,926 frames and5,869,600–6,032,808 bytes per second. This short pending-work result does not establish visible-motion, whole-application, source-generator, leak or Pi performance. See [scheduled frames](../../frame-scheduling.md).

## Spinner·공통 원환 기하

공식 Spinner Preview를 기존 상속·정적 동작/표현·생성 팩터리 경로에 연결했다. 샘플 전용 그림이나 다른 컨트롤의 이름을 바꾸는 방식이 아니다. 공통 AddAnnularSector는1픽셀 반경 방향 커버리지와0.01 논리 픽셀 이하 현 오차를 유지하고 큰 메시를 분할한다. CPU 구성/업로드는 계속 필요하며 지연 중에도 공통 연속 프레임 요청을 사용한다. 앱 CPU/Private/FPS/Pi 개선율이나 소스 생성기 자체의 새 성능 개선은 주장하지 않는다. 이후 공통 엔진 측정·시각 지정 깨우기와 기하 비용 개선이 남아 있다.

순수 측정·수명·지연·모션·기본/사용자 표현과 UIA를 검증했다. 초기 EasyEase 끝점·항목 영역 메타데이터 오류를 수정했고 측정/렌더 비교는 같은 제약으로 고쳤다. 최종 Parent 식별자 회귀를 추가한 뒤 고정 빌드로 다시 검증했다. .NET10 전체3140·GPU1312, 고정 .NET11 RC3 NativeAOT EXT2/core3 각각1312, 실제 갤러리 UIA33·정상 종료0이다. Spinner 이미지1137개×2가 실행기 사이에서 같고 Core 생성71→72 중70개 동일·카탈로그1개 변경·Spinner1개 추가다. 기존 생성기 구현/검사115는 범위대로 재사용했다. 공식 브라우저 룩앤필 동치는 미완료다. 근거 `artifacts/fluent-spinner-2026-11-06/verification.json`.

English: Spinner Preview now consumes the common inheritable/static generated path and a general annular-sector primitive. Precision and radial coverage are preserved; animated CPU/upload work and continuous delay wakes remain. Final NET10 full3140/GPU1312, fixed RC3 NativeAOT1312 per present mode and external UIA33 pass. All1137 images agree per mode. This establishes functionality/path/runtime equivalence, not official browser fidelity or application/Pi speed and memory improvement.

## UIA 공급자 제거·용량 회수

가설: 제거 후 미래 UIA 조회에 의존하던 약한 공급자 맵 키/급증 용량을 제거 시점에 회수하고, native 분리를 렌더 생산자 밖으로 옮길 수 있다. 제거 발행 후 알림·즉시 맵 정리·최대32개 Windows 메시지 처리로 연결했다. 기능 집중 검사에서 실제 공급자4096→1개, 맵 용량256이하를 강제 GC 없이 확인했다. 이는 결정론적인 저장소 수명 검증이며 전체 Private 메모리 개선율이나 실행 시간 개선율이 아니다.

독자4개/발행1000회·Dispose, 관리/Native 독립 호스트3/클라이언트4, 외부 Field14를 검증했다. 최종 .NET10 전체3024·집중40, 고정 .NET11 RC3 NativeAOT 집중40·Field14 통과. native 제거 회수는 S_OK0/이미 사용 불가1/대기0으로 기록했다. 장시간 Native/프로세스 자원 회수나 앱 CPU/FPS/Pi 향상을 주장하지 않는다. 근거 `artifacts/uia-lifetime-2026-11-06/verification.json`.

English: removal publication now prunes provider keys and burst capacity without waiting for another client query. Registered providers retire outside render/cache/publication locks through bounded queued Windows messages. The4,096→1 provider/map-capacity check uses no forced GC. Managed/Native concurrent and separate-process tests pass, including14 external Field properties/interactions. Recorded native retirement is one already-unavailable result, not S_OK. No process-memory, application-time or Pi performance improvement is claimed.

## 공통 UIA 기반과 할당 회귀 보존

가설: 기존 위젯·동작·소스 생성 경로를 유지하며 접근성 공급자의 읽기와 실제 입력 처리를 분리할 수 있다. UiAccessibilityTree가 완성된 논리 프레임만 발행하고 Windows 공급자는 소스 생성 COM으로 조회한다. 변경 없는 저장소는 재사용하고 제거 후 큰 용량은 회수한다. 외부 요청은 기존 프레임 깨우기와 입력·바인딩·콜백 경로를 소비한다. 새로운 런타임 테마 레지스트리를 추가하지 않았다.

초기 AccessibleName 기본 필드가 기존 Parent 팩터리 할당 기준을 깨뜨렸다. 이름을 지연 정체성 캐시에 보관하고 의미 정보 범위를 분리해 객체 크기·ID를 보존했다. 수정 전 실패와 수정 후 기존25% 감소 검사 통과를 원본 로그에 남겼다. 범위 정체성 캐시의 참조1개와 실제 UIA 트리/공급자 비용은 추가된다. 준비 후1만 노드의 반복 발행 할당0B는 기능 집중 검사다. 앱 CPU/Private/FPS나 Pi 개선의3회 교대 측정으로 해석하지 않는다.

검증: .NET10 전체3024·집중34, 고정 .NET11 RC3 NativeAOT 집중34와 외부 UIA 클라이언트4, 실제 Native 갤러리 정상 종료0. 초기 종료 시 공급자 등록/해제 순서 오류도 수정했다. 최종 net8/9/10 경고0/오류0. 근거 `artifacts/uia-foundation-2026-11-06/verification.json`과 [접근성 문서](../../widget-accessibility.ko.md). 자식 공급자 회수·동시성·다른 역할/패턴/이벤트·전체 비용과 공식 동치는 미완료다.

English: the common semantic tree and source-generated native COM provider now serve actual Button/Input controls. External requests reach existing bindings/callbacks on the render producer. The initial base-field allocation and disconnect-order regressions were reproduced and corrected. Final NET10 full3024/focused34, fixed RC3 NativeAOT focused34 and external UIA4 pass. This adds semantic/provider storage and makes no application or Pi speed/memory claim. Child-provider reclamation, broader concurrency, remaining roles/events and full Fluent fidelity remain open.

## 현재 Fluent 폰트 계약의 DirectText 재검증 (2026-11-06)

가설: 기존 공통 엔진의 레이아웃 기능이 현재 Semibold14/행높이20에서도 반복 래스터화·할당을 줄이는지 확인한다. 기존 측정기는 새 폰트 메트릭 인터페이스와 UiTypographyFonts 설정이 없어 첫 프레임에서 실패했다. 양쪽 어댑터에 같은 폰트 계약을 연결하고 실제 폰트 프로필과 메트릭 호출 수를 결과에 기록하도록 기존 측정기만 수정했다. 런타임 엔진을 새로 최적화한 작업은 아니다.

.NET10 SDK10.0.301/runtime10.0.9로 실패 재현 후 입력 검증 통과. 수치 비교는 고정 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 win-x64 NativeAOT 한 실행 파일에서 eager/lazy, lazy/eager, eager/lazy 독립6프로세스다. 10000개 유지 버튼·1280×720·요청 크기110×28·idle 입력·같은 실제 Semibold14/행높이20을 유지한다. CPU 구성만 측정하며 VSync/MSAA/GPU 프레임은 해당하지 않는다. 워밍업 최소0.5초·측정 최소1초/12표본, 강제 GC 없음. eager는 각13표본, lazy는502/499/498표본이다. 아래는 독립3회 결과의 중앙값이며 범위는 각 실행 결과 사이의 범위다.

| 항목 | 래스터 우선(eager) | 레이아웃 기능(lazy) | 변화율 | 실행 범위 겹침 |
| --- | ---: | ---: | ---: | --- |
| CPU 구성 중앙값(ms) | 78.984200 | 1.995200 | -97.4739% | 없음 |
| CPU 구성 p95(ms) | 100.521940 | 2.060320 | -97.9504% | 없음 |
| CPU 구성 p99(ms) | 108.633964 | 2.089660 | -98.0764% | 없음 |
| 현재 스레드 할당(B/회) | 88927336.000000 | 664.000000 | -99.9993% | 없음 |
| 프로세스 Private(B) | 77451264.000000 | 36249600.000000 | -53.1969% | 없음 |
| 작업 집합(B) | 87511040.000000 | 45772800.000000 | -47.6948% | 없음 |
| 하위1% CPU 구성 반복률(회/s) | 9.113959 | 471.689213 | +5075.4590% | 없음 |
| 평균 CPU 구성 반복률(회/s) | 12.136892 | 498.728692 | +4009.1960% | 없음 |

lazy는 측정 중 레이아웃/래스터/폰트 메트릭 호출0회다. eager는 실행마다 래스터123344회, 프레임당9488회였다. 두 군의 DrawData 명령은462개로 같지만 이 숫자를 GPU draw calls로 보지 않는다. 이 조건에서는 반복 래스터 작업이 큰 CPU·할당 비용을 만들었다. 기본 WindowsPlatformTextBackend는 이미 레이아웃 기능을 제공하므로 이 차이를 정상 Windows 앱의 새 개선율로 보고하지 않는다. 기존 최적화를 현재 폰트 계약에서 재검증한 결과다.

리스크/한계:1초 구간의 Private/작업 집합 종점이며 장시간 증가·피크·관리 힙 크기를 증명하지 않는다. eager13표본의 꼬리 통계는 짧은 집중 검사다. 평균/하위 반복률은 CPU 구성 처리율이며 실제 앱 FPS가 아니다. GPU 업로드/draw 병목, Pi FPS와 기존 폰트 조건의 과거 수치 대비 개선은 미검증이다. 품질·부하를 낮추어 두 비교군을 맞추지 않았다. 다음은 실제 GPU·앱 고부하 경로의 남은 비용을 분리한다.

근거: `artifacts/directtext-current-2026-11-06/measurement-plan.json`, `summary.json`, `run-*.json`과 원시 CSV·분리 stdout/stderr.소스·설정·자산 입력161개·폰트3개·실행 파일 해시가6회 내내 같음을 확인했다. 원래 실패도 보존했다. 처음 계획 작성 시 없는 Directory.Build.props를 읽으려다 실패해, 다음 계획에는 파일이 없다는 사실을 기록한 뒤 측정했다. .NET10 입력 검증 숫자는 Native 성능 비교에 합치지 않았다. `dotnet build Duxel.slnx -c Release` 경고0·오류0.


## 공통 그래픽 큐 깊이 버퍼 공유

가설: GPU 고부하 Private는 draw 수뿐 아니라 중복 깊이 할당의 영향을 받는다. 기존 도구로 5,000입자/MSAA4에서 실제 GPU 55 draw·5,992B 업로드·깊이3개 162,201,600B를 확인했다. 단계57표본의 imageFence 중앙값366.8μs, record31.9μs였다. GPU 시간에는 compute도 포함된다. 깊이는 매 rendering에서 CLEAR하고 표시하지 않아 큐 공유가 가능하다.

변경: 공통 DepthLayers/CommandRecording에서 깊이1개를 공유하고 EARLY/LATE 읽기·쓰기 배리어와 기존 마지막 제출 기반 retirement를 유지했다. 품질·부하·깊이 비교와 샘플 그리기 코드는 유지했다. 측정기에 크기 요청·자원 CSV·출력 보존을 추가하고 숨긴 창의 핸들을 필수로 보던 잘못된 조건을 수정했다.

검증: SDK10.0.301/runtime10.0.9 전체 렌더링1,862개·관리GPU1,067개. SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 win-x64 NativeAOT Speed, EXT2/core3슬롯 각각1,067개. `dotnet build Duxel.slnx -c Release` 경고0·오류0. 기존 측정기 AB/BA/AB 각3회 독립 실행,6초/warmup1초,5,000입자·크기6·충돌2·VSync끔·MSAA4·실제2400×1350. 비교 중 실행 파일 해시를 유지했다.

Before/After: 깊이162,201,600→54,067,200B(-66.67%), Private434,470,912→326,250,496B(-24.91%)는 실행 범위가 분리됐다. WS131,657,728→131,538,944B(-0.09%), FPS2,084.26→2,081.14(-0.15%), median/p95/p99 0.46740/0.57898/0.78469→0.46690/0.58907/0.81540ms, low1 1,130.41→1,094.84, GPU362.00→364.43μs. 시간/WS는 범위가 겹쳐 개선으로 확정하지 않는다. 꼬리 중앙값 악화도 보존한다. Private는 프로세스3~6초 표본 중앙값이며 UI 워밍업과 정확히 동기화하지 않았다. 강제 GC는 없다.

초기 핸들 판정 실패·과거320×240 대비 현재400×300 비교·새 검사 선행에 따른14개 이미지 차이를 보존했다. 숨긴 창 조건 수정·같은 DPI 기준 재사용·새 검사를 기존 스냅샷 뒤로 분리한 후 이전1,109×2/관리1,117×2이미지가 정확히 같았다. 허용 오차를 완화하지 않았다.

리스크: 공유 깊이는 프레임 간 병렬성을 줄일 수 있다. 다른 GPU/Pi/장기 메모리는 미검증이다. 동기화 validation layer는 활성화하지 않았다. 집중 검증을 전체 Fluent 완료로 올리지 않는다. 명령·원시 표본·해시·범위는 artifacts/gpu-current-2026-11-06/ab-plan.json, summary.json, verification.json과 [설명](../../shared-depth-target.ko.md)을 따른다.


## Checkbox 정적 표현과 정확한 크기 아이콘

가설: 이미 연결된 Checkbox의 초기 표현을 공식 지침에 맞추되 동작과 수명은 공통으로 유지하고, 크기/형태/배치를 컴파일 시점에 확정할 수 있다. 기존 토큰/동작 원본과 공식 아이콘 폰트를 재사용했다.

변경: Regular14/20 텍스트 스코프, medium/large·square/circular·before/after의8개 닫힌 표현 조합, 혼합/선택/비활성/hover/pressed 팔레트, 정확한12/16 checkmark/square/circle 글리프6개를 연결했다. 고정 크기 생성 출력에는 크기 분기나 런타임 JSON이 없다. 기존2인수 특성 생성자와 기존 사용자 표현의 빈 텍스트 스코프를 보존한다. 레거시 즉시 Checkbox도 보존한다.

Before/After 구조: 기존 Core 생성57개 →63개, 기존57개는 바이트 동일하며 새6개만 추가했다. 기존16px 지표·수제 체크 선/혼합 막대·주변 글꼴에서 공식16/20px 지표·12/16px 글리프·명시적Regular14/20으로 바뀐다. 신규 FPS/Private/GC/Pi 개선율은 측정하지 않았으므로 주장하지 않는다. Native 검사도 기능/픽셀 검사이며 성능 교대 측정으로 세지 않는다.

검증: SDK10.0.301/runtime10.0.9 전체2046·실제GPU1251·입력/토큰48·생성기111. 고정SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 win-x64 NativeAOT EXT2슬롯/core3슬롯 각1251. 기존1117×2·관리/Native1261×2 이미지 동치, 새144×2 포함. 라벨 없는 마크 제거 음성 대조8개를 실제 GPU로 확인했다. net8/9/10 솔루션 경고0·오류0, FBA 관리/Native 소비자 컴파일과 --checkbox 첫 제출/3초 자동 종료0을 확인했다.

초기 테스트 알파 속성명/명시적 폰트 누락을 수정했다. fixture가 grey30/70을 산술 반올림한 두 오류는 원본 colors.ts를 읽어24개 상태 행 전체를 다시 확인하고 바로잡았다. 생산 팔레트를 잘못된 fixture에 맞추지 않았다. 초기 로그를 보존하고 --checkbox-only 집중 검사를 긴 실행 전에 사용했다. 기존 strict 제거 캐시 기준은 여전히 false다.

리스크/후속: 공식 브라우저 픽셀 일치, UIA/논리적 RTL/강제 색상, 긴 라벨/Field, 공식 focus/motion과 전체 컴포넌트 지원은 미완료다. Checkbox는 원래 연결되어 있었으므로 카탈로그 상위 지원 수를 늘리지 않았다. 정확한 명령/해시/결과는 artifacts/fluent-checkbox-2026-11-06/verification.json과 [설명](../../fluent2-checkbox.ko.md)을 따른다.


## RadioGroup와 보존된 여러 줄 텍스트

가설: 미연결 RadioGroup을 기존 개별 라디오의 이름만 바꾸지 않고 공통 위젯/입력/생성 체계로 연결할 수 있다. 그룹 입력은1개 Tab 정지점, 화살표·Space·배타 선택·콜백 순서를 처리하고 표현은 세 배치를 제공한다. 기존 개별 RadioButton과 다른 기능을 가진 공통 구현이다.

변경: 생성된 UiFluentRadioGroup, Dux/제네릭 직접 경로, 복사된 항목 스냅샷·38개 입력/캐시 검사를 추가했다. 같은 폭/폰트/컨텍스트의 레이아웃과 줄 문자열을 유지하며 추가 문자열4096자 상한·약한 컨텍스트 참조·대형 이벤트 목록 회수·실패한 사용자 측정의 임시 행 정리를 구현했다. 수직/수평/아래 라벨은 닫힌 구현 타입으로 선택한다.

실제 GPU readback에서 첫 동치 검사가 놓친 마지막 줄 잘림을 발견했다. 단일 줄 중앙 배치 함수에 여러 줄을 전달한 사용 오류였다. UiControlTextBlock을 추가해 줄 문자열/폭을 측정 시 보존하고 각 행의 정확한 기준선을 그린다. 첫 MSAA4 중앙 비교에서는 글리프 advance 폭으로 자른 가장자리 차이도 발견해 실제 잉크에 호출자 clip을 적용했다. 기존 단일 줄 함수의 배치/출력은 유지했다. 실패 이미지/로그를 보존하고 독립20px 행 기준·첫/마지막 줄의 실제 픽셀32개 검사를 추가했다.

검증: 관리 SDK10.0.301/runtime10.0.9 전체2308, 현재 GPU1513, 입력38, 생성기115. 고정SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 win-x64 NativeAOT EXT2/core3슬롯 각1513. 기존1261이미지×2·관리/Native1469이미지×2 동치, 새208이미지 포함. 최종 block 가시성 보정 뒤에는 변경 함수를 포함한 관리/Native GPU 검사를 재실행하고 무관한 전체 깊이/레이어 근거는 재사용했다. net8/9/10 솔루션 경고0·오류0. FBA 관리 컴파일·Native 첫 제출/3초 자동 종료0.

Before/After: 기본 카탈로그27→28이름, 상위 연결8→9, 미연결47→46. Core 생성63→64파일이며 카탈로그 수정과 새 RadioGroup 어댑터 외62파일은 바이트 동일하다. 신규 기능/구조의 수치이며 FPS/Private/GC/Pi 개선율이 아니다. 같은 조건 성능 비교를 수행하지 않아 속도 향상을 주장하지 않는다.

리스크/후속: 공식 브라우저/UIA/RTL/강제 색상/Field-required/toolbar 입력/모션은 미완료다. 아주 긴 단일 단어는 공통 최소 내용 폭을 사용할 수 있다. 현재 전체 Fluent 목표나 공통 엔진 최적화 완료로 올리지 않는다. 근거는 artifacts/fluent-radio-2026-11-06/verification.json, plan.json, image-comparison-final.json, visual-defect.json과 [설명](../../fluent2-radio-group.ko.md)이다.

## Switch 정적 크기·구성 보완

가설: 기존 Switch의 기본 연결을 보존하면서 공통 입력/수명과 분리된 표현 타입으로 공식 크기·라벨 구성·상태별 그리기를 제공할 수 있다. medium/small과 before/after/above를 닫힌 구현으로 선택한다. 새 독립 상위 컴포넌트 수를 늘리지 않는다.

변경: 공통 UiToggle 경로의 새 Switch 표현과 공식 circle20 손잡이를 생성했다. checked 투명 테두리·일관된 라벨 색·disabled 선택 배경을 분리하고 기존 브랜드/중립 토큰과 폰트 자산을 재사용한다. bool 바인딩·Space 해제·상속 콜백·해제 경로는 유지한다.

검증: 관리 .NET10 전체2652/입력56/GPU1857/생성기115, 고정 RC3 NativeAOT EXT2/core3 각각1857. 기존1469이미지×2와 관리/Native1661이미지×2 정확히 동일. 두 크기·세 배치·두 값·다섯 상태·두 색·MSAA1/4·raster/vector 신규192이미지씩. 실제 readback의 오른쪽 시험 항목 잘림을 발견해 두 열로 수정하고 모든 상태의 경계/잉크를 검사한다. 초기 샘플 잘못된 제목 API를 수정했다. 실패 기록은 보존한다.

Before/After: 기존 Core생성64개 바이트 동일, 새 고정 아이콘 어댑터1개. 카탈로그28개 이름·상위9/55·미연결46 유지. 기능/구조 개선이며 실행 시간·Private·할당 개선율은 측정하지 않았다. 공식 브라우저/UIA/RTL/강제 색상·모션/줄바꿈·지표 영역 hover 및 전체 엔진/Pi 검증은 남는다. 근거 artifacts/fluent-switch-2026-11-06/verification.json.

## Link 및 공통 텍스트 보유 상한

가설: 미연결 탐색 Link를 공통 위젯·입력·정적 표현 체계로 연결하고, 유지한 줄 배치를 재사용할 수 있다. 목적지와 필수 앱 탐색 콜백을 분리해 렌더링 시 외부 프로세스를 실행하지 않는다. Enter 전용 키 정책·포인터 release·Tab·비활성·손 모양 커서를 같은 공통 입력에서 처리한다.

변경: 네 외형×두 밑줄 구현과 생성된 기본/사용자 상속·직접 경로를 제공한다. 실제 baseline에 따른 줄별 밑줄·이중 포커스, 줄바꿈, 약한 컨텍스트와 font/width/observer 무효화를 적용한다. RadioGroup과 공통 키를 공유했다. 추가 검토에서 문자 수만 검사하면 빈 줄 배열과 사용자 표현의 큰 대체 단일 문자열을 유지할 수 있음을 발견했다. 추가 줄 배열256칸과 실제 대체 문자열4096자 상한으로 보완했다. 큰 입력은 해당 프레임의 정상 그리기를 유지하며 캐시만 해제한다.

검증: 초기 관리 GPU2051·전체2846 이후 상한 회귀3개를 추가했다. 최종 수치·관리/고정 RC3 Native 구분·픽셀 비교·소스/실행 파일 해시는 artifacts/fluent-link-2026-11-06/verification.json을 따른다. 직접 GPU readback에서 모든 상태의 줄·밑줄·비활성·이중 포커스를 확인했고 경로 동치 외에 모든 상태 영역의 실제 잉크와 inline rest 차이/hover·disabled·focus 동치를 검사한다.

Before/After: 기본 팩터리28→29, 상위 연결9→10, 미연결46→45. 문자열 크기가0이어도 큰 빈 줄 배열이 남을 수 있던 조건을 명시적 슬롯 상한으로 바꿨다. 기능/캐시 계약 개선이며 FPS·할당·Private·Pi 개선율은 측정하지 않았다. 공식 브라우저 동치/underline 메트릭, UIA·RTL/강제 색상·선택·방문/문맥 메뉴·문단 구성 등은 남는다. 전체 엔진 미션을 완료로 세지 않는다.

## 한 줄 입력 엔진의 동작/표현 분리와 기록 수명

가설: 기존 InputText의 선택·스크롤·클립보드·실행 취소/재실행·IME를 버리지 않고 측정/배치/편집/그리기를 구분해 생성 위젯과 직접 호출이 같은 커널을 소비할 수 있다. Fluent Input 외형/기본 노출과 분리된 기반 단계이며 전체 목표를 축소하지 않는다.

변경: UiInput<TBehavior,TPresentation>, 정적 입력 정책/표현 계약과 UiTextInputOwner를 추가했다. 기존 문자열 API도 호환 구현을 통해 같은 커널을 사용한다. 편집 기록 배열의 미사용 슬롯과 반환 배열을 지우고 위젯 해제 시 상태 항목을 회수한다. IME 해제 실패에도 참조를 놓고 오류를 전달한다. 활성 필드를 비활성화하면 문자 입력을 중단한다. 편집 전 라벨 페인트 단계로 누락 글리프 관찰 콜백의 횟수·순서를 보존한다.

검증: 관리 전체2876·GPU2197, 고정 RC3 NativeAOT EXT2/core3 각2197. 기존1789이미지×2와 관리/Native1905이미지×2 정확히 동일. 테스트용 대체 표현의 테마/배경 불일치는 실제 readback에서 발견해 보정했고 라이브러리 변경 없이 현재 집중144·117이미지 동치로 재검증했다. 일반 Core148컴파일 입력·실제 DLL 메타데이터에서 동결 참조 제외 확인. 생성기115와 net8/9/10 빌드 경고0/오류0. 실제 OS IME가 아닌 대역의 조합/커밋/해제 실패를 검증했다.

Before/After: 기본 카탈로그29·상위10/55·미연결45 유지. 풀에 남던 문자열 참조와 위젯의 편집 저장소 수명을 개선했으며 undo 개수·입력 내용·화질을 줄이지 않았다. 풀/사전 용량 모두 반환 또는 Private/FPS 개선을 주장하지 않는다. 배열 지우기의 성장/해제 비용도 새 성능 측정 전에는 개선으로 세지 않는다. flags 무시 문제와 공식 Input 표현/포커스/Field/접근성·전체 엔진/Pi 미션은 남는다. 근거 artifacts/input-kernel-2026-11-06/verification.json.

## Fluent Input 연결과 공통 포커스·IME

가설/변경: 한 줄 편집 기반을 보존하면서 기본과 사용자 입력을 같은 생성 상속/동작/표현 경로에 연결한다. 3크기×4표현을 닫힌 제네릭으로 선택하고 placeholder/invalid/readonly/초기 선택과 Tab을 제공했다. 공통 빈 문자열 페인트의 글리프 조회와 비활성 내비게이션에서 IME 해제 누락을 수정했다. 조합 글자에도 본문 행 메트릭을 적용했다. 기존 enum은 None만 있었으며 새 옵션의 추가임을 교정한다.

Before/After: 팩터리29→30, 공식 상위 연결10→11, 미연결45→44. 성능/Private 개선율은 측정하지 않았다. 생성 코드67개 중 새Input어댑터1개와 카탈로그만 변경, 기존65개는 바이트 동일하다. 기능 연결 수를 전체 적합성 수로 세지 않는다.

검증: .NET10 전체2913·Input37·집중GPU229·생성기115; 솔루션 net8/9/10 경고0/오류0. 고정RC3 NativeAOT EXT2/core3 전체각2427·집중각229. 같은 크기 집중193이미지×2 일치, Native 전체2097이미지 일치. 관리 GPU 최종 전체 재실행은 기존 Badge 할당12336B 검사에서 실패했으며 미해결로 보존한다. 이전GPU2421과 현재집중229는 범위를 나눠 재사용한다. 실행 명령/해시/픽셀은 artifacts/fluent-input-2026-11-06/verification.json 및 로그, API/재현은 docs/fluent2-input.ko.md를 따른다.

리스크/후속: before/after 슬롯·모션·Field/UIA·RTL/강제 색상·실제IME·공식 브라우저 동치, 전체 컴포넌트와 엔진/Pi 검증. 갤러리 첫 제출/자동 종료는 수동 상호작용 승인이 아니다.

## Badge 반복 읽기의 측정 경계와 할당 대조

가설: 기존 대형 시나리오 안의 짧은 워밍업과 실제10000회 측정은 같은 실행 경계가 아니므로 정확한 루프를 분리하고 대조 검사로 판별력을 확인한다. 원래12336B의 정확한 원인을 확정하지 않은 상태에서 제품 코드를 바꾸지 않는다.

변경: NoInlining 측정 메서드·동일 루프 워밍업·cold 보존·캐시 매회 무효화 대조. 기존0B 통과 기준은 유지한다. Before/After는 제품 비용 개선 수치가 아니라 검사 경계와 판별력의 변화다. .NET10 독립3회/고정RC3 Native에서 정상0B·대조320000B, 각50검사 통과. 관리GPU전체2428 및 이전Native 두 경로2097이미지 동치. 제품 관련9소스 해시는 같다. 이전 Input 전체 검증과 생성기 결과는 호환 범위대로 재사용한다.

명령/해시/수치: artifacts/badge-allocation-2026-11-06/verification.json·원본로그, 재현은 docs/fluent2-badge.ko.md. 원래12336B 발생은 재현되지 않았으며 미해결이다. 새 근거 없는 전체 재진단을 늘리지 않는다. FPS/Private/전체 처리 비용 개선율은 측정하지 않았다.

## Input 콘텐츠 조합과 편집 영역

가설/변경: 소스 생성기로 선택한 앞/뒤 텍스트·아이콘 그리기를 공통 Input에 연결하고 입력/선택/IME의 영역을 분리한다. 정적4타입 표현과 기존2타입 기본 표현을 공유하며 실제 콘텐츠만 옵션 데이터로 바꾼다. 텍스트 페인트는 측정값을 재사용한다. readback에서 드러난 협소 폭 겹침과 추가 원문 검토에서 확인한 Underline 중앙 위치를 수정했다.

Before/After: 앞/뒤 콘텐츠 미제공→비대화형 텍스트/정적 아이콘 제공. Core 생성67→67 전체 동일, 사용자 소비 코드만 새 조합을 생성/실행한다. 옵션 참조/레이아웃 필드가 추가되므로 전체 메모리·속도 개선율은 측정하지 않았다. 기능/구조 개선과 성능 개선을 구분한다.

검증: 최종 NET10 전체2934·입력57·GPU3408·집중1209, 고정RC3 Native EXT2/core3 전체각3408·집중각1209. 전체2481·집중577이미지×2 동치, 기존2097/193 보존. 네 표현·세 크기·두 콘텐츠의 잉크/클립/협소 배치와 입력/해제 검사. Underline 새 수정/미검증 변형 추가 때문에 후속 빌드·검증을 수행했으며 초기 Outline 단계는 별도로 보존한다. 명령/해시/원본은 artifacts/fluent-input-slots-2026-11-06/verification.json과 로그, 재현/API는 docs/fluent2-input.ko.md.

후속: 대화형 자식 슬롯·Field/UIA·모션·공식 브라우저 동치, 전체 Fluent·엔진·Pi. 별도 슬롯 객체/런타임 타입 레지스트리를 추가하지 않았으나 미측정 비용을 개선으로 합산하지 않는다.

## Input 실제 자식 위젯과 공통 소유권/그리기

가설/변경: 고유 버튼 흉내 대신 기존 생성/상속 위젯을 소유하고 기존 Build 수명과 입력을 재사용한다. BeforeSlot/AfterSlot·SetSlots, 채택/키/공유/조상 거부와 Build 지우기 전 검사를 구현했다. 앞/편집기/뒤 입력 순서를 유지하고 부모 그리기 후 자식 목록을 옮긴다. 정점/인덱스 추가 복사 없이8단 보유 캡처와 더 깊은 임시 캡처를 사용한다. 일반 입력은 자식 경로를 초기화하지 않는다.

Before/After: 비대화형 텍스트/아이콘→실제 버튼/사용자 UiWidget 자식도 사용. Core 생성67→67 바이트 동일, 기존2481/577이미지 보존. 명시 자식 영역과 추가 소유권 컨텍스트/목록 경계가 있으므로 메모리/CPU/draw 개선율은 측정하지 않았다. 입력/수명/구조 결과를 성능 향상으로 세지 않는다.

검증: 최종 .NET10 전체2959·입력82·GPU3561·집중1362, 고정RC3 Native EXT2/core3 전체각3561·집중각1362. 전체2737·집중833이미지×2 동치, 네 표현의 부모+실제 버튼 기준과 같은 프레임 포커스 비교. 초기 전체 통과 뒤 조상/공개 레이아웃/Build 전 검사 보완은 별도 최종 소스로 검증했으며 이전 자료를 삭제하지 않았다. 명령·소스/실행 파일 해시·전체 원본은 artifacts/fluent-input-children-2026-11-06/verification.json과 로그, API/재현은 docs/fluent2-input.ko.md를 따른다.

후속: 자연 측정·Field/UIA·모션·공식 동치·전체 컴포넌트·공통 엔진/Pi. 새 갤러리의 Clear는 실제 생성 버튼이지만 자동 종료 검사는 수동 UI 승인과 구분한다.
# Field composition and UCS4 glyph support

Hypothesis: a field can own a real generated/user control and reuse measured descriptions without duplicating its editor or allocating a new child tree every frame. The common allocated-child layout/clip path now serves Input adornments and Field controls. `UiFieldContent` retains bounded measured lines with font/width/context invalidation and weak context ownership. Additional per-field cache/layout storage remains a cost; no CPU/Private/FPS improvement or percentage is claimed. Performance Before/After,independent alternating runs and timing distributions are unmeasured for this extension; earlier common-engine measurements remain scoped to their original sources.

Actual official `useField.tsx` selects DiamondDismiss12Filled at U+F0A11. Its stale types comment names ErrorCircle. The real glyph exposed a BMP-only rejection in the shared DirectWrite mapping/layout/raster path. UCS4 scalar validation replaces that restriction while glyph indices remain16-bit. Original Field failure and pre-change rasterizer are preserved under `artifacts/fluent-field-2026-11-06/`. Tests verify exact12px advance,layout/raster agreement and one vector glyph per supplementary scalar in mixed text. Official browser parity,UIA,RTL,forced colors,motion,intrinsic layout and Pi remain open.

Validation: fixed NET10 SDK10.0.301/runtime10.0.9 and fixed SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 NativeAOT are recorded separately in that directory. Full NET10 and GPU checks include actual generated Field consumers. Generated/immediate GPU equivalence is not official-reference conformance. Prior2737 full images are byte-identical;480 Field images are new. Final cross-runtime counts and executable/source hashes are recorded in verification.json after execution.

## Common widget measurement and automatic Field sizing

Hypothesis: form layout should obtain actual child size without processing input,reading bindings or rendering twice. The common Measure/MeasureContent contract now feeds default/inherited form widgets from the same static presentation measurement as rendering. Field has an additive one-argument generated constructor and AutoField immediate API; existing explicit sizing remains source-compatible. Button/Toggle/Input geometry was extracted and reused,not approximated in the sample. The gallery no longer supplies hardcoded Input heights and includes an available-width Field.

Before: Field required manual child height. After: Input24/32/40,tall adornments,RadioGroup,Slider,Toggle,Button and user controls supply desired size; Field metadata participates in measurement. This is a structural result. Timing,allocation,Private,FPS and percent improvement remain unmeasured for this extension. Extra measure passes,identity scopes and render/input guard branches are costs,not measured savings. No forced GC or quality/workload reduction is used.

Actual GPU readback exposed long-label overflow at narrow horizontal widths. The shared Fluent Field label column now clips its own ink. A no-label reference must leave the neighboring control/description pixels identical. Natural allocation is compared against heights obtained from real rendering,independently of Measure. Two widths,both orientations,light/dark,MSAA1/4 and raster/vector paths are tested. Initial results before this observed correction are preserved;qualified outputs use the corrected source. Exact counts,source/executable hashes and SDK/runtime separation are in artifacts/widget-measurement-2026-11-06/verification.json. No whole-app or Pi performance claim follows from these functional comparisons.

# Live native resize ordering and lifetime correction

The user rejected DC656134 despite its successful Native30-cycle checks for frame slots1/2/3. The screen still trailed the changing window. The earlier DCA37C18 user-accepted checkpoint used synchronous committed-WM_SIZE submission; the recent System-title experiment deferred that handshake to a queued message. Restore committed-size submission before WM_SIZE returns, preserving native caption and prior lifetime/shutdown fixes. The regression now checks submission immediately after synchronous SetWindowPos rather than accepting eventual catch-up. Managed .NET11 slots2/three cycles passes five checks; Native qualification and visual acceptance are pending at this entry.

Earlier diagnostics found shutdown Thread.Join blocked while the renderer entered WSI. Shutdown now pumps native messages after cancellation. Frame-owned acquire semaphores, monotonic frame age across ordinary swapchain recreation and nonblocking graphics/present readiness checks address independent lifetime/scheduling risks. Pointer interaction alone no longer forces unchanged continuous rendering. One shared rounded framebuffer extent removes an observed fractional-DPI clip mismatch of98 pixels. These are correctness/structure findings, not measured whole-app speedups.

DC656134 checkpoint resource snapshots showed slots2 Private growing307834880→352595968 bytes. This remains unexplained; pass snapshots are not settled-memory or long-leak proof. GPU exact-image tests and bounded resize counts do not prove photon latency or absence of visible trails. All prior artifacts and user windows remain preserved. Current evidence: artifacts/resize-stress-2026-11-06/live-order-source-manifest.json, acceptance-plan.json and L:/Cache/Duxel/resize-stress-2026-11-06/ logs. Development validation uses only pinned .NET11 RC3; .NET9/10 deployment builds remain deferred to whole-goal completion. No resolution, MSAA, vector-quality or sample-workload reduction is used.

# Common widget ownership and Build storage

Hypothesis: statically generated direct controls and ownership-only containers should not allocate dependency/rollback collections they never use. UiBuildContext now creates each collection on demand and clears storage/callback references on release. UiWidget shares one reference slot between a direct frame callback and its existing Build context; callback transfer/replacement preserves invalidation. No global cache,extra wrapper or sample workload reduction was introduced. The generated closed layout/presentation path is unchanged. The type checks added on render/invalidation are a cost; CPU timing is not claimed improved.

Fixed SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 NativeAOT Speed:10000 generated Container ownership constructions allocate7,840,000→6,000,000B(-23.4694%);10000 first user-widget Builds allocate3,360,000→880,000B(-73.8095%);subsequent unchanged traversal stays0B.96 warm frames,three independent processes per variant,AB/BA/AB,no forced GC,all per-variant counts exact. Preallocated arrays are excluded; first-Build widget objects are excluded. These are current-thread allocation probes,not app FPS/raw-frame percentiles,Private/working-set,resident GPU,draw calls or Pi results.

The initial lazy-only candidate passed container checks but exposed the existing factory25-percent allocation criterion failure(98,304vs122,880B/1024). The criterion was not relaxed. Compact shared widget storage restores it in managed.NET11(90,112vs122,880B);tree72 and ownership17 checks pass. Native callback/ownership and container GPU checks are recorded separately. Final hashes,commands,raw counts and remaining scope are in artifacts/build-context-memory-2026-11-06/. Resize investigation is deferred by explicit user request; its changes and failures remain preserved.


# Retained DirectText maintenance

Hypothesis: a retained scene above the ordinary512-entry bound should not allocate eviction scratch when every actual cache key is protected. TrimDirectTextCache previously scanned entries and allocated an approximately98KB LOH array even for zero candidates. It now reuses an exact all-keys-retained proof until membership or a layer retention revision changes. Actual eligible counts size stale/LRU scratch;one static comparer preserves layout-only-first/oldest-first ordering. StoreDirectTextEntry centralizes membership invalidation. No global key cache,pooled ownership,workload/quality reduction or generated-theme routing change was added.

Fixed SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 NativeAOT Speed,2048 actual Windows labels,font14,320x240,96 warm and1000 measured Render calls,three independent processes each,AB/BA/AB,no forced GC. Allocation per Render median98,732.768→256B(-99.7407%);timing median377→0.7us(-99.8143%),p95653.2→0.8us,p99901.2→1.2us. All run ranges are nonoverlapping. NewFrame,GetDrawData and ownership release are outside this measured phase;these are not whole-frame/FPS/GPU/process-CPU values.

Adverse result:post-phase Private median21,004,288→31,891,456B(+51.8331%),also nonoverlapping. Baseline naturally collected each generation29..30 times,candidate0. This difference may contribute,but no confirmed explanation or long-run/application-memory improvement is claimed. Working set,GPU residency,draw/upload remain unmeasured by this CPU probe. The direction is retained while broader memory qualification remains open.

Managed.NET11 probe passes cache2048/pins2048/builds1,new unretained eviction,dependency replacement,layer removal restoring the512 bound and602 idle-frame expiry;allocation98,815,480→264,000B per1000 Render calls. Final Native repeats all contracts and passes35 focused checks,including594 Windows metric combinations,600 nested GPU text textures and10000-button MSAA4 five-phase exact bytes with three frame slots. Functional weak-backend collection checks call GC explicitly outside the performance probe;the measurement itself never forces GC. No full4694-style run was repeated.

Evidence:artifacts/retained-text-maintenance-2026-11-06/verification.json,comparison.json,paired-summary.json,source-manifest-after.json and before/after source copies. Raw runs/publish logs live under L:/Cache/Duxel/retained-text-maintenance-2026-11-06/. Native before SHA25623C1F6EFD2E0257E835BC6F3C42688FD78FD461EE578C5EEADB5405C8520A727;after A47882ECA51BEDCCC84C63E1E5A8E40DBD7AD0985E8565508FF5A0E0AC4B29B4. Commands:Duxel.Graphics.Tests.exe OUTPUT retained-text-maintenance;OUTPUT directtext-cache 3. Native source hashes stayed unchanged through qualification. Resize remains separately deferred;full goal counts are not increased by this focused improvement. NET9/10 deployment compilation remains deferred to whole-goal completion.

# Current high-load application and primitive command accumulation candidate

The previous retained-text probe is a different workload:the existing Overlay app reuses five labels. Its new current-source Native pilot had draw0/upload0,GPU2.46us and roughly5..9ms UI Render. The common primitive writer repeatedly copied a large immutable command while constructing32768 swatches. UiDrawListBuilder now accumulates count/bounds in four value fields and commits at every other command access or storage switch. Public command immutability,callbacks/input,primitive geometry/colors,vector quality and source-generated widget wiring remain unchanged. Added fields/branches are costs. Only this production source differs between application baselines;both include the preceding text-cache fix.

Fixed RC3 SDK/runtime,win-x64 NativeAOT Speed,existing128palette x256swatch workload with1800background rectangles,actual1600x1000,MSAA1,VSyncoff,pipelineon,idleSkipoff. Minimum96accepted presentations/1s warmup then12s measured,three independent processes per variant per idle/motion,AB/BA/AB,12executions. No forced GC. Reused scripts/measure-overlay-layers.ps1 and the existing app/memory diagnostics. Raw frame audits pass. FPS medians idle139.7838→153.7823(+10.0144%),motion115.0806→142.4457(+23.7791%);median frame7.0353→6.0980ms(-13.3228%) and7.0032→6.7267ms(-3.9482%). All timing/memory run ranges overlap,so application improvement remains unconfirmed.

Adverse medians are preserved:idle1%low53.5316→47.0805FPS(-12.0509%),motion working set132833280→133210112B(+0.2837%),GPU3.5683→3.7346us(+4.6604%) and27.8728→29.3575us(+5.3268%),all inconclusive. Private idle180019200→180150272B(+0.0728%),motion182091776→180436992B(-0.9088%),also overlapping. Idle retains128lists with0rebuild/draw/upload;motion127reused/1rebuilt and66512B upload/frame in both. Callback-only allocation remains0/608B;this excludes outside-engine work. Sparse120-frame CPU profiles include warmup and are not process CPU time or exact phase accounting. Neither these medians nor historical11/04 values resolve the2048-text short Private issue or prove long-run memory stability.

Managed/Native74 focused checks each pass;all100 GPU readback images match. Count/bounds,clip/callback boundaries,channels,queued-frame independence,reset,MSAA1/4 gradients,popup/text coverage and reference-counted resources are checked. Initial test compile errors7 were corrected to existing Split/SetCurrentChannel/Merge and explicit ReleasePooled/Return;no product criterion was relaxed. No full suite or desktop capture was repeated. App sources stayed unchanged through comparison and Native focus.

Evidence:artifacts/highload-app-2026-11-06/verification.json,comparison.json,cpu-diagnostics.json. Full raw records,source hashes and saved source files:L:/Cache/Duxel/highload-app-2026-11-06/. Native app before FE87F18F91B42F6303F6A32C04DBD2657735FD01268729CED0EB5E9BA570BC59;after A7BC05106B4F5A33270392D92E0592298A3D48CD97B9EA8791EDD0DD64E37CC7. Focus verifier C40332E1649A574D8943322560A7773D8887E09D055B9AFF7F35C8AAC2B6B479. Twenty existing debug-symbol files were losslessly NTFS compressed with all hashes preserved;W free307310592→1037254656B at that snapshot. No deletion/move. Whole-goal counts remain unchanged;resize remains deferred;NET11-only development validation continues.

# Weighted Row/Column and generated flex infrastructure

Before:Row/Column measured only natural-size children;Expanded/Flexible/Spacer were absent. After:the existing source generator emits UiExpanded:UiFlexibleWidget<UiTightFlexFit>,UiFlexible:UiFlexibleWidget<UiLooseFlexFit>,and UiSpacer:UiFlexSpacer. The same ownership/layout path serves attributed user wrappers. Non-flex children/spacing precede weighted allocation;loose/tight fit,MainAxisSize,finite Stretch,explicit overflow/unbounded failures and natural zero flex are supported. Live Flex updates request frames and preserve owned child instances/state. Parent data forwards through Build composition and stops at physical layout barriers. The shared measurement scope also guards metadata Build;captured ordinary Button input is rejected and state restored.

Generated buttons honor parent paint/hit/accessibility size. Custom leaves use LayoutConstraints/ConstrainToLayout. Immediate buttons inside custom controls and input-slot size contracts stay unchanged. Legacy Dux.Spacer(float height) is preserved;the new helper is Dux.FlexSpacer(int flex). These are Infrastructure attributes,not additional Fluent visual components. Fit/axis implementation selection is closed at compilation;runtime weights/constraints remain necessary data. One optional parent-data array per flex Row is retained by child count and cleared on disposal;no per-frame metadata objects are introduced.

Pinned RC3 managed49 CPU contracts and Native/managed67 GPU-inclusive contracts pass.37 actual GPU readback images match byte-for-byte across runtimes,including MSAA1/4,three frame slots,independent weighted rectangles,a full-width Fluent button,live weight changes and acquired-image replay.10000 generated Expanded children with small measurement probes allocate0 current-thread bytes for the next pure constrained measurement after two warm frames. This is not10000-button rendering,input/throughput,Private/working-set or application/Pi FPS. No before/after speedup percentage applies to a previously unsupported feature. The preceding application benchmark remains inconclusive;it was not rerun.

The fixed1:1:2 sample first-presented and exited normally after15s. This binary was built after the measurement guard but before the unused live-setter extension;reuse is limited to its unchanged fixed-weight behavior. Final Native67 checks cover the current setter and new GPU update cases. Exact source/executable hashes,records and scope:artifacts/flex-containers-2026-11-06/verification.json and L:/Cache/Duxel/flex-containers-2026-11-06/. Final verifier E714CC00A9E1999500EF55951C3022E714E1077936B2155F713373CAEF091164. Gallery9C4686B4DFAACAECE021339F93158290917BC2A9F63C2E8AB886BCF7B2346374. Verification command:Duxel.Graphics.Tests.exe OUTPUT containers 3.

Resolved validation failures remain preserved:optional Spacer ambiguity was corrected without rebinding old calls;old23-check output accidentally executed after a failed build is explicitly invalid,and later build/run commands stop on build failure. A new test incorrectly expected180 instead of40+20spacing+20loose+120tight=200;the arithmetic was corrected. Native57 and59 intermediate artifacts remain historical,not final-current evidence. The additional state-preserving weight contract justified final Native67 qualification without a full suite or12-run application repeat. SDK selection/source snapshot paths were corrected before native execution when a relative path was evaluated from the toolchain directory.

The row-column-flex-expanded-flexible-spacer stage is now1/6 complete;the original other five stages remain open. General leaf stretching,single-child wrappers,mutable keyed child collections,RTL/baselines,Stack/Positioned/Wrap/Grid,scroll/virtualization,incremental layout and full render/memory performance remain required. Full Fluent2,portable/Pi and previous engine goals are preserved. Resize stays separately deferred;NET11-only development validation continues,NET9/10 final deployment compilation remains unrun.

## 공통 위젯 Build 저장 비용 개선 — 2026-11-06

UiWidget의 소유권은 기존 키별 자식 저장소에서 검증하고, 부모 범위 키는 기존 정체성 저장 공간을 재사용한다. 의존 상태와 자식 정리에 쓰는 임시 목록도 기존 Dictionary 객체에 둔다. 위젯 필드8바이트와 의존 없는 Build 문맥24바이트를 줄이며 별도 래퍼 객체를 만들지 않는다. 기본·생성·사용자 파생 위젯에 같은 경로를 적용한다. 구독,실패한 Build 정리,정체성,입력,해제와 측정 무효화는 유지한다.

고정.NET11 RC3 NativeAOT에서 세 모드별 전후 독립3회씩 ABBAAB 순서로 총18개 프로세스를 실행했다. 생성 Container1만 개의 생성 할당6,080,000→5,760,000B(-5.26%),최초 Build880,000→640,000B(-27.27%)다. 서로 다른 상태를 관찰하는 루트1만 개의 최초 Build5,280,000→5,200,000B(-1.52%),키별 자식을 만드는 최초 Build4,800,000→4,560,000B(-5%)다. 관찰 상태의 변경 없는 Build·상태 변경 뒤 Build와 소유 위젯의 변경 없는 Build는0B를 유지한다. 의존 측정 구간에서 루트 객체·상태·배열 준비와 해제는 제외하되 자식 팩터리 생성은 포함한다. 생성 비용과 최초 배치 유지 결과 확보 비용은 별개다.

관리/Native 각각 트리·소유권·수명101개와 컨테이너175개 검사 통과,GPU121이미지 전부 일치. 기존 팩터리25% 감소 조건은 완화하지 않았고,Native 비소유 버튼1024개에서114,688→81,920B(-28.57%)로 통과했다. 유지된 Wrap/Grid 순수 측정 중앙값0.2→0.2µs,p950.3→0.3,p990.3→0.3이며 실행별 범위가 겹쳐 속도 향상은 확정하지 않는다. 작은 진단의 Private·작업 집합 감소는 실제 앱·장기 메모리 개선의 근거가 아니다. 강제GC·부하·품질 저하는 없다. 이전 Container 생성 추가 비용80,000B보다 크게 줄었으며 과거 기록은 보존한다.

근거:[검증](../../../artifacts/build-store-2026-11-06/verification.json),[전후 비교](../../../artifacts/build-store-2026-11-06/comparison.json). 원본과 초기 실패 진단은 L:/Cache/Duxel/build-store-2026-11-06/에 보존한다. Fluent2 전체·나머지 컨테이너 계약·고부하 메모리·이식성은 미완료이며 리사이즈는 별도 보류다. 개발 검증은.NET11만,NET9·10 배포 컴파일은 전체 목표 최종 완료 시 수행한다.

## 생성된 단일 자식 배치 위젯 — 2026-11-06

`UiPadding`, `UiAlign`, `UiCenter`, `UiSizedBox`, `UiConstrainedBox`를 공통 `UiSingleChildBox<TLayout,TOptions,TPresentation>`의 소유권·유지 경로에 연결했다. 기존 `UiContainerBox`도 컴파일 시점 어댑터로 같은 기반을 사용하며 옵션·필수 자식·사용자 배치/표면 확장을 유지한다. `[UiWidgetStyle]`은 기본·사용자 위젯의 실제 닫힌 기반 타입과 생성자를 만든다. 다섯 위젯은 Infrastructure이며 Fluent 시각 컴포넌트 수에 더하지 않는다. 런타임 테마 탐색이나 빈 자리를 위한 가짜 자식은 없다.

```csharp
var counter = Fluent.Button("Count", OnCount);
var body = Dux.SizedBox(
    Dux.Padding(Dux.Align(counter, UiAlignment.BottomRight), new(12)),
    width: 240, height: 80);
var centered = Dux.Center(Fluent.Button("Reset", OnReset), widthFactor: 2);
var bounded = Dux.ConstrainedBox(centered, new(100, 300, 40, 100));
```

Padding은 자식의 최소·최대 제약을 여백만큼 줄인다. Align/Center는 자식 최소 제약을 풀고,배수가 없으면 유한 축을 채우고 무한 축은 자식 크기를 따른다.0이상의 배수는 축별로 적용한다. SizedBox는 부모 제약 안에서 지정한 축을 강제하고 나머지는 자식 크기,자식이 없으면0을 따른다. `Dux.Expand()`는 유한 축이 필요하다. ConstrainedBox는 부모 범위 안에서 추가 제약을 적용한다. 자식이 없어도 대체 위젯을 만들지 않고,넘치는 그리기는 표면에서 자른다. 옵션은 채택·변경 전에 검증하고 변경 시 자식 정체성을 보존하며 부모 측정을 무효화한다. 기존 Container의 자연 크기 자식 배치는 유지하며,자식 크기를 강제할 때 SizedBox를 사용한다.

고정RC3 관리/Native 각각 컨테이너·GPU229검사(이전175+54),MSAA1/4·프레임 슬롯3에서GPU159이미지가 모두 일치했다. 강제/느슨한/무한 제약,배수,빈 공간,중첩,실제 Fluent 클릭,실시간 옵션 변경,해제,사용자 생성 위젯,사용자 배치의 보수적 캐시 참여,획득 이미지 반복을 확인했다. 변경 없는 기존 수명101검사는 소스·실행 경로 호환성을 확인해 재사용했다. 전체 검사·고부하 앱을 다시 실행하거나 새 속도 향상을 주장하지 않는다.

별도 Native1만 Padding 저장 진단:생성5,280,000B,최초 측정 확보2,320,000B,변경 없는 측정과 무효화 후 재측정0B,자식1만 개 해제. 최초 측정 비용은 정체성 준비와 유지 결과를 포함하며 캐시만의 크기나 프로세스 Private가 아니다. 배열·문맥·프레임·해제는 할당 측정 구간 밖이며 강제GC는 없다. 관리 생성은5,280,384B로 관측했고 최초 측정·이후 값은 같다. 런타임별1회 저장 진단이며 전후 성능·메모리 향상 비교가 아니다. 기존 Container 생성5,760,000B/첫Build640,000B/변경 없는Build0B도 Native 집중 회귀 진단에서 유지됐다.

[검증 기록](../../../artifacts/single-child-2026-11-06/verification.json). 컨테이너3/6단계를 완료했고,말단 확장·스크롤/가상화·전체 통합/성능은 남아 있다. 리사이즈는 별도 보류,개발 검증은.NET11만 수행한다.2026-11-06 조회한 공식 참고:[Padding](https://api.flutter.dev/flutter/widgets/Padding-class.html),[Align](https://api.flutter.dev/flutter/widgets/Align-class.html),[SizedBox](https://api.flutter.dev/flutter/widgets/SizedBox-class.html),[ConstrainedBox](https://api.flutter.dev/flutter/widgets/ConstrainedBox-class.html). 갱신되는 API 문서이며 Flutter 소스 커밋을 고정하지 않았고 Flutter 전체 API 동등성을 주장하지 않는다.

## Slider·Toggle의 부모 할당 반영 — 2026-11-06

SizedBox 측정은80x64인데 생성 Slider의 실제 그리기·입력은120x32인 불일치를 재현했다. 짧은 레일의 끝이 할당된 클립 밖에 있어 의도한 끝값에 도달하지 못했다. 생성 Slider·Toggle이 기존 공통 렌더에 명시적으로 배치 참여를 전달하도록 수정했다. Slider/Checkbox/Switch/ToggleButton의 동작과 시각 부품 크기를 유지하면서 부모 크기에 루트·레일·표시기/레이블 구성을 배치한다. 짧은 레일도 원래 썸 크기를 유지하고 넘침을 자르며 잘못된 clamp 범위·0나눗셈을 막는다. 포인터 이동 거리가0이어도 키보드로 조작한다.

`IUiSliderPresentation.Arrange`·`IUiTogglePresentation.Arrange`는 기존 닫힌 제네릭 표현에서 선택하는 정적 가상 계약이다. 부모가 다른 크기를 주면 표현이 Arrange를 구현해야 하며,미지원 사용자 표현은 다른 영역을 몰래 그리지 않고 측정 중 명확히 실패한다. 할당된 크기를 무시한 결과도 거부한다. 기본 표현은 계약을 구현한다. 생성 사용자 다이얼은 같은 Slider 동작에 다른 실제 배치/그리기를 연결해 검증했다. 런타임 표현 탐색이나 위젯별 추가 필드는 없다.

기존 즉시 API는 자연 크기 의미를 보존한다. 사용자 컨트롤은 새 `Button(...,size,constrainLayout:true)`, `Slider(...,options,size,constrainLayout:true)`, `Toggle(...,ref value,constrainLayout:true)` 오버로드로 같은 배치 경로에 참여한다. 공개 `UiControlContext.MeasureSlider<TPresentation>`·`MeasureToggle<TPresentation>`도 같은 제약 측정을 사용한다. 고정 입력 자식 슬롯은 기존의 명시적인 배치 참여 구분을 유지한다.

고정RC3 관리/Native 각각172개 집중 검사에 기존 Slider/Checkbox/Switch 계약,할당 입력 영역·끝값·세로/0길이 레일·키보드·사용자 참여/실패·실제 생성 Arrange 선택을 포함했다. MSAA1/4·프레임 슬롯3에서GPU43이미지 전부 일치했다. 독립적인 할당 기하로 썸/표시기 크기와 부모 변경을 확인했다. 원본 엔진의 재현 실패,새 검사의 DirectText 준비 누락,중립 기대 화면과 실제 hover 입력이 달랐던 이미지는 로그에 보존했다. 마지막 검사는 실제·기대 화면 모두 포인터를 화면 밖에 두어 같은 rest 상태로 비교하며 픽셀 기준을 완화하지 않았다.

[검증 기록](../../../artifacts/leaf-constraints-2026-11-06/verification.json). 기능·배치·입력 수정이며 속도 향상 주장은 아니다. 컨테이너3/6을 유지하고 Input·RadioGroup·텍스트·진행/스피너 등 나머지 말단 계약은 전체 점검이 필요하다. Fluent2 전체·장기 메모리·이식성은 미완료이며 리사이즈는 별도 보류한다.

## 편집 입력창의 부모 할당 반영 — 2026-11-06

기존 엔진은 부모80x64로 측정해도 Input 필드를80x32로 그렸다. 별도 원본 소스 빌드로 불일치를 재현했고,개선본은 기존 형식화 입력 엔진에서 명시적으로 배치에 참여한다. `IUiInputPresentation.Arrange`가 배치·편집 전에 부모 크기에 맞는 표현을 정적으로 선택한다. 미지원 사용자 표현은 측정 중 실패하며 Place가 할당된 루트 크기를 바꾸는 것도 거부한다. 기본 Fluent 외곽선/밑줄/채움·크기·장식 구현은 같은 기하 경로를 쓰고,호환 문맥 표현도 Arrange를 구현한다.

필드·편집 영역·텍스트 줄 위치·앞뒤 슬롯이 실제 할당 크기를 따른다. 폰트/줄 지표와 명시한 자식 슬롯 크기는 유지하고 좁거나0인 영역에서는 내용을 자른다. Arrange는 장식을 다시 측정하지 않고 이미 측정된 크기를 쓴다. 부모 크기를 바꿔도 입력 소유자·활성 편집·undo 이력·실제 자식 콜백을 보존한다. 새 공개 형식화 InputText 오버로드는 constrainLayout 참여를 명시하고,기존 즉시 오버로드는 자연 크기를 유지한다. 공개 UiControlContext.MeasureInput도 같은 제약 측정 경로를 사용한다.

고정RC3 관리/Native 각각212개 검사에 기존 InputKernel·Fluent Input·장식·소유 자식·Field 검사를 포함했다. MSAA1/4·프레임 슬롯3에서GPU25이미지 전부 일치했다. 독립적인80x64/160x48 기하·타이포그래피·밑줄 기준선·24px 소유 자식·획득 이미지 반복을 확인했다. 원본 빌드는 실제 선택한5개 파일과 이전 검증본에 일치하는 해시를 기록했다. 초기에 겹친 빌드는 중단·무효 처리했고,기준 빌드는 별도 중간 경로를 사용한 뒤 개선본을 직렬 전체 재빌드했다. 최초 수명 검사는 다른 살아 있는 입력 소유자를 빠뜨렸으며,수정한 검사는 각 소유자 상태의 유지/해제와 모두 해제한 뒤 저장소0개를 확인한다. GPU 기준 입력의 빈 키도 유효한 숨김ID로 고쳤다. 초기 기록은 보존한다.

[검증 기록](../../../artifacts/input-allocation-2026-11-06/verification.json). 기능·편집·배치 수정이며 처리량/FPS/Private 향상 주장은 아니다. 컨테이너3/6을 유지하고 RadioGroup·텍스트·진행/스피너·스크롤/가상화·전체 말단/통합 계약은 남아 있다. 리사이즈는 별도 보류,개발 검증은.NET11만 수행했다.
