using System.Globalization;
using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxX11PlatformBackend
{
    private sealed record DesktopScale(float Content, float Text);
    private DesktopScale _desktopScale = new(1, 1);
    private nuint _settingsSelection, _settingsProperty, _settingsOwner, _managerAtom, _resourcesAtom;
    public float ContentScale => Volatile.Read(ref _desktopScale).Content;
    public float TextScale => Volatile.Read(ref _desktopScale).Text;

    private void InitializeDesktopSettings(int screen)
    {
        X11Native.XrmInitialize();
        _settingsSelection = X11Native.XInternAtom(_display, $"_XSETTINGS_S{screen}", 0);
        _settingsProperty = X11Native.XInternAtom(_display, "_XSETTINGS_SETTINGS", 0);
        _managerAtom = X11Native.XInternAtom(_display, "MANAGER", 0);
        _resourcesAtom = X11Native.XInternAtom(_display, "RESOURCE_MANAGER", 0);
        X11Native.XSelectInput(_display, X11Native.XDefaultRootWindow(_display), 131072 | 4194304);
        RefreshDesktopSettings();
    }

    private void RefreshDesktopSettings()
    {
        LinuxDesktopSettings settings = default;
        float? resourceDpi = null;
        // Keep the manager alive between selection lookup, subscription and property read.
        X11Native.XGrabServer(_display);
        try
        {
            var owner = X11Native.XGetSelectionOwner(_display, _settingsSelection);
            if (owner != 0)
            {
                X11Native.XSelectInput(_display, owner, 131072 | 4194304);
                var bytes = ReadDesktopProperty(owner, _settingsProperty, _settingsProperty);
                if (bytes.Length != 0)
                    settings = LinuxDesktopSettings.Parse(bytes);
            }
            _settingsOwner = owner;
            var resource = ReadDesktopProperty(X11Native.XDefaultRootWindow(_display), _resourcesAtom, 31);
            if (resource.Length != 0)
            {
                var database = X11Native.XrmGetStringDatabase(System.Text.Encoding.Latin1.GetString(resource));
                if (database == 0)
                    throw new InvalidOperationException("The X resource database could not be opened.");
                try
                {
                    if (X11Native.XrmGetResource(database, "Xft.dpi", "Xft.Dpi", out _, out var value) != 0)
                    {
                        var text = Marshal.PtrToStringUTF8(value.Address, checked((int)value.Size)).TrimEnd('\0');
                        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !float.IsFinite(parsed) || parsed <= 0)
                            throw new InvalidDataException("Xft.dpi must contain a positive DPI value.");
                        resourceDpi = parsed;
                    }
                }
                finally { X11Native.XrmDestroyDatabase(database); }
            }
        }
        finally { X11Native.XUngrabServer(_display); X11Native.XFlush(_display); }

        float? fontSize = null;
        var absolute = false;
        if (!string.IsNullOrWhiteSpace(settings.FontName))
        {
            var description = LinuxParagraphNative.pango_font_description_from_string(settings.FontName);
            if (description == 0)
                throw new InvalidOperationException("The desktop font description could not be parsed.");
            try
            {
                var size = LinuxParagraphNative.pango_font_description_get_size(description);
                if (size > 0)
                    fontSize = size / 1024f;
                absolute = LinuxParagraphNative.pango_font_description_get_size_is_absolute(description) != 0;
            }
            finally { LinuxParagraphNative.pango_font_description_free(description); }
        }
        var resolved = settings.Resolve(resourceDpi, fontSize, absolute);
        Volatile.Write(ref _desktopScale, new(resolved.Content, resolved.Text));
    }

    private byte[] ReadDesktopProperty(nuint window, nuint property, nuint expectedType)
    {
        var result = X11Native.XGetWindowProperty(_display, window, property, 0, 262144, 0, expectedType,
            out var type, out var format, out var count, out var remaining, out var pointer);
        try
        {
            if (result != 0)
                throw new InvalidOperationException($"XGetWindowProperty failed: {result}.");
            if (type == 0)
                return [];
            if (type != expectedType || format != 8 || remaining != 0 || count > 1048576)
                throw new InvalidDataException("Invalid or oversized desktop settings property.");
            return new ReadOnlySpan<byte>((void*)pointer, checked((int)count)).ToArray();
        }
        finally { if (pointer != 0) X11Native.XFree(pointer); }
    }

    private bool IsDesktopSettingsEvent(in X11Native.Event value)
        => value.Type == 28 && (value.Property.Window == _settingsOwner && value.Property.Atom == _settingsProperty
            || value.Property.Window == X11Native.XDefaultRootWindow(_display) && value.Property.Atom == _resourcesAtom)
        || value.Type == 17 && value.Configure.Window == _settingsOwner
        || value.Type == 33 && value.Client.MessageType == _managerAtom && value.Client.Format == 32
            && (nuint)value.Client.Data1 == _settingsSelection;
}
