# 시스템 글자 배율

[검증 기록](../artifacts/fluent-messagebar-2026-11-15/system-typography-verification.json)은 호스트 검사와 실제 네이티브 실행을 구분합니다. 글자 크기100%·114%에서 소수점 위치를 바꾼192개 GPU 캡처에서 미정렬 대조군8/12의 커버리지가 달라졌고, 픽셀 정렬12/12는 모양을 유지했습니다. 별도36개 비교는 독립 네이티브 RGB 마스크를 CPU에서 어두운·밝은·유색 배경과 합성한 뒤 GPU 결과와 비교해 8비트 채널 한 단계 이내로 일치했습니다. 이는 원시 커버리지 합성 검사이며 네이티브 GDI·Direct2D 감마 정책과의 동등성은 별도입니다.

`IPlatformBackend.ContentScale`은 화면 DPI, `TextScale`은 접근성·데스크톱 글꼴 크기를 담당합니다. 사용자 정의 백엔드의 기본 글자 배율은 1입니다. 앱은 `TextScale`을 `UiTextSettings.Scale`로 전달하며 Fluent의 자연 측정 크기도 글자와 함께 커집니다. 배율이 바뀌면 보관한 배치 측정과 레이어를 무효화합니다. 명시적인 고정 크기 제약은 앱에서 관리합니다.

Windows는 별도 관리 패키지 없이 네이티브 WinRT의 `UISettings.TextScaleFactor`를 읽습니다. 변경 이벤트는 창 소유 스레드를 깨우며 렌더 스레드는 저장된 값만 읽습니다. 기존 모니터별 DPI 처리는 별도입니다. 글자 배율은 타이포그래피 전체에 균일하게 적용하며, WinUI의 비선형 제목 배율이나 앱 글꼴 패밀리 교체를 구현한 것은 아닙니다.

Linux X11은 해당 화면의 XSETTINGS 관리자에서 `Gdk/WindowScalingFactor`, `Gdk/UnscaledDPI`, `Xft/DPI`, `Gtk/FontName`을 읽습니다. 포인트·절대 픽셀 크기가 포함된 글꼴 설명은 Pango로 해석합니다. 데스크톱 본문 크기는 Fluent Body1의 14 DIP를 기준으로 적용하고 나머지 타이포그래피의 크기 비율을 유지합니다. XSETTINGS에 DPI가 없으면 X resource의 `Xft.dpi`, 서버에 설정 자체가 없으면 96 DPI를 사용합니다. GTK의 DPI -1은 미지정입니다. Windows 호스트 설정을 읽거나 매 프레임 설정 명령을 실행하지 않습니다.

관리자 탐색·구독·속성 읽기는 짧은 X server grab 안에서 수행합니다. 속성 변경과 관리자 교체·제거 이벤트에서 완전한 설정 스냅샷을 갱신하며, 이벤트 대기 중 주기적인 설정 폴링은 하지 않습니다. AT-SPI의 컨트롤·이미지·텍스트 좌표도 논리 배치와 실제 데스크톱 픽셀 사이에서 변환합니다.

래스터 텍스트는 완성된 비트맵 전체를 물리 픽셀 격자에 배치해 `PixelSnap`을 적용합니다. 네이티브 글리프 폭과 글리프 사이의 상대 위치는 보존합니다. 일반 텍스트·Fluent 타이포그래피·스타일 문단 런이 같은 규칙을 사용해 소수 픽셀 스크롤에 따른 재샘플링·커버리지 변화를 줄입니다. `PixelSnap = false`이면 소수점 배치를 유지하며, 벡터 텍스트는 연속적인 변환을 유지합니다.

기존 로컬 소스 기반 그래픽·Linux 테스트 빌드에서 다음 집중 검증을 실행합니다.

```powershell
./Duxel.Graphics.Tests.exe <새-결과-경로> system-typography-only
./Duxel.Graphics.Tests.exe <새-결과-경로> raster-text-scroll 2
./Duxel.Graphics.Tests.exe <새-결과-경로> text-background-gpu
dotnet ./Duxel.Linux.App.Tests.dll desktop-typography <새-결과-경로>
```

Linux 파서·배율 검사는 Linux 네이티브 라이브러리 없이 실행할 수 있습니다. 실제 데스크톱의 X11·Pango 알림 동작까지 증명하지는 않습니다. 실제 OS 설정 변경, Linux 실행 수용, NativeAOT 적합성은 관리 호스트 검사와 구분합니다.

출처: [Windows 텍스트 배율](https://learn.microsoft.com/windows/apps/develop/input/text-scaling), [XSETTINGS](https://specifications.freedesktop.org/xsettings/latest/), [GTK 설정 이름](https://github.com/GNOME/gtk/blob/gtk-3-24/gdk/x11/gdksettings.c), [네이티브 WinRT ABI](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/winrt/windows.ui.viewmanagement.h).
