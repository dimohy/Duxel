using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Duxel.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using GlfwApi = Silk.NET.GLFW.Glfw;
using GlfwCursor = Silk.NET.GLFW.Cursor;
using GlfwCursorShape = Silk.NET.GLFW.CursorShape;
using GlfwImage = Silk.NET.GLFW.Image;
using GlfwWindowHandle = Silk.NET.GLFW.WindowHandle;

namespace Duxel.Platform.Glfw;

public readonly record struct GlfwPlatformBackendOptions(
    int Width,
    int Height,
    string Title,
    bool VSync,
    IKeyRepeatSettingsProvider KeyRepeatSettingsProvider
);

public sealed class GlfwPlatformBackend : IPlatformBackend, IWin32PlatformBackend
{
    private readonly IWindow _window;
    private readonly IInputContext _inputContext;
    private readonly GlfwInputBackend _inputBackend;
    private readonly IVulkanSurfaceSource? _vulkanSurface;
    private readonly GlfwApi _glfw;
    private readonly unsafe GlfwWindowHandle* _glfwWindow;
    private readonly Dictionary<UiMouseCursor, nint> _cursorCache = new();
    private UiMouseCursor _currentCursor = UiMouseCursor.Arrow;

    public GlfwPlatformBackend(GlfwPlatformBackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.KeyRepeatSettingsProvider);

        Window.PrioritizeOrAdd(CreateWindowingPlatform, isFirstParty: true);

