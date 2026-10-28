namespace RecursiveLab.Core.Recursion;

/// <summary>Чем закончился рекурсивный вызов.</summary>
public enum RecursionOutcome
{
    /// <summary>Вызов ещё не завершился (он сейчас в стеке вызовов).</summary>
    Running,

    /// <summary>Обычный вызов: сделал свою часть работы и вернулся.</summary>
    Completed,

    /// <summary>Базовый случай: рекурсия здесь остановилась.</summary>
    BaseCase,

    /// <summary>Ветка привела к ответу (перебор с возвратом нашёл решение).</summary>
    Success,

    /// <summary>Тупик: ветка не подошла, выполнен откат.</summary>
    DeadEnd,
}

/// <summary>
/// «Наблюдатель» за рекурсией. Алгоритм сообщает ему о входе в вызов и о выходе
/// из него; из этих сообщений строится дерево рекурсии. Если трассировщик не
/// передан (null), алгоритм работает как раньше и ничего не тратит на запись.
/// </summary>
public interface IRecursionTracer
{
    /// <summary>Вход в рекурсивный вызов. Возвращает номер узла, который потом передаётся в <see cref="Exit"/>.</summary>
    int Enter(string label, string? detail = null);

    /// <summary>Пометка к текущему (самому глубокому незавершённому) вызову.</summary>
    void Note(string text);

    /// <summary>Выход из вызова. Вызовы должны закрываться в обратном порядке, как в стеке.</summary>
    void Exit(int nodeId, RecursionOutcome outcome, string? result = null);
}

/// <summary>Один узел дерева = один рекурсивный вызов.</summary>
public sealed class RecursionNode
{
    internal readonly List<int> ChildList = new();

    internal RecursionNode(int id, int parentId, int depth, string label, string? detail, int enterEvent)
    {
        Id = id;
        ParentId = parentId;
        Depth = depth;
        Label = label;
        Detail = detail;
        EnterEvent = enterEvent;
    }

    public int Id { get; }

    /// <summary>Номер родительского вызова или −1 для корня.</summary>
    public int ParentId { get; }

    /// <summary>Глубина рекурсии: 0 — корень.</summary>
    public int Depth { get; }

    /// <summary>Короткая подпись для дерева.</summary>
    public string Label { get; }

    /// <summary>Подробное описание вызова (параметры).</summary>
    public string? Detail { get; }

    public RecursionOutcome Outcome { get; internal set; } = RecursionOutcome.Running;

    /// <summary>Что вернул вызов / что сделал.</summary>
    public string? Result { get; internal set; }

    /// <summary>Пометки, сделанные во время вызова.</summary>
    public string? Note { get; internal set; }

    /// <summary>Номер события «вход» в общей ленте событий.</summary>
    public int EnterEvent { get; }

    /// <summary>Номер события «выход» или −1, если вызов не завершён.</summary>
    public int ExitEvent { get; internal set; } = -1;

    public IReadOnlyList<int> Children => ChildList;
}

public enum RecursionEventKind
{
    Enter,
    Exit,
}

/// <summary>Событие в ленте: вход в вызов или выход из него. По ленте дерево можно «проиграть».</summary>
public readonly record struct RecursionEvent(RecursionEventKind Kind, int NodeId);

/// <summary>Готовое дерево рекурсии.</summary>
public sealed class RecursionTree
{
    internal RecursionTree(
        string title,
        string nodeMeaning,
        IReadOnlyList<RecursionNode> nodes,
        IReadOnlyList<RecursionEvent> events,
        IReadOnlyList<int> roots,
        long totalCalls,
        long droppedCalls)
    {
        Title = title;
        NodeMeaning = nodeMeaning;
        Nodes = nodes;
        Events = events;
        Roots = roots;
        TotalCalls = totalCalls;
        DroppedCalls = droppedCalls;
        MaxDepth = nodes.Count == 0 ? 0 : nodes.Max(node => node.Depth);
    }

    public string Title { get; }

    /// <summary>Что означает один узел (например, «вызов Move(n, откуда, куда)»).</summary>
    public string NodeMeaning { get; }

    public IReadOnlyList<RecursionNode> Nodes { get; }

