# 현재 Native 검증 도구 상태

2026-11-18에 .NET SDK 11.0.100의 `dotnet publish`로 갤러리의 Windows x64 NativeAOT 빌드와 링크를 완료했습니다. 게시 과정에서 MSVC 14.52.37025와 Windows SDK 10.0.26100.0을 자동으로 찾았습니다. 실행 파일은 .NET 11.0.0으로 Vulkan 표시 시나리오 100개를 통과하고 종료 코드 0으로 끝났습니다. 게시 로그와 실행 결과는 `W:/Cache/Duxel/gallery-source-20261117/native-ga1900`에 있습니다.

## 이전 점검 기록

이전 성공 로그 `artifacts/fluent-link-2026-11-06/gallery-native-publish.log`는 `P:/Program Files/Microsoft Visual Studio/18/Community/VC/Tools/MSVC/14.51.36231/bin/HostX64/x64/link.exe`, 파일 버전 `14.51.36260.0`을 기록합니다. Native 코드 생성을 포함하지만 이전 net10 라이브러리 의존 행도 있으므로 현재 소스나 전체 net11 검증의 근거로 재사용하지 않습니다.

2026-11-11 경로 복구 점검에서 파일 시스템 드라이브는 C:·L:·W:뿐입니다. 로그의 링커 경로는 사용할 수 없고 현재 실행 환경에는 P:의 mounted-device 항목과 사용자 네트워크 매핑도 없습니다. 이전 제한 점검에서는 표준 vswhere·SDK 라이브러리 경로와 cl/link 명령을 찾지 못했습니다. 이는 이 환경에서 도구를 사용할 수 없다는 근거이며 컴퓨터 전체 미설치·삭제를 입증하지 않습니다. 이전 L: 런타임·캐시 접근 거부는 빈 디렉터리로 해석하지 않습니다.

마운트·설치·탐색 우회·반복 Native publish는 수행하지 않았습니다. 독립 구현을 계속하면서 현재 C++/Windows SDK 위치를 확인 요청했습니다. 사용 가능한 도구가 확인되면 링커·Windows SDK 라이브러리를 검증하고, 고정 SDK/런타임11 RC3·win-x64로 현재 소비 앱을 게시하여 실제 Native 생성·경고를 확인한 뒤 CPU/GPU 실행과 현재 관리형/Native 이미지를 대조해야 합니다. .NET9/10은 전체 목표 최종 배포 컴파일 때만 수행합니다. 전체 목표는 활성 상태이며 Native/런타임 비교는 미검증입니다.

현재 근거: `artifacts/multiline-elements-2026-11-11/native-path-audit.json`. 표준 경로 근거: `artifacts/text-commands-2026-11-11/native-prerequisite-recheck.json`, `artifacts/allocation-routing-2026-11-10/native-toolchain-audit.json`.
