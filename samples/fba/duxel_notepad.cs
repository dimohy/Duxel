#:property TargetFramework=net11.0
#:property PublishAot=false
#:property SelfContained=false
#:property DuxelHost=$([System.String]::Copy('$(OS)').Replace('Windows_NT','Windows').Replace('Unix','Linux'))
#:property DefineConstants=$(DefineConstants);DUXEL_$(DuxelHost)
#:package Duxel.$(DuxelHost).App@0.4.0

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
    throw new ArgumentException("Supply at most one note file path.");

var defaultPath = Path.GetFullPath("duxel-note.txt");
var draftPath = new UiState<string>(args.Length == 0 ? defaultPath : Path.GetFullPath(args[0]));
var activePath = new UiState<string>(draftPath.Value);
var note = new UiState<string>("");
var status = new UiState<string>("New document");
var hasFile = new UiState<bool>(false);
var saved = "";
if (File.Exists(activePath.Value))
    Open();

var menuOptions = new UiMenuOptions(MinimumWidth: 144, ItemHeight: 28, Padding: 4, ItemSpacing: 1);
var menuBar = Dux.Padding(Dux.Row(new UiFlexOptions(Spacing: 4, MainAxisSize: UiMainAxisSize.Min),
    Dux.SizedBox(Fluent.Menu("File",
        [new("new", "New"), new("open", "Open"), new("save", "Save"), new("exit", "Exit")],
        FileCommand, menuOptions), width: 52, height: 28),
    Dux.SizedBox(Fluent.Menu("Edit",
        [new("revert", "Revert to saved"), new("clear", "Clear document")],
        EditCommand, menuOptions), width: 52, height: 28)),
    new UiEdgeInsets(8, 0, 8, 0));
var editor = Dux.Column(new UiFlexOptions(Spacing: 8,
    CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    Fluent.Input("File path", draftPath.Binding, 4096),
    Dux.Expanded(Fluent.Textarea("Document", note.Binding, MaxCharacters, 0, 0)),
    Fluent.Divider(),
    Dux.Row(new(Spacing: 12, CrossAxisAlignment: UiCrossAxisAlignment.Center),
        Dux.Expanded(Fluent.Text(Text.Caption1, () => status.Value)),
        Fluent.Text(Text.Caption1, () => $"{note.Value.Length:N0} characters"),
        Fluent.Text(Text.Caption1Strong, () => saved != note.Value ? "Modified" : hasFile.Value ? "Saved" : "New")));
var window = Dux.Column(new UiFlexOptions(CrossAxisAlignment: UiCrossAxisAlignment.Stretch),
    menuBar, Dux.Expanded(Dux.Padding(editor, new UiEdgeInsets(10, 6, 10, 8))));
Run(new UiView(ui =>
{
    ui.EnableRootViewportContentLayout(contentPadding: 0);
    window.Render(ui);
}), "Duxel Notepad", 700, 540);

void FileCommand(string command)
{
    switch (command)
    {
        case "new": New(); break;
        case "open": Open(); break;
        case "save": Save(); break;
        case "exit": if (CanReplace()) DuxelApp.Exit(); break;
    }
}

void EditCommand(string command)
{
    if (command == "revert")
        Revert();
    else if (command == "clear")
    {
        note.Value = "";
        status.Value = "Document cleared.";
    }
}

void New()
{
    if (!CanReplace())
        return;
    draftPath.Value = defaultPath;
    activePath.Value = defaultPath;
    note.Value = saved = "";
    hasFile.Value = false;
    status.Value = "New document. Choose a file path, then save.";
}

void Open()
{
    if (!CanReplace())
        return;
    try
    {
        var path = DocumentPath();
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        if (text.Length > MaxCharacters)
        {
            status.Value = $"Choose a document with at most {MaxCharacters:N0} characters.";
            return;
        }

        note.Value = saved = text;
        activePath.Value = draftPath.Value = path;
        hasFile.Value = true;
        status.Value = $"Opened {Path.GetFileName(path)}";
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
    {
        status.Value = $"Could not open the document: {error.Message}";
    }
}

void Save()
{
    try
    {
        var path = DocumentPath();
        File.WriteAllText(path, note.Value);
        saved = note.Value;
        activePath.Value = draftPath.Value = path;
        hasFile.Value = true;
        status.Value = $"Saved {Path.GetFileName(path)}";
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
    {
        status.Value = $"Could not save the document: {error.Message}";
    }
}

void Revert()
{
    note.Value = saved;
    draftPath.Value = activePath.Value;
    status.Value = "Restored the last opened or saved text.";
}

bool CanReplace()
{
    if (saved == note.Value)
        return true;
    status.Value = "Save or revert your edits before switching documents.";
    return false;
}

string DocumentPath() => Path.GetFullPath(draftPath.Value.Trim());

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
