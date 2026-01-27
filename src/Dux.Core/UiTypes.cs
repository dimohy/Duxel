using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Dux.Core;

public readonly record struct UiVector2(float X, float Y);

public readonly record struct UiVector4(float X, float Y, float Z, float W);

public readonly record struct UiRect(float X, float Y, float Width, float Height);

public readonly record struct UiColor(uint Rgba);

public readonly record struct UiTheme(
    UiColor Text,
    UiColor TextDisabled,
    UiColor WindowBg,
    UiColor TitleBg,
    UiColor TitleBgActive,
    UiColor MenuBarBg,
    UiColor PopupBg,
    UiColor Border,
    UiColor FrameBg,
    UiColor FrameBgHovered,
    UiColor FrameBgActive,
    UiColor Header,
    UiColor HeaderHovered,
    UiColor HeaderActive,
    UiColor Button,
    UiColor ButtonHovered,
    UiColor ButtonActive,
    UiColor Tab,
    UiColor TabHovered,
    UiColor TabActive,
    UiColor CheckMark,
    UiColor SliderGrab,
    UiColor SliderGrabActive,
    UiColor PlotLines,
    UiColor PlotHistogram,
    UiColor Separator,
    UiColor TableHeaderBg,
    UiColor TableRowBg0,
    UiColor TableRowBg1,
    UiColor TableBorder,
    UiColor TextSelectedBg
)
{
    public static UiTheme ImGuiDark => new(
        new UiColor(0xFFE6E6E6), // Text
        new UiColor(0xFF8C8C8C), // TextDisabled
        new UiColor(0xFF1A1A1A), // WindowBg
        new UiColor(0xFF141414), // TitleBg
        new UiColor(0xFF2A2A2A), // TitleBgActive
        new UiColor(0xFF1E1E1E), // MenuBarBg
        new UiColor(0xEE1A1A1A), // PopupBg
        new UiColor(0xFF2A2A2A), // Border
        new UiColor(0xFF2B2B2B), // FrameBg
        new UiColor(0xFF3A3A3A), // FrameBgHovered
        new UiColor(0xFF4A4A4A), // FrameBgActive
        new UiColor(0xFF2D2D2D), // Header
        new UiColor(0xFF3C3C3C), // HeaderHovered
        new UiColor(0xFF4A4A4A), // HeaderActive
        new UiColor(0xFF2B2B2B), // Button
        new UiColor(0xFF3C3C3C), // ButtonHovered
        new UiColor(0xFF4A4A4A), // ButtonActive
        new UiColor(0xFF242424), // Tab
        new UiColor(0xFF3A3A3A), // TabHovered
        new UiColor(0xFF4A4A4A), // TabActive
        new UiColor(0xFF4AA3FF), // CheckMark
        new UiColor(0xFF4AA3FF), // SliderGrab
        new UiColor(0xFF6BB2FF), // SliderGrabActive
        new UiColor(0xFFA0A0A0), // PlotLines
        new UiColor(0xFFB58A42), // PlotHistogram
        new UiColor(0xFF2A2A2A), // Separator
        new UiColor(0xFF262626), // TableHeaderBg
        new UiColor(0xFF1E1E1E), // TableRowBg0
        new UiColor(0xFF222222), // TableRowBg1
        new UiColor(0xFF2A2A2A), // TableBorder
        new UiColor(0x882A4A68)  // TextSelectedBg
    );

    public static UiTheme ImGuiLight => new(
        new UiColor(0xFF000000), // Text
        new UiColor(0xFF808080), // TextDisabled
        new UiColor(0xFFF0F0F0), // WindowBg
        new UiColor(0xFFE5E5E5), // TitleBg
        new UiColor(0xFFD0D0D0), // TitleBgActive
        new UiColor(0xFFE0E0E0), // MenuBarBg
        new UiColor(0xFFF8F8F8), // PopupBg
        new UiColor(0xFFB0B0B0), // Border
        new UiColor(0xFFE0E0E0), // FrameBg
        new UiColor(0xFFD0D0D0), // FrameBgHovered
        new UiColor(0xFFC0C0C0), // FrameBgActive
        new UiColor(0xFFD8D8D8), // Header
        new UiColor(0xFFC8C8C8), // HeaderHovered
        new UiColor(0xFFB8B8B8), // HeaderActive
        new UiColor(0xFFD8D8D8), // Button
        new UiColor(0xFFC8C8C8), // ButtonHovered
        new UiColor(0xFFB8B8B8), // ButtonActive
        new UiColor(0xFFD0D0D0), // Tab
        new UiColor(0xFFC0C0C0), // TabHovered
        new UiColor(0xFFB0B0B0), // TabActive
        new UiColor(0xFF1E90FF), // CheckMark
        new UiColor(0xFF1E90FF), // SliderGrab
        new UiColor(0xFF3CA0FF), // SliderGrabActive
        new UiColor(0xFF505050), // PlotLines
        new UiColor(0xFFB08040), // PlotHistogram
        new UiColor(0xFFB0B0B0), // Separator
        new UiColor(0xFFE0E0E0), // TableHeaderBg
        new UiColor(0xFFF4F4F4), // TableRowBg0
        new UiColor(0xFFEFEFEF), // TableRowBg1
        new UiColor(0xFFB0B0B0), // TableBorder
        new UiColor(0x804A78B0)  // TextSelectedBg
    );

    public static UiTheme ImGuiClassic => new(
        new UiColor(0xFF000000), // Text
        new UiColor(0xFF808080), // TextDisabled
        new UiColor(0xFFEFEFEF), // WindowBg
        new UiColor(0xFFD5D5D5), // TitleBg
        new UiColor(0xFFBEBEBE), // TitleBgActive
        new UiColor(0xFFDCDCDC), // MenuBarBg
        new UiColor(0xFFF8F8F8), // PopupBg
        new UiColor(0xFF8C8C8C), // Border
        new UiColor(0xFFDCDCDC), // FrameBg
        new UiColor(0xFFCFCFCF), // FrameBgHovered
        new UiColor(0xFFBFBFBF), // FrameBgActive
        new UiColor(0xFFD0D0D0), // Header
        new UiColor(0xFFC0C0C0), // HeaderHovered
        new UiColor(0xFFB0B0B0), // HeaderActive
        new UiColor(0xFFD0D0D0), // Button
        new UiColor(0xFFC0C0C0), // ButtonHovered
        new UiColor(0xFFB0B0B0), // ButtonActive
        new UiColor(0xFFD0D0D0), // Tab
        new UiColor(0xFFC0C0C0), // TabHovered
        new UiColor(0xFFB0B0B0), // TabActive
        new UiColor(0xFF1E90FF), // CheckMark
        new UiColor(0xFF6D8ACF), // SliderGrab
        new UiColor(0xFF4D74C9), // SliderGrabActive
        new UiColor(0xFF444444), // PlotLines
        new UiColor(0xFFBB7A2A), // PlotHistogram
        new UiColor(0xFF9A9A9A), // Separator
        new UiColor(0xFFD8D8D8), // TableHeaderBg
        new UiColor(0xFFF4F4F4), // TableRowBg0
        new UiColor(0xFFEFEFEF), // TableRowBg1
        new UiColor(0xFF9A9A9A), // TableBorder
        new UiColor(0x803A7BD5)  // TextSelectedBg
    );
}

