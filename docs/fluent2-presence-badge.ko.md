# 상태 배지

`Fluent.PresenceBadge(new UiPresenceBadgeOptions(Status: UiPresenceStatus.Available, Size: UiBadgeSize.Medium))`로 정적인 상태 이미지를 만듭니다. 기본값은 사용 가능·Medium(논리16px)·외근 꺼짐입니다. `Options` 변경은 프레임을 요청하며 같은 값이나 그대로 그리는 동작은 주기 프레임을 요청하지 않습니다. 상태 문구는 변경할 때 저장합니다. 기본 표현은 측정값을 재사용하며 사용자 `IUiPresenceBadgePresentation`은 명시적으로 선택합니다.

Available·Away·Busy·DoNotDisturb·Offline·OutOfOffice·Unknown·Blocked의8개 상태를 지원합니다. Tiny/ExtraSmall/Small/Medium/Large/ExtraLarge 크기는6/10/12/16/20/28입니다. `OutOfOffice`는 상태와 조합해 공식 아이콘·색 규칙을 적용합니다. 밝음·어두움 전경색과 배경 원은 고정한 Fluent 소스를 따릅니다. 부모의 실제 할당 영역과 클립을 사용하고 직사각형 안에는 정사각형을 가운데 배치합니다. 잘못된 열거형이나 표현의 크기는 오류로 처리합니다.

기본 아이콘은 컴파일된 Fluent 아이콘 폰트의 정확한10/12/16/20 글리프를 사용하며6/28에는 크기를 조정합니다. XML이나 실행 중 아이콘 카탈로그 조회·리플렉션은 사용하지 않습니다. 선택적인 `Icon`은 `UiMenuIcon`을 빌려 씁니다. SVG는 기존 `UiSvgIcon.MenuIcon`을 전달하며 위젯→painter→문서/store 순서로 해제하세요. 기존 SVG 캐시를 재사용하고 빌린 자원을 위젯이 해제하지 않습니다.

접근성에는 포커스를 받지 않는 Image(Windows50006) 하나와 기본 상태 이름을 노출합니다. `AccessibleName`으로 한국어 문구를 지정하세요. Invoke·Value·호버·클릭·진행 애니메이션·타이머가 없습니다. Avatar 위에 겹치기·투명한 잘라내기·활동 링·모션·강제 색·전체 참조 적합성은 후속 범위이며 현재는 독립 배지입니다.

저장소 루트에서 `./run-gallery.ps1` 실행→**아바타**→**상태 배지**를 확인하세요. 버튼으로 상태·외근·크기를 바꿉니다. 작은 창에서는 페이지를 스크롤하며 탭을 바꿔도 상태를 유지합니다.

참조는 microsoft/fluentui 커밋8830c10bfb2f4c1bdd540287b09713b9679cd1ad의 react-badge PresenceBadge와 기존 Fluent 폰트입니다. 소스·라이선스 사본은 `artifacts/fluent-presence-2026-11-12`에 있습니다. [공식 Badge 사용 안내](https://fluent2.microsoft.design/components/web/react/core/badge/usage).

검증: CPU50/50계약·8086회 검사(상태 배지61·관찰179), 생성기117, GPU 상태 배지552·Avatar100·관찰3·둥근 이미지40이 통과했습니다. 해당 밝음/어두움·MSAA1/4 조합을 포함합니다. 별도 Windows 접근성 호스트/클라이언트는 상태 배지11개와 기존5개 프로필66개를 통과했습니다. 작은 빨간 상태 모양도 화면으로 확인했습니다. 실제 스왑체인의 각 이미지를 준비한 뒤 다시 그릴 때 draw와 스트리밍 업로드0을 확인했으며 전체 프레임 CPU0을 의미하지 않습니다. SDK11.0.100/runtime11.0.0 관리 실행 검증이며 원래 고정 RC3 NativeAOT·전체 부하 검증은 남아 있습니다.

Windows의 물리10px 미만 글꼴은 기존2배 슈퍼샘플링을 사용하고 회색조 RGB에 폰트 셰이더가 기대하는 커버리지를 넣습니다. 단일·혼합 글꼴 경로를 검사했습니다. 작은 방해 금지 획을 복구하는 대신 첫 래스터 비용은 증가했습니다. [측정 기록](archive/performance/optimization-session-2026-11-12.md)을 참고하세요. 글리프 캐시는 그대로이며 셰이더 바이너리는 변경하지 않았습니다.

Avatar는 `UiAvatarOptions.Presence`로 이 표현을 직접 사용합니다. 실제 투명 마스크와 이름을 합친 Image 하나를 유지합니다. [Avatar 겹침 계약](fluent2-avatar.ko.md#avatar-상태-배지-겹침)을 참고하세요.
