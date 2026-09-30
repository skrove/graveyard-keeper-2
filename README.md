# Keeper’s Codex / Кодекс смотрителя

**A practical companion for Graveyard Keeper 2 — from your first ingredient to the finished craft.**

Search items, trace crafting chains, calculate raw materials, explore alchemy, and look up quests, traders, maps and skill trees. Available in English and Russian, in your browser or as a portable Windows app.

**[Open in your browser](https://skrove.github.io/graveyard-keeper-2/) · [Download for Windows](https://github.com/skrove/graveyard-keeper-2/releases/latest/download/KeepersCodex.exe) · [All releases](https://github.com/skrove/graveyard-keeper-2/releases)**

## Что умеет справочник

Неофициальный помощник для Graveyard Keeper 2. Помогает узнать, где взять предмет, сколько ресурсов нужно для крафта и что понадобится для следующего шага в игре.

- Поиск предметов и рецептов, источники добычи и цены торговцев.
- Цепочки производства и расчёт ресурсов с учётом запасов, введённых вручную.
- Станки, постройки, технологии, перки и деревья талантов.
- Алхимические формулы и подбор смесей из доступных ингредиентов.
- Квесты, персонажи, рыбалка, карты локаций и справочные таблицы.
- Переключение EN/RU с сохранением языка и плана в приложении или браузере.

## Windows app / Приложение для Windows

Download **KeepersCodex.exe** from [Releases](https://github.com/skrove/graveyard-keeper-2/releases/latest) and double-click it. No installer or administrator access is required. The database, artwork and fonts are bundled, so the companion works offline.

Скачайте **KeepersCodex.exe** из [последнего выпуска](https://github.com/skrove/graveyard-keeper-2/releases/latest) и запустите двойным щелчком. Установка и права администратора не нужны. База, изображения и шрифты находятся внутри одного файла, поэтому справочник работает без интернета. Внешние ссылки открываются в обычном браузере.

- **Platform:** Windows 10/11, x64; .NET Framework 4.x. The application uses Microsoft Edge WebView2 when available.
- **Fallback:** if WebView2 is unavailable, the same companion opens in your default browser. Keep its small launcher window open while using it.
- **Settings:** `%LOCALAPPDATA%\KeepersCodex`. Browser and desktop settings are separate.
- **Options:** `--browser` opens browser mode; `--devtools` enables developer tools.
- **Signature:** this release is unsigned. Windows may display a reputation warning. The release includes SHA-256 checksums for verifying the downloaded file.

Если WebView2 недоступен, справочник откроется в браузере; маленькое окно запуска должно оставаться открытым. Настройки хранятся в `%LOCALAPPDATA%\KeepersCodex`. EXE не подписан цифровым сертификатом, поэтому Windows может показать предупреждение о репутации файла.

## Run the website locally / Локальный запуск сайта

Download **Code → Download ZIP**, extract the archive and run the following command from its folder with Python 3:

```sh
python -m http.server 8099 --bind 127.0.0.1
```

Alternatively, on Windows:

```powershell
.\scripts-serve.ps1
```

Open **http://localhost:8099/**. Stop the server with **Ctrl+C**. No npm packages or build step are needed. The web version loads Google Fonts online and falls back to system fonts without a connection.

## Source files / Исходники

| Path | Contents |
|---|---|
| `index.html` | Interface and crafting calculator |
| `db.js`, `ru.js`, `guides.js` | Bundled database, Russian translation and reference tables |
| `atlas.png`, `img/` | Item icons, building artwork and location maps |
| `app/Program.cs`, `app/app.manifest` | Windows application source and manifest |
| `app/app.ico`, `app/fonts/`, `app/licenses/` | Application icon, offline fonts and third-party notices |
| `build-exe.ps1` | Windows build and built-in smoke test |
| `scripts-serve.ps1` | Local development server |

Intermediate downloads, raw wiki exports, translation working files, build output and local caches are not part of the published repository. Windows executables are distributed through Releases.

## Build the Windows app / Сборка EXE

Requires 64-bit Windows, the .NET Framework compiler at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, and WebView2 Runtime for the built-in test.

Download the [Microsoft.Web.WebView2 1.0.4258.31 NuGet package](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31), open it as a ZIP archive and copy these files into `build/sdk/`:

- `lib/net462/Microsoft.Web.WebView2.Core.dll`
- `lib/net462/Microsoft.Web.WebView2.WinForms.dll`
- `runtimes/win-x64/native/WebView2Loader.dll`

Then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build-exe.ps1
```

The script bundles the website and local fonts, compiles the app, and checks all views plus English/Russian switching in a separate test profile. It writes `dist/KeepersCodex.exe` only after the test passes. Test results and a screenshot are saved as `build/selftest.json` and `build/selftest.json.png`.

## Data and limitations / Данные и ограничения

The bundled dataset is a snapshot dated **2026-09-22**, obtained through [Wikily](https://wikily.gg/graveyard-keeper-2). It may differ from later game patches. The Russian translation is unofficial; English item names are also shown for reference.

Снимок данных — **22 сентября 2026 года**. После обновлений игры рецепты и значения могут отличаться. Русский перевод неофициальный.

- Alchemy can have many valid ingredient combinations; the calculator treats potions as raw materials by default.
- Garden yields depend on beds and fertilizers and are not expanded into raw materials.
- Alternative ingredients stay grouped where the data lists them together.
- Trader prices can depend on tier and reputation.
- Inventory is entered manually; the app does not read game saves.

## Credits / Авторы материалов

Graveyard Keeper 2 game data and artwork belong to their respective rights holders, including Lazy Bear Games / tinyBuild. This is an unofficial fan project, not affiliated with or endorsed by the developers or publisher. Publication of this repository does not grant rights to the game materials.

The Windows app includes Microsoft WebView2 SDK components and the IM Fell English, IM Fell English SC and Archivo fonts. Their notices and licenses are in [app/licenses](app/licenses).

## Feedback and support / Обратная связь и поддержка

Found an incorrect recipe, translation or calculation? [Open an issue](https://github.com/skrove/graveyard-keeper-2/issues) with the item name, expected result and steps to reproduce.

Нашли ошибку? [Создайте issue](https://github.com/skrove/graveyard-keeper-2/issues): укажите предмет, ожидаемый результат и шаги воспроизведения.

The companion is free to use. / Справочник доступен бесплатно.

**[Support the project on Boosty / Поддержать проект](https://boosty.to/skrover/donate)**