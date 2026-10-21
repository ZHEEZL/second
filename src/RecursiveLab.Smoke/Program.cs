using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RecursiveLab.App.Views;
using RecursiveLab.App.Views.Pages;

namespace RecursiveLab.Smoke;

/// <summary>
/// Headless-проверка: создаёт окно без реального дисплея, переключает все
/// задачи и сохраняет PNG. Помогает ловить ошибки вёрстки и XAML в CI.
/// </summary>
internal static class Program
{
    private static readonly string[] TaskIds = ["htree", "sudoku", "closest", "hanoi", "charts"];

    public static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : Path.Combine("artifacts", "screenshots");
        Directory.CreateDirectory(outDir);

        AppBuilder.Configure<RecursiveLab.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont()
            .SetupWithoutStarting();

        var window = new MainWindow
        {
            Width = 1280,
            Height = 860,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        const double windowWidth = 1280;
        const double windowHeight = 860;

        var failed = false;

        foreach (var taskId in TaskIds)
        {
            window.SelectTask(taskId);
            Dispatcher.UIThread.RunJobs();

            window.Measure(new Size(windowWidth, windowHeight));
            window.Arrange(new Rect(0, 0, windowWidth, windowHeight));
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            var path = Path.Combine(outDir, $"task-{taskId}.png");

            if (frame is null)
            {
                Console.WriteLine($"[FAIL] {taskId}: окно не отрендерилось");
                failed = true;
                continue;
            }

            frame.Save(path);
            Console.WriteLine($"[ok] {taskId} -> {path}");
        }

        // Дополнительные кадры H-дерева на разных глубинах — для проверки геометрии.
        window.SelectTask("htree");
        Dispatcher.UIThread.RunJobs();

        var host = window.FindControl<ContentControl>("PageHost");
        if (host?.Content is HTreePage htreePage)
        {
            foreach (var depth in new[] { 2, 3, 4, 5 })
            {
                htreePage.SetDepthForPreview(depth);
                Dispatcher.UIThread.RunJobs();
                window.Measure(new Size(windowWidth, windowHeight));
                window.Arrange(new Rect(0, 0, windowWidth, windowHeight));
                Dispatcher.UIThread.RunJobs();

                var frame = window.CaptureRenderedFrame();
                if (frame is null)
                {
                    continue;
                }

                var path = Path.Combine(outDir, $"htree-depth-{depth}.png");
                frame.Save(path);
                Console.WriteLine($"[ok] htree depth {depth} -> {path}");
            }
        }

        // Кадр с подсвеченной ближайшей парой (эталонный перебор).
        window.SelectTask("closest");
        Dispatcher.UIThread.RunJobs();

        if (window.FindControl<ContentControl>("PageHost")?.Content is ClosestPairPage closestPage)
        {
            closestPage.FindControl<Button>("ReferenceButton")?
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            if (frame is not null)
            {
                var path = Path.Combine(outDir, "closest-reference.png");
                frame.Save(path);
                Console.WriteLine($"[ok] closest reference -> {path}");
            }
        }

        return failed ? 1 : 0;
    }
}
