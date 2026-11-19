using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class SwitchAlignmentRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int width, int height)
    {
        var labels = new[] { "토글 버튼과 값 공유", "Agyp", "한글 English", "V-Sync", "이벤트 대기 모드" };
        foreach (var samples in new[] { 1, 4 })
            foreach (var vector in new[] { false, true })
                for (var style = 0; style < 4; style++)
                    foreach (var label in labels)
                    {
                        renderer.SetMsaaSamples(samples);
                        var value = new UiState<bool>(false);
                        using UiWidget widget = style switch
                        {
                            0 => new SwitchPaint0(label, value.Binding),
                            1 => new SwitchPaint1(label, value.Binding),
                            2 => new SwitchPaint3(label, value.Binding),
                            _ => new SwitchPaint4(label, value.Binding)
                        };
                        var root = default(UiRect);
                        var pixels = capture($"switch-align-{samples}-{vector}-{style}-{Array.IndexOf(labels, label)}", ui =>
                        {
                            var viewport = new UiRect(0, 0, width, height);
                            ui.GetWindowDrawList().AddRectFilled(viewport, new(32, 32, 32), ui.WhiteTextureId, viewport);
                            if (vector)
                                ui.BeginLayer("switch-alignment-vector", new UiLayerOptions(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                            try
                            {
                                ui.SetCursorScreenPos(new(16, 16));
                                widget.Render(ui);
                                var position = ui.GetItemRectMin();
                                var size = ui.GetItemRectSize();
                                root = new(position.X, position.Y, size.X, size.Y);
                            }
                            finally { if (vector) ui.EndLayer(); }
                        });
                        var trackWidth = style < 2 ? 40 : 32;
                        var trackHeight = style < 2 ? 20 : 16;
                        var before = style % 2 == 1;
                        var left = before ? (int)root.X + 8 : (int)root.X + 8 + trackWidth + 6;
                        var right = before ? (int)(root.X + root.Width) - 8 - trackWidth - 6 : width;
                        var min = height;
                        var max = -1;
                        for (var y = 0; y < height; y++)
                            for (var x = left; x < right; x++)
                            {
                                var at = (y * width + x) * 4;
                                // Include antialiased ink support rather than just bright glyph interiors.
                                if (pixels[at] <= 32 || pixels[at + 1] <= 32 || pixels[at + 2] <= 32)
                                    continue;
                                min = Math.Min(min, y);
                                max = Math.Max(max, y);
                            }
                        var glyphCenter = (min + max + 1) * .5;
                        var trackCenter = root.Y + 8 + trackHeight * .5;
                        check(max >= min && Math.Abs(glyphCenter - trackCenter) <= .5,
                            $"actual Switch glyph ink centers on its track within pixel-grid parity : MSAA{samples}, vector={vector}, style={style}, label={label}, glyph={glyphCenter}, track={trackCenter}");
                    }
        renderer.SetMsaaSamples(1);
        foreach (var vector in new[] { false, true })
            foreach (var label in new[] { "품질 우선", "한글 Agyp" })
                foreach (var radio in new[] { false, true })
                {
                    using UiWidget widget = radio
                        ? Fluent.RadioGroup(string.Empty, new UiState<string?>("a"), new UiRadioOption[] { new("a", label) }, 220)
                        : Fluent.Checkbox(label, new UiState<bool>(false));
                    var pixels = capture($"selector-align-{radio}-{vector}-{label}", ui =>
                    {
                        var viewport = new UiRect(0, 0, width, height);
                        ui.GetWindowDrawList().AddRectFilled(viewport, new(32, 32, 32), ui.WhiteTextureId, viewport);
                        if (vector)
                            ui.BeginLayer("selector-align-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                        try
                        {
                            ui.SetCursorScreenPos(new(16, 16));
                            widget.Render(ui);
                        }
                        finally { if (vector) ui.EndLayer(); }
                    });
                    var min = height;
                    var max = -1;
                    for (var y = 16; y < 56; y++)
                        for (var x = 48; x < 240; x++)
                        {
                            var at = (y * width + x) * 4;
                            if (pixels[at] <= 32 || pixels[at + 1] <= 32 || pixels[at + 2] <= 32)
                                continue;
                            min = Math.Min(min, y);
                            max = Math.Max(max, y);
                        }
                    var center = (min + max + 1) * .5;
                    check(max >= min && Math.Abs(center - 32) <= .5,
                        $"selector glyph ink centers on its indicator : radio={radio}, vector={vector}, label={label}, center={center}");
                }
    }
}
