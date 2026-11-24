# Duxel Gallery on Linux and WSLg

The Linux host uses X11 and Vulkan. Run it in an X11 desktop, an Xwayland session, or WSL 2 with WSLg. Install the [.NET 11 SDK for Linux](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) inside the distribution. On WSL use the Linux `dotnet` executable. [Microsoft WSL GUI setup](https://learn.microsoft.com/en-us/windows/wsl/tutorials/gui-apps)

## Ubuntu 24.04

The following installs Fontconfig, image and text libraries, the Vulkan loader, and Mesa drivers. If you already have a working driver for your GPU, keep using it.

~~~bash
sudo apt update
sudo apt install curl fontconfig fonts-noto-cjk libx11-6 libxcb1 libvulkan1 \
  mesa-vulkan-drivers vulkan-tools libfreetype6 libharfbuzz0b libcairo2 \
  libpango-1.0-0 libpangocairo-1.0-0 libpangoft2-1.0-0 libgdk-pixbuf-2.0-0
~~~

Run `dotnet --version`, `echo "$DISPLAY"`, and `vulkaninfo --summary` to check the SDK, X11 session, and available Vulkan device.

## Run the 0.4.0 Gallery

~~~bash
curl -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/v0.4.0/samples/fba/duxel_gallery_linux.cs && dotnet run --file duxel_gallery_linux.cs
~~~

The file restores [Duxel.Gallery.Linux 0.4.0](https://www.nuget.org/packages/Duxel.Gallery.Linux/0.4.0) and launches the Gallery. It uses Fontconfig to select installed fonts. Ubuntu 24.04 x64 on WSLg was checked with Mesa software Vulkan and Mesa Dozen. GPU and native Linux results can vary with the driver and window system.

## Build a Linux app

Use [Duxel.Linux.App](https://www.nuget.org/packages/Duxel.Linux.App/0.4.0) as the Linux host. The [Calculator example](../samples/fba/duxel_calculator.cs) shows how to select the host while keeping the same UI code. Linux uses system window chrome, and Windows-specific options such as extended title bars and tray integration require the Windows host.

[More one-file apps](fba-run-samples.md) · [한국어](linux-platform.ko.md)
