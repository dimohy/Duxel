# 2026-11-05 공통 CPU 재사용 진행

## 내부 창 배치·해제 위치와 사용자 이동 지연

사용자 화면에서 제목 잘림·과도한 하단 여백·내부 창 이동 지연을 확인해 위젯 추가보다 우선 처리했다. 선언형 위치/크기가 매 프레임 재적용되는 것을 소스로 확인했고 해제 위치 회귀 검사가 수정 전 실패했다. Initial/Always 배치, 작업 영역 좌표, 콘텐츠 AutoHeight와 크롬 위 제목 숨김 방지를 공통 창 경로에 추가했다. 샘플만 좌표를 바꾸는 수정으로 끝내지 않았다.

NET10전체1004·관리/고정RC3 Native108·이전428이미지 동치·Release 경고0/오류0. 드래그 해제 전체 GPU 픽셀과 실제 출력 창 이미지를 확인했다. 이 검사는 처리 위치·화면 정확성이지 전체 앱 성능 검사가 아니다. 원래 창을 재생성하거나 부하를 낮추지 않고 필요한 콜백을 프레임마다 유지한다.

이동 검사 draw6/업로드1376B(320×240)·1584B(404×281)는 다른 크기의 카운터이며 속도 비교로 세지 않는다. CPU/FPS/Private/작업집합 Before/After·개선율은 아직 미측정이다. 새 Native 갤러리에서 기존 App/Vulkan 계측을 켜 사용자 이동 입력을 확인 중이다. 초기 유휴의 GPU수us·프레임펜스4~8ms를 이동 병목으로 확정하지 않는다. 사용자 체감 지연·정확한 프레임 분포·기존 메모리 목표는 미해결/미검증으로 유지한다.

근거와 명령: [내부 창 배치](window-placement.ko.md), artifacts/gallery-window-fix-2026-11-05/verification.json; `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- <결과> --window-interaction-only`로 집중 재현을 실행한다. 실제화면 실행은 [가이드](widget-gallery-run.ko.md)를 따른다.

## 기본 Fluent Text와 공통 줄바꿈

가설/변경: 일반 생성 Text가 컨텍스트 글꼴을 사용해 명명된 Body1과 달랐다. 정적 표현을 실제 Regular Body1(14px/20px)·기본 줄바꿈에 연결했다. 줄바꿈을 기본 사용하면서 드러난 불필요한 단어/결과 문자열 생성은 들어맞는 한 줄의 원본 반환으로 줄였다. 명시적 개행에서 폭 초기화를 하지 않던 오류도 공통 코드에서 수정했다. 기존 비제네릭 즉시 API의 글꼴 정책은 명시적 호환 표현으로 보존한다.

검증: NET10 SDK10.0.301/runtime10.0.9 전체963·관리67·고정SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 Native67, Release net8/9/10 경고0·오류0, 이전417이미지 동치. 한글 포함 래스터/벡터 Text와Body1 GPU 픽셀·좁은 줄바꿈·개행·동적 소스·백엔드 누락·글꼴범위를 검사했다. 문자열 재사용은 참조 동치로 확인했다. 기존650프레임 상주 테스트가 다른 유휴 글꼴 회수까지 금지하던 오류를 실제 참조 자원으로 범위를 바로잡았다.

구성1,024개 재확인: 관리 이전114,688B→생성90,112B(-21.43%), Native 이전98,304B→생성90,112B(-8.33%). 같은 런타임 안의 비교만 유효하며 이번 글꼴 변경의 속도 개선이 아니다. 앱FPS/median/p95/p99/1%low·Private/작업집합·Pi 개선은 미측정이며 워밍업 시간 벤치로 보고하지 않는다. 전체 Text 변형·공식 룩앤필/UIA·기존 즉시 API 기본 이전은 미완료다.

명령: `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- <결과 경로>`, `dotnet run --project tests/Duxel.Graphics.Tests -c Release -- <결과 경로>`, `dotnet build Duxel.slnx -c Release`. 상세 근거: artifacts/fluent-default-text-2026-11-05/verification.json과 [한영 Text 후속](fluent2-text.ko.md).

