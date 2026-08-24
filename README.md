# PowerSave Studio — Premium Windows Power Switcher

> **One click. Three worlds.**  
> Ultra Power Save · Power Save · Ultra Performance  
> Featherweight WinForms · 0% idle CPU · ~10–25 MB RAM · ~26 MB single-file

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)
![Size](https://img.shields.io/badge/size-%7E26%20MB-blue)

```
+--------------------------------------------------------------+
|  POWERSAVE  studio                                    _   X  |
|  Good evening, FSOS                    ( ULTRA POWER SAVE )  |
|  Friday, Aug 22 - On battery                                |
|                                                              |
|  +--------------------------+   +------------------------+   |
|  | ULTRA POWER SAVE       o |   |      BATTERY           |   |
|  +--------------------------+   |         (ring)         |   |
|  | POWER SAVE             o |   |        87 %            |   |
|  +--------------------------+   | ~7 hr 12 min remaining |   |
|  | ULTRA PERFORMANCE      o |   | Source    Battery      |   |
|  +--------------------------+   | Drain rate -3.2 %/h    |   |
|                                 +------------------------+   |
|  ADVANCED TUNING                                             |
|  QUICK ACTIONS                                               |
|  Ready - Ultra Power Save active          idle - v1.0        |
+--------------------------------------------------------------+
```

Designed for laptops that need to last — and desktops that need to scream. No bloat, no installers, no services. Just pick a mode.

---

## ✨ Why PowerSave Studio?

| | What you get |
|---|---|
| **Instant switching** | Creates a dedicated power plan per mode once (`powercfg -duplicatescheme`), reuses it thereafter — switching is ~1 click, ~1 second. |
| **Zero idle cost** | No polling loop. Battery checks every 60 s (5 min in tray) + instant on `WM_POWERBROADCAST`. Animation timer only while animating. Working set trimmed via `SetProcessWorkingSetSize` — Task Manager often shows 10–25 MB. |
| **Apple-polished UI** | Dark squircle cards, iOS segmented control & switches, SF Symbols via Heroicons/Tabler/Lucide, spring animations, PerMonitorV2 DPI, rounded corners (`DWMWA_WINDOW_CORNER_PREFERENCE`). |
| **Respects you** | Brightness is **never** touched. Only documented visual tweaks (transparency off, best-performance) are stored with snapshot+restore. No background-app / widget nuking. |
| **Single-instance** | Second launch forwards `--apply=` via named pipe, never spawns a duplicate. |
| **Tray-native** | Left-click restores, right-click shows Apple HIG popup. `CloseToTray` toggle keeps it in the tray. |

---

## 🎚️ Modes

| Mode | What it does |
|---|---|
| **Ultra Power Save** | Own plan cloned from Energy Saver. CPU max **15%**, boost **off**, Energy-Performance preference **100**, screen off 1–3 min, sleep 5–15 min, Wi-Fi max saving, USB selective suspend **on**, PCIe ASPM **on**. Optionally pauses `WSearch`, `DiagTrack`, `SysMain`, `Spooler`. Feels slow *on purpose* — built to stretch toward many hours. |
| **Power Save** *(Balanced)* | Balanced clone. CPU 100% AC / 80% DC, boost **on**, sensible timeouts, Wi-Fi medium saving. The everyday mode. |
| **Ultra Performance** | High-Performance clone. CPU min/max **100%**, aggressive boost, display/sleep timeouts disabled on AC, Wi-Fi/USB/PCIe all **maximum performance**. Expect fan + heat — that's the price of fast. |

All three modes disable taskbar transparency and set **best-performance visuals** (animations/shadows off) for GPU saving — snapped and restorable. `MenuShowDelay` / bloat removals are **not** touched.

> Indexing pause & service tweaks need elevation — use **Restart as admin**.

---

## 🚀 Quick Start

### Option A — Download
Grab `PowerSave.exe` from **Releases** (~26 MB single-file framework-dependent) or `PowerSave self-contained.exe` (~69 MB, no runtime). Framework-dependent still needs the .NET 8 Desktop Runtime.

### Option B — Build
Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# Framework-dependent single exe -> publish/
.\build.ps1

# Standalone (no runtime needed) -> publish selfcontained/
.\build.ps1 -SelfContained

# Build + launch
.\build.ps1 -Run

# Manual
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Framework-dependent single-file is ~26 MB (includes the 24 MB `Microsoft.Windows.SDK.NET.dll` projection + app).  
Self-contained is ~69 MB (no runtime needed).  
Framework-dependent builds need the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime). If missing, use `-SelfContained`.

