# Persona — 인물 정보

```csharp
var person = Fluent.Persona(new UiPersonaContent("정세일",
    SecondaryText: "개발자", TertiaryText: "Duxel", QuaternaryText: "서울"));
person.Avatar.Options = new(Color: UiAvatarColor.Brand, Presence: new());
person.Avatar.PresenceText = "사용 가능";
person.Options = new(UiPersonaSize.Huge, TextPosition: UiPersonaTextPosition.Below);
```

Persona는 최대 네 줄의 단일 행 정보와 소유 아바타 또는 상태 전용 배지를 결합합니다. `PrimaryText`를 지정하지 않으면 `Name`을 표시하며 빈 슬롯은 해당 줄을 숨깁니다. `Content.Name` 변경은 아바타 이름도 갱신합니다. 표시 이름을 다르게 하려면 `Content.PrimaryText`를 사용하세요. 제어 문자·줄바꿈은 거부합니다. 유한한 부모 할당 안에서 텍스트를 자르며 자동 말줄임·줄바꿈·슬롯별 Tooltip은 남아 있습니다.

| 크기 | 아바타 본체 | 상태 전용 배지 | 아바타 간격 |
| --- | ---: | --- | ---: |
| ExtraSmall | 20 | Tiny | 6 |
| Small | 28 | ExtraSmall | 8 (상태 전용6) |
| Medium | 32 | Small | 8 |
| Large | 36 | Medium | 10 |
| ExtraLarge | 40 | Large | 10 |
| Huge | 56 | Large | 12 |

텍스트는 미디어 뒤·앞·아래에 놓습니다. 시작·중앙 정렬과 논리적 RTL 배치를 지원합니다. `PresenceOnly`는 `Avatar.Options.Presence`를 사용하며 상태가 없으면 빈 미디어 간격도 만들지 않습니다. `ShowMedia: false`는 텍스트만 표시합니다. 큰 크기의 글꼴 단계·보조 행 글꼴·둘째 줄 간격과 상태/첫 줄 정렬은 고정 Fluent 소스를 기준으로 합니다.

`Avatar`는 키를 가진 소유 자식입니다. Source·아이콘·색상·상태·활동·비활성 변경은 기존 아바타 경로를 사용하고 Persona의 조상 배치를 무효화합니다. 표시 아바타의 크기·방향은 Persona 옵션이 정하며 원래 아바타 옵션 값은 바꾸지 않습니다. 독립 비활성 합성·그림자 자원을 가진 시각 바인딩 하나를 소유합니다. 소유 트리에서 Persona를 Dispose하면 Avatar도 함께 Dispose됩니다. 렌더러·네이티브 import·파서·타이머를 추가하지 않습니다.

하나의 수동 네이티브 Group이 표시된 모든 정보와 지역화한 표시 상태를 제공합니다. 가짜 Invoke·Toggle·편집 값·키보드 경유점은 만들지 않습니다. 표현·내용을 바꿔도 공급자 식별자를 유지하고 Persona를 제거하면 공급자를 폐기합니다. 지역화한 상태는 `Avatar.PresenceText`에 지정하세요. 사용자 표현은 `UiPersona<TPresentation, TAvatarPresentation>`과 명시적 `IUiPersonaPresentation` 측정·그리기 계약을 사용합니다.

실행: `./run-gallery.ps1` → **인물 정보**. 탭이 숨겨졌으면 상단 더 보기에서 선택하세요. 이름·여섯 크기·텍스트 위치/정렬/방향·줄 수·상태 전용/텍스트 전용·SVG 이미지/이니셜·비활성 표현을 바꿀 수 있고 탭을 왕복해도 유지됩니다. SVG는 기존 이미지 디코더를, 이니셜은 폰트를 사용합니다.

기준: [고정 동작 소스](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-persona/library/src/components/Persona/usePersona.ts), [고정 스타일 소스](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-persona/library/src/components/Persona/usePersonaStyles.styles.ts). 스냅샷·해시·검증 기록은 `artifacts/persona-2026-11-13/`에 있습니다. 큰 GPU 결과는 용량을 검사한 `L:/Cache/Duxel/persona-2026-11-13-evidence/`에 명시적으로 저장합니다. 카탈로그 연결이 전체 적합성 완료를 뜻하지 않습니다. 실제 스크린 리더·강제 색상·정확한 CSS/말줄임 동작과 고정 RC3 NativeAOT/런타임 이미지/성능/실기기 관문은 남아 있습니다.

`Avatar.Options.Presence.Size`를 명시하면 상태 전용 기본 크기보다 우선하며, 필요한 경우 첫 행 높이를 늘려 배지가 경계 안에 들어오게 합니다.

표시된 아바타 활동은 `Avatar.ActivityText`로 설명하며, 비활성·상태 전용·미디어 숨김에서는 보이지 않는 활동을 알리지 않습니다. 아바타의 계산된 상태 문자열도 재사용합니다.
