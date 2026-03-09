using System.Globalization;

namespace Duxel.Core;

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
        public bool PositionClosableWindowNextFrame = true;
        public int Counter;
        public float SliderValue = 0.5f;
        public bool DemoCheckbox = true;
        public string TextInput = "Hello, Duxel!";
    }

    private static readonly DemoWindowState DemoState = new();
    private static readonly string[] DemoStyleNames = ["Dark", "Light", "Classic"];

    private void RenderBuiltInHero(
        string title,
        string description,
        UiColor accent,
        params (string Label, string Value, UiColor ValueColor)[] cards)
    {
        var visibleCardCount = Math.Clamp(cards.Length, 0, 2);
        var lineHeight = GetTextLineHeight();
        const float accentWidth = 5f;
        const float paddingX = 12f;
        const float paddingY = 8f;
        const float sectionGap = 4f;
        const float cardsTopGap = 8f;
        const float cardGap = 8f;

        var origin = GetCursorScreenPos();
        var width = MathF.Max(160f, GetContentRegionAvail().X);
        var contentX = origin.X + accentWidth + paddingX;
        var contentWidth = MathF.Max(80f, width - (contentX - origin.X) - paddingX);
        var headingText = $"BUILT-IN TOOLS · {title}";
        var headingSize = CalcTextSize(headingText);
        var wrappedDescription = WrapText(description, contentWidth);
        var descriptionSize = CalcTextSize(wrappedDescription);
        var cardHeight = lineHeight + 10f;
        var cardsHeight = visibleCardCount > 0 ? cardHeight + cardsTopGap : 0f;
        var contentHeight = headingSize.Y + sectionGap + descriptionSize.Y + cardsHeight;
        var heroHeight = MathF.Max(visibleCardCount > 0 ? 62f : 40f, contentHeight + (paddingY * 2f));
        var rect = new UiRect(origin.X, origin.Y, width, heroHeight);
        var drawList = GetWindowDrawList();

        drawList.AddRectFilled(rect, new UiColor(0xEE121A24));
        drawList.AddRect(rect, new UiColor(0xFF314050), 6f, 1f);
        drawList.AddRectFilled(new UiRect(rect.X, rect.Y, accentWidth, rect.Height), accent);

        SetCursorScreenPos(new UiVector2(contentX, rect.Y + paddingY));
        TextDisabled(headingText);
        SetCursorScreenPos(new UiVector2(contentX, rect.Y + paddingY + headingSize.Y + sectionGap));
        TextColored(new UiColor(0xFFB7C7D9), wrappedDescription);

        if (visibleCardCount > 0)
        {
            var cardWidth = (rect.Width - (contentX - rect.X) - paddingX - ((visibleCardCount - 1) * cardGap)) / visibleCardCount;
            var cardY = rect.Y + paddingY + headingSize.Y + sectionGap + descriptionSize.Y + cardsTopGap;
            for (var i = 0; i < visibleCardCount; i++)
            {
                var cardRect = new UiRect(contentX + (i * (cardWidth + cardGap)), cardY, cardWidth, cardHeight);
                drawList.AddRectFilled(cardRect, new UiColor(0xFF1D2732));
                drawList.AddRect(cardRect, new UiColor(0xFF384656), 5f, 1f);
                var textRect = new UiRect(cardRect.X + 10f, cardRect.Y, MathF.Max(0f, cardRect.Width - 20f), cardRect.Height);
                DrawTextAligned(textRect, $"{cards[i].Label}: {cards[i].Value}", cards[i].ValueColor, UiItemHorizontalAlign.Left, UiItemVerticalAlign.Center);
            }
        }

        SetCursorScreenPos(origin);
        Dummy(new UiVector2(width, heroHeight + 2f));
    }

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

        BeginWindow("Duxel Demo");
        RenderBuiltInHero(
            "Duxel Demo",
            "A curated subset of widgets and utility windows intended as the built-in quick tour of the framework.",
            new UiColor(0xFF58A6FF),
            ("Counter", DemoState.Counter.ToString(CultureInfo.InvariantCulture), new UiColor(0xFF8DE1A6)),
            ("Slider", DemoState.SliderValue.ToString("0.00", CultureInfo.InvariantCulture), new UiColor(0xFFFFD479)));
        SeparatorText("Help");
        Text("Duxel demo window showing a subset of widgets and tools.");
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
            if (DemoState.ShowClosableWindow)
            {
                DemoState.PositionClosableWindowNextFrame = true;
            }

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
            RenderBuiltInHero(
                "Style Editor",
                "Theme and font inspection tools for the built-in demo environment.",
                new UiColor(0xFF58A6FF));
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
            RenderBuiltInHero(
                "User Guide",
                "A quick interaction cheat sheet for mouse, resize, and general demo navigation.",
                new UiColor(0xFF58A6FF));
            ShowUserGuide();
            if (SmallButton("Close"))
            {
                DemoState.ShowUserGuide = false;
            }
            EndWindow();
        }

        if (DemoState.ShowClosableWindow && DemoState.PositionClosableWindowNextFrame)
        {
            var viewport = GetMainViewport();
            var targetSize = new UiVector2(360f, 176f);
            var targetPos = new UiVector2(
                viewport.WorkPos.X + MathF.Max(12f, viewport.WorkSize.X - targetSize.X - 12f),
                viewport.WorkPos.Y + 12f);
            SetNextWindowPos(targetPos);
            SetNextWindowSize(targetSize);
        }

        SetNextWindowTopMost(true);
        SetNextWindowOpen(DemoState.ShowClosableWindow);
        BeginWindow("Closable Window");
        RenderBuiltInHero(
            "Closable Window",
            "A minimal built-in example showing title-bar close behavior.",
            new UiColor(0xFF58A6FF));
        Text("Close with the title bar X button.");
        EndWindow();
        if (DemoState.ShowClosableWindow)
        {
            DemoState.PositionClosableWindowNextFrame = false;
        }

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
        RenderBuiltInHero(
            "Metrics/Debugger",
            "Frame timing, pointer data, and immediate UI state for runtime inspection and troubleshooting.",
            new UiColor(0xFF58A6FF),
            ("Mouse", $"{_mousePosition.X:0.0}, {_mousePosition.Y:0.0}", new UiColor(0xFF8DE1A6)),
            ("Wheel", _mouseWheel.ToString("0.00", CultureInfo.InvariantCulture), new UiColor(0xFFFFD479)));
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
        RenderBuiltInHero(
            "Debug Log",
            "A built-in rolling text console for framework diagnostics and development-time event tracing.",
            new UiColor(0xFF58A6FF),
            ("Entries", _state.DebugLogEntries.Count.ToString(CultureInfo.InvariantCulture), new UiColor(0xFF8DE1A6)));
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
        RenderBuiltInHero(
            "ID Stack Tool",
            "A focused view of recently interacted IDs and the current immediate-mode focus chain.",
            new UiColor(0xFF58A6FF),
            ("Hovered", _state.HoveredId ?? "<null>", new UiColor(0xFF8DE1A6)));
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

        BeginWindow("About Duxel");
        RenderBuiltInHero(
            "About Duxel",
            "Framework identity, version information, and the compact origin story of the built-in UI sample set.",
            new UiColor(0xFF58A6FF),
            ("Version", Ui.GetVersion(), new UiColor(0xFF8DE1A6)));
        TextV("{0}", Ui.GetVersion());
        Text("Immediate-mode UI sample implementation.");
        Text("Powered by Duxel Core.");
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
        label ??= "Style";
        var index = _state.GetCursor("style.selector", 0);
        if (index < 0 || index >= DemoStyleNames.Length)
        {
            index = 0;
        }

        Text(label);
        var changed = Combo(ref index, DemoStyleNames, 4, $"style.selector/{label}");
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