        GlfwWindowing.Use();
        Window.PrioritizeGlfw();
        var windowOptions = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(options.Width, options.Height),
            Title = options.Title,
            VSync = options.VSync,
            IsEventDriven = false,
        };

        _window = Window.Create(windowOptions);
        _window.Initialize();
        CenterWindowOnMonitor(options);

        InputWindowExtensions.ShouldLoadFirstPartyPlatforms(false);
        InputWindowExtensions.Add(CreateInputPlatform());

        _inputContext = _window.CreateInput();
        _inputBackend = GlfwInputBackend.Create(_inputContext, options.KeyRepeatSettingsProvider);
        _vulkanSurface = _window.VkSurface is null ? null : new GlfwVulkanSurfaceSource(_window.VkSurface);

        _glfw = GlfwApi.GetApi();
        unsafe
        {
            _glfwWindow = (GlfwWindowHandle*)_window.Native!.Glfw!.Value;
        }
    }

    public PlatformSize WindowSize
    {
        get
        {
            var size = _window.Size;
            return new PlatformSize(size.X, size.Y);
        }
    }

    public PlatformSize FramebufferSize
    {
        get
        {
            var size = _window.FramebufferSize;
            return new PlatformSize(size.X, size.Y);
        }
    }

    public bool ShouldClose => _window.IsClosing;

    public double TimeSeconds => _window.Time;

    public IInputBackend Input => _inputBackend;

    public IVulkanSurfaceSource? VulkanSurface => _vulkanSurface;

    public nint WindowHandle => _window.Native!.Win32!.Value.Hwnd;

    public void PollEvents()
    {
        _inputBackend.BeginFrame();
        _window.DoEvents();
    }

    public void WaitEvents(int timeoutMilliseconds)
    {
        _inputBackend.BeginFrame();
        if (timeoutMilliseconds <= 0)
        {
            _glfw.WaitEvents();
        }
        else
        {
            var timeoutSeconds = Math.Max(0.001d, timeoutMilliseconds / 1000d);
            _glfw.WaitEventsTimeout(timeoutSeconds);
        }
        _window.DoEvents();
    }

    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.NonPublicConstructors,
        "Silk.NET.Windowing.Glfw.GlfwPlatform",
        "Silk.NET.Windowing.Glfw")]
    private static IWindowPlatform CreateWindowingPlatform()
    {
        var type = Type.GetType("Silk.NET.Windowing.Glfw.GlfwPlatform, Silk.NET.Windowing.Glfw", throwOnError: true);
        if (type is null || !typeof(IWindowPlatform).IsAssignableFrom(type))
        {
            throw new PlatformNotSupportedException("GLFW windowing platform type is not available.");
        }

        return (IWindowPlatform)Activator.CreateInstance(type, nonPublic: true)!;
    }

    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.NonPublicConstructors,
        "Silk.NET.Input.Glfw.GlfwInputPlatform",
        "Silk.NET.Input.Glfw")]
    private static IInputPlatform CreateInputPlatform()
    {
        var type = Type.GetType("Silk.NET.Input.Glfw.GlfwInputPlatform, Silk.NET.Input.Glfw", throwOnError: true);
        if (type is null || !typeof(IInputPlatform).IsAssignableFrom(type))
        {
            throw new PlatformNotSupportedException("GLFW input platform type is not available.");
        }

        return (IInputPlatform)Activator.CreateInstance(type, nonPublic: true)!;
    }

    public void SetMouseCursor(UiMouseCursor cursor)
    {
        if (cursor == _currentCursor)
        {
            return;
        }

        _currentCursor = cursor;
        var handle = GetCursorHandle(cursor);
        unsafe
        {
            _glfw.SetCursor(_glfwWindow, (GlfwCursor*)handle);
        }
    }

    public void Dispose()
    {
        unsafe
        {
            foreach (var handle in _cursorCache.Values)
            {
                _glfw.DestroyCursor((GlfwCursor*)handle);
            }
        }
        _cursorCache.Clear();
        _inputContext.Dispose();
        _window.Dispose();
    }

    private nint GetCursorHandle(UiMouseCursor cursor)
    {
        if (_cursorCache.TryGetValue(cursor, out var handle))
        {
            return handle;
        }

        var shape = cursor switch
        {
            UiMouseCursor.TextInput => GlfwCursorShape.IBeam,
            UiMouseCursor.Hand => GlfwCursorShape.Hand,
            UiMouseCursor.ResizeEW => GlfwCursorShape.HResize,
            UiMouseCursor.ResizeNS => GlfwCursorShape.VResize,
            UiMouseCursor.ResizeNWSE => GlfwCursorShape.Arrow,
            UiMouseCursor.ResizeNESW => GlfwCursorShape.Arrow,
            UiMouseCursor.ResizeAll => GlfwCursorShape.HResize,
            UiMouseCursor.NotAllowed => GlfwCursorShape.Arrow,
            _ => GlfwCursorShape.Arrow,
        };

        if (cursor is UiMouseCursor.ResizeNWSE or UiMouseCursor.ResizeNESW)
        {
            handle = CreateDiagonalCursor(cursor == UiMouseCursor.ResizeNWSE);
            _cursorCache[cursor] = handle;
            return handle;
        }

        unsafe
        {
            handle = (nint)_glfw.CreateStandardCursor(shape);
        }

        _cursorCache[cursor] = handle;
        return handle;
    }

    private nint CreateDiagonalCursor(bool isNWSE)
    {
        const int size = 24;
        const int hot = 12;
        var pixels = new byte[size * size * 4];

        void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
        {
            if ((uint)x >= size || (uint)y >= size)
            {
                return;
            }

            var index = (y * size + x) * 4;
            pixels[index + 0] = r;
            pixels[index + 1] = g;
            pixels[index + 2] = b;
            pixels[index + 3] = a;
        }

        static void DrawLine(int x0, int y0, int x1, int y1, Action<int, int> plot)
        {
            var dx = Math.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Math.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var err = dx + dy;

            while (true)
            {
                plot(x0, y0);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                var e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        void PlotThick(int x, int y)
        {
            SetPixel(x, y, 255, 255, 255, 255);
        }

        static int Edge(int ax, int ay, int bx, int by, int px, int py) => (px - ax) * (by - ay) - (py - ay) * (bx - ax);

        void FillTriangle(int ax, int ay, int bx, int by, int cx, int cy)
        {
            var minX = Math.Min(ax, Math.Min(bx, cx));
            var maxX = Math.Max(ax, Math.Max(bx, cx));
            var minY = Math.Min(ay, Math.Min(by, cy));
            var maxY = Math.Max(ay, Math.Max(by, cy));

            minX = Math.Clamp(minX, 0, size - 1);
            maxX = Math.Clamp(maxX, 0, size - 1);
            minY = Math.Clamp(minY, 0, size - 1);
            maxY = Math.Clamp(maxY, 0, size - 1);

            var area = Edge(ax, ay, bx, by, cx, cy);
            if (area == 0)
            {
                return;
            }

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var e1 = Edge(ax, ay, bx, by, x, y);
                    var e2 = Edge(bx, by, cx, cy, x, y);
                    var e3 = Edge(cx, cy, ax, ay, x, y);
                    if ((e1 >= 0 && e2 >= 0 && e3 >= 0) || (e1 <= 0 && e2 <= 0 && e3 <= 0))
                    {
                        SetPixel(x, y, 255, 255, 255, 255);
                    }
                }
            }
        }

        if (isNWSE)
        {
            DrawLine(7, 7, 16, 16, PlotThick);
            FillTriangle(4, 4, 9, 4, 4, 9);
            FillTriangle(19, 19, 14, 19, 19, 14);
        }
        else
        {
            DrawLine(16, 7, 7, 16, PlotThick);
            FillTriangle(19, 4, 14, 4, 19, 9);
            FillTriangle(4, 19, 9, 19, 4, 14);
        }

        unsafe
        {
            fixed (byte* pPixels = pixels)
            {
                var image = new GlfwImage
                {
                    Width = size,
                    Height = size,
                    Pixels = pPixels
                };
                return (nint)_glfw.CreateCursor(&image, hot, hot);
            }
        }
    }

    private void CenterWindowOnMonitor(GlfwPlatformBackendOptions options)
    {
        var monitor = _window.Monitor;
        if (monitor is null)
        {
            return;
        }

        var bounds = monitor.Bounds;
        var origin = bounds.Origin;
        var size = bounds.Size;
        var positionX = origin.X + (size.X - options.Width) / 2;
        var positionY = origin.Y + (size.Y - options.Height) / 2;
        _window.Position = new Vector2D<int>(positionX, positionY);
    }
}

