namespace Pente.Core;

/// <param name="TournamentRule">When true, White's second stone must be at least 3 intersections from the center.</param>
public sealed record GameOptions(bool TournamentRule = false)
{
    public const int CapturesToWin = 5;
    public const int TournamentDistance = 3;
}

public enum WinReason
{
    None,
    FiveInARow,
    Captures,
    Draw,
}

/// <summary>A move that was played, including the stones it captured.</summary>
public sealed record MoveRecord(Stone Player, Point Point, IReadOnlyList<Point> Captured);
