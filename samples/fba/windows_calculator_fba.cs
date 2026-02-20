// FBA: Windows 표준 계산기 스타일 샘플
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.Globalization;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "계산기",
        Width = 420,
        Height = 700,
        VSync = true
    },
    Renderer = new DuxelRendererOptions
    {
        EnableDWriteText = true,
        FontLinearSampling = false
    },
    Font = new DuxelFontOptions
    {
        FastStartup = true,
        UseBuiltInAsciiAtStartup = false,
        StartupFontSize = 16,
        FontSize = 16,
        AtlasWidth = 2048,
        AtlasHeight = 2048,
        Oversample = 2
    },
    Screen = new WindowsCalculatorStandardScreen()
});

public sealed class WindowsCalculatorStandardScreen : UiScreen
{
    private enum ProgrammerBase
    {
        Hex = 16,
        Dec = 10,
        Oct = 8,
        Bin = 2,
    }

    private enum CalcButtonTone
    {
        Function,
        Digit,
        Operator,
        Equals,
        Disabled,
    }

    private string _display = "0";
    private string _history = string.Empty;
    private readonly List<string> _expressionTokens = [];
    private int _openParenCount;
    private bool _startNewEntry = true;
    private bool _error;
    private ProgrammerBase _selectedBase = ProgrammerBase.Dec;
    private const float DisplayValueScale = 2f;
    private const float DisplayExprScale = 1f;
    private readonly UiFpsCounter _fpsCounter = new(0.25d);
    private double _lastTime;
    private float _fps;
    private float _frameMs;
    private float _fxTime;
    private float _displayImpact;
    private string _displayPrev = "0";
    private readonly List<ButtonRipple> _buttonRipples = [];

