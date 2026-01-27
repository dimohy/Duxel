namespace Dux.Core;

public interface IUiClipboard
{
    string GetText();
    void SetText(string text);
}
