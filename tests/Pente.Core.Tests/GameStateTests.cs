using Pente.Core;

namespace Pente.Core.Tests;

public class GameStateTests
{
    private static readonly Point Center = new(9, 9);

    /// <summary>Plays a list of moves alternating White/Black starting with White.</summary>
    private static GameState Play(GameState game, params (int r, int c)[] moves)
    {
        foreach (var (r, c) in moves)
        {
            game.Play(new Point(r, c));
        }
        return game;
    }

    [Fact]
    public void NewGame_WhiteToMove_EmptyBoard()
    {
        var game = new GameState();
        Assert.Equal(Stone.White, game.CurrentPlayer);
        Assert.Equal(0, game.Board.StoneCount);
        Assert.False(game.IsOver);
    }

    [Fact]
    public void FirstMove_MustBeCenter()
    {
        var game = new GameState();
        Assert.False(game.IsLegal(new Point(3, 3), out _));
        Assert.True(game.IsLegal(Center, out _));
        Assert.Throws<InvalidOperationException>(() => game.Play(new Point(3, 3)));
    }

    [Fact]
    public void Play_PlacesStone_AndSwitchesPlayer()
    {
        var game = new GameState();
        game.Play(Center);
        Assert.Equal(Stone.White, game.Board[Center]);
        Assert.Equal(Stone.Black, game.CurrentPlayer);
    }

    [Fact]
    public void OccupiedAndOutOfBoundsMoves_AreIllegal()
    {
        var game = Play(new GameState(), (9, 9));
        Assert.False(game.IsLegal(Center, out _));
        Assert.False(game.IsLegal(new Point(-1, 0), out _));
        Assert.False(game.IsLegal(new Point(0, 19), out _));
    }

    [Fact]
    public void TournamentRule_RestrictsWhiteSecondStone()
    {
        var game = Play(new GameState(new GameOptions(TournamentRule: true)), (9, 9), (0, 0));
        Assert.False(game.IsLegal(new Point(9, 11), out _));   // distance 2
        Assert.False(game.IsLegal(new Point(11, 11), out _));  // distance 2 diagonal
        Assert.True(game.IsLegal(new Point(9, 12), out _));    // distance 3
        Assert.True(game.IsLegal(new Point(6, 6), out _));
    }

    [Fact]
    public void WithoutTournamentRule_WhiteSecondStoneMayBeNearCenter()
    {
        var game = Play(new GameState(), (9, 9), (0, 0));
        Assert.True(game.IsLegal(new Point(9, 10), out _));
    }

    [Fact]
    public void TournamentRule_DoesNotRestrictBlack()
    {
        var game = Play(new GameState(new GameOptions(TournamentRule: true)), (9, 9));
        Assert.True(game.IsLegal(new Point(9, 10), out _));
    }

    [Theory]
    [InlineData(0, 1)]  // horizontal
    [InlineData(1, 0)]  // vertical
    [InlineData(1, 1)]  // diagonal
    [InlineData(1, -1)] // anti-diagonal
    public void FiveInARow_Wins(int dr, int dc)
    {
        var game = new GameState();
        // White builds a line through the center; Black plays far away in a non-capturing scatter.
        var black = new[] { (0, 0), (0, 2), (0, 4), (0, 6) };
        for (int i = 0; i < 5; i++)
        {
            int step = i == 0 ? 0 : (i % 2 == 1 ? (i + 1) / 2 : -(i / 2));
            game.Play(new Point(9 + dr * step, 9 + dc * step));
            if (i < 4)
            {
                Assert.False(game.IsOver);
                game.Play(new Point(black[i].Item1, black[i].Item2));
            }
        }
        Assert.True(game.IsOver);
        Assert.Equal(Stone.White, game.Winner);
        Assert.Equal(WinReason.FiveInARow, game.WinReason);
        Assert.Equal(5, game.WinLine.Count);
    }

    [Fact]
    public void FourInARow_DoesNotWin()
    {
        var game = Play(new GameState(), (9, 9), (0, 0), (9, 10), (0, 2), (9, 11), (0, 4), (9, 12));
        Assert.False(game.IsOver);
    }

