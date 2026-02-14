using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Duxel.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct UiVector2(float X, float Y);

public readonly record struct UiVector4(float X, float Y, float Z, float W);

public readonly record struct UiRect(float X, float Y, float Width, float Height);

[StructLayout(LayoutKind.Sequential)]
public readonly record struct UiColor(uint Rgba)
{
    /// <summary>
    /// Creates a UiColor from individual RGBA byte components.
    /// </summary>
    public UiColor(byte r, byte g, byte b, byte a = 255)
        : this((uint)a << 24 | (uint)b << 16 | (uint)g << 8 | r) { }
}

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
    UiColor TextSelectedBg,
    UiColor ScrollbarBg,
    UiColor ScrollbarGrab,
    UiColor ScrollbarGrabHovered,
    UiColor ScrollbarGrabActive
)
{
    public static UiTheme ImGuiDark => new(
        new UiColor(0xFFE6E8EA), // Text
        new UiColor(0xFF8A9099), // TextDisabled
        new UiColor(0xFF1E1F22), // WindowBg
        new UiColor(0xFF1B1C1F), // TitleBg
        new UiColor(0xFF2A2D32), // TitleBgActive
        new UiColor(0xFF202225), // MenuBarBg
        new UiColor(0xEE1F2124), // PopupBg
        new UiColor(0xFF2C2F36), // Border
        new UiColor(0xFF2B2D31), // FrameBg
        new UiColor(0xFF3A3F47), // FrameBgHovered
        new UiColor(0xFF4A515C), // FrameBgActive
        new UiColor(0xFF2F3136), // Header
        new UiColor(0xFF3C424D), // HeaderHovered
        new UiColor(0xFF4C5462), // HeaderActive
        new UiColor(0xFF2F3136), // Button
        new UiColor(0xFF3C424D), // ButtonHovered
        new UiColor(0xFF4C5462), // ButtonActive
        new UiColor(0xFF26292E), // Tab
        new UiColor(0xFF3A3F47), // TabHovered
        new UiColor(0xFF4A515C), // TabActive
        new UiColor(0xFF58A6FF), // CheckMark
        new UiColor(0xFF58A6FF), // SliderGrab
        new UiColor(0xFF79B8FF), // SliderGrabActive
        new UiColor(0xFFB0B6BE), // PlotLines
        new UiColor(0xFFB58A42), // PlotHistogram
        new UiColor(0xFF2C2F36), // Separator
        new UiColor(0xFF26292E), // TableHeaderBg
        new UiColor(0xFF1E1F22), // TableRowBg0
        new UiColor(0xFF22252A), // TableRowBg1
        new UiColor(0xFF2C2F36), // TableBorder
        new UiColor(0x88406AA3)  // TextSelectedBg
        ,new UiColor(0x87020202)  // ScrollbarBg
        ,new UiColor(0xFF4F4F4F) // ScrollbarGrab
        ,new UiColor(0xFF696969) // ScrollbarGrabHovered
        ,new UiColor(0xFF828282) // ScrollbarGrabActive
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
        ,new UiColor(0x33000000)  // ScrollbarBg
        ,new UiColor(0xFFA0A0A0) // ScrollbarGrab
        ,new UiColor(0xFFB0B0B0) // ScrollbarGrabHovered
        ,new UiColor(0xFFC0C0C0) // ScrollbarGrabActive
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
        ,new UiColor(0x33000000)  // ScrollbarBg
        ,new UiColor(0xFF9A9A9A) // ScrollbarGrab
        ,new UiColor(0xFFAEAEAE) // ScrollbarGrabHovered
        ,new UiColor(0xFFC0C0C0) // ScrollbarGrabActive
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
    float TreeIndent,
    float ScrollbarSize
)
{
    public static UiStyle Default => new(
        new UiVector2(10f, 10f),
        new UiVector2(10f, 6f),
        new UiVector2(6f, 4f),
        new UiVector2(6f, 4f),
        6f,
        6f,
        240f,
        240f,
        18f,
        14f
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
            ScrollbarSize = ScrollbarSize * s,
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
    ScrollbarBg,
    ScrollbarGrab,
    ScrollbarGrabHovered,
    ScrollbarGrabActive,
}

public enum UiStyleVar
{
    WindowPadding,
    ItemSpacing,
    FramePadding,
    IndentSpacing,
}

[StructLayout(LayoutKind.Sequential)]
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

/// <summary>
/// ArrayPool-backed buffer with zero-copy transfer to <see cref="UiPooledList{T}"/>.
/// Replaces List&lt;T&gt; for vertex/index/command buffers in draw list building.
/// </summary>
internal sealed class PooledBuffer<T>
{
    private T[] _array;
    private int _count;

    public PooledBuffer(int initialCapacity = 1024)
    {
        _array = ArrayPool<T>.Shared.Rent(Math.Max(initialCapacity, 16));
        _count = 0;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    public int Capacity => _array.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if (capacity > _array.Length)
        {
            Grow(capacity);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int capacity)
    {
        var newArray = ArrayPool<T>.Shared.Rent(capacity);
        _array.AsSpan(0, _count).CopyTo(newArray);
        ArrayPool<T>.Shared.Return(_array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _array = newArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var array = _array;
        var count = _count;
        if ((uint)count < (uint)array.Length)
        {
            array[count] = item;
            _count = count + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        Grow(_count + 1);
        _array[_count] = item;
        _count++;
    }

    public void AddRange(IReadOnlyList<T> source)
    {
        var sourceCount = source.Count;
        EnsureCapacity(_count + sourceCount);
        if (source is UiPooledList<T> pooled)
        {
            pooled.AsSpan().CopyTo(_array.AsSpan(_count));
        }
        else
        {
            for (var i = 0; i < sourceCount; i++)
            {
                _array[_count + i] = source[i];
            }
        }
        _count += sourceCount;
    }

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _array[index];
    }

    public ref T this[Index index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _array[index.GetOffset(_count)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _array.AsSpan(0, _count);

    public void SetCount(int count)
    {
        if (count > _array.Length)
        {
            Grow(count);
        }
        _count = count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _count = 0;

    public void RemoveAt(int index)
    {
        _count--;
        if (index < _count)
        {
            Array.Copy(_array, index + 1, _array, index, _count - index);
        }
    }

    public void RemoveRange(int start, int count)
    {
        if (start + count < _count)
        {
            Array.Copy(_array, start + count, _array, start, _count - start - count);
        }
        _count -= count;
    }

    public void TrimExcess()
    {
        // For pooled buffers, return old and rent a minimal one
        if (_count == 0 && _array.Length > 64)
        {
            ArrayPool<T>.Shared.Return(_array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            _array = ArrayPool<T>.Shared.Rent(16);
        }
    }

    /// <summary>
    /// Zero-copy transfer: hands off the backing array to a <see cref="UiPooledList{T}"/> and rents a fresh buffer.
    /// </summary>
    public UiPooledList<T> TransferToPooledList()
    {
        if (_count == 0)
        {
            return new UiPooledList<T>(Array.Empty<T>(), 0, pooled: false);
        }
        var result = new UiPooledList<T>(_array, _count, pooled: true);
        _array = ArrayPool<T>.Shared.Rent(1024);
        _count = 0;
        return result;
    }
}

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

    public static UiPooledList<T> RentAndCopy(IReadOnlyList<T> source)
    {
        var count = source.Count;
        if (count == 0)
        {
            return new UiPooledList<T>(Array.Empty<T>(), 0, pooled: false);
        }

        var buffer = ArrayPool<T>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            buffer[i] = source[i];
        }

        return new UiPooledList<T>(buffer, count, pooled: true);
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
    string? GetCompositionText();
    void SetCompositionOwner(string? inputId);
    string? ConsumeCommittedText(string inputId);
    string? ConsumeRecentCommittedText();
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
    void SetVSync(bool enable);
    void SetMsaaSamples(int samples);
    void SetTaaEnabled(bool enable);
    void SetFxaaEnabled(bool enable);
    void SetTaaExcludeFont(bool exclude);
    void SetTaaCurrentFrameWeight(float weight);
}