public sealed record class UiStyle(
    UiVector2 WindowPadding,
    UiVector2 ItemSpacing,
    UiVector2 FramePadding,
    UiVector2 ButtonPadding,
    float RowSpacing,
    float CheckboxSpacing,
    float InputWidth,
    float SliderWidth,
    float TreeIndent
)
{
    public static UiStyle Default => new(
        new UiVector2(8f, 8f),
        new UiVector2(8f, 4f),
        new UiVector2(4f, 4f),
        new UiVector2(4f, 4f),
        4f,
        4f,
        220f,
        220f,
        16f
    );

    public UiStyle ScaleAllSizes(float scale)
    {
        var s = MathF.Max(0.1f, scale);
        return this with
        {
            WindowPadding = Scale(WindowPadding, s),
            ItemSpacing = Scale(ItemSpacing, s),
            FramePadding = Scale(FramePadding, s),
            ButtonPadding = Scale(ButtonPadding, s),
            RowSpacing = RowSpacing * s,
            CheckboxSpacing = CheckboxSpacing * s,
            InputWidth = InputWidth * s,
            SliderWidth = SliderWidth * s,
            TreeIndent = TreeIndent * s,
        };
    }

    private static UiVector2 Scale(UiVector2 value, float scale) => new(value.X * scale, value.Y * scale);
}

