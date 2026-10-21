# Окружение и запуск

## Что нужно

- .NET SDK 8.0 или новее (проект целится в `net8.0`; SDK 9/10 тоже подходят).
- Windows, Linux или macOS (Avalonia кроссплатформенная).
- IDE: Visual Studio 2022, JetBrains Rider или VS Code с C# Dev Kit.

Проверка:

```powershell
dotnet --list-sdks
dotnet --version
```

## Сборка и запуск

```powershell
dotnet restore
dotnet build
dotnet run --project src/RecursiveLab.App
```

Открыть решение в IDE: `RecursiveLab.sln`.

## Тесты

```powershell
dotnet test
```

Сейчас зелёные все тесты Core и контрактные тесты алгоритмов (H-дерево,
судоку, ближайшая пара, Ханой). Пропущенных тестов нет.

## Замеры времени

В приложении: страница **«Замеры»** → кнопка **«Замерить»**. Замеры
выполняются в фоне, графики «эксперимент + теория» обновляются автоматически.

Консольный стенд (методика как в лабораторной №1):

```powershell
dotnet run --project benchmarks/RecursiveLab.Benchmarks
```

Скрипт пишет `benchmarks/results/htree.csv`, `hanoi.csv`, `sudoku.csv`,
`closest-pair.csv`, `summary.md` и графики в `docs/charts/`.
Подробности по ближайшей паре — в
[CLOSEST-PAIR-REPORT.md](CLOSEST-PAIR-REPORT.md).

## Скриншоты без окна

```powershell
dotnet run --project src/RecursiveLab.Smoke -- artifacts/screenshots
```

Smoke-проект использует `Avalonia.Headless` + Skia: создаёт окно, переключает
все задачи и сохраняет PNG. Полезно, чтобы быстро проверить вёрстку после
изменений. На Linux может понадобиться X-сервер или `xvfb-run`.

## Обёртка dotnet для ограниченных сред

В некоторых песочницах `dotnet` не может писать в `%USERPROFILE%` и в
`AppData`. Тогда используйте:

```powershell
.\scripts\dotnet.ps1 build
.\scripts\dotnet.ps1 run --project src/RecursiveLab.App
```

Скрипт перенаправляет `DOTNET_CLI_HOME` и `NUGET_PACKAGES` в папки репозитория
(`.dotnet/`, `.nuget/`), которые уже в `.gitignore`.

## Типовые проблемы

| Проблема | Решение |
|----------|---------|
| `Не удалось загрузить индекс служб...` / NU1301 | Нет доступа к nuget.org. Проверьте интернет/VPN или используйте кэш пакетов. |
| `AvaloniaStatsTask ... Access is denied` | Ограниченная песочница. Запустите через `scripts/dotnet.ps1` или с полным доступом. |
| Приложение не стартует на Linux | Установите зависимости Avalonia (`libx11`, `libice`, `libsm`, `fontconfig`). |
| Кнопка «Открыть файл» ничего не делает | Проводник Windows; на Linux/macOS кнопка просто ничего не делает (ошибка не пробрасывается). |
| `NETSDK1047 ... net8.0/win-x64` | Не запускайте `dotnet test --arch x64`; используйте обычный `dotnet test`. |
| Шрифты выглядят иначе | Inter встроен в приложение; сторонние шрифты не нужны. |

## Как добавить новую задачу

1. Модели и алгоритм — в `src/RecursiveLab.Core/Tasks/<НоваяЗадача>/`.
2. Страница — `src/RecursiveLab.App/Views/Pages/<НоваяЗадача>Page.axaml(.cs)`;
   используйте существующие стили `Border.card`, `TextBlock.section`, `Button.primary`.
3. Зарегистрируйте задачу в `src/RecursiveLab.App/Services/LabTaskCatalog.cs`
   (название, автор, сложность, статус, цвет, фабрика страницы).
4. Тесты — в `tests/RecursiveLab.Tests/`.
5. Документация — `docs/TASK-<НОВАЯ>.md`.

Каркас специально сделан так, чтобы новая страница не требовала правок окна
и навигации: достаточно одной записи в каталоге.
