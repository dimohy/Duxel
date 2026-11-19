using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class FocusEventsHost
{
    internal const string Title = "Duxel owned focus event fixture";

    internal static void Run(Action<bool, string> check, string coordinationDirectory)
    {
        using var peer = new WindowsPlatformBackend(new()
        {
            Width = 240, Height = 120, Title = "Duxel owned focus peer",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = Title,
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var value = new UiState<string?>("alpha");
        using var tabs = Fluent.TabList("Focus tabs", value.Binding, new UiTabOption[] { new("alpha", "Alpha"), new("beta", "Beta") });
        using var body = Fluent.Button("Alpha body action");
        UiKeyEvent[] pending = [];
        var showBody = true;
        using var status = Fluent.Button("Logical focus : none");
        var finish = false;
        var changes = 0;
        platform.AccessibilityTree.FocusChanged += change =>
        {
            changes++;
            Console.WriteLine($"CoreFocus : {change.Previous?.Node.Name} -> {change.Current?.Node.Name}");
        };
        Action<UiImmediateContext> panel = ui => { if (showBody) body.Render(ui); else ui.Text("Body removed"); };
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            tabs.Render(ui);
            tabs.RenderPanel(ui, "alpha", panel);
            status.Render(ui);
        });
        var clock = Stopwatch.StartNew();
        var previous = 0d;
        var ready = false;
        var commandPath = Path.Combine(coordinationDirectory, "command.txt");
        var previousCommand = string.Empty;
        while (!finish && !platform.ShouldClose && clock.Elapsed.TotalSeconds < 30)
        {
            platform.PollEvents();
            peer.PollEvents();
            var command = File.Exists(commandPath) ? File.ReadAllText(commandPath) : string.Empty;
            if (command != previousCommand)
            {
                previousCommand = command;
                switch (command)
                {
                    case "right": pending = [new(UiKey.RightArrow, true, KeyModifiers.None)]; break;
                    case "left": pending = [new(UiKey.LeftArrow, true, KeyModifiers.None)]; break;
                    case "peer":
                        SetActiveWindow(peer.WindowHandle);
                        SetFocus(peer.WindowHandle);
                        if (GetFocus() != peer.WindowHandle)
                            throw new Win32Exception(Marshal.GetLastPInvokeError(), "The owned peer did not receive keyboard focus.");
                        break;
                    case "remove": showBody = false; break;
                    case "finish": finish = true; break;
                    default: throw new InvalidOperationException("Unknown focus fixture command : " + command);
                }
            }
            var now = clock.Elapsed.TotalSeconds;
            var keys = pending;
            pending = [];
            var input = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, keys, [], default, default);
            context.NewFrame(new((float)(now - previous), new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            previous = now;
            context.Render(screen);
            using var draw = context.GetDrawData();
            var name = platform.AccessibilityTree.Root.Focus is { } id && platform.AccessibilityTree.TryGet(id, out var focused)
                ? focused.Node.Name : "none";
            status.Label = "Logical focus : " + name;
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_FOCUS_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish, "external focus client finishes its owned HWND fixture");
        check(changes == 5, "five logical focus publications are independent of native departure and restoration");
        check(value.Value == "alpha" && !showBody, "manual arrow focus and body removal preserve actual tab selection");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetFocus(nint window);
    [LibraryImport("user32.dll")]
    private static partial nint GetFocus();
    [LibraryImport("user32.dll")]
    private static partial nint SetActiveWindow(nint window);
}
