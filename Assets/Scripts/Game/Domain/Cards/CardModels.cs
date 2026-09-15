namespace KLTN.Game.Domain
{
    public enum CardZone : byte
    {
        Deck = 0,
        DrawHand = 1,
        Reserve = 2,
        Board = 3,
        Graveyard = 4,
    }

    public enum MatchPhase : byte
    {
        Mulligan = 0,
        RoundStart = 1,
        Priority = 2,
        BlockDeclaration = 3,
        CombatResolution = 4,
        RoundEnd = 5,
        Finished = 6,
        AbilitySelection = 7,
    }

    public enum MatchOutcome : byte
    {
        Running,
        HostWon,
        GuestWon,
        Draw,
    }
}
