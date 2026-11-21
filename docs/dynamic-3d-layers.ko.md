# 동적 3D 레이어

## 최종 검증 결과 — 2026-11-03

현재 검증한 배포 이미지는 **DE068949E4A92689455341057405F6BFE0BEC38C9C89DC64D50A1D43A6D60715**다. 아래의 과거 단계별 수치는 당시 소스·이미지의 기록이며 현재 결과는 이 절과 [최신 FPS 기록](../artifacts/perf-2026-11-03/fps-current.json)을 기준으로 한다.

| ERD1200테이블·MSAA4 | 독립3회 평균의 중앙값 | 실행 범위 | 목표1000 |
| --- | ---: | ---: | --- |
| 카메라 이동 | 1093.47FPS | 1046.70–1180.66 | 3/3통과 |
| 카메라＋엔티티 이동 | 1044.80FPS | 1028.66–1171.38 | 3/3통과 |

단일 실행 평균 최고1180.66FPS다. .NET11 RC3 NativeAOT,`OptimizationPreference=Speed`,이식 가능한 기본 CPU명령어 집합,VSync off,최초 표시 후1초 준비와 실제18초 측정 조건이다. Vulkan 표시 요청 성공 간격을 사용하며 모든 프레임1ms나 모니터의 실제1000Hz 표시를 보장하지 않는다. 이전 단계와는 여러 변경이 포함되어 격리된 개선율을 계산하지 않는다.

레이어는 공통 Vulkan 경로의 실제3D 면·MVP·깊이를 사용한다. 별도 레이어 비트맵이나 합성 타깃이 없다. 카메라만 바뀌면 월드 지오메트리 생성0회이며 GPU상주 데이터를 유지한다. GPU가 최종 픽셀을 다시 래스터화하는 비용은 남는다. 미니맵과 다른 위젯은 공통 경로의 독립 viewport·camera·depth scope를 사용한다. 불투명 경계의 color/depth coverage를 MSAA1/4모두 일치시켰으며 부분 alpha 재질은 depth-read와 뒤→앞 혼합을 사용한다. 1x의 경계 정밀도는 한 표본이며 기본4x의 부드러운 품질은 유지한다.

GPU검사 NET10/11각438개,직접 제출414개,실제 동적3D 샘플·상태 패널 각9개,전체 Release경고0/오류0을 통과했다. 상태 패널 상단·하단·줄 간격은 현재 렌더러의 GPU 버퍼에서 확인했다. 세 프레임의 NET10/11픽셀은 동일하다. 데스크톱 캡처나 이 세 프레임의 FPS측정으로 확대하지 않는다.

| 모든 내용이 매 프레임 바뀌는3D 장면 | 1층×1000 | 8층×1000 | 8층×4000 |
| --- | ---: | ---: | ---: |
| .NET10 Managed | 8461.55 | 2453.86 | 719.93 |
| .NET11 Managed | 8044.89 | 2600.19 | 882.55 |
| .NET10 NativeAOT Speed | 7049.16 | 1815.60 | 581.45 |
| .NET11 NativeAOT Speed | 8124.30 | 2426.84 | 806.74 |

각 값은 독립3회 FPS중앙값이다. 전체12실행·72구간·원시 프레임72개·GPU계측72구간·표시 창12개 검증을 완료했다. .NET10 SDK10.0.403/runtime10.0.14와 .NET11 SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148을 사용했다. MSAA1,VSync off,정적 레이어 캐시 없음,직렬 UI프레임 시작 시계,구간별 준비0.25초＋측정1.5초다. 순서 반전과 동일한 표시 창 조건을 적용했다. 이 표의 수치는 ERD의 병렬 렌더링·MSAA4·표시 요청 시계와 직접 비교하지 않는다. 평균 외 median/p95/p99/1%low,할당,드로우,업로드,GPU시간과 실행 범위는 [런타임 결과](../artifacts/perf-2026-11-03/coverage-complete-runtime/summary.json)에 보존했다. .NET11이나 AOT가 모든 부하에서 가장 빠르다고 주장하지 않는다.

픽셀 소유권 재사용의 이전 변경 전/후 각3회180초 비교에서 Private Bytes 증가 중앙값은116.33MiB→0.375MiB(증가량99.68%감소)였다. 최종 DE068949이미지의 별도180초 확인은 증가0.383MiB,작업 집합 변화-0.297MiB,정상 종료였다. 유휴59.99초는 추가 로그 프레임0,CPU62.5ms,정상 종료였다. 강제 GC를 사용하지 않았다. 전체 메모리99.68%감소,무기한 누수 없음 또는 VRAM 측정으로 해석하지 않는다. [현재 자원 관측](../artifacts/perf-2026-11-03/coverage-complete-resources-audit.json).

현재 GPU 실측은 NVIDIA RTX5070Ti Laptop이다. 다른 GPU의 성능 보장은 하지 않는다. 글리프는 벡터 메시이고 기본 곡선 근사 오차0.001폰트 픽셀이므로 무한 배율의 해석적 글리프라고 주장하지 않는다. 비트맵 이미지의 확대 품질은 원본 해상도에 제한된다. 기존 SDK10.0.301기준과 공개 지원 TFM은 유지한다.

Vulkan은 각 목록의 명령 경계를 합쳐 상태 설정 전에 가시성을 판정한다. 완전히 화면 밖인 목록은 행렬·viewport·깊이 상태를 설정하지 않는다. 경계가 모두 확인된 목록이 화면 안쪽이면 개별 명령의 중복 검사를 생략하고, 경계에 걸치면 기존 명령별 검사를 유지한다. 콜백이나 미확인 경계는 보수적으로 유지한다. 원근 투영의 동차 좌표와 2픽셀 여유를 사용하며, 위젯 좌표 변환과 GPU 변환은 같은 계산을 공유한다. 지오메트리 준비와 유지 정책은 별개이므로 숨긴 목록을 다시 보여도 카메라 변경 때문에 재업로드하지 않는다. `UiRendererFrameMetrics.LayerTransformPushes`와 `CulledLayerLists`로 실제 상태 갱신과 목록 제외를 확인할 수 있다.

`UiContext.GetDrawData()`가 반환하는 프레임은 `using var frame = ...` 또는 `ReleasePooled()`로 반환한다. 카메라 상태를 담은 프레임별 목록 객체는 프레임 반환 후 재사용할 수 있으므로, 반환한 목록을 계속 참조하지 않는다. 장기 보관할 카메라 상태는 `UiLayer3D` 값을 복사한다. 지오메트리의 유지와 프레임 상태 객체의 수명은 별개이며, 아직 반환하지 않은 프레임의 행렬은 변경하지 않는다. `UiRetainedLayerBatcher.Frame.DrawData`는 빌린 뷰이므로 직접 해제하지 않고 `Frame.Dispose()`로 뷰를 반환한 뒤 원본 프레임을 해제한다.

Vulkan 백엔드는 `UiRetainedLayerBatcher`를 자동 적용한다. 3D 행렬·viewport·깊이 상태가 동일한 인접 resident 목록을 벡터 버퍼와 호환 명령으로 묶어 유지한다. 공통 카메라 변경에는 행렬만 갱신하며 지오메트리를 다시 묶거나 업로드하지 않는다. 콜백이나 자주 바뀌는 콘텐츠는 분리한다. 개별 모델 행렬 변경으로 이 CPU 묶음이 나뉘면 최초 재배치 비용이 발생할 수 있다. 이후 Vulkan 컴파일러는 서로 다른 행렬의 목록도 GPU 명령 데이터로 결합한다. 제출 순서·로컬 clip·불투명도·텍스처·해석적 곡선 품질은 유지한다. 묶음 크기와 캐시 개수를 제한하고 미사용 데이터와 종료 시 CPU 버퍼를 반환한다. `UiRendererFrameMetrics.BatchedSourceLists`와 `RebuiltBatchGroups`로 CPU 묶음과 재생성 횟수를 구분한다.