    [Fact]
    public void SixInARow_Wins()
    {
        // White: 9,7 9,8 9,10 9,11 9,12 then 9,9 fills the gap => six.
        var game = Play(new GameState(), (9, 9), (0, 0), (9, 7), (0, 2), (9, 8), (0, 4), (9, 11), (0, 6), (9, 12));
        // That's already 9,7..9,9 + 9,11..9,12 — fill 9,10 to make six.
        Assert.False(game.IsOver);
        game.Play(new Point(0, 8));
        game.Play(new Point(9, 10));
        Assert.True(game.IsOver);
        Assert.Equal(Stone.White, game.Winner);
        Assert.Equal(6, game.WinLine.Count);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, 1)]
    [InlineData(-1, -1)]
    [InlineData(1, -1)]
    [InlineData(-1, 1)]
    public void FlankingPair_IsCaptured_InAllDirections(int dr, int dc)
    {
        // White center, Black two stones next to it in direction, White closes.
        var game = Play(new GameState(),
            (9, 9),
            (9 + dr, 9 + dc),
            (0, 0),
            (9 + 2 * dr, 9 + 2 * dc));
        var record = game.Play(new Point(9 + 3 * dr, 9 + 3 * dc));

        Assert.Equal(2, record.Captured.Count);
        Assert.Equal(Stone.None, game.Board[new Point(9 + dr, 9 + dc)]);
        Assert.Equal(Stone.None, game.Board[new Point(9 + 2 * dr, 9 + 2 * dc)]);
        Assert.Equal(1, game.Captures(Stone.White));
        Assert.Equal(0, game.Captures(Stone.Black));
    }

    [Fact]
    public void SingleOrTripleStones_AreNotCaptured()
    {
        // Single: W at 9,9, B at 9,10, W at 9,11 -> no capture.
        var game = Play(new GameState(), (9, 9), (9, 10));
        var record = game.Play(new Point(9, 11));
        Assert.Empty(record.Captured);

        // Triple: W 9,9; B 10,9 11,9 12,9; W 13,9 -> no capture.
        game = Play(new GameState(), (9, 9), (10, 9), (0, 0), (11, 9), (0, 2), (12, 9));
        record = game.Play(new Point(13, 9));
        Assert.Empty(record.Captured);
        Assert.Equal(Stone.Black, game.Board[new Point(10, 9)]);
    }

    [Fact]
    public void MovingIntoFlankedPosition_IsSafe()
    {
        // W 9,9 and W 9,12; B 9,10; B then plays 9,11 between -> not captured.
        var game = Play(new GameState(), (9, 9), (9, 10), (9, 12));
        var record = game.Play(new Point(9, 11));
        Assert.Empty(record.Captured);
        Assert.Equal(Stone.Black, game.Board[new Point(9, 10)]);
        Assert.Equal(Stone.Black, game.Board[new Point(9, 11)]);
    }

    [Fact]
    public void OneMove_CanCaptureMultiplePairs()
    {
        // Target point 9,12 for White. Pairs: (9,10),(9,11) left of it with W at 9,9;
        // and (10,12),(11,12) below with W at 12,12.
        var game = Play(new GameState(),
            (9, 9), (9, 10),
            (12, 12), (9, 11),
            (0, 0), (10, 12),
            (0, 2), (11, 12));
        var record = game.Play(new Point(9, 12));
        Assert.Equal(4, record.Captured.Count);
        Assert.Equal(2, game.Captures(Stone.White));
    }

    [Fact]
    public void FiveCaptures_Wins()
    {
        var game = new GameState();
        // Each round: Black places a pair in a column, White flanks both ends.
        game.Play(Center);
        int[] cols = [1, 4, 7, 12, 15];
        for (int i = 0; i < cols.Length; i++)
        {
            int c = cols[i];
            game.Play(new Point(1, c));  // B
            game.Play(new Point(0, c));  // W top
            game.Play(new Point(2, c));  // B
            Assert.False(game.IsOver);
            game.Play(new Point(3, c));  // W bottom -> capture
            if (i < cols.Length - 1)
            {
                Assert.False(game.IsOver);
            }
        }
        Assert.True(game.IsOver);
        Assert.Equal(Stone.White, game.Winner);
        Assert.Equal(WinReason.Captures, game.WinReason);
        Assert.Equal(5, game.Captures(Stone.White));
    }

    [Fact]
    public void NoMovesAllowed_AfterGameOver()
    {
        var game = Play(new GameState(), (9, 9), (0, 0), (9, 10), (0, 2), (9, 11), (0, 4), (9, 12), (0, 6), (9, 13));
        Assert.True(game.IsOver);
        Assert.False(game.IsLegal(new Point(5, 5), out _));
    }

    [Fact]
    public void Undo_RestoresCapturedStonesAndCounts()
    {
        var game = Play(new GameState(), (9, 9), (9, 10), (0, 0), (9, 11));
        game.Play(new Point(9, 12));
        Assert.Equal(1, game.Captures(Stone.White));

        Assert.True(game.Undo());
        Assert.Equal(0, game.Captures(Stone.White));
        Assert.Equal(Stone.Black, game.Board[new Point(9, 10)]);
        Assert.Equal(Stone.Black, game.Board[new Point(9, 11)]);
        Assert.Equal(Stone.None, game.Board[new Point(9, 12)]);
        Assert.Equal(Stone.White, game.CurrentPlayer);
    }

    [Fact]
    public void Undo_ClearsWin()
    {
        var game = Play(new GameState(), (9, 9), (0, 0), (9, 10), (0, 2), (9, 11), (0, 4), (9, 12), (0, 6), (9, 13));
        Assert.True(game.Undo());
        Assert.False(game.IsOver);
        Assert.Equal(Stone.None, game.Winner);
        Assert.Empty(game.WinLine);
    }

    [Fact]
    public void Undo_OnEmptyGame_ReturnsFalse()
    {
        Assert.False(new GameState().Undo());
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var game = Play(new GameState(), (9, 9));
        var clone = game.Clone();
        clone.Play(new Point(9, 10));
        Assert.Equal(Stone.None, game.Board[new Point(9, 10)]);
        Assert.Equal(Stone.Black, game.CurrentPlayer);
        Assert.Equal(Stone.White, clone.CurrentPlayer);
    }
}
