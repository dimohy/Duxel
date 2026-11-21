using System.Globalization;
using Duxel.Core;

namespace Duxel.App;

internal static class FileChooserLocalization
{
    private static readonly UiLocalization Catalog = new(Translations());

    internal static string GetText(string source, CultureInfo culture) => Catalog.GetText(source, culture);

    private static IEnumerable<(string CultureName, string Source, string Text)> Translations()
    {
        (string Source, string Korean, string Japanese, string SimplifiedChinese)[] entries =
        [
            ("Save", "저장", "保存", "保存"),
            ("Select Folder", "폴더 선택", "フォルダーを選択", "选择文件夹"),
            ("Open", "열기", "開く", "打开"),
            ("Files and folders", "파일 및 폴더 목록", "ファイルとフォルダー", "文件和文件夹"),
            ("Current folder", "현재 폴더", "現在のフォルダー", "当前文件夹"),
            ("Go", "이동", "移動", "转到"),
            ("Up", "상위 폴더", "親フォルダーへ", "上一级"),
            ("File name", "파일 이름", "ファイル名", "文件名"),
            ("File type", "파일 형식", "ファイルの種類", "文件类型"),
            ("Show options", "옵션 표시", "オプションを表示", "显示选项"),
            ("Cancel", "취소", "キャンセル", "取消"),
            ("No items to show.", "표시할 항목이 없습니다.", "表示する項目がありません。", "没有可显示的项目。"),
            ("Could not open the folder.", "폴더를 열 수 없습니다.", "フォルダーを開けませんでした。", "无法打开文件夹。"),
            ("Reading the folder.", "폴더를 읽는 중입니다.", "フォルダーを読み込んでいます。", "正在读取文件夹。"),
            ("A selected folder no longer exists.", "선택한 폴더를 찾을 수 없습니다.", "選択したフォルダーが見つかりません。", "找不到所选文件夹。"),
            ("Select the files to open.", "열 파일을 선택하세요.", "開くファイルを選択してください。", "请选择要打开的文件。"),
            ("Enter a file name.", "파일 이름을 입력하세요.", "ファイル名を入力してください。", "请输入文件名。"),
            ("This file path cannot be selected.", "이 경로의 파일을 선택할 수 없습니다.", "このファイルパスは選択できません。", "无法选择此文件路径。"),
            ("Replace", "덮어쓰기", "置き換え", "替换"),
            ("This file already exists. Select Replace to use this path.", "같은 이름의 파일이 있습니다. 덮어쓸 경로로 선택하시겠습니까?", "同じ名前のファイルがすでにあります。このパスを使うには「置き換え」を選んでください。", "同名文件已存在。请选择“替换”以使用此路径。"),
            ("{0:N0} selected", "{0:N0}개 선택", "{0:N0} 件選択", "已选择 {0:N0} 项"),
            ("Open folder {0}", "폴더 열기 {0}", "フォルダーを開く {0}", "打开文件夹 {0}")
        ];
        foreach (var (source, korean, japanese, chinese) in entries)
        {
            yield return ("ko", source, korean);
            yield return ("ja", source, japanese);
            yield return ("zh-Hans", source, chinese);
        }
    }
}
