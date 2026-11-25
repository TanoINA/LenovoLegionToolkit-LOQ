Lenovo Legion Toolkit (LOQ Edition) synced with upstream v2.38.0.0 (`ab16eb1b`), plus fixes from a post-merge audit.

### From upstream v2.38.0.0
- Multi-fan motherboard sensors across the dashboard, OSD, and tray status popup.
- Safe handling when a laptop runs with no battery detected.
- Better Spectrum HID detection, 24-zone support, and extra bindable Fn keys.
- Updated Crowdin translations and AMD CPU/iGPU naming fixes.

### LOQ and stability fixes
- **Modern Standby & dGPU wake**: `Compatibility.GetAlwaysOnAcStatus()` had an inverted `NTSTATUS` check that kept S0ix resume callbacks disabled. `DgpuAwakeManager.PulseDgpuAsync` now targets the NVIDIA adapter (`0x10DE`) rather than the iGPU and uses trailing-edge AC debouncing so rapid plug/unplug changes are not dropped.
- **Custom mode & GPU P-States**: `GodModeController` no longer runs an NVAPI PCF reset on every mode switch while the dGPU is asleep, and `GodModeSettingsWindow` checks PCF support off the UI thread. `DiscreteGPUManagementWindow` keeps your saved P-State lock when the dGPU wakes while the window is open, and `GPUController` stays running even if a UI tooltip hits an exited process.
- **OSD & Sensors**: Closing a visible OSD no longer leaks its sensor subscription or clears a newly created `App.Current.OsdWindow` during style switches. `SensorsGroupController` recovers properly after a failed `ResetSensors()`, and localized builds no longer print `-1` with translated watt units when the dGPU is inactive.
- **Battery**: `BatteryPage` keeps refreshing past transient battery tag resets, `GetOnBatterySince()` works past 24.85 days of uptime without falling back to year `0001`, and concurrent battery readers no longer race on shared handle disposal.
- **Automation & Settings**: `AutomationProcessor` coalesces burst events per type instead of overwriting unrelated pending events, and `AbstractSettings` enforces an allowlist `ISerializationBinder` against untrusted JSON `$type` payloads.
- **Updates & Packaging**: `AuthenticodeVerifier` uses the correct `WTD_HASH_ONLY_FLAG` (`0x00000200`), `UpdateWindow` restricts markdown links to HTTP/HTTPS, and assembly/installer versions are bumped to `2.38.0.0` so the updater will not offer upstream v2.38.0.0 as an upgrade.
- **Installer & Scripts**: Fixed `.NET 9` `findstr` quoting in `make_installer.nsi`, automatic `build_installer/` creation in `make_nsis.bat`, and crash detection in `smoke_test.ps1`.

### Requirements
- Lenovo LOQ or Legion laptop (tested mainly on LOQ 15IRX10, Model 83JE, i5-13450HX, RTX 5050 Laptop GPU, 144Hz).
- Microsoft .NET Desktop Runtime 9.0 (x64).

Note: If you see an orange Log badge in the top-right corner, debug logging is enabled. Turn it off in Settings -> App for daily use to avoid extra disk writes.
