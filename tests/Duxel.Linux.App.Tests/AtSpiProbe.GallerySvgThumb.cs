using System.Diagnostics;
using System.Globalization;

internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGallerySvgThumb(nint window, Func<string, nint, int?, NativeObject> required,
        List<string> checks, string output, nuint windowId, bool layerMix)
    {
        const int width = 1280, height = 900, moveCount = 64, distance = 8;
        var directory = Path.GetDirectoryName(Path.GetFullPath(output))!;
        var renderLog = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_OUT");
        using var peer = new NativePeer(windowId);
        Wait(() => peer.CanCapture(width, height, out _), "the mapped Gallery window");
        peer.BeginNativeObservation();

        (string Tab, string File)[] tabs = layerMix ? [("레이어·테두리", "layer")] : [("테두리 범위", "stroke"), ("호·곡선 비교", "arc")];
        foreach (var (tabName, fileName) in tabs)
        {
            using var tab = required(tabName, window, 37);
            Reveal(tab);
            InvokeNamed(tab, "select");
            using var pane = required("observer-svg-page", window, null);
            var bounds = ComponentBox(pane.Value);
            var x = bounds.X + bounds.Width - 5;
            var bodyX = bounds.X + 100;
            var bodyY = bounds.Y + bounds.Height * 2 / 3;
            peer.Pointer(6, bodyX, bodyY);
            for (var i = 0; i < 8; i++)
            {
                using var code = Find(window, element => Name(element) == "C# 예제 소스" && ComponentBox(element).Height >= 500);
                if (code is not null)
                    break;
                peer.Pointer(4, bodyX, bodyY, 5);
                Thread.Sleep(100);
            }
            using var visibleCode = Find(window, element => Name(element) == "C# 예제 소스" && ComponentBox(element).Height >= 500)
                ?? throw new InvalidOperationException("The SVG viewport does not show enough code for a thumb replay.");

            // Let the navigation and outer scrollbar finish their 400 ms hold and 160 ms fade.
            // Those controls can still be changing after AT-SPI reveals the next tab.
            Thread.Sleep(650);
            peer.Pointer(6, x, bounds.Y + bounds.Height / 2);
            Thread.Sleep(200);
            var original = Capture(fileName + "-before");
            var thumbY = FindThumb(original, x, bounds.Y + 4, bounds.Y + bounds.Height - 4);
            Observe("before-press");
            peer.Pointer(6, x, thumbY);
            peer.Pointer(4, x, thumbY, detail : 1);
            Thread.Sleep(150);
            _ = Capture(fileName + "-pressed");
            Observe("after-press");
            Console.WriteLine($"{fileName} thumb=({x},{thumbY}) viewport={bounds.Width}x{bounds.Height}");

            Drag(distance, "down");
            Drag(0, "return");
            peer.Pointer(5, x, thumbY, detail : 1, state : 256);
            checks.Add($"{tabName}: native button capture and two bursts of {moveCount} moves changed visible code pixels before release.");
            if (layerMix)
                SelectCodeAfterWheel();

            void SelectCodeAfterWheel()
            {
                var point = ComponentBox(visibleCode.Value);
                var anchorX = point.X + 105;
                var anchorY = point.Y + 96;
                peer.Pointer(6, anchorX, anchorY);
                var wheelBaseline = Capture("layer-wheel-before");
                Wheel(5, "layer-wheel-down", wheelBaseline, expectOriginal : false);
                Wheel(4, "layer-wheel-return", wheelBaseline, expectOriginal : true);
                checks.Add("X11 wheel input moved the source and returned to the same painted code.");

                using var text = new NativeObject(atspi_accessible_get_text_iface(visibleCode.Value));
                var source = ReadText(text.Value, 0, -1);
                peer.Pointer(6, anchorX, anchorY);
                peer.Pointer(4, anchorX, anchorY, detail : 1);
                Wait(() => TextSelectionCount(text.Value) == 0, "the initial code caret");
                Mark("selection-start");
                for (var i = 0; i < moveCount; i++)
                    peer.Pointer(6, anchorX + i % 4, point.Y - 9 + i % 7, state : 256, flush : i == moveCount - 1);
                Wait(() => TextSelectionCount(text.Value) == 1, "the actual code selection");
                var range = ReadSelection(text.Value);
                if (range.End <= range.Start || ReadText(text.Value, 0, -1) != source)
                    throw new InvalidOperationException("The pointer drag must select code without editing it.");
                _ = Capture("layer-selected");
                Mark("selection-capture");
                Observe("selection");
                peer.Pointer(5, anchorX, point.Y - 9, detail : 1, state : 256);
                Console.WriteLine($"layer selected-range: {range.Start} {range.End}");
                checks.Add("A held X11 pointer moving outside the code clip selected an AT-SPI text range without editing source.");

                peer.Pointer(6, anchorX, anchorY);
                var selectedBaseline = Capture("layer-focused-before");
                Wheel(5, "layer-focused-down", selectedBaseline, expectOriginal : false);
                Wheel(4, "layer-focused-return", selectedBaseline, expectOriginal : true);
                var retained = ReadSelection(text.Value);
                if (TextSelectionCount(text.Value) != 1 || retained.Start != range.Start || retained.End != range.End)
                    throw new InvalidOperationException("The wheel must preserve the released code selection.");
                checks.Add("X11 wheel input moved the document after code selection and preserved the selected range.");

                void Wheel(uint button, string phase, byte[] baseline, bool expectOriginal)
                {
                    for (var i = 0; i < 2; i++)
                        peer.Pointer(4, anchorX, anchorY, button);
                    Wait(() => SameCodePixels(Capture(phase), baseline) == expectOriginal, phase);
                }
            }

            bool SameCodePixels(byte[] pixels, byte[] baseline)
            {
                for (var row = bodyY - 84; row < bodyY + 84; row++)
                {
                    var startIndex = (row * width + bodyX) * 3;
                    if (!pixels.AsSpan(startIndex, 360).SequenceEqual(baseline.AsSpan(startIndex, 360)))
                        return false;
                }
                return true;
            }

            void Drag(int target, string direction)
            {
                Mark(direction + "-start");
                var start = Stopwatch.GetTimestamp();
                for (var index = 0; index < moveCount; index++)
                {
                    int offset = target == 0 ? distance - 1 - index % 7 : 1 + index % 7;
                    if (index == moveCount - 1)
                        offset = target;
                    peer.Pointer(6, x, thumbY + offset, state : 256, flush : index == moveCount - 1);
                }
                try
                {
                    Wait(() =>
                    {
                        return SameCodePixels(Capture(fileName + "-" + direction), original) == (target == 0);
                    }, fileName + " code pixels after " + direction + " thumb drag");
                }
                finally
                {
                    Observe(direction);
                }
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{fileName} {direction} matching X11 capture at {Stopwatch.GetElapsedTime(start).TotalMilliseconds:F2} ms"));
                Mark(direction + "-capture");
            }

            void Mark(string phase)
            {
                if (renderLog is not null)
                    Console.WriteLine($"{fileName}-{phase}: {new FileInfo(renderLog).Length}");
            }

            void Observe(string phase)
            {
                var events = peer.DrainNativeObservation();
                var motion = events.Count(type => type == 6);
                var otherEvents = string.Join(',', events.Where(type => type != 6));
                Console.WriteLine($"{fileName} {phase} focus={peer.HasNativeFocus()} motion={motion} other={otherEvents}");
            }
        }

        byte[] Capture(string name)
        {
            byte[] pixels = [];
            peer.Capture(Path.Combine(directory, name + ".ppm"), width, height, captured => pixels = captured);
            return pixels;
        }

        static int FindThumb(byte[] pixels, int x, int top, int end)
        {
            var brightest = 0;
            for (var row = top; row < end; row++)
                brightest = Math.Max(brightest, pixels[(row * width + x) * 3]);
            var longest = 0;
            var position = 0;
            var run = 0;
            for (var row = top; row < end; row++)
            {
                run = pixels[(row * width + x) * 3] >= brightest - 2 ? run + 1 : 0;
                if (run > longest)
                    (longest, position) = (run, row - run / 2);
            }
            if (longest < 8 || longest > (end - top) / 2)
                throw new InvalidOperationException($"The vertical thumb could not be identified at x={x}, length={longest}, intensity={brightest}.");
            return position;
        }
    }
}
