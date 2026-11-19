# Current Native validation toolchain

On 2026-11-18, the standard `dotnet publish` command in SDK 11.0.100 compiled and linked the Windows x64 NativeAOT Gallery. The build discovered MSVC 14.52.37025 and Windows SDK 10.0.26100.0. The published .NET 11.0.0 executable passed 100 Vulkan presentation scenarios and exited with code 0. The publish log and run results are under `W:/Cache/Duxel/gallery-source-20261117/native-ga1900`.

## Previous toolchain audit

The successful historical log `artifacts/fluent-link-2026-11-06/gallery-native-publish.log` identifies `P:/Program Files/Microsoft Visual Studio/18/Community/VC/Tools/MSVC/14.51.36231/bin/HostX64/x64/link.exe`, file version `14.51.36260.0`. It records native code generation, but contains older net10 library dependency rows and is not evidence for the current source or the required all-net11 validation.

The 2026-11-11 recovery audit finds only C:, L: and W: filesystem drives. The logged linker path is unavailable; neither a P: mounted-device entry nor a P: per-user network mapping exists in this execution environment. Earlier bounded checks found no standard vswhere/SDK library roots or cl/link commands. This establishes unavailable tooling here, not machine-wide absence or an uninstall. Earlier L: runtime/cache access-denied observations remain access denied, rather than empty directories.

No mount, installation, discovery bypass or repeated native publish was attempted. The current C++/Windows SDK location has been requested while independent implementation continues. Once a usable toolchain is confirmed, validate its linker and Windows SDK libraries, publish the current consumer with the fixed SDK/runtime 11 RC3 and win-x64, confirm native generation and warnings, execute CPU/GPU cases, and compare current managed/native images. Keep .NET9/10 for the whole-goal final deployment compilation only. The whole goal remains active with Native/runtime comparison unverified.

Current evidence: `artifacts/multiline-elements-2026-11-11/native-path-audit.json`. Standard-path evidence: `artifacts/text-commands-2026-11-11/native-prerequisite-recheck.json` and `artifacts/allocation-routing-2026-11-10/native-toolchain-audit.json`.