    private sealed class ButtonRipple
    {
        public UiVector2 Center;
        public float Radius;
        public float Life;
        public UiColor Color;
    }

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0d, 0.05d);
        _lastTime = now;
        _fxTime += (float)delta;
        _frameMs = (float)(delta * 1000d);

        var fpsSample = _fpsCounter.Tick(delta);
        if (fpsSample.Updated)
        {
            _fps = fpsSample.Fps;
        }

        if (!string.Equals(_displayPrev, _display, StringComparison.Ordinal))
        {
            _displayPrev = _display;
            _displayImpact = 1f;
        }

        _displayImpact = MathF.Max(0f, _displayImpact - (float)(delta * 2.4d));
        UpdateRipples((float)delta);

        ui.SetDirectTextEnabled(true);
        ui.EnableRootViewportContentLayout();

        DrawCyberBackdrop(ui);

        ui.PushStyleColor(UiStyleColor.WindowBg, new UiColor(0xCC111116));
        ui.PushStyleVar(UiStyleVar.WindowPadding, new UiVector2(8f, 8f));
        ui.PushStyleVar(UiStyleVar.ItemSpacing, new UiVector2(3f, 4f));

        RenderHeader(ui);
        ui.Dummy(new UiVector2(0f, 6f));

        RenderDisplay(ui);
        ui.Dummy(new UiVector2(0f, 6f));
        RenderButtons(ui);

        RenderRipples(ui);
        DuxelApp.RequestFrame();

        ui.PopStyleVar(2);
        ui.PopStyleColor();
    }

    private static void RenderHeader(UiImmediateContext ui)
    {
        var rowStart = ui.GetCursorPos();
        var rowHeight = 34f;

        ui.PushStyleColor(UiStyleColor.Button, new UiColor(0xFF1B1B1B));
        ui.PushStyleColor(UiStyleColor.ButtonHovered, new UiColor(0xFF292929));
        ui.PushStyleColor(UiStyleColor.ButtonActive, new UiColor(0xFF323232));

        _ = ui.Button("≡", new UiVector2(34f, rowHeight));
        ui.SameLine(verticalAlign: UiItemVerticalAlign.Center);
        ui.Text("프로그래머");

        var rowBottom = MathF.Max(ui.GetItemRectMax().Y, rowStart.Y + rowHeight);
        ui.SetCursorPos(new UiVector2(rowStart.X, rowBottom + 4f));

        ui.PopStyleColor(3);
    }

    private void DrawCyberBackdrop(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var canvas = new UiRect(viewport.WorkPos.X, viewport.WorkPos.Y, viewport.WorkSize.X, viewport.WorkSize.Y);
        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;

        drawList.AddRectFilled(canvas, new UiColor(0xFF090A12), white, canvas);

        var gridStep = 28f;
        var xOffset = (_fxTime * 42f) % gridStep;
        var yOffset = (_fxTime * 25f) % gridStep;

        for (var x = canvas.X - xOffset; x <= canvas.X + canvas.Width; x += gridStep)
        {
            drawList.AddLine(new UiVector2(x, canvas.Y), new UiVector2(x, canvas.Y + canvas.Height), new UiColor(0x1B2ED1FF), 1f);
        }

        for (var y = canvas.Y - yOffset; y <= canvas.Y + canvas.Height; y += gridStep)
        {
            drawList.AddLine(new UiVector2(canvas.X, y), new UiVector2(canvas.X + canvas.Width, y), new UiColor(0x1228A0E0), 1f);
        }

        var orbA = new UiVector2(canvas.X + canvas.Width * 0.18f + MathF.Sin(_fxTime * 1.9f) * 48f, canvas.Y + canvas.Height * 0.22f + MathF.Cos(_fxTime * 1.2f) * 28f);
        var orbB = new UiVector2(canvas.X + canvas.Width * 0.78f + MathF.Cos(_fxTime * 1.4f) * 56f, canvas.Y + canvas.Height * 0.74f + MathF.Sin(_fxTime * 1.8f) * 32f);

        drawList.AddCircleFilled(orbA, 64f, new UiColor(0x2232B4FF), white, canvas, 26);
        drawList.AddCircleFilled(orbB, 72f, new UiColor(0x221BC8A4), white, canvas, 26);

        var scanY = canvas.Y + ((_fxTime * 220f) % MathF.Max(1f, canvas.Height));
        drawList.AddRectFilled(new UiRect(canvas.X, scanY, canvas.Width, 2.5f), new UiColor(0x2AF5F5FF), white, canvas);
    }

    private void UpdateRipples(float delta)
    {
        for (var i = _buttonRipples.Count - 1; i >= 0; i--)
        {
            var ripple = _buttonRipples[i];
            ripple.Radius += delta * 220f;
            ripple.Life -= delta * 1.6f;
            if (ripple.Life <= 0f)
            {
                _buttonRipples.RemoveAt(i);
            }
        }
    }

    private void RenderRipples(UiImmediateContext ui)
    {
        if (_buttonRipples.Count == 0)
        {
            return;
        }

        var drawList = ui.GetWindowDrawList();
        var viewport = ui.GetMainViewport();
        var clip = new UiRect(viewport.WorkPos.X, viewport.WorkPos.Y, viewport.WorkSize.X, viewport.WorkSize.Y);
        var white = ui.WhiteTextureId;

        for (var i = 0; i < _buttonRipples.Count; i++)
        {
            var ripple = _buttonRipples[i];
            var alpha = (byte)Math.Clamp((int)(ripple.Life * 120f), 0, 255);
            var color = new UiColor((ripple.Color.Rgba & 0x00FFFFFFu) | ((uint)alpha << 24));
            drawList.AddCircleFilled(ripple.Center, ripple.Radius, color, white, clip, 24);
        }
    }

    private void RenderDisplay(UiImmediateContext ui)
    {
        ui.PushStyleVar(UiStyleVar.FramePadding, new UiVector2(0f, 0f));
        ui.PushStyleColor(UiStyleColor.Button, new UiColor(0xCC1B1B1B));
        ui.PushStyleColor(UiStyleColor.ButtonHovered, new UiColor(0xCC1B1B1B));
        ui.PushStyleColor(UiStyleColor.ButtonActive, new UiColor(0xCC1B1B1B));

        var frameSize = new UiVector2(MathF.Max(320f, GetContentAvailWidth(ui)), 88f);
        _ = ui.Button("##display_frame", frameSize);
        var frameMin = ui.GetItemRectMin();
        var frameExtent = ui.GetItemRectSize();
        var drawList = ui.GetWindowDrawList();
        var frameRect = new UiRect(frameMin.X, frameMin.Y, frameExtent.X, frameExtent.Y);
        var viewport = ui.GetMainViewport();
        var viewportClip = new UiRect(viewport.WorkPos.X, viewport.WorkPos.Y, viewport.WorkSize.X, viewport.WorkSize.Y);
        var glow = ui.AnimateFloat("display.glow", _displayImpact > 0f ? 1f : 0f, 0.13f, UiAnimationEasing.OutCubic);
        if (glow > 0.001f)
        {
            var glowAlpha = (uint)Math.Clamp((int)(glow * 70f), 0, 255);
            drawList.AddRectFilled(
                new UiRect(frameRect.X - 4f, frameRect.Y - 4f, frameRect.Width + 8f, frameRect.Height + 8f),
                new UiColor((glowAlpha << 24) | 0x35B6FFu),
                ui.WhiteTextureId,
            viewportClip);
        }
        drawList.AddRect(frameRect, new UiColor(0x8848C8FF), 0f, 1.2f);

        var sweepX = frameRect.X + 8f + ((MathF.Sin(_fxTime * 3.4f) + 1f) * 0.5f) * MathF.Max(4f, frameRect.Width - 20f);
        drawList.AddRectFilled(new UiRect(sweepX, frameRect.Y + 4f, 2f, frameRect.Height - 8f), new UiColor(0x66DDF9FF), ui.WhiteTextureId, frameRect);

        var expressionText = BuildDisplayExpressionText();
        if (!string.IsNullOrWhiteSpace(expressionText))
        {
            var expressionRect = new UiRect(
                frameMin.X + 10f,
                frameMin.Y + 12f,
                MathF.Max(0f, frameExtent.X - 20f),
                22f);

            ui.DrawTextAligned(
                expressionRect,
                expressionText,
                new UiColor(0xFFB8C6D8),
                UiItemHorizontalAlign.Right,
                UiItemVerticalAlign.Top,
                fontSize: 17f * DisplayExprScale,
                clipToContainer: false);
        }

        var valueText = _error ? "오류" : _display;
        var valueRect = new UiRect(
            frameMin.X + 10f,
            frameMin.Y + 6f,
            MathF.Max(0f, frameExtent.X - 20f),
            MathF.Max(0f, frameExtent.Y - 12f));

        ui.DrawTextAligned(
            valueRect,
            valueText,
            new UiColor(0xFFF4F8FF),
            UiItemHorizontalAlign.Right,
            UiItemVerticalAlign.Bottom,
            fontSize: 44f,
            clipToContainer: false);

        ui.Dummy(new UiVector2(0f, 4f));

        var numericValue = ParseDisplayInteger();
        RenderBaseListBox(ui, numericValue);
        ui.Dummy(new UiVector2(0f, 6f));

        ui.PopStyleColor(3);
        ui.PopStyleVar();
    }

    private void RenderBaseListBox(UiImmediateContext ui, long numericValue)
    {
        var listWidth = MathF.Max(220f, GetContentAvailWidth(ui));
        var rows = new (ProgrammerBase Base, string Label, string Value)[]
        {
            (ProgrammerBase.Hex, "HEX", FormatByBase(numericValue, ProgrammerBase.Hex)),
            (ProgrammerBase.Dec, "DEC", FormatByBase(numericValue, ProgrammerBase.Dec)),
            (ProgrammerBase.Oct, "OCT", FormatByBase(numericValue, ProgrammerBase.Oct)),
            (ProgrammerBase.Bin, "BIN", FormatByBase(numericValue, ProgrammerBase.Bin)),
        };

        ui.PushStyleColor(UiStyleColor.FrameBg, new UiColor(0xCC1B1B1B));
        ui.PushStyleColor(UiStyleColor.HeaderHovered, new UiColor(0xCC2A2A2A));
        ui.PushStyleColor(UiStyleColor.HeaderActive, new UiColor(0xCC2A2A2A));

        if (ui.BeginListBox(new UiVector2(listWidth, 122f), rows.Length, "base_list"))
        {
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var selected = _selectedBase == row.Base;

                if (ui.ListBoxRow($"base_row_{i}", selected, new UiVector2(0f, 26f), out var rowRect))
                {
                    SetBase(row.Base);
                }

                ui.DrawKeyValueRow(
                    rowRect,
                    row.Label,
                    row.Value,
                    selected: selected,
                    keyColor: selected ? new UiColor(0xFFFFFFFF) : new UiColor(0xFFECECEC),
                    valueColor: selected ? new UiColor(0xFFFFFFFF) : new UiColor(0xFFD0D0D0),
                    keyWidth: 46f,
                    horizontalPadding: 12f,
                    verticalPadding: 4f,
                    clipToRow: true);
            }

            ui.EndListBox();
        }

        ui.PopStyleColor(3);
    }

    private static float GetContentAvailWidth(UiImmediateContext ui)
    {
        return MathF.Max(0f, ui.GetContentRegionAvail().X);
    }

    private static float GetContentAvailHeight(UiImmediateContext ui)
    {
        return MathF.Max(0f, ui.GetContentRegionAvail().Y);
    }

    private void RenderButtons(UiImmediateContext ui)
    {
        var avail = GetContentAvailWidth(ui);
        var availHeight = GetContentAvailHeight(ui);
        var spacing = 3f;
        var buttonWidth = MathF.Max(62f, MathF.Floor((avail - (spacing * 4f)) / 5f));
        var rowCount = 6f;
        var computedHeight = MathF.Floor((availHeight - (spacing * (rowCount - 1f))) / rowCount);
        var buttonHeight = MathF.Max(44f, computedHeight);

        RenderProgrammerRow(ui, avail, buttonWidth, spacing, buttonHeight,
            ("A", () => OnDigit("A"), CalcButtonTone.Digit, IsDigitButtonEnabled("A")),
            ("<<", () => OnOperator("<<"), CalcButtonTone.Function, true),
            (">>", () => OnOperator(">>"), CalcButtonTone.Function, true),
            ("C##clear", OnClearAll, CalcButtonTone.Function, true),
            ("BS", OnBackspace, CalcButtonTone.Function, true));

        RenderProgrammerRow(ui, avail, buttonWidth, spacing, buttonHeight,
            ("B", () => OnDigit("B"), CalcButtonTone.Digit, IsDigitButtonEnabled("B")),
            ("(", OnOpenParen, CalcButtonTone.Function, true),
            (")", OnCloseParen, CalcButtonTone.Function, true),
            ("%", OnPercent, CalcButtonTone.Function, true),
            ("/", () => OnOperator("/"), CalcButtonTone.Operator, true));

        RenderProgrammerRow(ui, avail, buttonWidth, spacing, buttonHeight,
            ("C##hex", () => OnDigit("C"), CalcButtonTone.Digit, IsDigitButtonEnabled("C")),
            ("7", () => OnDigit("7"), CalcButtonTone.Digit, IsDigitButtonEnabled("7")),
            ("8", () => OnDigit("8"), CalcButtonTone.Digit, IsDigitButtonEnabled("8")),
            ("9", () => OnDigit("9"), CalcButtonTone.Digit, IsDigitButtonEnabled("9")),
            ("*", () => OnOperator("*"), CalcButtonTone.Operator, true));

        RenderProgrammerRow(ui, avail, buttonWidth, spacing, buttonHeight,
            ("D", () => OnDigit("D"), CalcButtonTone.Digit, IsDigitButtonEnabled("D")),
            ("4", () => OnDigit("4"), CalcButtonTone.Digit, IsDigitButtonEnabled("4")),
            ("5", () => OnDigit("5"), CalcButtonTone.Digit, IsDigitButtonEnabled("5")),
            ("6", () => OnDigit("6"), CalcButtonTone.Digit, IsDigitButtonEnabled("6")),
            ("-", () => OnOperator("-"), CalcButtonTone.Operator, true));

        RenderProgrammerRow(ui, avail, buttonWidth, spacing, buttonHeight,
            ("E", () => OnDigit("E"), CalcButtonTone.Digit, IsDigitButtonEnabled("E")),
            ("1", () => OnDigit("1"), CalcButtonTone.Digit, IsDigitButtonEnabled("1")),
            ("2", () => OnDigit("2"), CalcButtonTone.Digit, IsDigitButtonEnabled("2")),
            ("3", () => OnDigit("3"), CalcButtonTone.Digit, IsDigitButtonEnabled("3")),
            ("+", () => OnOperator("+"), CalcButtonTone.Operator, true));

        RenderProgrammerRow(ui, avail, buttonWidth, spacing, buttonHeight,
            ("F", () => OnDigit("F"), CalcButtonTone.Digit, IsDigitButtonEnabled("F")),
            ("+/-", OnToggleSign, CalcButtonTone.Function, true),
            ("0", () => OnDigit("0"), CalcButtonTone.Digit, IsDigitButtonEnabled("0")),
            (".", OnDot, CalcButtonTone.Digit, _selectedBase == ProgrammerBase.Dec),
            ("=", OnEquals, CalcButtonTone.Equals, true));
    }

    private void RenderProgrammerRow(
        UiImmediateContext ui,
        float totalWidth,
        float buttonWidth,
        float spacing,
        float buttonHeight,
        params (string Label, Action Action, CalcButtonTone Tone, bool Enabled)[] buttons)
    {
        var widthBudget = MathF.Max(0f, totalWidth - (spacing * (buttons.Length - 1)));
        var firstColumnsWidth = buttonWidth * Math.Max(0, buttons.Length - 1);
        var lastWidth = MathF.Max(buttonWidth, widthBudget - firstColumnsWidth);

        for (var i = 0; i < buttons.Length; i++)
        {
            var (label, action, tone, enabled) = buttons[i];
            var effectiveTone = enabled ? tone : CalcButtonTone.Disabled;
            var width = i == buttons.Length - 1 ? lastWidth : buttonWidth;

            if (DrawFxButton(ui, label, new UiVector2(width, buttonHeight), effectiveTone) && enabled)
            {
                action();
                DuxelApp.RequestFrame();
            }

            if (i < buttons.Length - 1)
            {
                ui.SameLine();
            }
        }
    }

    private bool DrawFxButton(UiImmediateContext ui, string label, UiVector2 size, CalcButtonTone tone)
    {
        var id = $"fxbtn::{label}";
        ui.InvisibleButton(id, size);
        var rectMin = ui.GetItemRectMin();
        var rectSize = ui.GetItemRectSize();
        var rect = new UiRect(rectMin.X, rectMin.Y, rectSize.X, rectSize.Y);

        var hovered = ui.IsItemHovered();
        var held = ui.IsItemActive();
        var clicked = ui.IsItemClicked();

        var hoverBlend = ui.AnimateFloat($"{id}/hover", hovered ? 1f : 0f, 0.10f, UiAnimationEasing.OutCubic);
        var holdBlend = ui.AnimateFloat($"{id}/hold", held ? 1f : 0f, 0.08f, UiAnimationEasing.OutCubic);
        var pulse = (MathF.Sin((_fxTime * 4.2f) + rect.X * 0.02f) + 1f) * 0.5f;

        var (baseColor, hoveredColor, activeColor, textColor) = GetToneColors(tone);
        var drawList = ui.GetWindowDrawList();
        var white = ui.WhiteTextureId;
        var viewport = ui.GetMainViewport();
        var clip = new UiRect(viewport.WorkPos.X, viewport.WorkPos.Y, viewport.WorkSize.X, viewport.WorkSize.Y);

        var glowAlpha = (uint)Math.Clamp((int)((hoverBlend * 36f) + (holdBlend * 74f) + (pulse * 10f)), 0, 255);
        if (glowAlpha > 0)
        {
            drawList.AddRectFilled(
                new UiRect(rect.X - 3f, rect.Y - 3f, rect.Width + 6f, rect.Height + 6f),
                new UiColor((glowAlpha << 24) | 0x3AB9FFu),
                white,
                clip);
        }

        drawList.AddRectFilled(rect, baseColor, white, clip);

        var topSheenRect = new UiRect(rect.X + 1f, rect.Y + 1f, MathF.Max(0f, rect.Width - 2f), MathF.Max(0f, rect.Height * 0.45f));
        drawList.AddRectFilled(topSheenRect, new UiColor(0x1FFFFFFF), white, clip);

        var bottomShadeRect = new UiRect(rect.X + 1f, rect.Y + rect.Height * 0.52f, MathF.Max(0f, rect.Width - 2f), MathF.Max(0f, rect.Height * 0.46f));
        drawList.AddRectFilled(bottomShadeRect, new UiColor(0x22000000), white, clip);

        if (hoverBlend > 0.001f)
        {
            var alpha = (uint)Math.Clamp((int)(hoverBlend * 120f), 0, 255);
            drawList.AddRectFilled(rect, new UiColor((alpha << 24) | (hoveredColor.Rgba & 0x00FFFFFFu)), white, clip);
        }

        if (holdBlend > 0.001f)
        {
            var alpha = (uint)Math.Clamp((int)(holdBlend * 180f), 0, 255);
            drawList.AddRectFilled(rect, new UiColor((alpha << 24) | (activeColor.Rgba & 0x00FFFFFFu)), white, clip);
        }

        drawList.AddRect(rect, new UiColor(0xAA36C8FF), 0f, 1f);
        drawList.AddRect(new UiRect(rect.X + 1f, rect.Y + 1f, MathF.Max(0f, rect.Width - 2f), MathF.Max(0f, rect.Height - 2f)), new UiColor(0x4438E3FF), 0f, 1f);

        var visibleLabel = ExtractLabel(label);
        ui.DrawTextAligned(rect, visibleLabel, textColor, UiItemHorizontalAlign.Center, UiItemVerticalAlign.Center);

        if (clicked)
        {
            var toneColor = tone switch
            {
                CalcButtonTone.Equals => new UiColor(0x992D7DFF),
                CalcButtonTone.Operator => new UiColor(0x9935D0FF),
                CalcButtonTone.Digit => new UiColor(0x9964B4FF),
                CalcButtonTone.Disabled => new UiColor(0x553A3A3A),
                _ => new UiColor(0x9975F6E2),
            };
            _buttonRipples.Add(new ButtonRipple
            {
                Center = new UiVector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f),
                Radius = 8f,
                Life = 1f,
                Color = toneColor,
            });
        }

        return clicked;
    }

    private static string ExtractLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return string.Empty;
        }

        var idx = label.IndexOf("##", StringComparison.Ordinal);
        return idx < 0 ? label : label[..idx];
    }

    private static (UiColor Button, UiColor Hovered, UiColor Active, UiColor Text) GetToneColors(CalcButtonTone tone)
    {
        return tone switch
        {
            CalcButtonTone.Digit => (
                new UiColor(0xCC2B2B2B),
                new UiColor(0xCC353535),
                new UiColor(0xCC444444),
                new UiColor(0xFFFFFFFF)
            ),
            CalcButtonTone.Operator => (
                new UiColor(0xCC202020),
                new UiColor(0xCC2C2C2C),
                new UiColor(0xCC3A3A3A),
                new UiColor(0xFFFFFFFF)
            ),
            CalcButtonTone.Equals => (
                new UiColor(0xCC2D7DFF),
                new UiColor(0xCC3A89FF),
                new UiColor(0xCC1D6EF5),
                new UiColor(0xFFFFFFFF)
            ),
            CalcButtonTone.Disabled => (
                new UiColor(0xCC151515),
                new UiColor(0xCC151515),
                new UiColor(0xCC151515),
                new UiColor(0xFF5E5E5E)
            ),
            _ => (
                new UiColor(0xCC1A1A1A),
                new UiColor(0xCC262626),
                new UiColor(0xCC333333),
                new UiColor(0xFFFFFFFF)
            ),
        };
    }

    private void SetBase(ProgrammerBase targetBase)
    {
        if (_selectedBase == targetBase)
        {
            return;
        }

        var value = ParseDisplayInteger();
        _selectedBase = targetBase;
        _display = FormatByBase(value, _selectedBase);
        _expressionTokens.Clear();
        _openParenCount = 0;
        _history = string.Empty;
        _startNewEntry = true;
    }

    private long ParseDisplayInteger()
    {
        if (_error)
        {
            return 0;
        }

        var text = _display.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var negative = text.StartsWith("-", StringComparison.Ordinal);
        if (negative)
        {
            text = text[1..];
        }

        try
        {
            long value = _selectedBase switch
            {
                ProgrammerBase.Hex => Convert.ToInt64(string.IsNullOrEmpty(text) ? "0" : text, 16),
                ProgrammerBase.Oct => Convert.ToInt64(string.IsNullOrEmpty(text) ? "0" : text, 8),
                ProgrammerBase.Bin => Convert.ToInt64(string.IsNullOrEmpty(text) ? "0" : text, 2),
                _ => (long)double.Parse(string.IsNullOrEmpty(text) ? "0" : text, CultureInfo.InvariantCulture),
            };

            return negative ? -value : value;
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatByBase(long value, ProgrammerBase numberBase)
    {
        return numberBase switch
        {
            ProgrammerBase.Hex => value < 0 ? "-" + Convert.ToString(-value, 16).ToUpperInvariant() : Convert.ToString(value, 16).ToUpperInvariant(),
            ProgrammerBase.Oct => value < 0 ? "-" + Convert.ToString(-value, 8) : Convert.ToString(value, 8),
            ProgrammerBase.Bin => value < 0 ? "-" + Convert.ToString(-value, 2) : Convert.ToString(value, 2),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };
    }

    private bool IsValidDigitForBase(string digit)
    {
        if (string.IsNullOrEmpty(digit))
        {
            return false;
        }

        var c = char.ToUpperInvariant(digit[0]);
        return _selectedBase switch
        {
            ProgrammerBase.Bin => c is '0' or '1',
            ProgrammerBase.Oct => c is >= '0' and <= '7',
            ProgrammerBase.Dec => c is >= '0' and <= '9',
            _ => c is (>= '0' and <= '9') or (>= 'A' and <= 'F'),
        };
    }

    private bool IsDigitButtonEnabled(string digit)
    {
        return IsValidDigitForBase(digit);
    }

    private void OnDigit(string digit)
    {
        digit = digit.ToUpperInvariant();
        if (!IsValidDigitForBase(digit))
        {
            return;
        }

        if (_error)
        {
            OnClearAll();
        }

        if (_startNewEntry)
        {
            _display = digit;
            _startNewEntry = false;
            return;
        }

        if (_display == "0")
        {
            _display = digit;
            return;
        }

        if (_display.Length < 24)
        {
            _display += digit;
        }
    }

    private void OnDot()
    {
        if (_selectedBase != ProgrammerBase.Dec)
        {
            return;
        }

        if (_error)
        {
            OnClearAll();
        }

        if (_startNewEntry)
        {
            _display = "0.";
            _startNewEntry = false;
            return;
        }

        if (!_display.Contains('.', StringComparison.Ordinal))
        {
            _display += ".";
        }
    }

    private void OnOperator(string op)
    {
        if (_error)
        {
            return;
        }

        CommitCurrentEntryIfNeeded();

        if (_expressionTokens.Count == 0)
        {
            _expressionTokens.Add(ToNumericToken(ParseDisplay()));
        }

        if (_expressionTokens.Count == 0)
        {
            return;
        }

        var tail = _expressionTokens[^1];
        if (IsOperatorToken(tail))
        {
            _expressionTokens[^1] = op;
        }
        else if (tail == "(")
        {
            if (op == "-")
            {
                _display = "-";
                _startNewEntry = false;
                _history = BuildDisplayExpressionText();
                return;
            }

            return;
        }
        else
        {
            _expressionTokens.Add(op);
        }

        _startNewEntry = true;
        _history = BuildDisplayExpressionText();
    }

    private void OnOpenParen()
    {
        if (_error)
        {
            return;
        }

        if (!_startNewEntry)
        {
            CommitCurrentEntryIfNeeded();
            _expressionTokens.Add("*");
        }
        else if (_expressionTokens.Count > 0)
        {
            var tail = _expressionTokens[^1];
            if (tail == ")" || IsNumericToken(tail))
            {
                _expressionTokens.Add("*");
            }
        }

        _expressionTokens.Add("(");
        _openParenCount++;
        _startNewEntry = true;
        _history = BuildDisplayExpressionText();
    }

    private void OnCloseParen()
    {
        if (_error || _openParenCount <= 0)
        {
            return;
        }

        CommitCurrentEntryIfNeeded();
        if (_expressionTokens.Count == 0)
        {
            return;
        }

        var tail = _expressionTokens[^1];
        if (IsOperatorToken(tail) || tail == "(")
        {
            return;
        }

        _expressionTokens.Add(")");
        _openParenCount--;
        if (TryEvaluateExpression(_expressionTokens, out var result))
        {
            SetDisplay(result);
            _startNewEntry = true;
        }
        else
        {
            SetError();
            return;
        }

        _history = BuildDisplayExpressionText();
    }

    private void OnEquals()
    {
        if (_error)
        {
            return;
        }

        var evalTokens = new List<string>(_expressionTokens.Count + 8);
        evalTokens.AddRange(_expressionTokens);

        if (!_startNewEntry)
        {
            evalTokens.Add(ToNumericToken(ParseDisplay()));
        }
        else if (evalTokens.Count > 0 && IsOperatorToken(evalTokens[^1]))
        {
            evalTokens.Add(ToNumericToken(ParseDisplay()));
        }

        for (var i = 0; i < _openParenCount; i++)
        {
            evalTokens.Add(")");
        }

        if (evalTokens.Count == 0)
        {
            return;
        }

        if (!TryEvaluateExpression(evalTokens, out var result))
        {
            SetError();
            return;
        }

        _history = BuildExpressionText(evalTokens) + " =";
        SetDisplay(result);
        _expressionTokens.Clear();
        _openParenCount = 0;
        _startNewEntry = true;
    }

    private void OnClearAll()
    {
        _display = "0";
        _history = string.Empty;
        _expressionTokens.Clear();
        _openParenCount = 0;
        _startNewEntry = true;
        _error = false;
    }

    private void OnClearEntry()
    {
        if (_error)
        {
            OnClearAll();
            return;
        }

        _display = "0";
        _startNewEntry = true;
    }

    private void OnBackspace()
    {
        if (_error)
        {
            OnClearAll();
            return;
        }

        if (_startNewEntry)
        {
            return;
        }

        if (_display.Length <= 1 || (_display.Length == 2 && _display.StartsWith("-", StringComparison.Ordinal)))
        {
            _display = "0";
            _startNewEntry = true;
            return;
        }

        _display = _display[..^1];
    }

    private void OnToggleSign()
    {
        if (_error)
        {
            return;
        }

        if (_display == "0" || _display == "0.")
        {
            return;
        }

        _display = _display.StartsWith("-", StringComparison.Ordinal)
            ? _display[1..]
            : "-" + _display;
    }

    private void OnPercent()
    {
        if (_error)
        {
            return;
        }

        var value = ParseDisplay();
        var result = value / 100d;
        SetDisplay(result);
        _startNewEntry = true;
    }

    private void OnReciprocal()
    {
        if (_error)
        {
            return;
        }

        var value = ParseDisplay();
        if (Math.Abs(value) < double.Epsilon)
        {
            SetError();
            return;
        }

        SetDisplay(1d / value);
        _startNewEntry = true;
    }

    private void OnSquare()
    {
        if (_error)
        {
            return;
        }

        var value = ParseDisplay();
        SetDisplay(value * value);
        _startNewEntry = true;
    }

    private void OnSquareRoot()
    {
        if (_error)
        {
            return;
        }

        var value = ParseDisplay();
        if (value < 0d)
        {
            SetError();
            return;
        }

        SetDisplay(Math.Sqrt(value));
        _startNewEntry = true;
    }

    private double ParseDisplay()
    {
        if (_selectedBase == ProgrammerBase.Dec)
        {
            return double.TryParse(_display, NumberStyles.Float, CultureInfo.InvariantCulture, out var decValue)
                ? decValue
                : 0d;
        }

        return ParseDisplayInteger();
    }

    private void SetDisplay(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            SetError();
            return;
        }

        _display = value.ToString("G15", CultureInfo.InvariantCulture);
        if (_selectedBase != ProgrammerBase.Dec)
        {
            _display = FormatByBase((long)value, _selectedBase);
        }
        else if (_display.Length > 24)
        {
            _display = value.ToString("0.###############E+0", CultureInfo.InvariantCulture);
        }
        _error = false;
    }

    private void SetError()
    {
        _display = "오류";
        _history = string.Empty;
        _expressionTokens.Clear();
        _openParenCount = 0;
        _startNewEntry = true;
        _error = true;
    }

    private static string ToOperatorSymbol(string op) => op switch
    {
        "+" => "+",
        "-" => "-",
        "*" => "*",
        "/" => "/",
        _ => op,
    };

    private static string FormatValue(double value)
    {
        return value.ToString("G15", CultureInfo.InvariantCulture);
    }

    private string BuildDisplayExpressionText()
    {
        if (_error)
        {
            return string.Empty;
        }

        if (_expressionTokens.Count == 0)
        {
            return _history;
        }

        var temp = new List<string>(_expressionTokens.Count + 1);
        temp.AddRange(_expressionTokens);
        if (!_startNewEntry)
        {
            temp.Add(ToNumericToken(ParseDisplay()));
        }

        return BuildExpressionText(temp);
    }

    private static bool IsOperatorToken(string token)
    {
        return token is "+" or "-" or "*" or "/" or "<<" or ">>";
    }

    private static bool IsNumericToken(string token)
    {
        return !string.IsNullOrEmpty(token) && token != "(" && token != ")" && !IsOperatorToken(token);
    }

    private void CommitCurrentEntryIfNeeded()
    {
        if (_startNewEntry)
        {
            return;
        }

        _expressionTokens.Add(ToNumericToken(ParseDisplay()));
    }

    private static int GetPrecedence(string op)
    {
        return op switch
        {
            "*" or "/" => 3,
            "+" or "-" => 2,
            "<<" or ">>" => 1,
            _ => 0,
        };
    }

    private static bool TryEvaluateExpression(List<string> tokens, out double value)
    {
        value = 0d;
        var values = new Stack<double>();
        var ops = new Stack<string>();

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (token == "(")
            {
                ops.Push(token);
                continue;
            }

            if (token == ")")
            {
                while (ops.Count > 0 && ops.Peek() != "(")
                {
                    if (!ApplyTopOperation(values, ops))
                    {
                        return false;
                    }
                }

                if (ops.Count == 0 || ops.Peek() != "(")
                {
                    return false;
                }

                _ = ops.Pop();
                continue;
            }

            if (IsOperatorToken(token))
            {
                while (ops.Count > 0 && ops.Peek() != "(" && GetPrecedence(ops.Peek()) >= GetPrecedence(token))
                {
                    if (!ApplyTopOperation(values, ops))
                    {
                        return false;
                    }
                }

                ops.Push(token);
                continue;
            }

            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            values.Push(parsed);
        }

        while (ops.Count > 0)
        {
            if (ops.Peek() == "(")
            {
                return false;
            }

            if (!ApplyTopOperation(values, ops))
            {
                return false;
            }
        }

        if (values.Count != 1)
        {
            return false;
        }

        value = values.Pop();
        return !(double.IsNaN(value) || double.IsInfinity(value));
    }

    private static bool ApplyTopOperation(Stack<double> values, Stack<string> ops)
    {
        if (ops.Count == 0 || values.Count < 2)
        {
            return false;
        }

        var op = ops.Pop();
        var rhs = values.Pop();
        var lhs = values.Pop();
        double result;

        switch (op)
        {
            case "+":
                result = lhs + rhs;
                break;
            case "-":
                result = lhs - rhs;
                break;
            case "*":
                result = lhs * rhs;
                break;
            case "/":
                if (Math.Abs(rhs) < double.Epsilon)
                {
                    return false;
                }

                result = lhs / rhs;
                break;
            case "<<":
            {
                var left = (long)Math.Truncate(lhs);
                var right = (int)Math.Clamp((long)Math.Truncate(rhs), 0, 63);
                result = left << right;
                break;
            }
            case ">>":
            {
                var left = (long)Math.Truncate(lhs);
                var right = (int)Math.Clamp((long)Math.Truncate(rhs), 0, 63);
                result = left >> right;
                break;
            }
            default:
                return false;
        }

        values.Push(result);
        return true;
    }

    private static string ToNumericToken(double value)
    {
        return value.ToString("G17", CultureInfo.InvariantCulture);
    }

    private static string BuildExpressionText(List<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(tokens.Count);
        for (var i = 0; i < tokens.Count; i++)
        {
            parts.Add(IsNumericToken(tokens[i]) ? FormatValue(double.Parse(tokens[i], CultureInfo.InvariantCulture)) : ToOperatorSymbol(tokens[i]));
        }

        return string.Join(" ", parts);
    }

}
