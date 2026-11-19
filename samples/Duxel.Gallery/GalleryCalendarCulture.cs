using Duxel.Core;
using System.Globalization;

namespace Duxel.Gallery;

internal static class GalleryCalendarCulture
{
    internal static readonly UiPickerLabels Labels = new("날짜 선택", "시각 선택", "시", "분", "초", "오전·오후", "확인",
        "올바른 날짜를 입력하세요.", "올바른 시각을 입력하세요.", "값을 적용할 수 없습니다.");
    internal static readonly UiCalendarLabels CalendarLabels = new("이전 달", "다음 달", "오늘", "종료일을 선택하세요", "연도 선택", "월 선택");
    internal static UiCultureCalendar Create(string key, bool latin = false)
    {
        var digits = latin ? UiCalendarDayNumbers.Latin : UiCalendarDayNumbers.Culture;
        return key switch
        {
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
    internal static string SystemSource(string key) => key == "system" ? "new UiCultureCalendar()"
        : "new UiCultureCalendar(System.Globalization.CultureInfo.GetCultureInfo(" + GallerySample.String(key) + ")"
            + (key == "he-IL" ? ", new System.Globalization.HebrewCalendar()" : key == "ja-JP" ? ", new System.Globalization.JapaneseCalendar()" : string.Empty) + ")";
}
