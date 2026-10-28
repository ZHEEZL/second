using System.Globalization;
using RecursiveLab.Core.Tasks.Sudoku;
using SkiaSharp;

namespace RecursiveLab.App.Services;

/// <summary>Что и как напечатать.</summary>
/// <param name="Title">Заголовок страниц с задачами.</param>
/// <param name="Subtitle">Строка под заголовком (сложность, сколько задач…).</param>
/// <param name="IncludeAnswers">Добавить в конец страницы с ответами (компактно, по 12 на листе).</param>
/// <param name="PuzzlesPerPage">Сколько задач на одном листе: 1, 2, 4 или 6.</param>
public sealed record SudokuPdfOptions(string Title, string Subtitle, bool IncludeAnswers, int PuzzlesPerPage = 1)
{
    /// <summary>Подпись для одной задачи: сложность, подсказки, единственность, зерно.</summary>
    public static SudokuPdfOptions For(SudokuPuzzleResult result, string difficultyLabel, bool includeAnswers)
    {
        ArgumentNullException.ThrowIfNull(result);

        var removedPercent = (result.RemovedShare * 100).ToString("0", CultureInfo.InvariantCulture);
        var solutions = result.SolutionCount == 1 ? "решение единственное" : $"решений: {result.SolutionCount}+";
        var subtitle =
            $"Сложность: {difficultyLabel} · подсказок {result.ActualClues} из 81 (убрано {removedPercent}%) · {solutions} · № {result.Seed}";

        return new SudokuPdfOptions("Судоку", subtitle, includeAnswers);
    }

    /// <summary>Подпись для подборки из нескольких задач.</summary>
    public static SudokuPdfOptions ForCollection(
        IReadOnlyList<SudokuPuzzleResult> puzzles,
        string difficultyLabel,
        bool includeAnswers,
        int puzzlesPerPage)
    {
        ArgumentNullException.ThrowIfNull(puzzles);

        if (puzzles.Count == 1)
        {
            return For(puzzles[0], difficultyLabel, includeAnswers) with { PuzzlesPerPage = puzzlesPerPage };
        }

        var minClues = puzzles.Min(p => p.ActualClues);
        var maxClues = puzzles.Max(p => p.ActualClues);
        var clues = minClues == maxClues ? $"{minClues}" : $"{minClues}–{maxClues}";
        var unique = puzzles.All(p => p.SolutionCount == 1) ? " · у каждой решение единственное" : "";

        return new SudokuPdfOptions(
            $"Сборник судоку: {puzzles.Count} {Plural(puzzles.Count, "задача", "задачи", "задач")}",
            $"Сложность: {difficultyLabel} · подсказок {clues} из 81{unique}",
            includeAnswers,
            puzzlesPerPage);
    }

    internal static string Plural(int n, string one, string few, string many)
    {
        var n10 = n % 10;
        var n100 = n % 100;
        if (n10 == 1 && n100 != 11)
        {
            return one;
        }

        return n10 is >= 2 and <= 4 && (n100 < 12 || n100 > 14) ? few : many;
    }
}

/// <summary>
/// Рисует задачи (и, по желанию, ответы) в PDF формата A4.
/// Несколько задач размещаются сеткой на одном листе (1, 2, 4 или 6), ответы —
/// по 12 мелких сеток на листе, чтобы подборка не тратила лишнюю бумагу.
/// Используется SkiaSharp — он уже есть в проекте как графический движок Avalonia.
/// Экспортёр пишет в любой Stream и не зависит от диалогов и окон.
/// </summary>
public static class SudokuPdfExporter
{
    /// <summary>Поддерживаемые раскладки «задач на листе».</summary>
    public static IReadOnlyList<int> SupportedPerPage { get; } = [1, 2, 4, 6];

    private const int AnswersPerPage = 12;

    // PDF меряет в пунктах: 1 pt = 1/72 дюйма. A4 = 210 × 297 мм ≈ 595 × 842 pt.
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Margin = 36f;
    private const float ContentTop = 100f;
    private const float ContentBottom = PageHeight - 44f;
    private const float MaxGridSize = 450f;

    /// <summary>Одна задача (как раньше).</summary>
    public static void Export(SudokuPuzzleResult result, Stream output, SudokuPdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        Export([result], output, options);
    }

    /// <summary>Подборка задач в одном файле.</summary>
    public static void Export(IReadOnlyList<SudokuPuzzleResult> puzzles, Stream output, SudokuPdfOptions options)
    {
        ArgumentNullException.ThrowIfNull(puzzles);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);

        if (puzzles.Count == 0)
        {
            throw new ArgumentException("Нет задач для печати.", nameof(puzzles));
        }

        var perPage = NormalizePerPage(options.PuzzlesPerPage, puzzles.Count);
        var answersPerPage = AnswersLayout(perPage, puzzles.Count);
        var puzzlePages = PagesFor(puzzles.Count, perPage);
        var answerPages = options.IncludeAnswers ? PagesFor(puzzles.Count, answersPerPage) : 0;
        var totalPages = puzzlePages + answerPages;

