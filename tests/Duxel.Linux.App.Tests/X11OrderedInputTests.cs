using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Linux;

internal static unsafe partial class X11OrderedInputTests
{
    internal static void Run()
    {
        using var platform = new LinuxX11PlatformBackend(new(400, 300, Visible: false));
        var display = X11Native.XOpenDisplay(0);
        if (display == 0)
            throw new InvalidOperationException("Cannot open the X11 sender connection.");

        var window = platform.WindowHandle;
        var root = X11Native.XDefaultRootWindow(display);
        try
        {
            platform.PollEvents();
            Send(2, 1, Keycode('x'));
            Send(3, 2, Keycode('x'));
            Send(2, 1, Keycode(0xff51));
            Send(3, 2, Keycode(0xff51));
            Send(2, 1, Keycode('y'));
            Send(3, 2, Keycode('y'));
            Send(4, 4, 1);
            Send(5, 8, 1);
            Send(4, 4, 5);
            Send(4, 4, 6);
            _ = XSync(display, 0);
            platform.PollEvents();

            var snapshot = platform.Input.Snapshot;
            var steps = snapshot.OrderedInputs;
            Check(steps is { Count: 10 }, "One ordered step per delivered native event.");
            var actual = steps!;
            Check(actual[0].KeyEvents is [{ Key: UiKey.X, IsDown: true }] && actual[0].CharEvents is [{ CodePoint: 'x' }]
                && actual[1].KeyEvents is [{ Key: UiKey.X, IsDown: false }] && actual[1].CharEvents.Count == 0,
                "A native key press carries only its own committed text.");
            Check(actual[2].KeyEvents is [{ Key: UiKey.LeftArrow, IsDown: true }]
                && actual[3].KeyEvents is [{ Key: UiKey.LeftArrow, IsDown: false }]
                && actual[2].CharEvents.Count == 0 && actual[3].CharEvents.Count == 0
                && actual[4].KeyEvents is [{ Key: UiKey.Y, IsDown: true }] && actual[4].CharEvents is [{ CodePoint: 'y' }]
                && actual[5].KeyEvents is [{ Key: UiKey.Y, IsDown: false }],
                "A navigation key stays between the two character-producing keys.");
            Check(actual[6].LeftMouseDown && actual[6].LeftMousePressed && !actual[6].LeftMouseReleased
                && !actual[7].LeftMouseDown && !actual[7].LeftMousePressed && actual[7].LeftMouseReleased,
                "Button press and release remain distinct within one poll.");
            Check(actual[8].MouseWheel == -1 && actual[8].MouseWheelHorizontal == 0
                && actual[9].MouseWheel == 0 && actual[9].MouseWheelHorizontal == 1,
                "Wheel steps contain only their own axis delta.");
            Check(actual.All(step => step.CoordinateScale == platform.ContentScale)
                && snapshot.KeyEvents.SequenceEqual(actual.SelectMany(step => step.KeyEvents))
                && snapshot.CharEvents.SequenceEqual(actual.SelectMany(step => step.CharEvents)),
                "Native scale and aggregate input agree with the ordered steps.");
            platform.PollEvents();
            Check(platform.Input.Snapshot.OrderedInputs is { Count: 0 }
                && platform.Input.Snapshot.KeyEvents.Count == 0 && platform.Input.Snapshot.CharEvents.Count == 0,
                "The next idle poll does not replay events.");
        }
        finally { _ = X11Native.XCloseDisplay(display); }

        uint Keycode(nuint symbol)
        {
            var code = XKeysymToKeycode(display, symbol);
            return code != 0 ? code : throw new InvalidOperationException($"No X11 keycode for {symbol}.");
        }

        void Send(int type, nint mask, uint detail)
        {
            var input = new X11Native.Event
            {
                Input = new()
                {
                    Type = type, Display = display, Window = window, Root = root, SameScreen = 1,
                    X = 24, Y = 28, Detail = detail
                }
            };
            if (XSendEvent(display, window, 0, mask, &input) == 0)
                throw new InvalidOperationException($"XSendEvent failed for event type {type}.");
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + name);
    }

    [LibraryImport("libX11.so.6")]
    private static partial byte XKeysymToKeycode(nint display, nuint symbol);

    [LibraryImport("libX11.so.6")]
    private static partial int XSendEvent(nint display, nuint window, int propagate, nint mask, X11Native.Event* input);

    [LibraryImport("libX11.so.6")]
    private static partial int XSync(nint display, int discard);
}
