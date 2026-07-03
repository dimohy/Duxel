// FBA: Declarative UI dashboard using reusable Dux components.
#:property TargetFramework=net10.0
#:property OutputType=WinExe
#:property OptimizationPreference=Size
#:property InvariantGlobalization=true
#:property DebuggerSupport=false
#:property EventSourceSupport=false
#:property MetricsSupport=false
#:property MetadataUpdaterSupport=false
#:property StackTraceSupport=false
#:property UseSystemResourceKeys=true
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;
using System.Collections.Generic;

DuxelWindowsApp.Run<DashboardDesign>(
    Dux.App(new DashboardScreen(new DashboardState())),
    title: "Duxel Declarative Dashboard",
    width: 1200,
    height: 720);

public readonly struct DashboardDesign : IUiDesign
{
    public static UiCompiledDesign Create()
        => UiCompiledDesign.Windows11Dark with
        {
            Tokens = UiDesignTokens.Windows11 with
            {
                WindowCornerRadius = 8f,
                ControlCornerRadius = 6f,
                InputCornerRadius = 6f,
                ProgressCornerRadius = 4f
            }
        };
}

sealed class DashboardScreen(DashboardState state) : UiComponent
{
    private readonly DashboardState _state = state;

    protected override IUiView Build()
        => Dux.Group(
            Dux.AppShell(
                () => _state.ProjectName.Value,
                _state.SelectedSection,
                [
                    Dux.NavItem(DashboardSection.Overview, "Overview", new OverviewTab(_state), "Run controls"),
                    Dux.NavItem(DashboardSection.Tasks, "Tasks", new TasksTab(_state), () => $"{_state.Tasks.Count} active rows"),
                    Dux.NavItem(DashboardSection.Notes, "Notes", new NotesTab(_state), "Composition notes")
                ],
                new UiAppShellOptions(
                    WindowTitle: "Workspace",
                    SidebarTitle: "Workspace",
                    Position: new UiVector2(24f, 84f),
                    Size: new UiVector2(820f, 620f),
                    SidebarWidth: 176f,
                    Focus: true,
                    Menu: BuildMenu()),
                Dux.Meta(() => $"Runs {_state.Runs.Value}"),
                Dux.Meta(() => _state.SelectedChannel),
                Dux.Meta(() => $"Priority {_state.Priority.Value}", UiTextTone.Warning)),
            new DetailsWindow(_state));

    private IUiView BuildMenu()
        => Dux.MainMenuBar(
            Dux.Menu(
                "File",
                Dux.MenuItem("New run", () =>
                {
                    _state.Runs.Update(value => value + 1);
                    _state.Status.Value = "New run queued";
                }),
                Dux.MenuItem(
                    "Show details",
                    () => _state.DetailsOpen.Value = !_state.DetailsOpen.Value,
                    selected: () => _state.DetailsOpen.Value)),
            Dux.Menu(
                "View",
                Dux.MenuItem(
                    "Live preview",
                    () => _state.LivePreview.Value = !_state.LivePreview.Value,
                    selected: () => _state.LivePreview.Value)));
}

sealed class OverviewTab(DashboardState state) : UiComponent
{
    private readonly DashboardState _state = state;