`BeginLayer`의 `UiLayerOptions.Scene3D`는 레이어의 도형을 실제 3D 면으로 투영한다. 내용은 매 프레임 다시 생성할 수 있다. 레이어별 이미지나 합성 패스는 만들지 않는다. 정점 셰이더가 변환·원근 투영을 수행하고, Vulkan D32 깊이 버퍼가 도형의 앞뒤와 면의 교차를 판정한다.

## 좌표와 사용법

기존 픽셀 좌표는 먼저 전체 display 기준 `[-1, 1]` 평면으로 정규화된다. `Transform`은 이 평면에서 clip space까지의 `model * view * projection` 행렬이다. `System.Numerics`의 행 벡터 행렬을 그대로 전달한다. GLSL의 column-major 해석과 `matrix * vector` 연산이 대응하므로 호출자가 다시 전치하지 않는다. Vulkan의 깊이 범위는 `0..1`이다.

```csharp
var camera = Matrix4x4.CreateLookAt(new(0, 0, 3), Vector3.Zero, Vector3.UnitY)
    * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, width / height, .1f, 20);
var model = Matrix4x4.CreateRotationY(angle) * Matrix4x4.CreateTranslation(x, y, z);
var options = new UiLayerOptions(false, 1f, default)
{
    Scene3D = new UiLayer3D(model * camera)
};
if (ui.BeginLayer("moving-plane", options))
{
    var draw = ui.GetWindowDrawList();
    draw.AddRectFilled(rect, changingColor, ui.WhiteTextureId);
}
ui.EndLayer();
```

매 프레임 위치·색·도형 수·행렬을 변경할 수 있다. 불투명 도형은 제출 순서와 관계없이 가까운 깊이가 보인다. 빈 공간에는 애초에 도형이 없으므로 뒤쪽 도형이 그대로 보인다. 깊이가 같은 도형은 `LessOrEqual` 비교에 따라 나중 제출한 도형이 보인다. 가려진 픽셀도 색상 출력만 차단될 뿐, CPU의 도형 생성과 업로드 비용이 자동으로 없어지는 것은 아니다.

텍스처 알파가 0인 fragment는 깊이도 쓰지 않는다. 부분 알파 혼합은 별도의 재질 조건이다. 불투명 도형을 먼저 그리고, 반투명 도형은 `new UiLayer3D(transform, WriteDepth: false)`로 깊이를 읽기만 하면서 뒤에서 앞으로 제출한다. 교차하는 반투명 면의 순서 독립 혼합은 제공하지 않는다. 도형이 없는 공간의 가시성과 부분 알파 색상 혼합을 혼동하지 않는다.

로컬 clip은 면과 함께 투영되며 fragment에서 검사한다. CPU의 기존 screen scissor로 회전한 도형을 잘라내지 않는다. 3D 레이어의 정적 캐시를 선택하면 지오메트리만 재사용하고 카메라는 매 프레임 바꿀 수 있다. 중첩 레이어의 명시적 `Scene3D`는 완성된 MVP이므로 상위 MVP를 다시 곱하지 않는다. 명시적 MVP가 없는 자식은 부모의 MVP를 상속한다. 중첩 레이어 id는 서로 달라야 한다.

`Scene3D`가 없는 일반 2D draw list는 깊이 검사 없이 기존 순서대로 그려진다. 장면 뒤의 UI는 장면 전에, HUD·편집 도구 등 앞에 놓을 UI는 장면 뒤에 제출한다. 이 API는 렌더링 변환이며 3D 면의 마우스 picking을 자동으로 제공하지 않는다.

## 실행 경로와 자원

- 동적 `EndLayer`는 프레임 소유 지오메트리를 부모로 이전한다. 정점·인덱스·primitive의 복사와 인덱스 재배치를 수행하지 않는다.
- 레이어 builder는 id별로 재사용한다. GPU가 사용하는 프레임 버퍼의 수명은 기존 fence와 프레임 슬롯 계약을 따른다.
- 순수 2D, 3D depth read, 3D depth read/write 파이프라인을 구분한다. specialization constant로 순수 2D 셰이더에서 3D 행렬·로컬 clip 분기를 제거할 수 있도록 한다.
- 프레임당 dynamic rendering 한 번, submit/present 경로는 기존 경로를 사용한다. 레이어마다 offscreen color image를 만들지 않는다.
- 깊이 attachment와 3D 파이프라인은 처음 사용할 때 생성한다. 깊이 이미지는 swapchain 이미지별로 하나이며 여러 레이어가 공유한다. MSAA sample 수는 color attachment와 일치해야 한다.
- resize와 MSAA 변경 시 깊이 자원을 함께 재생성하며 종료 시 해제한다. 깊이 이미지는 첫 사용 뒤 재사용을 위해 보유한다. 일반 2D만 사용하는 실행에는 깊이 이미지를 할당하지 않는다.
- `DUXEL_VK_PROFILE=1`, `DUXEL_VK_GPU_PROFILE=1`과 `DUXEL_VK_PROFILE_OUT`으로 CPU·GPU 시간, draw 수, 실제 동적 업로드 바이트, 깊이 할당 바이트를 관측할 수 있다. 프로파일링 자체의 비용은 별도로 고려한다.

## 검증과 측정

```powershell
dotnet build Duxel.slnx -c Release
dotnet run --project tests/Duxel.Rendering.Tests -c Release -p:DuxelValidationHooks=true -- artifacts/rendering-validation
dotnet run --project tests/Duxel.Performance.Bench -c Release -- artifacts/layer-cpu.json
./run-fba.ps1 samples/fba/dynamic_3d_layers_bench.cs -Managed -NoCache
```

GPU 검사는 실제 swapchain에 그린 결과를 present 전에 읽어 픽셀을 비교한다. 시험용 readback은 `DuxelValidationHooks=true` 빌드에만 포함된다. 일반 빌드에는 readback 호출·할당·GPU 대기가 없다. 테스트 로그의 Vulkan validation 오류도 별도로 확인해야 한다.

동적 샘플은 기본으로 화면을 계속 표시한다. 자동 측정은 `DUXEL_3D_BENCH_OUT`을 지정한다. `DUXEL_3D_BENCH_REVERSE=1`은 실제 phase 순서를 뒤집는다. 각 phase는 0.25초 워밍업 뒤 기본 1.5초 측정한다. 모든 레이어의 위치·색과 행렬을 매 프레임 변경하고, 레이어 수 1/8, 도형 수 1,000/4,000, 겹침 계수 0.2/0.8을 사용한다. JSON의 실제 phase 순서와 phase별 raw frame CSV를 함께 보존한다.

2D/3D phase는 투영 면적과 가시성이 다르므로 같은 화면의 최적화 전후 비교가 아니다. 기능 비용과 확장성을 보는 지표다. 기존 소스와 동일한 CPU fixture를 비교하는 별도 측정 및 layer/widget 런타임 비교와 구분한다.

## 성능 판단 기준

목표는 원하는 3D 구조를 유지하면서 하드웨어 처리량·지연 시간·메모리 효율을 개선하는 것이다. 초기 성능 하락만으로 구조를 폐기하거나 정적 캐시·별도 2D 합성으로 바꾸지 않는다. 병목을 측정하고 데이터 배치·전송·배칭·병렬성·동기화를 개선한다. GPU 사용률을 높인 것 자체를 최적화로 세지 않으며, 화면 정확성과 반복 측정 결과로 판단한다.

