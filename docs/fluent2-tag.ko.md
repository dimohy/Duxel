# Fluent Tag와 선택 태그

`Fluent.Tag("정세일")`은 읽기 전용 값입니다. 해제 콜백을 제공하면 태그 전체가 클릭·Enter·Space·Delete·Backspace로 해제 가능한 컨트롤이 됩니다. 콜백이 실제 데이터 변경을 담당합니다.

```csharp
var tag = Fluent.Tag("SVG", () => selected.Value = []);
var tags = new UiFluentTagGroup("선택한 기능", selected.Binding, options);
```

여기서 `selected`는 `UiState<string[]>`, `options`는 `UiSelectOption[]`입니다. Dropdown 또는 다중 Combobox에 같은 바인딩과 옵션을 전달하면 목록과 태그가 하나의 선택 값을 공유합니다. 그룹은 실제 getter/setter 결과를 읽으며 외부 배열의 직접 변경도 감지합니다. 옵션 값은 고유해야 하고 선택 값은 해당 옵션의 중복 없는 부분 집합이어야 합니다. 옵션 배열은 생성 때 복사합니다. 옵션의 비활성 항목은 표시하지만 해제하지 않습니다.

`UiTagOptions`는 Medium/Small/ExtraSmall, Filled/Outline/Brand, Rounded/Circular, RTL과 해제 동작 이름을 제공합니다. 기본 크기의 높이는 32/24/20이며 그룹 간격은 8/6/4입니다. `UiTagContent`로 주 텍스트·보조 텍스트·빌린 `UiMenuIcon`을 전달할 수 있습니다. 한글을 포함한 전체 텍스트로 크기를 측정하며 말줄임표를 추가하지 않습니다. 부모가 제한한 실제 경계를 넘어 그리지는 않습니다.

TagGroup은 기존 Wrap 배치에 선택된 자식만 참여시켜 빈 자리 없이 줄을 바꿉니다. 각 태그는 선택이 유지되는 동안 정체성을 유지합니다. 방향키와 Home/End로 이동하고 RTL에서는 가로 방향을 반대로 적용합니다. 그룹은 하나의 Tab 경유점을 사용합니다. 해제 후 다음 활성 태그, 없으면 이전 태그로 포커스를 옮깁니다. 그룹이 비면 기존 Tab 탐색 규칙으로 바깥 컨트롤에 포커스를 넘기며 이동할 대상이 없으면 해제합니다. `dismissible: false`는 읽기 전용 그룹입니다. 클릭 해제 후 다음 요청 프레임에서 태그가 제거됩니다.

닫기 ×는 **Fluent 아이콘 폰트**로 그립니다. 사용자 아이콘은 기존 SVG `MenuIcon` 경로를 빌릴 수 있습니다. SVG의 XML은 자산을 읽을 때 처리하며 태그 프레임마다 다시 해석하지 않습니다. 자산 소유자는 태그보다 오래 유지하고, 그룹 해제는 빌린 SVG 자산을 파괴하지 않습니다.

실행: `./run-gallery.ps1` → **선택 목록** → **확인할 기능**에서 이미지·SVG 등을 선택하세요. 아래 **기본 태그**의 전체를 눌러 목록의 선택도 해제되는지 확인합니다. 위쪽 확인형 태그는 본문 확인과 × 해제를 구분합니다. **태그 크기 전환**, 방향 변경, 탭 왕복으로 배치와 상태 유지를 관찰할 수 있습니다.

현재 지원 범위는 기본 Tag와 선택 해제 그룹입니다. 선택 강조·아바타 미디어·초과 항목 패턴, 강제 색상·추가 작은 크기 터치 영역, 실제 스크린 리더 검증은 전체 적합성의 남은 범위입니다. 그룹은 네이티브 Toolbar 아래 실제 Button/Label을 게시합니다. 고정 RC3 NativeAOT와 성능·실기기 검증 완료를 뜻하지 않습니다.

