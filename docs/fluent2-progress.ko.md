# 생성된 ProgressBar 위젯

## 읽기 전용 숫자 접근성 — 2026-11-14

생성된 Fluent 위젯을 포함한 모든 `UiProgress` 소스·표현·동작 조합이 고정 식별자의 ProgressBar 의미 요소를 게시합니다. 용도는 `AccessibleName`으로 지정하세요. 확정 진행률은0부터Maximum까지의 읽기 전용 범위와 보정한 실제 소스 값, 조정 증분0을 제공합니다. 보간은 그리기에만 적용합니다. 접근성에는 목표 값을 즉시 전달하며 시각적 프레임마다 범위 이벤트를 발생시키지 않습니다. 미확정 진행률은 요소 식별자를 유지하면서 숫자 범위를 생략하고 확정 값으로 돌아오면 복원합니다. 화면 밖에서도 메타정보는 유지하지만 애니메이션 프레임은 예약하지 않습니다.

접근성이 필요한 즉시 렌더링은 `ui.ProgressBar<TPresentation>(id, value, size)` 또는 `ui.ProgressBar<TBehavior, TPresentation>(id, ref behavior, value, size)`를 사용합니다. ID는 비어 있지 않고 같은 범위에서 고유해야 합니다. 기존 ID 없는 오버로드는 의미 식별자 없는 그리기 호출을 유지하므로 여러 번 호출해도 충돌하지 않습니다. 보조 도구가 진행률을 찾을 수 있어야 한다면 명시적 오버로드나 유지되는 위젯을 사용하세요. 부모가 소유한 자식은 기존 자식 키를 재사용하며 새 식별자 필드나 전역 사전을 추가하지 않았습니다. 접근성 트리가 없으면 의미 식별자를 계산하지 않습니다.

관리형 .NET11 컨트롤95건에서 목표 값과 표시 값, 변경된 범위의 게시, 보정, 읽기 전용 쓰기 거부, 미확정 모드, getter 한 번 읽기, 명시적 ID, 부모 소유 형제와 화면 밖 대기를 검사합니다. 실제 Linux 앱35건과 실행 경로별 독립 AT-SPI32건에서는 네이티브 읽기 전용 범위와 캐시를 지우지 않는 Value 인터페이스 제거·복원도 확인합니다. Windows UIA는 기존 공통 범위 계약을 소비하지만 이번에 새로 실제 화면 읽기 프로그램이나 Windows 네이티브 실행을 검증했다는 뜻은 아닙니다. 근거는 `artifacts/linux-accessibility-2026-11-14/progress-verification.json`입니다. 전체 진행률·플랫폼 적합성, 고정 RC3 NativeAOT와 하드웨어 검증은 남아 있습니다.

## 부모 할당과 순수 측정 — 2026-11-10

Progress의 측정과 렌더가 실제 부모 루트를 반영합니다. `IUiProgressPresentation.Arrange`로 변경된 할당을 명시적으로 처리하며 값 기반 측정 중 미지원/잘못된 구현을 거부합니다. Fluent는 실제 너비·높이로 트랙·채움 비율·이동 그라데이션·펄스를 그립니다. 자연 두께는 2/4px이며 명시 크기나 강제 부모 할당은 그 높이를 덮어씁니다. 실제 루트로 그리기를 클립합니다. 기존 즉시 오버로드는 자연 크기를 유지하고 마지막 `constrainLayout` 인수로 사용자 컨트롤이 할당에 참여합니다.

`IUiProgressSource<T>.Measure<TPresentation>`는 별도의 정적 순수 측정 계약입니다. 정적 값은 실제 모양·두께 정보를 사용합니다. getter 소스는 측정 중 렌더 콜백을 실행하지 않으며 Fluent는 `Size.Y` 또는 강제 부모 높이를 요구합니다. 높이가 불명확하면 Medium으로 추정하지 않고 실패합니다. 사용자 소스는 선언한 배치 메타데이터로 순수 측정을 구현하고 `Read`는 렌더 전용으로 유지할 수 있습니다. 해당 메타데이터·표현은 실제 소스의 배치 계약과 일치해야 합니다. 위젯 필드·배열·런타임 등록부·값 스냅샷은 추가하지 않았습니다. 측정은 전환 동작을 실행하지 않고 getter는 렌더마다 한 번 읽습니다.

