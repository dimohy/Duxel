using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsPlatformBackend : IPlatformFileDialogs
{
    IReadOnlyList<string> IPlatformFileDialogs.ShowFileDialog(UiFileDialogRequest request)
    {
        var owner = WindowHandle;
        var result = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new Thread(() =>
        {
            try { result.SetResult(WindowsFileDialogs.Show(owner, request)); }
            catch (Exception error) { result.SetException(error); }
        }) { IsBackground = true, Name = "DuxelFileDialog" };
        dialog.SetApartmentState(ApartmentState.STA);
        dialog.Start();
        // Show owns its modal message loop. The window's thread must continue dispatching cross-thread messages.
        while (!result.Task.IsCompleted)
            WaitEvents(16);
        dialog.Join();
        return result.Task.GetAwaiter().GetResult();
    }
}

/// <summary>Windows Common Item Dialogs. Call from an STA and keep the parent window's event loop running.</summary>
[SupportedOSPlatform("windows")]
public static unsafe partial class WindowsFileDialogs
{
    private const int Cancelled = unchecked((int)0x800704C7);
    private const uint OverwritePrompt = 0x00000002;
    private const uint NoChangeDirectory = 0x00000008;
    private const uint PickFolders = 0x00000020;
    private const uint ForceFileSystem = 0x00000040;
    private const uint AllowMultiple = 0x00000200;
    private const uint PathMustExist = 0x00000800;
    private const uint FileMustExist = 0x00001000;
    private const uint FileSystemPath = 0x80058000;

    public static IReadOnlyList<string> Show(nint owner, UiFileDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Ole32.CoInitializeEx(0, Ole32.CoinitApartmentThreaded).ThrowIfFailed("Initialize the file dialog STA.");
        nint dialog = 0;
        try
        {
            var save = request.Kind == UiFileDialogKind.SaveFile;
            var clsid = save ? new Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B") : new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
            var iid = save ? new Guid("84BCCD23-5FDE-4CDB-AEA4-AF64B83D78AB") : new Guid("D57C7288-D4AD-4768-BE02-9D969532D960");
            Ole32.CoCreateInstance(ref clsid, 0, Ole32.ClsctxInprocServer, ref iid, out dialog)
                .ThrowIfFailed("Create the Windows file dialog.");
            var table = *(FileDialogVtable**)dialog;
            uint options = 0;
            table->GetOptions(dialog, &options).ThrowIfFailed("Read native file dialog options.");
            options |= NoChangeDirectory | ForceFileSystem | PathMustExist;
            if (save)
                options |= OverwritePrompt;
            else if (request.Kind == UiFileDialogKind.SelectFolder)
                options = (options & ~FileMustExist) | PickFolders;
            else
                options |= FileMustExist;
            if (request.AllowMultiple)
                options |= AllowMultiple;
            table->SetOptions(dialog, options).ThrowIfFailed("Configure native file dialog options.");
            SetText(dialog, table->SetTitle, request.Title, "Set the file dialog title.");
            SetText(dialog, table->SetFileName, request.SuggestedFileName, "Set the suggested file name.");
            if (request.Filters.Count > 0)
                SetFilters(dialog, table, request.Filters);
            SetText(dialog, table->SetDefaultExtension, request.DefaultExtension, "Set the default file extension.");
            if (request.InitialDirectory is { } initialDirectory)
                SetDefaultFolder(dialog, table, initialDirectory);

            var result = table->Show(dialog, owner);
            if (result == Cancelled)
                return Array.Empty<string>();
            result.ThrowIfFailed("Show the Windows file dialog.");
            return request.AllowMultiple ? ReadMultiple(dialog) : ReadSingle(dialog, table);
        }
        finally
        {
            Release(dialog);
            Ole32.CoUninitialize();
        }
    }

    private static void SetText(nint dialog, delegate* unmanaged[Stdcall]<nint, char*, int> method, string? text, string operation)
    {
        if (text is not null)
            fixed (char* characters = text)
                method(dialog, characters).ThrowIfFailed(operation);
    }

    private static void SetDefaultFolder(nint dialog, FileDialogVtable* table, string directory)
    {
        var iid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        nint item = 0;
        try
        {
            SHCreateItemFromParsingName(directory, 0, &iid, &item).ThrowIfFailed("Resolve the initial file dialog folder.");
            table->SetDefaultFolder(dialog, item).ThrowIfFailed("Set the initial file dialog folder.");
        }
        finally { Release(item); }
    }

