# Linux 텍스트 조판과 행 위치

Last synced: 2026-11-16

[스타일 문단 기능](styled-paragraphs.ko.md)을 일반 문자열용 `UiInlineText`에 연결했습니다. 고정 .NET11 RC3 NativeAOT에서 스타일351개·기존 위치334개·텍스트 기준114개가 통과했습니다. 하나의 네이티브 결과로 줄바꿈·기준 방향·원문 커서·래스터 및 벡터 위치·Core 배치를 공유합니다. Markdown과 실제 인라인 자식 연결은 남아 있습니다. 잉크 없는 글리프는 배치에 보존하고 잘라 그리는 Cairo 호출에서만 제외해 가장자리 픽셀을 유지합니다. Pango1.50 이상이 필요하며 백엔드 생성 시 검사합니다.

Linux 텍스트 백엔드는 Pango로 한 행 전체의 스크립트 분리·양방향 순서·HarfBuzz 조판을 계산합니다. 래스터·측정·벡터 출력은 그 결과의 시각적 글리프 런을 기존 Cairo/FreeType 및 HarfBuzz 윤곽 모듈에서 함께 사용합니다. Windows는 Linux 의존성을 추가하지 않고 DirectWrite를 계속 사용합니다.

백엔드마다 전용 폰트 맵을 최대16개 보관합니다. 빈 Fontconfig 설정에 명시적으로 받은 파일만 등록하며 프로세스의 전역 설정을 교체하거나 다른 폰트를 검색하지 않습니다. 기존의 주 폰트 우선 규칙으로 연속 폰트 구간을 정한 뒤 조판하고, 반환된 각 런의 실제 파일·페이스 인덱스를 대조합니다. 없는 글리프는 명시적으로 실패합니다. 같은 폰트가 이어지는 구간은 합쳐 커닝·합자·한글 조합을 보존합니다.

`IPlatformTextGeometryBackend.CreateLineGeometry`는 원문 UTF16 위치를 보존하는 불변 스냅샷을 반환합니다. 같은 스타일의 Core 행은 이 결과의 커서 위치·분리된 시각적 선택 조각·네이티브 클릭 경계를 재사용합니다. 결합 문자와 아랍어는 클릭이 다음 문자로 넘어가는 지점이 도형의 중간과 다를 수 있습니다. `PlatformTextCharacterGeometry.HitBoundaryX`는 생성 시 그 경계를 저장하므로 이후 클릭에는 네이티브 레이아웃이 필요하지 않습니다. 네이티브 레이아웃과 반복자는 반환 전에 해제합니다. 탭은 원문 한 글자를 유지하면서 공백 네 개로 그립니다.

Cairo 회색조 커버리지는 명목상 잉크 경계를 한 픽셀 넘을 수 있습니다. 래스터 할당에는 이 여유를 포함하고 논리적 이동 폭은 유지합니다. 빈 출력은 빈 상태로 남습니다. 독립 기준 구현이 Pango 전체 레이아웃을 직접 그리며, 제품 렌더러끼리 비교하지 않고 커버리지 바이트가 정확히 같은지 검사합니다.

## 의존성

Linux에는 FreeType·Cairo·HarfBuzz·Pango>=1.50·PangoCairo·PangoFc·Fontconfig·GLib/GObject가 필요합니다. Ubuntu의 Pango1.52.1·HarfBuzz8.3.0으로 검증했습니다. Linux 네이티브 라이브러리이며 Skia나 관리형 GUI 프레임워크 의존성은 아닙니다. Ubuntu의 일반 런타임 패키지는 기존 FreeType/Cairo/HarfBuzz 외에 `libpango-1.0-0`·`libpangocairo-1.0-0`·`libpangoft2-1.0-0`·`libfontconfig1`입니다. 이번 검증에서 시스템 패키지를 설치하거나 전역 폰트 설정을 바꾸지는 않았습니다.

## 실행하고 확인하기

Windows 저장소 루트에서 실행합니다.

```powershell
& 'W:/MyWorks/Duxel/artifacts/linux-present-path-2026-11-15/run-observer.ps1'
```

검증 원장은 NativeAOT 실행본 `7746d0b7`를 선택하고 전체 해시를 확인합니다. 실행기는 명시적인 폰트 다섯 개를 전달하고, 실행한 프로세스의 llvmpipe 작업 스레드를4개로 제한합니다. **입력·IME → 커닝·혼합 문장 예제**에서 커닝과 혼합 방향 문장을 살펴보세요. 이전 전달본 `ce552ef3`는 별도로 보존합니다.

`tests/Duxel.Vulkan.Linux.Tests`의 `paragraph-geometry` 집중 프로필은 새 결과 경로, 표본 수1, 일반·굵은·한글·고정폭 폰트 다섯 경로, 프로필 이름 순서로 받습니다. GUI 없이 실행합니다. 네이티브 검사334개가 통과했습니다. 문구11개를 다섯 크기로 검사하며 이동 폭·정확한 네이티브 픽셀·원문 그래핌 커서·Core 결과 재사용·네이티브 클릭 위치와 앞뒤 경계·선택 영역을 각각 확인하고 자원·폰트 격리 검사 네 개를 더합니다. 텍스트 기준114개·실제 옵저버41개·전달 실행기38개 시나리오도 통과했습니다. Windows의 관련 위치·조판·입력·Markdown 회귀 검사도 통과했습니다. [검증 근거](../artifacts/fluent-messagebar-2026-11-15/linux-paragraph-verification.json).

검증 범위는 LTR 문단 기준 방향과 명시적 폰트 선택을 사용하는 동일 스타일 행입니다. 혼합 스타일 문단 구성·전체 줄바꿈과 기준 방향 통합·시각적 키보드 이동·컬러 폰트·이형 문자 시퀀스·전체 접근성·물리 입력 및 표시 지연은 남아 있습니다. 새로운 속도 향상이나 성능 회귀 없음을 주장하지 않습니다. MessageBar는12/13이며 전체 프로젝트 완료가 아닙니다.

네이티브 참고: [전용 폰트 맵 설정](https://gnome.pages.gitlab.gnome.org/gtk/PangoFc/method.FontMap.set_config.html), [글리프 런](https://docs.gtk.org/Pango/struct.GlyphItem.html), [문자 영역](https://docs.gtk.org/Pango/method.Layout.index_to_pos.html), [클릭 위치](https://docs.gtk.org/Pango/method.Layout.xy_to_index.html), [시각적 선택 구간](https://docs.gtk.org/Pango/method.LayoutLine.get_x_ranges.html).
