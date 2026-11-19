# Panel과 앱 영역 직접 구성

Last synced: 2026-11-05

내부에서 이동·크기 변경·접기·펼치기를 제공하던 Window는 이제 선택적인 **Panel**이다. 운영체제가 소유하는 바깥 앱 Window와 구분한다. 기본 샘플은 앱 작업 영역에 직접 구성하며, 화면을 Panel로 감싸지 않는다.

```csharp
// Default: direct application content.
Dux.App(Dux.Column(12, Fluent.Title3("My app"), Fluent.Body1("Content")));

// Optional: a movable, collapsible internal panel.
Dux.Panel("Inspector", Dux.Column(12, Fluent.Body1("Details")),
    new UiPanelOptions(Position: new(24,24), Size: new(420,300),
        PositionSpace: UiPanelPositionSpace.WorkArea, AutoHeight: true));

// Immediate API, sharing the same container implementation.
ui.BeginPanel("Inspector");
ui.Text("Details");
ui.EndPanel();
```

`UiDefaultPanel`은 생성기가 `UiPanel<IUiView>` 상속과 생성자를 컴파일 시점에 확정한다. 사용자도 같은 공개 기반 클래스를 상속한다. 문자열 기반 타입 조회·리플렉션·런타임 테마 선택은 추가하지 않는다. Panel은 기반 시설 컴포넌트로 표시하므로 공식 Fluent 컴포넌트 카탈로그에 포함하지 않는다.

`UiPanelOptions`는 최초 위치·크기를 기본으로 적용하고 이후 사용자의 이동을 보존한다. `Placement=Always`는 매번 지정하는 명시적 동작이다. `Open` 바인딩과 포커스·최상위·AutoHeight를 지원한다. `SetPanelCollapsed`와 `SetNextPanelCollapsed`는 펼쳐진 크기를 보존하여 복원한다. Panel 관련 위치·크기·스크롤·그리기 API는 `Panel` 이름으로 제공한다.

기존 `BeginWindow/EndWindow`, `Dux.Window`, `DuxelView.Windows`, `UiWindowOptions`는 삭제하지 않는다. Legacy 특성과 EditorBrowsable(Never)로 표시하고 같은 Panel 구현으로 전달한다. DSL의 `Panel`이 기본 이름이며 기존 `Window/BeginWindow`도 `BeginPanel`로 정규화한다. 바깥 앱의 `DuxelWindowOptions`는 그대로 유지한다.

기본 갤러리의 종합·타이포그래피·Progress·Slider 화면은 직접 배치한다. `--panel`만 접기·펼치기 시연이다. `all_features.cs`는 메뉴에서37개 페이지를 선택하여 직접 표시하며 `--panels`가 명시적인 예전 배치 검증 모드다. Hello·Theme·Dashboard·입력/레이아웃 등 기본 샘플과 Duxel.Sample도 내부 Window 없이 구성한다. AppShell도 기본 직접 배치이며 `UiAppShellOptions.UsePanel=true`로만 선택적인 Panel을 만든다. 벤치마크·레이어 실험·전용 검증 샘플은 부하를 보존하고 명시적인 Panel API를 사용한다.

창 확대 중 멈춤은 Windows 메시지 처리 안에서 GPU 표시를 기다리는 WM_SIZING 경로로 재현했다. 이 동기 대기를 제거하고 WM_SIZE가 확정한 클라이언트 영역을 레이아웃·Vulkan에 전달한다. 예상 외곽 사각형을 실제 프레임버퍼로 먼저 게시하지 않는다. 공식 계약: [WM_SIZING](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-sizing), [WM_SIZE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-size).

검증 근거는 `artifacts/panel-root-2026-11-05`다. .NET10 전체1014개·MSAA4 GPU159개, 고정 RC3 NativeAOT의 core/EXT 표시 수명 × 프레임 자원1/2/3개에서 각159개가 통과했다. 관리 이미지157개와 Native6개 구성의942개 비교가 바이트 단위로 같았다. 전체 기능37개 직접 페이지와 생성기66개는 앞선 근거를 재사용한다. 이는 전체 Fluent·Private·FPS·Pi 목표의 완료를 뜻하지 않는다.

실행 방법은 [갤러리 실행](widget-gallery-run.ko.md)을 참고한다. 관리 실행의 `-NoLaunch`는 이제 실제로 빌드만 수행한다.

반복 확대 후 검정 영역·정지가 기존 B078 실행 파일에서 사용자에게 재현되었다. WM_SIZING 대기 제거만으로 완료하지 않는다. 이후 실제 결함으로 **실패한 비차단 이미지 획득에서도 세마포어 순번이 증가하던 경로**를 확인했다. 순번은 획득 성공 때만 증가한다. 집중 회귀에서 이전 동작이 실패하고 수정 동작이 통과했다. swapchain 변경 후에는 새 동기화 자원으로 다시 획득하고, 오래된 크기의 대기 프레임을 버리며, 표시 실패나 회수 대기 때는 앱이 재시도한다.

