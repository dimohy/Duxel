#:property TargetFramework=net10.0
#:project ../../src/Dux.Core/Dux.Core.csproj
#:project ../../src/Dux.Platform.Glfw/Dux.Platform.Glfw.csproj
#:project ../../src/Dux.Vulkan/Dux.Vulkan.csproj

using System.Collections.Generic;
using System.Text;
using System.Threading;
using Dux.Core;
using Dux.Platform.Glfw;
using Dux.Vulkan;

var platformOptions = new GlfwPlatformBackendOptions(
    1280,
    720,
    "Dux Vulkan Basic",
    true
);

using var platform = new GlfwPlatformBackend(platformOptions);

var rendererOptions = new VulkanRendererOptions(
    MinImageCount: 2,
    EnableValidationLayers: true
);

using var renderer = new VulkanRendererBackend(platform, rendererOptions);

var textureId = new UiTextureId(1);
var textureCreated = false;
var userScale = 1.0f;
var lineHeightScale = 1.3f;
var pixelSnap = true;
var useBaseline = true;
var systemFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf");
var fallbackFontPath = Path.GetFullPath(".github/skills/imgui/imgui/misc/fonts/ProggyClean.ttf");
var hasSystemFont = File.Exists(systemFontPath);
var baseText = "Dux: The quick brown fox jumps over the lazy dog. 1234567890\n한글 테스트: 가나다라마바사 아자차카타파하";
var sampleText = baseText;
var ascii = new List<int>();
for (var c = 0x20; c <= 0x7E; c++)
{
    ascii.Add(c);
}

var rangesBuilder = new UiFontGlyphRangesBuilder();
rangesBuilder.AddText(sampleText);
var hangul = new List<int>();
var hangulSet = new HashSet<int>();
foreach (var codepoint in rangesBuilder.BuildCodepoints())
{
    if (codepoint > 0x7E)
    {
        hangul.Add(codepoint);
        hangulSet.Add(codepoint);
    }
}

UiFontAtlas fontAtlas;
var fontSources = new List<UiFontSource>();
var fontSize = 28;
var atlasWidth = 1024;
var atlasHeight = 1024;
var padding = 2;
var oversample = 2;
if (hasSystemFont)
{
    fontSources =
    [
        new UiFontSource(fallbackFontPath, ascii, 0, 1.0f),
        new UiFontSource(systemFontPath, hangul, 1, 0.95f),
    ];
    fontAtlas = UiFontAtlasBuilder.CreateFromTtfMerged(
        fontSources,
        fontSize: fontSize,
        atlasWidth: atlasWidth,
        atlasHeight: atlasHeight,
        padding: padding,
        oversample: oversample
    );
}
else
{
    var codepoints = new List<int>(ascii);
    foreach (var cp in hangul)
    {
        codepoints.Add(cp);
    }
    fontSources =
    [
        new UiFontSource(fallbackFontPath, codepoints, 0, 1.0f),
    ];
    fontAtlas = UiFontAtlasBuilder.CreateFromTtf(
        fallbackFontPath,
        codepoints,
        fontSize: fontSize,
        atlasWidth: atlasWidth,
        atlasHeight: atlasHeight,
        padding: padding,
        oversample: oversample
    );
}

