# Fluent 2 NavigationBar

`Fluent.NavigationBar(label, title, leading, trailing, secondary, options)`으로 현재 화면의 헤더를 만듭니다. 보조 기술에서는 `label`을 탐색 영역의 이름으로 읽습니다. 실행 중에 제목을 바꿔도 양쪽 버튼은 상태를 유지합니다. 동작만 표시하려면 제목을 비워 두세요. 헤더에 넣은 위젯은 헤더가 소유합니다.

양쪽 동작의 너비가 달라도 제목은 가운데에 놓입니다. 공간이 부족하면 제목이 다음 줄로 내려가고, 두 동작도 한 줄에 들어가지 않으면 각각 줄을 차지합니다. 보조 행에는 `Fluent.SegmentedControl` 같은 위젯을 넣을 수 있습니다. `Options.RightToLeft`는 양쪽 동작의 배치를 뒤집습니다. 안에 넣은 컨트롤도 오른쪽에서 왼쪽으로 조작해야 한다면 그 컨트롤의 방향을 지정하세요.

`UiNavigationBarAppearance`로 테마 배경, 투명 배경, 밝은 글자를 쓰는 파란색 그라데이션을 선택할 수 있습니다. 시스템 대비 테마가 켜져 있으면 시스템 색상을 따릅니다. `Options.Width`를 지정하면 부모가 허용하는 범위에서 그 너비를 사용합니다.

Windows에서 `./run-gallery.ps1`을 실행하고 **동작과 탐색 → NavigationBar**를 여세요. 첫 예제에서는 제목과 너비를 바꿔 볼 수 있습니다. 둘째 예제에서는 브랜드 배경과 기간 선택을 확인할 수 있습니다. 각 예제 바로 아래에 C# 소스가 있습니다. [Fluent 2 Navigation bar 가이드](https://fluent2.microsoft.design/components/ios/core/navigationbar/usage)

Windows 갤러리에서 NavigationBar를 포함한 100개 화면을 표시했습니다. 집중 검사 13건으로 동작 버튼, 제목 배치, RTL, 좁은 너비, 예제 소스, 브랜드와 시스템 대비 테마의 Vulkan 픽셀을 확인했습니다. 두 예제 소스는 Windows용과 플랫폼 공통 .NET 11에서 각각 컴파일했습니다.

WSLg의 Linux x64 .NET 11.0.0 관리형 빌드와 앞서 게시한 NativeAOT 빌드에서 외부 `atspi-gallery-navigation-bar` 검사 4건씩을 통과했습니다. 접근성 클라이언트에서 이름이 붙은 탐색 영역과 제목, 양쪽 버튼을 찾았습니다. 돌아가기와 저장을 실행해 제목을 바꿨고, 보조 행에서 주간을 선택한 뒤 오늘 버튼으로 일간을 다시 선택했습니다. 실제 화면 읽기 프로그램의 음성 출력은 미검증입니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/navigation-bar-atspi-2035/verification.json`입니다.

브랜드 배경 예제의 기간 버튼은 ‘일간’, ‘주간’, ‘월간’을 전부 표시할 수 있는 너비를 사용합니다. 창이 좁을 때는 가로로 스크롤해 나머지 기간을 선택할 수 있습니다.

부모가 전달하는 최대 너비가 달라도 기간 선택의 실제 너비가 같으면 배치를 재사용합니다. 합성 텍스처가 바뀌면 그 텍스처를 쓰는 영역만 다시 그립니다. Linux 갤러리 검사는 연속 모드에서 제목 동작과 기간 선택을 실행하고 Vulkan이 실제로 다시 그린 범위를 기록합니다.