public enum UiStyleColor
{
    Text,
    TextDisabled,
    WindowBg,
    TitleBg,
    TitleBgActive,
    MenuBarBg,
    PopupBg,
    Border,
    FrameBg,
    FrameBgHovered,
    FrameBgActive,
    Header,
    HeaderHovered,
    HeaderActive,
    Button,
    ButtonHovered,
    ButtonActive,
    Tab,
    TabHovered,
    TabActive,
    CheckMark,
    SliderGrab,
    SliderGrabActive,
    PlotLines,
    PlotHistogram,
    Separator,
    TableHeaderBg,
    TableRowBg0,
    TableRowBg1,
    TableBorder,
    TextSelectedBg,
}

public enum UiStyleVar
{
    WindowPadding,
    ItemSpacing,
    FramePadding,
    IndentSpacing,
}

public readonly record struct UiDrawVertex(UiVector2 Position, UiVector2 UV, UiColor Color);

public readonly record struct UiTextureId(nuint Value);

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1 << 0,
    Shift = 1 << 1,
    Alt = 1 << 2,
    Super = 1 << 3,
}

public enum UiKey
{
    None = 0,
    Tab,
    LeftArrow,
    RightArrow,
    UpArrow,
    DownArrow,
    PageUp,
    PageDown,
    Home,
    End,
    Insert,
    Delete,
    Backspace,
    Space,
    Enter,
    Escape,
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
}

public readonly record struct UiKeyEvent(UiKey Key, bool IsDown, KeyModifiers Modifiers);

public readonly record struct UiCharEvent(uint CodePoint);

public readonly record struct UiKeyRepeatSettings(
    double InitialDelaySeconds,
    double RepeatIntervalSeconds
);

public enum UiTextureFormat
{
    Rgba8Unorm,
    Rgba8Srgb,
}

public enum UiTextureUpdateKind
{
    Create,
    Update,
    Destroy,
}

public enum UiDir
{
    Left,
    Right,
    Up,
    Down,
}

public enum UiMouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
}

public enum UiMouseCursor
{
    Arrow,
    TextInput,
    ResizeAll,
    ResizeNS,
    ResizeEW,
    ResizeNESW,
    ResizeNWSE,
    Hand,
    NotAllowed,
}

[Flags]
public enum UiItemFlags
{
    None = 0,
    AllowOverlap = 1 << 0,
    Disabled = 1 << 1,
}

[Flags]
public enum UiSelectableFlags
{
    None = 0,
}

[Flags]
public enum UiMultiSelectFlags
{
    None = 0,
}

[Flags]
public enum UiDragDropFlags
{
    None = 0,
    SourceNoPreviewTooltip = 1 << 0,
    SourceNoDisableHover = 1 << 1,
    SourceAllowNullID = 1 << 2,
    SourceExtern = 1 << 3,
    AcceptBeforeDelivery = 1 << 10,
    AcceptNoDrawDefaultRect = 1 << 11,
    AcceptNoPreviewTooltip = 1 << 12,
}

[Flags]
public enum UiTreeNodeFlags
{
    None = 0,
    DefaultOpen = 1 << 0,
}

