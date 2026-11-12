# Known issues / Известные ограничения

This is a public preview, not a stable compatibility release.

| Area | Status |
| --- | --- |
| Windows | Verified on Windows 11 26H2 build 26300.9974 x64 only. Other Windows 11 builds are unverified; Windows 10 is unsupported. |
| Taskbar | Uses an unofficial native child-window integration with the primary taskbar. It occupies free space rather than an officially reserved slot. Windows updates can break compatibility. |
| Vertical taskbar | Experimental compact layout with independent top-edge height resizing. Geometry, rendering and native child-window resizing are tested; an actual vertical Explorer taskbar has not been verified. |
| Taskbar context menu | Original right-click scenario was confirmed on the tested system: the indicator remains visible and updates while the menu is open. |
| Explorer restart | Recovery was observed after an actual Explorer restart on the tested system. |
| Scaling | Actual display scales 100%, 125%, 150% and 200% were checked during development; rendering is also tested at 96/120/144/192 DPI. |
| Theme and layout | Light/dark render cases pass. Actual theme switching, centered taskbar layout, taskbar overflow, auto-hide, multiple monitors and physical network/VPN transitions are not fully verified. |
| File icon | The EXE's file icon is Flows. The app preference changes tray and window icons. |
| Graph | Strong smoothing intentionally delays the graph response. Real sustained speed changes remain visible. |
| Signature | The portable EXE is unsigned. |

## Test coverage

The current offline suite has 73 checks. Interactive development checks passed 133 behavior checks, both languages across four settings tabs, and 128 render cases (48 vertical). These checks do not prove compatibility with every Windows 11 build.

The resize test drives the same pointer handlers on a real HWND without taking global mouse capture. Horizontal and vertical drag handlers preserve their fixed end and clamp to available space.

## Кратко по-русски

Подтверждена только сборка Windows 11 26H2 26300.9974 x64. Встраивание в панель задач не является официальным API расширения и зависит от Windows.

Восстановление после реального перезапуска Explorer было проверено и прошло.

Если сообщаете об ошибке, укажите сборку Windows, масштаб, язык и расположение панели задач. Удалите личные данные из журналов и снимков перед публикацией.
