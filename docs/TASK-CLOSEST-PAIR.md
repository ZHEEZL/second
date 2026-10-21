# Задача: ближайшая пара точек (divide and conquer)

**Статус:** готово (алгоритм + анимация шагов). **Автор:** участник 1.
**Ветка:** `feature/closest-pair`.

> Полный отчёт с теорией, замерами и графиками:
> [CLOSEST-PAIR-REPORT.md](CLOSEST-PAIR-REPORT.md).

## Файлы

- Алгоритм: `src/RecursiveLab.Core/Tasks/ClosestPair/DivideAndConquerClosestPair.cs`
- Контракт: `src/RecursiveLab.Core/Tasks/ClosestPair/ClosestPairModels.cs`
- Эталонный перебор (не сдаваемый): `src/RecursiveLab.Core/Tasks/ClosestPair/ClosestPairReference.cs`
- Страница: `src/RecursiveLab.App/Views/Pages/ClosestPairPage.axaml(.cs)`
- Тест: `tests/RecursiveLab.Tests/ClosestPairReferenceTests.cs` (включён)

## Контракт

```csharp
ClosestPairResult Find(
    IReadOnlyList<PointD> points,
    Action<ClosestPairStep>? onStep = null,
    CancellationToken cancellationToken = default);

public sealed record ClosestPairResult(
    PointD A, PointD B, double Distance,
    long DistanceComparisons, long RecursiveCalls, TimeSpan Elapsed);
```

Требования:

- Точек меньше двух — `ArgumentException`.
- Возвращается именно **пара точек**, а не только расстояние.
- `DistanceComparisons` — сколько раз реально сравнивали расстояния.
- `onStep` — для анимации: `Split` (разбили по X), `BaseCase`,
  `Strip` (смотрим полосу), `BestChanged`, `Done`. UI собирает шаги и
  проигрывает их с выбранной скоростью.

## Алгоритм

1. Отсортировать точки по X (при равенстве — по Y).
2. Рекурсивно решить левую и правую половины (для маленьких частей ≤ 3 точек —
   перебор).
3. `d = min(dLeft, dRight)`.
4. Взять точки в полосе `|x − midX| < d`, отсортировать по Y.
5. Для каждой точки проверить не более 7 следующих: если `dy ≥ d`, дальше
   можно не смотреть.
6. Вернуть лучшую пару.

Сложность: `T(n) = 2·T(n/2) + O(n)` ⇒ `O(n log n)` (сортировка полосы на
каждом уровне даёт `O(n log² n)`, если сортировать заново; это тоже
приемлемо, но лучше один раз отсортировать по Y и делить).

## Эталон для проверки

`ClosestPairReference.FindBruteForce` — честный перебор `O(n²)`. В кнопке
«Перебор (эталон)» он уже подключён. Свой алгоритм должен давать **то же
расстояние** (пары могут быть разными при равных расстояниях). Использовать
перебор внутри своего решения нельзя — это будет видно по числу сравнений.

## Замеры

Стенд: `benchmarks/RecursiveLab.Benchmarks` (консоль, .NET 8, ScottPlot).

```powershell
dotnet run --project benchmarks/RecursiveLab.Benchmarks
```

Результаты: `benchmarks/results/closest-pair.csv` и `summary.md`;
графики — `docs/charts/closest-pair-*.png`.
Методика как в лабораторной №1: фиксированный seed, прогрев, 5 повторов,
медиана, CSV и графики. Те же графики доступны в приложении на странице
«Замеры» (кнопка «Замерить» пересчитывает данные на месте).

## Приёмка

- [x] Тест `DivideAndConquer_MatchesBruteForceOnRandomSets` без `Skip` зелёный.
- [x] На 120 точках ответ совпадает с эталоном; на 2000 точках работает
      заметно быстрее перебора (по `DistanceComparisons`).
- [x] Кнопка «Найти ближайшую пару» подсвечивает пару и заполняет статистику.
- [x] Крайние случаи: 2 точки, совпадающие точки, все точки на одной прямой.
- [ ] Ветка без предупреждений, PR просмотрен.

## Подсказки

- `PointD.DistanceSquaredTo` — сравнивайте квадраты расстояний, корень берите
  один раз в конце (меньше вычислений).
- Не мутируйте входной список: скопируйте в массив.
- `ClosestPairStep` умеет передавать `X0/X1` для полосы и `A/B` для пары —
  пригодится, если захотите анимацию.