[Flags]
public enum UiTabBarFlags
{
    None = 0,
}

[Flags]
public enum UiTabItemFlags
{
    None = 0,
}

[Flags]
public enum UiInputTextFlags
{
    None = 0,
}

[Flags]
public enum UiTableFlags
{
    None = 0,
    Borders = 1 << 0,
    RowBg = 1 << 1,
    Sortable = 1 << 2,
}

[Flags]
public enum UiTableColumnFlags
{
    None = 0,
}

[Flags]
public enum UiTableRowFlags
{
    None = 0,
}

public enum UiTableBgTarget
{
    RowBg0,
    RowBg1,
    CellBg,
}

public readonly record struct UiTextureUpdate(
    UiTextureUpdateKind Kind,
    UiTextureId TextureId,
    UiTextureFormat Format,
    int Width,
    int Height,
    ReadOnlyMemory<byte> RgbaPixels
);

public sealed record class UiDrawCommand(
    UiRect ClipRect,
    UiTextureId TextureId,
    uint IndexOffset,
    uint ElementCount,
    uint VertexOffset,
    UiDrawCallback? Callback = null,
    object? UserData = null
);

public delegate void UiDrawCallback(UiDrawList drawList, UiDrawCommand command);

public sealed class UiPooledList<T> : IReadOnlyList<T>
{
    private T[]? _buffer;
    private int _count;
    private readonly bool _pooled;

    public UiPooledList(T[] buffer, int count, bool pooled)
    {
        _buffer = buffer;
        _count = count;
        _pooled = pooled;
    }

    public int Count => _count;

    public T this[int index]
    {
        get
        {
            if (_buffer is null)
            {
                throw new ObjectDisposedException(nameof(UiPooledList<T>));
            }

            return _buffer[index];
        }
    }

    public static UiPooledList<T> RentAndCopy(List<T> source)
    {
        if (source.Count == 0)
        {
            return new UiPooledList<T>(Array.Empty<T>(), 0, pooled: false);
        }

        var buffer = ArrayPool<T>.Shared.Rent(source.Count);
        source.CopyTo(buffer);
        return new UiPooledList<T>(buffer, source.Count, pooled: true);
    }

    public static UiPooledList<T> RentAndCopy(ReadOnlySpan<T> source)
    {
        if (source.Length == 0)
        {
            return new UiPooledList<T>(Array.Empty<T>(), 0, pooled: false);
        }

        var buffer = ArrayPool<T>.Shared.Rent(source.Length);
        source.CopyTo(buffer);
        return new UiPooledList<T>(buffer, source.Length, pooled: true);
    }

    public static UiPooledList<T> FromArray(T[] array)
    {
        return new UiPooledList<T>(array, array.Length, pooled: false);
    }

    public static UiPooledList<T> FromArray(T[] array, int count)
    {
        return new UiPooledList<T>(array, count, pooled: false);
    }

