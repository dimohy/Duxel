# 파일 및 폴더 대화 상자

갤러리에서 **통합·개발 도구 → 시스템 연동 → 파일 대화 상자**를 엽니다.

버튼을 누르는 등 앱에서 입력을 받을 때 실행 중인 `DuxelAppSession`으로 파일이나 폴더를 선택할 수 있습니다. 사용자가 취소하면 빈 목록을 반환합니다. 파일 열기와 폴더 선택은 여러 항목을 고를 수 있고, 파일 저장에서는 파일 이름과 기본 확장자를 제안할 수 있습니다.

```csharp
var paths = await session.ShowFileDialogAsync(new(
    UiFileDialogKind.OpenFile,
    title: "이미지 선택",
    allowMultiple: true,
    filters: [new("이미지", "*.png", "*.jpg"), new("모든 파일", "*")]));
```

대화 상자는 파일 시스템 경로를 반환합니다. 반환된 경로의 파일을 읽거나 쓰는 코드는 앱에서 작성합니다. 처음 보여줄 폴더의 절대 경로와 파일 이름 필터를 전달할 수 있습니다. 세션에서는 한 번에 대화 상자 하나를 엽니다.

Windows에서는 시스템 대화 상자가 전용 STA 스레드에서 열리는 동안 갤러리 창이 계속 입력을 처리합니다. Linux에서는 데스크톱의 XDG FileChooser를 먼저 사용합니다. FileChooser를 사용할 수 없는 환경에서는 추가 패키지 없이 Duxel의 Fluent 대화 상자가 열립니다. 이 대화 상자에서는 실행 중인 앱이 접근할 수 있는 파일을 고를 수 있습니다. [Windows Common Item Dialog](https://learn.microsoft.com/windows/win32/shell/common-file-dialog) [XDG FileChooser](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.FileChooser.html)