internal sealed class GlfwInputBackend : IInputBackend
{
    private readonly IKeyboard _keyboard;
    private readonly IMouse _mouse;
    private readonly UiKeyRepeatSettings _keyRepeatSettings;
    private readonly List<UiKeyEvent> _keyEvents = [];
    private readonly List<UiCharEvent> _charEvents = [];
    private UiVector2 _mousePosition;
    private bool _leftDown;
    private bool _rightDown;
    private bool _middleDown;
    private bool _leftPressedEvent;
    private bool _leftReleasedEvent;
    private bool _rightPressedEvent;
    private bool _rightReleasedEvent;
    private bool _middlePressedEvent;
    private bool _middleReleasedEvent;
    private float _wheel;
    private float _wheelHorizontal;

    private GlfwInputBackend(IKeyboard keyboard, IMouse mouse, UiKeyRepeatSettings keyRepeatSettings)
    {
        _keyboard = keyboard;
        _mouse = mouse;
        _keyRepeatSettings = keyRepeatSettings;

        _keyboard.KeyDown += OnKeyDown;
        _keyboard.KeyUp += OnKeyUp;
        _keyboard.KeyChar += OnKeyChar;

        _mouse.MouseDown += OnMouseDown;
        _mouse.MouseUp += OnMouseUp;
        _mouse.MouseMove += OnMouseMove;
        _mouse.Scroll += OnMouseScroll;
    }

    public static GlfwInputBackend Create(IInputContext inputContext, IKeyRepeatSettingsProvider keyRepeatProvider)
    {
        if (inputContext.Keyboards.Count is 0)
        {
            throw new InvalidOperationException("No keyboard device available.");
        }

        if (inputContext.Mice.Count is 0)
        {
            throw new InvalidOperationException("No mouse device available.");
        }

        return new GlfwInputBackend(inputContext.Keyboards[0], inputContext.Mice[0], keyRepeatProvider.GetSettings());
    }

    public InputSnapshot Snapshot => new(
        _mousePosition,
        _leftDown,
        _rightDown,
        _middleDown,
        _leftPressedEvent,
        _leftReleasedEvent,
        _rightPressedEvent,
        _rightReleasedEvent,
        _middlePressedEvent,
        _middleReleasedEvent,
        _wheel,
        _wheelHorizontal,
        _keyEvents,
        _charEvents,
        _keyRepeatSettings
    );

    public void BeginFrame()
    {
        _keyEvents.Clear();
        _charEvents.Clear();
        _leftPressedEvent = false;
        _leftReleasedEvent = false;
        _rightPressedEvent = false;
        _rightReleasedEvent = false;
        _middlePressedEvent = false;
        _middleReleasedEvent = false;
        _wheel = 0;
        _wheelHorizontal = 0;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int _) =>
        _keyEvents.Add(new UiKeyEvent(MapKey(key), true, GetModifiers()));

    private void OnKeyUp(IKeyboard keyboard, Key key, int _) =>
        _keyEvents.Add(new UiKeyEvent(MapKey(key), false, GetModifiers()));

    private void OnKeyChar(IKeyboard keyboard, char c) =>
        _charEvents.Add(new UiCharEvent(c));

    private void OnMouseDown(IMouse mouse, Silk.NET.Input.MouseButton button) => SetMouseButton(button, true);

    private void OnMouseUp(IMouse mouse, Silk.NET.Input.MouseButton button) => SetMouseButton(button, false);

