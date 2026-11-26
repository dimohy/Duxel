#:property TargetFramework=net11.0
#:property PublishAot=false
#:property SelfContained=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:property DefineConstants=$(DefineConstants);DUXEL_$(DuxelHost)
#:project ../../src/Duxel.Core/Duxel.Core.csproj
#:project ../../src/Duxel.App/Duxel.App.csproj
#:project ../../src/Duxel.$(DuxelHost).App/Duxel.$(DuxelHost).App.csproj

using System.Diagnostics;
using Duxel.App;
using Duxel.Core;
#if DUXEL_Windows
using Duxel.Windows.App;
#elif DUXEL_Linux
using Duxel.Linux.App;
#endif

const int MaxCharacters = 131072;
if (args.Length > 1)
    throw new ArgumentException("Supply at most one text file to open.");

var document = new UiState<string>(string.Empty);
var file = new UiState<string?>(null);
var status = new UiState<string>("Ready");
var showCount = new UiState<bool>(true);
var savedText = string.Empty;
var filePending = (Task<IReadOnlyList<string>>?)null;
var fileKind = UiFileDialogKind.OpenFile;
var clipboardCommand = (string?)null;
var session = DuxelApp.PrimarySession;
if (args.Length == 1)
    ReadDocument(args[0]);

var menu = Fluent.MenuBar("Notepad menu",
    new("File",
        [new("new", "New") { SecondaryContent = "Ctrl+N" },
         new("open", "Open…") { SecondaryContent = "Ctrl+O" },
         UiMenuOption.Separator("file-actions"),
         new("save", "Save") { SecondaryContent = "Ctrl+S" },
         new("save-as", "Save as…") { SecondaryContent = "Ctrl+Shift+S" },
         UiMenuOption.Separator("window-actions"),
         new("exit", "Exit")], FileCommand, UiKey.F),
    new("Edit",
        [new("copy", "Copy document"), new("paste", "Paste at end"),
         UiMenuOption.Separator("document-actions"),
         UiMenuOption.Submenu("transform", "Change case",
             [new("upper", "Uppercase"), new("lower", "Lowercase")]),
         new("revert", "Revert to saved"), new("clear", "Clear document")], EditCommand, UiKey.E),
    new("View", [UiMenuOption.Checkbox("count", "Character count", showCount.Binding)], _ => { }, UiKey.V));

var documentHeader = Dux.Row(new UiFlexOptions(Spacing: 16, CrossAxisAlignment: UiCrossAxisAlignment.Center),
    Dux.Expanded(Dux.Column(new(Spacing: 2),
        Fluent.Text(Text.Title2, () => file.Value is { } path ? Path.GetFileName(path) : "Untitled"),
        Fluent.Text(Text.Caption1, () => file.Value is { } path
            ? Path.GetFileName(Path.GetDirectoryName(path)) ?? "Plain text document"
            : "Plain text document"))),
    new UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance>>(
        "Save", () => FileCommand("save")));

