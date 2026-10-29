# Audit dan Bugfix Mendalam - 2026-10-29

## Ruang lingkup dan metode

- Basis: commit `8a730187` pada branch `master`.
- Enam subagent menelusuri Controllers/Listeners, interop hardware, WPF Windows, WPF Controls/Pages, Automation/Macro/CLI, serta Utils/Settings.
- Setiap temuan diverifikasi ulang terhadap call path aktual. Klaim yang hanya berupa pattern, best practice, atau spekulasi tidak diterapkan.
- Patch menjaga API publik, format settings/JSON, perilaku kanal update, dan kontrak fitur yang ada.
- Tidak ada perubahan hardware, registry, service, atau settings pengguna selama audit.

## Baseline

- Release build awal: 0 warning dan 0 error.
- Test awal: 30/31 lulus. Dua test throttle berbasis waktu terbukti dapat gagal ketika thread tertunda melewati interval; test dibuat deterministik tanpa mengubah implementasi throttle produk.

## Bug yang diperbaiki

| Area | Akar masalah | Perbaikan | Status |
|---|---|---|---|
| AIController | Exception event dapat lolos dari `async void`; array `Process` tidak dibuang | Guard bersama dan disposal seluruh array pada `finally` | Diperbaiki |
| Download HTTP | Response 404/500 disalin sebagai payload sukses | `EnsureSuccessStatusCode` sebelum membaca body | Diperbaiki |
| Update/package | Download gagal meninggalkan installer/package besar di temp | Hapus temp pada kegagalan tanpa menutupi exception asli | Diperbaiki |
| External detection | Executable final dibuat sebelum download selesai | Download ke temp unik lalu atomic move setelah sukses | Diperbaiki |
| Update integrity | Thumbprint tidak membuktikan hash Authenticode | `WinVerifyTrust` hash-only plus thumbprint publisher yang sudah ada | Diperbaiki |
| Power plan | GUID dari `PowerGetActiveScheme` tidak dibebaskan | `LocalFree` pada seluruh return/exception path di tiga call site | Diperbaiki |
| KnownFolders | Buffer `SHGetKnownFolderPath` tidak dibebaskan | `FreeCoTaskMem` pada `finally` | Diperbaiki |
| WiFi listener | Registrasi notification gagal setelah handle dibuka | `WlanCloseHandle` pada cabang gagal | Diperbaiki |
| Registry helpers | Registry key, identity, dan wait-handle CTS tertahan | Disposal key/identity dan teardown observer idempoten | Diperbaiki |
| DgpuAwakeManager | Dispose melewati cleanup device; HRESULT create diabaikan | Cleanup tetap berjalan saat dispose; create gagal tidak dianggap aktif | Diperbaiki |
| GPUController | Start bersamaan dapat membuat refresh loop yatim | Lifecycle start/stop diserialkan dan CTS dibersihkan setelah stop | Diperbaiki |
| GPU overclock | Apply dapat interleave; struct settings dapat terbaca torn | Apply lock, settings snapshot lock, event `Changed` setelah gate dilepas | Diperbaiki |
| Spectrum controller | Hotkey HID dapat crash; Aurora start/stop dapat menimpa task | Guard hotkey, lifecycle gate, stop exception-safe, HID cleanup terlindungi | Diperbaiki |
| Sensor producer | Subscriber bersamaan dapat menimpa producer CTS/task | Producer gate dan subscriber count di bawah lock | Diperbaiki |
| FPS monitoring | Linked CTS menumpuk pada pergantian foreground | Dispose linked CTS pada `finally` untuk semua completion path | Diperbaiki |
| AMD overclock | Apply dapat interleave; profile dapat tercampur; nilai negatif dibungkus ke `uint` | Apply lock, snapshot profile deep-copy, validasi sebelum hardware | Diperbaiki |
| Macro hotkey | Key-up memicu/interupsi playback | Playback hanya pada key-down; key-up hotkey tetap disuppress seperti semula | Diperbaiki |
| Macro playback | Cancel-await-replace dapat menjalankan dua playback | Preambule diserialkan dengan gate | Diperbaiki |
| Macro cancellation | Tombol yang sudah dikirim dapat tertinggal dalam keadaan Down | Lacak kepemilikan input dan kirim Up pada cleanup | Diperbaiki |
| Macro mouse | Horizontal wheel no-op; absolute move memakai working area | Mapping `MOUSEEVENTF_HWHEEL`; virtual-screen origin/bounds dan `VIRTUALDESK` | Diperbaiki |
| Automation startup | Pipeline non-startup tidak menandai `environment.Startup` | Teruskan konteks startup; Run Now tetap bukan startup | Diperbaiki |
| Run step | Substring direktori dapat dianggap process yang sama; process bocor | Bandingkan basename executable case-insensitive dan dispose process | Diperbaiki |
| UI threading | Dua continuation meninggalkan UI thread sebelum menyentuh kontrol | Pertahankan Dispatcher context pada handler logging dan Spectrum | Diperbaiki |
| Tray | Icon resource bersama ikut dibuang; tooltip lama menimpa window baru | Clone icon saat assignment; ownership CTS/window dibuat lokal | Diperbaiki |
| Sensor V1/Battery/OSD | Transisi visibility dapat menimpa CTS/task aktif | Visibility gate, task ditunggu sebelum CTS/lock dibuang | Diperbaiki |
| Sensor V2 | Dispatcher shutdown dapat lolos; refresh dapat overlap | Tangani cancellation dan batasi satu refresh aktif | Diperbaiki |
| Sensor V2 | Legacy-only tidak memiliki producer tick; probe transien tidak pulih | Pump CPU khusus legacy-only dan tiga probe dengan backoff | Diperbaiki |
| MessagingCenter | Combo-box feature control menumpuk subscription | Unsubscribe simetris saat unload | Diperbaiki |
| App startup/shutdown | Startup lanjut setelah close; instance kedua dapat mematikan instance pertama; cleanup reentrant | Shutdown guards, single-instance handler aman, shutdown single-flight | Diperbaiki |
| Status/update windows | CTS yang sudah dibatalkan dipakai ulang; cancel dapat tetap menjalankan installer | Recreate CTS dan periksa cancellation sebelum `Process.Start` | Diperbaiki |
| OSD FPS | Setiap window dapat membuang singleton FPS milik IoC | Window hanya stop/unsubscribe; ownership dispose tetap pada container | Diperbaiki |