## 카메라, 지오메트리 유지와 위젯 뷰포트

`UiLayerOptions.ContentVersion`을 지정하면 카메라와 독립적으로 지오메트리를 유지한다. 버전이 같으면 `BeginLayer`는 false를 반환한다. `EndLayer`는 항상 호출하고 `Scene3D`는 매 프레임 갱신한다. 도형·로컬 배치·텍스트·재질 데이터가 바뀌면 버전을 올리거나 `MarkLayerDirty`를 호출한다. 완전히 동적인 콘텐츠는 버전을 null로 둔다. 두 방식은 레이어 이미지 없이 같은 렌더러를 사용한다. 유지된 지오메트리는 풀 배열을 소유하며, 참조한 네이티브 텍스트 자원을 교체·제거 시점까지 유지한다. 버전으로 생략하는 도형 생성 본문에 매 프레임 실행해야 하는 즉시 모드 위젯의 입력 처리를 넣지 않는다.

독립 위젯 카메라는 `UiLayer3D.Viewport`에 콘텐츠 영역을 지정한다. 원본 픽셀 좌표를 해당 영역 기준으로 정규화하고 GPU viewport와 screen clip으로 출력을 제한한다. `(DepthScopeId, Viewport)`가 깊이 영역을 식별하며 같은 영역의 draw list는 연속 제출해야 한다. 새로운 깊이 영역은 기존 rendering scope 안에서 해당 깊이 영역만 지우고 색상은 보존한다. 따라서 위젯 패널은 앞서 그린 전체 장면의 깊이에 가려지지 않는다. 같은 viewport에 독립 장면이 여럿이면 `DepthScopeId`를 구분한다. 위젯별 color target이나 추가 합성 패스는 없다.

Vulkan 장치가 `shaderClipDistance`를 지원하면 소수점 뷰포트에 정수 하드웨어 뷰포트를 설정하고 투영을 보정한다. 일반 도형과 컷아웃 도형에는 클립 평면 네 개를 적용해 원래 소수점 경계에서 그리기를 제한한다. 장치가 기능을 지원하지 않거나 정수 뷰포트가 장치 한도를 넘으면 기존 뷰포트 경로를 사용한다. 깊이 스코프는 요청한 뷰포트와 스코프 ID로 구분한다.

Vulkan 장치가 표준 샘플 위치를 지원하면, 4× MSAA로 깊이를 기록할 때 불투명하다고 확인된 둥근 채우기·그라데이션·이미지에 샘플 마스크를 적용한다. 프레임버퍼에서 뷰포트에 소수 좌표가 있으면 `shaderClipDistance`가 필요하다. 정수 뷰포트는 장치 한도 안에서 이 기능 없이도 샘플 마스크를 사용할 수 있다. 도형이 덮는 각 샘플에 색과 깊이를 함께 기록한다. 텍스처 일부를 갱신하면 불투명 판정이 무효화되며, 도형 경계와 별도로 UV 경계가 있는 이미지는 기존 방식으로 그린다.

`UiLayerOptions.Translation`으로 레이어를 옮겨도 도형의 불투명 판정은 유지된다. 레이어의 불투명도를 낮추면 기존 방식으로 경계를 처리하고 합성한다.

`scene.TryMapScreenPoint(screenPoint, displayBounds, out localPoint)`는 같은 MVP의 역변환으로 포인터 광선과 로컬 평면의 교점을 구한다. viewport 밖의 점과 역변환 불가능한 행렬은 거부한다. 반환한 원본 픽셀 좌표로 콘텐츠를 판정할 수 있다. 기존 즉시 모드 위젯 전체의 입력 처리가 자동으로 변환되는 것은 아니다.

`./run-fba.ps1 samples/fba/dynamic_layer_camera_fba.cs -Managed -NoCache`는 전체 카메라와 독립 위젯 카메라 둘을 보여 준다. 휠은 확대·축소, 오른쪽 드래그는 이동이다. resident 모드는 레이어 본문 다섯 개를 한 번씩 생성하고, 버튼으로 매 프레임 도형을 바꾸는 모드로 전환한다. resize는 콘텐츠 버전을 갱신한다. `DUXEL_CAMERA_DEMO_SECONDS`를 지정하면 자동 종료한다.

해석적 원·둥근 사각형은 GPU에 수학적 경계를 유지한다. `fwidth`로 원근 확대·축소 이후의 화면 픽셀에 맞춰 경계 안티앨리어싱 폭을 계산하며, 레이어의 픽셀 이미지를 늘리지 않는다. GPU 검사는 8배 카메라 확대와 직접 8배 크기로 생성한 벡터를 비교하고 지오메트리 재생성·업로드가 없는지도 확인한다. 명시적 폴리곤 메시의 분할 수는 원본을 유지한다. 3D 레이어의 네이티브 텍스트는 이제 `IPlatformVectorTextBackend`가 필요하다. Windows에서는 DirectWrite 윤곽을 Direct2D로 삼각형 분할하여 벡터 지오메트리로 유지한다. 기본 근사 오차는 원본 폰트 픽셀 0.001이며 데모는 MSAA 4를 사용한다. 오차는 배율만큼 확대된다. 일반 2D 네이티브 텍스트의 래스터 경로는 유지한다. 래스터 이미지의 세부 표현은 원본 해상도에 제한된다. 편집 동작과 갱신 모드 비교는 [ERD 레이어 실험실](erd-layer-lab.ko.md)을 참고한다.

분리된 .NET 11 비교 명령은 `tests/runtime-net11/README.md`를 참고한다. 저장소 SDK는 .NET 10으로 유지한다. 상세 측정 결과와 하드웨어·남은 한계는 [성능 개선 기록](archive/performance/optimization-session-2026-11-03.md)에 기록한다.

## 서로 다른 행렬의 GPU 드로우 결합

정적 레이어의 텍스처만 바뀌어 명령 배열의 크기와 지오메트리가 같더라도 명령 위상 서명이 바뀌면 새 명령 래퍼를 발행한다. GPU 결합 계획은 새 재질 경계를 반영하며 지오메트리 재업로드는 하지 않는다. 가시성·클립·불투명도·카메라는 이 명령 위상과 별개의 프레임 상태다.

공통 CPU 배처는 인접 목록의 3D 상태가 다르면 명령 검사와 콘텐츠 이력 조회를 생략한다. 묶인 목록의 카메라 래퍼는 단독 프레임 대여 범위에서 재사용하고 원본 장면 스냅샷은 바꾸지 않는다. `Frame.DrawData`는 빌린 뷰이며 `Frame.Dispose()` 이후 사용하지 않는다. 이 최적화는 GPU 명령 결합과 독립적으로 적용된다.

같은 행렬 기저를 사용하는 레이어는 GPU에 공통 기저를 push constant로 전달하고 레이어별 마지막 행만 읽는다. `DUXEL_VK_SHARED_LAYER_BASIS=0`으로 이 최적화를 끄고 비교할 수 있으며 `SharedBasisLayerDraws`로 사용 횟수를 확인한다. CPU 가시성 판정용 개별 행렬과 실제 GPU에 전달한 행렬은 독립적으로 추적하므로 인접 묶음은 같은 기저를 다시 전달하지 않는다. 서로 다른 회전·원근 행렬은 전체 행렬을 사용하는 기존 계약을 유지한다.

카메라 이동은 유지된 도형의 생성·삼각형 분할·업로드를 다시 요구하지 않는다. 하지만 화면의 픽셀 위치가 달라지므로 보이는 벡터의 GPU 투영·래스터화는 다시 수행한다. 도형 재생성이 없는 것과 화면을 그리는 비용이 없는 것은 별개다.