    protected override IUiView Build()
        => Dux.VStack(
            10f,
            Dux.Section(
                "Run Controls",
                Dux.VStack(
                    8f,
                    Dux.Toolbar(
                        Dux.CommandBar(
                            Dux.Command(
                                "Queue",
                                QueueRun,
                                UiButtonRole.Primary,
                                tooltip: () => "Queue one declarative run"),
                            Dux.Command(
                                "Reset",
                                Reset,
                                tooltip: () => "Restore the sample state")),
                        Dux.Checkbox("Live preview", _state.LivePreview),
                        Dux.Checkbox("Details", _state.DetailsOpen)),
                    Dux.SettingsGroup(
                        new UiSettingsGroupOptions(LabelWidth: 106f),
                        Dux.Setting(
                            "Project",
                            Dux.TextField("project", _state.ProjectName, 128).ItemWidth(360f),
                            "Display name used by the workspace shell."),
                        Dux.Setting(
                            "Channel",
                            Dux.EnumSegmented<ReleaseChannel>(
                                "channel",
                                _state.Channel,
                                static channel => channel.ToDisplayName(),
                                new UiSegmentedOptions(ItemSize: new UiVector2(84f, 0f))),
                            "Choose the rollout lane for this run."),
                        Dux.Setting(
                            "Priority",
                            Dux.Slider("priority", _state.Priority, 1, 5).ItemWidth(360f),
                            "Controls callout severity and scheduling order."),
                        Dux.Setting(
                            "Progress",
                            Dux.Slider("progress", _state.Progress, 0f, 1f).ItemWidth(360f),
                            "Preview the current workflow completion.")))),
            Dux.Section(
                "Status",
                Dux.VStack(
                    8f,
                    Dux.ProgressBar(
                        () => _state.Progress.Value,
                        new UiVector2(360f, 22f),
                        () => $"{_state.Progress.Value:P0}"),
                    Dux.Callout(
                        "Run Status",
                        () => _state.Status.Value,
                        options: new UiCalloutOptions(Tone: UiTextTone.Success, Height: 88f))),
                new UiSectionOptions(Collapsible: false)));

    private void QueueRun()
    {
        _state.Runs.Update(value => value + 1);
        _state.Progress.Value = MathF.Min(1f, _state.Progress.Value + 0.08f);
        _state.Status.Value = "Queued from declarative button";
    }

    private void Reset()
    {
        _state.Progress.Set(0.35f);
        _state.Runs.Set(12);
        _state.Status.Set("Dashboard reset");
    }
}

sealed class TasksTab(DashboardState state) : UiComponent
{
    private readonly DashboardState _state = state;

    protected override IUiView Build()
        => Dux.VStack(
            10f,
            Dux.Section(
                "Live Stream",
                Dux.VStack(
                    8f,
                    Dux.List(
                        "TaskList",
                        _state.Tasks,
                        task => task.Id,
                        task => Dux.StatusRow(
                            task.Name,
                            task.Owner,
                            () => task.Progress,
                            Dux.Bind(() => task.Selected, value => task.Selected = value),
                            new UiStatusRowOptions(Tooltip: () => task.Selected ? "Selected task" : "Select task")),
                        new UiVector2(0f, 205f),
                        border: true)
                        .VisibleIf(() => _state.LivePreview.Value),
                    Dux.Unless(
                        () => _state.LivePreview.Value,
                        Dux.EmptyState(
                            "Live preview disabled",
                            "Turn it on from the View menu or overview controls.",
                            options: new UiEmptyStateOptions(Height: 92f, Padding: 12f))))),
            Dux.Section(
                "Table",
                Dux.Table(
                    "task-table",
                    [
                        Dux.TableColumn("Task"),
                        Dux.TableColumn("Owner", 92f),
                        Dux.TableColumn("Progress", 110f, 1f)
                    ],
                    _state.Tasks,
                    task =>
                    [
                        Dux.Text(task.Name),
                        Dux.Text(task.Owner).Muted(),
                        Dux.Text(() => $"{task.Progress:P0}")
                    ]).FillFrameWidth()));
}

sealed class NotesTab(DashboardState state) : UiComponent
{
    private readonly DashboardState _state = state;

    protected override IUiView Build()
        => Dux.VStack(
            8f,
            Dux.Tree(
                "Composition",
                Dux.VStack(
                    6f,
                    Dux.ForEachIndexed(
                        _state.CompositionNotes,
                        (index, note) => Dux.Text($"{index + 1}. {note}"))),
                defaultOpen: true),
            Dux.TextArea("notes", _state.Notes, 4096, 190f)
                .FillWidth());
}

sealed class DetailsWindow(DashboardState state) : UiComponent
{
    private readonly DashboardState _state = state;

