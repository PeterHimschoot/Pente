namespace Pente.Core;

public static class GameRules
{
    /// <summary>The four line directions (the other four are their negations).</summary>
    public static readonly (int dr, int dc)[] LineDirections = [(0, 1), (1, 0), (1, 1), (1, -1)];

    /// <summary>All eight directions.</summary>
    public static readonly (int dr, int dc)[] AllDirections =
        [(0, 1), (1, 0), (1, 1), (1, -1), (0, -1), (-1, 0), (-1, -1), (-1, 1)];

    /// <summary>
    /// Opponent stones that would be captured if <paramref name="player"/> placed a stone at <paramref name="p"/>.
    /// The board is not modified.
    /// </summary>
    public static List<Point> FindCaptures(Board board, Point p, Stone player)
    {
        var captured = new List<Point>(0);
        var opp = player.Opponent();
        foreach (var (dr, dc) in AllDirections)
        {
            if (board[p.Row + dr, p.Col + dc] == opp
                && board[p.Row + 2 * dr, p.Col + 2 * dc] == opp
                && board[p.Row + 3 * dr, p.Col + 3 * dc] == player)
            {
                captured.Add(p.Offset(dr, dc));
                captured.Add(p.Offset(dr, dc, 2));
            }
        }
        return captured;
    }

    /// <summary>Number of opponent pairs a stone at <paramref name="p"/> would capture.</summary>
    public static int CountCapturePairs(Board board, Point p, Stone player)
    {
        int count = 0;
        var opp = player.Opponent();
        foreach (var (dr, dc) in AllDirections)
        {
            if (board[p.Row + dr, p.Col + dc] == opp
                && board[p.Row + 2 * dr, p.Col + 2 * dc] == opp
                && board[p.Row + 3 * dr, p.Col + 3 * dc] == player)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// If the stone at <paramref name="p"/> is part of a line of five or more of its color,
    /// returns the points of the longest such line; otherwise an empty list.
    /// </summary>
    public static IReadOnlyList<Point> FindFiveInARow(Board board, Point p)
    {
        var player = board[p];
        if (player == Stone.None) return [];

        List<Point>? best = null;
        foreach (var (dr, dc) in LineDirections)
        {
            int back = 0;
            while (board[p.Row - dr * (back + 1), p.Col - dc * (back + 1)] == player) back++;
            int fwd = 0;
            while (board[p.Row + dr * (fwd + 1), p.Col + dc * (fwd + 1)] == player) fwd++;
            int length = back + fwd + 1;
            if (length >= 5 && (best is null || length > best.Count))
            {
                best = new List<Point>(length);
                for (int i = -back; i <= fwd; i++) best.Add(p.Offset(dr, dc, i));
            }
        }
        return best ?? (IReadOnlyList<Point>)[];
    }

    /// <summary>Would a stone of <paramref name="player"/> at empty point <paramref name="p"/> make five or more?</summary>
    public static bool MakesFive(Board board, Point p, Stone player)
    {
        foreach (var (dr, dc) in LineDirections)
        {
            int count = 1;
            for (int i = 1; i < 5 && board[p.Row + dr * i, p.Col + dc * i] == player; i++) count++;
            for (int i = 1; i < 5 && board[p.Row - dr * i, p.Col - dc * i] == player; i++) count++;
            if (count >= 5) return true;
        }
        return false;
    }
}