평면의 이동과 깊이를 별도로 유지하려면 `UiLayer3D.PlaneModel`에 `UiLayerPlaneModel`을 지정한다. 이때 `Transform`은 공통 카메라 행렬이다. `Translation`은 투영 전 원본 픽셀 단위 이동이며 `DepthOffset`은 투영 후 정규화 깊이 오프셋(`clip.z += offset * clip.w`)이다. 물리적 월드 Z 이동이나 개별 회전은 기존 완성된 MVP 경로로 표현한다. 두 계약은 같은 깊이 attachment와 공통 렌더러를 사용한다.

```csharp
var scene = new UiLayer3D(camera)
{
    Viewport = contentBounds,
    PlaneModel = new UiLayerPlaneModel(new UiVector2(32, 16), -.02f),
};
```

Vulkan은 같은 카메라를 가진 인접 평면의 카메라 행렬을 공유하고 기존 64B 호환 슬롯 안에 모델 payload 16B를 유지한다. 카메라만 바뀌면 슬롯 워밍업 후 레이어별 버퍼 쓰기는 0B이며, 평면 하나의 위치만 바뀌면 해당 슬롯에서 모델 16B만 쓴다. 공통 카메라 push constant 전송은 이 버퍼 계측과 별개다. viewport 정규화도 연속된 같은 카메라에서 재사용한다. 서로 다른 카메라나 일반 3D 행렬이 섞이면 기존 전체 행렬 계약으로 처리하며 모드 전환 시 슬롯별 저장 표현을 갱신한다. `SharedCameraLayerDraws`와 `DUXEL_VK_SHARED_LAYER_CAMERA=0`으로 비교한다. `GetEffectiveTransform(displayBounds)`와 `TryMapScreenPoint`는 같은 모델을 반영한다. 레이어 제출·가시성 검사를 위한 CPU 순회는 여전히 남아 있다.

Vulkan의 resident 레이어 컴파일러는 서로 다른 모델·카메라 행렬, 로컬 클립, 이동, 불투명도와 analytic primitive 종류를 드로우별 GPU 데이터로 전달한다. 원래 명령과 삼각형 순서를 유지한 인덱스 스트림에서 상위 8비트는 명령 데이터, 하위 16비트는 원본 정점을 지정한다. 같은 viewport·depth scope·depth 쓰기 모드·텍스처·폰트 출력 모드의 인접 목록을 실제 하나의 indexed draw로 그린다. 여러 드로우를 한 API 호출로 제출하고 개수를 낮춰 표시하는 방식이 아니다.

묶음은 최대 명령 256개와 인덱스 1,048,576개다. 긴 목록은 내부 명령 범위로 나눠 결합하며 큰 관계선 목록도 제외하지 않는다. 인덱스 캐시는 최대 256개·512MiB이며 기존 직접 제출 경로와 작업 범위를 나눈다. 여러 텍스처가 섞인 목록, 프레임마다 새로 만드는 목록, 콜백, 기존 primitive 삼각형 확장, 원본 정점 65,536개 초과와 단일 primitive 명령의 가상 정점 65,536개 초과는 기존 제출 계약을 사용한다. 화면 밖 명령은 유지된 인덱스 범위에서 제외하고 보이는 연속 범위를 합친다. 한 목록의 여러 범위는 같은 프레임의 가시성 판정을 공유한다. 카메라 변경은 인덱스를 다시 만들지 않으며, 정점 위치만 바뀌고 인덱스 연결이 같은 콘텐츠 버전도 해당 인덱스와 결합 계획을 재사용한다.

프레임 슬롯별 GPU 데이터는 해당 fence를 기다린 뒤 변경 부분만 쓴다. 명령 데이터와 행렬을 분리해 같은 레이어의 행렬을 명령마다 중복 전송하지 않는다. CPU 비교에는 CPU 복사본을 사용해 매 프레임 GPU 메모리를 읽지 않는다. resize·MSAA 변경·종료는 이 자원을 해제하며 교체된 묶음은 진행 중인 GPU 프레임이 끝난 뒤 해제한다. 현재 구현은 기존 원본 인덱스와 컴파일된 인덱스를 함께 유지한다. 기본 ERD에서는 추가 인덱스가 약 270MB이며, 이는 메모리 개선이 완료됐다는 의미가 아니다.

`UiRendererFrameMetrics.CompiledLayerDraws`는 실제 결합 드로우 수, `CompiledLayerCommands`는 그 안의 원본 명령 수다. `CompiledIndexUploadBytes`, `LayerStateUploadBytes`, `CompiledIndexCacheBytes`로 인덱스 전송·상태 전송·캐시를 구분한다. 기존 `DrawCalls`는 전체 GPU 드로우 수다. `DUXEL_VK_COMPILED_LAYERS=0`으로 비교용 직접 제출을 선택한다. 기본값은 결합 사용이며, 두 경로 모두 같은 벡터 품질·깊이·클립 계약을 유지한다.

`UiDrawList.CommandStateStamp`는 변경 없는 명령 디스크립터 상태를 나타내는 0이 아닌 값이다. 명령 종류·범위·텍스처·클립·이동·불투명도를 바꾸면 `ComputeCommandStateStamp(commands)`로 다시 계산한다. 0은 미확인이며 디스크립터 재사용을 끈다. 명령을 수정한 draw list 복사본도 스탬프를 다시 계산하거나 0으로 지워야 한다. 지오메트리 바인딩과 화면 변환은 별도로 캐시를 무효화한다. 상주 레이어 재생과 유지 배칭은 스탬프를 자동 설정한다. 슬롯 준비 후 카메라만 움직이면 디스크립터 재구성을 생략하며 CPU 가시성 검사와 GPU 그리기는 계속 수행한다. 가려진 명령 데이터도 채운 뒤 소스를 캐시하며, 화면 변환 변경은 가려진 멤버까지 무효화한다. `UiRendererFrameMetrics.CompiledDescriptorBuilds`는 CPU 디스크립터 구성 횟수이며 GPU 전송 바이트와 구분한다. `DUXEL_VK_DESCRIPTOR_REUSE=0`으로 끌 수 있다. `scripts/measure-erd-layers.ps1 -CompareDescriptorReuse -Executable <exe>`는 같은 실행 파일로 두 경로를 비교한다.

`UiRendererFrameMetrics.PresentationSequence`는 Vulkan `QueuePresent`가 성공 또는 suboptimal을 반환했을 때만 증가한다. `PresentationTimestampTicks`는 `Stopwatch` 시각이며, `PresentationIntervalSeconds`는 성공한 요청 사이의 간격이다. 첫 간격은 0이다. 건너뛴 렌더 호출은 이전 지표를 유지할 수 있으므로 순번이 증가했을 때만 간격을 소비한다. 모니터의 실제 표시 횟수나 GPU 완료 횟수와 구분한다. ERD 상태 패널과 원시 CSV는 이 시계를 사용하고 `fpsTimingBasis: accepted-presentation`을 기록한다. 해당 필드가 없는 과거 결과는 UI 루프 간격을 사용했다. 측정 실행기는 서로 다른 시계의 비교를 거부한다.

화면 안에 완전히 들어오고 명령 상태가 유지된 컴파일 멤버는 원본 명령을 순회하지 않고 연속된 기존 인덱스 범위를 사용한다. 부분 가시성·미확인·변경 상태는 명령 단위 순회를 유지한다. 미리 계산한 종류 전환 요약은 멤버 경계와 빈 명령을 포함해 프로파일 통계를 보존한다. `CompiledCommandVisits`는 실제 원본 명령 루프 방문 횟수이고, `CompiledLayerCommands`는 제출한 비어 있지 않은 원본 명령 수다. `DUXEL_VK_COMMAND_SPANS=0` 또는 `-CompareCommandSpans -Executable <exe>`로 비교한다. 카메라·모델 갱신과 GPU 그리기는 계속 필요하다.

