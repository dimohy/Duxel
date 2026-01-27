using System.Globalization;

namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    private sealed class DemoWindowState
    {
        public bool ShowMetrics;
        public bool ShowDebugLog;
        public bool ShowIdStack;
        public bool ShowAbout;
        public bool ShowStyleEditor;
        public bool ShowUserGuide;
        public bool ShowClosableWindow = true;
        public int Counter;
        public float SliderValue = 0.5f;
        public bool DemoCheckbox = true;
        public string TextInput = "Hello, Dux!";
    }

    private static readonly DemoWindowState DemoState = new();
    private static readonly string[] DemoStyleNames = ["Dark", "Light", "Classic"];

    public void ShowDemoWindow()
    {
        var open = _state.GetBool("demo.window", true);
        ShowDemoWindow(ref open);
        _state.SetBool("demo.window", open);
    }

    public void ShowDemoWindow(ref bool open)
    {
        if (!open)
        {
            return;
        }

        BeginWindow("Dux Demo");
        SeparatorText("Help");
        Text("Dux demo window showing a subset of widgets and tools.");
        if (SmallButton("Close Demo"))
        {
            open = false;
        }

        SeparatorText("Tools");
        DemoState.ShowMetrics = ToggleWindow("Metrics/Debugger", DemoState.ShowMetrics);
        DemoState.ShowDebugLog = ToggleWindow("Debug Log", DemoState.ShowDebugLog);
        DemoState.ShowIdStack = ToggleWindow("ID Stack Tool", DemoState.ShowIdStack);
        DemoState.ShowAbout = ToggleWindow("About", DemoState.ShowAbout);
        DemoState.ShowStyleEditor = ToggleWindow("Style Editor", DemoState.ShowStyleEditor);
        DemoState.ShowUserGuide = ToggleWindow("User Guide", DemoState.ShowUserGuide);

        SeparatorText("Widgets");
        _ = Checkbox("Demo checkbox", ref DemoState.DemoCheckbox);
        SliderFloat("Demo slider", ref DemoState.SliderValue, 0f, 1f);
        InputText("Demo input", ref DemoState.TextInput, 64);
        if (Button("Increment"))
        {
            DemoState.Counter++;
        }
        SameLine();
        TextV("Counter: {0}", DemoState.Counter);

        SeparatorText("Windows");
        if (Checkbox("Show closable window", ref DemoState.ShowClosableWindow))
        {
            SetWindowOpen("Closable Window", DemoState.ShowClosableWindow);
        }

        EndWindow();

        if (DemoState.ShowMetrics)
        {
            ShowMetricsWindow(ref DemoState.ShowMetrics);
        }

        if (DemoState.ShowDebugLog)
        {
            ShowDebugLogWindow(ref DemoState.ShowDebugLog);
        }

        if (DemoState.ShowIdStack)
        {
            ShowIDStackToolWindow(ref DemoState.ShowIdStack);
        }

        if (DemoState.ShowAbout)
        {
            ShowAboutWindow(ref DemoState.ShowAbout);
        }

        if (DemoState.ShowStyleEditor)
        {
            BeginWindow("Style Editor");
            ShowStyleEditor();
            if (SmallButton("Close"))
            {
                DemoState.ShowStyleEditor = false;
            }
            EndWindow();
        }

        if (DemoState.ShowUserGuide)
        {
            BeginWindow("User Guide");
            ShowUserGuide();
            if (SmallButton("Close"))
            {
                DemoState.ShowUserGuide = false;
            }
            EndWindow();
        }

        SetNextWindowOpen(DemoState.ShowClosableWindow);
        BeginWindow("Closable Window");
        Text("Close with the title bar X button.");
        EndWindow();
        DemoState.ShowClosableWindow = _state.GetWindowOpen("Closable Window", DemoState.ShowClosableWindow);
    }

    public void ShowMetricsWindow()
    {
        var open = _state.GetBool("demo.metrics", false);
        ShowMetricsWindow(ref open);
        _state.SetBool("demo.metrics", open);
    }

    public void ShowMetricsWindow(ref bool open)
    {
        if (!open)
        {
            return;
        }

        BeginWindow("Metrics/Debugger");
        SeparatorText("Frame");
        TextV("Display size: {0} x {1}", _displaySize.X.ToString("0", CultureInfo.InvariantCulture), _displaySize.Y.ToString("0", CultureInfo.InvariantCulture));
        TextV("Mouse pos: {0}, {1}", _mousePosition.X.ToString("0.0", CultureInfo.InvariantCulture), _mousePosition.Y.ToString("0.0", CultureInfo.InvariantCulture));
        TextV("Mouse down: {0}", _leftMouseDown ? "true" : "false");
        TextV("Mouse wheel: {0}", _mouseWheel.ToString("0.00", CultureInfo.InvariantCulture));

        SeparatorText("State");
        TextV("ActiveId: {0}", _state.ActiveId ?? "<null>");
        TextV("HoveredId: {0}", _state.HoveredId ?? "<null>");
        TextV("FocusedId: {0}", _state.FocusedId ?? "<null>");
        TextV("Current window: {0}", _currentWindowId ?? "<null>");

        if (SmallButton("Close"))
        {
            open = false;
        }
        EndWindow();
    }

    public void ShowDebugLogWindow()
    {
        var open = _state.GetBool("demo.debuglog", false);
        ShowDebugLogWindow(ref open);
        _state.SetBool("demo.debuglog", open);
    }

    public void ShowDebugLogWindow(ref bool open)
    {
        if (!open)
        {
            return;
        }

        BeginWindow("Debug Log");
        if (SmallButton("Clear"))
        {
            _state.ClearDebugLog();
        }
        SameLine();
        if (SmallButton("Close"))
        {
            open = false;
        }

        var available = GetContentRegionAvail();
        var width = MathF.Max(1f, available.X);
        var height = MathF.Max(GetFrameHeight() * 6f, 160f);
        if (BeginChild("DebugLog##child", new UiVector2(width, height), true))
        {
            if (_state.DebugLogEntries.Count == 0)
            {
                TextDisabled("No debug log entries yet.");
            }
            else
            {
                foreach (var entry in _state.DebugLogEntries)
                {
                    TextUnformatted(entry);
                }
            }
        }
        EndChild();
        EndWindow();
    }

    public void ShowIDStackToolWindow()
    {
        var open = _state.GetBool("demo.idstack", false);
        ShowIDStackToolWindow(ref open);
        _state.SetBool("demo.idstack", open);
    }

    public void ShowIDStackToolWindow(ref bool open)
    {
        if (!open)
        {
            return;
        }

        BeginWindow("ID Stack Tool");
        TextV("Last item id: {0}", _lastItemId ?? "<null>");
        TextV("Hovered id: {0}", _state.HoveredId ?? "<null>");
        TextV("Active id: {0}", _state.ActiveId ?? "<null>");
        TextV("Focused id: {0}", _state.FocusedId ?? "<null>");
        TextV("Current window: {0}", _currentWindowId ?? "<null>");
        if (SmallButton("Close"))
        {
            open = false;
        }
        EndWindow();
    }

    public void ShowAboutWindow()
    {
        var open = _state.GetBool("demo.about", false);
        ShowAboutWindow(ref open);
        _state.SetBool("demo.about", open);
    }

    public void ShowAboutWindow(ref bool open)
    {
        if (!open)
        {
            return;
        }

        BeginWindow("About Dux");
        TextV("{0}", Ui.GetVersion());
        Text("Immediate-mode UI sample implementation.");
        Text("Powered by Dux Core.");
        if (SmallButton("Close"))
        {
            open = false;
        }
        EndWindow();
    }

    public void ShowStyleEditor(UiTheme? reference = null)
    {
        var style = _theme;
        var refStyle = reference ?? style;

        SeparatorText("Style");
        ShowStyleSelector("Theme");
        ShowFontSelector("Fonts");

        SeparatorText("Colors");
        ShowThemeColor("Text", style.Text);
        ShowThemeColor("TextDisabled", style.TextDisabled);
        ShowThemeColor("WindowBg", style.WindowBg);
        ShowThemeColor("FrameBg", style.FrameBg);
        ShowThemeColor("Button", style.Button);
        ShowThemeColor("ButtonHovered", style.ButtonHovered);
        ShowThemeColor("ButtonActive", style.ButtonActive);
        ShowThemeColor("Header", style.Header);
        ShowThemeColor("HeaderHovered", style.HeaderHovered);
        ShowThemeColor("HeaderActive", style.HeaderActive);
        ShowThemeColor("Separator", style.Separator);
        ShowThemeColor("CheckMark", style.CheckMark);
        ShowThemeColor("SliderGrab", style.SliderGrab);
        ShowThemeColor("SliderGrabActive", style.SliderGrabActive);

        if (!refStyle.Equals(style))
        {
            TextDisabled("Reference style differs from current style.");
        }
    }

    public bool ShowStyleSelector(string label)
    {
        var index = _state.GetCursor("style.selector", 0);
        if (index < 0 || index >= DemoStyleNames.Length)
        {
            index = 0;
        }

        var changed = Combo(label, ref index, DemoStyleNames, 4);
        _state.SetCursor("style.selector", index);

        if (changed)
        {
            var theme = index switch
            {
                1 => UiTheme.ImGuiLight,
                2 => UiTheme.ImGuiClassic,
                _ => UiTheme.ImGuiDark,
            };
            _state.RequestTheme(theme);
        }

        return changed;
    }

    public void ShowFontSelector(string label)
    {
        label ??= "Fonts";
        SeparatorText(label);
        LabelText("Atlas size", $"{_fontAtlas.Width} x {_fontAtlas.Height}");
        LabelText("LineHeight", _fontAtlas.LineHeight.ToString("0.00", CultureInfo.InvariantCulture));
        LabelText("Ascent", _fontAtlas.Ascent.ToString("0.00", CultureInfo.InvariantCulture));
        LabelText("Descent", _fontAtlas.Descent.ToString("0.00", CultureInfo.InvariantCulture));
        LabelText("Glyphs", _fontAtlas.Glyphs.Count.ToString(CultureInfo.InvariantCulture));
    }

    public void ShowUserGuide()
    {
        BulletText("Drag windows by their title bar.");
        BulletText("Resize from the lower-right corner.");
        BulletText("Use mouse wheel to scroll.");
        BulletText("Click checkboxes and buttons to interact.");
    }

    private bool ToggleWindow(string label, bool value)
    {
        var current = value;
        _ = Checkbox(label, ref current);
        return current;
    }

    private void ShowThemeColor(string label, UiColor color)
    {
        ColorButton($"##{label}", color, new UiVector2(18f, 18f));
        SameLine();
        Text(label);
    }
}