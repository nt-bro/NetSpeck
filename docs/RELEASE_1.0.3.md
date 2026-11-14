# NetSpeck 1.0.3 — Windows 11 public preview

Fixed excessive memory growth during long-running taskbar monitoring. Native cached UI Automation snapshots replace the legacy managed proxy factory; COM references are released explicitly on a dedicated background thread.

On the tested Windows 11 26H2 26300.9974 x64 system:

- 2,000 accelerated taskbar scans: retained private memory growth dropped from **30.75 MiB to 0.80–1.58 MiB**.
- 10,000 graph redraws: **0.32–0.39 MiB** retained growth, with no increase in handles.
- A six-minute live run with the graph enabled remained around **47–48 MiB working set**.
- Offline, menu/settings, layout and rendering regression checks pass. A multi-day run of the new version has not yet been completed.

Download **NetSpeck-v1.0.3-win-x64.zip**, close the previous copy, replace its files and run `NetSpeck.exe`. Preferences are retained. The EXE is unsigned. Vertical placement remains experimental; Windows 10 is unsupported.

---

## По-русски

Исправлен чрезмерный рост памяти при длительной работе. Заменён способ опроса панели задач; служебные ресурсы Windows теперь освобождаются явно в отдельном фоновом потоке.

На проверенной Windows 11 26H2 26300.9974 x64:

- После 2000 ускоренных опросов рост удерживаемой памяти снизился с **30,75 до 0,80–1,58 МиБ**.
- После 10 000 перерисовок графика — **0,32–0,39 МиБ**, без роста числа дескрипторов.
- За шесть минут обычной работы с включённым графиком занятая физическая память оставалась около **47–48 МиБ**.
- Пройдены проверки расчётов, меню, настроек, размещения и отрисовки. Многосуточный прогон новой версии пока не завершён.

Скачай **NetSpeck-v1.0.3-win-x64.zip**, закрой предыдущую копию, замени файлы и запусти `NetSpeck.exe`. Настройки сохранятся. EXE без цифровой подписи. Вертикальное размещение пока экспериментальное; Windows 10 не поддерживается.
