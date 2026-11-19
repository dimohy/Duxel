# Fluent AvatarGroup

[English](fluent2-avatar-group.md)

Last synced: 2026-11-13

`Fluent.AvatarGroup`는 구성원별 이미지 임대와 입력·팝업 상태를 소유합니다. 펼침(Spread), 겹침(Stack), 원 분할(Pie)을 지원합니다. 기본은32px 펼침이며 인라인 최대 슬롯은5개입니다. 초과 인원이 있으면 마지막 슬롯을 정확한 초과 수 버튼에 사용하고 앞쪽 구성원을 목록으로 옮깁니다. 최대 슬롯이1이면 버튼만 표시합니다. 원 분할은 첫 세 구성원을 한 원에 표시하고 원을 누르면 전체 구성원 목록을 엽니다.

```csharp
using var group = Fluent.AvatarGroup("공동 작업자",
    [new("jeong", "정세일"), new("ada", "Ada Lovelace"), new("grace", "Grace Hopper")],
    new(UiAvatarSize.Size48, UiAvatarGroupLayout.Stack),
    selected: value => OpenPerson(value));
group.Render(ui);
```

`UiAvatarGroupItem.Value`는 고유한 이동 값이며 `Name`은 전체 접근성 이름입니다. 이름·값이 비어 있거나 중복된 값은 생성 전에 실패합니다. `UiWidgetIdentity.Parent`를 직접 쓰는 여러 그룹도 이름이 다르면 내부 버튼 ID를 분리합니다. 같은 Home 값을 쓰는 Breadcrumb에도 이 격리를 적용했습니다.

항목을 복사해 보관하며 공개 API로 목록을 교체하지 않습니다. `Initials`, 불변 `Source`, 빌려 쓰는 `Icon`, 이름 색과 활동 링·상태 배지를 항목마다 지정할 수 있습니다. 기존 Avatar의 한글 첫 음절·이미지·SVG 캐시와 리소스 수명을 재사용합니다. 원본 이미지·아이콘 소유자는 그룹을 해제할 때까지 유지하세요.

`Options`에서14개 아바타 크기, 배치, 최대 인라인 슬롯, RTL과 지역화한 목록 버튼 이름을 바꿉니다. 펼침은 활동 링·상태 배지를 표시하고 겹침은 활동 링만, 원 분할은 둘 다 표시하지 않습니다. 활동 링과 큰 배지의 전체 시각 영역을 예약합니다. 부모의 최대 크기가 더 작으면 공통 클립을 따릅니다. 가용 폭에 맞춘 슬롯 수 자동 변경은 하지 않으므로 호출자가 최대 슬롯을 선택합니다.

겹침은 크기에 따른 음수 간격과 중립 배경2 구분선을 사용합니다. 원 분할은 기존 analytic 둥근 클립과 사각 클립을 함께 사용합니다. 새 셰이더·다각형 대체 경로가 없습니다. 두 구성원은 좌우 절반, 세 구성원은 첫 구성원 절반과 나머지 두 구성원 사분면입니다. RTL에서는 표시 방향을 뒤집고 의미 순서는 유지합니다. 반 크기 이니셜은 원래 글꼴을 절반 크기로 래스터화합니다.

수동 그룹과 구성원별 Image를 공개하고 초과 버튼만 키보드 정지점·Invoke·ExpandCollapse를 제공합니다. 버튼의 모서리 밖 클릭은 원형 영역에서 제외됩니다. 실제 공통 Menu가 구성원 선택·스크롤·Escape와 포커스 복원을 처리합니다. 목록의 아바타 크기는 그룹 크기를 따르고 최대 높이는220px입니다. 현재 목록은 동작 가능한 MenuItem을 사용하며 웹의 수동 목록과 완전히 같은 접근성 구조를 주장하지 않습니다. 그룹의 AccessibleName 재정의가 내부 버튼 이름을 덮지 않습니다.

측정·분할·레이블·아이콘 설명자는 유지하며 요청된 프레임의 입력·그리기를 처리합니다. 주기 프레임이나 XML 재파싱을 추가하지 않습니다. 초과 목록의 원래 컨텍스트와 스크롤 참조는 공통 소유자에서 정리합니다. 같은 소유자를 Breadcrumb에도 적용했습니다. 시간·메모리 개선율은 측정하지 않았습니다.

`./run-gallery.ps1` → **아바타 그룹**에서 (탭이 보이지 않으면 상단 더 보기 메뉴를 사용하세요) 배치·크기·표시 수·방향을 바꾸고 숫자 버튼 또는 원을 누르세요. 이미지·한글·SVG 아이콘을 함께 표시하고 탭을 왕복해도 설정과 선택 이름을 유지합니다.

근거는 `artifacts/avatar-group-implementation-2026-11-12/verification.json`입니다. 공식 전체 상태·색상·강제 색상·Tooltip·동적 항목 교체, 원래 고정 RC3 NativeAOT·성능·기기별 관문은 남아 있습니다. 팩터리 연결은 전체 Fluent 적합성 완료가 아닙니다.

참고: [공식 사용 가이드](https://fluent2.microsoft.design/components/web/react/core/avatargroup/usage), [고정 구현 원본](https://github.com/microsoft/fluentui/tree/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-avatar/library/src/components/AvatarGroup). 원본 스냅샷은 `artifacts/avatar-group-2026-11-12/sources.json`에 기록합니다.
