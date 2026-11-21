# 종합 기능 샘플 검증 (2026-11-04)

대상은 `samples/fba/all_features.cs`다. Component Gallery와 Applied Samples에서 37개 기능 창을 연다. 최신 모든 API의 완전한 목록을 뜻하지는 않는다.

## 실행

```powershell
./run-fba.ps1 samples/fba/all_features.cs -NoCache
```

기존 출력이 보존 정책에 의해 보호되는 경우, 소유권 검사와 보관 한도를 그대로 적용하는 전용 출력을 명시할 수 있다.

```powershell
./run-fba.ps1 samples/fba/all_features.cs -NoCache -ArtifactRootDirectory 'W:/MyWorks/Duxel/artifacts/all-features-2026-11-04/native'
```

출력은 저장소 `artifacts` 아래의 전용 하위 폴더여야 한다. 루트 전체·외부 경로·Managed 모드 지정은 실행 전에 거부된다. 기존 보호 출력은 삭제하지 않았다.

## 확인 범위

- .NET SDK 10.0.301, .NET10 NativeAOT 게시 및 실제 Windows 창 확인.
- 37개 창의 실제 소스·Vulkan 렌더링 확인. `views-completed/views.json`과 PPM에 기록.
- 버튼, 체크박스, 슬라이더, 입력, 콤보, 팝업의 상태 전이 6개 확인. 입력 검사는 UiInputState와 Windows IME의 커밋 경계를 사용하며 물리 키보드 IME 검증은 아니다.
- 최종 .NET10 전체 회귀 검사 715개 통과. 솔루션 Release 빌드 경고 0개·오류 0개. 별도 NativeAOT 게시에는 ILCompiler NU1903 경고가 남았다.
- 진행 막대 레이블 잘림과 열 간 텍스트 겹침 수정. `TextWrapped`는 공통 엔진에서 현재 열의 가용 폭을 사용한다.

근거는 `artifacts/all-features-2026-11-04/`에 보존한다. 이전 미커밋 구현은 유지한다.

## FPS와 깜빡임 조사

밀리초 단위 FPS 계측이 같은 밀리초 내 프레임을 누락하던 문제를 고해상도 Stopwatch 시간으로 수정했다. 이는 측정 정확도 수정이며 렌더링 속도 개선율을 뜻하지 않는다. FPS는 화면 Render 호출 주기이고 유휴 프레임 생략과 입력 대기 시간을 포함한다. 기본 MSAA4·직렬 렌더링 설정은 기존 RC3 속도 측정 조건과 다르다. .NET11 RC3 성능 측정을 이번 .NET10 기능 검증으로 대체하지 않는다.

부분 갱신 모드는 재획득한 swapchain 이미지를 읽으므로 `clipped=false`로 생성한다. Vulkan은 clipped=true인 경우 가려진 픽셀 내용 보존을 보장하지 않는다. 이것만으로 사용자 관측 깜빡임이 해결됐다고 판정하지 않는다.

사용자 관측은 **왼쪽 버튼 유지·3000 FPS 이상에서 FPS 줄만 소실되고 NF 줄은 유지되는 현상**이었다. 전체/부분 갱신 및 NativeAOT/관리 실행 모두에서 재현됐다. 두 줄을 매 프레임 바꾸는 1024개 동기·1024개 in-flight 검사(3074개 검사)는 통과했으므로, 단순 반복 횟수 증가만으로 원인을 찾을 수 없었다.

FPS 내용은 16프레임 유지하고 NF 내용은 3프레임마다 바꾸는 128프레임 검사로 캐시 재사용·승격 전환을 포함하자 비동기 프레임 1에서 이전 내용이 표시되는 픽셀 불일치를 재현했다. 공통 `UiRetainedLayerBatcher`의 캐시 키가 기하 지문만 비교하고 그리기 명령 상태를 누락한 것이 원인이었다. 텍스처·클립·불투명도·오프셋 등을 포함한 명령 상태 지문을 비교하도록 수정했다. 캐시를 끄거나 샘플만 우회하지 않았다.

수정 후 같은 128개 동기·128개 in-flight 프레임 검사 386개가 통과했고, 기하가 같은 명령 변경의 배치 재생성·명령 보존 검사도 추가했다. 사용자가 최종 NativeAOT 창에서 버튼을 유지한 상태의 **깜빡임 소멸을 확인했다**. 근거: `overlay-promotions`(수정 전 실패), `overlay-key-final`(수정 후 통과), `regression-complete-fixed`(715개), `completion-audit.json`.

표시 완료 세마포어도 프레임 순서 대신 swapchain 이미지별로 재사용하도록 수정했다. 이 동기화 수정만으로는 사용자 증상이 해결되지 않았으며, 위 캐시 수정과 효과를 구분한다. [Vulkan 공식 지침](https://docs.vulkan.org/guide/latest/swapchain_semaphore_reuse.html)을 따른다.

최종 실행 파일은 `artifacts/all-features-2026-11-04/native/all_features.exe`, SHA256은 `37FEB8C1D4BE032F7F5D1EDD5C14420ABA6A4147C859E218F5B688DDC0E8A3E2`다. 유휴 대기 상태에서는 FPS가 정지하거나 낮게 보일 수 있고, 왼쪽 버튼 유지 시 입력 처리에 의해 연속 렌더링한다. 고속 프레임 계측은 수정했지만 이것을 렌더링 속도 향상으로 계산하지 않는다.

정량 성능 개선율·Private 메모리 개선율은 이번 조사에서 주장하지 않는다. 계측 로그는 원인 구분용 단일 실행이며 워밍업·3회 중앙값 성능 비교가 아니다.

기존 삭제 거부 대상 22개와 직접 삭제 절차는 정리 안내에 있다. 이번 검증은 해당 삭제 없이 진행했다.
