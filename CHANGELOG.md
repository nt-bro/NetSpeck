# Changelog

## 1.0.2 — public preview — 2026-11-12

- Keep the last verified indicator position while an Explorer popup temporarily withdraws the taskbar accessibility tree.
- Allow a short recovery interval after a popup closes; persistent failures, confirmed occupied space, changed geometry and changed sizes still invalidate the old position.
- Added 14 regression checks for popup lifetime, layout recovery, collisions, DPI and native popup detection.

## 1.0.1 — public preview — 2026-11-12

- Exclude popup menus and owned windows from taskbar free-space calculation to prevent sideways jumps when opening a menu nearby.
- Experimental vertical taskbar layout with stacked values and separate top-edge height resizing. Horizontal width is preserved independently.
- Discard cached resize placement when taskbar geometry or DPI changes.
- Regression coverage: 73 offline checks, 119 behavior checks, 80 horizontal and 48 vertical render cases. Actual vertical taskbar integration remains unverified.

## 1.0.0 — public preview — 2026-11-12

First public release under the NetSpeck name. Existing development-version preferences, imported fonts/icons and enabled startup are migrated automatically.

- Download/upload numbers embedded in the standard Windows 11 taskbar.
- Separate numeric averaging and graph smoothing, with a three-stage graph filter that suppresses periodic packet-batch ripple.
- Configurable graph history, frame rate, scale, colors, opacity, thickness and fill; 10% fade at each end.
- Left-edge width resizing with a fixed right anchor.
- Installed/custom fonts and two built-in icons, Flows and Speedometer, plus ICO/PNG imports.
- Russian/English interface, system-language detection and explanatory tooltips.
- Optional per-user startup and indicator recreation when Explorer recreates the taskbar.
- Build script, offline tests, CI and portable packaging.

Compatibility is currently verified only on Windows 11 26H2 26300.9974 x64. See [known issues](docs/KNOWN_ISSUES.md).
