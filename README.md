# Lenovo Legion Toolkit (LOQ 15IRX10 Custom Build)

This repository is a personal fork of [LenovoLegionToolkit-Team/LenovoLegionToolkit](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit), maintained for personal use on a Lenovo LOQ 15IRX10 (Intel Core i5-13450HX, NVIDIA GeForce RTX 5050 Laptop GPU).

## Disclaimer

This fork is intended strictly for personal use and experimentation. It is not affiliated with, endorsed by, or supported by Lenovo or the upstream Lenovo Legion Toolkit maintainers. No warranty or support is provided. If you choose to run or build this fork, you do so entirely at your own risk.

For official and widely tested releases, please visit the upstream repository:
https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit

## Changes in this Fork

This build is based on upstream `dev` branch commits and adds specific adjustments for the LOQ 15IRX10 platform:

- Advanced Optimus tray icon fix: Added an automatic dGPU pulse kick during AC startup/wake to resolve missing NVIDIA Dynamic Display Mode tray icon.
- Refresh rate persistence: Fixed display refresh rate automatically reverting to 144Hz across reboots, sleep/wake cycles, and AC adapter connection/disconnection.
- Custom Mode stability: Handled unsupported FanFullSpeed WMI capabilities and fan table write timeouts on LOQ motherboards, preventing presets from dropping back to Performance mode.
- Power adapter state handling: Synchronized power profile enforcement and refresh rate verification when transitioning between AC and battery power.
- NSIS installer: Replaced the installer pipeline with an NSIS script that configures desktop and start menu shortcuts, removes orphaned legacy uninstaller registry entries, and manages elevation for unsigned local binaries.

## Requirements

- Windows 10 or Windows 11 (64-bit)
- Microsoft .NET Desktop Runtime 9.0 (x64)
- Compatible Lenovo LOQ or Legion laptop

## Building from Source

To build the binaries and package the NSIS installer:

1. Ensure .NET 9 SDK and NSIS are installed and available on PATH.
2. Run `make_nsis.bat` from the root of the repository.
3. The installer executable will be generated in the `build_installer` directory.

## Credits

- Original creator: Bartosz Cichecki
- Maintainers: LenovoLegionToolkit-Team
- License: GNU General Public License v3.0 (see LICENSE)