공통 Vulkan 레이어 가시성 경로는 직접 제출을 포함해 0이 아닌 `CommandStateStamp`로 전체 경계를 재사용한다. `ComputeCommandStateStamp`는 이제 경계 좌표와 `HasBounds`도 포함하며, 이 값이 바뀌면 스탬프도 갱신해야 한다. 캐시에는 원본 좌표의 경계만 저장하고 매 프레임 현재 카메라·모델·화면 변환으로 가시성을 다시 판정한다. 미확인 스탬프는 경계를 다시 합산하고 미확인·잘못된 경계는 보수적으로 표시한다. 목록 순서가 바뀌면 위치별 캐시 상태를 비교한다. `LayerBoundsCommandVisits`는 레이어 경계 합산에 방문한 명령 수이며 `CompiledCommandVisits`와 구분한다. `DUXEL_VK_LAYER_BOUNDS_REUSE=0` 또는 `-CompareLayerBoundsReuse -Executable <exe>`로 비교한다. CPU 경계만 재사용하며 레이어별 래스터 타깃을 추가하거나 GPU 벡터 그리기를 생략하지 않는다.

`UiImmediateContext.TryReplayLayer(id, in options)`는 스코프를 열지 않고 유효한 상주 내용을 제출한다. false이면 기존 `BeginLayer`/`EndLayer` 경로로 내용을 생성하거나 갱신한다. 일치하는 내용 버전(또는 정적 캐시), 변경되지 않은 항목과 저장된 내용이 필요하며 유효한 빈 내용도 true다. 실패한 조회는 스코프를 열거나 저장된 버전을 바꾸지 않는다. 성공한 재생은 현재 장면을 검증하고 불투명도·이동·부모의 텍스트 의존성과 프레임별 카메라 래퍼를 보존한다. 직접 재생에 성공했으면 `EndLayer`를 호출하지 않는다. ERD의 격자·관계선 묶음·선택 관계선·테이블·미니맵은 이 공통 엔진 API를 사용한다. `DUXEL_ERD_DIRECT_REPLAY=0`이면 기존 스코프를 사용하고, `scripts/measure-erd-layers.ps1 -CompareDirectLayerReplay -Executable <exe>`로 같은 실행 파일을 비교한다. 스코프 준비와 복사는 줄이지만 레이어별 조회·제출은 계속하며 장면 전체를 한 번에 제출하는 API는 아니다.

```csharp
if (!ui.TryReplayLayer(id, options))
{
    if (ui.BeginLayer(id, options)) DrawContent(ui);
    ui.EndLayer();
}
```

상주 지오메트리와 명령 상태는 첫 상주 제출에서 획득해 `UiDrawData`로 전달하는 CPU 프레임 단위 임대 하나로 보호한다. 반환 대기 중인 풀 버퍼는 이를 참조하는 마지막 프레임 세대가 끝나면 반환하므로, 새 프레임이 계속 겹쳐도 오래된 자원이 쌓이지 않는다. 빌린 프레임이 남아 있으면 상주 소스나 재생 명령 배열을 수정하기 전에 복사한다. 카메라만 이동하는 프레임은 기존 지오메트리를 계속 공유하며 GPU 전체 대기나 레이어 렌더 타깃을 추가하지 않는다.

`GetDrawData()`가 반환한 소유 객체는 소비 후 해제한다. 반복 호출이나 레코드 복사로 별도의 소유권이 생기지는 않는다. `UiContext`는 수령하지 않은 출력을 다음 프레임·다음 Render·EndFrame·종료 시 정리하고, 중단된 렌더의 보류 중인 상주 프레임 임대도 반환한다. 수령한 프레임은 UI 컨텍스트가 종료된 뒤에도 지오메트리를 유지할 수 있다. 이 보장은 지오메트리·명령 스냅샷에 관한 것이며 UiContext의 동시 쓰기를 허용하는 것은 아니다.

`UiDrawData.ScaleClipRects`와 `UiDrawList.ScaleClipRects`는 빈 경우도 독립 소유 복사본을 만든다. 원본의 지오메트리·카메라 프레임 임대는 복사하지 않고, 지오메트리·명령과 함께 텍스처 갱신 목록 컨테이너를 복사한다. 클립 배율 복사본의 텍스처 픽셀 데이터도 독립 소유 메모리에 복사한다. 원본과 클립 배율 복사본은 독립적으로 해제할 수 있다.

`UiImmediateContext.GetDrawDataFrameId()`는 UiContext 안에서 현재 Render 호출을 식별한다. 이 값은 `UiDrawData.DrawDataFrameId`로 전달되고 클립 복사본에도 유지되며 `UiRendererFrameMetrics`에 포함된다. 렌더러의 성공한 표시 순번인 `PresentationSequence`나 지오메트리 내용 버전과는 구분한다.

`GetPresentationReader()`는 일관된 최신 지표 스냅샷·성공한 표시 표본의 유한 FIFO·덮어쓴 표본 수를 제공하는 `IUiPresentationReader`를 반환한다. `Drain(Span<UiRendererFrameMetrics>)`는 단일 소비자용 표본을 꺼내며 UI 상태 버퍼가 종료된 뒤에도 마지막 결과를 읽을 수 있다. UI 편의 메서드도 같은 이력을 사용한다. 같은 표시 순번은 최신 지표만 갱신하고 표본을 중복 추가하지 않으며, 하나의 렌더러 이력에서 순번이 역행하면 거부한다. GPU 타임스탬프는 기존 프레임 슬롯 지연을 유지하므로 현재 소스 ID의 GPU 시간으로 재해석하지 않는다. UiContext 자체의 동시 쓰기를 허용하는 기능은 아니다.

ERD는 `presentation-history-v1`로 CPU 장면 통계를 소스 프레임 ID에 연결하고, 표시 순번의 연속성·실제 표시 시각에 따른 준비 구간·저장 시 마지막 결과를 검사한다. 기존 한 열짜리 프레임 CSV는 유지하고 `.presentations.csv`로 각 표본의 표시 순번·소스 ID를 기록한다. 별도 시간 버퍼 대신 BenchFrameRecorder.FrameSeconds를 재사용하고 같은 용량의 기존 버퍼를 소스 ID 저장에 사용한다. 실행기는 연결 행·순서·개수·덮어쓰기·준비 구간 정보를 감사하며, 수집 방식이 다르면 개선율을 내지 않는다. 아래의 제한된 선택형 파이프라인이 이 표시 이력을 사용한다.

`DuxelFrameOptions.EnableRenderPipeline`으로 UI 생성과 렌더러 실행을 겹친다(기본 false). 전용 소비자가 Vulkan 호출·설정·표시 피드백을 담당하고 UiContext는 생산자 하나만 쓴다. 대기 프레임1개와 실행 프레임1개로 제한하며 넘치는 프레임을 버리지 않고 생산자를 기다리게 한다. 설정은 소스 프레임과 함께 전달하고 리사이즈·최초 표시 알림은 실제 표시 성공 뒤 발생한다. 종료 시 대기 프레임을 처리하고 스레드를 합류시킨 뒤 UI·렌더러를 해제한다. 소비자 오류는 대기 프레임의 소유권을 해제하고 종료 경로를 깨우며 원래 예외를 전달한다.