## 공통 둥근 그라데이션의 배치·업로드

가설: 불확정 ProgressBar의 별도 배경과 두 메시 사각형이 도형당 명령 전환·기하 업로드를 늘리고 둥근 경계를 사각 클립으로 제한한다. 공통32B 사각형 슬롯2개를 한64B 그라데이션으로 해석하면 기존 스트라이드·품질을 유지하며 배치할 수 있다.

변경: UiLinearGradient/AddRectFilledGradient·명령 변형을 공통 엔진에 추가하고 동적/정적/컴파일 레이어 및 기존 분석적 마스크에 연결했다. 실제 생성/즉시 ProgressBar가 사용한다. 후보 SPIR-V를 별도 검증한 뒤 보존된 원본과 구분해 반영했다.

| 동일256개·동일 픽셀 | Before | After | 감소율 |
| --- | ---: | ---: | ---: |
| 실제 draw calls | 512 | 1 | 99.80% |
| 동적 기하 업로드 | 61,440B | 16,384B | 73.33% |

검증: NET10 SDK10.0.301/runtime10.0.9 전체954·관리 GPU45·입력62·Release net8/9/10 경고0·오류0. 고정SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 NativeAOT GPU45검사를 독립3프로세스에서 메시/도형·도형/메시·메시/도형으로 실행했으며 모두 같은 draw/upload 수치다. Native 입력62도 통과했다. MSAA1/4·정적 컴파일·깊이순서·불투명도·획득이미지/변경영역 재사용을 검사했다. 원본 셰이더를 현재와 같은320×240에서 실행한323이미지 모두 새 셰이더와 동치다. 과거400×300 결과와 직접 비교한 결과는 크기가 달라 동치 근거에서 제외했다.

명령: `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- <결과 경로>`, `dotnet build Duxel.slnx -c Release`. 공통 GPU 소비 앱은 `tests/Duxel.Graphics.Tests`이며 결과 경로 뒤 `packed-first`로 순서를 교대한다. 정확한 게시·실행 로그는 artifacts/rounded-gradient-2026-11-05에 보존한다.

한계: 시간 벤치가 아닌 실제 draw/upload 카운터 비교다. 워밍업/평균FPS/median/p95/p99/1%low와 Private/작업집합은 이번 단계에서 측정하지 않았다. 전체 앱 속도·메모리·Pi 개선으로 합산하지 않는다. NVIDIA 실제 GPU만 확인했으며 AMD 점유율/시간은 미검증이다. 추가 셰이더 보간 비용·기존 캐시상주 비용·RC3물리 비교 실패·전체Fluent는 미해결/미완료로 유지한다.

근거: [한영 공통 그라데이션 문서](rounded-gradients.ko.md), artifacts/rounded-gradient-2026-11-05/verification.json, image-comparison-final.json, integrated-native-run1~3/result.json, source-hashes.json, preservation-audit.json. 직전247소스 중232동일·의도한15개 변경·누락0이며 기존 실행 파일을 보존했다.

## Fluent 기본 팩터리·리프 생성 비용

가설: 기본 선언형 팩터리를 실제 상속 위젯으로 전환하면서 리프에 필요 없는 Build 컨텍스트·구독 컬렉션·렌더 델리게이트를 없애면 생성 할당을 줄일 수 있다. 정체성은 기존 매 프레임 재선언 및 keyed child 입력 계약을 모두 유지해야 한다.

변경: `Dux.Button()`을 생성된 `Fluent.Button()`·`UiFluentButton`에 연결했다. 리프는 같은 UiWidget의 재진입·소유·해제를 공유하되 직접 렌더링한다. Parent 정체성은 기존 라벨 범위 및 소유된 자식 키를 사용한다. 노출 특성은 Core의 표준 이름·팩터리 생성 경로에 적용하고 레거시 버튼을 보존·제외했다. 다른 컴포넌트의 전체 전환은 미완료다.

