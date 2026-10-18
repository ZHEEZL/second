using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using RecursiveLab.App.Controls;
using RecursiveLab.App.Services;
using RecursiveLab.Core.Geometry;
using RecursiveLab.Core.Tasks.HTree;

namespace RecursiveLab.App.Views.Pages;

public partial class HTreePage : UserControl
{
    private const int MaxUiDepth = 6;

    private readonly DispatcherTimer _timer;
    private readonly HTreeCanvas _canvas;
    private readonly Slider _depthSlider;
    private readonly Slider _speedSlider;
    private readonly Button _playButton;
    private readonly Button _resetButton;
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _depthValue;
    private readonly TextBlock _speedValue;
    private readonly TextBlock _progressText;
    private readonly TextBlock _hudSegments;
    private readonly TextBlock _hudStatus;
    private readonly TextBlock _hudDepth;
    private readonly Ellipse? _hudDot;

    private IReadOnlyList<Segment> _segments = Array.Empty<Segment>();
    private int _depth = 4;
    private int _totalSegments;
    private double _speed = 1;
    private double _renderPosition;
    private bool _isPlaying;

    public HTreePage()
    {
        InitializeComponent();

        _canvas = Get<HTreeCanvas>("TreeCanvas");
        _depthSlider = Get<Slider>("DepthSlider");
        _speedSlider = Get<Slider>("SpeedSlider");
        _playButton = Get<Button>("PlayButton");
        _resetButton = Get<Button>("ResetButton");
        _progressBar = Get<ProgressBar>("BuildProgress");
        _depthValue = Get<TextBlock>("DepthValue");
        _speedValue = Get<TextBlock>("SpeedValue");
        _progressText = Get<TextBlock>("ProgressText");
        _hudSegments = Get<TextBlock>("HudSegments");
        _hudStatus = Get<TextBlock>("HudStatus");
        _hudDepth = Get<TextBlock>("HudDepth");
        _hudDot = this.FindControl<Ellipse>("HudDot");

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => OnAnimationTick();

        _depthSlider.ValueChanged += (_, e) => OnDepthChanged(e.NewValue);
        _speedSlider.ValueChanged += (_, e) => OnSpeedChanged(e.NewValue);
        _playButton.Click += OnPlayClicked;
        _resetButton.Click += OnResetClicked;
        Get<Button>("OpenCodeButton").Click += (_, _) =>
            RepoPaths.ShowInExplorer("src/RecursiveLab.Core/Tasks/HTree/HTreeGenerator.cs");

        Regenerate(fullVisible: true);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Для smoke-скриншотов: выставить глубину без анимации.</summary>
    public void SetDepthForPreview(int depth)
    {
        _depth = Math.Clamp(depth, 0, MaxUiDepth);
        StopAnimation();
        Regenerate(fullVisible: true);
        SetHudStatus("построено");
        _playButton.Content = "Построить";
    }

    // ------------------------------------------------------------------ depth

    private void OnDepthChanged(double newValue)
    {
        var depth = (int)Math.Round(newValue);
        if (depth == _depth)
        {
            return;
        }

        _depth = Math.Clamp(depth, 0, MaxUiDepth);
        StopAnimation();
        Regenerate(fullVisible: true);
        SetHudStatus("построено");
        _playButton.Content = "Построить";
    }

    private void Regenerate(bool fullVisible)
    {
        var stopwatch = Stopwatch.StartNew();
        _segments = HTreeGenerator.Generate(1.0, _depth);
        stopwatch.Stop();

        _canvas.Segments = _segments;
        _totalSegments = _segments.Count;

        var visible = fullVisible ? _totalSegments : 0;
        _canvas.VisibleCount = visible;
        _renderPosition = visible;

        _progressBar.Maximum = Math.Max(1, _totalSegments);
        _progressBar.Value = visible;

        _depthValue.Text = _depth.ToString(CultureInfo.CurrentCulture);
        _depthSlider.Value = _depth;
        _speedValue.Text = $"×{_speed:0.00}";

        Get<TextBlock>("DepthHint").Text = _depth == 0
            ? "Глубина 0 — ровно одна буква H, 3 отрезка."
            : $"Глубина {_depth}: {HTreeGenerator.HShapeCount(_depth):N0} фигур H, {_totalSegments:N0} отрезков.";

        Get<TextBlock>("StatHShapes").Text = HTreeGenerator.HShapeCount(_depth).ToString("N0", CultureInfo.CurrentCulture);
        Get<TextBlock>("StatSegments").Text = _totalSegments.ToString("N0", CultureInfo.CurrentCulture);
        Get<TextBlock>("StatMaxDepth").Text = _depth.ToString(CultureInfo.CurrentCulture);
        Get<TextBlock>("StatGenTime").Text = $"{stopwatch.Elapsed.TotalMilliseconds:0.0} мс";

        Get<TextBlock>("RecursionLine").Text = _depth == 0
            ? "S(0) = 3"
            : $"S({_depth}) = 4 · S({_depth - 1}) + 3";

        _hudDepth.Text = $"глубина {_depth}";
        _hudSegments.Text = $"{visible:N0} / {_totalSegments:N0} отрезков";
        _progressText.Text = $"{visible:N0} / {_totalSegments:N0}";
    }

    // ------------------------------------------------------------------ animation

    private void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            PauseAnimation();
            return;
        }

