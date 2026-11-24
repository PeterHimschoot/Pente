namespace Pente.Core;

public enum Stone : byte
{
    None = 0,
    White = 1,
    Black = 2,
}

public static class StoneExtensions
{
    public static Stone Opponent(this Stone stone) => stone switch
    {
        Stone.White => Stone.Black,
        Stone.Black => Stone.White,
        _ => Stone.None,
    };
}
