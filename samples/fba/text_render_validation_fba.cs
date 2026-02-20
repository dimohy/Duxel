// FBA: 텍스트 렌더링 전용 검증 샘플
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Text Render Validation",
        Width = 1360,
        Height = 900,
        VSync = true
    },
    Font = new DuxelFontOptions
    {
        InitialGlyphs = TextRenderValidationScreen.GlyphStrings
    },
    Screen = new TextRenderValidationScreen()
});

public sealed class TextRenderValidationScreen : UiScreen
{
    public static readonly IReadOnlyList<string> GlyphStrings = new[]
    {
        "Text Render Validation",
        "Direct Text Enabled",
        "Text",
        "TextColored",
        "TextDisabled",
        "TextWrapped",
        "Scroll/Clip",
        "Table",
        "한글 렌더링 테스트",
        "동해물과 백두산이 마르고 닳도록",
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        "0123456789 !@#$%^&*()_+-=[]{};:'\",.<>/?",
    };

    private bool _directTextEnabled = true;
    private bool _animateCounter = true;
    private bool _showLongParagraph = true;
    private float _wrapWidth = 420f;
    private int _frameCounter;

    public override void Render(UiImmediateContext ui)
    {
        _frameCounter++;

        var viewport = ui.GetMainViewport();
        var margin = 12f;

        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + margin, viewport.Pos.Y + margin));
        ui.SetNextWindowSize(new UiVector2(350f, viewport.Size.Y - margin * 2f));
        ui.BeginWindow("Controls");

        if (ui.Checkbox("Direct Text Enabled", ref _directTextEnabled))
        {
            ui.SetDirectTextEnabled(_directTextEnabled);
        }

        ui.Checkbox("Animate Counter", ref _animateCounter);
        ui.Checkbox("Show Long Paragraph", ref _showLongParagraph);
        ui.SliderFloat("Wrap Width", ref _wrapWidth, 180f, 760f, 0f, "0");

        ui.SeparatorText("Guide");
        ui.Text("1) Direct Text ON/OFF 전환 후 글자 깨짐 여부 비교");
        ui.Text("2) Wrap/Clip 영역에서 겹침/누락 여부 확인");
        ui.Text("3) 숫자 카운터(동적 문자열) 안정성 확인");

        ui.EndWindow();

        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + 374f, viewport.Pos.Y + margin));
        ui.SetNextWindowSize(new UiVector2(viewport.Size.X - 386f, viewport.Size.Y - margin * 2f));
        ui.BeginWindow("Text Cases");

        RenderTextCases(ui);

        ui.EndWindow();
    }

    private void RenderTextCases(UiImmediateContext ui)
    {
        ui.SeparatorText("Text");
        ui.Text("Plain: The quick brown fox jumps over the lazy dog.");
        ui.Text("한글: 동해물과 백두산이 마르고 닳도록");
        ui.Text("Mixed: Duxel 12345 ABCDE 한글 테스트");

        ui.SeparatorText("TextColored / TextDisabled");
        ui.TextColored(new UiColor(0xFF67D3FF), "TextColored: Cyan style text for blend/alpha check");
        ui.TextColored(new UiColor(0xFFFFD166), "TextColored: Warm yellow for gamma/AA check");
        ui.TextDisabled("TextDisabled: this line should look dimmed but readable");

        ui.SeparatorText("Dynamic");
        var tick = _animateCounter ? _frameCounter : 0;
        ui.TextV("Frame Counter: {0}", tick);
        ui.TextV("Timestamp: {0:HH:mm:ss}", DateTime.Now);
        ui.TextV("Random-like token: {0:X8}", tick * 2654435761u);

        ui.SeparatorText("TextWrapped");
        ui.PushTextWrapPos(ui.GetCursorPosX() + _wrapWidth);
        if (_showLongParagraph)
        {
            ui.TextWrapped("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vestibulum vulputate, lorem non tincidunt pretium, nibh augue aliquet sem, at pretium urna augue non neque. 줄바꿈과 공백 처리를 동시에 점검하기 위한 긴 문장입니다.");
        }
        else
        {
            ui.TextWrapped("Short wrapped text.");
        }
        ui.PopTextWrapPos();

        ui.SeparatorText("Scroll / Clip");
        if (ui.BeginChild("text_clip_child", new UiVector2(0f, 220f), true))
        {
            for (var i = 0; i < 120; i++)
            {
                ui.TextV("Row {0:D3} | 0123456789 | ABCDEF | 한글테스트", i);
            }

            ui.EndChild();
        }

        ui.SeparatorText("Table");
        if (ui.BeginTable("text_table_validation", 3, UiTableFlags.Borders | UiTableFlags.RowBg))
        {
            ui.TableSetupColumn("Type", 160f, 0f, UiTableColumnFlags.None);
            ui.TableSetupColumn("Value", 420f, 0f, UiTableColumnFlags.None);
            ui.TableSetupColumn("State", 120f, 0f, UiTableColumnFlags.None);
            ui.TableHeadersRow();

            ui.TableNextRow();
            ui.Text("ASCII");
            ui.TableNextColumn();
            ui.Text("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            ui.TableNextColumn();
            ui.Text("OK");

            ui.TableNextRow();
            ui.Text("Numeric");
            ui.TableNextColumn();
            ui.Text("0123456789 +-*/= () [] {}");
            ui.TableNextColumn();
            ui.Text("OK");

            ui.TableNextRow();
            ui.Text("Korean");
            ui.TableNextColumn();
            ui.Text("한글 렌더링 품질 및 baseline 확인");
            ui.TableNextColumn();
            ui.Text("Check");

            ui.EndTable();
        }
    }
}
