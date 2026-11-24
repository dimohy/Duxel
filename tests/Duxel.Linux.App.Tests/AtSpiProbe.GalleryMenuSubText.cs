internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryMenuSubText(nint window, Func<string, nint, int?, NativeObject> required,
        Action<string, int> waitForRemoval, List<string> checks, nuint xid)
    {
        const int menuItem = 35;
        const string format = "형식 선택 공유할 이미지는 PNG로 저장하세요.";
        using var opener = required("아이콘 메뉴 열기", window, 43);
        Reveal(opener);
        InvokeNamed(opener, "click");

        using (var item = required(format, window, menuItem))
        {
            if (Description(item.Value) != "PNG")
                throw new InvalidOperationException("The menu's trailing text must remain its AT-SPI description.");
            checks.Add("AT-SPI includes the supporting sentence in the menu item name and keeps PNG in its description.");

            using var singleLine = required("새 항목 선택", window, menuItem);
            if (ComponentBox(item.Value).Height <= ComponentBox(singleLine.Value).Height)
                throw new InvalidOperationException("The supporting sentence needs a taller menu row.");
            checks.Add("The menu allocates additional row height for the supporting sentence.");
        }

        using var peer = new NativePeer(xid);
        peer.KeyBurst([0xff1b]);
        waitForRemoval(format, menuItem);
        checks.Add("X11 Escape dismisses the menu.");

        InvokeNamed(opener, "click");
        using var reopened = required(format, window, menuItem);
        InvokeNamed(reopened, "click");
        using var selected = required("아이콘 메뉴 선택 : format", window, null);
        waitForRemoval(format, menuItem);
        checks.Add("The AT-SPI action activates the original format command and closes the menu.");
    }
}
