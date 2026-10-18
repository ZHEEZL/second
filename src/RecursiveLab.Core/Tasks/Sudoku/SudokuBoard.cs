namespace RecursiveLab.Core.Tasks.Sudoku;

/// <summary>
/// Доска судоку 9×9. Отделена от UI, чтобы алгоритм можно было реализовать
/// и протестировать без запуска окна.
/// </summary>
public sealed class SudokuBoard
{
    public const int Size = 9;
    public const int CellCount = Size * Size;

    private readonly int[] _cells;
    private readonly bool[] _givens;

    public SudokuBoard()
    {
        _cells = new int[CellCount];
        _givens = new bool[CellCount];
    }

    private SudokuBoard(int[] cells, bool[] givens)
    {
        _cells = cells;
        _givens = givens;
    }

    /// <summary>Значение в клетке по индексу 0..80 (0 = пусто).</summary>
    public int this[int index] => _cells[index];

    public int Get(int row, int column) => _cells[row * Size + column];

    /// <summary>Стартовая (заданная условием) клетка — её нельзя менять.</summary>
    public bool IsGiven(int index) => _givens[index];

    public SudokuBoard Clone() => new((int[])_cells.Clone(), (bool[])_givens.Clone());

    /// <summary>Ставит значение (0..9); для стартовых клеток значение не меняется.</summary>
    public bool TrySet(int index, int value, bool asGiven = false)
    {
        if (index is < 0 or >= CellCount || value is < 0 or > Size)
        {
            return false;
        }

        if (_givens[index])
        {
            return false;
        }

        _cells[index] = value;
        if (asGiven)
        {
            _givens[index] = true;
        }

        return true;
    }

    /// <summary>Очищает клетку, если она не стартовая.</summary>
    public bool TryClear(int index)
    {
        if (index is < 0 or >= CellCount || _givens[index])
        {
            return false;
        }

        _cells[index] = 0;
        return true;
    }

    public void SetGiven(int index, int value)
    {
        if (index is < 0 or >= CellCount || value is < 1 or > Size)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _cells[index] = value;
        _givens[index] = true;
    }

    public void ClearAll()
    {
        Array.Clear(_cells);
        Array.Clear(_givens);
    }

    /// <summary>
    /// Разбирает строку из 81 символа: цифры 1..9 — значения,
    /// '0', '.', '-' — пустые клетки. Пробелы, переводы строк и '|' игнорируются.
    /// </summary>
    public static SudokuBoard Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var board = new SudokuBoard();
        var index = 0;

        foreach (var symbol in text)
        {
            if (char.IsWhiteSpace(symbol) || symbol is '|' or '/')
            {
                continue;
            }

            if (index >= CellCount)
            {
                break;
            }

            switch (symbol)
            {
                case >= '1' and <= '9':
                    board._cells[index] = symbol - '0';
                    board._givens[index] = true;
                    index++;
                    break;
                case '0' or '.' or '-':
                    index++;
                    break;
                default:
                    throw new FormatException($"Недопустимый символ судоку: '{symbol}'.");
            }
        }

        if (index != CellCount)
        {
            throw new FormatException($"Ожидалось {CellCount} клеток, получено {index}.");
        }

        return board;
    }

    public string ToCompactString() => string.Concat(_cells.Select(value => (char)('0' + value)));

    public static (int Row, int Column) ToRowColumn(int index) => (index / Size, index % Size);

    public static int ToIndex(int row, int column) => row * Size + column;

    /// <summary>Может ли значение стоять в клетке по правилам судоку (без учёта самой клетки).</summary>
    public bool IsValidPlacement(int index, int value)
    {
        if (value is < 1 or > Size)
        {
            return false;
        }

        var (row, column) = ToRowColumn(index);

        for (var i = 0; i < Size; i++)
        {
            if (_cells[row * Size + i] == value && i != column)
            {
                return false;
            }

            if (_cells[i * Size + column] == value && i != row)
            {
                return false;
            }
        }

        var boxRow = row / 3 * 3;
        var boxColumn = column / 3 * 3;

        for (var r = boxRow; r < boxRow + 3; r++)
        {
            for (var c = boxColumn; c < boxColumn + 3; c++)
            {
                var current = r * Size + c;
                if (current != index && _cells[current] == value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Индексы клеток, которые конфликтуют с другими (для подсветки ошибок в UI).</summary>
    public IReadOnlyList<int> FindConflicts()
    {
        var conflicts = new HashSet<int>();

        for (var index = 0; index < CellCount; index++)
        {
            var value = _cells[index];
            if (value == 0)
            {
                continue;
            }

            var (row, column) = ToRowColumn(index);

            for (var i = 0; i < Size; i++)
            {
                Check(index, row * Size + i, value, conflicts);
                Check(index, i * Size + column, value, conflicts);
            }

            var boxRow = row / 3 * 3;
            var boxColumn = column / 3 * 3;

            for (var r = boxRow; r < boxRow + 3; r++)
            {
                for (var c = boxColumn; c < boxColumn + 3; c++)
                {
                    Check(index, r * Size + c, value, conflicts);
                }
            }
        }

        return conflicts.OrderBy(value => value).ToArray();
    }

    private void Check(int index, int other, int value, HashSet<int> conflicts)
    {
        if (other == index || _cells[other] != value)
        {
            return;
        }

        conflicts.Add(index);
        conflicts.Add(other);
    }

    /// <summary>Все ли клетки заполнены и нет ли конфликтов.</summary>
    public bool IsSolved()
    {
        if (_cells.Any(value => value == 0))
        {
            return false;
        }

        return FindConflicts().Count == 0;
    }

    /// <summary>Список возможных значений для клетки (удобно для эвристики MRV).</summary>
    public IReadOnlyList<int> GetCandidates(int index)
    {
        if (_cells[index] != 0)
        {
            return Array.Empty<int>();
        }

        var candidates = new List<int>(Size);
        for (var value = 1; value <= Size; value++)
        {
            if (IsValidPlacement(index, value))
            {
                candidates.Add(value);
            }
        }

        return candidates;
    }

    public void CopyFrom(SudokuBoard other)
    {
        Array.Copy(other._cells, _cells, CellCount);
        Array.Copy(other._givens, _givens, CellCount);
    }
}
