using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using RecursiveLab.App.Services;

namespace RecursiveLab.App.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, LabTaskInfo> _tasks;
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, Control> _pageCache = new();
    private string? _currentTaskId;

    public MainWindow()
    {
        InitializeComponent();

        _tasks = LabTaskCatalog.All.ToDictionary(task => task.Id);
        BuildNavigation();
        SelectTask(LabTaskCatalog.All[0].Id);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Открывает задачу по id. Используется и smoke-тестом для скриншотов.</summary>
    public void SelectTask(string taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            return;
        }

        if (_currentTaskId is not null && _navButtons.TryGetValue(_currentTaskId, out var previousButton))
        {
            previousButton.Classes.Remove("active");
        }

        _currentTaskId = taskId;

        if (_navButtons.TryGetValue(taskId, out var button))
        {
            if (!button.Classes.Contains("active"))
            {
                button.Classes.Add("active");
            }
        }

        Get<ContentControl>("PageHost").Content = GetPage(taskId);

        Get<TextBlock>("TaskTitle").Text = task.Title;
        Get<TextBlock>("TaskSubtitle").Text = task.Subtitle;
        Get<TextBlock>("TaskAuthor").Text = task.Author;
        Get<TextBlock>("TaskComplexity").Text = task.Complexity;

        var statusText = Get<TextBlock>("TaskStatus");
        statusText.Text = LabTaskCatalog.StatusText(task.Status);
        statusText.Foreground = Brush(LabTaskCatalog.StatusColor(task.Status));

        Get<Ellipse>("StatusDot").Fill = Brush(LabTaskCatalog.StatusColor(task.Status));
        Get<Border>("StatusChip").BorderBrush = BrushWithAlpha(LabTaskCatalog.StatusColor(task.Status), 0x55);
    }

    // ---------------------------------------------------------------- navigation

    private void BuildNavigation()
    {
        var host = Get<StackPanel>("TaskButtons");

        foreach (var task in LabTaskCatalog.All)
        {
            var button = CreateNavButton(task);
            _navButtons[task.Id] = button;
            host.Children.Add(button);
        }
    }

    private Button CreateNavButton(LabTaskInfo task)
    {
        var statusBrush = Brush(LabTaskCatalog.StatusColor(task.Status));

        var accent = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2),
            Background = Brush(task.AccentColor),
            Margin = new Thickness(0, 2),
        };

        var title = new TextBlock
        {
            Text = task.Title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
        };

        var subtitle = new TextBlock
        {
            Text = task.Subtitle,
            FontSize = 10,
            Foreground = Brush("#8FA3C8"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var statusDot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = statusBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var statusText = new TextBlock
        {
            Text = LabTaskCatalog.StatusText(task.Status),
            FontSize = 9.5,
            Foreground = statusBrush,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 7, 0, 0),
        };
        statusRow.Children.Add(statusDot);
        statusRow.Children.Add(statusText);

        var textPanel = new StackPanel
        {
            Margin = new Thickness(11, 1, 0, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        textPanel.Children.Add(title);
        textPanel.Children.Add(subtitle);
        textPanel.Children.Add(statusRow);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("4,*"),
        };
        grid.Children.Add(accent);
        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        var button = new Button
        {
            Content = grid,
            Tag = task.Id,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        button.Classes.Add("nav");
        button.Click += (_, _) => SelectTask(task.Id);

        return button;
    }

    private Control GetPage(string taskId)
    {
        if (_pageCache.TryGetValue(taskId, out var page))
        {
            return page;
        }

        page = _tasks[taskId].CreatePage();
        _pageCache[taskId] = page;
        return page;
    }

    // ---------------------------------------------------------------- helpers

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в MainWindow.");

    private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));

    private static SolidColorBrush BrushWithAlpha(string hex, byte alpha)
    {
        var color = Color.Parse(hex);
        return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }
}