    public void Return()
    {
        if (!_pooled || _buffer is null)
        {
            _buffer = null;
            _count = 0;
            return;
        }

        ArrayPool<T>.Shared.Return(_buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _buffer = null;
        _count = 0;
    }

    public ReadOnlySpan<T> AsSpan()
    {
        if (_buffer is null)
        {
            throw new ObjectDisposedException(nameof(UiPooledList<T>));
        }

        return new ReadOnlySpan<T>(_buffer, 0, _count);
    }

    public void CopyTo(Span<T> destination)
    {
        AsSpan().CopyTo(destination);
    }

    public ref readonly T ItemRef(int index)
    {
        if (_buffer is null)
        {
            throw new ObjectDisposedException(nameof(UiPooledList<T>));
        }

        return ref _buffer[index];
    }

    public IEnumerator<T> GetEnumerator()
    {
        if (_buffer is null)
        {
            yield break;
        }

        for (var i = 0; i < _count; i++)
        {
            yield return _buffer[i];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record class UiDrawList(
    UiPooledList<UiDrawVertex> Vertices,
    UiPooledList<uint> Indices,
    UiPooledList<UiDrawCommand> Commands
)
{
    public void ReleasePooled()
    {
        Vertices.Return();
        Indices.Return();
        Commands.Return();
    }

    public UiDrawList DeIndexAllBuffers()
    {
        if (Indices.Count == 0 || Vertices.Count == 0)
        {
            return this;
        }

        var newVertices = new List<UiDrawVertex>(Indices.Count);
        for (var i = 0; i < Indices.Count; i++)
        {
            var index = Indices[i];
            newVertices.Add(Vertices[(int)index]);
        }

        var newIndices = new List<uint>(Indices.Count);
        for (var i = 0; i < newVertices.Count; i++)
        {
            newIndices.Add((uint)i);
        }

        var newCommands = new List<UiDrawCommand>(Commands.Count);
        var offset = 0u;
        for (var i = 0; i < Commands.Count; i++)
        {
            ref readonly var cmd = ref Commands.ItemRef(i);
            newCommands.Add(cmd with { IndexOffset = offset });
            offset += cmd.ElementCount;
        }

        return new UiDrawList(
            UiPooledList<UiDrawVertex>.RentAndCopy(newVertices),
            UiPooledList<uint>.RentAndCopy(newIndices),
            UiPooledList<UiDrawCommand>.RentAndCopy(newCommands)
        );
    }

    public UiDrawList ScaleClipRects(UiVector2 scale)
    {
        if (Commands.Count == 0)
        {
            return this;
        }

        var scaled = new List<UiDrawCommand>(Commands.Count);
        for (var i = 0; i < Commands.Count; i++)
        {
            ref readonly var cmd = ref Commands.ItemRef(i);
            var rect = cmd.ClipRect;
            var scaledRect = new UiRect(rect.X * scale.X, rect.Y * scale.Y, rect.Width * scale.X, rect.Height * scale.Y);
            scaled.Add(cmd with { ClipRect = scaledRect });
        }

        return new UiDrawList(
            UiPooledList<UiDrawVertex>.RentAndCopy(Vertices.AsSpan()),
            UiPooledList<uint>.RentAndCopy(Indices.AsSpan()),
            UiPooledList<UiDrawCommand>.RentAndCopy(scaled)
        );
    }
}

public sealed record class UiDrawData(
    UiVector2 DisplaySize,
    UiVector2 DisplayPos,
    UiVector2 FramebufferScale,
    int TotalVertexCount,
    int TotalIndexCount,
    UiPooledList<UiDrawList> DrawLists,
    UiPooledList<UiTextureUpdate> TextureUpdates
)
{
    public void ReleasePooled()
    {
        for (var i = 0; i < DrawLists.Count; i++)
        {
            DrawLists[i].ReleasePooled();
        }

        DrawLists.Return();
        TextureUpdates.Return();
    }

    public UiDrawData ScaleClipRects(UiVector2 scale)
    {
        if (DrawLists.Count == 0)
        {
            return this;
        }

        var scaled = new List<UiDrawList>(DrawLists.Count);
        for (var i = 0; i < DrawLists.Count; i++)
        {
            scaled.Add(DrawLists[i].ScaleClipRects(scale));
        }

        return this with { DrawLists = UiPooledList<UiDrawList>.RentAndCopy(scaled) };
    }
}

public readonly record struct UiViewport(
    UiVector2 Pos,
    UiVector2 Size,
    UiVector2 WorkPos,
    UiVector2 WorkSize
);

public sealed record class UiDrawListSharedData(
    UiFontAtlas FontAtlas,
    UiTextSettings TextSettings,
    float LineHeight
);

public sealed class UiDragDropPayload
{
    public string DataType { get; internal set; } = string.Empty;
    public ReadOnlyMemory<byte> Data { get; internal set; }
    public int DataSize => Data.Length;
    public bool Preview { get; internal set; }
    public bool Delivery { get; internal set; }
    public string? SourceId { get; internal set; }
    internal int DataFrameCount { get; set; } = -1;

    public bool IsDataType(string type) => string.Equals(DataType, type, StringComparison.Ordinal);
}

public sealed class UiStateStorage
{
    private sealed class Box<T>	where T : struct
    {
        public T Value;
        public Box(T value) => Value = value;
    }

    private readonly Dictionary<string, Box<int>> _ints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Box<float>> _floats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Box<bool>> _bools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Box<nint>> _voidPtrs = new(StringComparer.Ordinal);

    public int GetInt(string key, int defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _ints.TryGetValue(key, out var box) ? box.Value : defaultValue;
    }

    public void SetInt(string key, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_ints.TryGetValue(key, out var box))
        {
            box.Value = value;
            return;
        }

        _ints[key] = new Box<int>(value);
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _floats.TryGetValue(key, out var box) ? box.Value : defaultValue;
    }

    public void SetFloat(string key, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_floats.TryGetValue(key, out var box))
        {
            box.Value = value;
            return;
        }

        _floats[key] = new Box<float>(value);
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _bools.TryGetValue(key, out var box) ? box.Value : defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_bools.TryGetValue(key, out var box))
        {
            box.Value = value;
            return;
        }

        _bools[key] = new Box<bool>(value);
    }

    public nint GetVoidPtr(string key, nint defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _voidPtrs.TryGetValue(key, out var box) ? box.Value : defaultValue;
    }

    public void SetVoidPtr(string key, nint value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_voidPtrs.TryGetValue(key, out var box))
        {
            box.Value = value;
            return;
        }

        _voidPtrs[key] = new Box<nint>(value);
    }

