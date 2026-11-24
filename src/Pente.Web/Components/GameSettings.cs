using Pente.Core;
using Pente.Core.Ai;

namespace Pente.Web.Components;

public enum GameMode
{
    TwoPlayers,
    VersusComputer,
}

public sealed class GameSettings
{
    public GameMode Mode { get; set; } = GameMode.VersusComputer;

    /// <summary>The human's color when playing against the computer.</summary>
    public Stone HumanColor { get; set; } = Stone.White;

    public Difficulty Level { get; set; } = Difficulty.Medium;

    public bool TournamentRule { get; set; }

    public string WhiteName { get; set; } = "Player 1";
    public string BlackName { get; set; } = "Player 2";

    public Stone ComputerColor => Mode == GameMode.VersusComputer ? HumanColor.Opponent() : Stone.None;

    public string NameOf(Stone stone) => Mode switch
    {
        GameMode.VersusComputer => stone == HumanColor ? "You" : $"Computer ({Level})",
        _ => stone == Stone.White ? WhiteName : BlackName,
    };

    public GameSettings Copy() => (GameSettings)MemberwiseClone();
}
