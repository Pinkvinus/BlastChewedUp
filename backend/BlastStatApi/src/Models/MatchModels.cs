namespace BlastStatApi.Models;

public record MatchData(
    string Map,
    TeamInfo TeamCT,
    TeamInfo TeamT,
    List<Round> Rounds,
    List<KillEvent> Kills,
    List<MatchEvent> Events,
    List<PlayerStats> PlayerStats
);

public record TeamInfo(string Name, string Side);

public record Round(
    int Number,
    string WinnerSide,
    string WinCondition,
    int ScoreCT,
    int ScoreT,
    DateTime StartTime,
    DateTime EndTime,
    double DurationSeconds,
    double MatchOffsetSeconds,
    List<KillEvent> Kills
);

public record KillEvent(
    DateTime Timestamp,
    string KillerName,
    string KillerTeam,
    string VictimName,
    string VictimTeam,
    string Weapon,
    bool Headshot,
    int RoundNumber,
    double MatchOffsetSeconds
);

/// <summary>
/// A unified timeline event — covers kills, util throws, bomb plants/defuses,
/// and round starts. The frontend uses EventType to decide how to render each entry.
/// </summary>
public record MatchEvent(
    double MatchOffsetSeconds,
    int RoundNumber,
    string EventType,       // "kill" | "util" | "bomb_plant" | "bomb_defuse" | "round_start"
    string PlayerName,
    string PlayerTeam,
    string? Detail,         // weapon / grenade type / bombsite / victim name
    bool Headshot
);

public record PlayerStats(
    string Name,
    string Team,
    int Kills,
    int Deaths,
    int Headshots,
    double HeadshotPercentage,
    Dictionary<string, int> WeaponKills,
    int RoundsPlayed
);