Before/After: 준비 후 .NET10 구성 전용1,024회에서 보존한 이전 delegate 팩터리131,072B→98,304B, 25% 감소(128→96B/개). 강제 GC 없음. 앱 Private·전체 렌더링 FPS·NativeAOT 반복 성능은 미측정이며 이 수치로 대체하지 않는다.

검증: SDK10.0.301 및 고정 SDK11.0.100-rc.3.26522.148의 전체 관리 회귀 각775건, 집중63건, 생성기35건. 솔루션 Release net8/9/10 경고0·오류0. 고정RC3 Speed/win-x64 NativeAOT 및 .NET10 관리 FBA는 실제 첫 GPU present·exit0. 명령은 `dotnet build Duxel.slnx -c Release`, `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- <결과경로>`, `dotnet run --project tests/Duxel.Generators.Tests -c Release -- <결과경로>`, `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache`이며 고정RC3·관리 실행 옵션은 각 로그와 검증 기록을 따른다.

리스크·남은 범위: Fluent 전체 공식 분모·하위 구성·상태, 모든 비표준 비노출, 전체 룩앤필·접근성·모션·재질, 동일 조건 Private·CPU/GPU 성능, Linux/ARM64·Pi 실측. 카탈로그 버튼 한 항목과 토큰 존재를 전체 지원·시각 일치로 보고하지 않는다. 근거 `artifacts/fluent-components-2026-11-05/`; 상세 [Fluent 전체 전환](fluent2-coverage.ko.md).

## 위젯 구조 교정과 구체 구현 연결

가설: 위젯 구현의 선택을 생성 시점에 닫힌 제네릭 타입으로 확정하면 사용자 상속을 유지하면서 프레임마다 타입 검색·구현 객체 생성 없이 동작과 표현을 분리할 수 있다. 숫자 상수 생성만으로는 표현 알고리즘 교체 요구를 충족하지 못한다.

변경: 기본 `UiButton`을 `UiWidget` 수명 체계에 추가하고, `IUiButtonBehavior`/`IUiButtonPresentation`을 공통 immediate kernel에 연결했다. 증분 생성기는 선택 타입으로 상속 가능한 버튼을 생성한다. 첫 테마는 사용자 확정 Microsoft Fluent 2이며 Windows 11/WinUI 지침을 같은 테마에 반영한다. 기존 수치 스타일은 호환 경로로 남아 있으며 다른 위젯 전환은 미완료다.

검증: SDK10.0.301에서 Release 솔루션 경고0·오류0, 전체769검사·집중57검사·생성기27검사. NativeAOT 스킬에 따라 고정 SDK11.0.100-rc.3.26522.148, win-x64, Speed로 FBA 실제 native code generation과 첫 GPU present·exit0을 확인했다. 관리 .NET10도 첫 present·exit0. 처음 원형 표현 검사가 구형 circle 전용 목록을 검사해 실패했으며, 실제 공통 primitive 저장 방식을 직접 확인하여 음수 반경 rectangle stream을 검사하도록 수정했다. 이 실패를 제품 렌더링 결함으로 오인하지 않는다.

명령: `dotnet build Duxel.slnx -c Release`; 고정 SDK 경로에서 `dotnet run --project tests/Duxel.Generators.Tests -c Release -- <결과경로>` 및 `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- <결과경로>`. FBA는 `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache`를 사용하고 관리/고정RC3 별도 실행·로그를 보존한다.

Before/After·개선율: 이번 구현 타입 연결의 CPU·Private·FPS 수치는 아직 미측정이다. 시작 시간의 단일 관측을 성능 개선으로 사용하지 않는다. 기존 atlas CPU 개선과 합산하지 않는다. 근거: `artifacts/widget-implementation-2026-11-05/`.

리스크·다음 검증: 모든 기본 위젯의 공통 상속·표현 분리, 테마 단위 생성과 공식 Fluent 적용 범위, 사용자 확장 API, 동일 조건 성능·메모리 비교가 남았다. 목표는 활성 상태이며 숫자 생성 성공2조건은 재개방했다.

가설: atlas 텍스트의 glyph 배치·kerning·snap·clip 계산과 기하 작성은 입력과 분리된 순수 작업이므로 동일 요청에서는 재사용할 수 있다. 즉시 모드 입력·콜백·커서 상태를 생략하지 않는다.

