## What's Changed

### Bug Fixes
* **Power Mode Switching Loop (Fn+Q)**: Fixed an issue where switching power modes (e.g. from Balance/Quiet to Performance) caused rapid, repeated looping between modes. Mode transitions are now strictly serialized to prevent WMI event race conditions.
* **OSD Notification Delay**: Fixed a noticeable delay (several seconds) when displaying on-screen notifications (FnLock, CapsLock, Touchpad, Refresh Rate, Power Mode). Service checks are now cached (5s TTL) and notification windows are reused in-place to avoid flicker and DWM lag.
* **Resource & Handle Leaks**:
  * Fixed unmanaged Windows Event Log record handle leak in battery history queries.
  * Fixed cancellation token source disposal in the automation processor.
  * Explicitly disposed ServiceController handles in software disablers.
* **Stability & Interop**:
  * Added safe pattern matching for DXGI COM factory pointers in `DgpuAwakeManager` to avoid null reference / invalid cast crashes.
  * Expanded process path buffer in `FullscreenHelper` from 260 to 1024 characters for long NT paths.
  * Updated BIOS version regex to support 3-digit version numbers without truncation.
* **Code Health**: Cleaned up redundant async state machines across 8 files; project builds with 0 warnings.

### Quality & Tests
* Added unit test suite (`LenovoLegionToolkit.Tests`) covering concurrency serialization, BIOS regex edge cases, and cache expiration.
* Added 60-second runtime diagnostic smoke test (`smoke_test.ps1`) confirming stable memory and zero handle leaks.

---

### Notes
* Tested on Lenovo LOQ 15IRX10 (Model 83JE, BIOS R3CN44WW, i5-13450HX, RTX 5050 Laptop GPU, 144Hz).
* If the orange **Log** badge is visible in the top-right corner, application logging is enabled. For normal daily use, you can turn off **Logging** in *Settings -> App* to reduce disk writes.

**Installer**: `build_installer/LenovoLegionToolkitSetup-v2.35.4.5_Build20260908_NSIS.exe`
