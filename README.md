# Lenovo Legion Toolkit (LOQ Edition)

This is my personal fork of [Lenovo Legion Toolkit](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit), adjusted for Lenovo LOQ laptops. I test it mainly on a LOQ 15IRX10 (i5-13450HX, RTX 5050 Laptop GPU, 144Hz), and I keep it synced with upstream releases from [LenovoLegionToolkit-Team](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit).

## Why this build exists

Lenovo Legion Toolkit was built for Legion laptops first. Most of it works on LOQ machines, too, but newer LOQ motherboards and BIOS revisions act up around Modern Standby, AC plug/unplug events, and fan WMI calls.

This build tracks upstream v2.38.0.0 and adds fixes for those LOQ quirks:

- Power mode switching stays put on AC instead of flickering between Quiet and Balance when EC WMI events and the dispatcher overlap.
- Quiet mode keeps Windows on Best Power Efficiency while plugged in.
- Display refresh rate sticks across reboots, sleep/wake cycles, and AC changes.
- On AC startup and resume, the app briefly wakes the NVIDIA dGPU so the Advanced Optimus tray icon shows up.
- Custom mode ignores unsupported `FanFullSpeed` WMI calls and fan table timeouts on LOQ boards instead of failing the whole preset.
- Extra concurrency, memory, and update-security fixes from local audits, plus a standalone NSIS installer.

## Upstream

Almost all of the app comes from **Bartosz Cichecki** and the [LenovoLegionToolkit-Team](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit) maintainers and contributors.

Use the [official releases](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit/releases) unless you actually run into the LOQ issues listed above.

## Requirements

- Windows 10 or 11 (64-bit)
- Microsoft .NET Desktop Runtime 9.0 (x64)
- A supported Lenovo LOQ or Legion laptop

## Building from source

Install the .NET 9 SDK and NSIS, then run:

```cmd
make_nsis.bat
```

The installer lands in `build_installer/`.

## License

GNU General Public License v3.0, same as upstream.
