# SVG 브라우저 비교

Linux Gallery의 일반 실행에서 ‘테두리 범위’와 ‘호·곡선 비교’의 세로 스크롤 손잡이를 놓은 직후 다시 잡았습니다. 관리형·NativeAOT 모두 두 번째 드래그에서 코드 영역의 픽셀이 이동했고, 반대 방향으로 되돌리면 원래 화면과 일치했습니다. 실제 창은 1280×900으로 유지했습니다. [X11 손잡이 재선택 검증](L:/Cache/Duxel/linux-gallery-segmented-live-20261121/verification.json)

장면 셰이더는 곡선 경로에 들어 있는 직선의 정렬된 끝점을 픽셀 좌표계에서 판정합니다. 1/256픽셀 단위로 정렬한 뒤 샘플이 경계에 정확히 놓이면 교차로 세지 않습니다. 배율 0.75에서 Intel에 나타났던 커버리지 차이를 고쳤고 Intel과 NVIDIA 모두 스칼라 픽셀 비교 57개를 통과했습니다. Windows 관리형 반복 비교에서는 GPU별 세 쌍에서 수정 전후 복합 SVG 캡처 120장이 모두 일치했습니다. Intel 점선 검사는 기존과 같은 24/45였습니다. [검증 결과](L:/Cache/Duxel/intel-cubic-oracle-20261120/qualification.json)

Vulkan 점선 셰이더는 각 점선 구간의 첫 경로 인덱스를 끝까지 유지합니다. 닫힌 경로 전체를 덮는 점선에서 Intel이 마지막 선분만 그리던 문제를 고쳤습니다. 창 크기를 고정한 Windows 관리형 검사에서 Intel과 NVIDIA 모두 점선 비교 45개를 통과했습니다. NVIDIA의 점선 영역 45개는 변경 전과 바이트 단위로 일치했습니다. [검증 결과](L:/Cache/Duxel/intel-dash-diagnostics-20261120/qualification.json).

그래픽 검증 실행기의 테스트 전용 `gpu-boundary-hardware`, `gpu-boundary-hardware-approx`, `gpu-boundary-hardware-local`로 실제 Vulkan 삼각형 출력을 확인합니다. 현재 Windows 배율125%를 입력 조건으로 삼으며, 프레임버퍼가764×372(확대 타깃 검사는1024×512)가 아니면 장치 생성 전에 실패합니다. 시스템 설정이나 대화형 리사이즈는 바꾸지 않습니다. 같은 삼각형을 일반 Vulkan4×MSAA 메시 경로로 검은색·흰색 배경에 그립니다. 큰 타깃은 같은764×372 논리 영역을 비교하고, 지역 좌표 검사는 정수 경계 원점(215,303)으로 이동해 그린 뒤 원래 비어 있던 흰 영역을 복원해 비교합니다. 세 경우 모두 고정 브라우저 흰색 이미지와1픽셀·최대56 차이가 남습니다. 내부·외부 색상이 올바르게 캡처됐다는 검사는 진단의 전제 조건이며 브라우저 일치 통과를 뜻하지 않습니다. 제품 Core·SVG·Vulkan DLL은 conic 검증 시점과 바이트 단위로 같습니다. [결과](../artifacts/svg-native-boundary-2026-11-17/verification.json).

고정된 `RootTransforms` 기준은 소수 크기의 원본과 정수 크기 루트에 행렬·장치 좌표로 표현한 삼각형을 구분합니다. `--browser-root-transforms <새-출력-폴더>`로 흰색 초기 배경과 투명 출력을 각각 검사합니다. 투명 출력은 두 대조군과 일치하지만, 흰색 초기 배경에서는 세 경우 모두 (323,323)에서 실패합니다. 브라우저 대조군 자체도 해당 픽셀에서 흰색 초기 출력과 투명 출력의 사후 합성 사이에 채널값56 차이가 있습니다. 실제 4샘플 WebGL 삼각형은 현재 엔진처럼 샘플2개를 덮었습니다. 따라서 일반 정수 경계 계산이나 루트 변환만으로 모든 단계를 설명할 수는 없습니다. 제품 렌더링이나 허용 오차는 바꾸지 않았습니다. [검증 기록](../artifacts/svg-root-transform-2026-11-17/verification.json).

경계 상자에는 들어오지만 CPU 커버리지가 0인 도형도 `contributes: false`로 남겨, 브라우저에서만 덮이는 샘플을 진단에서 놓치지 않습니다. 면적 기반 커버리지처럼 이산 마스크가 없는 경우 `fillSampleMask: null`로 표시합니다. 원본 도형을 분리한 독립 브라우저 캡처에서 빨간 원본의 최대 오차는 회색 경계의 1픽셀(브라우저 알파191·CPU127), 노란 원본은 흰색 하이라이트(브라우저 알파64·CPU0)로 좁혀졌습니다. 노란 원본의 해당 지점에서 차체·그라데이션·그림자 조합은 일치합니다. 아직 해결되지 않은 커버리지 차이이며 제품 출력을 고친 것으로 처리하지 않습니다. [근거](../artifacts/svg-pixel-attribution-2026-11-17/verification.json).