var statusBar = Dux.Column(new(Spacing: 8, CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    Fluent.Divider(),
    Dux.Row(new UiFlexOptions(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Center),
        Dux.Expanded(Fluent.Text(Text.Caption1, () => status.Value)),
        Fluent.Text(Text.Caption1, () => showCount.Value ? $"{document.Value.Length:N0} characters" : string.Empty),
        Fluent.Text(Text.Caption1Strong, () => IsModified() ? "Modified" : file.Value is null ? "Untitled" : "Saved")));

var workspace = Dux.Column(new(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    documentHeader,
    Dux.Expanded(Fluent.Textarea("Document", document.Binding, MaxCharacters, 0, 0)),
    statusBar);
var window = Dux.Column(new(CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    Dux.Padding(menu, new UiEdgeInsets(12, 0, 12, 0)), Fluent.Divider(),
    Dux.Expanded(Dux.Padding(workspace, new UiEdgeInsets(20, 16, 20, 12))));

Run(new UiView(ui =>
{
    ReadFileDialogResult();
    ui.EnableRootViewportContentLayout(contentPadding: 0);
    if (ui.Shortcut(UiKey.N, KeyModifiers.Ctrl))
        FileCommand("new");
    if (ui.Shortcut(UiKey.O, KeyModifiers.Ctrl))
        FileCommand("open");
    if (ui.Shortcut(UiKey.S, KeyModifiers.Ctrl | KeyModifiers.Shift))
        FileCommand("save-as");
    else if (ui.Shortcut(UiKey.S, KeyModifiers.Ctrl))
        FileCommand("save");
    window.Render(ui);
    ApplyClipboard(ui);
}), "Duxel Notepad", 820, 600);

bool IsModified() => !string.Equals(document.Value, savedText, StringComparison.Ordinal);

bool CanReplace()
{
    if (!IsModified())
        return true;
    status.Value = "Save your changes or choose Edit → Revert to saved first.";
    return false;
}

void FileCommand(string command)
{
    switch (command)
    {
        case "new" when CanReplace():
            file.Value = null;
            document.Value = savedText = string.Empty;
            status.Value = "New document";
            break;
        case "open" when CanReplace():
            ChooseFile(UiFileDialogKind.OpenFile);
            break;
        case "save" when file.Value is { } path:
            WriteDocument(path);
            break;
        case "save":
        case "save-as":
            ChooseFile(UiFileDialogKind.SaveFile);
            break;
        case "exit" when CanReplace():
            session.Exit();
            break;
    }
}

void EditCommand(string command)
{
    switch (command)
    {
        case "copy":
        case "paste":
            clipboardCommand = command;
            break;
        case "upper":
            document.Value = document.Value.ToUpperInvariant();
            status.Value = "Converted to uppercase";
            break;
        case "lower":
            document.Value = document.Value.ToLowerInvariant();
            status.Value = "Converted to lowercase";
            break;
        case "revert":
            document.Value = savedText;
            status.Value = "Restored the last saved text";
            break;
        case "clear":
            document.Value = string.Empty;
            status.Value = "Document cleared";
            break;
    }
}

void ApplyClipboard(UiImmediateContext ui)
{
    if (clipboardCommand == "copy")
    {
        ui.SetClipboardText(document.Value);
        status.Value = "Document copied";
    }
    else if (clipboardCommand == "paste")
    {
        var text = ui.GetClipboardText().ReplaceLineEndings("\n");
        if (text.Length <= MaxCharacters - document.Value.Length)
        {
            document.Value += text;
            status.Value = "Text pasted at the end";
        }
        else
            status.Value = $"The document can contain at most {MaxCharacters:N0} characters.";
    }
    clipboardCommand = null;
}

void ChooseFile(UiFileDialogKind kind)
{
    if (filePending is not null)
        return;

    fileKind = kind;
    var save = kind == UiFileDialogKind.SaveFile;
    filePending = session.ShowFileDialogAsync(new(kind, save ? "Save text document" : "Open text document",
        initialDirectory: file.Value is { } path ? Path.GetDirectoryName(path) : null,
        suggestedFileName: save ? Path.GetFileName(file.Value) ?? "Untitled.txt" : null,
        defaultExtension: save ? "txt" : null,
        filters: [new("Text files", "*.txt"), new("All files", "*")]));
}

void ReadFileDialogResult()
{
    if (filePending is not { IsCompleted: true } pending)
        return;
    filePending = null;
    try
    {
        var paths = pending.GetAwaiter().GetResult();
        if (paths.Count == 0)
            return;
        if (fileKind == UiFileDialogKind.SaveFile)
            WriteDocument(paths[0]);
        else if (CanReplace())
            ReadDocument(paths[0]);
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
    {
        status.Value = $"File dialog unavailable: {error.Message}";
    }
}

void ReadDocument(string path)
{
    try
    {
        var fullPath = Path.GetFullPath(path);
        using var reader = File.OpenText(fullPath);
        var buffer = new char[MaxCharacters + 1];
        var read = reader.ReadBlock(buffer);
        if (read > MaxCharacters)
        {
            status.Value = $"Choose a document with at most {MaxCharacters:N0} characters.";
            return;
        }
        document.Value = savedText = new string(buffer, 0, read).ReplaceLineEndings("\n");
        file.Value = fullPath;
        status.Value = $"Opened {Path.GetFileName(fullPath)}";
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
    {
        status.Value = $"Could not open the document: {error.Message}";
    }
}

void WriteDocument(string path)
{
    try
    {
        var fullPath = Path.GetFullPath(path);
        File.WriteAllText(fullPath, document.Value);
        savedText = document.Value;
        file.Value = fullPath;
        status.Value = $"Saved {Path.GetFileName(fullPath)}";
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
    {
        status.Value = $"Could not save the document: {error.Message}";
    }
}

static void Run(IUiView root, string title, int width, int height, Func<bool>? animationActive = null)
{
    var options = DuxelApp.Options(Dux.App(root), title: title, width: width, height: height);
    options = options with
    {
        Debug = new() { LogStartupTimings = true },
        Frame = options.Frame with
        {
            UseEventDrivenPlatformLoop = true,
            EnableIdleFrameSkip = true,
            IsAnimationActiveProvider = animationActive
        }
    };
#if DUXEL_Windows
    if (!OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("This build requires Windows.");
    DuxelWindowsApp.Run(options);
#elif DUXEL_Linux
    if (!OperatingSystem.IsLinux())
        throw new PlatformNotSupportedException("This build requires Linux.");
    using var match = Process.Start(new ProcessStartInfo("fc-match", ["-f", "%{file}", "sans-serif"])
    {
        RedirectStandardOutput = true,
        UseShellExecute = false
    }) ?? throw new InvalidOperationException("Could not find a desktop font.");
    var font = match.StandardOutput.ReadToEnd().Trim();
    match.WaitForExit();
    if (match.ExitCode != 0 || !File.Exists(font))
        throw new InvalidOperationException("Install Fontconfig and a sans-serif desktop font.");

    DuxelLinuxApp.Run(options with
    {
        Design = ReadDesktopDesign(),
        Font = new() { PrimaryFontPath = font, SecondaryFontPath = null, Typography = new(font, font, font) }
    });
#else
#error This example requires the Windows or Linux Duxel host.
#endif
}

#if DUXEL_Linux
static UiCompiledDesign ReadDesktopDesign()
{
    var request = new ProcessStartInfo("gdbus",
    [
        "call", "--session", "--timeout", "1",
        "--dest", "org.freedesktop.portal.Desktop",
        "--object-path", "/org/freedesktop/portal/desktop",
        "--method", "org.freedesktop.portal.Settings.Read",
        "org.freedesktop.appearance", "color-scheme"
    ])
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    try
    {
        using var portal = Process.Start(request)!;
        var errors = portal.StandardError.ReadToEndAsync();
        var setting = portal.StandardOutput.ReadToEnd();
        portal.WaitForExit();
        errors.GetAwaiter().GetResult();
        return portal.ExitCode == 0 && setting.Contains("<uint32 1>", StringComparison.Ordinal)
            ? UiCompiledDesign.Windows11Dark
            : UiCompiledDesign.Windows11;
    }
    catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 2)
    {
        // The Settings portal is optional in a minimal X11 session.
        return UiCompiledDesign.Windows11;
    }
}
#endif
