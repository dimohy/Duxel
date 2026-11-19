internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGallerySvgPixels(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, string output, nuint windowId)
    {
        using var tab = required("브라우저 비교", window, 37);
        Reveal(tab);
        InvokeNamed(tab, "select");
        using var backgroundSwitch = required("흰색 배경을 SVG에 포함", window, null);
        using var peer = new NativePeer(windowId);
        var initial = CaptureGalleryPair(peer, output, "SvgOriginal", checks);

        SetSlider("SVG 확대", 2);
        SetSlider("SVG 회전", 37);
        // The software Vulkan path can present the gesture preview before the exact frame.
        Thread.Sleep(1500);
        CaptureGalleryPair(peer, output, "SvgZoom2Rotate37", checks, initial);

        // The rotated preview can extend below the initial viewport, leaving only
        // its white background visible. Scroll the owned window and require painted pixels.
        const int previewX = 985, previewY = 815;
        peer.Pointer(6, previewX, previewY);
        for (var step = 0; step < 4; step++)
            peer.Pointer(4, previewX, previewY, 5);
        CaptureGalleryPair(peer, output, "SvgZoom2Rotate37Scrolled", checks, requireVisibleVector: true);

        void SetSlider(string name, double expected)
        {
            using var slider = required(name, window, 51);
            Reveal(slider);
            using var numeric = new NativeObject(atspi_accessible_get_value_iface(slider.Value));
            nint error = 0;
            var accepted = atspi_value_set_current_value(numeric.Value, expected, &error);
            Error(ref error);
            if (accepted == 0)
                throw new InvalidOperationException($"The Gallery SVG slider rejected its value : {name}");
            Wait(() =>
            {
                nint readError = 0;
                var current = atspi_value_get_current_value(numeric.Value, &readError);
                Error(ref readError);
                return current == expected;
            }, name + " to publish its exact value");
            checks.Add($"External AT-SPI set and read {name} at {expected}.");
        }
    }

    private static byte[] CaptureGalleryPair(NativePeer peer, string output, string page, List<string> checks,
        byte[]? previousImage = null, bool requireVisibleVector = false)
    {
        const int width = 1280, height = 900;
        var directory = Path.GetDirectoryName(Path.GetFullPath(output))!;
        Wait(() => peer.CanCapture(width, height, out _), "the mapped Gallery capture rectangle");
        byte[] current = [];
        for (var index = 1; index <= 2; index++)
        {
            var file = Path.Combine(directory, $"{page}-{index}.ppm");
            var started = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                Thread.Sleep(350);
                if (peer.Capture(file, width, height, pixels => current = pixels) < 16)
                    throw new InvalidOperationException($"Gallery capture has too few visible colors : {page}.");
                if ((previousImage is null || CapturedSvgChanged(current, previousImage, width, height))
                    && (!requireVisibleVector || CapturedRedVector(current, width, height)))
                    break;
                if (started.Elapsed.TotalSeconds > 10)
                    throw new TimeoutException("The requested SVG state did not produce the expected pixels in the X11 viewport.");
            }
            while (true);
        }
        checks.Add("The external X11 client captured the live " + page + " page twice.");
        return current;
    }

    private static bool CapturedRedVector(byte[] pixels, int width, int height)
    {
        // The reference image ends before x=700. Look for the red car inside the GPU preview.
        var redPixels = 0;
        for (var row = 180; row < height - 8; row++)
            for (var column = 700; column < width - 24; column++)
            {
                var index = (row * width + column) * 3;
                if (pixels[index] > 160 && pixels[index] > pixels[index + 1] + 75
                    && pixels[index] > pixels[index + 2] + 75 && ++redPixels >= 512)
                    return true;
            }
        return false;
    }

    private static bool CapturedSvgChanged(byte[] actual, byte[] previous, int width, int height)
    {
        // The browser comparison's right-hand viewport starts below the controls and tab header.
        for (var row = 700; row < height - 8; row++)
        {
            var start = (row * width + 700) * 3;
            var length = (width - 724) * 3;
            if (!actual.AsSpan(start, length).SequenceEqual(previous.AsSpan(start, length)))
                return true;
        }
        return false;
    }
}
