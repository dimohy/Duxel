# Fluent Card

[English](fluent2-card.md)

Last synced: 2026-11-12

`Fluent.Card`는 현재 기본 콘텐츠 묶음을 제공합니다. 지속되는 위젯 트리 하나를 소유하며 일반 Row/Column과 실제 입력·버튼·링크를 내부에 구성합니다. 네 가지 포커스 방식과 루트만 비활성화하는 옵션을 제공합니다. 바인딩된 카드 선택을 지원하며 `Fluent.CardFooter(children, action)`은 푸터 콘텐츠와 오른쪽 끝 동작 슬롯을 제공합니다. `Fluent.CardHeader(header, description, image, action)`은 선택적인 이름별 머리글 슬롯을 제공합니다. 생성 시 지정하는 떠 있는 동작 슬롯을 지원하며 CardPreview는 CardContent를 통해 사용합니다. 팩터리 노출이 공식 Card 전체 적합성 완료를 뜻하지 않습니다.

```csharp
using var card = Fluent.Card("작업 항목",
    Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
        Fluent.Body1("문서를 검토하세요"),
        Fluent.Button("열기", OpenDocument)),
    new(Width: 320));
card.Render(ui);
```

`UiCardSize` Small/Medium/Large는 여백8/12/16과 모서리 반경2/4/6을 선택합니다. `UiCardAppearance` Filled/FilledAlternative는 중립 배경1/2와 Shadow4를 사용합니다. Outline은 배경을 드러내며 중립 선1을 그리고, Subtle은 테두리·그림자 없이 배경을 드러냅니다. 이 기본 토큰은 고정 원본을 따릅니다. 기존 analytic 둥근 도형과 공통 `UiBoxShadow`로 그리며 별도 다각형·셰이더·렌더러를 만들지 않습니다.

`UiCardOptions.Width/Height`는 콘텐츠 여백을 포함한 본체 크기입니다. 생략하면 콘텐츠로 본체 크기를 구합니다. 네이티브 레이아웃은 이웃 할당과 클립을 명확히 하기 위해 본체 밖에 전체 그림자 범위를 예약합니다. 이 외부 공간은 CSS의 시각적 넘침과 다르며 접근성 그룹은 본체 영역을 나타냅니다. 실제 부모의 고정 제약이 우선하므로 본체·콘텐츠 공간이 줄어들 수 있습니다. 주 콘텐츠는 여백 안의 할당을 따르고 보조 위젯은 독립된 오른쪽 위 슬롯을 사용합니다. 사용자 측정 규격은0여백을 허용하며 페인트와 포인터 획득을 실제 둥근 본체로 제한합니다.

루트는 이름 있는 네이티브 UIA Group 하나와 ClassName `Duxel.Card`를 제공합니다. 콜백이 없는 루트에는 Invoke·편집 가능한 Value를 추가하지 않습니다. 기본 정적 모드에서는 루트 키보드 정지점도 없습니다. 자식은 원래 제공자·포커스·포인터·편집 동작을 유지합니다. 그룹 이름은 `Label`이며 상속한 `AccessibleName`으로 재정의할 수 있습니다. 이름·옵션을 바꿔도 소유한 인스턴스와 입력 상태를 유지합니다. 해제하면 소유한 전체 트리와 그림자 임대를 해제합니다. 자식은 다른 컨테이너와 동시에 공유할 수 없습니다.

`UiCard<TPresentation>`은 컴파일된 표현을 공통 소유권·박스 레이아웃과 분리합니다. `IUiCardPresentation.Measure`는 규격을 반환하고 `Paint`는 정적 본체를 그립니다. 사용자 표현은 측정 재사용을 기본적으로 허용하지 않습니다. 옵션으로만 규격을 구할 때만 재사용을 허용하세요. 공통 단일 자식 캐시는 자식의 측정 재사용과 설정된 레이아웃 자원도 확인합니다. 요청된 프레임의 실제 콘텐츠 그리기·입력은 계속 처리합니다. 불변 그림자는 재사용하며 Card는 주기 프레임을 예약하지 않습니다.

