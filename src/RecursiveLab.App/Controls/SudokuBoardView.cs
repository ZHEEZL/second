using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using RecursiveLab.Core.Tasks.Sudoku;

namespace RecursiveLab.App.Controls;

/// <summary>
/// Отрисовка и ввод судоку. Контрол не решает головоломку — он только
/// показывает доску и даёт ввести/стереть значение. Алгоритм живёт в Core.
/// </summary>
public sealed class SudokuBoardView : Control
{
    private static readonly IBrush GivenBrush = new SolidColorBrush(Color.Parse("#E8EEF9"));
    private static readonly IBrush EntryBrush = new SolidColorBrush(Color.Parse("#67E8F9"));
    private static readonly IBrush ConflictBrush = new SolidColorBrush(Color.Parse("#F87171"));
    private static readonly IBrush GridThinBrush = new SolidColorBrush(Color.Parse("#1E2D4B"));
    private static readonly IBrush GridThickBrush = new SolidColorBrush(Color.Parse("#3A5585"));
    private static readonly IBrush CellHighlightBrush = new SolidColorBrush(Color.Parse("#14233C"));
    private static readonly IBrush RelatedHighlightBrush = new SolidColorBrush(Color.Parse("#101B31"));
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#1C355C"));
    private static readonly IBrush SelectedBorderBrush = new SolidColorBrush(Color.Parse("#67E8F9"));
    private static readonly IBrush BoardBackgroundBrush = new SolidColorBrush(Color.Parse("#0C1628"));
    private static readonly Typeface DigitTypeface =
        new(FontFamily.Parse("Inter, Segoe UI"), FontStyle.Normal, FontWeight.SemiBold);

    private SudokuBoard _board = new();
    private int _selectedIndex = -1;
    private IReadOnlyList<int> _conflicts = Array.Empty<int>();

    public SudokuBoardView()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public event Action? BoardChanged;

    public SudokuBoard Board
    {
        get => _board;
        set
        {
            _board = value ?? new SudokuBoard();
            _selectedIndex = -1;
            _conflicts = _board.FindConflicts();
            InvalidateVisual();
        }
    }

    public IReadOnlyList<int> Conflicts
    {
        get => _conflicts;
        set
        {
            _conflicts = value ?? Array.Empty<int>();
            InvalidateVisual();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var clamped = value < 0 ? -1 : Math.Clamp(value, 0, SudokuBoard.CellCount - 1);
            if (clamped == _selectedIndex)
            {
                return;
            }

            _selectedIndex = clamped;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var geometry = GetGeometry();
        var boardRect = geometry.BoardRect;
        var cell = geometry.CellSize;

        context.DrawRectangle(
            BoardBackgroundBrush,
            null,
            new RoundedRect(boardRect, 14));

        var selectedRow = _selectedIndex < 0 ? -1 : _selectedIndex / SudokuBoard.Size;
        var selectedColumn = _selectedIndex < 0 ? -1 : _selectedIndex % SudokuBoard.Size;

        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            var row = index / SudokuBoard.Size;
            var column = index % SudokuBoard.Size;
            var x = boardRect.X + column * cell;
            var y = boardRect.Y + row * cell;

            var brush = GetCellBrush(index, row, column, selectedRow, selectedColumn);
            if (brush is not null)
            {
                context.DrawRectangle(brush, null, new Rect(x, y, cell, cell));
            }
        }

        DrawGrid(context, boardRect, cell);
        DrawDigits(context, boardRect, cell);

        if (_selectedIndex >= 0)
        {
            var row = _selectedIndex / SudokuBoard.Size;
            var column = _selectedIndex % SudokuBoard.Size;
            var rect = new Rect(
                boardRect.X + column * cell + 1,
                boardRect.Y + row * cell + 1,
                cell - 2,
                cell - 2);

            context.DrawRectangle(null, new Pen(SelectedBorderBrush, 2), rect);
        }
    }

    private IBrush? GetCellBrush(int index, int row, int column, int selectedRow, int selectedColumn)
    {
        if (index == _selectedIndex)
        {
            return SelectedBrush;
        }

        if (selectedRow < 0)
        {
            return null;
        }

        var sameBox = row / 3 == selectedRow / 3 && column / 3 == selectedColumn / 3;
        if (row == selectedRow || column == selectedColumn || sameBox)
        {
            return row == selectedRow || column == selectedColumn ? CellHighlightBrush : RelatedHighlightBrush;
        }

        return null;
    }