        var fonts = new Fonts(ResolveTypeface(SKFontStyle.Normal), ResolveTypeface(SKFontStyle.Bold));

        using (var document = SKDocument.CreatePdf(output)
                              ?? throw new InvalidOperationException("Не удалось создать PDF-документ."))
        {
            var page = 0;

            for (var first = 0; first < puzzles.Count; first += perPage)
            {
                page++;
                DrawPage(document, puzzles, first, perPage, withAnswers: false, options.Title, options.Subtitle,
                    page, totalPages, fonts);
            }

            if (options.IncludeAnswers)
            {
                for (var first = 0; first < puzzles.Count; first += answersPerPage)
                {
                    page++;
                    DrawPage(document, puzzles, first, answersPerPage, withAnswers: true, options.Title + " — ответы",
                        "Жирные цифры — подсказки из условия, серые — ответы.", page, totalPages, fonts);
                }
            }

            document.Close(); // Без Close PDF получится неполным.
        }

        output.Flush();
    }

    /// <summary>Сколько листов займёт подборка (для подсказки в окне).</summary>
    public static (int PuzzlePages, int AnswerPages) EstimatePages(int puzzleCount, int puzzlesPerPage, bool includeAnswers)
    {
        if (puzzleCount <= 0)
        {
            return (0, 0);
        }

        var perPage = NormalizePerPage(puzzlesPerPage, puzzleCount);
        var answers = includeAnswers ? PagesFor(puzzleCount, AnswersLayout(perPage, puzzleCount)) : 0;
        return (PagesFor(puzzleCount, perPage), answers);
    }

    /// <summary>
    /// Раскладка, которая реально будет использована: не больше задач, чем есть,
    /// округлённое вверх до поддерживаемой (3 задачи при «по 6» → сетка на 4).
    /// </summary>
    public static int NormalizePerPage(int requested, int puzzleCount)
    {
        var wanted = Math.Clamp(Math.Min(requested, Math.Max(1, puzzleCount)), 1, SupportedPerPage[^1]);
        return SupportedPerPage.First(value => value >= wanted);
    }

    private static int AnswersLayout(int perPage, int puzzleCount) =>
        puzzleCount == 1 && perPage == 1 ? 1 : AnswersPerPage;

    private static int PagesFor(int count, int perPage) => (count + perPage - 1) / perPage;

    private static (int Columns, int Rows) LayoutOf(int perPage) => perPage switch
    {
        1 => (1, 1),
        2 => (1, 2),
        4 => (2, 2),
        6 => (2, 3),
        12 => (3, 4),
        _ => throw new ArgumentOutOfRangeException(nameof(perPage), perPage, null),
    };

    private sealed record Fonts(SKTypeface Regular, SKTypeface Bold);

    private static void DrawPage(
        SKDocument document,
        IReadOnlyList<SudokuPuzzleResult> puzzles,
        int first,
        int perPage,
        bool withAnswers,
        string title,
        string subtitle,
        int pageNumber,
        int totalPages,
        Fonts fonts)
    {
        var canvas = document.BeginPage(PageWidth, PageHeight);

        using (var titlePaint = TextPaint(fonts.Bold, 16f, SKColors.Black))
        using (var subtitlePaint = TextPaint(fonts.Regular, 9.5f, new SKColor(90, 90, 90)))
        using (var footerPaint = TextPaint(fonts.Regular, 9f, new SKColor(120, 120, 120)))
        using (var rulePaint = LinePaint(0.6f, new SKColor(190, 190, 190)))
        {
            DrawCentered(canvas, title, PageWidth / 2f, 58f, titlePaint);
            DrawCentered(canvas, subtitle, PageWidth / 2f, 76f, subtitlePaint);
            canvas.DrawLine(Margin, 86f, PageWidth - Margin, 86f, rulePaint);
            DrawCentered(canvas, $"стр. {pageNumber} из {totalPages}", PageWidth / 2f, PageHeight - 20f, footerPaint);
        }

        var (columns, rows) = LayoutOf(perPage);
        const float gapX = 24f;
        const float gapY = 18f;
        var contentWidth = PageWidth - 2 * Margin;
        var contentHeight = ContentBottom - ContentTop;
        var slotWidth = (contentWidth - (columns - 1) * gapX) / columns;
        var slotHeight = (contentHeight - (rows - 1) * gapY) / rows;

        // Над каждой сеткой — подпись «№ k · подсказок». Для одной большой задачи она не нужна.
        var showLabels = perPage > 1 || puzzles.Count > 1;
        var labelSize = Math.Clamp(slotWidth / 24f, 8f, 11f);
        var labelHeight = showLabels ? labelSize + 6f : 0f;
        var gridSize = Math.Min(MaxGridSize, Math.Min(slotWidth, slotHeight - labelHeight));

        using var labelPaint = TextPaint(fonts.Bold, labelSize, new SKColor(60, 60, 60));

        for (var slot = 0; slot < perPage && first + slot < puzzles.Count; slot++)
        {
            var index = first + slot;
            var puzzle = puzzles[index];
            var column = slot % columns;
            var row = slot / columns;

            var slotLeft = Margin + column * (slotWidth + gapX);
            var slotTop = ContentTop + row * (slotHeight + gapY);
            var gridLeft = slotLeft + (slotWidth - gridSize) / 2f;
            var gridTop = slotTop + labelHeight;

            if (showLabels)
            {
                var label = $"№ {index + 1} · подсказок {puzzle.ActualClues}";
                canvas.DrawText(label, gridLeft, slotTop + labelSize, labelPaint);
            }

            DrawSudoku(canvas, puzzle.Puzzle, withAnswers ? puzzle.Solution : null, gridLeft, gridTop, gridSize, fonts);
        }

        document.EndPage();
    }

    private static void DrawSudoku(
        SKCanvas canvas,
        SudokuBoard puzzle,
        SudokuBoard? solution,
        float left,
        float top,
        float size,
        Fonts fonts)
    {
        var cell = size / SudokuBoard.Size;
        var digitSize = cell * 0.6f;

        // Толщина линий зависит от размера сетки: на маленьких сетках тоньше.
        using var thin = LinePaint(Math.Clamp(size / 750f, 0.35f, 0.6f), new SKColor(150, 150, 150));
        using var thick = LinePaint(Math.Clamp(size / 225f, 0.9f, 2f), SKColors.Black);
        using var givenPaint = TextPaint(fonts.Bold, digitSize, SKColors.Black);
        using var answerPaint = TextPaint(fonts.Regular, digitSize, new SKColor(125, 125, 125));

        // Сначала тонкие линии, потом толстые — чтобы толстые лежали сверху.
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
                var offset = i * cell;
                canvas.DrawLine(left + offset, top, left + offset, top + size, paint);
                canvas.DrawLine(left, top + offset, left + size, top + offset, paint);
            }
        }

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
            var centerX = left + column * cell + cell / 2f;
            // Базовая линия цифры: центр клетки + примерно треть кегля (высота цифр ≈ 0,7 кегля).
            var baseline = top + row * cell + cell / 2f + paint.TextSize * 0.35f;

            DrawCentered(canvas, value.ToString(CultureInfo.InvariantCulture), centerX, baseline, paint);
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
    /// Шрифт с кириллицей: «Судоку», «подсказок». Перебираем распространённые
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

