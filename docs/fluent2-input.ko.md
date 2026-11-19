# Fluent 2 Input

시스템 대비 팔레트가 활성화되면 선택된 일반 텍스트에 HighlightText/Highlight를 적용합니다. 공통 편집기는 원래 조판 결과의 구간을 겹치지 않게 그려 선택 밖의 글자색을 유지하고 글자 가장자리의 중복 합성을 피합니다. [적용 범위·GPU 검증·스타일 및 IME 한계](system-forced-colors.ko.md).

`UiFluentInputPalette.Resolve(context, state, appearance, invalid, held)`는 활성 시스템 대비 색상을 사용합니다. 일반 입력은 Canvas/CanvasText와 포커스 밑줄의 Highlight를, 비활성 입력은 GrayText와 투명 배경을 사용합니다. Underline 배경도 투명하게 유지합니다. Input 장식, Dropdown 화살표, SearchBox 아이콘은 같은 시스템 전경색을 공유합니다. 색상 모드만 받는 기존 오버로드는 지정된 Fluent 색상을 계산합니다. [공통 계약과 남은 범위](system-forced-colors.ko.md).

[한 줄 드래그](input-drag.ko.md): 입력에서 누른 드래그는 영역 밖 선택·가로 자동 스크롤을 이어가고 해제·숨김·클립/비활성·상위 팝업 차단 시 종료합니다. CPU336·입력커널400 통과, 이전 입력357장·다중 행3457장 일치. 실제 OS 캡처와 전체 텍스트/Native 일치는 미완료입니다.

[공통 키보드 명령](text-commands.ko.md): Ctrl+Backspace/Delete와 반복은 온전한 단어 범위를 삭제하고 다중 행 Left/Right는 선택을 접습니다. CPU301·입력커널304·다중행2336 통과이며 전체 네이티브/Unicode/OS 일치는 미완료입니다.

공통 [한 줄 원문 요소 규칙](single-line-elements.ko.md)으로 온전한 이동/삭제·포인터 정렬·방향 선택 접기·반복을 연결했습니다. 통합 CPU231·입력커널240 통과. 샘플: `fluent_input_elements_fba.cs`이며 전체 셰이핑/bidi/OS 일치는 미완료입니다.

Last synced: 2026-11-16

현재는 아래의 실제 자식 슬롯도 지원한다. 초기 비대화형 구현과 이전 검증 수치는 기록 시점을 구분해 읽는다.

한 줄 입력은 기본·사용자 위젯과 직접 API에서 공통 편집 커널을 사용한다. 기본 `Fluent.Input`은 소스 생성기로 중간 크기·Outline 표현에 고정된다. 사용자 표현도 같은 `UiInput<TBehavior, TPresentation>`를 상속하고 입력 저장소의 소유 수명을 사용한다.

```csharp
var field = Fluent.Input("Name", state.Binding, maxLength: 256, width: 220);
field.Options = new(UiInputTextFlags.AutoSelectAll, Placeholder: "Type here");
IUiView view = Dux.Input("Name", state.Binding, 256, 220);

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior),
    typeof(UiFluentInputPresentation<UiFluentLargeInputSize, UiFluentFilledDarkerInput>))]
public partial class ProductInput;
```

`Label`은 정체성/이름에 사용한다. `Fluent.Field`로 보이는 라벨·힌트·필수/검증 메타데이터와 기본 컨트롤의 접근성 연결을 구성할 수 있다. placeholder는 필수 라벨을 대신하지 않는다. `Value`, `MaxLength`, `Width`, `Options`를 변경할 수 있고 상속 클래스는 `OnChanged(string)`을 확장할 수 있다. Build에서는 `context.Child`로 인스턴스를 유지한다. 해제하면 바인딩과 입력 버퍼·선택·커서·undo/redo 기록을 놓는다.