관리형 테스트 실행기의 `--probe-pixel <svg> <너비> <높이> <x> <y> <새-json-파일>`로 특정 픽셀에 기여한 도형을 찾을 수 있습니다. 실제 컴파일된 장면에서 CPU 전체 픽셀, 원본 ID·트리 경로, 도형별 단독 출력값을 기록합니다. 단독 출력도 전체 뷰포트를 사용해 장치 좌표를 유지합니다. 부모 효과는 별도로 기록하며 단독 출력에는 적용하지 않습니다. 직접 연결된 svg/g/a 계층을 지원하고 use·텍스트·마커 확장은 명시적으로 거부합니다. 일반 경로의 샘플 마스크와 정수 고정소수점 경계 계산도 대조합니다. 이 진단은 차이의 위치와 원인을 좁히는 용도이며 독립 브라우저 기준이나 정확성 검증을 대체하지 않습니다.

2026-11-17 conic 구현: SVG 호의 제어점과 가중치를 보존하며 cubic 근사로 바꾸지 않습니다. 공통 기하는 정확한 극값·경계 계산, 클립·마스크·필터, 테두리의 접선·길이·분할·점선, CPU 진단과 GPU 평가를 연결합니다. 제어점과 가중치는 최초 컴파일 때 보관하고 카메라 확대는 GPU에서 평가합니다. 새 데이터 의미에 맞춰 장면·테두리 셰이더 모두 프로토콜4를 검사합니다. 실제 이전 프로토콜3 장면 셰이더와 이전 테두리 셰이더는 모듈 생성 전에 거부합니다. push constant 크기96/40바이트는 유지합니다.

고정된 호·대조군6개는 GPU 투명 출력과 흰색 초기 대상에서 모두 정확히 일치합니다. 관리형 `--browser-arc-opaque-target`도 흰색 초기 출력과 별도 투명 출력을 함께 검사해6개 모두 통과합니다. 기존 `--browser-arc-geometry`와 GPU `gpu-arc-geometry`는 투명 텍스처를 resolve한 뒤 흰색에 합성하는 별도 경로를 유지하며, 여전히 채널값1 차이가 남습니다. 기준·허용 오차를 바꾸거나 이 경로를 통과한 것으로 처리하지 않습니다.

| 비교 범위 | 이전 | 현재 |
| --- | ---: | ---: |
| 호6개 투명 GPU 이미지 | 2/6 정확 일치 | 6/6 정확 일치 |
| 호6개 흰색 초기 GPU 대상 | 2/6 정확 일치 | 6/6 정확 일치 |
| 빨간 원본의 기본 불투명 비교 | 36,176픽셀·최대55 | 33,419픽셀·최대56 |
| 노란 원본의 기본 불투명 비교 | 1,933픽셀·최대52 | 1,892픽셀·최대34 |
| 갤러리 문서 전체 비교 | 60픽셀·최대1 | 60픽셀·최대1 |

빨간 원본은 불일치 수가 줄었지만 최대 오차는1 커졌습니다. 원래 두 문서와 갤러리 전체 일치는 아직 미통과입니다. 새 conic 검사8개를 포함한 CPU309개, GPU 테두리46·점선46·장면67·픽셀58·배경30개가 통과했습니다. 잘못된 가중치·구간·종류는 GPU 제출 전에 거부합니다. 일반 위젯1,387개·Markdown312개가 통과했고 기존677개 화면은 바이트 단위로 같습니다. 성능 향상이나 전체 SVG 완료를 주장하지 않습니다. [검증 기록](../artifacts/svg-conic-2026-11-17/verification.json).

갤러리 **추가 샘플 → SVG → 호·곡선 비교**에서 같은6개 기준을 확인합니다. 확대·회전·여백·모서리·흰 배경 전환을 사용하며, 남은 배경 합성 차이는 별도로 표시합니다. Windows67개 실제 표시와 미리보기6개의 CPU 래스터 증가량0을 확인했습니다. Linux는 호스트 빌드만 확인했습니다.

2026-11-17 소스 합성 정밀도: CPU 진단과 Vulkan의 공통 source-over는 전경의 RGB·알파를 모두 RGBA8에 저장한 뒤 합성합니다. 알파만 반올림하는 방식은 독립 기준에 맞지 않아 채택하지 않았습니다. 기하 커버리지·그라데이션 보간·디더·셰이더 ABI는 변경하지 않습니다. 당시 프로토콜3을 사용했으며 현재 conic 데이터는 프로토콜4입니다.