/// <summary>
/// Запись готового PDF на диск с понятными ошибками и запасным вариантом.
/// «Access denied» на Windows чаще всего значит: папку (Рабочий стол, Документы,
/// в т. ч. в OneDrive) защищает «Контролируемый доступ к папкам», или файл открыт
/// в просмотрщике PDF. Тогда сохраняем в «Загрузки» (а если и туда нельзя — во временную папку).
/// </summary>
public static class PdfFileWriter
{
    /// <summary>Результат записи: куда в итоге сохранили и что пошло не так с выбранным путём.</summary>
    public sealed record SaveOutcome(string? SavedPath, bool UsedFallback, string? Problem);

    public static async Task<SaveOutcome> WriteAsync(byte[] pdf, string? preferredPath, Func<Task<Stream>>? openStream, string fileName)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        string? problem = null;

        try
        {
            if (preferredPath is not null)
            {
                await File.WriteAllBytesAsync(preferredPath, pdf);
                return new SaveOutcome(preferredPath, false, null);
            }

            if (openStream is not null)
            {
                await using var stream = await openStream();
                if (stream.CanSeek)
                {
                    stream.SetLength(0);
                }

                await stream.WriteAsync(pdf);
                await stream.FlushAsync();
                return new SaveOutcome(fileName, false, null);
            }

            problem = "не удалось получить путь к выбранному файлу";
        }
        catch (UnauthorizedAccessException)
        {
            problem = "Windows запретил запись в эту папку (часто это «Контролируемый доступ к папкам» " +
                      "для Рабочего стола/Документов) или файл только для чтения";
        }
        catch (IOException exception)
        {
            problem = $"файл занят или недоступен ({exception.Message}) — возможно, он открыт в просмотрщике PDF";
        }

        foreach (var folder in FallbackFolders())
        {
            try
            {
                Directory.CreateDirectory(folder);
                var path = UniquePath(folder, fileName);
                await File.WriteAllBytesAsync(path, pdf);
                return new SaveOutcome(path, true, problem);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                // Пробуем следующую папку.
            }
        }

        return new SaveOutcome(null, true, problem);
    }

    private static IEnumerable<string> FallbackFolders()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
        {
            yield return Path.Combine(profile, "Downloads");
        }

        yield return Path.Combine(Path.GetTempPath(), "RecursiveLab");
    }

    private static string UniquePath(string folder, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".pdf";
        }

        var path = Path.Combine(folder, name + extension);
        for (var i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{name} ({i}){extension}");
        }

        return path;
    }
}