변경: 공통 `UiDrawListBuilder.AddText`에 정확한 요청 키와 제한된 glyph-run 재사용을 추가했다. 첫 요청은 메타데이터만 기록하고 반복할 때 기하를 보관한다. 메타데이터는 값 형식이며 builder당256개·256KiB 기하·256자,120프레임 유휴 해제를 적용한다. 측정 캐시는 폰트 해시 대신 실제 객체의 약한 소유 관계를 사용하고 알림 콜백·nullable 행높이 의미를 유지한다. 샘플·셰이더·벡터 품질을 변경하지 않았다.

검증: 저장소 SDK10.0.301 / runtime10.0.9와 별도 고정 SDK11.0.100-rc.3.26522.148 / runtime11.0.0-rc.3.26522.148에서 전체731개씩 통과했다.108개 기하·명령 조합, 소유권·상한·변화 문자열·유휴 해제를 포함한다. net8/net9/net10 Release 빌드 경고0·오류0.

측정: 고정 RC3 NativeAOT Speed의 분리 CPU 진단은 같은32개×64자·같은 글꼴·1600×1000 clip, 준비1초·96회 이상, 측정2초·독립3회 AB/BA/AB이다. 최종 반복/초 중앙값29,371.79→132,492.16(+351.09%), median -77.98%,p95 -77.78%,p99 -75.91%이며 범위가 겹치지 않는다. 정상 상태 기하 캐시는212,992B/32항목이고 할당량은440.02→440.00B/반복으로 범위가 겹쳐 감소 미확정이다. 강제 GC는 진단에 사용하지 않았다. 초기 참조 형식 후보도 보존했다.

리스크·남은 범위: 이 결과는 앱 FPS·Private 감소·기본 DirectText 전체 구성 개선·Pi 실측이 아니다. DirectText와 비텍스트 paint/컨테이너 레이아웃의 공통 재사용 확장, 원래 고부하 앱의 RC3 비교와 Private·작업 집합·상주량 검증, GPU 기능 호환 및 Linux/ARM64 경로가 남아 있다. 목표는 활성 상태다.

재현과 근거: [공통 계약·상세 기록](widget-cpu-retention.ko.md), `artifacts/widget-retention-2026-11-04/iteration-audit.json`, `text-cpu-final/summary.json`. 기존 소스240개 동일·기존3개 의도한 수정·새 소스2개를 감사했다. 삭제·커밋·푸시하지 않았다.


## 버튼1만개 공통 입력 ID 재사용

가설:4,096개 공통 ID 캐시 뒤의 유지 위젯은 매 프레임 문자열을 다시 만들어 할당과 CPU 비용을 유발한다. 위젯 수명별 현재 부모·범위·기본 ID를 보존하고4,096자 상한·부모 변경·해제를 적용했다. 입력/콜백·전체 위젯 호출·기하·품질·부하를 유지했다.

검증: NET10 전체801·생성기64·net8/9/10 솔루션 경고0·오류0. 최종상한 전후 이미지323개 동일. 고정 RC3 NativeAOT Speed·1600×1000 clip·10,000개·준비0.5초/측정1초·독립3회 AB/BA/AB CPU 진단. median12.55~14.13% 감소, 할당378,528~425,752→664B/반복(99.82~99.84% 감소). 종료 Private·작업집합도 해당 관측 범위는 겹치지 않았지만 장기/최대/앱 전체 감소로 확대하지 않는다. partial/full p95와 partial p99는 겹쳐 개선 미확정이다. CPU 명령562개 유지이며 GPU draw가 아니다.

회귀: 위젯당 참조 필드24B, 일시 팩터리1,024개98,304→122,880B(+25%). 이전 delegate보다6.25% 적다는 사실로 이번 회귀를 숨기지 않는다. Native CPU EXE+1,024B. DirectText·layout/paint·실제 앱 GPU/메모리·Pi와 이 일시 객체 비용 해결은 남았다. 반사 JSON 기록의 Native 경고를 타입 DTO+소스 생성 컨텍스트로 해결한 뒤 측정했다. 원본/초기 후보/경고 산출물은 보존했다.

