using System.Diagnostics;

namespace Pente.Core.Ai;

/// <summary>
/// Computer opponent. Easy picks greedily (with some randomness) from heuristic move scores;
/// Medium and Hard run a negamax alpha-beta search over the most promising candidate moves.
/// </summary>
public sealed class PenteAi : IPenteAi
{
    private readonly Random _random;
    private readonly int _maxDepth;
    private readonly int _width;
    private readonly TimeSpan _budget;

    private Stopwatch _clock = new();
    private bool _timedOut;
    private CancellationToken _ct;

    public PenteAi(Difficulty level, int? seed = null)
    {
        Level = level;
        _random = seed is { } s ? new Random(s) : new Random();
        (_maxDepth, _width, _budget) = level switch
        {
            Difficulty.Easy => (1, 6, TimeSpan.FromSeconds(1)),
            Difficulty.Medium => (2, 12, TimeSpan.FromSeconds(2)),
            _ => (6, 12, TimeSpan.FromMilliseconds(1800)),
        };
    }

    public Difficulty Level { get; }

    public Task<Point> ChooseMoveAsync(GameState state, CancellationToken cancellationToken = default)
    {
        if (state.IsOver) throw new InvalidOperationException("The game is over.");
        return Task.FromResult(ChooseMove(state.Clone(), cancellationToken));
    }

    private Point ChooseMove(GameState state, CancellationToken ct)
    {
        if (state.History.Count == 0) return Board.Center;

        var ordered = OrderedCandidates(state, int.MaxValue);
        if (ordered.Count == 0) throw new InvalidOperationException("No legal moves.");

        foreach (var (p, _) in ordered)
        {
            if (Evaluator.IsImmediateWin(state, p)) return p;
        }

        if (Level == Difficulty.Easy) return PickEasy(ordered);

        _ct = ct;
        _clock = Stopwatch.StartNew();
        _timedOut = false;

        var rootMoves = ordered.Take(_width).Select(m => m.Point).ToList();
        Point best = rootMoves[0];

        // Iterative deepening; keep the result of the deepest completed iteration.
        for (int depth = 1; depth <= _maxDepth; depth++)
        {
            int alpha = -Evaluator.Win - 1;
            Point iterationBest = rootMoves[0];
            var scores = new List<(Point Point, int Score)>(rootMoves.Count);

            foreach (var move in rootMoves)
            {
                state.Play(move);
                int score = -Negamax(state, depth - 1, -Evaluator.Win - 1, -alpha, 1);
                state.Undo();
                if (_timedOut) break;

                scores.Add((move, score));
                if (score > alpha)
                {
                    alpha = score;
                    iterationBest = move;
                }
            }

            if (_timedOut) break;
            best = iterationBest;

            // Found a forced win: no need to look deeper.
            if (alpha >= Evaluator.Win - 100) break;

            // Search the best moves first next iteration (better pruning).
            rootMoves = scores.OrderByDescending(s => s.Score).Select(s => s.Point).ToList();
        }

        return best;
    }

    private int Negamax(GameState state, int depth, int alpha, int beta, int ply)
    {
        if (state.IsOver)
        {
            // The player who just moved won (or draw).
            return state.Winner == Stone.None ? 0 : -(Evaluator.Win - ply);
        }

        if (depth == 0) return Evaluator.Evaluate(state);

        if (_clock.Elapsed > _budget || _ct.IsCancellationRequested)
        {
            _timedOut = true;
            return 0;
        }

        var moves = OrderedCandidates(state, _width);
        if (moves.Count == 0) return 0;

        // Immediate win available: take it without searching further.
        if (Evaluator.IsImmediateWin(state, moves[0].Point)) return Evaluator.Win - ply - 1;

        int best = -Evaluator.Win - 1;
        foreach (var (move, _) in moves)
        {
            state.Play(move);
            int score = -Negamax(state, depth - 1, -beta, -alpha, ply + 1);
            state.Undo();
            if (_timedOut) return 0;

            if (score > best) best = score;
            if (score > alpha) alpha = score;
            if (alpha >= beta) break;
        }
        return best;
    }

    private Point PickEasy(List<(Point Point, int Score)> ordered)
    {
        int bestScore = ordered[0].Score;
        // Always respond to serious threats (fours, capture wins, open fours).
        if (bestScore >= 5_000) return ordered[0].Point;

        var pool = ordered.Take(5).Where(m => m.Score >= bestScore / 3).ToList();
        return pool[_random.Next(pool.Count)].Point;
    }

    /// <summary>Legal empty points near existing stones, sorted by heuristic score (best first).</summary>
    internal static List<(Point Point, int Score)> OrderedCandidates(GameState state, int limit)
    {
        var board = state.Board;
        var seen = new bool[Board.Size * Board.Size];
        var result = new List<(Point, int)>(64);

        for (int r = 0; r < Board.Size; r++)
        {
            for (int c = 0; c < Board.Size; c++)
            {
                if (board[r, c] == Stone.None) continue;
                for (int dr = -2; dr <= 2; dr++)
                {
                    for (int dc = -2; dc <= 2; dc++)
                    {
                        int rr = r + dr, cc = c + dc;
                        if (!Board.InBounds(rr, cc)) continue;
                        int idx = rr * Board.Size + cc;
                        if (seen[idx]) continue;
                        seen[idx] = true;
                        var p = new Point(rr, cc);
                        if (board[rr, cc] == Stone.None && state.IsLegal(p))
                        {
                            result.Add((p, Evaluator.ScoreMove(state, p)));
                        }
                    }
                }
            }
        }

        // Tournament rule can forbid every nearby point: fall back to any legal point.
        if (result.Count == 0)
        {
            for (int r = 0; r < Board.Size; r++)
                for (int c = 0; c < Board.Size; c++)
                {
                    var p = new Point(r, c);
                    if (state.IsLegal(p)) result.Add((p, Evaluator.ScoreMove(state, p) - p.DistanceTo(Board.Center)));
                }
        }

        result.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        if (result.Count > limit) result.RemoveRange(limit, result.Count - limit);
        return result;
    }
}
