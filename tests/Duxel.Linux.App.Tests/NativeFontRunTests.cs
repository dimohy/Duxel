using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Linux;

internal static partial class NativeFontRunTests
{
    internal static string Run(string primary, string secondary, Action<bool, string> check)
    {
        PlatformFontMetadata retained;
        using (var backend = new LinuxPlatformTextBackend())
        {
            IPlatformTextFontRunBackend capability = backend;
            const string source = "정A😀e\u0301\n\tB";
            var request = new PlatformTextMeasureRequest(primary, secondary, source, 18);
            var runs = capability.GetFontRuns(request, "en");
            check(runs.Count == 3 && runs[0].StartUtf16 == 0 && runs[0].EndUtf16 == 1
                && runs[1].StartUtf16 == 1 && runs[1].EndUtf16 == 6 && runs[2].StartUtf16 == 7 && runs[2].EndUtf16 == 9,
                "Actual shared font selection preserves exact original UTF16 ranges across Korean, emoji, combining text, newline and tab");
            check(runs[0].Font is { FamilyName: "Malgun Gothic", SubfamilyName: "Regular", Weight: 400, Italic: false, SimulatedOblique: false },
                "Selected Korean face metadata matches independent OpenType name and OS/2 values");
            check(runs[1].Font is { FamilyName: "DejaVu Sans", SubfamilyName: "Book", Weight: 400, Italic: false, NameLanguage: "en" },
                "Selected primary face metadata uses its actual family and weight rather than the supplied filename");
            check(ReferenceEquals(runs[1].Font, runs[2].Font), "Separate source intervals reuse the same selected face metadata");
            check(backend.TryLayout(request, out var layout) && layout.Advance > 0 && layout.Height > 0,
                "The exact source selection fixture also passes actual shaping and layout");
            var again = capability.GetFontRuns(request, "EN");
            check(ReferenceEquals(again[0].Font, runs[0].Font) && ReferenceEquals(again[1].Font, runs[1].Font),
                "Unchanged selected fonts and normalized requested language reuse immutable metadata");
            retained = runs[1].Font;
            var korean = capability.GetFontRuns(new(secondary, null, "정", 18), "ko");
            check(korean[0].Font.FamilyName == "맑은 고딕" && korean[0].Font.NameLanguage == "ko",
                "An explicitly requested installed Korean font name is read in Korean");
            var absent = capability.GetFontRuns(new(secondary, null, "정", 18), "zz");
            check(absent[0].Font.FamilyName is null && absent[0].Font.SubfamilyName is null && absent[0].Font.Weight == 400,
                "Unavailable name language remains absent without silently substituting English or discarding independent weight metadata");
            check(runs[0].Font.FamilyName == "Malgun Gothic", "Changing name language does not mutate prior font snapshots");
            var oblique = backend.GetFontVariantResource(primary, PlatformFontSimulation.Oblique);
            var slanted = capability.GetFontRuns(new(oblique, null, "A", 18), "en");
            check(slanted[0].Font is { SimulatedOblique: true, Italic: false, FamilyName: "DejaVu Sans", Weight: 400 },
                "Actual synthetic oblique stays distinct from the source face's native italic flag");
            var separated = capability.GetFontRuns(new(primary, secondary, "A\r\n정\tB\rZ", 18), "en");
            check(separated.Select(r => (r.StartUtf16, r.EndUtf16)).SequenceEqual(new[] { (0, 1), (3, 5), (5, 6), (7, 8) })
                && separated[1].Font.FamilyName == "Malgun Gothic" && separated[2].Font.FamilyName == "DejaVu Sans",
                "CRLF and bare CR retain exact source gaps while the expanded tab keeps the preceding Korean face and its one source position");
            check(capability.GetFontRuns(new(primary, secondary, "\r\n", 18), "en").Count == 0
                && capability.GetFontRuns(new(primary, secondary, "", 18), "en").Count == 0,
                "Empty text and line separators do not invent painted font runs");
            foreach (var invalid in new Action[]
            {
                () => capability.GetFontRuns(new(primary, null, "정", 18), "en"),
                () => capability.GetFontRuns(new(primary, secondary, "\U0010ffff", 18), "en"),
                () => capability.GetFontRuns(new(primary, secondary, "\ud800", 18), "en"),
                () => capability.GetFontRuns(request, ""), () => capability.GetFontRuns(request, "en invalid")
            })
            {
                var rejected = false;
                try
                {
                    invalid();
                }
                catch (Exception error) when (error is NotSupportedException or ArgumentException) { rejected = true; }
                check(rejected, "Invalid source coverage, malformed UTF16 and invalid requested languages fail explicitly");
            }
        }
        check(retained.FamilyName == "DejaVu Sans" && retained.Weight == 400,
            "Immutable metadata remains readable after all native font resources are disposed");
        return Marshal.PtrToStringUTF8(hb_version_string()) ?? throw new InvalidOperationException("Missing HarfBuzz version.");
    }
    [LibraryImport("libharfbuzz.so.0")] private static partial nint hb_version_string();
}
