# Задача: судоку (перебор с возвратом)

**Статус:** UI готов, алгоритм — заглушка. **Автор:** участник 2.
**Ветка:** `feature/sudoku`.

## Файлы

- Заглушка и точка входа: `src/RecursiveLab.Core/Tasks/Sudoku/BacktrackingSudokuSolver.cs`
- Модель доски: `src/RecursiveLab.Core/Tasks/Sudoku/SudokuBoard.cs`
- Контракт: `src/RecursiveLab.Core/Tasks/Sudoku/SudokuModels.cs`
- Страница: `src/RecursiveLab.App/Views/Pages/SudokuPage.axaml(.cs)`
- Тест: `tests/RecursiveLab.Tests/SolverContractTests.cs` (снять `Skip`)

## Контракт

```csharp
SudokuSolveResult Solve(
    SudokuBoard board,
    Action<SudokuStep>? onStep = null,
    CancellationToken cancellationToken = default);

public sealed record SudokuSolveResult(
    bool Solved,
    SudokuBoard? Board,
    int Steps,
    long RecursiveCalls,
    TimeSpan Elapsed);
```

Требования:

- **Исходная доска не изменяется** — работайте с `board.Clone()`.
- Если решения нет — `Solved = false`, `Board = null` (для валидного судоку
  решение всегда есть, но проверка пригодится).
- `onStep` можно не вызывать, но лучше вызывать: UI задуман так, чтобы позже
  проигрывать шаги.
- `Steps` — сколько шагов сделали, `RecursiveCalls` — сколько раз вошли в
  рекурсивную функцию, `Elapsed` — время работы.

`SudokuStepKind`:

| Kind | Когда вызывать | Что делает UI |
|------|----------------|---------------|
| `Try` | проверили кандидата | подсветить клетку |
| `Place` | поставили значение | показать значение |
| `Remove` | откатили значение | очистить клетку |
| `Solved` | нашли решение | финальный статус |

## Как реализовывать (план, не код)

1. Найти пустую клетку (0). Можно первую попавшуюся, можно MRV — клетку с
   наименьшим числом кандидатов.
2. Если пустых нет — проверить доску и вернуть `true`.
3. Для каждого кандидата 1..9:
   - если `work.IsValidPlacement(index, value)`;
   - поставить (`work.TrySet`), вызвать `onStep(Place)`, увеличить `Steps`;
   - уйти в рекурсию; если вернула `true` — выйти наверх;
   - иначе откатить (`work.TryClear`), вызвать `onStep(Remove)`.
4. Если ни один кандидат не подошёл — вернуть `false` (backtracking).
5. На успехе вызвать `onStep(Solved)` и вернуть копию решённой доски.

Дополнительно: `SudokuBoard.GetCandidates(index)` и
`SudokuBoard.FindConflicts()` уже реализованы — можно использовать.

## Приёмка

- [ ] Кнопка «Решить судоку» заполняет доску для всех 4 условий из списка.
- [ ] Нет исключения «заглушка»; на панели появляются шаги/вызовы/время.
- [ ] «Проверить» и ручной ввод продолжают работать.
- [ ] Тест `SudokuSolver_SolvesClassicPuzzle` без `Skip` зелёный.
- [ ] Ветка собрана без предупреждений, PR просмотрен.

## Подсказки

- Сложность в худшем случае `O(9^m)`, где `m` — число пустых клеток; на
  практике с проверкой кандидатов и MRV решается мгновенно.
- Не используйте готовые библиотеки судоку — преподаватель смотрит на рекурсию.
- Счётчик вызовов удобно вести в замыкании или поле класса.
