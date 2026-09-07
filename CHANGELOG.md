# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