근거·명령: [한영 상세](widget-scale.ko.md), artifacts/widget-scale-2026-11-05/comparison-final.json, final-measurement-runs.jsonl, baseline-src, full-final-net10. 실행은 각 고정 Native CPU EXE에 `<result.json> --widgets`를 전달한다. 앱 FPS·Pi 실측·전체 목표 완료가 아니다.

## ID 저장소 지연 생성과 할당 회귀 수정

가설·변경: 캐시 필드3개를 모든 새 위젯에 넣은 것이 일시 팩터리의24B 증가 원인이다. 기존 정체성 참조에 token/cache 수명 상태를 보관하고 범위 렌더에서만48B 소유 캐시를 생성하도록 분리했다. 비소유 Parent에는 생성하지 않는다. 기존 ID 의미·상한·입력·콜백을 유지한다.

결과: NET10 팩터리1,024개122,880→98,304B(-20%), 캐시 전 원본 수준 복구 및 기존 delegate 대비25% 감소. 전체802·생성기64·net8/9/10 경고0·오류0·직전 이미지323개 동일. 고정 RC3 NativeAOT Speed의 동일 측정기·조건으로 원본/inline/lazy 각3회 교대 비교했다. 원본 대비CPU median12.40~13.31% 감소·664B/반복 유지. inline 대비CPU는 모든 범위가 겹쳐 개선/악화 미확정이다.

비용: 유지된 본체+캐시는 inline보다24B 크다. 종료 Private0.47~1.18MiB·작업집합3.42~4.42% 증가가 관측됐고 숨기지 않는다. 전체 메모리 감소나 장기 안정화로 승격하지 않는다. 원본 대비partial/full p95/p99는 겹치며 악화된 중앙값도 comparison.json에 기록했다. Native CPU EXE는직전대비+512B다. DirectText·실제 앱 전체 성능과 메모리·Fluent·Pi 미완료 축은 유지한다.

근거: artifacts/widget-identity-storage-2026-11-05/verification.json, comparison.json, runs.jsonl, full-net10. 재현은 보존된 세 CPU Native EXE 각각에 `<result.json> --widgets`를 전달한다. 원래 기록과 실행 파일은 보존했다.

## 텍스트 소스·표현의 컴파일 연결

변경: 기존 Text/색상/줄바꿈 팩터리를 생성된 상속 위젯으로 연결하고 즉시 API와 공통 측정·배치·그리기를 사용한다. 문자열/함수 소스 정책까지 닫힌 제네릭으로 고정하여 소스 종류의 프레임별 선택 분기·불필요한 필드를 제거했다. 사용자 getter는 매 렌더 한 번 유지한다. 사용자 표현은 임의 측정·배경·표식·그리기를 제공한다.

검증: .NET10 전체815·집중103·생성기66·net8/9/10 경고0·오류0, 직전 이미지323개 동일. 고정 RC3 NativeAOT native code generation·첫 present·exit0을 확인했다. 전체 상호작용·룩앤필·Native 성능으로 확대하지 않는다.

할당: .NET10 실제 이전 매개변수 캡처 본문의 구성1,024회114,688→90,112B(-21.43%). 처음 상수 람다를 쓴 잘못된 비교는 실제 본문 fixture로 수정했다. 이 수치는 CPU 프레임·Private·NativeAOT 개선 증거가 아니다. 이전 프로토타입과 최종 바인딩 실행 파일을 별도로 보존했다.

남은 범위·근거: 공식 Text의 전체 글꼴·장식·크기·프리셋·의미 정보·화면 일치, 공통 DirectText/layout/paint 재사용과 전체 성능·메모리·Pi 검증은 미완료다. [한영 Text 기록](fluent2-text.ko.md), artifacts/fluent-text-2026-11-05/verification.json, source-hashes.json, native-final-run.json을 따른다.

## 실제 글꼴·기준선과 명명된 프리셋17종