while (!platform.ShouldClose)
{
    platform.PollEvents();
    var input = platform.Input.Snapshot;
    var rebuild = false;
    foreach (var keyEvent in input.KeyEvents)
    {
        if (!keyEvent.IsDown)
        {
            continue;
        }

        switch (keyEvent.Key)
        {
            case UiKey.R:
                rebuild = true;
                break;
            case UiKey.UpArrow:
                userScale = MathF.Min(2.5f, userScale + 0.05f);
                break;
            case UiKey.DownArrow:
                userScale = MathF.Max(0.5f, userScale - 0.05f);
                break;
            case UiKey.RightArrow:
                lineHeightScale = MathF.Min(2.0f, lineHeightScale + 0.05f);
                break;
            case UiKey.LeftArrow:
                lineHeightScale = MathF.Max(0.8f, lineHeightScale - 0.05f);
                break;
            case UiKey.P:
                pixelSnap = !pixelSnap;
                break;
            case UiKey.B:
                useBaseline = !useBaseline;
                break;
        }
    }
    var windowSize = platform.WindowSize;
    var framebufferSize = platform.FramebufferSize;
    var displaySize = new UiVector2(windowSize.Width, windowSize.Height);

    var scaleX = windowSize.Width > 0 ? (float)framebufferSize.Width / windowSize.Width : 1f;
    var scaleY = windowSize.Height > 0 ? (float)framebufferSize.Height / windowSize.Height : 1f;
    var framebufferScale = new UiVector2(scaleX, scaleY);

    var clipRect = new UiRect(0, 0, displaySize.X, displaySize.Y);
    var fontGlobalScale = framebufferScale.X * userScale;
    var lineHeight = fontAtlas.LineHeight * lineHeightScale;
    var textSettings = new UiTextSettings(fontGlobalScale, lineHeightScale, pixelSnap, useBaseline);
    var renderText = $"{baseText}\nBuild: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nScale: {fontGlobalScale:F2} Line: {lineHeightScale:F2} Snap: {pixelSnap} Base: {useBaseline}";
    var measured = UiTextBuilder.MeasureText(fontAtlas, renderText, textSettings, lineHeight);
    renderText += $"\nSize: {measured.X:F1} x {measured.Y:F1}";
    var textRanges = new UiFontGlyphRangesBuilder();
    textRanges.AddText(renderText);
    var addedGlyphs = false;
    foreach (var codepoint in textRanges.BuildCodepoints())
    {
        if (codepoint <= 0x7E)
        {
            continue;
        }
        if (hangulSet.Add(codepoint))
        {
            hangul.Add(codepoint);
            addedGlyphs = true;
        }
    }
    if (addedGlyphs)
    {
        rebuild = true;
        if (hasSystemFont)
        {
            fontSources =
            [
                new UiFontSource(fallbackFontPath, ascii, 0, 1.0f),
                new UiFontSource(systemFontPath, hangul, 1, 0.95f),
            ];
        }
        else
        {
            var codepoints = new List<int>(ascii);
            codepoints.AddRange(hangul);
            fontSources =
            [
                new UiFontSource(fallbackFontPath, codepoints, 0, 1.0f),
            ];
        }
    }

    var drawList = UiTextBuilder.BuildText(
        fontAtlas,
        renderText,
        new UiVector2(40, 120),
        new UiColor(0xFFFFFFFF),
        textureId,
        clipRect,
        textSettings,
        lineHeight
    );

    UiTextureUpdate[] textureUpdates;
    if (rebuild && fontSources.Count > 0)
    {
        UiFontAtlasBuilder.InvalidateCacheForSources(fontSources, fontSize, atlasWidth, atlasHeight, padding, oversample);
        fontAtlas = UiFontAtlasBuilder.CreateFromTtfMerged(fontSources, fontSize, atlasWidth, atlasHeight, padding, oversample);
        textureUpdates = textureCreated
            ? [new UiTextureUpdate(UiTextureUpdateKind.Destroy, textureId, fontAtlas.Format, 0, 0, ReadOnlyMemory<byte>.Empty), fontAtlas.CreateTextureUpdate(textureId)]
            : [fontAtlas.CreateTextureUpdate(textureId)];
        textureCreated = true;
    }
    else
    {
        textureUpdates = textureCreated
            ? []
            : [fontAtlas.CreateTextureUpdate(textureId)];
        textureCreated = true;
    }

    var drawData = new UiDrawData(
        displaySize,
        new UiVector2(0, 0),
        framebufferScale,
        drawList.Vertices.Count,
        drawList.Indices.Count,
        [drawList],
        textureUpdates
    );

    try
    {
        renderer.RenderDrawData(drawData);
    }
    catch (InvalidOperationException ex) when (
        ex.Message.Contains("Suboptimal", StringComparison.OrdinalIgnoreCase)
    )
    {
    }
    Thread.Sleep(1);
}
