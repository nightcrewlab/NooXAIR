# NooXAIR v3 — Low-latency AirPlay receiver (iPhone → Windows)

Mirror your iPhone's screen to your Windows PC with sound and low latency. It sits in the system tray, starts with Windows, and is ready to connect without you doing anything.

## Building the setup file

```
build.bat
```

| Command | What it does |
|---|---|
| `build.bat` | Builds everything → `dist\NooXAIR_Setup.exe` |
| `build.bat rebuild` | Rebuilds the UxPlay engine from scratch (to get updates) |
| `build.bat app` | Builds only the interface and setup, reusing the existing engine |

`build.bat` handles the rest automatically:
1. Draws the icon and setup images (`tools\make_icon.ps1`)
2. Builds `NooXAIR.exe` with .NET Framework 4.8's own `csc.exe` (no SDK needed)
3. Downloads MSYS2 if it's missing, installs the packages, and builds **native UxPlay** from source
   - Copies only the DLLs and GStreamer plugins it actually needs (objdump dependency analysis)
4. Downloads Apple Bonjour and bundles it into the setup (offline install)
5. Downloads Inno Setup if it's missing and builds the setup file

## What the setup does
- Installs to `C:\Program Files\NooXAIR`
- **Bonjour:** if it's missing, installs it (bundled MSI → winget → Apple download), sets it to start automatically, and sets it to restart after a crash
- **Firewall:** rules for the engine (program-based, so it covers dynamic ports) and mDNS 5353 (all profiles); removes old *block* rules. Fixed 7000/7100 ports are not used: Hyper-V often reserves that range (WSAEACCES).
- Optional (on by default): sets the network profile to Private, turns off Wi-Fi power saving
- Adds NooXAIR to Windows startup (`--tray`, in the background)

## Why it's faster than uxplay-windows
- Engine: native C UxPlay 1.74, Direct3D 11/12 rendering, GPU hardware decoding (auto-selected NVDEC/QSV/AMF/D3D11)
- `-vsync no` (ultra-low latency), `wasapisink low-latency=true` audio, requests 60 FPS
- Engine process runs at **High** priority, 1 ms timer resolution, sleep blocked while streaming
- Built-in mDNS: the engine doesn't depend on the Bonjour service, so there's one less moving part

## Reliability
- If the engine crashes it restarts automatically (exponential backoff)
- If it crashes twice in a row right after starting → **safe mode** (software decoding, automatic output)
- When the IP changes, the network reconnects or the PC wakes from sleep → the engine refreshes itself (an active stream is never interrupted)
- Job Object: if NooXAIR closes, `uxplay.exe` never stays behind as an orphan

## Files
```
build.bat                 setup builder
tools\build.ps1           build pipeline
tools\build_engine.sh     UxPlay + GStreamer build/packaging (MSYS2)
tools\make_icon.ps1       icon / setup images
src\*.cs                  interface (dark theme), tray, engine manager
installer\NooXAIR.iss     Inno Setup script
installer\postinstall.ps1 Bonjour / firewall / network automation
assets\                   icon and setup images
```

Logs: `%APPDATA%\NooXAIR\nooxair.log`, setup log: `%ProgramData%\NooXAIR\install.log`
