namespace Pente.Core.Ai;

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
}

public interface IPenteAi
{
    Difficulty Level { get; }

    /// <summary>Chooses a legal move for <see cref="GameState.CurrentPlayer"/>. The given state is not modified.</summary>
    Task<Point> ChooseMoveAsync(GameState state, CancellationToken cancellationToken = default);
}
