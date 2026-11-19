# Windows 크기 변경과 프레임 제출 순서

## 크기 변경 중 실제 픽셀 용량 재사용

동기식 정확한 크기 후보는 Native30회를 통과했지만 SetWindowPos 최대1024.4454ms가 관측됐다. 누적 GC 정지는8.661ms였으며 마지막13.22초 회차의13.18초가 크기 변경 호출에 쓰였다. 안정성 통과만으로 반응성 요구를 충족하지 않는다. 상세 WSI 계측도 이 부하에 영향을 주므로 낮은 비용의 실행과 구분하며, 드라이버 내부 원인을 확정하지 않는다.

렌더러는 선택한 표시 모드의 surface-maintenance 능력을 조회한다. ONE_TO_ONE과 왼쪽 위 정렬을 지원하면 크기 변경 중 더 큰 스왑체인 이미지 용량을 재사용하고 실제 네이티브 뷰포트만 그린다. 픽셀을 늘리거나 재표본화하지 않는다. 기존 MSAA와 같은64픽셀 단위로 성장하며 축소 중에는 용량을 유지한다. 크기 변경 종료 후 첫 프레임에서 정확한 네이티브 타깃으로 돌아가 MSAA 여유 용량도 반환한다. 미지원 표면은 정확한 크기 경로를 유지한다. 표시 펜스는 이전 객체 파괴를 계속 보호하며 device-idle 우회·표시 모드 전환·품질 저하는 추가하지 않는다.

