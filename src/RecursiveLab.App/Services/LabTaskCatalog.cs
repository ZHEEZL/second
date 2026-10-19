using Avalonia.Controls;

namespace RecursiveLab.App.Services;

public enum LabTaskStatus
{
    Ready,
    InProgress,
    Stub,
}

/// <summary>
/// Описание задачи в каталоге приложения. Страницы создаются лениво,
/// поэтому каждая задача может лежать в своей папке и отдельном git-бранче.
/// </summary>
public sealed record LabTaskInfo(
    string Id,
    string Title,
    string Subtitle,
    string Author,
    string Complexity,
    LabTaskStatus Status,
    string AccentColor,
    Func<Control> CreatePage);

public static class LabTaskCatalog
{
    public static IReadOnlyList<LabTaskInfo> All { get; } =
    [
        new(
            "htree",
            "H-дерево",
            "Фрактал: рекурсивное рисование",
            "участник 1",
            "O(4^d)",
            LabTaskStatus.Ready,
            "#22D3EE",
            () => new Views.Pages.HTreePage()),

        new(
            "sudoku",
            "Судоку",
            "Перебор с возвратом",
            "участник 2",
            "backtracking",
            LabTaskStatus.InProgress,
            "#A78BFA",
            () => new Views.Pages.SudokuPage()),

        new(
            "closest",
            "Ближайшая пара",
            "Divide and conquer, O(n log n)",
            "участник 3",
            "O(n log n)",
            LabTaskStatus.InProgress,
            "#5B8CFF",
            () => new Views.Pages.ClosestPairPage()),

        new(
            "hanoi",
            "Ханойские башни",
            "Рекурсия и явный стек",
            "участник 4",
            "O(2^n)",
            LabTaskStatus.Ready,
            "#F472B6",
            () => new Views.Pages.HanoiPage()),
    ];

    public static string StatusText(LabTaskStatus status) => status switch
    {
        LabTaskStatus.Ready => "готово",
        LabTaskStatus.InProgress => "в разработке",
        LabTaskStatus.Stub => "заглушка",
        _ => "неизвестно",
    };

    public static string StatusColor(LabTaskStatus status) => status switch
    {
        LabTaskStatus.Ready => "#34D399",
        LabTaskStatus.InProgress => "#FBBF24",
        LabTaskStatus.Stub => "#F87171",
        _ => "#8FA3C8",
    };
}
