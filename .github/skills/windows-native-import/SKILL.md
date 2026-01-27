---
name: "Windows Native Import"
description: "Windows에서 제공하는 네이티브 기능(Win32/Direct2D/DirectWrite/Direct3D/DXGI/IMM/COM/PInvoke) Import 지침 (NativeAOT/최신 C# 포함)"
version: "1.0"
owner: "team"
---

# 목적
- Windows 네이티브 기능을 **공식 명세에 맞게 Import**하여 버그와 비정상 동작을 방지한다.
- NativeAOT 친화적인 최신 C# 문법(`LibraryImport` 등)을 표준으로 사용한다.

# 적용 시점(트리거)
- Win32/Direct2D/DirectWrite/Direct3D/DXGI/IMM/COM/PInvoke를 **Import**하거나 시그니처/마샬링을 설계·변경하는 작업
- 메시지 루프/윈도우 프로시저/입력(포인터/키보드/IME) 처리를 구현하는 작업
- NativeAOT 배포/트리밍 대응을 전제로 네이티브 호출 경로를 수정하는 작업

# 올바른 명세 강조 (필수)
아래 항목은 **Microsoft Learn 명세**에 근거한다. 임의 추측/관행 기반 구현은 금지한다.

## 1) 메시지 루프 (정확한 규칙)
- `GetMessage`는 **성공 시 > 0**, **WM_QUIT 시 0**, **오류 시 -1**을 반환한다. 오류(-1) 처리를 반드시 포함한다.
- 키 입력 처리를 위해 메시지 루프에 **TranslateMessage**를 포함해야 `WM_CHAR`가 생성된다.
- 루프는 `TranslateMessage` → `DispatchMessage` 순서를 따른다.

## 2) Window Procedure (정확한 규칙)
- 처리하지 않는 메시지는 **DefWindowProc**로 반드시 전달한다.
- 종료 경로는 일반적으로 **WM_DESTROY에서 PostQuitMessage**를 호출하여 루프가 종료되도록 한다.

## 3) 메시지 큐/전달 특성 (다양한 케이스)
- **Queued** 메시지(입력/타이머/페인트/WM_QUIT)와 **Nonqueued** 메시지는 전달 경로가 다르다.
- `PeekMessage` 루프를 사용할 경우에도 **WM_QUIT 처리**와 **DispatchMessage** 경로는 유지한다.

# NativeAOT 친화 P/Invoke (최신 C# 표준)
- .NET 7+에서는 **`LibraryImport` 소스 생성기**를 우선 사용한다.
- `DllImport`는 런타임 IL 스텁이 필요하므로 **NativeAOT 기본 선택지가 아니다**.
- `LibraryImport` 사용 시 `StringMarshalling = Utf16` 등 **명시적 마샬링**을 지정한다.
- 호출 규약은 `UnmanagedCallConv`로 명시한다.

# Windows 타입/마샬링 정확성
- `HWND/HINSTANCE/LPARAM/WPARAM/LRESULT`는 포인터 크기이므로 `nint/nuint`를 사용한다.
- 구조체는 `StructLayout(LayoutKind.Sequential)`로 **명시적 레이아웃**을 유지한다.
- 오류 코드는 API 명세에 따라 `SetLastError`와 `Marshal.GetLastPInvokeError()`로 즉시 확인한다.

# 금지/회피 패턴 (명세 위반)
- `GetMessage` 반환값을 무시한 무한 루프(오류(-1) 미처리)
- `TranslateMessage` 없이 키 입력을 처리한다고 가정
- `DefWindowProc` 생략
- NativeAOT 환경에서 `DllImport`를 기본으로 사용
- 포인터 크기 타입을 `int`로 단정

# 진단 체크리스트
- 메시지 루프가 `GetMessage`의 -1/0/>0 규칙을 정확히 처리하는가?
- `TranslateMessage`를 호출해 `WM_CHAR` 경로가 유지되는가?
- 처리하지 않는 메시지는 `DefWindowProc`로 전달되는가?
- NativeAOT 빌드에서 `LibraryImport` 기반 P/Invoke가 사용되는가?

# 참고 문서(공식)
- About Messages and Message Queues: https://learn.microsoft.com/windows/win32/winmsg/about-messages-and-message-queues
- Window Messages (message loop): https://learn.microsoft.com/windows/win32/learnwin32/window-messages
- P/Invoke source generation (LibraryImport): https://learn.microsoft.com/dotnet/standard/native-interop/pinvoke-source-generation
- Native interop best practices: https://learn.microsoft.com/dotnet/standard/native-interop/best-practices

# 시각적 활성 표시
- 스킬이 실제로 적용되는 응답에는 눈에 띄는 표시를 포함한다.
- 예: "🟢 Skill Active: Windows Native Import" 같은 라벨을 응답 상단에 표기
