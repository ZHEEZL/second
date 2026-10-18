using System.Diagnostics;

namespace RecursiveLab.App.Services;

/// <summary>Помогает открыть файл алгоритма/документацию из UI (для защиты работы).</summary>
public static class RepoPaths
{
    public static string? Root { get; } = FindRoot();

    private static string? FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RecursiveLab.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static string Resolve(string relativePath) =>
        Root is null ? relativePath : Path.Combine(Root, relativePath);

    /// <summary>Открывает проводник с выделенным файлом. Ошибки не пробрасывает.</summary>
    public static void ShowInExplorer(string relativePath)
    {
        try
        {
            var fullPath = Resolve(relativePath);
            if (!File.Exists(fullPath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{fullPath}\"")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // Открытие проводника — не критичная операция, не роняем приложение.
        }
    }
}
