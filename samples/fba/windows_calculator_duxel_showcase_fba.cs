// FBA: Duxel Showcase Calculator (1+2+3+4 Rule)
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
        Title = "Duxel Showcase Calculator",
        Width = 1280,
        Height = 820,
        VSync = true
    },
    Renderer = new DuxelRendererOptions
    {
        EnableDWriteText = true,
        FontLinearSampling = false
    },
    Screen = new DuxelShowcaseCalculatorScreen()
});

public sealed class DuxelShowcaseCalculatorScreen : UiScreen
{
    private string _display = "0";
    private readonly List<string> _expressionTokens = [];
    private bool _startNewEntry = true;
    private bool _error;
    private int _openParenCount;

    private readonly List<string> _traceLines = [];
    private string _traceTokenLine = "(none)";
    private string _traceRpnLine = "(none)";

    private readonly UiFpsCounter _fpsCounter = new(0.25d);
    private float _fps;
    private double _lastTime;
    private float _lastFrameMs;

    private string _lastDisplay = "0";
    private float _displayFlash;

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0d ? 0.016d : Math.Clamp(now - _lastTime, 0d, 0.05d);
        _lastTime = now;
        _lastFrameMs = (float)(delta * 1000d);

        var fpsSample = _fpsCounter.Tick(delta);
        if (fpsSample.Updated)
        {
            _fps = fpsSample.Fps;
        }

        if (!string.Equals(_lastDisplay, _display, StringComparison.Ordinal))
        {
            _displayFlash = 1f;
            _lastDisplay = _display;
        }

        _displayFlash = MathF.Max(0f, _displayFlash - (float)(delta * 2.8d));
        var flashBlend = ui.AnimateFloat("calc.display.flash", _displayFlash > 0f ? 1f : 0f, 0.12f, UiAnimationEasing.OutCubic);

