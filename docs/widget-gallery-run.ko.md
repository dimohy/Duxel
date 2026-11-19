# 작업 중인 위젯 화면 실행

2026-11-10 [키 가상 목록](virtual-list.ko.md): 세로 고정 높이 행을 double 오프셋·뷰포트 근처 좌표로 배치해 10억 항목을 처리합니다. CPU43·확대 좌표2317검사 통과, 범위 밖 행은 기존 소유권으로 해제합니다. 가변 높이/다른 축·교차 키 재활용 정책·외부 UIA·현재 Native는 미완료이며 컨테이너3/6 유지. 샘플: `layout_virtual_list_fba.cs`.

2026-11-10 [소유 ScrollView](scroll-view.ko.md): Dux.ScrollView가 유한 뷰포트 측정·실제 자식 배치·오프셋·스크롤바 수명과 무효화를 소유합니다. CPU47·별도 GPU/좌표1933검사 통과. 월드 좌표 정밀도 부족은 명시적으로 거부하며 큰 콘텐츠 재배치/가상화·현재 Native는 미완료입니다. 컨테이너3/6 유지. 샘플: `layout_scroll_view_fba.cs`.

2026-11-10 [중첩 스크롤 전달](scroll-routing.ko.md): 독립 휠 축의 경계 전달, 호출자 클립·팝업·창 순서 히트 검사, 공통 스크롤바 캡처/해제와 DirectText 실제 줄 높이를 반영합니다. 확대 관리 회귀 1,645검사·readback 1,025회 통과, 컨테이너 3/6 유지. 소유 스크롤 위젯·가상화·현재 Native는 미완료입니다. 샘플: `layout_scroll_routing_fba.cs`.

## 부모 할당 Field 샘플 — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/field-allocation-2026-11-10/sample-build/bin/layout_field_allocation_fba.cs/release/layout_field_allocation_fba.exe
```

Width : 180 / Height : 180, Validation : On을 바꾸며 두 실제 편집기를 확인합니다. 오른쪽 가로 Field는 UiWidgetStyle 생성 경로를 사용합니다. 로컬 미게시 API/샘플이며 실행기는 PowerShell7을 요구합니다. 현재 PS5에서는 동일본문 로컬 참조와 generator Analyzer 메타데이터를 명시한 sample-validation.targets로 검증했습니다. 첫 표시·15초 exit0은 수동 입력/공식 외형/실제 Windows UIA/현재 Native 승인과 구분합니다. [계약과 근거](fluent2-field.ko.md#부모-할당--2026-11-10).

## 공통 자식 좌표 수정 후 관리 샘플 — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/allocation-routing-2026-11-10/sample-build/bin/layout_badge_allocation_fba.cs/release/layout_badge_allocation_fba.exe
```