변경: 닫힌 source/typeface/metrics 표현을 생성된 위젯17종에 연결했다. 실제 Regular400/Semibold600/Bold700 파일과 DirectWrite ascent/descent로 공식 행높이를 적용한다. 메트릭 캐시는64개·경로2,048자 상한이며 기존 DirectText 캐시와 실제3D 벡터 기하를 유지한다. 기존 FBA의 --typography 모드에서17종을 keyed child로 사용한다.

검증: NET10 전체815→847·생성기66·net8/9/10 경고0/오류0·기존 이미지323개 동일. 고정 RC3 NativeAOT 실제 code generation과 기본/타이포그래피 두 모드의 첫 present·exit0을 확인했다. 위젯 렌더 검사는 실제 굵기·기준선·DPI·행높이·getter·벡터·명시 실패를 포함한다. 시작 성공을 공식 화면 비교로 확대하지 않는다.

Before/After·개선율: 이전 명명된 프리셋0→17종의 실제 기본 표현 연결이며, 전체 속도·Private 개선율은 미측정이다. 이번 단계에서 동일 조건 성능 재측정이 필요할 만큼 엔진 최적화를 주장하지 않는다. 여러 줄 substring·기존 글꼴 face 수명·한국어 Semibold 차이·기본 Text 전환·장식/정렬/절단·UIA·공식 시각 일치가 남았다. 과거 GPU 실패와 ID 캐시 상주 비용도 미해결이다.

명령·근거: [한영 타이포그래피 기록](fluent2-typography.ko.md), artifacts/fluent-typography-2026-11-05/verification.json, native-gallery-run.json, image-comparison.json, source-hashes.json. 고정 게시물은 native-gallery-rc3/net11.0-windows7.0/widget_tree_theme_fba.exe에 --typography를 전달한다. 모든 기존 미커밋 변경과 비교 실행 파일을 보존했다.

## 공통 키보드 입력·포커스와 불필요한 프레임 요청 수정

변경: 생성된 기본/사용자 컨트롤의 Tab·Enter/Space·포커스 표시·취소·기존 item 상태를 공통 입력 경로에 연결했다. Checkbox/Switch는 생성 시 Space 전용 동작 타입을 고정한다. 탭 순서 두 버퍼의 참조 교체·제거·120프레임 축소·Dispose를 구현했다. 전체 입력 종류·포커스 범위 통합은 남아 있다.

발견·수정: 새 코드의 null 포커스 비교가 정지 상태에서도 매번 프레임을 요청했다. 수정 전 실패/수정 후 통과하는 회귀를 추가해 실제 소유 포커스 해제에서만 요청하도록 고쳤다. 이 기능 검사를 FPS나 Private 개선율로 해석하지 않는다.

결과: .NET10 전체847→871·집중159·생성기66·net8/9/10 경고0/오류0·기존323이미지 동일. 테스트 본문을 재사용한 별도 소비 앱의24검사를 관리/고정RC3 NativeAOT에서 같은 이름/순서로 통과했다. 실제 native code generation과 소스 생성 JSON을 사용하며 테스트용 창을 열지 않는다. 직전226소스 동일·의도한10개 변경을 분리했다.

Before/After·개선율: 입력/수명 기능의 추가 검증이며 동일 조건 성능 비교는 미실행이다. 탭 순서 참조·두 배열의 상주 비용이 추가되고 큰 용량은120프레임 후 축소된다. Private/작업집합·전체 CPU 영향과 기존 회귀는 다음 측정에서 확인해야 한다. 사용자 ERD 창은 이전 게시물로 보존하며 이 실행을 최종 성능 근거로 세지 않는다.

명령·근거: [한영 키보드 기록](widget-keyboard.ko.md)의 관리/Native 소비 검사 명령, artifacts/widget-keyboard-2026-11-05/verification.json, controls-native-rc3.json, idle-before.stderr.log, image-comparison.json, preservation-audit.json. 기존 목표와 미커밋 변경을 유지한다.

## ProgressBar의 소스·표현 컴파일 연결과 가시 애니메이션

