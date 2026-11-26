# Duxel Gallery for Linux

[한국어](README.ko.md) · [Linux setup and WSLg](../../docs/linux-platform.md)

Use the same Fluent 2 examples and C# samples on an X11 or Xwayland desktop. See the [Gallery guide](../../docs/gallery-navigation.md) for the desktop menu bar and other examples.

From the repository root, run:

~~~bash
dotnet run --project samples/Duxel.Gallery.Linux -f net11.0
~~~

Use the repository SDK and install Fontconfig, a desktop font, and a working Vulkan driver. You can select `net8.0`, `net9.0`, or `net10.0` with the corresponding runtime installed. The [Linux setup guide](../../docs/linux-platform.md) also shows how to run the published package.
