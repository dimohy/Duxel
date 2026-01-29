using System;

namespace Duxel.Core;

public static class Ui
{
    private static UiContext? _current;

    public static UiContext CreateContext(UiFontAtlas fontAtlas, UiTextureId fontTexture, UiTextureId whiteTexture)
    {
        var context = new UiContext(fontAtlas, fontTexture, whiteTexture);
        _current = context;
        return context;
    }

    public static void DestroyContext(UiContext? context = null)
    {
        var target = context ?? _current;
        if (target is null)
        {
            throw new InvalidOperationException("No current context to destroy.");
        }

        target.Dispose();
        if (ReferenceEquals(target, _current))
        {
            _current = null;
        }
    }

    public static UiContext? GetCurrentContext() => _current;

    public static void SetCurrentContext(UiContext? context)
    {
        _current = context;
    }

    public static UiIO GetIO() => RequireContext().GetIO();

    public static UiPlatformIO GetPlatformIO() => RequireContext().GetPlatformIO();

    public static UiTheme GetStyle() => RequireContext().GetStyle();

    public static UiStyle GetStyleData() => RequireContext().GetStyleData();

    public static void SetStyle(UiStyle style) => RequireContext().SetStyle(style);

    public static void StyleColorsDark() => RequireContext().StyleColorsDark();

    public static void StyleColorsLight() => RequireContext().StyleColorsLight();

    public static void StyleColorsClassic() => RequireContext().StyleColorsClassic();

    public static void EndFrame() => RequireContext().EndFrame();

    public static string GetVersion()
    {
        var version = typeof(Ui).Assembly.GetName().Version;
        return version is null ? "Duxel" : $"Duxel {version}";
    }

    public static void LogToTTY() => RequireContext().LogToTTY();

    public static void LogToClipboard() => RequireContext().LogToClipboard();

    public static void LogToFile(string? filePath = null) => RequireContext().LogToFile(filePath);

    public static void LogButtons() => RequireContext().LogButtons();

    public static void LogText(string text) => RequireContext().LogText(text);

    public static void LogTextV(string text) => RequireContext().LogTextV(text);

    public static void LogFinish() => RequireContext().LogFinish();

    public static void DebugTextEncoding(string text) => RequireContext().DebugTextEncoding(text);

    public static void DebugFlashStyleColor() => RequireContext().DebugFlashStyleColor();

    public static void DebugStartItemPicker() => RequireContext().DebugStartItemPicker();

    public static bool DebugCheckVersionAndDataLayout() => RequireContext().DebugCheckVersionAndDataLayout();

    public static void DebugLog(string text) => RequireContext().DebugLog(text);

    public static void DebugLogV(string text) => RequireContext().DebugLogV(text);

    public static void ClearPlatformHandlers() => RequireContext().ClearPlatformHandlers();

    public static void ClearRendererHandlers() => RequireContext().ClearRendererHandlers();

    public static void SetAllocatorFunctions(UiMemAlloc allocFunc, UiMemFree freeFunc) => RequireContext().SetAllocatorFunctions(allocFunc, freeFunc);

    public static void GetAllocatorFunctions(out UiMemAlloc allocFunc, out UiMemFree freeFunc) => RequireContext().GetAllocatorFunctions(out allocFunc, out freeFunc);

    public static byte[] MemAlloc(int size) => RequireContext().MemAlloc(size);

    public static void MemFree(byte[] block) => RequireContext().MemFree(block);

    public static void LoadIniSettingsFromDisk(string iniFilename) => RequireContext().LoadIniSettingsFromDisk(iniFilename);

    public static void LoadIniSettingsFromMemory(string iniData) => RequireContext().LoadIniSettingsFromMemory(iniData);

    public static void SaveIniSettingsToDisk(string iniFilename) => RequireContext().SaveIniSettingsToDisk(iniFilename);

    public static string SaveIniSettingsToMemory() => RequireContext().SaveIniSettingsToMemory();

    public static void AddKeyEvent(UiKey key, bool down) => RequireContext().AddKeyEvent(key, down);

    public static void AddKeyAnalogEvent(UiKey key, bool down, float value) => RequireContext().AddKeyAnalogEvent(key, down, value);

