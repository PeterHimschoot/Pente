namespace Pente.Core;

/// <summary>A full game of Pente: board, turn, captures, history and result.</summary>
public sealed class GameState
{
    private readonly List<MoveRecord> _history;
    private readonly int[] _captures; // pairs captured, indexed by (int)Stone

    public GameState(GameOptions? options = null)
    {
        Options = options ?? new GameOptions();
        Board = new Board();
        _history = [];
        _captures = new int[3];
        CurrentPlayer = Stone.White;
    }

    private GameState(GameState other)
    {
        Options = other.Options;
        Board = other.Board.Clone();
        _history = [.. other._history];
        _captures = (int[])other._captures.Clone();
        CurrentPlayer = other.CurrentPlayer;
        Winner = other.Winner;
        WinReason = other.WinReason;
        WinLine = other.WinLine;
    }

    public GameOptions Options { get; }
    public Board Board { get; }
    public Stone CurrentPlayer { get; private set; }
    public IReadOnlyList<MoveRecord> History => _history;
    public MoveRecord? LastMove => _history.Count > 0 ? _history[^1] : null;
    public Stone Winner { get; private set; }
    public WinReason WinReason { get; private set; }
    public IReadOnlyList<Point> WinLine { get; private set; } = [];
    public bool IsOver => WinReason != WinReason.None;

    /// <summary>Number of pairs captured by <paramref name="player"/>.</summary>
    public int Captures(Stone player) => _captures[(int)player];

    public bool IsLegal(Point p) => IsLegal(p, out _);

    public bool IsLegal(Point p, out string reason)
    {
        if (IsOver) { reason = "The game is over."; return false; }
        if (!Board.InBounds(p)) { reason = "Outside the board."; return false; }
        if (Board[p] != Stone.None) { reason = "That intersection is occupied."; return false; }
        if (_history.Count == 0 && p != Board.Center)
        {
            reason = "The first stone must be placed on the center point.";
            return false;
        }
        if (Options.TournamentRule && _history.Count == 2 && p.DistanceTo(Board.Center) < GameOptions.TournamentDistance)
        {
            reason = "Tournament rule: White's second stone must be at least 3 intersections from the center.";
            return false;
        }
        reason = "";
        return true;
    }

    public MoveRecord Play(Point p)
    {
        if (!IsLegal(p, out var reason)) throw new InvalidOperationException(reason);

        var player = CurrentPlayer;
        var captured = GameRules.FindCaptures(Board, p, player);
        Board[p] = player;
        foreach (var c in captured) Board[c] = Stone.None;
        _captures[(int)player] += captured.Count / 2;

        var record = new MoveRecord(player, p, captured);
        _history.Add(record);

        var line = GameRules.FindFiveInARow(Board, p);
        if (line.Count > 0)
        {
            SetResult(player, WinReason.FiveInARow, line);
        }
        else if (_captures[(int)player] >= GameOptions.CapturesToWin)
        {
            SetResult(player, WinReason.Captures, []);
        }
        else if (Board.StoneCount == Board.Size * Board.Size)
        {
            SetResult(Stone.None, WinReason.Draw, []);
        }

        CurrentPlayer = player.Opponent();
        return record;
    }

    public bool Undo()
    {
        if (_history.Count == 0) return false;
        var record = _history[^1];
        _history.RemoveAt(_history.Count - 1);

        Board[record.Point] = Stone.None;
        var opp = record.Player.Opponent();
        foreach (var c in record.Captured) Board[c] = opp;
        _captures[(int)record.Player] -= record.Captured.Count / 2;

        CurrentPlayer = record.Player;
        SetResult(Stone.None, WinReason.None, []);
        return true;
    }

    public GameState Clone() => new(this);

    private void SetResult(Stone winner, WinReason reason, IReadOnlyList<Point> line)
    {
        Winner = winner;
        WinReason = reason;
        WinLine = line;
    }
}
