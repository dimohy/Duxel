using Duxel.App;
using Duxel.Core;
using Duxel.Core.Dsl;

var logPath = Path.Combine(AppContext.BaseDirectory, "theme-demo.log");
try
{
    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Creating UiDslScreen...\n");

    var values = new UiDslValueBinder()
        .BindString("status", "Ready")
        .BindInt("theme", 0)
        .BindString("cf_mode", "")
        .BindBool("cf_mode.A", false)
        .BindBool("cf_mode.B", false)
        .BindBool("cf_mode.C", false);

    var clickCount = 0;
    UiDslScreen? screen = null;

    void SetStatus(string message)
    {
        values.SetString("status", message);
        File.AppendAllText(logPath, $"[Event] {message}\n");
    }

    UiTheme[] themePresets =
    [
        UiTheme.ImGuiDark, UiTheme.ImGuiLight, UiTheme.ImGuiClassic,
        UiTheme.Nord, UiTheme.SolarizedDark, UiTheme.SolarizedLight,
        UiTheme.Dracula, UiTheme.Monokai, UiTheme.CatppuccinMocha,
        UiTheme.GitHubDark,
    ];
    string[] themeNames =
    [
        "Dark", "Light", "Classic", "Nord", "Solarized Dark", "Solarized Light",
        "Dracula", "Monokai", "Catppuccin Mocha", "GitHub Dark",
    ];

    void ApplyTheme(string name, UiTheme theme)
    {
        screen!.RequestTheme(theme);
        SetStatus($"Theme: {name}");
    }

    void UpdateCfMode()
    {
        string mode = values.GetBool("cf_mode.C") ? "C"
                    : values.GetBool("cf_mode.B") ? "B"
                    : values.GetBool("cf_mode.A") ? "A"
                    : "";
        values.SetString("cf_mode", mode);
        SetStatus(mode.Length > 0 ? $"Mode: {mode}" : "No mode");
    }

    var events = new UiDslEventBinder()
        .Bind("primary", () => SetStatus($"Primary clicked ({++clickCount})"))
        .Bind("secondary", () => SetStatus("Small button clicked"))
        .Bind("arrow_r", () => SetStatus("Arrow right pressed"))
        .Bind("hover_me", () => SetStatus("Hover button clicked!"))
        .Bind("file.new", () => SetStatus("File → New"))
        .Bind("file.open", () => SetStatus("File → Open"))
        .Bind("file.save", () => SetStatus("File → Save"))
        .Bind("edit.undo", () => SetStatus("Edit → Undo"))
        .Bind("edit.redo", () => SetStatus("Edit → Redo"))
        .Bind("view.theme", () => SetStatus("View → Theme Info"))
        .Bind("style.neon", () => SetStatus("Style: Neon"))
        .Bind("style.vapor", () => SetStatus("Style: Vapor"))
        .Bind("style.retro", () => SetStatus("Style: Retro"))
        .Bind("style.synth", () => SetStatus("Style: Synth"))
        // Theme preset: single handler using int index from Combo
        .Bind("theme", () =>
        {
            var idx = values.GetInt("theme");
            if (idx >= 0 && idx < themePresets.Length)
                ApplyTheme(themeNames[idx], themePresets[idx]);
        })
        // Control flow demo: Switch binding — recompute mode from selectable states
        .BindCheckbox("cf_mode.A", _ => UpdateCfMode())
        .BindCheckbox("cf_mode.B", _ => UpdateCfMode())
        .BindCheckbox("cf_mode.C", _ => UpdateCfMode())
        .BindCheckbox("neon", v => SetStatus($"Neon Glow: {(v ? "ON" : "OFF")}"))
        .BindCheckbox("wireframe", v => SetStatus($"Wireframe: {(v ? "ON" : "OFF")}"))
        .OnAnyButton(id => SetStatus($"Button pressed: {id}"))
        .OnAnyCheckbox((id, v) => SetStatus($"Checkbox {id}: {v}"));

    screen = new UiDslScreen("Ui/Main.ui", "Ui/cyberpunk.duxel-theme",
        eventSink: events, valueSource: values);
    screen.Trace = msg => File.AppendAllText(logPath, $"[ThemeTrace] {msg}\n");
    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] UiDslScreen created. Starting app...\n");

    DuxelApp.Run(new DuxelAppOptions
    {
        Window = new DuxelWindowOptions
        {
            Title = "Duxel Theme Hot-Reload Demo",
            Width = 960,
            Height = 640,
            VSync = true
        },
        Debug = new DuxelDebugOptions
        {
            Log = msg => File.AppendAllText(logPath, $"[DuxelTrace] {msg}\n"),
            LogEveryNFrames = 100,
            LogStartupTimings = true
        },
        Screen = screen
    });
    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] App exited normally.\n");
}
catch (Exception ex)
{
    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] FATAL: {ex}\n");
}
