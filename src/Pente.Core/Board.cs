namespace Pente.Core;

public sealed class Board
{
    public const int Size = 19;
    public static readonly Point Center = new(Size / 2, Size / 2);

    private readonly Stone[] _cells;

    public Board() => _cells = new Stone[Size * Size];

    private Board(Stone[] cells, int stoneCount)
    {
        _cells = cells;
        StoneCount = stoneCount;
    }

    public int StoneCount { get; private set; }

    public static bool InBounds(int row, int col) => (uint)row < Size && (uint)col < Size;

    public static bool InBounds(Point p) => InBounds(p.Row, p.Col);

    /// <summary>Reading outside the board returns <see cref="Stone.None"/>.</summary>
    public Stone this[int row, int col]
    {
        get => InBounds(row, col) ? _cells[row * Size + col] : Stone.None;
        set
        {
            ref var cell = ref _cells[row * Size + col];
            if (cell == Stone.None && value != Stone.None) StoneCount++;
            else if (cell != Stone.None && value == Stone.None) StoneCount--;
            cell = value;
        }
    }

    public Stone this[Point p]
    {
        get => this[p.Row, p.Col];
        set => this[p.Row, p.Col] = value;
    }

    public bool IsEmpty(Point p) => InBounds(p) && this[p] == Stone.None;

    public Board Clone() => new((Stone[])_cells.Clone(), StoneCount);
}
