# Lenovo Legion Toolkit (LOQ Edition)

A custom build of [Lenovo Legion Toolkit](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit) tailored for Lenovo LOQ series laptops (tested primarily on the LOQ 15IRX10 with i5-13450HX and RTX 5050 Laptop GPU).

This project tracks official upstream releases by the [LenovoLegionToolkit-Team](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit) while incorporating platform-specific fixes and concurrency hardening for LOQ hardware.

## About this Build

Lenovo Legion Toolkit was originally designed for the Legion ecosystem. While upstream provides broad compatibility, certain newer LOQ motherboards, BIOS revisions, and power controllers behave slightly differently under Windows Modern Standby and rapid power transitions.

This build addresses those platform-specific behaviors while preserving 100% upstream feature parity:

- **Power Mode Switching Stability**: Fixed a concurrency feedback loop between EC WMI events and the dispatcher that caused rapid mode flickering (LED cycling between Quiet and Balance) when on AC power.
- **Native Windows Power Mode on AC**: Quiet mode cleanly stays on *Best Power Efficiency* on AC power without firmware rejection or fallback loops.
- **Refresh Rate Persistence**: Prevents display refresh rate from unexpectedly reverting across reboots, sleep/wake cycles, and AC adapter transitions.
- **Advanced Optimus Wake Handshake**: Added an automatic dGPU pulse kick during AC startup/wake to ensure the NVIDIA Dynamic Display Mode tray icon appears reliably.
- **Custom Mode Compatibility**: Gracefully handles unsupported `FanFullSpeed` WMI calls and fan table timeouts on LOQ motherboards, preventing custom presets from failing.
- **Lifecycle & Memory Hardening**: Centralized control attach/detach hooks to eliminate lingering event subscriptions when switching views, decoupled static system listeners, and serialized delayed power transitions.
- **Upstream Parity (v2.36.0.0)**: Fully synced with upstream v2.36.0.0, including Manual game detection, updated Vantage/Legion Space disablers, and Crowdin localizations.
- **Standalone NSIS Installer**: Packaged into a clean setup installer with automatic elevation and shortcut management.

## Upstream Project

All credit for the core application architecture, features, and continuous development belongs to the original creator **Bartosz Cichecki** and the [LenovoLegionToolkit-Team](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit) maintainers and contributors.

We strongly recommend using the [official releases](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit/releases) whenever possible. This custom build is intended only for LOQ users experiencing the specific hardware and timing issues described above.

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
