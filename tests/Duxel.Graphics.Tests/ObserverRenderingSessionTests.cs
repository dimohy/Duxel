using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Gallery;

internal static class ObserverRenderingSessionTests
{
    internal static void Run(Action<bool, string> check, bool keepRenderingVisible = false, bool exerciseSelection = false)
    {
        var session = new DuxelAppSession();
        using var observer = new ObserverScreen(ObserverServices.CreateWindows());
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 1280,
            Height = 900,
            Title = "Duxel owned Observer rendering regression",
            VSync = false,
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new(),
            FrameInvalidated = session.RequestFrame
        });
        var screen = new Screen(observer, session, keepRenderingVisible, platform.AccessibilityTree, exerciseSelection ? check : null);
        Console.WriteLine($"OBSERVER_SESSION_BEGIN visible={keepRenderingVisible}");
        session.RunCore(new()
        {
            Screen = screen,
            Design = UiCompiledDesign.Windows11Dark,
            Window = new()
            {
                Width = 1280,
                Height = 900,
                VSync = false
            },
            Frame = new()
            {
                EnableIdleFrameSkip = true,
                UseEventDrivenPlatformLoop = true,
                IsAnimationActiveProvider = () => !observer.UpdateOnlyOnChange
            },
            Debug = new()
            {
                LogStartupTimings = true
            }
        }, platform);
        check(screen.Frames == 614, "actual Observer application session completes all requested page frames");
        check(screen.Presented > 0, "actual Observer session presents GPU frames through the application renderer");
        check(observer.RenderingPage.StaticBuilds > 0, "actual rendering tab builds its static primitive content");
        check(observer.SelectedPage == "개요", "actual Observer session leaves rendering and returns to overview");
    }

    private sealed class Screen(ObserverScreen observer, DuxelAppSession session, bool keepRenderingVisible,
        UiAccessibilityTree accessibility, Action<bool, string>? selectionCheck) : UiScreen
    {
        internal int Frames;
        internal long Presented;
        public override void Render(UiImmediateContext ui)
        {
            observer.SelectedPage = Frames is >= 1 and <= 3 or >= 610 and <= 612
                || keepRenderingVisible && Frames is >= 4 and < 610 ? "렌더링" : "개요";
            if (selectionCheck is not null && Frames is >= 4 and <= 30)
                observer.SelectedPage = Frames is >= 14 and <= 20 ? "개요" : "선택 목록";
            if (selectionCheck is not null && Frames is 27 or 29)
                observer.DropdownPage.Editable!.Open!.Value = Frames == 27;
            if (selectionCheck is not null && Frames == 28)
                observer.DropdownPage.Editable!.Text.Value = "unmatched city";
            if (selectionCheck is not null && Frames == 29)
                selectionCheck(HasVisibleLabel("검색 결과가 없습니다"),
                    "actual GPU application publishes the localized empty-result message after rendering its Korean glyphs");
            if (selectionCheck is not null && Frames is 7 or 9)
            {
                UiAccessibilityElement Find(string name, UiAccessibilityRole role)
                {
                    UiAccessibilityElement? result = null;
                    void Visit(string? id)
                    {
                        while (id is not null && accessibility.TryGet(id, out var item))
                        {
                            if (item.Node.Name == name && item.Node.Role == role)
                                result = item;
                            Visit(item.FirstChild);
                            id = item.Next;
                        }
                    }
                    Visit(accessibility.Root.First);
                    return result ?? throw new InvalidOperationException(name);
                }
                if (Frames == 7)
                {
                    var root = Find("도시 검색", UiAccessibilityRole.ComboBox);
                    selectionCheck(accessibility.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
                        "actual Observer session accepts Combobox focus through its published provider");
                    selectionCheck(accessibility.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.SetValue, "New ", wait: false) == UiAccessibilityResult.Success,
                        "actual Observer session accepts a real Combobox input query");
                }
                else
                {
                    var option = Find("New York", UiAccessibilityRole.Option);
                    selectionCheck(accessibility.RequestAction(option.Node.Id, option.RuntimeId, UiAccessibilityAction.Select, wait: false) == UiAccessibilityResult.Success,
                        "actual Observer session accepts a filtered canonical option selection");
                }
            }
            Console.WriteLine($"OBSERVER_FRAME_BEGIN {Frames} {observer.SelectedPage}");
            Console.Out.Flush();
            observer.Render(ui);
            if (selectionCheck is not null && Frames is 10 or 25)
                selectionCheck(observer.DropdownPage.Editable is { IsOpen: false } combo && combo.Text.Value == "New York",
                    "actual GPU application preserves the committed Combobox input across page changes : " + Frames);
            if (selectionCheck is not null && Frames is 27 or 28 or 29)
                selectionCheck(observer.DropdownPage.Editable!.IsOpen == (Frames != 29)
                    && observer.DropdownPage.OpenChanges == (Frames == 29 ? 4 : 3),
                    "actual GPU application consumes controlled popup state without repeated transition callbacks : " + Frames);
            Presented = (long)ui.GetRendererFrameMetrics().PresentationSequence;
            Console.WriteLine($"OBSERVER_FRAME_END {Frames} presented={Presented} static={observer.RenderingPage.StaticBuilds}");
            Console.Out.Flush();
            Frames++;
            if (Frames == 614)
                session.Exit();
            else
                ui.RequestFrameAt(ui.GetTime());
        }
        private bool HasVisibleLabel(string name)
        {
            var found = false;
            void Visit(string? id)
            {
                while (id is not null && accessibility.TryGet(id, out var item))
                {
                    found |= item.Node.Name == name && item.Node.Role == UiAccessibilityRole.Label && !item.Node.Offscreen;
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(accessibility.Root.First);
            return found;
        }
    }
}
