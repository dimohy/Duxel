using System.Globalization;

namespace Duxel.Core;

public enum UiFileDialogKind
{
    OpenFile,
    SaveFile,
    SelectFolder
}

/// <summary>A named group of shell glob patterns, such as *.png and *.jpg.</summary>
public sealed class UiFileDialogFilter
{
    public string Name { get; }
    public IReadOnlyList<string> Patterns { get; }

    public UiFileDialogFilter(string name, params string[] patterns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(patterns);
        if (name.Contains('\0') || patterns.Length == 0
            || patterns.Any(pattern => string.IsNullOrWhiteSpace(pattern) || pattern.IndexOfAny(['\0', ';', '/', '\\']) >= 0))
            throw new ArgumentException("File filters need a name and one or more file name patterns.", nameof(patterns));
        Name = name;
        Patterns = Array.AsReadOnly((string[])patterns.Clone());
    }
}

/// <summary>Options for a native, window-owned file or folder dialog.</summary>
public sealed class UiFileDialogRequest
{
    public UiFileDialogKind Kind { get; }
    public string? Title { get; }
    public string? InitialDirectory { get; }
    public string? SuggestedFileName { get; }
    public string? DefaultExtension { get; }
    public bool AllowMultiple { get; }
    public IReadOnlyList<UiFileDialogFilter> Filters { get; }

    /// <summary>Locale for the Duxel file chooser when the system dialog is unavailable.</summary>
    /// <remarks>Defaults to the system UI culture. Native dialog controls follow the desktop language.</remarks>
    public CultureInfo? Culture { get; init; }

    public UiFileDialogRequest(UiFileDialogKind kind, string? title = null, string? initialDirectory = null,
        string? suggestedFileName = null, string? defaultExtension = null, bool allowMultiple = false,
        IReadOnlyList<UiFileDialogFilter>? filters = null)
    {
        if (kind is < UiFileDialogKind.OpenFile or > UiFileDialogKind.SelectFolder)
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (title is not null && (string.IsNullOrWhiteSpace(title) || title.Any(char.IsControl)))
            throw new ArgumentException("The title must be on a single line.", nameof(title));
        if (initialDirectory is not null && (!Path.IsPathFullyQualified(initialDirectory) || initialDirectory.Contains('\0')))
            throw new ArgumentException("The initial directory must be an absolute path.", nameof(initialDirectory));
        if (suggestedFileName is not null && (kind != UiFileDialogKind.SaveFile
            || string.IsNullOrWhiteSpace(suggestedFileName) || suggestedFileName.IndexOfAny(['\0', '/', '\\', ':']) >= 0
            || suggestedFileName is "." or ".."))
            throw new ArgumentException("A suggested file name is available for save dialogs and cannot contain a path.", nameof(suggestedFileName));
        if (defaultExtension is not null && (kind != UiFileDialogKind.SaveFile || string.IsNullOrWhiteSpace(defaultExtension)
            || defaultExtension.Any(character => char.IsWhiteSpace(character) || character is '\0' or '.' or '*' or '?' or '/' or '\\' or ':' or ';')))
            throw new ArgumentException("Provide a save extension such as txt without the leading dot.", nameof(defaultExtension));
        if (allowMultiple && kind == UiFileDialogKind.SaveFile)
            throw new ArgumentException("A save dialog returns one destination.", nameof(allowMultiple));
        var filterArray = filters?.ToArray() ?? [];
        if (filterArray.Any(filter => filter is null) || kind == UiFileDialogKind.SelectFolder && filterArray.Length != 0)
            throw new ArgumentException("Named file filters are available for file dialogs.", nameof(filters));
        Kind = kind;
        Title = title;
        InitialDirectory = initialDirectory;
        SuggestedFileName = suggestedFileName;
        DefaultExtension = defaultExtension;
        AllowMultiple = allowMultiple;
        Filters = Array.AsReadOnly(filterArray);
    }
}

/// <summary>Called by the session on the thread that owns the native window. An empty list means user cancellation.</summary>
public interface IPlatformFileDialogs
{
    IReadOnlyList<string> ShowFileDialog(UiFileDialogRequest request);
}

/// <summary>The platform cannot provide the requested native picker. A desktop host may supply its own picker.</summary>
public sealed class UiNativeFileDialogUnavailableException(string message) : NotSupportedException(message);
