// FBA: 폰트 스타일/크기 검증 샘플 (regular/bold/italic 실행 모드)
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.IO;
using Duxel.App;
using Duxel.Core;

var style = ResolveStyle(Environment.GetEnvironmentVariable("DUXEL_FONT_STYLE"));
var primaryFontPath = ResolvePrimaryFontPath(style);
var fontLinearSampling = ResolveBool(Environment.GetEnvironmentVariable("DUXEL_FONT_LINEAR_SAMPLING"), defaultValue: false);

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = $"Duxel Font Style Validation ({style})",
        Width = 1400,
        Height = 920,
        VSync = true
    },
    Font = new DuxelFontOptions
    {
        PrimaryFontPath = primaryFontPath,
        SecondaryFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf"),
        // InitialGlyphs =
        // [
        //     "Font Style Validation",
        //     "The quick brown fox jumps over the lazy dog.",
        //     "동해물과 백두산이 마르고 닳도록",
        //     "가나다라마바사",
        //     "0123456789 !@#$%^&*()_+-=[]{};:'\",.<>/?",
        //     "Regular",
        //     "Bold",
        //     "Italic"
        // ]
    },
    Renderer = new DuxelRendererOptions
    {
        FontLinearSampling = fontLinearSampling,
        EnableGlobalStaticGeometryCache = true
    },
    Screen = new FontStyleValidationScreen(style, primaryFontPath, fontLinearSampling)
});

static bool ResolveBool(string? raw, bool defaultValue)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return defaultValue;
    }

    var normalized = raw.Trim();
    if (normalized == "1")
    {
        return true;
    }

    if (normalized == "0")
    {
        return false;
    }

    if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("on", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("off", StringComparison.OrdinalIgnoreCase)
        || normalized.Equals("no", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return defaultValue;
}

static string ResolveStyle(string? raw)
{
    if (string.Equals(raw, "bold", StringComparison.OrdinalIgnoreCase))
    {
        return "bold";
    }

    if (string.Equals(raw, "italic", StringComparison.OrdinalIgnoreCase))
    {
        return "italic";
    }

    return "regular";
}

static string ResolvePrimaryFontPath(string style)
{
    var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    var candidate = style switch
    {
        "bold" => Path.Combine(fontsDir, "segoeuib.ttf"),
        "italic" => Path.Combine(fontsDir, "segoeuii.ttf"),
        _ => Path.Combine(fontsDir, "segoeui.ttf")
    };

    if (File.Exists(candidate))
    {
        return candidate;
    }

    var fallback = Path.Combine(fontsDir, "segoeui.ttf");
    if (File.Exists(fallback))
    {
        return fallback;
    }

    return candidate;
}

public sealed class FontStyleValidationScreen(string style, string primaryFontPath, bool fontLinearSampling) : UiScreen
{
    private float _previewSize = 28f;
    private float _ladderMin = 10f;
    private float _ladderMax = 64f;
    private float _ladderStep = 6f;
    private bool _directTextEnabled = true;
    private bool _directTextStateInitialized;

    public override void Render(UiImmediateContext ui)
    {
        if (!_directTextStateInitialized)
        {
            _directTextEnabled = ui.GetDirectTextEnabled();
            _directTextStateInitialized = true;
        }

        var viewport = ui.GetMainViewport();
        const float margin = 12f;

        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + margin, viewport.Pos.Y + margin));
        ui.SetNextWindowSize(new UiVector2(420f, viewport.Size.Y - margin * 2f));
        ui.BeginWindow("Controls");

        ui.Text($"Current style mode: {style}");
        ui.Text($"Primary font file: {primaryFontPath}");
        ui.Text($"FontLinearSampling: {(fontLinearSampling ? "ON(linear)" : "OFF(nearest)")}");

        if (ui.Checkbox("Direct Text Enabled", ref _directTextEnabled))
        {
            ui.SetDirectTextEnabled(_directTextEnabled);
        }

        ui.SeparatorText("Preview");
        ui.SliderFloat("Preview Size", ref _previewSize, 8f, 96f, 0f, "0");

        ui.SeparatorText("Size Ladder");
        ui.SliderFloat("Min Size", ref _ladderMin, 8f, 40f, 0f, "0");
        ui.SliderFloat("Max Size", ref _ladderMax, 24f, 120f, 0f, "0");
        ui.SliderFloat("Step", ref _ladderStep, 1f, 16f, 0f, "0");

        if (_ladderMax < _ladderMin)
        {
            (_ladderMin, _ladderMax) = (_ladderMax, _ladderMin);
        }

        ui.SeparatorText("How to validate weight/italic");
        ui.Text("Run this same file with different DUXEL_FONT_STYLE values:");
        ui.BulletText("regular");
        ui.BulletText("bold");
        ui.BulletText("italic");
        ui.Text("Compare glyph strokes/slant across runs.");

        ui.EndWindow();

        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + 444f, viewport.Pos.Y + margin));
        ui.SetNextWindowSize(new UiVector2(viewport.Size.X - 456f, viewport.Size.Y - margin * 2f));
        ui.BeginWindow("Font Preview");

        ui.SeparatorText("Live Preview");
        ui.PushFontSize(_previewSize);
        ui.Text("The quick brown fox jumps over the lazy dog.");
        ui.Text("동해물과 백두산이 마르고 닳도록");
        ui.Text("0123456789 !@#$%^&*()_+-=[]{};:'\",.<>/?");
        ui.PopFontSize();

        ui.SeparatorText("Size Ladder");
        var size = _ladderMin;
        var guard = 0;
        while (size <= _ladderMax && guard < 64)
        {
            ui.PushFontSize(size);
            ui.Text($"{size,5:0} px | The quick brown fox | 가나다라마바사");
            ui.PopFontSize();

            size += MathF.Max(1f, _ladderStep);
            guard++;
        }

        ui.SeparatorText("Notes");
        ui.Text("- This runtime supports dynamic font size via PushFontSize/PopFontSize.");
        ui.Text("- Weight/italic comparison is done by switching the startup font file per run.");

        ui.EndWindow();
    }
}
