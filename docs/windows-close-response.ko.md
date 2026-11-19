# Windows 창 닫기 응답

갤러리에서 닫기를 누르면 렌더링 스레드·텍스트·Vulkan 자원을 정리한 뒤 HWND를 파괴합니다. 이전에는 이 과정 동안 창도 계속 보여 약 0.1초 멈춘 것처럼 보였습니다. 현재는 트레이가 종료를 가로채지 않고 WM_CLOSE를 수락했을 때 먼저 창을 숨깁니다. 실제 HWND·Vulkan 표면·접근성 공급자의 안전한 정리 순서는 유지합니다.

첫 구간 측정에서 렌더링 스레드 대기는 약 0.1ms, UiContext 해제는 약 3.3ms였고 Vulkan 해제에는 약 102.5ms가 걸렸습니다. 그중 인스턴스 해제 구간은 약 52.4ms였습니다. 이는 한 실행의 관측이며 드라이버 내부 작업을 분해해 측정한 결과는 아닙니다.

동일한 기본 갤러리에서 첫 프레임 후 250ms 기다린 뒤, 변경 전후 각각 3개의 독립 프로세스를 실행했습니다. 실행 순서는 after1→before1→before2→after2→after3→before3입니다. 표시 상태는 Win32 WS_VISIBLE 기준이며 DWM의 마지막 픽셀 소멸 시간을 측정한 것은 아닙니다.

| 구간 중앙값 | 변경 전 | 변경 후 |
| --- | ---: | ---: |
| 닫기 요청→숨김 | 116.94ms | 13.77ms |
| 닫기 요청→HWND 소유 종료 | 121.30ms | 77.56ms |
| 닫기 요청→프로세스 종료 | 130.91ms | 82.80ms |

숨김 응답은 약 88.22% 줄었습니다. 프로세스 종료 시간도 이번 표본에서는 줄었으나 모든 화면·드라이버의 자원 해제가 같은 비율로 빨라진다는 뜻은 아닙니다. 변경 후 세 실행 모두 숨김 뒤에도 HWND가 유지됐고, 강제 종료 없이 종료 코드 0으로 끝났습니다. Core·App·Vulkan·Gallery DLL 지문은 양쪽이 같으며 Windows 플랫폼 DLL만 다릅니다. FPS·프레임 시간 p95/p99는 종료 응답 측정의 지표가 아니므로 산출하지 않았습니다.

기존 LogStartupTimings 진단을 켜면 ShutdownTiming과 VulkanShutdown 구간도 남깁니다. 별도의 타이머·백그라운드 정리 경로를 추가하지 않습니다. 측정기는 직접 만든 프로세스의 창 소유 PID를 확인하고 WM_CLOSE만 보냅니다. 다음과 같이 빌드된 측정 클라이언트에서 재현합니다.

```text
Duxel.Graphics.Tests.exe <새 결과 폴더> gallery-close-latency <Duxel.Gallery.exe 절대 경로>
```

근거: artifacts/gallery-close-latency-2026-11-17/result.json. Windows 갤러리와 Linux 호스트 빌드는 경고·오류 0입니다. 실제 Linux·고정 RC3 NativeAOT·외부 UIA TooltipClosed 종료 검증은 별도 미완료 범위입니다. 장시간 사용한 무거운 화면과 앱 내부 RequestExit는 이 WM_CLOSE 비교에 포함하지 않았습니다. 창을 완전히 파괴하기 전에 숨기는 동작이므로 트레이와 모달 소유자 등의 별도 동작을 임의로 바꾸지 않습니다.

같은 명령에서 `gallery-close-time-popup`을 사용하면 UI Automation으로 TimePicker 팝업과 분 휠이 보이는 것을 확인한 뒤 WM_CLOSE를 보냅니다. Windows x64 .NET 11.0.0 NativeAOT 실행에서 창은 10.83ms 후 숨겨졌고 프로세스는 184.10ms 후 정상 종료했습니다. 개요 화면 대조 실행도 정상 종료했습니다. 각 조건을 한 번 확인한 값이며 TooltipClosed 이벤트의 외부 전달은 별도 검증입니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/gallery-popup-close-1935/gallery-close-time-popup/result.json`입니다.

`Duxel.Gallery.exe --verify <새-결과-폴더>`는 앱 내부의 `DuxelAppSession.Exit()` 경로를 검증합니다. Windows x64 .NET 11.0.0 관리형과 NativeAOT 빌드가 같은 표시 시나리오 104개를 순서대로 마쳤습니다. 두 실행 모두 렌더 스레드, UI 컨텍스트, Vulkan 인스턴스를 순서대로 정리하고 종료 코드 0으로 끝났습니다. 근거는 `W:/Cache/Duxel/gallery-source-20261117/gallery-session-exit-1936/comparison.json`입니다.

Win32 근거: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-iswindowvisible