변경: 실제 상속 ProgressBar와 생성 팩터리에서 값/함수 소스·표현을 닫힌 타입으로 고정했다. 값/최댓값·shape·두께·색·불확정3초 그라데이션·모션 감소를 구현했다. 가시 불확정 상태만 애니메이션을 요청하고 기존 오버레이/콜백 API는 보존한다. UIA·전체 기본 이전·300ms 전환·정확한 곡선 마스크·공식 시각 일치는 남아 있다.

검증: NET10 전체871→898·생성기66·net8/9/10 경고0/오류0·기존323이미지 동일·새7GPU이미지/5검사. 관리/고정RC3 NativeAOT의 같은46검사에서 실제 공통 소스/입력/기하/수명 경로를 실행했다. GPU 밖 픽셀 누출0개는 사각 클립 조건이며 곡선 마스크 동치가 아니다. 초기 vertex 중심 fixture를 실제 분석적 프리미티브 기준으로 수정했다.

Before/After·개선율: 새 기능/소비 경로 검증이며 동일 조건 속도·할당·Private 개선율은 미측정이다. 별도 영구 애니메이션 캐시/구독은 추가하지 않았으나 전체 앱 비용을0이라고 주장하지 않는다. 직전232소스 동일·의도한9개 변경과 새 소스를 기록했다. 기존 메모리 비용과 RC3 전체 GPU 실패는 미해결이다.

근거·명령: [한영 ProgressBar 기록](fluent2-progress.ko.md), artifacts/fluent-progress-2026-11-05/verification.json, controls-native-rc3.json, image-comparison.json, official-source-hashes.json. 기존 FBA의 --progress 모드를 사용한다. 새 Native 갤러리는 게시만 확인했고 사용자 ERD 실행을 종료하거나 대체하지 않았다.

## ProgressBar 전환의 정적 동작 선택·소유 상태

변경: 생성된 위젯의 구체 동작 타입에서300ms CSS ease를 계산한다. 전역 애니메이션 캐시 대신 소유 값 형식 상태를 쓰며 사용자 동작의 Release를 Dispose에 연결했다. 기존 비전환 클래스에는 필드를 추가하지 않았다. 재지정은 현재 폭에서 이어가고 목표 도달/클립 밖/제거 후에는 프레임 요청을 멈춘다. 정지 상태의 곡선 계산도 생략한다.

검증: NET10 전체898→915·생성기66·net8/9/10 경고0/오류0·기존323이미지 동일. 관리/고정RC3 NativeAOT 같은62검사, 독립 역산4,097표본 오차1e-6 미만, 종료 시각 반올림·사용자 해제·GPU 시작/최종 폭을 검사했다. 초기 nullable 경고는 수정하고 최종 Native 로그 경고0을 확인했다. 처음60검사 게시물과 중간 결과는 최종 결과와 분리했다.

Before/After·비용: 동일 Parent 정체성1,024개 구성에서106,496→131,072B(+24,576B, +23.08%, 개당24B)를 관리/Native 모두 기록했다. 필요한 전환 상태의 추가 비용이며 전체 Private나 성능 개선으로 세지 않는다. 속도 비교는 미측정이다. CSS 반전 단축·곡선 마스크·공식 전체 적합성과 기존 공통 엔진/메모리 목표는 남아 있다.

근거: artifacts/fluent-progress-motion-2026-11-05/verification.json, controls-verified-native-rc3.json, image-comparison-final.json, source-hashes.json과 [한영 상세](fluent2-progress.ko.md). 현재 갤러리 게시물에 --progress를 전달하면 된다. 실행 중인 사용자 ERD는 교체하지 않았다.


## 연속 크기 변경의 GPU·표시 자원 수명