`gpu-blend-backdrops`와 관리형 `--browser-blend-backdrops`는 색이 있는 배경 위의 선형·방사형 그라데이션12개를 독립 Chromium Canvas 기준과 전체 픽셀로 비교합니다. 원본·부모·기준 해시와 캡처 조건은 `tests/Duxel.Svg.Managed.Tests/Fixtures/Browser/BlendBackdrops`에 보존했습니다. 기존 복잡한 SVG의 기준 이미지나 허용 오차를 대체하지 않습니다. [이번 검증](../artifacts/svg-source-storage-2026-11-17/verification.json).

제품 검증에서 CPU·GPU 모두12개·49,152픽셀이 정확히 일치합니다. 기존 SVG301개, 일반 위젯1,387개, Markdown312개가 통과했고 기존 Markdown677개 화면은 바이트 단위로 같습니다. Windows 갤러리66개 표시 시나리오와 Linux 호스트 빌드도 통과했습니다. 원래 복잡한 SVG의 불투명 비교는42,430→36,176 및44,101→1,933픽셀로 개선되지만 최대 오차55·52와 전체 비교 실패는 남습니다. 투명 출력의 알파 오차는277픽셀로 유지되거나4,816→173픽셀로 감소합니다. 갤러리의 전체 비교도96→60픽셀로 줄었으나 미통과입니다. 성능 개선이나 전체 SVG 완료를 주장하지 않습니다.

```powershell
$exe = 'W:\Cache\Duxel\svg-rounded-stroke-20261116\publish-491\Duxel.Graphics.Tests.exe'
& $exe 'W:\Cache\Duxel\blend-review-001' 'gpu-blend-backdrops'
```

출력은 아직 없는 새 디렉터리를 지정하세요. 이 검사는12개 합성 기준이며 원래 두 복잡한 문서의 전체 수용 검사를 대신하지 않습니다.

2026-11-17 GPU 대상 배경: 공통 엔진의 `UiTextureUpdate.VectorBackground`와 위젯의 `UiSvg.Background`를 연결했습니다. 지정 색을 GPU 대상의 초기값으로 사용하고, 내부 의존 장면은 투명을 유지합니다. 배경만 변경하면 장면 업로드 없이 한 번 그리며, 같은 위젯 배경으로 돌아오면 캐시를 재사용합니다. 셰이더 프로토콜은3으로 올리고 실제 이전 프로토콜2 SPIR-V를 모듈 생성 전에 거부하는 검사를 추가했습니다. push constant 범위는96바이트, 배경 필드는 offset88이며 추가 Vulkan 기능을 요구하지 않습니다.

새28개 검사는 정수 기준의 전체 픽셀·반투명 배경·캐시·letterbox·카메라·그룹·마스크·패턴·Gaussian 필터·실제 위젯과 해제를 확인합니다. 기존1,387개·Markdown312개·GPU 벡터57개·읽기9개·SVG 픽셀49개·문서 기능30개가 통과했습니다. 기본 투명 출력은 이전 두 복잡한 SVG 및677개 Markdown 원본 이미지와 바이트 단위로 같습니다. Windows 갤러리66개 시나리오는 새 스위치의 두 상태와 실제 화면 제출을 확인했고, Linux는 호스트 빌드만 확인했습니다.

원래 Canvas 전체 비교는 기본 투명 경로에서 여전히42,430·44,101픽셀 차이로 실패합니다. 흰색을 대상에 먼저 넣은 별도 `gpu-svg-opaque-target-browser`에서는41,858·47,735픽셀로 한 문서는 줄고 다른 문서는 늘었습니다. 따라서 이 경로를 전체 일치 해결책으로 적용하지 않았습니다. 갤러리 문서 기능의96픽셀 차이도 유지합니다. 기준 이미지·허용 오차는 바꾸지 않았습니다. [검증 기록](../artifacts/svg-target-background-2026-11-17/verification.json).

2026-11-17 기준 입력 검증: 캡처 도구가 요구하는 DPR1·화면 영역 정렬 조건을 관리형·GPU 비교기에도 연결했습니다. 보존된 두 직접 표시 기준은 기록된 영역 크기가 정수 픽셀과 달라 이제 렌더링 전에 거부됩니다. 기준 이미지와 허용 오차는 바꾸지 않았습니다. 정확한 영역으로 새로 캡처한 Red Gallardo도 두 반복에서 7,698픽셀(최대 채널 차이56)이 달라 새 기준으로 채택하지 않았습니다. 두 Canvas 출력은 반복 결과 및 기존 불투명 기준과 바이트 단위로 같습니다. 직접 표시의 재현성과 엔진의 출력 차이는 별도로 해결해야 합니다.

