# NetSpeck 1.0.2 — Windows 11 public preview

Fixes the indicator disappearing when Explorer temporarily withdraws its taskbar accessibility tree during a popup menu. The app keeps the last verified position while the menu is open and rechecks placement after it closes. Confirmed occupied space, taskbar geometry changes and size changes still invalidate the previous position.

The original right-click scenario was verified on Windows 11 26H2 26300.9974 x64.

Validation: 73 offline checks, 133 behavior checks and 128 rendering cases. Vertical taskbar placement remains experimental; other Windows 11 builds are unverified. Windows 10 is unsupported. The EXE is unsigned.

Download **NetSpeck-v1.0.2-win-x64.zip**, extract it and run `NetSpeck.exe`. Close the previous copy before replacing its EXE. Your preferences are retained. The background graph can be completely disabled in the menu or settings.

---

## По-русски

Исправлено исчезновение индикатора, когда Explorer временно перестаёт предоставлять элементы панели задач при открытии всплывающего меню. Приложение сохраняет последнее проверенное положение на время меню и заново проверяет его после закрытия. Если место действительно занято, изменилась геометрия панели или её размер, старое положение не используется.

Проверки: 73 автономные, 133 проверки поведения и 128 вариантов отрисовки. Вертикальное размещение пока экспериментальное; другие сборки Windows 11 не проверены. Windows 10 не поддерживается. EXE без цифровой подписи.

Скачайте **NetSpeck-v1.0.2-win-x64.zip**, распакуйте и запустите `NetSpeck.exe`. Перед заменой EXE закройте предыдущую копию. Настройки сохраняются. Фоновый график можно полностью отключить в меню или настройках.