        if (_canvas.VisibleCount >= _totalSegments)
        {
            _canvas.VisibleCount = 0;
            _renderPosition = 0;
            _progressBar.Value = 0;
            _progressText.Text = $"0 / {_totalSegments:N0}";
        }

        _renderPosition = _canvas.VisibleCount;
        _isPlaying = true;
        _playButton.Content = "Пауза";
        SetHudStatus("рисуем рекурсию…");
        _hudDot!.Fill = new SolidColorBrush(Color.Parse("#FBBF24"));
        _timer.Start();
    }

    private void PauseAnimation()
    {
        _isPlaying = false;
        _timer.Stop();
        _playButton.Content = "Продолжить";
        SetHudStatus("пауза");
        _hudDot!.Fill = new SolidColorBrush(Color.Parse("#FBBF24"));
    }

    private void StopAnimation()
    {
        _isPlaying = false;
        _timer.Stop();
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        StopAnimation();
        _canvas.VisibleCount = 0;
        _renderPosition = 0;
        _progressBar.Value = 0;
        _progressText.Text = $"0 / {_totalSegments:N0}";
        _hudSegments.Text = $"0 / {_totalSegments:N0} отрезков";
        _playButton.Content = "Построить";
        SetHudStatus("готово к построению");
        _hudDot!.Fill = new SolidColorBrush(Color.Parse("#22D3EE"));
    }

    private void OnAnimationTick()
    {
        if (!_isPlaying)
        {
            return;
        }

        var durationSeconds = Math.Clamp(_totalSegments / 500.0, 0.8, 6.0) / _speed;
        var perTick = _totalSegments / (durationSeconds * 60.0);

        _renderPosition = Math.Min(_totalSegments, _renderPosition + Math.Max(perTick, 0.5));
        var visible = (int)_renderPosition;

        _canvas.VisibleCount = visible;
        _progressBar.Value = visible;
        _progressText.Text = $"{visible:N0} / {_totalSegments:N0}";
        _hudSegments.Text = $"{visible:N0} / {_totalSegments:N0} отрезков";

        if (visible < _totalSegments)
        {
            return;
        }

        StopAnimation();
        _playButton.Content = "Построить";
        SetHudStatus("построено");
        _hudDot!.Fill = new SolidColorBrush(Color.Parse("#34D399"));
    }

    private void OnSpeedChanged(double newValue)
    {
        _speed = Math.Clamp(newValue, 0.25, 3);
        _speedValue.Text = $"×{_speed:0.00}";
    }

    private void SetHudStatus(string text) => _hudStatus.Text = text;

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в HTreePage.");
}