`svg-reference-contracts-only`는 새 입력 계약12개를 검사하며 통과합니다. `gpu-svg-image-browser`는 직접 표시를 완료한 것으로 취급하지 않고, 원래 두 문서의 Canvas 전체 이미지 검사를 별도로 실행합니다. 현재 각각42,430·44,101픽셀 차이로 둘 다 실패합니다. 기존 `gpu-svg-browser`와 관리형 `--browser`는 잘못 정렬된 직접 표시 기준을 렌더링 전에 거부합니다. 관리형 SVG 회귀301개는 통과했습니다. 단위원 좌표 그라데이션 후보는 출력이 그대로여서 제외했고, 제품 DLL5개와 내장 셰이더는 이전 정상본과 같습니다. [검증 기록](../artifacts/svg-reference-preflight-2026-11-17/verification.json).

```powershell
$exe = 'W:\Cache\Duxel\svg-rounded-stroke-20261116\publish-476\Duxel.Graphics.Tests.exe'
& $exe 'W:\Cache\Duxel\reference-contracts-review-001' 'svg-reference-contracts-only'
& $exe 'W:\Cache\Duxel\original-images-review-001' 'gpu-svg-image-browser'
```

두 번째 명령은 현재 이미지 차이 때문에 실패하는 것이 정상입니다. 전체 일치나 전체 SVG 목표의 완료를 뜻하지 않습니다.

2026-11-17 렌더러 확인: 현재 Chrome156.0.8078.96의 실제 Skia 경로는 `GraphiteDawnD3D11`, 리비전은 `ea1e651d7a105f5ca327dfd65d28957957279e2e`입니다. 새로 캡처한 불투명 기준은 기존 파일과 바이트 단위로 같고, 투명 기준도 premultiplied RGBA8 전체가 같습니다. 원래 캡처 당시의 백엔드는 기록되지 않았으므로 소급해 단정하지 않습니다. `renderer-verification.json`을 실제 합성 검사에서 읽고 원본·단계별 기준 해시를 GPU 작업 전에 확인하며, 잘못 연결된 기준과 누락된 렌더러 정보를 거부합니다. 검증된 정보는 `composition.json`에도 남깁니다. 경계 보간 후보는 96→97픽셀로 악화됐고 연산 순서 후보는 출력이 그대로여서 둘 다 채택하지 않았습니다. 새 계약6개와 기존 계약3개는 통과하지만 전체 이미지 검사1개는 여전히 실패합니다. 제품 셰이더는 바꾸지 않았습니다. [근거와 한계](../artifacts/svg-renderer-qualification-2026-11-17/verification.json).

2026-11-17: 별도 `svg-feature-composition-only` 검증 모드로 화면 합성 전의 GPU 텍스처를 읽고, 보존된 투명 브라우저 캡처 및 불투명 전체 이미지와 비교합니다. 갤러리 문서 기능 예제에서는 정수 premultiplied-over 계산과 화면의 51,200픽셀이 모두 같지만, 직접 그린 브라우저 이미지와는 여전히 96픽셀이 다릅니다. 최대 채널 차이는 1입니다. 투명 출력 비교에서는 1,454픽셀이 다르며 그중 알파가 다른 위치는 17개입니다. 서로 다른 렌더링 단계의 기준을 구분합니다. 전체 이미지 일치를 요구하는 검사는 현재 실패하므로 내부 색상 검사만으로 브라우저 동등성을 완료로 처리할 수 없습니다. 원래의 복잡한 SVG 문서도 별도 미완료 대상으로 유지합니다. 격리한 원 거리식 셰이더 후보는 불투명 차이가 96→180, 알파 차이가 17→101로 늘어 채택하지 않았습니다. 제품 셰이더와 제품 어셈블리 다섯 개는 직전 체크포인트와 바이트 단위로 같습니다. Skia 의존성·배경색 보정·허용 오차·셰이더 교체는 추가하지 않았습니다. [검증 기록](../artifacts/svg-feature-composition-2026-11-17/verification.json).

2026-11-16 추가 수정: 원·원형 모서리의 테두리는 비트맵 출력과 GPU에서 바깥·안쪽 경계의 해석적 커버리지 차를 공유합니다. 타원형 모서리·점선·일반 곡선은 기존 테두리 프로그램을 유지합니다. 고정 Observer 기준의 다른 픽셀은253→96개, 최대 채널 오차는65→1로 줄었으며 전체 일치는 미완료입니다. 중간에 보인 연한 파란 삼각형은 새 장면 데이터와 이전 내장 셰이더가 섞인 결과였습니다. 대응 셰이더를 반영하고 모듈 생성 전에 프로토콜2 표식을 검사합니다. 회귀29개에는 실제 이전 SPIR-V 거부·잘못된 데이터·원본 색상 보존이 포함됩니다. 정상 Observer 검증도 **SVG document features**를 포함한46개 시나리오로 확장했습니다. [검증 기록](../artifacts/svg-rounded-stroke-2026-11-16/verification.json).

