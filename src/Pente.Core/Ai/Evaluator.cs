namespace Pente.Core.Ai;

/// <summary>Heuristic scoring of moves (for ordering) and positions (for search leaves).</summary>
public static class Evaluator
{
    public const int Win = 10_000_000;
    public const int FiveValue = 1_000_000;

    // Score of a run of own stones through a point, indexed by length (capped at 4), for 0/1/2 open ends.
    private static readonly int[,] RunScore =
    {
        //  0 open, 1 open, 2 open
        { 0, 0, 0 },          // len 0
        { 0, 1, 3 },          // len 1
        { 0, 20, 100 },       // len 2
        { 0, 300, 4_000 },    // len 3
        { 0, 5_000, 100_000 },// len 4
    };

    // Score of a 5-window holding only one color, indexed by stone count.
    private static readonly int[] WindowMoveScore = [0, 1, 20, 300, 5_000, FiveValue];
    private static readonly int[] WindowPositionScore = [0, 1, 12, 120, 1_500, FiveValue];
    private static readonly int[] CaptureCountScore = [0, 500, 1_200, 2_500, 5_000, Win];

    /// <summary>
    /// How attractive empty point <paramref name="p"/> is for the side to move:
    /// its own attacking value plus the value of denying the point to the opponent.
    /// </summary>
    public static int ScoreMove(GameState state, Point p)
    {
        var me = state.CurrentPlayer;
        var opp = me.Opponent();
        long attack = PointValue(state.Board, p, me) + CaptureValue(state, p, me);
        long defense = PointValue(state.Board, p, opp) + CaptureValue(state, p, opp);
        long score = attack + defense * 9 / 10;
        return (int)Math.Min(score, int.MaxValue);
    }

    /// <summary>Is placing a stone of the side to move at <paramref name="p"/> an immediate win?</summary>
    public static bool IsImmediateWin(GameState state, Point p)
    {
        var me = state.CurrentPlayer;
        return GameRules.MakesFive(state.Board, p, me)
            || state.Captures(me) + GameRules.CountCapturePairs(state.Board, p, me) >= GameOptions.CapturesToWin;
    }

    private static int CaptureValue(GameState state, Point p, Stone player)
    {
        int pairs = GameRules.CountCapturePairs(state.Board, p, player);
        if (pairs == 0) return 0;
        int have = state.Captures(player);
        if (have + pairs >= GameOptions.CapturesToWin) return FiveValue;
        return pairs * (1_500 + 700 * have);
    }

    /// <summary>Line-building value of a stone of <paramref name="player"/> at empty point <paramref name="p"/>.</summary>
    private static int PointValue(Board board, Point p, Stone player)
    {
        var opp = player.Opponent();
        int total = 0;
        foreach (var (dr, dc) in GameRules.LineDirections)
        {
            // Contiguous run through p.
            int fwd = 0;
            while (board[p.Row + dr * (fwd + 1), p.Col + dc * (fwd + 1)] == player) fwd++;
            int back = 0;
            while (board[p.Row - dr * (back + 1), p.Col - dc * (back + 1)] == player) back++;
            int len = fwd + back + 1;
            if (len >= 5) return FiveValue;

            int open = 0;
            if (board.IsEmpty(p.Offset(dr, dc, fwd + 1))) open++;
            if (board.IsEmpty(p.Offset(-dr, -dc, back + 1))) open++;
            int dirBest = RunScore[len, open];

            // Split shapes: best 5-window through p containing no opponent stones.
            for (int start = -4; start <= 0; start++)
            {
                int count = 1;
                bool blocked = false;
                for (int i = start; i < start + 5; i++)
                {
                    if (i == 0) continue;
                    int r = p.Row + dr * i, c = p.Col + dc * i;
                    if (!Board.InBounds(r, c)) { blocked = true; break; }
                    var s = board[r, c];
                    if (s == opp) { blocked = true; break; }
                    if (s == player) count++;
                }
                if (!blocked) dirBest = Math.Max(dirBest, WindowMoveScore[count]);
            }

            total += dirBest;
        }
        return total;
    }

    /// <summary>Static evaluation from the perspective of the side to move.</summary>
    public static int Evaluate(GameState state)
    {
        var board = state.Board;
        var me = state.CurrentPlayer;
        var opp = me.Opponent();

        Span<long> lines = stackalloc long[3];
        Span<int> fours = stackalloc int[3];
        Span<int> captureChances = stackalloc int[3]; // opportunities for the indexed color to capture

        const int n = Board.Size;
        foreach (var (dr, dc) in GameRules.LineDirections)
        {
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    // 5-window starting at (r, c).
                    int er = r + dr * 4, ec = c + dc * 4;
                    if (Board.InBounds(er, ec))
                    {
                        int w = 0, b = 0;
                        for (int i = 0; i < 5; i++)
                        {
                            var s = board[r + dr * i, c + dc * i];
                            if (s == Stone.White) w++;
                            else if (s == Stone.Black) b++;
                        }
                        if (w > 0 && b == 0)
                        {
                            lines[(int)Stone.White] += WindowPositionScore[w];
                            if (w == 4) fours[(int)Stone.White]++;
                        }
                        else if (b > 0 && w == 0)
                        {
                            lines[(int)Stone.Black] += WindowPositionScore[b];
                            if (b == 4) fours[(int)Stone.Black]++;
                        }
                    }

                    // Capture pattern a-x-x-d starting at (r, c).
                    int dr3 = r + dr * 3, dc3 = c + dc * 3;
                    if (Board.InBounds(dr3, dc3))
                    {
                        var x1 = board[r + dr, c + dc];
                        if (x1 != Stone.None && board[r + 2 * dr, c + 2 * dc] == x1)
                        {
                            var a = board[r, c];
                            var d = board[dr3, dc3];
                            var enemy = x1.Opponent();
                            if ((a == enemy && d == Stone.None) || (a == Stone.None && d == enemy))
                            {
                                captureChances[(int)enemy]++;
                            }
                        }
                    }
                }
            }
        }

        int myCaps = state.Captures(me);
        int oppCaps = state.Captures(opp);

        // Side to move completes a four (or takes the fifth pair) right away.
        if (fours[(int)me] > 0) return Win / 2;
        if (myCaps >= GameOptions.CapturesToWin - 1 && captureChances[(int)me] > 0) return Win / 2;

        long score = lines[(int)me] * 11 / 10 - lines[(int)opp];
        score += CaptureCountScore[Math.Min(myCaps, 4)] - CaptureCountScore[Math.Min(oppCaps, 4)];
        score += captureChances[(int)me] * (300 + 150 * myCaps);
        score -= captureChances[(int)opp] * (200 + 150 * oppCaps);

        // Two separate fours for the opponent usually cannot both be blocked.
        if (fours[(int)opp] >= 2) score -= 200_000;

        return (int)Math.Clamp(score, -Win / 4, Win / 4);
    }
}