---

## 🎮 Usage

**Modes tab** — click a card. Progress pill animates, border accent morphs, tray icon tints.

**Battery tab** — large ring, state line, Source/Drain stats. Pulse when charging.

**Advanced tab** — toggles:

- Silence background services on Ultra Save
- Best performance visuals (ALL modes)
- Wi-Fi maximum power saving
- USB selective suspend off (Performance)
- Keep display awake (Performance)
- Start with Windows (`HKCU\...\Run`)
- *Close button hides to tray* (tray popup toggle)

**Quick actions:** Sleep now · Lock · Free up memory (trim all processes) · Toggle Bluetooth · Open power settings · Restart as admin.

**Tray:** Left-click restores, right-click shows modes + Sleep/Lock/Quit. `Close` hides to tray.

---

## 🤖 Automation (CLI)

```powershell
PowerSave.exe --apply=ultrasave        # ultra power save
PowerSave.exe --apply=powersave        # balanced
PowerSave.exe --apply=ultraperf        # ultra performance
PowerSave.exe --tray                   # start minimized to tray
PowerSave.exe --help
```

Aliases are normalized (`ultrasave`, `ups`, `balanced`, `eco`, `perf`, `up`, …). Second instance forwards over named pipe `PowerSaveStudio.Ipc.v1` with retry+backoff.

---

## 🛠️ Under the Hood

- **Power:** `powercfg /getactivescheme` + `/duplicatescheme` + `/setacvalueindex`/`/setdcvalueindex`/`/setactive`. Plans cached in `%LOCALAPPDATA%\PowerSave\settings.json`.
- **Battery:** `GetSystemPowerStatus` (AC/BatteryFlag/Percent/LifeTime) + 20-min sliding sample for `% / h` drain estimate.
- **Visuals:** `SystemParametersInfo(SPI_SETUIEFFECTS)` + `HKCU\...Themes\Personalize\EnableTransparency` + `HKCU\...\VisualEffects\VisualFXSetting`.
- **Keep-awake:** `SetThreadExecutionState(ES_DISPLAY_REQUIRED|ES_SYSTEM_REQUIRED)`.
- **Bluetooth:** `Windows.Devices.Radios.Radio`.
- **Logs:** `%LOCALAPPDATA%\PowerSave\powersave.log` (1 MB rotation), flush every 4 s.

### Project layout
```
PowerSave.csproj  UI/  Core/  Infra/  app.manifest  build.ps1
UI/ Theme.cs Controls.cs ModeCard.cs BatteryCard.cs QuickStrip.cs AdvancedSection.cs
    Indicators.cs Icons.cs AnimEngine.cs TitleBar.cs TrayPopup.cs ModeProgressBar.cs MainForm.*
Core/ Modes.cs PowerManager.cs BatteryMonitor.cs SystemTweaks.cs BluetoothController.cs
Infra/ Settings.cs Ipc.cs Logger.cs CliOptions.cs Native.cs
```

---

## 🔧 Troubleshooting

- **First click of each mode creates a plan** named `PowerSave — <Mode>` — this needs ~1 s, subsequent switches are instant.
- **UAC tip appears** if "Silence background services" is on but not elevated.
- **No battery?** Desktop PCs show `--` / "Desktop PC" correctly.
- **Restore visuals:** Toggle *Best performance visuals* off — restores snapshot.
- **Startup:** *Start with Windows* writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\PowerSave = "…\PowerSave.exe" --tray`.

---

## 🤝 Contributing

PRs welcome! Please:

1. Keep WinForms — no WPF/MAUI.
2. Keep framework-dependent single file ~26 MB, self-contained ~69 MB — don't bloat further.
3. Respect `PowerSave.csproj:1` style (see `UI/Theme.cs:1` for tokens).
4. Run `dotnet build -c Release` before pushing.

---

## 📄 License

MIT — see [LICENSE](LICENSE). Icons: Heroicons (MIT), Tabler (MIT), Lucide (ISC).

---

*Built by FSOS Labs — for laptops that refuse to die.*