    private static void DrawGrid(DrawingContext context, Rect boardRect, double cell)
    {
        var thinPen = new Pen(GridThinBrush, 1);
        var thickPen = new Pen(GridThickBrush, 2.2);

        for (var i = 0; i <= SudokuBoard.Size; i++)
        {
            var pen = i % 3 == 0 ? thickPen : thinPen;
            var x = boardRect.X + i * cell;
            var y = boardRect.Y + i * cell;

            context.DrawLine(pen, new Point(x, boardRect.Y), new Point(x, boardRect.Bottom));
            context.DrawLine(pen, new Point(boardRect.X, y), new Point(boardRect.Right, y));
        }
    }

    private void DrawDigits(DrawingContext context, Rect boardRect, double cell)
    {
        var fontSize = cell * 0.5;

        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            var value = _board[index];
            if (value == 0)
            {
                continue;
            }

            var row = index / SudokuBoard.Size;
            var column = index % SudokuBoard.Size;

            var isConflict = _conflicts.Contains(index);
            var isGiven = _board.IsGiven(index);

            var brush = isConflict ? ConflictBrush : isGiven ? GivenBrush : EntryBrush;
            var typeface = isGiven
                ? new Typeface(DigitTypeface.FontFamily, FontStyle.Normal, FontWeight.Bold)
                : DigitTypeface;

            var text = new FormattedText(
                value.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush);

            var x = boardRect.X + column * cell + (cell - text.Width) / 2;
            var y = boardRect.Y + row * cell + (cell - text.Height) / 2;
            context.DrawText(text, new Point(x, y));
        }
    }

    private (Rect BoardRect, double CellSize) GetGeometry()
    {
        var size = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) - 32);
        var left = (Bounds.Width - size) / 2;
        var top = (Bounds.Height - size) / 2;
        return (new Rect(left, top, size, size), size / SudokuBoard.Size);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);
        var geometry = GetGeometry();

        if (!geometry.BoardRect.Contains(point) || geometry.CellSize <= 0)
        {
            return;
        }

        var column = (int)((point.X - geometry.BoardRect.X) / geometry.CellSize);
        var row = (int)((point.Y - geometry.BoardRect.Y) / geometry.CellSize);
        column = Math.Clamp(column, 0, SudokuBoard.Size - 1);
        row = Math.Clamp(row, 0, SudokuBoard.Size - 1);

        SelectedIndex = row * SudokuBoard.Size + column;
        Focus();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_selectedIndex < 0)
        {
            return;
        }

        var deltaRow = 0;
        var deltaColumn = 0;

        switch (e.Key)
        {
            case Key.Left:
                deltaColumn = -1;
                break;
            case Key.Right:
                deltaColumn = 1;
                break;
            case Key.Up:
                deltaRow = -1;
                break;
            case Key.Down:
                deltaRow = 1;
                break;
            case Key.Back:
            case Key.Delete:
                if (_board.TryClear(_selectedIndex))
                {
                    AfterBoardMutation();
                }

                e.Handled = true;
                return;
            default:
                if (TryGetDigit(e.Key, out var digit))
                {
                    if (_board.TrySet(_selectedIndex, digit))
                    {
                        AfterBoardMutation();
                    }

                    e.Handled = true;
                }

                return;
        }

        var rowIndex = _selectedIndex / SudokuBoard.Size + deltaRow;
        var columnIndex = _selectedIndex % SudokuBoard.Size + deltaColumn;

        if (rowIndex is >= 0 and < SudokuBoard.Size && columnIndex is >= 0 and < SudokuBoard.Size)
        {
            SelectedIndex = rowIndex * SudokuBoard.Size + columnIndex;
        }

        e.Handled = true;
    }

    private static bool TryGetDigit(Key key, out int digit)
    {
        switch (key)
        {
            case >= Key.D1 and <= Key.D9:
                digit = key - Key.D0;
                return true;
            case >= Key.NumPad1 and <= Key.NumPad9:
                digit = key - Key.NumPad0;
                return true;
            default:
                digit = 0;
                return false;
        }
    }

    private void AfterBoardMutation()
    {
        _conflicts = _board.FindConflicts();
        InvalidateVisual();
        BoardChanged?.Invoke();
    }
}