    protected override IUiView Build()
        => Dux.Window(
            "Details",
            Dux.VStack(
                8f,
                Dux.Subtitle("Compiled Design"),
                Dux.Callout(
                    "Reusable components",
                    "Reusable UiComponent classes describe the screen before the first frame.",
                    options: new UiCalloutOptions(Tone: UiTextTone.Accent, Height: 96f, Padding: 10f)),
                Dux.Separator(),
                Dux.PropertyList(
                    new UiPropertyListOptions(LabelWidth: 70f),
                    Dux.Property("Project", () => _state.ProjectName.Value),
                    Dux.Property("Channel", () => _state.SelectedChannel, UiTextTone.Accent)),
                Dux.Switch(
                    () => _state.Priority.Value,
                    Dux.Badge(() => $"Priority {_state.Priority.Value}", new UiBadgeOptions(Tone: UiBadgeTone.Neutral)),
                    Dux.Case(1, Dux.Badge("Low", new UiBadgeOptions(Tone: UiBadgeTone.Neutral))),
                    Dux.Case(2, Dux.Badge("Guarded", new UiBadgeOptions(Tone: UiBadgeTone.Accent))),
                    Dux.Case(3, Dux.Badge("Normal", new UiBadgeOptions(Tone: UiBadgeTone.Success))),
                    Dux.Case(4, Dux.Badge("Elevated", new UiBadgeOptions(Tone: UiBadgeTone.Warning))),
                    Dux.Case(5, Dux.Badge("Critical", new UiBadgeOptions(Tone: UiBadgeTone.Danger)))),
                Dux.Button("Close details", () => _state.DetailsOpen.Value = false)),
            new UiWindowOptions(
                Position: new UiVector2(865f, 84f),
                Size: new UiVector2(310f, 430f),
                Open: _state.DetailsOpen,
                TopMost: true));
}

sealed class DashboardState
{
    public string[] CompositionNotes { get; } =
    [
        "UiState<T> keeps local app state.",
        "UiComponent keeps reusable view fragments testable.",
        "DuxelWindowsApp.Run<TDesign>() applies a compiled design at app startup."
    ];
    public UiState<string> ProjectName { get; } = Dux.State("Duxel Control Surface");
    public UiState<string> Status { get; } = Dux.State("Ready for declarative composition");
    public UiState<string> Notes { get; } = Dux.State("State is held in UiState<T>. Views describe how it should be drawn.");
    public UiState<bool> LivePreview { get; } = Dux.State(true);
    public UiState<bool> DetailsOpen { get; } = Dux.State(true);
    public UiState<int> Runs { get; } = Dux.State(12);
    public UiState<int> Priority { get; } = Dux.State(3);
    public UiState<DashboardSection> SelectedSection { get; } = Dux.State(DashboardSection.Overview);
    public UiState<ReleaseChannel> Channel { get; } = Dux.State(ReleaseChannel.Preview);
    public UiState<float> Progress { get; } = Dux.State(0.62f);
    public List<TaskRow> Tasks { get; } =
    [
        new TaskRow("layout", "Layout pass", "Core", 0.82f),
        new TaskRow("theme", "Theme tokens", "Design", 0.74f),
        new TaskRow("chrome", "Windows chrome", "Platform", 0.91f),
        new TaskRow("fba", "FBA validation", "Samples", 0.58f)
    ];

    public string SelectedChannel => Channel.Value.ToDisplayName();
}

enum ReleaseChannel
{
    Stable,
    Preview,
    Canary
}

enum DashboardSection
{
    Overview,
    Tasks,
    Notes
}

static class ReleaseChannelExtensions
{
    public static string ToDisplayName(this ReleaseChannel channel)
        => channel switch
        {
            ReleaseChannel.Stable => "Stable",
            ReleaseChannel.Preview => "Preview",
            ReleaseChannel.Canary => "Canary",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
        };
}

sealed class TaskRow(string id, string name, string owner, float progress)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Owner { get; } = owner;
    public float Progress { get; } = progress;
    public bool Selected { get; set; }
}