```csharp
var progress = Fluent.ProgressBar(() => new(value.Value, 100));
var allocated = Dux.SizedBox(progress, 80, 64);
// For natural container layout, declare the getter's height instead:
var natural = Fluent.ProgressBar(() => new(value.Value, 100));
natural.Size = new(0, 4);
```

사용자 표현은 값 없이 측정하는 `Measure(context, size)`와 정적 `Arrange`를 구현할 수 있으며 사용자 컨트롤도 `UiControlContext.MeasureProgress<TPresentation>`를 공유합니다. 요청 크기는 유한한 0 이상의 값이어야 합니다. 0 크기는 보이는 애니메이션을 요청하지 않습니다. 기존 300ms 전환·논리적 소스 값·해제를 보존합니다.

원본에서 정적 측정 예외와 부모 80x64 대비 실제 220x4 루트를 재현했습니다. .NET11 RC3 관리 검증은 CPU 60개·신규 할당 픽셀 정확 비교 236개·소스 읽기 횟수·기존 전환/GPU 검사를 포함해 306개가 통과했습니다. MSAA1/4·Light/Dark·프레임 슬롯3·readback465개로 동적 부모 변경·색·모양·끝값·불확정 모션·생성 사용자 표현·획득 이미지 재생을 검사했습니다. 빌드 경고·오류는 0개입니다. C++ Build Tools 부재로 Native는 미검증입니다. 근거: `artifacts/progress-allocation-2026-11-10/verification.json`. 기능 수정이며 FPS·Private·할당 개선은 미측정입니다. 전체 목표 수치와 리사이즈 보류를 유지합니다.

로컬 샘플: `samples/fba/layout_progress_allocation_fba.cs`(Width/Height·실제 Slider·공유 값). 현재 .NET11 로컬 소스가 필요하며 기존 NuGet 패키지에 게시된 API/샘플은 아닙니다. [검증한 샘플 실행](widget-gallery-run.ko.md).


Last synced: 2026-11-14

`Fluent.ProgressBar(float? value, float maximum = 1)`과 `Fluent.ProgressBar(Func<UiProgressValue>)`는 생성된 상속 위젯을 반환한다. 문자열 이름 탐색이나 런타임 구현 레지스트리를 사용하지 않는다. `UiProgress<TSource,TSourcePolicy,TPresentation>`은 값/함수 소스와 표현 구현을 닫힌 타입으로 연결하며 함수는 렌더마다 한 번 읽는다. 사용자는 기본 위젯을 상속하거나 `IUiProgressPresentation`으로 측정·배치·그리기 알고리즘을 바꿀 수 있다.

## 계약과 실제 구현

