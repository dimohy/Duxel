# Fluent 2 Field

`Fluent.Field(control, controlSize, options)`는 실제 `UiWidget`을 소유하고 라벨·필수 표시·검증 메시지·도움말을 구성한다. `Dux.Field`와 직접 `ui.Field<TBehavior, TPresentation>`도 같은 구현을 사용한다. 기본 위젯과 사용자 위젯은 같은 소유권·입력·그리기 경로를 사용한다.

```csharp
var field = context.Child("email", () => Fluent.Field(
    Fluent.Input("Email", email.Binding, 256, 220),
    new(220, 32),
    new() { Label = "Work email", Hint = "Use your work address", Required = true }));
field.Options = field.Options with {
    ValidationMessage = email.Value.Length < 3 ? "Enter at least three characters" : null
};
```

자식은 Field에 한 번만 소유시킨다. 같은 입력을 바깥 `context.Child`에도 등록하면 공유 소유권으로 거부된다. Field를 첫 렌더 전에 해제해도 자식이 해제된다. 자식 교체는 새 키의 Field로 구성한다.

`UiField<TBehavior, TPresentation>`을 상속하거나 `UiWidgetStyle`로 닫힌 타입을 생성할 수 있다. 기본 `UiFluentField`는 `UiLabelFieldBehavior`와 `UiFluentFieldPresentation<UiFluentMediumFieldSize, UiVerticalFieldLayout>`을 생성기로 연결한다. 세 라벨 크기와 가로/세로 배치를 타입으로 선택하며 런타임 테마 레지스트리를 사용하지 않는다.

라벨 클릭은 자식의 실제 포커스 경로로 전달된다. Input에서는 앞쪽 버튼보다 본문 편집기에 먼저 전달되고 버튼에서는 활성화 없이 포커스만 이동한다. 키보드와 콜백은 기존 컨트롤이 처리한다. `Options.Disabled`는 자식만 비활성화하며 라벨·설명은 읽을 수 있게 유지한다. 외부 비활성 범위도 입력을 차단한다.

검증 상태는 `None`, `Error`, `Warning`, `Success`다. 상태를 생략하고 비어 있지 않은 메시지를 지정하면 오류다. 오류는 Input의 원래 Options를 변경하지 않고 유효성 표현으로 전달된다. `ui.CurrentField`와 `UiControlContext.Field`는 사용자 컨트롤에도 라벨·설명·필수·검증 정보를 제공하고 중첩/실패 시 복원된다. [공통 접근성 공급자](widget-accessibility.ko.md)는 이제 주 Input의 메타데이터를 실제 Windows UIA로 제공한다. 외부 관리/NativeAOT 클라이언트가 검증 변화·물리 좌표를 포함한 Field14개 사례를 확인한다. 이벤트와 화면 읽기 알림은 남아 있다.

기존 명시적 할당 API와 함께 `Fluent.Field(control)` 자연 측정을 제공한다. Width는 바깥 Field 너비이고0이면 가용 폭을 사용한다. 기본 폼 컨트롤과 사용자 측정 구현의 실제 높이를 사용한다. 전체 컨테이너 제약 협상은 남아 있다. 가로 배치는 공식33% 라벨 열을 사용하며 설명은 컨트롤 열 아래에 배치한다. [공통 측정 계약](widget-measurement.ko.md)을 참고한다.

공식 표현은 측정한 줄을 위젯별 캐시에 유지한다. 입력 문자열 합계1024자·파생 문자열4096자·256줄·각 폰트 경로1024자까지 보유한다. 범위를 넘는 내용도 같은 품질로 렌더링하고 프레임 뒤 보유하지 않는다. 너비·옵션·폰트·텍스트 설정·배율·백엔드 세대·컨텍스트 변경은 무효화하며 해제 시 참조를 정리한다. 컨텍스트는 약한 참조다. 직접 API에서 재사용하려면 `UiFieldContent<TPresentation>`을 소유하고 해제한다. 추가 캐시와 레이아웃 값의 비용은 있으며 앱 CPU·Private·FPS 개선을 아직 주장하지 않는다.

