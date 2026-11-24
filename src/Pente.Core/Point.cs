namespace Pente.Core;

/// <summary>An intersection on the board, zero-based.</summary>
public readonly record struct Point(int Row, int Col)
{
    public Point Offset(int dRow, int dCol, int steps = 1) => new(Row + dRow * steps, Col + dCol * steps);

    /// <summary>Chebyshev (king-move) distance.</summary>
    public int DistanceTo(Point other) => Math.Max(Math.Abs(Row - other.Row), Math.Abs(Col - other.Col));

    /// <summary>Board notation, e.g. K10 for the center (column letters skip I).</summary>
    public string ToNotation() => $"{(char)('A' + Col + (Col >= 8 ? 1 : 0))}{Board.Size - Row}";
}