`UiDrawData.CaptureTexturePayloads()`는 독점 소유권을 다른 스레드에 넘기기 전 텍스처 갱신 데이터 조각을 풀에서 빌린 프레임 소유 버퍼 하나에 복사한다. 픽셀 데이터가 없는 프레임은 버퍼를 만들지 않고 변경 없는 상주 지오메트리도 복사하지 않는다. 생산자가 원본 픽셀 메모리를 수정하기 전에 호출해야 한다. `ScaleClipRects`도 픽셀 데이터를 독립 복사하므로 원본 프레임 해제 뒤 사용할 수 있다. 레코드 복사는 독립 수명이 아닌 공유 소유권이다. 콜백이 있는 프레임은 완료 장벽을 통과한 뒤 다음 UI 프레임을 생성하고 일반 프레임은 겹쳐 실행한다. 이는 UI와 콜백의 동시 실행 방지이며 콜백의 스레드 ID 보장은 아니다. 외부 콜백은 UI 생산자 스레드에서 실행된다고 가정해서는 안 된다.

ERD는 `DUXEL_ERD_PIPELINE=1`로 이 엔진 옵션을 선택하고 실행 JSON의 `renderPipeline`에 기록한다. 측정 실행기의 `-CompareRenderPipeline`은 같은 실행 파일에서 Off/On 순서를 교대하고 결과의 모드를 검사한다. GPU 검사에는 큐의 생산자 대기·순서·종료·예외 전달·텍스처 소유권·Vulkan 소비 전 생성한 상주 지오메트리3개 버전이 포함된다. 전체 성능·입력 검증 전에는 명시적으로 켜는 옵션으로 유지한다. 병렬 APP_PROFILE의 `getDraw`에는 큐 대기 시간이 포함되고 `total`은 소스 생성부터 렌더 완료까지의 지연이며 직렬 CPU 작업량이 아니다. SubmitTimeMs는 마지막 완료 렌더 시간이다.
파이프라인의 준비 구간은 `fpsWarmupMethod: first-accepted-presentation-v1`이다. 두 번째 UI 호출이 아닌 최초 표시 성공 시각에서 정확히 Stopwatch 1초 뒤를 마감으로 사용한다. JSON에 시작 시각과 시계 주파수를 남기고 비교 실행기는 준비 시간·최초 측정 시각·samples와framesAfterWarmup 개수 일치를 검사한다. 준비 방식이 다르면 개선율을 계산하지 않는다. UI 기준이었던 앞선12회 결과는 render-queue-ab에 INVALID_FOR_FINAL_COMPARISON으로 분리 보존하며 현재 FPS로 사용하지 않는다. 직접 소스에서 확인한 원인과 수정 후 재측정 예산도 기록했다. ERD는 이제 엔진 파이프라인을 기본으로 켜며 `DUXEL_ERD_PIPELINE=0`으로 직렬 실행을 선택한다. 라이브러리 옵션 자체는 콜백의 스레드 제약이 있는 소비자를 위해 명시적으로 켜도록 유지한다.
생산자는 입력을 읽고 UI를 만들기 전에 대기 슬롯이 빌 때까지 기다린다. 따라서 실행 프레임과 대기 프레임 뒤에 세 번째 CPU 프레임을 미리 만들지 않으며 렌더러와 UI 작업은 계속 겹친다. 콜백 명령은 요소·지오메트리가0이어도 실행 작업이다. 빌더 출력·정적 추가·상주 재생에서 보존하고 같은 콜백이 연속돼도 병합하지 않으며 Vulkan이 명령 순서대로 호출한다. 콜백 프레임은 완료를 기다린 뒤 다음 UI를 만든다. 병렬 모드의 콜백은 소비자 스레드에서 실행하며 제출한 지오메트리 수정이나 재귀 렌더링·Vulkan 기록 상태 변경은 허용하지 않는다.
`DuxelFrameOptions.UseEventDrivenPlatformLoop`으로 UI·렌더러 스레드가 동작하는 동안 플랫폼 소유 스레드는 이벤트를 기다린다. 명시적인 `IPlatformEventWakeBackend` 기능이 필요하며 없으면 GPU 준비 전에 실패한다. 라이브러리 기본값은 false다. Windows의 `WakeEvents()`는 호출 시점을 보존하는 스레드 안전 신호이며 MsgWaitForMultipleObjectsEx/MWMO_INPUTAVAILABLE로 이 신호와 입력 큐를 함께 기다린다. SafeHandle 참조로 네이티브 대기 중 핸들을 보호하고 이미 확인한 미처리 입력도 전달한다. 대기·반환 오류는 명시적으로 실패시키며 깨우기 실패를 주기적 확인 시간으로 대신하지 않는다.

Exit는 UI·플랫폼 대기를 깨우고 리사이즈 표시 승인 대기를 취소한다. 커서 변경과 작업 스레드 오류도 플랫폼 소유자를 깨운다. 동시 호출에 사용하는 잠금 아래에서 세션의 깨우기 콜백을 해제한 뒤 백엔드·핸들을 정리한다. 플랫폼이 잠든 동안에도 RequestFrame으로 UI·렌더링을 진행한다. 최초 표시·리사이즈 알림은 기존처럼 실제 표시 성공에 연결한다. 검사는 실제 App 어셈블리를 참조하며 WM_QUIT가 스레드의 큐에 속하므로 실제 앱 검사는 독립 소유 스레드를 쓴다. 검사 시간 제한에 의한 강제 깨우기는 성공으로 처리하지 않는다.

ERD는 이 옵션을 기본으로 켜며 `DUXEL_ERD_EVENT_LOOP=0`으로 폴링을 선택한다. `-ComparePlatformEventLoop`은 렌더링 파이프라인을 켠 같은 실행 파일에서 두 모드를 교대한다. JSON에는 모드·앱 실행 구간 CPU밀리초·단조 시계의 벽시계 밀리초·두 값의 비율인 `averageBusyCpuCores`(논리 코어1개를 계속 사용하면1)·프로세스 수명 동안의 최대 작업 집합을 기록한다. CPU 구간은 초기화·준비 구간·종료를 포함하며 최대 작업 집합은 GPU VRAM이나 안정 상태 힙 사용량이 아니다. 카메라 시계·지오메트리·품질 경로는 유지한다.
상세 CPU 시간 계측은 `DUXEL_VK_PROFILE_EVERY`로 출력할 프레임에서 수행한다. `DUXEL_VK_PROFILE_TIMING_SAMPLING=0`은 기존처럼 매 프레임의 상세 CPU 시간을 구하고1이 기본값이다. 잘못된 불리언 값은 명시적으로 실패한다. 렌더러의 공통 시간 함수는 비수집 시작값으로0을 반환하고 종료 함수도 이 값에 대해0을 반환해 가짜 경과 시간을 만들지 않는다. 드로우·업로드·상주·명령 카운터, 할당 계측, GPU 타임스탬프와 성공한 표시 시계는 별개로 유지한다. 지오메트리·셰이더·MSAA·카메라 동작은 바꾸지 않는다.

