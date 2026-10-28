using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using RecursiveLab.Core.Recursion;

namespace RecursiveLab.App.Controls;

/// <summary>
/// Рисует дерево рекурсии и «проигрывает» его по ленте событий: узел появляется
/// в момент входа в вызов и получает цвет итога в момент выхода. Путь от корня
/// до текущего вызова — это стек вызовов, он подсвечен.
/// Колесо мыши — масштаб, перетаскивание — сдвиг, клик — выбрать узел.
/// </summary>
public sealed class RecursionTreeView : Control
{
    // Расстояние между соседними листьями и между уровнями при масштабе 1.
    private const double UnitX = 30;
    private const double UnitY = 70;
    private const double Margin = 36;
    private const double MinZoom = 0.01;
    private const double MaxZoom = 6;

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#0C1628"));
    private static readonly IBrush RunningBrush = new SolidColorBrush(Color.Parse("#22D3EE"));
    private static readonly IBrush CompletedBrush = new SolidColorBrush(Color.Parse("#5B8CFF"));
    private static readonly IBrush BaseCaseBrush = new SolidColorBrush(Color.Parse("#A78BFA"));
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush DeadEndBrush = new SolidColorBrush(Color.Parse("#F87171"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#E8EEF9"));
    private static readonly IBrush LabelBackBrush = new SolidColorBrush(Color.Parse("#E6081120"));
    private static readonly IPen EdgePen = new Pen(new SolidColorBrush(Color.Parse("#2A3D63")), 1);
    private static readonly IPen StackEdgePen = new Pen(new SolidColorBrush(Color.Parse("#22D3EE")), 2.5);
    private static readonly IPen CurrentPen = new Pen(Brushes.White, 2);
    private static readonly IPen SelectedPen = new Pen(new SolidColorBrush(Color.Parse("#FBBF24")), 2.5);
    private static readonly Typeface LabelTypeface = new(FontFamily.Parse("Inter, Segoe UI"), FontStyle.Normal, FontWeight.Medium);

    private RecursionTree? _tree;
    private double[] _layoutX = Array.Empty<double>();
    private double _layoutWidth;
    private int _position;
    private int _selected = -1;
    private double _zoom = 1;
    private Vector _offset;
    private bool _userMoved;

    private Point? _pressPoint;
    private Vector _pressOffset;
    private bool _dragging;

    public RecursionTreeView()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    /// <summary>Пользователь кликнул по узлу (номер узла или −1).</summary>
    public event Action<int>? NodeClicked;

    public RecursionTree? Tree
    {
        get => _tree;
        set
        {
            _tree = value;
            ComputeLayout();
            _position = value?.Events.Count ?? 0;
            _selected = -1;
            _userMoved = false;
            FitToView();
        }
    }

    /// <summary>Сколько событий ленты уже «произошло» (0 — пусто, Events.Count — всё дерево).</summary>
    public int Position
    {
        get => _position;
        set
        {
            var max = _tree?.Events.Count ?? 0;
            var clamped = Math.Clamp(value, 0, max);
            if (clamped == _position)
            {
                return;
            }

            _position = clamped;
            if (FollowCurrent)
            {
                KeepNodeVisible(CurrentNodeId);
            }

            InvalidateVisual();
        }
    }

    /// <summary>Во время проигрывания сдвигать вид так, чтобы текущий вызов был на экране.</summary>
    public bool FollowCurrent { get; set; } = true;

    public int SelectedNodeId
    {
        get => _selected;
        set
        {
            _selected = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Текущий вызов: если последним был вход — это он сам; если выход —
    /// управление вернулось к родителю.
    /// </summary>
    public int CurrentNodeId
    {
        get
        {
            if (_tree is null || _position == 0)
            {
                return -1;
            }

            var last = _tree.Events[_position - 1];
            return last.Kind == RecursionEventKind.Enter
                ? last.NodeId
                : _tree.Nodes[last.NodeId].ParentId;
        }
    }

    /// <summary>Узел уже появился на экране.</summary>
    public bool IsShown(RecursionNode node) => node.EnterEvent < _position;

    /// <summary>Узел уже завершился к текущему моменту.</summary>
    public bool IsDone(RecursionNode node) => node.ExitEvent >= 0 && node.ExitEvent < _position;

    public void FitToView()
    {
        _userMoved = false;

        if (_tree is null || _tree.Nodes.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            InvalidateVisual();
            return;
        }

        var width = Math.Max(UnitX, _layoutWidth * UnitX);
        var height = Math.Max(UnitY, _tree.MaxDepth * UnitY);
        var availableWidth = Math.Max(1, Bounds.Width - 2 * Margin);
        var availableHeight = Math.Max(1, Bounds.Height - 2 * Margin);

        _zoom = Math.Clamp(Math.Min(availableWidth / width, availableHeight / height), MinZoom, 1.6);

        var contentWidth = _layoutWidth * UnitX * _zoom;
        var contentHeight = _tree.MaxDepth * UnitY * _zoom;
        _offset = new Vector(
            (Bounds.Width - contentWidth) / 2,
            Math.Max(Margin, (Bounds.Height - contentHeight) / 2));

        InvalidateVisual();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (!_userMoved)
        {
            // Bounds обновятся после Arrange, поэтому вписываем по finalSize.
            FitToSize(finalSize);
        }

        return result;
    }

    private void FitToSize(Size size)
    {
        if (_tree is null || _tree.Nodes.Count == 0 || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var width = Math.Max(UnitX, _layoutWidth * UnitX);
        var height = Math.Max(UnitY, _tree.MaxDepth * UnitY);
        _zoom = Math.Clamp(
            Math.Min((size.Width - 2 * Margin) / width, (size.Height - 2 * Margin) / height),
            MinZoom,
            1.6);

        _offset = new Vector(
            (size.Width - _layoutWidth * UnitX * _zoom) / 2,
            Math.Max(Margin, (size.Height - _tree.MaxDepth * UnitY * _zoom) / 2));
    }

    // ------------------------------------------------------------- раскладка

    /// <summary>
    /// Простая «аккуратная» раскладка: листья ставим подряд слева направо,
    /// родителя — посередине между крайними детьми. Это тоже рекурсия.
    /// </summary>
    private void ComputeLayout()
    {
        if (_tree is null)
        {
            _layoutX = Array.Empty<double>();
            _layoutWidth = 0;
            return;
        }

        _layoutX = new double[_tree.Nodes.Count];
        double nextLeaf = 0;

        foreach (var root in _tree.Roots)
        {
            Place(root, ref nextLeaf);
            nextLeaf += 1; // зазор между деревьями
        }

        _layoutWidth = Math.Max(0, nextLeaf - 2);
    }

    private void Place(int id, ref double nextLeaf)
    {
        var node = _tree!.Nodes[id];

        if (node.Children.Count == 0)
        {
            _layoutX[id] = nextLeaf;
            nextLeaf += 1;
            return;
        }

        foreach (var child in node.Children)
        {
            Place(child, ref nextLeaf);
        }

        _layoutX[id] = (_layoutX[node.Children[0]] + _layoutX[node.Children[^1]]) / 2;
    }

    private Point ToScreen(int id)
    {
        var node = _tree!.Nodes[id];
        return new Point(
            _offset.X + _layoutX[id] * UnitX * _zoom,
            _offset.Y + node.Depth * UnitY * _zoom);
    }

    private void KeepNodeVisible(int id)
    {
        if (_tree is null || id < 0 || Bounds.Width <= 0)
        {
            return;
        }

        var point = ToScreen(id);
        const double pad = 40;

        if (point.X < pad || point.X > Bounds.Width - pad || point.Y < pad || point.Y > Bounds.Height - pad)
        {
            _offset += new Vector(Bounds.Width / 2 - point.X, Bounds.Height / 2 - point.Y);
        }
    }

    // ------------------------------------------------------------- отрисовка

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(BackgroundBrush, bounds);

        if (_tree is null || _tree.Nodes.Count == 0)
        {
            return;
        }

        var nodes = _tree.Nodes;
        var radius = Math.Clamp(8 * _zoom, 1.5, 9);
        var view = bounds.Inflate(UnitY * _zoom + 20);

        // Путь от текущего вызова к корню — это стек вызовов.
        var onStack = new HashSet<int>();
        for (var id = CurrentNodeId; id >= 0; id = nodes[id].ParentId)
        {
            onStack.Add(id);
        }

        // 1. Рёбра.
        foreach (var node in nodes)
        {
            if (node.ParentId < 0 || !IsShown(node))
            {
                continue;
            }

            var from = ToScreen(node.ParentId);
            var to = ToScreen(node.Id);
            if (!view.Contains(from) && !view.Contains(to))
            {
                continue;
            }

            var pen = onStack.Contains(node.Id) ? StackEdgePen : EdgePen;
            context.DrawLine(pen, from, to);
        }

        // 2. Узлы.
        foreach (var node in nodes)
        {
            if (!IsShown(node))
            {
                continue;
            }

            var center = ToScreen(node.Id);
            if (!view.Contains(center))
            {
                continue;
            }

            var brush = IsDone(node) ? OutcomeBrush(node.Outcome) : RunningBrush;
            var r = onStack.Contains(node.Id) ? radius * 1.25 : radius;
            context.DrawEllipse(brush, null, center, r, r);
        }

        // 3. Подписи — только если хватает места, иначе будет каша.
        var spacing = UnitX * _zoom;
        if (spacing >= 34)
        {
            foreach (var node in nodes)
            {
                if (!IsShown(node))
                {
                    continue;
                }

                var center = ToScreen(node.Id);
                if (!bounds.Contains(center))
                {
                    continue;
                }

                DrawLabel(context, node.Label, center, radius, withBackground: false, maxWidth: spacing * 0.95);
            }
        }

        // 4. Текущий и выбранный узлы — обводка и подпись всегда.
        var current = CurrentNodeId;
        if (current >= 0)
        {
            var center = ToScreen(current);
            context.DrawEllipse(null, CurrentPen, center, radius * 1.25 + 3, radius * 1.25 + 3);
            if (spacing < 34)
            {
                DrawLabel(context, nodes[current].Label, center, radius * 1.25, withBackground: true, maxWidth: 220);
            }
        }

        if (_selected >= 0 && _selected < nodes.Count && IsShown(nodes[_selected]))
        {
            var center = ToScreen(_selected);
            context.DrawEllipse(null, SelectedPen, center, radius + 5, radius + 5);
            if (spacing < 34 && _selected != current)
            {
                DrawLabel(context, nodes[_selected].Label, center, radius, withBackground: true, maxWidth: 220);
            }
        }
    }

    private static void DrawLabel(DrawingContext context, string text, Point center, double radius, bool withBackground, double maxWidth)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            11,
            LabelBrush)
        {
            MaxTextWidth = Math.Max(20, maxWidth),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };

        var origin = new Point(center.X - formatted.Width / 2, center.Y + radius + 3);

        if (withBackground)
        {
            var back = new Rect(origin.X - 4, origin.Y - 1, formatted.Width + 8, formatted.Height + 2);
            context.FillRectangle(LabelBackBrush, back, 4);
        }

        context.DrawText(formatted, origin);
    }

    public static IBrush OutcomeBrush(RecursionOutcome outcome) => outcome switch
    {
        RecursionOutcome.Completed => CompletedBrush,
        RecursionOutcome.BaseCase => BaseCaseBrush,
        RecursionOutcome.Success => SuccessBrush,
        RecursionOutcome.DeadEnd => DeadEndBrush,
        _ => RunningBrush,
    };

    // ------------------------------------------------------------- мышь

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var point = e.GetPosition(this);
        var factor = e.Delta.Y > 0 ? 1.2 : 1 / 1.2;
        var newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        // Масштабируем вокруг курсора: точка под мышью остаётся на месте.
        _offset = new Vector(
            point.X - (point.X - _offset.X) * newZoom / _zoom,
            point.Y - (point.Y - _offset.Y) * newZoom / _zoom);
        _zoom = newZoom;
        _userMoved = true;

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressPoint = e.GetPosition(this);
        _pressOffset = _offset;
        _dragging = false;
        e.Pointer.Capture(this);
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_pressPoint is not { } start)
        {
            return;
        }

        var delta = e.GetPosition(this) - start;
        if (!_dragging && Math.Abs(delta.X) + Math.Abs(delta.Y) < 4)
        {
            return;
        }

        _dragging = true;
        _userMoved = true;
        _offset = _pressOffset + delta;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_pressPoint is { } start && !_dragging)
        {
            _selected = HitTest(start);
            NodeClicked?.Invoke(_selected);
            InvalidateVisual();
        }

        _pressPoint = null;
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private int HitTest(Point point)
    {
        if (_tree is null)
        {
            return -1;
        }

        var radius = Math.Max(Math.Clamp(8 * _zoom, 1.5, 9) + 4, 8);
        var best = -1;
        var bestDistance = radius * radius;

        foreach (var node in _tree.Nodes)
        {
            if (!IsShown(node))
            {
                continue;
            }

            var center = ToScreen(node.Id);
            var dx = center.X - point.X;
            var dy = center.Y - point.Y;
            var distance = dx * dx + dy * dy;

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = node.Id;
            }
        }

        return best;
    }
}