        var viewport = ui.GetMainViewport();
        var margin = 12f;
        var leftWidth = 460f;

        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + margin, viewport.Pos.Y + margin));
        ui.SetNextWindowSize(new UiVector2(leftWidth, viewport.Size.Y - margin * 2f));
        ui.BeginWindow("Showcase Calculator");
        DrawCalculatorPanel(ui, flashBlend);
        ui.EndWindow();

        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + leftWidth + margin * 2f, viewport.Pos.Y + margin));
        ui.SetNextWindowSize(new UiVector2(viewport.Size.X - leftWidth - margin * 3f, viewport.Size.Y - margin * 2f));
        ui.BeginWindow("Duxel Differentiation Panel");
        DrawTracePanel(ui);
        ui.SeparatorText("Multi-Base + Bit View");
        DrawMultiBaseAndBits(ui);
        ui.EndWindow();

        ui.DrawOverlayText(
            $"FPS {_fps:0.0} | Frame {_lastFrameMs:0.00}ms | Tokens {_expressionTokens.Count} | Trace {_traceLines.Count}",
            new UiColor(0xFFF5F5F5),
            UiItemHorizontalAlign.Right,
            UiItemVerticalAlign.Top,
            background: new UiColor(0x99202020),
            margin: new UiVector2(10f, 10f),
            padding: new UiVector2(8f, 5f));

        DuxelApp.RequestFrame();
    }

    private void DrawCalculatorPanel(UiImmediateContext ui, float flashBlend)
    {
        var baseBg = 0xFF1A1A1Au;
        var flash = (uint)(Math.Clamp(flashBlend, 0f, 1f) * 70f);
        var g = (uint)Math.Clamp(26 + flash, 0, 255);
        var b = (uint)Math.Clamp(26 + flash, 0, 255);
        var displayBg = new UiColor(0xFF000000u | (g << 8) | b);

        ui.PushStyleColor(UiStyleColor.Button, displayBg);
        ui.PushStyleColor(UiStyleColor.ButtonHovered, displayBg);
        ui.PushStyleColor(UiStyleColor.ButtonActive, displayBg);

        var w = MathF.Max(320f, ui.GetContentRegionAvail().X);
        _ = ui.Button("##display_bg", new UiVector2(w, 92f));
        var rectMin = ui.GetItemRectMin();
        var rectSize = ui.GetItemRectSize();

        var exprText = BuildExpressionPreview();
        if (!string.IsNullOrWhiteSpace(exprText))
        {
            ui.DrawTextAligned(
                new UiRect(rectMin.X + 10f, rectMin.Y + 12f, rectSize.X - 20f, 26f),
                exprText,
                new UiColor(0xFFB8B8B8),
                UiItemHorizontalAlign.Right,
                UiItemVerticalAlign.Top);
        }

        ui.DrawTextAligned(
            new UiRect(rectMin.X + 10f, rectMin.Y + 10f, rectSize.X - 20f, rectSize.Y - 18f),
            _error ? "오류" : _display,
            new UiColor(0xFFF0F0F0),
            UiItemHorizontalAlign.Right,
            UiItemVerticalAlign.Bottom,
            fontSize: 30f);

        ui.PopStyleColor(3);

        ui.Dummy(new UiVector2(0f, 8f));
        DrawKeypad(ui);

        _ = baseBg;
    }

    private void DrawKeypad(UiImmediateContext ui)
    {
        var availWidth = MathF.Max(280f, ui.GetContentRegionAvail().X);
        var gap = 4f;
        var cols = 4f;
        var buttonW = MathF.Floor((availWidth - gap * (cols - 1f)) / cols);
        var buttonH = 58f;

        DrawRow(ui, buttonW, buttonH, gap,
            ("C", OnClearAll, true),
            ("(", OnOpenParen, true),
            (")", OnCloseParen, true),
            ("/", () => OnOperator("/"), true));

        DrawRow(ui, buttonW, buttonH, gap,
            ("7", () => OnDigit("7"), true),
            ("8", () => OnDigit("8"), true),
            ("9", () => OnDigit("9"), true),
            ("*", () => OnOperator("*"), true));

        DrawRow(ui, buttonW, buttonH, gap,
            ("4", () => OnDigit("4"), true),
            ("5", () => OnDigit("5"), true),
            ("6", () => OnDigit("6"), true),
            ("-", () => OnOperator("-"), true));

        DrawRow(ui, buttonW, buttonH, gap,
            ("1", () => OnDigit("1"), true),
            ("2", () => OnDigit("2"), true),
            ("3", () => OnDigit("3"), true),
            ("+", () => OnOperator("+"), true));

        DrawRow(ui, buttonW, buttonH, gap,
            ("+/-", OnToggleSign, true),
            ("0", () => OnDigit("0"), true),
            (".", OnDot, true),
            ("=", OnEquals, true));
    }

    private static void DrawRow(UiImmediateContext ui, float w, float h, float gap, params (string Label, Action Action, bool Enabled)[] items)
    {
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (ui.Button(item.Label, new UiVector2(w, h)) && item.Enabled)
            {
                item.Action();
            }

            if (i < items.Length - 1)
            {
                ui.SameLine(gap, UiItemVerticalAlign.Top);
            }
        }
    }

    private void DrawTracePanel(UiImmediateContext ui)
    {
        ui.SeparatorText("1) Calculation Trace (Token -> RPN -> Eval)");
        ui.TextWrapped($"Tokenized: {_traceTokenLine}");
        ui.TextWrapped($"RPN: {_traceRpnLine}");

        if (ui.BeginChild("trace_lines", new UiVector2(0f, 240f), true))
        {
            if (_traceLines.Count == 0)
            {
                ui.TextDisabled("No evaluation trace yet. Press '=' after entering an expression.");
            }
            else
            {
                for (var i = 0; i < _traceLines.Count; i++)
                {
                    ui.TextV("{0:00}. {1}", i + 1, _traceLines[i]);
                }
            }

            ui.EndChild();
        }
    }

    private void DrawMultiBaseAndBits(UiImmediateContext ui)
    {
        var value = (int)Math.Clamp(Math.Truncate(ParseDisplaySafe()), int.MinValue, int.MaxValue);
        var unsigned = unchecked((uint)value);

        ui.TextV("DEC : {0}", value);
        ui.TextV("HEX : 0x{0:X8}", unsigned);
        ui.TextV("OCT : {0}", Convert.ToString(unsigned, 8).PadLeft(11, '0'));
        ui.TextV("BIN : {0}", Convert.ToString(unsigned, 2).PadLeft(32, '0'));

        ui.Dummy(new UiVector2(0f, 6f));
        ui.SeparatorText("2) Bit Grid (toggle -> value sync)");

        var bitButtonW = 26f;
        var bitButtonH = 24f;
        var spacing = 2f;

        for (var row = 3; row >= 0; row--)
        {
            var baseBit = row * 8;
            ui.TextV("{0,2}..{1,2}", baseBit + 7, baseBit);
            ui.SameLine(8f, UiItemVerticalAlign.Top);

            for (var col = 7; col >= 0; col--)
            {
                var bit = baseBit + col;
                var on = ((unsigned >> bit) & 1u) == 1u;

                ui.PushStyleColor(UiStyleColor.Button, on ? new UiColor(0xFF2D7DFF) : new UiColor(0xFF2A2A2A));
                ui.PushStyleColor(UiStyleColor.ButtonHovered, on ? new UiColor(0xFF3B89FF) : new UiColor(0xFF353535));
                ui.PushStyleColor(UiStyleColor.ButtonActive, on ? new UiColor(0xFF1D6EF5) : new UiColor(0xFF444444));

                if (ui.Button($"{(on ? '1' : '0')}##bit_{bit}", new UiVector2(bitButtonW, bitButtonH)))
                {
                    var toggled = unsigned ^ (1u << bit);
                    SetDisplay(toggled.ToString(CultureInfo.InvariantCulture));
                    _expressionTokens.Clear();
                    _openParenCount = 0;
                    _startNewEntry = true;
                }

                ui.PopStyleColor(3);

                if (col > 0)
                {
                    ui.SameLine(spacing, UiItemVerticalAlign.Top);
                }
            }
        }

        ui.Dummy(new UiVector2(0f, 8f));
        ui.SeparatorText("4) Motion/Feedback");
        ui.TextDisabled("- Display change flash (value update)");
        ui.TextDisabled("- Bit button state color transition");
        ui.TextDisabled("- Trace panel updates immediately per '=' evaluation");
    }

    private void OnDigit(string d)
    {
        if (_error)
        {
            OnClearAll();
        }

        if (_startNewEntry)
        {
            _display = d;
            _startNewEntry = false;
            return;
        }

        if (_display == "0")
        {
            _display = d;
            return;
        }

        if (_display.Length < 24)
        {
            _display += d;
        }
    }

    private void OnDot()
    {
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

        _display = _display.StartsWith("-", StringComparison.Ordinal) ? _display[1..] : "-" + _display;
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
            _expressionTokens.Add(ToNumericToken(ParseDisplaySafe()));
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
            }
        }
        else
        {
            _expressionTokens.Add(op);
        }

        _startNewEntry = true;
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

        _expressionTokens.Add("(");
        _openParenCount++;
        _startNewEntry = true;
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
        if (tail == "(" || IsOperatorToken(tail))
        {
            return;
        }

        _expressionTokens.Add(")");
        _openParenCount--;
        _startNewEntry = true;
    }

    private void OnEquals()
    {
        if (_error)
        {
            return;
        }

        var tokens = new List<string>(_expressionTokens);
        if (!_startNewEntry)
        {
            tokens.Add(ToNumericToken(ParseDisplaySafe()));
        }
        else if (tokens.Count > 0 && IsOperatorToken(tokens[^1]))
        {
            tokens.Add(ToNumericToken(ParseDisplaySafe()));
        }

        for (var i = 0; i < _openParenCount; i++)
        {
            tokens.Add(")");
        }

        if (tokens.Count == 0)
        {
            return;
        }

        if (!TryConvertToRpn(tokens, out var rpn, out var shuntingSteps))
        {
            SetError();
            return;
        }

        if (!TryEvaluateRpn(rpn, out var result, out var evalSteps))
        {
            SetError();
            return;
        }

        _traceTokenLine = string.Join(" ", tokens);
        _traceRpnLine = string.Join(" ", rpn);
        _traceLines.Clear();
        _traceLines.AddRange(shuntingSteps);
        _traceLines.AddRange(evalSteps);

        SetDisplay(result.ToString("G15", CultureInfo.InvariantCulture));
        _expressionTokens.Clear();
        _openParenCount = 0;
        _startNewEntry = true;
    }

    private void OnClearAll()
    {
        _display = "0";
        _expressionTokens.Clear();
        _openParenCount = 0;
        _startNewEntry = true;
        _error = false;
        _traceTokenLine = "(none)";
        _traceRpnLine = "(none)";
        _traceLines.Clear();
    }

    private void CommitCurrentEntryIfNeeded()
    {
        if (_startNewEntry)
        {
            return;
        }

        _expressionTokens.Add(ToNumericToken(ParseDisplaySafe()));
    }

    private double ParseDisplaySafe()
    {
        if (_error)
        {
            return 0d;
        }

        return double.TryParse(_display, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
    }

    private void SetDisplay(string value)
    {
        _display = string.IsNullOrWhiteSpace(value) ? "0" : value;
        _error = false;
    }

    private void SetError()
    {
        _display = "오류";
        _error = true;
        _startNewEntry = true;
        _expressionTokens.Clear();
        _openParenCount = 0;
    }

    private string BuildExpressionPreview()
    {
        if (_expressionTokens.Count == 0)
        {
            return _startNewEntry ? string.Empty : _display;
        }

        var preview = new List<string>(_expressionTokens);
        if (!_startNewEntry)
        {
            preview.Add(ToNumericToken(ParseDisplaySafe()));
        }

        return string.Join(" ", preview);
    }

    private static bool IsOperatorToken(string token)
        => token is "+" or "-" or "*" or "/";

    private static string ToNumericToken(double value)
        => value.ToString("G17", CultureInfo.InvariantCulture);

    private static int GetPrecedence(string op)
        => op is "*" or "/" ? 2 : op is "+" or "-" ? 1 : 0;

    private static bool TryConvertToRpn(List<string> tokens, out List<string> output, out List<string> steps)
    {
        output = [];
        steps = [];
        var opStack = new Stack<string>();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                output.Add(token);
                steps.Add($"Token {token} -> output");
                continue;
            }

            if (token == "(")
            {
                opStack.Push(token);
                steps.Add("Push ( to stack");
                continue;
            }

            if (token == ")")
            {
                while (opStack.Count > 0 && opStack.Peek() != "(")
                {
                    output.Add(opStack.Pop());
                }

                if (opStack.Count == 0 || opStack.Peek() != "(")
                {
                    return false;
                }

                _ = opStack.Pop();
                steps.Add("Pop until (");
                continue;
            }

            if (IsOperatorToken(token))
            {
                while (opStack.Count > 0 && IsOperatorToken(opStack.Peek()) && GetPrecedence(opStack.Peek()) >= GetPrecedence(token))
                {
                    output.Add(opStack.Pop());
                }

                opStack.Push(token);
                steps.Add($"Operator {token} -> stack");
                continue;
            }

            return false;
        }

        while (opStack.Count > 0)
        {
            var op = opStack.Pop();
            if (op == "(")
            {
                return false;
            }

            output.Add(op);
        }

        return true;
    }

    private static bool TryEvaluateRpn(List<string> rpn, out double result, out List<string> steps)
    {
        result = 0d;
        steps = [];
        var stack = new Stack<double>();

        for (var i = 0; i < rpn.Count; i++)
        {
            var token = rpn[i];
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                stack.Push(value);
                steps.Add($"Push {value:G15}");
                continue;
            }

            if (!IsOperatorToken(token) || stack.Count < 2)
            {
                return false;
            }

            var rhs = stack.Pop();
            var lhs = stack.Pop();
            double opResult;
            switch (token)
            {
                case "+": opResult = lhs + rhs; break;
                case "-": opResult = lhs - rhs; break;
                case "*": opResult = lhs * rhs; break;
                case "/":
                    if (Math.Abs(rhs) < double.Epsilon)
                    {
                        return false;
                    }

                    opResult = lhs / rhs;
                    break;
                default:
                    return false;
            }

            stack.Push(opResult);
            steps.Add($"{lhs:G15} {token} {rhs:G15} = {opResult:G15}");
        }

        if (stack.Count != 1)
        {
            return false;
        }

        result = stack.Pop();
        return !(double.IsNaN(result) || double.IsInfinity(result));
    }
}