`UiRendererFrameMetrics.CpuTimingCollected`는 상세 CPU 시간 수집 프레임을 나타내며 다른 프레임의 카운터·GPU 계측을 생략했다는 뜻이 아니다. ERD JSON은 준비 구간 이후 실제 `cpuTimingCollectedFrames`·`gpuTimingSamples`를 기록한다. `-CompareCpuTimingSampling`은 같은 Native 이미지에서 로그 주기1,000,000과 엔진 파이프라인·이벤트 루프를 고정해 비교한다. 기준 모드는 측정 프레임 모두, 샘플 모드는 짧은 실행 동안0프레임에서 상세 CPU 시간을 수집했는지 검사하고 GPU 계측·드로우·준비 구간·프로세스 계약도 확인한다. 유휴 벤치마크는 의도적으로 표본을 기록하지 않으므로 이 비교에서는 자식 실행 전에 거부한다.
임시 CPU 빌더의 자동 예약량은 이제 버퍼를 소유하는 목록의 최대 청크 크기를 기준으로 한다. 빌린 상주 레이어의 전체 정점·인덱스 수를 임시 버퍼에 다시 예약하지 않는다. 공개 장면 합계·GPU 지오메트리·명시적인 Reserve API는 유지한다. NET10/11 GPU각366개, 직접 제출342개가 통과했다. NativeAOT 성능·메모리 비교 전이므로 이 변경의 FPS 향상이나 작업 집합 감소는 아직 주장하지 않는다.

프레임의 정적 기하 바인딩은 조밀한 draw-list 번호로 직접 조회하는 배열을 사용한다. 프레임 준비·동적 업로드·GPU 결합 계획·드로우 기록에서 같은 프레임 바인딩을 공유한다. 프레임마다 이전 참조를 지우고 새 목록 수로 범위를 설정하므로 목록 축소·빈 프레임·동적 목록에는 오래된 바인딩이 적용되지 않는다. GPU 자원의 소유·fence·퇴역 정책은 유지한다. 해시 조회 제거의 성능 효과는 별도 NativeAOT 비교 전에는 미확정이다.

## 현재 소스의 .NET10/11 비교(2026-11-03)

SDK10.0.301/런타임10.0.9와SDK11.0.100-rc.3.26522.148/런타임11.0.0-rc.3.26522.148을 사용했다.11은RC다. 같은 소스의 Managed/NativeAOT 네 조합을 각각3회,총12회/72구간 실행하고 소스 지문·실행 유형·원시 표본 개수·종료를 검증했다. 각 구간은0.25초 준비+1.5초 측정,실행2는 구성 및 구간 순서를 뒤집었다. 레이어 내용은 매 프레임 변하며 정적 레이어 캐시는 사용하지 않는다. MSAA1,VSync off,직렬 렌더링,UI 프레임 시작 간격 기준이다. ERD의MSAA4·병렬 렌더링·표시 성공 기준1000목표와 직접 비교하지 않는다.

아래 값은 각 실행 평균 FPS와 프레임 분포의3회 중앙값이다. .NET11 개선율이 입증된 경우는 Managed8×1000(+16.52%),NativeAOT1×1000(+23.37%),8×1000(+21.37%),8×4000(+38.35%)이며 실행 범위가 겹치지 않는다. 나머지.NET10/11 FPS 범위는 겹쳐 개선율을 주장하지 않는다. NativeAOT가 항상 가장 빠르다고 가정하지 않는다. 원시 자료와 전체2D/3D구간·범위는artifacts/perf-2026-11-03/runtime-current/summary.json에 있다.
| Variant | Layers × shapes | FPS | median ms | p95 ms | p99 ms | 1% low FPS | Scene bytes/frame |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| net10-aot | 1 × 1000 | 6462.2 | 0.1279 | 0.2423 | 0.3639 | 1621.93 | 432 |
| net10-aot | 8 × 1000 | 1930.36 | 0.4921 | 0.6273 | 1.0268 | 685.18 | 3456 |
| net10-aot | 8 × 4000 | 570.06 | 1.6264 | 2.4658 | 3.2484 | 244.62 | 3456 |
| net10-managed | 1 × 1000 | 8121.65 | 0.1031 | 0.2039 | 0.32 | 1988.81 | 400 |
| net10-managed | 8 × 1000 | 2280.42 | 0.3734 | 0.704 | 1.126 | 685.64 | 3200 |
| net10-managed | 8 × 4000 | 699.23 | 1.1496 | 2.2268 | 2.767 | 276.41 | 3200 |
| net11-aot | 1 × 1000 | 7972.37 | 0.1121 | 0.1856 | 0.2874 | 2083.27 | 432 |
| net11-aot | 8 × 1000 | 2342.79 | 0.3998 | 0.5634 | 0.9127 | 836.02 | 3456 |
| net11-aot | 8 × 4000 | 788.66 | 1.2213 | 1.4647 | 2.0485 | 387.47 | 3456 |
| net11-managed | 1 × 1000 | 8302.55 | 0.1017 | 0.1974 | 0.32 | 1984.42 | 401.323375893359 |
| net11-managed | 8 × 1000 | 2657.16 | 0.3535 | 0.4946 | 0.7595 | 942.81 | 3200 |
| net11-managed | 8 × 4000 | 867.37 | 1.1118 | 1.354 | 1.9715 | 417.73 | 3200 |
GPU 프로필은64프레임 간격의 별도 진단이다. 지연 쿼리의 구간 경계 혼입을 피하려고 각 구간 첫 로그를 제외했고 준비 구간은 포함한다. FPS 표본 집합과 같은 것으로 취급하지 않는다. gpu-profile-summary.json은72구간의 GPU 시간·draw call·동적 업로드·깊이 attachment 바이트를 보존하며 전체VRAM 측정값은 아니다. audit.json에서12회/72개 원시 프레임 파일/72개 GPU 구간 및 자식 프로세스0을 확인했다.

GPU 결합 그룹의 기본 인덱스 한도는2,097,152개다. DUXEL_VK_COMPILED_INDEX_LIMIT는1,048,576/2,097,152/4,194,304/8,388,608개만 허용하며 다른 값은 명시적으로 거부한다. 디스크립터256개와 전체 인덱스 캐시512MiB 제한은 유지한다. UiRendererFrameMetrics.CompiledIndexLimit는 실제 설정값을 보고한다. ERD 측정 JSON의 compiledIndexLimit와 compiledIndexCachePeakBytes는 실제 설정과 측정 프레임에서 관측한 최대 인덱스 캐시 바이트이며 전체VRAM이 아니다. scripts/measure-erd-layers.ps1 -CompareCompiledIndexCapacity는 같은 실행 파일에서1Mi/2Mi를 교대 비교하고 설정·캐시·드로우·GPU·표시 표본을 검증한다.1.2M 인덱스 GPU 경계 검사에서2→1드로우,동일 픽셀,카메라 변경 시 재업로드0을 확인했다. NET10/11각390검사·직접 제출366검사·실행기10/10을 통과했다. ERD FPS 효과는 반복 비교 전까지 미확정이다.

인덱스 한도1Mi/2Mi 동일 NativeAOT 비교 완료(2026-11-03):1200테이블,dense/resident,MSAA4,VSync off,각18초 명목 실행×3회. 현재 카메라988.05 FPS(98.80%,범위899.53–1044.10),엔티티 동시 이동966.90 FPS(96.69%,828.92–1074.02),단일 실행 평균 최고1074.02다. 안정1000목표는 둘 다 미달.1Mi/2Mi FPS는923.78→988.05,1058.46→966.90으로 실행 범위가 겹쳐 개선율·회귀율은 미확정이다. 평균 드로우는97.59→91.91,80.69→74.65이며 인덱스 캐시 최대는270330504/270330552바이트로 모드 간 동일하다. GPU 경계의2→1드로우·픽셀 동치와 실제 실행 평균을 구분한다. 프레임 평균은 카메라 상태의 표본 가중치를 포함하므로 같은 시점의 순수 작업 감소율로 해석하지 않는다. 원시 프레임·표시 연결·실제 한도·캐시·준비·프로세스·생성0/1회 감사12/12통과. 기본EXE/PDB는측정이미지60FE419D와 해시가 일치한다. 최신요약fps-current.json,근거index-capacity-ab참고.

