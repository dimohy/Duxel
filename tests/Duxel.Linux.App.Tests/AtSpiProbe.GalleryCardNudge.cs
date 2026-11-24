internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryCardNudge(nint window, Func<string, nint, int?, NativeObject> required,
        Action<string, int> waitForRemoval, List<string> checks, nuint xid)
    {
        const int buttonRole = 43;
        const int focusedState = 12;
        const string messageName = "빠르게 찾아보세요";
        const string messageStatus = "즐겨찾기 팁을 열었습니다.";
        const string initialStatus = "최근에 살펴본 예제";
        using var peer = new NativePeer(xid);
        using var reset = required("안내 다시 표시", window, buttonRole);
        using var firstMessage = required(messageName, window, buttonRole);
        using var accent = required("즐겨찾기 팁", window, null);
        using var firstAction = required("즐겨찾기에 추가", window, buttonRole);
        using var firstClose = required("안내 닫기", window, buttonRole);
        checks.Add("The normal Gallery exposes the accented message, separate action and close button through AT-SPI.");

        Reveal(firstMessage);
        InvokeNamed(firstMessage, "click");
        using var invoked = required(messageStatus, window, null);
        using var stillOpen = required("즐겨찾기에 추가", window, buttonRole);
        checks.Add("AT-SPI invokes the message and leaves the separate action available.");

        Reset();
        using var keyboardMessage = required(messageName, window, buttonRole);
        Reveal(keyboardMessage);
        using var component = new NativeObject(atspi_accessible_get_component_iface(keyboardMessage.Value));
        nint error = 0;
        var focusResult = atspi_component_grab_focus(component.Value, &error);
        Error(ref error);
        if (focusResult == 0)
            throw new InvalidOperationException("The CardNudge message refused AT-SPI focus.");
        Wait(() => HasState(keyboardMessage.Value, focusedState), "the focused CardNudge message");
        peer.KeyBurst([0xff0d]);
        using var keyboardStatus = required(messageStatus, window, null);
        checks.Add("X11 Enter invokes the message after external focus.");

        foreach (var direction in new[] { -1, 1 })
        {
            Reset();
            using var message = required(messageName, window, buttonRole);
            Reveal(message);
            var box = ComponentBox(message.Value);
            if (box.Width < 190 || box.Height < 24)
                throw new InvalidOperationException($"CardNudge message has invalid native bounds {box}.");
            var start = box.X + box.Width / 2;
            var end = start + direction * 86;
            var y = box.Y + box.Height / 2;
            peer.Pointer(6, start, y);
            peer.Pointer(4, start, y, 1);
            for (var step = 1; step <= 4; step++)
            {
                Thread.Sleep(45);
                peer.Pointer(6, start + (end - start) * step / 4, y, state : 256);
            }
            peer.Pointer(5, end, y, 1, 256);
            waitForRemoval(messageName, buttonRole);
            using var unchanged = required(initialStatus, window, null);
            checks.Add(direction < 0 ? "An X11 left swipe dismisses without running the message action."
                : "An X11 right swipe dismisses without running the message action.");
        }

        Reset();
        using var finalAction = required("즐겨찾기에 추가", window, buttonRole);
        Reveal(finalAction);
        InvokeNamed(finalAction, "click");
        waitForRemoval(messageName, buttonRole);
        using var saved = required("즐겨찾기에 추가했습니다.", window, null);
        checks.Add("Invoking the independent action saves, dismisses and retires the message from AT-SPI.");
        peer.Close();

        void Reset()
        {
            Reveal(reset);
            InvokeNamed(reset, "click");
            using var restored = required(initialStatus, window, null);
        }
    }
}
