using System.Globalization;
using RecursiveLab.Core.Tasks.Sudoku;
using SkiaSharp;

namespace RecursiveLab.App.Services;

/// <summary>Что напечатать на листе.</summary>
public sealed record SudokuPdfOptions(string Title, string Subtitle, bool IncludeAnswers)
{
    /// <summary>Стандартная подпись: сложность, подсказки, единственность, зерно.</summary>
    public static SudokuPdfOptions For(SudokuPuzzleResult result, string difficultyLabel, bool includeAnswers)
    {
        ArgumentNullException.ThrowIfNull(result);

        var removedPercent = (result.RemovedShare * 100).ToString("0", CultureInfo.InvariantCulture);
        var solutions = result.SolutionCount == 1 ? "решение единственное" : $"решений: {result.SolutionCount}+";
        var subtitle =
            $"Сложность: {difficultyLabel} · подсказок {result.ActualClues} из 81 (убрано {removedPercent}%) · {solutions} · № {result.Seed}";

        return new SudokuPdfOptions("Судоку", subtitle, includeAnswers);
    }
}

/// <summary>
/// Рисует задачу (и, по желанию, ответы на второй странице) в PDF формата A4.
/// Используется SkiaSharp — он уже есть в проекте как графический движок Avalonia,
/// новых пакетов и лицензий не нужно. Экспортёр пишет в любой Stream, поэтому
/// не зависит от диалогов и окон.
/// </summary>
public static class SudokuPdfExporter
{
    // PDF меряет в пунктах: 1 pt = 1/72 дюйма. A4 = 210 × 297 мм ≈ 595 × 842 pt.
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float GridSize = 450f;
    private const float CellSize = GridSize / SudokuBoard.Size;
    private const float GridLeft = (PageWidth - GridSize) / 2f;
    private const float GridTop = 150f;

    public static void Export(SudokuPuzzleResult result, Stream output, SudokuPdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);

        var regular = ResolveTypeface(SKFontStyle.Normal);
        var bold = ResolveTypeface(SKFontStyle.Bold);

        using (var document = SKDocument.CreatePdf(output)
                              ?? throw new InvalidOperationException("Не удалось создать PDF-документ."))
        {
            var puzzleFooter = options.IncludeAnswers
                ? "Ответы — на следующей странице."
                : "Сгенерировано RecursiveLab: рекурсивный перебор с проверкой единственности решения.";

            DrawPage(document, result.Puzzle, solution: null, options.Title, options.Subtitle, puzzleFooter, regular, bold);

            if (options.IncludeAnswers)
            {
                DrawPage(
                    document,
                    result.Puzzle,
                    result.Solution,
                    options.Title + " — ответы",
                    options.Subtitle,
                    "Жирные цифры — подсказки из условия, серые — найденные ответы.",
                    regular,
                    bold);
            }

            document.Close(); // Без Close PDF получится неполным.
        }

        output.Flush();
    }

    private static void DrawPage(
        SKDocument document,
        SudokuBoard puzzle,
        SudokuBoard? solution,
        string title,
        string subtitle,
        string footer,
        SKTypeface regular,
        SKTypeface bold)
    {
        var canvas = document.BeginPage(PageWidth, PageHeight);

        using var titlePaint = TextPaint(bold, 26f, SKColors.Black);
        using var subtitlePaint = TextPaint(regular, 10.5f, new SKColor(90, 90, 90));
        using var givenPaint = TextPaint(bold, 28f, SKColors.Black);
        using var answerPaint = TextPaint(regular, 28f, new SKColor(120, 120, 120));
        using var footerPaint = TextPaint(regular, 9.5f, new SKColor(110, 110, 110));

        DrawCentered(canvas, title, PageWidth / 2f, 90f, titlePaint);
        DrawCentered(canvas, subtitle, PageWidth / 2f, 118f, subtitlePaint);

        DrawGrid(canvas);

        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            var given = puzzle[index];
            var value = given != 0 ? given : solution?[index] ?? 0;
            if (value == 0)
            {
                continue;
            }

            var (row, column) = SudokuBoard.ToRowColumn(index);
            var paint = given != 0 ? givenPaint : answerPaint;
            var centerX = GridLeft + column * CellSize + CellSize / 2f;
            // Базовая линия цифры: центр клетки + примерно треть кегля (высота цифр ≈ 0,7 кегля).
            var baseline = GridTop + row * CellSize + CellSize / 2f + paint.TextSize * 0.35f;

            DrawCentered(canvas, value.ToString(CultureInfo.InvariantCulture), centerX, baseline, paint);
        }

        DrawCentered(canvas, footer, PageWidth / 2f, GridTop + GridSize + 40f, footerPaint);

        document.EndPage();
    }

    private static void DrawGrid(SKCanvas canvas)
    {
        using var thin = LinePaint(0.6f, new SKColor(150, 150, 150));
        using var thick = LinePaint(2f, SKColors.Black);

        // Сначала тонкие, потом толстые — чтобы толстые лежали сверху.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i <= SudokuBoard.Size; i++)
            {
                var isThick = i % 3 == 0;
                if (isThick != (pass == 1))
                {
                    continue;
                }

                var paint = isThick ? thick : thin;
                var offset = i * CellSize;

                canvas.DrawLine(GridLeft + offset, GridTop, GridLeft + offset, GridTop + GridSize, paint);
                canvas.DrawLine(GridLeft, GridTop + offset, GridLeft + GridSize, GridTop + offset, paint);
            }
        }
    }

    private static void DrawCentered(SKCanvas canvas, string text, float centerX, float baseline, SKPaint paint)
    {
        var width = paint.MeasureText(text);
        canvas.DrawText(text, centerX - width / 2f, baseline, paint);
    }

    private static SKPaint TextPaint(SKTypeface typeface, float size, SKColor color) => new()
    {
        IsAntialias = true,
        Color = color,
        Typeface = typeface,
        TextSize = size,
    };

    private static SKPaint LinePaint(float width, SKColor color) => new()
    {
        IsAntialias = true,
        Color = color,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        StrokeCap = SKStrokeCap.Square,
    };

    /// <summary>
    /// Шрифт с кириллицей: «Судоку», «Сложность». Перебираем распространённые
    /// семейства; FromFamilyName при неудаче молча отдаёт шрифт по умолчанию,
    /// поэтому сверяем имя. Последний шанс — шрифт, в котором точно есть буква «Ж».
    /// </summary>
    private static SKTypeface ResolveTypeface(SKFontStyle style)
    {
        string[] families = ["Arial", "Segoe UI", "DejaVu Sans", "Liberation Sans", "Noto Sans"];

        foreach (var family in families)
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface is not null &&
                string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            {
                return typeface;
            }
        }

        return SKFontManager.Default.MatchCharacter('Ж') ?? SKTypeface.Default;
    }
}
