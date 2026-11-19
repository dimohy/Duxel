using System.Diagnostics;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class RelationshipEventsHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 360, Title = "Duxel owned relationship event fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        var tree = platform.AccessibilityTree;
        var phase = 0;
        var changes = 0;
        var targetInvocations = 0;
        var finish = false;
        tree.RelationshipChanged += change =>
        {
            changes++;
            Console.WriteLine($"CoreRelationship : {change.Property} / {change.Previous?.Element.Node.Id} -> {change.Current?.Element.Node.Id}");
        };
        static UiAccessibilityNode Node(string id, string name, UiRect bounds, UiAccessibilityRole role, string parent = "") =>
            new(id, parent, role, name, bounds, true, false, false, Focusable : role == UiAccessibilityRole.Button, ValuePattern : false);
        var clock = Stopwatch.StartNew();
        var ready = false;
        while (!finish && !platform.ShouldClose && clock.Elapsed.TotalSeconds < 20)
        {
            platform.PollEvents();
            tree.BeginFrame();
            if (tree.Take("advance", UiAccessibilityAction.Invoke) is { } advance)
            {
                phase++;
                advance.Result = UiAccessibilityResult.Success;
            }
            if (tree.Take("new", UiAccessibilityAction.Invoke) is { } invoke)
            {
                targetInvocations++;
                invoke.Result = UiAccessibilityResult.Success;
            }
            if (tree.Take("finish", UiAccessibilityAction.Invoke) is { } done)
            {
                finish = true;
                done.Result = UiAccessibilityResult.Success;
            }
            if (phase < 4)
            {
                var target = phase == 0 ? "old" : phase == 2 ? null : "new";
                tree.Add(Node("source", "Relationship source", new(20, 20, 200, 30), UiAccessibilityRole.Pane)
                    with { LabeledBy = target, ControlledElementId = target });
                if (target is not null)
                {
                    tree.Add(Node(target + "-parent", "Target parent", new(20, 70, 200, 70), UiAccessibilityRole.Pane));
                    tree.Add(Node(target, target == "old" ? "Old target" : "New target", new(30, 80, 150, 30), UiAccessibilityRole.Button, target + "-parent"));
                }
            }
            tree.Add(Node("advance", "Advance relation", new(300, 20, 180, 40), UiAccessibilityRole.Button));
            tree.Add(Node("finish", "Finish", new(300, 80, 180, 40), UiAccessibilityRole.Button));
            tree.EndFrame();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_RELATIONSHIP_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish, "external relationship client finishes its owned HWND fixture");
        check(phase == 4, "external client completes replace, clear, restore and source removal");
        check(changes == 6, "exactly six committed relationship changes occur across all idle frames");
        check(targetInvocations == 2, "relationship event payloads preserve the actual target's callable Invoke pattern");
    }
}