.NET11 집중 GPU 검사는 유지/전체 갱신과 정확한 비확대 스왑체인의 픽셀, 용량 내 동일 스왑체인 유지, 실제 크기 readback, ABI 크기와 종료 후 용량 반환을 비교한다. 현재 드라이버에서 MSAA1/4는 통과했고 Native 실제 반복·사용자 확인은 남았다. 근거는 `artifacts/resize-stress-2026-11-06/`와 연결된 L 캐시 기록이다. [Khronos의 ONE_TO_ONE 계약은 비확대 픽셀 매핑이다](https://docs.vulkan.org/refpages/latest/refpages/source/VkPresentScalingFlagBitsKHR.html).

## 2026-11-06 실시간 크기 변경 수정

사용자는5BFBD8BE와DC656134 모두 거부했다. 빨라졌지만 잔상이 남고 내용이 변경 중인 창 크기를 즉시 따라가지 못했다. DC656134의 Native 슬롯1·2·3 각각30회 통과는 실제 화면 승인 근거가 아니다. 아래 System WM_SIZE 비동기 경로는 폐기한 실험이다.

현재 후보는 승인됐던DCA37C18의 순서를 복원한다. 확정 WM_SIZE는 해당 크기의 프레임 제출 후 반환한다. 표준 System 타이틀, WVR_REDRAW, 종료 중 메시지 처리, 그래픽 준비 검사, 단조 증가 프레임 나이, 프레임 소유 acquire 세마포어와 소수 DPI 프레임버퍼 반올림 수정은 유지한다. WM_PAINT는 무효화만 한다. 대기 중 sent 메시지와 선택한 비입력 posted 메시지를 처리한다. 회귀 검사는 동기 SetWindowPos 직후 확정 시퀀스의 제출 여부를 즉시 확인하며, 나중에 완료되는 것만으로 통과시키지 않는다.

첫 동기 후보41737B46은 Native16회 완료 후 즉시 제출 검사에 실패했고 회당 시간이 증가했다. 이때 표시 펜스까지 교체의 선행 조건이었다. 현재 후보는 그래픽 완료 후 교체하며, 표시 펜스는 기존 지연 해제 경로에서 파괴의 안전성을 계속 보호한다. [Vulkan 펜스 계약](https://docs.vulkan.org/refpages/latest/refpages/source/VkSwapchainPresentFenceInfoKHR.html)은 자원 수명을 규정하며 실제 화면 표시 시각이나 새 스왑체인 생성의 선행 조건이 아니다. 현재 .NET11 관리 슬롯2·30회 검사는 통과했고 Native·실제 화면 확인은 남았다. 코드의 불필요한 의존성 제거와 드라이버 내부 원인 확정은 구분한다.

## 폐기한 System 크롬 실험

사용자는804F7691의 표준 타이틀 모양 개선을 확인했지만 크기 변경 지연·잔상을 보고했다. 아래DCA37C18 확인은 이전 사용자 정의 크롬 체크포인트이며 System 경로의 완료 근거가 아니다. 최신 후보5BFBD8BE의 실제 화면은 별도 확인 중이다.

System의 확정 WM_SIZE는 중복 없는 전용 메시지를 게시하고 즉시 반환한다. 게시한 메시지에서 표시 완료를 기다려 OS 크기 변경·동기 paint 콜백과 WSI의 순환 대기를 피한다. WM_SIZING의 미래 크기나 타이머 우회를 사용하지 않는다. 명시적인 Duxel 크롬의 기존 WM_SIZE 연결은 보존한다. System 대기는 sent 메시지와 paint만 처리하며 다음 마우스·키 입력을 재귀 소비하지 않는다. WM_PAINT는 새 프레임을 요청하고 그 안에서 기다리지 않는다.

System WM_NCCALCSIZE는 DefWindowProc의 네이티브 프레임 계산을 유지한 뒤 WVR_REDRAW를 반환한다. [Windows의 이전 클라이언트 픽셀 보존 계약](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-nccalcsize)에 따른 처리이며 평상시 레이어·캐시·부분 갱신을 끄는 변경이 아니다. 해상도·MSAA·벡터 품질을 낮추지 않는다.

실제 관리 프로세스 스택에서 UI 스레드는 Thread.Join, 렌더 스레드는 vkCreateSwapchainKHR에 머물렀다. 종료 시 취소·깨우기 후 메시지를 처리하면서 렌더 스레드 종료를 기다리도록 고쳤고 종료 시작 후 동기 WM_NULL을 보내는 집중 검사가 통과했다. 창 소유 스레드 생성 실험도 실패가 남아 제거했다. 검사 시작은 UI 구성과 고정300ms 지연 대신 실제 첫 표시·창 표시 완료를 기다리며 네이티브 가시성도 확인한다. 근거와 미해결 상태는 `artifacts/resize-stress-2026-11-06/`에 보존한다. 짧은 실행·입력 성공을 실제 화면의 잔상·지연 해결로 확대하지 않는다.

2026-11-05. 최신DCA37C18 갤러리에서 사용자가 **검정 영역 없이 빠르고 자연스러움**을 확인했다. 이전371139A9에서는 검정 영역과 지연이 남았다. 이는 현재 갤러리의 연속 외곽 크기 변경 확인이며 전체 Fluent·앱 메모리·Pi 완료를 의미하지 않는다.

## 회귀와 수정

이전 HEAD의 `WM_SIZING`은 제안된 크기를 먼저 게시하고 프레임을 기다렸다. 현재 작업에서 그 대기를 제거하면서 창 크기 확정과 새 프레임의 제출 시점까지 분리됐다. 확장된 창에 맞는 프레임이 준비되기 전 검정 영역이 노출되는 현상을 사용자가 확인했다. 입력·정지 검사나 별도 GPU readback은 실제 창의 이 시간 간격을 확인하지 못했다. 텍스트 CPU 벤치 결과는 이 회귀의 해결 근거가 아니다.

`WM_SIZING`에서 미래 크기를 추측하거나 기다리지 않는다. 실제 크기가 확정된 `WM_SIZE`가 새 프레임의 제출 완료와 연결된다. `MsgWaitForMultipleObjectsEx`는 완료 이벤트와 `QS_SENDMESSAGE`를 함께 기다리며, `PeekMessage`의 `PM_QS_SENDMESSAGE`로 Vulkan WSI가 동기 전송하는 메시지를 처리한다. 다음 마우스 입력을 재귀 소비하는 전체 메시지 펌프는 실행하지 않는다. 렌더 오류·세션 종료는 대기를 취소한다. 독립 프레임 생성 스레드와 invalidation callback이 연결된 호스트에 적용하며 창 스레드에서 직접 렌더링하는 호스트는 자기 자신을 기다리지 않는다.

크기 변경 종료 메시지가 중첩 전달된 뒤에도 최신 확정 치수를 직접 읽는다. 바깥 이벤트 루프가 `WM_SIZE`에서 돌아와 입력 스냅샷을 갱신해야만 새 크기를 읽을 수 있는 순환 의존을 제거했다. 정적/동적 레이어와 캐시·MSAA4·실제 해상도·VSync 정책은 유지한다. 임의 FPS 제한, 해상도 축소나 벡터 품질 저하는 없다.

여기서 완료 통지는 `vkQueuePresentKHR` 반환 후의 성공적인 제출이다. 물리 화면에 도달한 시각 또는 input-to-photon 측정으로 부르지 않는다. 검정 영역 제거와 자연스러운 반응은 별도의 실제 사용자 확인으로 남긴다.

## 회귀 검사와 한계

새 검사는 실제 `session.RequestFrame` 연결을 사용한다. 대기를 비활성화한 테스트 전용 대조군은 ‘WM_SIZE가 해당 크기 프레임 제출 전 반환’ 검사에서 실패한다. 수정 경로는 통과하며 렌더 스레드가 대기 중 UI 스레드에 동기 메시지를 보내도 완료되는지 확인한다. 클릭 좌표는 실제 버튼 영역×현재DPI다. 버튼 활성 상태와 실제 mouse-down을 함께 확인한 뒤 release를 보낸다.

.NET10 전체1035개 통과. 고정 RC3 Native 프레임 자원1/2개 검사는 통과했다.3개 구성의 한 실행에서 release 시간 초과가 있었고, 관측 추가 실행은 통과했다. 이 한 차례 실패를 재실행 성공만으로 해결했다고 기록하지 않으며 집중 안정성 결과는 verification.json에서 구분한다. 이전 스왑체인 단계 벤치와 GPU 이미지 근거는 호환 범위에서 재사용하며 새 사용자 확인을 정량 속도 향상률로 환산하지 않는다.

근거: `artifacts/resize-coherence-2026-11-05/user-acceptance.json`, `interactive-run.json`, 각 stdout/stderr. 원본 Windows 구현과 테스트를 before.cs로 보관한다. 이전 실행 파일을 덮어쓰거나 사용자 창을 임의 종료하지 않는다.

공식 계약: [Vulkan Win32 WSI의 메시지 펌프 요구](https://docs.vulkan.org/spec/latest/chapters/VK_KHR_surface/wsi.html), [MsgWaitForMultipleObjectsEx](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-msgwaitformultipleobjectsex), [PeekMessage의 sent-message 처리](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-peekmessagew), [WM_SIZE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-size).


추가 안정성 관측: 실제 mouse-down을 요구한 Native3자원 독립3회(총9pass),각4검사 모두 통과. 최종 같은 스레드 호스트 guard를 포함한 관리4검사와 net8/9/10 빌드(경고0/오류0)도 통과했다. 이전 release 시간 초과의 원인은 여전히 미확정이다.