공통 [텍스트 편집 무결성 규칙](text-edit-integrity.ko.md)이 들어오는 UTF-16 길이 제한과 외부 모델의 undo 기준점을 정의한다. 접근성 Value는 되돌릴 수 있는 편집으로 유지한다. 한 줄/다중 행을 합친 무결성 CPU175 통과이며 전체 Unicode 편집과 실제 OS 입력은 미완료다.

동작은 Tab/Shift+Tab 포커스, 포인터 선택·드래그, 클립보드, undo/redo와 IME를 공통 구현에서 처리한다. Enter/Escape는 Fluent 입력의 포커스를 유지한다. ReadOnly는 선택·복사·이동을 허용하고 변경·붙여넣기·잘라내기·IME를 막는다. AutoSelectAll은 새 포커스 때 전체를 선택한다. 비활성·Tab 이동 시 IME 소유자를 해제한다. 실제 OS 후보창 검증과 대역 검증은 구분한다.

Small/Medium/Large는 높이24/32/40, Regular12/16·14/20·16/22다. 네 표현 타입은 Outline/Underline/FilledDarker/FilledLighter이며 크기·표현의 선택은 닫힌 제네릭으로 확정한다. 테마의 명암 상태는 실행 중 팔레트에 반영한다. Outline은 중립 테두리와 진한 하단 선, Underline은 사각 하단 선, Filled는 표면과 투명 테두리를 그린다. 포커스 하단2px, error border, disabled/placeholder 색을 적용한다. 빈 문자열은 글리프를 조회하지 않으며 조합 글자도 본문과 같은 행 메트릭을 사용한다.

직접 호출도 같은 커널을 사용한다.

```csharp
ui.InputText<UiFluentTextInputBehavior,
    UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput>>(
    "Name", ref value, 256, new UiInputOptions(Placeholder: "Type here"), 220, owner);
```