## Regression test yang ditambahkan

- HTTP error tidak diterima sebagai payload; payload sukses tetap identik.
- Horizontal wheel mempertahankan delta; absolute mouse movement benar pada virtual screen dengan origin negatif.
- Step berbahaya dilewati pada startup meski pipeline tidak memakai startup trigger; Run Now tetap mengeksekusinya.
- Authenticode valid diterima; satu byte perubahan pada salinan executable ditolak.
- Test throttle diperbaiki agar menunggu task dispatch secara langsung, bukan timing `Sleep` arbitrer.

## False positive dan perubahan yang sengaja tidak diterapkan

- `Register<T>()` sudah selalu memanggil `.SingleInstance()`. Klaim settings, `UpdateChecker`, dan `HttpClientFactory` transient tidak valid; tidak ada perubahan IoC.
- Projection `GetDeviceRemovedReason()` mengembalikan `void` dan sudah melempar saat device gagal. Jalur tersebut dipertahankan.
- `.Result` pada task yang sudah selesai dan synchronous dispose yang memang diperlukan tidak diubah.
- Sentinel baterai 255 dan default status AC tidak diubah karena perbaikan yang benar memerlukan kontrak unknown/nullable yang jelas, bukan menebak nilai pengganti.
- Disposal `Process` pada snapshot GPU tidak diterapkan karena object masih diekspos ke konsumen; perlu perubahan ownership/snapshot yang terpisah.
- Coalescing automation menjadi FIFO, perubahan timeout CLI, dan penambahan trigger live baru tidak dilakukan karena mengubah semantics fitur.
- Rekomendasi yang membutuhkan redesign lebih besar pada `VolumeListener`, `SmartFnLockController`, dan `InternalDisplay` tidak dipaksakan dalam patch ini.

## Preservasi LOQ dan hardware

- `Power.cs` tidak diubah; low-wattage charger bypass tetap utuh.
- `WindowsPowerPlanController` hanya menerima perbaikan `LocalFree`; LOQ AC Power Guard tidak berubah.
- `PulseDgpuAsync`, cancellation pulse, dan COM cleanup tetap dipertahankan.
- `RefreshRateFeature` dan persistence/retry display tidak diubah.
- Tidak ada XAML, brush/theme, format settings, atau database schema yang diubah.

## Verifikasi terpusat

- Release build WPF: 0 warning dan 0 error.
- Full test: 38/38 lulus.
- `dotnet publish --no-build` ke folder scratch: berhasil.
- `git diff --check`: bersih.
- Uji hardware fisik tidak dijalankan; perubahan hardware diverifikasi melalui source/call path dan test non-destruktif.

Cakupan akhir: 38 file tracked yang dimodifikasi, 5 file source/test baru, dan dokumen audit ini.