    private void OnMouseMove(IMouse mouse, Vector2 position) =>
        _mousePosition = new UiVector2(position.X, position.Y);

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _wheel += wheel.Y;
        _wheelHorizontal += wheel.X;
    }

    private void SetMouseButton(Silk.NET.Input.MouseButton button, bool isDown)
    {
        switch (button)
        {
            case Silk.NET.Input.MouseButton.Left:
                if (_leftDown != isDown)
                {
                    if (isDown)
                    {
                        _leftPressedEvent = true;
                    }
                    else
                    {
                        _leftReleasedEvent = true;
                    }
                }
                _leftDown = isDown;
                break;
            case Silk.NET.Input.MouseButton.Right:
                if (_rightDown != isDown)
                {
                    if (isDown)
                    {
                        _rightPressedEvent = true;
                    }
                    else
                    {
                        _rightReleasedEvent = true;
                    }
                }
                _rightDown = isDown;
                break;
            case Silk.NET.Input.MouseButton.Middle:
                if (_middleDown != isDown)
                {
                    if (isDown)
                    {
                        _middlePressedEvent = true;
                    }
                    else
                    {
                        _middleReleasedEvent = true;
                    }
                }
                _middleDown = isDown;
                break;
        }
    }

    private KeyModifiers GetModifiers()
    {
        var modifiers = KeyModifiers.None;

        if (IsPressed(Key.ControlLeft) || IsPressed(Key.ControlRight))
        {
            modifiers |= KeyModifiers.Ctrl;
        }

        if (IsPressed(Key.ShiftLeft) || IsPressed(Key.ShiftRight))
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (IsPressed(Key.AltLeft) || IsPressed(Key.AltRight))
        {
            modifiers |= KeyModifiers.Alt;
        }

        if (IsPressed(Key.SuperLeft) || IsPressed(Key.SuperRight))
        {
            modifiers |= KeyModifiers.Super;
        }

        return modifiers;
    }

    private bool IsPressed(Key key) => _keyboard.IsKeyPressed(key);

    private static UiKey MapKey(Key key) => key switch
    {
        Key.Tab => UiKey.Tab,
        Key.Left => UiKey.LeftArrow,
        Key.Right => UiKey.RightArrow,
        Key.Up => UiKey.UpArrow,
        Key.Down => UiKey.DownArrow,
        Key.PageUp => UiKey.PageUp,
        Key.PageDown => UiKey.PageDown,
        Key.Home => UiKey.Home,
        Key.End => UiKey.End,
        Key.Insert => UiKey.Insert,
        Key.Delete => UiKey.Delete,
        Key.Backspace => UiKey.Backspace,
        Key.Space => UiKey.Space,
        Key.Enter => UiKey.Enter,
        Key.Escape => UiKey.Escape,
        Key.A => UiKey.A,
        Key.B => UiKey.B,
        Key.C => UiKey.C,
        Key.D => UiKey.D,
        Key.E => UiKey.E,
        Key.F => UiKey.F,
        Key.G => UiKey.G,
        Key.H => UiKey.H,
        Key.I => UiKey.I,
        Key.J => UiKey.J,
        Key.K => UiKey.K,
        Key.L => UiKey.L,
        Key.M => UiKey.M,
        Key.N => UiKey.N,
        Key.O => UiKey.O,
        Key.P => UiKey.P,
        Key.Q => UiKey.Q,
        Key.R => UiKey.R,
        Key.S => UiKey.S,
        Key.T => UiKey.T,
        Key.U => UiKey.U,
        Key.V => UiKey.V,
        Key.W => UiKey.W,
        Key.X => UiKey.X,
        Key.Y => UiKey.Y,
        Key.Z => UiKey.Z,
        Key.F1 => UiKey.F1,
        Key.F2 => UiKey.F2,
        Key.F3 => UiKey.F3,
        Key.F4 => UiKey.F4,
        Key.F5 => UiKey.F5,
        Key.F6 => UiKey.F6,
        Key.F7 => UiKey.F7,
        Key.F8 => UiKey.F8,
        Key.F9 => UiKey.F9,
        Key.F10 => UiKey.F10,
        Key.F11 => UiKey.F11,
        Key.F12 => UiKey.F12,
        _ => UiKey.None,
    };
}

internal sealed class GlfwVulkanSurfaceSource(IVkSurface surface) : IVulkanSurfaceSource
{
    public IReadOnlyList<string> RequiredInstanceExtensions
    {
        get
        {
            unsafe
            {
                var extensions = surface.GetRequiredExtensions(out var count);
                var result = new string[count];

                for (var i = 0u; i < count; i++)
                {
                    result[i] = SilkMarshal.PtrToString((nint)extensions[i]) ?? string.Empty;
                }

                return result;
            }
        }
    }

    public nuint CreateSurface(nuint instanceHandle)
    {
        unsafe
        {
            var handle = surface.Create<AllocationCallbacks>(new VkHandle((nint)instanceHandle), null);
            return (nuint)handle.Handle;
        }
    }
}

