using Pente.Core;
using Pente.Core.Ai;

namespace Pente.Core.Tests;

public class AiTests
{
    private static GameState Play(params (int r, int c)[] moves)
    {
        var game = new GameState();
        foreach (var (r, c) in moves)
        {
            game.Play(new Point(r, c));
        }
        return game;
    }

    public static TheoryData<Difficulty> Levels => new() { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task OpeningMove_IsCenter(Difficulty level)
    {
        var move = await new PenteAi(level, seed: 1).ChooseMoveAsync(new GameState());
        Assert.Equal(new Point(9, 9), move);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task TakesImmediateFiveInARow(Difficulty level)
    {
        // White has 9,9..9,12 open at 9,13 / 9,8; White to move.
        var game = Play((9, 9), (3, 3), (9, 10), (3, 5), (9, 11), (15, 3), (9, 12), (15, 15));
        var move = await new PenteAi(level, seed: 1).ChooseMoveAsync(game);
        Assert.Contains(move, new[] { new Point(9, 8), new Point(9, 13) });
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task BlocksOpponentFour(Difficulty level)
    {
        // White has 9,9..9,12 with 9,8 blocked by Black; Black to move must block 9,13.
        var game = Play((9, 9), (9, 8), (9, 10), (3, 3), (9, 11), (3, 6), (9, 12));
        var move = await new PenteAi(level, seed: 1).ChooseMoveAsync(game);
        Assert.Equal(new Point(9, 13), move);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task TakesWinningFifthCapture(Difficulty level)
    {
        var game = new GameState();
        game.Play(new Point(9, 9));
        int[] cols = [1, 4, 7, 12];
        foreach (var c in cols)
        {
            game.Play(new Point(1, c));
            game.Play(new Point(0, c));
            game.Play(new Point(2, c));
            game.Play(new Point(3, c));
        }
        // Black sets up a vulnerable pair at 16,9 17,9 with White at 15,9.
        game.Play(new Point(16, 9)); // B
        game.Play(new Point(15, 9)); // W
        game.Play(new Point(17, 9)); // B -> White to move, 18,9 captures and wins.
        Assert.Equal(4, game.Captures(Stone.White));

        var move = await new PenteAi(level, seed: 1).ChooseMoveAsync(game);
        Assert.Equal(new Point(18, 9), move);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public async Task ReturnsLegalMove_InMidGame(Difficulty level)
    {
        var game = Play((9, 9), (9, 10), (10, 10), (8, 8), (11, 11), (12, 12), (10, 9), (11, 9));
        var move = await new PenteAi(level, seed: 1).ChooseMoveAsync(game);
        Assert.True(game.IsLegal(move, out _));
    }

    [Fact]
    public async Task RespectsTournamentRule()
    {
        var game = new GameState(new GameOptions(TournamentRule: true));
        game.Play(new Point(9, 9));
        game.Play(new Point(9, 10));
        var move = await new PenteAi(Difficulty.Hard, seed: 1).ChooseMoveAsync(game);
        Assert.True(game.IsLegal(move, out _));
    }
}
