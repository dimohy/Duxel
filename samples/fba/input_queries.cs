// FBA: 키보드/마우스 입력 쿼리 API 시연 — IsKeyDown, Shortcut, Mouse 상태, 커서, 클립보드
#:property TargetFramework=net10.0
#:package Duxel.Windows.App@*-*

using System;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Input Queries Demo",
        Width = 1000,
        Height = 700
    },
    Screen = new InputQueriesScreen()
});

public sealed class InputQueriesScreen : UiScreen
{
    private int _frameCounter;
    private int _shortcutCount;
    private string _clipboardText = "";
    private string _lastKeyName = "(none)";


    public override void Render(UiImmediateContext ui)
    {
        _frameCounter++;

        RenderKeyboardWindow(ui);
        RenderMouseWindow(ui);
        RenderShortcutWindow(ui);
        RenderClipboardWindow(ui);
    }

    private void RenderKeyboardWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(460f, 320f));
        ui.BeginWindow("Keyboard Queries");

        ui.SeparatorText("Key State");

        // Show state for commonly used keys
        ReadOnlySpan<UiKey> keys =
        [
            UiKey.A, UiKey.W, UiKey.S, UiKey.D,
            UiKey.Space, UiKey.Enter, UiKey.Escape, UiKey.Tab,
            UiKey.LeftArrow, UiKey.RightArrow, UiKey.UpArrow, UiKey.DownArrow
        ];

        ui.Columns(4, true);
        ui.Text("Key");
        ui.NextColumn();
        ui.Text("Down");
        ui.NextColumn();
        ui.Text("Pressed");
        ui.NextColumn();
        ui.Text("Released");
        ui.NextColumn();

        foreach (var key in keys)
        {
            var name = ui.GetKeyName(key);
            var down = ui.IsKeyDown(key);
            var pressed = ui.IsKeyPressed(key);
            var released = ui.IsKeyReleased(key);

            if (pressed)
                _lastKeyName = name;

            ui.Text(name);
            ui.NextColumn();
            ui.TextColored(down ? new UiColor(0xFF44FF44) : new UiColor(0xFF888888), down ? "YES" : "no");
            ui.NextColumn();
            ui.TextColored(pressed ? new UiColor(0xFFFFFF44) : new UiColor(0xFF888888), pressed ? "YES" : "no");
            ui.NextColumn();
            ui.TextColored(released ? new UiColor(0xFFFF4444) : new UiColor(0xFF888888), released ? "YES" : "no");
            ui.NextColumn();
        }
        ui.Columns(1);

        ui.Separator();
        ui.Text($"Last pressed key: {_lastKeyName}");

        ui.EndWindow();
    }

    private void RenderMouseWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(420f, 340f));
        ui.BeginWindow("Mouse Queries");

        ui.SeparatorText("Mouse Position");
        var pos = ui.GetMousePos();
        ui.Text($"Position: ({pos.X:0.0}, {pos.Y:0.0})");
        ui.Text($"Valid: {ui.IsMousePosValid()}");
        ui.Text($"Any down: {ui.IsAnyMouseDown()}");

        ui.SeparatorText("Button State");
        for (var btn = 0; btn < 3; btn++)
        {
            var name = btn switch { 0 => "Left", 1 => "Right", _ => "Middle" };
            ui.Text($"{name}: Down={ui.IsMouseDown(btn)} Clicked={ui.IsMouseClicked(btn)} " +
                    $"DblClick={ui.IsMouseDoubleClicked(btn)} Released={ui.IsMouseReleased(btn)} " +
                    $"Count={ui.GetMouseClickedCount(btn)}");
        }

        ui.SeparatorText("Dragging");
        ui.Text($"Dragging(L): {ui.IsMouseDragging(0)}");
        var delta = ui.GetMouseDragDelta(0);
        ui.Text($"Drag delta: ({delta.X:0.0}, {delta.Y:0.0})");
        if (ui.Button("Reset Drag Delta"))
            ui.ResetMouseDragDelta(0);

        ui.SeparatorText("Hover Rect Test");
        var cursorPos = ui.GetCursorScreenPos();
        var rectMin = cursorPos;
        var rectMax = new UiVector2(cursorPos.X + 120f, cursorPos.Y + 40f);
        var hovering = ui.IsMouseHoveringRect(rectMin, rectMax);

        // Draw visual rect
        var drawList = ui.GetWindowDrawList();
        drawList.PushTexture(ui.WhiteTextureId);
        var color = hovering ? new UiColor(0xFF44FF44) : new UiColor(0xFF444444);
        drawList.AddRect(new UiRect(rectMin.X, rectMin.Y, 120f, 40f), color, 0f, 2f);
        drawList.PopTexture();
        ui.Dummy(new UiVector2(120f, 40f));
        ui.SameLine();
        ui.Text(hovering ? "HOVERING!" : "Not hovering");

        ui.EndWindow();
    }

    private void RenderShortcutWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(400f, 200f));
        ui.BeginWindow("Shortcuts");

        ui.SeparatorText("Shortcut Detection");
        ui.Text("Press Ctrl+S to trigger shortcut");

        if (ui.Shortcut(UiKey.S, KeyModifiers.Ctrl))
            _shortcutCount++;

        ui.Text($"Ctrl+S triggered: {_shortcutCount} times");

        ui.SeparatorText("IsKeyChordPressed");
        var ctrlZ = ui.IsKeyChordPressed(UiKey.Z, KeyModifiers.Ctrl);
        var ctrlShiftZ = ui.IsKeyChordPressed(UiKey.Z, KeyModifiers.Ctrl | KeyModifiers.Shift);
        ui.Text($"Ctrl+Z: {ctrlZ}");
        ui.Text($"Ctrl+Shift+Z: {ctrlShiftZ}");

        ui.EndWindow();
    }

    private void RenderClipboardWindow(UiImmediateContext ui)
    {
        if (_frameCounter == 1)
            ui.SetNextWindowSize(new UiVector2(380f, 180f));
        ui.BeginWindow("Clipboard");

        ui.SeparatorText("Clipboard API");
        if (ui.Button("Read Clipboard"))
            _clipboardText = ui.GetClipboardText();
        ui.SameLine();
        if (ui.Button("Write 'Hello Duxel!'"))
            ui.SetClipboardText("Hello Duxel!");

        ui.TextWrapped($"Clipboard: {_clipboardText}");

        ui.EndWindow();
    }
}
