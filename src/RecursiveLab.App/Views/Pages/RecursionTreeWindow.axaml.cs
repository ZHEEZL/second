using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using RecursiveLab.App.Controls;
using RecursiveLab.Core.Recursion;

namespace RecursiveLab.App.Views;

/// <summary>
/// Окно «Дерево рекурсии»: показывает дерево вызовов любого алгоритма лабы
/// и проигрывает его по шагам — вход в вызов, выход из вызова.
/// Открывается с любой вкладки через <see cref="ShowFor"/>.
/// </summary>
public partial class RecursionTreeWindow : Window
{
    private readonly IReadOnlyList<RecursionTree> _trees;
    private readonly RecursionTreeView _treeView;
    private readonly ComboBox _traceCombo;
    private readonly TextBlock _titleText;
    private readonly TextBlock _meaningText;
    private readonly TextBlock _statCalls;
    private readonly TextBlock _statDepth;
    private readonly TextBlock _statBase;
    private readonly TextBlock _statLeaves;
    private readonly TextBlock _statDeadEnds;
    private readonly TextBlock _statStack;
    private readonly TextBlock _truncatedText;
    private readonly TextBlock _nodeHeader;
    private readonly TextBlock _nodeLabel;
    private readonly TextBlock _nodeDetail;
    private readonly Ellipse _nodeDot;
    private readonly TextBlock _nodeState;
    private readonly TextBlock _nodeResult;
    private readonly TextBlock _nodeNote;
    private readonly TextBlock _nodePath;
    private readonly Button _playButton;
    private readonly Slider _positionSlider;
    private readonly TextBlock _positionText;
    private readonly TextBlock _speedValue;
    private readonly DispatcherTimer _timer;

    private RecursionTree? _tree;
    private bool _isPlaying;
    private bool _syncingSlider;
    private double _speed = 1;
    private double _pendingEvents;

    public RecursionTreeWindow()
        : this(Array.Empty<RecursionTree>())
    {
    }