    public ref int GetIntRef(string key, int defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_ints.TryGetValue(key, out var box))
        {
            box = new Box<int>(defaultValue);
            _ints[key] = box;
        }

        return ref box.Value;
    }

    public ref bool GetBoolRef(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_bools.TryGetValue(key, out var box))
        {
            box = new Box<bool>(defaultValue);
            _bools[key] = box;
        }

        return ref box.Value;
    }

    public ref float GetFloatRef(string key, float defaultValue = 0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_floats.TryGetValue(key, out var box))
        {
            box = new Box<float>(defaultValue);
            _floats[key] = box;
        }

        return ref box.Value;
    }

    public ref nint GetVoidPtrRef(string key, nint defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_voidPtrs.TryGetValue(key, out var box))
        {
            box = new Box<nint>(defaultValue);
            _voidPtrs[key] = box;
        }

        return ref box.Value;
    }

    public void BuildSortByKey()
    {
        RebuildSorted(_ints);
        RebuildSorted(_floats);
        RebuildSorted(_bools);
        RebuildSorted(_voidPtrs);
    }

    public void SetAllInt(int value)
    {
        foreach (var entry in _ints.Values)
        {
            entry.Value = value;
        }
    }

    private static void RebuildSorted<T>(Dictionary<string, Box<T>> dict) where T : struct
    {
        if (dict.Count <= 1)
        {
            return;
        }

        var ordered = new List<KeyValuePair<string, Box<T>>>(dict.Count);
        foreach (var entry in dict)
        {
            ordered.Add(entry);
        }

        ordered.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
        dict.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            dict[entry.Key] = entry.Value;
        }
    }
}

public delegate byte[] UiMemAlloc(int size);

public delegate void UiMemFree(byte[] block);

public interface IUiContext : IDisposable
{
    void NewFrame(UiFrameInfo frameInfo);
    void Render();
    UiDrawData GetDrawData();
}

public interface IUiImeHandler
{
    void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth);
}

public readonly record struct UiFrameInfo(
    float DeltaTime,
    UiVector2 DisplaySize,
    UiVector2 DisplayFramebufferScale
);

public interface IRendererBackend : IDisposable
{
    void CreateDeviceObjects();
    void InvalidateDeviceObjects();
    void RenderDrawData(UiDrawData drawData);
    void SetMinImageCount(int count);
}