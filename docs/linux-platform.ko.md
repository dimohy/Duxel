# Linux와 WSLg에서 Duxel Gallery 실행하기

Linux 호스트는 X11과 Vulkan을 사용합니다. X11 데스크톱, Xwayland 세션 또는 WSLg를 갖춘 WSL 2에서 실행할 수 있습니다. 배포판 안에 [Linux용 .NET 11 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)를 설치하세요. WSL에서는 Linux용 `dotnet`으로 실행합니다. [Microsoft WSL GUI 설정](https://learn.microsoft.com/ko-kr/windows/wsl/tutorials/gui-apps)

## Ubuntu 24.04 준비

Fontconfig, 이미지·텍스트 라이브러리, Vulkan 로더와 Mesa 드라이버를 설치합니다. 이미 동작하는 GPU용 Vulkan 드라이버가 있다면 그대로 사용하세요.

~~~bash
sudo apt update
sudo apt install curl fontconfig fonts-noto-cjk libx11-6 libxcb1 libvulkan1 \
  mesa-vulkan-drivers vulkan-tools libfreetype6 libharfbuzz0b libcairo2 \
  libpango-1.0-0 libpangocairo-1.0-0 libpangoft2-1.0-0 libgdk-pixbuf-2.0-0
~~~

`dotnet --version`, `echo "$DISPLAY"`, `vulkaninfo --summary`로 SDK, X11 세션, Vulkan 장치를 확인합니다.

## 0.4.0 Gallery 실행

~~~bash
curl -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/v0.4.0/samples/fba/duxel_gallery_linux.cs && dotnet run --file duxel_gallery_linux.cs
~~~

C# 파일이 [Duxel.Gallery.Linux 0.4.0](https://www.nuget.org/packages/Duxel.Gallery.Linux/0.4.0)을 복원하고 Gallery를 엽니다. 글꼴은 Fontconfig로 찾습니다. Ubuntu 24.04 x64 WSLg에서 Mesa 소프트웨어 Vulkan과 Mesa Dozen으로 실행을 확인했습니다. GPU와 창 시스템에 따라 결과가 달라질 수 있습니다.

## Linux 앱 만들기

[Duxel.Linux.App](https://www.nuget.org/packages/Duxel.Linux.App/0.4.0)을 Linux 호스트로 사용합니다. [계산기 예제](../samples/fba/duxel_calculator.cs)처럼 플랫폼마다 호스트를 선택하고 UI 코드를 공유할 수 있습니다. Linux는 시스템 창 테두리를 사용하며, 확장 타이틀바나 트레이 같은 Windows 전용 옵션은 Windows 호스트에서 지원합니다.

[파일 한 장으로 실행하는 예제](fba-run-samples.ko.md) · [English](linux-platform.md)