표시 자원은 그리기 완료와 구분해 회수한다. 이전 swapchain과 세마포어를 보관한 뒤 새 swapchain의 첫 표시 이미지를 다시 획득하고, 그 획득 대기를 포함한 제출의 완료를 확인하여 회수한다. 유휴 스킵도 필요한 회수 프레임을 끝낸다. 공식 근거는 [Khronos swapchain 재생성](https://docs.vulkan.org/samples/latest/samples/api/swapchain_recreation/README.html)과 [세마포어 재사용](https://docs.vulkan.org/guide/latest/swapchain_semaphore_reuse.html)이다. 확장 없는 최종 종료의 WaitIdle 한계는 남아 있으며 이를 완전한 표시 완료 증명으로 주장하지 않는다.

GPU146개 및 고정 RC3 자원1/2/3개에서3회×48개의 연속 크기 변경·표시 지속·후속 클릭을 확인했다.16번 연속 NotReady 회귀와 이전 표시 자원 최종 회수도 검사한다. GPU 검사에서 QueuePresent에 도달하기 전 취소하는 인위적 결함 주입을 사용한다. 이는 실제 vkQueuePresentKHR의 OutOfDate 반환 계약과 다르다. 실제 OutOfDate도 wait 연산을 큐에 넣으므로 "실제 표시 오류가 wait를 소비하지 않는다"는 이전 추정은 철회한다. 새7E1B 실행 파일에 대한 사용자 확인은 별도로 기록한다. 표시 카운터·짧은 Private 표본을 실제 부드러움·input-to-photon·메모리 개선으로 확대하지 않는다.


## 연속 크기 변경의 GPU·표시 자원 수명

사용자는849A·7E1B에서 멈춤 해소를 확인했지만 WinUI보다 반응이 느리다고 답했다. 새 경로는 GPU 전체 대기를 제거하고, 이전 타깃은 마지막 그래픽 제출의 완료 뒤 회수한다. 지원 장치에서는 swapchain maintenance의 표시 fence를 사용하고, 미지원 장치에서는 기존 새 이미지 재획득 증명을 유지한다. 표시 fence는 실제 화면 표시 완료 시각을 측정하는 장치가 아니다. [공식 표시 fence 계약](https://docs.vulkan.org/refpages/latest/refpages/source/VkSwapchainPresentFenceInfoKHR.html)을 따른다.

MSAA4 타깃은 크기 변경 중 충분한 용량을 재사용한다. 새 용량은64픽셀 단위로 확보하고 물리 장치의 이미지 크기 한도를 지키며, 크기 변경 종료 후 실제 크기로 축소한다. 렌더 영역·픽셀 해상도·MSAA 샘플 수는 그대로다. 용량의 픽셀 동치와 종료 후 축소를 GPU로 검사했다.

추가 후보에서 드물게 vkCreateSwapchain 내부의 GPU 동기화 대기를 재현했다. 그래픽 제출 완료를0시간 제한으로 확인한 후에만 재생성에 들어가며, 준비되지 않은 경우 다음 최신 프레임으로 재시도한다. 대기 중인 재생성은 idle skip으로 사라지지 않는다. 테스트 전용 busy 주입은 실제 재생성에 진입하지 않고도 이 계약을 검사한다.

고정 RC3 NativeAOT, FIFO/VSync·MSAA4·프레임 자원2개·300ms 준비·3×48개 크기 변경을8ms 간격으로 주는 조건에서 독립3회씩 교대 비교했다. 완료된 재생성 단계 p95의 실행별 중앙값은19.9392→7.7539ms(61.11% 감소), 범위는14.1804–23.6590→7.4340–7.9389ms다. 이는 전체 앱 지연·실제 표시 FPS·WinUI 동등성의 증명이 아니다. 마지막 활성 표본 Private 중앙값은416,411,648→372,330,496B였으나 실행 범위가 겹쳐 메모리 개선으로 확정하지 않는다.

근거: `resize-ready-comparison.json`, `ready-pixel-comparison.json`, `gallery-ready-resize/result.json`, `verification.json`. 실제 갤러리288회 크기 변경은 exit0으로 종료했다. 앱 프레임 시도30,838건은 실제 표시 프레임 수가 아니다. 새 게시물 SHA52493AB5…의 사용자 체감 확인은 별도로 진행한다. deferred-memory-allocation 실험은 DeviceLost가 발생해 사용하지 않는다.


## 실제 크기 변경 표시 회귀 수정

이전 비차단 경로는371139A9에서도 검정 영역과 지연이 남았다. 확정 WM_SIZE와 새 프레임 제출을 연결하고 WSI 동기 메시지를 계속 처리하는 DCA37C18에서 사용자가 검정 영역 없이 빠르고 자연스럽다고 확인했다. 이전의 대기 제거 설명은 WM_SIZING의 미래 치수/단순 대기를 제거한 이력이며, 현재 WM_SIZE 제출 동기화와 구분한다. [현재 계약과 검증 한계](windows-resize-coherence.ko.md)를 따른다.