기존 Badge 샘플을 현재 공통 배치 소스로 다시 빌드했습니다. 첫 표시 542.23ms·15초 exit0 확인. 수동 입력·공식 외형·현재 Native는 미검증이며 Field 자체 할당 미완료를 별도 기록했습니다. [범위와 근거](widget-containers.ko.md#할당-자식-좌표--2026-11-10).

## 부모 할당 Badge 샘플 — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/badge-allocation-2026-11-10/sample-build/bin/layout_badge_allocation_fba.cs/release/layout_badge_allocation_fba.exe
```

Width : 100 / Height : 64, Dot, Increment/Zero/Overflow로 프레임·클립·숨은 카운터 예약을 확인합니다. 로컬 미게시 API/샘플입니다. 실행기는 PowerShell7을 요구하며 현재 PS5에서는 지시문 외 동일 본문의 로컬 프로젝트 FBA로 빌드했습니다. 첫 표시 549.78ms·15초 exit0 확인. 수동 입력·공식 외형·현재 Native 미검증. [계약과 근거](fluent2-badge.ko.md#측정과-부모-할당--2026-11-10).

## 부모 할당 Link 샘플 — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/link-allocation-2026-11-10/sample-build/bin/layout_link_allocation_fba.cs/release/layout_link_allocation_fba.exe
```

Width : 100 / Height : 120과 Short/Long으로 실제 줄바꿈·클립을 바꿉니다. 링크 클릭/Enter는 앱의 상태 문구를 갱신합니다. 로컬 미게시 API/샘플이며 실행기는 PowerShell7을 요구합니다. 현재 PS5에서는 지시문 외 동일 본문의 로컬 프로젝트 FBA 빌드·첫 표시 560.27ms·15초 exit0 확인. 수동 입력·공식 외형·현재 Native는 미검증입니다. [계약과 근거](fluent2-link.ko.md#측정과-부모-할당--2026-11-10).

## 부모 할당 Divider 샘플 — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/divider-allocation-2026-11-10/sample-build/bin/layout_divider_allocation_fba.cs/release/layout_divider_allocation_fba.exe
```

Width : 100 / Height : 120, Orientation, Short/Empty/Reset으로 실제 선·캡션·클립을 확인합니다. 새 API/샘플은 로컬 미게시 상태입니다. 실행기는 PowerShell7을 요구하며 현재 PowerShell5에서는 동일 본문의 로컬 프로젝트 지시문으로 빌드했습니다. 관리 첫 프레임·15초 exit0 확인; 수동 조작·공식 외형·Native 미검증. [계약과 근거](fluent2-divider.ko.md#측정과-부모-할당--2026-11-10).

## 부모 할당 텍스트 샘플 — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/text-allocation-2026-11-10/sample-build/bin/layout_text_allocation_fba.cs/release/layout_text_allocation_fba.exe
```

Duxel / Allocated Text: Width : 100 / Height : 120으로 두 루트를 바꾸고 Short/Empty/Reset으로 getter 콘텐츠만 바꿉니다. 부모가 줄바꿈·클립을 결정하며 폰트·행 높이는 유지합니다. 로컬 참조 FBA 빌드와 첫 프레임·15초 exit0을 확인했습니다. 수동 입력·공식 외형과 Native는 미검증입니다. 기존 PowerShell5 실행기 비호환을 보존하고 명시적 로컬 프로젝트 지시문으로 빌드했습니다. 근거:artifacts/text-allocation-2026-11-10/verification.json. [텍스트 계약](fluent2-text.ko.md).

## 부모 할당 Progress 샘플 — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/progress-allocation-2026-11-10/sample-build/bin/layout_progress_allocation_fba.cs/release/layout_progress_allocation_fba.exe
```

Duxel / Allocated Progress: Slider로 라이브 값을 바꾸고 Width : 80 / Height : 64로 두 루트 크기를 바꿉니다. 사각 바는 50%를 유지합니다. 콜백은 렌더 전용이며 순수 측정은 강제 부모 크기를 사용합니다. 로컬 참조 FBA 빌드의 경고·오류 없음과 첫 프레임·15초 exit0을 확인했습니다. 수동 입력·공식 외형과 Native는 미검증입니다. 현재 PowerShell7이 없어 비호환이 확인된 실행기를 반복하지 않고 FBA 패키지 지시문을 명시적 로컬 프로젝트 참조로 바꿔 빌드했습니다. [계약과 근거](fluent2-progress.ko.md).

2026-11-10 공백 교정: C# 공백·줄바꿈과 Width : 160 / Height : 180 문구를 반영했습니다. 로컬 참조 FBA 빌드도 검증했으며 실행 파일은 artifacts/radio-resume-2026-11-10/sample-fba-build/bin/layout_radio_allocation_fba.cs/release/layout_radio_allocation_fba.exe입니다. 최신 교정 근거:artifacts/radio-resume-2026-11-10/formatting-correction.json.

## 관리 RadioGroup 부모 할당 — 2026-11-10

```powershell
& W:/MyWorks/Duxel/artifacts/radio-resume-2026-11-10/sample-build/bin/RadioAllocation.Sample/release/RadioAllocation.Sample.exe
```

Duxel / Allocated Radio Groups:세 배치가 선택을 공유합니다. Width/Height로 줄바꿈·클립을 바꾸고 Tab·방향키·Space로 선택합니다. 관리 첫 프레임 제출과15초 정상 종료를 확인했으며 수동 입력·외형과 Native는 미검증입니다. 현재 Windows PowerShell5에서 기존 FBA 실행기가 실패하여 동일 샘플 본문을 로컬 참조의 격리된 .NET11 프로젝트로 빌드했습니다. 근거:artifacts/radio-resume-2026-11-10/verification.json.

## 최신 편집 입력창 배치 샘플 — 2026-11-06

```powershell
& 'W:\MyWorks\Duxel\artifacts\input-allocation-2026-11-06\native-gallery\net11.0-windows7.0\layout_input_allocation_fba.exe'
```

제목은 **Duxel / Allocated Editable Input**이다. 입력창 아래쪽을 클릭해 입력한 뒤 **Width/Height**로 크기를 바꾸고 **Ctrl+Z**를 확인한다. 소유 자식 **?**·**Clear**는 고정24/48px 크기와 키보드 순서를 유지한다. SHA256 `76CDEC9AF6D3F3C2476B127C4917C33DCDDCF2047D2560BEC1F7791DFA1E61E3`. 첫 실제 표시·15초 정상 종료를 확인했으며 수동 입력·공식 외형·리사이즈 승인과 구분한다.

고정.NET11에서 로컬 소스 재실행:

```powershell
Set-Location -LiteralPath 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain'
& 'W:/MyWorks/Duxel/run-fba.ps1' 'W:/MyWorks/Duxel/samples/fba/layout_input_allocation_fba.cs' -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

[입력 계약과 검증](widget-containers.ko.md#편집-입력창의-부모-할당-반영--2026-11-06)을 참고한다. 나머지 말단·Fluent 전체 목표는 미완료이고 리사이즈는 별도 보류한다.

## 최신 부모 제약 컨트롤 샘플 — 2026-11-06

```powershell
& 'W:\MyWorks\Duxel\artifacts\leaf-constraints-2026-11-06\native-gallery\net11.0-windows7.0\layout_constrained_controls_fba.exe'
```

제목은 **Duxel / Constrained Fluent Controls**다.80x64 Slider의 아래쪽에서도 드래그하고,**Width:80**으로 너비를80/240으로 바꾼 뒤 **Reset**을 누른다. Tab·화살표·Home/End 동작을 유지하며,Switch/Checkbox도240x64 할당 영역 전체에서 입력을 받는다. SHA256 `29D16F03794CD2F448D68F69C350343BF069A46316F2026398CED0EC4D82C41D`. 첫 표시·15초 정상 종료를 확인했으며 수동 입력·공식 Fluent 외형·리사이즈 승인과 구분한다.

로컬 소스에서 실행:

```powershell
Set-Location -LiteralPath 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain'
& 'W:/MyWorks/Duxel/run-fba.ps1' 'W:/MyWorks/Duxel/samples/fba/layout_constrained_controls_fba.cs' -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

[계약과 검증](widget-containers.ko.md#slidertoggle의-부모-할당-반영--2026-11-06)을 따른다. 나머지 말단 계열·Fluent 전체 범위는 미완료이며 리사이즈는 별도 보류다.

## 최신 단일 자식 배치 샘플 — 2026-11-06

검증한 Native 실행 파일:

```powershell
& 'W:\MyWorks\Duxel\artifacts\single-child-2026-11-06\native-gallery\net11.0-windows7.0\layout_single_child_fba.exe'
```

제목은 **Duxel / Single Child Layouts**다. **Count**를 누른 뒤 **Inset**으로 같은 자식 트리의 여백을8/16으로 바꾸고 **Reset**으로 초기화한다. Tab/Space도 실제 Fluent 버튼 동작을 사용한다. 내부 Window 없이 SizedBox/Padding/Align과 ConstrainedBox/Center 구성을 확인한다. SHA256 `21FC77820D5A31AD93DE421399B08EBBF578ABF6A9C1E688FB3B9204B1CBE825`. 첫 실제 표시와15초 정상 종료를 검증했으며 수동 입력·공식 외형·리사이즈 승인 결과는 아니다.

고정.NET11로 로컬 소스에서 다시 실행하려면:

```powershell
Set-Location -LiteralPath 'W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain'
& 'W:/MyWorks/Duxel/run-fba.ps1' 'W:/MyWorks/Duxel/samples/fba/layout_single_child_fba.cs' -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

[계약과 저장 비용의 범위](widget-containers.ko.md#생성된-단일-자식-배치-위젯--2026-11-06)를 따른다. 전체 목표는 계속 진행 중이며 리사이즈는 별도 보류한다.

최신 **Stack and Flex** 검증본에 Row/Column·Container·Stack 유지 경로를 포함했습니다. 아래 소스 명령의 파일명을 `layout_containers_fba.cs`로 실행하거나 `artifacts/container-retention-2026-11-06/verification.json`의 정확한 Native 경로를 사용합니다. Count/Reset·Move overlay로 컴파일된 위젯 구성을 확인할 수 있습니다. 첫 표시/정상 종료는 수동 상호작용·리사이즈 검증과 구분하며 리사이즈는 보류합니다. 추가된 Container 생성 비용을 메모리 개선으로 표시하지 않습니다.

최신 배치 유지 빌드도 같은 **layout_flow_containers_fba.cs**를 사용합니다. Wrap/Grid는 참여한 측정을 유지하면서 버튼 그리기·입력을 계속 실행합니다. 새 Native 실행 파일/첫 표시 범위는 `artifacts/layout-retention-2026-11-06/verification.json`을 따르며 이전 flow 실행 파일은 과거 근거입니다. 순수 측정 비교를 이 샘플의 FPS·리사이즈 승인으로 해석하지 않습니다.

새 흐름 배치는 **layout_flow_containers_fba.cs**에서 확인합니다. **Columns**로2/3열을 바꿔도 여섯 셀의 카운터를 보존하며 조작 버튼들은 Wrap 줄로 배치됩니다. 제목은 **Duxel / Layout Containers / Wrap and Grid**입니다. 넘침/스크롤 통합 전까지 최소480x560을 사용하며 성능 개선 결과로 세지 않습니다. 소스/실행 파일 해시와 첫 표시 검증 범위:`artifacts/flow-containers-2026-11-06/verification.json`.

```powershell
Set-Location W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain
& W:/MyWorks/Duxel/run-fba.ps1 W:/MyWorks/Duxel/samples/fba/layout_flow_containers_fba.cs -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

같은 컨테이너 샘플에 **Move overlay** 버튼을 추가했습니다. 클릭하면 같은 버튼 인스턴스를 보존하며 Positioned 위치가 바뀝니다. Stack은 공통 제약 기반 배치를 사용하고 영역 밖을 자릅니다. Wrap/Grid는 아직 화면에 없습니다. 고정RC3 Native 실행 파일과 실제 첫 표시 검증 범위는 `artifacts/stack-containers-2026-11-06/verification.json`을 따릅니다. 리사이즈는 별도 보류합니다.

가중치 컨테이너는 `layout_containers_fba.cs`의 **Duxel / Layout Containers / Stack and Flex** 화면에서 확인합니다. Count 버튼·빈 공간·Reset 버튼이 남은 너비를1:1:2로 나누며 넓어진 버튼 전체에서 클릭됩니다. 먼저 `Set-Location W:/MyWorks/Duxel/artifacts/optimization-evaluation-2026-11-04/toolchain`으로 SDK11 RC3를 선택한 뒤 `& W:/MyWorks/Duxel/run-fba.ps1 W:/MyWorks/Duxel/samples/fba/layout_containers_fba.cs -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'`를 실행합니다. 이 샘플은 고정 가중치이며 동적 변경은 별도 검증에 포함합니다. 전체 Fluent·컨테이너 목표 완료나 리사이즈 해결을 뜻하지 않습니다.

Last synced: 2026-11-06

PowerShell에서 원하는 화면 하나를 실행한다. 창을 닫고 명령을 다시 실행하면 새 소스가 반영된다. 이 샘플은 전체 Fluent 컴포넌트 구현 완료를 뜻하지 않으며, 구현된 경로를 중간 확인하는 갤러리다.

RadioGroup 스냅샷은 아래와 같이 실행한다. 세 배치·그룹 키보드·설명을 제공하며 SHA256은 `200C1E191D31EA9F7F277C4B3E3F9385BBF9F130333BB09DD1DE1512958FF6D0`이다. 첫 제출/자동 종료 검사이며 수동 입력 승인과 구분한다.

```powershell
& 'W:\MyWorks\Duxel\artifacts\fluent-radio-2026-11-06\native-gallery\net11.0-windows7.0\widget_tree_theme_fba.exe' --radio
```

Checkbox의 검증된 Native 스냅샷도 실행할 수 있다. SHA256은 `17353B3F448A390DC9B064303BADE2199D0FD4C633CBF55888E0B76EA595E882`이며 첫 제출/자동 종료 검사까지 확인했다. 수동 입력 승인이나 전체 공식 룩앤필 완료를 뜻하지 않는다.

```powershell
& 'W:\MyWorks\Duxel\artifacts\fluent-checkbox-2026-11-06\native-gallery-qualified\net11.0-windows7.0\widget_tree_theme_fba.exe' --checkbox
```

## 현재 소스로 실행 (.NET10)

```powershell
Set-Location -LiteralPath 'W:\MyWorks\Duxel'
$run = @{
    Path = 'samples/fba/widget_tree_theme_fba.cs'
    Managed = $true
    NoCache = $true
    DotnetPath = 'C:\Users\dimohy\.cache\duxel-dotnet-10.0.301\dotnet.exe'
}

.\run-fba.ps1 @run                                  # 종합 화면
.\run-fba.ps1 @run -ExtraArgs @('--buttons')
.\run-fba.ps1 @run -ExtraArgs @('--checkbox')          # Checkbox8조합·선택/혼합
.\run-fba.ps1 @run -ExtraArgs @('--radio')             # 그룹 키보드·세 가지 배치
.\run-fba.ps1 @run -ExtraArgs @('--typography')        # 타이포그래피17종
.\run-fba.ps1 @run -ExtraArgs @('--progress')          # 진행 표시·모션
.\run-fba.ps1 @run -ExtraArgs @('--slider')            # Slider 입력·변형
.\run-fba.ps1 @run -ExtraArgs @('--divider')
.\run-fba.ps1 @run -ExtraArgs @('--badge')
.\run-fba.ps1 @run -ExtraArgs @('--panel')
```

원하는 명령만 실행한다. `ExtraArgs`에 별도 `--`를 넣지 않는다. 실행 스크립트가 앱 인수 구분자를 추가한다. `dotnet run samples/fba/...cs`로 직접 실행하면 NuGet 참조를 사용할 수 있으므로 로컬 변경 확인에는 위 스크립트를 사용한다. 소스 편집 중 컴파일이 실패하면 아래 검증된 게시물을 사용한다.

## 검증된 게시물로 바로 실행 (고정 RC3 NativeAOT)

```powershell
$demo = 'W:\MyWorks\Duxel\artifacts\fluent-icon-assets-2026-11-06\native-gallery-qualified\net11.0-windows7.0\widget_tree_theme_fba.exe'
& $demo
& $demo --buttons
& $demo --typography
& $demo --progress
& $demo --slider
& $demo --divider
& $demo --badge
& $demo --panel
```

이 실행 파일은 게시한 시점의 코드다. 이후 편집은 자동 반영되지 않는다. 런타임이 포함되어 있으므로 실행만 할 때 별도 SDK를 고를 필요가 없다. 경로·해시는 `artifacts/fluent-icon-assets-2026-11-06/gallery-smoke.json`에서 확인한다. .NET10 기능 확인과 RC3 NativeAOT 측정은 구분하며 갤러리 화면의 FPS만으로 성능 향상을 판정하지 않는다.

## 확인할 동작

- 종합: 버튼 클릭·토글·체크, 자식 제거/재생성, 동적 텍스트를 확인한다. 일부 기존 입력의 Fluent 전환은 진행 중이다.
- 타이포그래피: 크기·굵기·행높이17종과 텍스트 배치를 확인한다.
- Progress: 슬라이더로 값을 바꾸고 확정 진행·이동 그라데이션·모션 감소를 확인한다.
- Slider: 같은 값을 공유하는 medium/small·단계·반전·수직·비활성 표시를 확인한다. Tab/Shift+Tab으로 포커스하고 화살표·Home/End·PageUp/PageDown을 사용한다. 드래그는 경계 밖까지 이어진다.

이상 현상을 알려줄 때는 실행한 명령·화면 모드·입력 동작을 함께 적는다. 이미 떠 있는 ERD는 별도 창이므로 이 샘플을 확인하기 위해 종료할 필요가 없다.


기본 화면은 내부 창 없이 앱에 직접 표시한다. `--panel`은 접기·펼치기 전용 시연이며 전체 기능 샘플의 `--panels`는 명시적 Panel 배치 모드다. 자세한 API는 [Panel과 직접 구성](panels-root-content.ko.md)을 참조한다.


`--badge`는 색8종×표현4종,크기6종,앞/뒤/단독 사용자 벡터 아이콘과 동적 CounterBadge를 보여준다. Add one으로99 경계를 넘기고 Reset to zero로 숨김·0·dot 차이를 확인한다. 전체 UIA·공식 화면 동등성 완료를 뜻하지 않는다.

`--divider`는 가로/세로·정렬3종·표현4종·inset과 유지된 내용 갱신을 보여준다. 최신 게시물의 시작·첫 프레임·자동 종료는 검증했으나 사용자 육안/입력 확인을 완료로 기록하지 않는다. [구현과 한계](fluent2-divider.ko.md).

`--buttons`는 small/medium/large와 비활성 버튼을 보여준다. 클릭으로 카운터를 바꾸고 Tab/Space/Enter로 입력을 확인한다. 새 게시물의 시작·자동 종료만 자동 검증했으며 수동 입력 확인은 미검증이다. [구현과 한계](fluent2-button.ko.md).

현재 소스의 `--buttons`에는 primary·outline·subtle·transparent도 있다. `UiButtonStyle`과 크기별 `UiFluentButtonPresentation<TAppearance>` 계열로 공통 동작에 정적 연결한다. [정확한 API와 남은 범위](fluent2-button.ko.md).

`--buttons`의 square/circular3크기도 생성된 공통 위젯을 사용한다. 모양은 `UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>`처럼 표현 정책 안에 정적으로 조합한다. 사용자 프레임 알고리즘도 같은 확장 지점이며 [정확한 범위와 한계](fluent2-button.ko.md)를 참조한다.

아이콘 Button은 `UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>`을 생성 스타일에 연결한다. `--buttons`의 Add 아이콘은 고정 공식 폰트이며 사용자 특성의 생성 경로도 실제 소비한다. 아이콘 전용 UIA는 아직 미완료다. [구성·검증·한계](fluent2-button.ko.md).

공식 폰트와 `[UiFluentIcon]` 생성 경로는 [공식 아이콘](fluent2-icons.ko.md)을 참조한다. Add regular/filled/interactive를 제공하며 전체 심볼 API·공식 SVG/브라우저 일치·UIA 완료를 뜻하지 않는다.

Switch의 여섯 배치/크기 스냅샷: 시작·첫 제출·자동 종료만 검증했으며 수동 승인은 별도다.

```powershell
& 'W:\MyWorks\Duxel\artifacts\fluent-switch-2026-11-06\native-gallery\net11.0-windows7.0\widget_tree_theme_fba.exe' --switch
```

SHA256: `FA30899041EAD1D9E9DBC394EF01D7B88AB6EA71EA05BD8E6173F7350892A354`.

Link 여덟 표현과 내부 페이지 탐색/돌아오기 스냅샷이다. 시작·첫 제출·자동 종료0을 확인했으며 수동/전경 승인은 별도다.

```powershell
& 'W:\MyWorks\Duxel\artifacts\fluent-link-2026-11-06\native-gallery\net11.0-windows7.0\widget_tree_theme_fba.exe' --link
```

SHA256: `E3B0182BB9AD79B3E9DEDB89F5F6FC4BB4E790039D268BFDF745BD220F20D502`.

Input: 현재 소스로 `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -Managed -DotnetPath C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe -ExtraArgs @('--input')`를 실행한다. 12개 실제 입력과 readonly/disabled를 제공한다. 검증된 Native 스냅샷:

```powershell
& 'W:/MyWorks/Duxel/artifacts/fluent-input-2026-11-06/native-gallery/net11.0-windows7.0/widget_tree_theme_fba.exe' --input
```

SHA256 CD3044409186C985E333DC9351C71F1C4B4345D5C7C18001C6BECBEF2ABD8F3A. 첫 제출/3초 자동 종료0만 확인했다. 수동 입력·실제 보이는 창 승인은 아니다.

Input 앞/뒤 콘텐츠 후속 스냅샷은 아래 실행 파일이다. 텍스트/검색 아이콘과 접미사가 추가됐다. 첫 제출/자동 종료만 검증했으며 수동 승인은 아니다.

```powershell
& 'W:/MyWorks/Duxel/artifacts/fluent-input-slots-2026-11-06/native-gallery-final/net11.0-windows7.0/widget_tree_theme_fba.exe' --input
```

SHA256 898085D0451227CAADB5167540774875CD4EE34BD139E219D2B1C115D5728E55

실제 자식 Clear 버튼을 포함한 Input 스냅샷:

```powershell
& 'W:/MyWorks/Duxel/artifacts/fluent-input-children-2026-11-06/native-gallery-final/net11.0-windows7.0/widget_tree_theme_fba.exe' --input
```

SHA256 B66A4252597E21E0C4269077504B67BA01F0B83C7E7265AD46F2428242FF0451. 첫 제출/3초 자동 종료0만 검증했으며 수동·가시 창 승인은 아니다.
## Field 갤러리

```powershell
.\run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -Managed -DotnetPath 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' -ExtraArgs @('--field')
```

작은/중간/큰 라벨, 세로/가로 배치, 필수 표시·오류/성공·도움말·비활성을 확인한다. 라벨을 클릭하면 실제 입력으로 포커스가 이동하며 세 글자 이상 입력하면 성공 상태가 된다. 입력 높이는 자연 측정하며 마지막 Field는 창의 가용 너비를 사용한다. UIA·공식 전체 동치는 남아 있다. [Field 설명](fluent2-field.ko.md)과 [측정 계약](widget-measurement.ko.md)을 참고한다.

새 Field Native 스냅샷을 바로 실행할 수도 있다.

```powershell
& 'W:/MyWorks/Duxel/artifacts/fluent-field-2026-11-06/native-gallery/net11.0-windows7.0/widget_tree_theme_fba.exe' --field
```

SHA256 BB118CDCAADEE7C6144AE5A68B858565612E1C0DBC85845A5AFBC6F9DD4F90A1. 첫 제출·3초 종료0을 검사했으며 가시 창/수동 입력 승인은 아니다.

## 자연 측정을 적용한 최신 Field 스냅샷

```powershell
& 'W:/MyWorks/Duxel/artifacts/widget-measurement-2026-11-06/native-gallery-final/net11.0-windows7.0/widget_tree_theme_fba.exe' --field
```

SHA256 728DD82D3965374B144C30C369C31B7705B3D2F67DDDE8B71E3A633129E05CF2. 입력의24/32/40 높이를 직접 지정하지 않고 실제 위젯을 측정한다. 마지막 Field는 창의 가용 너비를 사용한다. 숨김 실행의 첫 제출/3초 종료0을 확인했으며 수동 화면·입력 승인은 아니다. [측정 계약](widget-measurement.ko.md).

# UIA 기반 체크포인트 (2026-11-06)

최신 검증 Input 갤러리는 `artifacts/uia-foundation-2026-11-06/native-gallery-names-final/net11.0-windows7.0/widget_tree_theme_fba.exe --input`이다(SHA256 `CC4DBDF979E1B72B94FE8F58CB8DD1107BA5E2F6D0E0DFC7CB15287CD7AFE6D4`). 외부 NativeAOT UIA 클라이언트의 상호작용4개를 통과하고 갤러리는15초 후 정상 종료했다. 수동 화면 승인은 아니다. [접근성 범위](widget-accessibility.ko.md)와 산출물 검증 기록을 참고한다. 기존 resize/FPS 사용자 승인 근거를 보존한다.

# UIA 수명 체크포인트 (2026-11-06)

최신 갤러리: `artifacts/uia-lifetime-2026-11-06/native-gallery/net11.0-windows7.0/widget_tree_theme_fba.exe --field`, SHA256 `032DF8EDB028D142630D45ACB434E773FA3D5F555BE239357A7BADA32AC97864`. 외부 NativeAOT Field14개를 통과했고 대상은12초 후 정상 종료했다. `--input`으로 입력/장식 갤러리를 열 수 있다. 로컬 소스 재빌드 명령은 `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -ExtraArgs @('--field')`다. UIA 검증은 수동 화면 승인이 아니다.

# Spinner 체크포인트 (2026-11-06)

최신 갤러리: `artifacts/fluent-spinner-2026-11-06/native-gallery-final/net11.0-windows7.0/widget_tree_theme_fba.exe --spinner`, SHA256 `8A7384E3CCFB0BBA4072277407956B271E55D70826A826342E01C30135FB3413`. 크기·라벨·모션·표현16개 예제를 제공한다. 외부 NativeAOT UIA33개를 통과하고 대상은15초 후 정상 종료했다. 재빌드 명령은 `./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -ExtraArgs @('--spinner')`다. 첫 제출/UIA 검사는 수동 화면 승인이나 공식 브라우저 동치가 아니다.


최신 Build 저장 비용 개선 샘플(2026-11-06): `& 'W:\MyWorks\Duxel\artifacts\build-store-2026-11-06\native-gallery\net11.0-windows7.0\layout_containers_fba.exe'`. SHA2566E8C8A939846673FE35670FE6BDFCCA0FAA2D8858252EF418E47AD31C0FB7311. 첫 실제 표시와15초 정상 종료를 확인했다. 수동 입력·리사이즈를 새로 승인한 결과는 아니다. Count/Reset과 Move overlay로 현재 공통 위젯 경로를 실행할 수 있다. [메모리 결과와 한계](widget-cpu-retention.ko.md#공통-위젯-build-저장-비용-개선--2026-11-06)를 참조한다.