[공식 Input 사용 지침](https://fluent2.microsoft.design/components/web/react/core/input/usage)은 Preview다. 참조 커밋 `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`의 스타일과 기존 해시가 고정된 팔레트 원본을 사용한다. 결합 가로 여백은8/12/18px이며 outline border1px를 별도로 반영한다. 원본·해시·검증 기록은 `artifacts/fluent-input-2026-11-06/plan.json`과 `verification.json`을 따른다.

교정: 이전 UiInputTextFlags에는 None만 정의되어 있었다. ReadOnly/AutoSelectAll은 새 기능이며, 과거에 정의된 옵션이 사라졌다는 설명은 부정확했다. 알 수 없는 캐스트 값은 이제 배치/편집 전 명시적으로 거부한다.

갤러리는 `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -Managed -DotnetPath C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe -ExtraArgs @('--input')`으로 연다. 12개 크기/표현 조합이 실제 값을 공유하며 readonly/disabled도 확인할 수 있다. 내부 Window를 사용하지 않는다. .NET10 기능 검증과 고정 .NET11 RC3 NativeAOT를 구분하며 속도·Private 개선을 주장하지 않는다.

전체 Input 완료는 아니다. 대화형 contentBefore/After, 포커스 모션/모션 감소, deprecated shadow 변형, Field/UIA, RTL/강제 색상, 실제 IME 후보창과 공식 브라우저의 동일 조건 화면/동작 비교가 남았다. 기본 카탈로그 연결과 생성/직접 GPU 동치는 공식 룩앤필 전체 완료 증거가 아니다. 전체 Fluent·공통 엔진·Pi 목표도 유지한다.

검증 결과: .NET10 전체2913·Input37·집중 GPU229·생성기115, net8/9/10 경고0/오류0. 고정 RC3 Native 전체 EXT2/core3 각2427·집중 각229. 같은400×300 집중 이미지193개씩 정확히 같다. Native 전체255×273 이미지2097개도 두 경로가 같다. 과거 전체1905개 중1901개 동일,4개는 앞서 기록된 테스트 테마 보정이다. Core 생성66→67 중 기존65개는 동일하고 카탈로그와 새 Input 어댑터만 달라졌다. 관리 GPU 전체 재실행은 기존 Badge의12336B 할당 검사에서 실패했다. 최신 Input 집중 결과와 변경 전 GPU2421의 관련 없는 결과를 구분해 재사용하며 이를 전체 통과로 바꾸지 않는다.

후속 Badge 검사 개선 후 관리 GPU 전체2428개가 통과했고 이전 Native 전체2097이미지×2와 일치한다. 당시 실패 기록은 보존하며 정확한12336B 원인은 미확정이다. 제품 소스는 같다. 후속 근거 artifacts/badge-allocation-2026-11-06/verification.json.

## 앞·뒤 콘텐츠와 정적 표현 조합 (2026-11-06)

기본 Input은 `Options.ContentBefore`/`ContentAfter`로 한 줄 텍스트를 표시한다. 이는 값이나 스타일 선택이 아니라 실행 중 바뀌는 콘텐츠다. 줄바꿈은 이 텍스트 표현의 지원 범위 밖이며 편집 전 명시적으로 거부한다. 아이콘이나 다른 그리기 알고리즘은 `UiFluentInputPresentation<TSize, TAppearance, TBefore, TAfter>`와 `IUiInputAdornment`로 컴파일 시점에 선택한다.

```csharp
field.Options = new(Placeholder: "Amount") { ContentBefore = "$", ContentAfter = "USD" };

[UiFluentIcon("search", UiFluentIconStyle.Regular, 20)]
public readonly partial struct ProductSearchIcon;

[UiWidgetStyle(typeof(UiInput<,>), typeof(UiFluentTextInputBehavior),
    typeof(UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput,
        UiInputIconAdornment<ProductSearchIcon>, UiInputTextAdornment>))]
public partial class ProductSearchInput;
```

아이콘용 슬롯은 표현에 고정되며 텍스트 슬롯은 null이면 없다. `IUiInputAdornment`는 존재 여부·측정·그리기의 정적 계약이다. 현재는 비대화형 콘텐츠 계약이며 버튼 등의 자식 입력·수명 조합까지 완료했다고 주장하지 않는다. 사용자 정의 표현도 기본 위젯과 같은 공통 편집/그리기 경로를 사용한다. `IUiInputPresentation.MeasureContent`는 기본 측정 뒤 옵션에 따른 콘텐츠 측정을 제공하며 기존 표현에는 원래 배치가 유지된다.

공식 크기별 root/input 가로 여백은6+2·10+2·12+6px, gap은2/2/6px, 아이콘은16/20/24px다. 텍스트/아이콘 전경은 중립3, 비활성은 disabled를 사용한다. 외곽 루트와 편집 영역을 구분해 앞/뒤 장식 클릭은 텍스트를 편집하지 않고 선택·스크롤·IME가 장식 위를 덮지 않게 한다. 좁아지면 입력을0폭까지 줄이고 콘텐츠 순서를 유지한 채 루트에서 자른다. 콘텐츠 변경은 편집 정체성을 보존하며 해제 시 옵션 문자열도 놓는다.

텍스트 페인트는 이미 측정한 경계로 한 줄 블록을 구성해 같은 프레임의 이중 측정을 제거했다. 별도 슬롯 객체·런타임 타입 레지스트리를 만들지 않는다. 옵션 참조와 레이아웃 값 필드는 추가되며 전체 메모리/FPS 개선은 아직 측정하지 않았다.

최신 검증은 `artifacts/fluent-input-slots-2026-11-06/verification.json`이다. .NET10 전체2934·GPU3408·집중1209, 고정RC3 Native EXT2/core3 전체각3408·집중각1209. 기존 전체2097이미지와 집중193이미지 보존, 관리/Native 전체2481·집중577이미지씩 동치다. 세 크기의 독립 좌표, 실제 장식 잉크, 긴 선택 본문과의 픽셀 분리,40px 협소 배치,1/50/400px 반복 폭 변경과 입력/해제를 검사했다. GPU readback에서 발견한 협소 폭 겹침은 수정했고 실패 자료를 보존했다. Core 생성67개는 전부 이전과 같다. 새 사용자 아이콘/슬롯 생성 결과는 실제 테스트와 갤러리가 소비한다.

갤러리 `--input`에 `$`/`USD`와 생성된 검색 아이콘/`items` 접미사를 추가했다. 대화형 자식 슬롯, Field/UIA, 모션, RTL/강제 색상, 실제 OS IME 및 공식 브라우저 동치는 남는다. 전체 Fluent·엔진·Pi 목표도 유지한다.

Underline은 하단1px를 제외한 콘텐츠 높이로 장식을 중앙 정렬한다. 이 추가 점검 뒤 슬롯 GPU 검증을 세 크기×두 콘텐츠×네 표현으로 확장했다. 최종 전체2481·집중577이미지는 각각 같은 크기에서 비교하며 서로 다른 크기를 섞지 않는다.

## 실제 자식 위젯 슬롯

`BeforeSlot`/`AfterSlot`은 기본 또는 사용자 `UiWidget`을 소유한다. 버튼 모양을 별도로 흉내 내지 않고 기존 버튼의 입력·표현·콜백을 그대로 사용한다. 테마/동작 선택은 기존 생성 클래스와 닫힌 제네릭에 고정되며 자식 구성·포커스·값만 실행 중 바뀐다.

```csharp
var input = Fluent.Input("Amount", state.Binding, 256, 220);
var clear = Fluent.Button("x", () => state.Value = "");
clear.Size = new(24, 24);
input.AfterSlot = new("clear", clear, new(24, 24));

// Move the same keyed instance without replacing its state.
input.SetSlots(input.AfterSlot, null);
```

크기는 자식에 할당할 명시적 영역이다. 버튼의 `Size` 등도 이 영역에 맞춘다. 자연 크기 자동 측정·제약 협상까지 구현한 것은 아니다. 값은 양수·유한 크기여야 한다. 복합 콘텐츠는 사용자 `UiWidget.Build()`로 구성한다. 동일한 쪽에 문자열 콘텐츠와 자식 슬롯을 동시에 지정할 수 없다.

슬롯 키와 인스턴스는 안정적으로 유지한다. 같은 키에 다른 인스턴스를 덮어쓰거나 다른 부모의 자식을 공유하면 변경 전에 거부한다. 교체는 새 키로 수행하며 재배치/양쪽 교환은 `SetSlots`로 원자적으로 처리한다. 입력 자신·조상의 소유 순환, 중복 키/인스턴스도 거부한다. 슬롯 자식은 Input이 소유하므로 페이지의 `context.Child`에 별도로 중복 등록하지 않는다. 페이지에는 Input만 유지한다.

```csharp
var input = context.Child("input", () => {
    var field = Fluent.Input("Value", state.Binding, 256, 220);
    var button = Fluent.Button("x", () => state.Value = "");
    button.Size = new(24, 24);
    field.AfterSlot = new("clear", button, new(24, 24));
    return field;
});
return Dux.Row(input);
```

설정 즉시 기존 Build 소유권에 편입하며 제거는 다음 Build에서 자식을 해제한다. 첫 렌더 이전에 Input을 Dispose해도 채택한 자식을 해제한다. Build 중 슬롯을 변경하려는 호출은 그래프를 바꾸기 전에 거부한다. 기본과 사용자 위젯은 같은 `UiWidget` 수명/키 경로를 사용한다. 보호된 `OwnChild`/`ValidateOwnedChild`와 `RenderBuiltContent`는 기존 Build 경로를 재사용하며 leaf의 일반 빠른 경로는 유지한다.

입력 순서는 앞 슬롯→편집기→뒤 슬롯이다. 실제 Enter/Space/포인터 활성화와 키보드 포커스, 부모 비활성, 눌린 자식 제거를 처리한다. 편집기에서 자식 버튼으로 이동하면 IME 소유자를 해제하고 같은 프레임의 focus-within을 표시한다. 자식의 레이아웃·클립·가용 영역을 제한하고 부모의 커서/항목 메타데이터를 복원한다. 사용자 표현은 `SupportsChildren`을 명시하고 `MeasureContent`에서 `HasBeforeChild`/`HasAfterChild`·`Before`/`After`를 사용할 수 있다. 지원하지 않는 표현은 자식 채택을 거부한다.

자식 입력을 먼저 처리한 뒤 부모를 그리고, 자식의 기존 그리기 목록을 옮겨 위에 합친다. 자식 정점/인덱스를 다시 복사하지 않는다. 캡처 빌더는 위젯마다 보유하지 않고 컨텍스트에서8단까지만 재사용하며 더 깊은 중첩은 같은 렌더링을 수행하는 임시 빌더를 사용한다. 실패한 자식의 캡처는 폐기하고 컨텍스트 해제 시 캐시를 정리한다. 전체 CPU/Private/draw 성능 개선은 측정하지 않았다. 자식 동작이 외부 바인딩을 바꾸는 경우 기존 다음 프레임 무효화 모델을 따른다.

샘플의 `--input`에는 소스 생성된 Fluent dismiss 아이콘 버튼으로 공유 값을 지우는 실제 동작을 추가했다. 최신 검증/실행 파일/해시는 `artifacts/fluent-input-children-2026-11-06/verification.json`을 따른다. 공식 브라우저 전체 동치·자연 측정·Field/UIA·모션·RTL/강제 색상·실제 OS IME·전체 엔진/Pi는 계속 미완료다.

최종 대화형 슬롯 검증: .NET10 전체2959·입력82·GPU3561·집중1362, 고정 RC3 Native EXT2/core3 전체각3561·집중각1362. 관리/Native 전체2737·집중833이미지씩 일치하며 기존2481전체·577집중 이미지를 보존했다. 네 표현의 휴지·앞 버튼 누름·오류 상태 뒤 버튼 누름·비활성 화면을 부모와 실제 버튼의 독립 그리기 순서와 비교했다. Core 생성67개는 전부 이전과 같다. 한 번 잘못 표시한 진행표는 Panel6/7·DirectText6/6 원장 재조회와 서버 보고 복구로 바로잡았으며 정책 자산은 변경하지 않았다.


## 부모 측정 재사용 — 2026-11-12

내장 Fluent Input의 크기·외관·장식은 CanRetainMeasurement로 부모 측정 재사용을 명시합니다. 사용자 정의 특성과 호환 표현의 기본값은 false입니다. 측정은 라벨·옵션·너비·선언된 슬롯 크기를 사용하며 편집 값이나 슬롯 자식의 측정을 소비하지 않습니다. 슬롯 자식이 측정에 불투명해도 기존처럼 렌더링합니다. 입력·되돌리기·IME·바인딩은 기존 편집 경로에서 계속 처리합니다. 속성·슬롯 변경은 기존 소유권 체인을 무효화하며 새 리프 캐시는 추가하지 않습니다.

근거: artifacts/form-layout-retention-2026-11-12/verification.json. 관찰: ./run-gallery.ps1 → 측정 재사용.


## TextField

`Fluent.TextField(label, text.Binding, inputOptions: ..., field: ...)`는 한 줄 입력창과 레이블을 묶어 제공합니다. `UiFieldOptions.Hint`, `Required`, `ValidationMessage`로 도움말·필수 여부·오류를 표시할 수 있습니다. 검증 시점은 앱에서 정합니다. 초점이 있거나 입력값이 있으면 지우기 버튼이 보입니다. 버튼을 누르면 바인딩 값을 지우고 입력창으로 초점을 돌립니다. 읽기 전용에서는 지우기 버튼을 숨기고, 오른쪽에서 왼쪽으로 배치하는 경우에는 반대쪽 끝에 표시합니다. 갤러리의 **입력과 선택 → TextField**에서 두 예제와 각 예제의 현재 코드를 확인할 수 있습니다. [Fluent iOS 동작 안내](https://fluent2.microsoft.design/components/ios/core/textfield/usage).
