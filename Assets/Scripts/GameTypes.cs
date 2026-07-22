namespace AmongUsClone
{
    public enum PlayerRole
    {
        Unassigned,
        Crewmate,
        Impostor,
        Spectator
    }

    public enum MatchState
    {
        Lobby,
        Playing,
        Meeting,
        Ended
    }

    public enum WinningTeam
    {
        None,
        Crewmates,
        Impostors
    }

    public enum SabotageType
    {
        None,
        Lights,
        Reactor,
        Communications,
        Oxygen
    }

    public enum InteractionKind
    {
        None,
        Task,
        Repair
    }

    public enum AnnouncementType
    {
        None,
        MeetingResult,
        RoundResult
    }
}
