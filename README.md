<div align="center">
  <h1>NetSpeck</h1>
  <p><strong>Live download &amp; upload speed. Right in your Windows 11 taskbar.</strong></p>
  <p><img src="docs/images/indicator.png" width="264" height="88" alt="NetSpeck panel preview with download and upload speeds"></p>
  <p><sub>App-rendered preview · sample traffic</sub></p>
  <p>
    <a href="https://github.com/nt-bro/NetSpeck/releases/tag/v1.0.2"><img src="https://img.shields.io/badge/preview-v1.0.2-38bdf8?style=flat-square" alt="Public preview v1.0.2"></a>
    <img src="https://img.shields.io/badge/Windows-11%20x64-0078D4?style=flat-square" alt="Windows 11 x64">
    <a href="https://github.com/nt-bro/NetSpeck/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/nt-bro/NetSpeck/build.yml?branch=main&amp;style=flat-square&amp;label=build" alt="Build status"></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-34d399?style=flat-square" alt="MIT license"></a>
  </p>
  <p>
    <a href="https://github.com/nt-bro/NetSpeck/releases/download/v1.0.2/NetSpeck-v1.0.2-win-x64.zip"><strong>⬇️ Download ZIP</strong></a>
    &nbsp; · &nbsp; <a href="README.ru.md">🌐 Русский</a>
    &nbsp; · &nbsp; <a href="https://github.com/nt-bro/NetSpeck/issues/new/choose">🐞 Report a bug</a>
  </p>
  <p><sub>Portable · No administrator rights · Russian &amp; English · .NET Framework 4.8</sub></p>
</div>

---

## 🚀 Download and run

1. Open [Releases](https://github.com/nt-bro/NetSpeck/releases).
2. Download `NetSpeck-v1.0.2-win-x64.zip` from the release assets. GitHub's automatic **Source code** archives contain source files, not the ready-to-run app.
3. Extract the archive and run `NetSpeck.exe`. No installation or administrator rights are required. The portable EXE is unsigned.
4. Right-click the indicator or its tray icon to open settings. Starting the app again also opens settings.

Close the app before replacing its EXE during an update. If startup is enabled and you move the EXE, disable and re-enable startup to update the path.

## ✨ Make it yours

| Feature | What you can adjust |
| :--- | :--- |
| ⬇️ **Download & upload** | Two speed values, in decimal bytes or bits per second. |
| 🎚️ **Steadier numbers** | Independent averaging: off or 2 / 3 / 5 / 10 seconds. Number refresh: 0.5 / 1 / 2 seconds. |
| 📈 **Optional graph** | 15–120 seconds of history, 5–30 fps, separate 0–5 second smoothing, colors, opacity, line width, scale and fill. A 10% fade softens each end. |
| ↔️ **Panel width** | Enable **Resize panel width** and drag the left edge. The right edge stays fixed; **Reset panel width** restores automatic sizing. On a vertical taskbar, use height resizing from the top edge. On a vertical taskbar, the menu changes to height resizing from the top edge. |
| 🔤 **Fonts** | Installed families and styles, preview and private TTF/TTC/compatible OTF imports. |
| 🎨 **Icons** | Flows, Speedometer or your own ICO/PNG files for the tray and app windows. |
| 🔄 **Startup & recovery** | Optional startup at sign-in and indicator recreation after Explorer recreates the taskbar. |

> [!TIP]
> **The background graph can be completely disabled.** Right-click the indicator and clear **Background graph**, or turn it off in **Settings → Graph**. Download/upload numbers remain visible.

## 🖼️ In the taskbar

Actual screenshots from Windows 11.

### With the background graph

<p align="center">
  <img src="docs/images/taskbar-live.png" width="686" alt="Actual NetSpeck screenshot with the background graph enabled">
</p>

### Numbers only

<p align="center">
  <img src="docs/images/taskbar-no-graph-compact.png" width="534" alt="Actual NetSpeck screenshot without a graph, with the arrows close to the speed values">
</p>

## 🪟 Compatibility

> [!NOTE]
> **Public preview:** verified on Windows 11 26H2 **26300.9974 x64**, with the standard horizontal taskbar on the primary monitor. Other Windows 11 builds are unverified; Windows 10 is unsupported.

Taskbar integration uses an unofficial native child-window technique. It does not patch Explorer, inject a DLL or require a taskbar replacement. Windows updates can affect compatibility.

See [known issues](docs/KNOWN_ISSUES.md) for the tested scenarios and remaining limitations.

## 🔎 What the numbers mean

NetSpeck reads local 64-bit interface byte counters. It does not download a test file or measure your plan's maximum speed. Values include any LAN traffic on the chosen adapter.

Automatic mode selects one adapter using the system internet route. VPN and physical-interface traffic are not added together. With split tunneling, select an adapter manually if needed. Route lookup sends no packets.

The primary taskbar is supported. Vertical placement is experimental: it uses a compact stacked layout and a separate height setting, adjusted by dragging the top edge. It has been tested with synthetic geometry and native child windows, but not yet with an actual vertical taskbar. The app uses available space; it does not reserve an official taskbar slot. If no safe free space can be found, the indicator hides temporarily and reports why in the tray/settings.

## 🔒 Preferences and privacy

Settings and a bounded local log are stored in `%LOCALAPPDATA%\NetSpeck`. Imported fonts and icons are copied into its `Fonts` and `Icons` folders. Imported assets are private to this app; system fonts are not bundled.

Upgrading from the development version: close the previous app before starting NetSpeck. Existing preferences and imported assets are copied automatically on first launch without removing the originals. Enabled startup is updated to the new EXE.

There is no telemetry, analytics or automatic upload. Diagnostic exports may include adapter names and a taskbar screenshot; review them before sharing.

## 🛠️ Build from source

Requires Windows x64 and its .NET Framework 4.8 compiler. No NuGet packages or .NET SDK are required.

```powershell
powershell -NoProfile -File .\build.ps1
```

This creates `NetSpeck.exe` and runs **73 offline checks**. Close any running copy before rebuilding. To create a portable ZIP and SHA-256 checksum:

```powershell
powershell -NoProfile -File .\scripts\package.ps1
```

<details>
<summary>🧪 Interactive checks</summary>

Interactive checks require a compatible Windows 11 desktop and an active adapter:

```powershell
powershell -NoProfile -File .\build.ps1 -Integration -Menus
```

These additionally check all four settings tabs in both languages, **80 render cases**, menu/settings behavior and icon imports. They temporarily alter and restore local app preferences. They do not restart Explorer. CI runs only the build and offline checks; a successful CI build is not a compatibility test for its Windows Server runner.

</details>

## 🤝 Contributing

Bug reports and focused pull requests are welcome. Start with [contributing](CONTRIBUTING.md), [release notes](CHANGELOG.md) and [known issues](docs/KNOWN_ISSUES.md).

## 📄 License

[MIT](LICENSE). Copyright © 2026 nt-bro.