2026-11-16: Observer **SVG → 문서 기능**에서 줄무늬·방사형 그라디언트가 연한 파랑 테두리에 덮이는 오류를 수정했습니다. 테두리 경로가 둥근 사각형·타원의 **채움** 커버리지 정보를 물려받아 테두리 생성을 건너뛰던 문제였습니다. `SvgStrokePlan.GpuSource`에는 테두리에서 계산한 분석적 커버리지만 남깁니다. 둥근 도형의 채움과 실제 둥근 끝점 선분의 최적화는 유지합니다. 원본 SVG는 변경하지 않았습니다. 색상 회귀12·격리 SVG301·GPU 테두리46·GPU SVG49 검사를 통과했습니다. 사전에 정한 내부8픽셀은 독립 Chromium 캡처와 정확히 일치합니다. 전체320×160 영역에는 경계·합성의253픽셀 차이와 최대 채널 오차65가 남아 전체 SVG의 브라우저 동등성을 완료로 처리하지 않았습니다. [검증 기록](../artifacts/svg-feature-colors-2026-11-16/verification.json).

완료 기준은 고정한 브라우저와 같은 결과입니다. 그림이 표시되거나 평균 오차가 작다는 이유로 통과시키지 않습니다. 현재 원본 두 문서는 전체 픽셀 일치 검사를 통과하지 못했습니다.

## 원본과 기준 이미지

| 원본 | 복잡도 | 출처·라이선스 |
| --- | --- | --- |
| Red Gallardo | 경로251개, 선형 그라데이션228개, 방사형51개, 패턴17개, Gaussian blur | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Red_Gallardo.svg), Tomeq183·Tacosunday·Svgalbertian, CC BY-SA3.0 |
| TheStructorr Lamborghini Gallardo | 경로132개, 선형 그라데이션82개, 방사형31개, 패턴3개 | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:TheStructorr_Lamborghini_Gallardo.svg), Michał Pecyna, CC0 |

원본은 수정하지 않았습니다. `tests/Duxel.Svg.Managed.Tests/Fixtures/Browser/sources.json`에 원본 URL·SHA-256·저작자를 고정했습니다. `references.json`에는 실제 브라우저 버전·배율·크기·수집 시점·픽셀 해시가 있습니다. 라이선스는 같은 폴더의 `ATTRIBUTION.md`를 따릅니다.

브라우저 내부에서 직접 SVG를 표시한 결과와 SVG 이미지를 Canvas2D로 그린 결과를 각각 확보합니다. 배경은 흰색, 색 공간은 sRGB, DPR은1입니다. 원본 크기를 유지한 `xMidYMid meet` 배치와 원본 뷰포트 클립을 사용합니다. 이미지 경로에는 투명 배경 픽셀도 별도로 보관합니다. 화면 캡처와 이미지 경로의 중간 색 저장 방식이 다르므로 서로 다른 기준으로 검사합니다.

흰 배경 결과는 모든 RGBA 채널의 차이가0이어야 통과합니다. 투명 결과는 알파와 premultiplied RGBA8 값을 정확히 비교합니다. 같은 premultiplied 픽셀을 나타내는 직선 알파 RGB의 표현 차이는 실제 표시 차이로 세지 않습니다. 평균 오차·큰 차이가 있는 픽셀 비율은 원인 분석용이며 통과 기준이 아닙니다.

## 옵저버에서 확인

```powershell
Start-Process -FilePath 'L:\Cache\Duxel\linux-accessibility-2026-11-14\observer-managed-build\bin\Duxel.Gallery\release\Duxel.Gallery.exe' -WorkingDirectory 'W:\MyWorks\Duxel' -WindowStyle Normal
```

**SVG → 브라우저 비교**를 엽니다. 왼쪽은 브라우저 기준, 오른쪽은 같은 원본을 그리는 `UiSvg`입니다. 배경을 흰색으로 맞추고, 두 그림에 같은 원본 픽셀 해상도를 사용합니다. 첫 진입에서 원본·기준 픽셀의 해시를 검사합니다. 다시 그리거나 탭을 왕복해도 XML을 다시 읽지 않습니다. **문서 기능**에서는 점선·마커·패턴·마스크를 확인합니다.

일반 옵저버 창은 자동 실행하지 않습니다. 이 경로는 .NET11.0.100 관리형 검증 빌드이며 고정 RC3 NativeAOT 결과가 아닙니다.

## 비교 실행

