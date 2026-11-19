# 가로 투명 마스크와 캐시 카메라

`UiHorizontalOpacityMask`는 출력 영역의 왼쪽·오른쪽을 선형으로 투명하게 만듭니다. 배경색으로 덮지 않으며, 기존 검정·흰색 기준 합성의 RGB별 글자 커버리지를 유지합니다.

```csharp
// Keep the group alive across frames. Change contentVersion when content or fonts change.
ui.PaintGroup(group, sourceBounds, contentVersion, PaintContent,
    new UiImageUvTransform(new(.25f, .25f), new(.5f, 0), new(0, .5f)),
    new UiHorizontalOpacityMask(.25f, .25f),
    destination : viewport, textMode : UiPaintGroupTextMode.Projected);
```

- LeftFadeFraction/RightFadeFraction은 출력 너비에 대한 비율입니다. 각각0이면 그쪽은 감추지 않습니다. 두 값은 유한한0 이상이며 합은1 이하입니다. 기본값은 마스크가 없는 상태입니다.
- `PaintGroup`, `BackdropImage`, `BackdropComposite`에 마스크를 전달할 수 있습니다. UV가 필요한 오버로드는 기존 `UiImageUvTransform`을 받습니다. 원래 공개 시그니처는 유지합니다.
- 마스크는 destination에 붙고 UV는 캐시 원본을 읽는 위치를 정합니다. 마스크·UV·최종 불투명도만 바꾸면 원본 캡처를 다시 만들지 않습니다. 내용·글꼴·캡처 크기·DPI·textMode가 바뀌는 경우에는 해당 캐시 계약을 따릅니다.
- `Projected`는 확대·투영하는 예제에서 명시적으로 사용합니다. 기존 Native 모드의 독립 RGB 커버리지도 지원합니다. 이 카메라는 캡처한 이미지의 UV를 바꾸므로 확대 품질은 캡처 해상도에 따릅니다. 필요한 경우 rasterScale을 지정합니다.
- 삼각형의 정점 알파 보간을 재사용합니다. 양끝 마스크는 최대12정점·18인덱스이며 셰이더·GPU 기능·지원 버전을 추가하지 않습니다. CPU에서 이 작은 합성 기하를 준비하고, 픽셀별 합성은 GPU가 수행합니다. CPU로 마스크 이미지를 만들거나 매 프레임 텍스트를 다시 캡처하지 않습니다.
- 기존 원형 잘라내기와 최종 불투명도를 함께 사용할 수 있습니다. 원래 API와 기본 마스크의 명령·기하는 정확히 같습니다. 일반 이미지의 색상·합성 상태도 다음 그리기로 새지 않습니다.

실행: `.\run-gallery.ps1` → **통합·개발 도구 → 개발 도구 → 렌더링**에서 맨 아래 **양끝 투명 마스크와 캐시 카메라**를 확인하세요. 양끝·왼쪽·오른쪽·없음, 배경 전환과 카메라 확대를 조작할 수 있습니다. 실제 소스가 바로 아래에 있고, 내용 준비 횟수는 마스크·배경·카메라만 바꿀 때 증가하지 않습니다.

검증은 [실행 기록](../artifacts/horizontal-opacity-mask-2026-11-18/result.json)에 있습니다. 서로 다른 배경·양쪽/한쪽/없는 마스크·불투명도·MSAA1/4를 독립 합성식과 비교했습니다. RGB 커버리지가 있지만 alpha가0인 글자 기준도 포함합니다. 겹친 도형에 불투명도가 한 번 적용되는지, 마스크/UV만 바꿀 때 캡처가 유지되는지와 실제 갤러리 소스도 확인했습니다.

실제 Linux·고정 RC3 NativeAOT·3D 변환 전체 적합성은 이번에 실행하지 않았습니다. SegmentedControl의 스크롤 연결과 전체 프로젝트 목표는 계속 진행 중입니다.
