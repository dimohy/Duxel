# File and folder dialogs

In Gallery, open **System integration → Files and folders → File dialogs**.

Use the running `DuxelAppSession` to pick files or folders from an input callback. An empty result means the user canceled. Open and folder dialogs support multiple selections. Save returns a destination path and can suggest a file name and default extension.

```csharp
var paths = await session.ShowFileDialogAsync(new(
    UiFileDialogKind.OpenFile,
    title: "Choose images",
    allowMultiple: true,
    filters: [new("Images", "*.png", "*.jpg"), new("All files", "*")]));
```

The dialog returns file-system paths. The app opens or writes the selected file using its own I/O code. Callers can provide an absolute starting directory and named glob filters. A running session accepts one dialog request at a time.

Windows uses the Common Item Dialog and keeps its event loop active while the STA presents the system window. Linux prefers the desktop's XDG FileChooser. When the portal is unavailable, Duxel opens its built-in Fluent chooser without requiring another package. The built-in chooser accesses the files already visible to the process. [Windows Common Item Dialog](https://learn.microsoft.com/windows/win32/shell/common-file-dialog) [XDG FileChooser](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.FileChooser.html)
