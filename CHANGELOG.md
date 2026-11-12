# Changelog

## 1.6.0 — public preview — 2026-11-12

First public packaging of the application developed as TaskbarNet 1.6.

- Download/upload numbers embedded in the standard Windows 11 taskbar.
- Separate numeric averaging and graph smoothing, with a three-stage graph filter that suppresses periodic packet-batch ripple.
- Configurable graph history, frame rate, scale, colors, opacity, thickness and fill; 10% fade at each end.
- Left-edge width resizing with a fixed right anchor.
- Installed/custom fonts and two built-in icons, Flows and Speedometer, plus ICO/PNG imports.
- Russian/English interface, system-language detection and explanatory tooltips.
- Optional per-user startup and indicator recreation when Explorer recreates the taskbar.
- Build script, offline tests, CI and portable packaging.

Compatibility is currently verified only on Windows 11 26H2 26300.9974 x64. See [known issues](docs/KNOWN_ISSUES.md), including the unverified sleep/resume scenario.
