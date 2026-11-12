# NetSpeck 1.0.1 — Windows 11 public preview

- Fixed taskbar free-space calculation: popup menus no longer push the indicator sideways.
- Added an experimental vertical taskbar layout. Values and units stack to fit a narrow panel; enable **Resize panel height** and drag the top edge. Horizontal width is saved separately.
- Added regression checks for menu geometry, both resize directions and multiple DPI settings.

Verified on Windows 11 26H2 26300.9974 x64 with the standard horizontal taskbar. Vertical layout has passed geometry, rendering and native-window tests; actual vertical Explorer integration is not yet verified. Other Windows 11 builds are unverified; Windows 10 is unsupported. The EXE is unsigned.

Download **NetSpeck-v1.0.1-win-x64.zip**, extract it and run `NetSpeck.exe`. Close the previous copy before replacing the EXE. The background graph can be completely disabled in the context menu or settings.

---

## По-русски

- Исправлен расчёт свободного места: всплывающие меню больше не должны сдвигать индикатор в сторону.
- Добавлен экспериментальный вид для вертикальной панели задач. Числа и единицы расположены друг под другом. Для изменения высоты включите **Изменять высоту** и потяните верхний край. Ширина горизонтального индикатора сохраняется отдельно.
- Добавлены проверки геометрии меню, обоих направлений изменения размера и разных масштабов Windows.

Проверено на Windows 11 26H2 26300.9974 x64 со стандартной горизонтальной панелью задач. Для вертикального вида проверены геометрия, отрисовка и нативные окна; встраивание в реальную вертикальную панель Explorer пока не проверено. Другие сборки Windows 11 не проверены; Windows 10 не поддерживается. EXE без цифровой подписи.

Скачайте **NetSpeck-v1.0.1-win-x64.zip**, распакуйте и запустите `NetSpeck.exe`. Перед заменой EXE закройте предыдущую копию. Фоновый график можно полностью отключить через контекстное меню или настройки.
