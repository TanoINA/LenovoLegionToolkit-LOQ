# Changelog

Notable changes for this LOQ fork are tracked here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.38.0.0-loq] - 2026-11-25

### Changed
- Merged upstream Lenovo Legion Toolkit v2.38.0.0 (commit `ab16eb1b`). This brings multi-fan motherboard sensors, safe handling when no battery is present, better Spectrum HID detection, extra bindable Fn keys, and updated Crowdin translations.
- Bumped assembly, NSIS installer, and LampArray manifest versions to `2.38.0.0` so the in-app updater stops offering upstream v2.38.0.0 as a newer release.

### Fixed
- `Compatibility.GetAlwaysOnAcStatus()` had an inverted `NTSTATUS` check (`==` instead of `!=`), so Modern Standby (S0ix) resume callbacks now actually fire in `PowerStateListener`.
- `DgpuAwakeManager.PulseDgpuAsync` now picks the NVIDIA adapter (`0x10DE`) instead of waking the iGPU, and AC plug/unplug events use trailing-edge debouncing so quick transitions are not dropped.
- `GodModeController` no longer triggers an NVAPI PCF reset on every mode switch while the dGPU is asleep, `GetConfigAsync()` assigns `_config` only after filtering capabilities, and `GodModeSettingsWindow` runs `IsPcfAvailable()` off the UI thread.
- `DiscreteGPUManagementWindow` keeps a saved P-State lock when the dGPU wakes while the window is open, `GPUController` stays alive if a subscriber fails on an exited process, and `NVAPI.SetPerformanceState` ignores `Status.NotSupported` on `ClearRatedTdp()`.
- `OsdWindowBase` cleans up `_sensorSubscription` when closed while visible and only clears `App.Current.OsdWindow` if it still owns that reference; `App.RestartMainWindow()` reuses `EnsureOsdWindowCreated` and `ShutdownCoreAsync()` runs `Shutdown` inside `finally`.
- `SensorsGroupController.ResetSensors()` resets `_initialized` and `InitialState` on failure so retries can recover, `GetCpuNameAsync()` locks `_hardwareLock` instead of `_configLock`, and `SensorsControlV2` formats watt values numerically rather than stripping `"W"` in localized builds.
- `Battery.TryGetBatteryHandleAndTag` and `Devices.InvalidateBattery` avoid disposing a shared battery handle out from under another reader; `BatteryPage` keeps polling across transient tag resets; `GetOnBatterySince()` uses `Environment.TickCount64` and returns `null` instead of `DateTime.MinValue` when no discharge event exists.
- `AutomationProcessor.ProcessEvent` coalesces bursts per event type instead of overwriting unrelated pending events, and `CreateAutomationPipelineWindow` blocks duplicate-trigger workarounds in multi-select mode.
- `AbstractSettings` enforces an allowlist `ISerializationBinder` against JSON `$type` gadgets, locks `Store`/`LoadStore`/`Reset` access, and writes to unique temp files.
- `AuthenticodeVerifier` uses the real `WTD_HASH_ONLY_FLAG` constant (`0x00000200`), while `UpdateWindow` restricts markdown links to HTTP/HTTPS and `UpdateChecker` keeps server downloads on the same host.
- `IpcServer` recovers from transient pipe-creation errors and bounds shutdown waits in both `IpcServer` and `PowerStateListener`.
- `make_installer.nsi`, `make_nsis.bat`, `build_identity_package.ps1`, and `smoke_test.ps1` now handle `.NET 9` `findstr` quoting, missing `build_installer/` creation, missing `SignTool.exe`, and mid-run process crashes properly.

### Tests
- Updated `SensorResilienceTests` for `MotherboardTemperature` and added a transient fan-speed failure test for `GetFanSpeedsAsync`.
- Added `SettingsHardeningTests` to confirm the JSON binder blocks external types while allowing toolkit triggers and collections.

---

## [2.35.4.6-loq] - 2026-09-08

### Fixed
- Released `_setStateLock` in a `finally` block in `ITSModeFeature.SetStateAsync` so exceptions no longer deadlock Fn+Q switching.
- Wrapped fire-and-forget `UpdateStateAsync()` and the `async void` power handler in `DgpuAwakeManager` so COM/disposal errors are logged instead of crashing the app.
- Added burst event coalescing in `AutomationProcessor` so rapid Fn+Q and AC events stay serialized without dropping the latest state.
- Made `NotificationWindow.Close(bool)` idempotent and guarded `SourceInitialized` positioning against multi-screen teardown races.
- Added `IoCContainer.Dispose()` on `Application_Exit` so drivers, sensors, and controllers release native resources on exit.
- Added 30-second timeouts to `WaitForStatus` and explicit disposal of `ServiceController.GetServices()` in `AbstractSoftwareDisabler`.
- Delayed setting `_hardwareInitialized` in `SensorsGroupController` until discovery succeeds so failed startup probes can recover.
- Added `try/catch` guards around `async void` dashboard handlers and explicit disposal for WMI COM wrappers.

### Tests
- Added `SetStateLockReleaseTests` and `ProcessEventCoalescingTests`.

---

## [2.35.4.5-loq] - 2026-09-08

### Added
- Added the `LenovoLegionToolkit.Tests` suite (`PowerModeConcurrencyTests`, `BiosRegexTests`, `ScmCacheExpiryTests`) and `smoke_test.ps1`.
- Added `InvalidateCache()` to `AbstractSoftwareDisabler`.

### Fixed
- Serialized power mode transitions with `SemaphoreSlim` in `PowerModeListener` and `ITSModeFeature` to stop Fn+Q mode loops.
- Cached SCM/process status checks (5s TTL) and reused `NotificationWindow` instances in place to cut OSD lag from ~7 seconds to instant.
- Fixed handle leaks in `Battery.cs` (`EventLogRecord`), `AutomationProcessor.cs` (`CancellationTokenSource`), and `AbstractSoftwareDisabler.cs`.
- Replaced unsafe COM casts in `DgpuAwakeManager.cs`, expanded the path buffer in `FullscreenHelper.cs` to 1024 chars, and serialized `Compatibility.GetMachineInformationAsync`.
- Cleared all 14 `CS1998` compiler warnings.

---

## [2.35.4.4-loq] - 2026-09-07

### Added
- Added calibration and testing for Lenovo LOQ 15IRX10 (Model 83JE, BIOS R3CN44WW, i5-13450HX, RTX 5050 Laptop GPU, 144Hz).

### Fixed
- Stabilized AC adapter detection against transient `ACLineStatus = 255` readings and hybrid battery discharge spikes.
