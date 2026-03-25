// FBA: Duxel 전체 위젯 종합 시연 — Immediate Mode API의 거의 모든 기능을 하나의 앱에서 데모
#:property TargetFramework=net10.0
#:property platform=windows
// run-fba 사용 시: --platform windows
// dotnet run 직접 사용 시: -p:platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel All Features Showcase",
        Width = 1600,
        Height = 1000,
        VSync = false
    },
    Screen = new AllFeaturesScreen()
});

public sealed class AllFeaturesScreen : UiScreen
{
    private const string LayoutAlphaPreviewTitle = "Layout Alpha Preview";

    private const string TextWindowTitle = "Text & Links";
    private const string TypographyWindowTitle = "Typography Studio";
    private const string ButtonWindowTitle = "Buttons & Actions";
    private const string CheckboxWindowTitle = "Checkboxes & Flags";
    private const string RadioWindowTitle = "Radio Choices";
    private const string ProgressWindowTitle = "Progress & Status";
    private const string SliderWindowTitle = "Sliders & Ranges";
    private const string DragWindowTitle = "Drag Editors";
    private const string InputWindowTitle = "Text & Numeric Input";
    private const string ColorWindowTitle = "Color Tools";
    private const string ComboWindowTitle = "Combo & Quick Pick";
    private const string ListBoxWindowTitle = "List Boxes";
    private const string SelectableWindowTitle = "Selectable Items";
    private const string TreeWindowTitle = "Tree & Outline";
    private const string TabWindowTitle = "Tabs & Workspace";
    private const string TableWindowTitle = "Tables & Data Grid";
    private const string PopupWindowTitle = "Popups & Modal Basics";
    private const string AdvancedPopupWindowTitle = "Popup Patterns";
    private const string TooltipWindowTitle = "Tooltips & Hover Cards";
    private const string ContextMenuWindowTitle = "Context Menus";
    private const string ChildWindowTitle = "Child Panels";
    private const string LayoutWindowTitle = "Layout & Spacing";
    private const string ColumnsWindowTitle = "Columns & Split Views";
    private const string InputQueriesWindowTitle = "Input Diagnostics";
    private const string ItemStatusWindowTitle = "Item State Inspector";
    private const string MultiSelectWindowTitle = "Multi-Selection";
    private const string LayersWindowTitle = "Layers & Motion";
    private const string DrawingWindowTitle = "Draw List Primitives";
    private const string ImageWindowTitle = "Images & Effects";
    private const string DragDropWindowTitle = "Drag and Drop";
    private const string ListClipperWindowTitle = "Virtualized List (10K Items)";
    private const string StyleWindowTitle = "Style Lab";
    private const string SettingsWindowTitle = "Settings Dashboard";
    private const string ProfileEditorWindowTitle = "Profile Workspace";
    private const string FileBrowserWindowTitle = "File Explorer";
    private const string LogViewerWindowTitle = "Log Console";
    private const string MarkdownStudioWindowTitle = "Markdown Studio";

    // ── Window visibility flags (Widgets) ──
    private bool _showText;
    private bool _showTypography;
    private bool _showButton;
    private bool _showCheckbox;
    private bool _showRadioButton;
    private bool _showProgressBar;
    private bool _showSlider;
    private bool _showDrag;
    private bool _showInput;
    private bool _showColor;
    private bool _showCombo;
    private bool _showListBox;
    private bool _showSelectable;
    private bool _showTree;
    private bool _showTab;
    private bool _showTable;
    private bool _showPopup;
    private bool _showAdvancedPopup;
    private bool _showTooltip;
    private bool _showContextMenu;
    private bool _showChild;
    private bool _showLayout;
    private bool _showColumns;
    private bool _showInputQueries;
    private bool _showItemStatus;
    private bool _showMultiSelect;
    private bool _showLayersAnimation;
    private bool _showDrawing;
    private bool _showImage;
    private bool _showDragDrop;
    private bool _showListClipper;
    private bool _showStyle;

    // ── Window visibility flags (Samples) ──
    private bool _showSettingsPanel;
    private bool _showProfileEditor;
    private bool _showFileBrowser;
    private bool _showLogViewer;
    private bool _showMarkdownStudio;

    // ── Window focus tracking ──
    private string? _focusWindowName;

    private void InitWindowOnce(UiImmediateContext ui, string name, UiVector2 size)
    {
        var viewport = ui.GetMainViewport();
        const float outerPadding = 8f;
        var menuHeight = MathF.Max(ui.GetFrameHeight(), 24f);
        var pos = new UiVector2(
            viewport.WorkPos.X + outerPadding,
            viewport.WorkPos.Y + menuHeight + outerPadding);
        var targetSize = new UiVector2(
            MathF.Max(320f, viewport.WorkSize.X - (outerPadding * 2f)),
            MathF.Max(220f, viewport.WorkSize.Y - menuHeight - (outerPadding * 2f)));

        ui.SetNextWindowPos(pos);
        ui.SetNextWindowSize(targetSize);
        if (_focusWindowName == name)
        {
            ui.SetNextWindowFocus();
            _focusWindowName = null;
        }
    }

    private void InitCenteredWindow(UiImmediateContext ui, string name, UiVector2 size)
    {
        var viewport = ui.GetMainViewport();
        const float outerPadding = 8f;
        var menuHeight = MathF.Max(ui.GetFrameHeight(), 24f);
        var availableHeight = MathF.Max(160f, viewport.WorkSize.Y - menuHeight - (outerPadding * 2f));
        var targetWidth = MathF.Min(size.X, MathF.Max(220f, viewport.WorkSize.X - (outerPadding * 2f)));
        var targetHeight = MathF.Min(size.Y, availableHeight);
        var regionTop = viewport.WorkPos.Y + menuHeight + outerPadding;
        var pos = new UiVector2(
            viewport.WorkPos.X + MathF.Max(outerPadding, (viewport.WorkSize.X - targetWidth) * 0.5f),
            regionTop + MathF.Max(0f, (availableHeight - targetHeight) * 0.5f));

        ui.SetNextWindowPos(pos);
        ui.SetNextWindowSize(new UiVector2(targetWidth, targetHeight));
        if (_focusWindowName == name)
        {
            ui.SetNextWindowFocus();
            _focusWindowName = null;
        }
    }

    private static string WrapTextToWidth(UiImmediateContext ui, string? text, float maxWidth)
    {
        var source = text ?? string.Empty;
        if (string.IsNullOrEmpty(source) || maxWidth <= 0f)
        {
            return source;
        }

        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var paragraphs = normalized.Split('\n');
        var builder = new StringBuilder(normalized.Length + 16);

        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            var paragraph = paragraphs[paragraphIndex];
            if (paragraph.Length is 0)
            {
                if (paragraphIndex > 0)
                {
                    builder.Append('\n');
                }

                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var currentLine = string.Empty;
            for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                var word = words[wordIndex];
                var candidate = currentLine.Length is 0 ? word : string.Concat(currentLine, " ", word);
                if (currentLine.Length is 0 || ui.CalcTextSize(candidate).X <= maxWidth)
                {
                    currentLine = candidate;
                    continue;
                }

                if (builder.Length > 0 && builder[^1] != '\n')
                {
                    builder.Append('\n');
                }

                builder.Append(currentLine);
                currentLine = word;
            }

            if (builder.Length > 0 && builder[^1] != '\n')
            {
                builder.Append('\n');
            }

            builder.Append(currentLine);
        }

