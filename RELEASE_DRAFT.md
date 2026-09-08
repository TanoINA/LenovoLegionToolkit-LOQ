## What's New

Follow-up release fixing a few concurrency and stability issues that slipped through in v2.35.4.5.

### Fixes
- **Fn+Q deadlock** - `SetStateAsync` could hold the ITS mode lock permanently if it threw before releasing it. The lock now releases in a `finally` block on every path.
- **Atomic ITS overlay sync** - converted `_pendingOverlaySync` to atomic `Interlocked.Exchange`, eliminating double-firing between WMI events and timer timeouts.
- **Hardware sensor resilience** - GPU refresh loop catches and logs transient WMI errors instead of terminating monitoring.
- **PowerListener thread safety** - guarded overlay and power plan snapshot reads/writes with `_stateLock` to prevent torn Guid reads between UI and poll threads.
- **dGPU Awake silent init failure** - the constructor's fire-and-forget `UpdateStateAsync()` is wrapped in `try/catch` so D3D11/IDXGIFactory6 init failures are logged instead of swallowed.
- **dGPU Awake flapping on AC spikes** - transient `ACLineStatus=255` readings are debounced (500ms) to stop rapid on/off oscillation.
- **Automation event flood & dropped events** - rapid Fn+Q / AC plug-unplug bursts are coalesced via an `Interlocked` reentrancy guard; an event arriving mid-cycle is now processed in a trailing pass instead of being dropped, so state-based triggers (e.g. `PowerMode=Performance` landing mid-cycle) are acted upon.
- **OSD multi-screen reuse race** - `Close(bool)` is now idempotent and the `SourceInitialized` position update is guarded by `IsOpen`, fixing a crash when a screen is pruned while the auto-close timer fires.
- **dGPU Awake crash on dispose** - the `async void` power-state handler is wrapped in `try/catch` so an `ObjectDisposedException`/COM error during teardown can't crash the process; added a post-lock `_isDisposed` re-check against concurrent `DisposeAsync`.
- **Clean exit & singleton disposal** - added `IoCContainer.Dispose()` on `Application_Exit` to ensure hardware sensor drivers and controllers release resources cleanly.
- **Service hang prevention & SCM leaks** - added 30s timeouts to `WaitForStatus` and wrapped `ServiceController.GetServices()` in explicit disposal.
- **Sensor recovery resilience** - hardware initialization flag only set upon success, allowing automatic recovery if initial discovery fails.
- **WPF async void exception guards** - wrapped UI event handlers in try/catch to prevent unhandled exceptions from crashing the application.
- **WMI COM resource cleanup** - explicit disposal for WMI searchers, parameter objects, and result collections.
- **Packaging & Build System** - hardened `make_nsis.bat` and `make_installer.nsi` to cleanly separate `NUMERIC_VERSION` from `-loq` SemVer tag and clean build staging directory.

### Tests
- Added `SetStateLockReleaseTests` (4 tests) and `ProcessEventCoalescingTests` (3 tests). 24/24 unit tests passing (100% PASS), builds clean (0 warnings).

---

### Notes
* Tested on Lenovo LOQ 15IRX10 (Model 83JE, BIOS R3CN44WW, i5-13450HX, RTX 5050 Laptop GPU, 144Hz).
* If the orange **Log** badge is visible in the top-right corner, application logging is enabled. For normal daily use, you can turn off **Logging** in *Settings -> App* to reduce disk writes.

**Installer**: `build_installer/LenovoLegionToolkitSetup-v2.35.4.6-loq_Build20260908_NSIS.exe`