    public IReadOnlyList<RecursionEvent> Events { get; }

    public IReadOnlyList<int> Roots { get; }

    /// <summary>Сколько вызовов было на самом деле (включая не записанные из-за лимита).</summary>
    public long TotalCalls { get; }

    /// <summary>Сколько вызовов не записано из-за лимита узлов.</summary>
    public long DroppedCalls { get; }

    public bool IsTruncated => DroppedCalls > 0;

    public int MaxDepth { get; }

    public int Count(RecursionOutcome outcome) => Nodes.Count(node => node.Outcome == outcome);

    public int LeafCount => Nodes.Count(node => node.ChildList.Count == 0);
}

/// <summary>
/// Записывает вызовы в дерево. Родитель нового вызова — тот, что сейчас на вершине
/// стека (так же устроен настоящий стек вызовов). Чтобы окно не тормозило,
/// записывается не больше <c>maxNodes</c> узлов; остальные только считаются.
/// </summary>
public sealed class RecursionTraceRecorder : IRecursionTracer
{
    public const int DefaultMaxNodes = 6000;

    /// <summary>Номер, который получают вызовы сверх лимита.</summary>
    public const int DroppedNodeId = -2;

    private readonly string _title;
    private readonly string _nodeMeaning;
    private readonly int _maxNodes;
    private readonly List<RecursionNode> _nodes = new();
    private readonly List<RecursionEvent> _events = new();
    private readonly List<int> _roots = new();
    private readonly Stack<int> _stack = new();
    private long _totalCalls;
    private long _droppedCalls;

    public RecursionTraceRecorder(string title, string nodeMeaning, int maxNodes = DefaultMaxNodes)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(nodeMeaning);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxNodes, 1);

        _title = title;
        _nodeMeaning = nodeMeaning;
        _maxNodes = maxNodes;
    }

    /// <summary>Текущая глубина стека вызовов.</summary>
    public int StackDepth => _stack.Count;

    public int Enter(string label, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        _totalCalls++;

        var parentId = _stack.Count == 0 ? -1 : _stack.Peek();

        // Сверх лимита (или внутри уже отброшенной ветки) — только считаем.
        if (_nodes.Count >= _maxNodes || parentId == DroppedNodeId)
        {
            _droppedCalls++;
            _stack.Push(DroppedNodeId);
            return DroppedNodeId;
        }

        var node = new RecursionNode(_nodes.Count, parentId, _stack.Count, label, detail, _events.Count);
        _nodes.Add(node);

        if (parentId >= 0)
        {
            _nodes[parentId].ChildList.Add(node.Id);
        }
        else
        {
            _roots.Add(node.Id);
        }

        _events.Add(new RecursionEvent(RecursionEventKind.Enter, node.Id));
        _stack.Push(node.Id);
        return node.Id;
    }

    public void Note(string text)
    {
        if (_stack.Count == 0 || _stack.Peek() < 0)
        {
            return;
        }

        var node = _nodes[_stack.Peek()];
        node.Note = node.Note is null ? text : node.Note + "; " + text;
    }

    public void Exit(int nodeId, RecursionOutcome outcome, string? result = null)
    {
        if (_stack.Count == 0 || _stack.Peek() != nodeId)
        {
            throw new InvalidOperationException(
                "Выход не из того вызова: Enter/Exit должны идти парами, как вход и выход в стеке вызовов.");
        }

        _stack.Pop();

        if (nodeId == DroppedNodeId)
        {
            return;
        }

        var node = _nodes[nodeId];
        node.Outcome = outcome;
        node.Result = result;
        node.ExitEvent = _events.Count;
        _events.Add(new RecursionEvent(RecursionEventKind.Exit, nodeId));
    }

    /// <summary>Собирает дерево. Все вызовы к этому моменту должны быть завершены.</summary>
    public RecursionTree Build()
    {
        if (_stack.Count != 0)
        {
            throw new InvalidOperationException($"Не завершено вызовов: {_stack.Count}.");
        }

        return new RecursionTree(
            _title,
            _nodeMeaning,
            _nodes.ToArray(),
            _events.ToArray(),
            _roots.ToArray(),
            _totalCalls,
            _droppedCalls);
    }
}