        return builder.ToString();
    }

    private void RenderCompactShowcaseHero(
        UiImmediateContext ui,
        string id,
        string title,
        string description,
        UiColor accent,
        params (string Label, string Value, UiColor ValueColor)[] cards)
    {
        var visibleCardCount = Math.Clamp(cards.Length, 0, 2);
        var lineHeight = ui.GetTextLineHeight();
        const float accentWidth = 5f;
        const float paddingX = 12f;
        const float paddingY = 8f;
        const float sectionGap = 4f;
        const float cardsTopGap = 8f;
        const float cardGap = 8f;

        var origin = ui.GetCursorScreenPos();
        var width = MathF.Max(120f, ui.GetContentRegionAvail().X);
        var contentX = origin.X + accentWidth + paddingX;
        var contentWidth = MathF.Max(80f, width - (contentX - origin.X) - paddingX);
        var headingText = $"SHOWCASE · {title}";
        var headingSize = ui.CalcTextSize(headingText);
        var wrappedDescription = WrapTextToWidth(ui, description, contentWidth);
        var descriptionSize = ui.CalcTextSize(wrappedDescription);
        var cardHeight = lineHeight + 10f;
        var cardsHeight = visibleCardCount > 0 ? cardHeight + cardsTopGap : 0f;
        var contentHeight = headingSize.Y + sectionGap + descriptionSize.Y + cardsHeight;
        var heroHeight = MathF.Max(visibleCardCount > 0 ? 62f : 38f, contentHeight + (paddingY * 2f));
        var rect = new UiRect(origin.X, origin.Y, width, heroHeight);
        var drawList = ui.GetWindowDrawList();
        drawList.AddRectFilled(rect, new UiColor(0xEE121A24));
        drawList.AddRect(rect, new UiColor(0xFF314050), 6f, 1f);
        drawList.AddRectFilled(new UiRect(rect.X, rect.Y, accentWidth, rect.Height), accent);

        ui.SetCursorScreenPos(new UiVector2(contentX, rect.Y + paddingY));
        ui.TextDisabled(headingText);
        ui.SetCursorScreenPos(new UiVector2(contentX, rect.Y + paddingY + headingSize.Y + sectionGap));
        ui.TextColored(new UiColor(0xFFB7C7D9), wrappedDescription);

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
                ui.DrawTextAligned(textRect, $"{cards[i].Label}: {cards[i].Value}", cards[i].ValueColor, UiItemHorizontalAlign.Left, UiItemVerticalAlign.Center);
            }
        }

        ui.SetCursorScreenPos(origin);
        ui.Dummy(new UiVector2(width, heroHeight + 2f));
        _ = id;
    }

    private void ToggleMenuItem(UiImmediateContext ui, string label, ref bool show, string windowName)
    {
        if (ui.MenuItem(label, show))
        {
            show = !show;
            if (show) _focusWindowName = windowName;
        }
    }

    private void RenderShowcaseHero(
        UiImmediateContext ui,
        string id,
        string title,
        string description,
        UiColor accent,
        params (string Label, string Value, UiColor ValueColor)[] cards)
    {
        var visibleCardCount = Math.Clamp(cards.Length, 0, 3);
        const float innerPaddingX = 16f;
        const float innerPaddingTop = 10f;
        const float accentWidth = 6f;
        const float cardGap = 10f;
        var lineHeight = ui.GetTextLineHeight();
        var cardHeight = MathF.Max(48f, (lineHeight * 2f) + 14f);
        var heroHeight = visibleCardCount > 0 ? MathF.Max(132f, cardHeight + 80f) : 86f;

        var origin = ui.GetCursorScreenPos();
        var width = MathF.Max(220f, ui.GetContentRegionAvail().X);
        var heroRect = new UiRect(origin.X, origin.Y, width, heroHeight);
        var drawList = ui.GetWindowDrawList();

        drawList.AddRectFilled(heroRect, new UiColor(0xFF111A25));
        drawList.AddRect(heroRect, new UiColor(0xFF344A63), 8f, 1.5f);
        drawList.AddRectFilled(new UiRect(heroRect.X, heroRect.Y, accentWidth, heroRect.Height), accent);

        var contentLeft = origin.X + accentWidth + innerPaddingX;
        var contentTop = origin.Y + innerPaddingTop;

        ui.SetCursorScreenPos(new UiVector2(contentLeft, contentTop));
        ui.TextDisabled($"SHOWCASE · {title}");
        ui.PushTextWrapPos(heroRect.X + heroRect.Width - innerPaddingX);
        ui.TextColored(new UiColor(0xFFB7C7D9), description);
        ui.PopTextWrapPos();

        if (visibleCardCount > 0)
        {
            var cardsTop = heroRect.Y + heroRect.Height - cardHeight - 12f;
            var cardsLeft = contentLeft;
            var cardsWidth = heroRect.Width - (cardsLeft - heroRect.X) - innerPaddingX;
            var cardWidth = MathF.Max(96f, (cardsWidth - (cardGap * (visibleCardCount - 1))) / visibleCardCount);

            for (var i = 0; i < visibleCardCount; i++)
            {
                var cardX = cardsLeft + (i * (cardWidth + cardGap));
                var cardRect = new UiRect(cardX, cardsTop, cardWidth, cardHeight);
                var textTop = cardRect.Y + 6f;
                var labelRect = new UiRect(cardRect.X + 10f, textTop, cardRect.Width - 20f, lineHeight);
                var valueRect = new UiRect(cardRect.X + 10f, textTop + lineHeight, cardRect.Width - 20f, lineHeight);

                drawList.AddRectFilled(cardRect, new UiColor(0xFF202833));
                drawList.AddRect(cardRect, new UiColor(0xFF3A4656), 6f, 1f);
                ui.DrawTextAligned(labelRect, cards[i].Label, new UiColor(0xFF9AA8B8), UiItemHorizontalAlign.Left, UiItemVerticalAlign.Center);
                ui.DrawTextAligned(valueRect, cards[i].Value, cards[i].ValueColor, UiItemHorizontalAlign.Left, UiItemVerticalAlign.Center);
            }
        }

        ui.SetCursorScreenPos(origin);
        ui.Dummy(new UiVector2(width, heroHeight + 2f));
    }

    // ── Basic ──
    private int _clickCount;
    private bool _checkboxA = true;
    private bool _checkboxB;
    private int _flagsBitmask = 0b101;
    private int _radioValue;
    private float _progress;
    private bool _progressDir = true;

    // ── Sliders ──
    private float _sliderFloat = 0.5f;
    private int _sliderInt = 5;
    private float _sl2x = 0.3f, _sl2y = 0.7f;
    private float _sl3x = 0.1f, _sl3y = 0.5f, _sl3z = 0.9f;
    private float _sl4x = 0.2f, _sl4y = 0.4f, _sl4z = 0.6f, _sl4w = 0.8f;
    private int _slI2x = 2, _slI2y = 8;
    private int _slI3x = 1, _slI3y = 5, _slI3z = 9;
    private int _slI4x = 0, _slI4y = 3, _slI4z = 6, _slI4w = 9;
    private float _angle = 1.57f;
    private float _vSliderF = 0.5f;
    private int _vSliderI = 3;
    private readonly float[] _scalarFloats = [0.1f, 0.2f, 0.3f, 0.4f];
    private readonly int[] _scalarInts = [1, 2, 3];
    private readonly double[] _scalarDoubles = [0.5, 1.5, 2.5];

    // ── Drag ──
    private float _dragF = 0.5f;
    private int _dragI = 5;
    private float _drag2x = 0.1f, _drag2y = 0.2f;
    private float _drag3x = 0.1f, _drag3y = 0.2f, _drag3z = 0.3f;
    private float _drag4x, _drag4y, _drag4z, _drag4w;
    private int _dragI2x = 1, _dragI2y = 2;
    private int _dragI3x = 1, _dragI3y = 2, _dragI3z = 3;
    private int _dragI4x, _dragI4y, _dragI4z, _dragI4w;
    private float _dragRangeMin = 0.2f, _dragRangeMax = 0.8f;
    private int _dragIRangeMin = 2, _dragIRangeMax = 8;

    // ── Input ──
    private string _inputText = "Hello";
    private string _inputHint = "";
    private string _inputMultiline = "Line 1\nLine 2\nLine 3";
    private string _inputMultilineLong = """
Line 01: Long multiline scrolling sample
Line 02: Verify caret visibility while typing
Line 03: Verify wheel scrolling
Line 04: Verify scrollbar dragging
Line 05: Verify drag selection inside text only
Line 06: Verify PageUp and PageDown
Line 07: Verify Home and End per line
Line 08: Verify selection painting
Line 09: Verify IME composition placement
Line 10: Verify bottom lines stay reachable
Line 11: Additional content for overflow
Line 12: Additional content for overflow
Line 13: Additional content for overflow
Line 14: Additional content for overflow
Line 15: Additional content for overflow
""";
    private int _inputInt = 42;
    private int _inputI2x = 1, _inputI2y = 2;
    private int _inputI3x = 1, _inputI3y = 2, _inputI3z = 3;
    private int _inputI4x = 1, _inputI4y = 2, _inputI4z = 3, _inputI4w = 4;
    private float _inputFloat = 3.14f;
    private float _inputF2x = 1f, _inputF2y = 2f;
    private float _inputF3x = 1f, _inputF3y = 2f, _inputF3z = 3f;
    private float _inputF4x = 1f, _inputF4y = 2f, _inputF4z = 3f, _inputF4w = 4f;
    private double _inputDouble = 2.718;
    private double _inputD2x = 1.0, _inputD2y = 2.0;

    // ── Color ──
    private float _colR = 0.4f, _colG = 0.7f, _colB = 1.0f, _colA = 1.0f;
    private float _pickR = 0.8f, _pickG = 0.2f, _pickB = 0.5f, _pickA = 1.0f;

    // ── Combo/ListBox ──
    private int _comboIdx;
    private int _comboGetterIdx;
    private int _listBoxIdx;
    private int _listBoxGetterIdx;
    private int _beginComboIdx;
    private int _beginListBoxIdx;
    private string _selectionStatus = "No selection action yet.";
    private readonly string[] _items = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta"];

    // ── Selectable ──
    private readonly bool[] _selectables = [false, true, false, false];

    // ── Tree ──
    private bool _openHeaderRef = true;
    private string _navigationStatus = "No navigation action yet.";

    // ── Tab ──
    private bool _tabClosed;
    private int _tabIndex;

    // ── Table ──
    private readonly bool[] _tableChecks = new bool[5];

    // ── Popup ──
    private bool _modalOpen;
    private bool _advancedModalOpen;
    private string _advancedPopupAction = "No action chosen yet.";
    private string _contextMenuAction = "No context action chosen yet.";

    // ── Multi-select ──
    private readonly bool[] _multiSelectItems = [true, false, true, false, false, true, false, false];
    private string _multiSelectMessage = "Toggle any item to inspect attached selection metadata.";

    // ── Child ──
    private float _childScroll;

    // ── Columns ──
    private bool _columnsBorder = true;
    private float _columnsFirstWidth = 180f;

    // ── Typography ──
    private float _typographyPreviewSize = 28f;
    private float _typographyLadderMin = 10f;
    private float _typographyLadderMax = 46f;
    private float _typographyLadderStep = 6f;
    private float _typographyWrapWidth = 320f;

    // ── Layout ──
    private float _layoutBgAlpha = 0.92f;
    private float _layoutScrollTarget = 160f;
    private float _layoutItemWidth = 220f;
    private float _layoutIndentWidth = 28f;
    private float _layoutPreviewFloat = 0.35f;
    private int _layoutPreviewInt = 6;

    // ── Disabled ──
    private bool _disableSection;
    private float _disabledFloat = 0.5f;

    // ── Style ──
    private bool _customStyle;
    private float _styleItemSpacingX = 16f;
    private float _styleItemSpacingY = 10f;
    private float _styleFramePaddingX = 12f;
    private float _styleFramePaddingY = 8f;
    private float _styleWindowPadding = 18f;
    private float _stylePreviewValue = 0.42f;

    // ── Input queries / clipboard ──
    private string _clipboardSample = "Duxel clipboard sample";
    private string _clipboardEcho = "Clipboard is ready.";
    private string _lastShortcut = "Press Ctrl+K, Ctrl+L, or Ctrl+M in this window.";

    // ── Item status ──
    private bool _statusToggle = true;
    private bool _statusSelectableA = true;
    private bool _statusSelectableB;
    private string _statusInput = "Edit this field to inspect activation / edit state.";
    private string _itemStatusMessage = "Hover, click, toggle, or edit the controls below.";

    // ── Layers / animation ──
    private bool _layerStaticCache = true;
    private bool _layerTextureCache;
    private bool _layerExpanded = true;
    private float _layerOpacity = 1f;
    private float _layerTranslationX;

    // ── Plot data ──
    private readonly float[] _plotSin = new float[64];
    private readonly float[] _plotHist = [0.2f, 0.8f, 0.4f, 0.9f, 0.1f, 0.6f, 0.3f, 0.7f];

    // ── Drawing ──
    private float _drawThickness = 2f;
    private int _drawSegments = 24;

    // ── Image ──
    private int _imgBtnClicks;
    private AnimatedUiImagePlayer? _imgPlayer;
    private string? _imgLoadError;
    private bool _imgLoadAttempted;
    private float _imgZoom = 1f;
    private float _imgRotation;
    private float _imgAlpha = 1f;
    private float _imgBrightness = 1f;
    private float _imgContrast = 1f;
    private int _imgPixelate = 1;
    private bool _imgGrayscale;
    private bool _imgInvert;

    // ── DragDrop ──
    private string _dragDropSource = "Drag me!";
        private string _dragDropResult = "(none)";

    // ── ListClipper ──
    private int _clipperItemCount = 10000;

    // ── Settings Panel Sample ──
    private int _settingsTheme;
    private float _settingsVolume = 0.7f;
    private float _settingsBrightness = 1.0f;
    private bool _settingsNotifications = true;
    private bool _settingsAutoSave = true;
    private bool _settingsVSync;
    private int _settingsLanguage;
    private int _settingsQuality = 2;
    private int _settingsTab;
    private readonly string[] _themeNames = ["Dark", "Light", "Blue", "Green"];
    private readonly string[] _languageNames = ["English", "한국어", "日本語", "中文", "Deutsch"];
    private readonly string[] _qualityNames = ["Low", "Medium", "High", "Ultra"];

    // ── Profile Editor Sample ──
    private string _profileName = "John Doe";
    private string _profileEmail = "john@example.com";
    private string _profileBio = "Software developer\nLoves coding and coffee.";
    private int _profileRole;
    private float _profileAvatarR = 0.3f, _profileAvatarG = 0.6f, _profileAvatarB = 0.9f;
    private bool _profilePublic = true;
    private bool _profileNewsletter;
    private readonly string[] _roleNames = ["Viewer", "Editor", "Admin", "Owner"];

    // -- Custom widget sample --
    private readonly MarkdownEditorWidget _markdownEditor = new("markdown-editor", "Source")
    {
        Height = 420f,
        MaxLength = 32_768,
        Text = SampleMarkdown,
    };
    private readonly MarkdownViewerWidget _markdownViewer = new("markdown-viewer")
    {
        Height = 420f,
        Markdown = SampleMarkdown,
    };

    // ── File Browser Sample ──
    private int _fileBrowserSelected = -1;
    private readonly bool[] _fileBrowserExpanded = new bool[5];
    private readonly string[] _fileBrowserFolders = ["Documents", "Pictures", "Music", "Videos", "Projects"];
    private readonly string[][] _fileBrowserFiles =
    [
        ["report.docx", "notes.txt", "budget.xlsx", "readme.md"],
        ["photo1.png", "photo2.jpg", "screenshot.bmp"],
        ["song1.mp3", "song2.wav", "playlist.m3u"],
        ["clip1.mp4", "clip2.avi"],
        ["app.cs", "main.py", "index.html", "style.css", "build.sh"]
    ];
    private readonly string[][] _fileBrowserSizes =
    [
        ["245 KB", "12 KB", "89 KB", "4 KB"],
        ["1.2 MB", "890 KB", "2.4 MB"],
        ["4.5 MB", "12 MB", "1 KB"],
        ["120 MB", "85 MB"],
        ["8 KB", "3 KB", "5 KB", "2 KB", "1 KB"]
    ];

    // ── Log Viewer Sample ──
    private string _logFilter = "";
    private bool _logAutoScroll = true;
    private bool _logShowInfo = true;
    private bool _logShowWarn = true;
    private bool _logShowError = true;
    private readonly List<(string Level, string Message, UiColor Color)> _logEntries = [];

    private const string SampleMarkdown = """
# Markdown Studio

This custom widget sample edits markdown on the left and renders a preview with interactive checklists, hoverable links, and copyable code blocks on the right.

## Supported blocks

- Headings
- Paragraphs with [links](https://learn.microsoft.com/dotnet/) and `inline code`
- Bullet lists
- Nested ordered lists
- Tables
- Code fences
- Quotes
- Checklists

### Nested ordered list

1. Plan the widget contract
    1. Keep the widget instance-based
    2. Pass `ui` into `Render`
2. Extend the viewer renderer
    1. Add table support
    2. Add link hover and copy actions

### Table

| Feature | Status | Notes |
| --- | --- | --- |
| Checklist toggle | Ready | Preview state is interactive |
| Code copy | Ready | Uses clipboard integration |
| Tables | Ready | Border and row background enabled |

> Duxel custom widgets are instance-based objects that receive `ui` and compose their own behavior.

### Checklist

- [x] Parse markdown into blocks
- [x] Render a live preview
- [ ] Add more markdown syntax later

### Code

```csharp
var widget = new MarkdownViewerWidget("preview")
{
    Markdown = "# Hello from Duxel"
};
```
""";

    // ── Misc ──
    private int _frameCounter;
    private readonly UiFpsCounter _fpsCounter = new(0.5d);
    private float _fps;

    public AllFeaturesScreen()
    {
        for (var i = 0; i < _plotSin.Length; i++)
        {
            _plotSin[i] = MathF.Sin(i * 0.2f) * 0.5f + 0.5f;
        }

        // Initialize log entries
        var levels = new[] { "INFO", "WARN", "ERROR" };
        var colors = new[] { new UiColor(0xFFCCCCCC), new UiColor(0xFFFFCC44), new UiColor(0xFFFF4444) };
        var messages = new[]
        {
            "Application started", "Loading configuration...", "Config loaded successfully",
            "Connecting to database...", "Connection timeout, retrying...", "Database connected",
            "Loading user data...", "Cache miss for user #1042", "User data loaded (234 records)",
            "Starting background scheduler", "Disk space low (< 10%)", "Scheduled task completed",
            "HTTP request to /api/data", "Response 200 OK (45ms)", "Rate limit approaching (80%)",
            "Processing batch job #7", "Unexpected null reference in BatchProcessor.Run()",
            "Batch job completed with warnings", "Memory usage: 512 MB", "GC collection triggered",
            "File not found: config.bak", "Retrying file operation...", "Backup created successfully",
            "Session expired for user #88", "New session created", "Authentication successful",
            "WebSocket connected", "Message queue depth: 142", "Failed to parse JSON payload",
            "Reconnecting to message broker..."
        };
        for (var i = 0; i < 200; i++)
        {
            var lvlIdx = i % 10 == 9 ? 2 : i % 5 == 3 ? 1 : 0;
            var msg = messages[i % messages.Length];
            _logEntries.Add((levels[lvlIdx], $"[{i:D4}] {msg}", colors[lvlIdx]));
        }
    }

    public override void Render(UiImmediateContext ui)
    {
        _frameCounter++;
        UpdateFps();

        // Animate progress bar
        if (_progressDir) _progress += 0.005f;
        else _progress -= 0.005f;
        if (_progress >= 1f) { _progress = 1f; _progressDir = false; }
        if (_progress <= 0f) { _progress = 0f; _progressDir = true; }

        RenderMainMenuBar(ui);

        // Widget windows — sync open state with toggle flags
        if (_showText) { ui.SetWindowOpen(TextWindowTitle, true); RenderTextWidget(ui); _showText = ui.GetWindowOpen(TextWindowTitle); }
        if (_showTypography) { ui.SetWindowOpen(TypographyWindowTitle, true); RenderTypographyWidget(ui); _showTypography = ui.GetWindowOpen(TypographyWindowTitle); }
        if (_showButton) { ui.SetWindowOpen(ButtonWindowTitle, true); RenderButtonWidget(ui); _showButton = ui.GetWindowOpen(ButtonWindowTitle); }
        if (_showCheckbox) { ui.SetWindowOpen(CheckboxWindowTitle, true); RenderCheckboxWidget(ui); _showCheckbox = ui.GetWindowOpen(CheckboxWindowTitle); }
        if (_showRadioButton) { ui.SetWindowOpen(RadioWindowTitle, true); RenderRadioButtonWidget(ui); _showRadioButton = ui.GetWindowOpen(RadioWindowTitle); }
        if (_showProgressBar) { ui.SetWindowOpen(ProgressWindowTitle, true); RenderProgressBarWidget(ui); _showProgressBar = ui.GetWindowOpen(ProgressWindowTitle); }
        if (_showSlider) { ui.SetWindowOpen(SliderWindowTitle, true); RenderSliderWidget(ui); _showSlider = ui.GetWindowOpen(SliderWindowTitle); }
        if (_showDrag) { ui.SetWindowOpen(DragWindowTitle, true); RenderDragWidget(ui); _showDrag = ui.GetWindowOpen(DragWindowTitle); }
        if (_showInput) { ui.SetWindowOpen(InputWindowTitle, true); RenderInputWidget(ui); _showInput = ui.GetWindowOpen(InputWindowTitle); }
        if (_showColor) { ui.SetWindowOpen(ColorWindowTitle, true); RenderColorWidget(ui); _showColor = ui.GetWindowOpen(ColorWindowTitle); }
        if (_showCombo) { ui.SetWindowOpen(ComboWindowTitle, true); RenderComboWidget(ui); _showCombo = ui.GetWindowOpen(ComboWindowTitle); }
        if (_showListBox) { ui.SetWindowOpen(ListBoxWindowTitle, true); RenderListBoxWidget(ui); _showListBox = ui.GetWindowOpen(ListBoxWindowTitle); }
        if (_showSelectable) { ui.SetWindowOpen(SelectableWindowTitle, true); RenderSelectableWidget(ui); _showSelectable = ui.GetWindowOpen(SelectableWindowTitle); }
        if (_showTree) { ui.SetWindowOpen(TreeWindowTitle, true); RenderTreeWidget(ui); _showTree = ui.GetWindowOpen(TreeWindowTitle); }
        if (_showTab) { ui.SetWindowOpen(TabWindowTitle, true); RenderTabWidget(ui); _showTab = ui.GetWindowOpen(TabWindowTitle); }
        if (_showTable) { ui.SetWindowOpen(TableWindowTitle, true); RenderTableWidget(ui); _showTable = ui.GetWindowOpen(TableWindowTitle); }
        if (_showPopup) { ui.SetWindowOpen(PopupWindowTitle, true); RenderPopupWidget(ui); _showPopup = ui.GetWindowOpen(PopupWindowTitle); }
        if (_showAdvancedPopup) { ui.SetWindowOpen(AdvancedPopupWindowTitle, true); RenderAdvancedPopupWidget(ui); _showAdvancedPopup = ui.GetWindowOpen(AdvancedPopupWindowTitle); }
        if (_showTooltip) { ui.SetWindowOpen(TooltipWindowTitle, true); RenderTooltipWidget(ui); _showTooltip = ui.GetWindowOpen(TooltipWindowTitle); }
        if (_showContextMenu) { ui.SetWindowOpen(ContextMenuWindowTitle, true); RenderContextMenuWidget(ui); _showContextMenu = ui.GetWindowOpen(ContextMenuWindowTitle); }
        if (_showChild) { ui.SetWindowOpen(ChildWindowTitle, true); RenderChildWidget(ui); _showChild = ui.GetWindowOpen(ChildWindowTitle); }
        if (_showLayout) { ui.SetWindowOpen(LayoutWindowTitle, true); RenderLayoutWidget(ui); _showLayout = ui.GetWindowOpen(LayoutWindowTitle); }
        if (_showColumns) { ui.SetWindowOpen(ColumnsWindowTitle, true); RenderColumnsWidget(ui); _showColumns = ui.GetWindowOpen(ColumnsWindowTitle); }
        if (_showInputQueries) { ui.SetWindowOpen(InputQueriesWindowTitle, true); RenderInputQueriesWidget(ui); _showInputQueries = ui.GetWindowOpen(InputQueriesWindowTitle); }
        if (_showItemStatus) { ui.SetWindowOpen(ItemStatusWindowTitle, true); RenderItemStatusWidget(ui); _showItemStatus = ui.GetWindowOpen(ItemStatusWindowTitle); }
        if (_showMultiSelect) { ui.SetWindowOpen(MultiSelectWindowTitle, true); RenderMultiSelectWidget(ui); _showMultiSelect = ui.GetWindowOpen(MultiSelectWindowTitle); }
        if (_showLayersAnimation) { ui.SetWindowOpen(LayersWindowTitle, true); RenderLayersAnimationWidget(ui); _showLayersAnimation = ui.GetWindowOpen(LayersWindowTitle); }
        if (_showDrawing) { ui.SetWindowOpen(DrawingWindowTitle, true); RenderDrawingPrimitives(ui); _showDrawing = ui.GetWindowOpen(DrawingWindowTitle); }
        if (_showImage) { ui.SetWindowOpen(ImageWindowTitle, true); RenderImageWidgets(ui); _showImage = ui.GetWindowOpen(ImageWindowTitle); }
        if (_showDragDrop) { ui.SetWindowOpen(DragDropWindowTitle, true); RenderDragDrop(ui); _showDragDrop = ui.GetWindowOpen(DragDropWindowTitle); }
        if (_showListClipper) { ui.SetWindowOpen(ListClipperWindowTitle, true); RenderListClipper(ui); _showListClipper = ui.GetWindowOpen(ListClipperWindowTitle); }
        if (_showStyle) { ui.SetWindowOpen(StyleWindowTitle, true); RenderStyleWidget(ui); _showStyle = ui.GetWindowOpen(StyleWindowTitle); }

        // Sample windows
        if (_showSettingsPanel) { ui.SetWindowOpen(SettingsWindowTitle, true); RenderSettingsPanel(ui); _showSettingsPanel = ui.GetWindowOpen(SettingsWindowTitle); }
        if (_showProfileEditor) { ui.SetWindowOpen(ProfileEditorWindowTitle, true); RenderProfileEditor(ui); _showProfileEditor = ui.GetWindowOpen(ProfileEditorWindowTitle); }
        if (_showFileBrowser) { ui.SetWindowOpen(FileBrowserWindowTitle, true); RenderFileBrowser(ui); _showFileBrowser = ui.GetWindowOpen(FileBrowserWindowTitle); }
        if (_showLogViewer) { ui.SetWindowOpen(LogViewerWindowTitle, true); RenderLogViewer(ui); _showLogViewer = ui.GetWindowOpen(LogViewerWindowTitle); }
        if (_showMarkdownStudio) { ui.SetWindowOpen(MarkdownStudioWindowTitle, true); RenderMarkdownStudio(ui); _showMarkdownStudio = ui.GetWindowOpen(MarkdownStudioWindowTitle); }

        RenderFpsOverlay(ui);
    }

    // ─────────────────────────── Main Menu Bar ───────────────────────────
    private void RenderMainMenuBar(UiImmediateContext ui)
    {
        if (ui.BeginMainMenuBar())
        {
            if (ui.BeginMenu("Component Gallery"))
            {
                if (ui.BeginMenu("Content & Typography"))
                {
                    ToggleMenuItem(ui, TextWindowTitle, ref _showText, TextWindowTitle);
                    ToggleMenuItem(ui, TypographyWindowTitle, ref _showTypography, TypographyWindowTitle);
                    ui.EndMenu();
                }
                if (ui.BeginMenu("Actions & Toggles"))
                {
                    ToggleMenuItem(ui, ButtonWindowTitle, ref _showButton, ButtonWindowTitle);
                    ToggleMenuItem(ui, CheckboxWindowTitle, ref _showCheckbox, CheckboxWindowTitle);
                    ToggleMenuItem(ui, RadioWindowTitle, ref _showRadioButton, RadioWindowTitle);
                    ui.EndMenu();
                }
                if (ui.BeginMenu("Editors & Pickers"))
                {
                    ToggleMenuItem(ui, SliderWindowTitle, ref _showSlider, SliderWindowTitle);
                    ToggleMenuItem(ui, DragWindowTitle, ref _showDrag, DragWindowTitle);
                    ToggleMenuItem(ui, InputWindowTitle, ref _showInput, InputWindowTitle);
                    ToggleMenuItem(ui, ColorWindowTitle, ref _showColor, ColorWindowTitle);
                    ui.EndMenu();
                }
                if (ui.BeginMenu("Selection & Lists"))
                {
                    ToggleMenuItem(ui, ComboWindowTitle, ref _showCombo, ComboWindowTitle);
                    ToggleMenuItem(ui, ListBoxWindowTitle, ref _showListBox, ListBoxWindowTitle);
                    ToggleMenuItem(ui, SelectableWindowTitle, ref _showSelectable, SelectableWindowTitle);
                    ui.EndMenu();
                }
                if (ui.BeginMenu("Navigation & Data"))
                {
                    ToggleMenuItem(ui, TreeWindowTitle, ref _showTree, TreeWindowTitle);
                    ToggleMenuItem(ui, TabWindowTitle, ref _showTab, TabWindowTitle);
                    ToggleMenuItem(ui, TableWindowTitle, ref _showTable, TableWindowTitle);
                    ui.EndMenu();
                }
                if (ui.BeginMenu("Layout & Panels"))
                {
                    ToggleMenuItem(ui, ChildWindowTitle, ref _showChild, ChildWindowTitle);
                    ToggleMenuItem(ui, LayoutWindowTitle, ref _showLayout, LayoutWindowTitle);
                    ToggleMenuItem(ui, ColumnsWindowTitle, ref _showColumns, ColumnsWindowTitle);
                    ui.EndMenu();
                }
                if (ui.BeginMenu("Feedback & Diagnostics"))
                {
                    ToggleMenuItem(ui, ProgressWindowTitle, ref _showProgressBar, ProgressWindowTitle);
                    ToggleMenuItem(ui, PopupWindowTitle, ref _showPopup, PopupWindowTitle);
                    ToggleMenuItem(ui, AdvancedPopupWindowTitle, ref _showAdvancedPopup, AdvancedPopupWindowTitle);
                    ToggleMenuItem(ui, TooltipWindowTitle, ref _showTooltip, TooltipWindowTitle);
                    ToggleMenuItem(ui, ContextMenuWindowTitle, ref _showContextMenu, ContextMenuWindowTitle);
                    ToggleMenuItem(ui, InputQueriesWindowTitle, ref _showInputQueries, InputQueriesWindowTitle);
                    ToggleMenuItem(ui, ItemStatusWindowTitle, ref _showItemStatus, ItemStatusWindowTitle);
                    ui.EndMenu();
                }
                if (ui.BeginMenu("Graphics & Media"))
                {
                    ToggleMenuItem(ui, DrawingWindowTitle, ref _showDrawing, DrawingWindowTitle);
                    ToggleMenuItem(ui, ImageWindowTitle, ref _showImage, ImageWindowTitle);
                    ui.EndMenu();
                }
                if (ui.BeginMenu("Advanced Patterns"))
                {
                    ToggleMenuItem(ui, MultiSelectWindowTitle, ref _showMultiSelect, MultiSelectWindowTitle);
                    ToggleMenuItem(ui, LayersWindowTitle, ref _showLayersAnimation, LayersWindowTitle);
                    ToggleMenuItem(ui, DragDropWindowTitle, ref _showDragDrop, DragDropWindowTitle);
                    ToggleMenuItem(ui, ListClipperWindowTitle, ref _showListClipper, ListClipperWindowTitle);
                    ToggleMenuItem(ui, StyleWindowTitle, ref _showStyle, StyleWindowTitle);
                    ui.EndMenu();
                }
                ui.EndMenu();
            }
            if (ui.BeginMenu("Applied Samples"))
            {
                ToggleMenuItem(ui, SettingsWindowTitle, ref _showSettingsPanel, SettingsWindowTitle);
                ToggleMenuItem(ui, ProfileEditorWindowTitle, ref _showProfileEditor, ProfileEditorWindowTitle);
                ToggleMenuItem(ui, FileBrowserWindowTitle, ref _showFileBrowser, FileBrowserWindowTitle);
                ToggleMenuItem(ui, LogViewerWindowTitle, ref _showLogViewer, LogViewerWindowTitle);
                ToggleMenuItem(ui, MarkdownStudioWindowTitle, ref _showMarkdownStudio, MarkdownStudioWindowTitle);
                ui.EndMenu();
            }
            ui.EndMainMenuBar();
        }
    }

    // ─────────────────────────── Text ───────────────────────────
    private void RenderTextWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, TextWindowTitle, default);
        ui.BeginWindow(TextWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_text_links",
            TextWindowTitle,
            "Foundational text, wrapped content, lightweight status rows, and inline link actions in one compact surface.",
            new UiColor(0xFF5DB3FF),
            ("Coverage", "Labels · wrapped text · links", new UiColor(0xFF8DE1A6)),
            ("Tone", "Documentation-friendly", new UiColor(0xFFFFD479)),
            ("Status", "Clipboard-ready", new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Core Text APIs");
        ui.Text("Text: Normal");
        ui.TextV("TextV: frame={0}", _frameCounter);
        ui.TextColored(new UiColor(0xFF44BBFF), "TextColored: Blue");
        ui.TextColoredV(new UiColor(0xFF44FF88), "TextColoredV: {0}", "Green");
        ui.TextDisabled("TextDisabled");
        ui.TextDisabledV("TextDisabledV: {0}", "dimmed");
        ui.TextWrapped("TextWrapped: The quick brown fox jumps over the lazy dog. This text will wrap within the window.");
        ui.TextWrappedV("TextWrappedV: val={0:0.0}", _progress);
        ui.TextUnformatted("TextUnformatted");
        ui.LabelText("LabelText", "value");
        ui.LabelTextV("LabelTextV", "{0}", _clickCount);
        ui.BulletText("BulletText");
        ui.BulletTextV("BulletTextV: {0}", "item");

        ui.SeparatorText("Utility Text Rows");
        if (ui.BeginChild("text_kv_preview", new UiVector2(0f, 92f), true))
        {
            var origin = ui.GetCursorScreenPos();
            var width = MathF.Max(220f, ui.GetContentRegionAvail().X);
            ui.DrawKeyValueRow(new UiRect(origin.X, origin.Y, width, 24f), "FPS", $"{_fps:0}", selected: true, accent: new UiColor(0xFF39AFFF));
            ui.DrawKeyValueRow(new UiRect(origin.X, origin.Y + 28f, width, 24f), "Input", "IME + multiline + clipboard ready", valueColor: new UiColor(0xFF8DE1A6));
            ui.DrawKeyValueRow(new UiRect(origin.X, origin.Y + 56f, width, 24f), "Theme", _customStyle ? "Custom accent" : "Default", valueColor: new UiColor(0xFFFFD479));
            ui.Dummy(new UiVector2(width, 84f));
        }
        ui.EndChild();

        ui.SeparatorText("Links");
        ui.TextWrapped("Inline links are useful for docs, package pages, or issue trackers embedded in your tooling UI.");
        _ = ui.TextLinkOpenURL("Open .NET Learn", "https://learn.microsoft.com/dotnet/");
        ui.SameLine();
        ui.TextDisabled("|");
        ui.SameLine();
        _ = ui.TextLinkOpenURL("Open Duxel repo folder", "file:///P:/MyWorks/Duxel");

        ui.EndWindow();
    }

    private void RenderTypographyWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, TypographyWindowTitle, new UiVector2(560f, 640f));
        ui.BeginWindow(TypographyWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_typography",
            TypographyWindowTitle,
            "Scale, hierarchy, wrapping, and alignment patterns for polished editorial UI instead of plain text dumps.",
            new UiColor(0xFF7EC8FF),
            ("Headline", $"{_typographyPreviewSize:0}px live preview", new UiColor(0xFF7EC8FF)),
            ("Ladder", $"{_typographyLadderMin:0}-{_typographyLadderMax:0}px", new UiColor(0xFFFFD479)),
            ("Wrap", $"{_typographyWrapWidth:0}px column", new UiColor(0xFF8DE1A6)));

        ui.SeparatorText("Live Preview");
        ui.SliderFloat("Preview Size", ref _typographyPreviewSize, 8f, 72f, 0f, "0");
        ui.SliderFloat("Wrap Width", ref _typographyWrapWidth, 160f, 520f, 0f, "0");

        ui.PushFontSize(_typographyPreviewSize + 10f);
        ui.Text("Display headline for dashboards and hero sections");
        ui.PopFontSize();

        ui.PushFontSize(_typographyPreviewSize + 2f);
        ui.TextColored(new UiColor(0xFF7EC8FF), "Section heading — strong hierarchy without extra widgets");
        ui.PopFontSize();

        ui.PushFontSize(_typographyPreviewSize);
        ui.Text("Primary body preview / 가나다라마바사 / 0123456789");
        ui.PopFontSize();

        ui.PushTextWrapPos(_typographyWrapWidth);
        ui.Text("PushTextWrapPos lets you constrain explanatory copy to an intentional reading width so dense windows do not become horizontal chaos goblins.");
        ui.PopTextWrapPos();

        ui.SeparatorText("Size Ladder");
        ui.SliderFloat("Min Size", ref _typographyLadderMin, 8f, 30f, 0f, "0");
        ui.SliderFloat("Max Size", ref _typographyLadderMax, 20f, 84f, 0f, "0");
        ui.SliderFloat("Step", ref _typographyLadderStep, 2f, 14f, 0f, "0");
        if (_typographyLadderMax < _typographyLadderMin)
        {
            (_typographyLadderMin, _typographyLadderMax) = (_typographyLadderMax, _typographyLadderMin);
        }

        var size = _typographyLadderMin;
        var guard = 0;
        while (size <= _typographyLadderMax && guard < 64)
        {
            ui.PushFontSize(size);
            ui.Text($"{size,5:0} px | The quick brown fox | 가나다라마바사");
            ui.PopFontSize();
            size += MathF.Max(1f, _typographyLadderStep);
            guard++;
        }

        ui.SeparatorText("Alignment Card");
        if (ui.BeginChild("typography_alignment", new UiVector2(0f, 170f), true))
        {
            var canvas = ui.BeginWindowCanvas(new UiColor(0xFF141922));
            var drawList = ui.GetWindowDrawList();
            var gap = 12f;
            var cardWidth = MathF.Max(80f, (canvas.Width - (gap * 4f)) / 3f);
            var cardHeight = 72f;
            var top = canvas.Y + 16f;

            var leftRect = new UiRect(canvas.X + gap, top, cardWidth, cardHeight);
            var centerRect = new UiRect(leftRect.X + cardWidth + gap, top, cardWidth, cardHeight);
            var rightRect = new UiRect(centerRect.X + cardWidth + gap, top, cardWidth, cardHeight);

            drawList.AddRect(leftRect, new UiColor(0xFF44607A), 6f, 1.5f);
            drawList.AddRect(centerRect, new UiColor(0xFF44607A), 6f, 1.5f);
            drawList.AddRect(rightRect, new UiColor(0xFF44607A), 6f, 1.5f);

            ui.DrawTextAligned(leftRect, "Left", new UiColor(0xFFFFFFFF), UiItemHorizontalAlign.Left, UiItemVerticalAlign.Center);
            ui.DrawTextAligned(centerRect, "Center", new UiColor(0xFFFFFFFF), UiItemHorizontalAlign.Center, UiItemVerticalAlign.Center);
            ui.DrawTextAligned(rightRect, "Right", new UiColor(0xFFFFFFFF), UiItemHorizontalAlign.Right, UiItemVerticalAlign.Center);
            ui.DrawKeyValueRow(new UiRect(canvas.X + gap, top + cardHeight + 14f, canvas.Width - gap * 2f, 28f), "API", "DrawTextAligned + DrawKeyValueRow", selected: true, accent: new UiColor(0xFF55C2FF));
            _ = ui.EndWindowCanvas();
        }
        ui.EndChild();

        ui.SeparatorText("Font Style Note");
        ui.TextWrapped("This runtime showcase demonstrates live size hierarchy and alignment. True regular / bold / italic face comparison is currently validated with the dedicated sample `samples/fba/font_style_validation_fba.cs`, which switches the startup font file per run.");
        ui.Text("dotnet run samples/fba/font_style_validation_fba.cs");
        ui.Text("./run-fba.ps1 samples/fba/font_style_validation_fba.cs -Managed");
        ui.Text("$env:DUXEL_FONT_STYLE='bold'   # regular / bold / italic");
        if (ui.SmallButton("Copy dotnet run command"))
        {
            ui.SetClipboardText("dotnet run samples/fba/font_style_validation_fba.cs");
            _clipboardEcho = "Copied font style validation dotnet command.";
        }
        ui.SameLine();
        if (ui.SmallButton("Copy run-fba command"))
        {
            ui.SetClipboardText("./run-fba.ps1 samples/fba/font_style_validation_fba.cs -Managed");
            _clipboardEcho = "Copied font style validation run-fba command.";
        }

        ui.EndWindow();
    }

    // ─────────────────────────── Button ───────────────────────────
    private void RenderButtonWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ButtonWindowTitle, default);
        ui.BeginWindow(ButtonWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_buttons",
            ButtonWindowTitle,
            "From primary calls to compact toolbar affordances, this window shows how action density changes the visual rhythm of a UI.",
            new UiColor(0xFF58A6FF),
            ("Primary", "Large CTA buttons", new UiColor(0xFF8DE1A6)),
            ("Secondary", "Toolbar micro-actions", new UiColor(0xFFFFD479)),
            ("Counter", _clickCount.ToString(), new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Core Buttons");
        if (ui.Button("Button")) _clickCount++;
        ui.SameLine();
        if (ui.Button("Sized Button", new UiVector2(120f, 30f))) _clickCount += 10;
        if (ui.SmallButton("SmallButton")) _clickCount--;
        ui.SameLine();
        ui.ArrowButton("arr_l", UiDir.Left);
        ui.SameLine();
        ui.ArrowButton("arr_r", UiDir.Right);
        ui.SameLine();
        ui.ArrowButton("arr_u", UiDir.Up);
        ui.SameLine();
        ui.ArrowButton("arr_d", UiDir.Down);
        ui.InvisibleButton("inv_btn", new UiVector2(60f, 20f));
        ui.SameLine();
        ui.Text("(InvisibleButton left)");

        ui.SeparatorText("Toolbar");
        if (ui.SmallButton("Add +5")) _clickCount += 5;
        ui.SameLine();
        if (ui.SmallButton("Subtract -5")) _clickCount -= 5;
        ui.SameLine();
        if (ui.SmallButton("Reset")) _clickCount = 0;
        ui.SameLine();
        ui.BeginDisabled(_clickCount <= 0);
        if (ui.Button("Consume Click Count", new UiVector2(150f, 0f))) _clickCount = 0;
        ui.EndDisabled();

        ui.SeparatorText("Action Cluster");
        if (ui.BeginChild("button_action_cluster", new UiVector2(0f, 92f), true))
        {
            if (ui.Button("Primary Action", new UiVector2(140f, 34f))) _clickCount += 25;
            ui.SameLine();
            if (ui.Button("Secondary Action", new UiVector2(160f, 34f))) _clickCount += 3;
            ui.TextWrapped("Use large buttons for primary flows, small buttons for toolbars, and invisible buttons when the visual surface is custom-drawn.");
        }
        ui.EndChild();

        ui.Text($"Click count: {_clickCount}");

        ui.EndWindow();
    }

    // ─────────────────────────── Checkbox ───────────────────────────
    private void RenderCheckboxWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, CheckboxWindowTitle, default);
        ui.BeginWindow(CheckboxWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_checkbox",
            CheckboxWindowTitle,
            "Binary toggles, packed bit flags, and dependent enablement rules for settings-oriented UI.",
            new UiColor(0xFF58A6FF),
            ("Primary", _checkboxA ? "On" : "Off", new UiColor(0xFF8DE1A6)),
            ("Secondary", _checkboxB ? "On" : "Off", new UiColor(0xFFFFD479)),
            ("Flags", $"0b{Convert.ToString(_flagsBitmask, 2).PadLeft(3, '0')}", new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Binary States");
        ui.Checkbox("Checkbox A", ref _checkboxA);
        ui.SameLine();
        ui.Checkbox("Checkbox B", ref _checkboxB);

        ui.SeparatorText("Bit Flags");
        ui.CheckboxFlags("Flag 0 (1<<0)", ref _flagsBitmask, 1 << 0);
        ui.CheckboxFlags("Flag 1 (1<<1)", ref _flagsBitmask, 1 << 1);
        ui.CheckboxFlags("Flag 2 (1<<2)", ref _flagsBitmask, 1 << 2);
        ui.SeparatorText("Dependent Settings");
        ui.BeginDisabled(!_checkboxA);
        ui.Checkbox("Dependent notifications", ref _checkboxB);
        ui.EndDisabled();
        ui.Value("Master Enabled", _checkboxA);
        ui.Value("Secondary Enabled", _checkboxB);
        ui.Text($"Flags bitmask: 0b{Convert.ToString(_flagsBitmask, 2).PadLeft(3, '0')}");

        ui.EndWindow();
    }

    // ─────────────────────────── RadioButton ───────────────────────────
    private void RenderRadioButtonWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, RadioWindowTitle, default);
        ui.BeginWindow(RadioWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_radio",
            RadioWindowTitle,
            "Mutually exclusive choices with a compact footprint for mode and preset selection flows.",
            new UiColor(0xFF58A6FF),
            ("Active", $"Option {_radioValue + 1}", new UiColor(0xFF8DE1A6)));

        ui.RadioButton("Radio A", ref _radioValue, 0);
        ui.SameLine();
        ui.RadioButton("Radio B", ref _radioValue, 1);
        ui.SameLine();
        ui.RadioButton("Radio C", ref _radioValue, 2);
        ui.Text($"Selected: {_radioValue}");

        ui.EndWindow();
    }

    // ─────────────────────────── ProgressBar ───────────────────────────
    private void RenderProgressBarWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ProgressWindowTitle, default);
        ui.BeginWindow(ProgressWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_progress",
            ProgressWindowTitle,
            "Animated meters, compact status readouts, and dashboard-friendly summaries that stay readable at a glance.",
            new UiColor(0xFF58A6FF),
            ("Progress", $"{_progress * 100f:0}%", new UiColor(0xFF8DE1A6)),
            ("Renderer", _showLayersAnimation ? "Enhanced preview" : "Standard preview", new UiColor(0xFFFFD479)),
            ("Health", _checkboxA ? "Ready" : "Paused", new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Animated Status Bars");
        ui.ProgressBar(_progress, new UiVector2(200f, 16f), $"{_progress * 100f:0}%");
        ui.ProgressBar(MathF.Min(1f, (_progress * 0.6f) + 0.2f), new UiVector2(260f, 12f), "Buffered stage");
        ui.ProgressBar(MathF.Abs(MathF.Sin(_frameCounter * 0.05f)), new UiVector2(160f, 8f), null);

        ui.SeparatorText("Status Summary");
        ui.Bullet();
        ui.SameLine();
        ui.Text("Bullet + SameLine");
        ui.Value("Bool", _checkboxA);
        ui.Value("Int", _clickCount);
        ui.Value("Float", _progress);
        if (ui.BeginChild("progress_status_rows", new UiVector2(0f, 88f), true))
        {
            var summaryOrigin = ui.GetCursorScreenPos();
            var summaryWidth = MathF.Max(220f, ui.GetContentRegionAvail().X - 10f);
            const float rowHeight = 22f;
            const float rowGap = 4f;
            const float keyWidth = 110f;

            ui.DrawKeyValueRow(new UiRect(summaryOrigin.X, summaryOrigin.Y, summaryWidth, rowHeight), "Build", "Ready", selected: true, accent: new UiColor(0xFF58A6FF), keyWidth: keyWidth, horizontalPadding: 8f);
            ui.DrawKeyValueRow(new UiRect(summaryOrigin.X, summaryOrigin.Y + rowHeight + rowGap, summaryWidth, rowHeight), "Assets", "Streaming", valueColor: new UiColor(0xFFFFD479), keyWidth: keyWidth, horizontalPadding: 8f);
            ui.DrawKeyValueRow(new UiRect(summaryOrigin.X, summaryOrigin.Y + ((rowHeight + rowGap) * 2f), summaryWidth, rowHeight), "Renderer", _showLayersAnimation ? "Advanced showcase" : "Basic showcase", valueColor: new UiColor(0xFF8DE1A6), keyWidth: keyWidth, horizontalPadding: 8f);
            ui.Dummy(new UiVector2(summaryWidth, (rowHeight * 3f) + (rowGap * 2f)));
        }
        ui.EndChild();

        ui.EndWindow();
    }

    // ─────────────────────────── Slider ───────────────────────────
    private void RenderSliderWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, SliderWindowTitle, default);
        ui.BeginWindow(SliderWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_slider",
            SliderWindowTitle,
            "Continuous ranges, angle controls, vector sliders, and vertical meters for tunable numeric input.",
            new UiColor(0xFF58A6FF),
            ("Float", _sliderFloat.ToString("0.00"), new UiColor(0xFF8DE1A6)),
            ("Int", _sliderInt.ToString(), new UiColor(0xFFFFD479)),
            ("Angle", _angle.ToString("0.00"), new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Float/Int Sliders");
        ui.SliderFloat("SliderFloat", ref _sliderFloat, 0f, 1f, 0.01f, "0.00");
        ui.SliderInt("SliderInt", ref _sliderInt, 0, 10);
        ui.SliderFloat2("SliderFloat2", ref _sl2x, ref _sl2y, 0f, 1f);
        ui.SliderFloat3("SliderFloat3", ref _sl3x, ref _sl3y, ref _sl3z, 0f, 1f);
        ui.SliderFloat4("SliderFloat4", ref _sl4x, ref _sl4y, ref _sl4z, ref _sl4w, 0f, 1f);
        ui.SliderInt2("SliderInt2", ref _slI2x, ref _slI2y, 0, 10);
        ui.SliderInt3("SliderInt3", ref _slI3x, ref _slI3y, ref _slI3z, 0, 10);
        ui.SliderInt4("SliderInt4", ref _slI4x, ref _slI4y, ref _slI4z, ref _slI4w, 0, 10);

        ui.SeparatorText("Angle & Scalar");
        ui.SliderAngle("SliderAngle", ref _angle, -180f, 180f);
        ui.SliderScalar("SliderScalar(f)", ref _sliderFloat, 0f, 1f, 0f, "0.00");
        ui.SliderScalarN("SliderScalarN(f[])", _scalarFloats, 0f, 1f, 0f, "0.00");
        ui.SliderScalarN("SliderScalarN(i[])", _scalarInts, 0, 10);
        ui.SliderScalarN("SliderScalarN(d[])", _scalarDoubles, 0.0, 3.0, 0.0, "0.00");

        ui.SeparatorText("Vertical Sliders");
        ui.VSliderFloat("VSliderFloat", new UiVector2(24f, 100f), ref _vSliderF, 0f, 1f);
        ui.SameLine();
        ui.VSliderInt("VSliderInt", new UiVector2(24f, 100f), ref _vSliderI, 0, 10);

        ui.EndWindow();
    }

    // ─────────────────────────── Drag ───────────────────────────
    private void RenderDragWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, DragWindowTitle, default);
        ui.BeginWindow(DragWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_drag",
            DragWindowTitle,
            "Drag editors trade slider rails for direct value scrubbing, which is often better for dense inspector tooling.",
            new UiColor(0xFF58A6FF),
            ("Float", _dragF.ToString("0.00"), new UiColor(0xFF8DE1A6)),
            ("Int", _dragI.ToString(), new UiColor(0xFFFFD479)),
            ("Range", $"{_dragRangeMin:0.00}-{_dragRangeMax:0.00}", new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("DragFloat/Int");
        ui.DragFloat("DragFloat", ref _dragF, 0.01f, 0f, 1f, "0.00");
        ui.DragInt("DragInt", ref _dragI, 0.2f, 0, 20);
        ui.DragFloat2("DragFloat2", ref _drag2x, ref _drag2y, 0.01f, 0f, 1f, "0.00");
        ui.DragFloat3("DragFloat3", ref _drag3x, ref _drag3y, ref _drag3z, 0.01f, 0f, 1f, "0.00");
        ui.DragFloat4("DragFloat4", ref _drag4x, ref _drag4y, ref _drag4z, ref _drag4w, 0.01f, 0f, 1f, "0.00");
        ui.DragInt2("DragInt2", ref _dragI2x, ref _dragI2y, 0.2f, 0, 10);
        ui.DragInt3("DragInt3", ref _dragI3x, ref _dragI3y, ref _dragI3z, 0.2f, 0, 10);
        ui.DragInt4("DragInt4", ref _dragI4x, ref _dragI4y, ref _dragI4z, ref _dragI4w, 0.2f, 0, 10);

        ui.SeparatorText("DragRange");
        ui.DragFloatRange2("DragFloatRange2", ref _dragRangeMin, ref _dragRangeMax, 0.005f, 0f, 1f, "Min: 0.00", "Max: 0.00");
        ui.DragIntRange2("DragIntRange2", ref _dragIRangeMin, ref _dragIRangeMax, 0.2f, 0, 10);

        ui.SeparatorText("DragScalar");
        ui.DragScalar("DragScalar(f)", ref _dragF, 0.01f, 0f, 1f, "0.00");
        ui.DragScalarN("DragScalarN(f[])", _scalarFloats, 0.01f, 0f, 1f, "0.00");
        ui.DragScalarN("DragScalarN(i[])", _scalarInts, 0.2f, 0, 10);
        ui.DragScalarN("DragScalarN(d[])", _scalarDoubles, 0.01f, 0.0, 3.0, "0.00");

        ui.EndWindow();
    }

    // ─────────────────────────── Input ───────────────────────────
    private void RenderInputWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, InputWindowTitle, default);
        ui.BeginWindow(InputWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_input",
            InputWindowTitle,
            "Single-line, multiline, integer, floating-point, and scalar editors gathered into one focused editing lab.",
            new UiColor(0xFF58A6FF),
            ("Text", _inputText, new UiColor(0xFF8DE1A6)),
            ("Lines", _inputMultiline.Split('\n').Length.ToString(), new UiColor(0xFFFFD479)),
            ("Int", _inputInt.ToString(), new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Text Input");
        ui.InputText("InputText", ref _inputText, 64);
        ui.InputTextWithHint("WithHint", "type here...", ref _inputHint, 64);
        ui.InputTextMultiline("Multiline", ref _inputMultiline, 512, 3);
        ui.InputTextMultiline("Multiline Large", ref _inputMultilineLong, 4096, 8);

        ui.SeparatorText("Numeric Input");
        ui.InputInt("InputInt", ref _inputInt);
        ui.InputInt2("InputInt2", ref _inputI2x, ref _inputI2y);
        ui.InputInt3("InputInt3", ref _inputI3x, ref _inputI3y, ref _inputI3z);
        ui.InputInt4("InputInt4", ref _inputI4x, ref _inputI4y, ref _inputI4z, ref _inputI4w);
        ui.InputFloat("InputFloat", ref _inputFloat, "0.00");
        ui.InputFloat2("InputFloat2", ref _inputF2x, ref _inputF2y, "0.00");
        ui.InputFloat3("InputFloat3", ref _inputF3x, ref _inputF3y, ref _inputF3z, "0.00");
        ui.InputFloat4("InputFloat4", ref _inputF4x, ref _inputF4y, ref _inputF4z, ref _inputF4w, "0.00");
        ui.InputDouble("InputDouble", ref _inputDouble, "0.000");
        ui.InputDouble2("InputDouble2", ref _inputD2x, ref _inputD2y, "0.00");

        ui.SeparatorText("InputScalar");
        ui.InputScalar("InputScalar(i)", ref _inputInt);
        ui.InputScalar("InputScalar(f)", ref _inputFloat, "0.00");
        ui.InputScalar("InputScalar(d)", ref _inputDouble, "0.000");
        ui.InputScalarN("InputScalarN(i[])", _scalarInts);
        ui.InputScalarN("InputScalarN(f[])", _scalarFloats, "0.00");
        ui.InputScalarN("InputScalarN(d[])", _scalarDoubles, "0.00");

        ui.EndWindow();
    }

    // ─────────────────────────── Color ───────────────────────────
    private void RenderColorWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ColorWindowTitle, default);
        ui.BeginWindow(ColorWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_color",
            ColorWindowTitle,
            "Quick edits, full pickers, and button previews for accent, theme, and asset color authoring.",
            new UiColor(0xFF58A6FF),
            ("Edit", $"R{_colR:0.00} G{_colG:0.00}", new UiColor(0xFF8DE1A6)),
            ("Pick", $"R{_pickR:0.00} G{_pickG:0.00}", new UiColor(0xFFFFD479)));

        ui.SeparatorText("ColorEdit");
        ui.ColorEdit3("ColorEdit3", ref _colR, ref _colG, ref _colB);
        ui.ColorEdit4("ColorEdit4", ref _colR, ref _colG, ref _colB, ref _colA);

        ui.SeparatorText("ColorPicker");
        ui.ColorPicker3("ColorPicker3", ref _pickR, ref _pickG, ref _pickB);
        ui.ColorPicker4("ColorPicker4", ref _pickR, ref _pickG, ref _pickB, ref _pickA);

        ui.SeparatorText("ColorButton");
        var btnColor = new UiColor(
            ((uint)(_colB * 255f) & 0xFF) |
            (((uint)(_colG * 255f) & 0xFF) << 8) |
            (((uint)(_colR * 255f) & 0xFF) << 16) |
            (((uint)(_colA * 255f) & 0xFF) << 24));
        ui.ColorButton("color_btn", btnColor, new UiVector2(32f, 32f));
        ui.SameLine();
        ui.Text("ColorButton");

        ui.EndWindow();
    }

    // ─────────────────────────── Combo ───────────────────────────
    private void RenderComboWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ComboWindowTitle, default);
        ui.BeginWindow(ComboWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_combo",
            ComboWindowTitle,
            "Compact selection widgets for presets, quick picks, and command choice surfaces.",
            new UiColor(0xFF58A6FF),
            ("Combo", _items[_comboIdx], new UiColor(0xFF8DE1A6)),
            ("Getter", _items[_comboGetterIdx], new UiColor(0xFFFFD479)),
            ("BeginCombo", _items[_beginComboIdx], new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Basic Combo");
        ui.Text("Combo");
        var previousComboIdx = _comboIdx;
        ui.Combo(ref _comboIdx, _items, 5, "Combo");
        if (previousComboIdx != _comboIdx)
        {
            _selectionStatus = $"Combo changed to {_items[_comboIdx]}";
        }

        ui.SeparatorText("Getter Combo");
        ui.Text("Combo(getter)");
        var previousGetterIdx = _comboGetterIdx;
        ui.Combo(ref _comboGetterIdx, _items.Length, i => _items[i], 5, "Combo(getter)");
        if (previousGetterIdx != _comboGetterIdx)
        {
            _selectionStatus = $"Getter combo changed to {_items[_comboGetterIdx]}";
        }

        _beginComboIdx = Math.Clamp(_beginComboIdx, 0, _items.Length - 1);
        ui.SeparatorText("BeginCombo");
        ui.Text("BeginCombo");
        if (ui.BeginCombo(_items[_beginComboIdx], 5, "BeginCombo"))
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (ui.Selectable(_items[i], _beginComboIdx == i))
                {
                    _beginComboIdx = i;
                    _selectionStatus = $"BeginCombo picked {_items[i]}";
                }
            }
            ui.EndCombo();
        }

        ui.SeparatorText("Selection Summary");
        if (ui.BeginChild("combo_summary", new UiVector2(0f, 96f), true))
        {
            ui.DrawKeyValueRow(new UiRect(ui.GetCursorScreenPos().X, ui.GetCursorScreenPos().Y, MathF.Max(220f, ui.GetContentRegionAvail().X), 24f), "Combo", _items[_comboIdx], selected: true, accent: new UiColor(0xFF58A6FF));
            var y = ui.GetCursorScreenPos().Y + 28f;
            var x = ui.GetCursorScreenPos().X;
            var width = MathF.Max(220f, ui.GetContentRegionAvail().X);
            ui.DrawKeyValueRow(new UiRect(x, y, width, 24f), "Getter", _items[_comboGetterIdx], valueColor: new UiColor(0xFFFFD479));
            ui.DrawKeyValueRow(new UiRect(x, y + 28f, width, 24f), "BeginCombo", _items[_beginComboIdx], valueColor: new UiColor(0xFF8DE1A6));
            ui.Dummy(new UiVector2(width, 84f));
        }
        ui.EndChild();
        ui.Text($"Last selection event: {_selectionStatus}");

        ui.EndWindow();
    }

    // ─────────────────────────── ListBox ───────────────────────────
    private void RenderListBoxWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ListBoxWindowTitle, default);
        ui.BeginWindow(ListBoxWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_listbox",
            ListBoxWindowTitle,
            "List boxes are ideal when the visible set matters as much as the final selection itself.",
            new UiColor(0xFF58A6FF),
            ("ListBox", _items[_listBoxIdx], new UiColor(0xFF8DE1A6)),
            ("Getter", _items[_listBoxGetterIdx], new UiColor(0xFFFFD479)),
            ("BeginListBox", _items[_beginListBoxIdx], new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Basic ListBox");
        ui.Text("ListBox");
        var previousListBoxIdx = _listBoxIdx;
        ui.ListBox(ref _listBoxIdx, _items, 4, "ListBox##lb");
        if (previousListBoxIdx != _listBoxIdx)
        {
            _selectionStatus = $"ListBox changed to {_items[_listBoxIdx]}";
        }

        ui.SeparatorText("Getter ListBox");
        ui.Text("ListBox(getter)");
        var previousListBoxGetterIdx = _listBoxGetterIdx;
        ui.ListBox(ref _listBoxGetterIdx, _items.Length, i => _items[i], 4, "ListBox(getter)");
        if (previousListBoxGetterIdx != _listBoxGetterIdx)
        {
            _selectionStatus = $"Getter ListBox changed to {_items[_listBoxGetterIdx]}";
        }

        ui.SeparatorText("BeginListBox");
        ui.Text("BeginListBox");
        if (ui.BeginListBox(new UiVector2(0f, 0f), 4, "BeginListBox"))
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (ui.Selectable(_items[i], _beginListBoxIdx == i))
                {
                    _beginListBoxIdx = i;
                    _selectionStatus = $"BeginListBox picked {_items[i]}";
                }
            }
            ui.EndListBox();
        }

        ui.SeparatorText("Selection Snapshot");
        ui.Text($"ListBox: {_items[_listBoxIdx]}");
        ui.Text($"Getter: {_items[_listBoxGetterIdx]}");
        ui.Text($"BeginListBox: {_items[_beginListBoxIdx]}");
        ui.Text($"Last selection event: {_selectionStatus}");

        ui.EndWindow();
    }

    // ─────────────────────────── Selectable ───────────────────────────
    private void RenderSelectableWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, SelectableWindowTitle, default);
        ui.BeginWindow(SelectableWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_selectable",
            SelectableWindowTitle,
            "Bare selectable rows are a useful primitive for custom lists, command palettes, and selection-driven panels.",
            new UiColor(0xFF58A6FF),
            ("Selected", _selectables.Count(static value => value).ToString(), new UiColor(0xFF8DE1A6)));

        for (var i = 0; i < _selectables.Length; i++)
        {
            ui.Selectable($"Selectable {i}", ref _selectables[i]);
        }
        ui.Selectable("Sized Selectable", false, UiSelectableFlags.None, new UiVector2(180f, 24f));

        ui.EndWindow();
    }

    // ─────────────────────────── Tree ───────────────────────────
    private void RenderTreeWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, TreeWindowTitle, default);
        ui.BeginWindow(TreeWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_tree",
            TreeWindowTitle,
            "Hierarchical navigation, collapsible branches, and status feedback for inspectors and project explorers.",
            new UiColor(0xFF58A6FF),
            ("Status", _navigationStatus, new UiColor(0xFF8DE1A6)));

        ui.SeparatorText("Tree Navigation");
        if (ui.TreeNode("TreeNode A", true))
        {
            ui.Text("Child of A");
            if (ui.TreeNode("TreeNode A.1", false))
            {
                ui.Text("Leaf A.1");
                if (ui.SmallButton("Select leaf A.1"))
                {
                    _navigationStatus = "Selected TreeNode A.1 leaf";
                }
                ui.TreePop();
            }
            ui.TreePop();
            _navigationStatus = "TreeNode A opened";
        }
        if (ui.TreeNodeEx("TreeNodeEx B", UiTreeNodeFlags.DefaultOpen))
        {
            ui.Text("Child of B");
            if (ui.Button("Inspect branch B"))
            {
                _navigationStatus = "Inspect branch B";
            }
            ui.TreePop();
        }
        if (ui.TreeNodeEx("TreeNodeEx Leaf", UiTreeNodeFlags.DefaultOpen))
        {
            ui.BulletText("Bullet tree node leaf");
            ui.TreePop();
        }
        if (ui.CollapsingHeader("CollapsingHeader"))
        {
            ui.Text("Collapsible content");
            _navigationStatus = "Opened CollapsingHeader";
        }
        if (ui.CollapsingHeader("CollapsingHeader(ref)", ref _openHeaderRef, UiTreeNodeFlags.DefaultOpen))
        {
            ui.Text("Closeable header content");
            _navigationStatus = _openHeaderRef ? "Closeable header opened" : "Closeable header ready to close";
        }

        ui.SeparatorText("Navigation Status");
        ui.TextWrapped(_navigationStatus);

        ui.EndWindow();
    }

    // ─────────────────────────── Tab ───────────────────────────
    private void RenderTabWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, TabWindowTitle, default);
        ui.BeginWindow(TabWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_tabs",
            TabWindowTitle,
            "Workspace-style organization with closable tabs, tab buttons, and explicit selection state.",
            new UiColor(0xFF58A6FF),
            ("Current", _tabIndex switch { 0 => "Tab A", 1 => "Tab B", 2 => "Closeable", _ => "Unknown" }, new UiColor(0xFF8DE1A6)),
            ("Closeable", _tabClosed ? "Closed" : "Open", new UiColor(0xFFFFD479)));

        ui.SeparatorText("Tab Bar");
        if (ui.BeginTabBar("MainTabs"))
        {
            if (ui.BeginTabItem("Tab A"))
            {
                _tabIndex = 0;
                _navigationStatus = "Viewing Tab A";
                ui.EndTabItem();
            }
            if (ui.BeginTabItem("Tab B"))
            {
                _tabIndex = 1;
                _navigationStatus = "Viewing Tab B";
                ui.EndTabItem();
            }
            if (!_tabClosed && ui.BeginTabItem("Closeable", UiTabItemFlags.None))
            {
                _tabIndex = 2;
                if (ui.SmallButton("Close This Tab"))
                {
                    _tabClosed = true;
                    _tabIndex = 0;
                    _navigationStatus = "Closed the closeable tab";
                    ui.SetTabItemClosed("Closeable");
                }
                ui.EndTabItem();
            }
            if (ui.TabItemButton("+"))
            {
                _navigationStatus = "Pressed the add-tab button";
            }
            ui.EndTabBar();
        }

        if (_tabClosed && _tabIndex == 2)
        {
            _tabIndex = 0;
        }

        if (_tabClosed && ui.Button("Restore Closeable Tab"))
        {
            _tabClosed = false;
            _navigationStatus = "Restored the closeable tab";
        }

        switch (_tabIndex)
        {
            case 0:
                ui.Text("Content of Tab A");
                ui.BulletText("This is the first tab.");
                ui.BulletText("It has bullet items.");
                break;
            case 1:
                ui.Text("Content of Tab B");
                ui.BulletText("This is the second tab.");
                ui.BulletText("Different content here.");
                break;
            case 2:
                if (!_tabClosed)
                {
                    ui.Text("Closeable Tab Content");
                    ui.BulletText("This tab can be closed.");
                }
                break;
        }

            ui.SeparatorText("Tab Status");
            ui.Text(_navigationStatus);

        ui.EndWindow();
    }

    // ─────────────────────────── Table ───────────────────────────
    private void RenderTableWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, TableWindowTitle, default);
        ui.BeginWindow(TableWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_table",
            TableWindowTitle,
            "Structured multi-column data with sortable headers, row backgrounds, and embedded controls.",
            new UiColor(0xFF58A6FF),
            ("Columns", "4", new UiColor(0xFF8DE1A6)),
            ("Rows", "6 demo rows", new UiColor(0xFFFFD479)),
            ("Use", "Data grids", new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Data Table");
        if (ui.BeginTable("AllFeaturesTable", 4, UiTableFlags.Borders | UiTableFlags.RowBg | UiTableFlags.Sortable))
        {
            ui.TableSetupScrollFreeze(0, 1);
            ui.TableSetupColumn("ID", 50f, 0f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Name", 100f, 0f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Value", 80f, 1f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Active", 60f, 0f, UiTableColumnFlags.None);
            ui.TableHeadersRowSortable();

            for (var i = 0; i < 5; i++)
            {
                ui.TableNextRow();
                ui.Text($"{i}");
                ui.TableNextColumn();
                ui.Text(i < _items.Length ? _items[i] : $"Item {i}");
                ui.TableNextColumn();
                ui.Text($"{i * 11}");
                ui.TableNextColumn();
                ui.Checkbox($"##tbl{i}", ref _tableChecks[i]);
            }

            // Demonstrate bg colors
            ui.TableNextRow();
            ui.TableSetBgColor(UiTableBgTarget.RowBg0, new UiColor(0x40335588));
            ui.Text("5");
            ui.TableNextColumn();
            ui.Text("Custom BG");
            ui.TableNextColumn();
            ui.TableSetBgColor(UiTableBgTarget.CellBg, new UiColor(0x40883322));
            ui.Text("55");
            ui.TableNextColumn();
            ui.Text("-");

            ui.EndTable();
        }

        ui.SeparatorText("Table Sort Info");
        ui.Text("(Sort by clicking column headers)");
        if (ui.BeginChild("table_notes", new UiVector2(0f, 92f), true))
        {
            var origin = ui.GetCursorScreenPos();
            var width = MathF.Max(220f, ui.GetContentRegionAvail().X);
            ui.DrawKeyValueRow(new UiRect(origin.X, origin.Y, width, 24f), "Rows", "6 demo rows", selected: true, accent: new UiColor(0xFF58A6FF));
            ui.DrawKeyValueRow(new UiRect(origin.X, origin.Y + 28f, width, 24f), "Features", "Headers / RowBg / BgColor / Checkbox cells", valueColor: new UiColor(0xFFFFD479));
            ui.DrawKeyValueRow(new UiRect(origin.X, origin.Y + 56f, width, 24f), "Use case", "Logs, file browsers, property grids", valueColor: new UiColor(0xFF8DE1A6));
            ui.Dummy(new UiVector2(width, 84f));
        }
        ui.EndChild();

        ui.EndWindow();
    }

    // ─────────────────────────── Popup ───────────────────────────
    private void RenderPopupWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, PopupWindowTitle, default);
        ui.BeginWindow(PopupWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_popup",
            PopupWindowTitle,
            "Basic context popups and modal confirmation flows for compact decision points.",
            new UiColor(0xFF58A6FF),
            ("Last", _advancedPopupAction, new UiColor(0xFF8DE1A6)));

        ui.SeparatorText("Basic Popup");
        if (ui.Button("Open Popup"))
        {
            ui.OpenPopup("TestPopup");
        }
        if (ui.BeginPopup("TestPopup"))
        {
            ui.Text("Popup content!");
            if (ui.Button("Close")) ui.CloseCurrentPopup();
            ui.EndPopup();
        }

        ui.SameLine();
        if (ui.Button("Open Modal"))
        {
            _modalOpen = true;
            ui.OpenPopup("TestModal");
        }
        if (ui.BeginPopupModal("TestModal", ref _modalOpen))
        {
            ui.Text("This is a modal dialog.");
            ui.Text("You must close it to continue.");
            if (ui.Button("OK")) _modalOpen = false;
            ui.EndPopupModal(ref _modalOpen);
        }

        ui.SeparatorText("Inline Context Popup");
        ui.Button("Right-click this inline item");
        if (ui.BeginPopupContextItem("popup_inline_context"))
        {
            if (ui.MenuItem("Copy inline item"))
            {
                _advancedPopupAction = "Copied inline item";
            }
            if (ui.MenuItem("Flag for review"))
            {
                _advancedPopupAction = "Flagged inline item";
            }
            ui.EndPopup();
        }

        ui.Text($"Last popup outcome: {_advancedPopupAction}");

        ui.EndWindow();
    }

    private void RenderAdvancedPopupWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, AdvancedPopupWindowTitle, new UiVector2(460f, 420f));
        ui.BeginWindow(AdvancedPopupWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_popup_patterns",
            AdvancedPopupWindowTitle,
            "Command palette style popups, item-triggered menus, hover detail, and confirmation modal patterns in one place.",
            new UiColor(0xFF58A6FF),
            ("Action", _advancedPopupAction, new UiColor(0xFF8DE1A6)));

        ui.TextWrapped("This window demonstrates item-triggered popups, richer context actions, item tooltips, and modal confirmation flow in one place.");

        if (ui.Button("Open command palette popup"))
        {
            ui.OpenPopup("command_palette_popup");
        }

        if (ui.BeginPopup("command_palette_popup"))
        {
            ui.Text("Quick Commands");
            ui.Separator();
            if (ui.MenuItem("Create note"))
            {
                _advancedPopupAction = "Create note";
                ui.CloseCurrentPopup();
            }
            if (ui.MenuItem("Duplicate panel"))
            {
                _advancedPopupAction = "Duplicate panel";
                ui.CloseCurrentPopup();
            }
            if (ui.MenuItem("Close popup"))
            {
                ui.CloseCurrentPopup();
            }
            ui.EndPopup();
        }

        ui.SeparatorText("OpenPopupOnItemClick / context item");
        ui.Button("Right-click this action chip");
        ui.OpenPopupOnItemClick("advanced_item_context");
        if (ui.BeginPopup("advanced_item_context"))
        {
            if (ui.MenuItem("Pin to top"))
            {
                _advancedPopupAction = "Pin to top";
            }
            if (ui.MenuItem("Archive selection"))
            {
                _advancedPopupAction = "Archive selection";
            }
            if (ui.MenuItem("Inspect metadata"))
            {
                _advancedPopupAction = "Inspect metadata";
            }
            ui.EndPopup();
        }

        ui.SeparatorText("Item tooltip");
        ui.Button("Hover for item tooltip");
        if (ui.BeginItemTooltip())
        {
            ui.Text("Tooltip opened from BeginItemTooltip()");
            ui.BulletText("Good for status summaries");
            ui.BulletText("Can include controls or progress");
            ui.ProgressBar(_progress, new UiVector2(180f, 12f), $"{_progress * 100f:0}% ready");
            ui.EndTooltip();
        }

        ui.SeparatorText("Modal flow");
        if (ui.Button("Open confirmation modal"))
        {
            _advancedModalOpen = true;
            ui.OpenPopup("advanced_confirm_modal");
        }
        if (ui.BeginPopupModal("advanced_confirm_modal", ref _advancedModalOpen))
        {
            ui.Text("Apply the selected popup command?");
            ui.TextWrapped($"Pending action: {_advancedPopupAction}");
            if (ui.Button("Confirm"))
            {
                _advancedPopupAction = $"Confirmed: {_advancedPopupAction}";
                _advancedModalOpen = false;
            }
            ui.SameLine();
            if (ui.Button("Cancel"))
            {
                _advancedPopupAction = "Modal cancelled";
                _advancedModalOpen = false;
            }
            ui.EndPopupModal(ref _advancedModalOpen);
        }

        ui.Separator();
        ui.Text($"Last popup action: {_advancedPopupAction}");

        ui.EndWindow();
    }

    // ─────────────────────────── Tooltip ───────────────────────────
    private void RenderTooltipWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, TooltipWindowTitle, default);
        ui.BeginWindow(TooltipWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_tooltip",
            TooltipWindowTitle,
            "Transient hover affordances ranging from one-line hints to rich mini cards with status context.",
            new UiColor(0xFF58A6FF),
            ("Preview", "SetTooltip · BeginTooltip · ItemTooltip", new UiColor(0xFF8DE1A6)));

        ui.SeparatorText("Tooltip Variants");
        ui.Button("Hover me (SetTooltip)");
        if (ui.IsItemHovered())
        {
            ui.SetTooltip("This is a SetTooltip!");
        }
        ui.Button("Hover me (BeginTooltip)");
        if (ui.IsItemHovered())
        {
            if (ui.BeginTooltip())
            {
                ui.Text("Rich tooltip with:");
                ui.BulletText("Multiple lines");
                ui.BulletText("Bullet points");
                ui.ProgressBar(0.6f, new UiVector2(150f, 12f), null);
                ui.EndTooltip();
            }
        }
        ui.Button("Hover me (SetItemTooltip)");
        if (ui.IsItemHovered())
        {
            ui.SetItemTooltip("SetItemTooltip example");
        }

        ui.SeparatorText("Hover Card");
        if (ui.BeginChild("tooltip_hover_card", new UiVector2(0f, 92f), true))
        {
            ui.Button("Hover this status card");
            if (ui.BeginItemTooltip())
            {
                ui.Text("Status Card");
                ui.ProgressBar(_progress, new UiVector2(160f, 12f), $"{_progress * 100f:0}%");
                ui.TextWrapped("BeginItemTooltip is ideal when the tooltip should belong to the item that was just rendered.");
                ui.EndTooltip();
            }
        }
        ui.EndChild();

        ui.EndWindow();
    }

    // ─────────────────────────── ContextMenu ───────────────────────────
    private void RenderContextMenuWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ContextMenuWindowTitle, default);
        ui.BeginWindow(ContextMenuWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_context_menu",
            ContextMenuWindowTitle,
            "Item, window, and void context menus for local actions exactly where the user expects them.",
            new UiColor(0xFF58A6FF),
            ("Last", _contextMenuAction, new UiColor(0xFF8DE1A6)));

        ui.SeparatorText("Item / Window / Void Context");
        ui.Text("Right-click below for context menus:");
        ui.Button("ContextItem Target");
        if (ui.BeginPopupContextItem("ctx_item"))
        {
            if (ui.MenuItem("Action 1")) _contextMenuAction = "Item: Action 1";
            if (ui.MenuItem("Action 2")) _contextMenuAction = "Item: Action 2";
            ui.EndPopup();
        }

        if (ui.BeginPopupContextWindow("ctx_window"))
        {
            if (ui.MenuItem("Window Action 1")) _contextMenuAction = "Window: Action 1";
            if (ui.MenuItem("Window Action 2")) _contextMenuAction = "Window: Action 2";
            ui.EndPopup();
        }

        ui.Separator();
        ui.TextWrapped("Right-click the empty area below to open a void-context popup.");
        if (ui.BeginChild("context_void_area", new UiVector2(0f, 100f), true))
        {
            ui.Text("Empty workspace area");
            ui.TextDisabled("Right-click here");
            if (ui.BeginPopupContextVoid("ctx_void"))
            {
                if (ui.MenuItem("Create window")) _contextMenuAction = "Void: Create window";
                if (ui.MenuItem("Reset layout")) _contextMenuAction = "Void: Reset layout";
                ui.EndPopup();
            }
        }
        ui.EndChild();

        ui.Separator();
        ui.Text($"Last context action: {_contextMenuAction}");

        if (ui.BeginPopupContextWindow("ctx_window_footer"))
        {
            if (ui.MenuItem("Window Action 3")) _contextMenuAction = "Window footer: Action 3";
            ui.EndPopup();
        }

        ui.EndWindow();
    }

    // ─────────────────────────── Child/Columns/Layout ───────────────────────────
    private void RenderChildWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ChildWindowTitle, new UiVector2(400f, 200f));
        ui.BeginWindow(ChildWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_child",
            ChildWindowTitle,
            "Nested containers, local scrolling, and child-local layout behavior for dashboard sections and inspectors.",
            new UiColor(0xFF58A6FF),
            ("Scroll", _childScroll.ToString("0.00"), new UiColor(0xFF8DE1A6)));

        ui.SeparatorText("Child Window");
        if (ui.BeginChild("child1", new UiVector2(350f, 126f), true))
        {
            ui.Text("Inside child window");
            ui.SliderFloat("ChildSlider", ref _childScroll, 0f, 1f, 0f, "0.00");
            ui.ProgressBar(_childScroll, new UiVector2(200f, 12f), null);

            if (ui.BeginChild("child_nested_preview", new UiVector2(0f, 46f), true))
            {
                ui.Columns(2, false);
                ui.Text("Nested child");
                ui.NextColumn();
                ui.Text("Columns stay local");
                ui.Columns(1, false);
            }
            ui.EndChild();
        }
        ui.EndChild();

        ui.EndWindow();
    }

    private void RenderLayoutWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, LayoutWindowTitle, new UiVector2(520f, 560f));
        ui.BeginWindow(LayoutWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_layout",
            LayoutWindowTitle,
            "Spacing, indentation, width control, wrapping, and scrolling cues tuned for inspector-style interfaces that feel deliberate.",
            new UiColor(0xFF7EC8FF),
            ("Width", $"{_layoutItemWidth:0}px pushed", new UiColor(0xFF8DE1A6)),
            ("Indent", $"{_layoutIndentWidth:0}px", new UiColor(0xFFFFD479)),
            ("Alpha", _layoutBgAlpha.ToString("0.00"), new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Layout Helpers");
        ui.BeginGroup();
        ui.Text("Group start");
        ui.Dummy(new UiVector2(60f, 10f));
        ui.Spacing();
        ui.NewLine();
        ui.SliderFloat("Indent Width", ref _layoutIndentWidth, 8f, 64f, 0f, "0");
        ui.Indent(_layoutIndentWidth);
        ui.Text("Indented");
        ui.Unindent(_layoutIndentWidth);
        ui.EndGroup();

        ui.SeparatorText("PushID / Item Width");
        ui.SliderFloat("Target Width", ref _layoutItemWidth, 100f, 360f, 0f, "0");
        for (var i = 0; i < 3; i++)
        {
            ui.PushID(i);
            if (ui.Button("Action"))
            {
                _clickCount += i + 1;
            }
            ui.SameLine();
            ui.Text($"Scoped button {i} | ID={ui.GetID("Action")}");
            ui.PopID();
        }
        ui.PushItemWidth(_layoutItemWidth);
        ui.SliderFloat("Width-pushed Slider", ref _layoutPreviewFloat, 0f, 1f, 0f, "0.00");
        ui.DragInt("Width-pushed Drag", ref _layoutPreviewInt, 0.2f, 0, 20);
        ui.PopItemWidth();
        ui.SetNextItemWidth(120f);
        ui.InputInt("120px Input", ref _layoutPreviewInt);

        ui.SeparatorText("Cursor / Region Queries");
        var cursorPos = ui.GetCursorPos();
        var startPos = ui.GetCursorStartPos();
        var avail = ui.GetContentRegionAvail();
        var contentMax = ui.GetContentRegionMax();
        var windowPos = ui.GetWindowPos();
        var windowSize = ui.GetWindowSize();
        ui.Text($"Cursor: ({cursorPos.X:0}, {cursorPos.Y:0})  Start: ({startPos.X:0}, {startPos.Y:0})");
        ui.Text($"Avail: ({avail.X:0}, {avail.Y:0})  Max: ({contentMax.X:0}, {contentMax.Y:0})");
        ui.Text($"Window: ({windowPos.X:0}, {windowPos.Y:0}) size ({windowSize.X:0}, {windowSize.Y:0})");
        ui.AlignTextToFramePadding();
        ui.Text("Aligned text");
        ui.SameLine();
        ui.Button("Peer control");

        ui.SeparatorText("Text Wrap / Scroll Control");
        ui.SliderFloat("Wrap Width##layout", ref _typographyWrapWidth, 160f, 520f, 0f, "0");
        ui.PushTextWrapPos(_typographyWrapWidth);
        ui.Text("Layout-heavy windows often need a narrower reading width than the full window. PushTextWrapPos keeps notes readable while controls can still span the rest of the row.");
        ui.PopTextWrapPos();

        ui.SliderFloat("Scroll Target", ref _layoutScrollTarget, 0f, 500f, 0f, "0");
        if (ui.Button("Apply next scroll target"))
        {
            ui.SetNextWindowScroll(0f, _layoutScrollTarget);
        }
        if (ui.BeginChild("layout_scroll_demo", new UiVector2(0f, 120f), true))
        {
            for (var i = 0; i < 28; i++)
            {
                ui.Text($"Layout scroll line {i:00}");
            }
            ui.Text($"ScrollY: {ui.GetScrollY():0} / {ui.GetScrollMaxY():0}");
        }
        ui.EndChild();

        ui.SeparatorText("Window Alpha Preview");
        ui.SliderFloat("BG Alpha", ref _layoutBgAlpha, 0.1f, 1f, 0f, "0.00");
        ui.SetNextWindowBgAlpha(_layoutBgAlpha);
        InitCenteredWindow(ui, LayoutAlphaPreviewTitle, new UiVector2(280f, 148f));
        ui.BeginWindow(LayoutAlphaPreviewTitle);
        RenderCompactShowcaseHero(
            ui,
            "hero_layout_alpha",
            "Alpha Preview",
            "A translucent utility surface preview for floating inspectors and overlays.",
            new UiColor(0xFF7EC8FF),
            ("Alpha", _layoutBgAlpha.ToString("0.00"), new UiColor(0xFF8DE1A6)));
        ui.Text($"Alpha: {_layoutBgAlpha:0.00}");
        ui.Text("Useful for translucent overlays and inspectors.");
        ui.EndWindow();

        ui.EndWindow();
    }

    private void RenderColumnsWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ColumnsWindowTitle, new UiVector2(620f, 520f));
        ui.BeginWindow(ColumnsWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_columns",
            ColumnsWindowTitle,
            "Legacy columns still shine for quick inspectors and split-pane tools when you need structure without the overhead of full data grids.",
            new UiColor(0xFF58A6FF),
            ("Border", _columnsBorder ? "Visible" : "Hidden", new UiColor(0xFF8DE1A6)),
            ("Lead Column", $"{_columnsFirstWidth:0}px", new UiColor(0xFFFFD479)),
            ("Layout", "Inspector / preview", new UiColor(0xFF7EC8FF)));

        ui.Checkbox("Column Border", ref _columnsBorder);
        ui.SliderFloat("First Column Width", ref _columnsFirstWidth, 100f, 280f, 0f, "0");

        ui.SeparatorText("2 Columns");
        ui.Columns(2, _columnsBorder);
        ui.SetColumnWidth(0, _columnsFirstWidth);
        ui.Text("Left column");
        ui.Text("Custom width via SetColumnWidth");
        ui.NextColumn();
        ui.Text("Right column");
        ui.Text("Details / preview / inspector");
        ui.Columns(1, false);

        ui.SeparatorText("3 Columns + Queries");
        ui.Columns(3, _columnsBorder);
        for (var i = 0; i < 3; i++)
        {
            ui.Text($"Column {i}");
            ui.Text($"Width: {ui.GetColumnWidth(i):0.0}");
            ui.Text($"Offset: {ui.GetColumnOffset(i):0.0}");
            if (i < 2)
            {
                ui.NextColumn();
            }
        }
        ui.Columns(1, false);
        ui.Text($"Count={ui.GetColumnsCount()}  Current={ui.GetColumnIndex()}");

        ui.SeparatorText("Mixed Content");
        ui.Columns(2, true);
        ui.Text("Actions");
        if (ui.Button("Rebuild Layout"))
        {
            _clickCount += 5;
        }
        ui.Button("Export Snapshot");
        ui.NextColumn();
        ui.Text("Status");
        ui.Checkbox("Custom Style Active", ref _customStyle);
        ui.ProgressBar(_progress, new UiVector2(MathF.Max(120f, ui.GetContentRegionAvail().X - 6f), 12f), $"{_progress * 100f:0}%");
        ui.Columns(1, false);

        ui.SeparatorText("Notes");
        ui.TextWrapped("Use the legacy Columns API for simple inspectors or side-by-side editors. For denser data, prefer the dedicated Table API shown in the `Table` window.");

        ui.EndWindow();
    }

    private void RenderInputQueriesWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, InputQueriesWindowTitle, new UiVector2(500f, 460f));
        ui.BeginWindow(InputQueriesWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_input_queries",
            InputQueriesWindowTitle,
            "Low-level keyboard, mouse, drag delta, clipboard, and hover rectangle queries for interaction diagnostics.",
            new UiColor(0xFF58A6FF),
            ("Shortcut", _lastShortcut, new UiColor(0xFF8DE1A6)),
            ("Clipboard", _clipboardEcho, new UiColor(0xFFFFD479)));

        if (ui.Shortcut(UiKey.K, KeyModifiers.Ctrl))
        {
            _lastShortcut = "Ctrl+K detected";
        }
        else if (ui.Shortcut(UiKey.L, KeyModifiers.Ctrl))
        {
            _lastShortcut = "Ctrl+L detected";
        }
        else if (ui.Shortcut(UiKey.M, KeyModifiers.Ctrl))
        {
            _lastShortcut = "Ctrl+M detected";
        }

        ui.SeparatorText("Keyboard");
        ui.Text($"Last shortcut: {_lastShortcut}");
        ui.Text($"Enter: down={ui.IsKeyDown(UiKey.Enter)} pressed={ui.IsKeyPressed(UiKey.Enter, false)} released={ui.IsKeyReleased(UiKey.Enter)}");
        ui.Text($"Space: down={ui.IsKeyDown(UiKey.Space)} pressed={ui.IsKeyPressed(UiKey.Space, true)}");
        ui.Text($"Arrows: left={ui.IsKeyDown(UiKey.LeftArrow)} right={ui.IsKeyDown(UiKey.RightArrow)} up={ui.IsKeyDown(UiKey.UpArrow)} down={ui.IsKeyDown(UiKey.DownArrow)}");
        ui.Text($"Tab repeats this frame: {ui.GetKeyPressedAmount(UiKey.Tab, 0.35f, 0.08f)}");

        ui.SeparatorText("Mouse");
        var mouse = ui.GetMousePos();
        var dragDelta = ui.GetMouseDragDelta((int)UiMouseButton.Left);
        ui.Text($"Mouse Pos: ({mouse.X:0.0}, {mouse.Y:0.0}) valid={ui.IsMousePosValid()}");
        ui.Text($"Left clicked={ui.IsMouseClicked((int)UiMouseButton.Left)} released={ui.IsMouseReleased((int)UiMouseButton.Left)} double={ui.IsMouseDoubleClicked((int)UiMouseButton.Left)}");
        ui.Text($"Any mouse down={ui.IsAnyMouseDown()} dragging={ui.IsMouseDragging((int)UiMouseButton.Left)} cursor={ui.GetMouseCursor()}");
        ui.Text($"Drag delta: ({dragDelta.X:0.0}, {dragDelta.Y:0.0})");
        if (ui.Button("Reset Drag Delta"))
        {
            ui.ResetMouseDragDelta((int)UiMouseButton.Left);
        }

        ui.SeparatorText("Clipboard");
        ui.SetNextItemShortcut(UiKey.C, KeyModifiers.Ctrl);
        ui.InputText("Clipboard Text", ref _clipboardSample, 256);
        if (ui.Button("Copy to Clipboard"))
        {
            ui.SetClipboardText(_clipboardSample);
            _clipboardEcho = $"Copied at frame {_frameCounter}";
        }
        ui.SameLine();
        if (ui.Button("Read Clipboard"))
        {
            _clipboardEcho = ui.GetClipboardText();
        }
        ui.TextWrapped($"Clipboard echo: {_clipboardEcho}");

        ui.SeparatorText("Hover Rectangle Query");
        if (ui.BeginChild("hover_rect_probe", new UiVector2(0f, 90f), true))
        {
            var origin = ui.GetCursorScreenPos();
            var probeRect = new UiRect(origin.X + 8f, origin.Y + 8f, 180f, 46f);
            var drawList = ui.GetWindowDrawList();
            drawList.AddRectFilled(probeRect, new UiColor(0xFF233142));
            drawList.AddRect(probeRect, new UiColor(0xFF5DB3FF), 6f, 1.5f);
            var hovered = ui.IsMouseHoveringRect(new UiVector2(probeRect.X, probeRect.Y), new UiVector2(probeRect.X + probeRect.Width, probeRect.Y + probeRect.Height));
            ui.DrawTextAligned(probeRect, hovered ? "Rect hovered" : "Move cursor here", new UiColor(0xFFFFFFFF), UiItemHorizontalAlign.Center, UiItemVerticalAlign.Center);
            ui.Dummy(new UiVector2(200f, 62f));
        }
        ui.EndChild();

        ui.EndWindow();
    }

    private void RenderItemStatusWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ItemStatusWindowTitle, new UiVector2(560f, 560f));
        ui.BeginWindow(ItemStatusWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_item_status",
            ItemStatusWindowTitle,
            "Per-item hover, activation, edit, and selection metadata captured immediately after rendering each control.",
            new UiColor(0xFF58A6FF),
            ("Latest", _itemStatusMessage, new UiColor(0xFF8DE1A6)));

        ui.TextWrapped("Each block below captures the last item state immediately after rendering the control, so you can inspect hover/active/focus/edit/toggle behavior without guessing.");
        ui.Text($"Latest event: {_itemStatusMessage}");
        ui.Text($"Any hovered={ui.IsAnyItemHovered()} active={ui.IsAnyItemActive()} focused={ui.IsAnyItemFocused()}");

        ui.SeparatorText("Button");
        _ = ui.Button("Inspect Button");
        RenderItemSnapshot(ui, "Inspect Button");

        ui.SeparatorText("Selectable");
        ui.Selectable("Status Selectable A", ref _statusSelectableA);
        RenderItemSnapshot(ui, "Status Selectable A");
        ui.Selectable("Status Selectable B", ref _statusSelectableB);
        RenderItemSnapshot(ui, "Status Selectable B");

        ui.SeparatorText("Checkbox / Input");
        ui.Checkbox("Tracked Toggle", ref _statusToggle);
        RenderItemSnapshot(ui, "Tracked Toggle");
        ui.InputText("Tracked Input", ref _statusInput, 256);
        RenderItemSnapshot(ui, "Tracked Input");

        ui.EndWindow();
    }

    private void RenderItemSnapshot(UiImmediateContext ui, string label)
    {
        var hovered = ui.IsItemHovered();
        var active = ui.IsItemActive();
        var focused = ui.IsItemFocused();
        var clicked = ui.IsItemClicked();
        var visible = ui.IsItemVisible();
        var edited = ui.IsItemEdited();
        var activated = ui.IsItemActivated();
        var deactivated = ui.IsItemDeactivated();
        var deactivatedAfterEdit = ui.IsItemDeactivatedAfterEdit();
        var toggledOpen = ui.IsItemToggledOpen();
        var toggledSelection = ui.IsItemToggledSelection();
        var id = ui.GetItemID() ?? "(no id)";
        var min = ui.GetItemRectMin();
        var max = ui.GetItemRectMax();
        var size = ui.GetItemRectSize();

        if (clicked)
        {
            _itemStatusMessage = $"{label}: clicked";
        }
        else if (deactivatedAfterEdit)
        {
            _itemStatusMessage = $"{label}: deactivated after edit";
        }
        else if (edited)
        {
            _itemStatusMessage = $"{label}: edited";
        }
        else if (toggledSelection)
        {
            _itemStatusMessage = $"{label}: selection toggled";
        }
        else if (activated)
        {
            _itemStatusMessage = $"{label}: activated";
        }

        if (ui.BeginChild($"snapshot_{label}", new UiVector2(0f, 92f), true))
        {
            ui.Text($"ID: {id}");
            ui.Text($"hovered={hovered} active={active} focused={focused} visible={visible}");
            ui.Text($"clicked={clicked} edited={edited} activated={activated} deactivated={deactivated}");
            ui.Text($"deactivatedAfterEdit={deactivatedAfterEdit} toggledOpen={toggledOpen} toggledSelection={toggledSelection}");
            ui.Text($"rect min=({min.X:0.0}, {min.Y:0.0}) max=({max.X:0.0}, {max.Y:0.0}) size=({size.X:0.0}, {size.Y:0.0})");
        }
        ui.EndChild();
    }

    private void RenderMultiSelectWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, MultiSelectWindowTitle, new UiVector2(480f, 460f));
        ui.BeginWindow(MultiSelectWindowTitle);

        var selectedCount = 0;
        for (var i = 0; i < _multiSelectItems.Length; i++)
        {
            if (_multiSelectItems[i])
            {
                selectedCount++;
            }
        }

        RenderShowcaseHero(
            ui,
            "hero_multi_select",
            MultiSelectWindowTitle,
            "Selection metadata and toggled state for multi-pick flows that need more than a single active item.",
            new UiColor(0xFF58A6FF),
            ("Selected", $"{selectedCount} / {_multiSelectItems.Length}", new UiColor(0xFF8DE1A6)),
            ("Last", _multiSelectMessage, new UiColor(0xFFFFD479)));

        ui.TextWrapped("The current multi-select API surface is intentionally small, so this showcase focuses on selection metadata, toggled state, and item-level diagnostics.");
        ui.Text($"Selected count: {selectedCount} / {_multiSelectItems.Length}");
        ui.Text($"Last selection event: {_multiSelectMessage}");

        if (ui.BeginMultiSelect(UiMultiSelectFlags.None, selectedCount, _multiSelectItems.Length))
        {
            if (ui.BeginChild("multi_select_list", new UiVector2(0f, 280f), true))
            {
                for (var i = 0; i < _multiSelectItems.Length; i++)
                {
                    ui.SetNextItemSelectionUserData(i);
                    ui.Selectable($"Multi item {i:00}", ref _multiSelectItems[i]);

                    if (ui.TryGetItemSelectionUserData(out var userData) && ui.IsItemToggledSelection())
                    {
                        _multiSelectMessage = $"Item {userData} toggled to {(_multiSelectItems[i] ? "selected" : "cleared")}";
                    }

                    if (ui.BeginItemTooltip())
                    {
                        ui.Text($"Selection user data: {i}");
                        ui.Text($"Selected: {_multiSelectItems[i]}");
                        ui.EndTooltip();
                    }
                }
            }
            ui.EndChild();
            ui.EndMultiSelect();
        }

        ui.SeparatorText("Selected Items");
        ui.Columns(2, true);
        ui.Text("Index");
        ui.NextColumn();
        ui.Text("State");
        ui.NextColumn();

        for (var i = 0; i < _multiSelectItems.Length; i++)
        {
            ui.Text($"{i:00}");
            ui.NextColumn();
            ui.Text(_multiSelectItems[i] ? "Selected" : "Idle");
            ui.NextColumn();
        }
        ui.Columns(1, false);

        ui.EndWindow();
    }

    private void RenderLayersAnimationWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, LayersWindowTitle, new UiVector2(620f, 520f));
        ui.BeginWindow(LayersWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_layers",
            LayersWindowTitle,
            "Cached replay, opacity, translation, and lightweight motion cues working together like a compact rendering systems postcard.",
            new UiColor(0xFF5DD6FF),
            ("Cache", _layerStaticCache ? "Static" : "Dynamic", new UiColor(0xFF8DE1A6)),
            ("Opacity", _layerOpacity.ToString("0.00"), new UiColor(0xFFFFD479)),
            ("Offset", $"{_layerTranslationX:0}px", new UiColor(0xFF7EC8FF)));

        ui.TextWrapped("This showcase combines cached layers, translation/opacity replay, and a small animation overlay. Think of it as a postcard from the rendering subsystem.");
        ui.Checkbox("Static Cache", ref _layerStaticCache);
        ui.Checkbox("Expanded", ref _layerExpanded);
        ui.SliderFloat("Layer Opacity", ref _layerOpacity, 0.2f, 1f, 0f, "0.00");
        ui.SliderFloat("Layer Translation X", ref _layerTranslationX, -40f, 120f, 0f, "0");
        if (ui.Button("Mark Layer Dirty"))
        {
            ui.MarkLayerDirty("showcase.layer.card");
        }
        ui.SameLine();
        if (ui.Button("Mark All Layers Dirty"))
        {
            ui.MarkAllLayersDirty();
        }

        var blend = ui.AnimateFloat("showcase.layer.blend", _layerExpanded ? 1f : 0f, 0.2f, UiAnimationEasing.OutCubic);
        var rotation = ui.AnimateToggleRotationDegrees("showcase.layer.chevron", _layerExpanded);

        if (ui.BeginChild("layer_animation_canvas", new UiVector2(0f, 320f), true))
        {
            var canvas = ui.BeginWindowCanvas(new UiColor(0xFF10141C));
            var drawList = ui.GetWindowDrawList();

            for (var x = canvas.X; x <= canvas.X + canvas.Width; x += 28f)
            {
                drawList.AddLine(new UiVector2(x, canvas.Y), new UiVector2(x, canvas.Y + canvas.Height), new UiColor(0x1FFFFFFF), 1f);
            }
            for (var y = canvas.Y; y <= canvas.Y + canvas.Height; y += 28f)
            {
                drawList.AddLine(new UiVector2(canvas.X, y), new UiVector2(canvas.X + canvas.Width, y), new UiColor(0x1FFFFFFF), 1f);
            }

            ui.DrawLayerCardInteractive(
                canvas,
                new UiVector2(24f, 34f),
                new UiVector2(MathF.Min(360f, canvas.Width - 48f), 170f),
                new UiColor(0xFF2B3A4D),
                $"Render Layer ({(_layerStaticCache ? "cached" : "dynamic")})",
                out _,
                out var bodyRect,
                out var interaction,
                bodyBackground: new UiColor(0xCC1A202A),
                borderColor: new UiColor(0xFF8696AA),
                headerHeight: 28f,
                hitTestId: "showcase.layer.card.hit");

            var options = new UiLayerOptions(
                StaticCache: _layerStaticCache,
                Opacity: _layerOpacity,
                Translation: new UiVector2(bodyRect.X + _layerTranslationX, bodyRect.Y));

            if (ui.BeginLayer("showcase.layer.card", options))
            {
                var localBody = new UiRect(0f, 0f, bodyRect.Width, bodyRect.Height);
                DrawLayerShowcaseBody(ui, localBody, blend);
            }
            ui.EndLayer();

            var indicatorCenter = new UiVector2(canvas.X + canvas.Width - 72f, canvas.Y + 86f + (blend * 54f));
            drawList.AddCircleFilled(indicatorCenter, 14f, new UiColor(0xFF5DD6FF), ui.WhiteTextureId, canvas, 24);

            var radians = rotation * (MathF.PI / 180f);
            var pointerStart = new UiVector2(canvas.X + canvas.Width - 72f, canvas.Y + 42f);
            var pointerEnd = new UiVector2(pointerStart.X + MathF.Cos(radians) * 18f, pointerStart.Y + MathF.Sin(radians) * 18f);
            drawList.AddLine(pointerStart, pointerEnd, new UiColor(0xFFFFFFFF), 2f);

            ui.DrawKeyValueRow(new UiRect(canvas.X + 24f, canvas.Y + canvas.Height - 42f, canvas.Width - 48f, 26f), "Layer", interaction.Hovered ? "Card hovered" : "Card idle", selected: interaction.Hovered, accent: new UiColor(0xFF58A6FF));
            _ = ui.EndWindowCanvas();
        }
        ui.EndChild();

        ui.Text($"Animation blend={blend:0.00} rotation={rotation:0.0}deg");

        ui.EndWindow();
    }

    private void DrawLayerShowcaseBody(UiImmediateContext ui, UiRect localBody, float blend)
    {
        var drawList = ui.GetWindowDrawList();
        drawList.AddRectFilled(localBody, new UiColor(0xC41A1F29), ui.WhiteTextureId, localBody);

        for (var x = localBody.X; x <= localBody.X + localBody.Width; x += 20f)
        {
            drawList.AddLine(new UiVector2(x, localBody.Y), new UiVector2(x, localBody.Y + localBody.Height), new UiColor(0x16FFFFFF), 1f);
        }

        var barWidth = MathF.Max(40f, (localBody.Width - 40f) * (0.25f + blend * 0.65f));
        var barRect = new UiRect(localBody.X + 16f, localBody.Y + 18f, barWidth, 18f);
        drawList.AddRectFilled(barRect, new UiColor(0xFF58A6FF), ui.WhiteTextureId, localBody);
        drawList.AddRectFilled(new UiRect(localBody.X + 16f, localBody.Y + 52f, MathF.Max(80f, localBody.Width - 32f), 10f), new UiColor(0x663A4454), ui.WhiteTextureId, localBody);
        drawList.AddRectFilled(new UiRect(localBody.X + 16f, localBody.Y + 74f, MathF.Max(120f, localBody.Width - 90f), 10f), new UiColor(0x664A5567), ui.WhiteTextureId, localBody);
        drawList.AddCircleFilled(new UiVector2(localBody.X + localBody.Width - 42f, localBody.Y + localBody.Height - 32f), 12f + blend * 8f, new UiColor(0xFF8DE1A6), ui.WhiteTextureId, localBody, 20);
    }

    // ─────────────────────────── Drawing Primitives ───────────────────────────
    private void RenderDrawingPrimitives(UiImmediateContext ui)
    {
        InitWindowOnce(ui, DrawingWindowTitle, new UiVector2(420f, 360f));
        ui.BeginWindow(DrawingWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_drawing",
            DrawingWindowTitle,
            "Low-level draw list geometry, strokes, fills, curves, and text rendered directly without higher-level widgets.",
            new UiColor(0xFF58A6FF),
            ("Thickness", _drawThickness.ToString("0.0"), new UiColor(0xFF8DE1A6)),
            ("Segments", _drawSegments.ToString(), new UiColor(0xFFFFD479)));

        if (!ui.IsWindowCollapsed())
        {
            ui.SliderFloat("Thickness", ref _drawThickness, 1f, 5f, 0.5f, "0.0");
            ui.SliderInt("Segments", ref _drawSegments, 3, 48);

            var avail = ui.GetContentRegionAvail();
            var canvasW = MathF.Max(avail.X, 360f);
            var canvasH = 220f;

            if (ui.BeginChild("draw_canvas", new UiVector2(canvasW, canvasH), false))
            {
                var drawList = ui.GetWindowDrawList();
                var p = ui.GetCursorScreenPos();

                // Reserve space
                ui.Dummy(new UiVector2(canvasW, canvasH));

                drawList.PushTexture(ui.WhiteTextureId);
                var thick = _drawThickness;

                // Line
                drawList.AddLine(
                    new UiVector2(p.X + 10f, p.Y + 10f),
                    new UiVector2(p.X + 80f, p.Y + 10f),
                    new UiColor(0xFFFF4444), thick);

                // Rect outline
                drawList.AddRect(
                    new UiRect(p.X + 10f, p.Y + 20f, 60f, 30f),
                    new UiColor(0xFF44FF44), 4f, thick);

                // Rect filled
                drawList.AddRectFilled(
                    new UiRect(p.X + 80f, p.Y + 20f, 60f, 30f),
                    new UiColor(0xFF4444FF));

                // RectFilledMultiColor
                drawList.AddRectFilledMultiColor(
                    new UiRect(p.X + 150f, p.Y + 20f, 60f, 30f),
                    new UiColor(0xFFFF0000),
                    new UiColor(0xFF00FF00),
                    new UiColor(0xFF0000FF),
                    new UiColor(0xFFFFFF00));

                // Triangle
                drawList.AddTriangle(
                    new UiVector2(p.X + 230f, p.Y + 20f),
                    new UiVector2(p.X + 260f, p.Y + 50f),
                    new UiVector2(p.X + 200f, p.Y + 50f),
                    new UiColor(0xFFFF88FF), thick);

                // Triangle filled
                drawList.AddTriangleFilled(
                    new UiVector2(p.X + 290f, p.Y + 20f),
                    new UiVector2(p.X + 330f, p.Y + 50f),
                    new UiVector2(p.X + 260f, p.Y + 50f),
                    new UiColor(0xFFFFAA44));

                // Circle
                drawList.AddCircle(
                    new UiVector2(p.X + 40f, p.Y + 80f), 20f,
                    new UiColor(0xFFFFFFFF), _drawSegments, thick);

                // Circle filled
                drawList.AddCircleFilled(
                    new UiVector2(p.X + 100f, p.Y + 80f), 20f,
                    new UiColor(0xFF88CCFF), ui.WhiteTextureId, _drawSegments);

                // Ngon
                drawList.AddNgon(
                    new UiVector2(p.X + 160f, p.Y + 80f), 20f,
                    6, new UiColor(0xFFFFCC44), thick);

                // Ngon filled
                drawList.AddNgonFilled(
                    new UiVector2(p.X + 220f, p.Y + 80f), 20f,
                    5, new UiColor(0xFF44FFCC));

                // Ellipse
                drawList.AddEllipse(
                    new UiVector2(p.X + 300f, p.Y + 80f),
                    new UiVector2(30f, 15f),
                    new UiColor(0xFFFF66FF), 24, thick);

                // Ellipse filled
                drawList.AddEllipseFilled(
                    new UiVector2(p.X + 50f, p.Y + 130f),
                    new UiVector2(35f, 18f),
                    new UiColor(0xFF66FFAA), 24);

                // Bezier cubic
                drawList.AddBezierCubic(
                    new UiVector2(p.X + 100f, p.Y + 120f),
                    new UiVector2(p.X + 130f, p.Y + 100f),
                    new UiVector2(p.X + 170f, p.Y + 150f),
                    new UiVector2(p.X + 200f, p.Y + 120f),
                    new UiColor(0xFFFFFF00), thick, 20);

                // Bezier quadratic
                drawList.AddBezierQuadratic(
                    new UiVector2(p.X + 210f, p.Y + 120f),
                    new UiVector2(p.X + 250f, p.Y + 100f),
                    new UiVector2(p.X + 290f, p.Y + 140f),
                    new UiColor(0xFFFF8800), thick, 20);

                // Quad
                drawList.AddQuad(
                    new UiVector2(p.X + 10f, p.Y + 160f),
                    new UiVector2(p.X + 60f, p.Y + 155f),
                    new UiVector2(p.X + 65f, p.Y + 190f),
                    new UiVector2(p.X + 15f, p.Y + 195f),
                    new UiColor(0xFFCCCCFF), thick);

                // Quad filled
                drawList.AddQuadFilled(
                    new UiVector2(p.X + 80f, p.Y + 160f),
                    new UiVector2(p.X + 130f, p.Y + 155f),
                    new UiVector2(p.X + 135f, p.Y + 190f),
                    new UiVector2(p.X + 85f, p.Y + 195f),
                    new UiColor(0xFFFFCC88));

                // Text via draw list
                drawList.AddText(
                    new UiVector2(p.X + 150f, p.Y + 170f),
                    new UiColor(0xFFFFFFFF), "DrawList Text");

                drawList.PopTexture();
            }
            ui.EndChild();
        }

        ui.EndWindow();
    }

    // ─────────────────────────── Image Widgets ───────────────────────────
    private void RenderImageWidgets(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ImageWindowTitle, new UiVector2(500f, 520f));
        ui.BeginWindow(ImageWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_image",
            ImageWindowTitle,
            "Static image widgets plus runtime image effects like zoom, rotation, alpha, brightness, contrast, and pixelation.",
            new UiColor(0xFF58A6FF),
            ("Zoom", _imgZoom.ToString("0.00x"), new UiColor(0xFF8DE1A6)),
            ("Rotation", _imgRotation.ToString("0.0°"), new UiColor(0xFFFFD479)),
            ("Alpha", _imgAlpha.ToString("0.00"), new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Image / ImageWithBg / ImageButton");
        ui.Image(ui.WhiteTextureId, new UiVector2(48f, 48f), new UiColor(0xFF4488FF));
        ui.SameLine();
        ui.ImageWithBg(ui.WhiteTextureId, new UiVector2(48f, 48f), new UiColor(0xFF222222), new UiColor(0xFFFF44FF));
        ui.SameLine();
        if (ui.ImageButton("img_btn1", ui.WhiteTextureId, new UiVector2(48f, 48f), new UiColor(0xFF6688CC)))
            _imgBtnClicks++;
        ui.SameLine();
        if (ui.ImageButton("img_btn2", ui.WhiteTextureId, new UiVector2(48f, 48f), new UiColor(0xFFCC8866)))
            _imgBtnClicks++;
        ui.Text($"ImageButton clicks: {_imgBtnClicks}");

        ui.SeparatorText("Image Effects (Remote Sample Image)");
        EnsureImageLoaded();

        if (_imgPlayer is null)
        {
            ui.TextColored(new UiColor(0xFFFF4444), _imgLoadError ?? "Loading failed");
            if (ui.Button("Retry Image Load"))
            {
                ResetImageLoadState();
                EnsureImageLoaded();
            }
            ui.EndWindow();
            return;
        }

        var fx = UiImageEffects.Create(_imgGrayscale, _imgInvert, _imgBrightness, _imgContrast, _imgPixelate);
        _imgPlayer.Prepare(ui, fx);

        ui.TextV("Image: {0}x{1}", _imgPlayer.Width, _imgPlayer.Height);
        ui.SliderFloat("Zoom", ref _imgZoom, 0.1f, 4f, 0f, "0.00x");
        ui.SliderFloat("Rotation", ref _imgRotation, -180f, 180f, 0f, "0.0 deg");
        ui.SliderFloat("Alpha", ref _imgAlpha, 0f, 1f, 0f, "0.00");
        ui.SliderFloat("Brightness##img", ref _imgBrightness, 0.2f, 2.2f, 0f, "0.00");
        ui.SliderFloat("Contrast##img", ref _imgContrast, 0.2f, 2.2f, 0f, "0.00");
        ui.SliderInt("Pixelate##img", ref _imgPixelate, 1, 24);
        ui.Checkbox("Grayscale##img", ref _imgGrayscale);
        ui.SameLine();
        ui.Checkbox("Invert##img", ref _imgInvert);

        if (ui.Button("Reset##img"))
        {
            _imgZoom = 1f;
            _imgRotation = 0f;
            _imgAlpha = 1f;
            _imgBrightness = 1f;
            _imgContrast = 1f;
            _imgPixelate = 1;
            _imgGrayscale = false;
            _imgInvert = false;
        }

        var canvasSize = ui.GetContentRegionAvail();
        if (ui.BeginChild("ImgCanvas", canvasSize, true))
        {
            _imgPlayer.DrawInCurrentRegion(ui, _imgZoom, _imgRotation, _imgAlpha);
            ui.EndChild();
        }

        ui.EndWindow();
    }

    private void EnsureImageLoaded()
    {
        if (_imgLoadAttempted) return;
        _imgLoadAttempted = true;

        try
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "Duxel", "fba-image-cache");
            Directory.CreateDirectory(cacheDir);
            var imagePath = Path.Combine(cacheDir, "web-sample-clean.jpg");

            if (!File.Exists(imagePath))
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://dummyimage.com/960x640/111827/f8fafc.jpg&text=Duxel+Web+Image");
                request.Headers.UserAgent.ParseAdd("Duxel-FBA/1.0");
                using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                using var stream = response.Content.ReadAsStream();
                using var fs = File.Create(imagePath);
                stream.CopyTo(fs);
            }

            _imgPlayer = AnimatedUiImagePlayer.Load(imagePath, 8001);
        }
        catch (Exception ex)
        {
            _imgLoadError = ex.Message;
        }
    }

    private void ResetImageLoadState()
    {
        _imgPlayer = null;
        _imgLoadError = null;
        _imgLoadAttempted = false;
    }

    // ─────────────────────────── Drag & Drop ───────────────────────────
    private void RenderDragDrop(UiImmediateContext ui)
    {
        InitWindowOnce(ui, DragDropWindowTitle, new UiVector2(350f, 200f));
        ui.BeginWindow(DragDropWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_dragdrop",
            DragDropWindowTitle,
            "Payload source, target, and preset switching for verifying transfer semantics and feedback flows.",
            new UiColor(0xFF58A6FF),
            ("Source", _dragDropSource, new UiColor(0xFF8DE1A6)),
            ("Dropped", _dragDropResult, new UiColor(0xFFFFD479)));

        ui.SeparatorText("Payload Source / Target");
        ui.Button($"{_dragDropSource}##drag_source");
        if (ui.BeginDragDropSource(UiDragDropFlags.None))
        {
            var data = Encoding.UTF8.GetBytes(_dragDropSource);
            ui.SetDragDropPayload("TEXT", data);
            ui.Text($"Dragging: {_dragDropSource}");
            ui.EndDragDropSource();
        }

        ui.SameLine();
        ui.Button("[Drop here]##drag_target");
        if (ui.BeginDragDropTarget())
        {
            var payload = ui.AcceptDragDropPayload("TEXT", UiDragDropFlags.None);
            if (payload is not null)
            {
                _dragDropResult = Encoding.UTF8.GetString(payload.Data.Span);
            }
            ui.EndDragDropTarget();
        }

        ui.SeparatorText("Payload Presets");
        if (ui.SmallButton("Use text payload")) _dragDropSource = "Drag me!";
        ui.SameLine();
        if (ui.SmallButton("Use file payload")) _dragDropSource = "report.md";
        ui.SameLine();
        if (ui.SmallButton("Use scene payload")) _dragDropSource = "Layer/Widget/Preview";

        ui.Text($"Source: {_dragDropSource}");
        ui.Text($"Dropped: {_dragDropResult}");
        ui.TextWrapped("Try swapping payload presets, then drag the source button into the drop target to verify payload decoding and UI feedback.");

        ui.EndWindow();
    }

    // ─────────────────────────── ListClipper ───────────────────────────
    private void RenderListClipper(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ListClipperWindowTitle, new UiVector2(350f, 320f));
        ui.BeginWindow(ListClipperWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_list_clipper",
            ListClipperWindowTitle,
            "Virtualized rendering for very long lists where drawing every row every frame would be wasteful.",
            new UiColor(0xFF58A6FF),
            ("Items", _clipperItemCount.ToString("N0"), new UiColor(0xFF8DE1A6)));

        ui.Text($"Total items: {_clipperItemCount}");
        if (ui.BeginChild("clipper_child", new UiVector2(360f, 150f), true))
        {
            var clipper = new UiListClipper();
            var itemHeight = ui.GetTextLineHeightWithSpacing();
            var listOrigin = ui.GetCursorPos();
            clipper.Begin(ui, _clipperItemCount, itemHeight);
            while (clipper.Step())
            {
                ui.SetCursorPos(new UiVector2(listOrigin.X, listOrigin.Y + (clipper.DisplayStart * itemHeight)));
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    ui.Text($"Item #{i:D5}");
                }
            }

            ui.SetCursorPos(new UiVector2(listOrigin.X, listOrigin.Y + (_clipperItemCount * itemHeight)));
        }
        ui.EndChild();

        ui.EndWindow();
    }

    // ─────────────────────────── Style / Disabled / Misc ───────────────────────────
    private void RenderStyleWidget(UiImmediateContext ui)
    {
        InitWindowOnce(ui, StyleWindowTitle, new UiVector2(460f, 500f));
        ui.BeginWindow(StyleWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_style",
            StyleWindowTitle,
            "A compact playground for spacing, padding, disabled states, and accent tweaks so the rest of the showcase feels more intentional.",
            new UiColor(0xFF58A6FF),
            ("Spacing", $"{_styleItemSpacingX:0}×{_styleItemSpacingY:0}", new UiColor(0xFF8DE1A6)),
            ("Padding", $"{_styleFramePaddingX:0}×{_styleFramePaddingY:0}", new UiColor(0xFFFFD479)),
            ("Theme", _customStyle ? "Custom" : "Default", new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Style Push/Pop");
        ui.Checkbox("Custom Style", ref _customStyle);
        if (_customStyle)
        {
            ui.PushStyleColor(UiStyleColor.Button, new UiColor(0xFF2266AA));
            ui.PushStyleColor(UiStyleColor.ButtonHovered, new UiColor(0xFF3388CC));
        }
        ui.Button("Styled Button");
        if (_customStyle)
        {
            ui.PopStyleColor(2);
        }

        ui.SeparatorText("Style Variables");
        ui.SliderFloat("ItemSpacing X", ref _styleItemSpacingX, 4f, 28f, 0f, "0");
        ui.SliderFloat("ItemSpacing Y", ref _styleItemSpacingY, 2f, 20f, 0f, "0");
        ui.SliderFloat("FramePadding X", ref _styleFramePaddingX, 4f, 24f, 0f, "0");
        ui.SliderFloat("FramePadding Y", ref _styleFramePaddingY, 2f, 20f, 0f, "0");
        ui.SliderFloat("WindowPadding", ref _styleWindowPadding, 4f, 28f, 0f, "0");
        ui.PushStyleVar(UiStyleVar.ItemSpacing, new UiVector2(_styleItemSpacingX, _styleItemSpacingY));
        ui.PushStyleVar(UiStyleVar.FramePadding, new UiVector2(_styleFramePaddingX, _styleFramePaddingY));
        ui.PushStyleVar(UiStyleVar.WindowPadding, new UiVector2(_styleWindowPadding, _styleWindowPadding));
        if (ui.BeginChild("style_lab", new UiVector2(0f, 110f), true))
        {
            ui.Text("Preview with pushed spacing/padding");
            ui.Button("Primary");
            ui.SameLine();
            ui.Button("Secondary");
            ui.SliderFloat("Preview Slider", ref _stylePreviewValue, 0f, 1f, 0f, "0.00");
            ui.InputText("Preview Input", ref _clipboardSample, 256);
        }
        ui.EndChild();
        ui.PopStyleVar(3);

        ui.SeparatorText("Disabled");
        ui.Checkbox("Disable Below", ref _disableSection);
        ui.BeginDisabled(_disableSection);
        ui.SliderFloat("Disabled Slider", ref _disabledFloat, 0f, 1f, 0f, "0.00");
        ui.Button("Disabled Button");
        ui.InputText("Disabled Input", ref _inputText, 32);
        ui.EndDisabled();

        ui.SeparatorText("Plots");
        ui.PlotLines("PlotLines", _plotSin, _plotSin.Length, 0, "sin(x)", 0f, 1f, new UiVector2(350f, 40f));
        ui.PlotHistogram("PlotHistogram", _plotHist, _plotHist.Length, 0, null, 0f, 1f, new UiVector2(350f, 40f));

        ui.EndWindow();
    }

    // ─────────────────────────── FPS ───────────────────────────
    private void UpdateFps()
    {
        _fps = _fpsCounter.Tick().Fps;
    }

    private void RenderFpsOverlay(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var vsync = ui.GetVSync();
        var vsyncText = vsync ? "[VSync ON]" : "[VSync OFF]";
        var fpsText = $"FPS: {_fps:0}  {vsyncText}";
        var timingText = $"NF:{ui.GetNewFrameTimeMs():0.00} R:{ui.GetRenderTimeMs():0.00} S:{ui.GetSubmitTimeMs():0.00}ms";
        var textSize = ui.CalcTextSize(fpsText);
        var timingSize = ui.CalcTextSize(timingText);
        var maxWidth = MathF.Max(textSize.X, timingSize.X);
        var margin = 8f;
        var lineHeight = ui.GetTextLineHeight();
        var pos = new UiVector2(viewport.Size.X - maxWidth - margin, 4f);

        ui.DrawOverlayText(
            fpsText,
            new UiColor(200, 200, 200),
            UiItemHorizontalAlign.Right,
            UiItemVerticalAlign.Top,
            background: null,
            margin: new UiVector2(margin, 4f));

        ui.DrawOverlayText(
            timingText,
            new UiColor(160, 160, 160),
            UiItemHorizontalAlign.Right,
            UiItemVerticalAlign.Top,
            background: null,
            margin: new UiVector2(margin, 4f + lineHeight + 2f));

        // Click detection on vsync label
        var mouse = ui.GetMousePos();
        var vsyncSize = ui.CalcTextSize(vsyncText);
        var vsyncX = pos.X + textSize.X - vsyncSize.X;
        if (ui.IsMouseClicked(0) &&
            mouse.X >= vsyncX && mouse.X <= vsyncX + vsyncSize.X &&
            mouse.Y >= pos.Y && mouse.Y <= pos.Y + textSize.Y)
        {
            ui.SetVSync(!vsync);
        }
    }

    // ─────────────────────────── Sample: Settings Panel ───────────────────────────
    private void RenderSettingsPanel(UiImmediateContext ui)
    {
        InitWindowOnce(ui, SettingsWindowTitle, new UiVector2(460f, 500f));
        ui.BeginWindow(SettingsWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_settings",
            SettingsWindowTitle,
            "An applied sample that blends tabs, forms, toggles, and progress feedback into a settings surface with clearer information hierarchy.",
            new UiColor(0xFF58A6FF),
            ("Theme", _themeNames[_settingsTheme], new UiColor(0xFF8DE1A6)),
            ("Language", _languageNames[_settingsLanguage], new UiColor(0xFFFFD479)),
            ("Volume", $"{_settingsVolume * 100f:0}%", new UiColor(0xFF7EC8FF)));

        if (ui.BeginTabBar("SettingsTabs"))
        {
            if (ui.BeginTabItem("General"))
            {
                _settingsTab = 0;
                ui.EndTabItem();
            }
            if (ui.BeginTabItem("Audio"))
            {
                _settingsTab = 1;
                ui.EndTabItem();
            }
            if (ui.BeginTabItem("Graphics"))
            {
                _settingsTab = 2;
                ui.EndTabItem();
            }
            ui.EndTabBar();
        }

        switch (_settingsTab)
        {
            case 0:
                ui.SeparatorText("Appearance");
                ui.Combo(ref _settingsTheme, _themeNames, 4, "Theme");
                ui.Combo(ref _settingsLanguage, _languageNames, 5, "Language");

                ui.SeparatorText("Behavior");
                ui.Checkbox("Enable Notifications", ref _settingsNotifications);
                ui.Checkbox("Auto Save", ref _settingsAutoSave);
                break;

            case 1:
                ui.SeparatorText("Volume");
                ui.SliderFloat("Master Volume", ref _settingsVolume, 0f, 1f, 0.01f, "0%");
                ui.ProgressBar(_settingsVolume, new UiVector2(300f, 12f), $"{_settingsVolume * 100f:0}%");
                break;

            case 2:
                ui.SeparatorText("Display");
                ui.SliderFloat("Brightness", ref _settingsBrightness, 0.1f, 2.0f, 0.01f, "0.00");
                ui.Combo(ref _settingsQuality, _qualityNames, 4, "Quality");
                _settingsVSync = ui.GetVSync();
                if (ui.Checkbox("VSync", ref _settingsVSync))
                {
                    ui.SetVSync(_settingsVSync);
                }
                break;
        }

        ui.Separator();
        if (ui.Button("Apply")) { /* apply settings */ }
        ui.SameLine();
        if (ui.Button("Reset Defaults"))
        {
            _settingsTheme = 0;
            _settingsVolume = 0.7f;
            _settingsBrightness = 1.0f;
            _settingsNotifications = true;
            _settingsAutoSave = true;
            _settingsVSync = false;
            ui.SetVSync(false);
            _settingsLanguage = 0;
            _settingsQuality = 2;
        }

        ui.EndWindow();
    }

    // ─────────────────────────── Sample: Profile Editor ───────────────────────────
    private void RenderProfileEditor(UiImmediateContext ui)
    {
        InitWindowOnce(ui, ProfileEditorWindowTitle, new UiVector2(440f, 440f));
        ui.BeginWindow(ProfileEditorWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_profile",
            ProfileEditorWindowTitle,
            "A realistic profile editing surface with avatar color, identity fields, role selection, and privacy toggles.",
            new UiColor(0xFF58A6FF),
            ("Name", _profileName, new UiColor(0xFF8DE1A6)),
            ("Role", _roleNames[_profileRole], new UiColor(0xFFFFD479)),
            ("Public", _profilePublic ? "Yes" : "No", new UiColor(0xFF7EC8FF)));

        ui.SeparatorText("Avatar Color");
        ui.ColorEdit3("Avatar", ref _profileAvatarR, ref _profileAvatarG, ref _profileAvatarB);
        var avatarColor = new UiColor(
            ((uint)(_profileAvatarB * 255f) & 0xFF) |
            (((uint)(_profileAvatarG * 255f) & 0xFF) << 8) |
            (((uint)(_profileAvatarR * 255f) & 0xFF) << 16) |
            0xFF000000);
        ui.ColorButton("avatar_preview", avatarColor, new UiVector2(48f, 48f));
        ui.SameLine();
        ui.Text(_profileName);

        ui.SeparatorText("Personal Info");
        ui.InputText("Name", ref _profileName, 64);
        ui.InputText("Email", ref _profileEmail, 128);
        ui.InputTextMultiline("Bio", ref _profileBio, 512, 3);
        ui.Combo(ref _profileRole, _roleNames, 4, "Role");

        ui.SeparatorText("Privacy");
        ui.Checkbox("Public Profile", ref _profilePublic);
        ui.Checkbox("Newsletter", ref _profileNewsletter);

        ui.Separator();
        if (ui.Button("Save Profile")) { /* save */ }
        ui.SameLine();
        if (ui.Button("Cancel")) { _showProfileEditor = false; }

        ui.EndWindow();
    }

    // ─────────────────────────── Sample: File Browser ───────────────────────────
    private void RenderFileBrowser(UiImmediateContext ui)
    {
        InitWindowOnce(ui, FileBrowserWindowTitle, new UiVector2(560f, 460f));
        ui.BeginWindow(FileBrowserWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_file_browser",
            FileBrowserWindowTitle,
            "A split-pane explorer sample combining folder trees, selectable rows, and a details table.",
            new UiColor(0xFF58A6FF),
            ("Folders", _fileBrowserFolders.Length.ToString(), new UiColor(0xFF8DE1A6)),
            ("Selected", _fileBrowserSelected >= 0 ? _fileBrowserSelected.ToString() : "None", new UiColor(0xFFFFD479)));

        ui.Columns(2, true);
        ui.SetColumnWidth(0, 160f);

        // Left: folder tree
        ui.SeparatorText("Folders");
        for (var f = 0; f < _fileBrowserFolders.Length; f++)
        {
            if (ui.TreeNode(_fileBrowserFolders[f], _fileBrowserExpanded[f]))
            {
                _fileBrowserExpanded[f] = true;
                for (var i = 0; i < _fileBrowserFiles[f].Length; i++)
                {
                    var globalIdx = f * 100 + i;
                    if (ui.Selectable($"  {_fileBrowserFiles[f][i]}", _fileBrowserSelected == globalIdx))
                    {
                        _fileBrowserSelected = globalIdx;
                    }
                }
                ui.TreePop();
            }
            else
            {
                _fileBrowserExpanded[f] = false;
            }
        }

        // Right: file details table
        ui.NextColumn();
        ui.SeparatorText("Files");

        if (ui.BeginTable("fb_table", 3, UiTableFlags.Borders | UiTableFlags.RowBg))
        {
            ui.TableSetupColumn("Name", 180f, 0f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Size", 80f, 0f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Folder", 100f, 0f, UiTableColumnFlags.None);
            ui.TableHeadersRow();

            for (var f = 0; f < _fileBrowserFolders.Length; f++)
            {
                if (!_fileBrowserExpanded[f]) continue;
                for (var i = 0; i < _fileBrowserFiles[f].Length; i++)
                {
                    ui.TableNextRow();
                    var globalIdx = f * 100 + i;
                    var isSelected = _fileBrowserSelected == globalIdx;
                    if (isSelected)
                    {
                        ui.TableSetBgColor(UiTableBgTarget.RowBg0, new UiColor(0x40335588));
                    }
                    ui.Text(_fileBrowserFiles[f][i]);
                    ui.TableNextColumn();
                    ui.Text(_fileBrowserSizes[f][i]);
                    ui.TableNextColumn();
                    ui.Text(_fileBrowserFolders[f]);
                }
            }

            ui.EndTable();
        }

        ui.Columns(1, false);
        ui.EndWindow();
    }

    // ─────────────────────────── Sample: Log Viewer ───────────────────────────
    private void RenderLogViewer(UiImmediateContext ui)
    {
        InitWindowOnce(ui, LogViewerWindowTitle, new UiVector2(580f, 440f));
        ui.BeginWindow(LogViewerWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_log_viewer",
            LogViewerWindowTitle,
            "Filtering, severity toggles, clipping, and auto-scroll behavior for long-running diagnostic consoles.",
            new UiColor(0xFF58A6FF),
            ("Entries", _logEntries.Count.ToString(), new UiColor(0xFF8DE1A6)),
            ("Filter", string.IsNullOrEmpty(_logFilter) ? "(none)" : _logFilter, new UiColor(0xFFFFD479)),
            ("Auto", _logAutoScroll ? "Scroll on" : "Scroll off", new UiColor(0xFF7EC8FF)));

        // Toolbar
        ui.InputText("Filter", ref _logFilter, 128);
        ui.SameLine();
        if (ui.SmallButton("Clear Filter")) _logFilter = "";

        ui.Checkbox("INFO", ref _logShowInfo);
        ui.SameLine();
        ui.Checkbox("WARN", ref _logShowWarn);
        ui.SameLine();
        ui.Checkbox("ERROR", ref _logShowError);
        ui.SameLine();
        ui.Checkbox("Auto-scroll", ref _logAutoScroll);

        ui.Separator();

        // Filter entries
        var filterLower = _logFilter.ToLowerInvariant();
        if (ui.BeginChild("log_list", new UiVector2(0f, 300f), true))
        {
            var clipper = new UiListClipper();
            var itemHeight = ui.GetTextLineHeightWithSpacing();
            var listOrigin = ui.GetCursorPos();
            // Count visible entries for clipper
            var visibleEntries = new List<int>();
            for (var i = 0; i < _logEntries.Count; i++)
            {
                var (level, message, _) = _logEntries[i];
                if (level == "INFO" && !_logShowInfo) continue;
                if (level == "WARN" && !_logShowWarn) continue;
                if (level == "ERROR" && !_logShowError) continue;
                if (filterLower.Length > 0 && !message.ToLowerInvariant().Contains(filterLower)) continue;
                visibleEntries.Add(i);
            }

            clipper.Begin(ui, visibleEntries.Count, itemHeight);
            while (clipper.Step())
            {
                ui.SetCursorPos(new UiVector2(listOrigin.X, listOrigin.Y + (clipper.DisplayStart * itemHeight)));
                for (var idx = clipper.DisplayStart; idx < clipper.DisplayEnd; idx++)
                {
                    var entry = _logEntries[visibleEntries[idx]];
                    ui.TextColored(entry.Color, entry.Message);
                }
            }

            ui.SetCursorPos(new UiVector2(listOrigin.X, listOrigin.Y + (visibleEntries.Count * itemHeight)));

            if (_logAutoScroll && visibleEntries.Count > 0)
            {
                ui.SetScrollY(float.MaxValue);
            }
        }
        ui.EndChild();

        ui.Text($"Total: {_logEntries.Count} entries");

        ui.EndWindow();
    }

    private void RenderMarkdownStudio(UiImmediateContext ui)
    {
        InitWindowOnce(ui, MarkdownStudioWindowTitle, new UiVector2(1100f, 760f));
        ui.BeginWindow(MarkdownStudioWindowTitle);

        RenderShowcaseHero(
            ui,
            "hero_markdown",
            MarkdownStudioWindowTitle,
            "A side-by-side editor and live preview sample that feels closer to a real product surface than a bare widget test harness.",
            new UiColor(0xFF7EC8FF),
            ("Mode", "Editor + preview", new UiColor(0xFF8DE1A6)),
            ("Source", $"{_markdownEditor.Text.Length} chars", new UiColor(0xFFFFD479)),
            ("Widget", "Instance-based custom UI", new UiColor(0xFF7EC8FF)));

        ui.TextWrapped("This sample uses the custom widget mechanism: instance-based widgets render themselves by receiving the current ui context.");

        if (ui.SmallButton("Load Sample"))
        {
            _markdownEditor.Text = SampleMarkdown;
        }
        ui.SameLine();
        if (ui.SmallButton("Clear"))
        {
            _markdownEditor.Text = string.Empty;
        }

        ui.Separator();
        var content = ui.GetContentRegionAvail();
        var titleReserve = ui.GetFrameHeightWithSpacing();
        var statsReserve = ui.GetTextLineHeightWithSpacing() + 4f;
        var widgetHeight = MathF.Max(120f, MathF.Max(240f, content.Y) - titleReserve - statsReserve - 8f);

        ui.Columns(2, false);

        ui.SeparatorText("Editor");
        _markdownEditor.Height = widgetHeight;
        _markdownEditor.Render(ui);

        ui.NextColumn();
        ui.SeparatorText("Preview");
        _markdownViewer.Markdown = _markdownEditor.Text;
        _markdownViewer.Height = widgetHeight;
        _markdownViewer.Render(ui);

        ui.Columns(1, false);

        ui.EndWindow();
    }
}
