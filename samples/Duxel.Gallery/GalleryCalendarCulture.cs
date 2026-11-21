using Duxel.Core;
using System.Globalization;

namespace Duxel.Gallery;

internal static class GalleryCalendarCulture
{
    internal static UiSelectOption GalleryOption => new("gallery", GalleryText.Get("Gallery language"));
    internal static UiSelectOption SystemOption => new("system", GalleryText.Get("System settings"));

    internal static UiPickerLabels Labels => new(GalleryText.Get("Choose date"), GalleryText.Get("Choose time"),
        GalleryText.Get("Hour"), GalleryText.Get("Minute"), GalleryText.Get("Seconds"), GalleryText.Get("AM/PM"),
        GalleryText.Get("Confirm"), GalleryText.Get("Enter a valid date."), GalleryText.Get("Enter a valid time."),
        GalleryText.Get("The value was not accepted."));

    internal static UiCalendarLabels CalendarLabels => new(GalleryText.Get("Previous month"), GalleryText.Get("Next month"),
        GalleryText.Get("Today"), GalleryText.Get("Choose the end date"), GalleryText.Get("Choose year"),
        GalleryText.Get("Choose month"));

    internal static string CalendarLabelsSource(UiCalendarLabels labels) =>
        "new UiCalendarLabels(" + string.Join(", ", GallerySample.String(labels.PreviousMonth),
            GallerySample.String(labels.NextMonth), GallerySample.String(labels.Today),
            GallerySample.String(labels.ChooseRangeEnd), GallerySample.String(labels.ChooseYear),
            GallerySample.String(labels.ChooseMonth)) + ")";

    internal static string PickerLabelsSource(UiPickerLabels labels) =>
        $"new UiPickerLabels(ChooseDate : {GallerySample.String(labels.ChooseDate)}, ChooseTime : {GallerySample.String(labels.ChooseTime)},\n"
        + $"    Hour : {GallerySample.String(labels.Hour)}, Minute : {GallerySample.String(labels.Minute)}, Second : {GallerySample.String(labels.Second)},\n"
        + $"    Period : {GallerySample.String(labels.Period)}, Done : {GallerySample.String(labels.Done)},\n"
        + $"    InvalidDate : {GallerySample.String(labels.InvalidDate)}, InvalidTime : {GallerySample.String(labels.InvalidTime)},\n"
        + $"    Rejected : {GallerySample.String(labels.Rejected)})";

    internal static UiCultureCalendar Create(string key, bool latin = false)
    {
        var digits = latin ? UiCalendarDayNumbers.Latin : UiCalendarDayNumbers.Culture;
        return key switch
        {
            "gallery" => new(CultureInfo.GetCultureInfo(GalleryText.RegionalCultureName), dayNumbers : digits),
            "system" => new(dayNumbers : digits),
            "ja-JP" => new(CultureInfo.GetCultureInfo(key), new JapaneseCalendar(), digits),
            "he-IL" => new(CultureInfo.GetCultureInfo(key), new HebrewCalendar(), digits),
            "ko-KR" or "en-US" or "en-GB" or "ar-SA" or "fa-IR" or "th-TH" => new(CultureInfo.GetCultureInfo(key), dayNumbers : digits),
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
    }
    internal static UiTypographyFonts ThaiFonts(UiTypographyFonts? inherited)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSansThai", "NotoSansThai.ttf");
        if (!File.Exists(path)) throw new FileNotFoundException("The Thai calendar example requires its bundled Noto Sans Thai font.", path);
        return new(path, path, path, inherited?.SecondaryRegular, inherited?.SecondarySemibold, inherited?.SecondaryBold) { Monospace = inherited?.Monospace };
    }
    internal static string SystemSource(UiCultureCalendar system, string key, bool includeDayNumbers = false)
    {
        var arguments = new List<string>();
        if (key != "system")
        {
            arguments.Add("System.Globalization.CultureInfo.GetCultureInfo(" + GallerySample.String(system.Culture.Name) + ")");
            if (key == "he-IL") arguments.Add("new System.Globalization.HebrewCalendar()");
            else if (key == "ja-JP") arguments.Add("new System.Globalization.JapaneseCalendar()");
        }

        if (includeDayNumbers) arguments.Add("dayNumbers : UiCalendarDayNumbers." + system.DayNumbers);
        return "new UiCultureCalendar(" + string.Join(", ", arguments) + ")";
    }
}
