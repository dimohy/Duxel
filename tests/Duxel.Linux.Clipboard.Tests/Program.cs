using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Platform.Linux;

if (!OperatingSystem.IsLinux() || args.Length != 1)
    throw new ArgumentException("Run on Linux with a fresh result directory.");
var output = Path.GetFullPath(args[0]);
if (Directory.Exists(output))
    throw new IOException("Result directory already exists: " + output);
Directory.CreateDirectory(output);
var selection = "_DUXEL_CLIPBOARD_FIXTURE_" + Guid.NewGuid().ToString("N");
var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException(name);
    checks.Add(name);
    Console.WriteLine("PASS " + name);
}
var options = new LinuxX11ClipboardOptions { Selection = selection, ChunkBytes = 1024, TransferTimeout = TimeSpan.FromSeconds(3) };
using var first = new LinuxX11Clipboard(options);
using var second = new LinuxX11Clipboard(options);
Check(first.GetText() == "", "A fresh private selection is empty without touching the user's CLIPBOARD");
foreach (var text in new[] { "", "ASCII café", "정세일 한글 😀", "line one\n둘째 줄\0tail", string.Concat(Enumerable.Repeat("한글 😀 abc\n", 15000)) })
{
    first.SetText(text);
    Check(first.GetText() == text, "The owner's receiver reads exact Unicode text, bytes=" + System.Text.Encoding.UTF8.GetByteCount(text));
    Check(second.GetText() == text, "A separate connection reads exact text/INCR, bytes=" + System.Text.Encoding.UTF8.GetByteCount(text));
}
second.SetText("새 소유자");
Check(first.GetText() == "새 소유자", "Ownership transfer does not return stale local data");
second.Dispose();
Check(first.GetText() == "", "Disposing the owner releases its private selection");
var rejected = false;
try
{
    second.SetText("disposed");
}
catch (ObjectDisposedException) { rejected = true; }
Check(rejected, "Disposed writers reject new requests");
first.SetText("다시 사용");
Check(first.GetText() == "다시 사용", "The remaining service stays usable after another owner retires");
var unicode = "독립 Xlib café 😀";
first.SetText(unicode);
var independent = XlibSelectionPeer.Read(selection, "UTF8_STRING");
Check(independent.Accepted && System.Text.Encoding.UTF8.GetString(independent.Bytes) == unicode, "Independent Xlib reads production Unicode");
Check(XlibSelectionPeer.Targets(selection).Contains(XlibSelectionPeer.Atom("UTF8_STRING")), "Independent Xlib decodes LP64 TARGETS");
var timestamp = XlibSelectionPeer.Read(selection, "TIMESTAMP");
Check(timestamp.Accepted && timestamp.Format == 32 && timestamp.Words.Length == 1 && timestamp.Words[0] != 0, "Independent Xlib reads the acquisition timestamp");
Check(!XlibSelectionPeer.Read(selection, "_DUXEL_UNSUPPORTED").Accepted, "Unknown conversion is explicitly refused");
Check(!XlibSelectionPeer.Read(selection, "STRING").Accepted, "Unicode is not silently replaced by Latin1");
Check(XlibSelectionPeer.Multiple(selection, unicode), "MULTIPLE serves valid pairs and refuses unknown, recursive or aliased destinations");
Check(XlibSelectionPeer.Multiple(selection, unicode, oversized: true), "Oversized foreign MULTIPLE properties are refused");
Check(first.GetText() == unicode, "Malformed foreign requests do not kill the owner service");
first.SetText("café");
Check(System.Text.Encoding.Latin1.GetString(XlibSelectionPeer.Read(selection, "STRING").Bytes) == "café", "Representable Latin1 is served exactly");
var large = string.Concat(Enumerable.Repeat("한글 😀 abc\n", 15000));
first.SetText(large);
independent = XlibSelectionPeer.Read(selection, "UTF8_STRING");
Check(independent.Incremental && System.Text.Encoding.UTF8.GetString(independent.Bytes) == large, "Independent Xlib receives complete production INCR");
Check(XlibSelectionPeer.Read(selection, "UTF8_STRING", abandonIncremental: true).Incremental, "An independent requestor can disappear during INCR");
Check(first.GetText() == large, "The production owner survives a disappearing INCR requestor");
foreach (var text in new[] { unicode, large })
{
    using var peer = new XlibSelectionPeer(selection, text);
    Check(first.GetText() == text, "Production receives independent Xlib text with split UTF8 chunks, bytes=" + System.Text.Encoding.UTF8.GetByteCount(text));
}
using (var silent = new XlibSelectionPeer(selection, "", silent: true))
{
    using var bounded = new LinuxX11Clipboard(options with
    {
        TransferTimeout = TimeSpan.FromMilliseconds(200)
    });
    var timedOut = false;
    try
    {
        bounded.GetText();
    }
    catch (TimeoutException) { timedOut = true; }
    Check(timedOut, "A silent foreign owner fails with an explicit timeout");
    silent.Requested.Reset();
    using var cancelled = new LinuxX11Clipboard(options with
    {
        TransferTimeout = TimeSpan.FromSeconds(5)
    });
    var pending = Task.Run(() =>
    {
        try
        {
            cancelled.GetText();
            return false;
        }
        catch (ObjectDisposedException) { return true; }
    });
    Check(silent.Requested.Wait(TimeSpan.FromSeconds(2)), "The cancellation fixture reaches a real foreign-owner wait");
    var watch = System.Diagnostics.Stopwatch.StartNew();
    cancelled.Dispose();
    Check(pending.GetAwaiter().GetResult() && watch.Elapsed < TimeSpan.FromSeconds(1), "Dispose promptly cancels and joins an active selection read");
}
using (var limited = new LinuxX11Clipboard(options with
{
    MaximumBytes = 4
}))
{
    var oversized = false;
    try
    {
        limited.SetText("한글");
    }
    catch (ArgumentException) { oversized = true; }
    Check(oversized, "UTF8 byte limits reject oversized text before ownership changes");
    limited.SetText("é");
    Check(limited.GetText() == "é", "Small text limits do not incorrectly reject TARGETS metadata");
}
var malformed = false;
try
{
    first.SetText("\ud800");
}
catch (System.Text.EncoderFallbackException) { malformed = true; }
Check(malformed, "Malformed UTF16 is rejected instead of substituted");
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
{
    framework = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    selection,
    userClipboardRead = false,
    userClipboardWritten = false,
    passed = checks.Count,
    checks,
    qualification = "Independent Xlib transport and lifetime qualification; app integration and full platform acceptance remain"
}, new JsonSerializerOptions { WriteIndented = true }));
