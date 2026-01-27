using System.Numerics;
using Dux.Core;
using Dux.Platform.Windows;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;

namespace Dux.Platform.Glfw;

public readonly record struct GlfwPlatformBackendOptions(
    int Width,
    int Height,
    string Title,
    bool VSync
);

public sealed class GlfwPlatformBackend : IPlatformBackend, IWin32PlatformBackend
{
    private readonly IWindow _window;
    private readonly IInputContext _inputContext;
    private readonly GlfwInputBackend _inputBackend;
    private readonly IVulkanSurfaceSource? _vulkanSurface;

    public GlfwPlatformBackend(GlfwPlatformBackendOptions options)
    {
        if (!Window.TryAdd("Silk.NET.Windowing.Glfw"))
        {
            throw new PlatformNotSupportedException("GLFW windowing platform not registered.");
        }

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

        if (!InputWindowExtensions.TryAdd("Silk.NET.Input.Glfw"))
        {
            throw new PlatformNotSupportedException("GLFW input platform not registered.");
        }

        _inputContext = _window.CreateInput();
        _inputBackend = GlfwInputBackend.Create(_inputContext, new WindowsKeyRepeatSettingsProvider());
        _vulkanSurface = _window.VkSurface is null ? null : new GlfwVulkanSurfaceSource(_window.VkSurface);
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

    public void Dispose()
    {
        _inputContext.Dispose();
        _window.Dispose();
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
        _wheel = 0;
        _wheelHorizontal = 0;
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int _) =>
        _keyEvents.Add(new UiKeyEvent(MapKey(key), true, GetModifiers()));

    private void OnKeyUp(IKeyboard keyboard, Key key, int _) =>
        _keyEvents.Add(new UiKeyEvent(MapKey(key), false, GetModifiers()));

    private void OnKeyChar(IKeyboard keyboard, char c) =>
        _charEvents.Add(new UiCharEvent(c));

    private void OnMouseDown(IMouse mouse, MouseButton button) => SetMouseButton(button, true);

    private void OnMouseUp(IMouse mouse, MouseButton button) => SetMouseButton(button, false);

    private void OnMouseMove(IMouse mouse, Vector2 position) =>
        _mousePosition = new UiVector2(position.X, position.Y);

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _wheel += wheel.Y;
        _wheelHorizontal += wheel.X;
    }

    private void SetMouseButton(MouseButton button, bool isDown)
    {
        switch (button)
        {
            case MouseButton.Left:
                _leftDown = isDown;
                break;
            case MouseButton.Right:
                _rightDown = isDown;
                break;
            case MouseButton.Middle:
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
