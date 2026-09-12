# Lenovo Legion Toolkit (LOQ Edition)

A custom build of the excellent [Lenovo Legion Toolkit](https://github.com/BartoszCichecki/LenovoLegionToolkit) tailored specifically for Lenovo LOQ series laptops (tested primarily on the LOQ 15IRX10 with i5-13450HX and RTX 5050).

This project tracks official upstream releases by Bartosz Cichecki while incorporating platform-specific timing adjustments for LOQ hardware.

## About this Build

Lenovo Legion Toolkit is originally crafted for the Legion ecosystem. While upstream provides broad compatibility, certain newer LOQ motherboards and BIOS revisions feature distinct EC firmware timings and ACPI event behaviors.

This build introduces specific hardware-level refinements for these devices:
- **Power Mode Timing**: Refined WMI listener debouncing and dispatcher synchronization to ensure smooth, stable Fn+Q mode transitions on AC power.
- **Windows Power Mode on AC**: Quiet mode cleanly stays on *Best Power Efficiency* on AC power without reverting.
- **Display Mode Handshake**: Added a wake pulse to ensure NVIDIA Dynamic Display Mode (Advanced Optimus) tray icons reliably appear after system resume.
- **Background Worker Resilience**: Added extra safety boundaries across automation handlers and stabilized background sensor polling loops.
- **Full Upstream Parity**: Fully synced with upstream v2.36.0.0, retaining all official features, software disablers, and Crowdin translations.
- **NSIS Installer**: Packaged into a standalone Windows setup installer.

## Upstream Project

All credit for the application architecture, features, and continuous development goes to **Bartosz Cichecki** and the [LenovoLegionToolkit](https://github.com/BartoszCichecki/LenovoLegionToolkit) community. If you are using a standard Legion laptop, we strongly recommend using the [official releases](https://github.com/BartoszCichecki/LenovoLegionToolkit/releases).

## Requirements

- Windows 10 or 11 (64-bit)
- Microsoft .NET Desktop Runtime 9.0 (x64)
- Compatible Lenovo LOQ or Legion laptop

## Building from Source

```cmd
# Requires .NET 9 SDK and NSIS
make_nsis.bat
```
The installer executable will be generated in `build_installer/`.

## License

Licensed under the **GNU General Public License v3.0** (same as upstream).
