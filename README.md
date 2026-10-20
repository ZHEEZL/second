# Лабораторная работа №2 — Рекурсивные алгоритмы

[![build-and-test](https://github.com/ZHEEZL/second/actions/workflows/ci.yml/badge.svg)](https://github.com/ZHEEZL/second/actions/workflows/ci.yml)

Оконное приложение на C# для лабораторной работы №2
«Рекурсивные алгоритмы». У каждой задачи своя страница и **свой файл с алгоритмом**: участники не мешают
друг другу и не конфликтуют в git.

![H-дерево](docs/screenshots/h-tree.png)

## Задачи

| Задача | Автор      | Статус | Файл алгоритма |
|--------|------------|--------|----------------|
| H-дерево (фрактал) | команда    | готово | `src/RecursiveLab.Core/Tasks/HTree/HTreeGenerator.cs` |
| Судоку | участник 2 | готово | `src/RecursiveLab.Core/Tasks/Sudoku/BacktrackingSudokuSolver.cs` |
| Ближайшая пара точек | участник 1 | готово | `src/RecursiveLab.Core/Tasks/ClosestPair/DivideAndConquerClosestPair.cs` |
| Ханойские башни | команда    | готово | `src/RecursiveLab.Core/Tasks/Hanoi/RecursiveHanoiSolver.cs`, `StackHanoiSolver.cs` |


## Соответствие лабораторной (PDF)

| Пункт задания из PDF | Реализация | Статус |
|----------------------|------------|--------|
| Задание 1. Отрисовка фрактала с графическим интерфейсом, рекурсивный алгоритм | H-дерево: `HTreeGenerator.cs`, `HTreePage.axaml` | готово |
| Задание 2. «Ханойские башни» с графическим интерфейсом | `HanoiPage.axaml`, `RecursiveHanoiSolver.cs` | готово |
| Доп. задание. Ханойские башни через стек | `StackHanoiSolver.cs` | готово |
| Сверх задания команды: судоку (backtracking) | `BacktrackingSudokuSolver.cs`, `SudokuPage.axaml` | готово |
| Сверх задания команды: ближайшая пара (divide and conquer) | `DivideAndConquerClosestPair.cs`, `ClosestPairPage.axaml` | готово |

## Что умеет приложение

- **H-дерево.** Глубина 0–6, анимация «рисования по рекурсии» (кнопка
  «Построить»), пауза, скорость, счётчики H-фигур/отрезков, время генерации.
  Цвет и толщина линий показывают уровень рекурсии.
- **Судоку.** Ввод с клавиатуры, подсветка строки/столбца/квадрата, поиск
  конфликтов, скорость, готовые условия. Кнопка
  «Решить» заполняет доску, а панель показывает число шагов/вызовов/время.
- **Ближайшая пара.** Генерация случайных точек (10–2000), анимация шагов
  (разбиение, полоса, смена лучшей пары) с ползунком скорости, кнопка
  «Перебор (эталон)» для проверки ответа, подсветка найденной пары.
- **Ханойские башни.** Выбор числа колец, режим «рекурсия» / «стек»,
  анимация ходов, скорость, счётчики ходов и времени.

## Быстрый старт

Нужен .NET 8 SDK (или новее) и любая IDE: Visual Studio 2022, Rider или VS Code.

```powershell
git clone https://github.com/ZHEEZL/second.git
cd second

dotnet restore
dotnet run --project src/RecursiveLab.App
```

Если `dotnet` в вашей среде не может писать в профиль пользователя
(например, ограниченная песочница), используйте обёртку:

```powershell
.\scripts\dotnet.ps1 run --project src/RecursiveLab.App
```

Тесты:

```powershell
dotnet test
```

Скриншоты без реального окна (headless-рендер):

```powershell
dotnet run --project src/RecursiveLab.Smoke -- artifacts/screenshots
```


## Структура

```text
RecursiveLab.sln
├── src/
│   ├── RecursiveLab.Core/        # алгоритмы и модели, без UI — можно тестировать отдельно
│   │   ├── Geometry/
│   │   └── Tasks/
│   │       ├── HTree/            # H-дерево (готово)
│   │       ├── Sudoku/           # backtracking с MRV (готово)
│   │       ├── ClosestPair/      # divide and conquer + эталон (готово)
│   │       └── Hanoi/            # рекурсия и стек (готово)
│   ├── RecursiveLab.App/         # Avalonia: окно, навигация, тема, страницы
│   └── RecursiveLab.Smoke/       # headless-скриншоты для проверки вёрстки
├── tests/RecursiveLab.Tests/     # xUnit-тесты + контрактные тесты для участников
├── docs/                         # планы, задания участникам, скриншоты
└── scripts/dotnet.ps1            # dotnet с локальными кэшами (для песочниц)
```

## Скриншоты

| H-дерево | Судоку |
|----------|--------|
| ![](docs/screenshots/h-tree.png) | ![](docs/screenshots/sudoku.png) |

| Ближайшая пара | Ханойские башни |
|----------------|-----------------|
| ![](docs/screenshots/closest-pair.png) | ![](docs/screenshots/hanoi.png) |