공식 기준은 [Field Preview](https://fluent2.microsoft.design/components/web/react/core/field/usage)와 Fluent UI 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`다. 실제 `useField.tsx`가 선택하는 오류 아이콘은 `DiamondDismiss12Filled`다. 타입 문서의 오래된 ErrorCircle 주석과 구분한다. 이 아이콘 U+F0A11을 연결하면서 Windows 텍스트 경로의 BMP 제한을 수정했다. DirectWrite의 UCS4 입력과16비트 글리프 인덱스는 서로 다른 범위다. 래스터·벡터 모두 실제 보충 평면 글리프를 사용한다.

초기 근거는 `artifacts/fluent-field-2026-11-06/`이며 자연 측정과 라벨 열 클립의 최신 근거는 `artifacts/widget-measurement-2026-11-06/`다. .NET10 입력·소유권·캐시·UCS4와 전체 검사를 통과했다. 화면 검사는 세 크기×두 방향×다섯 상태×두 테마×MSAA1/4×래스터/벡터의 실제 GPU readback으로 생성/직접 경로를 비교한다. 공식 브라우저 화면과의 동치 검사는 아직 아니다. 전체 공식 룩앤필·UIA·RTL·강제 색상·모션·정보 버튼/임의 슬롯·전체 컨테이너 측정·Pi 검증은 남아 있다.

```powershell
.\run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -Managed -DotnetPath 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' -ExtraArgs @('--field')
```

## 부모 할당 — 2026-11-10

생성된 Field 위젯이 실제 부모 할당에 참여합니다. 루트·실제 컨트롤·라벨 타깃·설명·루트 클립이 지정 영역을 공유합니다. 가로 33/67 열을 포함한 실제 너비를 텍스트 측정 전에 반영하며 라벨/설명 행을 예약한 나머지 높이를 실제 컨트롤에 줍니다. 폰트·아이콘은 유지하고 짧거나 0인 부모는 기존 콘텐츠를 자릅니다. 선언한 Width/ControlSize는 바꾸지 않습니다.

```csharp
using var field = Fluent.Field(Fluent.Input("Email", email.Binding, 128));
field.Options = new() { Label = "Email", Required = true, Hint = "Work address" };
using var box = Dux.SizedBox(field, 180, 120);
```

IUiFieldPresentation.Arrange(context, options, natural, allocatedSize)가 변경된 부모 크기를 명시적으로 수용합니다. 제약된 명시적 ControlSize에는 라벨 열을 포함한 자연 외부 폭 FieldWidth(controlWidth)를 선언하며 기본 구현은 미지원 예외를 냅니다. ControlWidth(fieldWidth)는 정방향 계산입니다. 기본 가로 표현은 둘 다 구현하며 자연 자식 모드는 이미 알려진 외부 Width로 시작합니다. 잘못된 폭/크기·다른 루트를 반환하는 배치는 라벨/입력 동작 전에 실패합니다.

기존 제한된 UiFieldContent는 실제 컨트롤 너비의 자연 기하를 캐시합니다. Arrange는 지역 복사본만 바꾸므로 부모 높이만 바뀌면 Field 텍스트를 다시 측정하지 않습니다. 공개 UiControlContext.MeasureField(control, controlSize, width, options, content)로 사용자 래퍼도 같은 순수 경로를 사용합니다. 새 즉시 Field/AutoField 오버로드는 content 뒤 명시적 constrainLayout을 받습니다. 기존 즉시 오버로드와 독립 자연 렌더는 이전 동작을 보존하며 실제 자식 늘리기는 부모 할당이 활성화된 경우에 적용합니다.

측정은 입력 바인딩을 읽거나 입력/이벤트를 실행하지 않습니다. required/invalid/disabled/name/hint가 실제 편집기에 전달되고 Core 접근성 경계도 이를 반영합니다. 부모 변경 뒤 라벨 포커스·타이핑·undo·편집기 정체성을 유지합니다. 중첩 Field는 가장 가까운 메타데이터를 쓰고 외부 스코프를 복원합니다. 임의 IUiView의 독립 Panel은 Open 바인딩을 읽기 전에 일반 말단 측정을 거부하며 닫힌 독립 루트 동작은 보존합니다. 이 경계 검사가 모든 부동 패널 수명을 검증한 것은 아닙니다. 위젯/컨텍스트 필드·새 캐시·런타임 등록부·소스 스냅샷 추가 없음.

고정 .NET11 RC3 Field 관리 1018검사 통과: CPU/의미/수명 64, 기존 Field GPU 480, 신규 할당 픽셀 432, 서로 다른 유지주기 픽셀 40, 재사용 횟수 2. 세 라벨 크기·양 방향·자연/명시 크기·빈 검증 아이콘·비활성·짧은/0 경계·래스터/벡터·MSAA 1/4·Light/Dark·슬롯 3, 준비 포함 readback 1425개입니다. FPS16/NF3 라벨/설명 갱신과 루트 클립 변경도 정확히 재생합니다. Field를 추가한 표/열 확대 회귀는 1563검사(CPU1115, GPU448, readback897) 통과. 두 검증 범위를 고유 요구사항 수로 합산하지 않습니다.

원본 진단의 부모 측정 80 x 64·실제 80 x 32가 같은 진단에서 실제 80 x 64로 바뀌었습니다. 새 layout_field_allocation_fba.cs는 생성된 세로·사용자 생성 가로 Field, Width : 180, Height : 180, Validation을 제공합니다. 지시문 외 동일본문 로컬 참조 빌드·첫 표시 543.94ms·15초 exit0 확인. 초기 로컬 FBA 참조의 Analyzer 메타데이터 누락은 sample-validation.targets에서 표준 OutputItemType=Analyzer/ReferenceOutputAssembly=false를 참조 해석 전에 지정해 해결했고 생성된 AllocationHorizontalField 원문도 보존합니다. 샘플을 통과시키려고 생성기 구현을 바꾸지 않았습니다.

근거: artifacts/field-allocation-2026-11-10/verification.json, qualified-build.log, managed-qualified/result.json, routing-regression/result.json, sample-generated. 수동 OS 입력·실제 Windows UIA·공식 외형·현재 Native/런타임 비교는 남아 있습니다. Native 표준 경로에서 도구를 확인하지 못했고 대체 L: 경로는 기존 감사와 같이 열거 접근 거부 상태입니다. 성능·앱 전체 메모리 개선은 주장하지 않습니다. 컨테이너 3/6·리사이즈 사용자 보류·.NET9/10 최종 배포 컴파일 보류 유지.

제약 측정에서는 자식의 자연 높이를 무제한 축에서 구하므로 축소 Column과 세로 스크롤용 무제한 높이를 지원합니다. 두 원본 실패를 추가 재현했고 임의 높이를 만들지 않고 MeasureConstrained의 자연 높이를 사용했습니다. 자연 Field의 외부 폭은 유한하거나 Width/ControlSize로 선언해야 합니다.

## 사용자 정의 측정 특성 — 2026-11-12

`IUiFieldSize.CanRetainMeasurement`와 `IUiFieldOrientation.CanRetainMeasurement`의 기본값은 false입니다. 제네릭 Fluent 표현은 두 특성이 모두 측정의 안정성을 명시할 때만 기존의 제한된 자연 레이아웃을 재사용합니다. 내장 크기와 방향은 이 계약을 선언합니다. 외부에서 간격이나 방향이 바뀌는 사용자 정의 특성은 다시 측정하며, 내장 캐시 계약을 자동으로 상속하지 않습니다. 기존 입력 렌더링과 소유권은 계속 유지합니다.


## 부모 측정 재사용 — 2026-11-12

Field는 표현의 ReuseLayout과 소유한 컨트롤의 RetainsMeasurement가 모두 허용할 때만 부모 측정 재사용에 참여합니다. 명시적인 ControlSize만으로 임의 자식 측정을 건너뛰지 않습니다. 자연 크기 Field 렌더링은 이제 측정 경로와 검증된 자식 크기를 공유합니다. 변하지 않은 알려진 소유 컨트롤은 결과를 재사용하고, 불투명한 컨트롤과 기존 즉시 모드 AutoField는 계속 측정합니다. 렌더링·필드 메타데이터·포커스·편집은 계속 처리합니다.

근거: artifacts/form-layout-retention-2026-11-12/verification.json. 관찰: ./run-gallery.ps1 → 측정 재사용.
