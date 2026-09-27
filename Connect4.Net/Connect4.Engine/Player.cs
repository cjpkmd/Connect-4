namespace Connect4.Engine;

public enum Player
{
    Red,
    Yellow,
}

public static class PlayerExtensions
{
    public static Player Opponent(this Player player) =>
        player == Player.Red ? Player.Yellow : Player.Red;
}
