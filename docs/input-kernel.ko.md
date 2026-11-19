# 한 줄 입력 엔진의 분리와 수명

[한 줄 드래그](input-drag.ko.md): 입력에서 누른 드래그는 영역 밖 선택·가로 자동 스크롤을 이어가고 해제·숨김·클립/비활성·상위 팝업 차단 시 종료합니다. CPU336·입력커널400 통과, 이전 입력357장·다중 행3457장 일치. 실제 OS 캡처와 전체 텍스트/Native 일치는 미완료입니다.

[공통 키보드 명령](text-commands.ko.md): Ctrl+Backspace/Delete와 반복은 온전한 단어 범위를 삭제하고 다중 행 Left/Right는 선택을 접습니다. CPU301·입력커널304·다중행2336 통과이며 전체 네이티브/Unicode/OS 일치는 미완료입니다.

[원문 요소 통합](single-line-elements.ko.md)으로 버퍼 소유 경계를 키보드·반복·포인터·선택에 적용했습니다. CPU231·입력커널240 및 기존 이미지 유지 검증 통과. 전체 셰이핑/bidi·실제 OS 입력·현재 Native/성능은 미완료입니다.

공통 [텍스트 편집 무결성 규칙](text-edit-integrity.ko.md)을 Input과 Textarea에 적용합니다. 제한된 입력 요소 접두부·최대 길이에서 선택 덮어쓰기·외부 모델 undo 초기화·접근성 Value의 undo를 연결했고 확대 CPU175 통과입니다. 전체 Unicode 이동/셰이핑·현재 Native/성능은 미완료입니다.

2026-11-10 [키 가상 목록](virtual-list.ko.md): 세로 고정 높이 행을 double 오프셋·뷰포트 근처 좌표로 배치해 10억 항목을 처리합니다. CPU43·확대 좌표2317검사 통과, 범위 밖 행은 기존 소유권으로 해제합니다. 가변 높이/다른 축·교차 키 재활용 정책·외부 UIA·현재 Native는 미완료이며 컨테이너3/6 유지. 샘플: `layout_virtual_list_fba.cs`.

2026-11-10 [소유 ScrollView](scroll-view.ko.md): Dux.ScrollView가 유한 뷰포트 측정·실제 자식 배치·오프셋·스크롤바 수명과 무효화를 소유합니다. CPU47·별도 GPU/좌표1933검사 통과. 월드 좌표 정밀도 부족은 명시적으로 거부하며 큰 콘텐츠 재배치/가상화·현재 Native는 미완료입니다. 컨테이너3/6 유지. 샘플: `layout_scroll_view_fba.cs`.

2026-11-10 [중첩 스크롤 전달](scroll-routing.ko.md): 독립 휠 축의 경계 전달, 호출자 클립·팝업·창 순서 히트 검사, 공통 스크롤바 캡처/해제와 DirectText 실제 줄 높이를 반영합니다. 확대 관리 회귀 1,645검사·readback 1,025회 통과, 컨테이너 3/6 유지. 소유 스크롤 위젯·가상화·현재 Native는 미완료입니다. 샘플: `layout_scroll_routing_fba.cs`.

Last synced: 2026-11-11

분리한 커널은 Fluent Input·Textarea와 사용자 위젯이 사용합니다. 동작·측정·배치·그리기·수명이 같은 편집기를 공유하며, 이 기반이 전체 Fluent 일치 완료를 뜻하지는 않습니다.

```csharp
[UiWidgetStyle(typeof(UiInput<,>), typeof(UiStandardTextInputBehavior),
    typeof(ProductInputPresentation))]
public partial class ProductInput;

var field = new ProductInput("Name", text.Binding, maxLength : 256, width : 240);
```

생성기는 실제 닫힌 `UiInput<TBehavior, TPresentation>`의 생성자를 전달한다. 생성 클래스를 상속하여 `OnChanged(string)`을 확장할 수 있다. 대기 프레임에서 바인딩을 쓰지 않으며 한 프레임의 여러 문자 입력은 최종 값으로 한 번 통지한다. `Label`, `Value`, `MaxLength`, `Width`와 공통 위젯 정체성·해제 경로를 사용한다. 최대 길이는 호출자가 명시한다.

`IUiTextInputBehavior.Process`는 편집 상태를 처리한다. 전달된 ID는 이미 해석된 ID다. `UiControlContext.EditSingleLine`은 기존 선택·캐럿·가로 스크롤·클립보드·실행 취소/재실행·IME 커널을 재사용한다. 사용자 동작은 같은 커널에 전달하거나 다른 처리 알고리즘을 제공할 수 있다.