`./run-gallery.ps1` 실행 → **카드**에서 외관·크기·폭을 바꿉니다. **카드 메모**에 입력하고 **카드 내부 실행**을 누른 뒤 다른 페이지를 다녀오세요. 갤러리는 크기·외관12개 조합을 보여 줍니다. 현재 회귀 검증 근거는 `artifacts/card-rounded-content-2026-11-12/verification.json`에 기록합니다. 원래 고정 RC3 NativeAOT·런타임 이미지·성능, 전체 공식 상호작용·외관·접근성 적합성과 기기별 관문은 남아 있습니다.

참고: [공식 Card 사용 가이드](https://fluent2.microsoft.design/components/web/react/core/card/usage), [고정 Card 속성](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-card/library/src/components/Card/Card.types.ts), [고정 스타일](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-card/library/src/components/Card/useCardStyles.styles.ts). 정확한 원본·토큰 검사 자료의 출처·MIT 라이선스는 근거 폴더에 보존합니다.


## 포커스와 루트 비활성화

`UiCardOptions.FocusMode`는 `UiFocusGroupMode`를 사용합니다. Off는 루트를 건너뛰고 자식으로 이동합니다. NoTab은 루트에서 Tab으로 자식을 건너뛰고 Enter나 자식 클릭으로 진입한 뒤 내부 Tab을 순환합니다. TabExit는 같은 방식으로 진입하되 마지막 자식에서 Tab으로 나갑니다. TabOnly는 루트 다음 Tab으로 자식에 진입합니다. 세 활성 모드 모두 Enter로 첫 자식에 진입하고 Escape로 루트에 돌아옵니다. 진입 Enter는 자식 버튼을 동시에 실행하지 않습니다. 키보드 포커스는 공통 윤곽선을 그립니다.

`Disabled = true`는 루트의 Enabled/Focusable만 해제합니다. 실제 자식 버튼·입력은 계속 사용할 수 있습니다. 기본 표현은 비활성 배경·선·글자색을 상속하고 채워진 카드는 Shadow2를 그리되 Shadow4 공간을 유지해 본체 위치가 바뀌지 않습니다. 자식의 독립 표현 색은 그대로 유지됩니다. 사용자 표현은 `ContentForeground`와 `UiCardMetrics.ReservedShadow`를 제공합니다. 예약 그림자는 실제 그림자보다 작을 수 없습니다.

`./run-gallery.ps1` → **카드** → **카드 포커스 방식**, **카드 루트 사용 전환**으로 비교하세요. 현재 근거는 `artifacts/card-focus-2026-11-12/verification.json`입니다. TabPanel의 늦은 정지점 삽입에도 실제 ID로 그룹 경계를 확정하며, 프레임 뒤 임시 ID 맵을 비우고 미사용 메타데이터 용량은 축소합니다. 중첩 NoTab 그룹의 양방향 이동·단계별 Escape, 실제 입력 소유자의 IME 조합과 한글 확정, 내부 메뉴의 Escape·Enter 우선순위를 검증했습니다. 모든 조합 및 원본 웹 구현과의 전체 적합성은 아직 완료되지 않았습니다.


## 루트 동작과 상호작용 표현

`Fluent.Card(label, content, options, activated: Open)` 또는 `card.Activated = Open`으로 루트 동작을 연결합니다. 배경을 누른 뒤 같은 배경에서 놓으면 한 번 실행합니다. 자식 버튼·입력·메뉴는 자신의 입력을 처리하며 루트 동작을 함께 실행하지 않습니다. 배경에서 누르고 자식 위에서 놓거나 반대로 이동하면 루트 실행을 취소합니다. 루트는 Group 의미를 유지하면서 실제 UIA Invoke를 제공합니다. Disabled 상태는 루트 Invoke를 거절합니다. `Activated = null`은 실행 기능을 제거합니다.

`FocusMode` 기본값은 null(자동)입니다. 콜백이 없으면 Off, 있으면 NoTab입니다. 명시한 Off/NoTab/TabExit/TabOnly는 자동 선택보다 우선합니다. Enter는 일반 동작 카드의 첫 자식에 진입하며 루트나 자식을 동시에 실행하지 않습니다. 콜백을 바꾸면 레이아웃·의미를 다시 확인합니다.

활성 카드의 hover/pressed 배경·선·전경은 고정 공식 토큰을 사용합니다. 채워진 카드는 hover에서 Shadow8을 그리며 정지·눌림·비활성 상태에도 Shadow8 공간을 예약해 본체와 자식 위치를 유지합니다. 바깥에서 누른 채 들어오는 포인터는 누름을 소유하지 않습니다. 불변 hover 프레임은 그림자 텍스처를 다시 업로드하지 않습니다. CSS 넘침과 다른 예약 공간은 앞서 설명한 네이티브 할당 계약을 따릅니다.

사용자 표현의 `Paint(context, bounds, options, metrics, interaction)`은 `UiButtonInteraction`을 받습니다. `options.IsInteractive`는 위젯이 콜백으로 계산한 읽기 전용 상태입니다. `Shadow`와 `ContentForeground`도 상호작용을 받습니다. 동적 그림자는 예약한 최대 elevation을 넘으면 명시적으로 실패합니다. 파생 단일 자식 레이아웃의 `LayoutOptions`는 재구축으로 무효화되는 상태에서만 계산합니다.

`./run-gallery.ps1` → **카드** → **카드 루트 동작 전환**으로 배경과 내부 버튼의 실행 횟수를 비교하세요. 현재 근거: `artifacts/card-action-2026-11-12/verification.json`. 전체 적합성·원래 고정 RC3 배포 검증은 계속 구현할 범위입니다.


## 바인딩된 선택

```csharp
var selected = new UiState<bool>(false);
using var card = Fluent.Card("문서", Fluent.Body1("검토할 문서"),
    new(Width: 280), selection: selected.Binding);
```

`Selection`은 선택 값을 읽고 쓰는 `UiBinding<bool>`입니다. null이면 선택 모드를 끕니다. 호출자가 가진 모델 값은 모드를 껐다 켜도 유지됩니다. 현재 값은 `card.Selection?.Value`로 읽습니다. 표현에 전달되는 Options의 `IsInteractive`/`IsSelectable`/`IsSelected`는 해석된 상태 스냅샷이며, `card.Options` 자체는 호출자가 설정한 구성입니다.

선택 모드에서는 명시한 FocusMode와 관계없이 포커스 그룹을 설치하지 않습니다. Group 아래의 실제 체크박스가 첫 Tab 정지점이며 Enter와 Space로 선택합니다. Space는 놓을 때 바뀝니다. 기본 체크박스는 고정 원본처럼 시각적으로 숨기며, 네이티브 접근성에서는 본체 영역을 선택·포커스 대상으로 사용하고 전체 본체에 윤곽선을 그립니다. 별도의 가짜 체크 표시를 추가하지 않습니다. 자식 입력·버튼·메뉴는 이후 Tab 정지점과 독립 동작을 유지합니다.

배경 클릭 또는 Group Invoke는 선택 바인딩을 한 번 갱신하고 선택적인 Activated를 호출합니다. 체크박스의 키·Toggle은 선택만 바꿉니다. 자식의 클릭·입력·Enter·메뉴 실행은 선택이나 루트 동작을 함께 실행하지 않습니다. 배경에서 누르고 자식 위에서 놓으면 취소합니다. Disabled는 루트·선택만 막으며 자식은 계속 사용할 수 있습니다. 외부 Windows 클라이언트는 실제 Checkbox Toggle과 정수 상태 변경 이벤트를 사용합니다. 제거된 체크박스와 보관한 Toggle 제공자는 Unavailable을 반환합니다.

선택 표면과 선·hover/pressed 우선순위는 고정 토큰을 따릅니다. 포인터 선택 후 요청되는 후속 프레임에 표면색을 반영합니다. 선택 변화는 주기적인 타이머를 만들지 않습니다. 외부 값이 사용자 측정 규격까지 바꾸면 현재 프레임의 부모 할당을 유지하고 부모 캐시를 무효화해 재배치 프레임을 요청합니다. 새 배치 후에는 다시 측정을 재사용합니다.

`./run-gallery.ps1` → **카드** → **카드 선택 모드 전환**. 배경과 내부 실행을 번갈아 누르고 선택 값·변경 횟수를 비교하세요. 탭을 바꿔도 값이 유지됩니다. 선택 바인딩 초기 검증: CPU55개/8485회(Card184·Observer210), GPU12개 프로필2433회(Card448), 외부 Card20·Avatar23회, 기존1108장 동일. 근거: `artifacts/card-selection-2026-11-12/verification.json`. 전체 적합성은 미완료입니다.

## Footer 슬롯

`Fluent.CardFooter([Fluent.Button("열기", OpenDocument)], Fluent.Caption1("준비됨"))`은 양쪽 슬롯을 소유합니다. 콘텐츠 사이는 12px이며 선택적인 동작 슬롯은 가용 폭의 오른쪽 끝에 놓입니다. 자식 높이는 푸터 높이에 맞춥니다. 폭이 무제한이면 자연 폭을 사용하고 공간이 부족하면 명시적으로 실패합니다. 공통 레이아웃 캐시를 재사용하고 자식 변경은 Card까지 전파됩니다. 별도 접근성 그룹이나 키보드 정지점·애니메이션을 추가하지 않습니다. RTL·고대비·전체 Card 적합성은 남아 있습니다.

Footer 검증: CPU55/8506(Card205,Footer 신규21개 포함),GPU Card464/Observer63,네이티브Card24. 이전Card PPM325개가 일치합니다. `artifacts/card-slots-2026-11-12/verification.json`을 참고하세요.

## Header 슬롯

`Fluent.CardHeader(Fluent.Body1Strong("작업"), Fluent.Caption1("설명"), Fluent.Avatar("정세일"), Fluent.Button("정보", ShowInfo))`는 전달한 슬롯을 모두 소유합니다. 각 슬롯은 생략할 수 있습니다. 그리기·키보드·접근성 순서는 이미지/제목/설명/동작입니다. 설명이 있으면 가운데 열의 두 자동 행에 제목·설명을 놓고 이미지·동작은 두 행에 걸쳐 세로 중앙에 놓습니다. 설명이 없으면 제목이 늘어나는 한 행을 사용합니다. 이미지 오른쪽과 동작 왼쪽에는 각각12px 여백이 있으며 가운데 슬롯이 없어도 유지합니다. 제한된 폭을 채우며 무제한 폭에서는 자연 폭과 명시적인 최소 크기를 사용합니다. 이미지·동작의 공간이나 제목·설명의 전체 높이가 부족하면 명시적으로 실패합니다. 기존 측정 캐시·포인터 처리·네이티브 제공자를 사용하며 별도 타이머나 접근성 그룹을 만들지 않습니다.

행 배치는 [고정 Header 스타일](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-card/library/src/components/CardHeader/useCardHeaderStyles.styles.ts)과 [CSS Grid 자동 행 분배](https://www.w3.org/TR/css-grid-2/#algo-stretch)를 따릅니다. 브라우저 전체 min-content·RTL·고대비·Card 적합성 완료를 뜻하지 않습니다. `./run-gallery.ps1` → 카드에서 한글 아바타·제목·설명·카드 정보 버튼의 실제 슬롯을 확인합니다. 머리글 버튼은 루트 선택·실행과 독립적으로 동작합니다.

Header 검증: CPU55/8550(Card247,Header42개 포함,Observer212),GPU Card492+Observer63,네이티브Card27(호스트6/클라이언트21),이전 Card 이미지337개 일치. `artifacts/card-header-2026-11-12/verification.json`을 참고하세요.


## 떠 있는 동작

`Fluent.Card` 또는 `UiCard<TPresentation>`에 `floatingAction: widget`을 추가합니다. 생성 시 주 콘텐츠와 보조 위젯을 모두 검사한 다음 소유하며, 같은 인스턴스를 두 슬롯에 넣을 수 없습니다. `Child`는 원래 콘텐츠이고 `FloatingAction`은 보조 위젯입니다. 보조 위젯은 카드의 고유 크기를 늘리지 않습니다. 본체에서 8px을 뺀 범위 안에서 측정하고 콘텐츠 패딩과 별개로 위쪽·오른쪽에서 각각4px 떨어져 배치합니다. 콘텐츠 뒤에 그려 겹친 부분을 덮지만 키보드·접근성 순서는 보조 위젯이 먼저입니다. 겹쳐 가린 콘텐츠는 클릭되지 않으며 더 높은 팝업 계층의 입력은 유지합니다.

선택 카드에는 같은 모델에 연결한 실제 체크박스를 넣습니다. 보조 위젯을 지정하면 기본 숨은 선택 체크박스를 제거합니다. 일반 버튼에 Toggle 의미를 자동으로 부여하지 않습니다. 보조 위젯의 사용 가능 상태는 호출자가 정합니다. Card.Disabled는 루트에 적용하며 실제 자식 위젯은 자체 상태를 유지합니다. 배경 선택은 해당 프레임에 캡처한 바인딩의 최신 값을 사용하고, 자식이 선택값을 바꾼 경우 중복 저장하지 않습니다. 배경색 변경은 후속 프레임을 요청합니다. 보호된 재정의 메서드는 `OnSelectionChanged(UiBinding<bool> selection, bool selected)`이며, 자식이 프레임 중 Selection을 바꿔도 캡처한 바인딩으로 저장합니다.

```csharp
var state = new UiState<bool>(false);
var action = Fluent.Checkbox("##select-document", state.Binding);
action.AccessibleName = "문서 선택";
using var card = Fluent.Card("문서", Fluent.Body1("검토할 문서"),
    new(Width: 280), selection: state.Binding, floatingAction: action);
```

기본 Fluent 모서리 크기를 지원합니다. 고정4px 슬롯은 사용자 정의 반지름에서도 [공통 둥근 페인트 클립](rounded-paint-clip.ko.md)으로 실제 본체 경계를 따릅니다. 주 콘텐츠와 보조 위젯을 실제 둥근 본체에 클리핑합니다. 전체 RTL/고대비/적합성·고정 RC3 배포 검증은 남아 있습니다.

`./run-gallery.ps1` → **카드** → **떠 있는 선택 동작**에서 오른쪽 체크박스와 배경 선택을 확인합니다. 내부 버튼은 실행 횟수만 바꿉니다. CPU55묶음8669검사(Card349,Observer214), GPU12프로필2509검사(Card524), 외부 접근성 Card27/CardFloating30/Avatar23 및 각각614프레임인 독립 렌더링 세션 두 건을 통과했습니다. 근거: `artifacts/card-floating-action-2026-11-12/verification.json`. 관리 런타임 검증이며 NativeAOT 완료나 성능 개선율을 뜻하지 않습니다.


## 둥근 콘텐츠와 입력 경계

Card는 주 콘텐츠와 floatingAction을 공통 분석적 페인트 클립으로 본체 안에 제한합니다. 루트 배경·테두리·그림자·루트 포커스 윤곽은 자식 클립 바깥에서 그립니다. 기본 Fluent 패딩과 반지름은 유지합니다. 사용자 정의 `UiCardMetrics`는 반지름보다 작은 패딩과0패딩을 허용하며 패딩이나 반지름을 임의로 바꾸지 않습니다. 반지름은 공통 도형 정책에 따라 실제 본체 작은 변의 절반까지 제한합니다. 떠 있는 동작의 사용자 정의 반지름 거부 조건도 제거했습니다.

포인터 획득도 같은 둥근 본체를 따르며 중첩 카드와 실제 보조 위젯에 적용합니다. 잘린 모서리로 루트·자식 버튼·선택 체크박스를 실행하거나 포커스할 수 없습니다. 숨은 선택 프록시도 그 부분에서 조상 카드의 보이는 배경 클릭을 가로채지 않습니다. 팝업 계층별 포인터 제한을 사용하므로 더 높은 팝업 빌더는 카드 밖에도 그리고 입력을 받습니다. 루트와 버튼은 둥근 경계 밖에서 놓으면 실행을 취소합니다. 키보드 탐색과 네이티브 Focus/Invoke/Toggle은 독립적으로 유지하며 접근성 경계 사각형은 사각형입니다.

카드 본체의 고정 크기가 모든 자식 위젯을 자동으로 늘리지는 않습니다.0패딩 콘텐츠로 본체를 채우려면 `Dux.Expand(content)`를 사용합니다. 샘플은 패딩0·반지름32인 사용자 정의 표현을 사용하며, 할당 크기를 유지한 채 반지름을0으로 전환합니다.

`./run-gallery.ps1` → **카드**에서 **둥근 경계와 입력**까지 스크롤합니다. 실제 **둥근 카드 내부 실행** 버튼의 가운데와 잘린 모서리를 누르고 **둥근 모서리**를 전환해 보세요. 실행 횟수와 선택은 탭 전환 후에도 유지합니다. 공통 ScrollItem 공급자로도 샘플에 접근할 수 있습니다. 근거: `artifacts/card-rounded-content-2026-11-12/verification.json`. 독립 GPU 기준과의 비교는 AA 경계의 상대·절대 SDF 보간 차이에 채널 값1을 허용하며, 동일 상태 재사용은 바이트 단위로 일치해야 합니다. 전체 공식 적합성·원래 NativeAOT/런타임 이미지/성능/기기 관문은 남아 있습니다.


`Fluent.CardPreview(content, logo)`는 미디어와 왼쪽·아래12px 위치의 고정32px 로고를 소유합니다. 로고는 고유 크기에 영향을 주지 않습니다. Card 주 콘텐츠에 `Dux.CardContent([...])`로 직접 슬롯을 나열합니다(구성 전용 래퍼 허용). Preview는 Card 여백까지 채웁니다. 세로는 좌우와 첫 항목 위·마지막 항목 아래, 가로는 위아래와 첫 항목 왼쪽·마지막 항목 오른쪽입니다. `UiCardOptions.Orientation` 기본값은 Vertical입니다. 가로의 마지막 Header/Footer는 남은 유한 폭을 채웁니다. 일반 사용자 콘텐츠 트리는 기존 레이아웃을 유지합니다. 주축 초과는 명시적으로 실패하며 CSS flex-shrink/min-content 전체 적합성은 남아 있습니다. 콘텐츠·로고의 실제 입력·접근성 공급자를 유지하고 Preview 위에 Card 테두리를 그립니다. 재사용 시 측정된 할당을 유지하고 주기 프레임을 요청하지 않습니다.

`./run-gallery.ps1` → **카드** → **미디어 미리보기**에서 세로·가로 SVG 카드, 폭·크기 전환, 고정 로고와 여백까지 채운 모서리를 확인합니다. SVG는 콘텐츠 수명 동안 한 번 읽습니다. 근거: `artifacts/card-preview-2026-11-12/verification.json`. 노출 팩터리46개는 여전히 상위55개 중23개를 연결하며, 이 변형을 새 상위 항목 완료로 세지 않습니다.


CardPreview 검증: 관리 CPU55개/8754회(Card407), 테스트만 추가한 뒤 영향받는 Observer223회 후속 검증, GPU12개 프로필2673회(Card608), 외부 Card27·CardFloating30·Avatar23회, 숨김/표시 앱 세션 각각614프레임. 기존 이미지2316장 중2300장 완전 동일,16장은 y32 위 동적 HUD만 다릅니다. 변경된 갤러리와 신규 이미지는 별도 검증했습니다. 일반 미리보기는 실행하지 않았습니다. 근거: `artifacts/card-preview-2026-11-12/verification.json`. 공식 전체 적합성과 원래 RC3 배포·성능 관문은 남아 있습니다.


2026-11-12 최소 할당 수정: CardContent와 직접 배치한 세로 CardPreview는 그림자·패딩을 뺀 Card 본체의 최소 크기를 유지합니다. 세로 Header/Footer는 먼저 고유 폭을 측정한 뒤 본체 폭으로 늘리고, 가로의 마지막 Header/Footer는 최대 폭이 느슨해도 남은 최소 폭을 채웁니다. Preview의 주축 고유 크기를 유지하며 가로 미디어는 최소 높이 안에서 가운데 배치됩니다. 준비한 부모 입력 스냅샷으로 재사용을 안정적으로 유지합니다. `./run-gallery.ps1` → **카드** → **부모의 최소 크기**에서 부모 최소 폭과 방향을 바꿉니다. 고정 Card 폭 없이 실제 제약 부모를 사용합니다. CSS min-content/flex-shrink 전체 적합성은 남아 있습니다.

근거: `artifacts/card-minimum-allocation-2026-11-12/verification.json`. 최초 계약 실패9건을 재현했고 최소 제약 전달과 슬롯 고유 폭을 교정한 뒤 전체 CPU55개/8772회(Card419·Observer226), GPU12개 프로필2725회(Card656), 외부 접근성80회와 숨김/표시614프레임 세션이 통과했습니다. 기존 RGB 이미지2332장 중2312장 완전 동일,20장은 y32 위 동적 HUD만 다릅니다. 신규32장과 변경된 Observer 갤러리는 기하·픽셀·정확한 재사용을 별도로 검사했습니다. 시간 개선율은 측정하지 않았습니다. 팩터리 노출46개, 상위55개 중 연결23개를 유지합니다. 원래 배포·성능·전체 적합성 관문은 남아 있습니다.

추가 프레임 회귀: Card GPU 프로필은 요청 MaxFramesInFlight3에서도656회 통과하여 총13개 프로필/3381회입니다. 요청2/3슬롯 사이 같은 이름의517장은 RGB 바이트 단위로 일치합니다. 실제 동시 프레임 수를 측정한 것은 아닙니다.

## CardNudge

`Fluent.CardNudge(title, description, open, action, options)`는 일반 세로 레이아웃에서 아래 내용보다 앞에 안내 카드를 배치합니다. 선택 사항인 동작 위젯은 카드가 소유합니다. 카드를 닫거나 표면을 좌우로 48 논리 단위 이상 밀면 `UiBinding<bool>`에 `false`를 씁니다. 호출자가 값을 유지하면 안내 카드도 계속 표시됩니다. 카드 안 동작 버튼은 자체 콜백을 실행합니다.

닫기 버튼은 키보드와 보조 기술로 사용할 수 있습니다. `UiCardNudgeOptions.CloseLabel`로 이름을 바꿀 수 있습니다. `null`을 지정할 때는 주변 화면에 키보드로 닫을 수 있는 별도 수단을 제공하세요. 카드가 사라지는 동안 아래 내용이 위로 올라옵니다. `ReducedMotion`을 켜면 즉시 접힙니다.

갤러리의 **콘텐츠와 목록 → CardNudge**에서 닫기 버튼과 좌우 밀기를 시험할 수 있습니다. **안내 다시 표시**로 예제를 초기화하고, 바로 아래에서 현재 값이 반영된 소스를 확인하세요. [Fluent iOS Card nudge 가이드](https://fluent2.microsoft.design/components/ios/core/cardnudge/usage)