    private static void SetFilters(nint dialog, FileDialogVtable* table, IReadOnlyList<UiFileDialogFilter> filters)
    {
        var specs = new FilterSpec[filters.Count];
        try
        {
            for (var i = 0; i < specs.Length; i++)
            {
                specs[i].Name = Marshal.StringToCoTaskMemUni(filters[i].Name);
                specs[i].Pattern = Marshal.StringToCoTaskMemUni(string.Join(';', filters[i].Patterns));
            }
            fixed (FilterSpec* spec = specs)
                table->SetFileTypes(dialog, (uint)specs.Length, spec).ThrowIfFailed("Set the file types.");
        }
        finally
        {
            foreach (var spec in specs)
            {
                Marshal.FreeCoTaskMem(spec.Name);
                Marshal.FreeCoTaskMem(spec.Pattern);
            }
        }
    }

    private static IReadOnlyList<string> ReadSingle(nint dialog, FileDialogVtable* table)
    {
        nint item = 0;
        try
        {
            table->GetResult(dialog, &item).ThrowIfFailed("Read the selected file or folder.");
            return Array.AsReadOnly(new[] { ReadPath(item) });
        }
        finally { Release(item); }
    }

    private static IReadOnlyList<string> ReadMultiple(nint dialog)
    {
        nint items = 0;
        try
        {
            var table = *(OpenDialogVtable**)dialog;
            table->GetResults(dialog, &items).ThrowIfFailed("Read the selected file dialog items.");
            var array = *(ShellItemArrayVtable**)items;
            uint count = 0;
            array->GetCount(items, &count).ThrowIfFailed("Read the number of selected items.");
            var paths = new string[checked((int)count)];
            for (var i = 0; i < paths.Length; i++)
            {
                nint item = 0;
                try
                {
                    array->GetItemAt(items, (uint)i, &item).ThrowIfFailed("Read a selected file or folder.");
                    paths[i] = ReadPath(item);
                }
                finally { Release(item); }
            }
            return Array.AsReadOnly(paths);
        }
        finally { Release(items); }
    }

    private static string ReadPath(nint item)
    {
        var table = *(ShellItemVtable**)item;
        nint path = 0;
        try
        {
            table->GetDisplayName(item, FileSystemPath, &path).ThrowIfFailed("Read the selected file-system path.");
            return Marshal.PtrToStringUni(path) ?? throw new IOException("The Windows file dialog returned a null path.");
        }
        finally { Marshal.FreeCoTaskMem(path); }
    }

    private static void Release(nint value)
    {
        if (value != 0)
            (*(UnknownVtable**)value)->Release(value);
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHCreateItemFromParsingName(string path, nint bindContext, Guid* iid, nint* item);

    [StructLayout(LayoutKind.Sequential)]
    private struct FilterSpec { internal nint Name, Pattern; }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnknownVtable
    {
        internal nint QueryInterface, AddRef;
        internal delegate* unmanaged[Stdcall]<nint, uint> Release;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDialogVtable
    {
        internal UnknownVtable Unknown;
        internal delegate* unmanaged[Stdcall]<nint, nint, int> Show;
        internal delegate* unmanaged[Stdcall]<nint, uint, FilterSpec*, int> SetFileTypes;
        internal nint SetFileTypeIndex, GetFileTypeIndex, Advise, Unadvise;
        internal delegate* unmanaged[Stdcall]<nint, uint, int> SetOptions;
        internal delegate* unmanaged[Stdcall]<nint, uint*, int> GetOptions;
        internal delegate* unmanaged[Stdcall]<nint, nint, int> SetDefaultFolder;
        internal nint SetFolder, GetFolder, GetCurrentSelection;
        internal delegate* unmanaged[Stdcall]<nint, char*, int> SetFileName;
        internal nint GetFileName;
        internal delegate* unmanaged[Stdcall]<nint, char*, int> SetTitle;
        internal nint SetOkButtonLabel, SetFileNameLabel;
        internal delegate* unmanaged[Stdcall]<nint, nint*, int> GetResult;
        internal nint AddPlace;
        internal delegate* unmanaged[Stdcall]<nint, char*, int> SetDefaultExtension;
        internal nint Close, SetClientGuid, ClearClientData, SetFilter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenDialogVtable
    {
        internal FileDialogVtable Dialog;
        internal delegate* unmanaged[Stdcall]<nint, nint*, int> GetResults;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShellItemVtable
    {
        internal UnknownVtable Unknown;
        internal nint BindToHandler, GetParent;
        internal delegate* unmanaged[Stdcall]<nint, uint, nint*, int> GetDisplayName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShellItemArrayVtable
    {
        internal UnknownVtable Unknown;
        internal nint BindToHandler, GetPropertyStore, GetPropertyDescriptionList, GetAttributes;
        internal delegate* unmanaged[Stdcall]<nint, uint*, int> GetCount;
        internal delegate* unmanaged[Stdcall]<nint, uint, nint*, int> GetItemAt;
    }
}