`IUiInputPresentation`은 글꼴 스코프, 측정, 배치, 편집 전/후 그리기를 제공한다. `PaintBeforeEdit`는 호환 라벨의 누락 글리프 관찰 콜백 순서를 보존한다. 지연 라벨 표현의 음성 대조와 실제 관찰 목록을 비교한다. `UiInputLayout`은 전체 크기와 입력 필드·텍스트 영역·라벨 위치·행 높이를 구분한다. `Place`는 기본 상대 배치 또는 사용자 알고리즘을 선택한다. `PaintEditableText`는 선택·문자·IME 조합·캐럿의 공통 렌더링을 제공하며 프레임·자식 구성을 강제하지 않는다. 호환 표현은 원래 부동소수점 계산 순서와 색·간격·그리기 순서를 보존한다. 잘못된 레이아웃은 편집 저장소 생성 전에 거부한다.

기존 `ui.InputText(label, ref value, maxLength)`도 같은 제네릭 커널을 명시적 호환 동작·표현으로 소비한다. 기존 숫자 기반 텍스트 필드 스킨은 이 호환 경로에 남긴다. 이것을 새로운 Fluent 구현으로 세지 않는다.

직접 호출에도 소유자를 보유할 수 있다.

```csharp
using var owner = new UiTextInputOwner();
ui.InputText<UiStandardTextInputBehavior, ProductInputPresentation>(
    "Name", ref value, maxLength: 256, width: 240, owner: owner);
```

소유자는 논리적 필드마다 따로 사용한다. 컨텍스트는 약하게 참조하며 해제 시 해당 필드의 편집 버퍼·기록·선택·캐럿·스크롤을 제거한다. 생성 위젯은 이를 자동 소유한다. 기존 소유자 없는 문자열 API의 상태 수명은 호환성을 위해 유지한다. 외부 바인딩은 Dispose하지 않는다. IME 해제 실패는 숨기지 않고 위젯의 기존 AggregateException 계약으로 전달하되 바인딩·편집 저장소는 해제한다.

기록 배열의 사용하지 않는 슬롯과 풀 반환 전 모든 슬롯을 지운다. 이는 문자열 참조 수명을 바로잡는 변경이며 실행 취소 개수나 입력 내용을 줄이지 않는다. 풀의 배열 용량과 사전 용량 자체가 모두 반환됐다는 뜻은 아니다. Private/FPS 개선율은 측정하지 않았으며 생성 코드나 항목 수를 성능 개선으로 세지 않는다.

검증 전용 `InputLegacyReference.cs`는 추출 전 함수를 동결한 참조다. `DuxelValidationHooks=true`일 때만 Core에 연결하며 일반 라이브러리 입력에서는 제외한다. 정적/호버/활성/선택/스크롤/문자/라벨을 light/dark·MSAA1/4·raster/vector에서 비교한다. 별도 위 라벨/둥근 프레임 표현도 생성 위젯과 직접 호출에서 비교한다. 이 대체 표현은 테스트용이며 새로운 기본 테마가 아니다. 초기 검사에서 키를 놓지 않아 비교가 틀어진 기록을 보존하고 각 캡처의 키 상태를 명시했다.

IME는 대역으로 조합·커밋·캐럿·해제 실패를 검사한다. 실제 Windows IME 후보창·OS 입력 승인은 별도다. 최신 수치와 소스/실행 파일 해시는 `artifacts/input-kernel-2026-11-06/verification.json`을 따른다.

공식 Fluent Input의 크기·외형·상태·placeholder·포커스/Tab·readonly/flags·Field·접근성·RTL/강제 색상 및 전체 위젯/공통 엔진/Pi 목표는 계속 진행한다. 교정: 당시 UiInputTextFlags에는 None만 있었다. 정의된 기존 옵션의 누락은 아니며, 알 수 없는 캐스트 값이 무시되던 계약은 이후 Fluent Input에서 명시적 거부로 바꿨다. ReadOnly/AutoSelectAll은 새 기능이다.

현재 소스의 기존 전체 기능 앱에서 입력 경로를 확인하려면 프로젝트 루트에서 `./run-fba.ps1 samples/fba/all_features.cs -NoCache`를 사용한다. 새 Fluent Input 외형 적용 또는 최신 실행 파일의 수동 승인을 의미하지 않는다.

전체2876·GPU2197 각 설정은 대체 표현의 테스트 테마 보정 전 결과다. 라이브러리 코드는 같으며, 보정 후 관리/Native 집중144검사·117이미지 일치로 해당 장면을 확인해 나머지 결과를 재사용한다. 일반 Core 재빌드의148개 컴파일 입력과 DLL 메타데이터에서 동결 참조가 제외됨을 확인했다. 메타데이터 검사 호스트 .NET10.0.14는 기능/성능 측정에 포함하지 않는다.