[공식 ProgressBar 지침](https://fluent2.microsoft.design/components/web/react/core/progressbar/usage)과 microsoft/fluentui 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 react-progress API·스타일·모션·색 원본을2026-11-05 대조했다. API의 public 표시는 확인했지만 패키지 릴리스 버전/채널 전체 검토는 계속 필요하다. 문서의 Preview 제목을 릴리스 채널로 해석하지 않는다.

- Value/Maximum을0~1 비율로 제한한다. null은 불확정 상태다. NaN·무한대·0 이하 Maximum과 잘못된 enum은 명시적으로 거부한다. 원본 웹의 잘못된 입력 보정 대신 오류를 드러내는 계약이다.
- Rounded/Square, Medium2px/Large4px와 Brand/Success/Warning/Error를 제공한다. 상태색은 공식 palette의 primary 값(#107c10/#da3b01/#d13438)이며 양쪽 색상 테마의 Background3 대응을 확인했다. Brand/배경은 현재 Duxel 테마를 사용하므로 공식 전체 팔레트 일치를 보장하지 않는다.
- 불확정 상태는33% 폭의 실제 그라데이션을3초 선형 주기로 이동한다. ReducedMotion은 전체 폭의 불투명도를0.2~1로 왕복한다. root와 현재 클립의 교차 영역을 사용하고 white 텍스처를 명시적으로 선택·복원한다.
- 보이는 불확정 상태만 애니메이션을 요청한다. 클립 밖/확정된 정지 상태는 렌더 루프를 계속 깨우지 않는다. 별도 영구 애니메이션 캐시나 구독을 만들지 않는다.

타입이 지정된 즉시 API `ui.ProgressBar<UiFluentProgressPresentation>(state, size)`와 생성된 위젯은 같은 커널을 사용한다. 이전 `ui.ProgressBar(float, size, overlay)`와 `Dux.ProgressBar(Func<float>, size, overlay)`의 기존 표현·오버레이·콜백은 보존했다. 기존 표현은 위치 배치 도우미를 공유하지만 아직 새 기본 Fluent 표현으로 바뀐 것은 아니다. 이 호환 경로의 비노출·이전도 전체 목표의 미완료 범위다.

## 실행과 검증

기존 `widget_tree_theme_fba.cs`에 `--progress` 모드를 추가했다. 슬라이더 진행값, 세 상태색, 불확정 이동과 모션 감소 표시를 구성한다.

```powershell
./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -NoLaunch
# 출력된 실행 파일에 --progress 전달. 아래는 보존된 고정 RC3 게시물이다.
& './artifacts/fluent-progress-2026-11-05/native-gallery-rc3/net11.0-windows7.0/widget_tree_theme_fba.exe' --progress
```

.NET10 전체898·생성기66검사, net8/9/10 솔루션 경고0·오류0이다. 기존323이미지는 동일하고 새 GPU 이미지7개를 생성했다. 생성/즉시 경로의 GPU 픽셀 동치, 값/색 변경, 이동 그라데이션의 영역 밖 픽셀0개와 모션 감소 프레임을 검사했다. 실제 GPU 이미지도 확인했으며 공식 브라우저 참조 화면과의 비교는 아니다. 초기 검사는 사각형/원이 vertex 배열에 있다고 잘못 가정했으므로 실제 분석적 프리미티브를 검사하도록 고쳤다.

창 없는 공통 소비 앱에서 관리/고정RC3 NativeAOT의 동일46검사(기존 키보드24+진행 표시22)를 통과했다. Native SDK11.0.100-rc.3.26522.148·런타임11.0.0-rc.3.26522.148·win-x64·Speed와 실제 native code generation을 확인했다. 갤러리 FBA도 NativeAOT 게시했으며 이번 단계의 실제 GUI 시작/사용자 입력 검증으로 확대하지 않는다. 사용자가 연 ERD 창과 과거 실행 파일은 보존했다.

## 후속: 위젯이 소유하는300ms 전환

`UiFluentProgressBehavior`를 생성 시 연결하여 확정값의 폭을300ms CSS ease로 전환한다. [W3C ease 정의](https://www.w3.org/TR/css-easing-1/#valdef-cubic-bezier-easing-function-ease)의 제어점(0.25,0.1,0.25,1)을 사용한다. 중간 목표값 변경은 현재 표시 폭에서 이어가고, clamped value가0.01 이하이면 즉시 재설정한다. 처음 표시와 불확정 상태에서의 복귀는 새 전환을 만들지 않는다. 표시 값이 목표에 도달하면 부동소수점 종료 시각 차이로 불필요한 프레임을 더 요청하지 않는다.

동작·소스·표현은 `UiProgress<TSource,TSourcePolicy,TBehavior,TPresentation>`으로 분리한다. 문자열 이름이나 실행 중 테마 분기 없이 정적 동작 타입을 선택한다. `IUiProgressBehavior<TSelf>`의 Evaluate·Release는 위젯이 소유한 값 형식 상태를 처리·해제한다. 사용자 동작도 같은 경로로 연결하며 Dispose에서 Release를 한 번 호출한다. 기존 `UiProgress<TPresentation>`/3인수 generic은 애니메이션 필드 없이 즉시 표시 계약을 유지한다. 표시용 복사본을 보간하며 원래 Source의 목표값은 바꾸지 않는다.

직접 즉시 API를 쓸 때는 프레임 밖에 상태를 유지하고 `ui.ProgressBar<UiFluentProgressBehavior,UiFluentProgressPresentation>(ref state,value,size)`를 사용한다. 매 프레임 새 상태를 만들면 전환도 새로 시작한다. Build의 keyed child는 생성된 위젯과 그 상태를 함께 유지한다.

추가 검증은 .NET10 전체915·관리/고정RC3 NativeAOT 공통62검사다. 독립적인 곡선 역산과4,097표본에서 오차1e-6 미만, 재지정 연속성·정확한 종료·해제·클립 밖/제거 후 프레임 정지와 GPU 전환 시작/최종 폭을 검사했다. 기존323이미지가 같다. 초기 Native 테스트의 nullable 경고는 수정했고 최종 게시에 경고가 없다. 최초60검사 게시물은 최종 결과와 분리 보존한다.

동일한 Parent 정체성·1,024개 구성에서 비전환106,496B→전환131,072B로24,576B(+23.08%, 위젯당24B)가 추가됐다. 관리/Native 모두 같은 값이다. 생성 상태의 보관 비용이며 Private·앱 FPS·기본 Instance 정체성 전체 비용의 증거가 아니다. 전역 애니메이션 사전·숨은 구독을 추가하지 않았으며 정지 상태에는 곡선 계산을 생략한다. 속도 개선율은 미측정이다. 새 근거는 `artifacts/fluent-progress-motion-2026-11-05/`를 따른다.

## 공통 둥근 그라데이션 후속

생성/즉시 불확정 ProgressBar를 [공통 그라데이션 도형](rounded-gradients.ko.md)에 연결했다. 배경·이동 색상을 한 인스턴스로 그리고 분석적 둥근 경계로 마스킹한다. 아래 직전 단계의 마스크 미완료 상태는 이 후속으로 해소했다. 전체 공식 시각 적합성·모션·팔레트·UIA는 여전히 남아 있다. NET10전체954·관리/RC3 GPU45·입력62검사와 같은 크기 원본 셰이더323이미지 동치를 확인했다. 전체 앱 성능 개선으로 세지 않는다.

## 남은 범위

CSS 역방향 전환의 기간 단축 규칙, 전체 색상 토큰, OS 모션 감소/고대비 설정 연결, Field 의미 정보·UIA와 공식 시각/모션 일치가 남아 있다. 둥근 마스크나 일반300ms 곡선 검사를 모든 브라우저 모션 규칙의 일치로 승격하지 않는다. 카탈로그23이름·알려진 요구289·누락266은 최종 전체 지원률이 아니다.

속도·할당·Private 개선율은 이번 단계에서 측정하지 않았다. 기존 메모리 상주 비용·RC3 전체 GPU 비교 실패·전체 Fluent/기본 Text·Pi/Linux/ARM64 목표를 유지한다. 근거는 `artifacts/fluent-progress-2026-11-05/verification.json`, controls-native-rc3.json, image-comparison.json, source-hashes.json과 공식 원본 파일들이다.

2026-11-12 후속 — [콘텐츠 측정 계약](widget-cpu-retention.ko.md): 결정론적 소스·표현은 부모 재사용에 참여하고 동적 문자열/배지 getter와 사용자 특성은 기본 재측정합니다. getter Progress의 선언 기하와 Spinner의 안정된 표시는 별도 약속이며 그리기·getter·애니메이션·이동 처리는 계속 실행합니다. 집중26개와 실제 GPU 비교는 이 범위의 근거이며 전체 NativeAOT·성능 완료가 아닙니다. 근거: artifacts/content-layout-retention-2026-11-12/verification.json.