안정 SDK를 사용하는 `W:\MyWorks\Duxel\tests\managed-validation`에서 실행합니다. 결과 디렉터리는 매번 새 경로여야 합니다.

```powershell
Set-Location -LiteralPath 'W:\MyWorks\Duxel\tests\managed-validation'
dotnet run --project 'W:\MyWorks\Duxel\tests\Duxel.Svg.Managed.Tests\Duxel.Svg.Managed.Tests.csproj' -c Release -p:TreatWarningsAsErrors=true -p:UseArtifactsOutput=true -p:ArtifactsPath='L:\Cache\Duxel\svg-browser-build' -- --browser 'L:\Cache\Duxel\svg-browser-review-001'
```

각 경로의 실제 이미지·차이 이미지·`result.json`을 만듭니다. 차이가 남으면 종료 코드는1입니다. 현재 실패를 성공으로 바꾸기 위해 기준 이미지를 다시 생성하거나 허용 오차를 넓혀서는 안 됩니다.

재수집은 `CaptureBrowserReferences.mjs`의 `captureBrowserReferences(page, fixtureDirectory, freshOutputDirectory)`를 기존 브라우저 스킬 런타임에서 호출합니다. 원본 해시·DPR·크기를 검사하고 새 디렉터리에 기록합니다. 호출자가 브라우저 페이지 수명을 관리하며, 기존 기준은 자동 교체하지 않습니다.

## 경로 가장자리와 숨김 처리

관측한 Chrome156 Canvas2D는 GraphiteDawnD3D11이며 Skia 리비전은 ea1e651d7a105f5ca327dfd65d28957957279e2e입니다. Browser.getVersion과 전체 chrome://gpu 스냅샷은 analytic-coverage 근거에 별도 보존했습니다. ANGLE은 WebGL 정보이므로 Canvas2D 렌더러의 식별 근거로 쓰지 않습니다. 엔진은 관측한 일반 경로의4개 샘플 위치와 RGBA8 resolve를 기하 coverage에 적용하고, `SvgCubicTessellation`에서 Wang 식으로 화면 크기에 맞는 균일 분할 수를 정합니다. 분할 수는2의 거듭제곱이며 정점은1/256픽셀 격자에 맞춥니다. 획의 길이·점선 배치를 위한 기존 적응형 분할은 별도로 유지합니다. 타원·둥근 사각형·점선이 아닌 둥근 끝 직선은 SvgAnalyticRRect를 공유하며, 일부 입력은 아직 브라우저와 일치하지 않습니다.

`Fixtures/Browser/Coverage/`의 독립 브라우저 원본 픽셀6개에는 입력·출력 해시와 관측 환경이 있습니다. 삼각형·오목 다각형·곡선·사각형·숨김 상속·숨김 클립을 전체 RGBA로 비교합니다. Canvas 픽셀 크기를32×32로 고정해 DOM 배율을 적용하지 않았습니다. 이 작은 예제의 통과가 복잡한 원본2개의 통과를 뜻하지는 않습니다. 분리한 원본 경로는 이후 획·뷰포트 예제로 검증했으며, 원본2개 전체는 아직 미통과입니다.

`visibility`는 상속되며 `hidden`·`collapse`의 자식이 `visible`로 다시 표시될 수 있습니다. 숨긴 도형의 기하는 그룹 경계 계산에 남고, 숨긴 글자는 다음 글자의 배치 간격을 유지합니다. `initial`·`inherit`·`unset`, CSS 우선순위, 참조 심볼·이미지·글자와 클립에도 적용합니다. 옵저버 **문서 기능**의 흰 마름모는 숨긴 그룹에서 자식만 다시 표시하는 예제입니다.