    public static void AddMousePosEvent(float x, float y) => RequireContext().AddMousePosEvent(x, y);

    public static void AddMouseButtonEvent(int button, bool down) => RequireContext().AddMouseButtonEvent(button, down);

    public static void AddMouseWheelEvent(float wheelX, float wheelY) => RequireContext().AddMouseWheelEvent(wheelX, wheelY);

    public static void AddMouseSourceEvent() => RequireContext().AddMouseSourceEvent();

    public static void AddFocusEvent(bool focused) => RequireContext().AddFocusEvent(focused);

    public static void AddInputCharacter(uint c) => RequireContext().AddInputCharacter(c);

    public static void AddInputCharacterUTF16(char c) => RequireContext().AddInputCharacterUTF16(c);

    public static void AddInputCharactersUTF8(string utf8Text) => RequireContext().AddInputCharactersUTF8(utf8Text);

    public static void SetKeyEventNativeData(UiKey key, int nativeKeycode, int nativeScancode, int nativeLegacyIndex) => RequireContext().SetKeyEventNativeData(key, nativeKeycode, nativeScancode, nativeLegacyIndex);

    public static void SetAppAcceptingEvents(bool acceptingEvents) => RequireContext().SetAppAcceptingEvents(acceptingEvents);

    public static void ClearEventsQueue() => RequireContext().ClearEventsQueue();

    public static void ClearInputKeys() => RequireContext().ClearInputKeys();

    public static void ClearInputMouse() => RequireContext().ClearInputMouse();

    public static UiVector4 ColorConvertU32ToFloat4(UiColor color)
    {
        var rgba = color.Rgba;
        var a = ((rgba >> 24) & 0xFF) / 255f;
        var r = ((rgba >> 16) & 0xFF) / 255f;
        var g = ((rgba >> 8) & 0xFF) / 255f;
        var b = (rgba & 0xFF) / 255f;
        return new UiVector4(r, g, b, a);
    }

    public static UiColor ColorConvertFloat4ToU32(UiVector4 color)
    {
        var r = (byte)Math.Clamp((int)MathF.Round(color.X * 255f), 0, 255);
        var g = (byte)Math.Clamp((int)MathF.Round(color.Y * 255f), 0, 255);
        var b = (byte)Math.Clamp((int)MathF.Round(color.Z * 255f), 0, 255);
        var a = (byte)Math.Clamp((int)MathF.Round(color.W * 255f), 0, 255);
        var rgba = (uint)(a << 24 | r << 16 | g << 8 | b);
        return new UiColor(rgba);
    }

    public static void ColorConvertRGBtoHSV(float r, float g, float b, out float h, out float s, out float v)
    {
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        v = max;

        var delta = max - min;
        if (max <= 0f)
        {
            s = 0f;
            h = 0f;
            return;
        }

        s = delta / max;
        if (delta <= 0f)
        {
            h = 0f;
            return;
        }

        if (max == r)
        {
            h = (g - b) / delta + (g < b ? 6f : 0f);
        }
        else if (max == g)
        {
            h = (b - r) / delta + 2f;
        }
        else
        {
            h = (r - g) / delta + 4f;
        }

        h /= 6f;
    }

    public static void ColorConvertHSVtoRGB(float h, float s, float v, out float r, out float g, out float b)
    {
        if (s <= 0f)
        {
            r = v;
            g = v;
            b = v;
            return;
        }

        h = (h % 1f + 1f) % 1f;
        var hf = h * 6f;
        var sector = (int)MathF.Floor(hf);
        var fraction = hf - sector;

        var p = v * (1f - s);
        var q = v * (1f - s * fraction);
        var t = v * (1f - s * (1f - fraction));

        switch (sector)
        {
            case 0:
                r = v; g = t; b = p;
                break;
            case 1:
                r = q; g = v; b = p;
                break;
            case 2:
                r = p; g = v; b = t;
                break;
            case 3:
                r = p; g = q; b = v;
                break;
            case 4:
                r = t; g = p; b = v;
                break;
            default:
                r = v; g = p; b = q;
                break;
        }
    }

    private static UiContext RequireContext()
    {
        if (_current is null)
        {
            throw new InvalidOperationException("No current context is set.");
        }

        return _current;
    }
}

