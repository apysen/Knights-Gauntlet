namespace KnightsGauntlet.Core
{
    // The JS used string literals ('rook', 'trap', 'battle', 'hard' ...).
    // C# enums give us compile-time safety and switch-expression support.

    public enum PieceType
    {
        Rook,
        Bishop,
        Knight,
        Queen,
        Trap,
        Nightrider // the knight-mimic transform form
    }

    public enum GameMode
    {
        Trap,
        Battle
    }

    public enum Difficulty
    {
        Easy,
        Medium,
        Hard,
        Grandmaster
    }
}