근거: `artifacts/tags-2026-11-13/`. [공식 Tag 지침](https://fluent2.microsoft.design/components/web/react/core/tag/usage), [고정 Tag 스타일](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tags/library/src/components/Tag/useTagStyles.styles.ts), [고정 그룹 동작](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tags/library/src/components/TagGroup/useTagGroup.ts).


## 포커스 재진입과 접근성 이름

Tab으로 그룹을 나갔다가 Shift+Tab으로 돌아오면 마지막으로 탐색한 활성 태그에 다시 들어갑니다. 마지막 선택 태그를 지울 때는 바깥 컨트롤로 이동하며, 그 컨트롤이 이번 프레임에서 먼저 렌더링된 경우에도 포커스를 보존합니다. 그룹 루트는 별도 실행 동작이 없는 Windows Toolbar로 노출하고 자식 태그가 실제 해제 동작을 제공합니다.

텍스트의 `##`를 내부 이름 구분자로 잘라내지 않고 원문 그대로 접근성 이름에 보존합니다. 사용자가 지정한 `AccessibleName`의 우선순위는 유지합니다. 실제 외부 UIA에서 Toolbar 형식·ARIA 역할·컨트롤/콘텐츠 뷰·자식 Invoke·한글 원문을 확인했습니다. 근거: `artifacts/tag-accessibility-2026-11-13/`. [Microsoft Toolbar 계약](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttoolbarcontroltype).

본문 동작과 별도 ×가 필요한 경우 [InteractionTag](fluent2-interaction-tag.ko.md)를 사용하세요. 선택 강조·아바타 미디어 등 전체 적합성은 별도로 남아 있습니다.

기본 Tag에도 `UiTagOptions.Selected`로 시각적 강조를 줄 수 있습니다. Toggle·선택 기능은 추가하지 않습니다. 제어 선택에는 독립 InteractionTag.Selected 바인딩을 사용하세요. [선택 강조 바인딩](fluent2-interaction-tag.ko.md#선택-강조-바인딩) 참고.


## 빌린 아바타·이미지 미디어

```csharp
using var avatar = Fluent.Avatar("정세일", new(Color: UiAvatarColor.Brand));
using var tag = Fluent.Tag(new UiTagContent("정세일", Media: UiTagMedia.Avatar(avatar)));
// 비트맵 또는 SVG 디코딩 이미지에는 UiTagMedia.Image(image)를 사용합니다.
```

`UiTagContent.Media`는 기본 Tag와 InteractionTag가 공유하는 장식용 선행 슬롯입니다. Icon과 Media 중 하나를 사용하며 둘을 동시에 지정하면 생성 시 실패합니다. 고정 아바타 기본값은 Medium/Small/ExtraSmall에서 28/20/16이고, 태그 모양에 따라 사각·원형을 사용하며 방향도 태그에 맞춥니다. 자동 이니셜은 실제 크기에서 기존 한글·문자 단위 규칙으로 정하며 원본이 바뀌기 전에는 계산한 문자열을 재사용합니다. 빌린 아바타의 크기·모양·방향 설정은 변경하지 않습니다.

각 태그가 비활성 합성·그림자 자원을 따로 연결하고 해제합니다. 하나의 아바타를 같은 프레임에서 서로 다른 크기의 여러 태그가 빌려도 다른 그림 정의가 충돌하지 않습니다. 아바타·이미지 소유자는 사용하는 모든 태그보다 오래 살아 있어야 합니다. 태그를 Dispose해도 빌린 소유자는 Dispose하지 않습니다. 내용 변경은 다음 요청 프레임에 반영하며 타이머·렌더러·셰이더·XML 파서를 추가하지 않습니다. 이미 Dispose된 아바타를 그리면 명시적으로 실패합니다.

`UiTagMedia.Image`는 기존 이미지의 cover 맞춤과 태그 모양을 사용합니다. SVG 래스터 입력은 기존 SVG 이미지 디코더로 한 번 변환하며, 아바타의 빌린 SVG 아이콘은 기존 벡터 경로를 사용합니다. 이니셜과 × 글리프는 폰트입니다. 미디어는 별도 네이티브 Image나 포커스 항목을 만들지 않으므로 태그 텍스트에 필요한 접근성 이름을 넣으세요.

TagGroup의 선택적 `media: item => UiTagMedia.Avatar(owner)` 선택자는 그룹 생성 때 숨겨진 항목까지 원본 항목별 한 번 실행합니다. 아이콘이 있는 항목에는 미디어를 중복 지정하지 마세요. 사용자 표현은 렌더링 콘텐츠로 전달된 바인딩된 미디어의 `Paint`를 호출합니다. 원래 팩토리 값은 독립 painter가 아니며 태그가 연결을 담당합니다.

실행: `./run-gallery.ps1` → **선택 목록**에서 **담당자 미디어**까지 스크롤하세요. 한글 이니셜·SVG 디코딩 이미지·상태 배지, 크기·방향·모양, 비활성 표현, 선택·해제와 **담당자 태그 복원**을 비교합니다. 근거: `artifacts/tag-media-2026-11-13/`. 초과 항목·강제 색상·확장 터치 영역·세부 포커스 표현과 전체 공식 적합성 검증은 남아 있습니다.