    public RecursionTreeWindow(IReadOnlyList<RecursionTree> trees)
    {
        InitializeComponent();

        _trees = trees ?? Array.Empty<RecursionTree>();

        _treeView = Get<RecursionTreeView>("TreeView");
        _traceCombo = Get<ComboBox>("TraceCombo");
        _titleText = Get<TextBlock>("TitleText");
        _meaningText = Get<TextBlock>("MeaningText");
        _statCalls = Get<TextBlock>("StatCalls");
        _statDepth = Get<TextBlock>("StatDepth");
        _statBase = Get<TextBlock>("StatBase");
        _statLeaves = Get<TextBlock>("StatLeaves");
        _statDeadEnds = Get<TextBlock>("StatDeadEnds");
        _statStack = Get<TextBlock>("StatStack");
        _truncatedText = Get<TextBlock>("TruncatedText");
        _nodeHeader = Get<TextBlock>("NodeHeader");
        _nodeLabel = Get<TextBlock>("NodeLabel");
        _nodeDetail = Get<TextBlock>("NodeDetail");
        _nodeDot = Get<Ellipse>("NodeDot");
        _nodeState = Get<TextBlock>("NodeState");
        _nodeResult = Get<TextBlock>("NodeResult");
        _nodeNote = Get<TextBlock>("NodeNote");
        _nodePath = Get<TextBlock>("NodePath");
        _playButton = Get<Button>("PlayButton");
        _positionSlider = Get<Slider>("PositionSlider");
        _positionText = Get<TextBlock>("PositionText");
        _speedValue = Get<TextBlock>("SpeedValue");

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => OnTick();
        Closed += (_, _) => _timer.Stop();

        _traceCombo.ItemsSource = _trees.Select(tree => tree.Title).ToList();
        _traceCombo.IsVisible = _trees.Count > 1;
        _traceCombo.SelectionChanged += (_, _) => ShowTree(_traceCombo.SelectedIndex);

        _playButton.Click += (_, _) => TogglePlay();
        Get<Button>("StartButton").Click += (_, _) => { Pause(); SetPosition(0); };
        Get<Button>("BackButton").Click += (_, _) => { Pause(); SetPosition(_treeView.Position - 1); };
        Get<Button>("ForwardButton").Click += (_, _) => { Pause(); SetPosition(_treeView.Position + 1); };
        Get<Button>("EndButton").Click += (_, _) => { Pause(); SetPosition(int.MaxValue); };
        Get<Button>("FitButton").Click += (_, _) => _treeView.FitToView();

        _positionSlider.ValueChanged += (_, e) =>
        {
            if (!_syncingSlider)
            {
                SetPosition((int)Math.Round(e.NewValue));
            }
        };

        Get<Slider>("SpeedSlider").ValueChanged += (_, e) =>
        {
            _speed = Math.Clamp(e.NewValue, 0.25, 4);
            _speedValue.Text = $"×{_speed:0.00}";
        };

        _treeView.NodeClicked += _ => UpdateNodePanel();

        if (_trees.Count > 0)
        {
            _traceCombo.SelectedIndex = 0; // вызовет ShowTree(0)
            if (_tree is null)
            {
                ShowTree(0);
            }
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Открыть окно с деревьями рекурсии поверх окна, в котором лежит <paramref name="owner"/>.</summary>
    public static RecursionTreeWindow ShowFor(Control owner, params RecursionTree[] trees)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var window = new RecursionTreeWindow(trees);
        if (TopLevel.GetTopLevel(owner) is Window parent)
        {
            window.Show(parent);
        }
        else
        {
            window.Show();
        }

        return window;
    }

    // ------------------------------------------------------------- дерево

    private void ShowTree(int index)
    {
        if (index < 0 || index >= _trees.Count)
        {
            return;
        }

        Pause();
        _tree = _trees[index];
        _treeView.Tree = _tree;

        Title = $"Дерево рекурсии — {_tree.Title}";
        _titleText.Text = _tree.Title;
        _meaningText.Text = _tree.NodeMeaning;

        _statCalls.Text = _tree.TotalCalls.ToString("N0", CultureInfo.CurrentCulture);
        _statDepth.Text = _tree.MaxDepth.ToString(CultureInfo.CurrentCulture);
        _statBase.Text = _tree.Count(RecursionOutcome.BaseCase).ToString("N0", CultureInfo.CurrentCulture);
        _statLeaves.Text = _tree.LeafCount.ToString("N0", CultureInfo.CurrentCulture);
        _statDeadEnds.Text = _tree.Count(RecursionOutcome.DeadEnd).ToString("N0", CultureInfo.CurrentCulture);

        _truncatedText.IsVisible = _tree.IsTruncated;
        _truncatedText.Text = _tree.IsTruncated
            ? $"Вызовов слишком много для экрана: нарисованы первые {_tree.Nodes.Count:N0} из {_tree.TotalCalls:N0}. " +
              "Число вызовов посчитано полностью; глубина, листья и тупики — по нарисованной части."
            : "";

        _syncingSlider = true;
        _positionSlider.Maximum = Math.Max(1, _tree.Events.Count);
        _syncingSlider = false;

        // Сразу показываем всё дерево; «Пуск» проиграет его с начала.
        SetPosition(_tree.Events.Count);
    }

    private void SetPosition(int position)
    {
        if (_tree is null)
        {
            return;
        }

        var total = _tree.Events.Count;
        position = Math.Clamp(position, 0, total);
        _treeView.Position = position;

        _syncingSlider = true;
        _positionSlider.Value = position;
        _syncingSlider = false;

        _positionText.Text = position == 0
            ? $"событие 0 / {total:N0} · ещё ничего не вызвано"
            : $"событие {position:N0} / {total:N0} · {DescribeEvent(_tree.Events[position - 1])}";

        _statStack.Text = StackDepth().ToString(CultureInfo.CurrentCulture);
        UpdateNodePanel();
    }

    private string DescribeEvent(RecursionEvent recursionEvent)
    {
        var node = _tree!.Nodes[recursionEvent.NodeId];
        return recursionEvent.Kind == RecursionEventKind.Enter
            ? $"вход в {node.Label} (глубина {node.Depth})"
            : $"выход из {node.Label}: {OutcomeText(node.Outcome)}";
    }

    private int StackDepth()
    {
        var depth = 0;
        for (var id = _treeView.CurrentNodeId; id >= 0; id = _tree!.Nodes[id].ParentId)
        {
            depth++;
        }

        return depth;
    }

    private void UpdateNodePanel()
    {
        if (_tree is null)
        {
            return;
        }

        var selected = _treeView.SelectedNodeId;
        var id = selected >= 0 ? selected : _treeView.CurrentNodeId;
        _nodeHeader.Text = selected >= 0 ? "ВЫБРАННЫЙ ВЫЗОВ" : "ТЕКУЩИЙ ВЫЗОВ";

        if (id < 0 || id >= _tree.Nodes.Count)
        {
            _nodeLabel.Text = "—";
            _nodeDetail.Text = "";
            _nodeState.Text = _treeView.Position == 0 ? "ещё ничего не вызвано" : "все вызовы завершены";
            _nodeDot.Fill = RecursionTreeView.OutcomeBrush(RecursionOutcome.Completed);
            _nodeResult.Text = "";
            _nodeNote.Text = "";
            _nodePath.Text = "";
            return;
        }

        var node = _tree.Nodes[id];
        _nodeLabel.Text = node.Label;
        _nodeDetail.Text = node.Detail ?? "";

        if (!_treeView.IsShown(node))
        {
            _nodeState.Text = "ещё не вызван";
            _nodeDot.Fill = Brushes.Gray;
            _nodeResult.Text = "";
            _nodeNote.Text = "";
        }
        else if (!_treeView.IsDone(node))
        {
            _nodeState.Text = $"в стеке, глубина {node.Depth} — выполняется";
            _nodeDot.Fill = RecursionTreeView.OutcomeBrush(RecursionOutcome.Running);
            _nodeResult.Text = "";
            _nodeNote.Text = "";
        }
        else
        {
            _nodeState.Text = $"{OutcomeText(node.Outcome)}, глубина {node.Depth}";
            _nodeDot.Fill = RecursionTreeView.OutcomeBrush(node.Outcome);
            _nodeResult.Text = node.Result is null ? "" : $"Итог: {node.Result}";
            _nodeNote.Text = node.Note ?? "";
        }

        _nodePath.Text = $"Путь от корня: {PathText(node)}";
    }

    private string PathText(RecursionNode node)
    {
        var labels = new List<string>();
        for (var id = node.Id; id >= 0; id = _tree!.Nodes[id].ParentId)
        {
            labels.Add(_tree.Nodes[id].Label);
        }

        labels.Reverse();

        if (labels.Count > 7)
        {
            labels = labels.Take(2).Append("…").Concat(labels.Skip(labels.Count - 4)).ToList();
        }

        return string.Join(" → ", labels);
    }

    private static string OutcomeText(RecursionOutcome outcome) => outcome switch
    {
        RecursionOutcome.Completed => "завершён",
        RecursionOutcome.BaseCase => "базовый случай",
        RecursionOutcome.Success => "ведёт к решению",
        RecursionOutcome.DeadEnd => "тупик, откат",
        _ => "выполняется",
    };

    // ------------------------------------------------------------- проигрывание

    private void TogglePlay()
    {
        if (_isPlaying)
        {
            Pause();
            return;
        }

        if (_tree is null || _tree.Events.Count == 0)
        {
            return;
        }

        if (_treeView.Position >= _tree.Events.Count)
        {
            SetPosition(0); // Проигрываем с начала.
        }

        _treeView.SelectedNodeId = -1;
        _pendingEvents = 0;
        _isPlaying = true;
        _playButton.Content = "Пауза";
        _timer.Start();
    }

    private void Pause()
    {
        _timer.Stop();
        _isPlaying = false;
        _playButton.Content = "Пуск";
    }

    private void OnTick()
    {
        if (_tree is null)
        {
            Pause();
            return;
        }

        // Всё дерево проигрывается примерно за 15 секунд (на скорости ×1),
        // но не быстрее 4 и не медленнее 4000 событий в секунду.
        var eventsPerSecond = Math.Clamp(_tree.Events.Count / 15.0, 4, 4000) * _speed;
        _pendingEvents += eventsPerSecond * _timer.Interval.TotalSeconds;

        var step = (int)_pendingEvents;
        if (step == 0)
        {
            return;
        }

        _pendingEvents -= step;
        SetPosition(_treeView.Position + step);

        if (_treeView.Position >= _tree.Events.Count)
        {
            Pause();
        }
    }

    private T Get<T>(string name) where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Не найден контрол '{name}' в RecursionTreeWindow.");
}
