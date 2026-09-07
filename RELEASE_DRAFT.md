# Lenovo Legion Toolkit v2.35.4.5 (LOQ Edition)

**Release Date:** September 8, 2026  
**Build:** `20260908`  
**Git Tag:** `v2.35.4.5-loq`  
**Target Device Tested:** Lenovo LOQ 15IRX10 (Model 83JE, BIOS R3CN44WW, i5-13450HX, RTX 5050 Laptop GPU, 144Hz)

---

## 🌟 Sorotan Pembaruan (Untuk Pengguna Awam)

- ⚡ **Perbaikan Bug Looping Fn + Q (Power Mode)**:
  Tidak ada lagi masalah mode daya yang gonta-ganti atau looping sendiri tanpa henti saat berpindah dari mode Quiet/Balance ke Performance. Transisi mode daya kini mulus, presisi, dan instan.
- 🚀 **Notifikasi Layar (OSD) Instan & Bebas Delay**:
  Pop-up notifikasi di layar untuk tombol FnLock, CapsLock, Touchpad, Refresh Rate, dan Power Mode kini muncul secara seketika tanpa delay (latensi terpangkas drastis dari ~7 detik menjadi ~0 milidetik). Animasi jendela notifikasi juga diperbarui agar tidak berkedip (*flicker-free*).
- 🛡️ **Stabilitas & Efisiensi Sistem Jangka Panjang**:
  Pembersihan menyeluruh terhadap resource dan memory leak memastikan laptop berjalan stabil dan ringan bahkan setelah berhari-hari pemakaian tanpa restart.

---

## 🛠️ Technical Changelog (Untuk Developer)

### 1. Concurrency & Re-Entrancy Fixes
- **`PowerModeListener.cs`**: Added asynchronous `_dependenciesLock` (`SemaphoreSlim(1, 1)`) guarding `ChangeDependenciesAsync` with strict `finally` block release. Eliminates race conditions between WMI hardware events, GodMode presets, GPU overclocking schedules, and Windows power plan sync.
- **`ITSModeFeature.cs`**: Added `_itsLock` (`SemaphoreSlim(1, 1)`) protecting `ToggleItsMode()`, guaranteeing clean atomic state machine transitions even on rapid consecutive Fn+Q keystrokes.
- **`Compatibility.cs`**: Implemented thread-safe double-checked locking with `_machineInfoLock` on `GetMachineInformationAsync()` to eliminate redundant concurrent WMI probing during startup.

### 2. SCM & Process Caching
- **`AbstractSoftwareDisabler.cs`**: Implemented high-performance 5-second TTL cache (`StatusCache`) with `InvalidateCache()` method. Replaced system-wide process enumeration with targeted `Process.GetProcessesByName`, reducing keystroke latency by ~99.9%.
- Explicitly disposed `ServiceController[]` arrays to eliminate Service Control Manager handle leaks.

### 3. Resource Leak & Interop Hardening
- **`Battery.cs`**: Wrapped `EventLogRecord` in `using` blocks within `GetOnBatterySince()` to prevent unmanaged Windows Event Log handle leakage.
- **`AutomationProcessor.cs`**: Ensured `_cts.Dispose()` is called upon pipeline cancellation before instantiating new tokens.
- **`DgpuAwakeManager.cs`**: Replaced direct COM pointer casts with pattern matching (`if (factoryObj is not IDXGIFactory6 factory)`), preventing `InvalidCastException` / `NullReferenceException`.
- **`FullscreenHelper.cs`**: Increased stackalloc buffer in `QueryFullProcessImageName` from 260 to 1024 characters for long NT device paths.
- **`Compatibility.cs`**: Updated `BiosVersionRegex` to `[0-9]{2,}` to cleanly support 3-digit BIOS revisions without truncation.

### 4. Zero Warnings & Clean State Machines
- Resolved all 14 `CS1998` warnings across 8 files (`SensorsControllerV0`, `PrecisionTouchpadLockFeature`, `ITSModeFeature`, `ITSModeListener`, `App.xaml.cs`, `RGBKeyboardBacklightControl`, `LampArrayRGBKeyboardPage`, `GodModeSettingsWindow`).
- **Build Status**: **`0 Error(s), 0 Warning(s)`**.
- **Automated Verification**:
  - `LenovoLegionToolkit.Tests`: 17 / 17 Unit Tests Passed (MSTest).
  - `smoke_test.ps1`: 60-second runtime diagnostic passed (Zero memory creep, zero handle leak).

---

## 📌 Catatan Penggunaan Penting

> [!TIP]
> **Badge "LOG" di Pojok Kanan Atas:**  
> Jika kamu melihat badge oranye bertuliskan **`Log`** di pojok kanan atas jendela aplikasi, itu menandakan fitur pelacakan aktivitas (*logging*) sedang aktif.  
> Untuk pemakaian harian, fitur ini **tidak diperlukan** dan disarankan untuk dimatikan agar tidak membebani penulisan ke disk SSD:  
> Buka menu **Settings** ➔ Bagian **App / General** ➔ Matikan toggle **Logging** / **Enable logging**.

---

## 📦 File Rilis

- **Installer NSIS**: `LenovoLegionToolkitSetup-v2.35.4.5_Build20260908_NSIS.exe` (16.9 MB, Solid LZMA)
- **Kompatibilitas OS**: Windows 10 / Windows 11 (64-bit)
