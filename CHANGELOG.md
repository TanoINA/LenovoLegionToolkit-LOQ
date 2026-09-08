# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [2.35.4.6-loq] - 2026-09-08

### Fixed
- **Fn+Q deadlock (ITSModeFeature)**: `SetStateAsync` could hold the ITS mode lock (`_setStateLock`) permanently if it threw `InvalidOperationException` before releasing it. The lock now releases in a `finally` block on every path, matching the pattern in `PowerModeListener`.
- **dGPU Awake silent init failure**: The constructor's fire-and-forget `UpdateStateAsync()` is wrapped in `try/catch` so D3D11/IDXGIFactory6 init failures are logged instead of swallowed.
- **dGPU Awake flapping on AC spikes**: Transient `ACLineStatus=255` readings on the AC-change handler are debounced (500ms) to stop rapid on/off oscillation.
- **Automation event flood & dropped events (AutomationProcessor)**: Rapid Fn+Q / AC plug-unplug bursts are coalesced via an `Interlocked` reentrancy guard, preventing unbounded `_runLock` queue growth. An event arriving mid-cycle is now processed in a trailing pass instead of being dropped, so state-based triggers (e.g. a `PowerMode=Performance` event landing mid-cycle) are acted upon.
- **OSD multi-screen reuse race (NotificationWindow)**: `Close(bool)` is now idempotent and the `SourceInitialized` position update is guarded by `IsOpen`, fixing a race when the manager prunes a screen while the auto-close timer fires.
- **dGPU Awake crash on dispose**: The `async void` power-state handler is wrapped in `try/catch` so an `ObjectDisposedException` or COM error during concurrent teardown can no longer propagate to the `SynchronizationContext` and crash the process. Added a post-lock `_isDisposed` re-check to close the window where a concurrent `DisposeAsync` could dispose the lock mid-acquire.
- **Clean app exit & singleton disposal (`IoCContainer`)**: Added `Dispose()` to `IoCContainer` called on `Application_Exit` to ensure hardware controllers, sensor drivers, and background managers clean up unmanaged resources properly.
- **Service hang prevention & SCM handle leak (`AbstractSoftwareDisabler`)**: Added 30-second timeouts to `WaitForStatus` calls and wrapped `ServiceController.GetServices()` in `try/finally` disposal.
- **Sensor recovery resilience (`SensorsGroupController`)**: `_hardwareInitialized` is now only set true after successful hardware discovery, and reset failures clear initialized state so subsequent calls can retry and recover missing sensors.
- **WPF async void exception guards**: Wrapped `async void` event handlers in dashboard controls (`DiscreteGPUControl`, `ITSModeControl`, `PowerModeControl`, `SpectrumKeyboardBacklightControl`) with `try/catch` logging to prevent UI crashes.
- **WMI COM wrapper disposal (`WMI.cs`)**: Added explicit disposal for `ManagementObjectSearcher`, parameter collections, and query result objects.

### Tests
- Added `SetStateLockReleaseTests` covering lock release on exception paths for ITS mode transitions.
- Added `ProcessEventCoalescingTests` (3 tests) verifying burst event concurrency stays at 1 and the latest event arriving mid-cycle is processed, not dropped.

---

## [2.35.4.5-loq] - 2026-09-08

### Added
- **Unit Test Suite (`LenovoLegionToolkit.Tests`)**:
  - `PowerModeConcurrencyTests`: Proves thread-safe serialization of power mode transitions and exception resilience without deadlocks.
  - `BiosRegexTests`: Validates LOQ and Legion BIOS parsing for 2-digit (`R3CN44WW`, `GKCN50WW`), 3-digit (`EFCN101WW`, `H1CN105WW`), and invalid inputs.
  - `ScmCacheExpiryTests`: Verifies 5-second TTL cache hit behavior, forced refresh bypass, and immediate cache invalidation.
- **Runtime Smoke Test Script (`smoke_test.ps1`)**:
  - Automated PowerShell test profiling memory, working set, handle count, and thread stability over 60 seconds with pass/fail health checks.
- **Smart Cache Invalidation**:
  - Added `InvalidateCache()` method to `AbstractSoftwareDisabler` for explicit, instant SCM cache invalidation.

### Fixed
- **Fn+Q Power Mode Looping**:
  - Resolved re-entrant race conditions causing rapid switching loops between Quiet/Balance and Performance modes by introducing strict `SemaphoreSlim` serialization in `PowerModeListener` and `ITSModeFeature`.
- **OSD Notification Delay & Lag**:
  - Reduced notification latency from ~7 seconds to ~0 ms by caching SCM/process checks with a 5-second TTL in `AbstractSoftwareDisabler`.
  - Implemented in-place window reuse in `NotificationsManager` and `NotificationWindow` to eliminate Win32 HWND re-creation, screen flicker, and DWM composition latency.
  - Accelerated notification dispatch to trigger immediately before awaiting heavy asynchronous operations.
- **Resource & Handle Leaks**:
  - Wrapped `EventLogRecord` in `using` blocks within `Battery.cs` to prevent unmanaged OS event log handle leakage.
  - Ensured prior `CancellationTokenSource` instances are properly canceled and disposed in `AutomationProcessor.cs`.
  - Explicitly disposed SCM `ServiceController[]` arrays in `AbstractSoftwareDisabler.cs`.
- **Null Safety & Interop Hardening**:
  - Replaced unsafe COM interface casts with safe pattern-matching in `DgpuAwakeManager.cs` (`IDXGIFactory6`).
  - Expanded `stackalloc` path buffer in `FullscreenHelper.cs` from 260 to 1024 characters to safely handle long NT paths.
  - Double-checked locking with `SemaphoreSlim` in `Compatibility.GetMachineInformationAsync` to eliminate redundant concurrent WMI initialization on startup.
- **Compiler Warnings (CS1998)**:
  - Eliminated all 14 `CS1998` warnings across 8 files by converting synchronous methods to `Task.FromResult` or synchronous `void`, eliminating unnecessary heap-allocated compiler state machines.

---

## [2.35.4.4-loq] - 2026-09-07

### Added
- Native support and calibration for **Lenovo LOQ 15IRX10** (Model 83JE, BIOS R3CN44WW).
- Support for Intel Core i5-13450HX and NVIDIA GeForce RTX 5050 Laptop GPU.
- Support for 144Hz high refresh rate display switching.

### Fixed
- Stabilized power adapter status detection against transient ACLineStatus 255 readings and hybrid battery discharge spikes.