분할 수의 수학적 근거는 [Wang 식 설명](https://github.com/google/skia/blob/main/src/gpu/tessellate/WangsFormula.h), 정점 격자의 근거는 [D3D11 rasterization 규칙](https://microsoft.github.io/DirectX-Specs/d3d/archive/D3D11_3_FunctionalSpec.htm)입니다. 구현은 자체 관리형 모듈이며 Skia 패키지·네이티브 라이브러리를 추가하지 않습니다. 현재 확인한 GPU 브라우저 조건과 다른 모든 브라우저·장치의 일치를 주장하지 않습니다.

## 엔진에 반영한 구조

`SvgStrokeCurve`는 점선으로 자를 때도 실제 제어점을 유지합니다. `SvgStrokePath`는 사용자 좌표의 길이를 별도로 측정하고, `SvgStrokeTessellation`은 요청한 화면 변환에서 Wang 위치 분할과 접선의 회전 분할을 합친 뒤 실제 접선을 따라 획을 확장합니다. 변곡점·반회전·일직선 첨점을 분리하고, 점 획·둥근 연결에도 같은 회전 정밀도를 사용합니다. 사각 끝은 별도의 접선 연장입니다. `CurveStrokes`의 독립 브라우저 예제20개는 Red 원본에서 분리한 획2개를 포함해 전체 RGBA를 고정하며, 두 원본 획과 채우기·획 결합도 일치합니다. 별도로 수집한 둥근 끝 직선은 analytic 경계 coverage가 아직 달라 실패 진단으로 보존합니다.

`SvgViewport`는 전체 문서를 합친 뒤 한 번 자르는 대신 각 그리기와 레이어 합성에 활성 픽셀 coverage를 제공합니다. 불투명도·필터·마스크 레이어는 대상에 합칠 때 클립을 적용합니다. analytic 사각형은 경계 coverage를 계산하기 전에 기하적으로 클립과 교차합니다. `Viewports`의 독립 전체 RGBA 예제16개는 반복 그리기·그룹·필터·사각형 경계와 원본의 채우기·획 결합2개를 포함합니다. Red 몸체 경로의39픽셀·1바이트 차이를 고쳤으며 폐기된 마지막 `ClipViewport` 처리를 제거했습니다.

`SvgStroke`는 뾰족한 획 연결의 내부 영역과 바깥 꼭짓점을 함께 채웁니다. 다각형 방향은 한 꼭짓점을 기준으로 double 정밀도로 구해, 좌표 이동에 따른 상쇄가 겹친 조각의 방향을 바꾸지 않게 합니다. `Fixtures/Browser/StrokeJoins`의 독립 브라우저 예제8개는 좌·우·사선 연결과 큰 원점의 전체 RGBA를 고정합니다. 수정 전에는 연결 영역 누락과 큰 좌표의 빈틈을 재현했습니다. 이후 곡선 획 교정은 별도의 `CurveStrokes` 예제가 검증하며, 두 작은 예제 모음으로 전체 원본의 적합성을 판단하지 않습니다.

색 디더링은8×8 Morton 순서와8비트로 정규화한 임계값을 사용합니다. [GPU 임계값 계산의 참고](https://github.com/google/skia/blob/main/src/gpu/DitherUtils.cpp)는 수학적 비교 자료이며 외부 렌더러 의존성이 아닙니다. 패턴·그라데이션의 불투명도는 `SvgCompositing.PaintServerOpacity`에서 반올림한8비트 값으로 변환합니다. `PaintServers/`의 패턴 불투명도0.1·0.5·0.9는 독립 브라우저의 전체 RGBA와 일치합니다.

별도로 수집한 선형·방사형 그라데이션6개에는 여전히 최대1바이트 차이가 있습니다. 단색 불투명도0.9의 빨강 채널도1바이트 차이가 남습니다. 이 진단을 통과로 바꾸기 위해 허용 오차를 추가하지 않습니다. 색 저장 정밀도와 원본2개의 완전 일치는 계속 미완료입니다.

경계가 겹칠 때는 coverage 평균만으로 다음 도형과 합성하지 않습니다. `SvgSamplePixel`이4개의 premultiplied RGBA8 색을 보관하고 `SvgRasterSurface.Samples`가 샘플 색이 다른 픽셀만 따로 유지합니다. 기존 픽셀 배열은 현재 합쳐진 색을 계속 제공하므로 공간 필터와 이미지 출력의 소비 경로를 공유합니다.

그룹 불투명도와 기하 클립은 샘플 위치를 유지합니다. 마스크 이미지는 색을 합친 뒤 적용하며, Gaussian 필터도 합쳐진 입력을 사용합니다. `mask-type="alpha"`와 기본 luminance 마스크를 구분합니다. `Fixtures/Browser/Compositing/`의 독립 브라우저 예제8개는 이 처리 경계를 전체 RGBA로 검사합니다. 인접 삼각형과 같은 삼각형의 중복 그리기에서 생기던 잘못된 알파 누적도 교정했습니다. 옵저버의 흰 마름모는 이제 삼각형4개로 구성되어 조각 경계의 합성을 확인할 수 있습니다.

이 예제의 통과와 복잡한 원본2개의 미통과는 구분합니다. 최초 래스터의 시간·할당·메모리 개선율은 측정하지 않았습니다.

| 모듈 | 책임 |
| --- | --- |
| Core `UiRgba8` | 직선 알파·premultiplied RGBA8 사이의 공통 변환 |
| SVG `SvgCoverageRasterizer` | 기하의 가장자리 coverage; 채색·합성과 분리 |
| SVG `SvgCompositing`·`SvgOrderedDither` | 같은 정밀도의 색 저장·합성·그라데이션 디더링 |
| SVG `SvgSamplePixel`·`SvgRasterSurface.Samples` | 샘플별 색과 레이어 처리 경계를 유지하는 합성 |
| SVG `SvgRasterSampling` | 내장 이미지와 패턴의 premultiplied 보간 |
| SVG `SvgStrokePlan` | 원본 선과 스타일을 보관하고 해상도에 맞춰 선 생성 |
| SVG `SvgPaintTemplates` | 로컬 그라데이션·패턴 참조를 최초 컴파일에서 해석 |
| SVG 뷰포트·필터 모듈 | 문서 클립과 Gaussian blur의 범위·색 공간·변환 |
| Vulkan 색 이미지 경로 | 업로드 시 staging 메모리에서 한 번 변환하고 GPU에서 보간한 뒤 공통 채색에 전달 |

`UiTextureUpdate.IsColorImage = true`인 입력은 계속 직선 알파 RGBA8입니다. 업로드 과정은 호출자의 버퍼를 바꾸지 않습니다. 폰트 coverage와 색 이미지를 구분하며, 글자 coverage를 이미지처럼 변환하지 않습니다. 원본 크기가 틀린 업로드는 잘라내거나0으로 채우지 않고 실패합니다. 같은 해상도의 재생에는 XML·선 생성·업로드를 반복하지 않습니다.

이번 기능에는 점선·위상·양수 `pathLength`, 그라데이션·패턴 참조 상속, 외부 편집기 메타데이터 구분, 단일 `feGaussianBlur`가 포함됩니다. 전체 SVG 사양 구현과 브라우저 일치는 계속 검증합니다.

## 공통 analytic 도형 coverage

`SvgAnalyticRRect`는 장면 컴파일 시 기준 사각형·타원 반경·아핀 좌표계를 보관합니다. 렌더링은 외접 carrier mesh에서 원래 로컬 좌표와 화면 AA 확장량을 보간한 뒤 타원의 음함수와 경계 평면으로 coverage를 계산합니다. 곡선 경계를 평평한 다각형 선분으로 근사하는 방식은 아닙니다. 작은 도형은 폭에 따른 coverage scale/bias를 사용하고, AA 경계의 페인트도 보간된 원래 좌표로 샘플링합니다. 경로는 bounds와 일반 획에 계속 사용하며 렌더링 중 XML 재처리나 Skia 실행 의존성은 없습니다.

독립 네이티브 브라우저 판정 입력은19개입니다. 동일 Linux x64 .NET11 런타임에서 전체 RGBA8의 정확 일치가1/19→11/19로 바뀌었고, 현재 Windows/Linux 출력19개도 모두 같습니다. 통과11개는 `Fixtures/Browser/Analytic/`에 해시로 고정했습니다. 사선 캡의 알파2픽셀·최대1 차이, 기울인 타원·원형 모서리·얇은 캡·그라데이션 사각형을 포함한 실패8개도 보존했습니다. 통과 예제로 전체 판정 입력을 대체하지 않습니다. 전체 원본2개·출력4경로는 여전히 엄격 비교에 실패합니다. 동일 부하 시간·할당·메모리 비교는 미실행이며 성능 향상을 주장하지 않습니다.

수학적 참고는 브라우저 리비전에 고정한 [Graphite analytic 렌더 단계](https://github.com/google/skia/blob/ea1e651d7a105f5ca327dfd65d28957957279e2e/src/gpu/graphite/render/AnalyticRRectRenderStep.cpp)와 [fragment coverage 함수](https://github.com/google/skia/blob/ea1e651d7a105f5ca327dfd65d28957957279e2e/src/sksl/sksl_graphite_frag.sksl)입니다. 한 렌더러·장치의 관측이며 모든 브라우저의 동일성을 뜻하지 않습니다. 고정 입력·실패 출력·전후 비교·렌더러 식별은 `artifacts/svg-analytic-coverage-2026-11-14/verification.json`을 참조하세요.

상단 조절 값은 브라우저 비교의 두 Duxel SVG에도 적용합니다. 회전·확대가 위젯 배치 크기를 바꾸고, 여백 조절은 왼쪽 공간을 확보합니다. 브라우저 캡처는 고정하며 각 SVG의 실제 배치 크기를 표시합니다. [SVG 위젯 배치](svg-widget-layout.ko.md)를 참조하세요.


이번 체크포인트의 Windows/.NET11 검증 빌드는 다음처럼 실행합니다. 전체 이미지 검사는 현재 종료 코드 1과 composition.json·failures.txt를 남깁니다. 이는 진단 성공을 뜻하는 통과 코드가 아니라 해결되지 않은 수용 검사 실패입니다.

```powershell
& 'W:/Cache/Duxel/svg-rounded-stroke-20261116/publish-466/Duxel.Graphics.Tests.exe' 'W:/Cache/Duxel/svg-feature-composition-check' svg-feature-composition-only
```