측정 조건 점검:App의자동종료는폰트 준비 전appStartUtc에서 시작하고,ERD카메라 투어는렌더 시작부터진행하며,표본은첫표시성공+1초이후다. 실제표본구간은명목18초중14.84–15.21초였고가시명령의평균도달랐다. 이사실만으로FPS변동의모든원인을확정하지않는다. 추가전체비교전에준비구간과측정용카메라/동작시작을분리해실험조건을교정해야한다. 기존결과는해당명목실행의관측값으로보존한다. runtime-current의.NET10/11비교는이한도변경전소스지문의동일조건비교이며새빌드의최종성능재측정으로승격하지않는다.

실제 동적 3D 샘플과 창 제목 표시줄을 현재 렌더러로 실행한 GPU 버퍼에서 상태 패널 제목·하단 문구·간결한 줄 간격·콘텐츠에 맞는 크기를 확인했다. NET10/11 각각8/8검사,색상 도형 검사 포함,최종 픽셀 전체 동치. 근거:artifacts/perf-2026-11-03/status-panel-gpu-review.json. 데스크톱/DWM 캡처나 FPS 측정이 아니다. 초기 텍스처 순서 가설은 픽셀 동치로 기각했으며 엔진 결함으로 기록하지 않는다.

픽셀 버퍼의 소유권은 `PlatformTextRasterizeResult.PixelOwner`와 `UiTextureUpdate.PixelOwner`의 선택적 `IMemoryOwner<byte>`로 전달한다. 렌더링에 사용할 동안 픽셀은 변경하지 않는다. 소유권이 없는 기존 업데이트는 `UiDrawData.CaptureTexturePayloads()`가 복사하고,독점 소유한 업데이트는 원본을 프레임이 보유해 추가 복사를 생략한다. 프레임 소비·폐기,대체된 큐 항목,측정만 한 텍스트의 캐시 제거와 컨텍스트 종료에서 소유권을 반환한다. GPU 텍스처의 수명은 기존 펜스/퇴역 경로를 따른다.

Windows 여러 줄 텍스트의 합성 비트맵은 반환 가능한 풀 버퍼를 사용한다. 버퍼를 지운 뒤 기존과 같은 픽셀 합성을 수행하며,측정 결과만 반환하는 호출도 버퍼를 반환한다. 3D 벡터 레이어·셰이더·폰트 윤곽·카메라 품질은 유지한다. 직접 래스터화 결과를 받는 호출자는 사용을 마친 뒤 PixelOwner를 Dispose하거나,텍스처 업데이트에 한 번 소유권을 넘긴다. 넘긴 버퍼를 호출자가 먼저 반환하거나 변경하지 않는다.

메모리 수정 검증: .NET11 GPU422·.NET10텍스트 아틀라스422·직접 제출398검사,변경 전후 GPU이미지119/119바이트 동치. 전용 수명 검사에는 소유/차용 혼합 프레임,소비 스레드 반환,컨텍스트보다 오래 살아 있는 프레임,미소비 프레임 폐기,캐시 제거,투명 영역을 포함한 실제 래스터 바이트 재사용이 포함된다. 최신 NativeAOT 후보52E300FA의 첫180초 관측은 Private Bytes 중앙값-10559488바이트,작업 집합-11585536바이트이며 아직 단일 실행이다. 정량 개선율은 변경 전후 각3회가 끝난 뒤 판정한다. 기본1000FPS검증본09584FF4는 유지한다.

2026-11-03 MSAA 깊이 경계 수정: 3D depth-write 파이프라인은 MSAA1을 포함한 모든 표본 수에서 분석적 AA alpha를 hardware alpha-to-coverage로 전달한다. 덮인 sample만 불투명 RGB와 깊이를 쓰므로 앞쪽 둥근 사각형·원·테두리를 먼저 그려도 뒤쪽 도형과의 전체 픽셀이 같다. 별도 레이어 텍스처·합성 패스·sample-rate shading·alpha-to-one 기능을 추가하지 않는다. 2D와 depth-read 재질 alpha 혼합은 기존 경로를 유지한다. 1x는 한 표본의 경계 정밀도를 사용하며 기본4x의 화질은 유지한다. alpha-to-coverage는 유한한 sample mask이며 연속 반투명 재질이나 임의 순서 투명도 정렬을 대신하지 않는다. 1x에서 재현된 불투명 경계의389픽셀 순서 차이를0으로 해결했고 회귀 검사에 포함했다. sample mask 매핑은 드라이버 의존이므로 두 벤더 실측 완료로 보고하지 않는다.

실제 포함 셰이더를 GPU 초기화 전에 선언한 아티팩트 SHA256과 비교한다. 임시 SPIR-V 파일의 과거 타임스탬프가 복사되어 증분 빌드가 이전 리소스를 재사용한 실패를 확인했고,최종 아티팩트의 수정 시각과 새 빌드를 확인했다. 최종 GPU NET10/NET11각435·직접411·실제 상태 패널 각9검사와 기존 이미지119/119동치,Release경고/오류0을 통과했다. 별도2048도형/16면/320×240/MSAA4 Managed 집중 비교는 준비2초＋측정6초×각3회이며 연속 완료 시각 표본을 감사했다. FPS중앙값7458.46→7306.80,관측 차이-2.03%이나 실행 범위가 겹쳐 개선·회귀율은 미입증이다. ERD1000FPS 판정과 분리한다. 근거:`coverage-embedded-net10/`,`coverage-embedded-net11/`,`coverage-embedded-direct/`,`coverage-probe-v2-summary.json`,`coverage-existing-pixel-audit.json`(모두artifacts/perf-2026-11-03하위).

## 재현 가능한 최신 검증 명령

아래 명령은 최신 소스를 사용한다. .NET10 비교용 SDK10.0.403/runtime10.0.14와 .NET11 RC3 경로를 분리하고,저장소 기본SDK10.0.301은 유지한다. NativeAOT 처리량 측정은 `OptimizationPreference=Speed`를 명시하며 CPU명령어 집합의 이식성 기준은 바꾸지 않는다. [공식 최적화 설정](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing).

```powershell
# .NET11 RC3 NativeAOT ERD,1200테이블·MSAA4·투어/애니메이션 기본On
Push-Location tests/runtime-net11
../../run-fba.ps1 ../../samples/fba/erd_layer_lab.cs -TargetFramework net11.0-windows7.0 -NativeAot -NoCache -ExtraArgs @('-p:OptimizationPreference=Speed')
Pop-Location

# 저장된 측정의 원시 FPS·percentile·1%low를 다시 검사;앱 재실행 없음
./scripts/measure-erd-layers.ps1 -SummarizeOnly -OutputDirectory artifacts/perf-2026-11-03/coverage-complete-erd

# 공통 FBA 추가 인수 전달의 회귀 검사
./tests/RunFbaArgumentTests.ps1
```

`scripts/measure-dynamic-3d-runtimes.ps1`은 .NET10/11 Managed/NativeAOT 비교를 재사용한다. 모든 모드를 정상 표시 창으로 실행하고,별도 읽기 전용 관측기가 각 실행의 창 표시를 확인한다. 숨김 Native 창과 표시 Managed 창을 섞은 이전 결과는 직접 비교에서 제외한다. 원시 프레임으로 평균·선형 percentile·1%low를 재계산하고 GPU timestamp·실제 draw/upload·소스 해시·자식 종료를 확인한 뒤 각 실행을 기록한다. `-SourceManifestPath`와 새로운 `-OutputDirectory`를 명시한다. Native 이미지는 같은 소스로 미리 만들고,빌드 설정은 결과의 `NativeOptimization`과 일치시킨다.
