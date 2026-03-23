namespace BlastStatApi.Models;

public record MatchData(
    string Map,
    TeamInfo TeamCT,
    TeamInfo TeamT,
    List<Round> Rounds,
    List<KillEvent> Kills,
    List<PlayerStats> PlayerStats
);

public record TeamInfo(string Name, string Side);

public record Round(
    int RoundNumber,
    string WinnerSide,
    string WinCondition,
    int ScoreCT,
    int ScoreT,
    DateTime StartTime,
    DateTime EndTime,
    double DurationSeconds,
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
    int RoundNumber
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