사용자는849A·7E1B에서 멈춤 해소를 확인했지만 WinUI보다 반응이 느리다고 답했다. 새 경로는 GPU 전체 대기를 제거하고, 이전 타깃은 마지막 그래픽 제출의 완료 뒤 회수한다. 지원 장치에서는 swapchain maintenance의 표시 fence를 사용하고, 미지원 장치에서는 기존 새 이미지 재획득 증명을 유지한다. 표시 fence는 실제 화면 표시 완료 시각을 측정하는 장치가 아니다. [공식 표시 fence 계약](https://docs.vulkan.org/refpages/latest/refpages/source/VkSwapchainPresentFenceInfoKHR.html)을 따른다.

MSAA4 타깃은 크기 변경 중 충분한 용량을 재사용한다. 새 용량은64픽셀 단위로 확보하고 물리 장치의 이미지 크기 한도를 지키며, 크기 변경 종료 후 실제 크기로 축소한다. 렌더 영역·픽셀 해상도·MSAA 샘플 수는 그대로다. 용량의 픽셀 동치와 종료 후 축소를 GPU로 검사했다.

추가 후보에서 드물게 vkCreateSwapchain 내부의 GPU 동기화 대기를 재현했다. 그래픽 제출 완료를0시간 제한으로 확인한 후에만 재생성에 들어가며, 준비되지 않은 경우 다음 최신 프레임으로 재시도한다. 대기 중인 재생성은 idle skip으로 사라지지 않는다. 테스트 전용 busy 주입은 실제 재생성에 진입하지 않고도 이 계약을 검사한다.

고정 RC3 NativeAOT, FIFO/VSync·MSAA4·프레임 자원2개·300ms 준비·3×48개 크기 변경을8ms 간격으로 주는 조건에서 독립3회씩 교대 비교했다. 완료된 재생성 단계 p95의 실행별 중앙값은19.9392→7.7539ms(61.11% 감소), 범위는14.1804–23.6590→7.4340–7.9389ms다. 이는 전체 앱 지연·실제 표시 FPS·WinUI 동등성의 증명이 아니다. 마지막 활성 표본 Private 중앙값은416,411,648→372,330,496B였으나 실행 범위가 겹쳐 메모리 개선으로 확정하지 않는다.

근거: `resize-ready-comparison.json`, `ready-pixel-comparison.json`, `gallery-ready-resize/result.json`, `verification.json`. 실제 갤러리288회 크기 변경은 exit0으로 종료했다. 앱 프레임 시도30,838건은 실제 표시 프레임 수가 아니다. 새 게시물 SHA52493AB5…의 사용자 체감 확인은 별도로 진행한다. deferred-memory-allocation 실험은 DeviceLost가 발생해 사용하지 않는다.


## DirectText 공통 CPU·메모리 및 입자 정밀도 검증

[DirectText 집중 최적화](directtext-layout.ko.md), [입자 접촉 판정 수정](particle-contact-precision.ko.md). 제품 셰이더 반영 후.NET10 전체1035개와 고정RC3 Native 입자19개 통과. 텍스트 CPU/Private 수치는 전체 앱 완료를 뜻하지 않는다. 전체 목표·크기 변경 실제 체감 검증은 계속 진행한다.


## 실제 외곽 크기 변경 회귀 수정

[WM_SIZE와 프레임 제출 동기화](windows-resize-coherence.ko.md).371139A9는 사용자 확인 실패, DCA37C18은 ‘검정 영역 없이 빠르고 자연스러움’ 확인. 단순 대기 복구 대신 확정 크기·WSI 동기 메시지 처리·종료 취소를 결합했다. 자동 제출 순서 검사에서 no-wait 대조군 실패. 정량 input-to-photon 또는 WinUI 속도 동등성은 측정하지 않았다. Native 입력 한 차례 시간 초과는 추가 통과와 구분해 미확정으로 남긴다.


## Badge·CounterBadge 공통 구현

[동작·표현·생성 경로와 검증](fluent2-badge.ko.md). 동작별 값 형식 상태로 정적 배지의 불필요한 숫자 캐시를 제거하고99+ 유지 시 재포맷하지 않는다. dot은 폰트 측정 없이 공통 벡터 기하로 그린다. .NET10 전체1095/집중49, 고정RC3 Native243×EXT/core, 기존/현재 GPU 이미지 동치와 실제 --badge 게시물 첫 프레임·exit0 확인. 공식 화면/UIA/RTL 및 전체 컴포넌트 목표 미완료. 새 앱 전체 Private/FPS 개선율을 측정했다고 주장하지 않는다.
